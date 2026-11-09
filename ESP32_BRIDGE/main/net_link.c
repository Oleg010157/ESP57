/*
 * net_link.c
 *
 * Сеть моста. Устройство - в net_link.h, формат - в net_proto.h.
 *
 * Сокеты в ESP_INV (net_cmd.c, net_tele.c) уже прошли стенд, отсюда и
 * TCP_NODELAY, keepalive, ресинхронизация TCP-потока по метке и UDP с
 * постоянным портом-источником. Своё здесь - только то, что мост переносит
 * кадры CAN, а не собственный протокол.
 */
#include <errno.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "freertos/semphr.h"
#include "freertos/task.h"

#include "lwip/sockets.h"

#include "esp_log.h"

#include "net_link.h"
#include "net_proto.h"
#include "spi_link.h"

static const char *TAG = "net";

#define NET_CORE 0 /* WiFi живёт на ядре 0, обмен с прибором - на ядре 1 */

/* Одна датаграмма - две посылки SPI, 4 мс: до 20 кадров 103 и кадр 140, около
 * 870 байт. Влезает в один MTU с запасом: фрагментированная датаграмма
 * теряется целиком при потере любого фрагмента. И это 250 датаграмм в
 * секунду, а не 500 - WiFi на ESP_INV держал ровно столько. */
#define UDP_PACKETS 2
#define OUT_MAX     1200 /* предел содержимого одного элемента очереди */

typedef struct {
    uint8_t  udp; /* 1 - датаграмма телеметрии, 0 - кусок TCP-потока */
    uint16_t len;
    uint8_t  data[OUT_MAX];
} out_item_t;

static QueueHandle_t     s_out;
static SemaphoreHandle_t s_tcp_mux;
static int               s_client = -1;
static uint32_t          s_client_ip;
static int               s_udp = -1;

static portMUX_TYPE       s_peer_mux = portMUX_INITIALIZER_UNLOCKED;
static bool               s_have_peer;
static struct sockaddr_in s_peer;

static net_link_stat_t s_stat;

/* Сборка - только в задаче SPI, поэтому без блокировок. */
static out_item_t s_udp_acc; /* копится датаграмма */
static int        s_udp_packets;
static uint16_t   s_udp_seq;
static uint16_t   s_udp_drop;
static out_item_t s_tcp_acc; /* копится кусок TCP */

/*---------------------------------------------------------------------------
 * Сборка - задача SPI
 *---------------------------------------------------------------------------*/
static void put_rec(out_item_t *it, uint16_t id, const uint8_t *data, uint8_t len)
{
    uint8_t *p = it->data + it->len;
    p[0] = K57_REC_SYNC;
    p[1] = len;
    p[2] = (uint8_t)id;
    p[3] = (uint8_t)(id >> 8);
    memcpy(p + K57_REC_HDR, data, len);
    it->len += K57_REC_HDR + len;
}

static void push(out_item_t *it)
{
    if (it->len == 0) {
        return;
    }
    if (xQueueSend(s_out, it, 0) != pdTRUE) {
        s_stat.outDrop++;
    }
    it->len = 0;
}

static void udp_begin(void)
{
    s_udp_acc.udp = 1;
    memcpy(s_udp_acc.data, K57_UDP_MAGIC, 4);
    s_udp_acc.data[4] = (uint8_t)s_udp_seq;
    s_udp_acc.data[5] = (uint8_t)(s_udp_seq >> 8);
    s_udp_acc.len = K57_UDP_HDR; /* слова 6-7 - при отправке */
    s_udp_seq++;
}

void net_link_frame(uint16_t id, const uint8_t *data, uint8_t len)
{
    if (s_client < 0 || len > K57_DATA_MAX) {
        return; /* слушать некому - и собирать незачем */
    }

    if (id == 103 || id == 140) {
        if (!s_have_peer) {
            return;
        }
        if (s_udp_acc.len == 0) {
            udp_begin();
        }
        if (s_udp_acc.len + K57_REC_HDR + len > OUT_MAX) {
            return; /* не бывает: две посылки SPI - меньше 1000 байт */
        }
        put_rec(&s_udp_acc, id, data, len);
    } else {
        if (s_tcp_acc.len + K57_REC_HDR + len > OUT_MAX) {
            push(&s_tcp_acc);
        }
        s_tcp_acc.udp = 0;
        put_rec(&s_tcp_acc, id, data, len);
    }
}

void net_link_packet_end(uint16_t f28Drop)
{
    /* ACK - сразу, не дожидаясь второй посылки: пульт ждёт ответ. */
    push(&s_tcp_acc);

    s_udp_drop += f28Drop;
    if (s_udp_acc.len != 0 && ++s_udp_packets >= UDP_PACKETS) {
        s_udp_acc.data[6] = (uint8_t)s_udp_drop;
        s_udp_acc.data[7] = (uint8_t)(s_udp_drop >> 8);
        s_udp_drop    = 0;
        s_udp_packets = 0;
        push(&s_udp_acc);
    }
}

/*---------------------------------------------------------------------------
 * Отправка - своя задача
 *---------------------------------------------------------------------------*/
static bool send_all(int sock, const uint8_t *p, int n)
{
    while (n > 0) {
        int sent = send(sock, p, n, 0);
        if (sent <= 0) {
            return false;
        }
        p += sent;
        n -= sent;
    }
    return true;
}

static void out_task(void *arg)
{
    (void)arg;
    static out_item_t it; /* 1,2 кБ - не на стек задачи */

    for (;;) {
        xQueueReceive(s_out, &it, portMAX_DELAY);

        if (it.udp) {
            struct sockaddr_in to;
            bool ok;
            portENTER_CRITICAL(&s_peer_mux);
            ok = s_have_peer;
            to = s_peer;
            portEXIT_CRITICAL(&s_peer_mux);
            if (!ok || s_udp < 0) {
                continue;
            }
            /* Повторять бессмысленно: отказ - почти всегда ENOMEM, кончились
             * буферы lwIP, и через 4 мс данные всё равно устареют. */
            if (sendto(s_udp, it.data, it.len, 0, (struct sockaddr *)&to, sizeof(to)) < 0) {
                s_stat.udpErr++;
            } else {
                s_stat.udpOut++;
            }
        } else {
            xSemaphoreTake(s_tcp_mux, portMAX_DELAY);
            if (s_client >= 0 && send_all(s_client, it.data, it.len)) {
                /* число кадров - по меткам */
                for (int i = 0; i < it.len;) {
                    s_stat.tcpOut++;
                    i += K57_REC_HDR + it.data[i + 1];
                }
            }
            xSemaphoreGive(s_tcp_mux);
        }
    }
}

/*---------------------------------------------------------------------------
 * UDP: приём K57U - откуда ПК ждёт телеметрию
 *---------------------------------------------------------------------------*/
static void udp_task(void *arg)
{
    (void)arg;

    s_udp = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    struct sockaddr_in local = {
        .sin_family      = AF_INET,
        .sin_addr.s_addr = htonl(INADDR_ANY),
        .sin_port        = htons(K57_UDP_PORT),
    };
    if (s_udp < 0 || bind(s_udp, (struct sockaddr *)&local, sizeof(local)) < 0) {
        ESP_LOGE(TAG, "UDP %d занять не удалось, errno %d", K57_UDP_PORT, errno);
        vTaskDelete(NULL);
        return;
    }
    ESP_LOGI(TAG, "слушаю UDP %d", K57_UDP_PORT);

    for (;;) {
        uint8_t            buf[16];
        struct sockaddr_in from;
        socklen_t          flen = sizeof(from);
        int n = recvfrom(s_udp, buf, sizeof(buf), 0, (struct sockaddr *)&from, &flen);
        if (n < 4 || memcmp(buf, K57_UDP_HELLO, 4) != 0) {
            continue;
        }
        /* Только от того, кто подключён по TCP: телеметрию не льём на
         * адрес, который просто прислал четыре байта. */
        if (s_client < 0 || from.sin_addr.s_addr != s_client_ip) {
            continue;
        }

        portENTER_CRITICAL(&s_peer_mux);
        bool fresh  = !s_have_peer || s_peer.sin_port != from.sin_port;
        s_peer      = from;
        s_have_peer = true;
        portEXIT_CRITICAL(&s_peer_mux);
        s_stat.udpPeer = true;
        if (fresh) {
            ESP_LOGI(TAG, "телеметрия пойдёт на порт %u", ntohs(from.sin_port));
        }
    }
}

/*---------------------------------------------------------------------------
 * TCP: команды от ПК
 *---------------------------------------------------------------------------*/

/* TCP - поток байт: кадр может приехать двумя пакетами, а два - одним.
 * Копим и разбираем; потеряли синхронизацию - ищем метку побайтно. */
static int process(uint8_t *buf, int have)
{
    int pos = 0;

    while (have - pos >= K57_REC_HDR) {
        if (buf[pos] != K57_REC_SYNC || buf[pos + 1] > K57_DATA_MAX) {
            pos++;
            continue;
        }
        int len = buf[pos + 1];
        if (have - pos < K57_REC_HDR + len) {
            break; /* ждём остаток */
        }

        spi_link_frame_t f = {
            .id  = (uint16_t)(buf[pos + 2] | (buf[pos + 3] << 8)),
            .len = (uint8_t)len,
        };
        /* К прибору идут только команды - 8 байт, ID 2. Остальное прибору
         * не адресовано, а длиннее 8 в очередь не влезет. */
        if (f.id == 2 && len == 8) {
            memcpy(f.data, buf + pos + K57_REC_HDR, 8);
            spi_link_send(&f);
            s_stat.cmdIn++;
        }
        pos += K57_REC_HDR + len;
    }

    if (pos > 0 && pos < have) {
        memmove(buf, buf + pos, have - pos);
    }
    return have - pos;
}

static void serve(int sock)
{
    uint8_t buf[512];
    int     have = 0;

    for (;;) {
        int n = recv(sock, buf + have, sizeof(buf) - have, 0);
        if (n == 0) {
            ESP_LOGI(TAG, "клиент отключился");
            return;
        }
        if (n < 0) {
            ESP_LOGW(TAG, "приём оборвался, errno %d", errno);
            return;
        }
        have = process(buf, have + n);
        if (have == (int)sizeof(buf)) {
            ESP_LOGW(TAG, "мусор в потоке, буфер сброшен");
            have = 0;
        }
    }
}

static void tcp_task(void *arg)
{
    (void)arg;
    int yes = 1;

    int listener = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    setsockopt(listener, SOL_SOCKET, SO_REUSEADDR, &yes, sizeof(yes));
    struct sockaddr_in addr = {
        .sin_family      = AF_INET,
        .sin_addr.s_addr = htonl(INADDR_ANY),
        .sin_port        = htons(K57_TCP_PORT),
    };
    if (listener < 0 || bind(listener, (struct sockaddr *)&addr, sizeof(addr)) < 0 ||
        listen(listener, 1) < 0) {
        ESP_LOGE(TAG, "TCP %d занять не удалось, errno %d", K57_TCP_PORT, errno);
        vTaskDelete(NULL);
        return;
    }
    ESP_LOGI(TAG, "слушаю TCP %d", K57_TCP_PORT);

    for (;;) {
        struct sockaddr_in peer;
        socklen_t          plen = sizeof(peer);
        int sock = accept(listener, (struct sockaddr *)&peer, &plen);
        if (sock < 0) {
            vTaskDelay(pdMS_TO_TICKS(100));
            continue;
        }

        /* Без TCP_NODELAY Нейгл придержит короткий ACK до 40 мс. */
        setsockopt(sock, IPPROTO_TCP, TCP_NODELAY, &yes, sizeof(yes));
        /* Разрыв WiFi без FIN оставил бы сокет висеть навсегда. */
        int idle = 5, intvl = 3, cnt = 3;
        setsockopt(sock, SOL_SOCKET, SO_KEEPALIVE, &yes, sizeof(yes));
        setsockopt(sock, IPPROTO_TCP, TCP_KEEPIDLE, &idle, sizeof(idle));
        setsockopt(sock, IPPROTO_TCP, TCP_KEEPINTVL, &intvl, sizeof(intvl));
        setsockopt(sock, IPPROTO_TCP, TCP_KEEPCNT, &cnt, sizeof(cnt));

        char ip[16];
        inet_ntoa_r(peer.sin_addr, ip, sizeof(ip));
        ESP_LOGI(TAG, "подключился %s", ip);

        s_client_ip = peer.sin_addr.s_addr;
        s_client    = sock;
        s_stat.clients++;
        s_stat.connected = true;

        serve(sock);

        /* Сначала снять указатель, потом закрыть: иначе задача отправки
         * успеет записать в дескриптор, который система уже отдала под новое
         * соединение. */
        xSemaphoreTake(s_tcp_mux, portMAX_DELAY);
        s_client = -1;
        xSemaphoreGive(s_tcp_mux);
        portENTER_CRITICAL(&s_peer_mux);
        s_have_peer = false;
        portEXIT_CRITICAL(&s_peer_mux);
        s_stat.connected = false;
        s_stat.udpPeer   = false;

        close(sock);
    }
}

void net_link_start(void)
{
    s_out     = xQueueCreate(6, sizeof(out_item_t));
    s_tcp_mux = xSemaphoreCreateMutex();
    configASSERT(s_out != NULL && s_tcp_mux != NULL);

    xTaskCreatePinnedToCore(out_task, "net_out", 3072, NULL, 6, NULL, NET_CORE);
    xTaskCreatePinnedToCore(udp_task, "net_udp", 3072, NULL, 5, NULL, NET_CORE);
    xTaskCreatePinnedToCore(tcp_task, "net_tcp", 4096, NULL, 5, NULL, NET_CORE);
}

void net_link_get_stat(net_link_stat_t *out)
{
    *out = s_stat;
}

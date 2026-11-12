/*
 * main.c
 *
 * Мост SPI <-> WiFi для прибора K57. Кадры прибора уходят на ПК по сети
 * (net_link.c), команды с ПК - прибору. Раз в секунду в консоль (COM-порт
 * платы, 115200) печатается, сколько кадров каждого номера пришло, что ушло
 * в сеть, и ответы ACK.
 *
 * Обмен с прибором поднимается ПЕРВЫМ и от сети не зависит: пока WiFi
 * подключается или его нет вовсе, прибор видит мост готовым и не копит кадры.
 *
 * Команду прибору можно дать и из консоли, без сети:
 *
 *   c <cmd> [d1] [d2] [d3]      например: c 333 - прибор пришлёт своё инфо
 *
 * Числа десятичные, отрицательные можно. Это та же команда, что шлёт
 * Motor_Control по CAN, - ID 2, cmd и три слова данных.
 *
 *   b                           пуск чёрного ящика вручную (bbox.h)
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/uart.h"
#include "driver/uart_vfs.h"
#include "esp_heap_caps.h"
#include "esp_log.h"

#include "bbox.h"
#include "bstore.h"
#include "web.h"
#include "live.h"
#include "board.h"
#include "net_link.h"
#include "net_proto.h"
#include "net_wifi.h"
#include "spi_link.h"

static const char *TAG = "bridge";

#define D(f) ((unsigned long)(s.f - p.f))

/* Строка из консоли. Без драйвера UART stdin неблокирующий, и fgets вернул
 * бы пустоту сразу; с драйвером задача честно спит до Enter. */
static void console_task(void *arg)
{
    (void)arg;
    char line[64];

    for (;;) {
        if (fgets(line, sizeof line, stdin) == NULL) {
            vTaskDelay(pdMS_TO_TICKS(50));
            continue;
        }
        if (line[0] == '\n' || line[0] == '\r' || line[0] == '\0') {
            continue; /* хвост CR LF от терминала */
        }
        if (line[0] == 'b') {
            bbox_manual();
            printf("чёрный ящик: пуск вручную\n");
            continue;
        }
        if (line[0] != 'c') {
            printf("команда прибору: c <cmd> [d1] [d2] [d3]; пуск чёрного ящика: b\n");
            continue;
        }

        char *q = line + 1;
        long v[4] = {0};
        int  n = 0;
        while (n < 4) {
            char *end;
            long x = strtol(q, &end, 10);
            if (end == q) {
                break;
            }
            v[n++] = x;
            q = end;
        }
        if (n == 0) {
            printf("нет номера команды\n");
            continue;
        }

        bool ok = spi_link_send_cmd((uint16_t)v[0], (uint16_t)v[1], (uint16_t)v[2], (uint16_t)v[3]);
        printf("-> %ld (%ld, %ld, %ld)%s\n", v[0], v[1], v[2], v[3], ok ? "" : "  ОЧЕРЕДЬ ПОЛНА");
    }
}

static void console_start(void)
{
    ESP_ERROR_CHECK(uart_driver_install(CONFIG_ESP_CONSOLE_UART_NUM, 256, 0, 0, NULL, 0));
    uart_vfs_dev_use_driver(CONFIG_ESP_CONSOLE_UART_NUM);
    /* idf.py monitor шлёт по Enter CR, терминалы бывают и с CR LF. */
    uart_vfs_dev_port_set_rx_line_endings(CONFIG_ESP_CONSOLE_UART_NUM, ESP_LINE_ENDINGS_CR);
    setvbuf(stdin, NULL, _IONBF, 0);
    xTaskCreate(console_task, "console", 3072, NULL, 3, NULL);
}

void app_main(void)
{
    ESP_LOGI(TAG, "мост K57, плата %s", BOARD_NAME);
    spi_link_start();
    console_start();

    net_wifi_start(); /* ждёт: сеть или своя точка доступа */
    net_link_start();
    ESP_LOGI(TAG, "сеть: %s %s, TCP %d, UDP %d, имя %s.local",
             net_wifi_is_sta() ? "подключён, адрес" : "точка доступа, адрес",
             net_wifi_ip(), K57_TCP_PORT, K57_UDP_PORT, K57_MDNS_HOST);

    /* HTTP: версия и обновление прошивки моста по WiFi. Тоже до чёрного
     * ящика - задача сервера и его сокеты берут кучу. */
    web_start();
    ESP_LOGI(TAG, "прошивка %s", web_version());

    /* Журнал и копия настройки: NVS поднял net_wifi_start. До чёрного ящика
     * - свою память берёт раньше его кольца. */
    bstore_start();

    /* После WiFi, а не до: кольцо берёт ту кучу, что осталась, а WiFi при
     * запуске занимает своё. Наоборот - WiFi мог бы и не подняться. */
    bbox_start();

    spi_link_stat_t p = {0};
    net_link_stat_t np = {0};
    for (;;) {
        vTaskDelay(pdMS_TO_TICKS(1000));

        /* Новая прошивка, дожившая сюда 20 раз, - годная (web.h). */
        web_confirm_if_pending();

        /* Страница в браузере открыта, пульта нет - «только смотреть» (live.h). */
        live_tick1s();

        spi_link_stat_t s;
        spi_link_get_stat(&s);
        ESP_LOGI(TAG, "за 1 с: посылок %lu | 103: %lu, 7E1: %lu, 140: %lu, 1: %lu, 132: %lu, "
                      "прочих %lu | заполнение %u, макс %u байт | к прибору %lu",
                 D(packets), D(fr103), D(frBbox), D(fr140), D(fr1), D(fr132), D(frOther),
                 s.lastFill, s.maxFill, D(cmdOut));
        ESP_LOGI(TAG, "    всего: разрывов 103 %lu, пропусков посылок %lu, "
                      "не влезло на приборе %lu, обрыв %lu, без метки %lu, битых %lu, "
                      "очередь к прибору полна %lu, перезапусков SPI %lu",
                 (unsigned long)s.tickGap, (unsigned long)s.seqGap,
                 (unsigned long)s.f28Drop, (unsigned long)s.truncated,
                 (unsigned long)s.badMagic, (unsigned long)s.badLayout,
                 (unsigned long)s.cmdDrop, (unsigned long)s.slaveResets);

        net_link_stat_t n;
        net_link_get_stat(&n);
        ESP_LOGI(TAG, "    сеть: %s%s | за 1 с: UDP %lu, TCP %lu, команд с ПК %lu | "
                      "всего: повторов UDP %lu, отказов UDP %lu, очередь в сеть полна %lu | сигнал %ld дБм",
                 n.connected ? "клиент есть" : "клиента нет",
                 n.connected ? (n.udpPeer ? ", телеметрия идёт" : ", ждём K57U") : "",
                 (unsigned long)(n.udpOut - np.udpOut), (unsigned long)(n.tcpOut - np.tcpOut),
                 (unsigned long)(n.cmdIn - np.cmdIn), (unsigned long)n.udpRetry, (unsigned long)n.udpErr,
                 (unsigned long)n.outDrop, (long)net_wifi_rssi());
        np = n;

        bbox_stat_t b;
        bbox_get_stat(&b);
        if (!b.noFlash) {
            static const char *const st[] = {"пишет", "дописывает хвост", "сохраняет"};
            ESP_LOGI(TAG, "    чёрный ящик: %s | кольцо %lu кБ, кадров %lu (%.2f с) | "
                          "пусков %lu, записей %lu, пропущено %lu | сохранение %lu мс",
                     st[b.state < 3 ? b.state : 0], (unsigned long)(b.ringBytes / 1024),
                     (unsigned long)b.ringFrames, b.ringFrames / 5000.0,
                     (unsigned long)b.triggers, (unsigned long)b.saved,
                     (unsigned long)b.missed, (unsigned long)b.saveMs);
        }
        bstore_stat_t j;
        bstore_get_stat(&j);
        ESP_LOGI(TAG, "    журнал: строк %lu, событий %lu, свёрнуто повторов %lu, потеряно %lu | "
                      "копия настройки: %s, принято %lu, битых %lu",
                 (unsigned long)j.rows, (unsigned long)j.events, (unsigned long)j.folded,
                 (unsigned long)j.lost, j.haveCopy ? "есть" : "нет",
                 (unsigned long)j.copies, (unsigned long)j.copyBad);
        /* Куча: сколько свободно, сколько было в самый тесный момент и самый
         * большой кусок. По «минимуму» видно, сколько WiFi берёт под нагрузкой,
         * - от этого зависит, сколько можно отдать кольцу (bbox.c). */
        ESP_LOGI(TAG, "    куча: свободно %u кБ, минимум %u кБ, кусок %u кБ, IRAM %u кБ",
                 (unsigned)(heap_caps_get_free_size(MALLOC_CAP_8BIT) / 1024),
                 (unsigned)(heap_caps_get_minimum_free_size(MALLOC_CAP_8BIT) / 1024),
                 (unsigned)(heap_caps_get_largest_free_block(MALLOC_CAP_8BIT) / 1024),
                 (unsigned)((heap_caps_get_free_size(MALLOC_CAP_32BIT) -
                             heap_caps_get_free_size(MALLOC_CAP_8BIT)) / 1024));

        /* dbg Испорченная посылка: заголовок и 16 слов вокруг места поломки. */
        const uint8_t *bad;
        int bw;
        if (spi_link_get_bad(&bad, &bw)) {
            int from = bw > 8 ? bw - 8 : 0;
            ESP_LOGW(TAG, "    ИСПОРЧЕННАЯ посылка, разбор сломался на слове %d, заголовок:", bw);
            ESP_LOG_BUFFER_HEX_LEVEL(TAG, bad, 8, ESP_LOG_WARN);
            ESP_LOGW(TAG, "    слова %d..%d:", from, from + 15);
            ESP_LOG_BUFFER_HEX_LEVEL(TAG, bad + 2 * from, 32, ESP_LOG_WARN);
            ESP_LOGW(TAG, "    кадры с начала (слова 4..35):");
            ESP_LOG_BUFFER_HEX_LEVEL(TAG, bad + 8, 64, ESP_LOG_WARN);
        }

        /* ACK печатаются не все: на залпе от ползунка их сотни в секунду. */
        uint16_t a[4];
        int shown = 0;
        while (spi_link_get_ack(a)) {
            if (shown++ < 12) {
                ESP_LOGI(TAG, "    <- ACK %u: %d %d %d", a[0], (int16_t)a[1], (int16_t)a[2], (int16_t)a[3]);
            }
        }
        p = s;
    }
}

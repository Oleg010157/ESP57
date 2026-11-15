/*
 *  net_wifi.c
 *
 *  Своя точка доступа K57 - всегда, и вдобавок роутер, если он задан
 *  (13.11.2026). ESP32 держит обе сети сразу (WIFI_MODE_APSTA): на стенде
 *  телефон и ноутбук подключаются к K57 напрямую, как раньше, а через роутер
 *  мост виден из дома или с работы.
 *
 *  До 13.11.2026 было «или - или», и выбиралось при сборке: в общей сети своей
 *  точки не было, а сеть с паролем жила в sdkconfig.local - сменить её можно
 *  было только пересборкой. Теперь роутеры задаются со страницы (вкладка «Мост»)
 *  и хранятся в NVS моста: ни в прошивку, ни в git они не попадают.
 *
 *  РОУТЕРОВ ДО ЧЕТЫРЁХ (15.11.2026): дом, работа... Пароль каждого вводится
 *  один раз, а мост, включившись, сам входит в тот, что рядом, - сильнейший по
 *  сигналу из запомненных. Один запомнен - сразу к нему, без поиска.
 *
 *  Запасной режим больше не нужен: K57 есть всегда, и до моста дойти можно, что
 *  бы ни случилось с роутером. Поэтому и пробовать роутер можно бесконечно - но
 *  не подряд: пока мост ищет сеть, радио обходит каналы, и тем, кто на K57,
 *  на эту секунду плохо. Паузы растут: 5 с, 30 с, потом раз в 5 минут.
 *
 *  Своя точка встаёт на канал роутера, когда тот найден, - так устроен ESP32:
 *  радио одно. Подключённые к K57 в этот момент на секунду отпадают.
 */

#include <stdio.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#include "esp_event.h"
#include "esp_log.h"
#include "esp_mac.h"
#include "esp_netif.h"
#include "esp_timer.h"
#include "esp_wifi.h"
#include "nvs.h"
#include "nvs_flash.h"

#include "mdns.h"

#include "net_proto.h"
#include "net_wifi.h"

static const char *TAG = "wifi";

#define NVS_NS "k57net" /* n, s0..s3, p0..p3, c0..c3 */

typedef struct {
    char    ssid[33];
    char    pass[65];
    uint8_t chan; /* канал в прошлый раз: подключаться без поиска */
} router_t;

static esp_netif_t       *s_ap_if;
static esp_timer_handle_t s_retry_t;
static TaskHandle_t       s_sel_task;
static portMUX_TYPE       s_mux = portMUX_INITIALIZER_UNLOCKED;

static router_t      s_r[NET_ROUTERS_MAX]; /* [0] - последний заданный */
static int           s_rn;
static volatile int  s_cur = -1;           /* к какому подключаемся или подключены */
static volatile int  s_want = -1;          /* со страницы: подключиться именно к этому */
static volatile int  s_state;              /* NET_ROUTER_* */
static volatile int  s_reason;             /* причина последнего разрыва (wifi_err_reason_t) */
static volatile int  s_clients;            /* подключено к K57 */
static int           s_fails;
static volatile bool s_quiet;              /* разрыв устроили сами - переподключаться не надо */
static char          s_sta_ip[16];
static char          s_ap_ip[16] = "192.168.4.1";
static char          s_mac[18];

/*---------------------------------------------------------------------------
 * Роутеры: список в NVS
 *---------------------------------------------------------------------------*/
static void save_routers(void)
{
    nvs_handle_t h;
    char         k[4];

    if (nvs_open(NVS_NS, NVS_READWRITE, &h) != ESP_OK) {
        return;
    }
    nvs_erase_all(h);
    nvs_set_u8(h, "n", (uint8_t)s_rn);
    for (int i = 0; i < s_rn; i++) {
        snprintf(k, sizeof k, "s%d", i);
        nvs_set_str(h, k, s_r[i].ssid);
        snprintf(k, sizeof k, "p%d", i);
        nvs_set_str(h, k, s_r[i].pass);
        snprintf(k, sizeof k, "c%d", i);
        nvs_set_u8(h, k, s_r[i].chan);
    }
    nvs_commit(h);
    nvs_close(h);
}

static void load_routers(void)
{
    nvs_handle_t h;
    size_t       n;
    char         k[4];
    uint8_t      cnt = 0;

    s_rn = 0;
    if (nvs_open(NVS_NS, NVS_READONLY, &h) == ESP_OK) {
        if (nvs_get_u8(h, "n", &cnt) == ESP_OK) {
            for (int i = 0; i < cnt && i < NET_ROUTERS_MAX; i++) {
                router_t *r = &s_r[s_rn];
                memset(r, 0, sizeof *r);
                snprintf(k, sizeof k, "s%d", i);
                n = sizeof r->ssid;
                if (nvs_get_str(h, k, r->ssid, &n) != ESP_OK || r->ssid[0] == 0) {
                    continue;
                }
                snprintf(k, sizeof k, "p%d", i);
                n = sizeof r->pass;
                nvs_get_str(h, k, r->pass, &n);
                snprintf(k, sizeof k, "c%d", i);
                nvs_get_u8(h, k, &r->chan);
                s_rn++;
            }
        } else {
            /* Запись 13.11.2026 - один роутер: ssid, pass, chan. Перенести. */
            router_t *r = &s_r[0];
            memset(r, 0, sizeof *r);
            n = sizeof r->ssid;
            if (nvs_get_str(h, "ssid", r->ssid, &n) == ESP_OK && r->ssid[0] != 0) {
                n = sizeof r->pass;
                nvs_get_str(h, "pass", r->pass, &n);
                nvs_get_u8(h, "chan", &r->chan);
                s_rn = 1;
            }
        }
        nvs_close(h);
        if (cnt == 0 && s_rn == 1) {
            save_routers();
        }
    }
#if !CONFIG_K57_AP_ONLY
    /* Со страницы ещё ничего не задавали - то, что было в sdkconfig.local. */
    if (s_rn == 0 && CONFIG_K57_WIFI_SSID[0] != 0) {
        strlcpy(s_r[0].ssid, CONFIG_K57_WIFI_SSID, sizeof s_r[0].ssid);
        strlcpy(s_r[0].pass, CONFIG_K57_WIFI_PASS, sizeof s_r[0].pass);
        s_r[0].chan = 0;
        s_rn        = 1;
    }
#endif
}

static int find_router(const char *ssid)
{
    for (int i = 0; i < s_rn; i++) {
        if (strcmp(s_r[i].ssid, ssid) == 0) {
            return i;
        }
    }
    return -1;
}

/*---------------------------------------------------------------------------
 * Подключение
 *---------------------------------------------------------------------------*/
static void connect_to(int i, bool useChan)
{
    wifi_config_t wc = {0};

    s_cur = i;
    strlcpy((char *)wc.sta.ssid, s_r[i].ssid, sizeof wc.sta.ssid);
    strlcpy((char *)wc.sta.password, s_r[i].pass, sizeof wc.sta.password);
    /* Знаем канал - сразу на него, без обхода всех: обход мешает K57. */
    wc.sta.channel     = useChan ? s_r[i].chan : 0;
    wc.sta.scan_method = WIFI_FAST_SCAN;
    esp_wifi_set_config(WIFI_IF_STA, &wc);
    s_state = NET_ROUTER_CONNECTING;
    ESP_LOGI(TAG, "роутер %s: подключаюсь", s_r[i].ssid);
    esp_wifi_connect();
}

/* Какой из запомненных роутеров рядом: поиск по каналам, сильнейший из
 * найденных. -1 - ни одного. Блокирует на 2-3 с - только из своей задачи. */
static int pick_router(void)
{
    static wifi_ap_record_t rec[16];
    wifi_scan_config_t      sc = {.show_hidden = false};
    uint16_t                n  = 16;
    int                     best = -1, bestRssi = -127;

    if (esp_wifi_scan_start(&sc, true) != ESP_OK || esp_wifi_scan_get_ap_records(&n, rec) != ESP_OK) {
        return -1;
    }
    for (int k = 0; k < n; k++) {
        int i = find_router((const char *)rec[k].ssid);
        if (i >= 0 && rec[k].rssi > bestRssi) {
            best     = i;
            bestRssi = rec[k].rssi;
            s_r[i].chan = rec[k].primary;
        }
    }
    return best;
}

/* Задача выбора: будят её старт WiFi, таймер повтора и страница. Поиск
 * блокирует, поэтому не в цикле событий и не в таймере. */
static void sel_task(void *arg)
{
    (void)arg;
    for (;;) {
        ulTaskNotifyTake(pdTRUE, portMAX_DELAY);
        if (s_state == NET_ROUTER_UP || s_rn == 0) {
            if (s_rn == 0) {
                s_state = NET_ROUTER_NONE;
            }
            continue;
        }
        int want = s_want;
        s_want   = -1;
        if (want >= 0 && want < s_rn) {
            connect_to(want, false); /* со страницы - к нему, искать незачем */
        } else if (s_rn == 1) {
            connect_to(0, s_fails < 2); /* две неудачи - роутер, может быть, сменил канал */
        } else {
            int i = pick_router();
            if (i >= 0) {
                connect_to(i, true);
            } else {
                s_state  = NET_ROUTER_DOWN;
                s_reason = WIFI_REASON_NO_AP_FOUND;
                s_fails++;
                int s = s_fails < 3 ? 5 : s_fails < 10 ? 30 : 300;
                esp_timer_stop(s_retry_t);
                esp_timer_start_once(s_retry_t, (uint64_t)s * 1000000);
            }
        }
    }
}

static void kick(void)
{
    if (s_sel_task != NULL) {
        xTaskNotifyGive(s_sel_task);
    }
}

static void retry_cb(void *arg)
{
    (void)arg;
    if (s_rn != 0 && s_state != NET_ROUTER_UP) {
        kick();
    }
}

static void schedule_retry(void)
{
    int s = s_fails < 3 ? 5 : s_fails < 10 ? 30 : 300;
    s_fails++;
    esp_timer_stop(s_retry_t);
    esp_timer_start_once(s_retry_t, (uint64_t)s * 1000000);
}

/*---------------------------------------------------------------------------
 * События. Живут в задаче цикла событий, не в прерывании.
 *---------------------------------------------------------------------------*/
static void on_wifi(void *arg, esp_event_base_t base, int32_t id, void *data)
{
    (void)arg;
    (void)base;

    switch (id) {
    case WIFI_EVENT_STA_START:
        kick();
        break;

    case WIFI_EVENT_STA_CONNECTED: {
        const wifi_event_sta_connected_t *e = data;
        int                               c = s_cur;
        if (c >= 0 && c < s_rn && e->channel != s_r[c].chan) {
            s_r[c].chan = e->channel;
            save_routers();
        }
        break;
    }

    case WIFI_EVENT_STA_DISCONNECTED: {
        const wifi_event_sta_disconnected_t *e = data;
        portENTER_CRITICAL(&s_mux);
        s_sta_ip[0] = 0;
        portEXIT_CRITICAL(&s_mux);
        s_reason = e->reason;
        if (s_quiet) {
            s_quiet = false;
            break;
        }
        if (s_rn == 0) {
            s_state = NET_ROUTER_NONE;
            break;
        }
        if (s_state == NET_ROUTER_UP) {
            ESP_LOGW(TAG, "роутер: связь потеряна (причина %d)", e->reason);
            s_fails = 0;
        }
        s_state = NET_ROUTER_DOWN;
        schedule_retry();
        break;
    }

    case WIFI_EVENT_AP_STACONNECTED: {
        const wifi_event_ap_staconnected_t *e = data;
        s_clients++;
        ESP_LOGI(TAG, "к K57 подключились " MACSTR, MAC2STR(e->mac));
        break;
    }

    case WIFI_EVENT_AP_STADISCONNECTED:
        if (s_clients > 0) {
            s_clients--;
        }
        break;

    default:
        break;
    }
}

static void on_got_ip(void *arg, esp_event_base_t base, int32_t id, void *data)
{
    (void)arg;
    (void)base;
    (void)id;

    const ip_event_got_ip_t *e = data;
    portENTER_CRITICAL(&s_mux);
    snprintf(s_sta_ip, sizeof s_sta_ip, IPSTR, IP2STR(&e->ip_info.ip));
    portEXIT_CRITICAL(&s_mux);
    s_fails = 0;
    s_state = NET_ROUTER_UP;
    int c = s_cur;
    ESP_LOGI(TAG, "роутер %s: адрес %s, сигнал %ld дБм", c >= 0 ? s_r[c].ssid : "?", s_sta_ip,
             (long)net_wifi_rssi());
}

/*---------------------------------------------------------------------------
 * Своя точка доступа
 *---------------------------------------------------------------------------*/
static void config_ap(void)
{
    wifi_config_t wc = {
        .ap = {
            .ssid_len       = strlen(CONFIG_K57_AP_SSID),
            .channel        = 1, /* пока нет роутера; с ним - его канал */
            /* Компьютер и пара телефонов или планшетов. По TCP мост всё
             * равно держит одного клиента, остальные увидят точку и смогут
             * подключиться, когда он отключится. */
            .max_connection = 4,
            .authmode       = WIFI_AUTH_WPA2_PSK,
        },
    };
    strlcpy((char *)wc.ap.ssid, CONFIG_K57_AP_SSID, sizeof(wc.ap.ssid));
    strlcpy((char *)wc.ap.password, CONFIG_K57_AP_PASS, sizeof(wc.ap.password));

    /* WPA2 требует минимум 8 символов. Короткий пароль молча превратил бы
     * точку в открытую - лучше сказать об этом вслух. */
    if (strlen(CONFIG_K57_AP_PASS) < 8) {
        ESP_LOGW(TAG, "пароль точки короче 8 символов, точка будет открытой");
        wc.ap.authmode = WIFI_AUTH_OPEN;
    }
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_AP, &wc));
}

/*---------------------------------------------------------------------------
 * mDNS
 *
 * Имя k57.local. Отвечает и в роутере, и в K57 (CONFIG_MDNS_PREDEF_NETIF_STA /
 * _AP). Windows такие имена понимает не всегда (стенд, 15.11.2026), поэтому
 * Motor_Control ищет мост ещё и своим вопросом по UDP (net_link.c, K57_DISC_PORT).
 *---------------------------------------------------------------------------*/
static void start_mdns(void)
{
    esp_err_t err = mdns_init();
    if (err != ESP_OK) {
        ESP_LOGW(TAG, "mDNS не поднялся: %s", esp_err_to_name(err));
        return;
    }

    mdns_hostname_set(K57_MDNS_HOST);
    mdns_instance_name_set("K57 WiFi bridge");
    mdns_service_add(NULL, "_k57", "_tcp", K57_TCP_PORT, NULL, 0);

    ESP_LOGI(TAG, "mDNS: %s.local", K57_MDNS_HOST);
}

/*---------------------------------------------------------------------------
 * Точка входа
 *---------------------------------------------------------------------------*/
esp_err_t net_wifi_start(void)
{
    /* WiFi хранит калибровки и настройки в NVS. Если раздел испорчен или
     * формат сменился после обновления IDF, стирание - единственный способ
     * стартовать; терять там, кроме роутеров, нечего. */
    esp_err_t err = nvs_flash_init();
    if (err == ESP_ERR_NVS_NO_FREE_PAGES || err == ESP_ERR_NVS_NEW_VERSION_FOUND) {
        ESP_ERROR_CHECK(nvs_flash_erase());
        err = nvs_flash_init();
    }
    ESP_ERROR_CHECK(err);

    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());

    s_ap_if = esp_netif_create_default_wifi_ap();
    esp_netif_create_default_wifi_sta();

    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK(esp_wifi_init(&cfg));
    ESP_ERROR_CHECK(esp_event_handler_instance_register(WIFI_EVENT, ESP_EVENT_ANY_ID, on_wifi, NULL, NULL));
    ESP_ERROR_CHECK(esp_event_handler_instance_register(IP_EVENT, IP_EVENT_STA_GOT_IP, on_got_ip, NULL, NULL));

    const esp_timer_create_args_t ta = {.callback = retry_cb, .name = "wifi_retry"};
    ESP_ERROR_CHECK(esp_timer_create(&ta, &s_retry_t));

    load_routers();
    s_state = s_rn ? NET_ROUTER_CONNECTING : NET_ROUTER_NONE;
    xTaskCreatePinnedToCore(sel_task, "wifi_sel", 3072, NULL, 4, &s_sel_task, 0);

    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_APSTA));
    config_ap();
    ESP_ERROR_CHECK(esp_wifi_start());

    /* Энергосбережение выключаем ПОСЛЕ старта: до него настройка не
     * применяется. С энергосбережением модем засыпает между маячками, и
     * UDP-телеметрия приезжает рывками с задержкой в десятки миллисекунд -
     * на осциллографе это выглядит как дёрганый поток, хотя такт управления
     * идёт ровно. Диагностировать такое, не зная про WIFI_PS, тяжело. */
    ESP_ERROR_CHECK(esp_wifi_set_ps(WIFI_PS_NONE));

    esp_netif_ip_info_t ip;
    if (esp_netif_get_ip_info(s_ap_if, &ip) == ESP_OK) {
        snprintf(s_ap_ip, sizeof s_ap_ip, IPSTR, IP2STR(&ip.ip));
    }
    uint8_t mac[6];
    if (esp_wifi_get_mac(WIFI_IF_STA, mac) == ESP_OK) {
        snprintf(s_mac, sizeof s_mac, MACSTR, MAC2STR(mac));
    }
    ESP_LOGI(TAG, "своя точка %s, адрес %s; роутеров запомнено %d", CONFIG_K57_AP_SSID, s_ap_ip, s_rn);

    start_mdns();
    return ESP_OK;
}

/*---------------------------------------------------------------------------
 * Со страницы (web_page.c)
 *---------------------------------------------------------------------------*/
static void drop_current(void)
{
    esp_timer_stop(s_retry_t);
    if (s_state == NET_ROUTER_UP || s_state == NET_ROUTER_CONNECTING) {
        s_quiet = true; /* этот разрыв - наш */
        esp_wifi_disconnect();
    }
    s_state = NET_ROUTER_DOWN;
    s_fails = 0;
    s_reason = 0;
}

const char *net_wifi_router_add(const char *ssid, const char *pass)
{
    size_t ls = strlen(ssid), lp = strlen(pass);

    if (ls == 0 || ls > 32) {
        return ls ? "имя сети длиннее 32 байт" : "нет имени сети";
    }
    if (lp != 0 && (lp < 8 || lp > 63)) {
        return "пароль WPA2 - от 8 до 63 символов (или пусто для открытой сети)";
    }

    /* Заданный последним - первым в списке; пятый вытесняет самый старый. */
    router_t r = {0};
    strlcpy(r.ssid, ssid, sizeof r.ssid);
    strlcpy(r.pass, pass, sizeof r.pass);
    int i = find_router(ssid);
    if (i < 0) {
        i = s_rn < NET_ROUTERS_MAX ? s_rn++ : NET_ROUTERS_MAX - 1;
    }
    memmove(&s_r[1], &s_r[0], (size_t)i * sizeof s_r[0]);
    s_r[0] = r;
    save_routers();

    drop_current();
    s_want = 0;
    ESP_LOGI(TAG, "роутер %s: задан со страницы", ssid);
    kick();
    return NULL;
}

const char *net_wifi_router_forget(const char *ssid)
{
    int i = find_router(ssid);
    if (i < 0) {
        return "такой роутер не запомнен";
    }
    bool wasCur = i == s_cur;
    memmove(&s_r[i], &s_r[i + 1], (size_t)(s_rn - i - 1) * sizeof s_r[0]);
    s_rn--;
    save_routers();
    if (s_cur > i) {
        s_cur--;
    }
    if (wasCur) {
        drop_current();
        s_cur = -1;
        if (s_rn > 0) {
            kick();
        }
    }
    if (s_rn == 0) {
        s_state = NET_ROUTER_NONE;
    }
    ESP_LOGI(TAG, "роутер %s забыт", ssid);
    return NULL;
}

int net_wifi_routers(char out[][33], int max)
{
    int n = s_rn < max ? s_rn : max;
    for (int i = 0; i < n; i++) {
        strlcpy(out[i], s_r[i].ssid, 33);
    }
    return n;
}

int net_wifi_scan(net_wifi_ap_t *out, int max)
{
    /* Пока мост сам подключается к роутеру, искать нельзя - подождать. */
    if (s_state == NET_ROUTER_CONNECTING) {
        return -1;
    }
    wifi_scan_config_t sc = {.show_hidden = false};
    if (esp_wifi_scan_start(&sc, true) != ESP_OK) {
        return -1;
    }
    uint16_t                n = (uint16_t)max;
    static wifi_ap_record_t rec[16];
    if (n > 16) {
        n = 16;
    }
    if (esp_wifi_scan_get_ap_records(&n, rec) != ESP_OK) {
        return -1;
    }
    for (int i = 0; i < n; i++) {
        strlcpy(out[i].ssid, (const char *)rec[i].ssid, sizeof out[i].ssid);
        out[i].rssi = rec[i].rssi;
        out[i].ch   = rec[i].primary;
        out[i].open = rec[i].authmode == WIFI_AUTH_OPEN;
    }
    return n;
}

void net_wifi_status(net_wifi_status_t *s)
{
    int c = s_cur;
    strlcpy(s->apSsid, CONFIG_K57_AP_SSID, sizeof s->apSsid);
    strlcpy(s->apIp, s_ap_ip, sizeof s->apIp);
    s->apClients = s_clients;
    strlcpy(s->routerSsid, c >= 0 && c < s_rn ? s_r[c].ssid : "", sizeof s->routerSsid);
    s->state  = s_state;
    s->reason = s_reason;
    portENTER_CRITICAL(&s_mux);
    strlcpy(s->staIp, s_sta_ip, sizeof s->staIp);
    portEXIT_CRITICAL(&s_mux);
    s->rssi = net_wifi_rssi();
    s->ch   = c >= 0 && c < s_rn ? s_r[c].chan : 0;
    strlcpy(s->mac, s_mac, sizeof s->mac);
    s->saved = s_rn;
}

bool net_wifi_is_sta(void) { return s_state == NET_ROUTER_UP; }

bool net_wifi_is_up(void) { return true; /* своя точка есть всегда */ }

int32_t net_wifi_rssi(void)
{
    if (s_state != NET_ROUTER_UP) return 0;

    wifi_ap_record_t ap;
    if (esp_wifi_sta_get_ap_info(&ap) != ESP_OK) return 0;
    return ap.rssi;
}

const char *net_wifi_ip(void) { return s_state == NET_ROUTER_UP ? s_sta_ip : s_ap_ip; }

const char *net_wifi_ssid(void)
{
    int c = s_cur;
    return s_state == NET_ROUTER_UP && c >= 0 ? s_r[c].ssid : CONFIG_K57_AP_SSID;
}

bool net_wifi_is_ap_peer(uint32_t addr_be)
{
    /* Своя точка - 192.168.4.0/24 (адрес s_ap_ip), сеть роутера - другая. */
    uint8_t a = (uint8_t)addr_be, b = (uint8_t)(addr_be >> 8), c = (uint8_t)(addr_be >> 16);
    unsigned x, y, z;
    return sscanf(s_ap_ip, "%u.%u.%u", &x, &y, &z) == 3 && a == x && b == y && c == z;
}

const char *net_wifi_ap_ip(void) { return s_ap_ip; }

const char *net_wifi_sta_ip(void) { return s_state == NET_ROUTER_UP ? s_sta_ip : ""; }

const char *net_wifi_mac(void) { return s_mac; }

/*
 *  net_wifi.c
 *
 *  Своя точка доступа K57 - всегда, и вдобавок роутер, если он задан
 *  (13.11.2026). ESP32 держит обе сети сразу (WIFI_MODE_APSTA): на стенде
 *  телефон и ноутбук подключаются к K57 напрямую, как раньше, а из дома мост
 *  виден через роутер - по адресу, который тот выдал, или по имени k57.local.
 *
 *  До 13.11.2026 было «или - или», и выбиралось при сборке: в общей сети своей
 *  точки не было, а сеть с паролем жила в sdkconfig.local - сменить её можно
 *  было только пересборкой. Теперь роутер задаётся со страницы (вкладка «Мост»)
 *  и хранится в NVS моста: ни в прошивку, ни в git он не попадает.
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

#define NVS_NS "k57net" /* роутер: ssid, pass, chan */

static esp_netif_t       *s_ap_if;
static esp_timer_handle_t s_retry_t;
static portMUX_TYPE       s_mux = portMUX_INITIALIZER_UNLOCKED;

static char             s_rssid[33]; /* роутер; пусто - не задан */
static char             s_rpass[65];
static uint8_t          s_chan;      /* канал роутера в прошлый раз: подключаться без поиска */
static volatile int     s_state;     /* NET_ROUTER_* */
static volatile int     s_reason;    /* причина последнего разрыва (wifi_err_reason_t) */
static volatile int     s_clients;   /* подключено к K57 */
static int              s_fails;
static volatile bool    s_quiet;     /* разрыв устроили сами - переподключаться не надо */
static char             s_sta_ip[16];
static char             s_ap_ip[16] = "192.168.4.1";

/*---------------------------------------------------------------------------
 * Роутер: настройка в NVS
 *---------------------------------------------------------------------------*/
static void load_router(void)
{
    nvs_handle_t h;
    size_t       n;

    s_rssid[0] = s_rpass[0] = 0;
    if (nvs_open(NVS_NS, NVS_READONLY, &h) == ESP_OK) {
        n = sizeof s_rssid;
        if (nvs_get_str(h, "ssid", s_rssid, &n) != ESP_OK) {
            s_rssid[0] = 0;
        }
        n = sizeof s_rpass;
        if (nvs_get_str(h, "pass", s_rpass, &n) != ESP_OK) {
            s_rpass[0] = 0;
        }
        nvs_get_u8(h, "chan", &s_chan);
        nvs_close(h);
    }
#if !CONFIG_K57_AP_ONLY
    /* Со страницы ещё ничего не задавали - то, что было в sdkconfig.local. */
    if (s_rssid[0] == 0) {
        strlcpy(s_rssid, CONFIG_K57_WIFI_SSID, sizeof s_rssid);
        strlcpy(s_rpass, CONFIG_K57_WIFI_PASS, sizeof s_rpass);
    }
#endif
}

static void save_chan(uint8_t ch)
{
    nvs_handle_t h;
    if (nvs_open(NVS_NS, NVS_READWRITE, &h) == ESP_OK) {
        nvs_set_u8(h, "chan", ch);
        nvs_commit(h);
        nvs_close(h);
    }
}

static void connect_now(void)
{
    wifi_config_t wc = {0};

    strlcpy((char *)wc.sta.ssid, s_rssid, sizeof wc.sta.ssid);
    strlcpy((char *)wc.sta.password, s_rpass, sizeof wc.sta.password);
    /* Знаем канал - сразу на него, без обхода всех: обход мешает K57. Две
     * неудачи подряд - роутер, может быть, сменил канал: искать по всем. */
    wc.sta.channel     = s_fails < 2 ? s_chan : 0;
    wc.sta.scan_method = WIFI_FAST_SCAN;
    esp_wifi_set_config(WIFI_IF_STA, &wc);
    s_state = NET_ROUTER_CONNECTING;
    esp_wifi_connect();
}

static void retry_cb(void *arg)
{
    (void)arg;
    if (s_rssid[0] != 0 && s_state != NET_ROUTER_UP) {
        connect_now();
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
        if (s_rssid[0] != 0) {
            ESP_LOGI(TAG, "роутер %s: подключаюсь", s_rssid);
            connect_now();
        }
        break;

    case WIFI_EVENT_STA_CONNECTED: {
        const wifi_event_sta_connected_t *e = data;
        if (e->channel != s_chan) {
            s_chan = e->channel;
            save_chan(s_chan);
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
        if (s_rssid[0] == 0) {
            s_state = NET_ROUTER_NONE;
            break;
        }
        if (s_state == NET_ROUTER_UP) {
            ESP_LOGW(TAG, "роутер %s: связь потеряна (причина %d)", s_rssid, e->reason);
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
    ESP_LOGI(TAG, "роутер %s: адрес %s, сигнал %ld дБм, канал %u", s_rssid, s_sta_ip,
             (long)net_wifi_rssi(), s_chan);
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
 * Нужен, чтобы программа на ПК находила мост по имени k57.local, а не по адресу,
 * который роутер выдаёт заново после каждой перезагрузки платы. Отвечает и в
 * роутере, и в K57 (CONFIG_MDNS_PREDEF_NETIF_STA / _AP).
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
     * стартовать; терять там, кроме роутера, нечего. */
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

    load_router();
    s_state = s_rssid[0] ? NET_ROUTER_CONNECTING : NET_ROUTER_NONE;

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
    ESP_LOGI(TAG, "своя точка %s, адрес %s; роутер: %s", CONFIG_K57_AP_SSID, s_ap_ip,
             s_rssid[0] ? s_rssid : "не задан");

    start_mdns();
    return ESP_OK;
}

/*---------------------------------------------------------------------------
 * Со страницы (web_page.c)
 *---------------------------------------------------------------------------*/
const char *net_wifi_router_set(const char *ssid, const char *pass)
{
    size_t ls = strlen(ssid), lp = strlen(pass);

    if (ls > 32) {
        return "имя сети длиннее 32 байт";
    }
    if (ls > 0 && lp != 0 && (lp < 8 || lp > 63)) {
        return "пароль WPA2 - от 8 до 63 символов (или пусто для открытой сети)";
    }

    nvs_handle_t h;
    if (nvs_open(NVS_NS, NVS_READWRITE, &h) != ESP_OK) {
        return "NVS не открывается";
    }
    if (ls == 0) {
        nvs_erase_all(h); /* забыть роутер - и канал заодно */
    } else {
        nvs_set_str(h, "ssid", ssid);
        nvs_set_str(h, "pass", pass);
        nvs_set_u8(h, "chan", 0);
    }
    esp_err_t err = nvs_commit(h);
    nvs_close(h);
    if (err != ESP_OK) {
        return "NVS не записался";
    }

    esp_timer_stop(s_retry_t);
    if (s_state == NET_ROUTER_UP || s_state == NET_ROUTER_CONNECTING) {
        s_quiet = true; /* этот разрыв - наш */
        esp_wifi_disconnect();
    }
    strlcpy(s_rssid, ssid, sizeof s_rssid);
    strlcpy(s_rpass, pass, sizeof s_rpass);
    s_chan  = 0;
    s_fails = 0;
    s_reason = 0;
    if (ls == 0) {
        s_state = NET_ROUTER_NONE;
        ESP_LOGI(TAG, "роутер забыт");
    } else {
        ESP_LOGI(TAG, "роутер %s: задан со страницы", s_rssid);
        connect_now();
    }
    return NULL;
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
    uint16_t         n = (uint16_t)max;
    wifi_ap_record_t rec[16];
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
    strlcpy(s->apSsid, CONFIG_K57_AP_SSID, sizeof s->apSsid);
    strlcpy(s->apIp, s_ap_ip, sizeof s->apIp);
    s->apClients = s_clients;
    strlcpy(s->routerSsid, s_rssid, sizeof s->routerSsid);
    s->state  = s_state;
    s->reason = s_reason;
    portENTER_CRITICAL(&s_mux);
    strlcpy(s->staIp, s_sta_ip, sizeof s->staIp);
    portEXIT_CRITICAL(&s_mux);
    s->rssi = net_wifi_rssi();
    s->ch   = s_chan;
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

const char *net_wifi_ssid(void) { return s_state == NET_ROUTER_UP ? s_rssid : CONFIG_K57_AP_SSID; }

/*
 * net_wifi.c
 *
 *  STA с запасным режимом точки доступа. Перенесено из ESP_INV
 *  (firmware/main/net_wifi.c), поменялись только имена настроек.
 *
 *  Почему запасной режим вообще есть. В K55 связь шла по CAN: кабель воткнут -
 *  связь есть, точка. У WiFi отказ тише: роутер перезагрузился, сменился
 *  пароль, плата уехала на другой стол - и мост пропадает из сети совсем,
 *  а достучаться до него можно только по UART, то есть физически подойдя с
 *  ноутбуком. Поэтому после CONFIG_K57_WIFI_RETRY неудач плата поднимает
 *  собственную точку: связь деградирует, но не исчезает.
 *
 *  Бесконечный цикл переподключения здесь был бы хуже, чем кажется: он
 *  выглядит работающим (лог бодро печатает "попытка N"), но приводит ровно к
 *  тому состоянию, из-за которого нужен UART.
 */

#include <stdio.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/event_groups.h"
#include "freertos/task.h"

#include "esp_event.h"
#include "esp_log.h"
#include "esp_mac.h"
#include "esp_netif.h"
#include "esp_wifi.h"
#include "nvs_flash.h"

#include "mdns.h"

#include "net_proto.h"
#include "net_wifi.h"

static const char *TAG = "wifi";

#define BIT_GOT_IP   BIT0
#define BIT_STA_FAIL BIT1

static EventGroupHandle_t s_events;
static int                s_retry;
static bool               s_is_sta;
static char               s_ip[16] = "0.0.0.0";

/*---------------------------------------------------------------------------
 * Обработчики событий
 *
 * Живут в задаче цикла событий, не в прерывании: esp_wifi_connect() из ISR
 * вызывать нельзя, а тяжёлого здесь всё равно ничего нет.
 *---------------------------------------------------------------------------*/
static void on_wifi(void *arg, esp_event_base_t base, int32_t id, void *data)
{
    (void)arg;
    (void)base;

    switch (id)
    {
    case WIFI_EVENT_STA_START:
        esp_wifi_connect();
        break;

    case WIFI_EVENT_STA_DISCONNECTED:
    {
        const wifi_event_sta_disconnected_t *e = data;

        /* Разрыв уже установленного соединения и неудача при первом подходе -
         * разные вещи, но лечатся одинаково: пробуем снова, пока не кончатся
         * попытки. Счётчик сбрасывается при получении IP. */
        if (s_retry < CONFIG_K57_WIFI_RETRY)
        {
            ++s_retry;
            ESP_LOGW(TAG, "нет связи (причина %d), попытка %d из %d",
                     e->reason, s_retry, CONFIG_K57_WIFI_RETRY);
            esp_wifi_connect();
        }
        else
        {
            ESP_LOGE(TAG, "к сети %s подключиться не удалось", CONFIG_K57_WIFI_SSID);
            xEventGroupSetBits(s_events, BIT_STA_FAIL);
        }
        break;
    }

    case WIFI_EVENT_AP_STACONNECTED:
    {
        const wifi_event_ap_staconnected_t *e = data;
        ESP_LOGI(TAG, "к точке доступа подключились " MACSTR, MAC2STR(e->mac));
        break;
    }

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

    snprintf(s_ip, sizeof(s_ip), IPSTR, IP2STR(&e->ip_info.ip));
    s_retry = 0;
    xEventGroupSetBits(s_events, BIT_GOT_IP);
}

/*---------------------------------------------------------------------------
 * Запасная точка доступа
 *---------------------------------------------------------------------------*/
static esp_err_t start_ap(void)
{
    esp_netif_t *ap = esp_netif_create_default_wifi_ap();

    wifi_config_t wc = {
        .ap = {
            .ssid_len       = strlen(CONFIG_K57_AP_SSID),
            .channel        = 1,
            .max_connection = 2,
            .authmode       = WIFI_AUTH_WPA2_PSK,
        },
    };
    strlcpy((char *)wc.ap.ssid, CONFIG_K57_AP_SSID, sizeof(wc.ap.ssid));
    strlcpy((char *)wc.ap.password, CONFIG_K57_AP_PASS, sizeof(wc.ap.password));

    /* WPA2 требует минимум 8 символов. Короткий пароль молча превратил бы
     * точку в открытую - лучше сказать об этом вслух. */
    if (strlen(CONFIG_K57_AP_PASS) < 8)
    {
        ESP_LOGW(TAG, "пароль точки короче 8 символов, точка будет открытой");
        wc.ap.authmode = WIFI_AUTH_OPEN;
    }

    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_AP));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_AP, &wc));
    ESP_ERROR_CHECK(esp_wifi_start());

    esp_netif_ip_info_t ip;
    esp_netif_get_ip_info(ap, &ip);
    snprintf(s_ip, sizeof(s_ip), IPSTR, IP2STR(&ip.ip));

    s_is_sta = false;
    ESP_LOGW(TAG, "запасной режим: точка доступа %s, адрес %s",
             CONFIG_K57_AP_SSID, s_ip);
    return ESP_OK;
}

/*---------------------------------------------------------------------------
 * mDNS
 *
 * Нужен, чтобы программа на ПК находила мост по имени k57.local, а не по адресу,
 * который роутер выдаёт заново после каждой перезагрузки платы.
 *---------------------------------------------------------------------------*/
static void start_mdns(void)
{
    esp_err_t err = mdns_init();
    if (err != ESP_OK)
    {
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
     * стартовать; терять там нечего. */
    esp_err_t err = nvs_flash_init();
    if (err == ESP_ERR_NVS_NO_FREE_PAGES || err == ESP_ERR_NVS_NEW_VERSION_FOUND)
    {
        ESP_ERROR_CHECK(nvs_flash_erase());
        err = nvs_flash_init();
    }
    ESP_ERROR_CHECK(err);

    s_events = xEventGroupCreate();

    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());
    esp_netif_create_default_wifi_sta();

    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK(esp_wifi_init(&cfg));

    ESP_ERROR_CHECK(esp_event_handler_instance_register(
        WIFI_EVENT, ESP_EVENT_ANY_ID, on_wifi, NULL, NULL));
    ESP_ERROR_CHECK(esp_event_handler_instance_register(
        IP_EVENT, IP_EVENT_STA_GOT_IP, on_got_ip, NULL, NULL));

    wifi_config_t wc = { 0 };
    strlcpy((char *)wc.sta.ssid, CONFIG_K57_WIFI_SSID, sizeof(wc.sta.ssid));
    strlcpy((char *)wc.sta.password, CONFIG_K57_WIFI_PASS, sizeof(wc.sta.password));

    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &wc));

    ESP_ERROR_CHECK(esp_wifi_start());

    /* Энергосбережение выключаем ПОСЛЕ старта: до него настройка не
     * применяется. С энергосбережением модем засыпает между маячками, и
     * UDP-телеметрия приезжает рывками с задержкой в десятки миллисекунд -
     * на осциллографе это выглядит как дёрганый поток, хотя такт управления
     * идёт ровно. Диагностировать такое, не зная про WIFI_PS, тяжело. */
    ESP_ERROR_CHECK(esp_wifi_set_ps(WIFI_PS_NONE));

    ESP_LOGI(TAG, "подключаюсь к %s", CONFIG_K57_WIFI_SSID);

    EventBits_t bits = xEventGroupWaitBits(
        s_events, BIT_GOT_IP | BIT_STA_FAIL, pdFALSE, pdFALSE, portMAX_DELAY);

    if (bits & BIT_GOT_IP)
    {
        s_is_sta = true;
        ESP_LOGI(TAG, "подключено, адрес %s, сигнал %ld дБм",
                 s_ip, (long)net_wifi_rssi());
    }
    else
    {
        /* Менять STA на AP на ходу нельзя - сначала останавливаем WiFi. */
        esp_wifi_stop();
        ESP_ERROR_CHECK(start_ap());
    }

    start_mdns();
    return ESP_OK;
}

bool net_wifi_is_sta(void) { return s_is_sta; }

bool net_wifi_is_up(void)
{
    if (!s_is_sta) return true; /* своя точка поднята - значит доступны */
    return (xEventGroupGetBits(s_events) & BIT_GOT_IP) != 0;
}

int32_t net_wifi_rssi(void)
{
    if (!s_is_sta) return 0;

    wifi_ap_record_t ap;
    if (esp_wifi_sta_get_ap_info(&ap) != ESP_OK) return 0;
    return ap.rssi;
}

const char *net_wifi_ip(void) { return s_ip; }

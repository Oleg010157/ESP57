/*
 * web.c
 *
 * HTTP-сервер моста. Что на нём и зачем - в web.h.
 */
#include <stdio.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#include "esp_app_desc.h"
#include "esp_http_server.h"
#include "esp_log.h"
#include "esp_ota_ops.h"
#include "esp_system.h"
#include "esp_timer.h"
#include "mbedtls/base64.h"

#include "web.h"

static const char *TAG = "web";

#define WEB_USER        "k57"
#define CONFIRM_AFTER_S 20  /* столько проработать, чтобы признать новую прошивку */
#define PENDING_LIMIT_S 60  /* не признана за это время - назад к прежней */

static httpd_handle_t s_srv;
static char           s_auth[96];   /* "Basic <base64(k57:пароль)>" */
static char           s_version[160];
static bool           s_pending;    /* новая прошивка ещё не признана */
static int            s_uptime_s;
static volatile bool  s_updating;

/*---------------------------------------------------------------------------
 * Пароль
 *---------------------------------------------------------------------------*/
static void auth_init(void)
{
    const char *pass = CONFIG_K57_ADMIN_PASS[0] != '\0' ? CONFIG_K57_ADMIN_PASS : CONFIG_K57_AP_PASS;
    char   plain[80];
    size_t olen = 0;

    snprintf(plain, sizeof plain, "%s:%s", WEB_USER, pass);
    strcpy(s_auth, "Basic ");
    mbedtls_base64_encode((unsigned char *)s_auth + 6, sizeof s_auth - 7, &olen,
                          (const unsigned char *)plain, strlen(plain));
    s_auth[6 + olen] = '\0';
}

/* true - пароль верный; иначе ответ 401 уже отправлен. */
bool web_auth_ok(httpd_req_t *req)
{
    char got[sizeof s_auth];

    if (httpd_req_get_hdr_value_str(req, "Authorization", got, sizeof got) == ESP_OK &&
        strcmp(got, s_auth) == 0) {
        return true;
    }
    /* Без WWW-Authenticate - нарочно (12.11.2026). С ним браузер на любой
     * отказ выводил своё окно «имя пользователя и пароль» поверх поля пароля
     * на странице, и было непонятно, что куда вводить. Страница, Motor_Control
     * и curl -u шлют пароль сразу, вопрос им не нужен. */
    httpd_resp_set_status(req, "401 Unauthorized");
    httpd_resp_sendstr(req, "нужен пароль");
    return false;
}

/*---------------------------------------------------------------------------
 * /version
 *---------------------------------------------------------------------------*/
static esp_err_t version_get(httpd_req_t *req)
{
    const esp_app_desc_t   *d   = esp_app_get_description();
    const esp_partition_t  *run = esp_ota_get_running_partition();
    char                    buf[256];

    snprintf(buf, sizeof buf,
             "{\"version\":\"%s\",\"date\":\"%s\",\"time\":\"%s\",\"idf\":\"%s\","
             "\"partition\":\"%s\",\"pending\":%s,\"uptime\":%d}",
             d->version, d->date, d->time, d->idf_ver, run ? run->label : "?",
             s_pending ? "true" : "false", s_uptime_s);
    httpd_resp_set_type(req, "application/json");
    return httpd_resp_sendstr(req, buf);
}

/*---------------------------------------------------------------------------
 * /update
 *---------------------------------------------------------------------------*/
static void restart_cb(void *arg)
{
    (void)arg;
    esp_restart();
}

static esp_err_t update_post(httpd_req_t *req)
{
    static char buf[2048]; /* один запрос за раз - сервер однопоточный */

    if (!web_auth_ok(req)) {
        return ESP_OK;
    }
    if (s_updating) {
        httpd_resp_set_status(req, "409 Conflict");
        return httpd_resp_sendstr(req, "обновление уже идёт");
    }

    const esp_partition_t *dst = esp_ota_get_next_update_partition(NULL);
    int total = req->content_len;
    if (dst == NULL || total <= 0 || (size_t)total > dst->size) {
        httpd_resp_set_status(req, "400 Bad Request");
        return httpd_resp_sendstr(req, total <= 0 ? "пустой файл" : "файл больше места под программу");
    }

    s_updating = true;
    ESP_LOGI(TAG, "обновление: %d байт в %s", total, dst->label);
    int64_t t0 = esp_timer_get_time();

    /* SEQUENTIAL_WRITES: стирать по мере записи, а не всё место сразу -
     * иначе первые секунды флеш стоял бы занятым целиком, и обмен с прибором
     * замер бы надолго одним куском. */
    esp_ota_handle_t h;
    esp_err_t err = esp_ota_begin(dst, OTA_WITH_SEQUENTIAL_WRITES, &h);
    int left = total;
    while (err == ESP_OK && left > 0) {
        int n = httpd_req_recv(req, buf, left < (int)sizeof buf ? left : (int)sizeof buf);
        if (n == HTTPD_SOCK_ERR_TIMEOUT) {
            continue;
        }
        if (n <= 0) {
            err = ESP_FAIL;
            break;
        }
        err = esp_ota_write(h, buf, n);
        left -= n;
    }
    if (err == ESP_OK) {
        err = esp_ota_end(h); /* здесь же проверка образа: заголовок, SHA-256 */
    } else {
        esp_ota_abort(h);
    }
    if (err == ESP_OK) {
        err = esp_ota_set_boot_partition(dst);
    }
    s_updating = false;

    if (err != ESP_OK) {
        ESP_LOGW(TAG, "обновление не удалось: %s", esp_err_to_name(err));
        httpd_resp_set_status(req, "500 Internal Server Error");
        char msg[96];
        snprintf(msg, sizeof msg, "не записалось: %s", esp_err_to_name(err));
        return httpd_resp_sendstr(req, msg);
    }

    ESP_LOGI(TAG, "записано за %lld мс - перезапуск", (esp_timer_get_time() - t0) / 1000);
    httpd_resp_sendstr(req, "OK, перезапуск");

    /* Перезапуск чуть позже: ответ должен успеть уйти. */
    const esp_timer_create_args_t a = {.callback = restart_cb, .name = "ota_restart"};
    esp_timer_handle_t t;
    if (esp_timer_create(&a, &t) == ESP_OK) {
        esp_timer_start_once(t, 500 * 1000);
    }
    return ESP_OK;
}

/*---------------------------------------------------------------------------*/
static void pending_timeout_cb(void *arg)
{
    (void)arg;
    if (s_pending) {
        /* Не признана за минуту. Перезапуск без отметки «годная» - загрузчик
         * сочтёт её неудачной и поднимет прежнюю. */
        esp_restart();
    }
}

void web_confirm_if_pending(void)
{
    s_uptime_s++;
    if (s_pending && s_uptime_s >= CONFIRM_AFTER_S) {
        if (esp_ota_mark_app_valid_cancel_rollback() == ESP_OK) {
            s_pending = false;
            ESP_LOGI(TAG, "новая прошивка проработала %d с - признана годной", s_uptime_s);
        }
    }
}

const char *web_version(void)
{
    return s_version;
}

void web_start(void)
{
    const esp_app_desc_t  *d   = esp_app_get_description();
    const esp_partition_t *run = esp_ota_get_running_partition();
    esp_ota_img_states_t   st;

    if (run != NULL && esp_ota_get_state_partition(run, &st) == ESP_OK &&
        st == ESP_OTA_IMG_PENDING_VERIFY) {
        s_pending = true;
        const esp_timer_create_args_t a = {.callback = pending_timeout_cb, .name = "ota_pending"};
        esp_timer_handle_t t;
        if (esp_timer_create(&a, &t) == ESP_OK) {
            esp_timer_start_once(t, (uint64_t)PENDING_LIMIT_S * 1000 * 1000);
        }
    }
    snprintf(s_version, sizeof s_version, "%s от %s %s, раздел %s%s", d->version, d->date, d->time,
             run ? run->label : "?", s_pending ? " - ПРОБНАЯ" : "");

    auth_init();

    httpd_config_t cfg   = HTTPD_DEFAULT_CONFIG();
    cfg.stack_size       = 6144;
    cfg.core_id          = 0;     /* не на ядре обмена с прибором */
    cfg.max_open_sockets = 3;     /* куча моста почти вся у чёрного ящика */
    cfg.lru_purge_enable = true;
    cfg.recv_wait_timeout = 10;
    cfg.max_uri_handlers = 20;
    cfg.uri_match_fn     = httpd_uri_match_wildcard;

    if (httpd_start(&s_srv, &cfg) != ESP_OK) {
        ESP_LOGE(TAG, "HTTP-сервер не поднялся");
        return;
    }
    static const httpd_uri_t ver = {.uri = "/version", .method = HTTP_GET, .handler = version_get};
    static const httpd_uri_t upd = {.uri = "/update", .method = HTTP_POST, .handler = update_post};
    httpd_register_uri_handler(s_srv, &ver);
    httpd_register_uri_handler(s_srv, &upd);
    web_page_register(s_srv);
    ESP_LOGI(TAG, "HTTP 80: страница /, /version, /update");
}

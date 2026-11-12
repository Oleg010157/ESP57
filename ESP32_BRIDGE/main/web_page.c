/*
 * web_page.c
 *
 * Страница прибора в браузере: http://192.168.4.1/ (или k57.local). Сама
 * страница - один файл page/index.html, вшитый в прошивку; данные она берёт
 * отсюда, запросами JSON, и раскладывает сама. Мост чисел не толкует: кадр
 * 140 уходит словами как есть, цены делений - в странице, рядом с подписями.
 *
 *   GET /                  страница
 *   GET /errorcodes.h      описания кодов - тот же bin/ErrorCodes.h, что у
 *                          Motor_Control, вшитый при сборке
 *   GET /api/state         кадр 140, лампа, чей сеанс (live.h)  раз в 0,7 с
 *   GET /api/errors        таблица ошибок прибора
 *   GET /api/journal?n=N   последние N строк длинного журнала моста
 *   GET /api/bbox          записи чёрного ящика
 *   GET /api/bridge        мост: сеть, обмен с прибором, память
 *
 * Без пароля: всё это только для чтения. Пароль - у /update и у будущего
 * управления (web.c).
 */
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "esp_heap_caps.h"
#include "esp_http_server.h"

#include "bbox.h"
#include "bstore.h"
#include "live.h"
#include "net_link.h"
#include "net_wifi.h"
#include "spi_link.h"
#include "web.h"

extern const char page_start[] asm("_binary_index_html_start");
extern const char page_end[] asm("_binary_index_html_end");
extern const char codes_start[] asm("_binary_ErrorCodes_h_start");
extern const char codes_end[] asm("_binary_ErrorCodes_h_end");

/* Обработчики идут по одному - сервер однопоточный, - буфер общий. */
static char s_buf[1024];
static int  s_len;

static void out(httpd_req_t *req, const char *fmt, ...) __attribute__((format(printf, 2, 3)));

static void out(httpd_req_t *req, const char *fmt, ...)
{
    va_list ap;
    va_start(ap, fmt);
    int n = vsnprintf(s_buf + s_len, sizeof s_buf - s_len, fmt, ap);
    va_end(ap);
    if (n < 0) {
        return;
    }
    if (s_len + n >= (int)sizeof s_buf) {
        /* не влезло: отправить накопленное и написать заново в пустой */
        httpd_resp_send_chunk(req, s_buf, s_len);
        s_len = 0;
        va_start(ap, fmt);
        n = vsnprintf(s_buf, sizeof s_buf, fmt, ap);
        va_end(ap);
        if (n >= (int)sizeof s_buf) {
            n = sizeof s_buf - 1;
        }
    }
    s_len += n;
}

static esp_err_t out_end(httpd_req_t *req)
{
    if (s_len > 0) {
        httpd_resp_send_chunk(req, s_buf, s_len);
    }
    s_len = 0;
    return httpd_resp_send_chunk(req, NULL, 0);
}

static void json_begin(httpd_req_t *req)
{
    s_len = 0;
    httpd_resp_set_type(req, "application/json");
    httpd_resp_set_hdr(req, "Cache-Control", "no-store");
}

/*---------------------------------------------------------------------------*/
static esp_err_t page_get(httpd_req_t *req)
{
    httpd_resp_set_type(req, "text/html; charset=utf-8");
    return httpd_resp_send(req, page_start, page_end - page_start - 1); /* без завершающего нуля */
}

static esp_err_t codes_get(httpd_req_t *req)
{
    /* ErrorCodes.h - в UTF-8, как и весь проект. */
    httpd_resp_set_type(req, "text/plain; charset=utf-8");
    httpd_resp_set_hdr(req, "Cache-Control", "max-age=3600");
    return httpd_resp_send(req, codes_start, codes_end - codes_start - 1);
}

static esp_err_t state_get(httpd_req_t *req)
{
    live_state_t s;
    live_touch(); /* страница открыта - мост держит «только смотреть» */
    live_get(&s);

    json_begin(req);
    out(req, "{\"age\":%ld,\"lamp\":%u,\"lampAge\":%ld,\"errAge\":%ld,\"pult\":%s,\"session\":%s,"
             "\"watch\":%s,\"w\":[",
        s.age == UINT32_MAX ? -1L : (long)s.age, s.lamp, s.lampAge == UINT32_MAX ? -1L : (long)s.lampAge,
        s.errAge == UINT32_MAX ? -1L : (long)s.errAge, s.pult ? "true" : "false",
        s.session ? "true" : "false", s.watching ? "true" : "false");
    for (int i = 0; i < 32; i++) {
        out(req, "%s%u", i ? "," : "", s.w[i]);
    }
    /* Текущие ошибки - для красного круга в шапке: бит 15 слова ErrBuff
     * (CurrentError). Таблица обновляется раз в три секунды (live.c). */
    static uint16_t ew[LIVE_ERR_CODES];
    static uint32_t et[LIVE_ERR_CODES];
    live_errors(ew, et);
    out(req, "],\"cur\":[");
    bool first = true;
    for (int i = 0; i < LIVE_ERR_CODES; i++) {
        if (ew[i] & 0x8000u) {
            out(req, "%s%d", first ? "" : ",", i);
            first = false;
        }
    }
    out(req, "]}");
    return out_end(req);
}

static esp_err_t errors_get(httpd_req_t *req)
{
    static uint16_t w[LIVE_ERR_CODES];
    static uint32_t t[LIVE_ERR_CODES];
    bool have = live_errors(w, t);

    json_begin(req);
    out(req, "{\"have\":%s,\"list\":[", have ? "true" : "false");
    bool first = true;
    for (int i = 0; i < LIVE_ERR_CODES; i++) {
        if (w[i] == 0) {
            continue;
        }
        out(req, "%s[%d,%u,%lu]", first ? "" : ",", i, w[i], (unsigned long)t[i]);
        first = false;
    }
    out(req, "]}");
    return out_end(req);
}

typedef struct {
    httpd_req_t *req;
    bool         first;
} jctx_t;

static void journal_row(const bstore_row_t *r, void *ctx)
{
    jctx_t *j = ctx;
    out(j->req, "%s[%lu,%lu,%u,%u,%u]", j->first ? "" : ",", (unsigned long)r->seq,
        (unsigned long)r->utc, r->code, r->session, r->count);
    j->first = false;
}

static esp_err_t journal_get(httpd_req_t *req)
{
    char     q[32], v[12];
    uint32_t n = 300;
    if (httpd_req_get_url_query_str(req, q, sizeof q) == ESP_OK &&
        httpd_query_key_value(q, "n", v, sizeof v) == ESP_OK) {
        n = (uint32_t)strtoul(v, NULL, 10);
    }
    bstore_stat_t st;
    bstore_get_stat(&st);
    uint32_t skip = st.rows > n ? st.rows - n : 0;

    json_begin(req);
    out(req, "{\"rows\":%lu,\"list\":[", (unsigned long)st.rows);
    jctx_t j = {.req = req, .first = true};
    bstore_walk(skip, journal_row, &j);
    out(req, "]}");
    return out_end(req);
}

static esp_err_t bbox_get(httpd_req_t *req)
{
    static bbox_entry_t e[8];
    int n = bbox_list(e, 8);
    bbox_stat_t st;
    bbox_get_stat(&st);

    json_begin(req);
    out(req, "{\"ring\":%lu,\"frames\":%lu,\"state\":%u,\"noFlash\":%s,\"list\":[",
        (unsigned long)st.ringBytes, (unsigned long)st.ringFrames, st.state,
        st.noFlash ? "true" : "false");
    for (int i = 0; i < n; i++) {
        out(req, "%s{\"seq\":%lu,\"code\":%u,\"utc\":%lu,\"type\":%u,\"session\":%u,"
                 "\"frames\":%lu,\"trig\":%lu,\"extra\":[%u,%u,%u,%u]}",
            i ? "," : "", (unsigned long)e[i].seq, e[i].code, (unsigned long)e[i].utc,
            e[i].frameType, e[i].session, (unsigned long)e[i].frames, (unsigned long)e[i].trigFrame,
            e[i].extra[0], e[i].extra[1], e[i].extra[2], e[i].extra[3]);
    }
    out(req, "]}");
    return out_end(req);
}

static esp_err_t bridge_get(httpd_req_t *req)
{
    spi_link_stat_t s;
    net_link_stat_t n;
    spi_link_get_stat(&s);
    net_link_get_stat(&n);

    json_begin(req);
    out(req, "{\"version\":\"%s\",\"ssid\":\"%s\",\"sta\":%s,\"ip\":\"%s\",\"rssi\":%ld,"
             "\"packets\":%lu,\"seqGap\":%lu,\"resets\":%lu,\"f28Drop\":%lu,"
             "\"pult\":%s,\"udpErr\":%lu,\"heap\":%u}",
        web_version(), net_wifi_ssid(), net_wifi_is_sta() ? "true" : "false", net_wifi_ip(),
        (long)net_wifi_rssi(), (unsigned long)s.packets, (unsigned long)s.seqGap,
        (unsigned long)s.slaveResets, (unsigned long)s.f28Drop, n.connected ? "true" : "false",
        (unsigned long)n.udpErr, (unsigned)(heap_caps_get_free_size(MALLOC_CAP_8BIT) / 1024));
    return out_end(req);
}

void web_page_register(httpd_handle_t srv)
{
    static const httpd_uri_t u[] = {
        {.uri = "/", .method = HTTP_GET, .handler = page_get},
        {.uri = "/index.html", .method = HTTP_GET, .handler = page_get},
        {.uri = "/errorcodes.h", .method = HTTP_GET, .handler = codes_get},
        {.uri = "/api/state", .method = HTTP_GET, .handler = state_get},
        {.uri = "/api/errors", .method = HTTP_GET, .handler = errors_get},
        {.uri = "/api/journal", .method = HTTP_GET, .handler = journal_get},
        {.uri = "/api/bbox", .method = HTTP_GET, .handler = bbox_get},
        {.uri = "/api/bridge", .method = HTTP_GET, .handler = bridge_get},
    };
    for (size_t i = 0; i < sizeof u / sizeof u[0]; i++) {
        httpd_register_uri_handler(srv, &u[i]);
    }
}

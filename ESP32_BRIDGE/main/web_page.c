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
 *   POST /api/ctl?do=..&v=N  управление - с паролем (live.c, live_control):
 *                          power, mode, gridEn, iset, uref, freq, etrip
 *   GET /tables.txt        таблицы Motor_Control (CanTxFiles, tables.h)
 *   POST /api/tbl?cmd=C&op=r|w|m&v=N&m=0|1  строка таблицы - с паролем
 *   GET /api/tblacks       ответы прибора на команды таблиц
 *   POST /tables           новые таблицы с ПК (Motor_Control) - с паролем
 *   GET /api/tblsrc        откуда таблицы: с ПК (когда) или вшитые
 *
 *   POST /api/f28img       образ прошивки прибора (K57F, fwup.h) - с паролем
 *   POST /api/f28go        прошить прибор образом из моста - с паролем
 *   POST /api/f28stop      остановить прошивку - с паролем
 *   POST /api/time?t=UTC   время браузера - мосту и прибору (live.h), без
 *                          пароля: страницу смотрят и не входя
 *   GET /api/f28[?ask=1]   образ в мосте, ход прошивки; ask - спросить у
 *                          прибора версию (333)
 *
 * GET без пароля: только чтение. Пароль - у /update и у /api/ctl.
 */
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "esp_heap_caps.h"
#include "esp_http_server.h"

#include "bbox.h"
#include "bstore.h"
#include "fwup.h"
#include "live.h"
#include "net_link.h"
#include "net_wifi.h"
#include "spi_link.h"
#include "tables.h"
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
    /* Без кэша: описания правят, а браузер держал бы старые час (было так). */
    httpd_resp_set_hdr(req, "Cache-Control", "no-cache");
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
    live_set_t st;
    live_get_set(&st);
    out(req, "],\"set\":{\"mode\":%u,\"modeAge\":%ld,\"en\":%u,\"enAge\":%ld,\"iset\":%u,"
             "\"isetAge\":%ld,\"uref\":%u,\"urefAge\":%ld,\"freq\":%u,\"freqAge\":%ld",
        st.mode, st.modeAge == UINT32_MAX ? -1L : (long)st.modeAge, st.gridEn,
        st.enAge == UINT32_MAX ? -1L : (long)st.enAge, st.iset,
        st.isetAge == UINT32_MAX ? -1L : (long)st.isetAge, st.uref,
        st.urefAge == UINT32_MAX ? -1L : (long)st.urefAge, st.freq,
        st.freqAge == UINT32_MAX ? -1L : (long)st.freqAge);
    out(req, ",\"energy\":[%lu,%lu,%lu,%lu],\"energyAge\":%ld}", (unsigned long)st.energy[0],
        (unsigned long)st.energy[1], (unsigned long)st.energy[2], (unsigned long)st.energy[3],
        st.energyAge == UINT32_MAX ? -1L : (long)st.energyAge);
    out(req, ",\"cur\":[");
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

/* Управление: POST /api/ctl?do=start|stop|mode|gridEn|iset|uref&v=N, с
 * паролем. Само действие и его проверки - live_control() (live.c). */
static esp_err_t ctl_post(httpd_req_t *req)
{
    char q[64], what[12] = "", v[12] = "0";

    if (!web_auth_ok(req)) {
        return ESP_OK;
    }
    if (httpd_req_get_url_query_str(req, q, sizeof q) != ESP_OK ||
        httpd_query_key_value(q, "do", what, sizeof what) != ESP_OK) {
        httpd_resp_set_status(req, "400 Bad Request");
        return httpd_resp_sendstr(req, "нет действия");
    }
    httpd_query_key_value(q, "v", v, sizeof v);

    const char *err = live_control(what, atoi(v));
    if (err != NULL) {
        httpd_resp_set_status(req, "409 Conflict");
        return httpd_resp_sendstr(req, err);
    }
    return httpd_resp_sendstr(req, "OK");
}

/* Таблицы Motor_Control (tables.h): сам текст - странице, команды - через
 * мост с проверкой, ответы прибора - списком. */
static esp_err_t tables_get(httpd_req_t *req)
{
    int         n;
    const char *t = tables_text(&n);
    httpd_resp_set_type(req, "text/plain; charset=utf-8");
    httpd_resp_set_hdr(req, "Cache-Control", "no-cache");
    return httpd_resp_send(req, t, n);
}

/* Новые таблицы с ПК: тело - текст в формате сборки («### имя» и файл),
 * X-K57-Time - UTC отправки. ?clear=1 - назад к вшитым при сборке. */
static esp_err_t tables_post(httpd_req_t *req)
{
    char q[16], v[4];

    if (!web_auth_ok(req)) {
        return ESP_OK;
    }
    if (httpd_req_get_url_query_str(req, q, sizeof q) == ESP_OK &&
        httpd_query_key_value(q, "clear", v, sizeof v) == ESP_OK && v[0] == '1') {
        tables_store_clear();
        return httpd_resp_sendstr(req, "OK, вшитые при сборке");
    }

    char     t[16] = "0";
    uint32_t utc   = 0;
    if (httpd_req_get_hdr_value_str(req, "X-K57-Time", t, sizeof t) == ESP_OK) {
        utc = (uint32_t)strtoul(t, NULL, 10);
    }
    int total = req->content_len;
    if (!tables_store_begin((uint32_t)total, utc)) {
        httpd_resp_set_status(req, "400 Bad Request");
        return httpd_resp_sendstr(req, total <= 0 ? "пустые таблицы" : "таблицы больше 64 кБ");
    }
    int left = total;
    while (left > 0) {
        int n = httpd_req_recv(req, s_buf, left < (int)sizeof s_buf ? left : (int)sizeof s_buf);
        if (n == HTTPD_SOCK_ERR_TIMEOUT) {
            continue;
        }
        if (n <= 0 || !tables_store_write(s_buf, (uint32_t)n)) {
            tables_store_clear();
            httpd_resp_set_status(req, "500 Internal Server Error");
            return httpd_resp_sendstr(req, "не записалось - мост вернулся к вшитым таблицам");
        }
        left -= n;
    }
    if (!tables_store_end()) {
        httpd_resp_set_status(req, "500 Internal Server Error");
        return httpd_resp_sendstr(req, "записалось с ошибкой - мост взял вшитые таблицы");
    }
    return httpd_resp_sendstr(req, "OK");
}

static esp_err_t tblsrc_get(httpd_req_t *req)
{
    uint32_t utc;
    int      n;
    bool     pc = tables_source(&utc);
    tables_text(&n);
    json_begin(req);
    out(req, "{\"pc\":%s,\"utc\":%lu,\"bytes\":%d}", pc ? "true" : "false", (unsigned long)utc, n);
    return out_end(req);
}

static esp_err_t tbl_post(httpd_req_t *req)
{
    char q[64], c[8] = "", op[4] = "r", v[12] = "0", m[4] = "0";

    if (!web_auth_ok(req)) {
        return ESP_OK;
    }
    if (httpd_req_get_url_query_str(req, q, sizeof q) != ESP_OK ||
        httpd_query_key_value(q, "cmd", c, sizeof c) != ESP_OK) {
        httpd_resp_set_status(req, "400 Bad Request");
        return httpd_resp_sendstr(req, "нет команды");
    }
    httpd_query_key_value(q, "op", op, sizeof op);
    httpd_query_key_value(q, "v", v, sizeof v);
    httpd_query_key_value(q, "m", m, sizeof m);

    const char *err = tables_cmd((uint16_t)atoi(c), op[0], atoi(v), atoi(m));
    if (err != NULL) {
        httpd_resp_set_status(req, "409 Conflict");
        return httpd_resp_sendstr(req, err);
    }
    return httpd_resp_sendstr(req, "OK");
}

typedef struct {
    httpd_req_t *req;
    bool         first;
} actx_t;

static void ack_row(uint16_t cmd, int16_t val, uint16_t flag, uint32_t age, void *ctx)
{
    actx_t *a = ctx;
    out(a->req, "%s[%u,%d,%u,%lu]", a->first ? "" : ",", cmd, val, flag, (unsigned long)age);
    a->first = false;
}

static esp_err_t tblacks_get(httpd_req_t *req)
{
    json_begin(req);
    out(req, "[");
    actx_t a = {.req = req, .first = true};
    tables_acks(ack_row, &a);
    out(req, "]");
    return out_end(req);
}

/*---------------------------------------------------------------------------*/
/* Строка в JSON: кавычки и обратная черта - с чертой, управляющие - мимо.
 * Имя файла и ответ прибора - не наши строки. */
static void out_str(httpd_req_t *req, const char *s)
{
    char   b[128];
    size_t n = 0;
    for (; *s && n < sizeof b - 2; s++) {
        if (*s == '"' || *s == '\\') {
            b[n++] = '\\';
        } else if ((unsigned char)*s < 0x20) {
            continue;
        }
        b[n++] = *s;
    }
    b[n] = 0;
    out(req, "\"%s\"", b);
}

/* Прошивка прибора через мост (fwup.h). */
static esp_err_t f28img_post(httpd_req_t *req)
{
    if (!web_auth_ok(req)) {
        return ESP_OK;
    }
    char     t[16];
    uint32_t utc   = 0;
    int      total = req->content_len;
    if (httpd_req_get_hdr_value_str(req, "X-K57-Time", t, sizeof t) == ESP_OK) {
        utc = (uint32_t)strtoul(t, NULL, 10);
    }
    const char *err = fwup_store_begin(total > 0 ? (uint32_t)total : 0, utc);
    if (err != NULL) {
        httpd_resp_set_status(req, "409 Conflict");
        return httpd_resp_sendstr(req, err);
    }
    int left = total;
    while (left > 0) {
        int n = httpd_req_recv(req, s_buf, left < (int)sizeof s_buf ? left : (int)sizeof s_buf);
        if (n == HTTPD_SOCK_ERR_TIMEOUT) {
            continue;
        }
        if (n <= 0 || !fwup_store_write(s_buf, (uint32_t)n)) {
            break;
        }
        left -= n;
    }
    err = fwup_store_end();
    if (err != NULL) {
        httpd_resp_set_status(req, "400 Bad Request");
        return httpd_resp_sendstr(req, err);
    }
    return httpd_resp_sendstr(req, "OK");
}

static esp_err_t f28go_post(httpd_req_t *req)
{
    if (!web_auth_ok(req)) {
        return ESP_OK;
    }
    const char *err = fwup_start();
    if (err != NULL) {
        httpd_resp_set_status(req, "409 Conflict");
        return httpd_resp_sendstr(req, err);
    }
    return httpd_resp_sendstr(req, "OK");
}

static esp_err_t time_post(httpd_req_t *req)
{
    char q[24], v[16];
    if (httpd_req_get_url_query_str(req, q, sizeof q) == ESP_OK &&
        httpd_query_key_value(q, "t", v, sizeof v) == ESP_OK) {
        live_set_time((uint32_t)strtoul(v, NULL, 10));
    }
    return httpd_resp_sendstr(req, live_utc() ? "OK" : "время не принято");
}

static esp_err_t f28stop_post(httpd_req_t *req)
{
    if (!web_auth_ok(req)) {
        return ESP_OK;
    }
    const char *err = fwup_stop();
    if (err != NULL) {
        httpd_resp_set_status(req, "409 Conflict");
        return httpd_resp_sendstr(req, err);
    }
    return httpd_resp_sendstr(req, "OK");
}

static esp_err_t f28_get(httpd_req_t *req)
{
    static const char *const st[] = {"idle", "run", "ok", "fail"};
    char          q[16], v[4];
    fwup_status_t f;

    /* Версию спрашиваем только без сеанса: ответ на 333 ушёл бы и в
     * Motor_Control, а тот его не просил. */
    if (httpd_req_get_url_query_str(req, q, sizeof q) == ESP_OK &&
        httpd_query_key_value(q, "ask", v, sizeof v) == ESP_OK && v[0] == '1' &&
        !fwup_busy() && !live_session_open()) {
        fwup_ask_version();
    }
    fwup_status(&f);

    json_begin(req);
    out(req, "{\"img\":%s", f.img ? "true" : "false");
    if (f.img) {
        out(req, ",\"name\":");
        out_str(req, f.name);
        out(req, ",\"ftime\":%lu,\"utime\":%lu,\"bytes\":%lu,\"segs\":%lu,\"entry\":%lu",
            (unsigned long)f.fileTime, (unsigned long)f.upTime, (unsigned long)f.bytes,
            (unsigned long)f.segs, (unsigned long)f.entry);
    }
    out(req, ",\"state\":\"%s\",\"pct\":%d,\"attempt\":%d,\"ms\":%lu,\"msg\":", st[f.state],
        f.pct, f.attempt, (unsigned long)f.elapsed);
    out_str(req, f.msg);
    out(req, ",\"up\":%lu,\"total\":%lu,\"ph\":[", (unsigned long)f.upMs, (unsigned long)f.totalMs);
    for (int i = 0; i < FWUP_PHASES; i++) {
        out(req, "%s%lu", i ? "," : "", (unsigned long)f.ph[i]);
    }
    out(req, "]");
    out(req, ",\"inBoot\":%s,\"session\":%s,\"devId\":", f.inBoot ? "true" : "false",
        live_session_open() ? "true" : "false");
    out_str(req, f.devId);
    out(req, ",\"devBuild\":");
    out_str(req, f.devBuild);
    out(req, "}");
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
        {.uri = "/api/ctl", .method = HTTP_POST, .handler = ctl_post},
        {.uri = "/tables.txt", .method = HTTP_GET, .handler = tables_get},
        {.uri = "/api/tbl", .method = HTTP_POST, .handler = tbl_post},
        {.uri = "/api/tblacks", .method = HTTP_GET, .handler = tblacks_get},
        {.uri = "/tables", .method = HTTP_POST, .handler = tables_post},
        {.uri = "/api/tblsrc", .method = HTTP_GET, .handler = tblsrc_get},
        {.uri = "/api/f28img", .method = HTTP_POST, .handler = f28img_post},
        {.uri = "/api/f28go", .method = HTTP_POST, .handler = f28go_post},
        {.uri = "/api/f28stop", .method = HTTP_POST, .handler = f28stop_post},
        {.uri = "/api/time", .method = HTTP_POST, .handler = time_post},
        {.uri = "/api/f28", .method = HTTP_GET, .handler = f28_get},
    };
    for (size_t i = 0; i < sizeof u / sizeof u[0]; i++) {
        httpd_register_uri_handler(srv, &u[i]);
    }
}

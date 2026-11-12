/*
 * tables.c
 *
 * Таблицы Motor_Control на странице. Что и зачем - в tables.h.
 */
#include <stdlib.h>
#include <string.h>

#include "freertos/FreeRTOS.h"

#include "esp_log.h"
#include "esp_partition.h"
#include "esp_rom_crc.h"
#include "esp_timer.h"

#include "live.h"
#include "spi_link.h"
#include "tables.h"

static const char *TAG = "tables";

extern const char tables_start[] asm("_binary_tables_txt_start");
extern const char tables_end[] asm("_binary_tables_txt_end");

#define T_MAX 160 /* разных команд во всех таблицах - 106 на 12.11.2026 */

typedef struct {
    uint16_t cmd;
    int16_t  min, max;
    int16_t  val;   /* последний ответ прибора */
    uint16_t flag;
    int64_t  t;     /* когда; 0 - ответа не было */
} trow_t;

static trow_t      s_row[T_MAX];
static int         s_n;
static portMUX_TYPE s_mux = portMUX_INITIALIZER_UNLOCKED;

static trow_t *find(uint16_t cmd)
{
    for (int i = 0; i < s_n; i++) {
        if (s_row[i].cmd == cmd) {
            return &s_row[i];
        }
    }
    return NULL;
}

/* «Имя = Мин,Макс,команда,...» - разбор хвоста после '='. */
static void add_line(const char *p, const char *end)
{
    long v[3];
    for (int k = 0; k < 3; k++) {
        while (p < end && (*p == ' ' || *p == '\t')) {
            p++;
        }
        char *q;
        v[k] = strtol(p, &q, 10);
        if (q == p) {
            return;
        }
        p = q;
        while (p < end && (*p == ' ' || *p == '\t')) {
            p++;
        }
        if (k < 2) {
            if (p >= end || *p != ',') {
                return;
            }
            p++;
        }
    }
    if (v[2] <= 0 || v[2] > 0xFFFF) {
        return;
    }
    trow_t *r = find((uint16_t)v[2]);
    if (r == NULL) {
        if (s_n >= T_MAX) {
            return;
        }
        r      = &s_row[s_n++];
        r->cmd = (uint16_t)v[2];
        r->min = (int16_t)v[0];
        r->max = (int16_t)v[1];
        return;
    }
    /* Та же команда в двух таблицах («Запись» у паспорта и у регуляторов) -
     * пределы шире из двух. */
    if (v[0] < r->min) r->min = (int16_t)v[0];
    if (v[1] > r->max) r->max = (int16_t)v[1];
}

/*---------------------------------------------------------------------------
 * Откуда таблицы. Присланные с ПК (Motor_Control, «Отправить таблицы в
 * WiFi-мост») лежат во флеше, в разделе bbox по смещению T_OFF (раскладка -
 * bbox.c): заголовок 16 байт - метка, длина, CRC-32 текста, UTC отправки, -
 * и текст. Читаются прямо из флеша, через отображение в адреса (mmap), в ОЗУ
 * не копируются. Нет их или не сошлась CRC - вшитые при сборке.
 *---------------------------------------------------------------------------*/
#define T_OFF   0xE0000u
#define T_SIZE  0x10000u
#define T_MAGIC 0x5437354Bu /* "K57T" */

typedef struct {
    uint32_t magic, len, crc, utc;
} thdr_t;

static const esp_partition_t *s_part;
static esp_partition_mmap_handle_t s_map;
static bool        s_mapped;
static const char *s_text;
static int         s_len;
static bool        s_fromPc;
static uint32_t    s_pcUtc;

static void parse(const char *p, const char *end);

static void load(void)
{
    s_text   = tables_start;
    s_len    = (int)(tables_end - tables_start - 1);
    s_fromPc = false;
    s_pcUtc  = 0;

    if (s_part == NULL) {
        return;
    }
    const void *m;
    if (esp_partition_mmap(s_part, T_OFF, T_SIZE, ESP_PARTITION_MMAP_DATA, &m, &s_map) != ESP_OK) {
        return;
    }
    s_mapped        = true;
    const thdr_t *h = m;
    const char   *t = (const char *)m + sizeof(thdr_t);
    if (h->magic == T_MAGIC && h->len > 0 && h->len <= T_SIZE - sizeof(thdr_t) &&
        h->crc == esp_rom_crc32_le(0, (const uint8_t *)t, h->len)) {
        s_text   = t;
        s_len    = (int)h->len;
        s_fromPc = true;
        s_pcUtc  = h->utc;
    }
}

static void unload(void)
{
    s_text = tables_start; /* пока флеш переписывается - вшитые */
    s_len  = (int)(tables_end - tables_start - 1);
    if (s_mapped) {
        esp_partition_munmap(s_map);
        s_mapped = false;
    }
}

void tables_init(void)
{
    s_part = esp_partition_find_first(ESP_PARTITION_TYPE_DATA, 0x40, "bbox");
    if (s_part != NULL && s_part->size < T_OFF + T_SIZE) {
        s_part = NULL;
    }
    load();
    s_n = 0;
    parse(s_text, s_text + s_len);
    ESP_LOGI(TAG, "таблицы Motor_Control: %s, %d байт, %d команд",
             s_fromPc ? "присланные с ПК" : "вшитые при сборке", s_len, s_n);
}

/* Приём новых таблиц (web_page.c, POST /tables). Стирание 64 кБ - около
 * полусекунды, обмен с прибором на это время прерывается, как у чёрного ящика. */
static uint32_t s_wOff, s_wCrc, s_wUtc;

bool tables_store_begin(uint32_t len, uint32_t utc)
{
    if (s_part == NULL || len == 0 || len > T_SIZE - sizeof(thdr_t)) {
        return false;
    }
    unload();
    if (esp_partition_erase_range(s_part, T_OFF, T_SIZE) != ESP_OK) {
        return false;
    }
    s_wOff = sizeof(thdr_t);
    s_wCrc = 0;
    s_wUtc = utc;
    return true;
}

bool tables_store_write(const void *data, uint32_t len)
{
    if (s_wOff + len > T_SIZE) {
        return false;
    }
    if (esp_partition_write(s_part, T_OFF + s_wOff, data, len) != ESP_OK) {
        return false;
    }
    s_wCrc = esp_rom_crc32_le(s_wCrc, data, len);
    s_wOff += len;
    return true;
}

bool tables_store_end(void)
{
    thdr_t h = {.magic = T_MAGIC, .len = s_wOff - sizeof(thdr_t), .crc = s_wCrc, .utc = s_wUtc};
    bool   ok = esp_partition_write(s_part, T_OFF, &h, sizeof h) == ESP_OK;
    tables_init(); /* сразу в работу: и страница, и список разрешённых команд */
    return ok && s_fromPc;
}

void tables_store_clear(void)
{
    if (s_part != NULL) {
        unload();
        esp_partition_erase_range(s_part, T_OFF, T_SIZE);
    }
    tables_init();
}

bool tables_source(uint32_t *utc)
{
    *utc = s_pcUtc;
    return s_fromPc;
}

static void parse(const char *p, const char *end)
{
    bool active = false;

    while (p < end) {
        const char *e = memchr(p, '\n', end - p);
        if (e == NULL) {
            e = end;
        }
        const char *s = p;
        while (s < e && (*s == ' ' || *s == '\t' || (unsigned char)*s == 0xEF ||
                         (unsigned char)*s == 0xBB || (unsigned char)*s == 0xBF)) {
            s++; /* отступ и метка UTF-8 в начале файла */
        }
        if (e - s >= 8 && memcmp(s, "[Active]", 8) == 0) {
            active = true;
        } else if (s < e && *s == '[') {
            active = false; /* [Hidden], [Settings] */
        } else if (e - s >= 3 && memcmp(s, "###", 3) == 0) {
            active = false; /* следующий файл */
        } else if (active && !(e - s >= 2 && s[0] == '/' && s[1] == '/')) {
            const char *eq = memchr(s, '=', e - s);
            if (eq != NULL) {
                add_line(eq + 1, e);
            }
        }
        p = e + 1;
    }
}

void tables_frame(uint16_t id, const uint8_t *data, uint8_t len)
{
    if (id != 1 || len < 8) {
        return;
    }
    uint16_t cmd = (uint16_t)(data[0] | (data[1] << 8));
    trow_t  *r   = find(cmd);
    if (r == NULL) {
        return;
    }
    portENTER_CRITICAL(&s_mux);
    r->val  = (int16_t)(data[2] | (data[3] << 8));
    r->flag = (uint16_t)(data[6] | (data[7] << 8));
    r->t    = esp_timer_get_time();
    portEXIT_CRITICAL(&s_mux);
}

const char *tables_cmd(uint16_t cmd, char op, int value, int flag)
{
    trow_t *r = find(cmd);
    if (r == NULL) {
        return "такой команды нет в таблицах";
    }
    if (live_session_open()) {
        return "открыт сеанс Motor_Control - управляет он";
    }
    if (op == 'w' && (value < r->min || value > r->max)) {
        return "значение вне пределов таблицы";
    }
    if (value < -32768 || value > 65535) {
        return "значение не влезает в слово";
    }
    uint16_t d1 = (uint16_t)value;
    bool     ok;
    switch (op) {
    case 'r': ok = spi_link_send_cmd(cmd, d1, 0, 0); break;
    case 'w': ok = spi_link_send_cmd(cmd, d1, 3333, 0); break;
    case 'm': ok = spi_link_send_cmd(cmd, d1, 5555, flag ? 1 : 0); break;
    default:  return "нет такого действия";
    }
    return ok ? NULL : "очередь к прибору полна";
}

void tables_acks(void (*cb)(uint16_t cmd, int16_t val, uint16_t flag, uint32_t age, void *ctx), void *ctx)
{
    int64_t now = esp_timer_get_time();
    for (int i = 0; i < s_n; i++) {
        portENTER_CRITICAL(&s_mux);
        trow_t r = s_row[i];
        portEXIT_CRITICAL(&s_mux);
        if (r.t != 0) {
            cb(r.cmd, r.val, r.flag, (uint32_t)((now - r.t) / 1000), ctx);
        }
    }
}

const char *tables_text(int *len)
{
    *len = s_len;
    return s_text;
}

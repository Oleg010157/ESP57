/*
 * bstore.c
 *
 * Журнал событий и копия настройки прибора. Что и зачем - в bstore.h.
 *
 * Строка журнала - 16 байт: номер, UTC, код, сессия, счётчик, CRC-16 первых
 * 14 байт. Счётчик 0 - само событие; N > 0 - «код повторился ещё N раз за
 * минуту после события». Стёртая строка - номер 0xFFFFFFFF.
 *
 * Пишет журнал и NVS только своя задача: задача SPI сеть и флеш не ждёт.
 * Чтение журнала пультом и запись идут под одним замком: иначе пульт мог бы
 * прочитать строку, которую в этот момент программирует флеш.
 */
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "freertos/semphr.h"
#include "freertos/task.h"

#include "esp_log.h"
#include "esp_partition.h"
#include "esp_timer.h"
#include "nvs.h"

#include "bstore.h"
#include "net_link.h"
#include "net_proto.h"
#include "spi_link.h"

static const char *TAG = "bstore";

#define J_OFFSET      0xF0000u /* в разделе bbox: после чёрного ящика и таблиц (bbox.c) */
#define J_SECTOR      4096u
#define J_SECTORS     16u
#define J_PER_SECTOR  (J_SECTOR / 16u)
#define J_FOLD_MS     60000u   /* один код - не чаще раза в минуту */
#define J_CODES       100u     /* ERR_BUFF_SIZE прибора: кодов больше нет */

#define CAL_MAX_WORDS 64u
#define CAL_HDR_WORDS 12u

#define BST_CMD_SEND  341u     /* прибору: прислать копию настройки */

typedef struct __attribute__((packed)) {
    uint32_t seq;
    uint32_t utc;
    uint16_t code;
    uint16_t session;
    uint16_t count;
    uint16_t crc;
} jrec_t;

_Static_assert(sizeof(jrec_t) == 16, "строка журнала - 16 байт");

typedef struct {
    uint16_t code;
    uint16_t session;
    uint32_t utc;
} jev_t;

/* Свёртка повторов: по коду - когда записан и сколько повторов с тех пор. */
typedef struct {
    uint32_t ms;      /* время моста, когда строка записана; 0 - не было */
    uint16_t repeats;
    uint16_t session;
    uint32_t utc;     /* UTC последнего повтора */
} jfold_t;

static const esp_partition_t *s_part;
static SemaphoreHandle_t      s_lock;
static QueueHandle_t          s_q;
static TaskHandle_t           s_task;

static jfold_t  s_fold[J_CODES];
static uint32_t s_seq = 1;
static uint32_t s_sector, s_slot;
static uint16_t s_rows_in[J_SECTORS];

/* Один сектор журнала - общий для jscan и send_journal, только под s_lock:
 * 4 кБ ОЗУ здесь отнимаются у кольца чёрного ящика. */
static jrec_t s_buf[J_PER_SECTOR];

static bstore_stat_t s_stat;

/* Копия настройки: собирается в задаче SPI, в NVS - своя задача. */
static uint16_t      s_cal_hdr[CAL_HDR_WORDS];
static uint16_t      s_cal_w[CAL_MAX_WORDS];
static uint64_t      s_cal_got;
static uint16_t      s_cal_n;
static uint16_t      s_pend[CAL_HDR_WORDS + CAL_MAX_WORDS];
static volatile bool s_pend_ready;

/*---------------------------------------------------------------------------*/
static uint16_t crc16(const uint8_t *p, int n)
{
    uint16_t crc = 0xFFFF;
    while (n-- > 0) {
        crc ^= (uint16_t)(*p++ << 8);
        for (int b = 0; b < 8; b++) {
            crc = (crc & 0x8000) ? (uint16_t)((crc << 1) ^ 0x1021) : (uint16_t)(crc << 1);
        }
    }
    return crc;
}

static bool rec_ok(const jrec_t *r)
{
    return r->seq != 0xFFFFFFFFu && r->crc == crc16((const uint8_t *)r, 14);
}

static uint32_t now_ms(void)
{
    uint32_t ms = (uint32_t)(esp_timer_get_time() / 1000);
    return ms == 0 ? 1 : ms; /* 0 в jfold_t значит «не было» */
}

/*---------------------------------------------------------------------------
 * Журнал во флеше - своя задача
 *---------------------------------------------------------------------------*/
static uint32_t sec_addr(uint32_t s)
{
    return J_OFFSET + s * J_SECTOR;
}

static void jwrite(uint16_t code, uint32_t utc, uint16_t session, uint16_t count)
{
    jrec_t r = {
        .seq = s_seq, .utc = utc, .code = code, .session = session, .count = count,
    };
    r.crc = crc16((const uint8_t *)&r, 14);

    xSemaphoreTake(s_lock, portMAX_DELAY);
    if (s_slot >= J_PER_SECTOR) {
        /* Сектор кончился - следующий стереть. Это 4 кБ, около 45 мс, раз
         * на 256 строк: задача SPI постоит, прибор пропустит пару посылок. */
        s_sector = (s_sector + 1) % J_SECTORS;
        esp_partition_erase_range(s_part, sec_addr(s_sector), J_SECTOR);
        s_stat.rows -= s_rows_in[s_sector];
        s_rows_in[s_sector] = 0;
        s_slot = 0;
    }
    esp_partition_write(s_part, sec_addr(s_sector) + s_slot * 16u, &r, sizeof r);
    s_slot++;
    s_rows_in[s_sector]++;
    s_stat.rows++;
    xSemaphoreGive(s_lock);

    s_seq++;
    s_stat.written++;
}

static void jevent(const jev_t *e)
{
    if (e->code >= J_CODES) {
        return;
    }
    jfold_t *f  = &s_fold[e->code];
    uint32_t ms = now_ms();

    if (f->ms != 0 && ms - f->ms < J_FOLD_MS) {
        f->repeats++;
        f->utc     = e->utc;
        f->session = e->session;
        s_stat.folded++;
        return;
    }
    if (f->repeats > 0) {
        jwrite(e->code, f->utc, f->session, f->repeats);
    }
    jwrite(e->code, e->utc, e->session, 0);
    f->ms      = ms;
    f->repeats = 0;
}

/* Минута после строки кончилась, а повторы были - сводка. */
static void jflush(void)
{
    uint32_t ms = now_ms();
    for (uint32_t c = 0; c < J_CODES; c++) {
        jfold_t *f = &s_fold[c];
        if (f->repeats > 0 && ms - f->ms >= J_FOLD_MS) {
            jwrite((uint16_t)c, f->utc, f->session, f->repeats);
            f->repeats = 0;
        }
    }
}

/* Где продолжать: сектор с наибольшим номером строки, в нём - первое
 * пустое место. Заодно счёт строк. */
static void jscan(void)
{
    jrec_t  *buf  = s_buf;
    uint32_t best = 0;
    bool     any  = false;

    xSemaphoreTake(s_lock, portMAX_DELAY);
    s_stat.rows = 0;
    for (uint32_t s = 0; s < J_SECTORS; s++) {
        esp_partition_read(s_part, sec_addr(s), buf, sizeof s_buf);
        s_rows_in[s] = 0;
        for (uint32_t i = 0; i < J_PER_SECTOR; i++) {
            if (!rec_ok(&buf[i])) {
                continue;
            }
            s_rows_in[s]++;
            if (!any || (int32_t)(buf[i].seq - best) > 0) {
                best     = buf[i].seq;
                s_sector = s;
                s_slot   = i + 1;
                any      = true;
            }
        }
        s_stat.rows += s_rows_in[s];
    }
    if (!any) {
        s_sector = 0;
        s_slot   = 0;
        esp_partition_erase_range(s_part, sec_addr(0), J_SECTOR);
    } else {
        s_seq = best + 1;
        /* Дальше по сектору могут быть остатки до сбоя питания: место за
         * последней строкой должно быть стёртым, иначе запись не ляжет. */
        esp_partition_read(s_part, sec_addr(s_sector), buf, sizeof s_buf);
        for (uint32_t i = s_slot; i < J_PER_SECTOR; i++) {
            if (buf[i].seq != 0xFFFFFFFFu) {
                s_slot = J_PER_SECTOR; /* следующая запись начнёт новый сектор */
                break;
            }
        }
    }
    xSemaphoreGive(s_lock);
}

static void jclear(void)
{
    xSemaphoreTake(s_lock, portMAX_DELAY);
    esp_partition_erase_range(s_part, J_OFFSET, J_SECTOR * J_SECTORS);
    memset(s_rows_in, 0, sizeof s_rows_in);
    s_stat.rows = 0;
    s_sector    = 0;
    s_slot      = 0;
    xSemaphoreGive(s_lock);
    memset(s_fold, 0, sizeof s_fold);
}

/*---------------------------------------------------------------------------
 * Копия настройки в NVS - своя задача
 *---------------------------------------------------------------------------*/
static void save_copy(void)
{
    uint16_t n     = s_pend[1];
    size_t   bytes = (CAL_HDR_WORDS + n) * 2u;
    nvs_handle_t h;

    if (nvs_open("k57", NVS_READWRITE, &h) != ESP_OK) {
        return;
    }
    /* Пишем, только если изменилась сама запись: прибор присылает её при
     * каждом включении, а NVS изнашивать незачем. Заголовок (время, причина)
     * не сравниваем. */
    uint16_t old[CAL_HDR_WORDS + CAL_MAX_WORDS];
    size_t   len = sizeof old;
    bool same = nvs_get_blob(h, "cal", old, &len) == ESP_OK && len == bytes &&
                memcmp(old + CAL_HDR_WORDS, s_pend + CAL_HDR_WORDS, n * 2u) == 0;
    if (!same) {
        nvs_set_blob(h, "cal", s_pend, bytes);
        nvs_commit(h);
        ESP_LOGI(TAG, "копия настройки обновлена: %u слов", n);
    }
    nvs_close(h);
    s_stat.haveCopy = true;
}

static bool have_copy(void)
{
    nvs_handle_t h;
    size_t       len = 0;
    bool         ok  = false;
    if (nvs_open("k57", NVS_READONLY, &h) == ESP_OK) {
        ok = nvs_get_blob(h, "cal", NULL, &len) == ESP_OK && len > CAL_HDR_WORDS * 2u;
        nvs_close(h);
    }
    return ok;
}

/*---------------------------------------------------------------------------*/
static void store_task(void *arg)
{
    (void)arg;
    bool asked = false;

    if (s_part != NULL) {
        jscan();
        ESP_LOGI(TAG, "журнал: строк %lu, следующая №%lu", (unsigned long)s_stat.rows,
                 (unsigned long)s_seq);
    }
    s_stat.haveCopy = have_copy();

    for (;;) {
        jev_t e;
        if (xQueueReceive(s_q, &e, pdMS_TO_TICKS(1000)) == pdTRUE) {
            if (s_part != NULL) {
                jevent(&e);
            }
        } else if (s_part != NULL) {
            jflush();
        }

        if (s_pend_ready) {
            save_copy();
            s_pend_ready = false;
        }

        /* Поднялись, а прибор уже работал: копию сам он больше не пришлёт -
         * попросим, как только обмен пошёл. */
        if (!asked) {
            spi_link_stat_t ss;
            spi_link_get_stat(&ss);
            if (ss.packets > 50) {
                spi_link_send_cmd(BST_CMD_SEND, 0, 0, 0);
                asked = true;
            }
        }
    }
}

/*---------------------------------------------------------------------------
 * От прибора - задача SPI
 *---------------------------------------------------------------------------*/
static uint16_t le16(const uint8_t *p)
{
    return (uint16_t)(p[0] | (p[1] << 8));
}

void bstore_frame(uint16_t id, const uint8_t *data, uint8_t len)
{
    switch (id) {
    case BST_ID_EVENT:
        if (len >= 8) {
            jev_t e = {
                .code    = le16(data),
                .utc     = (uint32_t)le16(data + 2) | ((uint32_t)le16(data + 4) << 16),
                .session = le16(data + 6),
            };
            s_stat.events++;
            if (s_q == NULL || xQueueSend(s_q, &e, 0) != pdTRUE) {
                s_stat.lost++;
            }
        }
        break;

    case BST_ID_CAL_HDR:
        if (len >= CAL_HDR_WORDS * 2) {
            for (int i = 0; i < (int)CAL_HDR_WORDS; i++) {
                s_cal_hdr[i] = le16(data + 2 * i);
            }
            s_cal_n   = s_cal_hdr[1];
            s_cal_got = 0;
            /* Пустая или чужая у прибора - копию не трогаем (s_cal_hdr[2] -
             * итог calib_peek: 0 - запись годная). */
            if (s_cal_hdr[2] != 0 || s_cal_n == 0 || s_cal_n > CAL_MAX_WORDS) {
                s_cal_n = 0;
            }
        }
        break;

    case BST_ID_CAL_DATA: {
        if (s_cal_n == 0 || len < 4) {
            break;
        }
        uint16_t off = le16(data);
        int      k   = len / 2 - 1;
        for (int i = 0; i < k && off + i < s_cal_n; i++) {
            s_cal_w[off + i] = le16(data + 2 + 2 * i);
            s_cal_got |= 1ull << (off + i);
        }
        uint64_t all = s_cal_n >= 64 ? ~0ull : (1ull << s_cal_n) - 1;
        if (s_cal_got == all) {
            uint16_t sum = 0;
            for (int i = 0; i < s_cal_n; i++) {
                sum = (uint16_t)(sum + s_cal_w[i]);
            }
            if (sum == s_cal_hdr[11] && !s_pend_ready) {
                memcpy(s_pend, s_cal_hdr, sizeof s_cal_hdr);
                memcpy(s_pend + CAL_HDR_WORDS, s_cal_w, s_cal_n * 2u);
                s_pend_ready = true;
                s_stat.copies++;
            } else if (sum != s_cal_hdr[11]) {
                s_stat.copyBad++;
            }
            s_cal_n = 0;
        }
        break;
    }

    default:
        break;
    }
}

/*---------------------------------------------------------------------------
 * Запросы пульта - задача TCP
 *---------------------------------------------------------------------------*/
static uint8_t s_out[1400];
static int     s_out_len;

static void out_flush(void)
{
    if (s_out_len > 0) {
        net_link_tcp_send(s_out, s_out_len);
        s_out_len = 0;
    }
}

static void out_rec(uint16_t id, const void *data, uint8_t len)
{
    if (s_out_len + K57_REC_HDR + len > (int)sizeof s_out) {
        out_flush();
    }
    uint8_t *p = s_out + s_out_len;
    p[0] = K57_REC_SYNC;
    p[1] = len;
    p[2] = (uint8_t)id;
    p[3] = (uint8_t)(id >> 8);
    memcpy(p + K57_REC_HDR, data, len);
    s_out_len += K57_REC_HDR + len;
}

static void send_end(uint32_t rows)
{
    uint8_t end[16];
    uint32_t cap = J_SECTORS * J_PER_SECTOR;
    memcpy(end, &rows, 4);
    memcpy(end + 4, &cap, 4);
    memcpy(end + 8, &s_stat.lost, 4);
    memcpy(end + 12, &s_stat.folded, 4);
    out_rec(BST_ID_JEND, end, sizeof end);
    out_flush();
}

/* Журнал от старых строк к новым: сектор за текущим - самый старый. */
uint32_t bstore_walk(uint32_t skip, void (*cb)(const bstore_row_t *r, void *ctx), void *ctx)
{
    jrec_t  *buf  = s_buf; /* под s_lock, как и jscan */
    uint32_t rows = 0;

    if (s_part == NULL) {
        return 0;
    }
    xSemaphoreTake(s_lock, portMAX_DELAY);
    for (uint32_t k = 1; k <= J_SECTORS; k++) {
        uint32_t s = (s_sector + k) % J_SECTORS;
        esp_partition_read(s_part, sec_addr(s), buf, sizeof s_buf);
        for (uint32_t i = 0; i < J_PER_SECTOR; i++) {
            if (!rec_ok(&buf[i])) {
                continue;
            }
            if (rows++ < skip) {
                continue;
            }
            bstore_row_t r = {
                .seq = buf[i].seq, .utc = buf[i].utc, .code = buf[i].code,
                .session = buf[i].session, .count = buf[i].count,
            };
            cb(&r, ctx);
        }
    }
    xSemaphoreGive(s_lock);
    return rows;
}

typedef struct {
    uint8_t pack[48];
    int     inPack;
} jsend_t;

static void jsend_row(const bstore_row_t *r, void *ctx)
{
    jsend_t *j = ctx;
    jrec_t   rec = {
        .seq = r->seq, .utc = r->utc, .code = r->code, .session = r->session, .count = r->count,
    };
    rec.crc = crc16((const uint8_t *)&rec, 14);
    memcpy(j->pack + 16 * j->inPack, &rec, 16);
    if (++j->inPack == 3) {
        out_rec(BST_ID_JRECS, j->pack, 48);
        j->inPack = 0;
    }
}

static void send_journal(void)
{
    jsend_t  j = {.inPack = 0};
    uint32_t rows = bstore_walk(0, jsend_row, &j);

    if (j.inPack > 0) {
        out_rec(BST_ID_JRECS, j.pack, (uint8_t)(16 * j.inPack));
    }
    send_end(rows);
}

static void send_copy(void)
{
    uint16_t     buf[CAL_HDR_WORDS + CAL_MAX_WORDS];
    size_t       len = sizeof buf;
    nvs_handle_t h;
    uint8_t      have = 0;

    if (nvs_open("k57", NVS_READONLY, &h) == ESP_OK) {
        if (nvs_get_blob(h, "cal", buf, &len) == ESP_OK && len > CAL_HDR_WORDS * 2u) {
            have = 1;
        }
        nvs_close(h);
    }
    if (have) {
        uint16_t n = buf[1];
        out_rec(BST_ID_COPY_HDR, buf, CAL_HDR_WORDS * 2);
        for (uint16_t off = 0; off < n; off += 30) {
            uint16_t piece[31];
            uint16_t k = (uint16_t)(n - off < 30 ? n - off : 30);
            piece[0] = off;
            memcpy(piece + 1, buf + CAL_HDR_WORDS + off, k * 2u);
            out_rec(BST_ID_COPY_DATA, piece, (uint8_t)(2 + 2 * k));
        }
    }
    out_rec(BST_ID_COPY_END, &have, 1);
    out_flush();
}

void bstore_request(uint16_t id, const uint8_t *data, uint8_t len)
{
    (void)data;
    (void)len;
    switch (id) {
    case BST_ID_JREAD:
        send_journal();
        break;
    case BST_ID_JCLEAR:
        if (s_part != NULL) {
            jclear();
        }
        send_end(0);
        break;
    case BST_ID_COPY:
        send_copy();
        break;
    default:
        break;
    }
}

/*---------------------------------------------------------------------------*/
void bstore_start(void)
{
    s_part = esp_partition_find_first(ESP_PARTITION_TYPE_DATA, 0x40, "bbox");
    if (s_part == NULL || s_part->size < J_OFFSET + J_SECTOR * J_SECTORS) {
        s_part         = NULL;
        s_stat.noFlash = true;
        ESP_LOGW(TAG, "раздела bbox нет - журнал выключен, копия настройки - только в NVS");
    }
    s_lock = xSemaphoreCreateMutex();
    s_q    = xQueueCreate(16, sizeof(jev_t));
    xTaskCreatePinnedToCore(store_task, "bstore", 3072, NULL, 2, &s_task, 0);
}

void bstore_get_stat(bstore_stat_t *out)
{
    *out = s_stat;
}

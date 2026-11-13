/*
 * fwup.c
 *
 * Прошивка прибора через мост. Зачем и формат образа - в fwup.h.
 *
 * Обмен с загрузчиком - пошагово, как его ведёт Motor_Control по CAN
 * (Bootloader/Bootloader.cs): связь, стирание, сегменты, сверка суммы, пуск.
 * Отличие одно - отказ не останавливает работу: образ лежит в мосте, прибор
 * после отказа ждёт в загрузчике команду стирания (CAN_Bootloader_Protocol.md,
 * «Маркеры и отказы»), и мост сам начинает заново, до трёх попыток.
 */
#include <stdarg.h>
#include <stdio.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "freertos/task.h"

#include "esp_log.h"
#include "esp_ota_ops.h"
#include "esp_partition.h"
#include "esp_rom_crc.h"
#include "esp_timer.h"

#include "fwup.h"
#include "live.h"
#include "spi_link.h"
#include "web.h"

static const char *TAG = "fwup";

#define IMG_MAGIC 0x4637354Bu /* 'K57F' - образ от ПК */
#define REC_MAGIC 0x5337354Bu /* 'K57S' - запись моста: образ принят и проверен */
#define DATA_OFF  0x1000      /* образ - со второго сектора, в первом запись */
#define SECTOR    0x1000

#define APP_START 0x080000u /* BANK0, как APP_START_ADDR у прибора */
#define APP_END   0x0FFFFFu /* BANK3 */
#define SECT_WORDS 0x400u   /* сектор флеша прибора, слов */
#define BLK       64        /* кадр данных = запись флеша на 512 бит */
#define MAX_SEGS  256

/* Кадров данных в пути без квитанции. У прибора восемь мест под кадры
 * (boot_spi.c, BS_RXQ); посылка к нему держит шесть по 64 байта. */
#define WINDOW 6

/* Команды и квитанции - can_boot_protocol.h прибора. */
#define CMD_HANDSHAKE    0xCC01
#define ACK_HANDSHAKE    0x02CC
#define CMD_ERASE        0xCC02
#define ACK_ERASE        0x05CC
#define ACK_ERASE_RANGE  0x0DCC
#define CMD_LOAD         0xCC07
#define ACK_LOAD_READY   0x0ACC
#define ACK_BLOCK        0x06CC
#define ACK_LOAD         0x07CC
#define ACK_NOVERIFY     0x0ECC
#define CMD_VERIFY       0xCC08
#define ACK_VERIFY_READY 0x09CC
#define ACK_VERIFY       0x08CC
#define CMD_REBOOT       0xFE17
#define ACK_REBOOT_READY 0x17FE
#define ACK_REBOOT       0x18FE
#define MARK_ERASE_PCT   0xEB00
#define BOOT_SPI_ENTER   0xB007
#define BOOT_SPI_KEY     0x5350
#define CMD_ABORT        0xCC0A
#define ACK_ABORT        0x0BCC

typedef struct {
    uint32_t magic;
    uint16_t ver, hdr;
    uint32_t entry, nseg, bytes, ftime;
    char     name[40];
} img_hdr_t;
_Static_assert(sizeof(img_hdr_t) == 64, "заголовок образа - 64 байта");

typedef struct {
    uint32_t magic, len, crc, utc;
} rec_t;

typedef struct {
    uint8_t len;
    uint8_t d[BLK];
} txf_t;

typedef struct {
    uint32_t addr, bytes, off; /* off - данные в разделе */
} seg_t;

static const esp_partition_t *s_part;
static QueueHandle_t          s_rxq; /* слово 0..3 кадров 0x32 */
static QueueHandle_t          s_txq;
static TaskHandle_t           s_task;
static portMUX_TYPE           s_mux = portMUX_INITIALIZER_UNLOCKED;

static fwup_status_t s_st;      /* под s_mux */
static volatile bool s_storing;
static volatile bool s_running;
static int64_t       s_lastBoot; /* мкс: последний кадр 0x32 */
static int64_t       s_t0;
static int64_t       s_phT; /* мкс: конец прошлого шага */
static int64_t       s_upT0;

/* Шаг i закончен: его время - от конца прошлого. */
static void phase(int i)
{
    int64_t t = esp_timer_get_time();
    taskENTER_CRITICAL(&s_mux);
    s_st.ph[i] = (uint32_t)((t - s_phT) / 1000);
    taskEXIT_CRITICAL(&s_mux);
    s_phT = t;
}

static seg_t s_seg[MAX_SEGS];
static int   s_nseg;

/* Ответ на 333 - строки по символу в квитанциях 32000 (SendInfo, MBOX28.c):
 * имя, два описания кадров, __DATE__, __TIME__; 32001 - конец. */
#define VER_STRS 5
static char    s_vs[VER_STRS][33];
static int     s_vi = -1;
static volatile bool s_verDone;

static uint32_t now_ms(void)
{
    return (uint32_t)(esp_timer_get_time() / 1000);
}

static void say(int pct, const char *fmt, ...) __attribute__((format(printf, 2, 3)));
static void say(int pct, const char *fmt, ...)
{
    char    m[sizeof s_st.msg];
    va_list a;
    va_start(a, fmt);
    vsnprintf(m, sizeof m, fmt, a);
    va_end(a);
    taskENTER_CRITICAL(&s_mux);
    if (pct >= 0) {
        s_st.pct = pct;
    }
    memcpy(s_st.msg, m, sizeof m);
    taskEXIT_CRITICAL(&s_mux);
    ESP_LOGI(TAG, "%s", m);
}

static void set_state(fwup_state_t st)
{
    taskENTER_CRITICAL(&s_mux);
    s_st.state = st;
    taskEXIT_CRITICAL(&s_mux);
}

/*---------------------------------------------------------------------------
 * Кадры от прибора и к нему
 *---------------------------------------------------------------------------*/
static uint16_t le16(const uint8_t *p)
{
    return (uint16_t)(p[0] | (p[1] << 8));
}

void fwup_frame(uint16_t id, const uint8_t *data, uint8_t len)
{
    if (s_rxq == NULL) {
        return; /* SPI поднимается раньше fwup_init */
    }
    if (id == FWUP_ID_FROM_F28 && len >= 2) {
        uint16_t w[4] = {0};
        for (int i = 0; i < 4 && 2 * i + 1 < len; i++) {
            w[i] = le16(data + 2 * i);
        }
        s_lastBoot = esp_timer_get_time();
        xQueueSend(s_rxq, w, 0);
        return;
    }
    /* ACK: cmd, d1, d2, d3 (spi_link.c). Строки версии. */
    if (id == 1 && len == 8) {
        uint16_t cmd = le16(data), d2 = le16(data + 4), d3 = le16(data + 6);
        if (cmd == 32000) {
            if (d2 == 0) {
                s_vi++;
            }
            if (s_vi >= 0 && s_vi < VER_STRS && d2 < sizeof s_vs[0] - 1) {
                s_vs[s_vi][d2]     = (char)d3;
                s_vs[s_vi][d2 + 1] = 0;
            }
        } else if (cmd == 32001) {
            /* Кто бы ни спросил - мы или Motor_Control, - ответ годится. */
            if (s_vi >= VER_STRS - 1) {
                taskENTER_CRITICAL(&s_mux);
                snprintf(s_st.devId, sizeof s_st.devId, "%s", s_vs[0]);
                snprintf(s_st.devBuild, sizeof s_st.devBuild, "%.12s %.10s", s_vs[3], s_vs[4]);
                taskEXIT_CRITICAL(&s_mux);
                s_verDone = true;
            }
            s_vi = -1;
            memset(s_vs, 0, sizeof s_vs);
        }
    }
}

int fwup_tx_next(uint8_t *data, int room)
{
    txf_t f;
    if (s_txq == NULL || xQueuePeek(s_txq, &f, 0) != pdTRUE || f.len > room) {
        return 0;
    }
    xQueueReceive(s_txq, &f, 0);
    memcpy(data, f.d, f.len);
    return f.len;
}

/* Кадры к загрузчику забирает задача SPI - на каждой посылке прибора.
 * Посылок нет (в приборе программа без моста или он стоит) - очередь не
 * пустеет. Ждать без конца нельзя: 12.11.2026 так встала вся прошивка, и мост
 * отказывал во всём, даже в собственном обновлении. */
static volatile bool s_stalled;
static volatile bool s_cancel; /* «Остановить» - fwup_stop() */
static bool          s_erased; /* стирание уже начато - отменять нечего */
static const char    s_stallMsg[] =
    "прибор не обменивается с мостом по SPI: в нём нет ни загрузчика, ни программы с мостом. "
    "Прошейте его по CAN или JTAG";

static void send_frame(const uint8_t *d, int len)
{
    txf_t f = {.len = (uint8_t)len};
    memcpy(f.d, d, len);
    if (!s_stalled && xQueueSend(s_txq, &f, pdMS_TO_TICKS(2000)) != pdTRUE) {
        s_stalled = true;
    }
}

static void send4(uint16_t w0, uint16_t w1, uint16_t w2, uint16_t w3)
{
    uint16_t w[4] = {w0, w1, w2, w3};
    uint8_t  d[8];
    for (int i = 0; i < 4; i++) {
        d[2 * i]     = (uint8_t)w[i];
        d[2 * i + 1] = (uint8_t)(w[i] >> 8);
    }
    send_frame(d, 8);
}

static void drain(void)
{
    uint16_t w[4];
    while (xQueueReceive(s_rxq, w, 0) == pdTRUE) {
    }
}

static bool in_boot(void)
{
    /* Три секунды: в ожидании после отказа загрузчик шлёт 0xDEAD раз в 1,8 с. */
    return s_lastBoot != 0 && esp_timer_get_time() - s_lastBoot < 3000000;
}

/* Отказ загрузчика - маркеры 0xDEAD, 0xDEnn, 0xD0nn, 0xDFnn и «сумма не
 * сошлась». */
static bool is_fail(uint16_t w)
{
    uint16_t hi = w & 0xFF00;
    return hi == 0xDE00 || hi == 0xD000 || hi == 0xDF00 || w == ACK_NOVERIFY;
}

static uint16_t s_failWord;

/* Ждать квитанцию want. Ход стирания пишет в pct (от pct0 до pct1).
 * fails - считать маркеры отказа отказом: до первой квитанции шага в очереди
 * ещё может лежать 0xDEAD из ожидания, от которого прибор уже ушёл. */
static bool wait_word(uint16_t want, uint32_t ms, bool fails, int pct0, int pct1)
{
    uint32_t t = now_ms();
    uint16_t w[4];

    while (now_ms() - t < ms && !s_stalled && !s_cancel) {
        if (xQueueReceive(s_rxq, w, pdMS_TO_TICKS(50)) != pdTRUE) {
            continue;
        }
        if (w[0] == want) {
            return true;
        }
        if ((w[0] & 0xFF00) == MARK_ERASE_PCT && pct1 > pct0) {
            taskENTER_CRITICAL(&s_mux);
            s_st.pct = pct0 + (pct1 - pct0) * (w[0] & 0xFF) / 100;
            taskEXIT_CRITICAL(&s_mux);
            t = now_ms(); /* стирание идёт - ждём дальше */
        }
        if (fails && is_fail(w[0])) {
            s_failWord = w[0];
            return false;
        }
    }
    s_failWord = 0;
    return false;
}

/*---------------------------------------------------------------------------
 * Образ в мосте
 *---------------------------------------------------------------------------*/
static bool bridge_pending(void)
{
    const esp_partition_t *run = esp_ota_get_running_partition();
    esp_ota_img_states_t   st;
    return run != NULL && esp_ota_get_state_partition(run, &st) == ESP_OK &&
           st == ESP_OTA_IMG_PENDING_VERIFY;
}

static uint8_t s_buf[SECTOR]; /* общий: приём и проверка не идут разом */

/* Прочитать и проверить образ: запись моста, CRC, раскладку сегментов.
 * Заполняет s_seg. NULL - годен. */
static const char *check_image(img_hdr_t *h, rec_t *r)
{
    if (s_part == NULL) {
        return "нет места под образ";
    }
    if (esp_partition_read(s_part, 0, r, sizeof *r) != ESP_OK || r->magic != REC_MAGIC ||
        r->len < sizeof *h || r->len > s_part->size - DATA_OFF) {
        return "образа в мосте нет";
    }
    uint32_t crc = 0;
    for (uint32_t o = 0; o < r->len; o += SECTOR) {
        uint32_t n = r->len - o < SECTOR ? r->len - o : SECTOR;
        if (esp_partition_read(s_part, DATA_OFF + o, s_buf, n) != ESP_OK) {
            return "образ не читается";
        }
        crc = esp_rom_crc32_le(crc, s_buf, n);
    }
    if (crc != r->crc) {
        return "образ в мосте испорчен";
    }

    esp_partition_read(s_part, DATA_OFF, h, sizeof *h);
    if (h->magic != IMG_MAGIC || h->ver != 1 || h->hdr != sizeof *h ||
        h->bytes != r->len - sizeof *h) {
        return "не образ K57F или чужая версия формата";
    }
    if (h->entry < APP_START || h->entry > APP_END) {
        return "адрес входа вне BANK0..BANK3";
    }
    if (h->nseg == 0 || h->nseg > MAX_SEGS) {
        return "сегментов нет или слишком много";
    }

    uint32_t off = DATA_OFF + sizeof *h;
    uint32_t end = DATA_OFF + r->len;
    s_nseg = 0;
    for (uint32_t i = 0; i < h->nseg; i++) {
        uint32_t sh[2];
        if (off + 8 > end || esp_partition_read(s_part, off, sh, 8) != ESP_OK) {
            return "сегменты обрываются";
        }
        uint32_t a = sh[0], b = sh[1];
        /* Те же условия, что проверяет прибор (can_bootloader.c, заголовок
         * сегмента): не проверить здесь - узнать об этом уже после стирания. */
        if (a < APP_START || (a & 31) || b == 0 || (b % BLK) || a + b / 2 - 1 > APP_END ||
            off + 8 + b > end) {
            return "сегмент вне BANK0..BANK3 или не выровнен";
        }
        s_seg[s_nseg++] = (seg_t){.addr = a, .bytes = b, .off = off + 8};
        off += 8 + b;
    }
    if (off != end) {
        return "лишние байты после сегментов";
    }
    return NULL;
}

static void refresh_image_info(void)
{
    img_hdr_t h;
    rec_t     r;
    const char *bad = check_image(&h, &r);
    uint32_t    bytes = 0;
    for (int i = 0; bad == NULL && i < s_nseg; i++) {
        bytes += s_seg[i].bytes;
    }
    taskENTER_CRITICAL(&s_mux);
    s_st.img = bad == NULL;
    if (bad == NULL) {
        memcpy(s_st.name, h.name, sizeof h.name);
        s_st.name[sizeof h.name] = 0;
        s_st.fileTime = h.ftime;
        s_st.upTime   = r.utc;
        s_st.bytes    = bytes;
        s_st.segs     = h.nseg;
        s_st.entry    = h.entry;
    }
    taskEXIT_CRITICAL(&s_mux);
}

static uint32_t s_wOff, s_wLen, s_wCrc, s_wUtc, s_wErased;

const char *fwup_store_begin(uint32_t len, uint32_t utc)
{
    if (s_running || s_storing) {
        return "мост уже занят прошивкой прибора";
    }
    if (web_updating()) {
        return "идёт обновление самого моста";
    }
    if (bridge_pending()) {
        return "новая прошивка моста ещё не подтверждена - подождите 20 с";
    }
    if (s_part == NULL) {
        return "нет места под образ";
    }
    if (len < sizeof(img_hdr_t) || len > s_part->size - DATA_OFF) {
        return len < sizeof(img_hdr_t) ? "пустой образ" : "образ больше места в мосте";
    }
    /* Запись «принят» стирается первой: оборвись приём - старый образ не
     * сочтут годным вместе с новым хвостом. */
    if (esp_partition_erase_range(s_part, 0, SECTOR) != ESP_OK) {
        return "флеш моста не стирается";
    }
    s_storing = true;
    s_wOff = 0;
    s_wLen = len;
    s_wCrc = 0;
    s_wUtc = utc;
    s_wErased = DATA_OFF;
    s_upT0    = esp_timer_get_time();
    taskENTER_CRITICAL(&s_mux);
    s_st.img = false;
    taskEXIT_CRITICAL(&s_mux);
    return NULL;
}

bool fwup_store_write(const void *data, uint32_t len)
{
    if (!s_storing || s_wOff + len > s_wLen) {
        return false;
    }
    /* Стирать по мере записи, по сектору: стереть всё разом - обмен с
     * прибором встал бы одним куском на секунды (как у обновления моста). */
    while (DATA_OFF + s_wOff + len > s_wErased) {
        if (esp_partition_erase_range(s_part, s_wErased, SECTOR) != ESP_OK) {
            s_storing = false;
            return false;
        }
        s_wErased += SECTOR;
    }
    if (esp_partition_write(s_part, DATA_OFF + s_wOff, data, len) != ESP_OK) {
        s_storing = false;
        return false;
    }
    s_wCrc = esp_rom_crc32_le(s_wCrc, data, len);
    s_wOff += len;
    return true;
}

const char *fwup_store_end(void)
{
    if (!s_storing || s_wOff != s_wLen) {
        s_storing = false;
        return "образ пришёл не целиком";
    }
    taskENTER_CRITICAL(&s_mux);
    s_st.upMs = (uint32_t)((esp_timer_get_time() - s_upT0) / 1000);
    taskEXIT_CRITICAL(&s_mux);
    rec_t r = {.magic = REC_MAGIC, .len = s_wLen, .crc = s_wCrc, .utc = s_wUtc};
    bool  ok = esp_partition_write(s_part, 0, &r, sizeof r) == ESP_OK;
    s_storing = false;

    img_hdr_t h;
    const char *bad = ok ? check_image(&h, &r) : "запись не легла";
    if (bad != NULL) {
        esp_partition_erase_range(s_part, 0, SECTOR); /* негодный - не хранить */
    }
    refresh_image_info();
    return bad;
}

/*---------------------------------------------------------------------------
 * Прошивка
 *---------------------------------------------------------------------------*/
static uint32_t s_eraseLo, s_eraseHi, s_sum, s_entry;

/* Диапазон стирания и сумма - как у Motor_Control (Bootloader.cs): от начала
 * первого сегмента до конца последнего, по границам секторов; сумма - всех
 * байт, что уйдут в кадрах данных. */
static bool prepare(void)
{
    s_eraseLo = APP_END;
    s_eraseHi = APP_START;
    s_sum     = 0;
    for (int i = 0; i < s_nseg; i++) {
        uint32_t a = s_seg[i].addr, last = a + s_seg[i].bytes / 2 - 1;
        if (a < s_eraseLo) {
            s_eraseLo = a;
        }
        if (last > s_eraseHi) {
            s_eraseHi = last;
        }
        for (uint32_t o = 0; o < s_seg[i].bytes; o += SECTOR) {
            uint32_t n = s_seg[i].bytes - o < SECTOR ? s_seg[i].bytes - o : SECTOR;
            if (esp_partition_read(s_part, s_seg[i].off + o, s_buf, n) != ESP_OK) {
                return false;
            }
            for (uint32_t k = 0; k < n; k++) {
                s_sum += s_buf[k];
            }
        }
    }
    s_eraseLo &= ~(SECT_WORDS - 1);
    s_eraseHi |= SECT_WORDS - 1;
    if (s_eraseHi > APP_END) {
        s_eraseHi = APP_END;
    }
    return true;
}

static const char s_noAnswer[] = "прибор не отвечает";

/* Прибор ещё в приложении: ключи закрыты, сеанса нет. Кадр 140 - на
 * «только смотреть», если его сейчас никто не держит. */
static const char *check_device(void)
{
    live_state_t ls;
    for (int i = 0; i < 20; i++) {
        live_get(&ls);
        if (ls.age < 1500) {
            break;
        }
        if (i % 5 == 0) {
            spi_link_send_cmd(344, 0, 0, 0);
        }
        vTaskDelay(pdMS_TO_TICKS(100));
    }
    if (ls.age >= 1500) {
        return s_noAnswer;
    }
    if (ls.w[2] & 1) {
        return "открыт сеанс Motor_Control - нажмите в нём «Отключить»";
    }
    if (ls.w[14] & 1) {
        return "ключи открыты - сначала выключите инвертор";
    }
    return NULL;
}

/* Один заход: от стирания до квитанции пуска. NULL - прошито и запущено. */
static const char *attempt(bool enter)
{
    static char why[64];

    /* Повтор: время неудачной попытки в шаги не идёт, только во всю прошивку. */
    s_phT = esp_timer_get_time();
    xQueueReset(s_txq);
    s_stalled = false;
    if (enter) {
        say(1, "зову прибор в загрузчик");
        drain();
        /* С повтором: посылку с битой CRC прибор выбрасывает целиком. Лишний
         * кадр загрузчик пропустит - он ждёт связи и смотрит только на неё. */
        uint32_t t = now_ms();
        for (int i = 0; !in_boot() && !s_cancel && now_ms() - t < 3000; i++) {
            if (i % 25 == 0) {
                send4(BOOT_SPI_ENTER, BOOT_SPI_KEY, 0, 0);
            }
            vTaskDelay(pdMS_TO_TICKS(20));
        }
        if (!in_boot()) {
            return "прибор не ушёл в загрузчик";
        }
    }

    /* Связь. Повторы безвредны: после ответа прибор ждёт стирания и лишние
     * кадры просто пропускает. Не ответил - он в ожидании после отказа, и
     * сразу стирание. */
    say(2, "связь с загрузчиком");
    bool hs = false;
    for (int i = 0; i < 6 && !hs; i++) {
        send4(CMD_HANDSHAKE, 0, 0, 0);
        hs = wait_word(ACK_HANDSHAKE, 300, false, 0, 0);
    }
    if (!hs) {
        vTaskDelay(pdMS_TO_TICKS(500));
        if (!in_boot()) {
            return "прибор не отвечает - ни программа, ни загрузчик";
        }
    }

    phase(FWUP_PH_ENTER);
    if (s_cancel) {
        return "остановлено";
    }
    say(3, "стирание 0x%06lX..0x%06lX", (unsigned long)s_eraseLo, (unsigned long)s_eraseHi);
    drain();
    s_erased = true; /* дальше отмена уже ничего не вернёт */
    send4(CMD_ERASE, 0, 0, 0);
    send4((uint16_t)(s_eraseLo >> 16), (uint16_t)s_eraseLo, (uint16_t)(s_eraseHi >> 16),
          (uint16_t)s_eraseHi);
    if (!wait_word(ACK_ERASE_RANGE, 8000, false, 0, 0)) {
        return "загрузчик не принял диапазон стирания";
    }
    if (!wait_word(ACK_ERASE, 30000, true, 3, 30)) {
        snprintf(why, sizeof why, "стирание не прошло (0x%04X)", s_failWord);
        return why;
    }
    phase(FWUP_PH_ERASE);

    uint32_t total = 0, done = 0;
    for (int i = 0; i < s_nseg; i++) {
        total += s_seg[i].bytes;
    }
    for (int i = 0; i < s_nseg; i++) {
        const seg_t *s = &s_seg[i];
        send4(CMD_LOAD, 0, 0, 0);
        if (!wait_word(ACK_LOAD_READY, 5000, true, 0, 0)) {
            snprintf(why, sizeof why, "сегмент %d: загрузчик не готов (0x%04X)", i + 1, s_failWord);
            return why;
        }
        send4((uint16_t)(s->addr >> 16), (uint16_t)s->addr, (uint16_t)(s->bytes >> 16),
              (uint16_t)s->bytes);

        uint32_t blocks = s->bytes / BLK, sent = 0, acked = 0;
        while (acked < blocks) {
            while (sent < blocks && sent - acked < WINDOW) {
                uint8_t d[BLK];
                if (esp_partition_read(s_part, s->off + sent * BLK, d, BLK) != ESP_OK) {
                    return "образ в мосте не читается";
                }
                send_frame(d, BLK);
                sent++;
            }
            if (!wait_word(ACK_BLOCK, 3000, true, 0, 0)) {
                snprintf(why, sizeof why, "запись 0x%06lX: нет квитанции (0x%04X)",
                         (unsigned long)(s->addr + acked * 32), s_failWord);
                return why;
            }
            acked++;
            done += BLK;
            if ((acked & 63) == 0 || acked == blocks) {
                say(30 + (int)(65ULL * done / total), "запись %lu из %lu кБ",
                    (unsigned long)(done / 1024), (unsigned long)(total / 1024));
            }
        }
        if (!wait_word(ACK_LOAD, 3000, true, 0, 0)) {
            snprintf(why, sizeof why, "сегмент %d не закрыт (0x%04X)", i + 1, s_failWord);
            return why;
        }
    }

    phase(FWUP_PH_WRITE);
    say(96, "сверка суммы 0x%08lX", (unsigned long)s_sum);
    send4(CMD_VERIFY, 0, 0, 0);
    if (!wait_word(ACK_VERIFY_READY, 5000, true, 0, 0)) {
        return "загрузчик не готов к сверке";
    }
    send4((uint16_t)(s_sum >> 16), 0, 0, 0);
    send4((uint16_t)s_sum, 0, 0, 0);
    if (!wait_word(ACK_VERIFY, 5000, true, 0, 0)) {
        return s_failWord == ACK_NOVERIFY ? "сумма не сошлась" : "нет ответа на сверку";
    }

    say(98, "пуск новой прошивки");
    send4(CMD_REBOOT, 0, 0, 0);
    if (!wait_word(ACK_REBOOT_READY, 5000, true, 0, 0)) {
        return "загрузчик не готов к пуску";
    }
    send4((uint16_t)(s_entry >> 16), (uint16_t)s_entry, 0, 0);
    if (!wait_word(ACK_REBOOT, 3000, true, 0, 0)) {
        return "загрузчик не подтвердил пуск";
    }
    phase(FWUP_PH_START);
    return NULL;
}

/* После пуска: приложение отвечает на 333 - значит, поднялось. Его строки
 * заодно говорят, от какого числа прошивка. */
static bool device_alive(void)
{
    /* Спрашиваем сразу и часто: пока приложение поднимается, вопрос просто
     * пропадает. Ответ - полсотни квитанций, это несколько посылок, и
     * следующий вопрос его не перебьёт. Раньше перед первым вопросом стояла
     * пауза 1,5 с - она одна была дольше всей записи. */
    uint32_t t = now_ms();
    while (now_ms() - t < 10000) {
        fwup_ask_version();
        for (int k = 0; k < 4 && !s_verDone; k++) {
            vTaskDelay(pdMS_TO_TICKS(100));
        }
        if (s_verDone) {
            return true;
        }
    }
    return false;
}

/* «Остановить». До стирания флеш прибора цел: CMD_ABORT, и загрузчик сам
 * сбрасывает прибор на прежнюю прошивку. После - уйти некуда: прибор ждёт в
 * загрузчике, и прошить его можно тем же путём, отправив верный файл. */
static void stop_job(void)
{
    s_cancel = false;
    xQueueReset(s_txq);
    s_stalled = false;
    drain();

    if (s_erased) {
        send4(CMD_ABORT, 0, 0, 0); /* посреди записи уведёт его в ожидание стирания */
        say(-1, "остановлено после начала стирания: в приборе нет программы. Он ждёт в загрузчике - "
                "отправьте верный файл и нажмите «Прошить прибор». Питание не снимать");
        set_state(FWUP_FAIL);
        return;
    }
    /* Зов в загрузчик мог уже уйти - дать прибору ответить. */
    for (int k = 0; k < 10 && !in_boot(); k++) {
        vTaskDelay(pdMS_TO_TICKS(100));
    }
    if (!in_boot()) {
        say(-1, "остановлено - прибор не тронут");
        set_state(FWUP_FAIL);
        return;
    }
    say(-1, "останавливаю - возвращаю прибор к прежней прошивке");
    send4(CMD_ABORT, 0, 0, 0);
    if (!wait_word(ACK_ABORT, 1500, false, 0, 0)) {
        /* Загрузчик до 12.11.2026 отмены не знает. Флеш цел - прибор
         * поднимется на прежней прошивке после перезапуска. */
        say(-1, "прибор не принял отмену (загрузчик без неё?). Флеш не тронут: перезапустите прибор "
                "по питанию - поднимется прежняя прошивка. Или прошейте верный файл");
        set_state(FWUP_FAIL);
        return;
    }
    s_lastBoot = 0;
    if (device_alive()) {
        say(-1, "остановлено до стирания: прибор на прежней прошивке, сборка %s", s_st.devBuild);
    } else {
        say(-1, "остановлено до стирания, но прибор не ответил - перезапустите его по питанию");
    }
    set_state(FWUP_FAIL);
}

static void job(void)
{
    img_hdr_t h;
    rec_t     r;

    s_t0  = esp_timer_get_time();
    s_phT = s_t0;
    taskENTER_CRITICAL(&s_mux);
    s_st.state   = FWUP_RUN;
    s_st.attempt = 0;
    s_st.devBuild[0] = 0;
    memset(s_st.ph, 0, sizeof s_st.ph);
    s_st.totalMs = 0;
    taskEXIT_CRITICAL(&s_mux);

    const char *bad = check_image(&h, &r);
    s_erased = false;
    if (bad == NULL && !prepare()) {
        bad = "образ в мосте не читается";
    }
    s_entry = h.entry;
    /* В загрузчике прибор уже бывает - мост перезапустился посреди прошивки
     * или прошлая не удалась. Тогда ни ключей, ни сеанса нет, и проверять
     * их не по чему. */
    bool enter = !in_boot();
    s_erased   = !enter; /* был в загрузчике - что во флеше, неизвестно */
    if (bad == NULL && enter) {
        bad = check_device();
        /* Молчит - может быть, это загрузчик, оставшийся посреди записи
         * (мост перезапустился): он ждёт кадр данных и сам ничего не шлёт.
         * Первый же не тот кадр уведёт его в ожидание стирания - пробуем. */
        if (bad == s_noAnswer) {
            ESP_LOGW(TAG, "прибор молчит - пробую через загрузчик");
            bad = NULL;
        }
    }
    if (bad != NULL) {
        say(0, "не начато: %s", bad);
        set_state(FWUP_FAIL);
        return;
    }

    if (s_cancel) {
        stop_job();
        return;
    }
    phase(FWUP_PH_CHECK);
    ESP_LOGI(TAG, "прошивка: %s, %d сегм., вход 0x%06lX, сумма 0x%08lX", h.name, s_nseg,
             (unsigned long)h.entry, (unsigned long)s_sum);

    for (int a = 1; a <= 3; a++) {
        taskENTER_CRITICAL(&s_mux);
        s_st.attempt = a;
        taskEXIT_CRITICAL(&s_mux);

        bad = attempt(enter && a == 1);
        if (bad == NULL) {
            break;
        }
        if (s_cancel) {
            stop_job();
            return;
        }
        if (s_stalled) {
            bad = s_stallMsg; /* повторять незачем - посылок нет вовсе */
            break;
        }
        ESP_LOGW(TAG, "попытка %d: %s", a, bad);
        if (a < 3) {
            say(-1, "попытка %d: %s - начинаю заново", a, bad);
            /* Отказавший загрузчик повторяет маркер 16 раз и уходит ждать
             * стирания - дать ему туда дойти. */
            for (int k = 0; k < 20 && !s_cancel; k++) {
                vTaskDelay(pdMS_TO_TICKS(100));
            }
            if (s_cancel) {
                stop_job();
                return;
            }
        }
    }
    if (bad != NULL) {
        say(-1, "не прошито: %s. Прибор ждёт в загрузчике - можно повторить", bad);
        set_state(FWUP_FAIL);
        return;
    }

    say(99, "прошито - жду ответа новой прошивки");
    s_lastBoot = 0;
    bool alive = device_alive();
    phase(FWUP_PH_ALIVE);
    taskENTER_CRITICAL(&s_mux);
    s_st.totalMs = (uint32_t)((esp_timer_get_time() - s_t0) / 1000);
    taskEXIT_CRITICAL(&s_mux);
    ESP_LOGI(TAG, "время, мс: приём %lu, проверка %lu, вход %lu, стирание %lu, запись %lu, "
             "пуск %lu, ответ %lu, прошивка %lu", (unsigned long)s_st.upMs,
             (unsigned long)s_st.ph[0], (unsigned long)s_st.ph[1], (unsigned long)s_st.ph[2],
             (unsigned long)s_st.ph[3], (unsigned long)s_st.ph[4], (unsigned long)s_st.ph[5],
             (unsigned long)s_st.totalMs);
    if (alive) {
        live_push_time(); /* новая прошивка проснулась без часов */
        /* Имя прибора - отдельной строкой на странице: вместе с ним время в
         * сообщение не влезало. */
        say(100, "прибор прошит и работает, сборка %s", s_st.devBuild);
        set_state(FWUP_OK);
    } else {
        say(100, "прошито и запущено, но прибор не отвечает мосту - в этой прошивке, "
                 "возможно, нет связи с мостом; проверьте его по CAN");
        set_state(FWUP_FAIL);
    }
}

static void task(void *arg)
{
    (void)arg;
    for (;;) {
        ulTaskNotifyTake(pdTRUE, portMAX_DELAY);
        job();
        s_running = false;
    }
}

const char *fwup_start(void)
{
    if (s_running || s_storing) {
        return "мост уже занят прошивкой прибора";
    }
    if (web_updating()) {
        return "идёт обновление самого моста";
    }
    if (!s_st.img) {
        return "в мосте нет образа - сначала отправьте файл .out";
    }
    if (live_session_open()) {
        return "открыт сеанс Motor_Control - нажмите в нём «Отключить»";
    }
    s_cancel  = false;
    s_running = true;
    taskENTER_CRITICAL(&s_mux);
    s_st.state = FWUP_RUN;
    s_st.pct   = 0;
    snprintf(s_st.msg, sizeof s_st.msg, "начинаю");
    taskEXIT_CRITICAL(&s_mux);
    xTaskNotifyGive(s_task);
    return NULL;
}

void fwup_ask_version(void)
{
    s_vi      = -1;
    s_verDone = false;
    memset(s_vs, 0, sizeof s_vs);
    spi_link_send_cmd(333, 0, 0, 0);
}

const char *fwup_stop(void)
{
    if (!s_running) {
        return "прошивка не идёт";
    }
    s_cancel = true;
    return NULL;
}

bool fwup_busy(void)
{
    return s_running || s_storing;
}

void fwup_status(fwup_status_t *out)
{
    taskENTER_CRITICAL(&s_mux);
    *out = s_st;
    taskEXIT_CRITICAL(&s_mux);
    out->inBoot  = in_boot();
    out->elapsed = s_st.state == FWUP_RUN ? (uint32_t)((esp_timer_get_time() - s_t0) / 1000) : 0;
}

void fwup_init(void)
{
    s_rxq = xQueueCreate(64, sizeof(uint16_t[4]));
    s_txq = xQueueCreate(WINDOW + 4, sizeof(txf_t));
    s_part = esp_ota_get_next_update_partition(NULL);
    refresh_image_info();
    if (s_st.img) {
        ESP_LOGI(TAG, "образ прибора в мосте: %s, %lu байт", s_st.name, (unsigned long)s_st.bytes);
    }
    xTaskCreatePinnedToCore(task, "fwup", 4096, NULL, 5, &s_task, 0);
}

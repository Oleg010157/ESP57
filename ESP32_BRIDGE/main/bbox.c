/*
 * bbox.c
 *
 * Чёрный ящик. Что и зачем - в bbox.h, формат - docs/esp32-spi-protocol-RU.md,
 * раздел «Чёрный ящик».
 *
 *---------------------------------------------------------------------------
 * КОЛЬЦО
 *
 * Элемент кольца - байт заголовка и данные кадра: len | kind << 7, len <= 64
 * байт; kind 1 - кадр 0x7E1, 0 - кадр 103. Кольцо собрано из кусков по 8 кБ:
 * одним куском столько памяти у ESP32 не бывает - свободная куча разбита на
 * несколько областей. Сверх обычной кучи берутся куски из IRAM - там ещё
 * около 25 кБ, но читать и писать их можно только словами по 4 байта.
 *
 * Памяти хватает примерно на 0,7 с ленты F3 (33 байта на кадр) - меньше
 * задуманной секунды; лента F1 (9 байт) влезает почти вчетверо длиннее.
 *
 * Пишет в кольцо только задача SPI (ядро 1), читает только задача сохранения,
 * и только когда кольцо заморожено (BB_FROZEN): блокировок не нужно, хватает
 * порядка записи состояния.
 *
 *---------------------------------------------------------------------------
 * ФЛЕШ
 *
 * Раздел bbox (partitions.csv), 1 МБ:
 *
 *   0x00000..0xBFFFF  шесть мест по 128 кБ - записи чёрного ящика (здесь)
 *   0xC0000..0xDFFFF  почасовая история энергии (ehist.c), с 13.11.2026 -
 *                     бывшее седьмое место; остальные шесть не сдвинулись,
 *                     и записи в них читаются как были
 *   0xE0000..0xEFFFF  таблицы Motor_Control, присланные с ПК (tables.c)
 *   0xF0000..0xFFFFF  длинный журнал событий (bstore.c)
 *
 * Запись не длиннее места, поэтому и кольцо не больше места. До 12.11.2026
 * мест было пять по 192 кБ при кольце в 104 кБ - треть места пустовала, а
 * таблицам не находилось и 64 кБ. Смена раскладки подняла версию записи до 2:
 * записи версии 1 лежат по другим адресам и не читаются.
 *
 * Одно место всегда стёрто заранее, под следующую запись: стирание места -
 * больше секунды, и делать его после аварии значило бы держать кольцо
 * замороженным ещё и это время. Хранятся, значит, на одну запись меньше, чем
 * мест: пять (до 13.11.2026 - шесть, пока мест было семь).
 *
 * Пока идёт запись во флеш, кэш ESP32 отключается на обоих ядрах, и задача
 * SPI стоит. Прибор видит мост неготовым и посылку пропускает - это
 * штатный путь (wifiLink.notReady), ничего не ломается, но живая лента у
 * пульта на эти секунды прерывается.
 *
 * Сначала пишутся данные, заголовок - последним. Пропало питание посреди
 * записи - заголовка нет, место считается пустым.
 */
#include <stddef.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#include "esp_heap_caps.h"
#include "esp_log.h"
#include "esp_partition.h"
#include "esp_rom_crc.h"
#include "esp_timer.h"

#include "bbox.h"
#include "net_link.h"
#include "net_proto.h"

static const char *TAG = "bbox";

#define BB_PART_SUBTYPE 0x40 /* data, свой подтип - partitions.csv */
#define BB_SLOT_SIZE    0x20000u
#define BB_AREA         0xC0000u    /* места - до истории энергии (ehist.c) */
#define BB_HDR_SIZE     64u
#define BB_MAGIC        0x4237354Bu /* "K57B" */
#define BB_VERSION      2u          /* 1 - места по 192 кБ, до 12.11.2026 */

#define BB_CHUNK        8192u
#define BB_MAX_CHUNKS   ((BB_SLOT_SIZE - BB_HDR_SIZE) / BB_CHUNK)
/* Сколько кучи оставить WiFi и lwIP. Буферы передачи WiFi динамические
 * (sdkconfig.defaults): кольцо не должно их выдавить - отказы sendto и так
 * были главной бедой ленты. После запуска WiFi свободно около 150 кБ; сколько
 * WiFi берёт под полной лентой - «минимум» в строке «куча» лога моста. */
#define BB_HEAP_RESERVE (46u * 1024u)

#define BB_POST_FRAMES  1000u /* 0,2 с на 5 кГц после пуска */

#define BB_KIND_FAST    0x80u /* бит kind в заголовке элемента: кадр 0x7E1 */

enum { BB_REC = 0, BB_POST = 1, BB_FROZEN = 2 };

/* Заголовок записи во флеше, 64 байта, младший байт первым. Он же целиком
 * уходит пульту в BBOX_ID_ENTRY - разбор в Motor_Control, BlackBox.cs. */
typedef struct __attribute__((packed)) {
    uint32_t magic;
    uint16_t version;
    uint16_t hdrSize;
    uint32_t seq;       /* сквозной номер записи */
    uint16_t code;      /* код аварии; 0xFFFF - пуск вручную */
    uint16_t frameType; /* тип ленты 103 у прибора (команда 1001: 1,2,3,4,6); 0 - не было */
    uint32_t utc;       /* время аварии от прибора, 0 - неизвестно */
    uint16_t session;   /* номер сессии прибора */
    uint16_t trigTick;  /* счётчик тактов прибора в момент аварии */
    uint32_t nFrames;   /* кадров в записи */
    uint32_t dataBytes; /* байт данных после заголовка */
    uint32_t dataCrc;   /* CRC-32 данных */
    uint32_t trigFrame; /* номер кадра, на котором пуск дошёл до моста */
    uint16_t extra[4];  /* коды аварий, пришедших, пока дописывался хвост */
    uint32_t bridgeMs;  /* время моста от включения в момент пуска */
    uint32_t rsv[2];
    uint32_t hdrCrc;    /* CRC-32 байт 0..59 */
} bb_hdr_t;

_Static_assert(sizeof(bb_hdr_t) == BB_HDR_SIZE, "заголовок записи - 64 байта");

/* Кольцо */
static uint32_t *s_chunk[BB_MAX_CHUNKS];
static uint32_t s_cap;
static uint32_t s_head, s_tail, s_used;
static uint32_t s_written, s_dropped; /* элементов всего и вытеснено */

static volatile int s_state = BB_REC;
static uint32_t     s_post_left;
static bb_hdr_t     s_trig; /* заполняется при пуске, дописывается при сохранении */
static uint32_t     s_trig_elems;

/* Флеш */
static const esp_partition_t *s_part;
static int      s_slots;
static int      s_next = -1;
static uint32_t s_seq;

static TaskHandle_t s_save_task;
static bbox_stat_t  s_stat;

static uint8_t s_io[4096]; /* буфер обмена с флешем - только задача сохранения */

/*---------------------------------------------------------------------------
 * Кольцо - задача SPI
 *---------------------------------------------------------------------------*/
/* Только 32-битные чтения и записи: часть кусков - в IRAM, а там побайтовый
 * доступ у ESP32 - исключение. Байт за байтом через слово - около процента
 * ядра на 5000 кадров в секунду, дешевле, чем держать два пути. */
static uint8_t ring_byte(uint32_t pos)
{
    uint32_t off = pos % BB_CHUNK;
    uint32_t w   = s_chunk[pos / BB_CHUNK][off >> 2];
    return (uint8_t)(w >> ((off & 3u) * 8u));
}

static void ring_set(uint32_t pos, uint8_t b)
{
    uint32_t  off = pos % BB_CHUNK;
    uint32_t *p   = &s_chunk[pos / BB_CHUNK][off >> 2];
    uint32_t  sh  = (off & 3u) * 8u;
    *p = (*p & ~(0xFFu << sh)) | ((uint32_t)b << sh);
}

static void ring_put(uint32_t pos, const uint8_t *src, uint32_t n)
{
    while (n-- > 0) {
        ring_set(pos, *src++);
        pos = pos + 1u == s_cap ? 0u : pos + 1u;
    }
}

static void ring_get(uint32_t pos, uint8_t *dst, uint32_t n)
{
    while (n-- > 0) {
        *dst++ = ring_byte(pos);
        pos    = pos + 1u == s_cap ? 0u : pos + 1u;
    }
}

static void ring_push(uint8_t kind, const uint8_t *data, uint8_t len)
{
    uint32_t need = 1u + len;

    while (s_cap - s_used < need) {
        uint32_t n = 1u + (ring_byte(s_tail) & 0x7Fu);
        s_tail = (s_tail + n) % s_cap;
        s_used -= n;
        s_dropped++;
    }
    uint8_t h = (uint8_t)(len | kind);
    ring_put(s_head, &h, 1);
    ring_put((s_head + 1u) % s_cap, data, len);
    s_head = (s_head + need) % s_cap;
    s_used += need;
    s_written++;
}

static uint16_t le16(const uint8_t *p)
{
    return (uint16_t)(p[0] | (p[1] << 8));
}

static void trigger(uint16_t code, uint32_t utc, uint16_t session, uint16_t frameType,
                    uint16_t tick)
{
    s_stat.triggers++;

    if (s_state == BB_POST) {
        /* Каскад: следом за первой аварией пришли ещё - в ту же запись. */
        for (int i = 0; i < 4; i++) {
            if (s_trig.extra[i] == 0) {
                s_trig.extra[i] = code;
                break;
            }
        }
        return;
    }
    if (s_state != BB_REC || s_part == NULL || s_cap == 0) {
        s_stat.missed++;
        return;
    }

    memset(&s_trig, 0, sizeof s_trig);
    s_trig.code      = code;
    s_trig.utc       = utc;
    s_trig.session   = session;
    s_trig.frameType = frameType;
    s_trig.trigTick  = tick;
    s_trig.bridgeMs  = (uint32_t)(esp_timer_get_time() / 1000);
    s_trig_elems     = s_written;
    s_post_left      = BB_POST_FRAMES;
    s_state          = BB_POST;
}

void bbox_frame(uint16_t id, const uint8_t *data, uint8_t len)
{
    if (id == BBOX_ID_TRIG) {
        /* code, utc мл., utc ст., сессия, тип ленты, такт - прибор, bbox_trig.c */
        if (len >= 12) {
            trigger(le16(data), (uint32_t)le16(data + 2) | ((uint32_t)le16(data + 4) << 16),
                    le16(data + 6), le16(data + 8), le16(data + 10));
        }
        return;
    }
    if (id != 103 && id != BBOX_ID_FAST) {
        return;
    }
    if (s_cap == 0 || s_state == BB_FROZEN || len > K57_DATA_MAX) {
        return;
    }

    ring_push(id == BBOX_ID_FAST ? BB_KIND_FAST : 0u, data, len);

    if (s_state == BB_POST && --s_post_left == 0) {
        __atomic_store_n(&s_state, BB_FROZEN, __ATOMIC_RELEASE);
        xTaskNotifyGive(s_save_task);
    }
}

void bbox_manual(void)
{
    /* Задача консоли, не SPI: пуск попадёт в кольцо с запаздыванием на такт
     * обмена, для проверки это всё равно. Тип ленты неизвестен. */
    trigger(0xFFFF, 0, 0, 0, 0);
}

/*---------------------------------------------------------------------------
 * Флеш - задача сохранения
 *---------------------------------------------------------------------------*/
static uint32_t slot_base(int slot)
{
    return (uint32_t)slot * BB_SLOT_SIZE;
}

static bool read_hdr(int slot, bb_hdr_t *h)
{
    if (esp_partition_read(s_part, slot_base(slot), h, sizeof *h) != ESP_OK) {
        return false;
    }
    return h->magic == BB_MAGIC && h->version == BB_VERSION && h->hdrSize == BB_HDR_SIZE &&
           h->hdrCrc == esp_rom_crc32_le(0, (const uint8_t *)h, offsetof(bb_hdr_t, hdrCrc));
}

/* Место под следующую запись: стереть, если оно не пустое. Пустое проверяем
 * чтением - стирание лишний раз изнашивает флеш и останавливает задачу SPI. */
static void prepare_slot(int slot)
{
    uint32_t base = slot_base(slot);

    for (uint32_t off = 0; off < BB_SLOT_SIZE; off += sizeof s_io) {
        if (esp_partition_read(s_part, base + off, s_io, sizeof s_io) != ESP_OK) {
            break;
        }
        for (uint32_t i = 0; i < sizeof s_io; i++) {
            if (s_io[i] != 0xFF) {
                esp_partition_erase_range(s_part, base, BB_SLOT_SIZE);
                return;
            }
        }
    }
}

static void save(void)
{
    int64_t  t0   = esp_timer_get_time();
    uint32_t base = slot_base(s_next);
    uint32_t pos  = s_tail;
    uint32_t left = s_used;
    uint32_t off  = BB_HDR_SIZE;
    uint32_t crc  = 0;

    while (left > 0) {
        uint32_t k = left < sizeof s_io ? left : sizeof s_io;
        ring_get(pos, s_io, k);
        crc = esp_rom_crc32_le(crc, s_io, k);
        esp_partition_write(s_part, base + off, s_io, k);
        pos = (pos + k) % s_cap;
        off += k;
        left -= k;
        vTaskDelay(1); /* между кусками - отпустить задачу SPI */
    }

    bb_hdr_t h = s_trig;
    h.magic     = BB_MAGIC;
    h.version   = BB_VERSION;
    h.hdrSize   = BB_HDR_SIZE;
    h.seq       = s_seq;
    h.nFrames   = s_written - s_dropped;
    h.dataBytes = s_used;
    h.dataCrc   = crc;
    h.trigFrame = s_trig_elems - s_dropped;
    h.hdrCrc    = esp_rom_crc32_le(0, (const uint8_t *)&h, offsetof(bb_hdr_t, hdrCrc));
    esp_partition_write(s_part, base, &h, sizeof h);

    s_stat.saved++;
    s_stat.saveMs = (uint32_t)((esp_timer_get_time() - t0) / 1000);

    /* Пульту - что запись есть: он покажет её, не дожидаясь вопроса. */
    uint8_t rec[K57_REC_HDR + 12];
    rec[0] = K57_REC_SYNC;
    rec[1] = 12;
    rec[2] = (uint8_t)BBOX_ID_SAVED;
    rec[3] = (uint8_t)(BBOX_ID_SAVED >> 8);
    rec[4] = (uint8_t)s_next;
    rec[5] = 0;
    memcpy(rec + 6, &h.seq, 4);
    memcpy(rec + 10, &h.code, 2);
    memcpy(rec + 12, &h.utc, 4); /* место, 0, номер, код, время - 12 байт */
    net_link_tcp_send(rec, K57_REC_HDR + 12);

    ESP_LOGI(TAG, "запись %lu (место %d): код %u, кадров %lu, %lu байт, %lu мс",
             (unsigned long)h.seq, s_next, h.code, (unsigned long)h.nFrames,
             (unsigned long)h.dataBytes, (unsigned long)s_stat.saveMs);

    s_seq++;
    s_next = (s_next + 1) % s_slots;
}

static void save_task(void *arg)
{
    (void)arg;

    /* Где продолжать: после записи с наибольшим номером. */
    uint32_t best = 0;
    int      bestSlot = -1;
    int      valid = 0;
    for (int i = 0; i < s_slots; i++) {
        bb_hdr_t h;
        if (read_hdr(i, &h)) {
            valid++;
            if (bestSlot < 0 || (int32_t)(h.seq - best) > 0) {
                best     = h.seq;
                bestSlot = i;
            }
        }
    }
    s_seq  = bestSlot < 0 ? 1 : best + 1;
    s_next = bestSlot < 0 ? 0 : (bestSlot + 1) % s_slots;
    prepare_slot(s_next);
    ESP_LOGI(TAG, "флеш: мест %d, записей %d, следующая - место %d", s_slots, valid, s_next);

    for (;;) {
        ulTaskNotifyTake(pdTRUE, portMAX_DELAY);
        if (__atomic_load_n(&s_state, __ATOMIC_ACQUIRE) != BB_FROZEN) {
            continue;
        }
        save();
        prepare_slot(s_next);

        s_head = s_tail = s_used = 0;
        s_written = s_dropped = 0;
        __atomic_store_n(&s_state, BB_REC, __ATOMIC_RELEASE);
    }
}

/*---------------------------------------------------------------------------
 * Запросы пульта - задача TCP
 *---------------------------------------------------------------------------*/

/* Записи копятся в пачку и уходят одним send: по записи на сегмент WiFi
 * тратил бы эфир впустую. */
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

int bbox_list(bbox_entry_t *out, int max)
{
    int n = 0;

    if (s_part == NULL) {
        return 0;
    }
    for (int i = 0; i < s_slots && n < max; i++) {
        bb_hdr_t h;
        if (i == s_next || !read_hdr(i, &h)) {
            continue;
        }
        out[n] = (bbox_entry_t){
            .slot = i, .seq = h.seq, .code = h.code, .frameType = h.frameType, .utc = h.utc,
            .session = h.session, .frames = h.nFrames, .trigFrame = h.trigFrame,
        };
        memcpy(out[n].extra, h.extra, sizeof h.extra);
        n++;
    }
    return n;
}

static void send_list(void)
{
    uint8_t n = 0;

    if (s_part != NULL) {
        for (int i = 0; i < s_slots; i++) {
            bb_hdr_t h;
            if (i == s_next || !read_hdr(i, &h)) {
                continue;
            }
            /* место, 0, и заголовок без своей CRC - 62 байта */
            uint8_t e[2 + offsetof(bb_hdr_t, hdrCrc)];
            e[0] = (uint8_t)i;
            e[1] = 0;
            memcpy(e + 2, &h, offsetof(bb_hdr_t, hdrCrc));
            out_rec(BBOX_ID_ENTRY, e, sizeof e);
            n++;
        }
    }

    bbox_stat_t s;
    bbox_get_stat(&s);
    uint8_t end[20];
    end[0] = n;
    end[1] = s.state;
    end[2] = s.noFlash ? 1 : 0;
    end[3] = 0;
    memcpy(end + 4, &s.ringBytes, 4);
    memcpy(end + 8, &s.ringFrames, 4);
    memcpy(end + 12, &s.saved, 4);
    memcpy(end + 16, &s.missed, 4);
    out_rec(BBOX_ID_LIST_END, end, sizeof end);
    out_flush();
}

/* Запись целиком, с заголовком: смещение (4 байта) и до 56 байт. */
#define BB_PIECE 56u

static void send_slot(int slot)
{
    bb_hdr_t h;
    uint8_t  end[8] = {(uint8_t)slot, 0};
    uint32_t total  = 0;

    if (s_part != NULL && slot >= 0 && slot < s_slots && slot != s_next && read_hdr(slot, &h)) {
        static uint8_t buf[BB_PIECE * 72]; /* 4032 байта за одно чтение флеша */
        uint32_t size = BB_HDR_SIZE + h.dataBytes;

        for (uint32_t off = 0; off < size; off += sizeof buf) {
            uint32_t k = size - off < sizeof buf ? size - off : sizeof buf;
            if (esp_partition_read(s_part, slot_base(slot) + off, buf, k) != ESP_OK) {
                break;
            }
            for (uint32_t i = 0; i < k; i += BB_PIECE) {
                uint8_t  piece[4 + BB_PIECE];
                uint32_t at = off + i;
                uint32_t m  = k - i < BB_PIECE ? k - i : BB_PIECE;
                memcpy(piece, &at, 4);
                memcpy(piece + 4, buf + i, m);
                out_rec(BBOX_ID_DATA, piece, (uint8_t)(4 + m));
            }
            total = off + k;
        }
        end[1] = total == size ? 1 : 0;
    }
    memcpy(end + 4, &total, 4);
    out_rec(BBOX_ID_READ_END, end, sizeof end);
    out_flush();
}

/* Стереть - значит обнулить метку: флеш пишет 1 -> 0 без стирания, и это
 * мгновенно. Настоящее стирание - когда место дойдёт до очереди. */
static void clear_all(void)
{
    if (s_part == NULL) {
        return;
    }
    for (int i = 0; i < s_slots; i++) {
        bb_hdr_t h;
        if (i != s_next && read_hdr(i, &h)) {
            uint32_t zero = 0;
            esp_partition_write(s_part, slot_base(i), &zero, 4);
        }
    }
}

void bbox_request(uint16_t id, const uint8_t *data, uint8_t len)
{
    switch (id) {
    case BBOX_ID_LIST:
        send_list();
        break;
    case BBOX_ID_READ:
        if (len >= 1) {
            send_slot(data[0]);
        }
        break;
    case BBOX_ID_CLEAR:
        clear_all();
        send_list();
        break;
    default:
        break;
    }
}

/*---------------------------------------------------------------------------*/
void bbox_start(void)
{
    s_part = esp_partition_find_first(ESP_PARTITION_TYPE_DATA, BB_PART_SUBTYPE, "bbox");
    if (s_part == NULL) {
        s_stat.noFlash = true;
        ESP_LOGW(TAG, "раздела bbox нет - чёрный ящик выключен (старая таблица разделов?)");
        return;
    }
    s_slots = (int)((s_part->size < BB_AREA ? s_part->size : BB_AREA) / BB_SLOT_SIZE);

    /* Задача - до кольца: пуск будит её, а пуск возможен, как только у
     * кольца появился размер. */
    xTaskCreatePinnedToCore(save_task, "bbox", 4096, NULL, 2, &s_save_task, 0);

    size_t free0 = heap_caps_get_free_size(MALLOC_CAP_8BIT);
    int n = 0;

    /* IRAM: WiFi её не берёт - забираем всю. Но с этими флагами куча отдаёт
     * и общую область DRAM/IRAM - по адресу из IRAM, а память та же, что у
     * WiFi (стенд, 10.11.2026: из семи «IRAM»-кусков четыре съели DRAM, и WiFi
     * осталось 15 кБ). Признак честный один: уменьшилась ли обычная куча.
     * Такой кусок придерживаем, чтобы куча искала дальше, и в конце отдаём.
     * Поэтому IRAM - первой: придержанное берётся из полной кучи, а не из
     * запаса, оставленного WiFi. */
    int   iram = 0;
    void *held[BB_MAX_CHUNKS];
    int   nheld = 0;
    while (n < (int)BB_MAX_CHUNKS && nheld < (int)BB_MAX_CHUNKS) {
        size_t before = heap_caps_get_free_size(MALLOC_CAP_8BIT);
        void  *p      = heap_caps_malloc(BB_CHUNK, MALLOC_CAP_32BIT | MALLOC_CAP_EXEC);
        if (p == NULL) {
            break;
        }
        if (heap_caps_get_free_size(MALLOC_CAP_8BIT) == before) {
            s_chunk[n++] = p;
            iram++;
        } else {
            held[nheld++] = p;
        }
    }
    while (nheld > 0) {
        heap_caps_free(held[--nheld]);
    }

    while (n < (int)BB_MAX_CHUNKS &&
           heap_caps_get_free_size(MALLOC_CAP_8BIT) > BB_HEAP_RESERVE + BB_CHUNK) {
        s_chunk[n] = heap_caps_malloc(BB_CHUNK, MALLOC_CAP_8BIT);
        if (s_chunk[n] == NULL) {
            break;
        }
        n++;
    }
    s_stat.ringBytes = (uint32_t)n * BB_CHUNK;
    /* Задача SPI уже работает и смотрит на s_cap: куски должны быть на месте
     * раньше, чем она увидит размер. */
    __atomic_store_n(&s_cap, (uint32_t)n * BB_CHUNK, __ATOMIC_RELEASE);
    ESP_LOGI(TAG, "кольцо %lu кБ (%d кусков, из них в IRAM %d), куча была %u кБ, осталось %u кБ",
             (unsigned long)(s_cap / 1024), n, iram, (unsigned)(free0 / 1024),
             (unsigned)(heap_caps_get_free_size(MALLOC_CAP_8BIT) / 1024));
}

void bbox_get_stat(bbox_stat_t *out)
{
    *out = s_stat;
    out->ringUsed   = s_used;
    out->ringFrames = s_written - s_dropped;
    out->state      = (uint8_t)s_state;
}

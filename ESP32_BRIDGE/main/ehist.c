/*
 * ehist.c
 *
 * Почасовая история счётчиков энергии. Что и зачем - ehist.h.
 */
#include <string.h>

#include "esp_log.h"
#include "esp_partition.h"

#include "ehist.h"

static const char *TAG = "ehist";

#define EH_PART_SUBTYPE 0x40      /* раздел bbox - partitions.csv */
#define EH_OFF          0xC0000u  /* бывшее седьмое место чёрного ящика (bbox.c) */
#define EH_SECT         4096u
#define EH_SECTORS      32u
#define EH_MAGIC        0x4537354Bu /* "K57E" - сектор наш */
#define EH_PER_SECT     340u        /* 4 + 340 * 12 = 4084 байта */
#define EH_N            (EH_SECTORS * EH_PER_SECT)

static const esp_partition_t *s_part;
static const uint8_t         *s_map; /* вся область, только чтение */
static esp_partition_mmap_handle_t s_mh;
static uint32_t s_next;     /* куда следующая запись */
static uint32_t s_lastHour; /* час последней записи */

static uint32_t rec_off(uint32_t i)
{
    return (i / EH_PER_SECT) * EH_SECT + 4u + (i % EH_PER_SECT) * sizeof(ehist_rec_t);
}

static const ehist_rec_t *rec_at(uint32_t i)
{
    return (const ehist_rec_t *)(s_map + rec_off(i));
}

static bool rec_valid(const ehist_rec_t *r)
{
    return r->hour != 0xFFFFFFFFu && r->out != 0xFFFFFFFFu && r->in != 0xFFFFFFFFu;
}

static uint32_t sect_magic(uint32_t s)
{
    uint32_t m;
    memcpy(&m, s_map + s * EH_SECT, 4);
    return m;
}

void ehist_init(void)
{
    s_part = esp_partition_find_first(ESP_PARTITION_TYPE_DATA, EH_PART_SUBTYPE, NULL);
    if (s_part == NULL || s_part->size < EH_OFF + EH_SECTORS * EH_SECT) {
        ESP_LOGW(TAG, "нет места под историю энергии");
        s_part = NULL;
        return;
    }
    const void *p;
    if (esp_partition_mmap(s_part, EH_OFF, EH_SECTORS * EH_SECT, ESP_PARTITION_MMAP_DATA, &p, &s_mh) != ESP_OK) {
        ESP_LOGW(TAG, "история энергии не отображается в память");
        s_part = NULL;
        return;
    }
    s_map = p;

    /* Чужой сектор - без метки и не стёртый: там запись чёрного ящика, пока
     * место было его (до 13.11.2026). Стереть, иначе её байты читались бы как
     * часы. */
    int wiped = 0;
    for (uint32_t s = 0; s < EH_SECTORS; s++) {
        uint32_t m = sect_magic(s);
        if (m != EH_MAGIC && m != 0xFFFFFFFFu) {
            esp_partition_erase_range(s_part, EH_OFF + s * EH_SECT, EH_SECT);
            wiped++;
        } else if (m == 0xFFFFFFFFu) {
            /* Стёрт до конца? Обрывок записи без метки - тоже стереть. */
            for (uint32_t k = 4; k < EH_SECT; k += 4) {
                uint32_t w;
                memcpy(&w, s_map + s * EH_SECT + k, 4);
                if (w != 0xFFFFFFFFu) {
                    esp_partition_erase_range(s_part, EH_OFF + s * EH_SECT, EH_SECT);
                    wiped++;
                    break;
                }
            }
        }
    }

    /* Конец истории - после записи с наибольшим часом. */
    int32_t best = -1;
    int     n    = 0;
    for (uint32_t i = 0; i < EH_N; i++) {
        if (sect_magic(i / EH_PER_SECT) != EH_MAGIC) {
            i += EH_PER_SECT - 1 - i % EH_PER_SECT; /* весь сектор пуст */
            continue;
        }
        const ehist_rec_t *r = rec_at(i);
        if (rec_valid(r)) {
            n++;
            if (best < 0 || r->hour > s_lastHour) {
                best       = (int32_t)i;
                s_lastHour = r->hour;
            }
        }
    }
    s_next = best < 0 ? 0 : ((uint32_t)best + 1) % EH_N;
    ESP_LOGI(TAG, "история энергии: %d записей, стёрто чужих секторов %d", n, wiped);
}

void ehist_add(uint32_t hour, uint32_t out, uint32_t in)
{
    if (s_part == NULL || hour == s_lastHour) {
        return;
    }
    uint32_t s = s_next / EH_PER_SECT;
    if (s_next % EH_PER_SECT == 0) {
        /* Новый сектор: стереть (по кругу - самые старые 340 часов) и пометить. */
        uint32_t m = EH_MAGIC;
        if (esp_partition_erase_range(s_part, EH_OFF + s * EH_SECT, EH_SECT) != ESP_OK ||
            esp_partition_write(s_part, EH_OFF + s * EH_SECT, &m, 4) != ESP_OK) {
            return;
        }
    }
    ehist_rec_t r = {.hour = hour, .out = out, .in = in};
    if (esp_partition_write(s_part, EH_OFF + rec_off(s_next), &r, sizeof r) == ESP_OK) {
        s_lastHour = hour;
        s_next     = (s_next + 1) % EH_N;
    }
}

void ehist_walk(uint32_t from, uint32_t to, void (*cb)(const ehist_rec_t *r, void *ctx), void *ctx)
{
    if (s_part == NULL) {
        return;
    }
    for (uint32_t k = 0; k < EH_N; k++) {
        uint32_t i = (s_next + k) % EH_N; /* от самой старой */
        if (sect_magic(i / EH_PER_SECT) != EH_MAGIC) {
            continue;
        }
        const ehist_rec_t *r = rec_at(i);
        if (rec_valid(r) && r->hour >= from && r->hour <= to) {
            ehist_rec_t c = *r; /* из флеша - копией: обработчик может ждать сеть */
            cb(&c, ctx);
        }
    }
}

/*
 * spi_link.c
 *
 * Ведомый SPI к прибору. Формат посылки - в spi_link.h.
 *
 * Кадры от прибора пока никуда не уходят - только счётчики и ACK для печати.
 * К прибору идут команды из очереди (spi_link_send), сейчас их ставит
 * консоль в main.c.
 */
#include <stdbool.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "driver/spi_slave.h"
#include "esp_attr.h"
#include "esp_log.h"

#include "board.h"
#include "net_link.h"
#include "spi_link.h"

#define WORDS     (SPI_LINK_BYTES / 2)
#define HDR_WORDS 4
#define LAST_WORD (WORDS - 1)
#define MAX_DATA  64 /* байт - больше в CAN FD не бывает */

#define MAGIC_F28 0xA55Au
#define MAGIC_ESP 0x5AA5u

static const char *TAG = "spi_link";

/* DMA ведомого SPI у ESP32: буфер с границы 4 байт, длина кратна 4. */
static WORD_ALIGNED_ATTR uint8_t s_tx[2][SPI_LINK_BYTES];
static WORD_ALIGNED_ATTR uint8_t s_rx[2][SPI_LINK_BYTES];
static spi_slave_transaction_t s_trans[2];

static spi_link_stat_t s_stat;
static uint16_t s_seq_out;

/* dbg Первая испорченная посылка целиком - разобрать глазами, что в ней.
 * Пишет задача SPI, печатает main.c; s_bad_ready - флаг «есть что печатать». */
static uint8_t           s_bad[SPI_LINK_BYTES];
static int               s_bad_word;  /* слово, на котором разбор сломался */
static volatile bool     s_bad_ready;
static bool              s_bad_taken; /* одну и запоминаем - дальше только счётчик */

static void keep_bad(const uint8_t *rx, int w)
{
    if (s_bad_taken) {
        return;
    }
    memcpy(s_bad, rx, SPI_LINK_BYTES);
    s_bad_word  = w;
    s_bad_taken = true;
    s_bad_ready = true;
}

bool spi_link_get_bad(const uint8_t **pkt, int *word)
{
    if (!s_bad_ready) {
        return false;
    }
    s_bad_ready = false;
    *pkt  = s_bad;
    *word = s_bad_word;
    return true;
}

/* К прибору: команды пульта. 64 - с запасом на залп от ползунка уставки
 * (до 160 в секунду на стенде) при 500 посылках в секунду. */
static QueueHandle_t s_to_f28;
/* От прибора: ACK для печати в консоль. Лишние выбрасываются - это только
 * глазам. */
static QueueHandle_t s_acks;

static void IRAM_ATTR on_setup(spi_slave_transaction_t *t)
{
    (void)t;
    gpio_set_level(BOARD_RDY_GPIO, 0);
}

static void IRAM_ATTR on_done(spi_slave_transaction_t *t)
{
    (void)t;
    gpio_set_level(BOARD_RDY_GPIO, 1);
}

/* Прибор передаёт 16-битные слова старшим байтом вперёд. */
static uint16_t word_at(const uint8_t *b, int i)
{
    return (uint16_t)((b[2 * i] << 8) | b[2 * i + 1]);
}

static void put_word(uint8_t *b, int i, uint16_t w)
{
    b[2 * i]     = (uint8_t)(w >> 8);
    b[2 * i + 1] = (uint8_t)w;
}

/* CRC-16/CCITT-FALSE (0x1021, начальное 0xFFFF) по байтам в порядке провода. */
static uint16_t crc16(const uint8_t *b, size_t n)
{
    uint16_t crc = 0xFFFF;
    for (size_t i = 0; i < n; i++) {
        crc ^= (uint16_t)b[i] << 8;
        for (int k = 0; k < 8; k++) {
            crc = (crc & 0x8000) ? (uint16_t)((crc << 1) ^ 0x1021) : (uint16_t)(crc << 1);
        }
    }
    return crc;
}

/* Посылка к прибору: всё, что ждёт в очереди и влезает, затем CRC. Слова кадра
 * - пары байт CAN младшим вперёд, по проводу старшим вперёд: прибор получает
 * ровно те слова, что разобрал бы из кадра CAN (myMCAN.c). */
static void prepare_tx(uint8_t *tx)
{
    spi_link_frame_t f;
    int w = HDR_WORDS;

    memset(tx, 0, SPI_LINK_BYTES);
    while (w + 2 + 4 <= LAST_WORD && xQueueReceive(s_to_f28, &f, 0) == pdTRUE) {
        int n = f.len / 2;
        put_word(tx, w, f.id);
        put_word(tx, w + 1, (uint16_t)(n * 2));
        for (int i = 0; i < n; i++) {
            put_word(tx, w + 2 + i, (uint16_t)(f.data[2 * i] | (f.data[2 * i + 1] << 8)));
        }
        w += 2 + n;
        s_stat.cmdOut++;
    }
    put_word(tx, 0, MAGIC_ESP);
    put_word(tx, 1, s_seq_out++);
    put_word(tx, 2, (uint16_t)((w - HDR_WORDS) * 2));
    put_word(tx, 3, (uint16_t)uxQueueMessagesWaiting(s_to_f28));
    put_word(tx, LAST_WORD, crc16(tx, LAST_WORD * 2));
}

bool spi_link_send(const spi_link_frame_t *f)
{
    if (f->len > 8 || (f->len & 1)) {
        return false;
    }
    if (xQueueSend(s_to_f28, f, 0) != pdTRUE) {
        s_stat.cmdDrop++;
        return false;
    }
    return true;
}

bool spi_link_send_cmd(uint16_t cmd, uint16_t d1, uint16_t d2, uint16_t d3)
{
    /* Раскладка MainWindow.cs: DATA[0..1] = cmd, [2..3] = d3, [4..5] = d2,
     * [6..7] = d1, младшим байтом вперёд. */
    spi_link_frame_t f = { .id = 2, .len = 8 };
    uint16_t w[4] = { cmd, d3, d2, d1 };
    for (int i = 0; i < 4; i++) {
        f.data[2 * i]     = (uint8_t)w[i];
        f.data[2 * i + 1] = (uint8_t)(w[i] >> 8);
    }
    return spi_link_send(&f);
}

bool spi_link_get_ack(uint16_t out[4])
{
    return xQueueReceive(s_acks, out, 0) == pdTRUE;
}

static void parse(const uint8_t *rx, size_t bits, uint16_t *last_seq, bool *have_seq,
                  uint16_t *last_tick, bool *have_tick)
{
    if (bits < SPI_LINK_BYTES * 8) {
        s_stat.truncated++;
        return;
    }
    if (word_at(rx, 0) != MAGIC_F28) {
        s_stat.badMagic++;
        keep_bad(rx, 0);
        return;
    }
    s_stat.packets++;

    uint16_t seq = word_at(rx, 1);
    if (*have_seq && seq != (uint16_t)(*last_seq + 1)) {
        s_stat.seqGap++;
    }
    *last_seq = seq;
    *have_seq = true;

    uint16_t fill = word_at(rx, 2);
    s_stat.f28Drop += word_at(rx, 3);
    s_stat.lastFill = fill;
    if (fill > s_stat.maxFill) {
        s_stat.maxFill = fill;
    }
    if (fill > (LAST_WORD - HDR_WORDS) * 2 || (fill & 1)) {
        s_stat.badLayout++;
        keep_bad(rx, 2);
        return;
    }

    int w   = HDR_WORDS;
    int end = HDR_WORDS + fill / 2;
    while (w < end) {
        if (w + 2 > end) {
            s_stat.badLayout++;
            keep_bad(rx, w);
            return;
        }
        uint16_t id  = word_at(rx, w);
        uint16_t len = word_at(rx, w + 1);
        int      n   = len / 2;
        if ((len & 1) || len > MAX_DATA || w + 2 + n > end) {
            s_stat.badLayout++;
            keep_bad(rx, w);
            return;
        }

        switch (id) {
        case 103: {
            /* Последнее слово быстрого кадра - счётчик тактов во всех
             * режимах (1ph_Sin.c, Main_ISR.c). Разрыв - кадр потерян. */
            s_stat.fr103++;
            if (n > 0) {
                uint16_t tick = word_at(rx, w + 2 + n - 1);
                if (*have_tick && tick != (uint16_t)(*last_tick + 1)) {
                    s_stat.tickGap++;
                }
                *last_tick = tick;
                *have_tick = true;
            }
            break;
        }
        case 140: s_stat.fr140++; break;
        case 1:
            /* ACK: cmd, d1, d2, d3 - SendAck() в myMCAN.c. */
            s_stat.fr1++;
            if (n == 4) {
                uint16_t a[4] = { word_at(rx, w + 2), word_at(rx, w + 3),
                                  word_at(rx, w + 4), word_at(rx, w + 5) };
                xQueueSend(s_acks, a, 0);
            }
            break;
        case 132: s_stat.fr132++; break;
        default:  s_stat.frOther++; break;
        }

        /* В сеть - байтами в порядке шины: каждое слово младшим байтом
         * вперёд. Получается ровно DATA кадра CAN FD. */
        {
            uint8_t data[MAX_DATA];
            for (int i = 0; i < n; i++) {
                uint16_t v      = word_at(rx, w + 2 + i);
                data[2 * i]     = (uint8_t)v;
                data[2 * i + 1] = (uint8_t)(v >> 8);
            }
            net_link_frame(id, data, (uint8_t)len);
        }
        w += 2 + n;
    }
    net_link_packet_end(word_at(rx, 3));
}

/* Прерывание ведомого SPI садится на то ядро, где позван spi_slave_initialize.
 * Поэтому зовём его здесь, в задаче на ядре 1, а не из app_main на ядре 0:
 * там живёт WiFi, и его прерывания задерживали бы перезарядку транзакции. */
static void slave_init(void)
{
    spi_bus_config_t bus = {
        .mosi_io_num     = BOARD_SPI_MOSI,
        .miso_io_num     = BOARD_SPI_MISO,
        .sclk_io_num     = BOARD_SPI_SCLK,
        .quadwp_io_num   = -1,
        .quadhd_io_num   = -1,
        .max_transfer_sz = SPI_LINK_BYTES,
    };
    spi_slave_interface_config_t slv = {
        .mode          = BOARD_SPI_MODE,
        .spics_io_num  = BOARD_SPI_CS,
        .queue_size    = 2,
        .post_setup_cb = on_setup,
        .post_trans_cb = on_done,
    };

    /* Без подтяжек провода висят, пока прибор не настроил SPIA: CS поплыл бы
     * вниз, и ведомый принял бы шум за посылку. */
    gpio_set_pull_mode(BOARD_SPI_MOSI, GPIO_PULLUP_ONLY);
    gpio_set_pull_mode(BOARD_SPI_SCLK, GPIO_PULLUP_ONLY);
    gpio_set_pull_mode(BOARD_SPI_CS, GPIO_PULLUP_ONLY);

    ESP_ERROR_CHECK(spi_slave_initialize(BOARD_SPI_HOST, &bus, &slv, SPI_DMA_CH_AUTO));
    ESP_LOGI(TAG, "ведомый SPI: режим %d, посылка %d байт, ядро %d",
             BOARD_SPI_MODE, SPI_LINK_BYTES, xPortGetCoreID());
}

static void queue_both(void)
{
    for (int i = 0; i < 2; i++) {
        prepare_tx(s_tx[i]);
        memset(s_rx[i], 0, SPI_LINK_BYTES);
        s_trans[i] = (spi_slave_transaction_t){
            .length    = SPI_LINK_BYTES * 8,
            .tx_buffer = s_tx[i],
            .rx_buffer = s_rx[i],
        };
        ESP_ERROR_CHECK(spi_slave_queue_trans(BOARD_SPI_HOST, &s_trans[i], portMAX_DELAY));
    }
}

/*
 * Перезапуск ведомого SPI.
 *
 * После перезагрузки моста ведомый SPI иногда стартует в сбитом состоянии:
 * в каждой посылке байты 1..7 заголовка приходят нулями, а всё дальше - на
 * своих местах (стенд, 10.11.2026: «a5 00 00 00 00 00 00 00 | 00 8c 00 40»,
 * то есть кадр 140 с восьмого байта, как положено). Само оно не проходит:
 * однажды держалось 70 секунд, и все ACK в это время терялись - у пульта
 * оставались жёлтые поля. Лечится перезапуском - драйвер при этом
 * сбрасывает и сам периферийный блок.
 *
 * Порядок важен: сначала поднять «есть данные», чтобы прибор не начал новую
 * посылку, дождаться конца текущей (она идёт меньше миллисекунды) и только
 * при высоком CS зарядить транзакции заново - иначе ведомый встанет на
 * середину посылки.
 */
#define BAD_IN_ROW 4

static void slave_reset(void)
{
    spi_slave_free(BOARD_SPI_HOST);
    gpio_set_level(BOARD_RDY_GPIO, 1);
    vTaskDelay(pdMS_TO_TICKS(5));
    slave_init();
    queue_both();
    s_stat.slaveResets++;
}

static void link_task(void *arg)
{
    (void)arg;
    uint16_t last_seq = 0, last_tick = 0;
    bool have_seq = false, have_tick = false;
    int bad_in_row = 0;

    slave_init();
    queue_both();

    for (;;) {
        spi_slave_transaction_t *t;
        esp_err_t err = spi_slave_get_trans_result(BOARD_SPI_HOST, &t, portMAX_DELAY);
        if (err != ESP_OK) {
            continue;
        }

        uint32_t bad_before = s_stat.truncated + s_stat.badMagic + s_stat.badLayout;
        parse(t->rx_buffer, t->trans_len, &last_seq, &have_seq, &last_tick, &have_tick);
        if (s_stat.truncated + s_stat.badMagic + s_stat.badLayout != bad_before) {
            if (++bad_in_row >= BAD_IN_ROW) {
                ESP_LOGW(TAG, "%d посылок подряд испорчены - перезапуск ведомого SPI", bad_in_row);
                bad_in_row = 0;
                have_seq = have_tick = false;
                slave_reset();
                continue; /* обе транзакции заряжены заново */
            }
        } else {
            bad_in_row = 0;
        }

        /* Транзакция закончена, её буферы DMA больше не трогает. Вторая в это
         * время уже заряжена - прибор её и заберёт. */
        prepare_tx((uint8_t *)t->tx_buffer);
        ESP_ERROR_CHECK(spi_slave_queue_trans(BOARD_SPI_HOST, t, portMAX_DELAY));
    }
}

void spi_link_start(void)
{
    s_to_f28 = xQueueCreate(64, sizeof(spi_link_frame_t));
    s_acks   = xQueueCreate(16, sizeof(uint16_t[4]));

    gpio_config_t rdy = {
        .pin_bit_mask = 1ULL << BOARD_RDY_GPIO,
        .mode         = GPIO_MODE_OUTPUT,
    };
    ESP_ERROR_CHECK(gpio_config(&rdy));
    gpio_set_level(BOARD_RDY_GPIO, 1);

    xTaskCreatePinnedToCore(link_task, "spi_link", 4096, NULL, 10, NULL, 1);
}

void spi_link_get_stat(spi_link_stat_t *out)
{
    *out = s_stat;
}

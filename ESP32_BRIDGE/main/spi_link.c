/*
 * spi_link.c
 *
 * Ведомый SPI к прибору: проверка канала. Формат посылки - в spi_link.h.
 */
#include <stdbool.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "driver/spi_slave.h"
#include "esp_attr.h"
#include "esp_log.h"

#include "board.h"
#include "spi_link.h"

#define LINK_BYTES 32

#define MAGIC_F28 0xA55Au
#define MAGIC_ESP 0x5AA5u

static const char *TAG = "spi_link";

/* DMA ведомого SPI у ESP32: буфер с границы 4 байт, длина кратна 4. */
static WORD_ALIGNED_ATTR uint8_t s_tx[LINK_BYTES];
static WORD_ALIGNED_ATTR uint8_t s_rx[LINK_BYTES];

static spi_link_stat_t s_stat;

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

static void check(const uint8_t *rx, size_t bits, uint16_t *last_seq, bool *have_seq)
{
    s_stat.total++;
    memcpy(s_stat.last, rx, LINK_BYTES);

    if (bits < LINK_BYTES * 8) {
        s_stat.truncated++;
        return;
    }
    if (word_at(rx, 0) != MAGIC_F28) {
        s_stat.noMagic++;
        return;
    }

    uint16_t seq = word_at(rx, 1);
    for (int i = 2; i < LINK_BYTES / 2; i++) {
        if (word_at(rx, i) != (uint16_t)(seq + i * 0x0101u)) {
            s_stat.bad++;
            return;
        }
    }

    if (*have_seq && seq != (uint16_t)(*last_seq + 1)) {
        s_stat.gap++;
    } else {
        s_stat.ok++;
    }
    *last_seq = seq;
    *have_seq = true;
}

static void link_task(void *arg)
{
    (void)arg;
    uint16_t last_seq = 0;
    bool have_seq = false;

    /* До первой посылки отвечать нечем: метка без эха. Прибор посчитает это
     * как seqMiss, а не как ошибку данных. */
    memset(s_tx, 0, sizeof s_tx);
    put_word(s_tx, 0, MAGIC_ESP);

    for (;;) {
        spi_slave_transaction_t t = {
            .length    = LINK_BYTES * 8,
            .tx_buffer = s_tx,
            .rx_buffer = s_rx,
        };
        memset(s_rx, 0, sizeof s_rx);

        esp_err_t err = spi_slave_transmit(BOARD_SPI_HOST, &t, portMAX_DELAY);
        if (err != ESP_OK) {
            ESP_LOGE(TAG, "spi_slave_transmit: %s", esp_err_to_name(err));
            continue;
        }

        check(s_rx, t.trans_len, &last_seq, &have_seq);

        /* Следующий ответ - эхо этой посылки. Транзакция закончена, буфер
         * передачи DMA больше не читает - менять его можно. */
        memcpy(s_tx, s_rx, LINK_BYTES);
        put_word(s_tx, 0, MAGIC_ESP);
    }
}

void spi_link_start(void)
{
    gpio_config_t rdy = {
        .pin_bit_mask = 1ULL << BOARD_RDY_GPIO,
        .mode         = GPIO_MODE_OUTPUT,
    };
    ESP_ERROR_CHECK(gpio_config(&rdy));
    gpio_set_level(BOARD_RDY_GPIO, 1);

    spi_bus_config_t bus = {
        .mosi_io_num   = BOARD_SPI_MOSI,
        .miso_io_num   = BOARD_SPI_MISO,
        .sclk_io_num   = BOARD_SPI_SCLK,
        .quadwp_io_num = -1,
        .quadhd_io_num = -1,
    };
    spi_slave_interface_config_t slv = {
        .mode          = BOARD_SPI_MODE,
        .spics_io_num  = BOARD_SPI_CS,
        .queue_size    = 1,
        .post_setup_cb = on_setup,
        .post_trans_cb = on_done,
    };

    /* Без подтяжек провода висят, пока прибор не настроил SPIA: CS поплыл бы
     * вниз, и ведомый принял бы шум за посылку. */
    gpio_set_pull_mode(BOARD_SPI_MOSI, GPIO_PULLUP_ONLY);
    gpio_set_pull_mode(BOARD_SPI_SCLK, GPIO_PULLUP_ONLY);
    gpio_set_pull_mode(BOARD_SPI_CS, GPIO_PULLUP_ONLY);

    ESP_ERROR_CHECK(spi_slave_initialize(BOARD_SPI_HOST, &bus, &slv, SPI_DMA_CH_AUTO));

    xTaskCreatePinnedToCore(link_task, "spi_link", 4096, NULL, 10, NULL, 1);
    ESP_LOGI(TAG, "ведомый SPI: режим %d, SCLK %d, MOSI %d, MISO %d, CS %d, RDY %d",
             BOARD_SPI_MODE, BOARD_SPI_SCLK, BOARD_SPI_MOSI, BOARD_SPI_MISO,
             BOARD_SPI_CS, BOARD_RDY_GPIO);
}

void spi_link_get_stat(spi_link_stat_t *out)
{
    *out = s_stat;
}

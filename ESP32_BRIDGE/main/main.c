/*
 * main.c
 *
 * Мост SPI <-> WiFi для прибора K57. Этап 1: посылка с кадрами, разбор и
 * счётчики. Раз в секунду в консоль (COM-порт платы, 115200) печатается,
 * сколько кадров каждого номера пришло.
 */
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "esp_log.h"

#include "board.h"
#include "spi_link.h"

static const char *TAG = "bridge";

#define D(f) ((unsigned long)(s.f - p.f))

void app_main(void)
{
    ESP_LOGI(TAG, "мост K57, плата %s, этап 1: кадры по SPI", BOARD_NAME);
    spi_link_start();

    spi_link_stat_t p = {0};
    for (;;) {
        vTaskDelay(pdMS_TO_TICKS(1000));

        spi_link_stat_t s;
        spi_link_get_stat(&s);
        ESP_LOGI(TAG, "за 1 с: посылок %lu | 103: %lu, 140: %lu, 1: %lu, 132: %lu, "
                      "прочих %lu | заполнение %u, макс %u байт",
                 D(packets), D(fr103), D(fr140), D(fr1), D(fr132), D(frOther),
                 s.lastFill, s.maxFill);
        ESP_LOGI(TAG, "    всего: разрывов 103 %lu, пропусков посылок %lu, "
                      "не влезло на приборе %lu, обрыв %lu, без метки %lu, битых %lu",
                 (unsigned long)s.tickGap, (unsigned long)s.seqGap,
                 (unsigned long)s.f28Drop, (unsigned long)s.truncated,
                 (unsigned long)s.badMagic, (unsigned long)s.badLayout);
        p = s;
    }
}

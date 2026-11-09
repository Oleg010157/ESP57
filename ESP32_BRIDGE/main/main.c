/*
 * main.c
 *
 * Мост SPI <-> WiFi для прибора K57. Этап 1: только проверка SPI-канала.
 * Раз в секунду в консоль (COM-порт платы, 115200) печатаются счётчики.
 */
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "esp_log.h"

#include "board.h"
#include "spi_link.h"

static const char *TAG = "bridge";

void app_main(void)
{
    ESP_LOGI(TAG, "мост K57, плата %s, этап 1: проверка SPI", BOARD_NAME);
    spi_link_start();

    spi_link_stat_t prev = {0};
    for (;;) {
        vTaskDelay(pdMS_TO_TICKS(1000));

        spi_link_stat_t s;
        spi_link_get_stat(&s);
        ESP_LOGI(TAG, "за 1 с: %lu посылок | всего ок %lu, нет метки %lu, "
                      "узор %lu, разрыв %lu, обрыв %lu",
                 (unsigned long)(s.total - prev.total), (unsigned long)s.ok,
                 (unsigned long)s.noMagic, (unsigned long)s.bad,
                 (unsigned long)s.gap, (unsigned long)s.truncated);
        if (s.total != prev.total) {
            ESP_LOG_BUFFER_HEX(TAG, s.last, 8);
        }
        prev = s;
    }
}

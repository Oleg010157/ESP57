/*
 * board.h
 *
 * Всё железозависимое моста. Остальной код о пинах не знает.
 * Провода к LaunchPad F28P55x - docs/esp32-spi-wiring-RU.md.
 */
#pragma once

#define BOARD_NAME "ESP32-Core-Board-V2"

/* Ведомый SPI на родных ножках VSPI (SPI3). Через IOMUX сигнал идёт мимо
 * матрицы GPIO - это запас по времени до 10 МГц. */
#define BOARD_SPI_HOST SPI3_HOST
#define BOARD_SPI_SCLK 18 /* F28 GPIO9,  J1.7  */
#define BOARD_SPI_MOSI 23 /* F28 GPIO8,  J2.15 - PICO */
#define BOARD_SPI_MISO 19 /* F28 GPIO10, J4.36 - POCI */
#define BOARD_SPI_CS   5  /* F28 GPIO11, J4.35 */

/* «Есть данные», активный низкий. У прибора на этом входе подтяжка вверх. */
#define BOARD_RDY_GPIO 22 /* F28 GPIO55, J5.48 */

/* Режим 1 (CPOL 0, CPHA 1): ведомый SPI с DMA у ESP32 работает только в
 * режимах 1 и 3. У прибора ему соответствует SPI_PROT_POL0PHA0 - нумерация
 * фазы у TI обратная, разбор в docs/esp32-spi-wiring-RU.md. */
#define BOARD_SPI_MODE 1

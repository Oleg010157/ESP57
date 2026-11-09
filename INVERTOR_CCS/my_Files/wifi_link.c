/*
 * wifi_link.c
 *
 * Проверка канала к ESP32 по SPIA. Смысл посылки и счётчиков - в wifi_link.h.
 */

#include "board.h"
#include "driverlib.h"
#include "device.h"
#include "wifi_link.h"

#define WL_MAGIC_F28 0xA55AU
#define WL_MAGIC_ESP 0x5AA5U

// Без отладчика 32 байта на 4,17 МГц - около 500 проходов цикла ожидания.
// С запасом на порядок: дольше ждать уже нечего, SPIA стоит.
#define WL_WAIT_LIMIT 5000U

volatile WifiLinkStat wifiLink;

static uint16_t tx[WIFI_LINK_WORDS];
static uint16_t prev[WIFI_LINK_WORDS]; // что ушло прошлый раз - с ним сверяется эхо
static uint16_t prevValid = 0;

static void fill(uint16_t seq)
{
    uint16_t i;
    tx[0] = WL_MAGIC_F28;
    tx[1] = seq;
    for (i = 2U; i < WIFI_LINK_WORDS; i++)
    {
        // Узор меняется от слова к слову и от посылки к посылке: застрявший
        // бит, сдвиг на бит и повтор старой посылки дают разные картины.
        tx[i] = (uint16_t)(seq + i * 0x0101U);
    }
}

void WifiLink_tick50ms(void)
{
    uint16_t i;
    uint16_t wait = 0;
    uint16_t rx[WIFI_LINK_WORDS];

    fill(wifiLink.seq);

    // Остатки от прерванного обмена (например, остановки отладчиком) сдвинули
    // бы весь ответ на слово - выбросить.
    while (SPI_getRxFIFOStatus(wifiSPI_BASE) != SPI_FIFO_RXEMPTY)
    {
        (void)SPI_readDataNonBlocking(wifiSPI_BASE);
    }
    SPI_clearInterruptStatus(wifiSPI_BASE, SPI_INT_RXFF_OVERFLOW);

    wifiLink.rdy = (uint16_t)GPIO_readPin(wifi_rdy);

    // CS программный: держится низким всю посылку, сколько бы она ни длилась.
    // Пауза после CS - время ведомому ESP32 заметить выбор кадра до первого
    // фронта CLK.
    GPIO_writePin(wifi_cs, 0);
    DEVICE_DELAY_US(2);

    for (i = 0U; i < WIFI_LINK_WORDS; i++)
    {
        SPI_writeDataNonBlocking(wifiSPI_BASE, tx[i]);
    }

    // 16 принятых слов = 16 переданных: последний бит уже ушёл по проводу.
    while ((SPI_getRxFIFOStatus(wifiSPI_BASE) != SPI_FIFO_RX16) &&
           (wait < WL_WAIT_LIMIT))
    {
        wait++;
    }

    GPIO_writePin(wifi_cs, 1);

    if (wait >= WL_WAIT_LIMIT)
    {
        wifiLink.timeout++;
        prevValid = 0;
        return;
    }

    for (i = 0U; i < WIFI_LINK_WORDS; i++)
    {
        rx[i]          = SPI_readDataNonBlocking(wifiSPI_BASE);
        wifiLink.rx[i] = rx[i];
    }

    if (rx[0] != WL_MAGIC_ESP)
    {
        wifiLink.noEsp++;
    }
    else if (!prevValid || (rx[1] != prev[1]))
    {
        // Первая посылка после старта или после сбоя: сверять не с чем.
        wifiLink.seqMiss++;
    }
    else
    {
        uint16_t same = 1U;
        for (i = 2U; i < WIFI_LINK_WORDS; i++)
        {
            if (rx[i] != prev[i])
            {
                same = 0U;
            }
        }
        if (same)
        {
            wifiLink.ok++;
        }
        else
        {
            wifiLink.bad++;
        }
    }

    for (i = 0U; i < WIFI_LINK_WORDS; i++)
    {
        prev[i] = tx[i];
    }
    prevValid = 1U;
    wifiLink.seq++;
}

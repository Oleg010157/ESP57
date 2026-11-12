/*****************************************************************************
 * boot_spi.c
 *
 * Канал загрузчика по SPIA до WiFi-моста. Зачем и как - boot_spi.h.
 *
 * ПРАВИЛА ТЕ ЖЕ, ЧТО У can_bootloader.c. Весь код - .TI.ramfunc, к железу -
 * только HWREG/HWREGH. Ни GPIO_writePin, ни SPI_*, ни DMA_*: при -Ooff эти
 * inline-функции driverlib компилятор выносит в .text этого файла, во флеш,
 * и первый же стёртый сектор их унесёт. Проверка после правки - та же: в
 * CPU1_FLASH/invertor.map у boot_spi.obj нет ни одной строки с (.text).
 *
 * SPIA и ножки настроены приложением (SysConfig, wifiSPI) и так и остаются:
 * ведущий, 16-битное слово, FIFO. Загрузчик снимает только DMA и дальше
 * гонит слова через FIFO сам.
 *****************************************************************************/

#include "driverlib.h"
#include "device.h"
#include "board.h"

#include "hw_spi.h"
#include "hw_gpio.h"
#include "hw_dma.h"

#include "can_boot_protocol.h"
#include "boot_spi.h"

#pragma CODE_SECTION(boot_spi_init,     ".TI.ramfunc")
#pragma CODE_SECTION(boot_spi_take,     ".TI.ramfunc")
#pragma CODE_SECTION(boot_spi_send,     ".TI.ramfunc")
#pragma CODE_SECTION(boot_spi_flush,    ".TI.ramfunc")
#pragma CODE_SECTION(boot_spi_exchange, ".TI.ramfunc")
#pragma CODE_SECTION(boot_spi_crc16,    ".TI.ramfunc")
#pragma CODE_SECTION(boot_spi_wait,     ".TI.ramfunc")

/* Посылка - как у рабочего канала (wifi_link.c): 256 слов, заголовок 4. */
#define BS_WORDS    256U
#define BS_HDR      4U
#define BS_LAST     (BS_WORDS - 1U)
#define BS_PAYLOAD  (BS_LAST - BS_HDR)

#define BS_MAGIC_F28 0xA55AU
#define BS_MAGIC_ESP 0x5AA5U

/* Кадров от моста про запас. Мост держит в пути не больше BOOT_SPI_WINDOW
 * кадров данных (fwup.c), плюс команды - восьми хватает с избытком, как
 * восьми элементов RX FIFO0 у CAN. */
#define BS_RXQ      8U

/* Ножки - по номерам из board.h (SysConfig). Номер сменили там - здесь
 * сменится сам, а не тот порт - скажет препроцессор. */
#if (wifi_cs >= 32U) || (wifi_rdy < 32U) || (wifi_rdy >= 64U)
#error "boot_spi.c: wifi_cs ждётся в порту A, wifi_rdy - в порту B"
#endif
#define BS_CS_MASK  (1UL << wifi_cs)
#define BS_RDY_MASK (1UL << (wifi_rdy - 32U))

/* Ожидания счётом, как всё в загрузчике. Посылка идёт около миллисекунды
 * (слово - 3,8 мкс), мост заряжает следующую за десятки микросекунд. */
#define BS_RDY_SPIN   200000UL  /* мост не готов - обмен пропускаем */
#define BS_WORD_SPIN  20000UL   /* слово не пришло - посылка оборвана */

/* DMA рабочему каналу не нужен, а буферам место всё равно в RAMGS2: .bss
 * загрузчика лежит в RAMLS5, а та заполнена почти до конца. */
#pragma DATA_SECTION(bsTx, "ramgs2");
#pragma DATA_SECTION(bsRx, "ramgs2");
#pragma DATA_SECTION(bsQ, "ramgs2");
static uint16_t bsTx[BS_WORDS];
static uint16_t bsRx[BS_WORDS];
static uint16_t bsQ[BS_RXQ][1U + BOOT_DATA_WORDS]; /* длина в байтах, слова */

static uint16_t bsTxLen = 0U;   /* слов кадров в bsTx */
static uint16_t bsSeq   = 0U;
static uint16_t bsQHead = 0U;
static uint16_t bsQTail = 0U;
static uint16_t bsQCount = 0U;

/*---------------------------------------------------------------------------
 * Пауза счётом - DEVICE_DELAY_US во флеше. Как boot_delay_stub().
 *---------------------------------------------------------------------------*/
static void boot_spi_wait(uint32_t us)
{
    uint32_t i;
    uint32_t count = us * 37UL;

    for (i = 0U; i < count; i++)
    {
        __asm(" NOP");
    }
}

/*---------------------------------------------------------------------------
 * CRC-16/CCITT-FALSE, как у моста и у wifi_link.c, но без таблицы: таблица -
 * константа во флеше. Слово целиком за раз - то же, что старший байт, потом
 * младший: младший байт до конца первых восьми сдвигов в обратную связь не
 * попадает. 255 слов - около 0,3 мс, на фоне посылки в миллисекунду терпимо.
 *---------------------------------------------------------------------------*/
static uint16_t boot_spi_crc16(const uint16_t *w, uint16_t n)
{
    uint16_t crc = 0xFFFFU;
    uint16_t i;
    uint16_t k;

    for (i = 0U; i < n; i++)
    {
        crc ^= w[i];
        for (k = 0U; k < 16U; k++)
        {
            crc = (crc & 0x8000U) ? (uint16_t)((crc << 1) ^ 0x1021U) : (uint16_t)(crc << 1);
        }
    }
    return crc;
}

/*---------------------------------------------------------------------------
 * Обмен одной посылкой. false - мост не готов или посылка оборвалась; тогда
 * накопленное к мосту остаётся и уйдёт следующей.
 *---------------------------------------------------------------------------*/
static bool boot_spi_exchange(void)
{
    uint32_t spin;
    uint16_t i;
    uint16_t j;
    uint16_t w;
    uint16_t end;

    /* «Есть данные» низкая - транзакция у моста заряжена. */
    for (spin = 0U; (HWREG(GPIODATA_BASE + GPIO_O_GPBDAT) & BS_RDY_MASK) != 0U; spin++)
    {
        if (spin >= BS_RDY_SPIN)
        {
            return false;
        }
    }

    bsTx[0] = BS_MAGIC_F28;
    bsTx[1] = bsSeq++;
    bsTx[2] = (uint16_t)(bsTxLen * 2U);
    bsTx[3] = 0U;
    bsTx[BS_LAST] = 0U;

    /* Остаток в FIFO от оборванной посылки сдвинул бы приём на слово. */
    HWREGH(SPIA_BASE + SPI_O_FFTX) &= ~SPI_FFTX_TXFIFO;
    HWREGH(SPIA_BASE + SPI_O_FFTX) |= SPI_FFTX_TXFIFO;
    HWREGH(SPIA_BASE + SPI_O_FFRX) &= ~SPI_FFRX_RXFIFORESET;
    HWREGH(SPIA_BASE + SPI_O_FFRX) |= SPI_FFRX_RXFIFORESET | SPI_FFRX_RXFFOVFCLR;

    /* CS программный, как в wifi_link.c: микросекунда ведомому заметить
     * выбор до первого фронта CLK. */
    HWREG(GPIODATA_BASE + GPIO_O_GPACLEAR) = BS_CS_MASK;
    boot_spi_wait(1U);

    /* Слово уходит только вместе с приходящим, поэтому впереди приёма
     * передача держится не больше чем на FIFO (16 слов). */
    i = 0U;
    j = 0U;
    spin = 0U;
    while (j < BS_WORDS)
    {
        if ((i < BS_WORDS) &&
            (((HWREGH(SPIA_BASE + SPI_O_FFTX) & SPI_FFTX_TXFFST_M) >> SPI_FFTX_TXFFST_S) < 16U))
        {
            HWREGH(SPIA_BASE + SPI_O_TXBUF) = bsTx[i];
            i++;
        }
        if ((HWREGH(SPIA_BASE + SPI_O_FFRX) & SPI_FFRX_RXFFST_M) != 0U)
        {
            bsRx[j] = HWREGH(SPIA_BASE + SPI_O_RXBUF);
            j++;
            spin = 0U;
        }
        else if (++spin >= BS_WORD_SPIN)
        {
            break;
        }
    }

    HWREG(GPIODATA_BASE + GPIO_O_GPASET) = BS_CS_MASK;

    if (j < BS_WORDS)
    {
        return false;
    }
    bsTxLen = 0U; /* ушло целиком */

    /* Без верной метки и CRC не берём ничего: битый кадр данных лёг бы во
     * флеш, а битый адрес входа увёл бы прибор неизвестно куда. */
    if ((bsRx[0] != BS_MAGIC_ESP) ||
        (boot_spi_crc16(bsRx, BS_LAST) != bsRx[BS_LAST]) ||
        ((bsRx[2] & 1U) != 0U) || ((bsRx[2] / 2U) > BS_PAYLOAD))
    {
        return true; /* обмен был, кадров нет */
    }

    w   = BS_HDR;
    end = (uint16_t)(BS_HDR + bsRx[2] / 2U);
    while ((uint16_t)(w + 2U) <= end)
    {
        uint16_t id    = bsRx[w];
        uint16_t bytes = bsRx[w + 1U];
        uint16_t n     = (uint16_t)(bytes / 2U);

        if (((bytes & 1U) != 0U) || ((uint16_t)(w + 2U + n) > end))
        {
            break;
        }
        if ((id == BOOT_CAN_RX_ID) && (bytes <= BOOT_DATA_BYTES) && (bsQCount < BS_RXQ))
        {
            uint16_t *q = bsQ[bsQHead];
            uint16_t  k;

            q[0] = bytes;
            for (k = 0U; k < n; k++)
            {
                q[1U + k] = bsRx[w + 2U + k];
            }
            bsQHead = (uint16_t)((bsQHead + 1U) % BS_RXQ);
            bsQCount++;
        }
        w = (uint16_t)(w + 2U + n);
    }

    return true;
}

void boot_spi_init(void)
{
    /* DMA рабочего канала: передача - CH5 (wifi_link.c), приём - CH6
     * (SysConfig, wifiSPI_RX_DMA). Пачки от событий SPI им больше не
     * положены - FIFO теперь читает и пишет процессор. */
    EALLOW;
    HWREGH(DMA_CH5_BASE + DMA_O_CONTROL) = DMA_CONTROL_HALT;
    HWREGH(wifiSPI_RX_DMA_BASE + DMA_O_CONTROL) = DMA_CONTROL_HALT;
    EDIS;

    /* Приложение могло уйти сюда посреди посылки: CS поднять и дать мосту
     * отбросить оборванную транзакцию и зарядить новые. */
    HWREG(GPIODATA_BASE + GPIO_O_GPASET) = BS_CS_MASK;
    boot_spi_wait(5000U);

    bsTxLen  = 0U;
    bsQHead  = 0U;
    bsQTail  = 0U;
    bsQCount = 0U;
}

bool boot_spi_take(volatile uint16_t *dst, volatile uint16_t *bytes)
{
    uint16_t *q;
    uint16_t  n;
    uint16_t  k;

    if (bsQCount == 0U)
    {
        (void)boot_spi_exchange();
        if (bsQCount == 0U)
        {
            return false;
        }
    }

    q = bsQ[bsQTail];
    n = (uint16_t)(q[0] / 2U);
    for (k = 0U; k < n; k++)
    {
        dst[k] = q[1U + k];
    }
    *bytes = q[0];

    bsQTail = (uint16_t)((bsQTail + 1U) % BS_RXQ);
    bsQCount--;
    return true;
}

void boot_spi_send(uint16_t w0, uint16_t w1, uint16_t w2, uint16_t w3)
{
    uint16_t *p;

    /* Места нет - отдать накопленное. Мост не ответил - выбросить: маркеры
     * хода и повторы квитанций дороже ожидания не стоят, а ждать без конца
     * загрузчику нельзя. */
    if ((uint16_t)(bsTxLen + 2U + BOOT_CMD_WORDS) > BS_PAYLOAD)
    {
        if (!boot_spi_exchange())
        {
            bsTxLen = 0U;
        }
    }

    p = &bsTx[BS_HDR + bsTxLen];
    p[0] = BOOT_CAN_TX_ID;
    p[1] = (uint16_t)(BOOT_CMD_WORDS * 2U);
    p[2] = w0;
    p[3] = w1;
    p[4] = w2;
    p[5] = w3;
    bsTxLen = (uint16_t)(bsTxLen + 2U + BOOT_CMD_WORDS);
}

void boot_spi_flush(void)
{
    uint16_t tries;

    for (tries = 0U; (bsTxLen != 0U) && (tries < 20U); tries++)
    {
        (void)boot_spi_exchange();
    }
}

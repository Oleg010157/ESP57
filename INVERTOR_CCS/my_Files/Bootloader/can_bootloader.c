/*****************************************************************************
 * can_bootloader.c
 *
 * CAN-загрузчик F28P55x. Живёт в .TI.ramfunc, то есть исполняется из ОЗУ.
 * Стирает и программирует BANK0..BANK3 через Flash API.
 *
 * СЦЕНАРИЙ
 *   1. Приложение получает по CAN команду 9999 (KSP/MBOX28.c), та взводит
 *      request_firmware_update.
 *   2. Фоновый цикл main() зовёт app_request_firmware_update(): закрывает IGBT,
 *      запрещает прерывания и переходит сюда.
 *   3. bootloader_main() перенастраивает MCAN под себя и ведёт обмен с ПК:
 *      связь -> стирание -> приём и запись -> сверка суммы -> пуск приложения.
 *
 * Тот же обмен идёт и по SPI до WiFi-моста - вход кадром BOOT_SPI_ENTER от
 * моста, канал в boot_spi.c. Различаются только boot_poll_rx() и
 * boot_can_send_frame(); всё остальное в этом файле от канала не зависит.
 *
 * ПОЧЕМУ ВСЁ ИЗ ОЗУ. Стираются ВСЕ банки с кодом, включая тот, откуда шла бы
 * выборка команд. В ОЗУ обязаны лежать три вещи, и все три там лежат:
 *   - код этого файла (pragma CODE_SECTION ниже);
 *   - библиотека Flash API (втянута в .TI.ramfunc линкером,
 *     см. 28p55x_generic_flash_lnk.cmd);
 *   - горячий путь обмена по CAN - поэтому он написан прямыми обращениями к
 *     регистрам, а не вызовами driverlib: его .text остаётся во флеше.
 * Из driverlib законно звать только то, что помечено .TI.ramfunc (Flash_*), и
 * только до первого стирания - настройку MCAN, пока флеш ещё читается.
 *
 * BANK4 (0x100000) не стирается никогда: там калибровки и настройка прибора.
 * Защита не программная, а по построению - диапазон зажат APP_END_ADDR.
 *
 * Автор: Олег Павловский
 * Перенос с F280039C/DCAN на F28P55x/MCAN: 29.09.2026
 *****************************************************************************/

#include "driverlib.h"
#include "device.h"
#include "board.h"
#include "f28p55x_device.h"

#include "hw_mcan.h"
#include "hw_mcanss.h"
#include "mcan.h"

#include "FlashTech_F28P55x_C28x.h"
#include "flash_eeprom/flash_programming_f28p55x.h"

#include "can_boot_protocol.h"
#include "boot_spi.h"

/*---------------------------------------------------------------------------
 * Раскладка Message RAM под загрузчик.
 *
 * Своя, а не приложенческая: у приложения элементы RX FIFO0 по 8 байт
 * (board.c: rxFIFO0ElemSize = 0), и 64-байтный кадр данных туда физически не
 * помещается. Адреса - смещения В БАЙТАХ от начала Message RAM, как их
 * понимает MCAN_msgRAMConfig() (сравни с myMCANA_MCAN_* в board.h).
 *
 * Размер элемента = 8 байт заголовка + размер данных.
 * Всего выходит 596 байт при доступных 0x1600 - места с запасом.
 *---------------------------------------------------------------------------*/
#define BOOT_RAM_FILTER_ADDR    0U      /* один стандартный фильтр = 4 байта */
#define BOOT_RAM_RXF0_ADDR      4U
#define BOOT_RAM_RXF0_NUM       8U
#define BOOT_RAM_RXF0_ELEM      72U     /* 8 + 64 */
#define BOOT_RAM_TXBUF_ADDR     (BOOT_RAM_RXF0_ADDR + (BOOT_RAM_RXF0_NUM * BOOT_RAM_RXF0_ELEM))

/* Кадры MCU -> ПК всегда служебные, 8 байт: элемент 8 + 8 = 16 байт. */
#define BOOT_TX_DLC             8U

/*---------------------------------------------------------------------------
 * Доступ к регистрам: только HWREG, и никогда HW_RD_REG32 / HW_WR_REG32.
 *
 * ЭТО НЕ СТИЛЬ, А УСЛОВИЕ РАБОТОСПОСОБНОСТИ. HWREG - чистый макрос разыменования
 * (inc/hw_types.h), разворачивается в одну инструкцию прямо здесь. HW_RD_REG32
 * же обёрнут вокруг static inline функции HW_RD_REG32_RAW, и при -Ooff, с
 * которым собран проект, компилятор выносит её отдельной функцией в .text -
 * то есть во флеш. Горячий путь обмена тогда на каждом обращении к регистру
 * прыгает в стираемый банк.
 *
 * Обнаруживается это только по карте памяти: собирается и линкуется такой код
 * без единого замечания, а падает уже на первом секторе, когда вернуться и
 * посмотреть некуда.
 *
 * ПРОВЕРКА ПОСЛЕ ЛЮБОЙ ПРАВКИ ЭТОГО ФАЙЛА: в invertor.map у can_bootloader.obj
 * не должно быть НИ ОДНОЙ строки с (.text) - только (.TI.ramfunc), (.bss) и
 * (.data). Появилась - значит очередная driverlib-обёртка приехала во флеш, и
 * её надо заменить прямым обращением к регистру.
 *---------------------------------------------------------------------------*/
#define BOOT_RD32(addr)        HWREG(addr)
#define BOOT_WR32(addr, val)   (HWREG(addr) = (uint32_t)(val))

/*---------------------------------------------------------------------------
 * Весь код загрузчика исполняется из ОЗУ - флеш под ним стирается.
 *---------------------------------------------------------------------------*/
#pragma CODE_SECTION(bootloader_main,             ".TI.ramfunc")
#pragma CODE_SECTION(bootloader_erase_app_flash,  ".TI.ramfunc")
#pragma CODE_SECTION(bootloader_program_block,    ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_init,               ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_send_frame,         ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_send_word,          ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_recv_frame,         ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_recv_word,          ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_recv_word_blocking, ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_wait_word,          ".TI.ramfunc")
#pragma CODE_SECTION(boot_poll_rx,                ".TI.ramfunc")
#pragma CODE_SECTION(boot_dlc_to_bytes,           ".TI.ramfunc")
#pragma CODE_SECTION(boot_service_watchdog,       ".TI.ramfunc")
#pragma CODE_SECTION(boot_wait_fsm_ready,         ".TI.ramfunc")
#pragma CODE_SECTION(boot_clear_fsm_status,       ".TI.ramfunc")
#pragma CODE_SECTION(boot_flash_unprotect,        ".TI.ramfunc")
#pragma CODE_SECTION(boot_send_error_word,        ".TI.ramfunc")
#pragma CODE_SECTION(boot_wait_recovery_cmd_erase, ".TI.ramfunc")
#pragma CODE_SECTION(boot_delay_stub,             ".TI.ramfunc")
#pragma CODE_SECTION(boot_jump_to_app,            ".TI.ramfunc")
#pragma CODE_SECTION(boot_abort_to_app,           ".TI.ramfunc")

/*---------------------------------------------------------------------------
 * Состояние приёма. Буфер на полный кадр данных - 32 слова.
 *---------------------------------------------------------------------------*/
static volatile uint16_t boot_rxBuf[BOOT_DATA_WORDS];
static volatile uint16_t boot_rxBytes = 0;   /* сколько байт в кадре по DLC */
volatile uint16_t        boot_rx_ready = 0;

/* Канал: 0 - CAN, иначе SPI до WiFi-моста (boot_spi.h). Выбирается при входе
 * и дальше не меняется. Протокол поверх обоих один и тот же. */
static uint16_t boot_via_spi = 0U;

/* Диапазон стирания, запрошенный ПК (адреса слов, включительно).
 * По умолчанию - вся область приложения. */
static uint32_t boot_erase_start_addr = APP_START_ADDR;
static uint32_t boot_erase_end_addr   = APP_END_ADDR;

/*---------------------------------------------------------------------------
 * Сторожевой таймер.
 *
 * Сегодня это пустая работа: Device_init() зовёт SysCtl_disableWatchdog(), и
 * запись в WDKEY ничего не сбрасывает. Вызовы оставлены в длинных циклах на
 * случай, если сторож когда-нибудь включат - тогда стирание флеша не должно
 * оказаться первым, что об этом узнает.
 *---------------------------------------------------------------------------*/
static void boot_service_watchdog(void)
{
    EALLOW;
    WdRegs.WDKEY.all = 0x0055U;
    WdRegs.WDKEY.all = 0x00AAU;
    EDIS;
}

/*---------------------------------------------------------------------------
 * Задержка счётом. DEVICE_DELAY_US звать нельзя - она во флеше.
 * Коэффициент подобран на глаз под 150 МГц и точности не требует: паузы нужны
 * только чтобы кадры диагностики не слипались на шине.
 *---------------------------------------------------------------------------*/
static void boot_delay_stub(uint32_t us)
{
    uint32_t i;
    uint32_t count = us * 37UL;

    for (i = 0U; i < count; i++)
    {
        __asm(" NOP");
    }
}

/*---------------------------------------------------------------------------
 * Переход в приложение по адресу _c_int00, полученному от ПК.
 *
 * _c_int00 сам выставит SP на __stack, разложит .cinit и скопирует .TI.ramfunc
 * из флеша в ОЗУ - то есть затрёт в том числе этот самый код. Возврата отсюда
 * нет и быть не может, поэтому обычный вызов по указателю ничем не хуже
 * настоящего перехода: два слова, оставшиеся на стеке, всё равно пропадут при
 * переустановке SP.
 *
 * Именно вызов, а не PUSH AL/AH + LRET из версии для F280039C: та схема молча
 * опиралась на то, что аргумент лежит в ACC, чего компилятор при смене уровня
 * оптимизации не обязан соблюдать.
 *---------------------------------------------------------------------------*/
static void boot_jump_to_app(uint32_t entryAddr)
{
    void (*appEntry)(void) = (void (*)(void))entryAddr;

    DINT;
    appEntry();
}

/*---------------------------------------------------------------------------
 * Отмена до стирания (CMD_ABORT): квитанция и сброс - прибор поднимается на
 * прежней прошивке, флеш ещё не тронут. Сброс - сторожем, теми же двумя
 * записями, что в app_boot_request.c: SysCtl_resetDevice() лежит во флеше.
 *---------------------------------------------------------------------------*/
static void boot_abort_to_app(void)
{
    boot_can_send_word(ACK_ABORT);
    if (boot_via_spi)
    {
        boot_spi_flush();
    }
    boot_delay_stub(10000U);

    EALLOW;
    WdRegs.WDCR.all = 0x0028U;
    WdRegs.WDCR.all = 0x0000U;
    EDIS;

    for (;;)
    {
    }
}

/*---------------------------------------------------------------------------
 * DLC -> число байт. Свыше восьми шкала нелинейная (CAN-FD).
 *---------------------------------------------------------------------------*/
static uint16_t boot_dlc_to_bytes(uint16_t dlc)
{
    if (dlc <= 8U)
    {
        return dlc;
    }

    switch (dlc)
    {
        case 9U:  return 12U;
        case 10U: return 16U;
        case 11U: return 20U;
        case 12U: return 24U;
        case 13U: return 32U;
        case 14U: return 48U;
        default:  return 64U;
    }
}

/*---------------------------------------------------------------------------
 * Приём: опрос RX FIFO0 напрямую по регистрам.
 *
 * Прерываний нет (в bootloader_main стоит IER = 0), поэтому забирать кадр
 * некому кроме этой функции. Порядок обязательный: прочитать элемент по
 * индексу F0GI, и только потом подтвердить его записью в RXF0A - подтверждение
 * освобождает элемент, и читать его после уже нельзя.
 *---------------------------------------------------------------------------*/
static void boot_poll_rx(void)
{
    uint32_t status;
    uint32_t idx;
    uint32_t elemAddr;
    uint32_t r1;
    uint16_t bytes;
    uint16_t words32;
    uint16_t i;

    /* По SPI кадры ждут своей очереди в boot_spi.c - берём следующий, только
     * когда прежний разобран. */
    if (boot_via_spi)
    {
        if (!boot_rx_ready && boot_spi_take(boot_rxBuf, &boot_rxBytes))
        {
            boot_rx_ready = 1U;
        }
        return;
    }

    status = BOOT_RD32(myMCANA_BASE + MCAN_RXF0S);

    if ((status & MCAN_RXF0S_F0FL_MASK) == 0U)
    {
        return;                      /* очередь пуста */
    }

    idx = (status & MCAN_RXF0S_F0GI_MASK) >> MCAN_RXF0S_F0GI_SHIFT;

    elemAddr = myMCANA_BASE + MCAN_MCAN_MSG_MEM + BOOT_RAM_RXF0_ADDR +
               (idx * BOOT_RAM_RXF0_ELEM);

    /* Слово R1 элемента: DLC в битах 19:16. Слово R0 (идентификатор) не
     * читаем - фильтр пропускает в FIFO0 только BOOT_CAN_RX_ID. */
    r1    = BOOT_RD32(elemAddr + 4U);
    bytes = boot_dlc_to_bytes((uint16_t)((r1 >> 16U) & 0x0FU));

    if (bytes > BOOT_DATA_BYTES)
    {
        bytes = BOOT_DATA_BYTES;
    }

    /* Данные лежат по четыре байта в 32-битном слове, младший байт - первый.
     * Пара байт = одно 16-битное слово протокола, два слова на слово RAM. */
    words32 = (uint16_t)((bytes + 3U) / 4U);

    for (i = 0U; i < words32; i++)
    {
        uint32_t d = BOOT_RD32(elemAddr + 8U + (uint32_t)(i * 4U));

        boot_rxBuf[i * 2U]      = (uint16_t)(d & 0xFFFFU);
        boot_rxBuf[i * 2U + 1U] = (uint16_t)(d >> 16U);
    }

    /* Подтверждаем приём - элемент свободен. */
    BOOT_WR32(myMCANA_BASE + MCAN_RXF0A, idx);

    boot_rxBytes  = bytes;
    boot_rx_ready = 1U;
}

/*---------------------------------------------------------------------------
 * Передача: служебный кадр 8 байт через выделенный Tx Buffer 0.
 *
 * Заголовок элемента (T0/T1) переписывается каждый раз - так функция не
 * зависит от того, что осталось в Message RAM от предыдущего кадра или от
 * приложения. FDF и BRS нулевые: MCU шлёт только короткие кадры, скорость
 * здесь не нужна, а классический кадр принимает и обычный, и FD-приёмник.
 *---------------------------------------------------------------------------*/
void boot_can_send_frame(uint16_t w0, uint16_t w1, uint16_t w2, uint16_t w3)
{
    uint32_t elemAddr = myMCANA_BASE + MCAN_MCAN_MSG_MEM + BOOT_RAM_TXBUF_ADDR;
    uint32_t spin     = 0U;

    if (boot_via_spi)
    {
        boot_spi_send(w0, w1, w2, w3);
        return;
    }

    /* Ждём, пока уйдёт предыдущий кадр. Таймаут нужен на случай обрыва шины:
     * без него первая же ошибка связи подвесила бы загрузчик навсегда. */
    while ((BOOT_RD32(myMCANA_BASE + MCAN_TXBRP) & 0x00000001UL) != 0U)
    {
        spin++;
        if (spin >= 2000000U)
        {
            return;
        }
    }

    BOOT_WR32(elemAddr,      ((uint32_t)BOOT_CAN_TX_ID) << 18U);  /* T0: 11-битный ID */
    BOOT_WR32(elemAddr + 4U, ((uint32_t)BOOT_TX_DLC) << 16U);     /* T1: DLC=8, FDF=0, BRS=0 */
    BOOT_WR32(elemAddr + 8U,  ((uint32_t)w1 << 16U) | (uint32_t)w0);
    BOOT_WR32(elemAddr + 12U, ((uint32_t)w3 << 16U) | (uint32_t)w2);

    BOOT_WR32(myMCANA_BASE + MCAN_TXBAR, 0x00000001UL);
}

void boot_can_send_word(uint16_t word)
{
    boot_can_send_frame(word, 0U, 0U, 0U);
}

/*---------------------------------------------------------------------------
 * Приём кадра целиком. Блокирующий: ждёт, пока кадр придёт.
 * Возвращает число принятых байт, слова кладёт в dst (не более maxWords).
 *---------------------------------------------------------------------------*/
uint16_t boot_can_recv_frame(uint16_t *dst, uint16_t maxWords)
{
    uint16_t bytes;
    uint16_t words;
    uint16_t i;

    while (!boot_rx_ready)
    {
        boot_poll_rx();
        boot_service_watchdog();
    }

    bytes = boot_rxBytes;
    words = (uint16_t)((bytes + 1U) / 2U);

    if (words > maxWords)
    {
        words = maxWords;
    }

    for (i = 0U; i < words; i++)
    {
        dst[i] = boot_rxBuf[i];
    }

    boot_rx_ready = 0U;
    return bytes;
}

/*---------------------------------------------------------------------------
 * Приём команды. Неблокирующий: 0 означает "кадра нет".
 *---------------------------------------------------------------------------*/
uint16_t boot_can_recv_word(void)
{
    uint16_t val;

    boot_poll_rx();

    if (!boot_rx_ready)
    {
        return 0U;
    }

    val           = boot_rxBuf[0];
    boot_rx_ready = 0U;
    return val;
}

uint16_t boot_can_recv_word_blocking(void)
{
    uint16_t val;

    while (!boot_rx_ready)
    {
        boot_poll_rx();
        boot_service_watchdog();
    }

    val           = boot_rxBuf[0];
    boot_rx_ready = 0U;
    return val;
}

/*---------------------------------------------------------------------------
 * Ожидание конкретной команды с ограничением по числу попыток.
 *---------------------------------------------------------------------------*/
bool boot_can_wait_word(uint16_t expected, uint32_t timeout_loops)
{
    uint32_t i;

    for (i = 0U; i < timeout_loops; i++)
    {
        if (boot_can_recv_word() == expected)
        {
            return true;
        }

        boot_service_watchdog();
        boot_delay_stub(100U);
    }

    return false;
}

/*---------------------------------------------------------------------------
 * Диагностический кадр повторяется несколько раз: одиночный ПК может не
 * заметить, если как раз перебирает очередь.
 *---------------------------------------------------------------------------*/
static void boot_send_error_word(uint16_t errorWord)
{
    uint32_t i;

    for (i = 0U; i < 16U; i++)
    {
        boot_can_send_word(errorWord);
        boot_delay_stub(1000U);
    }
}

/*---------------------------------------------------------------------------
 * Восстановление после отказа. Остаёмся в ОЗУ и ждём явного перезапуска с
 * фазы стирания. Ни сброса, ни перехода отсюда не делаем: во флеше сейчас
 * недописанный образ, и уйти туда значит потерять единственный способ
 * повторить прошивку без JTAG.
 *---------------------------------------------------------------------------*/
static void boot_wait_recovery_cmd_erase(void)
{
    uint32_t tick = 0U;

    for (;;)
    {
        if (boot_can_recv_word() == CMD_ERASE)
        {
            return;
        }

        /* Держим состояние видимым на шине. */
        if ((tick & 0x000003FFUL) == 0U)
        {
            boot_can_send_word(MARK_FAIL);
        }

        boot_service_watchdog();
        boot_delay_stub(100U);
        tick++;
    }
}

/*---------------------------------------------------------------------------
 * Настройка MCAN под загрузчик.
 *
 * Единственное место, где законно звать driverlib: функция выполняется до
 * первого стирания, флеш ещё читается. Дальше по коду - только регистры.
 *
 * Тайминг свой, из board.c не берётся: 1 Мбит/с арбитраж, 2.5 Мбит/с данные.
 * Он согласован с настройкой PCAN на стороне ПК (Bootloader.cs: BootFdBitrate),
 * и трогать его здесь в отрыве от ПК нельзя. Приложение бывает на
 * 5 Мбит/с, прошивка этим путём проверена 04.11.2026.
 *---------------------------------------------------------------------------*/
void boot_can_init(void)
{
    MCAN_InitParams          initParams;
    MCAN_ConfigParams        configParams;
    MCAN_MsgRAMConfigParams  ramParams;
    MCAN_BitTimingParams     bitTimes;
    MCAN_StdMsgIDFilterElement filtElem;

    initParams.fdMode          = true;
    initParams.brsEnable       = true;
    initParams.txpEnable       = true;
    initParams.efbi            = false;
    initParams.pxhddisable     = false;
    initParams.darEnable       = false;
    initParams.wkupReqEnable   = false;
    initParams.autoWkupEnable  = false;
    initParams.emulationEnable = false;
    initParams.tdcEnable       = true;
    initParams.wdcPreload      = 255;
    initParams.tdcConfig.tdcf  = 10;
    initParams.tdcConfig.tdco  = 33;

    configParams.monEnable         = false;
    configParams.asmEnable         = false;
    configParams.tsPrescalar       = 15;
    configParams.tsSelect          = 0;
    configParams.timeoutSelect     = MCAN_TIMEOUT_SELECT_RX_FIFO0;
    configParams.timeoutPreload    = 65535;
    configParams.timeoutCntEnable  = false;
    configParams.filterConfig.rrfs = true;   /* удалённые кадры отбрасывать */
    configParams.filterConfig.rrfe = true;
    configParams.filterConfig.anfe = 2;      /* чужой ID - отбросить, не в FIFO */
    configParams.filterConfig.anfs = 2;

    /* Разделы Message RAM. Всё, что не нужно, обнулено явно: остатки настройки
     * приложения в этих регистрах никуда сами не денутся. */
    ramParams.flssa                = BOOT_RAM_FILTER_ADDR;
    ramParams.lss                  = 1U;
    ramParams.flesa                = 0U;
    ramParams.lse                  = 0U;

    ramParams.txStartAddr          = BOOT_RAM_TXBUF_ADDR;
    ramParams.txBufNum             = 1U;
    ramParams.txFIFOSize           = 0U;
    ramParams.txBufMode            = 0U;
    ramParams.txBufElemSize        = MCAN_ELEM_SIZE_8BYTES;
    ramParams.txEventFIFOStartAddr = 0U;
    ramParams.txEventFIFOSize      = 0U;
    ramParams.txEventFIFOWaterMark = 0U;

    ramParams.rxFIFO0startAddr     = BOOT_RAM_RXF0_ADDR;
    ramParams.rxFIFO0size          = BOOT_RAM_RXF0_NUM;
    ramParams.rxFIFO0waterMark     = 0U;
    ramParams.rxFIFO0OpMode        = 0U;     /* блокирующий: терять кадры нельзя */
    ramParams.rxFIFO0ElemSize      = MCAN_ELEM_SIZE_64BYTES;

    ramParams.rxFIFO1startAddr     = 0U;
    ramParams.rxFIFO1size          = 0U;
    ramParams.rxFIFO1waterMark     = 0U;
    ramParams.rxFIFO1OpMode        = 0U;
    ramParams.rxFIFO1ElemSize      = MCAN_ELEM_SIZE_8BYTES;

    ramParams.rxBufStartAddr       = 0U;
    ramParams.rxBufElemSize        = MCAN_ELEM_SIZE_8BYTES;

    bitTimes.nomRatePrescalar   = 5;
    bitTimes.nomTimeSeg1        = 17;
    bitTimes.nomTimeSeg2        = 5;
    bitTimes.nomSynchJumpWidth  = 4;
    bitTimes.dataRatePrescalar  = 1;
    bitTimes.dataTimeSeg1       = 21;
    bitTimes.dataTimeSeg2       = 6;
    bitTimes.dataSynchJumpWidth = 5;

    MCAN_setOpMode(myMCANA_BASE, MCAN_OPERATION_MODE_SW_INIT);
    while (MCAN_OPERATION_MODE_SW_INIT != MCAN_getOpMode(myMCANA_BASE))
    {
    }

    MCAN_init(myMCANA_BASE, &initParams);
    MCAN_config(myMCANA_BASE, &configParams);
    MCAN_setBitTime(myMCANA_BASE, &bitTimes);
    MCAN_msgRAMConfig(myMCANA_BASE, &ramParams);

    /* Диапазон из одного идентификатора - точное совпадение с BOOT_CAN_RX_ID. */
    filtElem.sft   = MCAN_STDFILT_RANGE;
    filtElem.sfec  = MCAN_STDFILTEC_FIFO0;
    filtElem.sfid1 = BOOT_CAN_RX_ID;
    filtElem.sfid2 = BOOT_CAN_RX_ID;
    MCAN_addStdMsgIDFilter(myMCANA_BASE, 0U, &filtElem);

    /* Прерывания не нужны: весь обмен опросом, а ISR приложения всё равно во
     * флеше, который сейчас будет стёрт. */
    MCAN_enableIntr(myMCANA_BASE, MCAN_INTR_MASK_ALL, 0U);
    MCAN_enableIntrLine(myMCANA_BASE, MCAN_INTR_LINE_NUM_0, 0U);
    MCAN_enableIntrLine(myMCANA_BASE, MCAN_INTR_LINE_NUM_1, 0U);

    MCAN_setOpMode(myMCANA_BASE, MCAN_OPERATION_MODE_NORMAL);
    while (MCAN_OPERATION_MODE_NORMAL != MCAN_getOpMode(myMCANA_BASE))
    {
    }

    boot_rx_ready = 0U;
    boot_rxBytes  = 0U;
}

/*---------------------------------------------------------------------------
 * Ожидание конца операции флеша. Таймаут грубый и служит только страховкой от
 * вечного зависания: точное время зависит от тактовой.
 *---------------------------------------------------------------------------*/
static bool boot_wait_fsm_ready(void)
{
    uint32_t spin = 0U;

    while (Fapi_checkFsmForReady() == Fapi_Status_FsmBusy)
    {
        boot_service_watchdog();

        spin++;
        if (spin >= 5000000U)
        {
            return false;
        }
    }

    return true;
}

/*---------------------------------------------------------------------------
 * Сброс статуса предыдущей операции. Повторяет ClearFSMStatus() из
 * my_Files/flash_eeprom/F28P55x_EEPROM.c - та во флеше и звать её отсюда
 * нельзя.
 *---------------------------------------------------------------------------*/
static void boot_clear_fsm_status(void)
{
    while (Fapi_checkFsmForReady() != Fapi_Status_FsmReady)
    {
        boot_service_watchdog();
    }

    if (Fapi_getFsmStatus() != 0U)
    {
        (void)Fapi_issueAsyncCommand(Fapi_ClearStatus);

        while (Fapi_getFsmStatus() != 0U)
        {
            boot_service_watchdog();
        }
    }
}

/*---------------------------------------------------------------------------
 * Снятие защиты от стирания и записи. На F28P55x её надо снимать ПЕРЕД КАЖДОЙ
 * командой: CMDWEPROTA отвечает за секторы 0..31, младшие 12 бит CMDWEPROTB -
 * за группы по восемь секторов с 32-го по 127-й. Ноль означает "разрешено".
 *
 * BANK4 этим не открывается: регистры действуют на банк, выбранный адресом
 * команды, а адресов BANK4 мы не выдаём.
 *---------------------------------------------------------------------------*/
static void boot_flash_unprotect(void)
{
    (void)Fapi_setupBankSectorEnable(FLASH_WRAPPER_PROGRAM_BASE + FLASH_O_CMDWEPROTA, 0x00000000UL);
    (void)Fapi_setupBankSectorEnable(FLASH_WRAPPER_PROGRAM_BASE + FLASH_O_CMDWEPROTB, 0x00000000UL);
}

/*---------------------------------------------------------------------------
 * Стирание области приложения по секторам 0x400 слов.
 *
 * Стираем ровно запрошенный ПК диапазон, выровненный на границы секторов.
 * Посекторно, а не банками: диапазон почти всегда занимает часть первого
 * банка, и стирать из-за него все четыре незачем.
 *---------------------------------------------------------------------------*/
Fapi_StatusType bootloader_erase_app_flash(void)
{
    Fapi_StatusType status;
    uint32_t first;
    uint32_t last;
    uint32_t sector;
    uint32_t total;
    uint32_t done;
    uint16_t lastPercent = 0xFFFFU;

    boot_can_send_word(MARK_ERASE_ENTER);
    boot_delay_stub(1000U);

    /* Прежде чем стирать банки с кодом, гасим упреждающую выборку и кэш:
     * они умеют читать вперёд по флешу сами, без участия программы, и такое
     * чтение из стираемого банка - ошибка. */
    Flash_disablePrefetch(FLASH0CTRL_BASE);
    Flash_disableCache(FLASH0CTRL_BASE);

    status = Fapi_initializeAPI(FlashTech_CPU0_BASE_ADDRESS, 150U);
    if (status != Fapi_Status_Success)
    {
        boot_can_send_word((uint16_t)(MARK_FAPI_ERROR | ((uint16_t)status & 0x00FFU)));
        return status;
    }

    /* На F28P55x перечисление Fapi_FlashBankType состоит из одного элемента:
     * банк выбирается адресом команды, а не этим вызовом. Поэтому он здесь
     * один раз - в отличие от версии для F280039C, где банк переключали перед
     * каждой операцией. */
    status = Fapi_setActiveFlashBank(Fapi_FlashBank0);
    if (status != Fapi_Status_Success)
    {
        boot_can_send_word((uint16_t)(MARK_FAPI_ERROR | ((uint16_t)status & 0x00FFU)));
        return status;
    }

    boot_can_send_word(MARK_FAPI_OK);

    first = boot_erase_start_addr & ~(FLASH_SECTOR_WORDS - 1UL);
    last  = boot_erase_end_addr;
    total = ((last - first) / FLASH_SECTOR_WORDS) + 1UL;
    done  = 0U;

    for (sector = first; sector <= last; sector += FLASH_SECTOR_WORDS)
    {
        uint16_t percent = (uint16_t)((done * 100UL) / total);

        /* Кадр прогресса только при смене процента - иначе на каждый сектор
         * уходил бы кадр, и шина была бы забита ими, а не делом. */
        if (percent != lastPercent)
        {
            boot_can_send_word((uint16_t)(MARK_ERASE_PROGRESS | percent));
            lastPercent = percent;
        }

#ifdef BOOT_WAIT_FOR_CONTINUE
        /* Отладочное: ждать разрешения от ПК перед каждым сектором, чтобы
         * успеть посмотреть состояние отладчиком. */
        while (!boot_can_wait_word(CMD_CONTINUE, 0xFFFFFFFFU))
        {
            boot_service_watchdog();
        }
#endif

        boot_clear_fsm_status();
        boot_flash_unprotect();

        status = Fapi_issueAsyncCommandWithAddress(Fapi_EraseSector, (uint32_t *)sector);
        if (status != Fapi_Status_Success)
        {
            return status;
        }

        if (!boot_wait_fsm_ready())
        {
            boot_send_error_word((uint16_t)(MARK_ERASE_TIMEOUT | (uint16_t)(done & 0x00FFUL)));
            return Fapi_Status_FsmBusy;
        }

        /* Успех операции - STATCMD == 3 (команда выполнена и прошла). Так же
         * проверяют оба образца TI: EEPROM_CheckStatus() в
         * my_Files/flash_eeprom/F28P55x_EEPROM.c и flashapi_512bit_programming.c
         * в примерах C2000Ware. Ноль здесь означал бы, что команда вообще не
         * отработала, а не что всё хорошо. */
        if (Fapi_getFsmStatus() != 3U)
        {
            return Fapi_Error_Fail;
        }

        done++;
    }

    boot_can_send_word((uint16_t)(MARK_ERASE_PROGRESS | 100U));

    return Fapi_Status_Success;
}

/*---------------------------------------------------------------------------
 * Запись 32 слов (512 бит) с автоматическим расчётом ECC.
 *
 * Ровно один кадр CAN-FD. Адрес обязан быть выровнен на 32 слова - за это
 * отвечает ПК, здесь только проверка.
 *---------------------------------------------------------------------------*/
Fapi_StatusType bootloader_program_block(uint32_t addr, uint16_t *data32)
{
    Fapi_StatusType status;

    /* Последний рубеж перед флешем. Заголовок сегмента уже проверен выше, но
     * эта функция - единственное место во всём загрузчике, которое пишет, и
     * пускать её без собственной проверки адреса нельзя: цена ошибки здесь -
     * затёртые калибровки в BANK4. */
    if ((addr < APP_START_ADDR) ||
        ((addr + FLASH_PROG_WORDS - 1UL) > APP_END_ADDR) ||
        ((addr & (FLASH_PROG_WORDS - 1UL)) != 0U))
    {
        return Fapi_Error_Fail;
    }

    boot_clear_fsm_status();
    boot_flash_unprotect();

    status = Fapi_issueAutoEcc512ProgrammingCommand((uint32_t *)addr,
                                                    data32,
                                                    (uint16_t)FLASH_PROG_WORDS);
    if (status != Fapi_Status_Success)
    {
        return status;
    }

    if (!boot_wait_fsm_ready())
    {
        return Fapi_Status_FsmBusy;
    }

    if (Fapi_getFsmStatus() != 3U)
    {
        return Fapi_Error_Fail;
    }

    return Fapi_Status_Success;
}

/*---------------------------------------------------------------------------
 * Главный цикл загрузчика.
 *---------------------------------------------------------------------------*/
void bootloader_main(uint16_t viaSpi)
{
    uint16_t rxWord;
    uint32_t imgAddr  = 0U;
    uint32_t imgSize  = 0U;
    uint32_t checksum = 0U;
    Fapi_StatusType fStatus;

    /* Глушим всё, что могло бы вмешаться. ISR приложения лежат во флеше,
     * который сейчас будет стёрт: одно сработавшее прерывание - и процессор
     * уйдёт выполнять стёртую память. */
    Cla1Regs.MIER.all = 0U;
    AdcaRegs.ADCINTSEL1N2.all = 0U;

    /* Снятие такта ШИМ. Регистром, а не SysCtl_disablePeripheral(): при -Ooff
     * та функция выносится компилятором в .text, то есть во флеш, и тянет за
     * собой строку с (.text) у can_bootloader.obj - ровно то, чего мы здесь
     * добиваемся не иметь вовсе. Сама она отработала бы: до стирания ещё
     * далеко. Но проверка "во флеше от загрузчика не осталось ничего" ценна
     * своей простотой, и одного разрешённого исключения достаточно, чтобы она
     * перестала работать. */
    EALLOW;
    CpuSysRegs.PCLKCR0.bit.TBCLKSYNC = 0U;
    EDIS;

    DINT;
    IER = 0U;

    /* MCAN по SPI не трогаем вовсе: настройка приложения остаётся, кадры
     * на шине копятся в его FIFO и никому не мешают. */
    boot_via_spi = viaSpi;
    if (boot_via_spi)
    {
        boot_spi_init();
    }
    else
    {
        boot_can_init();
    }

    boot_can_send_word(MARK_BOOT_ENTER);
    boot_delay_stub(500U);
    boot_can_send_word(MARK_CAN_READY);
    boot_delay_stub(200U);

    /*--- Фаза 1: связь ---*/
    for (;;)
    {
        rxWord = boot_can_recv_word();

        if (rxWord == CMD_HANDSHAKE)
        {
            boot_can_send_word(ACK_HANDSHAKE);
            break;
        }
        if (rxWord == CMD_ABORT)
        {
            boot_abort_to_app();
        }

        boot_service_watchdog();
        boot_delay_stub(1000U);
    }

    /*--- Фаза 2: стирание ---*/
    /* Ждём стирания, а до него ещё можно отказаться (CMD_ABORT). Поэтому не
     * boot_can_wait_word(): та пропускает всё, кроме одного слова. */
    {
        uint32_t i;
        bool     got = false;

        for (i = 0U; (i < 500000UL) && !got; i++)
        {
            rxWord = boot_can_recv_word();
            if (rxWord == CMD_ERASE)
            {
                got = true;
            }
            else if (rxWord == CMD_ABORT)
            {
                boot_abort_to_app();
            }
            else
            {
                boot_service_watchdog();
                boot_delay_stub(100U);
            }
        }

        if (!got)
        {
            boot_send_error_word(MARK_FAIL);
            boot_wait_recovery_cmd_erase();
        }
    }

    /* ДВЕ ТОЧКИ ВОЗВРАТА, И ЭТО НЕ ЛИШНЕЕ.
     *
     * Сюда, на erase_range, возвращаются все пути после отказа: команду
     * CMD_ERASE там уже сняла с шины boot_wait_recovery_cmd_erase(), и ждать
     * её второй раз нельзя - ПК шлёт ровно одну на попытку. Прежняя версия
     * прыгала на общее начало фазы, снова уходила в ожидание CMD_ERASE,
     * не дожидалась, слала 0xDEAD - и всё повторялось по кругу, выглядя со
     * стороны ПК как неисправный прибор.
     *
     * Следом за CMD_ERASE обязан прийти кадр диапазона:
     * [startHi, startLo, endHi, endLo], адреса слов, включительно. */
erase_range:
    {
        uint32_t wait = 0U;

        while (!boot_rx_ready && (wait < 20000U))
        {
            boot_poll_rx();
            boot_service_watchdog();
            boot_delay_stub(10U);
            wait++;
        }

        if (!boot_rx_ready)
        {
            boot_send_error_word(MARK_FAIL);
            boot_wait_recovery_cmd_erase();
            goto erase_range;
        }

        {
            uint16_t rangeBuf[BOOT_CMD_WORDS];
            uint32_t reqStart;
            uint32_t reqEnd;

            (void)boot_can_recv_frame(rangeBuf, BOOT_CMD_WORDS);

            reqStart = ((uint32_t)rangeBuf[0] << 16U) | rangeBuf[1];
            reqEnd   = ((uint32_t)rangeBuf[2] << 16U) | rangeBuf[3];

            /* Зажим по области приложения. Это и есть защита BANK4: выше
             * APP_END_ADDR диапазон не уедет, какой бы кадр ни пришёл. */
            if (reqStart < APP_START_ADDR)
            {
                reqStart = APP_START_ADDR;
            }
            if (reqEnd > APP_END_ADDR)
            {
                reqEnd = APP_END_ADDR;
            }

            if (reqStart > reqEnd)
            {
                boot_send_error_word(MARK_FAIL);
                boot_wait_recovery_cmd_erase();
                goto erase_range;
            }

            boot_erase_start_addr = reqStart;
            boot_erase_end_addr   = reqEnd;

            boot_can_send_word(ACK_ERASE_RANGE);
        }
    }

    boot_can_send_word(MARK_ERASE_BEGIN);

    fStatus = bootloader_erase_app_flash();
    if (fStatus != Fapi_Status_Success)
    {
        boot_send_error_word(MARK_FAIL);
        boot_wait_recovery_cmd_erase();
        goto erase_range;
    }

    boot_can_send_word(ACK_ERASE);

    checksum = 0U;

    /*--- Фаза 3: приём сегментов ---*/
    /* Первый CMD_LOAD после ACK_ERASE. Квитанцию повторяем: ПК мог не увидеть
     * её, пока перебирал очередь маркеров стирания. */
    {
        uint32_t loadWait = 0U;

        for (;;)
        {
            if ((loadWait & 0x000003FFUL) == 0U)
            {
                boot_can_send_word(ACK_ERASE);
            }

            if (boot_can_recv_word() == CMD_LOAD)
            {
                boot_can_send_word(ACK_LOAD_READY);
                break;
            }

            boot_service_watchdog();
            boot_delay_stub(100U);

            loadWait++;
            if (loadWait >= 3000000UL)
            {
                boot_send_error_word(MARK_FAIL);
                boot_wait_recovery_cmd_erase();
                goto erase_range;
            }
        }
    }

segment_load_header:
    {
        uint16_t hdrBuf[BOOT_CMD_WORDS];

        if (boot_can_recv_frame(hdrBuf, BOOT_CMD_WORDS) < (2U * BOOT_CMD_WORDS))
        {
            boot_send_error_word(MARK_FAIL);
            boot_wait_recovery_cmd_erase();
            goto erase_range;
        }

        imgAddr = ((uint32_t)hdrBuf[0] << 16U) | hdrBuf[1];
        imgSize = ((uint32_t)hdrBuf[2] << 16U) | hdrBuf[3];
    }

    /* Сегмент обязан целиком лежать в области приложения, начинаться на границе
     * записи в 32 слова, а длиной быть кратен кадру. Это проверка не от помех
     * на шине (их ловит CRC самого CAN), а от рассогласования версий ПК и MCU:
     * молча записать такой сегмент хуже, чем отказаться.
     *
     * Проверка ХВОСТА здесь не формальность. Начало сегмента может честно
     * лежать в BANK3, а длина - вывести запись за 0x0FFFFF, то есть в BANK4, где
     * калибровки. Диапазон стирания зажат выше по коду, а вот адрес записи
     * приходит отдельным заголовком, и зажать его больше негде. */
    {
        uint32_t imgWords = imgSize / 2U;
        uint32_t imgLast  = imgAddr + imgWords - 1U;

        if ((imgAddr < APP_START_ADDR) || (imgAddr > APP_END_ADDR) ||
            ((imgAddr & (FLASH_PROG_WORDS - 1UL)) != 0U) ||
            (imgSize == 0U) || ((imgSize % BOOT_DATA_BYTES) != 0U) ||
            (imgLast > APP_END_ADDR) || (imgLast < imgAddr))
        {
            boot_send_error_word(MARK_FAIL);
            boot_wait_recovery_cmd_erase();
            goto erase_range;
        }
    }

    {
        uint32_t flashAddr = imgAddr;
        uint32_t byteCount = 0U;
        uint16_t wordBuf[BOOT_DATA_WORDS];

        /* Приглашения на первый кадр НЕ шлём: ПК отправляет его сразу за
         * заголовком. Квитанций ровно столько, сколько кадров, и ПК ждёт по
         * одной после каждого - без этого счёт разъезжается на единицу, и
         * лишняя квитанция приезжает уже в фазу ожидания ACK_LOAD.
         *
         * Гонки тут нет: RX FIFO0 держит восемь элементов и работает в
         * блокирующем режиме, так что кадр, пришедший раньше, чем мы дочитали
         * заголовок, дождётся своей очереди, а не потеряется. */
        while (byteCount < imgSize)
        {
            uint16_t bytes;
            uint16_t j;

            bytes = boot_can_recv_frame(wordBuf, BOOT_DATA_WORDS);

            if (bytes != BOOT_DATA_BYTES)
            {
                boot_send_error_word(MARK_FAIL);
                boot_wait_recovery_cmd_erase();
                goto erase_range;
            }

            /* Сумма считается по байтам ровно так же, как на ПК. */
            for (j = 0U; j < BOOT_DATA_WORDS; j++)
            {
                checksum += (uint32_t)(wordBuf[j] & 0x00FFU);
                checksum += (uint32_t)(wordBuf[j] >> 8U);
            }

            fStatus = bootloader_program_block(flashAddr, wordBuf);
            if (fStatus != Fapi_Status_Success)
            {
                boot_send_error_word((uint16_t)(MARK_PROG_ERROR | ((uint16_t)fStatus & 0x00FFU)));
                boot_wait_recovery_cmd_erase();
                goto erase_range;
            }

            boot_can_send_word(ACK_BLOCK);

            flashAddr += BOOT_DATA_WORDS;
            byteCount += BOOT_DATA_BYTES;
        }
    }

    boot_can_send_word(ACK_LOAD);

    /* После сегмента ждём либо следующий CMD_LOAD, либо CMD_VERIFY. */
    {
        uint32_t nextWait = 0U;

        for (;;)
        {
            uint16_t w;

            if ((nextWait & 0x000000FFUL) == 0U)
            {
                boot_can_send_word(ACK_LOAD);
            }

            w = boot_can_recv_word();

            if (w == CMD_LOAD)
            {
                boot_can_send_word(ACK_LOAD_READY);
                goto segment_load_header;
            }
            if (w == CMD_VERIFY)
            {
                boot_can_send_word(ACK_VERIFY_READY);
                goto verify_phase;
            }

            boot_service_watchdog();
            boot_delay_stub(100U);

            nextWait++;
            if (nextWait >= 500000UL)
            {
                boot_send_error_word(MARK_FAIL);
                boot_wait_recovery_cmd_erase();
                goto erase_range;
            }
        }
    }

verify_phase:
    {
        uint16_t chkHi = boot_can_recv_word_blocking();
        uint16_t chkLo = boot_can_recv_word_blocking();

        uint32_t pcChecksum = ((uint32_t)chkHi << 16U) | chkLo;

        if (pcChecksum != checksum)
        {
            /* Сумма не сошлась. Сообщаем ПК и ждём только CMD_ERASE: ни
             * сброса, ни перехода - во флеше недописанный образ. */
            boot_can_send_word(ACK_NOVERIFY);

            for (;;)
            {
                if (boot_can_recv_word() == CMD_ERASE)
                {
                    goto erase_range;
                }

                boot_service_watchdog();
                boot_delay_stub(100U);
            }
        }

        boot_can_send_word(ACK_VERIFY);
    }

    /*--- Фаза 5: пуск приложения ---*/
    if (!boot_can_wait_word(CMD_REBOOT, 500000UL))
    {
        boot_send_error_word(MARK_FAIL);
        boot_wait_recovery_cmd_erase();
        goto erase_range;
    }

    {
        uint16_t entryBuf[BOOT_CMD_WORDS];
        uint32_t entryAddr;

        boot_can_send_word(ACK_REBOOT_READY);

        (void)boot_can_recv_frame(entryBuf, BOOT_CMD_WORDS);
        entryAddr = ((uint32_t)entryBuf[0] << 16U) | entryBuf[1];

        if ((entryAddr < APP_START_ADDR) || (entryAddr > APP_END_ADDR))
        {
            boot_send_error_word(MARK_FAIL);
            boot_wait_recovery_cmd_erase();
            goto erase_range;
        }

        boot_can_send_word(ACK_REBOOT);
        boot_delay_stub(10000U);

        /* Эхо адреса входа - последнее, что ПК видит перед тишиной на шине.
         * По нему в логе отличают "ушли не туда" от "не ушли вовсе". */
        boot_can_send_frame((uint16_t)(entryAddr >> 16U),
                            (uint16_t)(entryAddr & 0xFFFFU),
                            0U, 0U);
        boot_delay_stub(10000U);

        boot_rx_ready = 0U;

        /* Кэш и упреждающую выборку включаем обратно: приложение стартует с
         * нормальной настройкой флеша, а не с той, что мы оставили под
         * стирание. Обе функции в .TI.ramfunc, звать их отсюда законно. */
        Flash_enableCache(FLASH0CTRL_BASE);
        Flash_enablePrefetch(FLASH0CTRL_BASE);

        /* По SPI кадры уходят только с посылкой: паузы выше их не отдают. */
        if (boot_via_spi)
        {
            boot_spi_flush();
        }

        boot_jump_to_app(entryAddr);
    }
}

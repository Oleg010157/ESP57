/*****************************************************************************
 * can_bootloader.c
 *
 * CAN-загрузчик для F280039C
 * Живёт в .TI.ramfunc
 * Стирает и программирует Bank0 Sec2..15, Bank1, Bank2 через Fapi
 *
 * Сценарий работы:
 *   1. Приложение получает команду 9999 по CAN
 *   2. app_request_firmware_update() отключает IGBT, прерывания
 *      и напрямую вызывает bootloader_main()
 *   3. bootloader_main() переинициализирует CAN (1Mbit/s) и выполняет:
 *      a. Ожидание handshake 0xCC01
 *      b. Стирание flash секторов приложения
 *      c. Приём данных по CAN, программирование flash
 *      d. Проверка контрольной суммы
 *      e. Перезагрузка
 *
 * ВАЖНО: Функции Fapi и функции bootloader.c должны выполняться из RAM!
 *
 * Автор: Олег Павловский
 * 10.01.2026
 *****************************************************************************/

#include "driverlib.h"
#include "device.h"
#include "f28p55x_device.h"
#include "can_boot_protocol.h"
#include "FlashTech.h"

#include "F28P55x.h"

void CAN_IF3_DMA_init(void);
/*---------------------------------------------------------------------------
 * Функции загрузчика выполняются из RAM (flash будет стираться!)
 *---------------------------------------------------------------------------*/
#pragma CODE_SECTION(bootloader_erase_app_flash, ".TI.ramfunc")
#pragma CODE_SECTION(bootloader_program_block, ".TI.ramfunc")
#pragma CODE_SECTION(bootloader_main, ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_init, ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_send_word, ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_send_8bytes, ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_recv_word, ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_recv_8bytes, ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_wait_word, ".TI.ramfunc")
#pragma CODE_SECTION(boot_can_recv_word_blocking, ".TI.ramfunc")

/* Avoid calling driverlib SysCtl_serviceWatchdog() from flash while erasing/programming.
 * Provide a tiny watchdog service routine that resides in RAM. */
#pragma CODE_SECTION(boot_service_watchdog, ".TI.ramfunc")
/* Helper for long flash operations: wait for FSM ready with a simple timeout.
 * Stays in RAM to be safe while flash banks are busy. */
#pragma CODE_SECTION(boot_wait_fsm_ready, ".TI.ramfunc")
#pragma CODE_SECTION(boot_send_error_word, ".TI.ramfunc")
#pragma CODE_SECTION(boot_wait_recovery_cmd_erase, ".TI.ramfunc")
#pragma CODE_SECTION(boot_delay_stub, ".TI.ramfunc")

#pragma CODE_SECTION(boot_jump_to_app, ".TI.ramfunc")
#pragma CODE_SECTION(boot_poll_dma6, ".TI.ramfunc")

static void boot_service_watchdog(void)
{
    /* WDKEY is a protected register on C2000.
     * Keep this routine minimal and always available from RAM while flash banks are busy. */
    EALLOW;
    WdRegs.WDKEY.all = 0x0055U;
    WdRegs.WDKEY.all = 0x00AAU;
    EDIS;
}

static void boot_jump_to_app(uint32_t entryAddr)
{
    /* Jump to application's _c_int00 (address received from PC).
     * _c_int00 resets SP to __stack, processes .cinit, copies .TI.ramfunc
     * from flash to RAM, and calls main().
     *
     * C28x: stack grows UP (PUSH → SP++, POP → SP--).
     * entryAddr is in ACC (AH:AL) per C28x calling convention.
     * DINT (SETC INTM) does not modify ACC.
     *
     * PUSH AL/AH кладёт адрес на стек в формате LCR (low, then high).
     * LRET снимает его и делает PC = entryAddr.
     * Итого: стек не изменился (push 2, pop 2). */
    DINT;
    __asm(" PUSH AL");      /* [SP] = entryAddr(15:0),  SP++ */
    __asm(" PUSH AH");      /* [SP] = entryAddr(31:16), SP++ */
    __asm(" LRET");         /* SP--; hi=[SP]; SP--; lo=[SP]; PC={hi,lo} */
}

static void boot_delay_stub(uint32_t us)
{
    /* Simple RAM-based delay to avoid executing from Flash (DEVICE_DELAY_US) */
    uint32_t i;
    /* Approx 30 loops/us for 120MHz (conservative) */
    uint32_t count = us * 30UL;
    for(i = 0; i < count; i++)
    {
         __asm(" NOP");
    }
}

static bool boot_wait_fsm_ready(uint16_t timeoutWord)
{
    (void)timeoutWord;
    uint32_t spin = 0U;

    while (Fapi_checkFsmForReady() == Fapi_Status_FsmBusy)
    {
        boot_service_watchdog();

        /* If FSM never becomes ready, don't hang forever.
         * Exact timing depends on CPU clock; this is a coarse safety net. */
        spin++;
        if (spin >= 5000000U)
        {
            return false;
        }
    }

    return true;
}

static void boot_send_error_word(uint16_t errorWord)
{
    /* Repeat diagnostic frame a few times so PC/UI surely sees it. */
    uint32_t i;
    for (i = 0U; i < 16U; i++)
    {
        boot_can_send_word(errorWord);
        boot_delay_stub(1000);
    }
}

static void boot_wait_recovery_cmd_erase(void)
{
    uint32_t tick = 0U;

    /* Stay in RAM and wait for explicit restart from PC. */
    for (;;)
    {
        uint16_t w = boot_can_recv_word();
        if (w == CMD_ERASE)
        {
            return;
        }

        /* Keep recovery state visible on the bus. */
        if ((tick & 0x000003FFUL) == 0U)
        {
            boot_can_send_word(0xDEADU);
        }

        boot_service_watchdog();
        boot_delay_stub(100);
        tick++;
    }
}

/*---------------------------------------------------------------------------
 * Флаг приёма — устанавливается из boot_poll_dma6()
 *---------------------------------------------------------------------------*/
 volatile uint16_t boot_rx_ready = 0;

/* rxBuf заполняется через IF3+DMA (myDCAN.c) — загрузчик его НЕ читает напрямую.
 * Аппаратный DMA продолжает писать в rxBuf, но с IER=0 DMA ISR не запускается.
 * Загрузчик читает данные через IF2 в собственный boot_rxBuf. */
extern volatile uint16_t rxBuf[4];

/* Приватный буфер загрузчика: заполняется ТОЛЬКО через boot_poll_dma6() via IF2.
 * Изолирован от DMA — гонки за данные исключены. */
static volatile uint16_t boot_rxBuf[4];

/* Erase range requested by PC (word addresses, inclusive).
 * Defaults to full application range for backward compatibility. */
static uint32_t boot_erase_start_addr = APP_START_ADDR;
static uint32_t boot_erase_end_addr   = APP_END_ADDR;

/* Debug artifacts removed: debug_* variables were used only for development
 * inspection and are cleared to reduce code size and data in RAM. */



/*---------------------------------------------------------------------------
 * CAN: поллинг Mailbox 1 через IF2.
 * IF3 отвязан от объекта 1 (IF3UPD=0 в boot_can_init) — NEWDAT не сбрасывается
 * аппаратно и остаётся доступным для чтения через IF2.
 *---------------------------------------------------------------------------*/
static void boot_poll_dma6(void)
{
    /* Проверяем NEWDAT бит Mailbox 1 в регистре NDAT_21.
     * IF3 отвязан → бит остаётся до явного чтения через IF2. */
    if (!(HWREG_BP(CANA_BASE + CAN_O_NDAT_21) & 0x00000001UL))
        return;  /* нет новых данных */

    /* Ждём свободный IF2 */
    while (HWREG_BP(CANA_BASE + CAN_O_IF2CMD) & CAN_IF1CMD_BUSY) {}

    /* DIR=0 (чтение из RAM сообщений), DATA_A+DATA_B+TXRQST (сбрасывает NEWDAT), объект 1 */
    HWREG_BP(CANA_BASE + CAN_O_IF2CMD) =
        (uint32_t)CAN_IF1CMD_DATA_B | (uint32_t)CAN_IF1CMD_DATA_A |
        (uint32_t)CAN_IF1CMD_TXRQST |
        (1UL & CAN_IF1CMD_MSG_NUM_M);
    while (HWREG_BP(CANA_BASE + CAN_O_IF2CMD) & CAN_IF1CMD_BUSY) {}

    uint32_t da = HWREG_BP(CANA_BASE + CAN_O_IF2DATA);
    uint32_t db = HWREG_BP(CANA_BASE + CAN_O_IF2DATB);
    boot_rxBuf[0] = (uint16_t)(da         & 0xFFFFU);
    boot_rxBuf[1] = (uint16_t)(da >> 16U);
    boot_rxBuf[2] = (uint16_t)(db         & 0xFFFFU);
    boot_rxBuf[3] = (uint16_t)(db >> 16U);

    /* boot_rxBuf copied */

    boot_rx_ready = 1;
}

/*---------------------------------------------------------------------------
 * CAN: отправить 16-битное слово через IF1 на Mailbox 2 (ID=BOOT_CAN_TX_ID)
 * Аналог SendAck из рабочего кода — без driverlib
 *---------------------------------------------------------------------------*/
 void boot_can_send_word(uint16_t word)
{
    /* Wait until IF1 is free before updating mailbox 2. With ACK_BLOCK sent for
     * every programmed chunk, back-to-back writes can otherwise collide. */
    while (HWREG_BP(CANA_BASE + CAN_O_IF1CMD) & CAN_IF1CMD_BUSY) {}

    /* Wait until previous mailbox 2 transmission request is no longer pending. */
    while (HWREG_BP(CANA_BASE + CAN_O_TXRQ_21) & 0x00000002UL) {}

    /* Данные: word в младших 16 бит IF1DATA, остальное = 0 */
    CanaRegs.CAN_IF1DATA.all = (uint32_t)word;
    CanaRegs.CAN_IF1DATB.all = 0UL;

    /* IF1CMD: запись DATA_A + DATA_B + TXRQST → Mailbox 2 */
    HWREG_BP(CANA_BASE + CAN_O_IF1CMD) =
        CAN_IF1CMD_DATA_B | CAN_IF1CMD_DATA_A |
        (uint32_t)CAN_IF1CMD_DIR | (uint32_t)CAN_IF1CMD_TXRQST |
        (2UL & CAN_IF1CMD_MSG_NUM_M);
}

/*---------------------------------------------------------------------------
 * CAN: отправить 8 байт через IF1 на Mailbox 2 (ID=BOOT_CAN_TX_ID)
 *---------------------------------------------------------------------------*/
void boot_can_send_8bytes(const uint16_t *buf4)
{
    while (HWREG_BP(CANA_BASE + CAN_O_IF1CMD) & CAN_IF1CMD_BUSY) {}
    while (HWREG_BP(CANA_BASE + CAN_O_TXRQ_21) & 0x00000002UL) {}

    /* IFxDATA/IFxDATB are 32-bit containers for 8 payload bytes.
     * Pack full words explicitly to avoid stale odd bytes from previous TX frames. */
    CanaRegs.CAN_IF1DATA.all = ((uint32_t)buf4[1] << 16) | (uint32_t)buf4[0];
    CanaRegs.CAN_IF1DATB.all = ((uint32_t)buf4[3] << 16) | (uint32_t)buf4[2];

    HWREG_BP(CANA_BASE + CAN_O_IF1CMD) =
        CAN_IF1CMD_DATA_B | CAN_IF1CMD_DATA_A |
        (uint32_t)CAN_IF1CMD_DIR | (uint32_t)CAN_IF1CMD_TXRQST |
        (2UL & CAN_IF1CMD_MSG_NUM_M);
}

/*---------------------------------------------------------------------------
 * CAN: принять 16-битное слово через IF2
 * Возвращает 0 если нет данных, иначе принятое слово
 *---------------------------------------------------------------------------*/
 uint16_t boot_can_recv_word(void)
{
    boot_poll_dma6();
    if (!boot_rx_ready)
        return 0;

    uint16_t val = boot_rxBuf[0];
    boot_rx_ready = 0;
    return val;
}

/*---------------------------------------------------------------------------
 * CAN: принять 16-битное слово (блокирующий вызов)
 *---------------------------------------------------------------------------*/
 uint16_t boot_can_recv_word_blocking(void)
{
    while (!boot_rx_ready)
    {
        boot_poll_dma6();
        boot_service_watchdog();
    }

    uint16_t val = boot_rxBuf[0];
    boot_rx_ready = 0;
    return val;
}

/*---------------------------------------------------------------------------
 * CAN: принять 8 байт
 * Блокирующий вызов — ждёт прерывание DMA6
 *---------------------------------------------------------------------------*/
 void boot_can_recv_8bytes(uint16_t *buf4)
{
    while (!boot_rx_ready)
    {
        boot_poll_dma6();
        boot_service_watchdog();
    }

    buf4[0] = boot_rxBuf[0];
    buf4[1] = boot_rxBuf[1];
    buf4[2] = boot_rxBuf[2];
    buf4[3] = boot_rxBuf[3];
    boot_rx_ready = 0;
}

/*---------------------------------------------------------------------------
 * CAN: ждать конкретное слово с таймаутом
 * Возвращает true при успехе
 *---------------------------------------------------------------------------*/
 bool boot_can_wait_word(uint16_t expected, uint32_t timeout_loops)
{
    uint32_t i;
    for (i = 0; i < timeout_loops; i++)
    {
        uint16_t w = boot_can_recv_word();
        if (w == expected)
            return true;

        boot_service_watchdog();
        boot_delay_stub(100);
    }
    return false;
}

/*---------------------------------------------------------------------------
 * Инициализация DCAN для загрузчика
 * CAN модуль уже работает на 1 Мбит/с (Board_init)
 * IF3+DMA уже настроены (CAN_IF3_DMA_init) — пишут в rxBuf[]
 * Нужно только переключить Mailbox 1 на ID загрузчика
 *---------------------------------------------------------------------------*/

 void boot_can_init(void)
{
    /* Полная переинициализация CAN для загрузчика.
     * Повторный вход в bootloader после предыдущей прошивки не должен зависеть
     * от состояния CANA/IF3/DMA, оставленного приложением или прошлым циклом. */

    CAN_initModule(CANA_BASE);
    CAN_setBitRate(CANA_BASE, DEVICE_SYSCLK_FREQ, 1000000U, 20U);
    CAN_disableAllMessageObjects(CANA_BASE);

    /* Переключаем Mailbox 1: RX ID=2 → ID=0x65 (загрузчик) */
    CAN_setupMessageObject(CANA_BASE, 1U, BOOT_CAN_RX_ID,
                           CAN_MSG_FRAME_STD,
                           CAN_MSG_OBJ_TYPE_RX,
                           0U, CAN_MSG_OBJ_NO_FLAGS, 8U);

    /* Mailbox 2: TX, ID = 0x64 */
    CAN_setupMessageObject(CANA_BASE, 2U, BOOT_CAN_TX_ID,
                           CAN_MSG_FRAME_STD,
                           CAN_MSG_OBJ_TYPE_TX,
                           0U, CAN_MSG_OBJ_NO_FLAGS, 8U);

    CAN_startModule(CANA_BASE);

    boot_rx_ready = 0;
}

/*---------------------------------------------------------------------------
 * Стирание flash секторов приложения (Bank0 Sec2..15, Bank1, Bank2)
 * ВЫПОЛНЯЕТСЯ ИЗ RAM!
 *---------------------------------------------------------------------------*/
 Fapi_StatusType bootloader_erase_app_flash(void)
{
    Fapi_StatusType status;
    uint32_t sectorAddr;
    uint16_t i;
   
   

    /* Notify host: entered erase routine */
    boot_can_send_word(0xEE00U);
    boot_delay_stub(1000);
    /* QUICK TEST: optionally fake Fapi to verify UI/transport without erasing flash */


    /* Инициализация Flash API */
  
    status = Fapi_initializeAPI(F021_CPU0_BASE_ADDRESS, 120U);
   

    /* Notify host: Fapi init result */
    if (status == Fapi_Status_Success)
        boot_can_send_word(0xEE02U);
    else
        boot_can_send_word((uint16_t)(0xDE00U | ((uint16_t)status & 0x00FFU)));
    if (status != Fapi_Status_Success)
        return status;

    status = Fapi_setActiveFlashBank(Fapi_FlashBank0);
    if (status != Fapi_Status_Success)
        return status;

    status = Fapi_setupBankSectorEnable();
    if (status != Fapi_Status_Success)
        return status;

    /* Bank0 Sec2..15 */

    // Protect bootloader and ramfuncs regions
    #define BOOT_START_ADDR 0x080002UL
    #define BOOT_END_ADDR   0x081FFFUL
    #define RAMFUNC_START   0x00009800UL
    #define RAMFUNC_END     0x0000A800UL

    for (i = 2; i < 16; i++)
    {
        sectorAddr = 0x080000UL + (uint32_t)i * 0x1000UL;
        {
            uint32_t secStart = sectorAddr;
            uint32_t secEnd = sectorAddr + 0x0FFFUL;
            if (secEnd < boot_erase_start_addr || secStart > boot_erase_end_addr)
                continue;
        }

        // Skip protected regions
        if ((sectorAddr >= BOOT_START_ADDR && sectorAddr <= BOOT_END_ADDR) ||
            (sectorAddr >= RAMFUNC_START && sectorAddr < RAMFUNC_END))
        {
            boot_can_send_word(0xDEAD); // Diagnostic: protected sector
            continue;
        }

        /* Notify host: bank0 + sector combined marker (0xEB0s) */
        boot_can_send_word((uint16_t)(0xEB00U | (i & 0x000FU)));
        boot_delay_stub(1000);

        /* Developer option: wait for CMD_CONTINUE from PC before erasing this sector.
         * Define BOOT_WAIT_FOR_CONTINUE to enable. This helps to attach debugger
         * and verify state right before FAPI call. */
#ifdef BOOT_WAIT_FOR_CONTINUE
        /* Wait indefinitely (large timeout) for continue command */
        while (!boot_can_wait_word(CMD_CONTINUE, 0xFFFFFFFFU))
        {
            boot_service_watchdog();
        }
#endif

      
        status = Fapi_issueAsyncCommandWithAddress(Fapi_EraseSector, (uint32_t *)sectorAddr);

        /* Small pause to let FAPI start and to make diagnostic frames visible in trace */
        boot_delay_stub(5000);
        if (status != Fapi_Status_Success)
            return status;
        if (!boot_wait_fsm_ready((uint16_t)(0xD000U | (i & 0x00FFU))))
        {
            boot_send_error_word((uint16_t)(0xD000U | (i & 0x00FFU)));
            return Fapi_Status_FsmBusy;
        }

       
        status = (Fapi_StatusType)Fapi_getFsmStatus();

        if (status != Fapi_Status_Success)
        {
           
            return status;
        }
    }
      

    /* Bank1 Sec0..15 */

    status = Fapi_setActiveFlashBank(Fapi_FlashBank1);
    if (status != Fapi_Status_Success)
        return status;

    status = Fapi_setupBankSectorEnable();
    if (status != Fapi_Status_Success)
        return status;

    /* Pump stabilization delay after bank switch */
    boot_delay_stub(5000);

    for (i = 0; i < 16; i++)
    {
        sectorAddr = 0x090000UL + (uint32_t)i * 0x1000UL;
        {
            uint32_t secStart = sectorAddr;
            uint32_t secEnd = sectorAddr + 0x0FFFUL;
            if (secEnd < boot_erase_start_addr || secStart > boot_erase_end_addr)
                continue;
        }
        /* Notify host: bank1 + sector combined marker (0xEB1s) */
        boot_can_send_word((uint16_t)(0xEB10U | (i & 0x000FU)));
        boot_delay_stub(1000);

#ifdef BOOT_WAIT_FOR_CONTINUE
        while (!boot_can_wait_word(CMD_CONTINUE, 0xFFFFFFFFU))
        {
            boot_service_watchdog();
        }
#endif

        status = Fapi_issueAsyncCommandWithAddress(
            Fapi_EraseSector, (uint32_t *)sectorAddr);

        /* Pause to let FSM transition to Busy before polling.
         * Without this delay Fapi_checkFsmForReady() may catch
         * the FSM in the gap before it reports Busy, falsely
         * treating the erase as complete. */
        boot_delay_stub(5000);

        if (status != Fapi_Status_Success)
            return status;

        if (!boot_wait_fsm_ready((uint16_t)(0xD100U | (i & 0x00FFU))))
        {
            boot_send_error_word((uint16_t)(0xD100U | (i & 0x00FFU)));
            return Fapi_Status_FsmBusy;
        }

        status = (Fapi_StatusType)Fapi_getFsmStatus();

        if (status != Fapi_Status_Success)
            return status;
    }

    

    /* Bank2 Sec0..15 */
    status = Fapi_setActiveFlashBank(Fapi_FlashBank2);
    if (status != Fapi_Status_Success)
        return status;

    status = Fapi_setupBankSectorEnable();
    if (status != Fapi_Status_Success)
        return status;

    /* Pump stabilization delay after bank switch */
    boot_delay_stub(5000);

    for (i = 0; i < 16; i++)
    {
        sectorAddr = 0x0A0000UL + (uint32_t)i * 0x1000UL;
        {
            uint32_t secStart = sectorAddr;
            uint32_t secEnd = sectorAddr + 0x0FFFUL;
            if (secEnd < boot_erase_start_addr || secStart > boot_erase_end_addr)
                continue;
        }

        /* Notify host: bank2 + sector combined marker (0xEB2s) */
        boot_can_send_word((uint16_t)(0xEB20U | (i & 0x000FU)));
        boot_delay_stub(1000);

#ifdef BOOT_WAIT_FOR_CONTINUE
        while (!boot_can_wait_word(CMD_CONTINUE, 0xFFFFFFFFU))
        {
            boot_service_watchdog();
        }
#endif

        status = Fapi_issueAsyncCommandWithAddress(
            Fapi_EraseSector, (uint32_t *)sectorAddr);

        /* Pause to let FSM transition to Busy before polling */
        boot_delay_stub(5000);

        if (status != Fapi_Status_Success)
            return status;

        if (!boot_wait_fsm_ready((uint16_t)(0xD200U | (i & 0x00FFU))))
        {
            boot_send_error_word((uint16_t)(0xD200U | (i & 0x00FFU)));
            return Fapi_Status_FsmBusy;
        }

        status = (Fapi_StatusType)Fapi_getFsmStatus();
        if (status != Fapi_Status_Success)
            return status;
    }

    return Fapi_Status_Success;
}

/*---------------------------------------------------------------------------
 * Программирование блока из 4 слов (64 бита) во flash
 * ВЫПОЛНЯЕТСЯ ИЗ RAM!
 *---------------------------------------------------------------------------*/
 /* Кэш активного банка — избегаем повторных Fapi_setActiveFlashBank при
  * последовательной записи в один банк (экономит ~50мкс на каждый блок) */
 static uint16_t prog_active_bank = 0xFFFFU;

  Fapi_StatusType bootloader_program_block(uint32_t addr, uint16_t *data4)
 {
     Fapi_StatusType status;

     /* programming block entry */

     /* F280039C errata "Memory: Prefetching Beyond Valid Memory":
      * the last 16 words of Bank2 (0x0AFFF0-0x0AFFFF) must not be programmed.
      * Skip silently — the PC includes those bytes in the checksum transfer so
      * both MCU and PC checksums still account for them. */
     if (addr >= 0x0AFFF0UL)
         return Fapi_Status_Success;

     /* Определяем банк по адресу */
     Fapi_FlashBankType bank;
     if (addr < 0x090000UL)
         bank = Fapi_FlashBank0;
     else if (addr < 0x0A0000UL)
         bank = Fapi_FlashBank1;
     else
         bank = Fapi_FlashBank2;

     if ((uint16_t)bank != prog_active_bank)
     {
         status = Fapi_setActiveFlashBank(bank);
         if (status != Fapi_Status_Success)
             return status;
         boot_delay_stub(5000); /* Стабилизация charge pump после смены банка */
         prog_active_bank = (uint16_t)bank;
     }

     status = Fapi_issueProgrammingCommand((uint32_t *)addr, data4, 4U, /* 4 слова = 64 бита */
                                            0U, 0U, Fapi_AutoEccGeneration);
     if (status != Fapi_Status_Success)
         return status;

     while (Fapi_checkFsmForReady() == Fapi_Status_FsmBusy)
     {
         boot_service_watchdog();
     }

     return (Fapi_StatusType)Fapi_getFsmStatus();
 }

/*---------------------------------------------------------------------------
 * Главный цикл загрузчика — ВЫПОЛНЯЕТСЯ ИЗ RAM!
 *---------------------------------------------------------------------------*/
void bootloader_main(void)
{
    uint16_t rxWord;
    uint32_t imgAddr = 0;
    uint32_t imgSize = 0;
    uint32_t checksum = 0;
    uint32_t byteCount = 0;
    Fapi_StatusType fStatus;

 //   extern uint16 FapiLoadStart;
 //   extern uint16 FapiLoadSize;
 //   extern uint16 FapiRunStart;
 //  memcpy(&FapiRunStart, &FapiLoadStart, (size_t)&FapiLoadSize);
    Cla1Regs.MIER.all = 0;
    AdcaRegs.ADCINTSEL1N2.all =0;
    SysCtl_disablePeripheral(SYSCTL_PERIPH_CLK_TBCLKSYNC);
    /* Disable all application ISRs before polling-based bootloader.
     * Without IER = 0 the application CAN ISR can fire and consume
     * incoming frames (clears NEWDAT) before boot_poll_dma6() sees them. */
    DINT;
    IER = 0;


    boot_can_init();
    /* entered bootloader_main */
    /* Инициализация CAN */
    /* Diagnostic: entered bootloader_main */
    boot_can_send_word(0xEF01U);  //ID =32h
    boot_delay_stub(500);


    /* Diagnostic: boot_can_init done */
    boot_can_send_word(0xEF02U);
    boot_delay_stub(200);

    /*--- Фаза 1: Handshake ---*/
    for (;;)
    {
        rxWord = boot_can_recv_word();
                
            if (rxWord == CMD_HANDSHAKE)
        {
            boot_can_send_word(ACK_HANDSHAKE);
            break;
        }
        boot_service_watchdog();
        boot_delay_stub(1000);
    }

wait_erase_cmd:
    /*--- Фаза 2: Стирание flash ---*/
    if (!boot_can_wait_word(CMD_ERASE, 500000UL))
    {
        boot_send_error_word(0xDEADU);
        boot_wait_recovery_cmd_erase();
        goto wait_erase_cmd;
    }

    /* Optional erase range frame right after CMD_ERASE:
     * [startHi, startLo, endHi, endLo], inclusive word addresses.
     * If absent, keep defaults and erase full application area. */
    {
        /* Keep this window short so legacy PC (without range frame)
         * does not stall before actual erase starts. */
        uint32_t wait = 0U;
        while (!boot_rx_ready && wait < 2000U)
        {
            boot_poll_dma6();
            boot_service_watchdog();
            boot_delay_stub(10);
            wait++;
        }

        if (boot_rx_ready)
        {
            uint16_t rangeBuf[4];
            boot_can_recv_8bytes(rangeBuf);

            uint32_t reqStart = ((uint32_t)rangeBuf[0] << 16) | rangeBuf[1];
            uint32_t reqEnd = ((uint32_t)rangeBuf[2] << 16) | rangeBuf[3];

            if (reqStart < APP_START_ADDR) reqStart = APP_START_ADDR;
            if (reqEnd > APP_END_ADDR) reqEnd = APP_END_ADDR;

            if (reqStart <= reqEnd)
            {
                boot_erase_start_addr = reqStart;
                boot_erase_end_addr = reqEnd;

                /* Explicit ACK only for valid range frame. */
                boot_can_send_word(ACK_ERASE_RANGE);
            }
            else
            {
                boot_send_error_word(0xDEADU);
                boot_wait_recovery_cmd_erase();
                goto wait_erase_cmd;
            }
        }
        else
        {
            boot_send_error_word(0xDEADU);
            boot_wait_recovery_cmd_erase();
            goto wait_erase_cmd;
        }
    }

    /* Inform PC that erase is starting (debug/status) */
    boot_can_send_word(0xEE01U);

    fStatus = bootloader_erase_app_flash();
    if (fStatus != Fapi_Status_Success)
    {
        boot_send_error_word(0xDEADU);
        boot_wait_recovery_cmd_erase();
        goto wait_erase_cmd;
    }
    /* Erase finished successfully */
    boot_can_send_word(ACK_ERASE);

    checksum = 0U;

    /*--- Фаза 3: Приём сегментов загрузки (CMD_LOAD + header + data) ---*/
    /* Ждём первый CMD_LOAD после ACK_ERASE */
    {
        uint32_t loadWait = 0U;
        for (;;)
        {
            uint16_t w;

            if ((loadWait & 0x000003FFUL) == 0U)
            {
                boot_can_send_word(ACK_ERASE);
            }

            w = boot_can_recv_word();
            if (w == CMD_LOAD)
            {
                boot_can_send_word(ACK_LOAD_READY);
                break;
            }

            boot_service_watchdog();
            boot_delay_stub(100);

            loadWait++;
            if (loadWait >= 3000000UL)
            {
                boot_send_error_word(0xDEADU);
                boot_wait_recovery_cmd_erase();
                goto wait_erase_cmd;
            }
        }
    }

segment_load_header:
    {
        uint16_t hdrBuf[4];
        uint16_t addrHi;
        uint16_t addrLo;
        uint16_t sizeHi;
        uint16_t sizeLo;

        boot_can_recv_8bytes(hdrBuf);
        addrHi = hdrBuf[0];
        addrLo = hdrBuf[1];
        sizeHi = hdrBuf[2];
        sizeLo = hdrBuf[3];

        imgAddr = ((uint32_t)addrHi << 16) | addrLo;
        imgSize = ((uint32_t)sizeHi << 16) | sizeLo;
    }

    {
        Fapi_FlashBankType startBank;
        if (imgAddr < 0x090000UL)
            startBank = Fapi_FlashBank0;
        else if (imgAddr < 0x0A0000UL)
            startBank = Fapi_FlashBank1;
        else
            startBank = Fapi_FlashBank2;

        Fapi_setActiveFlashBank(startBank);
        boot_delay_stub(5000);
        prog_active_bank = (uint16_t)startBank;

        {
            uint32_t firstBlockWait = 0U;

            for (;;)
            {
                if ((firstBlockWait & 0x000000FFUL) == 0U)
                {
                    boot_can_send_word(ACK_BLOCK);
                }

                boot_poll_dma6();
                if (boot_rx_ready)
                    break;

                boot_service_watchdog();
                boot_delay_stub(100);

                firstBlockWait++;
                if (firstBlockWait >= 500000UL)
                {
                    boot_send_error_word(0xDEADU);
                    boot_wait_recovery_cmd_erase();
                    goto wait_erase_cmd;
                }
            }
        }
    }

    {
        uint32_t flashAddr = imgAddr;
        uint16_t wordBuf[4];

        byteCount = 0;

        while (byteCount < imgSize)
        {
            uint16_t j;
            uint16_t remaining;

            boot_can_recv_8bytes(wordBuf);

            remaining = (imgSize - byteCount >= 8) ? 8 : (uint16_t)(imgSize - byteCount);
            for (j = 0; j < remaining; j++)
            {
                uint8_t b = (j & 1)
                    ? (uint8_t)(wordBuf[j >> 1] >> 8)
                    : (uint8_t)(wordBuf[j >> 1] & 0xFF);
                checksum += b;
            }

            fStatus = bootloader_program_block(flashAddr, wordBuf);
            if (fStatus != Fapi_Status_Success)
            {
                boot_send_error_word((uint16_t)(0xDF00U | ((uint16_t)fStatus & 0x00FFU)));
                boot_wait_recovery_cmd_erase();
                goto wait_erase_cmd;
            }

            boot_can_send_word(ACK_BLOCK);

            flashAddr += 4;
            byteCount += 8;
        }
    }

    boot_can_send_word(ACK_LOAD);

    /* После каждого сегмента ждём либо следующий CMD_LOAD, либо CMD_VERIFY */
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
            boot_delay_stub(100);

            nextWait++;
            if (nextWait >= 500000UL)
            {
                boot_send_error_word(0xDEADU);
                boot_wait_recovery_cmd_erase();
                goto wait_erase_cmd;
            }
        }
    }

verify_phase:

    {
        uint16_t chkHi = boot_can_recv_word_blocking();
        uint16_t chkLo = boot_can_recv_word_blocking();

        uint32_t pcChecksum = ((uint32_t)chkHi << 16) | chkLo;

        if (pcChecksum == checksum)
        {
            boot_can_send_word(ACK_VERIFY);
        }
        else
        {
            /* Контрольная сумма не совпала — сообщаем PC и ждём подтверждения.
             * PC обязан отправить CMD_ERROR_RECEIVED.
             * Это даёт возможность PC запросить повторный пакет или диагностировать состояние. */
            boot_can_send_word(ACK_NOVERIFY);

            /* Ждём указаний от PC: только CMD_ERASE для перезапуска flow с фазы стирания.
             * В recovery-режиме не выполняем reset/jump, чтобы не потерять возможность
             * безопасно повторить загрузку без JTAG. */
            for (;;)
            {
                uint16_t w = boot_can_recv_word();
                if (w == CMD_ERASE)
                {
                    goto wait_erase_cmd;
                }

                /* Ничего полезного — поддерживаем watchdog и продолжаем ждать. */
                boot_service_watchdog();
                boot_delay_stub(100);
            }
        }
    }

    /*--- Фаза 6: Перезагрузка ---*/
    if (!boot_can_wait_word(CMD_REBOOT, 500000UL))
    {
        boot_send_error_word(0xDEADU);
        boot_wait_recovery_cmd_erase();
        goto wait_erase_cmd;
    }

    {
        /* Receive application entry point (_c_int00) from PC */
        boot_can_send_word(ACK_REBOOT_READY);

        uint16_t entryBuf[4];
        boot_can_recv_8bytes(entryBuf);
        uint16_t entryHi = entryBuf[0];
        uint16_t entryLo = entryBuf[1];
        uint32_t entryAddr = ((uint32_t)entryHi << 16) | entryLo;

        /* Fail-fast: never jump outside application flash range. */
        if ((entryAddr < APP_START_ADDR) || (entryAddr > APP_END_ADDR))
        {
            boot_send_error_word(0xDEADU);
            boot_wait_recovery_cmd_erase();
            goto wait_erase_cmd;
        }

        boot_can_send_word(ACK_REBOOT);
        boot_delay_stub(10000);

        /* Diagnostic: echo received entryAddr back to PC (single 8-byte frame). */
        {
            uint16_t echoBuf[4];
            echoBuf[0] = (uint16_t)(entryAddr >> 16); /* entryHi */
            echoBuf[1] = (uint16_t)(entryAddr & 0xFFFF); /* entryLo */
            echoBuf[2] = 0U;
            echoBuf[3] = 0U;
            boot_can_send_8bytes(echoBuf);
        }
        boot_rx_ready = 0;
 //       ESTOP0;
        boot_jump_to_app(entryAddr);
    }
}

/* bootloader_check() удалена — загрузчик вызывается напрямую из
 * app_request_firmware_update() при получении команды 9999.
 * Флаг в flash больше не используется. */

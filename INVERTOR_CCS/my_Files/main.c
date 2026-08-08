// #############################################################################
//
//  FILE:   main.c
//
//  TITLE:   Test Example for 3phase inverter

// #############################################################################
#ifdef __clang__
#define __TMS320C28XX__ 1
#endif
//
// Included Files
//
#include "board.h"

#include "driverlib.h"
#include "device.h"
#include <hw_mcan.h>


// bitfield include
#include "f28x_project.h"
#include "math.h"

#include "KSP.h"

#include "myADC.h"
#include "myDCAN.h"
#include "myMCAN.h"

#include "myEPWM.h"
#include "main.h"
#include "cla_shared.h"
#include "SPI_EEPROM.h"




//
// Globals 
//
motor     motor1;
motor     motor2;
CAN_kadr  canaKadr;
STEND     stend;
STEND_bit sbit;
REFs      REF;
void      REF_init(REFs *v);

uint16_t Imax = 18;

float32_t freq = 50; // freq of sine waveform in Gz

uint16_t  timeBasePeriod;   //
float32_t Tpwm      = 1000; // мкс период pwm модуляции
uint16_t  pole_pair = 2;    // кол-во пар полюсов двигателя

uint16_t  dbgphone, dbgT0;
float32_t Urms = 100;
//extern uint16_t Req_336;

uint16_t cntT0   = 0;  // счётчик времени таймера Т0
uint16_t cnt1sek = 0;  // счётчик секунд
#define OneSecond 20u  // кол-во cntT0 равное 1 секунде
#define TenSecond 200u // кол-во cntT0 равное 10 секундам

void IparkToUrms(motor *m);

void CPU_TimersInit(void);
void MainISR(void);
void RMS(motor *m);


#if MCANA
//void mcan_init(void);

//MCAN_start(myMCANA_BASE); // Запуск самого модуля CAN в работу
#endif

uint16_t          request_firmware_update = 0;
extern void       app_request_firmware_update(void);


extern SHR_Flags shrTrip;
//
// Main
//
//MCAN_GlobalFiltConfig filterConfig;
void main(void)
{
   

    //
    // Initialize device clock and peripherals
    //
    Device_init();
    DEVICE_DELAY_US(100000);
    //
    // Initialize PIE and clear PIE registers. Disables CPU interrupts.
    //
    Interrupt_initModule();

    //
    // Initialize the PIE vector table with pointers to the shell Interrupt
    // Service Routines (ISR).
    //
    Interrupt_initVectorTable();

    //
    // Board initialization
    // Configure DAC - Setup the reference voltage and output value for the DAC
    //

    Board_init();

    IGBT1_Disable();
    IGBT2_Disable();
    
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_TBCLKSYNC);

    Interrupt_enable(INT_EPWM2);

    timeBasePeriod = EPwm2Regs.TBPRD;
    change_Tpwm();

    TRIP_Clear();
    XbarRegs.XBARCLR1.all = 0xffffffff;
    // TRIP4: оставляем только входы от драйверов, отключаем источники от CMPSS
    XBAR_disableEPWMMux(XBAR_TRIP4, XBAR_MUX00 | XBAR_MUX04);
    XBAR_enableEPWMMux(XBAR_TRIP4, XBAR_MUX01 | XBAR_MUX07);

    REF_init(&REF);
    motor_init(&motor1);

    SPI_setTxFifoTransmitDelay(mySPIB_BASE, 1);

    // -------------------  EEPROM  -----------------------------------------------------
#if CONTROL_CARD
    DEVICE_DELAY_US(20000); // подождать завершения auto-RECALL по включению питания.
#endif

    offsets = offsets_default;
    kadc    = kadc_default;

 //   EEprom_read_words(SPIB_BASE, READ0, offsets_addr, &offsets.Ia, (uint16_t)(sizeof(offsets)));
 //   EEprom_read_words(SPIB_BASE, READ0, kadc_addr, &kadc.Ia, (uint16_t)(sizeof(kadc)));
 //   EEprom_read_words(SPIB_BASE, READ0, errors_history_addr, (uint16_t *)&ErrHistory[0], (uint16_t)(sizeof(ErrHistory) / sizeof(uint16_t)));
    ErrHistory_ClearPending();

    {
        uint16_t sessionId = 0U;

  //      EEprom_read_words(SPIB_BASE, READ0, session_id_addr, &sessionId, 1U);

        sessionId++;

        EEprom_write_words(SPIB_BASE, WRITE0, session_id_addr, &sessionId, 1U);

        CurrentSessionId = sessionId;
    }

    checkEE();
    // -----------------------------------------------------------------------------------------------

    // команда запуска REF кадра по CANA
    CpuTimer0Regs.PRD.all     = mSecond(50); // период таймера Т0 в миллисекундах
    CpuTimer0Regs.TCR.bit.TRB = 1;           // устанавливаем счётчик на начало периода
    CpuTimer0Regs.TCR.bit.TSS = 0;           // запускаем таймер Т0

    CLA_shared_Init();

#if MCANA
//    mcan_init();
MCAN_initAllTxBuffers();
MCAN_enableLine1Routing();
#endif

    request_firmware_update = 0;
    EINT;
    ERTM;

    extern volatile uint16_t epwm3count;// dbg
extern volatile uint16_t epwm3time;// dbg

    while (1)
    {
        CMPSS_BackgroundCheck();


        // DBG измеритель времени прохождения фонового цикла
        // для измерения используется таймер ШИМ
        dbgphone = (EPwm7Regs.TBCTR - dbgT0); // время в 0.1 мкс
        dbgT0    = EPwm7Regs.TBCTR;

        // 3. Включение CAN загрузчика программы:
        if (request_firmware_update)
        {
 //           app_request_firmware_update();
            request_firmware_update = 0;
        }

     
 
        //===========================================================
        // send_REF_kadr() use Timer0 and Send_kadr60() use Timer1;

        
        // ловим переход из нуля в единицу и наоборот

        if (!sbit.bit.connect) // ПУСК/СТОП только для автономной работы ( без CAN )
        {

            if (!shr.bit.Inp1 && iprold) // 1-> 0 это команда ПУСК
            {
                // включаем IGBT инвертора- аналог команды ПУСК
                PUSK();
            }
            else if (shr.bit.Inp1 && !iprold) // 0->1 это команда СТОП
            {
                // выключаем IGBT инвертора - аналог команды СТОП
                STOP();
            }
            iprold = shr.bit.Inp1;
        }
        // Timer 0  -----------------------------------------------

        if (CpuTimer0Regs.TCR.bit.TIF == 1) // таймер0 отработал заданный период
        {
            CpuTimer0Regs.TCR.bit.TIF = 1; // clear flag
            cntT0++;
            epwm3time  = epwm3count;
            epwm3count = 0;
            epwmtime = epwmcount;
            epwmcount = 0;
          
           

            if (cntT0 == OneSecond) // события обрабатываемые каждую секунду
            {
                 Errors_CurrentErrorWindowTick();

           

                TRIP_Clear();

                if (CheckLamp > 0U)
                {
                    CheckLamp--;
                }

                cntT0 = 0;
                cnt1sek++;
            }

            if (sbit.bit.connect) // проверяем что CAN is active
            {
                // если завершена передача всех Mbox ref_kadra
#if MCANA
                // MCAN: читаем TX Buffer Request Pending регистр
                uint32_t txbrp = HWREG(MCANA_DRIVER_BASE + MCAN_TXBRP);
                if ((txbrp & ref_kadr_mask) == 0) // буферы свободны
                {
                    send_REF_kadr();
                }
#else
                // DCAN: классический CAN TX Request Register
                //       if (!canaKadr.bit.can_stopTX)
                {
                    if ((CanaRegs.CAN_TXRQ_21 & ref_kadr_mask) == 0)
                    {
                        send_REF_kadr();
                    }
                }
#endif
            }
        }

        // Timer 1  ------------------------------------------------------
        if (canaKadr.bit.k60_run) // запрос на отправку данных активен
        {
            if (CpuTimer1Regs.TCR.bit.TIF == 1) // таймер1 отработал заданный период
            {
                CpuTimer1Regs.TCR.bit.TIF = 1; // clear flag
#if MCANA
                // MCAN: читаем TX Buffer Request Pending регистр
                uint32_t txbrp = HWREG(MCANA_DRIVER_BASE + MCAN_TXBRP);
                if ((txbrp & kmask60) == 0) // буферы свободны
                {
                    Send_kadr60();
                }
#else
                // DCAN: классический CAN TX Request Register
                if (!canaKadr.bit.can_stopTX)
                {
                    if ((CanaRegs.CAN_TXRQ_21 & kmask60) == 0)
                    {
                        Send_kadr60();
                    }
                }
#endif
            }
        }

#include "SPI_EEPROM_PHONE.h"

        //=======================================================================

        change_Tpwm();
        c2p.Um_max = (Udc_volt > 0.01f) ? (Udc_volt / sqrt3) : 0.01f;

        //=======================================================================
        // CAND вывод по 32-му MBOX
        // то есть не по таймеру не в REF и не в Kadr60

#if MCANA
        {
            uint32_t txbrp = HWREG(MCANA_DRIVER_BASE + MCAN_TXBRP);
            if ((txbrp & BIT31) == 0)
#else
        if ((CanaRegs.CAN_TXRQ_21 & BIT31) == 0) // проверяем свободен ли 32-oй бох msgID = 131
#endif
            {
                uint16_t b = CheckLamp + CheckLamp0;
                if ((Req_336 & 1) == 1)
                {
                    Send_mbox(4, 0, 0, b, 32012);
                    Req_336 = Req_336 & ~1U; // сброс признака наличия ошибок

                    goto end_336;
                }

                if ((Req_336 & 2) == 2)
                {
                    SendErrs(); // Отправка списка ошибок по одной ошибке за один проход фона
                                //   Send_mbox(32, 250, 333, 444, 555);
                    goto end_336;
                }

                if ((Req_336 & 4) == 4)
                {
                    SendTemp();              // Отправка температур
                    Req_336 = Req_336 & ~4U; // сброс флага вывода температур
                    goto end_336;
                }
                if ((Req_336 & 8) == 8)
  //                  ADC_SendOffsets();   // Отправка таблицы АЦП
                Req_336 = Req_336 & ~8U; // сброс флага вывода лампочки

            end_336:;
            }
#if MCANA
        } // закрытие блока MCAN
#endif

    } // while
}
// Main

/****************************************************************************************************
 *  Функция ReadError просто считывает ошибки с мультиплексора * и заполняет
 *соответствующий массив либо 0 (ошибок нет), либо 1 (ошибка) *
 ****************************************************************************************************/
void ReadError(void) {}

void STOP(void)
{
    Reset_stop(&motor1);
    Reset_stop(&motor2);
    Reset_stend_filtr();
    motor1.error.all = 0;
    motor2.error.all = 0;
    IGBT1_Disable();
    IGBT2_Disable();

    sbit.bit.pusk = 0;
}

void PUSK(void)
{
    PUSK_Init(&motor1);
    PUSK_Init(&motor2);
    //    pi_Reset(&motor1);
    ccs.bit.Udc_IGBToff = 0;
    PWM1_On();
    PWM2_On();
    sbit.bit.pusk = 1;

    TRIP_Clear();
}

void Reset_stop(motor *v) // сброс по кнопке СТОП
{
    v->ref.gain  = 0;
    v->ref.Urms  = 0;
    v->ref.Id    = 0;
    v->ref.Iq    = 0;
    v->ref.Im    = 0;
    v->clark.UmF = 0;
}

//---------------------------------------------------------------------------
void PUSK_Init(motor *v) // сброс по кнопке ПУСК
{
    // установка положения вектора напряжения
    v->rg.Angle = 0;
    v->rg.Out   = 0;
    v->Sine     = 0;
    v->Cosine   = 1;

    v->error.all = 0;

    // сброс аккумуляторов суммы

    cs.Pain_sum = 0;

    // установка начала рекурентных фильтров измерения токов
    //  Ia_rf = 0;
    //  Ib_rf = 0;
    //Ic_rf = 0;

    cla.Pabc_rf = 0;

    // Кларк фильтры
    v->clark.UmF = 0;
    v->clark.ImF = 0;

    // Парк фильтры
    v->park.IqF   = 0;
    v->park.IdF   = 0;
    v->park.fiF   = 0;
    v->park.fuF   = 0;
    v->park.phaza = 0;

#if dbgIfull
    v->imit.ImF = 0;
#endif

    v->slip.absF  = 0;
    v->slip.procF = 0;
}

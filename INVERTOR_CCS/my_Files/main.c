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


//
// Битовые поля подключаются напрямую через f28p55x_device.h, а не через
// f28x_project.h. Разница в том, что f28x_project.h тянет за собой
// f28p55x_examples.h, а тот - f28p55x_globalprototypes.h, где символы
// линкера RamfuncsLoadStart..RamfuncsRunSize объявлены как Uint16.
// Сгенерированный syscfg/device.h объявляет их же как uint16_t, и на C28x
// это разные типы: Uint16 - это unsigned short, а uint16_t - unsigned int
// (C2000Ware 26.01, f28p55x_device.h:191 против stdint.h). Под -D_FLASH оба
// заголовка попадают в этот файл, и компилятор останавливается на error #148.
//
// f28p55x_device.h сам по себе globalprototypes не включает, поэтому
// конфликта нет. Так же подключены битовые поля во всех файлах K56.
//
#include "f28p55x_device.h"
#include "math.h"

#include "KSP.h"

#include "myADC.h"
#include "myDCAN.h"
#include "myMCAN.h"

#include "myEPWM.h"
#include "1ph_Sin.h"
#include "grid_tie.h"
#include "plant_sim.h"
#include "main.h"
#include "cla_shared.h"
#include "SPI_EEPROM.h"
#include "calib_store.h"




//
// Globals 
//
motor     motor1;
motor     motor2;
motor    *motors[2] = { &motor1, &motor2 };
CAN_kadr  canaKadr;
STEND     stend;
STEND_bit sbit;
union CCS ccs;
REFs      REF;
void      REF_init(REFs *v);

uint16_t Imax = 18;

// dbg Результат чтения калибровок из флеша при старте, см. CalibStatus:
// 0 = прочитано, 1 = флеш пуст, 2 = запись повреждена или чужого формата.
uint16_t calibLoadStatus = CALIB_EMPTY;

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
    motors[0] = &motor1;
    motors[1] = &motor2;

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

    // Такт управления живёт на ADCB INT1 (EOC1), а не на прерывании EPWM1: контур
    // должен читать отсчёты ТЕКУЩЕГО периода, а не прошлого - см. шапку
    // INT_myADCB_1_ISR в 1ph_Sin.c. Прежний источник гасить из кода не нужно,
    // галка "Enable Interrupt" у myEPWM1 снята в SysConfig, вектор не регистрируется.

#if DBG_SIN_DAC
    dacb_CMPSS1_init(); // dbg  вывод синусоиды на пин A1, B7, D11, CMP1_DACL
    change_sine(&sineB);
    EPwm1Regs.TBPRD = sineB.pwm_period;
    change_sine(&sineA);
    EPwm6Regs.TBPRD = sineA.pwm_period;
#endif

    IGBT1_Disable();
    IGBT2_Disable();
    
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_TBCLKSYNC);

    Interrupt_enable(INT_EPWM2);

    timeBasePeriod = EPwm2Regs.TBPRD;
    change_Tpwm();

    TRIP_Clear();
    XbarRegs.XBARCLR1.all = 0xffffffff;
    // TRIP4: оставляем только входы от драйверов, отключаем источники от CMPSS.
    // MUX00 = CMPSS1_CTRIPH, MUX04 = CMPSS3_CTRIPH - это вывод dacb и детектор
    // нуля U220, ни тому ни другому на аварийной линии делать нечего.
    XBAR_disableEPWMMux(XBAR_TRIP4, XBAR_MUX00 | XBAR_MUX04);
    XBAR_enableEPWMMux(XBAR_TRIP4, XBAR_MUX01 | XBAR_MUX07);

    // TRIP5: токовая защита от CMPSS2 (Ia) и CMPSS4 (Ib), обе полярности.
    TRIP_Init();

    REF_init(&REF);
    motor_init(motors[0]);

    SPI_setTxFifoTransmitDelay(mySPIB_BASE, 1);

    // -------------------  EEPROM  -----------------------------------------------------
#if CONTROL_CARD
    DEVICE_DELAY_US(20000); // подождать завершения auto-RECALL по включению питания.
#endif

    offsets = offsets_default;
    kadc    = kadc_default;

#if CALIB_IN_FLASH
    // Калибровки из флеша поверх заводских. Порядок важен: если во флеше
    // пусто или запись повреждена, calib_load() НЕ трогает offsets и kadc, и
    // остаётся то, что положено строками выше.
    //
    // Вызов безопасен на любом этапе: Flash API не инициализирован и не нужен,
    // читается обычная память банка. Ничего не стирается и не пишется.
    //
    // Результат оставлен в глобальной переменной, чтобы его было видно в Watch:
    //   0 = прочитано из флеша, 1 = флеш пуст, 2 = запись есть, но не наша.
    calibLoadStatus = calib_load();
#endif

 //   EEprom_read_words(SPIB_BASE, READ0, offsets_addr, &offsets.iPri, (uint16_t)(sizeof(offsets)));
 //   EEprom_read_words(SPIB_BASE, READ0, kadc_addr, &kadc.iPri, (uint16_t)(sizeof(kadc)));
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

    // Рабочие уставки токовой защиты. Ставятся именно здесь: checkEE() в конце
    // вызывает floatKadc(), а без готовых масштабов kadcf currentLimit() выйдет
    // сразу и пороги останутся раскрытыми настежь (см. TRIP_Init).
    currentLimit(Imax);
    // -----------------------------------------------------------------------------------------------

    // команда запуска REF кадра по CANA
    CpuTimer0Regs.PRD.all     = mSecond(50); // период таймера Т0 в миллисекундах
    CpuTimer0Regs.TCR.bit.TRB = 1;           // устанавливаем счётчик на начало периода
    CpuTimer0Regs.TCR.bit.TSS = 0;           // запускаем таймер Т0

    CLA_shared_Init();
    grid_tie_init();
    plant_sim_init(); // модель силовой части: my_Files/plant_sim.txt

#if MCANA
//    mcan_init();
MCAN_initAllTxBuffers();
MCAN_enableLine1Routing();
#endif

    request_firmware_update = 0;

#if CALIB_IN_FLASH
    // Заявки, накопившиеся за инициализацию, сбрасываем: SaveAdcOffsets() при
    // старте не вызывается, но если когда-нибудь начнёт - её заявка не должна
    // превратиться в запись во флеш ещё до первой команды оператора.
    calibSaveReq = 0;
#endif

    EINT;
    ERTM;

    extern volatile uint16_t epwm3count;// dbg
extern volatile uint16_t epwm3time;// dbg

    while (1)
    {
        // Вернуть CAN на шину, если он с неё ушёл. Нужно не только при Bus_Off:
        // ЛЮБАЯ остановка отладчиком уводит модуль в CCCR.INIT, и после
        // продолжения он там остаётся - связь пропадает до перезагрузки.
        // Измерено на плате, разбор в myMCAN.c.
        MCAN_keepOnBus();

        CMPSS_BackgroundCheck();

        // Сетевой режим: закрытие IGBT и запись аварии в журнал. Из прерывания
        // это звать нельзя - IGBT*_Disable() делает EINT внутри себя (вложенное
        // прерывание посреди ISR), а RegisterSessionEvent() лезет в историю
        // ошибок. Само отключение выхода уже произошло в ISR: там m = 0, то есть
        // мост даёт нулевое напряжение начиная со следующего периода ШИМ.
        grid_tie_background();

        // dbg Сохранение калибровок во флеш по команде из отладчика
        // (calibCmd = 1). Пока команды нет - ничего не делает.
        calib_store_background();


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

            Core_Temp_Update(); // температура ядра, раз в 50 мс, без прерывания
 
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
                if ((txbrp & MCAN_TXBRP_TRP2) == 0) // буфер 2 свободен ?
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
        // Здесь считался c2p.Um_max = Udc_volt / sqrt3 - амплитуда фазного
        // напряжения трёхфазного инвертора. Величину не читал никто, а sqrt3 в
        // однофазном мосте смысла не имеет. Убрано вместе со структурой c2p.

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
    Reset_stop(motors[0]);
    Reset_stop(motors[1]);
    Reset_stend_filtr();
    motors[0]->error.all = 0;
    motors[1]->error.all = 0;
    IGBT1_Disable();
    IGBT2_Disable();

    sbit.bit.pusk = 0;
}

void PUSK(void)
{
    PUSK_Init(motors[0]);
    PUSK_Init(motors[1]);
    //    pi_Reset(motors[0]);
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

void PUSK_Init(motor *v) // сброс по кнопке ПУСК
{
    // установка положения вектора напряжения
    v->rg.Angle = 0;
    v->rg.Out   = 0;
    v->Sine     = 0;
    v->Cosine   = 1;

    v->error.all = 0;

    // cs.Pain_sum / cla.Pabc_rf убраны вместе с Task7: накопители мощности
    // считала она, а её функционал давно переписан на C (xfmr_currents_rms).

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

void motor_init(motor *v)
{
    v->park.phaza = 0;
    v->park.VdF   = 0;
    v->park.VqF   = 0;

    v->svgen.Ta = 0.5f;
    v->svgen.Tb = 0.5f;
    v->svgen.Tc = 0.5f;

    v->ipark.Vdf    = 0;
    v->ipark.Vqf    = 0;
    v->ipark.Vd_lim = 0;
    v->ipark.Vq_lim = 0;

    v->qep.n3 = 0;

    v->ref.piRPM_max_Gen = 100;

    v->rg.dopusk = 60.0f / 360.0f;
    v->rg.epsv   = 0;
    v->rg.epsi   = 0;

    v->mbit.bit.Ltest = 0;

    v->ramp.ref  = 0;
    v->ramp.dout = 0;
    v->freq      = 0;

    PUSK_Init(v);

    v->clark.Ia   = 0;
    v->clark.Ib   = 0;
    v->clark.klow = 0;
    v->delay      = 0;
    v->clark.ku01 = 1;
}


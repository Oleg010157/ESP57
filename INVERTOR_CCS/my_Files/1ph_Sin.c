/*
 * 1ph_Sin.c
 *
 *  Created on: 26.02.2025
 *      Author: Oleg Pavlovsky
 *  модифицирован 03.10.2025  Олег Павловский
 *  модифицирован 27.07.2026  Олег Павловский  под проект К1
 *  модифицирован 07.08.2026  Олег Павловский  под проект К55
 */

/* Данный файл представляет программный код для реализации мостового однофазного инвертора.
*  Мост реализован на двух EPWM модулях - EPWM1 (фаза А)и EPWM2 (фаза B).
*  Запуск EPWM2 синхронный с запуском EPWM1.
*/

#include "board.h"
#include <math.h>
#include "F28P55x_Device.h" // Основные определения регистров и структуры устройства

#include "KSP.h"
#include "hw_mcan.h"
#include "myDCAN.h"
#include "myADC.h" // kadcf - масштабы каналов АЦП для синхронизации по амплитуде

#include "1ph_Sin.h"

//---------------------------------------------------------------------
// Пользовательские настройки (измените значения по необходимости)
//---------------------------------------------------------------------

//
// Globals
//
extern float     Tpwm;
extern STEND_bit sbit;
extern int       rgStrob_enable;

uint16_t sine_metod = 1;
uint16_t cmpaA, cmpaB;

float    duty;                 // Значение скважности ШИМ (в тактах таймера)
uint16_t sineTick = 0;         // счётчик PWM прерываний
uint16_t tim0S, sine_Time;     // dbg переменные для контроля времени выполнения программы
uint16_t sine_freq  = 50;      // Выходная частота синусоидального сигнала (от 40 до 1000 Гц)
float    sine_amp   = 1;       // амплитуда напряжения синусоидального сигнала Uref/Vin ( от 0 до 1)
uint16_t pwm_period = 60000;   //  период ШИМ в мкс
float    sine_angle;           // текущий угол синусоиды в радианнах:  y=sin(x)
float    sine_step = PI2 / 20; //(PI2 * 50.0f /1000.0f);// шаг приращения угла в радианах ( зависит от частоты синуса и частоты ШИМ)
float    test[32];
uint16_t index  = 0;      // номер от 0 до количества точек периодов ШИМ синусоиды
uint16_t spwmHz = 1000;   //заданная частота  ШИМ в Гц , глобальный параметр. Изменяется через команды CAN
float    sine0  = PI / 2; // регулируемое смещение фазы синусоиды
uint16_t cmpaA;
uint16_t cmpaB;
float duty; // Значение скважности ШИМ (в тактах таймера TBCLK)

//DBG
struct sine sineA = {.resolution = 256, .freq = 50, .gain = 1800};
struct sine sineB = {.resolution = 100, .freq = 50, .gain = 1800};
//---------------------------------------------------------------------------------------------------
#if Strob_rg // применяется для отладки метода формирования синусоиды

extern float rgStrob;

float angleOld   = 0;
int   strobIndex = 0; // строб номера  ШИМ периода в последовательности синусоиды

#endif // if Strob_rg

//-------------------------------------------------------------------------------------------------
#pragma CODE_SECTION(sine_invertor1, ".TI.ramfunc");

void sine_invertor1(struct sine *s)
{
    // В этом методе в режиме ШИМ работают только один из двух  мостов ключей.
    // В положительной  полуволне ШИМ только для ключей фазы А.
    // Для фазы В верхний ключ фазы выключен а нижний ключ включён постоянно

    // В отрицательной  полуволне ШИМ только для для ключей фазы В.
    // Для фазы А верхний ключ фазы выключен а нижний ключ включён постоянно
    // ----------------------------------------------------------------------

    // локальные переменные
   

    // вычисление и загрузка нового значения CMPA в PWM секции
    duty           = sinf(s->angle + s->offset);
    float tbperiod = s->gain / 2048 * s->pwm_period;

    if (duty >= 0) // реализация положительной волны синусоиды
    {
        EPwm1Regs.CMPA.bit.CMPA = cmpaA = s->pwm_period - tbperiod * duty; // ШИМ для верхнего ключа фазы А
        EPwm2Regs.CMPA.bit.CMPA = cmpaB = s->pwm_period;                   //верхний ключа фазы B ШИМ всегда в нуле
    }
    else // реализация отрицательной волны синусоиды
    {
        EPwm2Regs.CMPA.bit.CMPA = cmpaB = s->pwm_period + tbperiod * duty; // ШИМ для верхнего ключа фазы B
        EPwm1Regs.CMPA.bit.CMPA = cmpaA = s->pwm_period;                   // верхний ключа фазы A ШИМ всегда в нуле
    }

    // вычисление нового угла синусоиды и ограничение в диапазоне от -PI до PI
    s->angle = asut(s->angle + s->step);
}
//--------------------------------------------------------------------------------------------
#pragma CODE_SECTION(sine_invertor2, ".TI.ramfunc");

void sine_invertor2(struct sine *s)
{
    /* В этом методе в режиме ШИМ работают все четыре ключа.
     * ключи фазы А (EPWM1) модулируются скважностью = duty, 
     * а ключи фазы В (EPWM6) модулируются скважностью = (1-duty)
     * Вычисление duty происходит в прерывании которое запускается при достижении
     * счётчиком значения: TBCNT =  pwm_period, а загрузка нового значения скважности в 
     * компаратор CMPA происходит позже когда TBCNT = 0.
    */

    // вычисление нового значения скважности для ШИМ.
    // Скважность нормируется в диапазоне от 0 до 1 и умножается на коэффициент амплитуды amp,
    // который тоже нормируется от 0 до 1 -> amp = Uab/Vin;  где Uab - выходное напряжение, а
    // Vin - входное напряжение от АКБ

    // нормируем скважность в диапазоне от 0 до 1
    duty           = (0.5f * sinf(s->angle + s->offset) + 0.5f);
    float tbperiod = s->gain / 2048 * s->pwm_period;
    // загрузка скважности в теневые регистры компараторов:
    DINT;
    EPwm1Regs.CMPA.bit.CMPA = cmpaA = tbperiod * (duty);                  // ШИМ для верхнего ключа фазы А
    EPwm2Regs.CMPA.bit.CMPA = cmpaB = tbperiod - EPwm1Regs.CMPA.bit.CMPA; //  *(1.0f-duty); // ШИМ для верхнего ключа фазы B
    EINT;

    // вычисление нового угла синусоиды и ограничение в диапазоне от -PI до PI
    s->angle = asut(s->angle + s->step);
}

//-----------------------------------------------------------------------------------
/* программа для вычисления и изменения частоты ШИМ для формирования синусоиды. 
* pwmfreq - заданная частота  ШИМ , глобальный параметр. Изменяется через команды CAN
* sine_freq - заданная частота  синусоиды в Гц, глобальный параметр. Изменяется через команды CAN
* sine_step - расчитанное приращение угла синусоиды за один период ШИМ.
* pwm_period - расчитанное значение TBPRD. 
*/

void spwmCLKDIV(uint16_t div)
{
    EALLOW;
    SysCtl_disablePeripheral(SYSCTL_PERIPH_CLK_TBCLKSYNC);
    EPwm1Regs.TBCTL.bit.CLKDIV = div;
    EPwm2Regs.TBCTL.bit.CLKDIV = div;

    //   EPwm1Regs.DBFED.all =  (Deadband >> div);
    //   EPwm1Regs.DBRED.all = (Deadband >> div);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_TBCLKSYNC);
    EDIS;
}
//--------------------------------------------------------------------------------------
uint16_t daca  = 2048; // dbg
uint16_t dacb  = 2048; // dbg

#if DBG_SIN_DAC
//--------------------------------------------------------------------------------------
// dbg  Настройка ЦАП CMPSS1 (COMPDACL) на вывод в пин A1, B7, D11, CMP1_DACL.
// На F28P55x нет DACB, но нижний ЦАП CMPSS1 можно перевести в режим
// ЦАП общего назначения (11 бит, 0..2047) с выводом на внешний пин.
// ВНИМАНИЕ: при этом весь модуль CMPSS1 (оба компаратора, верхний ЦАП,
// генератор рампы и цифровые фильтры) становится недоступен - защита по току
// фазы А через CMPSS1 не работает.
//--------------------------------------------------------------------------------------
void dacb_CMPSS1_init(void)
{
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_CMPSS1);
    CMPSS_enableModule(CMPSS1_BASE); // COMPDACE - включение питания ЦАП

    // значение из DACLVALS переписывается в активный регистр по SYSCLK,
    // т.е. запись действует сразу, а не по PWMSYNC
    CMPSS_configDACLow(CMPSS1_BASE, CMPSS_DACVAL_SYSCLK | CMPSS_DACSRC_SHDW);
    CMPSS_setDACValueLow(CMPSS1_BASE, 1024); // середина 11-битной шкалы

    ASysCtl_enableCMPSSExternalDAC(ASYSCTL_CMPSS1_DACL); // вывод COMPDACL на пин
}
#endif // DBG_SIN_DAC

float    sine1 = 0;
volatile uint16_t epwmcount = 0;// dbg счётчик для измерения периода прерываний
volatile uint16_t epwmtime = 0;// dbg период прерываний

// Синхронизация по амплитуде живёт не здесь, а в INT_myADCA_1_ISR (my_Files/myADC.c):
// вилка u220hi_kod/u220low_kod ловит размах U220, sineB.gain идёт за ней. Прежний
// вариант через ampSync (gain = 2048 * U220ампл / Vin) удалён - два регулятора дрались
// бы за sineB.gain, а Vin на стенде всё равно не подключён.

#pragma CODE_SECTION(INT_myEPWM1_ISR, ".TI.ramfunc");

__interrupt void INT_myEPWM1_ISR(void)
{
    tim0S = EPwm7Regs.TBCTR;
    epwmcount++;
   
    if (sine_metod == 1)
    {
        sine_invertor1(&sineB);
    }
    else
    {
        sine_invertor2(&sineB);
    }
    //---------------------------------------------------------------------
    // DBG  daca -это синусоидальный сигнал, который выводится на вход АЦП для иммитации сигнала u220

    // daca = (0.5f * sinf(sine_angle +sineA) + 0.5f) *sineA.gain; // u220
    //  dacb = (0.5f * sinf(sine_angle + sine0 + sine1) + 0.5f) *sineB.gain;// pwm sinus

    // DAC_setShadowValue(myDAC1_BASE, dacb); //  A1, B7, DACB_OUT HSEC pin 11
    //---------------------------------------------------------------------
#if Strob_rg
    if (sineB.angle >= rgStrob)
        if (angleOld < rgStrob)
        {
            index = 0;
        }
    angleOld = sineB.angle;

    if (index == strobIndex)
    {
        GpioDataRegs.GPBTOGGLE.bit.GPIO32 = 1;
    }
#endif // Strob_rg

    index++;
    sineTick++;

// dacb нужен всегда: по нему в INT_U220_zero_ECAP_ISR и adjustPhaze() идёт подстройка фазы
    // offset обязателен: sine_invertor1/2 модулируют ШИМ как sinf(angle + offset),
    // и детектор фазы должен мерить именно тот сигнал, который реально идёт в мост.
    float b = sinf(sineB.angle + sineB.offset) * sineB.gain + 2048; // pwm sinus
    b       = (b < 0) ? 0 : b;
    dacb    = (b > 4095) ? 4095 : b;
#if DBG_SIN_DAC
    // dbg вывод сформированной синусоиды на ЦАП CMPSS1 (COMPDACL).
    // dacb хранится в 12-битной шкале (0..4095), CMP1_DACL - 11-битный, поэтому >> 1
    CMPSS_setDACValueLow(CMPSS1_BASE, dacb); // вывод на пин A1, B7, D11, CMP1_DACL
#endif
// -------------------------------------------------------------------------------
    // Угол уже увеличен на step внутри sine_invertor1()/sine_invertor2().
    // Повторное приращение здесь давало 2*step за прерывание -> выход шёл на 100 Гц
    // и подстройка фазы не успевала за дрожанием (2*step = 0.126 рад против шага 0.01).
    // sineB.angle = PI2sut(sineB.angle + sineB.step);

    EPwm1Regs.ETCLR.bit.INT = 1;
    Interrupt_clearACKGroup(INTERRUPT_ACK_GROUP3);

//   uint32_t txbrp = McanaRegs.MCAN_TXBRP;
    uint32_t txbrp = HWREG_BP(MCANA_DRIVER_BASE + MCAN_TXBRP);

   if (canaKadr.bit.F1_convertor == 1)
        if ((txbrp & MCAN_TXBRP_TRP1) == 0U) // проверяет Buff = 1  !!! Buff = 0,1,2,....32
        {
            uint16_t f[4] = { cmpaA, cmpaB, (int16_t)(duty*1000), sineTick };
            Send_mbox_FD(1, f, 4);                              // 8 байт (DLC=8)
        }

    sine_Time = EPwm7Regs.TBCTR - tim0S; // dbg монитор времени выполнения программы
}
//--------------------------------------------------------------------------------------
__interrupt void INT_myEPWM1_TZ_ISR(void) {}

float    zstep = 0.01;
uint16_t u220_Time; // период сетевого напряжения
uint16_t u220tim0;  // значение счётчика таймера при предыдущем пересечении нуля
float    u220freq;
uint16_t tbperEPWM1; // time base piriod for EPWM1
float    angleA, angleB;
uint32_t u220_badCapture; // dbg счётчик отбракованных захватов (32 бита - 16 переполнялся за секунды)

// Коэффициенты контура подстройки фазы. Вынесены в переменные, чтобы крутить
// их в отладчике на живой цели, не пересобирая проект.
float phaseK    = 0.25f; // коэффициент пропорциональной коррекции
float phaseMax  = 0.05f; // максимальная коррекция за период сети, рад (0.05 -> PI за ~1.3 с)
float phaseErr;          // dbg текущая ошибка фазы
float phaseCorr;         // dbg применённая коррекция

// Сглаживание измеренного периода сети. Частота сети стабильна, а вот момент
// захвата дрожит, и без фильтра TBPRD переписывается каждый период. Разброс
// u220_Time всего в 2% даёт уход фазы 0.126 рад за период - контур с ограничением
// phaseMax = 0.05 такое догнать не может.
float u220_TimeK = 0.1f; // постоянная IIR: 0.1 -> ~10 периодов сети на установление
float u220_TimeF;        // сглаженный период сети, такты TBCLK8

// Окно допустимого периода сети в тактах TBCLK8 (1 171 875 Гц):
// 14000 -> 84 Гц, 35000 -> 33 Гц. Всё, что вне окна - ложный захват.
#define U220_TIME_MIN 14000U
#define U220_TIME_MAX 35000U

//Эта программа прерывания запускается напрямую от CMPSS3 (верхний компаратор, переход U220 через ноль):
//CMPSS3.CTRIPH заведён во вход захвата eCAP1 (U220_zero_ECAP) по внутренней шине, без GPIO и без X-BAR.

__interrupt void INT_U220_zero_ECAP_ISR(void)
{
    ECAP_clearInterrupt(U220_zero_ECAP_BASE, ECAP_ISR_SOURCE_CAPTURE_EVENT_1);

    u220_Time       = EPwm8Regs.TBCTR - u220tim0; //  монитор времени выполнения программы
    u220tim0        = EPwm8Regs.TBCTR;
    // EPWM8: EPWMCLK = SYSCLK = 150 МГц (на F28P55x делителя EPWMCLKDIV нет),
    // CLKDIV=64 * HSPCLKDIV=2 -> /128 -> TBCLK8 = 1 171 875 Гц.
    // (на F280039C было 60 МГц / 32 = 1 875 000 Гц - отсюда старая константа)
    // Ложный захват (дребезг компаратора, первый захват после старта с u220tim0 = 0)
    // НЕЛЬЗЯ пускать в TBPRD: при u220_Time = 94 получается TBPRD = 30, период EPWM1
    // падает до 0.8 мкс, ISR EPWM1 (INT3.1) занимает CPU целиком и вытесняет
    // ISR EPWM6 (INT3.6) в той же группе PIE. daca замирает, CTRIPH залипает в 1,
    // захваты прекращаются - и починить TBPRD уже некому.
    if ((u220_Time >= U220_TIME_MIN) && (u220_Time <= U220_TIME_MAX))
    {
        // IIR по периоду сети. Первый достоверный захват загружаем как есть,
        // иначе фильтр будет долго выползать из нуля.
        if (u220_TimeF < (float)U220_TIME_MIN)
            u220_TimeF = (float)u220_Time;
        else
            u220_TimeF += u220_TimeK * ((float)u220_Time - u220_TimeF);

        u220freq        = 1171875.0f / u220_TimeF;
        // TBCLK1 = 150 МГц / 2 (HSPCLKDIV=2 по сбросу, CLKDIV=1) = 75 МГц -> TBCLK1/TBCLK8 = 64.
        // EPWM1 в режиме UP_DOWN, поэтому TBPRD = полпериода: 64/2 = 32.
        tbperEPWM1      = (u220_TimeF * 32.0f) / sineB.resolution;
        EPwm1Regs.TBPRD = sineB.pwm_period = tbperEPWM1;

        // Целочисленный TBPRD не может выразить дробную часть периода (при 50 Гц идеал
        // 7500.75, в регистр уходит 7500). Остаток расстройки забираем в шаг угла:
        // step = 2*PI * Tшим / Tсети, обе величины в тактах TBCLK8.
        // Период ШИМ в тактах TBCLK8 = TBPRD/32 (UP_DOWN даёт 2*TBPRD тактов TBCLK1,
        // а TBCLK1/TBCLK8 = 64). Берём именно tbperEPWM1 - то целое, что реально в TBPRD.
        // Без этой строки offset вынужден непрерывно ползти со скоростью 0.03 рад/с,
        // компенсируя уход частоты 1e-4.
        sineB.step = PI2 * ((float)tbperEPWM1 / 32.0f) / u220_TimeF;

        // Ошибка фазы = разница мгновенных значений опоры и нашей синусоиды в момент
        // захвата. Сравниваем значения, а не углы: тогда точка срабатывания компаратора
        // (из-за DAC_GAIN_TWO она не совпадает с нулём daca) выпадает из уравнения.
        angleB    = dacb;
        angleA    = daca;
        phaseErr  = (angleB - angleA) / sineB.gain; // в единицах sin(), знак = опережение
        phaseCorr = phaseK * phaseErr;
        // Ограничение скорости: один ложный захват не должен дёрнуть фазу рывком.
        if (phaseCorr > phaseMax)
            phaseCorr = phaseMax;
        else if (phaseCorr < -phaseMax)
            phaseCorr = -phaseMax;

        sineB.offset = asut(sineB.offset - phaseCorr);
        // sineB.angle НЕ трогаем: угол должен идти непрерывно, иначе каждый захват
        // даёт разрыв в форме сигнала. Фазу двигает только offset.
    }
    else
    {
        u220_badCapture++; // dbg
    }

    // Сжатие вилки U220 (u220hi_kod--/u220low_kod++) переехало в INT_myADCA_1_ISR:
    // здесь оно шло раз за период сети против 100 раздвижек за тот же период, и вилка
    // почти не сжималась. Скорость сжатия - u220spadDiv в myADC.c.

    // Без сброса глобального флага ECCLR.INT модуль eCAP больше не выдаёт
    // прерываний - ISR отрабатывает ровно один раз после прошивки.
    ECAP_clearGlobalInterrupt(U220_zero_ECAP_BASE);
    ECAP_reArm(U220_zero_ECAP_BASE);
    Interrupt_clearACKGroup(INTERRUPT_ACK_GROUP4);
}

__interrupt void U220_ADCC_ZERO(void)
{
    //adjustPhaze();
    // PieCtrlRegs.PIEACK.all = PIEACK_GROUP10;
    Interrupt_clearACKGroup(INTERRUPT_ACK_GROUP10);
}

void adjustPhaze(void)
{
    if (GpioDataRegs.GPASET.bit.GPIO26)
    {
        GpioDataRegs.GPBSET.bit.GPIO32 = 1;
        if (dacb > 2048)
            sine0 = sine0 + zstep;
        else
            sine0 = sine0 - zstep;
        Cmpss3Regs.COMPSTSCLR.bit.HLATCHCLR = 1;
    }
    else
    {
        GpioDataRegs.GPBCLEAR.bit.GPIO32    = 1;
        Cmpss3Regs.COMPSTSCLR.bit.HLATCHCLR = 1;
        if (dacb < 2048)
            sine0 = sine0 + zstep;
        else
            sine0 = sine0 - zstep;
    }
    // DEVICE_DELAY_US(50);
}
// ------------------------------------------------------------------------
// DBG

__interrupt void INT_myEPWM6_ISR(void)
{
      GpioDataRegs.GPBTOGGLE.bit.GPIO57 = 1; // scope dbg
    sineA.angle = PI2sut(sineA.angle + sineA.step);
    float a     = sinf(sineA.angle + sineA.offset) * sineA.gain + 2048; // u220
    a           = (a < 0) ? 0 : a;
    daca        = (a > 4095) ? 4095 : a;
    DAC_setShadowValue(myDAC1_BASE, daca); // A0, B15, C15 ,DACA_OUT -> HSEC pin 9

    EPwm6Regs.ETCLR.bit.INT = 1;
    Interrupt_clearACKGroup(INTERRUPT_ACK_GROUP3);
}

void change_sine(struct sine *s)
{
    s->step       = PI2 / s->resolution;
    s->tpwm_sek   = (1.0f / s->freq) / s->resolution; // pwm period  в секундах
    s->pwm_period = s->tpwm_sek * 75e6; //60e6*150/120;           // TBPRD в циклах таймера
}

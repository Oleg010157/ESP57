/*
 * Main_ISR.c
 *
 *  Created on: 21 февр. 2023 г.
 *      Author: Oleg Pavlovsky
 */

#include <stdbool.h>
#include <math.h>


#include "driverlib.h"
//#include "device.h"
#include "f28p55x_device.h"
#include "KSP.h"

#include "myDCAN.h"
#include "myADC.h"
#include "cla_shared.h" // измеренные величины всех каналов АЦП: кадр F1
#include "1ph_Sin.h"
#include "myEPWM.h"

#include "hw_mcanss.h"
#include "hw_memmap.h"
#include "hw_types.h"


uint16_t          IsrTicker = 0; // счётчик PWM прерываний
uint16_t          cTicker   = 0; // счётчик PWM прерываний или периодов при калибровке

uint16_t igbt_manually = 0; // 1 - мост под ручным управлением (тест IGBT)
uint16_t igbt_tripped  = 0; // тест прерван токовой защитой: TRIP_SRC_* | 0x8000
float    igbt_testIlim = 0.0f; // порог первички на время теста, А; 0 - рабочий
float    igbt_tripI    = 0.0f; // ток первички перед аварией теста, А
IGBT igbt;

extern float    Tpwm;          //  период ШИМ в мкс
extern uint16_t timeBasePeriod;

#if dbgIfull
int Udc_imit = 1000;
#endif

extern STEND        stend;
extern STEND_bit    sbit;
extern OPTIC_ERRs   OPTIC_ERR1;
extern OPTIC_ERRs   OPTIC_ERR2;
extern REFs         REF;
extern uint16_t       BrRes1_Val;
extern uint16_t       BrRes2_Val;
extern union PCflag sharp;

extern bool AdcCalInProgress; // True - калибровка каналов в процессе
extern int  SplMode;


uint16_t tim0M, mainISR_Time;

void COEFbit(QEPpar                    *p,
             volatile struct EQEP_REGS *reg); // расширение 16 bit таймера для измерения qep.pr  на малых оборотах
void QEP_Calc(QEPpar *p, volatile struct EQEP_REGS *reg);

void  Reset_stend_filtr(void);
void  ePWM_load();
void  ReadError();
void  Send_Tooth_Data(void);
float saturfloat(float input, float high, float low);

#if dbgUdc
float Udc_imit = 700;
#endif

//---------------------------------------------------------------

#pragma CODE_SECTION(MainISR, ".TI.ramfunc");

void MainISR(void)
{
    // DBG измеритель времени прохождения Maim_ISR
    // для измерения используется таймер ШИМ
    tim0M = EPwm7Regs.TBCTR;

    GpioDataRegs.GPBSET.bit.GPIO41 = 1;

    IsrTicker++; // номер прерывания

    // Здесь стояла загрузка CMPA из трёхфазного модулятора:
    //
    //     DINT;
    //     EPwm2Regs.CMPA.bit.CMPA = timeBasePeriod * (1 + motors[0]->svgen.Ta) / 2;
    //     EPwm3Regs.CMPA.bit.CMPA = ... svgen.Tb ...
    //     EPwm4Regs.CMPA.bit.CMPA = ... svgen.Tc ...
    //     EINT;
    //
    // Наследство трёхфазного проекта, и в K57 оно вредило. EPWM2 здесь - плечо B
    // моста однофазного инвертора (GPIO2/GPIO3), его CMPA каждый такт считает
    // sine_invertor2() / grid_tie_step() в INT_myADCB_1_ISR. А svgen.Ta/Tb/Tc
    // выставлены в 0.5 один раз в main.c (PUSK_Init) и больше не обновляются -
    // трёхфазного модулятора в проекте нет. То есть каждое прерывание EPWM3 в
    // плечо B прилетало 0.75 * timeBasePeriod поверх синусной модуляции.
    //
    // EPWM3/EPWM4 выходных пинов не имеют (в SysConfig у них только
    // $suggestSolution), работают таймерами и источниками SOC - записи в их
    // CMPA не делали ничего.
    //
    // Заодно ушёл EINT посреди обработчика: он открывал окно вложенных
    // прерываний в середине такта.

    //-----------------------------------------------------------------------------------------------------------
    ReadError(); // Считывание сигналов ошибок с КСП

  //--------------------------------------------------------------------------------
  // отправка одного какого-либо SPL файла если есть такой запрос
  //  if (SplMode == Spl_Fast)
  //      Send_FastKadr();

 //   uint32_t txbrp = McanaRegs.MCAN_TXBRP;
    uint32_t txbrp = HWREG_BP(MCANA_DRIVER_BASE + MCAN_TXBRP);

    // Быстрый кадр F3 выключен вместе с CANA_UI.c

    // Без проверки буфера CAN: кадр уходит и в WiFi, а буфер Send_mbox_FD
    // проверяет сама - см. 1ph_Sin.c, телеметрия в такте.
    (void)txbrp;
    if (canaKadr.bit.F1_invertor == 1)
        {
            // Токи берутся у CPU (1ph_Sin.c, xfmr_currents_from_adc), ток АКБ -
            // у CLA: канал A12 больше никто не читает.
            uint16_t f[4] = { iPri_kod, iSec_kod, iBat_kod, IsrTicker };
            Send_mbox_FD(1, f, 4);                              // 8 байт (DLC=8)
        }

    // DBG
    mainISR_Time = (EPwm7Regs.TBCTR - tim0M); // dbg время в 1/120 мкс

} // end void MainISR(void)

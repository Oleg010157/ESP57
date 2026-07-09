/*
 * Main_ISR.c
 *
 *  Created on: 21 февр. 2023 г.
 *      Author: Oleg Pavlovsky
 */

#include <stdbool.h>
#include <math.h>

#include "f28p55x_device.h"
#include "driverlib.h"
#include "device.h"

#include "KSP.h"

#include "myDCAN.h"
#include "myADC.h"
#include "cla_shared.h"
#include "myEPWM.h"
#include "hw_memmap.h"
#include "hw_types.h"
#

uint16_t          IsrTicker = 0; // счётчик PWM прерываний
uint16_t          cTicker   = 0; // счётчик PWM прерываний или периодов при калибровке

uint16_t igbt_manually = 0;// логическая переменная для включения прямого управления IGBT
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
void  Send_FastKadr();
void  Send_Tooth_Data(void);
void  SPL_UI();
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
    tim0M                          = EPwm7Regs.TBCTR;
    GpioDataRegs.GPBSET.bit.GPIO41 = 1;

    IsrTicker++; // номер прерывания

  

#if (CalcMotor1)
    MotorCalc(&motor1);
#endif

#if (CalcMotor2)
    MotorCalc(&motor2);
#endif

    // загрузка нового значения CMPA в PWM секцию

    DINT;
  
    {
        EPwm2Regs.CMPA.bit.CMPA = timeBasePeriod * (1.0F + motor1.svgen.Ta) / 2;
        EPwm3Regs.CMPA.bit.CMPA = timeBasePeriod * (1.0F + motor1.svgen.Tb) / 2;
        EPwm4Regs.CMPA.bit.CMPA = timeBasePeriod * (1.0F + motor1.svgen.Tc) / 2;
    }
    EINT;

    //-----------------------------------------------------------------------------------------------------------
    ReadError(); // Считывание сигналов ошибок с КСП

  //--------------------------------------------------------------------------------
  // отправка одного какого-либо SPL файла если есть такой запрос
  //  if (SplMode == Spl_Fast)
  //      Send_FastKadr();

    uint32_t txbrp = McanaRegs.MCAN_TXBRP.all;

    if (canaKadr.bit.F3_invertor == 1)
        if ((txbrp & MCAN_f3_mask) == 0U) // если завершена передача всех Mbox CANA_fastkadra
            SPL_UI();

    if (canaKadr.bit.F1_convertor == 1)                         // проверяем что есть запрос на передачу данных по CANA
        if ((txbrp & MCAN_f1_mask) == 0U)                       // проверяем что 103 Mbox свободен для передачи
            Send_mbox(3, Idc_kod, Udc_kod, Vin_kod, IsrTicker); // загружаем данные в 3-ий Mbox

    if (canaKadr.bit.F1_invertor == 1)                          // проверяем что есть запрос на передачу данных по CANA
        if ((txbrp & MCAN_f1_mask) == 0U)                       // проверяем что 103 Mbox свободен для передачи
            Send_mbox(3, Ia_kod, Ib_kod, Idc_kod, IsrTicker);   // загружаем данные в 3-ий Mbox

    // DBG
    mainISR_Time = (EPwm7Regs.TBCTR - tim0M); // dbg время в 1/120 мкс

} // end void MainISR(void)

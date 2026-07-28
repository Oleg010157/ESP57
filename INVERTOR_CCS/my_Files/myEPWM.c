/*
 * myEPWM.c
 *
 *  Created on: 13.05.2025 
 * модификация от 28.07.2026
 *      Author: olegs.pavlovskis
 */
#include "board.h"
#include "math.h"
#include "f28p55x_device.h"
#include "f28p55x_pie_defines.h"
#include "myEPWM.h"
#include "KSP.h"
#include "interrupt.h"

union XBARFLG1_REG xbarFlg1;
union ErrorFlags overload;

__interrupt void INT_EPWM2_Iu_ISR(void)
{

    EPWM_clearEventTriggerInterruptFlag(EPWM2_BASE); // EPwm1Regs.ETCLR.bit.INT = 1;
    Interrupt_clearACKGroup(INTERRUPT_ACK_GROUP3);      // PieCtrlRegs.PIEACK.all = PIEACK_GROUP3;
}

Uint16    dbgEPWM_TZ;
SHR_Flags shrTrip;
bool eventDetected = false;

 void RegisterSessionEvent(uint16_t code)
{
    bool isNewSessionEvent;

    if (code >= ERR_BUFF_SIZE)
    {
        return;
    }

    isNewSessionEvent = (ErrSessionEdgeLatch[code] == 0U);

    CurrentErrFlags[code] = 1U;
    ErrSessionEdgeLatch[code] = 1U;

    if (isNewSessionEvent)
    {
        if (ErrBuff[code].Pars.SessionCnt < 0x00FFU)
        {
            ErrBuff[code].Pars.SessionCnt++;
        }

        ErrHistory_OnError(code);
    }
    eventDetected = true;
     CheckLamp = 10;
}



__interrupt void INT_EPWM2_Iu_TZ_ISR(void)
{

    dbgEPWM_TZ++;
    

    EALLOW;
    EPwm2Regs.TZEINT.bit.DCAEVT1 = 0; // Disabled Interrupt for Digital Compare Output A Event 1 (DCAEVT1)
    EPwm2Regs.TZEINT.bit.OST     = 0; // Disabled Interrupt for One Shot Event
    EDIS;

  

    shrTrip.all   = 0xffff;//readShiftRegister(); //читаем сдвиговый регистр
 

    CheckLamp0    = 1;                   // зажигаем лампу диагностики постоянно до сброса от UI
    eventDetected = false;

    //проверяем ножку 57. Если в нуле то значит есть сигнал ошибки хотя от одного драйвера транзистора six-pack.
    if (XbarRegs.XBARFLG2.bit.INPUT4 == 1)
    {
        // делаем поиск от какого транзистора пришёл сигнал ошибки и формируем соответствующий код.
        if (!shrTrip.bit.t1)
        {
            RegisterSessionEvent(inv_T1);
        }
        if (!shrTrip.bit.t2)
        {
            RegisterSessionEvent(inv_T2);
        }
        if (!shrTrip.bit.t3)
        {
            RegisterSessionEvent(inv_T3);
        }
        if (!shrTrip.bit.t4)
        {
            RegisterSessionEvent(inv_T4);
        }
        if (!shrTrip.bit.t5)
        {
            RegisterSessionEvent(inv_T5);
        }
        if (!shrTrip.bit.t6)
        {
            RegisterSessionEvent(inv_T6);
        }
    }

    // проверяем ножку GPIO8 (INPUTXBAR1 -> TRIP4): перегрузка драйверов dc/dc конвертора
    if (XbarRegs.XBARFLG2.bit.INPUT1 == 1)
    {
        if (shrTrip.bit.vt1)
        {
            RegisterSessionEvent(conv_T1);
        }

        if (shrTrip.bit.vt2)
        {
            RegisterSessionEvent(conv_T2);
        }

        if (shrTrip.bit.vt3)
        {
            RegisterSessionEvent(conv_T3);
        }

        if (shrTrip.bit.vt4)
        {
            RegisterSessionEvent(conv_T4);
        }
    }
       if (!eventDetected)
        {
           shrTrip.bit.TS2 = 0;
           RegisterSessionEvent(trip4_uknow);
        }

    PieCtrlRegs.PIEACK.all = PIEACK_GROUP2;
}

void CMPSS_BackgroundCheck(void)
{
    xbarFlg1 = XbarRegs.XBARFLG1;
  
    if (XbarRegs.XBARFLG1.bit.CMPSS3_CTRIPL)
    {
        RegisterSessionEvent(cmp3L_Ia);
     }

    if (XbarRegs.XBARFLG1.bit.CMPSS3_CTRIPH)
    {
        RegisterSessionEvent(cmp3H_Ia);
    }

    if (XbarRegs.XBARFLG1.bit.CMPSS1_CTRIPL)
    {
        RegisterSessionEvent(cmp1L_Ib);
    }

    if (XbarRegs.XBARFLG1.bit.CMPSS1_CTRIPH)
    {
        RegisterSessionEvent(cmp1H_Ib);
    }
 

}

void TRIP_Clear(void)
{
    XbarRegs.XBARCLR1.all = 0xFFFFFFFFU;
    XbarRegs.XBARCLR2.all = 0xFFFFFFFFU;

    EALLOW;
    EPwm2Regs.TZCLR.bit.OST      = 1;
    EPwm2Regs.TZCLR.all          = 0xFFFF;
    EPwm3Regs.TZCLR.all          = 0xFFFF;
    EPwm4Regs.TZCLR.all          = 0xFFFF;
    EPwm2Regs.TZEINT.bit.DCAEVT1 = 1;
    EPwm2Regs.TZEINT.bit.OST     = 1;
    EDIS;
   
}

__interrupt void six_overload_XINT1_ISR(void) {}

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

    // Токовая защита от компараторов приходит по отдельной линии TRIP5 -> DCBEVT1,
    // а аварии драйверов по TRIP4. Разбираем причину по флагу, не лезя в сдвиговый
    // регистр: иначе срабатывание по току диагностировалось бы как trip4_uknow.
    if ((EPwm1Regs.TZFLG.bit.DCBEVT1) || (EPwm2Regs.TZFLG.bit.DCBEVT1))
    {
        // Какая фаза - различаем по защёлкам компараторов.
        // CMPSS2 = Ia (первичка), CMPSS4 = Ib (вторичка).
        if (Cmpss2Regs.COMPSTS.bit.COMPHLATCH || Cmpss2Regs.COMPSTS.bit.COMPLLATCH)
        {
            RegisterSessionEvent(gt_overIpri);
        }
        if (Cmpss4Regs.COMPSTS.bit.COMPHLATCH || Cmpss4Regs.COMPSTS.bit.COMPLLATCH)
        {
            RegisterSessionEvent(gt_overIsec);
        }

        CheckLamp0 = 1;
        PieCtrlRegs.PIEACK.all = PIEACK_GROUP2;
        return;
    }

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

/* Раскладка аварийных линий.
 *
 *   TRIP4 - аварии драйверов ключей (входы INPUTXBAR). Как было.
 *   TRIP5 - токовая защита от компараторов. НОВОЕ.
 *
 * Зачем отдельная линия, а не всё на TRIP4: обработчик INT_EPWM2_Iu_TZ_ISR
 * по TRIP4 читает сдвиговый регистр, чтобы понять, КАКОЙ транзистор пожаловался.
 * Если завести туда же ток, каждое срабатывание токовой защиты диагностировалось
 * бы как trip4_uknow, и причина терялась бы. С разными линиями источник аварии
 * виден сразу по флагу TZFLG, без разбора регистров.
 *
 * Раскладка мультиплексоров EPWM X-BAR на F28P55x:
 *   MUX02 - CMPSS2_CTRIPH_OR_L  -> Ia, первичка
 *   MUX06 - CMPSS4_CTRIPH_OR_L  -> Ib, вторичка
 * Вариант CTRIPH_OR_L несёт ОБЕ полярности в одной линии - для переменного тока
 * это ровно то, что нужно, отдельный мультиплексор на каждую полярность не нужен.
 *
 * ВНИМАНИЕ: аварийное состояние выхода задаёт IGBT_ACTIVE_HIGH (myEPWM.h).
 * Пока полярность драйверов новой платы не подтверждена, этот путь НЕ ПРОВЕРЕН
 * на железе - см. my_Files/grid_tie.txt, раздел 10.
 */
void TRIP_Init(void)
{
    // --- Безопасные пороги ДО включения линии --------------------------
    // Порядок обязателен. SysConfig генерирует для CMPSS4 DACH = 0, а верхний
    // компаратор срабатывает при "вход > DACH" - то есть CTRIPH взведён с самого
    // включения питания. Заведи такую линию в trip-zone, и мост уйдёт в аварию
    // на старте и уже не выйдет: PWM мёртв, причём молча.
    //
    // Раскрываем окно полностью (DACH = максимум, DACL = 0), чтобы компаратор
    // не мог сработать. Рабочие уставки поставит currentLimit() из main(), когда
    // будут готовы масштабы kadcf (их считает floatKadc() внутри checkEE(),
    // а это происходит ПОЗЖЕ этой функции).
    //
    // Пока currentLimit() не отработал, аппаратной токовой защиты нет. Это
    // правильный компромисс: на этом этапе ключи ещё закрыты, а вот
    // залипшая авария не даёт стартовать вообще.
    Cmpss2Regs.DACHVALS.bit.DACVAL = 4095U;
    Cmpss2Regs.DACLVALS.bit.DACVAL = 0U;
    Cmpss4Regs.DACHVALS.bit.DACVAL = 4095U;
    Cmpss4Regs.DACLVALS.bit.DACVAL = 0U;

    // --- X-BAR: источники -> TRIP5 ------------------------------------
    // Выбор источника мультиплексора глобальный, разрешение - на каждую линию.
    XBAR_setEPWMMuxConfig(XBAR_TRIP5, XBAR_EPWM_MUX02_CMPSS2_CTRIPH_OR_L);
    XBAR_setEPWMMuxConfig(XBAR_TRIP5, XBAR_EPWM_MUX06_CMPSS4_CTRIPH_OR_L);
    XBAR_enableEPWMMux(XBAR_TRIP5, XBAR_MUX02 | XBAR_MUX06);

    // --- EPWM1 и EPWM2: TRIP5 -> DCBEVT1 -> one-shot ------------------
    // DCAH уже занят линией TRIP4 (аварии драйверов), поэтому берём канал B.
    uint32_t base;
    uint16_t i;
    uint32_t bases[2] = { myEPWM1_BASE, myEPWM2_BASE };

    for (i = 0; i < 2U; i++)
    {
        base = bases[i];

        EPWM_selectDigitalCompareTripInput(base, EPWM_DC_TRIP_TRIPIN5, EPWM_DC_TYPE_DCBH);
        EPWM_setTripZoneDigitalCompareEventCondition(base, EPWM_TZ_DC_OUTPUT_B1,
                                                     EPWM_TZ_EVENT_DCXH_HIGH);
        EPWM_enableTripZoneSignals(base, EPWM_TZ_SIGNAL_DCBEVT1);

#if IGBT_ACTIVE_HIGH
        // HIGH открывает ключ -> аварийное состояние LOW
        EPWM_setTripZoneAction(base, EPWM_TZ_ACTION_EVENT_DCBEVT1, EPWM_TZ_ACTION_LOW);
#else
        // HIGH закрывает ключ -> аварийное состояние HIGH
        EPWM_setTripZoneAction(base, EPWM_TZ_ACTION_EVENT_DCBEVT1, EPWM_TZ_ACTION_HIGH);
#endif
    }

    // Прерывание по аварии разрешаем только у EPWM2: там зарегистрирован
    // обработчик INT_EPWM2_Iu_TZ_ISR. У EPWM1 действие trip-zone работает
    // так же (ключи закрываются), но поднимать у него прерывание незачем -
    // диагностика нужна одна, а вектор EPWM1_TZ может оказаться незанятым.
    EPWM_enableTripZoneInterrupt(myEPWM2_BASE, EPWM_TZ_INTERRUPT_DCBEVT1);
}

void TRIP_Clear(void)
{
    XbarRegs.XBARCLR1.all = 0xFFFFFFFFU;
    XbarRegs.XBARCLR2.all = 0xFFFFFFFFU;

    EALLOW;
    EPwm2Regs.TZCLR.bit.OST      = 1;
    EPwm1Regs.TZCLR.all          = 0xFFFF; // токовая защита живёт и на EPWM1
    EPwm2Regs.TZCLR.all          = 0xFFFF;
    EPwm3Regs.TZCLR.all          = 0xFFFF;
    EPwm4Regs.TZCLR.all          = 0xFFFF;
    EPwm2Regs.TZEINT.bit.DCAEVT1 = 1;
    EPwm2Regs.TZEINT.bit.OST     = 1;
    EDIS;

}

__interrupt void six_overload_XINT1_ISR(void) {}

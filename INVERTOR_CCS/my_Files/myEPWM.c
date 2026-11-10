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
#include "bbox_trig.h"
#include "bridge_store.h"

__interrupt void INT_EPWM2_Iu_ISR(void)
{

    EPWM_clearEventTriggerInterruptFlag(EPWM2_BASE); // EPwm1Regs.ETCLR.bit.INT = 1;
    Interrupt_clearACKGroup(INTERRUPT_ACK_GROUP3);      // PieCtrlRegs.PIEACK.all = PIEACK_GROUP3;
}

Uint16    dbgEPWM_TZ;
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
        BBox_onEvent(code);        // чёрный ящик в мосте, bbox_trig.h
        BridgeStore_onEvent(code); // длинный журнал в мосте, bridge_store.h
    }
    eventDetected = true;
     CheckLamp = 10;
}



// Единственная аварийная линия платы - токовая защита компараторов:
// TRIP5 -> DCBEVT1, раскладка в TRIP_Init(). Линия TRIP4 (аварии драйверов
// через сдвиговые регистры U4/U5) и разбор на коды inv_T*/conv_T* достались
// от B81, где за мостом стоял ещё и DC-DC конвертор 48/600 В. Этого железа
// здесь нет, и читать его флаги было нечем: обработчик подставлял 0xffff.
__interrupt void INT_EPWM2_Iu_TZ_ISR(void)
{
    dbgEPWM_TZ++;

    // Глушим источник на время разбора. Флаг DCBEVT1 в TZFLG держится до явной
    // очистки в TRIP_Clear(), поэтому с разрешённым прерыванием обработчик
    // входил бы заново до самой очистки - а она приходит из фона, раз в секунду.
    EALLOW;
    EPwm2Regs.TZEINT.bit.DCBEVT1 = 0;
    EPwm2Regs.TZEINT.bit.OST     = 0;
    EDIS;

    eventDetected = false;

    // Какой канал - различаем по защёлкам компараторов.
    // CMPSS2 = Ia (первичка), CMPSS4 = Ib (вторичка). Верхняя защёлка -
    // превышение в плюс, нижняя - в минус; знак нужен тесту IGBT, чтобы
    // сверить его с открытой диагональю.
    uint16_t src = 0U;

    if (Cmpss2Regs.COMPSTS.bit.COMPHLATCH) src |= TRIP_SRC_IPRI_H;
    if (Cmpss2Regs.COMPSTS.bit.COMPLLATCH) src |= TRIP_SRC_IPRI_L;
    if (Cmpss4Regs.COMPSTS.bit.COMPHLATCH) src |= TRIP_SRC_ISEC_H;
    if (Cmpss4Regs.COMPSTS.bit.COMPLLATCH) src |= TRIP_SRC_ISEC_L;

    if (src & (TRIP_SRC_IPRI_H | TRIP_SRC_IPRI_L))
    {
        RegisterSessionEvent(gt_overIpri);
    }
    if (src & (TRIP_SRC_ISEC_H | TRIP_SRC_ISEC_L))
    {
        RegisterSessionEvent(gt_overIsec);
    }

    // Ни одна защёлка не читается - авария всё равно была, раз мы здесь.
    // Код обязателен: лампа CheckLamp0 горит до ручного сброса, и зажечь её
    // без записи в таблицу значит оставить наладчика с красной лампой и
    // пустым окном кодов.
    if (!eventDetected)
    {
        RegisterSessionEvent(tz_unknown);
    }

    CheckLamp0 = 1; // зажигаем лампу диагностики постоянно до сброса от UI

    // Тест IGBT: ключи держит форсаж, и после снятия защёлки фоном он открыл
    // бы их снова. Гасим его здесь же.
    if (igbt_manually)
        Bridge_ManualTrip(src);

    PieCtrlRegs.PIEACK.all = PIEACK_GROUP2;
}

/* Раскладка аварийных линий.
 *
 *   TRIP5 - токовая защита от компараторов. Единственная линия на этой плате.
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

    // Защёлки компараторов держатся до явной очистки, а не снимаются вместе
    // с флагом trip-zone. Не чистить их значит навсегда запомнить первый
    // сработавший канал: следующая авария по вторичке всё равно показала бы
    // gt_overIpri, потому что защёлка первички так и осталась взведённой.
    Cmpss2Regs.COMPSTSCLR.bit.HLATCHCLR = 1;
    Cmpss2Regs.COMPSTSCLR.bit.LLATCHCLR = 1;
    Cmpss4Regs.COMPSTSCLR.bit.HLATCHCLR = 1;
    Cmpss4Regs.COMPSTSCLR.bit.LLATCHCLR = 1;

    EALLOW;
    EPwm2Regs.TZCLR.bit.OST      = 1;
    EPwm1Regs.TZCLR.all          = 0xFFFF; // токовая защита живёт и на EPWM1
    EPwm2Regs.TZCLR.all          = 0xFFFF;
    EPwm3Regs.TZCLR.all          = 0xFFFF;
    EPwm4Regs.TZCLR.all          = 0xFFFF;
    EPwm2Regs.TZEINT.bit.DCBEVT1 = 1; // источник тот же, что разрешает TRIP_Init()
    EPwm2Regs.TZEINT.bit.OST     = 1;
    EDIS;

}

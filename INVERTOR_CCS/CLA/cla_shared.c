// cla_shared.c
#include "f28p55x_device.h"
#include "f28p55x_pie_defines.h"
#include "KSP.h"

#include "cla_shared.h"

void MainISR(void);



// CPU → CLA
#pragma DATA_SECTION(kadcf, "CpuToCla1MsgRAM");
volatile kADCf kadcf;

#pragma DATA_SECTION(c2p, "Cla1ToCpuMsgRAM");
volatile ClaToCpu c2p;

#pragma DATA_SECTION(cla, "cla_shared");
volatile Cla7Shared cla;

#pragma DATA_SECTION(dc, "cla_shared");
volatile DCShared dc;

#pragma DATA_SECTION(cs, "cla_shared");
volatile Clasum cs;

#pragma DATA_SECTION(rms, "cla_shared");
volatile CLA_RMS rms;

#pragma DATA_SECTION(pi, "cla_shared");
PI_Controller pi;

#pragma DATA_SECTION(dctl, "cla_shared");
DutyControl dctl;

volatile uint16_t clatest = 0;
volatile float dbgIa,dbgIb,dbgIc;

void __interrupt INT_myCLA7_ISR()
{
    clatest++;

    // Вызов Adc_Calibrate() отсюда убран: этот вектор не регистрируется, пока
    // модуля CLA нет в активном c2000.syscfg, и автокалибровка нулей из-за
    // этого не работала вовсе. Теперь она живёт в INT_myADCA_1_ISR (myADC.c) -
    // прерывании, которое действительно выполняется.
    //
    // Если задачи CLA когда-нибудь оживят, ВЕРНУТЬ ЕЁ СЮДА НЕЛЬЗЯ: получится
    // два вызова за такт. То же касается MainISR() ниже - он уже вызывается из
    // INT_myEPWM3_ISR (3ph_sin.c).

    MainISR();

    dbgIa = c2p.Ia_amp;
    dbgIb = c2p.Ib_amp;
    dbgIc = c2p.Ic_amp;
    PieCtrlRegs.PIEACK.all = PIEACK_GROUP11;
}

// Функция инициализации общих структур
void CLA_shared_Init(void)
{
    // Входные данные
  
    cla.Ia_rf = 0;
    cla.Ib_rf = 0;
    cla.Ic_rf = 0;

    dc.Idc_rf = 0;


    // Общая структура
    cla.count = 0;
    cla.flag  = 0;

    // PI-регулятор
    pi.Kp       = 0.05f;
    pi.Ki       = 0.01f;
    pi.integral = 0.0f;
    pi.outMin   = 0.0f;
    pi.outMax   = 1.0f;

    // DutyControl
    dctl.prechargeLimit = 100000; // 50 кГц * 2 сек
    dc.Idc_max     = 5.0f;
    dc.Udc_ref     = 100.0f;
}
//void CLAfiltersReset(void)


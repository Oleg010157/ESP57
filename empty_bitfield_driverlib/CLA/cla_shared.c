// cla_shared.c
#include "f28p55x_device.h"
#include "f28з55x_pie_defines.h"
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
     //----------------------------------------------------------------------------------------------------------------
            // если есть запрос на калибровку нуля какого либо канала АЦП
            // то выполняем метод Adc_Calibrate()
            if (adcCalStart.all != 0)
            {
                Adc_Calibrate();
                
            }
    //----------------------------------------------------------------------------------------------------------------

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

    Idc_rf = 0;
    

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
    Idc_max        = 5.0f;
    Udc_ref        = 100.0f;
}
//void CLAfiltersReset(void)


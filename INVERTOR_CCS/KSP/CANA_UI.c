/*
 * CANA_UI.c
 *
 *  Created on: 24 авг. 2022 г.
 *      Author: Oleg Pavlovsky
 *  Быстрый SPL кадр - вывод включается  каждое MainISR прерывание по команде 1001 от MBOX28
 *  Вывод Kadr60() блокируется по команде 1001 от MBOX28
 *  Заполняет ячейки САNA MBOX- ов
 */
#include "f28p55x_device.h"
#include "KSP.h"

#include "myADC.h"
#include "myDCAN.h"
#include "cla_shared.h"

extern STEND_bit sbit;
extern motor     motor1;
extern motor     motor2;
extern uint16_t    IsrTicker;
extern uint16_t    canLossCounter;
extern uint16_t    K60cnt;

bool             CANA_fastkadr_run = 0;
extern uint16_t  timeBasePeriod;



void SPL_UI()
{
    uint16_t d1, d2, d3, d4;
#if (CANA_f3_mask & BIT2) == BIT2 // MsgID = 103
    d1 = Ia_kod;
    d2 = Ib_kod;
    d3 = Idc_kod;
    d4 = Udc_kod;
    Send_mbox(3, d1, d2, d3, d4);
#endif

#if (CANA_f3_mask & BIT3) == BIT3 // MsgID = 104
    d1 = rounding(c2p.Va_volt * 50);
    d2 = rounding(c2p.Vb_volt * 50);
    d3 = rounding(Idc_rf * 50); //rounding(motor1.rg.radian *1000);   //rounding(motor1.clark.Im * 100);
    d4 = rounding(Udc_rf * 50); //rounding(motor1.rg.Out * 1000);
    Send_mbox(4, d1, d2, d3, d4);
#endif

#if (CANA_f3_mask & BIT4) == BIT4 // MsgID = 110
    d1 = Vin_kod;
    d2 = (uint16_t)(Vin_rf * 50 + 0.5); //выходное результирующее значение индекса модуляции напряжения;
    d3 = (uint16_t)(c.duty*100 +0.5);
    d4 = IsrTicker;
    Send_mbox(5, d1, d2, d3, d4);
#endif
}

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



// Быстрый кадр F3_invertor: 3 группы по 4 слова = 12 слов = 24 байта, один FD-кадр (буфер 1).
// DLC=12 задан в MBOX28. Порядок слов сохранён (было ID 103 / 104 / 110).
void SPL_UI()
{
    uint16_t fast[12];

    // группа A (было ID 103): токи/напряжения инвертора
    fast[0] = Ia_kod;
    fast[1] = Ib_kod;
    fast[2] = Idc_kod;
    fast[3] = Udc_kod;

    // группа B (было ID 104)
    fast[4] = rounding(c2p.Va_volt * 50);
    fast[5] = rounding(c2p.Vb_volt * 50);
    fast[6] = rounding(Idc_rf * 50);
    fast[7] = rounding(Udc_rf * 50);

    // группа C (было ID 110)
    fast[8]  = Vin_kod;
    fast[9]  = (uint16_t)(Vin_rf * 50 + 0.5); // индекс модуляции напряжения
    fast[10] = (uint16_t)(c.duty * 100 + 0.5);
    fast[11] = IsrTicker;

    Send_mbox_FD(1, fast, 12);
}

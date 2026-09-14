/*
 * REF_Kadr.c
 *
 *  Created on: 25 июл. 2017 г.
 *      Author: olegs.pavlovskis
 */
#include "KSP.h"


#include "board.h"
#include "myDCAN.h"
#include "Errors.h"
#include "myADC.h"
#include "1ph_Sin.h"
#include "grid_tie.h"
#include "cla_shared.h"


extern STEND_bit sbit;
extern SHR_Flags shrTrip;


// Ключ выбора набора величин для слов 4..7. Пишет команда 634 (MBOX28.c).
// По умолчанию rms: именно они нужны на работающем мосте, а коды и мгновенные
// значения смотрят при наладке канала.
uint16_t teleSel = TELE_RMS;


void send_REF_kadr()
{
    // Компактный кадр ID 140: 16 слов = 32 байта.
    uint16_t tele[16] = {0};


    tele[0] = motors[0]->error.all;
    tele[1] = motors[1]->error.all;
    tele[2] = sbit.all;
    tele[3] = shrTrip.all;
    shrTrip.all = 0xFFFF;

    // Четыре канала моста, по слову на канал, все в единицах 0.1 (В или А).
    //
    // Три набора значений в кадр не помещаются, а смотреть их одновременно и
    // незачем: наладчик работает с одним. Что именно здесь лежит, говорит
    // teleSel - он же уходит в слове 13, чтобы UI подписывал поля правдой,
    // а не тем, что оператор нажал последним.
    //
    // Знаковые величины кладутся как int16_t: UI читает эти слова как short.
    switch (teleSel)
    {
    case TELE_KOD: // сырые коды АЦП, единицы кода
        tele[4] = (uint16_t)iPri_kod;
        tele[5] = (uint16_t)iSec_kod;
        tele[6] = u220_kod;
        tele[7] = (uint16_t)uSec_kod;
        break;

    case TELE_INS: // мгновенные физические значения
        tele[4] = (uint16_t)(int16_t)rounding(iPri_amp * 10.0f);
        tele[5] = (uint16_t)(int16_t)rounding(iSec_amp * 10.0f);
        tele[6] = (uint16_t)(int16_t)rounding(u220_volt * 10.0f);
        tele[7] = (uint16_t)(int16_t)rounding(uSec_volt * 10.0f);
        break;

    default: // TELE_RMS - действующие за период сети, считает CLA
        tele[4] = rounding(iPri_rms * 10.0f);
        tele[5] = rounding(iSec_rms * 10.0f);
        tele[6] = rounding(u220_rms * 10.0f);
        tele[7] = rounding(uSec_rms * 10.0f);
        break;
    }

    tele[8]  = rounding(uBat_volt * 10.0f); // напряжение АКБ, 0.1 В
    tele[9]  = core_temp;

    // Состояние автомата и код аварии - в одном слове: оба помещаются в байт,
    // а свободных слов в кадре нет.
    tele[10] = (uint16_t)(gt.State | (gt.fault << 8));
    tele[11] = rounding(gt.Iset * 100.0f); // что реально отдаём, 0.01 А
    tele[12] = rounding(sineB.gain);
    tele[13] = teleSel;
    tele[14] = motors[0]->flag.all;
    tele[15] = motors[1]->flag.all;


    Send_mbox_FD(2, tele, 16);
}







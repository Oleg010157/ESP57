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
#include "cla_shared.h"


extern STEND_bit sbit;
extern SHR_Flags shrTrip;


void send_REF_kadr()
{
    // Компактный кадр ID 140: 16 слов = 32 байта.
    uint16_t tele[16] = {0};


    tele[0] = motors[0]->error.all;
    tele[1] = motors[1]->error.all;
    tele[2] = sbit.all;
    tele[3] = shrTrip.all;
    shrTrip.all = 0xFFFF;

    tele[4] = (iPri_kod * 10);
    tele[5] = rounding(iPri_amp * 10.0f);
    tele[6] = rounding(iPri_rms * 10.0f);
    tele[7] = core_temp;

    tele[8]  = (iSec_kod * 10);
    tele[9]  = rounding(iSec_amp * 10.0f);
    tele[10] = rounding(iSec_rms * 10.0f);
    tele[11] = (u220_kod * 10);

    tele[12] = rounding(u220amp * 10);
    tele[13] = (uint16_t)(motors[1]->ipark.gain * 1000.0f + 0.5f);
    tele[14] = motors[0]->flag.all;
    tele[15] = motors[1]->flag.all;


    Send_mbox_FD(2, tele, 16);
}







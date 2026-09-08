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
#include "cla_shared.h"


extern STEND_bit sbit;
extern SHR_Flags shrTrip;



//#pragma CODE_SECTION(send_REF_kadr, ".text:board")

void send_REF_kadr()
{
    // Компактный кадр ID 140: 16 слов = 32 байта.
    uint16_t tele[16] = {0};

#if (ref_kadr_mask & BIT5) == BIT5
    tele[0] = motors[0]->error.all;
    tele[1] = motors[1]->error.all;
    tele[2] = sbit.all;
    tele[3] = shrTrip.all;
    shrTrip.all = 0xFFFF;
#endif

#if (ref_kadr_mask & BIT6) == BIT6
    tele[4] = rounding(cla.Ia_rf * 100.0f);
    tele[5] = rounding(cla.Ib_rf * 100.0f);
    tele[6] = rounding(Udc_rf * 10.0f);
    tele[7] = core_temp;
#endif

#if (ref_kadr_mask & BIT8) == BIT8
    tele[8]  = rounding(motors[0]->ramp.ref * 0.02f);
    tele[9]  = rounding(motors[0]->freq * 5.0f);
    tele[10] = rounding(motors[1]->ramp.ref * 0.02f);
    tele[11] = rounding(motors[1]->freq * 5.0f);
#endif

#if (ref_kadr_mask & BIT10) == BIT10
    tele[12] = (uint16_t)(motors[0]->ipark.gain * 1000.0f + 0.5f);
    tele[13] = (uint16_t)(motors[1]->ipark.gain * 1000.0f + 0.5f);
    tele[14] = motors[0]->flag.all;
    tele[15] = motors[1]->flag.all;
#endif

    Send_mbox_FD(2, tele, 16);
}







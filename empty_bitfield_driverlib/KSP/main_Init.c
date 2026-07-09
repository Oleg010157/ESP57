/*
 * main_Init.c
 *
 *  Created on: 7 окт. 2024 г.
 *      Author: Oleg Pavlovsky
 */

#include "f28p55x_device.h"
#include "Motor.h"
#include "PARAMS_ModeSwitch.h"
#include "PARAMS_INOUT.h"


extern REFs REF;

void REF_init(REFs *v)
{
    v->speedRef = 0;
    v->voltMax = 1250;
    v->voltMin = 1150;
    v->UdcMax = 1300;


    v->korUDC = 1.0;

    v->kIqRPM = 0.5;
}


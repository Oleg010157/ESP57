/*
 * pwm_funcs.c
 *
 *  Created on: 19 ���. 2015 �.
 *      Author: Andrejs.Gasilovs
 *  Modified on 15/09/2025
 *      Author: Oleg Pavlovsky
 */
#include "f28p55x_device.h"
#include "Motor.h"
#include "PARAMS_INOUT.h"
#include "myEPWM.h"

/* Эта функция отключает пины от функции EPWM и переводит их в пины вывода общего
 * назначения, после чего выставляет на них уровень, закрывающий транзисторы.
 *
 * Какой это уровень - задаёт IGBT_ACTIVE_HIGH в my_Files/myEPWM.h, ЕДИНСТВЕННОЕ
 * место, где полярность драйверов записана. Раньше здесь было жёстко GPASET
 * ("1 - выключает IGBT"), то есть вариант active-low. Это досталось от старого
 * проекта и противоречит остальной настройке (мёртвое время EPWM и полярность
 * модулятора отвечают active-high). Ошибка в эту сторону не безобидна: при
 * active-high драйверах прежний код не закрывал бы ключи, а ОТКРЫВАЛ их - и
 * именно эта функция вызывается по аварии из grid_tie_background().
 */
void IGBT2_Disable(void)
{
    DINT;
    EALLOW;

    // EPWM2 ===========================================================
    GpioCtrlRegs.GPAMUX1.bit.GPIO2 = 0; // 0=GPIO,  1=EPWM2A,
    GpioCtrlRegs.GPADIR.bit.GPIO2  = 1; // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO2);

    GpioCtrlRegs.GPBMUX1.bit.GPIO40 = 0; // 0=GPIO,  1=EPWM2B,
    GpioCtrlRegs.GPBDIR.bit.GPIO40  = 1; // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPB, GPIO40);

    // EPWM3  ================================================================
    GpioCtrlRegs.GPAMUX1.bit.GPIO14 = 0; // 0=GPIO,  1=EPWM3A,
    GpioCtrlRegs.GPADIR.bit.GPIO14  = 1; // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO14);

    GpioCtrlRegs.GPAMUX1.bit.GPIO15 = 0; // 0=GPIO,  1=EPWM3B,
    GpioCtrlRegs.GPADIR.bit.GPIO15  = 1; // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO15);

    // EPWM4 =================================================================
    GpioCtrlRegs.GPAMUX2.bit.GPIO22 = 0; // 0=GPIO,  2=EPWM4A,
    GpioCtrlRegs.GPADIR.bit.GPIO22  = 1;
    IGBT_OFF_SET(GPA, GPIO22);

    GpioCtrlRegs.GPAMUX1.bit.GPIO7 = 0; // 0=GPIO,  1=EPWM4B,
    GpioCtrlRegs.GPADIR.bit.GPIO7  = 1;
    IGBT_OFF_SET(GPA, GPIO7);

    EDIS;

    motor2.flag.bit.pwm_On = 0;
    EINT;
}

void IGBT1_Disable(void)
{

    DINT;
    EALLOW;

    // EPWM1 ===========================================================
    GpioCtrlRegs.GPAMUX1.bit.GPIO0 = 0;  // 0=GPIO,  1=EPWM1A
    GpioCtrlRegs.GPADIR.bit.GPIO0  = 1;  // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO0);

    GpioCtrlRegs.GPAMUX1.bit.GPIO1 = 0;  // 0=GPIO,  1=EPWM1B
    GpioCtrlRegs.GPADIR.bit.GPIO1  = 1;  // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO1);

    // EPWM5 ===========================================================
    GpioCtrlRegs.GPAMUX2.bit.GPIO16 = 0; // 0=GPIO,  1=EPWM5A
    GpioCtrlRegs.GPADIR.bit.GPIO16  = 1; // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO16);

    GpioCtrlRegs.GPAMUX2.bit.GPIO17 = 0; // 0=GPIO,  1=EPWM5B
    GpioCtrlRegs.GPADIR.bit.GPIO17  = 1; // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO17);

    EDIS;

    motor1.flag.bit.pwm_On = 0;
    EINT;
}
//===========================================

void PWM2_On(void)
{

    if (motor2.mbit.bit.AIN_Disable == 1)
    {
        motor2.flag.bit.pwm_On = 0;
        return;
    }
    DINT;

    EALLOW;
    GpioCtrlRegs.GPAMUX1.bit.GPIO2  = 1;  // 0=GPIO,  1=EPWM2A,
    GpioCtrlRegs.GPBMUX1.bit.GPIO40 = 1; // 0=GPIO,  1=EPWM2B,
    GpioCtrlRegs.GPAMUX1.bit.GPIO14 = 1; // 0=GPIO,  1=EPWM3A,
    GpioCtrlRegs.GPAMUX1.bit.GPIO15 = 1; // 0=GPIO,  1=EPWM3B,
    GpioCtrlRegs.GPAMUX2.bit.GPIO22 = 2;  // 0=GPIO,  2=EPWM4A,
    GpioCtrlRegs.GPAMUX1.bit.GPIO7  = 1; // 0=GPIO,  1=EPWM4B,

    EDIS;

    motor2.flag.bit.pwm_On = 1;
    EINT;
    //------------------------------
}

void PWM1_On(void)
{
    if (motor1.mbit.bit.AIN_Disable == 1)
    {
        motor1.flag.bit.pwm_On = 0;
        return;
    }
    DINT;

    EALLOW;
    GpioCtrlRegs.GPAMUX1.bit.GPIO0  = 1; // EPWM1A
    GpioCtrlRegs.GPAMUX1.bit.GPIO1  = 1; // EPWM1B
    GpioCtrlRegs.GPAMUX2.bit.GPIO16 = 1; // EPWM5A
    GpioCtrlRegs.GPAMUX2.bit.GPIO17 = 1; // EPWM5B
    EDIS;

    motor1.flag.bit.pwm_On = 1;
    EINT;
    //--------------------------------
}

//======================================================================

void IGBT1_ManualApply(uint16_t mask)
{
    igbt.all = mask;

    if ((mask & (1U << 6)) != 0U)
        GpioDataRegs.GPACLEAR.bit.GPIO0 = 1;
    else
        GpioDataRegs.GPASET.bit.GPIO0 = 1;

    if ((mask & (1U << 7)) != 0U)
        GpioDataRegs.GPACLEAR.bit.GPIO1 = 1;
    else
        GpioDataRegs.GPASET.bit.GPIO1 = 1;

    if ((mask & (1U << 8)) != 0U)
        GpioDataRegs.GPACLEAR.bit.GPIO16 = 1;
    else
        GpioDataRegs.GPASET.bit.GPIO16 = 1;

    if ((mask & (1U << 9)) != 0U)
        GpioDataRegs.GPACLEAR.bit.GPIO17 = 1;
    else
        GpioDataRegs.GPASET.bit.GPIO17 = 1;
}

void IGBT1_ManualEnable(void)
{
    IGBT1_Disable();
    igbt_manually |= (1U << 0);

    IGBT1_ManualApply(0U);
}

void IGBT1_ManualDisable(void)
{
    igbt_manually &= (uint16_t)~(1U << 0);
    IGBT1_Disable();
    PWM1_On();
}

void IGBT2_ManualApply(uint16_t mask)
{
    igbt.all = mask;

    if ((mask & (1U << 0)) != 0U)
        GpioDataRegs.GPACLEAR.bit.GPIO2 = 1;
    else
        GpioDataRegs.GPASET.bit.GPIO2 = 1;

    if ((mask & (1U << 1)) != 0U)
        GpioDataRegs.GPBCLEAR.bit.GPIO40 = 1;
    else
        GpioDataRegs.GPBSET.bit.GPIO40 = 1;

    if ((mask & (1U << 2)) != 0U)
        GpioDataRegs.GPACLEAR.bit.GPIO14 = 1;
    else
        GpioDataRegs.GPASET.bit.GPIO14 = 1;

    if ((mask & (1U << 3)) != 0U)
        GpioDataRegs.GPACLEAR.bit.GPIO15 = 1;
    else
        GpioDataRegs.GPASET.bit.GPIO15 = 1;

    if ((mask & (1U << 4)) != 0U)
        GpioDataRegs.GPACLEAR.bit.GPIO22 = 1;
    else
        GpioDataRegs.GPASET.bit.GPIO22 = 1;

    if ((mask & (1U << 5)) != 0U)
        GpioDataRegs.GPACLEAR.bit.GPIO7 = 1;
    else
        GpioDataRegs.GPASET.bit.GPIO7 = 1;
}

void IGBT2_ManualEnable(void)
{
    IGBT2_Disable();
    igbt_manually |= (1U << 1);

    IGBT2_ManualApply(0U);
}

void IGBT2_ManualDisable(void)
{
    igbt_manually &= (uint16_t)~(1U << 1);
    IGBT2_Disable();
    PWM2_On();
}

/*
 * Functions.c
 *
 *  Created on: 17 Apr 2020
 *      Author: Oleg Pavlovsky
 *      В файле собранны вспомогательные функции проекта для того чтобы разгрузить основные файлы
 */
//--------------------------------------------------------------
#include <system_constants.h>
#include "f28p55x_device.h"
#include "config_switch.h"
#include "Motor.h"
#include "Konstanty.h"

#include "driverlib.h"

int satFlag = 0;   // глобальный признак насыщения значения параметра
extern float Tpwm;  // период ШИМ в мкс
extern uint16_t timeBasePeriod; //
//--------------------------------------------------------------------------------------------------------------------
uint16_t Deadband = 128 ;  // dead Time = 1us if CLKDIV = 0

void change_Tpwm()
{
/* функция вычисляет и загружает в регистры EPWM период ШИМ для двух двигателей
 * по fpwm -заданному значению частоты ШИМ в герцах
 */

    Tpwm = (((Uint32) timeBasePeriod * 2) << EPwm2Regs.TBCTL.bit.CLKDIV) / (float) (SYSTEM_FREQUENCY);
    motors[0]->rg.k_AF = motors[0]->typeID.pole_pair / 60.0f * PI2 * Tpwm / 1000000.0f; // радиан за миллисекунду
}

void write_pwmCLKDIV(uint16_t div)
{

    EALLOW;
    SysCtl_disablePeripheral(SYSCTL_PERIPH_CLK_TBCLKSYNC);
    EPwm2Regs.TBCTL.bit.CLKDIV = div;
    EPwm3Regs.TBCTL.bit.CLKDIV = div;
    EPwm4Regs.TBCTL.bit.CLKDIV = div;

//    if(div >3)div =3;
    EPwm2Regs.DBFED.all =  (Deadband >> div);
    EPwm2Regs.DBRED.all = (Deadband >> div);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_TBCLKSYNC);
    EDIS;


}

/****************************************************************************************************
 *           Функция ограничения значений любой переменной во float                                    *
 ****************************************************************************************************/

float saturfloat(float input, float high, float low)
{
    float out;

    satFlag = 0;
    out = input;
    if (input > high)
    {
        out = high;
        satFlag = 1;
    }
    else if (input < low)
    {
        out = low;
        satFlag = 1;
    }
    return out;
}

long saturlong(long input, long high, long low)
{
    if (input > high)
        input = high;
    else if (input < low)
        input = low;
    return input;
}

uint16_t saturU16(uint16_t input, uint16_t high, uint16_t low)
{
    if (input > high)
        input = high;
    else if (input < low)
        input = low;
    return input;
}

/****************************************************************************************************
 *           Функция ограничения значений любой переменной во float                                    *
 ****************************************************************************************************/

float saturation(float input, float high, float low)
{
    float out;

    satFlag = 0;
    out = input;
    if (input > high)
    {
        out = high;
        satFlag = 1;
    }
    else if (input < low)
    {
        out = low;
        satFlag = 1;
    }
    return out;
}

//-------------------------------------------------------------------
float asut(float a)
{ // угол в диапазон +пи - пи радиан
    if (a > PI)
        a = a - PI2;
    if (a < -PI)
        a = a + PI2;
    return a;
}

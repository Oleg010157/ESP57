/*
 * Functions.h
 *
 *  Created on: 17 Apr 2020
 *      Author: Oleg Pavlovsky
 */

#ifndef FUNCTIONS_H_
#define FUNCTIONS_H_

#include "Konstanty.h"

#define mSecond(msec) (Uint32)(150000*msec) //  CPU Timers 

extern uint16_t Deadband;
extern int satFlag;  // флаг насыщения

void change_Tpwm();
void write_pwmCLKDIV(uint16_t div);
float asut(float a);

// float -> int с округлением
//#define  rounding(number) ((number >= 0)?(int)(number + 0.5):(int)(number - 0.5))
static inline int rounding(float number)
{
    int tmp =
    (number >= 0)? (number + 0.5f):(number - 0.5f);
    return(tmp);
}

struct wrd
{
    uint16_t Low;
    uint16_t Hi;
};
union word32
{
    Uint32 all;
    float   f32;
    struct wrd u16;
};


/****************************************************************************************************
 *           ������� ����������� �������� ����� ���������� �� float                                    *
 ****************************************************************************************************/

static inline float satfloatinline(float input, float high, float low)
{
    float out;

//    satFlag = 0;
    out = input;
    if (input > high)
    {
        out = high;
 //       satFlag = 1;
    }
    else if (input < low)
    {
        out = low;
//        satFlag = 1;
    }
    return out;
}

static inline long saturlong(long input, long high, long low)
{
    if (input > high)
        input = high;
    else if (input < low)
        input = low;
    return input;
}

static inline uint16_t saturU16(uint16_t input, uint16_t high, uint16_t low)
{
    if (input > high)
        input = high;
    else if (input < low)
        input = low;
    return input;
}

static inline float PI2sut(float a)
{ // угол в диапазон 0 -> 2 пи радиан
    if (a > PI2)
        a = a - PI2;
    
    return a;
}

float saturfloat(float input, float high, float low);

#endif /* FUNCTIONS_H_ */

/*
 * myEPWM.h
 *
 *  Created on: 15.09 2025
 *      Author: Olegs Pavlovskis
 */

#pragma once

typedef union
{
    uint16_t all;
    struct
    {
        // Invertor IGBT 
        uint16_t t1 : 1;
        uint16_t t2 : 1;
        uint16_t t3 : 1;
        uint16_t t4 : 1;
        uint16_t t5 : 1;
        uint16_t t6 : 1;
        // DC-DC Convertor IGBT 
        uint16_t vt1 : 1;
        uint16_t vt2 : 1;
        uint16_t vt3 : 1;
        uint16_t vt4 : 1;
    } bit;
} IGBT;

void TRIP_Clear(void);
void CMPSS_BackgroundCheck(void);
void IGBT1_Disable();
void IGBT2_Disable();
void PWM1_On(void);
void PWM2_On(void);
void IGBT1_ManualEnable(void);
void IGBT1_ManualDisable(void);
void IGBT1_ManualApply(uint16_t mask);
void IGBT2_ManualEnable(void);
void IGBT2_ManualDisable(void);
void IGBT2_ManualApply(uint16_t mask);

extern IGBT     igbt;
extern uint16_t igbt_manually; // логическая переменная для включения прямого управления IGBT
extern volatile uint16_t epwmcount;// dbg счётчик для измерения периода прерываний
extern volatile uint16_t epwmtime;// dbg период прерываний

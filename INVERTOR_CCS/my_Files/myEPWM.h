/*
 * myEPWM.h
 *
 *  Created on: 15.09 2025
 *      Author: Olegs Pavlovskis
 */

#pragma once

//---------------------------------------------------------------------------
// ПОЛЯРНОСТЬ ВХОДОВ ДРАЙВЕРОВ КЛЮЧЕЙ
//
// Плата в разводке, решение отложено. Меняется ЗДЕСЬ и только здесь - от этого
// параметра зависят и выключение ключей в pwm_funcs.c, и аварийное действие
// trip-zone в TRIP_Init().
//
//   1 = active-high: HIGH на пине ОТКРЫВАЕТ ключ, аварийное состояние - LOW
//   0 = active-low : HIGH на пине ЗАКРЫВАЕТ ключ, аварийное состояние - HIGH
//
// По умолчанию 1, потому что этому варианту отвечает всё остальное в проекте:
//   - мёртвое время EPWM настроено с полярностью по умолчанию (комплементарная
//     active-high). При active-low драйверах оно даст ПЕРЕКРЫТИЕ вместо паузы,
//     то есть сквозной ток;
//   - модулятор: больше duty -> дольше HIGH -> больше выходное напряжение.
//
// Комментарии в pwm_funcs.c ("1 - выключает IGBT") достались от старого проекта
// и отвечают обратному варианту. Если окажется, что верны они, поставьте здесь
// 0 И отдельно смените полярность мёртвого времени в SysConfig - одного этого
// параметра для active-low недостаточно.
//---------------------------------------------------------------------------
#define IGBT_ACTIVE_HIGH 1

#if IGBT_ACTIVE_HIGH
#define IGBT_OFF_SET(port, pin)   (GpioDataRegs.port##CLEAR.bit.pin = 1)
#else
#define IGBT_OFF_SET(port, pin)   (GpioDataRegs.port##SET.bit.pin = 1)
#endif

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
void TRIP_Init(void); // раскладка X-BAR и trip-zone: токовая защита на TRIP5
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

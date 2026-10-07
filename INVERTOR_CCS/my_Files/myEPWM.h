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

//---------------------------------------------------------------------------
// Ключи H-моста для теста IGBT (Motor_Control: Тест -> Тест IGBT, команда 338).
// Верхний ключ плеча - выход A модуля, нижний - выход B: комплементарная пара
// с мёртвым временем строится от A, B - его инверсия.
//
//   плечо A (EPWM1):  VT1 = EPWM1A, GPIO0     VT2 = EPWM1B, GPIO1
//   плечо B (EPWM2):  VT3 = EPWM2A, GPIO2     VT4 = EPWM2B, GPIO3
//
// Ток через первичку дают диагонали: VT1+VT4 - одним знаком, VT3+VT2 - другим.
// VT1+VT2 или VT3+VT4 - сквозной ток, прошивка такую маску не исполняет.
//---------------------------------------------------------------------------
typedef union
{
    uint16_t all;
    struct
    {
        uint16_t vt1 : 1; // 0 плечо A, верх
        uint16_t vt2 : 1; // 1 плечо A, низ
        uint16_t vt3 : 1; // 2 плечо B, верх
        uint16_t vt4 : 1; // 3 плечо B, низ
    } bit;
} IGBT;

#define BRIDGE_KEYS   0x000FU // все четыре ключа
#define BRIDGE_LEG_A  0x0003U // VT1 | VT2
#define BRIDGE_LEG_B  0x000CU // VT3 | VT4

void TRIP_Clear(void);
void TRIP_Init(void); // раскладка X-BAR и trip-zone: токовая защита на TRIP5
void IGBT1_Disable();
void IGBT2_Disable();
void PWM1_On(void);
void PWM2_On(void);
uint16_t Bridge_ManualEnable(void);  // 1 - вошли в тест, 0 - мост работает, отказ
void     Bridge_ManualDisable(void);
uint16_t Bridge_ManualApply(uint16_t mask); // 1 - исполнено, 0 - сквозная маска, всё закрыто
void     Bridge_ManualTrip(void);    // из прерывания trip-zone: закрыть ключи теста
uint16_t Bridge_ManualState(void);   // для ответа в Motor_Control, см. MBOX28.c case 338

extern IGBT     igbt;          // ключи, открытые тестом сейчас
extern uint16_t igbt_manually; // 1 - мост под ручным управлением (тест IGBT)
extern uint16_t igbt_tripped;  // 1 - тест прерван токовой защитой, до следующего входа
extern volatile uint16_t epwmcount;// dbg счётчик для измерения периода прерываний
extern volatile uint16_t epwmtime;// dbg период прерываний

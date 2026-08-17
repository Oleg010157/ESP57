/*
 * 1ph_Sin.h
 *
 *  Created on: 28.02.2025
 *      Author: Oleg Pavlovsky
 */

#ifndef SIN1_H_
#define SIN1_H_

// DBG_SIN_DAC - см. config_switch.h. При 1 нижний ЦАП CMPSS1 (COMPDACL) выводит
// синусоиду на пин A1, B7, D11 для контроля осциллографом; CMPSS1 при этом занят
// целиком, защита по току фазы А не работает (см. myADC.c).

extern float duty;           // Значение скважности ШИМ (в тактах таймера)
extern uint16_t sineTick;   // счётчик PWM прерываний
extern uint16_t tim0S, sine_Time; // dbg переменные для контроля времени выполнения программы
extern uint16_t sine_freq; // Выходная частота синусоидального сигнала (от 40 до 1000 Гц)
extern float sine_amp; // амплитуда напряжения синусоидального сигнала Uref/Vin ( от 0 до 1) 
extern uint16_t pwm_period; //  период ШИМ в мкс
extern  float sine_angle; // текущий угол синусоиды в радианнах:  y=sin(x)
extern  float sine_step;// шаг приращения угла в радианах ( зависит от частоты синуса и частоты ШИМ)
extern uint16_t spwmHz; //заданная частота  ШИМ в Гц , глобальный параметр. Изменяется через команды CAN
extern uint16_t sine_metod; // определяет номер метода формирования синусоиды
extern uint16_t cmpaA,cmpaB;
extern uint16_t index; // номер от 0 до количества точек периодов ШИМ синусоиды
extern uint16_t sine_resolution; // количество точек за период синусоиды
extern float sine0; // регулируемое смещение фазы синусоиды

void spwmCLKDIV(uint16_t div);
void adjustPhaze(void);
#if DBG_SIN_DAC
void dacb_CMPSS1_init(void); // dbg  ЦАП CMPSS1 (COMPDACL) -> пин A1, B7, D11, CMP1_DACL
#endif

#if Strob_rg  // применяется для отладки метода формирования синусоиды
extern int strobIndex; // строб номера  ШИМ периода в последовательности синусоиды
#endif

struct sine
{
    uint16_t resolution; // количество точек за период синусоиды
    uint16_t freq;
    float step;
    uint16_t pwm_period;
    float tpwm_sek;  // pwm period  в секундах
    float   angle;
    float offset;
    float gain;
    uint16_t amplitude;
};

// sine_invertor1 удалён 17.08.2026: каждая полуволна формировалась своим
// плечом, из-за чего любая асимметрия плеч превращалась в постоянную
// составляющую и намагничивала трансформатор. Разбор - my_Files/grid_tie.txt.
void sine_invertor2( struct sine *s );
void change_sine(struct sine *s);

extern struct sine sineA,sineB;

#endif /* 1ph_Sin.h */

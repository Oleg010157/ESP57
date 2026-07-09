#ifndef CLA_SHARED_H
#define CLA_SHARED_H

#include <stdint.h>
#include "myADC.h"
#include "cla_task1.h"

// Входные данные для CLA: токи, напряжения,
typedef struct
{
    int16_t Ia_kod, Ib_kod, Ic_kod; // АЦП код измеренныех  токов фаз с учётом смещения, код
    float   Ia_amp, Ib_amp, Ic_amp; // измеренные токи фаз, Ампер

    float Va_volt, Vb_volt, Vc_volt; // вычисленные фазные напряжения, Вольт
    float Um_max;                    // амплитуда фазного напряжения инвертора

} ClaToCpu;

// RMS-блок (отдельный набор для расчётов RMS)
typedef struct {
    float Ia, Ib, Ic; // измеренные токи фаз, Ампер
    float Va, Vb, Vc;     // вычисленные фазные напряжения, Вольт
    float Udc;            // напряжение на входе инвертора, Вольт
    // uint16_t flag;     // флаг завершения выборки (если понадобится)
} CLA_RMS;

// Совместно используемые данные CPU ↔ CLA для агрегированных расчётов
typedef struct {
    uint16_t count; // количество точек в выборке
    uint16_t flag;  // флаг завершения выборки
   
    uint16_t error;

    // CLA → CPU: сглаженные значения токов, Ампер
    float Ia_rf, Ib_rf, Ic_rf;

    // RMS значения токов, Ампер
    float Ia_rms, Ib_rms, Ic_rms;

    // RMS напряжение и Udc
    float Va_rms;
    float Udc_rms;

    // Мгновенные и агрегированные мощности
    float Pa, Pb, Pc;  // фазные мгновенные мощности, Ватт
    float Pabc;        // мгновенная мощность инвертора, Ватт
    float Pabc_rf;     // сглаженная активная мощность на выходе инвертора

    // Диагностические/модельные метрики
    float mPabc;
    float mPaz;
    float mPx;
    float mSabc;
    float mPF;
    float cosPhi;
    float Pain;        // входная мощность

    // Коэффициенты АЦП/масштабирования
   
} Cla7Shared;

// Сумматоры/накопители для расчётов (например, для RMS/сглаживания)
typedef struct {
    float Ia_sum;
    float Ib_sum;
    float Ic_sum;
   
    float Iaaf;
    float Ibbf;
    float Iccf;

    float Va_sum;
    float Udc_sum;

    float sPu;
    float sPabc;
    float Pain_sum;
    float spaz;
} Clasum;

extern volatile kADCf kadcf;
// Глобально разделяемые объекты (определяются в cla_shared.c)
extern volatile Clasum    cs;
extern volatile ClaToCpu c2p;
extern volatile Cla7Shared cla;
extern volatile CLA_RMS   rms;

void CLA_shared_Init(void);

#include "Task1_alias.h"

#endif

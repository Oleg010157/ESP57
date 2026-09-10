#ifndef CLA_SHARED_H
#define CLA_SHARED_H

#include <stdint.h>
#include "myADC.h"

//---------------------------------------------------------------------------
// Измеренные величины всех шести каналов АЦП.
//
// Определения - в cla_shared.c, там же по каждому каналу расписано, кто его
// пишет, в каком прерывании и почему именно там. Порядок каналов тот же, что
// в ADCpar и kADCf (myADC.h).
//
// Часть величин считает CPU, часть - CLA (Cla1Task1). Объявлены вместе
// намеренно: это полный список того, что прибор меряет, и искать его по
// файлам не нужно.
//
// Все лежат в одной секции cla_shared (RAMLS2, CPU/CLA shared data memory),
// то есть любой канал доступен обеим сторонам независимо от того, кто его
// пишет. А volatile стоит ровно у тех, кого пишет CLA: их значение меняется
// асинхронно к CPU. У остальных его нет намеренно - это горячий код контура
// тока, лишние обращения к памяти там не нужны.
//---------------------------------------------------------------------------

// Параметры, которые CPU задаёт, а CLA потребляет. Живут в CpuToCla1MsgRAM -
// направление там аппаратное.
extern volatile kADCf    kadcf;     // масштабы каналов, код -> физическая величина
extern volatile uint16_t rmsWindow; // окно RMS в отсчётах = копия sineB.resolution

// Диагностика Cla1Task1: длительность задачи в тактах EPWM7 (1/150 мкс).
// Живёт в Cla1ToCpuMsgRAM - блоке для направления CLA -> CPU (CLA читает и
// пишет, CPU только читает). На такте закрытия окна RMS заметно больше
// обычного: там четыре деления и четыре корня.
extern volatile uint16_t task1Start;
extern volatile uint16_t task1Time;

// Действующие значения (все четыре *_rms) считает CLA, Cla1Task1: там
// запаздывание безразлично, а работа заметная - четыре умножения с накоплением
// каждый такт и четыре корня раз за период сети.

// 1. iPri - ток первичной обмотки. B1, ADCB SOC0/PPB1, масштаб kadcf.iPri.
//    Мгновенные пишет CPU в прерывании ADCB (EOC1), 1ph_Sin.c.
extern int16_t        iPri_kod; // коды АЦП
extern float          iPri_amp; // А
extern volatile float iPri_rms; // А rms за период сети

// 2. iSec - ток вторичной обмотки, ток в сеть. B4, ADCB SOC1/PPB2, kadcf.iSec.
//    Мгновенные пишет CPU там же.
extern int16_t        iSec_kod; // коды АЦП
extern float          iSec_amp; // А
extern volatile float iSec_rms; // А rms за период сети

// 3. iBat - ток АКБ. A12, ADCA SOC2/PPB3, масштаб kadcf.iBat.
//    Пишет CLA. До появления Cla1Task1 канал не читал никто.
extern volatile int16_t iBat_kod; // коды АЦП, знаковые (за вычетом нуля канала)
extern volatile float   iBat_amp; // А
extern volatile float   iBat_rf;  // А, сглаженный

// 4. u220 - напряжение сети после контактора. A0, ADCA SOC0/PPB1.
//    Пишет CPU в прерывании ADCA (EOC0), myADC.c. Отсчёт сырой, не через PPB -
//    ноль вычитает grid_tie_step() через gt.u220Zero. Вольты считает gt.kU220,
//    масштаба в kadc для этого канала нет.
//    u220_volt - не путать с gt.uGrid: величина та же, но ноль там берётся
//    раз за период и только в gridMode. Подробно - в myADC.c.
extern uint16_t       u220_kod;  // сырой код, 0..4095
extern float          u220amp;   // амплитуда сети в кодах (полураствор вилки)
extern float          u220_volt; // В, мгновенное; ноль - середина вилки
extern volatile float u220_rms;  // В rms за период сети

// 5. uBat - напряжение АКБ, оно же напряжение звена на входе моста.
//    A4, ADCA SOC1/PPB2, масштаб kadcf.uBat. Пишет CLA.
//    Тот же канал мгновенным читает grid_tie_step() в gt.Udc.
extern volatile int16_t uBat_kod;  // коды АЦП, знаковые
extern volatile float   uBat_volt; // В
extern volatile float   uBat_rf;   // В, сглаженный

// 6. uSec - напряжение на выходе инвертора, до контактора.
//    A1, ADCA SOC3/PPB4, масштаб kadcf.uSec. Пишет CLA.
//    Пока DBG_SIN_DAC = 1, канал меряет собственный ЦАП (CMPSS1 COMPDACL),
//    а не датчик.
extern volatile int16_t uSec_kod;  // коды АЦП, знаковые
extern volatile float   uSec_volt; // В
extern volatile float   uSec_rms;  // В rms за период сети

void CLA_shared_Init(void);

#endif

/*
 * ADC.h
 *
 *  Created on: 2 may. 2024.
 *      Author: olegs.pavlovskis
 */

#ifndef ADC_H_
#define ADC_H_

#include "f28p55x_device.h"

// Канал сети U220 с вилкой огибающей. Не прерывание: зовётся первой строкой
// такта управления, INT_myADCB_1_ISR в my_Files/1ph_Sin.c. Отдельное
// прерывание ADCA INT1 под это снято 23.09.2026, разбор - у определения.
void u220_sample(void);

// Три канала ADCA, готовые только к концу такта: ток и напряжение АКБ,
// напряжение на выходе инвертора. Зовётся в конце INT_myADCB_1_ISR, под
// проверкой флага ADCA INT2 и только при выключенной модели. Разбор - у
// определения в myADC.c.
void adc_late_sample(void);

void Core_Temp_Update(void);

void myADC_PPB_Init(void);
void Adc_Calibrate(void);
void Reset_stend_filtr(void);
void SaveAdcOffsets(void);
void LoadOffsetsToPPB();
void Offsets_Apply(void); // применить нули, изменённые вручную (команды 207..211)

//float KadcSum();

struct AdcEEPROM //биты запросов чтения или записи калибровок АЦП в EEPROM.
{
    uint16_t SaveOffsets : 1;     // бит запрос на запись офФсетов ацп в EEPROM.
    uint16_t ReadOffsets : 1;     // бит запрос на чтение офФсетов ацп из EEPROM.
    uint16_t SaveCoef : 1;        // бит запрос на запись офФсетов ацп в EEPROM.
    uint16_t ReadCoef : 1;        // бит запрос на чтение офФсетов ацп из EEPROM.
    uint16_t Read_dbgOffsets : 1; // бит запрос на чтение офФсетов ацп из EEPROM.
    uint16_t Read_dbgCoef : 1;    // бит запрос на чтение офФсетов ацп из EEPROM.
    uint16_t SaveErrors : 1;      // бит запрос на запись массива кодов ошибок в EEPROM.
    uint16_t ReadErrors : 1;      // бит запрос на чтение массива кодов ошибок из EEPROM.
    uint16_t Read_dbgErrors : 1;  // бит запрос на чтение массива кодов ошибок из EEPROM.
    uint16_t Recall : 1;          // бит запрос на копирование nvRAM to SRAM
    uint16_t Store : 1;           // бит запрос на копирование SRAM to nvRAM
};

typedef union
{
    uint16_t         all;
    struct AdcEEPROM bit;
} EEcontrol;

extern EEcontrol eeprom;

extern uint16_t ical_counter;

// Заявки на калибровку нуля, по одному биту на канал.
//
// Бит ставится двумя путями: командой 70 сразу маской (меню "Откалибровать все
// датчики" шлёт ADC_CAL_ALL) или галочкой "М" у строки канала в Offsets.ini -
// тогда калибруется только этот канал.
//
// Раньше три канала тока стояли под одним битом iAll. Разделены, чтобы галочка
// у строки означала ровно свой канал и ничего больше.
struct AdcStartCal
{
    uint16_t iPri : 1; // 1   ток первичной обмотки (B1)
    uint16_t iSec : 1; // 2   ток вторичной обмотки (B4)
    uint16_t iBat : 1; // 4   ток АКБ (A12)
    uint16_t u220 : 1; // 8   напряжение сети (A0)
    uint16_t uBat : 1; // 16  напряжение АКБ (A4)
    uint16_t uSec : 1; // 32  напряжение на выходе инвертора (A1)
};

#define ADC_CAL_ALL 63U // все шесть каналов

typedef union
{
    uint16_t           all;
    struct AdcStartCal bit;
} AdcCal;

extern AdcCal adcCalStart;

// Имена полей - по точке измерения:
//   iPri - ток первичной обмотки  (B1, ADCB SOC0/PPB1)
//   iSec - ток вторичной обмотки  (B4, ADCB SOC1/PPB2)
//   iBat - ток АКБ                (A12, ADCA SOC2/PPB3)
//   u220 - напряжение сети после контактора (A0, ADCA SOC0/PPB1)
//   uBat - напряжение АКБ         (A4, ADCA SOC1/PPB2)
//   uSec - напряжение на выходе инвертора, до контактора (A1, ADCA SOC3/PPB4)
//
// ПОРЯДОК ПОЛЕЙ МЕНЯТЬ НЕЛЬЗЯ: структура целиком ложится в EEPROM с CRC по
// всей длине (см. SPI_EEPROM.c) и читается с адреса первого поля. Новые поля
// добавлять только в конец, перед crc - тогда старый EEPROM не сойдётся по
// контрольной сумме и подставит дефолты, что для нетарированного канала верно.
typedef struct
{
    uint16_t iPri;
    uint16_t iSec;
    uint16_t iBat;
    uint16_t u220;
    uint16_t uBat;
    uint16_t uSec; // добавлено 10.09.2026, см. правило выше - в конец, перед crc
    uint16_t crc;

} ADCpar;

typedef struct
{
    float iPri;
    float iSec;
    float iBat;
    float u220;
    float uBat;
    float uSec;

} kADCf;

// Измеренные величины всех шести каналов (u220_kod, u220amp, iPri_*, iSec_*,
// iBat_*, uBat_*, uSec_*) объявлены в CLA/cla_shared.h - одним списком.

extern ADCpar offsets, offsets_default;

extern ADCpar         kadc, kadc_default;
extern volatile kADCf kadcf;

extern ADCpar  dbgkadc;
extern int16_t core_temp; // температура ядра F28P55x, ГРАДУСЫ ЦЕЛЬСИЯ (не сотые)

extern uint16_t ControlSum(ADCpar *c);
extern void     floatKadc();
void            ADC_SendOffsets(void);
void            currentLimit(uint16_t Imax);
float           currentLimitPriActual(void); // фактический порог первички, А

#endif /* ADC_H_ */

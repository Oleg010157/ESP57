/*
 * ADC.h
 *
 *  Created on: 2 may. 2024.
 *      Author: olegs.pavlovskis
 */

#ifndef ADC_H_
#define ADC_H_

#include "f28p55x_device.h"

__interrupt void INT_myADCA_1_ISR(void);
__interrupt void INT_myADCC_1_ISR(void);

void myADC_PPB_Init(void);
void Adc_Calibrate(void);
void Reset_stend_filtr(void);
void SaveAdcOffsets(void);
void LoadOffsetsToPPB();

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

struct AdcStartCal
{
    uint16_t Iabc : 1; // бит запрос на калибровку офФсетов датчиков тока.
    uint16_t Udc : 1;  // бит запрос на калибровку офФсетов датчика напряжения Udc.
    uint16_t Vin : 1;  // бит запрос на калибровку офФсетов датчика входного напряжения Vin.
};

typedef union
{
    uint16_t           all;
    struct AdcStartCal bit;
} AdcCal;

extern AdcCal adcCalStart;

typedef struct
{
    uint16_t Ia;
    uint16_t Ib;
    uint16_t Idc;
    uint16_t Udc;
    uint16_t Vin;
    uint16_t crc;

} ADCpar;

typedef struct
{
    float Ia;
    float Ib;
    float Idc;
    float Udc;
    float Vin;
   
} kADCf;

extern ADCpar offsets, offsets_default;

extern ADCpar kadc, kadc_default;
extern volatile kADCf kadcf;

extern ADCpar dbgkadc;
extern int16_t core_temp; // температура ядра F280039C в Цельсиях

extern uint16_t  ControlSum(ADCpar *c);
extern void floatKadc();
void        ADC_SendOffsets(void);
void        currentLimit(uint16_t Imax);

#endif /* ADC_H_ */

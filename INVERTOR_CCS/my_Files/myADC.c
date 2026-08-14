/*
 * ADC.c
 *
 *  Created on: 2.06.2024 
 *      Author: olegs.pavlovskis
 */
#include "board.h"
#include "math.h"
#include "f28p55x_device.h"
#include "f28p55x_pie_defines.h"

#include "KSP.h"

#include "myADC.h"
#include "1ph_Sin.h" // DBG_SIN_DAC
#include "cla_shared.h"
#include "SPI_EEPROM.h"


AdcCal    adcCalStart; //биты запросов на запуск калибровок каналов АЦП
EEcontrol eeprom;      //биты запросов на сохранение калибровок АЦП в EEPROM
ADCpar    offsets = {2048, 2048,  2048, 200, 200, 10};

ADCpar    offsets_default = {2048, 2048, 2048, 110,110, 0}; //заводские настройки нулей каналов АЦП
ADCpar    kadc            = {90, 90, 90, 100, 100, 7};        //
ADCpar    kadc_default    = {90, 90, 90, 100, 100, 0};        //заводские настройки коэффициентов каналов АЦП



void MainISR(void);

//-------------------------------------------------------------------------------------------------------------


__interrupt void INT_myADCB_1_ISR(void)
{
    
}

//-------------------------------------------------------------------------------------------------------------

#define CORE_TEMP_SOC_NUM ADC_SOC_NUMBER0
#define CORE_TEMP_VREF 3.3f

#if CONTROL_CARD
#define ADC_REFERENCE ADC_REFERENCE_INTERNAL
#else 
#define ADC_REFERENCE ADC_REFERENCE_EXTERNAL
#endif

int16_t core_temp; // температура ядра F28P55x в Цельсиях
int16_t core_temp2; // температура ядра F28P55x в Цельсиях

// вызывается из фона раз в 50 мс (main.c, блок CpuTimer0) - SOC0 запускается
// от EPWM2 непрерывно, здесь просто читаем последний готовый результат.
void Core_Temp_Update(void)
{
    uint16_t core_temp_kod = ADC_readResult(ADCCRESULT_BASE, CORE_TEMP_SOC_NUM);
    core_temp = ADC_getTemperatureC(core_temp_kod, ADC_REFERENCE, CORE_TEMP_VREF);
}

void Core_Temp_Update2(void)
{
    uint16_t core_temp_kod = ADC_readResult(ADCCRESULT_BASE, CORE_TEMP_SOC_NUM);
    float32_t temp = (float32_t)core_temp_kod * (CORE_TEMP_VREF / 2.5F);
    
    int32_t slope  = (int32_t)ADC_INT_REF_TSSLOPE;   
    int32_t offset = (int32_t)ADC_INT_REF_TSOFFSET;  
    
    // 1. Считаем числитель в 32-битном формате
    int32_t numerator = ((int32_t)temp - offset);
    
    
    // Теперь значение искусственно увеличилось, и деление на slope не занулится!
    int32_t temp_scaled = (numerator * 4096) / slope; 
    
    // Результат будет, например, 2550 вместо 25.5 градусов.
    // Для вывода на экран или в лог просто делим на 100 там, где это нужно.
    core_temp2 = temp_scaled; 
}

//--------------------------------------------------------------------------------
// метод калибровки смещения нуля каналов АЦП
// вызывается по команде CAN посредством установки битов в структуре adcCalStart
// метод в цикле из 256

uint16_t ical_counter = 0;
void     Adc_Calibrate(void)
{

    if (ical_counter == 0)
    {
        if (adcCalStart.bit.Iabc)
        {
            AdcbRegs.ADCPPB1OFFREF = AdcbResultRegs.ADCRESULT0; // Ia_kod
            AdcbRegs.ADCPPB2OFFREF = AdcbResultRegs.ADCRESULT1; // Ib_kod
            AdcaRegs.ADCPPB3OFFREF = AdcaResultRegs.ADCRESULT2; // Idc_kod
        }
        if (adcCalStart.bit.Udc)
            AdcaRegs.ADCPPB1OFFREF = AdcaResultRegs.ADCRESULT0; // cla.Udc_kod

        if (adcCalStart.bit.Vin)
            AdcaRegs.ADCPPB2OFFREF = AdcaResultRegs.ADCRESULT1; // Vin_kod
    }
    if (ical_counter < 256)
    {
        if (adcCalStart.bit.Iabc)
        {
            if (Ia_kod > 0)
                AdcbRegs.ADCPPB1OFFREF++;
            else if (Ia_kod < 0)
                AdcbRegs.ADCPPB1OFFREF--;

            if (Ib_kod > 0)
                AdcbRegs.ADCPPB2OFFREF++;
            else if (Ib_kod < 0)
                AdcbRegs.ADCPPB2OFFREF--;

            if (Idc_kod > 0)
                AdcaRegs.ADCPPB3OFFREF++;
            else if (Idc_kod < 0)
                AdcaRegs.ADCPPB3OFFREF--;
        }
        if (adcCalStart.bit.Udc)
        {
            if (Udc_kod > 0)
                AdcaRegs.ADCPPB1OFFREF++;
            else if (Udc_kod < 0)
                AdcaRegs.ADCPPB1OFFREF--;
        }
        if (adcCalStart.bit.Vin)
        {
            if (Vin_kod > 0)
                AdcaRegs.ADCPPB2OFFREF++;
            else if (Vin_kod < 0)
                AdcaRegs.ADCPPB2OFFREF--;
        }

        ical_counter++;
    }
    else
    {
        Reset_stend_filtr();
        SaveAdcOffsets();
        adcCalStart.all        = 0;
        ical_counter           = 0; 
    }
}

void SaveAdcOffsets(void) //
{
    offsets.Udc            = AdcaRegs.ADCPPB1OFFREF;
    offsets.Vin            = AdcaRegs.ADCPPB2OFFREF;
    offsets.Idc            = AdcaRegs.ADCPPB3OFFREF;

    offsets.Ia             = AdcbRegs.ADCPPB1OFFREF;
    offsets.Ib             = AdcbRegs.ADCPPB2OFFREF;

    
    offsets.crc            = ControlSum(&offsets);

   EEprom_write_words(SPIB_BASE, WRITE0, offsets_addr, &offsets.Ia,
                       (uint16_t)(sizeof(offsets) / sizeof(uint16_t)));
}

void LoadOffsetsToPPB()
{
    AdcaRegs.ADCPPB1OFFREF = offsets.Udc;
    AdcaRegs.ADCPPB2OFFREF = offsets.Vin;
    AdcaRegs.ADCPPB3OFFREF = offsets.Idc;

    AdcbRegs.ADCPPB1OFFREF = offsets.Ia;
    AdcbRegs.ADCPPB2OFFREF = offsets.Ib;
    
}

uint16_t ControlSum(ADCpar *c)
{
    uint16_t sum = c->Ia + c->Ib + c->Idc +  c->Udc + c->Vin;
    return sum;
}

void floatKadc() //
{
   
    kadcf.Ia            = kadc.Ia / 10000.0f;
    kadcf.Ib            = kadc.Ib / 10000.0f;
    kadcf.Idc           = kadc.Idc / 10000.0f;
    kadcf.Udc           = kadc.Udc / 10000.0f;
    kadcf.Vin           = kadc.Vin / 10000.0f;
    kadc.crc            = kadc.Ia + kadc.Ib + kadc.Idc + kadc.Udc + kadc.Vin;
    eeprom.bit.SaveCoef = 1; // сохранить коэффициенты в SRAM EEPROM
}

void ADC_SendOffsets(void)
{
    extern void SendAck(int D1, int D2, int D3,  int Cmd);

    Uint16 *P  = (Uint16 *)&offsets;
    Uint16 *Pa = (Uint16 *)&offsets;
    Uint16  i  = 0;
    SendAck(0, 0, 0, 32015);
    for (i = 0; i < sizeof(offsets); i++)
        SendAck(*(Pa + i), *(P + i), i, 32016);

    SendAck(0, 0, 32017, 32017);
}

//---------------------------------------------------------------------

void currentLimit(uint16_t Imax) // определяет уровни срабатывания защиты по току для компараторов
{
#if !DBG_SIN_DAC // при DBG_SIN_DAC модуль CMPSS1 занят отладочным ЦАП синусоиды
    Cmpss1Regs.DACHVALS.bit.DACVAL = saturU16(Imax / kadcf.Ia + offsets.Ia, 4000, 2048);
    Cmpss1Regs.DACLVALS.bit.DACVAL = saturU16(offsets.Ia - (Imax / kadcf.Ia), 2048, 96);

    // При DBG_SIN_DAC верхний компаратор CMPSS3 работает детектором перехода U220
    // через ноль (CTRIPH -> eCAP1). Ему нужен порог на середине шкалы (2048), который
    // ставит myCMPSS3_init(). Уставка защиты по току уводит порог к 4000, а daca при
    // gain = 1800 доходит лишь до 3848 - компаратор перестаёт срабатывать совсем,
    // захваты прекращаются и синхронизация с сетью молча умирает.
    Cmpss3Regs.DACHVALS.bit.DACVAL = saturU16(Imax / kadcf.Ib + offsets.Ia, 4000, 2048);
    Cmpss3Regs.DACLVALS.bit.DACVAL = saturU16(offsets.Ib - (Imax / kadcf.Ia), 2048, 96);
#endif
}



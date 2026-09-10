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
#include "calib_store.h"
#include "1ph_Sin.h" // DBG_SIN_DAC
#include "grid_tie.h" // gt.n - коэффициент трансформации для порога первички
#include "cla_shared.h"
#include "SPI_EEPROM.h"


AdcCal    adcCalStart; //биты запросов на запуск калибровок каналов АЦП
EEcontrol eeprom;      //биты запросов на сохранение калибровок АЦП в EEPROM
//                          iPri, iSec, iBat, u220, uBat, crc
ADCpar    offsets = {2048, 2048,  2048, 200, 200, 10};

ADCpar    offsets_default = {2048, 2048, 2048, 110,110, 0}; //заводские настройки нулей каналов АЦП
ADCpar    kadc            = {90, 90, 90, 100, 100, 7};        //
ADCpar    kadc_default    = {90, 90, 90, 100, 100, 0};        //заводские настройки коэффициентов каналов АЦП



void MainISR(void);

//-------------------------------------------------------------------------------------------------------------
extern uint16_t daca, dacb;
uint16_t u220hi_kod = 2500; // u220hi_default;
uint16_t u220low_kod = 400; // u220low_default;
uint16_t u220_kod;
float u220amp; // амплитуда напряжения U220

// Вилка вокруг синусоиды опоры: u220hi_kod цепляется за верхушку, u220low_kod за низ.
// Раздвигается мгновенно - любой отсчёт выше (ниже) вилки сразу становится границей.
// Сжимается медленно - раз в u220spadDiv прерываний АЦП на 1 код с каждой стороны.
// Прерывание АЦП идёт 100 раз за период сети, значит при 8 вилка сжимается на 12 кодов
// за период. u220amp = половина раствора вилки.
uint16_t u220spadDiv = 8; // чем больше, тем медленнее вилка сжимается
uint16_t u220spadCnt = 0;

#pragma CODE_SECTION(INT_myADCA_1_ISR, ".TI.ramfunc");

__interrupt void INT_myADCA_1_ISR(void)
{
    // Калибровка нуля каналов, если её запросили командой CAN 70. Здесь, а не
    // в фоне: подстройка усредняет по 256 отсчётам и должна идти на такте
    // преобразования. В обычной работе стоит один сравнение - биты сброшены.
    //
    // Прежде вызывалась из INT_myCLA7_ISR, но тот вектор не регистрируется:
    // модуля CLA в активном c2000.syscfg нет, и автокалибровка не работала.
    if (adcCalStart.all != 0)
    {
        Adc_Calibrate();
    }

     u220_kod = (int16_t)AdcaResultRegs.ADCRESULT0;

  //  stend.Vin = AdccResultRegs.ADCPPB3RESULT.all;

    //  U220_kod для проекта K55   Кузьменкова
    // раздвигаем вилку - сразу на весь отсчёт. Если двигать по +1, вилка не успевает
    // за верхушкой синусоиды и садится ниже пика: при u220spadDiv = 8 недобор был
    // 145 кодов, из-за него gain вставал на 1655 вместо 1800.
    if (u220_kod > u220hi_kod)
    {
        u220hi_kod = u220_kod;
    }
    if (u220_kod < u220low_kod)
    {
        u220low_kod = u220_kod;
    }

    // сжимаем вилку - раз в u220spadDiv прерываний
    if (++u220spadCnt >= u220spadDiv)
    {
        u220spadCnt = 0;
        // Границы не должны пересечься: при пропаже опоры u220low_kod ушёл бы выше
        // u220hi_kod, а разность беззнаковых свернулась бы в ~65000 - u220amp стала бы
        // огромной и gain уехал бы в потолок.
        if (u220hi_kod > u220low_kod)
        {
            u220hi_kod--;
            u220low_kod++;
        }
    }

    u220amp = (u220hi_kod - u220low_kod) / 2.0f;
    // В острове gain задаёт Uref. Слежение за огибающей U220 — только в GridMode
    // (подстройка к сети перед замыканием контактора).
    if (gridMode)
    {
        if (u220amp > sineB.gain)
            sineB.gain++;
        else
            sineB.gain--;
    }

    AdcaRegs.ADCINTFLGCLR.bit.ADCINT1 = 1;             // clear INT1 flag for ADCC
    PieCtrlRegs.PIEACK.all            = PIEACK_GROUP1; // освобождаем для других прерываний
}  

//-------------------------------------------------------------------------------------------------------------

//#define CORE_TEMP_SOC_NUM ADC_SOC_NUMBER0
//#define CORE_TEMP_VREF 3.3f

#if CONTROL_CARD
#define ADC_REFERENCE ADC_REFERENCE_INTERNAL
#else 
#define ADC_REFERENCE ADC_REFERENCE_EXTERNAL
#endif

int16_t core_temp; // температура ядра F28P55x в Цельсиях
int16_t core_temp2; // температура ядра F28P55x в Цельсиях

// вызывается из фона раз в 50 мс (main.c, блок CpuTimer0) - SOC0 запускается
// от EPWM1 непрерывно, здесь просто читаем последний готовый результат.
void Core_Temp_Update(void)
{
    uint16_t core_temp_kod = ADC_readResult(ADCCRESULT_BASE, ADC_SOC_NUMBER0);
    core_temp = ADC_getTemperatureC(core_temp_kod, ADC_REFERENCE, 3.3f);
}

void Core_Temp_Update2(void)
{
    uint16_t core_temp_kod = ADC_readResult(ADCCRESULT_BASE, ADC_SOC_NUMBER0);
    float32_t temp = (float32_t)core_temp_kod * (3.3f / 2.5F);
    
    int32_t slope  = (int32_t)ADC_INT_REF_TSSLOPE;   
    int32_t offset = (int32_t)ADC_INT_REF_TSOFFSET;  
    
    // 1. Считаем числитель в 32-битном формате
    int32_t numerator = ((int32_t)temp - offset);
    
    
    // Теперь значение искусственно увеличилось, и деление на slope не занулится!
    int32_t temp_scaled = (numerator * 4096.0f) / (float)slope; 
    
    // Результат будет, например, 2550 вместо 25.5 градусов.
    // Для вывода на экран или в лог просто делим на 100 там, где это нужно.
    core_temp = temp_scaled; 
}

//--------------------------------------------------------------------------------
// Калибровка нуля каналов АЦП.
//
// Запускается командой CAN 70, которая выставляет биты в adcCalStart. Вызывается
// из INT_myADCA_1_ISR, то есть на каждом преобразовании: усреднение идёт по 256
// отсчётам, и из фонового цикла этого не сделать.
//
// Как работает. Регистр ADCPPBxOFFREF вычитается из результата канала, поэтому
// подстраивая его, мы добиваемся нуля на выходе PPB при отсутствии сигнала.
// Сначала грубо - в OFFREF кладётся текущий отсчёт целиком. Дальше 256 тактов
// подстройки по одному коду в сторону, обратную знаку остатка: так усредняется
// шум, и результат не зависит от того, каким оказался один конкретный отсчёт.
//
// ВХОД КАНАЛА ДОЛЖЕН БЫТЬ ОБЕСТОЧЕН. Функция считает нулём то, что видит.
//
// Раньше подстройка сравнивала с полями c2p.Ia_kod, dc.Udc_kod и подобными -
// их заполняли задачи CLA. Задачи не запускаются (модуля CLA нет в активном
// c2000.syscfg), поля всегда оставались нулями, и ни одна ветвь подстройки не
// срабатывала. Теперь знаковый остаток берётся прямо из регистров результата
// PPB - это ровно те значения, которые считала бы CLA.
//--------------------------------------------------------------------------------

// Знаковый результат PPB: то, что осталось от канала после вычитания OFFREF.
#define PPB_RESULT(regs, n) ((int16_t)(regs).ADCPPB##n##RESULT.bit.PPBRESULT)

uint16_t ical_counter = 0;
void     Adc_Calibrate(void)
{
    if (ical_counter == 0)
    {
        // Грубая загрузка: сырой отсчёт канала целиком идёт в OFFREF.
        if (adcCalStart.bit.iAll)
        {
            AdcbRegs.ADCPPB1OFFREF = AdcbResultRegs.ADCRESULT0; // iPri
            AdcbRegs.ADCPPB2OFFREF = AdcbResultRegs.ADCRESULT1; // iSec
            AdcaRegs.ADCPPB3OFFREF = AdcaResultRegs.ADCRESULT2; // iBat
        }
        if (adcCalStart.bit.u220)
            AdcaRegs.ADCPPB1OFFREF = AdcaResultRegs.ADCRESULT0; // u220

        if (adcCalStart.bit.uBat)
            AdcaRegs.ADCPPB2OFFREF = AdcaResultRegs.ADCRESULT1; // uBat
    }
    if (ical_counter < 256)
    {
        // Точная подстройка: шаг в сторону, обратную знаку остатка.
        if (adcCalStart.bit.iAll)
        {
            int16_t iPri = PPB_RESULT(AdcbResultRegs, 1);
            int16_t iSec = PPB_RESULT(AdcbResultRegs, 2);
            int16_t iBat = PPB_RESULT(AdcaResultRegs, 3);

            if (iPri > 0)
                AdcbRegs.ADCPPB1OFFREF++;
            else if (iPri < 0)
                AdcbRegs.ADCPPB1OFFREF--;

            if (iSec > 0)
                AdcbRegs.ADCPPB2OFFREF++;
            else if (iSec < 0)
                AdcbRegs.ADCPPB2OFFREF--;

            if (iBat > 0)
                AdcaRegs.ADCPPB3OFFREF++;
            else if (iBat < 0)
                AdcaRegs.ADCPPB3OFFREF--;
        }
        if (adcCalStart.bit.u220)
        {
            int16_t u220 = PPB_RESULT(AdcaResultRegs, 1);

            if (u220 > 0)
                AdcaRegs.ADCPPB1OFFREF++;
            else if (u220 < 0)
                AdcaRegs.ADCPPB1OFFREF--;
        }
        if (adcCalStart.bit.uBat)
        {
            int16_t uBat = PPB_RESULT(AdcaResultRegs, 2);

            if (uBat > 0)
                AdcaRegs.ADCPPB2OFFREF++;
            else if (uBat < 0)
                AdcaRegs.ADCPPB2OFFREF--;
        }

        ical_counter++;
    }
    else
    {
        // Готово: перенести OFFREF в offsets, посчитать сумму (это и есть
        // признак "калибровка проводилась") и запросить сохранение.
        Reset_stend_filtr();
        SaveAdcOffsets();
        adcCalStart.all = 0;
        ical_counter    = 0;
    }
}

void SaveAdcOffsets(void) //
{
    offsets.u220           = AdcaRegs.ADCPPB1OFFREF;
    offsets.uBat           = AdcaRegs.ADCPPB2OFFREF;
    offsets.iBat           = AdcaRegs.ADCPPB3OFFREF;

    offsets.iPri           = AdcbRegs.ADCPPB1OFFREF;
    offsets.iSec           = AdcbRegs.ADCPPB2OFFREF;


    offsets.crc            = ControlSum(&offsets);

    calibSaveReq = 1; // и во флеш, см. calib_store.h

   // Запись с адреса первого поля структуры - оно же адрес самой структуры.
   EEprom_write_words(SPIB_BASE, WRITE0, offsets_addr, &offsets.iPri,
                       (uint16_t)(sizeof(offsets) / sizeof(uint16_t)));
}

void LoadOffsetsToPPB()
{
    AdcaRegs.ADCPPB1OFFREF = offsets.u220;
    AdcaRegs.ADCPPB2OFFREF = offsets.uBat;
    AdcaRegs.ADCPPB3OFFREF = offsets.iBat;

    AdcbRegs.ADCPPB1OFFREF = offsets.iPri;
    AdcbRegs.ADCPPB2OFFREF = offsets.iSec;

}

uint16_t ControlSum(ADCpar *c)
{
    uint16_t sum = c->iPri + c->iSec + c->iBat +  c->u220 + c->uBat;
    return sum;
}

//---------------------------------------------------------------------------
// Пересчёт целых масштабов kadc в вещественные kadcf. Больше ничего.
//
// Раньше здесь стояли ещё две строки, и обе были неверны:
//
//   kadc.crc = kadc.iPri + ... + kadc.uBat;
//       Нулевая сумма - это ПРИЗНАК того, что каналы после заводских значений
//       не калибровались, на нём построен разбор в checkEE(): crc == 0 даёт
//       Kadc_NOCAL, а несовпадение суммы - другую ошибку. floatKadc()
//       вызывается при каждом старте из checkEE(), поэтому заводские значения
//       тут же получали "правильную" сумму и прибор навсегда начинал выглядеть
//       калиброванным, ни разу им не побывав. Сумму считает тот, кто
//       действительно калибрует: команды CAN 198..203 для масштабов и
//       SaveAdcOffsets() для нулей.
//
//   eeprom.bit.SaveCoef = 1;
//       Запрос на сохранение при каждом включении, без единого изменения.
//       Те же команды 198..203 ставят этот бит сами, когда есть что сохранять.
//---------------------------------------------------------------------------
void floatKadc() //
{
    kadcf.iPri = kadc.iPri / 10000.0f;
    kadcf.iSec = kadc.iSec / 10000.0f;
    kadcf.iBat = kadc.iBat / 10000.0f;
    kadcf.u220 = kadc.u220 / 10000.0f;
    kadcf.uBat = kadc.uBat / 10000.0f;
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

// Запас на ток намагничивания для порога первички. Ток намагничивания течёт
// ТОЛЬКО в первичке и в отражённый ток нагрузки не входит, поэтому порог
// первички - это не просто n * порог вторички.
float currentLimitMagK = 1.3f;

/* Уровни срабатывания аппаратной токовой защиты.
 *
 * Раскладка компараторов (подробно - my_Files/grid_tie.txt, раздел 10):
 *   CMPSS1 - вывод dacb через DACL. Компаратор не используется, НЕ ТРОГАТЬ.
 *   CMPSS2 - защита Ia, ток первички
 *   CMPSS3 - детектор нуля U220 -> eCAP1. Порог 2048. НЕ ТРОГАТЬ.
 *   CMPSS4 - защита Ib, ток вторички (ток в сеть)
 *
 * Прежняя редакция писала в Cmpss1 и Cmpss3 - раскладка досталась от старого
 * проекта. Cmpss3 в K55 это детектор нуля сети: одна команда CAN 53 переставляла
 * ему порог с 2048 на токовую уставку, компаратор переставал срабатывать,
 * захваты eCAP прекращались, и синхронизация с сетью молча умирала.
 *
 * Imax - предел по току ВТОРИЧКИ (тот, что уходит в сеть), А. Именно его
 * ограничивает команда CAN 53, и именно он имеет физический смысл уставки.
 */
void currentLimit(uint16_t Imax)
{
    // До вызова floatKadc() масштабы нулевые. Делить на них нельзя: получится
    // бесконечность, а её преобразование в целое не определено.
    if ((kadcf.iPri <= 0.0f) || (kadcf.iSec <= 0.0f))
    {
        return;
    }

    float ImaxSec = (float)Imax;
    float ImaxPri = ImaxSec * gt.n * currentLimitMagK;

    long dPri = (long)(ImaxPri / kadcf.iPri); // отклонение порога от нуля, коды
    long dSec = (long)(ImaxSec / kadcf.iSec);

    // Считаем в знаковом типе: при большой уставке offsets - d уходит ниже нуля,
    // а в uint16_t это свернулось бы в ~65000 и ограничитель принял бы его
    // за превышение сверху.
    Cmpss2Regs.DACHVALS.bit.DACVAL = (uint16_t)saturlong((long)offsets.iPri + dPri, 4000, 2048);
    Cmpss2Regs.DACLVALS.bit.DACVAL = (uint16_t)saturlong((long)offsets.iPri - dPri, 2048, 96);

    Cmpss4Regs.DACHVALS.bit.DACVAL = (uint16_t)saturlong((long)offsets.iSec + dSec, 4000, 2048);
    Cmpss4Regs.DACLVALS.bit.DACVAL = (uint16_t)saturlong((long)offsets.iSec - dSec, 2048, 96);
}



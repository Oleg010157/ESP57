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
//                    iPri, iSec, iBat, u220, uBat, uSec, crc
ADCpar    offsets = {2048, 2048,  2048, 200, 200, 200, 10};

ADCpar    offsets_default = {2048, 2048, 2048, 110, 110, 110, 0}; //заводские настройки нулей каналов АЦП
ADCpar    kadc            = {90, 90, 90, 100, 100, 100, 7};        //
ADCpar    kadc_default    = {90, 90, 90, 100, 100, 100, 0};        //заводские настройки коэффициентов каналов АЦП



void MainISR(void);

//-------------------------------------------------------------------------------------------------------------
extern uint16_t daca, dacb;
uint16_t u220hi_kod = 2500; // u220hi_default;
uint16_t u220low_kod = 400; // u220low_default;
// u220_kod / u220amp переехали в CLA/cla_shared.c - туда, где объявлены все
// шесть каналов АЦП сразу. Пишет их по-прежнему этот файл, INT_myADCA_1_ISR.

// Напряжение на выходе инвертора (A1) читает теперь Cla1Task1, см. cla_shared.h.
// Здесь его брали в INT_myADCA_1_ISR, а та взведена на EOC0 - то есть значение
// снимали на такт раньше, чем SOC3 успевал преобразоваться. У CLA триггер стоит
// на EOC3, это и есть момент готовности канала.

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
    // Прежде вызывалась из INT_myCLA7_ISR, который не выполнялся вовсе, из-за
    // чего автокалибровка не работала. Теперь CLA поднята (Task1, триггер
    // ADCA INT2 по EOC3), но калибровку сюда ВОЗВРАЩАТЬ НЕ НАДО: она правит
    // ADCPPBxOFFREF, то есть аппаратные нули, и должна идти в одном месте.
    if (adcCalStart.all != 0)
    {
        Adc_Calibrate();
    }

     u220_kod = (int16_t)AdcaResultRegs.ADCRESULT0;

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

    // Мгновенное напряжение сети в вольтах.
    //
    // Ноль берётся как СЕРЕДИНА ВИЛКИ, а не из ADCPPB1OFFREF: в офсете PPB
    // лежит калибровочный ноль канала (~110), а не середина шкалы, и знаковый
    // результат PPB нёс бы постоянное смещение почти в 2000 кодов. Вилка же
    // обхватывает реальный сигнал сверху и снизу, так что её середина и есть
    // измеренный ноль.
    //
    // ЭТО НЕ ДУБЛИКАТ gt.uGrid, хотя величина та же. Разница в источнике нуля:
    // grid_tie_step() берёт gt.u220Zero, который обновляется раз за период сети
    // из grid_tie_onGridZero() и ТОЛЬКО в gridMode - контуру нужен ноль, не
    // дёргающийся от такта к такту. В острове gt.u220Zero так и остаётся 2048 и
    // за сигналом не следит. u220_volt считается каждый такт по живой вилке и
    // верен в обоих режимах, поэтому годится для телеметрии и RMS.
    u220_volt = gt.kU220 * ((float)u220_kod - ((float)u220hi_kod + (float)u220low_kod) * 0.5f);

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

int16_t core_temp; // температура ядра F28P55x в ГРАДУСАХ ЦЕЛЬСИЯ

//--------------------------------------------------------------------------------
// Температура ядра. Вызывается из фона раз в 50 мс (main.c, блок CpuTimer0):
// ADCC SOC0 запускается от EPWM1 непрерывно, здесь просто читаем последний
// готовый результат.
//
// Датчик температуры на F28P55x подключён к ADCC, канал C12 - это внутреннее
// соединение, а не внешний пин (см. C2000Ware, adc_ex2_epwm_tempsensor.c).
//
// Режим опоры обязан совпадать с тем, что реально настроено: сгенерированный
// board.c делает ADC_setVREF(myADCA_BASE, ADC_REFERENCE_INTERNAL,
// ADC_REFERENCE_3_3V), поэтому INTERNAL. От режима зависит, какая пара
// slope/offset берётся из OTP, и с чужой парой температура уезжает.
//
// Здесь было две функции, и обе с изъяном.
//
// Core_Temp_Update() звала драйверлиб с макросом ADC_REFERENCE, который
// выбирался через #if CONTROL_CARD. CONTROL_CARD не определён НИГДЕ - ни в
// .cproject (там только _FLASH), ни в заголовках, - поэтому вычислялся
// EXTERNAL, то есть чужая пара slope/offset. Саму функцию при этом не вызывали.
//
// Core_Temp_Update2() повторяла формулу драйверлиба вручную (и верно, ветвь
// INTERNAL), но комментарий утверждал, что результат в сотых долях градуса и
// его "просто делим на 100 там, где это нужно". Это неправда: множитель 4096
// не масштабирует, он часть самой формулы (ADC_getTemperatureC в adc.h),
// результат - целые градусы. Потребитель у core_temp один, REF_Kadr.c:37, и он
// ничего не делит, то есть работал правильно вопреки комментарию.
//
// Оставлена одна функция, через драйверлиб, с явно указанным INTERNAL.
// Численно она совпадает с прежней Core_Temp_Update2 - поведение не меняется.
//--------------------------------------------------------------------------------
void Core_Temp_Update(void)
{
    uint16_t core_temp_kod = ADC_readResult(ADCCRESULT_BASE, ADC_SOC_NUMBER0);
    core_temp = ADC_getTemperatureC(core_temp_kod, ADC_REFERENCE_INTERNAL, 3.3f);
}

//--------------------------------------------------------------------------------
// Калибровка нуля каналов АЦП.
//
// Запускается командой CAN 70, которая выставляет биты в adcCalStart. Вызывается
// из INT_myADCA_1_ISR, то есть на каждом преобразовании: усреднение идёт по 256
// отсчётам, и из фонового цикла этого не сделать.
//
// Как работает. Регистр ADCPPBxOFFREF вычитается из результата канала, поэтому
// достаточно положить в него среднее значение сигнала при обесточенном входе.
// Окно усреднения - целое число периодов синусоиды, см. ADC_CAL_PERIODS.
//
// Прежняя редакция вместо усреднения шагала по ±1 в сторону, обратную знаку
// остатка, и делала это 256 раз. Проблем было две.
//
// Первая: при шуме, перекрывающем сигнал, знак остатка случаен, и подстройка
// превращалась в случайное блуждание - за 256 шагов конечная точка расходилась
// примерно на √256 ≈ 16 кодов. Усреднение и детерминировано, и подавляет
// случайную составляющую в √N раз.
//
// Вторая, и более важная: 256 отсчётов при resolution = 100 - это 2.56 периода
// синусоиды. Опрос жёстко связан с ней (АЦП запускается от EPWM1_SOCA), поэтому
// незамкнутый хвост периода попадал в результат целиком, а его величина
// зависела от фазы начала калибровки. Именно это давало разброс, а не шум сам
// по себе: измеренные на плате ±50 кодов - в основном остаток синусоиды.
//
// ВХОД КАНАЛА ДОЛЖЕН БЫТЬ ОБЕСТОЧЕН. Функция считает нулём то, что видит.
//
// Раньше подстройка сравнивала с полями c2p.Ia_kod, dc.Udc_kod и подобными -
// их должны были заполнять задачи CLA, а те не запускались вовсе, так что поля
// всегда оставались нулями и ни одна ветвь подстройки не срабатывала. Теперь
// накопление идёт прямо с регистров ADCRESULT, и от CLA калибровка не зависит.
// Так и оставить: усреднение обязано читать СЫРЫЕ отсчёты, а всё, что отдаёт
// CLA, - уже за вычетом того самого OFFREF, который мы здесь и вычисляем.
//--------------------------------------------------------------------------------

// Сколько ПЕРИОДОВ синусоиды усредняем.
//
// Считать надо именно периодами, а не отсчётами. АЦП запускается от EPWM1_SOCA,
// а период EPWM1 подстраивается PLL так, чтобы на период сети приходилось ровно
// sineB.resolution отсчётов. То есть опрос ЖЁСТКО СВЯЗАН с синусоидой, и целое
// число периодов в окне усреднения гасит её вклад точно, до нуля.
//
// Прежние 256 отсчётов при resolution = 100 давали 2.56 периода. Лишние
// 0.56 периода не компенсировались, и в среднее попадал кусок синусоиды,
// величина которого зависела от фазы начала калибровки. Каждый запуск - другая
// фаза, другой результат; отсюда и брались "совершенно случайные" значения.
#define ADC_CAL_PERIODS 3U

// Запасное число отсчётов, если resolution окажется бессмысленным.
#define ADC_CAL_N_FALLBACK 256U

// Накопители сырых отсчётов, по одному на канал.
// 300 * 4095 = 1 228 500 - в 32 бита укладывается с большим запасом.
static int32_t calAcc[6];

// Длина окна усреднения в отсчётах, вычисляется в начале каждой калибровки.
static uint16_t calN = ADC_CAL_N_FALLBACK;

enum
{
    CAL_iPri = 0,
    CAL_iSec,
    CAL_iBat,
    CAL_u220,
    CAL_uBat,
    CAL_uSec,
    CAL_COUNT
};

uint16_t ical_counter = 0;
void     Adc_Calibrate(void)
{
    if (ical_counter == 0)
    {
        uint16_t res = sineB.resolution;

        // Окно усреднения - целое число периодов синусоиды. Если resolution
        // почему-то невменяем, берём запасное число: усреднение будет хуже, но
        // калибровка не зависнет и не поделит на ноль.
        calN = ((res >= 10U) && (res <= 1000U)) ? (uint16_t)(ADC_CAL_PERIODS * res)
                                                : ADC_CAL_N_FALLBACK;

        calAcc[CAL_iPri] = 0;
        calAcc[CAL_iSec] = 0;
        calAcc[CAL_iBat] = 0;
        calAcc[CAL_u220] = 0;
        calAcc[CAL_uBat] = 0;
        calAcc[CAL_uSec] = 0;
    }

    if (ical_counter < calN)
    {
        // Копим СЫРЫЕ отсчёты. Именно сырые: OFFREF - это и есть то, что мы
        // считаем, а ADCRESULT от его текущего значения не зависит. Поэтому
        // результат не зависит от того, с какого офсета начали, и грубая
        // предзагрузка не нужна.
        if (adcCalStart.bit.iPri)
            calAcc[CAL_iPri] += (int32_t)AdcbResultRegs.ADCRESULT0;
        if (adcCalStart.bit.iSec)
            calAcc[CAL_iSec] += (int32_t)AdcbResultRegs.ADCRESULT1;
        if (adcCalStart.bit.iBat)
            calAcc[CAL_iBat] += (int32_t)AdcaResultRegs.ADCRESULT2;
        if (adcCalStart.bit.u220)
            calAcc[CAL_u220] += (int32_t)AdcaResultRegs.ADCRESULT0;
        if (adcCalStart.bit.uBat)
            calAcc[CAL_uBat] += (int32_t)AdcaResultRegs.ADCRESULT1;
        if (adcCalStart.bit.uSec)
            calAcc[CAL_uSec] += (int32_t)AdcaResultRegs.ADCRESULT3;

        ical_counter++;
    }
    else
    {
        // Среднее в OFFREF. Деление, а не сдвиг: calN кратно периоду
        // синусоиды и степенью двойки не является. Считается один раз за
        // калибровку, так что цена не имеет значения.
        int32_t n = (int32_t)calN;

        if (adcCalStart.bit.iPri)
            AdcbRegs.ADCPPB1OFFREF = (uint16_t)(calAcc[CAL_iPri] / n);
        if (adcCalStart.bit.iSec)
            AdcbRegs.ADCPPB2OFFREF = (uint16_t)(calAcc[CAL_iSec] / n);
        if (adcCalStart.bit.iBat)
            AdcaRegs.ADCPPB3OFFREF = (uint16_t)(calAcc[CAL_iBat] / n);
        if (adcCalStart.bit.u220)
            AdcaRegs.ADCPPB1OFFREF = (uint16_t)(calAcc[CAL_u220] / n);
        if (adcCalStart.bit.uBat)
            AdcaRegs.ADCPPB2OFFREF = (uint16_t)(calAcc[CAL_uBat] / n);
        if (adcCalStart.bit.uSec)
            AdcaRegs.ADCPPB4OFFREF = (uint16_t)(calAcc[CAL_uSec] / n);

        // Перенести OFFREF в offsets, посчитать сумму (это и есть признак
        // "калибровка проводилась") и запросить сохранение.
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
    offsets.uSec           = AdcaRegs.ADCPPB4OFFREF;
    offsets.iBat           = AdcaRegs.ADCPPB3OFFREF;

    offsets.iPri           = AdcbRegs.ADCPPB1OFFREF;
    offsets.iSec           = AdcbRegs.ADCPPB2OFFREF;


    offsets.crc            = ControlSum(&offsets);

    calibSaveReq = 1; // и во флеш, см. calib_store.h

   // Запись с адреса первого поля структуры - оно же адрес самой структуры.
   EEprom_write_words(SPIB_BASE, WRITE0, offsets_addr, &offsets.iPri,
                       (uint16_t)(sizeof(offsets) / sizeof(uint16_t)));
}

//--------------------------------------------------------------------------------
// Применить изменённые вручную нули: пересчитать сумму, отдать значения железу
// и запросить сохранение.
//
// Вызывается из обработчиков команд CAN 207..211, когда оператор правит ноль
// канала из таблицы Offsets.ini. Автокалибровка сюда не заходит - у неё свой
// путь через SaveAdcOffsets().
//
// Порядок тот же, что при сбросе к заводским, и по тем же причинам:
//   crc            иначе checkEE() при следующем старте сочтёт нули
//                  испорченными и откатит их на заводские;
//   LoadOffsetsToPPB()  иначе АЦП продолжит вычитать прежнее смещение;
//   currentLimit()      пороги защиты отсчитываются от нуля канала;
//   calibSaveReq        иначе правка не переживёт выключение.
//--------------------------------------------------------------------------------
void Offsets_Apply(void)
{
    extern uint16_t Imax;

    offsets.crc = ControlSum(&offsets);

    LoadOffsetsToPPB();
    currentLimit(Imax);

    calibSaveReq = 1;
}

void LoadOffsetsToPPB()
{
    AdcaRegs.ADCPPB1OFFREF = offsets.u220;
    AdcaRegs.ADCPPB2OFFREF = offsets.uBat;
    AdcaRegs.ADCPPB4OFFREF = offsets.uSec;
    AdcaRegs.ADCPPB3OFFREF = offsets.iBat;

    AdcbRegs.ADCPPB1OFFREF = offsets.iPri;
    AdcbRegs.ADCPPB2OFFREF = offsets.iSec;

}

uint16_t ControlSum(ADCpar *c)
{
    uint16_t sum = c->iPri + c->iSec + c->iBat + c->u220 + c->uBat + c->uSec;
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
    kadcf.uSec = kadc.uSec / 10000.0f;
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



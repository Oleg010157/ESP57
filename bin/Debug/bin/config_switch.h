#ifndef _ConfigSwicth_
#define _ConfigSwicth_

#include "f28p55x_device.h"
#include "inc/stw_types.h"

#define KSP_ID_STRING "K1  11.07.2026  start F28P55"
#define FAST_FRAME_STRING "F3_ui.h" // Строка ini файла быстрого кадра - имя.h

#define TOOTH_FRAME_STRING "Tooth_1234.h" // Строка ini файла зуб кадра - имя.ini

#include FAST_FRAME_STRING


#define CONTROL_CARD 1 //0  модуль компиляции для работы на отладочной плате ControlCard


#define MCANA    1
#define MCANB    0


#define Ic_sensor 0   // есть/нет  датчик тока в фазе С



// ========  OPTION  ===================

#define CalcRMS 1 //   для вычисления RMS - токов и мощностей
#define MD2 0
#define OBS1 0

#define T_SHEMA 0 // расчёт параметров эквивалентной Т- схемы по входным напряжениям и токам
#define aci_fe 0  // расчёт положения вектора потокосцепления ротора
#define SMO 0     //14 наблюдатель положения маг.потока ротора по методу SMO


//  ====   специальные функции   ===================

#define Calibrovka 0 // Версия для калибровки каналов АЦП
#define L_testing 0  // Bерсия для измерения индуктивности

#define ADC_SEND 0     //  модуль отправляем данные ножек ADC
#define ADC_SOC_EPWM 0 //  модуль для запуска АЦП от ePWM (при 1)


#define CalcMotor1 0 // компилирует программу MotorCalc для управления мотором М1 (АИН1)
#define CalcMotor2 1 // компилирует программу MotorCalc для управления мотором М2 (АИН2)

// дополнительные КУКи для отладки
//------------------------------------------------------------------------------
#define myDEBUG 1 // =1 разрешает подключить куки отладочных режимов

#if myDEBUG || CONTROL_CARD
#define dbgUdc 0    //
#define dbgIfull 0  // иммитатор фазных токов Iu_full, ia = Im*sin(rg.Out+imit.phaza),
#define FreqGen26 0 // выводим на GPIO26 импульсы заданной частоты с выхода CAP3. Используется для отладки QEP1 и QEP2.
#define FreqGen49 \
    0 // выводим на GPIO26 импульсы заданной частоты с выхода CAP6. Используется для иммитации частотного сигнала крутящего момента муфты T40B.
#define dbgTMP 0 //  присоединяет отладочные фрагменты программы. (от Андрея 2)

#define dbgRPM 0        //  модуль отладки оборотов RPM
#define ADC2dbg 0       //  при установке выводятся измереные мгновенные значения параметров вместо RMS
#define RG2manual 0     //  позволяет вручную выставлять положение вектора напряжения
#define TestT 0         //  Обороты по одному зубу без учета типа двиг.
#define ADC_imitation 0 //  подменяет сигналы от АЦП
#define dbgSRD 0        // кук меняет перечень переменных по MBOX6

#else
#define dbgUdc 0    //
#define dbgIfull 0  // иммитатор фазных токов Ia_full = Im*sin(rg.Out+imit.phaza)  , ...
#define FreqGen26 0 // выводим на GPIO26 импульсы заданной частоты с выхода CAP3. Используется для отладки QEP1 и QEP2.
#define FreqGen49 \
    0 // выводим на GPIO26 импульсы заданной частоты с выхода CAP6. Используется для иммитации частотного сигнала крутящего момента муфты T40B.
#define dbgTMP 0 //  присоединяет отладочные фрагменты программы. (от Андрея 2)

#define dbgRPM 0  //  модуль отладки оборотов RPM
#define ADC2dbg 0 //  кук для отладки и тарировки АЦП
#define RG2manual 0
#define TestT 0         // Обороты по одному зубу без учета типа двиг на всех скоростных режимах.
#define ADC_imitation 0 //  Обороты по одному зубу без учета типа двиг.
#define dbgSRD 0        // меняет перечень переменных по MBOX6
#endif

#define dbgPIN33 0  // ножка GPIO33 для осциллографа
#define dbgPIN59 0  // ножка GPIO59 для осциллографа
#define dbgPIN63 0  // ножка GPIO63 для осциллографа
#define Strob_QEP 0 // синхро сигнал для запуска развёртки осциллографа по положению ротора
#define Strob_rg 1  // синхро сигнал для запуска развёртки осциллографа  по положению вектора напряжения.
                    //--------------------------------------------------------------------------------

struct kuk1
{
    uint16_t k1_CONTROL_CARD : 1; //0  модуль компиляции для работы на отладочной платы ControlCard
    uint16_t k1_RSVD : 1;         //1
 
};

typedef union
{
    struct kuk1  kuk;
    const uint16_t all;
} KUK1;

#define KUK1_INIT                          \
    {CONTROL_CARD, 1,         FRAM,        \
                                           \
     QEP1_Sensor,  QEP1_Mode, QEP2_Sensor, \
     QEP2_Mode,                            \
                                           \
     CAP1_Mode,    CAP2_Mode, 1}

//----------------------------------------------------------------------------------------------------------
struct kuk2
{

    uint16_t k2_BlueSkaf : 1;  //1 для бирюзового шкафа Месняева
    uint16_t k2_WhiteSkaf : 1; //2 для белого шкафа Месняева
    uint16_t k2_Orange : 1;    //3 оранжевый шкаф Слискиса

    uint16_t k2_rez4 : 1;    //4 режим векторного управления подключён
    uint16_t k2_Park_2i : 1; //5 вычисление преобразования Парка по 2-м токам

    // !!! блок взаимоисключающих куков
    uint16_t k2_TORMOZ : 1;  //6 Схема испытаний КТЭО на 350 кВт
    uint16_t k2_rezerv7 : 1; //7 СРД алгоритм с пи регулятором EPSV
    uint16_t k2_SRDV1 : 1;   //8 СРД алгоритм векторного управления
                           //-------------------------------------------------------------------

    uint16_t k2_AINSync : 1; //9 Режим парралельной работы АИНов

    uint16_t k2_BC_to_ResLoad : 1; //10 Фазы В-C на резисторную нагрузку, С выключен
    uint16_t k2_Calibrovka : 1;    //11 Версия для калибровки каналов АЦП
    uint16_t k2_ADC_SOC_EPWM : 1;  //12 запуск АЦП от ePWM (при 1)
};

typedef union KUK2
{
    struct kuk2 kuk;
    uint16_t      all;
} KUK2;

#define KUK2_INIT             \
    {                         \
                              \
        1, 1,         Orange, \
                              \
        0,                    \
                              \
        1, 0,         0,      \
                              \
        1,                    \
                              \
        1, Calibrovka}

//--------------------------------------------------------------------------------------------------------

struct kuk3
{

    uint16_t k3_dbgIfull : 1;  //0  иммитатор фазных токов Iu_full, ia = Im*sin(rg.Out+imit.phaza),
    uint16_t k3_RPMimit : 1;   //1  Имитируем датчик оборотов от формы иммитации
    uint16_t k3_dbgTMP : 1;    //2  присоединяет отладочные фрагменты программы. (от Андрея 2)
    uint16_t k3_dbgPIN : 1;    //3  модуль компиляции присоединяет пины для отладки программы
    uint16_t k3_dbgRPM : 1;    //4  модуль отладки оборотов RPM
    uint16_t k3_rz5 : 1;       //5  модуль включает функции косинуса и т.д. (от Андрея 2)
    uint16_t k3_ADC2dbg : 1;   //6  при установке выводятся измереные мгновенные значения параметров вместо RMS
    uint16_t k3_RG2manual : 1; //7  позволяет вручную выставлять положение вектора напряжения
    uint16_t k3_TestT : 1;     //8  Обороты по одному зубу без учета типа двиг.
    uint16_t k3_rez9 : 1;      //9  Режим управления тормознымы резисторами с PWM
    uint16_t k3_dbgSRD : 1;    //10 кук меняет перечень переменных по MBOX6
   };

typedef union
{
    struct kuk3 kuk;
    uint16_t      all;
} KUK3;

#define KUK3_INIT                                                                                  \
    {                                                                                              \
        dbgIfull, FreqGen26, dbgTMP, 0, dbgRPM, 0, ADC2dbg, RG2manual, TestT, 0, dbgSRD, test_srd, \
    }

#endif

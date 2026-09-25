/*
 * MBOX28.c
 *
 *  Created on: 7 июня 2016 г.
 *  modified 01.08.2026
 *      Author: olegs.pavlovskis
 */
/***************************************************************************************************
 *  Функция cmd_MBOX28 принимает  команды от PC  через FIFO0
 *  срабатывает при получении нового сообщения в FIFO0                      
 *  структура команды -> cmd_MBOX28( int16,int16,int16, int_cmd)        
 ***************************************************************************************************/
#include "board.h"
#include "f28p55x_device.h"

#include "config_switch.h"
#include "Motor.h"
#include "PARAMS_INOUT.h"
#include "PARAMS_ModeSwitch.h"
#include "myDCAN.h"
#include "FLAGZ.h"
#include "main.h"

#include "MBOX.h"
#include "myADC.h"
#include "calib_store.h"
#include "cla_shared.h"
#include FAST_FRAME_STRING

#include "Functions.h"
#include "Konstanty.h"
#include "myEPWM.h"
#include "SPI_EEPROM.h"
#include "1ph_Sin.h"
#include "grid_tie.h"
#include "plant_sim.h"
#include "power_par.h"

motor *motStrob = &motor1;

volatile struct EQEP_REGS *qepRegs = &EQep1Regs;

uint16_t     K60per = 60 * 2; //период вывода медленного кадра
union PCflag sharp;

#if Strob_QEP         //------------------------------
int qepStrob     = 0; //
int strob_sektor = 80;
#endif //---------------------------------

#if Strob_rg
int   rgStrob_enable = 0;
float rgStrob        = 0; // угол синусоиды в радианнах по которому выдаётся строб на scope
#endif

extern uint16_t KpA; // коэффициент пропорционального регулятора тока возбуждения DC
extern uint16_t KpC; // коэффициент пропорционального регулятора тока якоря DC

extern int          dbgSabs;
extern unsigned int CalibrStart;
extern float32_t    Tpwm;           // период ШИМ в мкс
extern uint16_t     timeBasePeriod; //

extern float ku01;
motor       *mfast = &motor1;

//extern float IGBT_T[];

extern long fref1, fref2; // заданная частота поля АИН1, АИН2   0.1 об/мин
//extern bool TXN, Fs;
extern bool     Fs;

#if dbgIfull
extern int Udc_imit;
#endif

#if KSP_GEN
extern int VTGen_hold;
#endif

extern uint16_t request_firmware_update;

// Extern function
void DSP28x_usDelay(unsigned long Count);
void WriteErrorTable(void); // FRAM.c   Сохранение буфера ошибок из RAM в FRAM - Вызов по кнопке СТОП в MotorControl

void SendInfo(void);
void SendStr(int size, char *str);
void SendTemp(void);

void change_motID(motor *v);
void ClearErrorTable(void); // Очистка таблицы ошибок
void pi_Reset(motor *m);
void PUSK_Init(motor *v);
void Reset_stop(motor *v);
void Gain_Selector(motor *m, int data1, int data2, int data3, uint16_t cmd);

void Reset_stend_filtr(void);


//06/02/2018
motor       *mot28 = &motor1; //определяет номер (адресс) АИНа

extern REFs REF;
SharpMode    sharpMode;
extern float emstep; // шаг приращения EPS;
uint16_t     BrRes1_Val = 0;
uint16_t     BrRes2_Val = 0;
motor       *mK60       = &motor1;
MODEs        MODE; // Структура для описания переменных для различных режимов
extern float pi_kIqd;


//union CCS ccs;
union SHARP2     sharp2;
union StendFlag  sflag;
union StendError sError;

int SplMode = 0;

//28.08.2024
extern uint16_t rxdata[8]; // принимаем массив rxdata 8 байт из 29 Mailbox MsgID = 128
extern uint16_t cantxdata[8];

//02.09.2024
extern STEND_bit sbit;

uint32_t objID = 2;

//09.09.2024
volatile union word32 rxA, rxB;

uint16_t Req_336 = 0; // логическая переменная для вывода массивов в фоне, передаётся по 336 команде от GUI

extern uint16_t dbgTrip;
extern uint16_t Imax;

uint16_t dbgRECALL = 0;
uint16_t dbgi      = 0;

//13.06.2026
KUK1 kuk1;
KUK2 kuk2;
KUK3 kuk3;

//---------------------------------------------------------------------------
// Параметры сетевого режима таблицей (GridTie.ini - команды 634, 640..666;
// ЦАП.ini - 672, 673; Модель.ini - 667..671 и 674..679).
//
// Коэффициенты контура при наладке крутят десятками, и отдельный case на
// каждый - это три десятка одинаковых блоков по пять строк. Строка таблицы
// говорит то же самое, а обработчик у всех общий.
//
// Значение в таблице GUI - short, поэтому у каждого поля свой масштаб:
//
//     значение_в_таблице = поле * k
//
// Масштабы подобраны так, чтобы шаг таблицы был мельче разумного шага
// настройки, а весь диапазон влезал в 16 знаковых бит. Порядок строк здесь
// и в файлах таблиц совпадает - так их проще сверять.
//
// Таблица в приборе ОДНА, а файлов в GUI четыре, и делятся они по занятию:
//
//     GridTie.ini        контур тока, масштабы тракта, защиты, синхронизация -
//                        то, что задают один раз при наладке
//     Регуляторы АИН.ini коэффициенты двух медленных контуров: напряжения
//                        острова и подавления постоянки. Сюда лезут посреди
//                        опыта, подбирая число по реакции живой машины.
//                        "АИН" в имени - адрес, а не область действия:
//                        подавление постоянки работает в ОБОИХ режимах
//     Модель.ini         параметры железа для plant_sim.c
//     ЦАП.ini            что вывести на отладочный пин и с каким масштабом
//---------------------------------------------------------------------------
struct GtPar
{
    uint16_t  cmd;
    float    *f; // поле типа float, либо 0
    uint16_t *u; // поле типа uint16_t, либо 0
    float     k; // масштаб
};

static const struct GtPar gtPars[] = {
    // ключ телеметрии - не поле gt, но такой же настраиваемый параметр
    {634, 0, &teleSel, 1.0f},

    // контур тока
    {640, &gt.pr.Kp, 0, 100.0f},
    {641, &gt.pr.Ki, 0, 1.0f},
    {642, &gt.pr.leak, 0, 10000.0f},
    {643, &gt.pr.xMax, 0, 1.0f},
    {644, &gt.IsetSlew, 0, 100.0f},
    {645, &gt.Iq, 0, 100.0f},
    {646, &gt.mMax, 0, 1000.0f},

    // Два медленных контура - таблица GUI "Регуляторы АИН.ini", порядок строк
    // тот же. Оба в Cla1Task1, оба раз за период выходного напряжения.
    //
    // Подавление постоянной составляющей, разбор - grid_tie.txt, 3.1.
    // kDCp здесь не украшение: объект сам интегратор, и без пропорциональной
    // части контур не сходится, а качается (3.1.6). Порог dcSatLimit - это
    // диагностика, а не параметр регулятора, но крутят их вместе.
    {647, &kDC, 0, 10000.0f},
    {681, &kDCp, 0, 10000.0f},
    {648, &mDCmax, 0, 1000.0f},
    {680, 0, &dcSatLimit, 1.0f},

    // Контур выходного напряжения острова, разбор - grid_tie.txt, 11. Сама
    // уставка сюда не входит - она на движке Gain1 главного окна, команда 633.
    //
    // Коэффициенты безразмерные и читаются прямо: uOutKp - доля ошибки,
    // снимаемая сразу, uOutKi - доля, добираемая за окно (период выхода).
    // Так вышло потому, что поправка считается в вольтах ДЕЙСТВУЮЩИХ.
    {682, &uOutKp, 0, 1000.0f},
    {683, &uOutKi, 0, 1000.0f},
    {684, &uOutCorMax, 0, 10.0f},
    {685, 0, &uOutSatLimit, 1.0f},

    // ПАСПОРТ СИЛОВОЙ ЧАСТИ - таблица GUI "Силовая часть.ini".
    //
    // Описание КОНКРЕТНОЙ машины, переживающее выключение: эти семь чисел
    // лежат во флеше рядом с калибровками АЦП и поднимаются при старте без
    // единого кадра CAN. Разбор - my_Files/power_par.txt.
    //
    // Запись любой строки ниже тянет за собой power_par_apply() и заявку на
    // сохранение - см. хук в gt_par_cmd().
    //
    // Масштабы взяты те же, что у соседей, описывающих то же самое: n - как
    // у 649, индуктивности в мк/милли-генри - как у 668 и 676, сопротивление
    // в миллиомах - как у 669.
    {686, &pwr.n, 0, 100.0f},
    {687, &pwr.Lleak, 0, 1000000.0f}, // мкГн
    {688, &pwr.Rwind, 0, 1000.0f},    // мОм
    {689, &pwr.Lmag, 0, 1000.0f},     // мГн
    {690, &pwr.Pnom, 0, 1.0f},        // Вт, без масштаба: 32 кВт влезает в int16
    {691, &pwr.UacNom, 0, 10.0f},
    {692, &pwr.UdcNom, 0, 10.0f},

    // масштабы тракта
    //
    // 649 правит ПРЕДСТАВЛЕНИЕ КОНТУРА о коэффициенте трансформации, и после
    // появления паспорта (686) это стало осознанным расхождением с ним, а не
    // вторым местом для одного числа. Ради такого опыта gt.n и sim.n и
    // разведены - см. шапку struct Plant.
    {649, &gt.n, 0, 100.0f},
    {650, &gt.kU220, 0, 10000.0f},
    {651, &gt.iSign, 0, 1.0f},

    // синхронизация
    {652, 0, &gt.syncNeed, 1.0f},
    {653, 0, &gt.satLimit, 1.0f},
    {654, 0, &gt.noGridMax, 1.0f},
    {655, &gt.phaseOk, 0, 1000.0f},

    // защиты
    {656, &gt.ImaxPri, 0, 10.0f},
    {657, &gt.ImaxSec, 0, 10.0f},
    {658, &gt.UdcMin, 0, 10.0f},
    {659, &gt.UdcMax, 0, 10.0f},
    {660, &gt.freqMin, 0, 10.0f},
    {661, &gt.freqMax, 0, 10.0f},
    {662, &gt.ampMin, 0, 1.0f},
    {663, &gt.ampMax, 0, 1.0f},

    // источники опоры и звена
    {664, 0, &gt.UdcSrc, 1.0f},
    {665, &gt.UdcFixed, 0, 10.0f},
    {666, 0, &gt.refSrc, 1.0f},

    // Модель силовой части (my_Files/plant_sim.c). В GUI это отдельная таблица
    // Модель.ini: параметры железа и коэффициенты контура - разные занятия, и
    // искать строку модели среди трёх десятков настроек контура незачем.
    //
    // Номера 667..670 достались от прежней модели, жившей внутри gt, и
    // оставлены прежними намеренно: поля переехали, смысл команд не изменился.
    {667, 0, &sim.on, 1.0f},
    {668, &sim.Lleak, 0, 1000000.0f}, // мкГн
    {669, &sim.Rwind, 0, 1000.0f},    // мОм
    {670, &sim.uDCinject, 0, 100.0f},
    {671, 0, &gtTrcRun, 1.0f},

    {674, &sim.Udc, 0, 10.0f},
    {675, &sim.n, 0, 100.0f},
    {676, &sim.Lmag, 0, 1000.0f}, // мГн
    {677, 0, &sim.loadType, 1.0f},
    {678, &sim.Rload, 0, 100.0f},
    {679, &sim.Lload, 0, 1000.0f}, // мГн

    // Отладочный ЦАП на пине A1 (осциллограф): номер сигнала и его масштаб.
    // В GUI это ОТДЕЛЬНАЯ таблица ЦАП.ini, а не строки GridTie.ini: сюда лезут
    // посреди опыта, перекидывая щуп с величины на величину, и две строки среди
    // трёх десятков коэффициентов контура каждый раз пришлось бы выискивать.
    // Обработчик от этого не меняется - таблица в приборе всё равно одна.
    //
    // Масштаб идёт строкой следом за источником не для красоты - при смене
    // сигнала его правят сразу же, иначе новая величина либо упрётся в потолок
    // шкалы, либо ляжет прямой по середине. Список источников и разумные
    // значения kDacDbg - у определения dacDbgSrc в my_Files/1ph_Sin.c.
    {672, 0, &dacDbgSrc, 1.0f},
    {673, &kDacDbg, 0, 10.0f},

    // Команды 680..685 стоят выше, рядом со своими контурами: 25.09.2026 они
    // собраны в таблицу "Регуляторы АИН.ini", и порядок здесь сверяется с ней.
};

//---------------------------------------------------------------------------
// Возвращает 1, если команда нашлась в таблице и уже обработана.
//
// Запись при data2 = 3333, чтение - тем же cmd с data2 = 0: ровно так же,
// как у команд 630..633, и так же, как посылает строка таблицы в GUI.
//---------------------------------------------------------------------------
static uint16_t gt_par_cmd(uint16_t data1, uint16_t data2, uint16_t data3, uint16_t cmd)
{
    uint16_t i;

    for (i = 0; i < sizeof(gtPars) / sizeof(gtPars[0]); i++)
    {
        const struct GtPar *p = &gtPars[i];
        float               v;

        if (p->cmd != cmd)
            continue;

        if (data2 == 3333)
        {
            v = (float)(int16_t)data1 / p->k;

            if (p->f)
                *p->f = v;
            else
                *p->u = (uint16_t)rounding(v);

            // Коэффициент трансформации и напряжение звена входят в пересчёт
            // uOutRef -> sineB.gain. В острове это надо применить сразу:
            // оператор правит n или UdcFixed именно затем, чтобы напряжение на
            // выходе сошлось с измеренным. В сетевом режиме gain не при делах.
            if (!gridMode && (cmd == 649 || cmd == 665))
                grid_tie_apply_uref();

            // Смена сигнала на отладочном ЦАП тянет за собой свой масштаб:
            // 50 кодов на единицу годятся для тока первички и дают прямую по
            // середине для индекса модуляции. Строка kDacDbg в таблице
            // обновится сама - GUI перечитывает её следом за любой посылкой.
            if (cmd == 672)
                dacDbg_scale_default();

            //-----------------------------------------------------------
            // НАСТРОЙКА ПРИБОРА - ПРАВИТСЯ В ОЗУ, ВО ФЛЕШ ИДЁТ ОТДЕЛЬНО.
            //
            // Две группы команд правят не режим работы, а описание самой
            // машины (my_Files/power_par.h):
            //
            //   686..692             паспорт силовой части
            //   647..648, 680..685   коэффициенты двух медленных контуров
            //
            // ЗАЯВКИ НА ЗАПИСЬ ЗДЕСЬ НЕТ, и это осознанно. Подбирая значение
            // кнопками "+/-", оператор шлёт по команде на щелчок, и запись на
            // каждую означала бы полсотни страниц банка за одну настройку.
            // Правка живёт в ОЗУ и применяется немедленно; во флеш её
            // отправляет отдельная команда 693. Разбор - в power_par.h.
            //
            // Паспорт надо РАЗДАТЬ сразу: оператор правит его затем, чтобы
            // прибор тут же перестроился, а не после перезагрузки.
            // power_par_apply() пересчитает и пороги защит, и номинал нагрузки
            // модели, и запас по индексу модуляции.
            //
            // Сумму ставим ДО раздачи: по ней power_par_apply() и отличает
            // заданный паспорт от заводского.
            //-----------------------------------------------------------
            if ((cmd >= 686U) && (cmd <= 692U))
            {
                pwr.crc = POWER_SUM_PWR();
                power_par_apply();
            }
            else if ((cmd == 647U) || (cmd == 648U) ||
                     ((cmd >= 680U) && (cmd <= 685U)))
            {
                // Коэффициенты правятся прямо в message RAM (их читает CLA),
                // поэтому раздавать нечего - надо лишь снять с них копию для
                // флеша и пометить её суммой.
                reg.uOutKp       = uOutKp;
                reg.uOutKi       = uOutKi;
                reg.uOutCorMax   = uOutCorMax;
                reg.uOutSatLimit = uOutSatLimit;
                reg.kDC          = kDC;
                reg.kDCp         = kDCp;
                reg.mDCmax       = mDCmax;
                reg.dcSatLimit   = dcSatLimit;
                reg.crc          = POWER_SUM_REG();
            }
        }

        v = p->f ? *p->f : (float)*p->u;
        SendAck((int16_t)rounding(v * p->k), data2, data3, cmd);
        return 1;
    }

    return 0;
}

void cmd_MBOX28(uint16_t data1, uint16_t data2, uint16_t data3, uint16_t cmd)
{
    if (gt_par_cmd(data1, data2, data3, cmd))
        return;

    switch (cmd)
    {
        case 0: // команда посылается при открытии формы Мотор-Контрол
            // d1=55 d2=55
            // d3: bit0 = sbit.bit.connect, bit1 = CANA_FD (0=classic IDs 100-132, 1=FD ID 140)
            SendAck(55, 55, (uint16_t)(sbit.bit.connect | (CANA_FD << 1)), cmd);
            break;

        case 1001: // команда включения/ выключения и выбора типа   кадра   по CANA

            canaKadr.all              = 0; // выключаем передачу любого кадра
            CpuTimer1Regs.TCR.bit.TSS = 1; // останавливаем таймер Т1 для вывода k60 кадра

            switch (data3)
            {
                case 1:
                    canaKadr.bit.F1_convertor = 1;
                    MCAN_setFastDLC(8); // 8 байт (Idc,Udc,Vin,ticker)
                    break;
                case 2:
                    canaKadr.bit.F1_invertor = 1;
                    MCAN_setFastDLC(8); // 8 байт (Ia,Ib,Idc,ticker)
                    break;
                case 3:
                    canaKadr.bit.F3_invertor = 1;
                    MCAN_setFastDLC(12); // 24 байта (SPL_UI: 12 слов)
                    break;
                case 4:
                    canaKadr.bit.F3_convertor = 1;
                    MCAN_setFastDLC(13); // 32 байта (16 слов), расширен 23.09.2026
                    break;

                case 5: // включить передачу медленного кадра по CANA
                {
                    CpuTimer1Regs.PRD.all     = mSecond(K60per); // период таймера Т1 в миллисекундах
                    CpuTimer1Regs.TCR.bit.TRB = 1;               // устанавливаем счётчик на начало периода
                    CpuTimer1Regs.TCR.bit.TSS = 0;               // запускаем таймер Т1 для вывода кадра
                    canaKadr.bit.k60_run      = 1;               // команда запуска  кадра k60 по CANA
                }
                break;
            }

            SendAck(K60per, data2, canaKadr.all, cmd);
            break;

        case 1: // устанавливаем интервал передачи медленного кадра в милисекундах
            if (data2 == 3333)
            {
                K60per = data1;
            }
            SendAck(K60per, data2, data3, cmd);
            break;

        case 2: // нажата Кнопка "Подключить"
            REF_init(&REF);

            sbit.bit.connect = 1;

            IGBT1_Disable();
            IGBT2_Disable();
            // sharp2.all = 0;
            motor_init(motors[0]);
            motor_init(motors[1]);

            // команда запуска REF кадра по CANA
            CpuTimer0Regs.PRD.all     = mSecond(50); // период таймера Т0 в миллисекундах
            CpuTimer0Regs.TCR.bit.TRB = 1;           // устанавливаем счётчик на начало периода
            CpuTimer0Regs.TCR.bit.TSS = 0;           // запускаем таймер Т0
            SendAck(data1, data2, data3, cmd);

            break;

        case 3: // нажата Кнопка отключить
                //           CpuTimer0Regs.TCR.bit.TSS = 1; // останавливаем таймер Т0 для вывода REF кадра
            sbit.bit.connect = 0;
            REF_init(&REF);
            sharp.all = 0;

            IGBT1_Disable();
            IGBT2_Disable();
        
            SplMode = 0;

            SendAck(data1, data2, data3, cmd);
            break;

        case 4: // нажата Кнопка ПУСК
            PUSK();
            break;

        case 5: // Кнопка СТОП
            STOP();
            break;
            //-----------------------------------------------------------------------
        case 6: // Задаем скорость нарастания частоты
            if (data2 == 3333)
            {
                REF.ramp_step      = data1 * 10;
                sharp.bit.ramp_Off = data3;
            }
            data3 = (sharp.bit.ramp_Off) ? 1 : 0;
            SendAck(REF.ramp_step / 10, data2, data3, cmd);
            break;
            //------------------------------------------------------------------------
        case 7: // Действия совершаемые при закрытии формы Form1!
            //       REF_init(&REF);
            sharp.all = 0;

            //      RealMOD = RealMOD_def;

            IGBT1_Disable();
            IGBT2_Disable();

            break;

        case 8: // нажата

            break;

            //------------------------------------------------------------
            // сбрасываем галочку Enable AIN
        // data3 ответа - ФАКТИЧЕСКОЕ состояние ключей. По нему Motor_Control
        // красит индикатор однофазного моста (зелёный/красный), а не по тому,
        // что оператор нажал: нажатие - намерение, ключи - правда.
        // Запись data2 = 3333, чтение data2 = 0. Чтение нужно Motor_Control
        // при подключении: своего мнения о том, включены ли ключи моста, GUI
        // не имеет, а красить индикатор наугад хуже, чем не красить вовсе.
        case 10: // Disable  AIN1 data1 =1    IGBT as In Pin )
            if (data2 == 3333)
            {
                motors[0]->mbit.bit.AIN_Disable = data1;
                if (data1 == 1)
                    IGBT1_Disable();
            }
            SendAck(motors[0]->mbit.bit.AIN_Disable, data2, motors[0]->flag.bit.pwm_On, cmd);
            break;

        case 11: // Disable  AIN2  if data1 =1  IGBT as In Pin   )
            motors[1]->mbit.bit.AIN_Disable = data1;
            if (data1 == 1)
                IGBT2_Disable();
            SendAck(data1, data2, data3, cmd);
            break;

            //------------------------------------------------------------
            // устанавливаем галочку Enable AIN
            // Ответ нужен не для сведений, а чтобы GUI было чем гасить жёлтую
            // кнопку: там "послал - жду - ACK - снял". Без него "Пуск" остаётся
            // жёлтым навсегда, и оператор не отличает включённые ключи от
            // потерянной команды.
        case 12:
            PWM1_On();
            SendAck(data1, data2, motors[0]->flag.bit.pwm_On, cmd);
            break;

        case 13:
            PWM2_On();
            SendAck(data1, data2, data3, cmd);
            break;

            //-------------------------------------------------------------
        case 14: // mot28 -> определяет номер мотора для приёма команд от Мотор Control
            if (data2 == 3333)
            {
                if (data1 == 1)
                    mot28 = motors[0];
                else if (data1 == 2)

                    mot28 = motors[1];
            }

            if (mot28 == motors[0])
                data1 = 1;
            else if (mot28 == motors[1])
                data1 = 2;

            SendAck(data1, data2, data3, cmd);
            break;

        case 17:
            if (data2 == 3333)
                mot28->delay = data1;
            SendAck(mot28->delay, data2, data3, cmd);
            break;

        case 22:
            if (data2 == 3333)
                motors[0]->mode.bit.LL = data1;
            SendAck(motors[0]->mode.bit.LL, data2, data3, cmd);
            break;

        case 23:
            if (data2 == 3333)
                motors[1]->mode.bit.LL = data1;
            SendAck(motors[1]->mode.bit.LL, data2, data3, cmd);
            break;

        case 24:                                        // Запрос IDprom_
            SendAck(kuk1.all, kuk2.all, kuk3.all, cmd); // возвращаем установки кук-ов
            break;

        case 25: //пинимаем команду переключения номера мотора для параметров быстрого кадра
            if (data2 == 3333)
            {
                if (data1 == 1)
                {
                    mfast            = motors[0];
                    sbit.bit.motfast = 0;
                }
                else if (data1 == 2)
                {
                    mfast            = motors[1];
                    sbit.bit.motfast = 1;
                }
            }

            if (mfast == motors[0])
                data1 = 1;
            else if (mfast == motors[1])
                data1 = 2;

            SendAck(data1, data2, data3, cmd);
            break;

        case 26: // команда переключения номера мотора для параметров медленного кадра
            if (data2 == 3333)
            {
                if (data1 == 1)
                {
                    mK60            = motors[0];
                    sbit.bit.motK60 = 0;
                }
                else if (data1 == 2)
                {
                    mK60            = motors[1];
                    sbit.bit.motK60 = 1;
                }
            }

            if (mK60 == motors[0])
                data1 = 1;
            else if (mK60 == motors[1])
                data1 = 2;

            SendAck(data1, data2, data3, cmd);
            break;

        case 27: //
            if (data2 == 3333)
                mot28->clark.ku01 = (float)data1 / 100;
            SendAck((uint16_t)(mot28->clark.ku01 * 100 + 0.5), data2, data3, cmd);
            break;

    

        case 49:
            change_motID(mot28);
            SendAck((uint16_t)(mot28->tsh.Tr * 1e3), data2, data3, cmd);
            break;

        case 50:
            if (data2 == 3333)
            {
                motors[0]->ref.sMax         = (long)(data1) * 100000;
                motors[0]->mbit.bit.sMax_On = data3; // включить или выключить ограничение по скольжению
            }

            data3 = (motors[0]->mbit.bit.sMax_On) ? 1 : 0;
            SendAck((uint16_t)(motors[0]->ref.sMax / 100000), data2, data3, cmd);
            break;

        case 51:
            if (data2 == 3333)
            {
                motors[1]->ref.sMax         = (long)(data1) * 100000;
                motors[1]->mbit.bit.sMax_On = data3; // включить или выключить ограничение по скольжению
            }

            data3 = (motors[1]->mbit.bit.sMax_On) ? 1 : 0;
            SendAck((uint16_t)(motors[1]->ref.sMax / 100000), data2, data3, cmd);
            break;

        case 52: // сброс интегральных составляющих пи- регуляторов
            motors[0]->piRPM.i1  = 0;
            motors[0]->piIq.i1   = 0;
            motors[0]->piId.i1   = 0;
            motors[0]->piIm.i1   = 0;
            motors[0]->piEPSI.i1 = 0;
            break;

        case 53: // Защитные значения для токов ( PWM off)
                 // data1 - значение максимального тока
                 // data2 - флаг чтения/записи - 3333 - запись
                 // data3 - =1 direct control
            if (data2 == 3333)
            {
                Imax = saturU16(data1, 20, 0);
                currentLimit(Imax);
            }
            SendAck(Imax, data2, data3, cmd);
            break;

        case 54: //
            break;
        case 55: // Защитные значения по напряжению Udc ( off тормозных  резисторов)
            if (data2 == 3333)
                REF.voltMax = data1;
            SendAck(REF.voltMax, data2, data3, cmd);
            break;
        case 56: // Защитные значения по напряжению Udc ( on тормозных  резисторов)
            if (data2 == 3333)
                REF.voltMin = data1;
            SendAck(REF.voltMin, data2, data3, cmd);
            break;

#if Strob_rg
        case 57:
            if (data2 == 3333)
                rgStrob = (float)data1 / 180 * PI; //в data1 угол в градусах преобразуем в радианы
            if (data2 == 5555)
            {
                rgStrob_enable = data3;
            }
            data3 = rgStrob_enable;
            SendAck(rounding(rgStrob * 180 / PI), data2, data3, cmd);
            break;
#endif

#if Strob_QEP     //--------------------------------------------------------------
        case 570: // номер зуба энкодера по которому формируется строб запуска развёртки
            if (data2 == 3333)
            {
                qepRegs->QPOSCMP = (data1 <= qepRegs->QPOSMAX) ? data1 : qepRegs->QPOSMAX;
            }

            if (data2 == 5555)
            {
                if (data3 == 1)
                {
                    qepRegs->QCLR.all      = 0xFFFF; // QEP interrupts clear
                    qepRegs->QEINT.bit.PCM = 1;      // enable CMP interrupts

                    qepRegs->QPOSCTL.bit.PCE = 1; // Enable position compare unit
                }
                else
                {

                    qepRegs->QEINT.bit.PCM   = 0; // Disable CMP interrupts;
                    qepRegs->QPOSCTL.bit.PCE = 0; //Disable position compare unit;
                }
            }

            SendAck((uint16_t)qepRegs->QPOSCMP, data2, qepRegs->QEINT.bit.PCM, cmd);
            break;

        case 571:
            if (data2 == 3333)
                strob_sektor = data1;
            SendAck(strob_sektor, data2, data3, cmd);
            break;
#endif //----  Strob_QEP  ---------

        case 58:
            if (data2 == 3333)
            {
                motStrob = (data1 == 1) ? motors[0] : motors[1];
                qepRegs  = (data1 == 1) ? &EQep1Regs : &EQep2Regs;
            }
            data1 = (motStrob == motors[0]) ? 1 : 2;
            SendAck(data1, data2, data3, cmd);
            break;

        case 59: // выключение  режима удержания  оборотов вала  двигателя
                 // срабатывает по нажатию кнопки "поле/RPM" на Motor control
            if (data2 == 3333)
            {
                sharpMode.bits.rpm_Mode = data1;
                //          hold_rpm = 0;
            }
            SendAck(sharpMode.bits.rpm_Mode, data2, data3, cmd);
            break;
        // Команда 60 (Gen_Engine_SW для АИН-1) удалена: канал 1 - однофазный
        // мост 48->220, а не привод двигателя. Метка "Д"/"Г" убрана из
        // Motor_Control, и сам бит никто никогда не читал. Для АИН-2 (61)
        // всё остаётся как было.

        case 61:
            motors[1]->mbit.bit.Gen_Engine_SW = data1;
            SendAck(motors[1]->mbit.bit.Gen_Engine_SW, data2, data3, cmd);
            break;

        case 62: // изменение timeBasePeriod и соответственно Tpwm (data1 = период ШИМ в мкс если CLKDIV =0)
                 // эта команда запускается из таблицы TPWM
            if (data2 == 3333)
            {
                {
                    timeBasePeriod = data1 * 60;
                    EPWM_setTimeBasePeriod(myEPWM1_BASE, timeBasePeriod);
                    change_Tpwm();
                }
            }
            SendAck(timeBasePeriod / 60, data2, data3, cmd);
            break;

        case 620: // изменение делителя частоты ISR-PWM -> PeriodMax. data1 = CLKDIV
            if (data2 == 3333)
            {
                write_pwmCLKDIV(data1);
                change_Tpwm();
            }
            data1 = EPwm1Regs.TBCTL.bit.CLKDIV;
            SendAck(data1, data2, data3, cmd);
            break;

        case 621: // таблица sineB -> изменение количества точек формирования синусоиды sineB
            if (data2 == 3333)
            {
                sineB.resolution = data1;
                change_sine(&sineB);
                EPwm1Regs.TBPRD = sineB.pwm_period;
            }

            SendAck(sineB.resolution, data2, data3, cmd);
            break;

        case 622: // таблица sineB изменение метода формирования синусоиды
            if (data2 == 3333)
            {
                sine_metod = data1;
            }
            // Метод 1 удалён 17.08.2026 (асимметрия плеч -> постоянная
            // составляющая -> насыщение трансформатора, см. grid_tie.txt).
            // Зажимаем в 2, чтобы обратное чтение говорило правду: оператор
            // увидит, что установка не принята, а не решит, что метод 1 работает.
            if (sine_metod != 2)
            {
                sine_metod = 2;
            }
            data1 = sine_metod;
            SendAck(data1, data2, data3, cmd);
            break;

#if Strob_rg
        case 623: // таблица sineB -> строб номера  ШИМ периода в последовательности синусоиды
            if (data2 == 3333)
            {
                strobIndex = data1;
            }
            SendAck(strobIndex, data2, data3, cmd);
            break;
#endif

        case 624: // таблица sineB изменение частоты формирования синусоиды
            if (data2 == 3333)
            {
                sineB.freq = data1;
                change_sine(&sineB); // меняем параметры для формирования синусоиды
                EPwm1Regs.TBPRD = sineB.pwm_period;
            }

            SendAck(sineB.freq, data2, data3, cmd);
            break;

        case 625: // таблица sineA изменение частоты формирования синусоиды
            if (data2 == 3333)
            {
                sineA.freq = data1;
                change_sine(&sineA); // меняем параметры для формирования синусоиды
                EPwm6Regs.TBPRD = sineA.pwm_period;
            }

            SendAck(sineA.freq, data2, data3, cmd);
            break;

        case 626: // таблица sineA -> изменение количества точек формирования синусоиды sineB
            if (data2 == 3333)
            {
                sineA.resolution = data1;
                change_sine(&sineA);
                EPwm6Regs.TBPRD = sineA.pwm_period;
            }

            SendAck(sineA.resolution, data2, data3, cmd);
            break;

        // 630..635 — однофазный инвертор 48→220.
        // Запись: data2 = 3333. Чтение: data2 = 0, то же cmd.
        //
        //   630 - режим моста, 0/1/2 (см. GRID_MODE_* в grid_tie.h)
        //   635 - разрешение на выход в сеть (галочка Enable, блокировка)
        //
        // Режимы не независимы, они лежат на одной оси и идут по возрастанию
        // связи с сетью, поэтому это ОДНА команда, а не три флага:
        //
        //   0 АИН       - автономный инвертор напряжения. Частота своя
        //                 (sineB.freq, команда 624), амплитуда своя (uOutRef),
        //                 и держит её ЗАМКНУТЫЙ контур (grid_tie.txt, 11).
        //   1 Слежение  - синус ведётся за сетью по частоте, фазе и амплитуде,
        //                 контактор РАЗОМКНУТ. Наладочный режим: форму можно
        //                 спокойно сверить с сетью осциллографом.
        //   2 В сеть    - то же плюс замыкание контактора и токовый контур.
        //
        // Из 2 в 1 и обратно мост ходит, не прерывая слежения: разница ровно
        // в контакторе. В 2 пускает только gt.gridEn - без разрешения команда
        // молча не выполняется, и оператор видит это по ответу.
        case 630:
            if (data2 == 3333)
            {
                if (data1 == GRID_MODE_OFF)
                {
                    grid_tie_stop();
                    gridMode = 0;

                    // Остров начинаем с чистой прямой связи: поправка, набранная
                    // в прошлом заходе, была посчитана под другую нагрузку и
                    // другое звено. Запросом, а не записью - uOutCor хозяйство
                    // CLA (см. uOutResetReq в cla_shared.h).
                    uOutResetReq++;

                    grid_tie_apply_uref(); // амплитуда из uOutRef, поправка с нуля
                }
                else if ((data1 == GRID_MODE_SYNC) ||
                         ((data1 == GRID_MODE_GRID) && gt.gridEn))
                {
                    // Слежение нужно обоим верхним режимам, поэтому включаем
                    // его до разбора контактора. grid_tie_start() работает
                    // только из GT_OFF и сам обнуляет connReq - значит порядок
                    // важен: сначала пуск автомата, потом запрос.
                    if (!gridMode)
                    {
                        gridMode = 1;
                        grid_tie_start(); // GT_OFF -> GT_SYNC, контактор разомкнут
                    }
                    gt.connReq = (data1 == GRID_MODE_SYNC) ? 0U : 1U;
                }
            }
            SendAck(grid_mode_get(), data2, gt.State, cmd);
            break;

        case 635: // разрешение на выход в сеть (галочка Enable)
            if (data2 == 3333)
            {
                gt.gridEn = data1 ? 1U : 0U;

                // Снятое разрешение обязано снять и запрос: мост падает из
                // "в сеть" обратно в "слежение". Иначе он ушёл бы в сеть сам,
                // стоило поднять галочку обратно, - а галочка это блокировка,
                // она ничего не включает.
                if (!gt.gridEn)
                    gt.connReq = 0;
            }
            SendAck(gt.gridEn, data2, gt.State, cmd);
            break;

        case 632: // IsetRef, 0.01 А (амплитуда тока в сеть)
            if (data2 == 3333)
                gt.IsetRef = (float)data1 * 0.01f;
            SendAck((uint16_t)(gt.IsetRef * 100.0f + 0.5f), data2, data3, cmd);
            break;

        case 633: // uOutRef, 0.1 В (остров). В GridMode только запоминаем.
            //
            // Поправку контура при смене уставки НЕ сбрасываем, и это
            // осознанно: интеграл держит падение в трансформаторе, которое от
            // новой уставки никуда не делось, а обнуление дало бы провал
            // напряжения на всё время повторного набора. Контур отработает
            // ступеньку сам - тем же ПИ, что и всё остальное.
            if (data2 == 3333)
            {
                uOutRef = (float)data1 * 0.1f;
                if (!gridMode)
                    grid_tie_apply_uref();
            }
            SendAck((uint16_t)(uOutRef * 10.0f + 0.5f), data2, data3, cmd);
            break;

            //---------------------------------------------------------------
            // 693..698 - РАБОТА С ХРАНИЛИЩЕМ НАСТРОЙКИ. Строки таблицы
            // "Силовая часть", разбор - my_Files/power_par.txt.
            //
            // Три команды-действия и три индикатора. Действия НИЧЕГО НЕ ДЕЛАЮТ
            // САМИ - только взводят pwrCmd, а работает фон. Причина простая:
            // cmd_MBOX28() вызывается из прерывания MCAN, а трогать флеш из
            // прерывания нельзя. По той же причине во всём проекте заведены
            // gtCmd и calibCmd.
            //
            // Запись при data2 = 3333 и data1 = 1, как у строк-кнопок в
            // таблицах; ответ - нулём, потому что команда уже отработана и
            // держать взведённой её незачем.
            //---------------------------------------------------------------
        case 693: // ЗАПИСАТЬ настройку во флеш
            if ((data2 == 3333) && (data1 != 0))
                pwrCmd = 3;
            SendAck(0, data2, data3, cmd);
            break;

        case 695: // ЗАВОДСКОЙ ПАСПОРТ - в ОЗУ, без записи
            //
            // Только паспорт: коэффициенты регуляторов сбрасывает своя команда
            // 699. Порознь - потому что настраивают их порознь, ровно как нули
            // и масштабы АЦП со своими 213 и 206.
            //
            // Во флеше при этом остаётся прежняя настройка, и после
            // перезапуска она вернётся. Чтобы сброс стал постоянным, следом
            // нужна команда 693. Это ОТЛИЧАЕТСЯ от сброса калибровок АЦП:
            // те пишутся сразу, по своему давнему правилу.
            if ((data2 == 3333) && (data1 != 0))
                pwrCmd = 2;
            SendAck(0, data2, data3, cmd);
            break;

        case 699: // ЗАВОДСКИЕ КОЭФФИЦИЕНТЫ регуляторов - в ОЗУ, без записи
            if ((data2 == 3333) && (data1 != 0))
                pwrCmd = 5;
            SendAck(0, data2, data3, cmd);
            break;

        case 696: // сумма паспорта - ТОЛЬКО ЧТЕНИЕ. 0 = заводской
            SendAck(pwr.crc, data2, data3, cmd);
            break;

        case 697: // сумма коэффициентов - ТОЛЬКО ЧТЕНИЕ. 0 = заводские
            SendAck(reg.crc, data2, data3, cmd);
            break;

        case 694: // ОТКАТ: перечитать из флеша, отбросив правки в ОЗУ
            //
            // Откатывается ВСЯ запись целиком, обе настройки сразу. У сброса к
            // заводским это было бы неожиданностью - потому он и разделён на
            // 695 и 699, - а у отката нет: отмена несохранённого и должна
            // отменять всё несохранённое.
            if ((data2 == 3333) && (data1 != 0))
                pwrCmd = 4;
            SendAck(0, data2, data3, cmd);
            break;

            //---------------------------------------------------------------
            // 698 - НАЛАДОЧНАЯ, в таблицы оператора не вынесена: нужна тому,
            // кто разбирается, почему настройка не поднялась.
            //---------------------------------------------------------------
        case 698: // состояние флеша - ТОЛЬКО ЧТЕНИЕ
            //
            // Два байта в одном слове, как State и fault в слове 10 кадра 140:
            // свободных команд жалко, а обе величины в байт помещаются.
            //
            //   младший  calibLoadStatus - что нашлось во флеше при старте:
            //            0 прочитано, 1 пусто, 2 запись есть, но не наша
            //   старший  calibSaveStatus - чем кончилась последняя запись:
            //            0 успех, 1 банк стёрт, 3 сохранять нечего,
            //            4 ошибка флеша
            SendAck((uint16_t)(calibLoadStatus | (calibSaveStatus << 8)),
                    data2, data3, cmd);
            break;

        case 70: // запуск калибровки нуля выбранных каналов АЦП
            //
            // data1 - маска каналов, по биту на канал (см. AdcStartCal):
            //   1 iPri, 2 iSec, 4 iBat, 8 u220, 16 uBat
            //
            // Меню "Откалибровать все датчики" шлёт ADC_CAL_ALL = 31. Отдельный
            // канал удобнее калибровать галочкой "М" у его строки в Offsets.ini
            // - там не надо помнить номера битов.
            //
            // ОТВЕТ - ТЕКУЩЕЕ СОСТОЯНИЕ МАСКИ, и по нему видно, когда процесс
            // закончился: Adc_Calibrate() сбрасывает маску сама, отработав 256
            // отсчётов. При такте 5 кГц это около 51 мс, то есть ноль в ответе
            // приходит практически сразу. Ненулевой ответ означает, что
            // калибровка ещё идёт.
            if (data2 == 3333)
            {
                adcCalStart.all = data1;
                ical_counter    = 0; // начать отсчёт заново, даже если прошлый
                                     // запуск прервали на середине
            }

#if dbgIfull
            motors[0]->imit.ImF = 0;
            motors[1]->imit.ImF = 0;
#endif
            SendAck(adcCalStart.all, data2, data3, cmd);
            break;

        case 71: // настройка уровней компараторов  для защиты от превышения тока
            if (data2 == 3333)
            {
                Cmpss3Regs.DACHVALS.all = data1;
            }

            SendAck(Cmpss3Regs.DACHVALS.all, data2, data3, cmd);
            break;

        case 710: // настройка уровней компараторов  для защиты от превышения тока
            if (data2 == 3333)
            {
                Cmpss3Regs.DACLVALS.all = data1;
            }

            SendAck(Cmpss3Regs.DACLVALS.all, data2, data3, cmd);
            break;

        case 711: // настройка уровней компараторов  для защиты от превышения тока
            if (data2 == 3333)
            {
                Cmpss1Regs.DACLVALS.all = data1;
            }

            SendAck(Cmpss1Regs.DACLVALS.all, data2, data3, cmd);
            break;

        case 712: // настройка уровней компараторов  для защиты от превышения тока
            if (data2 == 3333)
            {
                Cmpss1Regs.DACHVALS.all = data1;
            }

            SendAck(Cmpss1Regs.DACHVALS.all, data2, data3, cmd);
            break;
        case 713: // настройка уровней компараторов  для защиты от превышения тока
            if (data2 == 3333)
            {
                Cmpss2Regs.DACLVALS.all = data1;
            }

            SendAck(Cmpss2Regs.DACLVALS.all, data2, data3, cmd);
            break;

        case 714: // настройка уровней компараторов  для защиты от превышения тока
            if (data2 == 3333)
            {
                Cmpss2Regs.DACHVALS.all = data1;
            }

            SendAck(Cmpss2Regs.DACHVALS.all, data2, data3, cmd);
            break;

        case 715: // настройка уровней компараторов  для защиты от превышения тока
            if (data2 == 3333)
            {
                Cmpss4Regs.DACLVALS.all = data1;
            }

            SendAck(Cmpss4Regs.DACLVALS.all, data2, data3, cmd);
            break;

        case 716: // настройка уровней компараторов  для защиты от превышения тока
            if (data2 == 3333)
            {
                Cmpss4Regs.DACHVALS.all = data1;
            }

            SendAck(Cmpss4Regs.DACHVALS.all, data2, data3, cmd);
            break;
            //----------------------------------------------------------------------------------------------
        case 72: // приём SpeedRef
                 // Принимаем  значения переменных
                 // data1 - значение
                 // data2 - флаг чтения/записи - 3333
                 // data3 - =1 direct control
            if (data2 == 3333)
            {
                REF.speedRef            = data1;
                sharpMode.bits.rpm_Mode = data3;
            }

            SendAck(REF.speedRef, data2, sharpMode.bits.rpm_Mode, cmd);
            break;
        case 73: // приём sAbs
            if (data2 == 3333)
            {
                motors[0]->ref.sAbs      = (long)data1;
                motors[0]->mode.bit.load = data3;
            }

            //     data3 = motors[0]->mode.bit.load;
            SendAck((uint16_t)motors[0]->ref.sAbs, data2, motors[0]->mode.bit.load, cmd);
            break;

        case 74:
            if (data2 == 3333)
                ;

            SendAck(data1, data2, data3, cmd);
            break;

        case 75: // приём mREF.gain1

            Gain_Selector(motors[0], data1, data2, data3, cmd);
            break;

        case 76: // приём REF.gain2
            Gain_Selector(motors[1], data1, data2, data3, cmd);

            break;

        case 77: // приём sAbs
            if (data2 == 3333)
            {
                motors[1]->ref.sAbs      = (long)data1;
                motors[1]->mode.bit.load = data3;
            }
            data3 = motors[1]->mode.bit.load;
            SendAck((uint16_t)motors[1]->ref.sAbs, data2, motors[1]->mode.bit.load, cmd);
            break;

            //------------------------------------------------------------------------------------------------
        case 78:
            //        pre_bl();
            break;

        case 79:
            if (data2 == 3333)
            {
                motors[0]->mode.bit.load = data1;
            }
            SendAck(motors[0]->mode.bit.load, data2, data3, cmd);
            break;

        case 80:
            if (data2 == 3333)
            {
                motors[1]->mode.bit.load = data1;
            }
            SendAck(motors[1]->mode.bit.load, data2, data3, cmd);
            break;
            //----------------------------------------------------------------------------------------------------------------------------------

        // Команды 810..813 (тип и паспорт двигателя для АИН-1) удалены вместе
        // с кнопкой AIN1_motorName в Motor_Control: канал 1 - однофазный мост
        // 48->220, двигателя на нём нет. motors[0]->typeID теперь остаётся
        // нулевым - защиту от этого см. в change_motID(). Для АИН-2 всё
        // работает по-прежнему, команды 820..823 ниже.
#if aci_fe
        case 815:
            if (data2 == 3333)
            {
                mot28->fe.Kp = data1 / 1000.0;
            }
            SendAck(rounding(mot28->fe.Kp * 1000), data2, data3, cmd);
            break;

        case 816:
            if (data2 == 3333)
            {
                mot28->fe.Ki = data1 / 10000.0;
            }
            SendAck(rounding(mot28->fe.Ki * 10000), data2, data3, cmd);
            break;
#endif
            //------------------------------------------------------------------------------------------------------------------------------------

        case 820:                            // меняем тип двигателя для АИН-2, если выключен  PWM2
            if (motors[1]->flag.bit.pwm_On == 0) //
            {
                motors[1]->typeID.motID     = data1;
                motors[1]->typeID.toothNum  = data2;
                motors[1]->typeID.pole_pair = data3;
            }
            SendAck(motors[1]->typeID.motID, motors[1]->typeID.pole_pair, motors[1]->typeID.pole_pair, cmd);
            break;

        case 821: // меняем параметры двигателя для АИН-1
            motors[1]->typeID.Rs = data1;
            motors[1]->typeID.Rr = data2;
            motors[1]->typeID.Ls = data3;
            SendAck(motors[1]->typeID.motID, motors[1]->typeID.Rr, motors[1]->typeID.Ls, cmd);
            break;
        case 822: // меняем параметры двигателя для АИН-1
            motors[1]->typeID.Lr   = data1;
            motors[1]->typeID.Lm   = data2;
            motors[1]->typeID.Inom = data3;
            SendAck(motors[1]->typeID.motID, motors[1]->typeID.Lm, motors[1]->typeID.Inom, cmd);
            break;
        case 823: // меняем параметры двигателя для АИН-2
            motors[1]->typeID.Unom = data1;
            motors[1]->typeID.Fnom = data2;
            motors[1]->typeID.Pnom = data3;

            SendAck(motors[1]->typeID.motID, 0, 0, cmd);

            change_motID(motors[1]);
            break;

            //------------------------------------------------------------------------------------------------------------------------------------
        case 83:
            if (data2 == 3333)
            {
                mot28->typeID.kEd = data1 / 100000.0f;
            }
            SendAck(rounding(mot28->typeID.kEd * 100000.0f), data2, data3, cmd);
            break;

        case 84:
            if (data2 == 3333)
            {
                mot28->typeID.kEq = data1 / 100000.0f;
            }
            SendAck(rounding(mot28->typeID.kEq * 100000.0f), data2, data3, cmd);
            break;

        case 85:
            if (data2 == 3333)
            {
                mot28->ipark.Vd0_max = data1;
            }
            SendAck(rounding(mot28->ipark.Vd0_max), data2, data3, cmd);
            break;

        case 89:
            if (data2 == 3333)
            {
                mot28->typeID.Rs = (uint16_t)data1;
            }
            SendAck(mot28->typeID.Rs, data2, data3, cmd);
            change_motID(mot28);
            //        SMO_Init(mot28);
            break;

        case 90:
            if (data2 == 3333)
            {
                mot28->typeID.Lm = (uint16_t)data1;
            }
            SendAck(mot28->typeID.Lm, data2, data3, cmd);

            break;

        case 91:
            if (data2 == 3333)
            {
                mot28->typeID.Ls = (uint16_t)data1;
            }
            SendAck(mot28->typeID.Ls, data2, data3, cmd);
            change_motID(mot28);

            break;

        case 92:
            if (data2 == 3333)
            {
                mot28->typeID.Lr = (uint16_t)data1;
            }
            SendAck(mot28->typeID.Lr, data2, data3, cmd);
            change_motID(mot28);
            //        SMO_Init(mot28);
            break;

        case 93:
            if (data2 == 3333)
            {
                mot28->typeID.Rr = (uint16_t)data1;
            }
            SendAck(mot28->typeID.Rr, data2, data3, cmd);
            break;

        case 94:
            if (data2 == 3333)
            {
                mot28->typeID.kTe = data1 / 100.0;
            }
            SendAck(mot28->typeID.kTe * 100, data2, data3, cmd);
            break;

        case 95: // команда изменения числа зубъев счётного диска. Запускается из таблицы typeID.ini
            if (data2 == 3333)
            {
                mot28->typeID.toothNum = data1;

                if (mot28 == motors[0])
                {
                    //                QEP_Init(&motor1.qep, &EQep1Regs);
                    data3 = 1;
                } //нужно для правильного вычисления RPM по энкодеру
                else
                {
                    //               QEP_Init(&motor2.qep, &EQep2Regs);
                    data3 = 2;
                }
            }

            SendAck(mot28->typeID.toothNum, data2, data3, cmd);
            break;

        case 99:
            if (data2 == 3333)
            {
                mot28->typeID.motID = data1;
            }
            SendAck(mot28->typeID.motID, data2, data3, cmd);
            break;

        case 110: // команда обнуление всего массива ошибок

            ClearErrorTable();
            eeprom.bit.SaveErrors = 1; // сохранить очистку ошибок и истории в EEPROM
            Req_336               = 1; //устанавливаем флаг для сканирования  в фоне текущих ошибок и при их отсутствии сброса лампы pb_ErrView
            break;

        case 112:
            BrRes1_Val = data1;
            BrRes2_Val = data2;
            break;

        case 118:
            if (data2 == 3333)
                mot28->pwmgen.Dead_time = (float)data1 / timeBasePeriod;
            SendAck(rounding(mot28->pwmgen.Dead_time * timeBasePeriod), data2, data3, cmd);
            break;

            //-----------------------------------------------------------------------

        case 191:
            if (data2 == 3333)
                mot28->rg.offset = data1;
            SendAck(mot28->rg.offset, data2, data3, cmd);
            break;

        case 192:
            if (data2 == 3333)
                motors[0]->ref.Iq = (float)data1;
            if (data2 == 5555)
                motors[0]->mbit.bit.Iq_ref = data3;
            data3 = (motors[0]->mbit.bit.Iq_ref) ? 1 : 0;
            SendAck(rounding(motors[0]->ref.Iq), data2, data3, cmd);
            break;

        case 1921:
            if (data2 == 3333)
                motors[1]->ref.Iq = (float)data1;
            if (data2 == 5555)
                motors[1]->mbit.bit.Iq_ref = data3;
            data3 = (motors[1]->mbit.bit.Iq_ref) ? 1 : 0;
            SendAck(rounding(motors[1]->ref.Iq), data2, data3, cmd);
            break;

        case 193:
            if (data2 == 3333)
            {
                mot28->ref.Id = data1;
            }
            if (data2 == 5555)
                mot28->mbit.bit.Id_ref = data3;
            data3 = (mot28->mbit.bit.Id_ref) ? 1 : 0;
            SendAck(rounding(mot28->ref.Id), data2, data3, cmd);
            break;

            //-------------------------------------------------------------------
            // data1 - значение
            // data2 - флаг чтения/записи - 3333 - запись
            // data3 - Мод
        case 194:
            if (data2 == 3333)

            {
                motors[0]->piIq.Kp = (float)data1 / 1000;
                motors[1]->piIq.Kp = (float)data1 / 1000;
            }
            SendAck(rounding(motors[0]->piIq.Kp * 1000.0f), data2, data3, cmd);
            break;

        case 195:
            if (data2 == 3333)
            {
                motors[0]->piIq.Ki = (float)data1 / 1000;
                motors[1]->piIq.Ki = (float)data1 / 1000;
            }
            SendAck(rounding(motors[0]->piIq.Ki * 1000.0f), data2, data3, cmd);
            break;

        case 196:
            if (data2 == 3333)
            {
                motors[0]->piId.Kp = (float)data1 / 1000.0f;
                motors[1]->piId.Kp = (float)data1 / 1000.0f;
            }
            SendAck(rounding(motors[0]->piId.Kp * 1000.0f), data2, data3, cmd);
            break;

        case 197:
            if (data2 == 3333)
            {
                motors[0]->piId.Ki = (float)data1 / 1000;
                motors[1]->piId.Ki = (float)data1 / 1000;
            }
            SendAck(rounding(motors[0]->piId.Ki * 1000.0f), data2, data3, cmd);
            break;
            //------------------------------------------------------------------
            // установка коэф. усиления каналов АЦП через таблицу
        case 198: // масштаб канала iPri (ток первички, B1)
            if (data2 == 3333)
            {
                kadc.iPri           = data1;
                kadcf.iPri          = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
                calibSaveReq        = 1; // и во флеш, см. calib_store.h
            }
            SendAck(kadc.iPri, data2, data3, cmd);
            break;

        case 199: // масштаб канала iSec (ток вторички, B4)
            if (data2 == 3333)
            {
                kadc.iSec           = data1;
                kadcf.iSec          = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
                calibSaveReq        = 1; // и во флеш, см. calib_store.h
            }
            SendAck(kadc.iSec, data2, data3, cmd);
            break;

        case 200: // масштаб канала iBat (ток АКБ, A12)
            if (data2 == 3333)
            {
                kadc.iBat           = data1;
                kadcf.iBat          = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
                calibSaveReq        = 1; // и во флеш, см. calib_store.h
            }
            SendAck(kadc.iBat, data2, data3, cmd);
            break;

        case 201: // контрольная сумма kadc - ТОЛЬКО ЧТЕНИЕ
            // Запись намеренно не выполняется, хотя data2 == 3333 сюда доходит.
            //
            // Сумму незачем задавать руками: её пересчитывает ControlSum() при
            // каждом изменении коэффициента (команды 198..203) и floatKadc()
            // при загрузке. Записанное вручную значение может только разойтись
            // с данными - и тогда checkEE() при следующем старте посчитает
            // калибровки испорченными и отправит их на заводские.
            //
            // Отвечаем фактическим значением: в таблице тарировки эта строка
            // нужна как индикатор, а попытка её изменить вернёт то, что есть.
            SendAck(kadc.crc, data2, data3, cmd);
            break;

        case 202: // масштаб канала u220 (напряжение сети, A0)
            if (data2 == 3333)
            {
                kadc.u220           = data1;
                kadcf.u220          = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
                calibSaveReq        = 1; // и во флеш, см. calib_store.h
            }
            SendAck(kadc.u220, data2, data3, cmd);
            break;

        case 203: // масштаб канала uBat (напряжение АКБ, A4)
            if (data2 == 3333)
            {
                kadc.uBat           = data1;
                kadcf.uBat          = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
                calibSaveReq        = 1; // и во флеш, см. calib_store.h
            }
            SendAck(kadc.uBat, data2, data3, cmd);
            break;

        case 204:
            if (data2 == 3333)
            {
                eeprom.bit.Store = 1;
            }

            eeStatusReg.all = send_EEcomand(SPIB_BASE, RDSR);
            SendAck(eeStatusReg.all, data2, data3, cmd);
            break;

        case 205:
            if (data2 == 3333)
            {
                eeprom.bit.Recall = 1;
            }

            eeStatusReg.all = send_EEcomand(SPIB_BASE, RDSR);
            SendAck(eeStatusReg.all, data2, data3, cmd);
            break;

            //---------------------------------------------------------------
            // 206 и 213 - сброс к заводским. Раздельно для масштабов и нулей,
            // потому что калибруются они независимо и разными процедурами:
            // масштабы вводит оператор из таблицы, нули измеряет автокалибровка.
            //
            // Обе команды рассчитаны на строку-переключатель в таблице .ini:
            // оператор ставит 1, команда срабатывает, в ответ уходит 0 - и
            // строка сама возвращается в исходное положение.
            //
            // Требование data1 == 1 не формальность: без него любое обращение к
            // строке, включая чтение при открытии таблицы, сбрасывало бы
            // калибровку.
            //---------------------------------------------------------------
        case 206: // сброс МАСШТАБОВ kadc к заводским (строка RESET в kADC.ini)
            if ((data2 == 3333) && (data1 == 1))
            {
                kadc = kadc_default; // crc = 0, признак "не калиброван"

                // Пересчитать вещественные масштабы, иначе контур продолжит
                // считать токи по прежней калибровке.
                floatKadc();

                // ПОРОГИ ЗАЩИТЫ ПО ТОКУ считаются из kadcf; без этого шага
                // компараторы остались бы настроены под старую калибровку.
                currentLimit(Imax);

                calib_store_after_reset();
            }
            SendAck(0, data2, data3, cmd);
            break;

        case 213: // сброс НУЛЕЙ offsets к заводским (строка RESET в Offsets.ini)
            if ((data2 == 3333) && (data1 == 1))
            {
                offsets = offsets_default; // crc = 0, признак "не калиброван"

                // Новые нули в регистры PPB, иначе АЦП продолжит вычитать
                // старые смещения.
                LoadOffsetsToPPB();

                // Пороги защиты отсчитываются от нуля канала, поэтому их надо
                // пересчитать и здесь.
                currentLimit(Imax);

                calib_store_after_reset();
            }
            SendAck(0, data2, data3, cmd);
            break;

            //---------------------------------------------------------------
            // 207..211 - нули каналов АЦП. Строка таблицы умеет два действия,
            // и различает их поле data2:
            //
            //   data2 = 3333   ручная правка: записать data1 как ноль канала
            //   data2 = 5555   щелчок по галочке "М", data3 - её новое
            //                  состояние. Единица означает "откалибровать
            //                  ЭТОТ канал"
            //
            // Галочка старше ручного ввода: поставили - канал калибруется, и
            // введённое до этого значение уже не важно. Снятие галочки ничего
            // не запускает.
            //
            // Автокалибровка точнее ручного ввода - она усредняет сигнал при
            // обесточенном входе по 256 отсчётам. Ручная правка нужна там, где
            // её не провести: подстроить по образцовому прибору, повторить
            // значение с другой платы, вернуть записанное в журнал.
            //
            // Каждая ручная запись проходит полную цепочку через
            // Offsets_Apply(): сумма, регистры PPB, пороги защиты, заявка на
            // сохранение. Иначе правка либо не дойдёт до железа, либо не
            // переживёт выключение.
            //
            // Контрольная сумма (212) остаётся только для чтения: считать её
            // руками незачем, Offsets_Apply() пересчитывает сумму сам, а
            // введённое значение могло бы только рассогласовать её с данными.
            //---------------------------------------------------------------
        case 207: // ноль канала iPri (ток первички, B1)
            if (data2 == 3333)
            {
                offsets.iPri = data1;
                Offsets_Apply();
            }
            else if ((data2 == 5555) && (data3 != 0))
            {
                adcCalStart.bit.iPri = 1;
                ical_counter         = 0;
            }
            SendAck(offsets.iPri, data2, 0, cmd); // 0 - галочка "М" сбрасывается сама
            break;

        case 208: // ноль канала iSec (ток вторички, B4)
            if (data2 == 3333)
            {
                offsets.iSec = data1;
                Offsets_Apply();
            }
            else if ((data2 == 5555) && (data3 != 0))
            {
                adcCalStart.bit.iSec = 1;
                ical_counter         = 0;
            }
            SendAck(offsets.iSec, data2, 0, cmd); // 0 - галочка "М" сбрасывается сама
            break;

        case 209: // ноль канала iBat (ток АКБ, A12)
            if (data2 == 3333)
            {
                offsets.iBat = data1;
                Offsets_Apply();
            }
            else if ((data2 == 5555) && (data3 != 0))
            {
                adcCalStart.bit.iBat = 1;
                ical_counter         = 0;
            }
            SendAck(offsets.iBat, data2, 0, cmd); // 0 - галочка "М" сбрасывается сама
            break;

        case 210: // ноль канала u220 (напряжение сети, A0)
            if (data2 == 3333)
            {
                offsets.u220 = data1;
                Offsets_Apply();
            }
            else if ((data2 == 5555) && (data3 != 0))
            {
                adcCalStart.bit.u220 = 1;
                ical_counter         = 0;
            }
            SendAck(offsets.u220, data2, 0, cmd); // 0 - галочка "М" сбрасывается сама
            break;

        case 211: // ноль канала uBat (напряжение АКБ, A4)
            if (data2 == 3333)
            {
                offsets.uBat = data1;
                Offsets_Apply();
            }
            else if ((data2 == 5555) && (data3 != 0))
            {
                adcCalStart.bit.uBat = 1;
                ical_counter         = 0;
            }
            SendAck(offsets.uBat, data2, 0, cmd); // 0 - галочка "М" сбрасывается сама
            break;

        case 212: // контрольная сумма offsets - ТОЛЬКО ЧТЕНИЕ
            SendAck(offsets.crc, data2, data3, cmd);
            break;

            //---------------------------------------------------------------
            // 224, 225 - канал uSec, напряжение на выходе инвертора (A1).
            //
            // Номера не подряд с остальными: диапазоны 198..205 и 207..213
            // заняты целиком, а 221..225 были свободны. Взяты две соседние.
            //
            // Работают в точности как их собратья: 224 по образцу 198..203
            // (масштаб), 225 по образцу 207..211 (ноль, с калибровкой по
            // галочке "М").
            //---------------------------------------------------------------
        case 224: // масштаб канала uSec (напряжение выхода инвертора, A1)
            if (data2 == 3333)
            {
                kadc.uSec           = data1;
                kadcf.uSec          = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
                calibSaveReq        = 1; // и во флеш, см. calib_store.h
            }
            SendAck(kadc.uSec, data2, data3, cmd);
            break;

        case 225: // ноль канала uSec
            if (data2 == 3333)
            {
                offsets.uSec = data1;
                Offsets_Apply();
            }
            else if ((data2 == 5555) && (data3 != 0))
            {
                adcCalStart.bit.uSec = 1;
                ical_counter         = 0;
            }
            SendAck(offsets.uSec, data2, 0, cmd); // 0 - галочка "М" сбрасывается сама
            break;

            //---------------------------------------------------------------------
            // data1 - значение
            // data2 - флаг чтения/записи - 3333 - запись
            // data3 - Мод

            //---------------------------------------------------------------------

        case 214:
            if (data2 == 3333)
                motors[1]->clark.kUa = (float)data1 / 100;
            SendAck(rounding(motors[1]->clark.kUa * 100), data2, data3, cmd);
            break;

        case 215:
            if (data2 == 3333)
                mot28->ref.piRPM_max_Gen = (float)data1 / 100;
            SendAck(rounding(mot28->ref.piRPM_max_Gen * 100), data2, data3, cmd);
            break;

            //---------------------------------------------------------------------

        case 216:
            if (data2 == 3333)
                mot28->piRPM.Kp = (float)data1 / 100;
            SendAck(rounding(mot28->piRPM.Kp * 100.0f), data2, data3, cmd);
            break;
        case 217:
            if (data2 == 3333)
                mot28->piRPM.Ki = (float)data1 / 10000;
            SendAck(rounding(mot28->piRPM.Ki * 10000.0f), data2, data3, cmd);
            break;
            //---------------------------------------------------------------------
        case 218:
            if (data2 == 3333)
                REF.ipVq = (float)data1 / 1000.0f;
            SendAck(rounding(REF.ipVq * 1000.0f), data2, data3, cmd);
            break;

        case 219:
            if (data2 == 3333)
                REF.ipVq = (float)data1 / 1000.0f;
            SendAck(rounding(REF.ipVq * 1000.0f), data2, data3, cmd);
            break;
            //---------------------------------------------------------------------------------
        case 220:
            if (data2 == 3333)
                motors[1]->rg.Out = (float)data1 / 180 * PI;
            if (data2 == 5555)
                motors[1]->mbit.bit.rgfix = data3;
            data3 = (motors[1]->mbit.bit.rgfix) ? 1 : 0;
            SendAck(rounding(motors[1]->rg.Out * 180 / PI), data2, data3, cmd);
            break;

        case 222:
            if (data2 == 3333)
                motors[0]->rg.Out = (float)data1 / 180 * PI;
            if (data2 == 5555)
                motors[0]->mbit.bit.rgfix = data3;
            data3 = (motors[0]->mbit.bit.rgfix) ? 1 : 0;
            SendAck(rounding(motors[0]->rg.Out * 180 / PI), data2, data3, cmd);
            break;

            //-----------------------------------------------------------------------------------------------
        case 226:
            if (data2 == 3333)
            {
                mot28->piIq.Umax = (float)data1;
                mot28->piIq.Umin = -(float)data1;
            }
            SendAck(rounding(mot28->piIq.Umax), data2, data3, cmd);
            break;

        case 227:
            if (data2 == 3333)
            {
                mot28->piId.Umax = (float)data1;
                mot28->piId.Umin = -(float)data1;
            }
            SendAck(rounding(mot28->piId.Umax), data2, data3, cmd);
            break;

        case 228:
            if (data2 == 3333)
            {
                mot28->typeID.pole_pair = data1;
            }
            SendAck(mot28->typeID.pole_pair, data2, data3, cmd);

            break;
            //--------------------------------------------------------------------------------
            // data1 - значение
            // data2 - флаг чтения/записи - 3333 - запись
            // data3 - флаг- direct control
        case 229:
            if (data2 == 3333)
            {
                mot28->park.kVd = (float)data1 / 100;
            }
            SendAck(rounding(mot28->park.kVd * 100), data2, data3, cmd);
            break;

        case 230:
            if (data2 == 3333)
            {
                mot28->park.kVq = (float)data1 / 100;
            }
            SendAck(rounding(mot28->park.kVq * 100), data2, data3, cmd);
            break;

            //---------------------------------------------------------------------------------
        case 236: //пустая команда TxTable[1]
            SendAck(data1, data2, data3, cmd);
            break;
        case 237: //пустая команда TxTable[2]
            SendAck(data1, data2, data3, cmd);
            break;
        case 238: //пустая команда TxTable[3]
            SendAck(data1, data2, data3, cmd);
            break;
        case 239: //пустая команда TxTable[4]
            SendAck(data1, data2, data3, cmd);
            break;
        case 240: //пустая команда TxTable[5]
            SendAck(data1, data2, data3, cmd);
            break;
            //-----------------------------------------------------------------------------------------------
            //  параметры для piIm регулятора
        case 243:
            if (data2 == 3333)
            {
                motors[0]->piIm.Umax = (float)data1;
                motors[0]->piIm.Umin = 0; //-(float) data1 / 1000;
            }
            SendAck(rounding(motors[0]->piIm.Umax), data2, data3, cmd);
            break;
        case 244:
            if (data2 == 3333)
                motors[0]->piIm.Ki = (float)data1 / 1000;
            SendAck(rounding(motors[0]->piIm.Ki * 1000), data2, data3, cmd);
            break;
        case 245:
            if (data2 == 3333)
                motors[0]->piIm.dumax = (float)data1;
            SendAck(rounding(motors[0]->piIm.dumax), data2, data3, cmd);
            break;
        case 246:
            if (data2 == 3333)
                motors[0]->piIm.Kp = (float)data1 / 1000;
            SendAck(rounding(motors[0]->piIm.Kp * 1000), data2, data3, cmd);
            break;
        case 247:
            if (data2 == 3333)
                motors[0]->ref.Im = (float)data1 / 1000;
            if (data2 == 5555)
                motors[0]->mbit.bit.Im = data3;
            data3 = (mot28->mbit.bit.Im) ? 1 : 0;
            SendAck(rounding(motors[0]->ref.Im * 1000), data2, data3, cmd);
            break;

            //---------------------------------------------------------------------------------
        case 333: // команда для получения инфо прошивки КСП
        {
            SendInfo();
        }
        break;

        case 336:
        {
            Req_336 = data1;
        }
        break;

        case 338:
        {
            igbt.all = data2;

            if (data1 == IGBT_ON1_CMD) // Включить прямое упр IGBT1 (DC-DC, VT1..VT4)
            {
                IGBT1_ManualEnable();
            }

            else if (data1 == IGBT_OFF1_CMD) // Выключить прямое упр IGBT1 (DC-DC)
            {
                IGBT1_ManualDisable();
            }

            else if (data1 == IGBT_SEND1_CMD) // Управление ключами IGBT1 (DC-DC)
            {
                if ((igbt_manually & (1U << 0)) != 0U)
                {
                    IGBT1_ManualApply(data2);
                }
            }

            else if (data1 == IGBT_ON2_CMD) // Включить прямое упр IGBT инвертора (стартовое состояние: все ключи выключены)
            {
                IGBT2_ManualEnable();
            }

            else if (data1 == IGBT_OFF2_CMD) // Выключить прямое упр IGBT инвертора.
            {
                IGBT2_ManualDisable();
            }

            else if (data1 == IGBT_SEND2_CMD) // Управление ключами. Принята новая конфигурация транзисторов инвертора.
            {
                if ((igbt_manually & (1U << 1)) != 0U)
                {
                    IGBT2_ManualApply(data2);
                }
            }

            else if (data1 == IGBT_OFFALL_CMD) // Выключить прямое управление обоими блоками
            {
                IGBT1_ManualDisable();
                IGBT2_ManualDisable();
            }

            SendAck(data1, data2, igbt.all, cmd);
            break;
        }

        case 9999: /* CAN bootloader: set flag + reset */
        {

            request_firmware_update = 1;

            break;
        }
        default:
            break;
    }
}

//======================================================================
void SendStr(int size, char *str)
{
    int i = 0;
    for (i = 0; i < size; i++)
    {
        SendAck(0, i, *(str + i), 32000);
    }
}
void SendInfo(void)
{
    Uint32 Bf = (Uint32)BoxesF;

    char str0[] = KSP_ID_STRING;
    char str1[] = FAST_FRAME_STRING;
    char str2[] = TOOTH_FRAME_STRING;
    char str3[] = __DATE__;
    char str4[] = __TIME__;
    SendStr(sizeof(str0), (char *)&str0);
    SendStr(sizeof(str1), (char *)&str1);
    SendStr(sizeof(str2), (char *)&str2);
    SendStr(sizeof(str3), (char *)&str3);
    SendStr(sizeof(str4), (char *)&str4);
    SendAck((int)(Bf & 0xFFFF), (int)(Bf >> 16) & 0xFFFF, (uint16_t)BoxesF, 32001);
    SendAck(kuk1.all, kuk2.all, kuk3.all, 32002);
}

void SendTemp(void)
{
    int i = 0;
    SendAck(32003, 32003, 32003, 32003);
    for (i = 0; i < 20; i += 2)
    {
        //       SendAck(i, IGBT_T[i], IGBT_T[i + 1], 32004);
    }
    SendAck(32005, 32005, 32005, 32005);
}

//----------------------------------------------------
void change_motID(motor *v)
{
    // Паспорт незаполнен - считать по нему нечего. Так теперь выглядит АИН-1:
    // это однофазный мост, команды 810..813 для него удалены, и typeID остаётся
    // нулевым. Без этой проверки деление 4096/pole_pair дало бы бесконечность,
    // а Unom/Fnom - NaN, стоило навести на motors[0] команду 14 и тронуть любую
    // таблицу двигателя.
    if ((v->typeID.pole_pair == 0) || (v->typeID.Fnom == 0))
        return;

    v->typeID.res_pp = (int)(4096.0f / v->typeID.pole_pair + 0.5f); // число тиков резолвера на эл.оборот  = 4096/pole_pair
    v->tsh.uf_norma  = (float)v->typeID.Unom / (float)v->typeID.Fnom * 1000;

    v->rg.k_AF = v->typeID.pole_pair / 60.0f * PI2 * Tpwm / 1000000.0f; // радиан за миллисекунду

    v->tsh.Rs  = v->typeID.Rs * 1.0e-6;
    v->tsh.Rr  = v->typeID.Rr * 1.0e-6;
    v->tsh.Ls  = v->typeID.Ls * 1.0e-6;
    v->tsh.Lm  = v->typeID.Lm * 1.0e-6;
    v->tsh.Lr  = v->typeID.Lr * 1.0e-6;
    v->tsh.Lrm = (v->typeID.Lr + v->typeID.Lm) * 0.000001;
    v->tsh.Lsm = (v->typeID.Ls + v->typeID.Lm) * 0.000001;
    v->tsh.kw  = (1.0 / 60 * PI2) * v->typeID.pole_pair; // коэффициент об.мин ротора -> рад/сек частота поля статора в ( рад/сек )

    v->tsh.Tr = (float)(v->typeID.Lr + v->typeID.Lm) / (float)v->typeID.Rr;
}
void pi_Reset(motor *m)
{
    m->piRPM.ui = 0;
    m->piRPM.i1 = 0;
    m->piId.ui  = 0;
    m->piId.i1  = 0;
    m->piIq.ui  = 0;
    m->piIq.i1  = 0;
    m->piIm.ui  = 0;
    m->piIm.i1  = 0;
}

void Reset_stend_filtr(void)
{
    uBat_rf = 0;
    iBat_rf = 0;

    // dc.Idc_sum убран: сумма квадратов входила в RMS-блок Task7, снятый
    // вместе с ней.

    stend.Pa_stend = 0;
}

void Gain_Selector(motor *m, int data1, int data2, int data3, uint16_t cmd)
{
    float *adr;
    float  value;
    float  out;
    int    iout;

    if (data2 == 3333) // write new value
    {
        if (m->mode.bit.umode != data3)
        {
            m->mode.bit.umode = data3;
        }
    }

    switch (m->mode.bit.umode)
    {
        case umode_gain:
            adr   = &m->ref.gain;
            value = (float)data1 / 1000.0f;
            out   = (*adr) * 1000.0f;

            break;

        case umode_Urms:
            adr   = &(m->ref.Urms);
            value = (float)data1 * sqrt2 / sqrt3; // преобразуем заданное rms в  значение амплитуды фазного напряжения
            out   = (*adr) / sqrt2 * sqrt3;       // возвращаем заданное значение rms линейного напряжения
            break;

        case umode_UF:
            adr   = &(m->ref.UF);
            value = (float)data1 / 1000.0 * sqrt2 / sqrt3;
            out   = (*adr) * 1000.0 / sqrt2 * sqrt3;
            break;

        case umode_Id:
            adr   = &(m->ref.Id);
            value = (float)data1 / 10;
            out   = (*adr) * 10.0;
            break;

        case umode_Im:
            adr   = &(m->ref.Im);
            value = (float)data1 / 10;
            out   = (*adr) * 10.0;
            break;
    }

    if (data2 == 3333)
    {
        *adr = value;
        iout = data1;
    } // write
    else
        iout = rounding(out);

    SendAck(iout, data2, m->mode.bit.umode, cmd);
}

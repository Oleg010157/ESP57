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
#include "cla_shared.h"
#include FAST_FRAME_STRING

#include "Functions.h"
#include "Konstanty.h"
#include "myEPWM.h"
#include "SPI_EEPROM.h"
#include "1ph_Sin.h"

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
extern uint16_t P15Kadr; // число определяет набор параметров 15-го MBOX

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
motor        *mot28 = &motor1; //определяет номер (адресс) АИНа
extern motor *mfast;

extern REFs  REF;
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

void cmd_MBOX28(uint16_t data1, uint16_t data2, uint16_t data3, uint16_t cmd)
{

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

                case 4: // включить передачу медленного кадра по CANA
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
            motor_init(&motor1);
            motor_init(&motor2);

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
        case 10: // Disable  AIN1 data1 =1    IGBT as In Pin )
            motor1.mbit.bit.AIN_Disable = data1;
            if (data1 == 1)
                IGBT1_Disable();
            SendAck(data1, data2, data3, cmd);
            break;

        case 11: // Disable  AIN2  if data1 =1  IGBT as In Pin   )
            motor2.mbit.bit.AIN_Disable = data1;
            if (data1 == 1)
                IGBT2_Disable();
            SendAck(data1, data2, data3, cmd);
            break;

            //------------------------------------------------------------
            // устанавливаем галочку Enable AIN
        case 12:
            PWM1_On();
            break;

        case 13:
            PWM2_On();
            break;

            //-------------------------------------------------------------
        case 14: // mot28 -> определяет номер мотора для приёма команд от Мотор Control
            if (data2 == 3333)
            {
                if (data1 == 1)
                    mot28 = &motor1;
                else if (data1 == 2)

                    mot28 = &motor2;
            }

            if (mot28 == &motor1)
                data1 = 1;
            else if (mot28 == &motor2)
                data1 = 2;

            SendAck(data1, data2, data3, cmd);
            break;

        case 15:
            sbit.bit.ResolverInverse = data1;
            break;

        case 16: // On/Off  Режим Реверса
            sbit.bit.Reverse = data1;
            break;

        case 17:
            if (data2 == 3333)
                mot28->delay = data1;
            SendAck(mot28->delay, data2, data3, cmd);
            break;

        case 18:
            motor1.mbit.bit.revers = data1;
            break;

        case 19:
            motor2.mbit.bit.revers = data1;
            break;

        case 20:
            motor1.mbit.bit.FOC_mode = data1;
            break;

        case 21:
            motor2.mbit.bit.FOC_mode = data1;
            break;

        case 22:
            if (data2 == 3333)
                motor1.mode.bit.LL = data1;
            SendAck(motor1.mode.bit.LL, data2, data3, cmd);
            break;

        case 23:
            if (data2 == 3333)
                motor2.mode.bit.LL = data1;
            SendAck(motor2.mode.bit.LL, data2, data3, cmd);
            break;

        case 24:                                        // Запрос IDprom_
            SendAck(kuk1.all, kuk2.all, kuk3.all, cmd); // возвращаем установки кук-ов
            break;

        case 25: //пинимаем команду переключения номера мотора для параметров быстрого кадра
            if (data2 == 3333)
            {
                if (data1 == 1)
                {
                    mfast            = &motor1;
                    sbit.bit.motfast = 0;
                }
                else if (data1 == 2)
                {
                    mfast            = &motor2;
                    sbit.bit.motfast = 1;
                }
            }

            if (mfast == &motor1)
                data1 = 1;
            else if (mfast == &motor2)
                data1 = 2;

            SendAck(data1, data2, data3, cmd);
            break;

        case 26: // команда переключения номера мотора для параметров медленного кадра
            if (data2 == 3333)
            {
                if (data1 == 1)
                {
                    mK60            = &motor1;
                    sbit.bit.motK60 = 0;
                }
                else if (data1 == 2)
                {
                    mK60            = &motor2;
                    sbit.bit.motK60 = 1;
                }
            }

            if (mK60 == &motor1)
                data1 = 1;
            else if (mK60 == &motor2)
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
                motor1.ref.sMax         = (long)(data1) * 100000;
                motor1.mbit.bit.sMax_On = data3; // включить или выключить ограничение по скольжению
            }

            data3 = (motor1.mbit.bit.sMax_On) ? 1 : 0;
            SendAck((uint16_t)(motor1.ref.sMax / 100000), data2, data3, cmd);
            break;

        case 51:
            if (data2 == 3333)
            {
                motor2.ref.sMax         = (long)(data1) * 100000;
                motor2.mbit.bit.sMax_On = data3; // включить или выключить ограничение по скольжению
            }

            data3 = (motor2.mbit.bit.sMax_On) ? 1 : 0;
            SendAck((uint16_t)(motor2.ref.sMax / 100000), data2, data3, cmd);
            break;

        case 52: // сброс интегральных составляющих пи- регуляторов
            motor1.piRPM.i1  = 0;
            motor1.piIq.i1   = 0;
            motor1.piId.i1   = 0;
            motor1.piIm.i1   = 0;
            motor1.piEPSI.i1 = 0;
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
                motStrob = (data1 == 1) ? &motor1 : &motor2;
                qepRegs  = (data1 == 1) ? &EQep1Regs : &EQep2Regs;
            }
            data1 = (motStrob == &motor1) ? 1 : 2;
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
        case 60:
            motor1.mbit.bit.Gen_Engine_SW = data1;
            break;

        case 61:
            motor2.mbit.bit.Gen_Engine_SW = data1;
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

            
        case 70:                     // запуск калибровки нуля выбранных АЦП каналов
            adcCalStart.all = data1; // задаёт список АЦП каналов
                                     //   DacaRegs.DACVALS.all = 2048;  // dbg
                                     //   DacbRegs.DACVALS.all = 2048;  // dbg

#if dbgIfull
            motor1.imit.ImF = 0;
            motor2.imit.ImF = 0;
#endif
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
                motor1.ref.sAbs      = (long)data1;
                motor1.mode.bit.load = data3;
            }

            //     data3 = motor1.mode.bit.load;
            SendAck((uint16_t)motor1.ref.sAbs, data2, motor1.mode.bit.load, cmd);
            break;

        case 74:
            if (data2 == 3333)
                ;

            SendAck(data1, data2, data3, cmd);
            break;

        case 75: // приём mREF.gain1

            Gain_Selector(&motor1, data1, data2, data3, cmd);
            break;

        case 76: // приём REF.gain2
            Gain_Selector(&motor2, data1, data2, data3, cmd);

            break;

        case 77: // приём sAbs
            if (data2 == 3333)
            {
                motor2.ref.sAbs      = (long)data1;
                motor2.mode.bit.load = data3;
            }
            data3 = motor2.mode.bit.load;
            SendAck((uint16_t)motor2.ref.sAbs, data2, motor2.mode.bit.load, cmd);
            break;

            //------------------------------------------------------------------------------------------------
        case 78:
            //        pre_bl();
            break;

        case 79:
            if (data2 == 3333)
            {
                motor1.mode.bit.load = data1;
            }
            SendAck(motor1.mode.bit.load, data2, data3, cmd);
            break;

        case 80:
            if (data2 == 3333)
            {
                motor2.mode.bit.load = data1;
            }
            SendAck(motor2.mode.bit.load, data2, data3, cmd);
            break;
            //----------------------------------------------------------------------------------------------------------------------------------

        case 810: // меняем тип двигателя для выбранного (mot28) АИН, если выключен  PWM
            if (motor1.flag.bit.pwm_On == 0)
            {
                motor1.typeID.motID     = data1;
                motor1.typeID.toothNum  = data2;
                motor1.typeID.pole_pair = data3;
#if (QEP1_Sensor != 0)
                //           QEP_Init(&motor1.qep, &EQep1Regs);
#endif
            }
            SendAck(motor1.typeID.motID, motor1.typeID.pole_pair, motor1.typeID.pole_pair, cmd);
            break;
        case 811: // меняем параметры двигателя для АИН-1
            motor1.typeID.Rs = data1;
            motor1.typeID.Rr = data2;
            motor1.typeID.Ls = data3;
            SendAck(motor1.typeID.motID, motor1.typeID.Rr, motor1.typeID.Ls, cmd);
            break;
        case 812: // меняем параметры двигателя для АИН-1
            motor1.typeID.Lr   = data1;
            motor1.typeID.Lm   = data2;
            motor1.typeID.Inom = data3;
            SendAck(motor1.typeID.motID, motor1.typeID.Lm, motor1.typeID.Inom, cmd);
            break;

        case 813: // меняем параметры двигателя для АИН-1
            motor1.typeID.Unom = data1;
            motor1.typeID.Fnom = data2;
            motor1.typeID.Pnom = data3;
            SendAck(motor1.typeID.motID, 0, 0, cmd);

            change_motID(&motor1);

#if aci_fe
            ACIFE_CONST_CALC(&motor1);
#endif
            break;
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
            if (motor2.flag.bit.pwm_On == 0) //
            {
                motor2.typeID.motID     = data1;
                motor2.typeID.toothNum  = data2;
                motor2.typeID.pole_pair = data3;
            }
            SendAck(motor2.typeID.motID, motor2.typeID.pole_pair, motor2.typeID.pole_pair, cmd);
            break;

        case 821: // меняем параметры двигателя для АИН-1
            motor2.typeID.Rs = data1;
            motor2.typeID.Rr = data2;
            motor2.typeID.Ls = data3;
            SendAck(motor2.typeID.motID, motor2.typeID.Rr, motor2.typeID.Ls, cmd);
            break;
        case 822: // меняем параметры двигателя для АИН-1
            motor2.typeID.Lr   = data1;
            motor2.typeID.Lm   = data2;
            motor2.typeID.Inom = data3;
            SendAck(motor2.typeID.motID, motor2.typeID.Lm, motor2.typeID.Inom, cmd);
            break;
        case 823: // меняем параметры двигателя для АИН-2
            motor2.typeID.Unom = data1;
            motor2.typeID.Fnom = data2;
            motor2.typeID.Pnom = data3;

            SendAck(motor2.typeID.motID, 0, 0, cmd);

            change_motID(&motor2);
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

                if (mot28 == &motor1)
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
                motor1.ref.Iq = (float)data1;
            if (data2 == 5555)
                motor1.mbit.bit.Iq_ref = data3;
            data3 = (motor1.mbit.bit.Iq_ref) ? 1 : 0;
            SendAck(rounding(motor1.ref.Iq), data2, data3, cmd);
            break;

        case 1921:
            if (data2 == 3333)
                motor2.ref.Iq = (float)data1;
            if (data2 == 5555)
                motor2.mbit.bit.Iq_ref = data3;
            data3 = (motor2.mbit.bit.Iq_ref) ? 1 : 0;
            SendAck(rounding(motor2.ref.Iq), data2, data3, cmd);
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
                motor1.piIq.Kp = (float)data1 / 1000;
                motor2.piIq.Kp = (float)data1 / 1000;
            }
            SendAck(rounding(motor1.piIq.Kp * 1000.0f), data2, data3, cmd);
            break;

        case 195:
            if (data2 == 3333)
            {
                motor1.piIq.Ki = (float)data1 / 1000;
                motor2.piIq.Ki = (float)data1 / 1000;
            }
            SendAck(rounding(motor1.piIq.Ki * 1000.0f), data2, data3, cmd);
            break;

        case 196:
            if (data2 == 3333)
            {
                motor1.piId.Kp = (float)data1 / 1000.0f;
                motor2.piId.Kp = (float)data1 / 1000.0f;
            }
            SendAck(rounding(motor1.piId.Kp * 1000.0f), data2, data3, cmd);
            break;

        case 197:
            if (data2 == 3333)
            {
                motor1.piId.Ki = (float)data1 / 1000;
                motor2.piId.Ki = (float)data1 / 1000;
            }
            SendAck(rounding(motor1.piId.Ki * 1000.0f), data2, data3, cmd);
            break;
            //------------------------------------------------------------------
            // установка коэф. усиления каналов АЦП через таблицу
        case 198:
            if (data2 == 3333)
            {
                kadc.Ia             = data1;
                kadcf.Ia            = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
            }
            SendAck(kadc.Ia, data2, data3, cmd);
            break;

        case 199:
            if (data2 == 3333)
            {
                kadc.Ib             = data1;
                kadcf.Ib            = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
            }
            SendAck(kadc.Ib, data2, data3, cmd);
            break;

        case 200:
            if (data2 == 3333)
            {
                kadc.Idc            = data1;
                kadcf.Idc           = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
            }
            SendAck(kadc.Idc, data2, data3, cmd);
            break;

        case 201:
            if (data2 == 3333)
            {
                kadc.crc = data1;
            }
            SendAck(kadc.crc, data2, data3, cmd);
            break;

        case 202:
            if (data2 == 3333)
            {
                kadc.Udc            = data1;
                kadcf.Udc           = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
            }
            SendAck(kadc.Udc, data2, data3, cmd);
            break;

        case 203:
            if (data2 == 3333)
            {
                kadc.Vin            = data1;
                kadcf.Vin           = (float)data1 / 10000;
                kadc.crc            = ControlSum(&kadc);
                eeprom.bit.SaveCoef = 1;
            }
            SendAck(kadc.Vin, data2, data3, cmd);
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

            //---------------------------------------------------------------------
            // data1 - значение
            // data2 - флаг чтения/записи - 3333 - запись
            // data3 - Мод

            //---------------------------------------------------------------------

        case 214:
            if (data2 == 3333)
                motor2.clark.kUa = (float)data1 / 100;
            SendAck(rounding(motor2.clark.kUa * 100), data2, data3, cmd);
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
                motor2.rg.Out = (float)data1 / 180 * PI;
            if (data2 == 5555)
                motor2.mbit.bit.rgfix = data3;
            data3 = (motor2.mbit.bit.rgfix) ? 1 : 0;
            SendAck(rounding(motor2.rg.Out * 180 / PI), data2, data3, cmd);
            break;

        case 222:
            if (data2 == 3333)
                motor1.rg.Out = (float)data1 / 180 * PI;
            if (data2 == 5555)
                motor1.mbit.bit.rgfix = data3;
            data3 = (motor1.mbit.bit.rgfix) ? 1 : 0;
            SendAck(rounding(motor1.rg.Out * 180 / PI), data2, data3, cmd);
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
                motor1.piIm.Umax = (float)data1;
                motor1.piIm.Umin = 0; //-(float) data1 / 1000;
            }
            SendAck(rounding(motor1.piIm.Umax), data2, data3, cmd);
            break;
        case 244:
            if (data2 == 3333)
                motor1.piIm.Ki = (float)data1 / 1000;
            SendAck(rounding(motor1.piIm.Ki * 1000), data2, data3, cmd);
            break;
        case 245:
            if (data2 == 3333)
                motor1.piIm.dumax = (float)data1;
            SendAck(rounding(motor1.piIm.dumax), data2, data3, cmd);
            break;
        case 246:
            if (data2 == 3333)
                motor1.piIm.Kp = (float)data1 / 1000;
            SendAck(rounding(motor1.piIm.Kp * 1000), data2, data3, cmd);
            break;
        case 247:
            if (data2 == 3333)
                motor1.ref.Im = (float)data1 / 1000;
            if (data2 == 5555)
                motor1.mbit.bit.Im = data3;
            data3 = (mot28->mbit.bit.Im) ? 1 : 0;
            SendAck(rounding(motor1.ref.Im * 1000), data2, data3, cmd);
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
    Udc_rf = 0;
    Idc_rf = 0;

    Idc_sum = 0;

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

/*
 * Calc_Field_motor1.qep.c
 *
 *  Created on: 28 авг. 2015 г.
 *      Author: olegs.pavlovskis
 */

//#include "PeripheralHeaderIncludes.h"
#include "f28p55x_device.h"
/* Включает в себя:
 * 1)
 * 2) Важнейшие asm. команды (EINT, EALLOW, etc.)
 * 3) Маски для битов (от BIT0 до BIT31) и целых чисел.
 * 4) Определены основные типы данных и включены все header файлы всей периферии для 335 процессора
 */

#include "QEP_RPM.h"       // Программа для одноканального датчика оборотов
#include "PARAMS_INOUT.h"                       // Определяет структуру входных данных
#include "PARAMS_ModeSwitch.h"
#include "Motor.h"
#include "FLAGZ.h"

//void freq_Ref(motor *m);
void hold_RPM(motor *m, long ref, long real); // Функция подтяжки оборотов к заданным
void pi_reg(PI_CONTROLLER *v);
void F_ramp(motor *m);
extern long saturlong(long input, long high, long low);

extern REFs REF;
extern union PCflag sharp;



int hold_rpm_max = 300; // максимальная добавка регулятора оборотов
int hold_rpm = 0; // Добавка к уставке оборотов двигателя в режиме стабилизации RPM вала спарки 0.1 об/мин

#define Sabs_Mode    0
#define Sproc_Mode   1
#define Srpm_Mode    2

void freq_Ref(motor *m)
{
    //----------------------------------------------------------------------------------------------------------------
    long fref;      // заданная частота поля без скольжения в 0.01 об/мин
    //   long rpm;       // текущие обороты ротора в 0.01 об/мин

    if (sharp.bit.KVU_On)
        fref = REF.speedRef_KVU * 100L;
    else if (m->mode.bit.load != 8) // 8- режим независимого задания оборотов каждому АИНу
    {
        fref = REF.speedRef * 100L; // режим прибавления скольжения относительно заданной частоты магнитного поля
        //       if (m->mode.bit.LL == 0)

        if (m->mode.bit.LL == 1)
            fref = labs((long) (m->qep.n3 * 100.0f + 0.5f)); // режим прибавления скольжения относительно скорости вращения ротора
    }
    else
        fref = m->ref.sAbs * 100L; // 8- режим независимого задания оборотов каждому АИНу
//----------------------------------------------------------------------------
    if (m->mode.bit.load == 0)
        m->ref.slip = m->ref.sAbs;
    else
        m->ref.slip = 0;

//---------------------------------------------------------------
    if (m->mbit.bit.Gen_Engine_SW == 0)
    {
        fref = fref + m->ref.slip; // Двигатель
    }
    else
    {
        fref = fref - m->ref.slip;  // Генератор
    }
//------------------------------------------------------------

    if (m->mbit.bit.revers == 0)
    {
        m->ramp.ref = fref;
    }
    else
    {
        m->ramp.ref = -fref;
    }
//--------------------------------------------------------------

    F_ramp(m);

}

/****************************************************************************************************
 *                           Функция подтяжки оборотов                                              *
 ****************************************************************************************************/

void hold_RPM(motor *m, long ref, long real)
{
    long a = ref - real;

    if (a > 2)          // Если измеренные обороты < заданных
    {
        m->slip.sRPM++;        // ---> увеличиваем скольжение
        if (m->slip.sRPM > hold_rpm_max)
            m->slip.sRPM = hold_rpm_max;
    }
    else
    {
        if (a < -2)      // Если заданных об. > об.вал
        {
            m->slip.sRPM--;        // ---> уменьшаем скольжение
            if (m->slip.sRPM < -hold_rpm_max)
                m->slip.sRPM = -hold_rpm_max;
        }
    }

}
//---------------------------- END podtjazka --------------------------------------------------------------------

void F_ramp(motor *m)
{

// все внутреннии вычисления делаются в масштабе об/мин * 100 000
    long dref = m->ramp.ref * 1000L;  // приведение к об/мин * 100 000
    long dsHi;
    long dsLow;

    long df = dref - m->ramp.dout; // предельная частота поля ограниченная максимальным скольжением

    if (labs(df) > 0)
    {
        if (m->mbit.bit.sMax_On == 1)
        {
            m->ramp.rpm = m->qep.n3 * 100000L;
            dsHi = m->ramp.rpm + m->ref.sMax;
            dsLow = m->ramp.rpm - m->ref.sMax;
            dref = saturlong(dref, dsHi, dsLow);
        }

        if (sharp.bit.ramp_Off == 0)
        {
            if (dref >= m->ramp.dout)
                m->ramp.dout = m->ramp.dout + REF.ramp_step;
            else
                m->ramp.dout = m->ramp.dout - REF.ramp_step;
        }
        else
            m->ramp.dout = dref;

    }

    m->rg.Freq = (float) m->ramp.dout / 100000.0f;

}

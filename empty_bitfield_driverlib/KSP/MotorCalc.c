/*
 * MotorCalc.c
 *
 *  Created on: 14 Nov 2019
 *      Author: Oleg Pavlovsky
 */
#include "board.h"
#include "math.h"

#include "KSP.h"
#include "cla_shared.h"
#include "myADC.h"
#include "myDCAN.h"

int   pwm_On = 0; // для выключения ПИ- регуляторов при выключенных IGBT
float asut(float a);
void  model2(motor *m);

void change_motID(motor *v);

void RMS2(motor *v);

extern motor *motStrob;
extern int    qepStrob;

#if Strob_rg //---------from MBOX28 -----------------------
float rgOld = 0;
#endif //   Strob_rg -------------------

extern int   rgStrob_enable;
extern float rgStrob;
extern REFs  REF;

//uint16_t  dac0Val   = 2047;
//uint16_t  dac1Val   = 2047;
float32_t magnitude = 2047;
float32_t kVa       = 5.0f; //


//-------------------------------------------------------------------------------------------------------------
static inline void svgen(motor *v)
{

    float tmp1, tmp2, tmp3;

    tmp1 = v->ipark.Beta;
    tmp2 = v->ipark.Beta / 2.0f + 0.866f * v->ipark.Alpha;
    tmp3 = tmp2 - tmp1;

    v->svgen.VecSector = 3;
    v->svgen.VecSector = (tmp2 > 0) ? (v->svgen.VecSector - 1) : v->svgen.VecSector;
    v->svgen.VecSector = (tmp3 > 0) ? (v->svgen.VecSector - 1) : v->svgen.VecSector;
    v->svgen.VecSector = (tmp1 < 0) ? (7 - v->svgen.VecSector) : v->svgen.VecSector;

    if (v->svgen.VecSector == 1 || v->svgen.VecSector == 4)
    {
        v->svgen.Ta = tmp2;
        v->svgen.Tb = tmp1 - tmp3;
        v->svgen.Tc = -tmp2;
    }

    else if (v->svgen.VecSector == 2 || v->svgen.VecSector == 5)
    {
        v->svgen.Ta = tmp3 + tmp2;
        v->svgen.Tb = tmp1;
        v->svgen.Tc = -tmp1;
    }

    else
    {
        v->svgen.Ta = tmp3;
        v->svgen.Tb = -tmp3;
        v->svgen.Tc = -(tmp1 + tmp2);
    }
}

static inline void VOLT_faza_CALC(motor *m)
{
    float tmp = Udc_volt / 6.0f;

    c2p.Va_volt  = tmp * (m->svgen.Ta * 2 - m->svgen.Tb - m->svgen.Tc);
    c2p.Vb_volt  = tmp * (m->svgen.Tb * 2 - m->svgen.Ta - m->svgen.Tc);
    c2p.Vc_volt  = tmp * (m->svgen.Tc * 2 - m->svgen.Ta - m->svgen.Tb);
  //  m->Vab = m->Va - m->Vb;
}

//#pragma CODE_SECTION(MotorCalc, ".TI.ramfunc")
void MotorCalc(motor *m)
{

// Преобразование измеренных мгновенных значений токов из ацп кода в физику.
#if dbgIfull == 0
 //   fizika(m);

#else
 //   tok_immit(m);
#endif
    //-------------------------------------------------------------------

    freq_Ref(m);
    clark(m);

    //--------------------------------------------------------------------------------------------------
    // Вот здесь должно быть вычисление нового угла положения магнитной оси ротора
    // тело вычисления угла вектора потокосцепления ротора или угла относительно которого задаются напряжения.

    if (m->mbit.bit.rgfix == 0)
    {

        if (m->mbit.bit.FOC_mode == 0) // выключён режим векторного управления
        {
            m->freq = m->rg.Freq;
            float a = m->rg.Out + (m->freq * m->rg.k_AF);
            if (a > PI)
            {
                a          = a - PI2;
                m->rg.flag = 1;
            }
            if (a < -PI)
            {
                a          = a + PI2;
                m->rg.flag = 1;
            }

            m->rg.Out = a;
        }
        else // включён режим векторного управления
        {
        }
    } // end   if (m->mbit.bit.rgfix == 0)
    //--------------------------------------------------------------------------------------------------
    m->Sine   = sinf(m->rg.Out);
    m->Cosine = cosf(m->rg.Out);

    //  иммитация токов  через модули DAC0- iA, DAC1-iB

 //   dac0Val                      = 2048 + (int16_t)(c2p.Va_volt * kVa);
 //   dac1Val                      = 2048 + (int16_t)(c2p.Vb_volt * kVa);
 //   DacaRegs.DACVALS.bit.DACVALS = dac0Val; // вывод на A0, B15, C15 ,DACB_OUT -> HSEC pin 9
 //   DacbRegs.DACVALS.bit.DACVALS = dac1Val; // вывод на A1, B7, DACB_OUT HSEC pin 11

#if Strob_rg

    if (m == motStrob)
    {
        if (rgStrob_enable == 1)
        {
            if (m->mbit.bit.revers == 0) // положительное вращение
            {
                if (m->rg.Out >= rgStrob)
                    if (rgOld < rgStrob)
                    {
                        GpioDataRegs.GPBSET.bit.GPIO33 = 1; // подаём импульс на XP5-3
                    }
            }
            else // отрицательное вращение
            {
                if (m->rg.Out <= rgStrob)
                    if (rgOld > rgStrob)
                    {
                        GpioDataRegs.GPBSET.bit.GPIO33 = 1; // подаём импульс на XP5-3
                    }
            }
        }
        rgOld = m->rg.Out;
    }
#endif // end Strob_rg

    clark(m);

//CLA_forceTasks(CLA1_BASE,CLA_TASKFLAG_7);


//---------------------------------------------------------------------------------------------------

    //---------------------------------------------------------------------------------------------

    //currentTest(m);
    pwm_On = m->flag.bit.pwm_On; // признак для инициализации ПИ- регуляторов

    if (m->mbit.bit.FOC_mode == 1) // включён режим векторного управления
    {
    }
    else
    {
        m->park.Cosine = cosf(m->rg.Out);
        m->park.Sine   = sinf(m->rg.Out);
    }

    park(m);

    slip(m);

    //-----------------------------------------------------------------------------------------

    //----------------------------------------------------------------------------------------------

    /* при векторном управлении на входы ( Vq,Vd) ipark. преобразователя подаём сигналы с выхода
     * соответствующего PI - регулятора или заданный gain в зависимости от динамических ключей управления
     */

    // вычисляет предельно возможные значения Udmax  Uqmax при текущем угле вращающейся системы
    // координат и напряжении звена постоянного тока. Модуль результирующего вектора напряжения не должен
    // быть больше амплитуды фазного напряжения то есть |U| <=  Um_max. Отсюда вычисляем ограничение по осям d и q
    m->piId.Umax = c2p.Um_max;
    m->piId.Umin = -m->piId.Umax;

    {
        float Urms; // амплитуда фазного напряжения

        m->flag.bit.Id_sat = 0;

        switch (m->mode.bit.umode) // выбираем режим движка gain
        {

            case umode_gain:
                Urms = m->ref.gain * Udc_volt / sqrt3;
                Um_Lim(m, Urms);
                break;

            case umode_UF:
                Urms = fabsf(m->rg.Freq * m->ref.UF);
                Um_Lim(m, Urms);
                break;

            case umode_Urms:
                Urms = m->ref.Urms;
                Um_Lim(m, Urms);
                break;

            case umode_Id:
                m->piId.Ref = m->ref.Id;
                Id_reg(m);
                break;

            case umode_Im:
                Im_reg(m);

                break;
        }
    }

    m->piIq.Umax = sqrtf(c2p.Um_max * c2p.Um_max - m->ipark.Vd * m->ipark.Vd);
    m->piIq.Umin = -m->piIq.Umax;

    float RPMref = (m->mbit.bit.revers) ? (-REF.speedRef) : REF.speedRef;

    switch (m->mode.bit.load)
    {
        case lmode_Sabs:
        case lmode_Sproc:
        case lmode_Srpm:

            m->ipark.Vq          = 0;
            m->flag.bit.Iq_sat   = 0;
            m->flag.bit.Irpm_sat = 0;

            break;

        case lmode_Iq:
            m->piIq.Ref = (m->mbit.bit.revers) ? (-m->ref.sAbs / 10.0f) : m->ref.sAbs / 10.0f;
            Iq_reg(m);
            break;

        case lmode_Iqd:
            m->piIq.Ref = m->piId.Ref; // ref.sAbs -> epsi
            Iq_reg(m);
            break;

        case lmode_Iq_rpm:
            //        m->piRPM.Ref = RPMref;
            //        m->piRPM.Fbk = m->qep.n3;
            //        Irpm_reg(m);
            break;

        case lmode_Iq_freq:
            m->piRPM.Ref = RPMref;
            m->piRPM.Fbk = m->freq;
            Irpm_reg(m);
            break;
    }

    //============================IPARK  =========================================================

    if (Udc_rf > 1)
    {

        m->ipark.puVd        = saturation(m->ipark.Vd / c2p.Um_max, 1.0, -1.0);
        m->flag.bit.puVd_sat = satFlag;

        m->ipark.puVq        = saturation(m->ipark.Vq / c2p.Um_max, 1.0, -1.0);
        m->flag.bit.puVq_sat = satFlag;
    }
    else
    {
        m->ipark.puVd = 0;
        m->ipark.puVq = 0;
    }

    m->ipark.Alpha = (m->ipark.puVd * m->Cosine) - (m->ipark.puVq * m->Sine);
    m->ipark.Beta  = (m->ipark.puVq * m->Cosine) + (m->ipark.puVd * m->Sine);

    m->ipark.gain = sqrt(m->ipark.Alpha * m->ipark.Alpha + m->ipark.Beta * m->ipark.Beta);

    svgen(m);
    VOLT_faza_CALC(m);

#if Strob_rg
    GpioDataRegs.GPBCLEAR.bit.GPIO33 = 1; // сбрасываем  импульс на XP5-3
#endif                                    // end Strob_rg
}

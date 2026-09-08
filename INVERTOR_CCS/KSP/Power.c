/*
 * Power.c
 *
 *  Created on: 06 марта 2015 г.
 *      Author: Olegs.Pavlovskis
 */

#include <complex.h> /* Standard Library of Complex Numbers */

#include <math.h>
#include <stdio.h>

#include "config_switch.h"
#include "ErrorCodes.h"
#include "FLAGZ.h"
#include "Konstanty.h"
#include "PARAMS_INOUT.h"
#include "VOLT_CALC_CLA.h"

#include "motor.h"

#include <myADC.h>
#include "cla_shared.h"
#include "Functions.h"



void SRD_Init(motor *m);
void PUSK_Init(motor *v);
void change_motID(motor *v);

void pi_Init(PI_CONTROLLER *v);
void piId_Init(motor *m);
void piIq_Init(motor *m);
void piSRPM_Init(motor *m);
void piRPM_Init(motor *m);
float asut(float a);

void IGBT1_Disable(void);
void IGBT2_Disable(void);


float saturation(float input, float high, float low);

void GenKSP_VToff(void);

float K1 = 0.5; // dbg
float K2 = 0.5; // dbg

float TTimeT;
extern REFs REF;
extern union PCflag sharp;
extern union SHARP2 sharp2;
//-------------------------------------------------------------------------------------
extern int VTGen_On;
//------------------------------------------------------------------------------

extern int pwm_On;
int rgCount = 0;
float eps_i = 0; // интегральная составляющия регулятора EPS_REG
float eps_p = 0; // пропорциональная составляющия регулятора EPS_REG
float eps_Kp = 0.5;
float eps_Ki = 0.5;
float epsv_max = 60.0f / 360.0f;
float epsi_max = 60.0f / 360.0f;
int epsPeriod = 100;
float emstep = 1.0f / 40960.0f; // шаг приращения EPS;
float pi_kIqd = 1;

// Instance the constant calculations for rotor flux and speed estimations

// extern const ACIFE fe_init ;
extern float Tpwm; // время ISR цикла в сукундах

///**************************************************************
/*
#ifdef _FLASH
#ifndef CMDTOOL
#pragma CODE_SECTION(RMS, ".TI.ramfunc");
#pragma CODE_SECTION(RMS2, ".TI.ramfunc");
#endif
#endif
*/
void KeyOnInit(motor *v)
{
    pi_Init(&v->piIq);
    pi_Init(&v->piId);
    pi_Init(&v->piRPM);
    pi_Init(&v->piEPSI);
    pi_Init(&v->piIm);
    pi_Init(&v->piSRPM);

    piId_Init(v);
    piIq_Init(v);
    piRPM_Init(v);
    piSRPM_Init(v);

    v->ref.parsel = 0;
    v->ref.gain = 0;
    v->ref.Urms = 0;
    v->ref.Id = 0;
    v->ref.Iq = 0;
    v->ref.Im = 0;
    v->ref.UF = 0;
    v->ref.emadd = 0;
    v->ref.lsw = 0;
    v->ref.Imax_off = 500;
    v->ref.sAbs = 0;

    v->mode.all = 0;
    v->mbit.all = 0;
    v->flag.all = 0;

    v->mbit.bit.emadd = 1;

    REF.epsi = 0;
    //   v->rg.epscontl = 0;

    v->ipark.Vd0_max = 1000;
    v->ipark.Vq0_max = 1000;
    v->park.kVd = 1 / sqrt2;
    v->park.kVq = 1 / sqrt2;

    v->typeID.kTe = 1;
}

void SRD_Init(motor *m)
{
    PUSK_Init(m);
    KeyOnInit(m);
}

//***************************************************************
void pi_Init(PI_CONTROLLER *v)
{
    v->Ref = 0; // Input: reference set-point
    v->Fbk = 0; // Input: feedback

    v->du = 0;

    v->Out = 0; // Output: controller output

    v->up = 0; // Data: proportional term
    v->ui = 0; // Data: integral term
    v->v1 = 0; // Data: pre-saturated controller output
    v->i1 = 0; // Data: integrator storage: ui(k-1)
}

void piId_Init(motor *m)
{
    m->piId.Kp = 0.1;
    m->piId.Ki = 0.002;
}
void piIq_Init(motor *m)
{
    m->piIq.Kp = 0.1;
    m->piIq.Ki = 0.002;
}

void piSRPM_Init(motor *m)
{
    m->piSRPM.Kp = 0.3;
    m->piSRPM.Ki = 0.001;

    m->piSRPM.Umax = m->ref.sMax / 100.f;
    m->piSRPM.Umin = -m->piSRPM.Umax;
}
void piRPM_Init(motor *m)
{
    m->piRPM.Kp = 0.05;
    m->piRPM.Ki = 0.001;

    m->piRPM.Umax = 300;
    m->piRPM.Umin = -300;
}

void piId_reg(PI_CONTROLLER *v)
{
    if (pwm_On == 0)
    {
        v->ui = 0;
        v->v1 = 0;
        v->Out = 0;
        satFlag = 0;
    }
    else
    {
        /* proportional term */
        v->du = v->Ref - v->Fbk;

        v->up = saturation(v->Kp * v->du, v->Umax, v->Umin);

        // integral term  ----------------------

        v->i1 = (v->Ki * v->du);
        v->v1 = v->up + v->ui + v->i1;
        v->Out = saturation(v->v1, v->Umax, v->Umin);

        if (satFlag == 0) // если вошли в насышение фиксируем интегральную составляющию
            v->ui = v->ui + v->i1;
    }
}
void pi_reg(PI_CONTROLLER *v)
{
    if (pwm_On == 0)
    {
        v->ui = 0;
        v->v1 = 0;
        v->Out = 0;
        satFlag = 0;
    }
    else
    {
        /* proportional term */
        v->up = (v->Ref - v->Fbk) * v->Kp;

        /* integral term */
        v->ui = v->up * v->Ki + v->i1;

        /* control output */
        v->v1 = v->up + v->ui;
        v->Out = saturation(v->v1, v->Umax, v->Umin);

        if (satFlag == 1) // если вошли в насышение фиксируем интегральную составляющию
        {
            v->ui = v->i1;
        }

        v->i1 = v->ui;
    }
}

//******************************************************************************
#define ck m->clark
void clark(motor *m)
{
    ck.Ua = c2p.Va_volt;
    ck.Ub = (c2p.Vb_volt - c2p.Vc_volt) / sqrt3;

    ck.Um = sqrtf(ck.Ua * ck.Ua + ck.Ub * ck.Ub);
    ck.UmF = ck.UmF * 0.999f + ck.Um * 0.001f;

    // выделяем первую гармонику напряжения со смещённым углом фазы относительно
    // rgOut
    m->rg.radian = m->rg.Out - (m->freq * 0.000392f - 0.03754f);
    ck.Va = ck.Um * cosf(m->rg.radian);
    ck.Vb = ck.Um * cosf(m->rg.radian - 2.0943951023931f);
    ck.Vc = ck.Um * cosf(m->rg.radian + 2.0943951023931f);

    ck.Ia = c2p.Ia_amp;
    ck.Ib = (c2p.Ib_amp - c2p.Ic_amp) / sqrt3;
    ck.Im = sqrtf(ck.Ia * ck.Ia + ck.Ib * ck.Ib);
    ck.ImF = ck.ImF * 0.999f + ck.Im * 0.001f;

}
#undef c


//******************************************************************************
void park(motor *v)
{

#define p v->park

    p.Id = (v->clark.Ia * p.Cosine + v->clark.Ib * p.Sine);
    p.Iq = (v->clark.Ib * p.Cosine - v->clark.Ia * p.Sine);

    p.Ud = (v->clark.Ua * p.Cosine + v->clark.Ub * p.Sine);
    p.Uq = (v->clark.Ub * p.Cosine - v->clark.Ua * p.Sine);

    p.fu = atan2f(p.Uq, p.Ud);
    p.fi = atan2f(p.Iq, p.Id);

    p.fui = asut(p.fu - p.fi);
    // filtr ----------------------------------------------

#define k1 0.999f
    p.VdF = k1 * p.VdF + (1.0F - k1) * p.Ud;
    p.VqF = k1 * p.VqF + (1.0F - k1) * p.Uq;

    p.IqF = k1 * p.IqF + (1.0F - k1) * p.Iq;
    p.IdF = k1 * p.IdF + (1.0F - k1) * p.Id;


#undef k1

    //--------------------------------------------------------------------------------------------------

#undef p
}

//-----------------------------------------------------------------------------------------------------
#if dbgIfull
#pragma CODE_SECTION(tok_immit, "ramfuncs");
void tok_immit(motor *m) {
#define s m->tsh
  // расчёт токов эквивалентной схемы замещения фазной обмотки двигателя
  complex float dEs;
  float fi;

  // m->imit.Slip=m->se.WSlip /( m->se.WPsi+1e-5);
  //  m->imit. Rslip = s.Rr;
  /*
   if (abs( m->imit.freq) >0.1)
   m->imit.Slip =( m->imit.freq - m->imit.rpm)/( m->imit.freq);
   */
  /*
     if (m->se.WPsi >= 0)
         we = m->se.WPsi + 1e-3;
     else
         we = m->se.WPsi - 1e-3;

     if (m->se.WSlip >= 0)
         wslip = m->se.WSlip + 1e-3;
     else
         wslip = m->se.WSlip - 1e-3;

     m->imit.Slip = wslip / we;
 */
  m->imit.Rslip = m->tsh.Rr; // m->imit.Slip;

  //        m->imit. Rslip = m->sh.Rr / m->imit.Slip;

  m->imit.Us = m->clark.Ua + m->clark.Ub * I;
  dEs = m->imit.Es;

  m->imit.Es = m->imit.Us - m->imit.Is * s.Rs - m->imit.dIs * s.Ls;
  m->imit.Im = m->imit.Im + (dEs + m->imit.Es) * 0.5f * Tpwm / s.Lm;
  m->imit.Ir =
      m->imit.Ir +
      (dEs + m->imit.Es - 2 * m->imit.Ir * m->imit.Rslip) * 0.5f * Tpwm / s.Lr;

  dEs = m->imit.Is;
  m->imit.Is = m->imit.Im + m->imit.Ir;
  m->imit.dIs = (m->imit.Is - dEs) / Tpwm;
  m->imit.Fr = -s.Lrm * m->imit.Ir + s.Lm * m->imit.Is;
  m->imit.Fm = s.Lm * m->imit.Im;
  m->imit.A_Fr = cargf(m->imit.Fr);

  dEs = m->imit.A_Im;
  m->imit.A_Im = cargf(m->imit.Im);
  m->imit.We = asut(m->imit.A_Im - dEs) / Tpwm;
  m->imit.freq = m->imit.We / m->tsh.kw;

  m->imit.A_Ir = cargf(m->imit.Ir);
  m->imit.Irm = cabsf(m->imit.Ir);
  //    m->imit.Te =  3/2* m->typeID.pole_pair *cabs( m->imit.Ir) *
  //    cabs(m->imit.Fm) *sin(m->imit.A_Ir - m->imit.A_Im);
  m->imit.Te = 3 / 2 * m->typeID.pole_pair *
               (-creal(m->imit.Ir) * cimagf(m->imit.Fm) +
                crealf(m->imit.Fm) * cimagf(m->imit.Ir));
  m->imit.M = m->imit.rpm;
  m->imit.rpm = m->imit.rpm + (m->imit.Te - m->imit.M) / m->typeID.J * Tpwm;
  fi = cargf(m->imit.Is);
  m->imit.ImF = cabsf(m->imit.Is);

  m->cla.c2p.I_amp = m->imit.ImF * cosf(fi);
  m->Ib_amp = m->imit.ImF * cosf(fi - ph120);
  m->Ic_amp = m->imit.ImF * cosf(fi + ph120);

#undef s
}

#else


#endif

//--------------------------------------------------------------------------------------------------------------------
/* pi_reg2 аналог pi_reg
 * отличается дополнительными параметрами для анализа работы ПИ-регулятора при
 * записи в быстрый кадр
 */
void pi_reg2(PI_CONTROLLER *v)
{
    if (pwm_On == 0)
    {
        v->ui = 0;
        v->v1 = 0;
        v->Out = 0;
        satFlag = 0;
    }
    else
    {
        /* proportional term */
        v->du = v->Ref - v->Fbk;
        v->up = v->Kp * v->du;
        v->ui = v->Ki * v->up + v->i1;
        // integral term  ----------------------

        v->v1 = v->up + v->ui;
        v->Out = saturation(v->v1, v->Umax, v->Umin);

        if (satFlag == 1) // если вошли в насышение фиксируем интегральную составляющию
        {
            v->ui = v->i1;
        }
        v->i1 = v->ui;
    }
}

//-----------------------------------------------------------------------------------------------
/*
 void svgen(motor *v)
 {

 float tmp1, tmp2, tmp3;

 tmp1 = v->ipark.Beta;
 tmp2 = v->ipark.Beta / 2.0 + 0.866 * v->ipark.Alpha;
 tmp3 = tmp2 - tmp1;

 v->svgen.VecSector = 3;
 v->svgen.VecSector = (tmp2 > 0) ? (v->svgen.VecSector - 1) :
 v->svgen.VecSector; v->svgen.VecSector = (tmp3 > 0) ? (v->svgen.VecSector - 1)
 : v->svgen.VecSector; v->svgen.VecSector = (tmp1 < 0) ? (7 -
 v->svgen.VecSector) : v->svgen.VecSector;

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


 v->pwmgen.MfuncC1 = v->svgen.Ta;
 v->pwmgen.MfuncC2 = v->svgen.Tb;
 v->pwmgen.MfuncC3 = v->svgen.Tc;


 }
 */

//---------------------------------------------------------------------------
void slip(motor *m)
{
    float rpm;
    float field = fabsf(m->freq); // частота поля приведённая к оборотам ротора


    rpm = fabs(m->qep.n3);


    m->slip.abs = field - rpm;
    //    m->slip.abs = (rpm<0)?  -slip : slip;

    m->slip.absF = 0.95 * m->slip.absF + 0.05 * m->slip.abs;

    if (fabsf(field) > 1)
        m->slip.procF = (m->slip.absF / field) * 100.0;
    else
        m->slip.procF = 0;
}

//---------------------------------------------------------------------------------------------
/* Вычисляет фазные напряжения в зависимости от напряжения DC и коэф. PWM
 * модуляции*/
/*
 void VOLT_faza_CALC(motor *m)
 {

 float ku01 = m->clark.ku01;
 float pu01 = m->pu01;
 float ua0, ub0, ua1, ub1;

 //    m->Va0 = m->Va1;
 m->Vb0 = m->Vb1;
 m->Vc0 = m->Vc1;
 m->clark.Ua0 = m->clark.Ua1;
 m->clark.Ub0 = m->clark.Ub1;

 m->Va1 = m->Va;
 m->Vb1 = m->Vb;
 m->Vc1 = m->Vc;
 m->clark.Ua1 = m->clark.Ua;
 m->clark.Ub1 = m->clark.Ub;

 //    m->Va01 = m->Va0 * pu01 + m->Va1 * (1 - pu01);
 m->Vb01 = m->Vb0 * pu01 + m->Vb1 * (1 - pu01);
 m->Vc01 = m->Vc0 * pu01 + m->Vc1 * (1 - pu01);

 //алгоритм с компенсацией мёртвого времени
 float dtCor = m->pwmgen.Dead_time;

 float tmp = cla.Udc_volt / 6.0;

 float Ta1 = m->cla.c2p.I_amp>0 ? m->svgen.Ta -dtCor : m->svgen.Ta +dtCor;
 float Tb1 = m->Ib_amp>0 ? m->svgen.Tb -dtCor : m->svgen.Tb +dtCor;
 float Tc1 = m->Ic_amp>0 ? m->svgen.Tc -dtCor : m->svgen.Tc +dtCor;

 m->Va0 = tmp*(m->svgen.Ta*2-m->svgen.Tb -m->svgen.Tc);
 m->Va = tmp*(Ta1 * 2 - Tb1 - Tc1);
 m->Vb = tmp*(Tb1 * 2 - Ta1 - Tc1);
 m->Vc = tmp*(Tc1 * 2 - Ta1 - Tb1);
 m->Vab =m->Va - m->Vb;

 m->clark.Ua = m->Va;
 m->clark.Ub = (m->Vb - m->Vc) / sqrt3;
 //---------------------------------------------------------------------------------------
 switch (m->delay)
 {
 case 0:
 ua0 = m->clark.Ua0;
 ub0 = m->clark.Ub0;
 ua1 = m->clark.Ua1;
 ub1 = m->clark.Ub1;
 break;

 case 1:
 ua0 = m->clark.Ua1;
 ub0 = m->clark.Ub1;
 ua1 = m->clark.Ua;
 ub1 = m->clark.Ub;
 break;
 default:
 break;
 }
 m->clark.Ua01 = ua0 * ku01 + ua1 * (1 - ku01);
 m->clark.Ub01 = ub0 * ku01 + ub1 * (1 - ku01);

 float fu = m->rg.Out - 0.20944 * m->freq*m->dwait;
 m->Vat = m->clark.Um*cos(fu);
 m->Vbt = m->clark.Um*cos(fu - 2.094395);
 m->Vct = m->clark.Um*cos(fu + 2.094395);

 float    fui = asut(fu -  m->clark.fi);
 //     if (m->freq < 0) ck.fui = -ck.fui;

 m->clark.fuiF =  m->clark.fuiF * 0.999 + fui * 0.001;
 m->Iat = m->clark.ImF*cos(fu -  m->clark.fuiF);
 m->Ibt = m->clark.ImF*cos(fu -  m->clark.fuiF- 2.094395);
 m->Ict = m->clark.ImF*cos(fu -  m->clark.fuiF + 2.094395);
 //--------------------------------------------------------------------------------------------

 }
 */

//-------------------------  POROG_REG
//------------------------------------------------------------------
/* реализует алгоритм следящей системы. Имеется цель, текущие значение параметра
 и коридоры допуска
 * широкий и узкий. Если параметр выходит за границы широкого допуска то допуск
 меняется на узкий и включается
 * регулятор, который увеличавает или уменьшает параметр воздействия в
 зависимости от знака отклонения параметра от цели.
 * Как только параметр попадает в узкий коридор, коридор меняется на широкий и и
 регулятор выключается.
 * Узкий коридор может быть равен нулю.
 * wide   -input    устанавливает величину широкого коридора
 * narrow -input  устанавливает величину узкого коридора
 * step   -input     устанавливает шаг инкримента/декремента регулятора
 *

 void porog_reg(float par, float target, float wide, float narrow, float step,
 porog *v)
 {
 float delta = par - target;
 if (delta > v->porog)      // выше коридора допуска
 {
 v->porog = narrow;
 }

 }
 */


//--------------------------------------------------------------------------------------------------
// держим Im to Im_ref за счёт изменения Urms
void Im_reg(motor *m)
{
    m->piIm.Ref = m->ref.Im;

    m->piIm.Fbk = m->clark.Im;


    m->piIm.Fbk = m->clark.Im;
    m->piIm.Umax = c2p.Um_max;
    m->piIm.Umin = -m->piIm.Umax;
    pi_reg(&m->piIm);
    m->flag.bit.Im_sat = satFlag;
    m->ipark.Vd = m->piIm.Out;
    m->ipark.Vq = 0;
}

//--------------------------------------------------------------------------------------------------
// держим Id to Id_ref за счёт изменения m->ipark.Vd
void Id_reg(motor *m)
{
#if KSP_GEN
  if (VTGen_On == 0)
    m->piId.Fbk =
        m->park
            .Id; // не обновляем во время импульса тока возбуждения генератора
#else
    m->piId.Fbk = m->park.Id;
#endif

    pi_reg2(&m->piId);
    m->flag.bit.Id_sat = satFlag;
    //   m->ipark.Vd = -m->piId.Out + m->park.Ed;
    m->ipark.Vd = m->piId.Out;
}

//--------------------------------------------------------------------------------------------------
// держим Iq to Iq_ref за счёт изменения m->ipark.Vq
void Iq_reg(motor *m)
{
#if KSP_GEN
  if (VTGen_On == 0)
    m->piIq.Fbk =
        m->park
            .Iq; // не обновляем во время импульса тока возбуждения генератора
#else
    m->piIq.Fbk = m->park.Iq;
#endif

    pi_reg2(&m->piIq);
    m->flag.bit.Iq_sat = satFlag;
    //    m->ipark.Vq = m->piIq.Out - m->park.Ed;
    m->ipark.Vq = m->piIq.Out;
}

//--------------------------------------------------------------------------------------------------
// держим rpm to speedRef.
void Irpm_reg(motor *m)
{
    float Umax, Umax2;

    // управление максимальным током Iq, А ( ограничение крутящего момента )

    Umax = m->ref.sAbs / 10.0; // задаётся от внешнего управления
    if (m->slip.abs < 1)
        Umax2 = Umax * m->ref.piRPM_max_Gen;
    else if ((m->slip.abs > 1) || (m->flag.bit.pwm_On == 0))
        Umax2 = Umax;

    m->piRPM.Umax = Umax2;
    m->piRPM.Umin = -Umax2;

    pi_reg(&m->piRPM);
    m->flag.bit.Irpm_sat = satFlag;

    m->piIq.Ref = m->piRPM.Out;
    Iq_reg(m);
}
//--------------------------------------------------------------------------------------------------
#if SMO
void SMO_Init(motor *m) {
  // Initialize the SMOPOS
#define pm m->typeID
  /*
      pm.Rs = 24550;  // mkOm
      pm.Ls = 10;
      pm.Inom = 400;
      pm.Unom = 500;
  */
  //-----------------------------------------------------
  float Rs = pm.Rs / 1e6; // перевод в Ом
  float Ld = pm.Ls / 1e6; // pm.Ls*1e-6;   // перевод в Генри
  float Lq = pm.Lr / 1e6; // pm.Ls*1e-6;   // перевод в Генри
  float Ts = Tpwm;        // Sampling period in sec

  //--------------------------------------------------------------
  m->smo.EstIalpha = 0;
  m->smo.EstIbeta = 0;
  m->smo.Ealpha = 0;
  m->smo.Ebeta = 0;

  m->smo.Fa = 1 - Rs * Ts / Ld;
  m->smo.Fb = 1 - Rs * Ts / Lq;
  m->smo.Ga = Ts / Ld;
  m->smo.Gb = Ts / Lq;

#undef pm
}

//----------------------------------------------------------------------------------------------------
void smo_calc(motor *m) {
#define v m->smo

  /*  Sliding mode current observer   */
  v.EstIalpha = v.Fa * v.EstIalpha + v.Ga * (v.Valpha - v.Ealpha - v.Zalpha);
  v.EstIbeta = v.Fb * v.EstIbeta + v.Gb * (v.Vbeta - v.Ebeta - v.Zbeta);

  /*  Current errors  */
  v.IalphaError = v.EstIalpha - v.Ialpha;
  v.IbetaError = v.EstIbeta - v.Ibeta;

  /*  Sliding control calculator  */
  /* v.Zalpha=v.IalphaError*v.Kslide/v.E0) where E0=0.5 here*/
  v.Zalpha = saturation(v.IalphaError, v.E0, -v.E0) * 2 * v.Kslide;
  v.Zbeta = saturation(v.IbetaError, v.E0, -v.E0) * 2 * v.Kslide;

  /*  Sliding control filter -> back EMF calculator   */
  v.Ealpha = v.Ealpha + v.Kslf * (v.Zalpha - v.Ealpha);
  v.Ebeta = v.Ebeta + v.Kslf * (v.Zbeta - v.Ebeta);

  /*  Rotor angle calculator -> Theta = atan(-Ealpha,Ebeta)   */
  v.Theta = atan2(-v.Ealpha, v.Ebeta);

#undef v
}
//--------------------------------------------------------------------------------------------------------
#endif

//-----------------------------------------------------------------------------------------------------------

//-----------------------------------------------------------------------------
void Um_Lim(motor *m, float Urms)
{
    m->ipark.Vd = saturation(Urms, c2p.Um_max, 0);
    m->flag.bit.Urms_sat = satFlag;
}

//------------------------------------------------------------------------------

#if T_SHEMA
//#pragma CODE_SECTION(model, "ramfuncs");
void T_shema(motor *m) {
  // обратное преобразование для вычисления скольжения
  //  находим комплексное сопротивление цепи ротора
  //  1. находим ток Icomplex на входе в Т схему замещения
  //  2. находим напряжение Umag на ротаре
  //  3. находим полное сопротивление парралельно соединённых Zr || Zm
  // 4. находим Zr = Rr/slip

  //   float w = m->freq *m->sh.kw;  // частота поля статора

  float A_Is;
  float complex old_Is;
  m->sh.Us = m->clark.Ua + m->clark.Ub * I;

  old_Is = m->sh.Is;
  m->sh.Is = 1e-6 + m->cla.c2p.I_amp + ((m->Ib_amp - m->Ic_amp) / sqrt3) * I;
  m->sh.dIs = m->sh.Is - old_Is;

  A_Is = carg(m->sh.Is);
  m->sh.We = 0.99 * m->sh.We + 0.01 * asut(A_Is - m->sh.A_Is) / Tpwm;
  m->sh.Z = m->sh.Us / m->sh.Is;

  m->sh.Zs =
      m->sh.Rs + (m->sh.We * m->sh.Ls) * I; // комплексное сопротивление статора
  m->sh.Zm = 1e-3 + (m->sh.We * m->sh.Lm) *
                        I; // комплексное сопротивление цепи намагничивания

  m->sh.Umag = m->sh.Us - m->sh.Is * m->sh.Zs;
  m->sh.Zrm = m->sh.Umag / m->sh.Is; // 1
  m->sh.Zr = m->sh.Zrm * m->sh.Zm / (m->sh.Zm - m->sh.Zrm);
  m->sh.Rrot = creal(m->sh.Zr) + 1e-6;
  m->sh.slip = (m->sh.Rr / m->sh.Rrot) * m->freq;

  m->sh.Imag = m->sh.Umag / m->sh.Zm;
  m->sh.A_Imag = carg(m->sh.Imag);
  m->sh.A_Umag = carg(m->sh.Umag);
  m->sh.Ir = -m->sh.Umag / m->sh.Zr;
  m->sh.A_Ir = carg(m->sh.Ir);

  m->sh.Fs = m->sh.Is * m->sh.Lsm + m->sh.Lm * m->sh.Ir;
  m->sh.A_Fs = carg(m->sh.Fs);
  m->sh.Fsa = creal(m->sh.Fs);
  m->sh.Fsb = cimag(m->sh.Fs);

  m->sh.Fr = m->sh.Is * m->sh.Lm + m->sh.Ir * m->sh.Lrm;
  m->sh.Frm = cabs(m->sh.Fr);
  m->sh.Fra = creal(m->sh.Fr);
  m->sh.Frb = cimag(m->sh.Fr);

  //    afr = carg(m->sh.Fr);
  //    m->sh.We = asut(afr - m->sh.A_Fr) / Tpwm;

  //   m->sh.A_Fr = afr;
  m->sh.freq = m->sh.We / m->sh.kw;

  m->sh.A_Us = carg(m->sh.Us);
  m->sh.A_Is = A_Is;
  m->sh.fi = asut(m->sh.A_Us - m->sh.A_Is);
}
#endif // T_SHEMA

#if MD2
void model2(motor *m) {
  float Lm = m->sh.Lm;
  float Ls = m->sh.Lm + m->sh.Ls;
  float Lr = m->sh.Lm + m->sh.Lr;
  float Kr = Lr / Lm; // обратная Kr
  float sigma = 1 - Lm * Lm / (Ls * Lr);
  //  m->md2.Umag = (m->sh.Us - m->sh.Is * m->sh.Rs)*K2 - m->md2.Umag*K1;
  //  //дискретный апериодический фильтр

  m->md2.Es = m->sh.Us - m->sh.Is * m->sh.Rs;
  m->md2.Fs = m->md2.Fs + (m->md2.Es + m->md2.Es0) * 0.5 * Tpwm + m->md2.Err;
  m->md2.Fsa = creal(m->md2.Fs);
  m->md2.Fsb = cimag(m->md2.Fr);
  m->md2.Es0 = m->md2.Es;

  //    m->md2.Fr =  (m->md2.Umag -  m->sh.Is * sigma * Ls ) * Kr;
  m->md2.Fr = (m->md2.Fs - m->sh.Is * sigma * Ls) * Kr;
  m->md2.AFr = carg(m->md2.Fr);
  m->md2.Frm = cabs(m->md2.Fr);
  m->md2.Fra = creal(m->md2.Fr);
  m->md2.Frb = cimag(m->md2.Fr);
}
#endif


/*
void RMS2(motor *v)

{

    v->Ia_sum = v->Ia_sum + c2p.I_amp * c2p.I_amp;
    v->Ib_sum = v->Ib_sum + Ib_amp * Ib_amp;
    v->Ic_sum = v->Ic_sum + Ic_amp * Ic_amp;
    v->Va_sum = v->Va_sum + v->clark.Va * v->clark.Va;
    v->Udc_sum = v->Udc_sum + Udc_volt * Udc_volt;

    v->Pu = c2p.I_amp * v->clark.Va; // мгновенная активная мощность фазы А
    v->sPu = v->sPu + v->Pu; // суммирование активной мощности

    //   v->sPabc = v->sPabc + v->pabc_ui;
    v->sPabc = v->sPabc + c2p.I_amp * v->clark.Va + v->Ib_amp * v->clark.Vb
            + v->Ic_amp * v->clark.Vc;

    v->paz = c2p.I_amp * v->svgen.Ta / 2 * Udc_volt;
    v->spaz = v->spaz + v->paz;

    v->count_sum++;
    float count = v->count_sum;

    // Вычисляем среднии значения из выборки измерений.
    // Выборка определяется как константное число samples плюс дополнительно
    // несколько измерений  до момента завершения периода частоты поля, определяет
    // установленный rg.flag Если поле стоит и соответсвенно  rg.Out не меняется
    // суммирование проводим по выборке двойного размера

    if ((count >= 100) && (v->rg.flag == 1) || (count >= (10000)))
    {
        // токи

        v->Ia_rms = sqrtf(v->Ia_sum / count);
        v->Ia_sum = 0;

        v->Ib_rms = sqrtf(v->Ib_sum / count);
        v->Ib_sum = 0;

        v->Ic_rms = sqrtf(v->Ic_sum / count);
        v->Ic_sum = 0;

        v->Va_rms = sqrtf(v->Va_sum / count);
        v->Va_sum = 0;

        v->Udc_rms = sqrtf(v->Udc_sum / count);
        v->Udc_sum = 0;

        v->Pa = v->sPu * 3 / count;
        v->sPu = 0;

        v->mPabc = v->sPabc / count;
        v->sPabc = 0;

        v->mPaz = v->spaz / count;
        v->spaz = 0;

        // мощности через Pa
        v->mSabc = (v->Ia_rms + v->Ib_rms + v->Ic_rms) * v->Va_rms;
        v->mPF = v->mPabc / v->mSabc;

        v->mPx = sqrtf((v->mSabc * v->mSabc) - (v->mPabc * v->mPabc));
        if (v->mSabc > 1)
            (v->cosPhi = v->Pa / v->mSabc);

        v->Pain = v->Pain_sum / count;
        v->Pain_sum = 0;

        v->count_sum = 0;


    }

    v->rg.flag = 0;
}
*/


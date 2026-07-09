/*
 * Motor.h
 *
 *  Created on: 04 марта 2015 г.
 *      Author: olegs.pavlovskis
 */

#ifndef MOTOR_H_
#define MOTOR_H_

#include <complex.h> /* Standard Library of Complex Numbers */
#include "f28p55x_device.h"

#include "QEP_RPM.h"

#include "pi.h"
#include "rampgen.h"

#define ERROR1 motor1.error
#define ERROR2 motor2.error

#define Ksumm 0.999
#define Kinst 0.001

//-------------------------------------------------------------------------------------------
typedef struct
{
    float  Ta;        // Output: reference phase-a switching function
    float  Tb;        // Output: reference phase-b switching function
    float  Tc;        // Output: reference phase-c switching function
    uint16_t VecSector; // Space vector sector
} SVGEN;

//-----------------------------------------------------------------------------

typedef struct
{
    long ref;  // Вход : Заданные обороты в об/мин * 100
    long dout; // Выход: об/мин * 100 000
    long rpm;  // Измеренные обороты : об/мин * 100 000

} RMPCNTL;

/* =============================================================================
 * CLARK  Структура параметров для Clark  преобразования
 =================================================================================*/
typedef struct
{
    float Ua;
    float Ub;
    float Um;  // амплитуда вектора фазного напряжения на выходе АИН
    float UmF; // UmF = UmF*0.99 +Umk*0.01

    float Va; // напряжение фазы А первой гармоники
    float Vb;
    float Vc;

    float ku01;
    float klow;   // уставка коэффициента коррекции напряжения от частоты поля
    float kufreq; // коэффициент коррекции напряжения от частоты поля - kufreq = (1-klow / freq/1000)
    float kUa;    //коэффициента коррекции напряжения

    float Ia;
    float Ib;
    float Im;
    float ImF;

} CLARK;

typedef struct
{

    complex float Us;
    complex float Umag;
    complex float Is;
    complex float Ir;
    complex float Imag;
    complex float dIs;

    complex float Zs;
    complex float Zr;
    complex float Zrm;
    complex float Zm;
    complex float Z;

    complex float Fr; //вектор магнитного потока ротора (Вебер)
    complex float Fs; //вектор магнитного потока статора (Вебер)
    float         A_Fs;
    float         Fsa;
    float         Fsb;

    float A_Us; // угол вектора напряжения фазы А в координатах Кларка
    float A_Is; //  угол вектора тока статора в координатах Кларка
    float fi;   // угол между вектором напряжения и тока фазы в радианах

    float A_Umag; //  угол вектора напряжения цепи намагничивания в координатах Кларка
    float A_Imag; //  угол вектора тока цепи намагничивания в координатах Кларка
    float A_Ir;   //  угол вектора тока ротора  в координатах Кларка
    float A_Fr;   //  угол вектора магнитного потока ротора в координатах Кларка ( rad )

    float We;   // вычисленная угловая скорость поля статора рад/с
    float freq; // вычисленная угловая скорость поля статора об/мин

    float Rrot;
    float slip; // вычисленное скольжение по рассчитанному активному сопротивлению ротора об/мин
    float Frm;
    float Fra;
    float Frb;

} MatModel;

typedef struct
{
    float Us; // фазное напряжение статорной обмотки,  Вольт
    float Ur; // фазное напряжение роторной обмотки,  Вольт
} Shema;
typedef struct
{
    complex float Fr; //вектор магнитного потока ротора (Вебер)
    complex float Fs; //вектор магнитного потока статора (Вебер)
    complex float Umag;
    complex float Es;
    complex float Es0;
    complex float Err;
    float         AFr; // rad/sek
    float         Frm;
    float         Fra;
    float         Frb;
    float         Fsa;
    float         Fsb;
} Model2;

typedef struct
{
    float Fra;  //проекция на ось а вектора потокосцепления ротора
    float Frb;  //проекция на ось b вектора потокосцепления ротора
    float Frm;  //модуль вектора потокосцепления ротора
    float A_Fr; //угол вектора потокосцепления ротора в системе кординат алфа-бета (Кларк)
    float wTr_dbg;
    float sma_dbg;
    float smb_dbg;
    float fral;
    float frbl;
    float A_eps;
    float IaLm;
    float IbLm;

} Observer;

/* ==============================================================================
 PARK  Структура параметров для Парк преобразования
 =================================================================================*/
typedef struct
{
    float Ud;  // вектор напряжения вдоль оси D
    float Uq;  // вектор напряжения вдоль оси Q
    float VdF; // вектор напряжения вдоль оси D
    float VqF; // вектор напряжения вдоль оси Q

    float Id; // вектор тока вдоль оси D
    float Iq; // вектор тока вдоль оси Q

    float IdF; // вектор тока вдоль оси D after filtr
    float IqF; // вектор тока вдоль оси Q after filtr

    float fiF;
    float fuF;

    float fu;  // угол обобщенного вектора напряжения от оси Парка в радианах
    float fi;  // угол обобщенного вектора тока от оси Парка в радианах
    float fui; // эл. угол между вектором напряжения и тока в радианах

    float phaza; // ручное смещение угла положения оси Парка

    float Sine;
    float Cosine;
    float kVd; // kVd * Vd = Vd_max
    float kVq; // kVq * Vq = Vq_max
} PARK;


// ====================    IPARK  ===============================================================
typedef struct
{
    float Alpha; // Output: stationary d-axis stator variable
    float Beta;  // Output: stationary q-axis stator variable
    float Angle; // угол вектора тока ротора относительно оси фазы А pu (0-1)
    float Vd;
    float Vq;
    float Vdf; // фильтрованное напряжение по оси d
    float Vqf; // фильтрованное напряжение по оси q

    float Vd0_max; // Ограничение напряжения Vd на нулевой частоте поля
    float Vq0_max; // Ограничение напряжения Vq на нулевой частоте поля
    float Vd_max;  // Ограничение напряжения Vd результирующее Vd_max = Vd0_max + Ed
    float Vq_max;  // Ограничение напряжения Vq результирующее Vq_max = Vq0_max + Eq
    float Udx;     // падение напряжения на реактивном сопротивлениии
    float Uqx;     // падение напряжения на реактивном сопротивлениии

    float gain;   // Output: индекс модуляции напряжения статора pu (0 - 1)
    float puVd;   // нормированное 0-1 Vd
    float puVq;   // нормированное 0-1 Vq
    float Vd_lim; // выходное напряжение с учётом ограничения по току
    float Vq_lim; // выходное напряжение с учётом ограничения по току

} IPARK;

typedef struct
{
  

    float Dead_time; // коррекция мёртвого времени PWM

} PWM_GEN;

typedef struct
{
    uint16_t emadd : 1;       //0 PC угол вектора напряжения статора
    uint16_t rgfix : 1;       //1 фиксируем угол вектора напряжения статора
    uint16_t Iq_ref : 1;      //2
    uint16_t Id_ref : 1;      //3
    uint16_t kIqd : 1;        //4 Iq_ref = kIqd * Id_ref
    uint16_t revers : 1;      //5 устанавливает направления вращения поля
    uint16_t AIN_Disable : 1; //6
    uint16_t epsi : 1;        //7 устанавливаем режим регулятора EPSv:  0=держим RPM 1=держим EPSi
    uint16_t emstep : 1;      //8 устанавливаем режим регулятора EPSv:  0=держим RPM 1=держим EPSi
    uint16_t Im : 1;          //9 =1 выключаем регулятор Im
    uint16_t Ltest : 1;       //10 включаем режим измерения индуктивности

    uint16_t sMax_On : 1;       //11
    uint16_t rez12 : 1;         //12 reserv
    uint16_t Gen_Engine_SW : 1; //13
    uint16_t FOC_mode : 1;      //14  =1 включает режим векторного управления

} Motor_bit;

typedef union
{
    Motor_bit bit;
    uint16_t    all;
} Mot_bit;

//-------------------------------------------------------------------
typedef struct
{
    uint16_t pwm_On : 1; // подключены выходы IGBT
    uint16_t Vd_sat : 1; //1
    uint16_t Vq_sat : 1; //2 фиксируем угол вектора напряжения статора
    uint16_t Id_sat : 1; //3 PC угол вектора напряжения статора

    uint16_t Iq_sat : 1;   //4 ограничение при задании Urms_sat
    uint16_t Im_sat : 1;   //5 ограничение при задании Urms_sat
    uint16_t Irpm_sat : 1; //6
    uint16_t r7 : 1;       //7 Iq_ref = kIqd * Id_ref

    uint16_t Urms_sat : 1; //8 ограничение при задании Urms_sat
    uint16_t r9 : 1;       //9 устанавливаем режим регулятора EPSv:  0=держим RPM 1=держим EPSi
    uint16_t r10 : 1;      //10 устанавливаем режим регулятора EPSv:  0=держим RPM 1=держим EPSi
    uint16_t r11 : 1;      //11 =1 выключаем регулятор Im

    uint16_t puVd_sat : 1; //12
    uint16_t puVq_sat : 1; //13
    //   uint16_t Filtr :1;       //14 =1  sAbs относительно оборотов ротора
    //   uint16_t Gen_Engine_SW :1; //15

} Motor_flag;

typedef union
{
    Motor_flag bit;
    uint16_t     all;
} Mot_flag;
/* ==============================================================================
 Imit_I  Структура параметров для иммитации измеренных токов и Udc
 =================================================================================*/

typedef struct
{
    complex float Us;
    complex float Es;
    complex float Is;
    complex float dIs;
    complex float Ir;
    complex float dIr;
    complex float Im;
    complex float Fr; // Fr = Lrm*Ir + Lm*Is;
    complex float Fm; // Fr = Lrm*Ir + Lm*Is;
    float         Te; // электромагнитный момент (n*m)
    float         M;  // момент сопротивления (n*m)

    float rpm; //  скорость вращения ротора ( об/мин)

    float ImF;  // амплитуда вектора тока иммитации после рекурентного фильтра, Ампер
    float Slip; // заданное от движка абсолютное скольжение  об/мин
    float We;
    float freq;
    float A_Im;
    float A_Ir;
    float A_Fr;
    float Irm;
    float Rslip;

} Imit;

//--------------------------------------------------------------------------------------------

typedef struct
{
    float abs;   //  абсолютное скольжение в об/мин
    float N3abs; //  абсолютное скольжение в об/мин по оборотам за оборот N3
                 //    float procent;		//  относительное скольжение в процентах
    int sRPM;    // скольжение при стабилизации оборотов ротора

    float absF;
    float procF; //  относительное скольжение в процентах
} SLIP;

//-------------------------------------------------------------------------------------------------------------------
//               REFm    Переменные управления
// в структуре собранны параметры управления от PC, которые задаются отдельно для каждого двигателя
typedef struct
{
    uint16_t load : 4;
    uint16_t LL : 4;
    uint16_t umode : 4;
} m_mode;

typedef union
{
    uint16_t all;
    m_mode bit;
} Motor_mode;

typedef struct
{
    int   parsel; // селектор требуемого параметра
    float gain;   // заданный коэфф. усиления  напряжения  0-1

    float Urms; // заданная амплитуда линейного напряжения Вольт
    float Id;   // заданный ток Id по Парку
    float Iq;   // заданный ток Iq по Парку
    float kIqd; // piIq.Ref = kIqId * piId.Ref
    float Im;   // заданный ток Im по Парку   0.001 A
    float UF;   // заданный U/F

    int Imax_off; // защита по току индивидуальная по АИН
    int lsw;      // динамическое переключение источника rg; rg=2 -> SMO

    float emadd; // сдвиг угла вектора напряжения относительно магнитной оси

    float piRPM_max_Gen; // коэф.ограничения выхода пи-регулятора RPM в генераторном режиме

    float kSlip; // коф. скольжения от RPM   sAbs = kSlip* RPM

    long sAbs; // заданное скольжение в 0.01 об/мин
    long sMax; // заданное ограничение по скольжению в 0.1 об/мин
    long slip;

} REFm;

#define umode_gain 0
#define umode_Urms 1
#define umode_UF 2
#define umode_Id 3
#define umode_Im 4

#define lmode_Sabs 0
#define lmode_Sproc 1
#define lmode_Srpm 2
#define lmode_Iq 3
#define lmode_Iqd 4
#define lmode_Iq_rpm 5
#define lmode_Iq_freq 6
#define lmode_gain 7
//-------------------------------------------------------------------------------------------------------

#define mot350 0
#define mot170 1
#define DTAN160 2
#define motSRD 3
#define SGT750 4
#define TAD800 5
#define GST7 6
#define dcmot 7
#define motACI 8
#define Induction 9

typedef struct
{
    uint16_t motID;     // 0-TAD350, 1-TAD170, 2- TAD170.1
    uint16_t toothNum;  // число зубьев счётного диска
    uint16_t pole_pair; // число пар полюсов

    uint16_t Rs;   // Stator resistance (mkOhm)
    uint16_t Rr;   // Stator resistance (mkOhm)
    uint16_t Ls;   // Stator inductance (mkH)
    uint16_t Lr;   // Stator inductance (mkH)
    uint16_t Lm;   // Main inductance (mkH)
    uint16_t Inom; // Base phase current (amp)
    uint16_t Unom; // Base phase voltage (volt)
    uint16_t Fnom; // RPM nom (rpm)
    uint16_t Pnom; // мощность nom (кВт)

    float J; //момент инерции двигателя

    float dVd_max; // максимальное изменение напряжения Vd за один тик
    float dVq_max; // максимальное изменение напряжения Vq за один тик

    uint16_t res_pp; // число тиков резолвера на эл.оборот  = 4096/pole_pair
    uint16_t enc_pp; // число тиков энкодера на эл.оборот  = число зубьев счётного колеса /pole_pair

    float kEd;
    float kEq;
    float kTe;

    uint16_t tip; // признак ассинхронный ACI =0, синхронный PMSM=1 , SynRM=2

} motorPar;
//----------------------------------------------------------
typedef struct // параметры Т-образной схемы замещения
{
    float Rs; // сопротивление статорной обмотки, Ом
    float Rr;
    float Ls;
    float Lm;
    float Lr;
    float Lrm; // = (Lr + Lm)
    float Lsm; // = (Ls + Lm)
    float Tr;
    float kw;       // коэффициент об.мин -> рад/сек
    float uf_norma; // коэффициент Unom / Fnom
} tsh_parametrs;

/*==============================================================================
 Структура для описания ошибок
 =================================================================================*/

typedef struct
{
    uint16_t Ia_Overload : 1;
    uint16_t Ib_Overload : 1;
    uint16_t Ic_Overload : 1;
    uint16_t Idc_Overload : 1;
    uint16_t Udc_Overload : 1;
    uint16_t IGBT_OverTemperature : 1;

    uint16_t Error_flag : 1;

} ERRORs;

typedef union
{
    ERRORs bit;
    uint16_t all;
} uiERROR;

/* ==============================================================================
 MOTOR  Главная Структура вычисляемых и измеренных значений
 =================================================================================*/

typedef struct
{
    motorPar      typeID;
    tsh_parametrs tsh;
    int           delay; //
    Mot_flag      flag;  // структура битов для подключения параметров и режимов иммитации.
    Mot_bit       mbit;  // структура битов для подключения параметров и режимов иммитации.
    Motor_mode    mode;
    Shema         shm;
    QEPpar        qep;

    float Sine;
    float Cosine;

   
    float Vab;

  
    

    // RMS2 variable  ----------------------------------------
    uint16_t count_sum; // Счётчик количества измерений для подсчёта   измерений при расчёте среднего.

  
    // activ Power  kWt
    float Pu;  // instant (cla.c2p.I_amp* Va)  power for phase A  u
    float sPu; // sPu = sPu +Pu
    float Pa;  // средняя по выборке activ power Pa =sPu*3/samples;

    float paz;  // мгновенная мощность paz = Udc*[Ta~-1]/(HalfPerMax*2)* cla.c2p.I_amp
    float spaz; // spaz = spaz + paz
    float mPaz; // средняя по выборке activ power Paz =spaz *3/samples;

    float sPabc; // sPabc = sPabc + pabc_ui
    float mPabc; // средняя по выборке activ power суммарная по всем фазам

    // full Power  kVA thru Urms
    float mPx;    //средняя по выборке реактивная мощность  mPx =sqrt((Pz*Pz)-(Pa*Pa)); for phase u
    float mSabc;  //средняя по выборке полная мощность суммарная по всем фазам Pz= Iu_rms* v.Urms*1.732;
    float mPF;    // коэф.мощности mPF = mPabc /mSabc
    float cosPhi; // power factor cosPhi= Pa/Pz ; for phase u

    // Power to AIN
    float Pain;     // instant ( Ir_full * Udc_full)  activ power for AIN
    float Sain;     // full power of AIN  Sain = Ir_rms*Udc_rms
    float Pain_sum; // Pain_sum = Pain_sum +Pain  - накопленная активная мощность AIN
    float Paain;    // midle activ power Paain =Pain_sum/samples;

    // к.п.д.
    float psIR; // потери на активном сопротивлении статора
    float prIR; // потери на активном сопротивлении ротора

   

    REFm ref; //параметры управления от PC

    //------------------------------------ PARK  ------------------------------------------------------------------------------
    CLARK clark;
    PARK  park;
    IPARK ipark;

    PI_CONTROLLER piIq;   // пи регулятор Vq
    PI_CONTROLLER piId;   // пи регулятор Vd
    PI_CONTROLLER piRPM;  // пи регулятор Vd
    PI_CONTROLLER piIm;   // пи регулятор Im
    PI_CONTROLLER piEPSI; // пи регулятор EPSi
    PI_CONTROLLER piSRPM; // пи регулятор оборотов за счёт управления скольжением

//------------------------------------ Immit  ------------------------------------------------------------------------------
#if dbgIfull
    Imit imit; // структура параметров для иммитации токов и Udc при отладке программы
#endif

    //------------------------------------ RAMPGEN  ------------------------------------------------------------------------------
    float rpm;
    float freq; // Фактическая частота магнитного поля приведённая к оборотам/мин  ротора

    RMPCNTL ramp;

    RAMPGEN rg;

    SVGEN   svgen;
    PWM_GEN pwmgen;

    SLIP slip;

    uiERROR error;

    //----------------------------------------------------------------------------------------------------------------
    // Мощности вычисленные классическим способом
    float pa_ui;   // мгновенная мощность фазы А
    float pb_ui;   // мгновенная мощность фазы B
    float pc_ui;   // мгновенная мощность фазы C
    float pabc_ui; // сумарная мощность по трём фазам
    float pabc_rf; // сглаженная сумарная мощность по трём фазам
    float pabc_p;  // средняя за период поля сумарная мощность по трём фазам

    //-----------------------------------------------------------------------------------------------------------------

    float Tp; // крутящий момент через мощность
    float Ti; // крутящий момент через ток

#if T_SHEMA
    MatModel sh;
#endif

#if MD2
    Model2 md2;
#endif

} motor;

//------------------------------------------------------------------------------------------------------------------

/* ==============================================================================
 struct	STEND  Структура вычисляемых и измеренных значений всего стенда
 =================================================================================*/
typedef struct
{
   

    float Pa_stend; // Pa = I * Udc_full instant  aktiv stend power
    float dPam;     // dPam = motor1.Pa - motor2.Pa	потери активной мощности в стенде
    float dPain;    // dPam = motor1.Pa - motor2.Pa	потери активной мощности

   
    
   
    
    uint16_t Udc_error; //значение Udc_full при котором сработала защита по максимальному напряжению
  

} STEND;

//----------------------------------------------------------------------------------------------------------

// Function prototype
void motor_init(motor *v);

void MotorCalc(motor *v);

//void currentTest(motor *v);

void CUR_MOD(motor *m);
void CUR_CONST(motor *m);

extern motor motor1, motor2;
extern STEND stend;

#endif /* MOTOR_H_ */

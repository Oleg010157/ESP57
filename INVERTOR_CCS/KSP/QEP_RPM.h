//###########################################################################
//
// FILE:	Example_freqcal.h
//
// TITLE:	Frequency measurement using EQEP peripheral
//
// DESCRIPTION:
//
// Header file containing data type and object definitions and
// initializers.
//
//###########################################################################
// Original Author: SD
//
// $TI Release: 2833x/2823x Header Files and Peripheral Examples V133 $
// $Release Date: June 8, 2012 $
//###########################################################################

#ifndef _QEP_RPM_H_
#define _QEP_RPM_H_

/*-----------------------------------------------------------------------------
 Define the structure of the FREQCAL Object
 -----------------------------------------------------------------------------*/
typedef struct
{
    Uint32 cnt1;


    int mode;		    // определяет режим работы модуля EQP
    uint16_t toothN;         // число зубьев счётного диска
    int strob_sektor; // число фронтов зубьев на пару полюсов двигателя
    float pr;           // обороты ротора по времени прохождения сектора зубьев
    float n3;			// сглаженные pr
    float fr;           // обороты ротора как количество импульсов от зубьев за еденицу времени
    Uint32 pr0;           // почищенные n3 обороты ротора как количество импульсов от зубьев за еденицу времени
    Uint32 pr1;           // почищенные n3 обороты ротора как количество импульсов от зубьев за еденицу времени
    float prf;           // почищенные n3 обороты ротора как количество импульсов от зубьев за еденицу времени
    Uint32 overcount;	// количество overflow QCTMR для вычисления на малых оборотах

    float Qway;		// длина сектора в долях полного оборота

    long angle;          // rotor angle from index strob;
    long an_last;

    long mov32;         // угловое перемещение(подсчитанное кол-во фронтов зубъев) за время подсчёта RPM_fr

    float kToRPM;  // коэф-т перевода mov32/timefr зависит от кол-ва зубъев счётного диска
    uint16_t psek;

    uint16_t N3_new :1;   // Global flag when N3 has new value
    uint16_t UPPS_new :1;   // Global flag when N3 has new value

    uint16_t sts;



//--------------------------------------------------------------
    union       // 32 бит счётчик время прохождения сектора зубьев
    {
        Uint32 all;
        struct
        {
            uint16_t L :16;
            uint16_t H :16;
        } w;

    } Qtime;
//----------------------------------------------------------------


#if dbgRPM == 1

    float rpm_dbg;      // обороты иммитации для отладки

#endif

} QEPpar;

typedef struct
{
    uint16_t RPM_fr_new :1;   // Global flag when freqRPM_fr has new value
} QEP_bits;

/*-----------------------------------------------------------------------------
 Prototypes for the functions
 -----------------------------------------------------------------------------*/
void QEP_Init(QEPpar *p, volatile struct EQEP_REGS *reg);
void QEP_Calc(QEPpar *p, volatile struct EQEP_REGS *reg);
void COEFbit(QEPpar *p, volatile struct EQEP_REGS *reg);

void InitEQep1Gpio(void);
void InitEQep2Gpio(void);

#endif /*  _QEP_RPM_H_ */


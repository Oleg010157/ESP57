/*
 * F3_pui.h
 *
 *      Author: olegs.pavlovskis
 */

#ifndef F3_pui
#define F3_pui

#define BoxesF  0 //0x7  // ���������� ���������� Mbox-��
#define noFlag  1    // =0 fst12 �������� �����


#define fst1	rounding(mfast->cla.c2p.I_amp*10)
#define fst2	rounding(mfast->Ib_amp*10)
#define fst3	rounding(mfast->Ic_amp*10)
#define fst4    rounding(motor1.svgen.Ta*HalfPerMax)

#define fst5    rounding(mfast->Va*10)
#define fst6    rounding(mfast->Vb*10)
#define fst7	rounding(mfast->Vc*10)
#define fst8    rounding(mfast->rg.Out*180/PI*10)

#define fst9     rounding(motor1.svgen.Tb*HalfPerMax) //rounding(motor2.Ir_full*10)
#define fst10	rounding(mfast->freq*100) //rounding(mfast->qep.pr*5)
#define fst11	rounding(motor1.Va0*10)
#define fst12  rounding(motor1.svgen.Tc*HalfPerMax) //rounding(mfast->freq*5)

#endif /* F3_pui */

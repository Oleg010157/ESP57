/*
 * F3_pui.h
 *
 *      Author: olegs.pavlovskis
 */

#ifndef F3_pui
#define F3_pui

#define BoxesF  0x7  // определяем количество Mbox-ов
#define noFlag  1    // =0 fst12 содержит флаги


#define fst1	mfast->Ia
#define fst2	mfast->Ib
#define fst3	mfast->Ic
#define fst4    motor1.Ir

#define fst5    rounding(mfast->Va*10)
#define fst6    rounding(mfast->Vb*10)
#define fst7	rounding(mfast->Vc*10)
#define fst8     AdcMirror.ADCRESULT9 // TorqU  Канал B4

#define fst9    motor2.Ir
#define fst10	 AdcMirror.ADCRESULT10 // Udc   Канал A5
#define fst11	rounding(cla.Udc_volt*10)
#define fst12  rounding(dc.Idc_amp*10) //rounding(mfast->freq*5)

#endif /* F3_pui */

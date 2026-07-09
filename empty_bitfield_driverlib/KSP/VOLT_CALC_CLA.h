#ifndef __VOLT_CALC_CLA_H__
#define __VOLT_CALC_CLA_H__

typedef struct 	{
//				  double  DcBusVolt;		// Input: DC-bus voltage (pu)
	  	  	  	  double  Va;			// Output: Phase voltage phase A
		   	  	  double  Vb;			// Output: Phase voltage phase B
		  	  	  double  Vc;			// Output: Phase voltage phase C
		  	  	  double  Valpha;			// Output: Stationary d-axis phase voltage
		  	  	  double  Vbeta;  			// Output: Stationary q-axis phase voltage
		  	  	  double  Vfull;				// Output: обобщённый вектор напряжения

		  	  	} VOLT_CALC_CLA;	                   


/*
OutOfPhase = 1 for the out of phase correction if
* MfuncV1 is out of phase with PWM1,
* MfuncV2 is out of phase with PWM3,
* MfuncV3 is out of phase with PWM5,
otherwise, set 0 if their phases are correct.
*/

#define ONE_THIRD  (0.33333333333333F)
#define TWO_THIRD  (0.66666666666667F)
#define INV_SQRT3  (0.57735026918963F)
#define rad90		(PI*(90.0f/180.0f))

/* макрос Volt_Shift вычисляет напряжение Vout фазы А в момент времени отстающий на delay микросекунд от текущего.
 * вызывается в процедуре MainISR. Vout нужно для вычисления активной мощности.
 * angle = rg.out относительный ( от -1.0 до +1.0) угол вращения магнитного поля
 * Udc  -  напряжение на входе инвертора
 * gain  - коэффициент ослабления Udc посредством  ШИМ,
 * sdvig - сдвиг  напряжения Vout в микросекундах.
 * freq -  частота магнитного поля в pu = freq_pr(Gz)/Freq_max(Gz)
 */
#define Volt_Shift(v,rg,sdvig)		\
{												\
	double Vamp;									\
	double phaza;								\
												\
	Vamp = stend.Udc_rf*v.Gain*INV_SQRT3;			\
	phaza =PI2*(rg.Out+sdvig*rg.Freq*Freq_max*1.0e-6F); \
												\
	v.Vsdvig =- ( Vamp * sin(phaza));			\
	v.Va90   =- (Vamp * sin(phaza-rad90));				\
}


#endif // __VOLT_CALC_CLA_H__























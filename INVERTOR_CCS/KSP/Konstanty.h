/*
 * Extern.h
 *
 *  Created on: 22 янв. 2015 г.
 *      Author: olegs.pavlovskis
 */

#ifndef EXTERN_H_
#define EXTERN_H_
/* В этом файле объявляются глобальные константы, переменные, структуры и прототипы функций */

//--------   CONSTANT    -------------------------------------------------------
// 		#define	kUdc	0.4132F		// коэффициент Вольт/код ADC датчиков напряжения

#define sqrt3	1.7320508075688772935274463415059f  // константа корень из 3
#define isqrt3	0.57735026918962576450914878050196f	// 1/sqrt3
#define sqrt2	1.4142135623730950488016887242097f	// константа корень из 2

#define PI 		3.14159265358979f
#define PI2		6.283185307179586476925286766559f	// константа  2*PI
#define	PI23	2.0943951023931954923084289221863f	// константа  2*PI/3
#define ph120   PI/180.0f*120.0f      // 120 град. -> рад.
//-------------------------------------------------------------------------------------
#define RPMmax  		6000.0f
#define invRPMmax  		1.0/(float)RPMmax
#define Freq_max		RPMmax/60.0f

// Нормированные величины
#define RPMmax_n  		RPMmax/1000.0f
#define invRPMmax_n  	1.0f/RPMmax_n


//-----------------------------------------------------------------------------------

#define ref_kadr_mask (BIT5|BIT6|BIT7|BIT8|BIT9|BIT10|BIT11|BIT12|BIT13)
#define CANA_f3_mask (BIT2|BIT3|BIT4)// Main_ISR Передача быстрого CANA кадра через Mbox3, Mbox4, Mbox5 ID =103,104,110
#define CANA_f1_mask (BIT2)  // Main_ISR Передача быстрого CANA кадра через Mbox3 ID =103
#define CANA_fon_mask (BIT31)  // Фон Передача ошибок  через Mbox32 ID =131
#define MCAN_f3_mask ((1UL << 3U) | (1UL << 4U) | (1UL << 5U))
#define MCAN_f1_mask (1UL << 3U)
//#define kmask60 (BIT14|BIT17|BIT18|BIT19|BIT20|BIT21|BIT22|BIT23|BIT24|BIT25|BIT26|BIT27|BIT30)
#define kmask60 BIT30



#endif /* EXTERN_H_ */

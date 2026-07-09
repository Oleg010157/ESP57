/* ==============================================================================

ќписани файла: ¬ данном файле содержатьс¤ все основные системные константы, объ¤вленные через #define

=====================================================================================
 History:
-------------------------------------------------------------------------------------
 13.01.2015 ‘айл был создан на основе файла KSP_Parameters.h
=================================================================================  */
#ifndef PROJ_SYS_CONST_H_
#define PROJ_SYS_CONST_H_

//------------------------------------------------------------------------------
#ifndef TRUE
#define FALSE 0
#define TRUE  1
#endif


#define SYSTEM_FREQUENCY 120 // mHz



// Define the ISR frequency (Hz)
#define ISR_FREQUENCY 2000

// Additional Parameters for CAN
#define canLossMax    10000
#define CANTR_usDELAY 100000L
// CAN Boot Loader Parameters
#define OTP_START_ADDR  0x380400
#define OTP_END_ADDR    0x3807FF
#define FLASH_START_ADDR  0x300000
#define FLASH_END_ADDR    0x33FFFF

// „исло циклов пропуска перед началом вычислени¤ offset'a (1 и 2.5 секунды соответственно)

#define	IsrStart 5000


#endif

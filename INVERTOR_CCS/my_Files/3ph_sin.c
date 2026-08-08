/*
 * 3ph_Sin.c
 *
 *  Created on: 26.02.2025
 *      Author: Oleg Pavlovsky
 *  модифицирован 03.10.2025  Олег Павловский
 *  модифицирован 27.07.2025  Олег Павловский  под проект К1
 */

/* Данный файл представляет программный код для реализации мостового однофазного инвертора.
*  Мост реализован на двух EPWM модулях - EPWM1 (фаза А)и EPWM2 (фаза B).
*  Запуск EPWM2 синхронный с запуском EPWM1.
*/

#include "board.h"
#include <math.h>
#include "F28P55x_Device.h" // Основные определения регистров и структуры устройства

#include "KSP.h"
#include "myDCAN.h"

#include "1ph_Sin.h"

void MainISR(void);
volatile uint16_t epwm3count = 0;// dbg
volatile uint16_t epwm3time = 0;// dbg

__interrupt void INT_myEPWM3_ISR(void) 
{ 
    epwm3count++;//dbg
   
    
    MainISR();
    EPwm3Regs.ETCLR.bit.INT = 1;
    Interrupt_clearACKGroup(INTERRUPT_ACK_GROUP3); 
}

/*
 * Copyright (c) 2020 Texas Instruments Incorporated - http://www.ti.com
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * *  Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 *
 * *  Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 *
 * *  Neither the name of Texas Instruments Incorporated nor the names of
 *    its contributors may be used to endorse or promote products derived
 *    from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
 * THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
 * PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
 * OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
 * OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
 * EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 */

#ifndef BOARD_H
#define BOARD_H

//*****************************************************************************
//
// If building with a C++ compiler, make all of the definitions in this header
// have a C binding.
//
//*****************************************************************************
#ifdef __cplusplus
extern "C"
{
#endif

//
// Included Files
//

#include "driverlib.h"
#include "device.h"

//*****************************************************************************
//
// PinMux Configurations
//
//*****************************************************************************

//
// EPWM2 -> myEPWM1 Pinmux
//
//
// EPWM2_A - GPIO Settings
//
#define GPIO_PIN_EPWM2_A 230
#define myEPWM1_EPWMA_GPIO 230
#define myEPWM1_EPWMA_PIN_CONFIG GPIO_230_EPWM2_A
//
// EPWM2_B - GPIO Settings
//
#define GPIO_PIN_EPWM2_B 228
#define myEPWM1_EPWMB_GPIO 228
#define myEPWM1_EPWMB_PIN_CONFIG GPIO_228_EPWM2_B

//
// EPWM1 -> myEPWM2 Pinmux
//
//
// EPWM1_A - GPIO Settings
//
#define GPIO_PIN_EPWM1_A 0
#define myEPWM2_EPWMA_GPIO 0
#define myEPWM2_EPWMA_PIN_CONFIG GPIO_0_EPWM1_A
//
// EPWM1_B - GPIO Settings
//
#define GPIO_PIN_EPWM1_B 226
#define myEPWM2_EPWMB_GPIO 226
#define myEPWM2_EPWMB_PIN_CONFIG GPIO_226_EPWM1_B

//
// MCANA -> myMCANA Pinmux
//
//
// MCANA_RX - GPIO Settings
//
#define GPIO_PIN_MCANA_RX 5
#define myMCANA_MCANRX_GPIO 5
#define myMCANA_MCANRX_PIN_CONFIG GPIO_5_MCANA_RX
//
// MCANA_TX - GPIO Settings
//
#define GPIO_PIN_MCANA_TX 4
#define myMCANA_MCANTX_GPIO 4
#define myMCANA_MCANTX_PIN_CONFIG GPIO_4_MCANA_TX

//
// SPIA -> mySPIB Pinmux
//
//
// SPIA_PICO - GPIO Settings
//
#define GPIO_PIN_SPIA_PICO 224
#define mySPIB_SPIPICO_GPIO 224
#define mySPIB_SPIPICO_PIN_CONFIG GPIO_224_SPIA_PICO
//
// SPIA_POCI - GPIO Settings
//
#define GPIO_PIN_SPIA_POCI 13
#define mySPIB_SPIPOCI_GPIO 13
#define mySPIB_SPIPOCI_PIN_CONFIG GPIO_13_SPIA_POCI
//
// SPIA_CLK - GPIO Settings
//
#define GPIO_PIN_SPIA_CLK 12
#define mySPIB_SPICLK_GPIO 12
#define mySPIB_SPICLK_PIN_CONFIG GPIO_12_SPIA_CLK
//
// SPIA_PTE - GPIO Settings
//
#define GPIO_PIN_SPIA_PTE 11
#define mySPIB_SPIPTE_GPIO 11
#define mySPIB_SPIPTE_PIN_CONFIG GPIO_11_SPIA_PTE

//*****************************************************************************
//
// EPWM Configurations
//
//*****************************************************************************
#define myEPWM1_BASE EPWM2_BASE
#define myEPWM1_TBPRD 0
#define myEPWM1_COUNTER_MODE EPWM_COUNTER_MODE_STOP_FREEZE
#define myEPWM1_TBPHS 0
#define myEPWM1_CMPA 0
#define myEPWM1_CMPB 0
#define myEPWM1_CMPC 0
#define myEPWM1_CMPD 0
#define myEPWM1_DBRED 0
#define myEPWM1_DBFED 0
#define myEPWM1_TZA_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM1_TZB_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM1_INTERRUPT_SOURCE EPWM_INT_TBCTR_DISABLED
#define myEPWM2_BASE EPWM1_BASE
#define myEPWM2_TBPRD 0
#define myEPWM2_COUNTER_MODE EPWM_COUNTER_MODE_STOP_FREEZE
#define myEPWM2_TBPHS 0
#define myEPWM2_CMPA 0
#define myEPWM2_CMPB 0
#define myEPWM2_CMPC 0
#define myEPWM2_CMPD 0
#define myEPWM2_DBRED 0
#define myEPWM2_DBFED 0
#define myEPWM2_TZA_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM2_TZB_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM2_INTERRUPT_SOURCE EPWM_INT_TBCTR_DISABLED

//*****************************************************************************
//
// INTERRUPT Configurations
//
//*****************************************************************************

// Interrupt Settings for INT_myMCANA_0
// ISR need to be defined for the registered interrupts
#define INT_myMCANA_0 INT_MCANA_0
#define INT_myMCANA_0_INTERRUPT_ACK_GROUP INTERRUPT_ACK_GROUP9
extern __interrupt void INT_myMCANA_0_ISR(void);

//*****************************************************************************
//
// MCAN Configurations
//
//*****************************************************************************
#define myMCANA_BASE MCANA_DRIVER_BASE
//
// Defines
//
#define myMCANA_MCAN_STD_ID_FILTER_NUM          (1)
#define myMCANA_MCAN_EXT_ID_FILTER_NUM          (1)
#define myMCANA_MCAN_FIFO_0_NUM                 (16)
#define myMCANA_MCAN_FIFO_1_NUM                 (0)
#define myMCANA_MCAN_TX_BUFF_SIZE               (32)
#define myMCANA_MCAN_TX_EVENT_SIZE              (0)

#define myMCANA_MCAN_STD_ID_FILT_START_ADDR     (0)
#define myMCANA_MCAN_EXT_ID_FILT_START_ADDR     (4)
#define myMCANA_MCAN_FIFO_0_START_ADDR          (12)
#define myMCANA_MCAN_FIFO_1_START_ADDR          (12)
#define myMCANA_MCAN_RX_BUFF_START_ADDR         (12)
#define myMCANA_MCAN_TX_BUFF_START_ADDR         (12)
#define myMCANA_MCAN_TX_EVENT_START_ADDR        (12)

#define myMCANA_MCAN_INTR_LINE_NUM_0_INT (MCAN_INTR_MASK_ALL)
void myMCANA_init();

//*****************************************************************************
//
// SPI Configurations
//
//*****************************************************************************
#define mySPIB_BASE SPIA_BASE
#define mySPIB_BITRATE 1000000
#define mySPIB_DATAWIDTH 16
void mySPIB_init();

//*****************************************************************************
//
// SYNC Scheme Configurations
//
//*****************************************************************************

//*****************************************************************************
//
// SYSCTL Configurations
//
//*****************************************************************************

//*****************************************************************************
//
// Board Configurations
//
//*****************************************************************************
void	Board_init();
void	EPWM_init();
void	INTERRUPT_init();
void	MCAN_SYSCFG_init();
void	SPI_init();
void	SYNC_init();
void	SYSCTL_init();
void	PinMux_init();

//*****************************************************************************
//
// Mark the end of the C bindings section for C++ compilers.
//
//*****************************************************************************
#ifdef __cplusplus
}
#endif

#endif  // end of BOARD_H definition

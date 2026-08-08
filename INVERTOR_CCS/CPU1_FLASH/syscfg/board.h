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
// ANALOG -> myANALOGPinMux0 Pinmux
//

//
// EPWM1 -> myEPWM1 Pinmux
//
//
// EPWM1_A - GPIO Settings
//
#define GPIO_PIN_EPWM1_A 0
#define myEPWM1_EPWMA_GPIO 0
#define myEPWM1_EPWMA_PIN_CONFIG GPIO_0_EPWM1_A
//
// EPWM1_B - GPIO Settings
//
#define GPIO_PIN_EPWM1_B 1
#define myEPWM1_EPWMB_GPIO 1
#define myEPWM1_EPWMB_PIN_CONFIG GPIO_1_EPWM1_B

//
// EPWM2 -> myEPWM2 Pinmux
//
//
// EPWM2_A - GPIO Settings
//
#define GPIO_PIN_EPWM2_A 2
#define myEPWM2_EPWMA_GPIO 2
#define myEPWM2_EPWMA_PIN_CONFIG GPIO_2_EPWM2_A
//
// EPWM2_B - GPIO Settings
//
#define GPIO_PIN_EPWM2_B 3
#define myEPWM2_EPWMB_GPIO 3
#define myEPWM2_EPWMB_PIN_CONFIG GPIO_3_EPWM2_B

//
// EPWM3 -> myEPWM3 Pinmux
//
//
// EPWM3_A - GPIO Settings
//
#define GPIO_PIN_EPWM3_A 14
#define myEPWM3_EPWMA_GPIO 14
#define myEPWM3_EPWMA_PIN_CONFIG GPIO_14_EPWM3_A
//
// EPWM3_B - GPIO Settings
//
#define GPIO_PIN_EPWM3_B 15
#define myEPWM3_EPWMB_GPIO 15
#define myEPWM3_EPWMB_PIN_CONFIG GPIO_15_EPWM3_B

//
// EPWM4 -> myEPWM4 Pinmux
//
//
// EPWM4_A - GPIO Settings
//
#define GPIO_PIN_EPWM4_A 22
#define myEPWM4_EPWMA_GPIO 22
#define myEPWM4_EPWMA_PIN_CONFIG GPIO_22_EPWM4_A
//
// EPWM4_B - GPIO Settings
//
#define GPIO_PIN_EPWM4_B 23
#define myEPWM4_EPWMB_GPIO 23
#define myEPWM4_EPWMB_PIN_CONFIG GPIO_23_EPWM4_B

//
// EPWM5 -> myEPWM5 Pinmux
//
//
// EPWM5_A - GPIO Settings
//
#define GPIO_PIN_EPWM5_A 37
#define myEPWM5_EPWMA_GPIO 37
#define myEPWM5_EPWMA_PIN_CONFIG GPIO_37_EPWM5_A
//
// EPWM5_B - GPIO Settings
//
#define GPIO_PIN_EPWM5_B 35
#define myEPWM5_EPWMB_GPIO 35
#define myEPWM5_EPWMB_PIN_CONFIG GPIO_35_EPWM5_B

//
// EPWM6 -> myEPWM6 Pinmux
//

//
// EPWM7 -> myEPWM7 Pinmux
//
//
// GPIO57 - GPIO Settings
//
#define scope_GPIO_PIN_CONFIG GPIO_57_GPIO57

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
// SPIB -> mySPIB Pinmux
//

//*****************************************************************************
//
// ADC Configurations
//
//*****************************************************************************
#define myADCA_BASE ADCA_BASE
#define myADCA_RESULT_BASE ADCARESULT_BASE
#define myADCA_SOC0_Udc ADC_SOC_NUMBER0
#define myADCA_FORCE_SOC0_Udc ADC_FORCE_SOC0
#define myADCA_SAMPLE_WINDOW_SOC0_Udc 80
#define myADCA_TRIGGER_SOURCE_SOC0_Udc ADC_TRIGGER_EPWM1_SOCA
#define myADCA_CHANNEL_SOC0_Udc ADC_CH_ADCIN0
#define myADCA_SOC1_Vin ADC_SOC_NUMBER1
#define myADCA_FORCE_SOC1_Vin ADC_FORCE_SOC1
#define myADCA_SAMPLE_WINDOW_SOC1_Vin 80
#define myADCA_TRIGGER_SOURCE_SOC1_Vin ADC_TRIGGER_EPWM1_SOCA
#define myADCA_CHANNEL_SOC1_Vin ADC_CH_ADCIN4
#define myADCA_SOC2_Idc ADC_SOC_NUMBER2
#define myADCA_FORCE_SOC2_Idc ADC_FORCE_SOC2
#define myADCA_SAMPLE_WINDOW_SOC2_Idc 80
#define myADCA_TRIGGER_SOURCE_SOC2_Idc ADC_TRIGGER_EPWM1_SOCA
#define myADCA_CHANNEL_SOC2_Idc ADC_CH_ADCIN12
#define myADCA_PPB1 ADC_PPB_NUMBER1
#define myADCA_SOC_PPB1 ADC_SOC_NUMBER0
#define myADCA_PPB2 ADC_PPB_NUMBER2
#define myADCA_SOC_PPB2 ADC_SOC_NUMBER1
#define myADCA_PPB3 ADC_PPB_NUMBER3
#define myADCA_SOC_PPB3 ADC_SOC_NUMBER2
void myADCA_init();

#define myADCB_BASE ADCB_BASE
#define myADCB_RESULT_BASE ADCBRESULT_BASE
#define myADCB_SOC0_Ia ADC_SOC_NUMBER0
#define myADCB_FORCE_SOC0_Ia ADC_FORCE_SOC0
#define myADCB_SAMPLE_WINDOW_SOC0_Ia 800
#define myADCB_TRIGGER_SOURCE_SOC0_Ia ADC_TRIGGER_EPWM2_SOCA
#define myADCB_CHANNEL_SOC0_Ia ADC_CH_ADCIN1
#define myADCB_SOC1_Ib ADC_SOC_NUMBER1
#define myADCB_FORCE_SOC1_Ib ADC_FORCE_SOC1
#define myADCB_SAMPLE_WINDOW_SOC1_Ib 80
#define myADCB_TRIGGER_SOURCE_SOC1_Ib ADC_TRIGGER_EPWM2_SOCA
#define myADCB_CHANNEL_SOC1_Ib ADC_CH_ADCIN7
#define myADCB_PPB1 ADC_PPB_NUMBER1
#define myADCB_SOC_PPB1 ADC_SOC_NUMBER0
#define myADCB_PPB2 ADC_PPB_NUMBER2
#define myADCB_SOC_PPB2 ADC_SOC_NUMBER1
void myADCB_init();

#define myADCC_BASE ADCC_BASE
#define myADCC_RESULT_BASE ADCCRESULT_BASE
#define myADCC_SOC0 ADC_SOC_NUMBER0
#define myADCC_FORCE_SOC0 ADC_FORCE_SOC0
#define myADCC_SAMPLE_WINDOW_SOC0 3413.3333333333335
#define myADCC_TRIGGER_SOURCE_SOC0 ADC_TRIGGER_EPWM2_SOCA
#define myADCC_CHANNEL_SOC0 ADC_CH_ADCIN12
void myADCC_init();


//*****************************************************************************
//
// ASYSCTL Configurations
//
//*****************************************************************************

//*****************************************************************************
//
// CLB Configurations
//
//*****************************************************************************
#define myCLB0_BASE CLB1_BASE
void myCLB0_init();
//
// Tile Configurations for all CLBs are in this file
//
#include "clb_config.h"

//*****************************************************************************
//
// DAC Configurations
//
//*****************************************************************************
#define myDAC1_BASE DACA_BASE
void myDAC1_init();

//*****************************************************************************
//
// EPWM Configurations
//
//*****************************************************************************
#define myEPWM1_BASE EPWM1_BASE
#define myEPWM1_TBPRD 30000
#define myEPWM1_COUNTER_MODE EPWM_COUNTER_MODE_UP_DOWN
#define myEPWM1_TBPHS 0
#define myEPWM1_CMPA 15000
#define myEPWM1_CMPB 0
#define myEPWM1_CMPC 0
#define myEPWM1_CMPD 0
#define myEPWM1_DBRED 150
#define myEPWM1_DBFED 150
#define myEPWM1_TZA_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM1_TZB_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM1_OSHT_SOURCES EPWM_TZ_SIGNAL_DCAEVT1
#define myEPWM1_INTERRUPT_SOURCE EPWM_INT_TBCTR_PERIOD
// myEPWM1 Configuration Template
void ePWM1_Template(uint32_t base);
void myEPWM1_init();
#define myEPWM2_BASE EPWM2_BASE
#define myEPWM2_TBPRD 30000
#define myEPWM2_COUNTER_MODE EPWM_COUNTER_MODE_UP_DOWN
#define myEPWM2_TBPHS 0
#define myEPWM2_CMPA 15000
#define myEPWM2_CMPB 0
#define myEPWM2_CMPC 0
#define myEPWM2_CMPD 0
#define myEPWM2_DBRED 150
#define myEPWM2_DBFED 150
#define myEPWM2_TZA_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM2_TZB_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM2_OSHT_SOURCES EPWM_TZ_SIGNAL_DCAEVT1
#define myEPWM2_INTERRUPT_SOURCE EPWM_INT_TBCTR_PERIOD
void myEPWM2_init();
#define myEPWM3_BASE EPWM3_BASE
#define myEPWM3_TBPRD 60000
#define myEPWM3_COUNTER_MODE EPWM_COUNTER_MODE_STOP_FREEZE
#define myEPWM3_TBPHS 0
#define myEPWM3_CMPA 0
#define myEPWM3_CMPB 0
#define myEPWM3_CMPC 0
#define myEPWM3_CMPD 0
#define myEPWM3_DBRED 150
#define myEPWM3_DBFED 150
#define myEPWM3_TZA_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM3_TZB_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM3_INTERRUPT_SOURCE EPWM_INT_TBCTR_PERIOD
void myEPWM3_init();
#define myEPWM4_BASE EPWM4_BASE
#define myEPWM4_TBPRD 60000
#define myEPWM4_COUNTER_MODE EPWM_COUNTER_MODE_STOP_FREEZE
#define myEPWM4_TBPHS 0
#define myEPWM4_CMPA 0
#define myEPWM4_CMPB 0
#define myEPWM4_CMPC 0
#define myEPWM4_CMPD 0
#define myEPWM4_DBRED 150
#define myEPWM4_DBFED 150
#define myEPWM4_TZA_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM4_TZB_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM4_INTERRUPT_SOURCE EPWM_INT_TBCTR_DISABLED
void myEPWM4_init();
#define myEPWM5_BASE EPWM5_BASE
#define myEPWM5_TBPRD 60000
#define myEPWM5_COUNTER_MODE EPWM_COUNTER_MODE_STOP_FREEZE
#define myEPWM5_TBPHS 0
#define myEPWM5_CMPA 0
#define myEPWM5_CMPB 0
#define myEPWM5_CMPC 0
#define myEPWM5_CMPD 0
#define myEPWM5_DBRED 150
#define myEPWM5_DBFED 150
#define myEPWM5_TZA_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM5_TZB_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM5_INTERRUPT_SOURCE EPWM_INT_TBCTR_DISABLED
void myEPWM5_init();
#define myEPWM6_BASE EPWM6_BASE
#define myEPWM6_TBPRD 0
#define myEPWM6_COUNTER_MODE EPWM_COUNTER_MODE_STOP_FREEZE
#define myEPWM6_TBPHS 0
#define myEPWM6_CMPA 0
#define myEPWM6_CMPB 0
#define myEPWM6_CMPC 0
#define myEPWM6_CMPD 0
#define myEPWM6_DBRED 0
#define myEPWM6_DBFED 0
#define myEPWM6_TZA_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM6_TZB_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM6_INTERRUPT_SOURCE EPWM_INT_TBCTR_DISABLED
void myEPWM6_init();
#define myEPWM7_BASE EPWM7_BASE
#define myEPWM7_TBPRD 65535
#define myEPWM7_COUNTER_MODE EPWM_COUNTER_MODE_UP
#define myEPWM7_TBPHS 0
#define myEPWM7_CMPA 0
#define myEPWM7_CMPB 0
#define myEPWM7_CMPC 0
#define myEPWM7_CMPD 0
#define myEPWM7_DBRED 0
#define myEPWM7_DBFED 0
#define myEPWM7_TZA_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM7_TZB_ACTION EPWM_TZ_ACTION_HIGH_Z
#define myEPWM7_INTERRUPT_SOURCE EPWM_INT_TBCTR_DISABLED
void myEPWM7_init();

//*****************************************************************************
//
// GPIO Configurations
//
//*****************************************************************************
#define scope 57
void scope_init();

//*****************************************************************************
//
// INTERRUPT Configurations
//
//*****************************************************************************

// Interrupt Settings for INT_myEPWM1
// ISR need to be defined for the registered interrupts
#define INT_myEPWM1 INT_EPWM1
#define INT_myEPWM1_INTERRUPT_ACK_GROUP INTERRUPT_ACK_GROUP3
extern __interrupt void INT_myEPWM1_ISR(void);

// Interrupt Settings for INT_myEPWM2
// ISR need to be defined for the registered interrupts
#define INT_myEPWM2 INT_EPWM2
#define INT_myEPWM2_INTERRUPT_ACK_GROUP INTERRUPT_ACK_GROUP3
extern __interrupt void INT_myEPWM2_ISR(void);

// Interrupt Settings for INT_myEPWM3
// ISR need to be defined for the registered interrupts
#define INT_myEPWM3 INT_EPWM3
#define INT_myEPWM3_INTERRUPT_ACK_GROUP INTERRUPT_ACK_GROUP3
extern __interrupt void INT_myEPWM3_ISR(void);

// Interrupt Settings for INT_myMCANA_0
// ISR need to be defined for the registered interrupts
#define INT_myMCANA_0 INT_MCANA_0
#define INT_myMCANA_0_INTERRUPT_ACK_GROUP INTERRUPT_ACK_GROUP9
extern __interrupt void INT_myMCANA_0_ISR(void);

// Interrupt Settings for INT_myMCANA_1
// ISR need to be defined for the registered interrupts
#define INT_myMCANA_1 INT_MCANA_1
#define INT_myMCANA_1_INTERRUPT_ACK_GROUP INTERRUPT_ACK_GROUP9
extern __interrupt void INT_myMCANA_1_ISR(void);

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
#define myMCANA_MCAN_EXT_ID_FILTER_NUM          (0)
#define myMCANA_MCAN_FIFO_0_NUM                 (10)
#define myMCANA_MCAN_FIFO_1_NUM                 (0)
#define myMCANA_MCAN_TX_BUFF_SIZE               (5)
#define myMCANA_MCAN_TX_EVENT_SIZE              (0)

#define myMCANA_MCAN_STD_ID_FILT_START_ADDR     (0)
#define myMCANA_MCAN_EXT_ID_FILT_START_ADDR     (4)
#define myMCANA_MCAN_FIFO_0_START_ADDR          (4)
#define myMCANA_MCAN_FIFO_1_START_ADDR          (164)
#define myMCANA_MCAN_RX_BUFF_START_ADDR         (164)
#define myMCANA_MCAN_TX_BUFF_START_ADDR         (164)
#define myMCANA_MCAN_TX_EVENT_START_ADDR        (524)

#define myMCANA_MCAN_INTR_LINE_NUM_0_INT (MCAN_IR_RF0N_MASK)
void myMCANA_init();

//*****************************************************************************
//
// SPI Configurations
//
//*****************************************************************************
#define mySPIB_BASE SPIB_BASE
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
void	ADC_init();
void	ASYSCTL_init();
void	CLB_init();
void	DAC_init();
void	EPWM_init();
void	GPIO_init();
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

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

#include "board.h"

//*****************************************************************************
//
// Board Configurations
// Initializes the rest of the modules.
// Call this function in your application if you wish to do all module
// initialization.
// If you wish to not use some of the initializations, instead of the
// Board_init use the individual Module_inits
//
//*****************************************************************************
void Board_init()
{
	EALLOW;

	PinMux_init();
	SYSCTL_init();
	SYNC_init();
	ASYSCTL_init();
	ADC_init();
	CLB_init();
	DAC_init();
	EPWM_init();
	GPIO_init();
	MCAN_SYSCFG_init();
	SPI_init();
	INTERRUPT_init();

	EDIS;
}

//*****************************************************************************
//
// PINMUX Configurations
//
//*****************************************************************************
void PinMux_init()
{
	//
	// PinMux for modules assigned to CPU1
	//

	//
	// ANALOG -> myANALOGPinMux0 Pinmux
	//
	// Analog PinMux for A0, B15, C15, DACA_OUT
	GPIO_setPinConfig(GPIO_231_GPIO231);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(231, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A2, B6, C9, PGA1_INP, GPIO224
	GPIO_setPinConfig(GPIO_224_GPIO224);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(224, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A3, B9, C7, PGA1_INM
	GPIO_setPinConfig(GPIO_229_GPIO229);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(229, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A4, B8
	GPIO_setPinConfig(GPIO_225_GPIO225);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(225, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A5
	GPIO_setPinConfig(GPIO_249_GPIO249);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(249, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A6, D14, E14, GPIO228
	GPIO_setPinConfig(GPIO_228_GPIO228);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(228, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A7, B30, C3, D12, E30
	GPIO_setPinConfig(GPIO_245_GPIO245);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(245, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A8
	GPIO_setPinConfig(GPIO_240_GPIO240);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(240, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A9, GPIO227
	GPIO_setPinConfig(GPIO_227_GPIO227);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(227, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A10, B1, C10, GPIO230
	GPIO_setPinConfig(GPIO_230_GPIO230);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(230, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A11, B10, C0, PGA2_OUT
	GPIO_setPinConfig(GPIO_237_GPIO237);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(237, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A12, C5
	GPIO_setPinConfig(GPIO_238_GPIO238);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(238, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A14, B14, C4, PGA1_OUT
	GPIO_setPinConfig(GPIO_239_GPIO239);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(239, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A16, B16, C16, GPIO28
	GPIO_setPinConfig(GPIO_28_GPIO28);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(28, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A17, B17, C17, GPIO20
	GPIO_setPinConfig(GPIO_20_GPIO20);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(20, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A18, B18, C18, GPIO21
	GPIO_setPinConfig(GPIO_21_GPIO21);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(21, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A19, B19, C19, GPIO13
	GPIO_setPinConfig(GPIO_13_GPIO13);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(13, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A20, B20, C20, GPIO12
	GPIO_setPinConfig(GPIO_12_GPIO12);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(12, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A24, D0, E0, GPIO11
	GPIO_setPinConfig(GPIO_11_GPIO11);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(11, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A25, D3, E3, GPIO17
	GPIO_setPinConfig(GPIO_17_GPIO17);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(17, GPIO_ANALOG_ENABLED);
	// Analog PinMux for B0, C11, GPIO253
	GPIO_setPinConfig(GPIO_253_GPIO253);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(253, GPIO_ANALOG_ENABLED);
	// Analog PinMux for B2, C6, E12, GPIO226
	GPIO_setPinConfig(GPIO_226_GPIO226);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(226, GPIO_ANALOG_ENABLED);
	// Analog PinMux for B3, PGA2_INP, GPIO242
	GPIO_setPinConfig(GPIO_242_GPIO242);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(242, GPIO_ANALOG_ENABLED);
	// Analog PinMux for B4, C8, GPIO236
	GPIO_setPinConfig(GPIO_236_GPIO236);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(236, GPIO_ANALOG_ENABLED);
	// Analog PinMux for B5, D15, E15, PGA3_OUT
	GPIO_setPinConfig(GPIO_252_GPIO252);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(252, GPIO_ANALOG_ENABLED);
	// Analog PinMux for B11, D16, E16, PGA3_INM
	GPIO_setPinConfig(GPIO_251_GPIO251);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(251, GPIO_ANALOG_ENABLED);
	// Analog PinMux for B12, C2, PGA2_INM
	GPIO_setPinConfig(GPIO_244_GPIO244);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(244, GPIO_ANALOG_ENABLED);
	// Analog PinMux for B24, D1, E1, GPIO33
	GPIO_setPinConfig(GPIO_33_GPIO33);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(33, GPIO_ANALOG_ENABLED);
	// Analog PinMux for B25, D4, E4, GPIO24
	GPIO_setPinConfig(GPIO_24_GPIO24);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(24, GPIO_ANALOG_ENABLED);
	// Analog PinMux for C1, E11, PGA3_INP
	GPIO_setPinConfig(GPIO_248_GPIO248);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(248, GPIO_ANALOG_ENABLED);
	// Analog PinMux for C14, GPIO247
	GPIO_setPinConfig(GPIO_247_GPIO247);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(247, GPIO_ANALOG_ENABLED);
	// Analog PinMux for C24, D2, E2, GPIO16
	GPIO_setPinConfig(GPIO_16_GPIO16);
	// AGPIO -> Analog mode selected
	GPIO_setAnalogMode(16, GPIO_ANALOG_ENABLED);
	// Analog PinMux for D20, E20, VREFHI
	GPIO_setPinConfig(GPIO_234_GPIO234);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(234, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A13, B13, C13, D13, E13, VREFLO
	GPIO_setPinConfig(GPIO_235_GPIO235);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(235, GPIO_ANALOG_ENABLED);
	// Analog PinMux for A1, B7, D11, CMP1_DACL
	GPIO_setPinConfig(GPIO_232_GPIO232);
	// AIO -> Analog mode selected
	GPIO_setAnalogMode(232, GPIO_ANALOG_ENABLED);
	//
	// EPWM1 -> myEPWM1 Pinmux
	//
	GPIO_setPinConfig(myEPWM1_EPWMA_PIN_CONFIG);
	GPIO_setPadConfig(myEPWM1_EPWMA_GPIO, GPIO_PIN_TYPE_STD);
	GPIO_setQualificationMode(myEPWM1_EPWMA_GPIO, GPIO_QUAL_SYNC);

	GPIO_setPinConfig(myEPWM1_EPWMB_PIN_CONFIG);
	GPIO_setPadConfig(myEPWM1_EPWMB_GPIO, GPIO_PIN_TYPE_STD);
	GPIO_setQualificationMode(myEPWM1_EPWMB_GPIO, GPIO_QUAL_SYNC);

	//
	// EPWM2 -> myEPWM2 Pinmux
	//
	//
	// EPWM3 -> myEPWM3 Pinmux
	//
	//
	// EPWM7 -> myEPWM7 Pinmux
	//
	// GPIO57 -> scope Pinmux
	GPIO_setPinConfig(GPIO_57_GPIO57);
	//
	// MCANA -> myMCANA Pinmux
	//
	GPIO_setPinConfig(myMCANA_MCANRX_PIN_CONFIG);
	GPIO_setPadConfig(myMCANA_MCANRX_GPIO, GPIO_PIN_TYPE_STD);
	GPIO_setQualificationMode(myMCANA_MCANRX_GPIO, GPIO_QUAL_ASYNC);

	GPIO_setPinConfig(myMCANA_MCANTX_PIN_CONFIG);
	GPIO_setPadConfig(myMCANA_MCANTX_GPIO, GPIO_PIN_TYPE_STD);
	GPIO_setQualificationMode(myMCANA_MCANTX_GPIO, GPIO_QUAL_ASYNC);

	//
	// SPIB -> mySPIB Pinmux
	//

}

//*****************************************************************************
//
// ADC Configurations
//
//*****************************************************************************
void ADC_init(){
	myADCA_init();
	myADCB_init();
	myADCC_init();
}

void myADCA_init(){
	//
	// ADC Initialization: Write ADC configurations and power up the ADC
	//
	// Set the analog voltage reference selection and ADC module's offset trims.
	// This function sets the analog voltage reference to internal (with the reference voltage of 1.65V or 2.5V) or external for ADC
	// which is same as ASysCtl APIs.
	//
	ADC_setVREF(myADCA_BASE, ADC_REFERENCE_EXTERNAL, ADC_REFERENCE_2_5V);
	//
	// Configures the analog-to-digital converter module prescaler.
	//
	ADC_setPrescaler(myADCA_BASE, ADC_CLK_DIV_2_0);
	//
	// Sets the timing of the end-of-conversion pulse
	//
	ADC_setInterruptPulseMode(myADCA_BASE, ADC_PULSE_END_OF_CONV);
	//
	// Powers up the analog-to-digital converter core.
	//
	ADC_enableConverter(myADCA_BASE);
	//
	// Delay for 1ms to allow ADC time to power up
	//
	DEVICE_DELAY_US(500);
	//
	// Enable alternate timings for DMA trigger
	//
	ADC_enableAltDMATiming(myADCA_BASE);
	//
	// SOC Configuration: Setup ADC EPWM channel and trigger settings
	//
	// Disables SOC burst mode.
	//
	ADC_disableBurstMode(myADCA_BASE);
	//
	// Sets the priority mode of the SOCs.
	//
	ADC_setSOCPriority(myADCA_BASE, ADC_PRI_ALL_HIPRI);
	//
	// Start of Conversion 0 Configuration
	//
	//
	// Disables Sample Capacitor to reset after each conversion.
	//
	ADC_disableSampleCAPReset(myADCA_BASE, ADC_SOC_NUMBER0);
	//
	// Configures a start-of-conversion (SOC) in the ADC and its interrupt SOC trigger.
	// 	  	SOC number		: 0
	//	  	Trigger			: ADC_TRIGGER_EPWM1_SOCA
	//	  	Channel			: ADC_CH_ADCIN0
	//	 	Sample Window	: 12 SYSCLK cycles
	//		Interrupt Trigger: ADC_INT_SOC_TRIGGER_NONE
	//
	ADC_setupSOC(myADCA_BASE, ADC_SOC_NUMBER0, ADC_TRIGGER_EPWM1_SOCA, ADC_CH_ADCIN0, 12U);
	ADC_setInterruptSOCTrigger(myADCA_BASE, ADC_SOC_NUMBER0, ADC_INT_SOC_TRIGGER_NONE);
	//
	// Start of Conversion 1 Configuration
	//
	//
	// Disables Sample Capacitor to reset after each conversion.
	//
	ADC_disableSampleCAPReset(myADCA_BASE, ADC_SOC_NUMBER1);
	//
	// Configures a start-of-conversion (SOC) in the ADC and its interrupt SOC trigger.
	// 	  	SOC number		: 1
	//	  	Trigger			: ADC_TRIGGER_EPWM1_SOCA
	//	  	Channel			: ADC_CH_ADCIN4
	//	 	Sample Window	: 12 SYSCLK cycles
	//		Interrupt Trigger: ADC_INT_SOC_TRIGGER_NONE
	//
	ADC_setupSOC(myADCA_BASE, ADC_SOC_NUMBER1, ADC_TRIGGER_EPWM1_SOCA, ADC_CH_ADCIN4, 12U);
	ADC_setInterruptSOCTrigger(myADCA_BASE, ADC_SOC_NUMBER1, ADC_INT_SOC_TRIGGER_NONE);
	//
	// Start of Conversion 2 Configuration
	//
	//
	// Disables Sample Capacitor to reset after each conversion.
	//
	ADC_disableSampleCAPReset(myADCA_BASE, ADC_SOC_NUMBER2);
	//
	// Configures a start-of-conversion (SOC) in the ADC and its interrupt SOC trigger.
	// 	  	SOC number		: 2
	//	  	Trigger			: ADC_TRIGGER_EPWM1_SOCA
	//	  	Channel			: ADC_CH_ADCIN12
	//	 	Sample Window	: 12 SYSCLK cycles
	//		Interrupt Trigger: ADC_INT_SOC_TRIGGER_NONE
	//
	ADC_setupSOC(myADCA_BASE, ADC_SOC_NUMBER2, ADC_TRIGGER_EPWM1_SOCA, ADC_CH_ADCIN12, 12U);
	ADC_setInterruptSOCTrigger(myADCA_BASE, ADC_SOC_NUMBER2, ADC_INT_SOC_TRIGGER_NONE);
	//
	// ADC Interrupt 1 Configuration
	// 		Source	: ADC_INT_TRIGGER_EOC2
	// 		Interrupt Source: enabled
	//		Continuous Mode	: enabled
	//
	//
	ADC_setInterruptSource(myADCA_BASE, ADC_INT_NUMBER1, ADC_INT_TRIGGER_EOC2);
	ADC_clearInterruptStatus(myADCA_BASE, ADC_INT_NUMBER1);
	ADC_enableContinuousMode(myADCA_BASE, ADC_INT_NUMBER1);
	ADC_enableInterrupt(myADCA_BASE, ADC_INT_NUMBER1);

	//
	// PPB Configuration: Configure high and low limits detection for ADCPPB
	//
	// Post Processing Block 1 Configuration
	// 		Configures a post-processing block (PPB) in the ADC.
	// 		PPB Number				: 1
	// 		SOC/EOC number			: 0
	// 		Calibration Offset		: 0
	// 		Reference Offset		: 200
	// 		Two's Complement		: Disabled
	// 		Trip High Limit			: 2000
	// 		Trip Low Limit			: -2000
	// 		Clear PPB Event Flags	: Disabled
	// 		Accumulation Limit		: 0
	// 		SyncInput Source		: ADC_SYNCIN_DISABLE
	// 		Comparator Source		: ADC_PPB_COMPSOURCE_RESULT
	// 		Right Shift				: 0
	// 		Absolute value				: false
	//
	ADC_setupPPB(myADCA_BASE, ADC_PPB_NUMBER1, ADC_SOC_NUMBER0);
	ADC_disablePPBEvent(myADCA_BASE, ADC_PPB_NUMBER1, (ADC_EVT_TRIPHI | ADC_EVT_TRIPLO | ADC_EVT_ZERO));
	ADC_disablePPBEventInterrupt(myADCA_BASE, ADC_PPB_NUMBER1, (ADC_EVT_TRIPHI | ADC_EVT_TRIPLO | ADC_EVT_ZERO));
	ADC_setPPBCalibrationOffset(myADCA_BASE, ADC_PPB_NUMBER1, 0);
	ADC_setPPBReferenceOffset(myADCA_BASE, ADC_PPB_NUMBER1, 200);
	ADC_disablePPBTwosComplement(myADCA_BASE, ADC_PPB_NUMBER1);
	ADC_setPPBTripLimits(myADCA_BASE, ADC_PPB_NUMBER1, 2000, -2000);
	ADC_disablePPBEventCBCClear(myADCA_BASE, ADC_PPB_NUMBER1);
	ADC_setPPBCountLimit(myADCA_BASE, ADC_PPB_NUMBER1,0);
	ADC_selectPPBSyncInput(myADCA_BASE, ADC_PPB_NUMBER1,ADC_SYNCIN_DISABLE);
	ADC_selectPPBCompareSource(myADCA_BASE, ADC_PPB_NUMBER1,ADC_PPB_COMPSOURCE_RESULT);
	ADC_setPPBShiftValue(myADCA_BASE, ADC_PPB_NUMBER1,0);
	ADC_disablePPBAbsoluteValue(myADCA_BASE, ADC_PPB_NUMBER1);
	//
	// Post Processing Block 2 Configuration
	// 		Configures a post-processing block (PPB) in the ADC.
	// 		PPB Number				: 2
	// 		SOC/EOC number			: 1
	// 		Calibration Offset		: 0
	// 		Reference Offset		: 2048
	// 		Two's Complement		: Disabled
	// 		Trip High Limit			: 4000
	// 		Trip Low Limit			: 0
	// 		Clear PPB Event Flags	: Disabled
	// 		Accumulation Limit		: 0
	// 		SyncInput Source		: ADC_SYNCIN_DISABLE
	// 		Comparator Source		: ADC_PPB_COMPSOURCE_RESULT
	// 		Right Shift				: 0
	// 		Absolute value				: false
	//
	ADC_setupPPB(myADCA_BASE, ADC_PPB_NUMBER2, ADC_SOC_NUMBER1);
	ADC_disablePPBEvent(myADCA_BASE, ADC_PPB_NUMBER2, (ADC_EVT_TRIPHI | ADC_EVT_TRIPLO | ADC_EVT_ZERO));
	ADC_disablePPBEventInterrupt(myADCA_BASE, ADC_PPB_NUMBER2, (ADC_EVT_TRIPHI | ADC_EVT_TRIPLO | ADC_EVT_ZERO));
	ADC_setPPBCalibrationOffset(myADCA_BASE, ADC_PPB_NUMBER2, 0);
	ADC_setPPBReferenceOffset(myADCA_BASE, ADC_PPB_NUMBER2, 2048);
	ADC_disablePPBTwosComplement(myADCA_BASE, ADC_PPB_NUMBER2);
	ADC_setPPBTripLimits(myADCA_BASE, ADC_PPB_NUMBER2, 4000, 0);
	ADC_disablePPBEventCBCClear(myADCA_BASE, ADC_PPB_NUMBER2);
	ADC_setPPBCountLimit(myADCA_BASE, ADC_PPB_NUMBER2,0);
	ADC_selectPPBSyncInput(myADCA_BASE, ADC_PPB_NUMBER2,ADC_SYNCIN_DISABLE);
	ADC_selectPPBCompareSource(myADCA_BASE, ADC_PPB_NUMBER2,ADC_PPB_COMPSOURCE_RESULT);
	ADC_setPPBShiftValue(myADCA_BASE, ADC_PPB_NUMBER2,0);
	ADC_disablePPBAbsoluteValue(myADCA_BASE, ADC_PPB_NUMBER2);
	//
	// Post Processing Block 3 Configuration
	// 		Configures a post-processing block (PPB) in the ADC.
	// 		PPB Number				: 3
	// 		SOC/EOC number			: 2
	// 		Calibration Offset		: 0
	// 		Reference Offset		: 2048
	// 		Two's Complement		: Disabled
	// 		Trip High Limit			: 0
	// 		Trip Low Limit			: 0
	// 		Clear PPB Event Flags	: Disabled
	// 		Accumulation Limit		: 0
	// 		SyncInput Source		: ADC_SYNCIN_DISABLE
	// 		Comparator Source		: ADC_PPB_COMPSOURCE_RESULT
	// 		Right Shift				: 0
	// 		Absolute value				: false
	//
	ADC_setupPPB(myADCA_BASE, ADC_PPB_NUMBER3, ADC_SOC_NUMBER2);
	ADC_disablePPBEvent(myADCA_BASE, ADC_PPB_NUMBER3, (ADC_EVT_TRIPHI | ADC_EVT_TRIPLO | ADC_EVT_ZERO));
	ADC_disablePPBEventInterrupt(myADCA_BASE, ADC_PPB_NUMBER3, (ADC_EVT_TRIPHI | ADC_EVT_TRIPLO | ADC_EVT_ZERO));
	ADC_setPPBCalibrationOffset(myADCA_BASE, ADC_PPB_NUMBER3, 0);
	ADC_setPPBReferenceOffset(myADCA_BASE, ADC_PPB_NUMBER3, 2048);
	ADC_disablePPBTwosComplement(myADCA_BASE, ADC_PPB_NUMBER3);
	ADC_setPPBTripLimits(myADCA_BASE, ADC_PPB_NUMBER3, 0, 0);
	ADC_disablePPBEventCBCClear(myADCA_BASE, ADC_PPB_NUMBER3);
	ADC_setPPBCountLimit(myADCA_BASE, ADC_PPB_NUMBER3,0);
	ADC_selectPPBSyncInput(myADCA_BASE, ADC_PPB_NUMBER3,ADC_SYNCIN_DISABLE);
	ADC_selectPPBCompareSource(myADCA_BASE, ADC_PPB_NUMBER3,ADC_PPB_COMPSOURCE_RESULT);
	ADC_setPPBShiftValue(myADCA_BASE, ADC_PPB_NUMBER3,0);
	ADC_disablePPBAbsoluteValue(myADCA_BASE, ADC_PPB_NUMBER3);
}

void myADCB_init(){
	//
	// ADC Initialization: Write ADC configurations and power up the ADC
	//
	// Set the analog voltage reference selection and ADC module's offset trims.
	// This function sets the analog voltage reference to internal (with the reference voltage of 1.65V or 2.5V) or external for ADC
	// which is same as ASysCtl APIs.
	//
	ADC_setVREF(myADCB_BASE, ADC_REFERENCE_EXTERNAL, ADC_REFERENCE_2_5V);
	//
	// Configures the analog-to-digital converter module prescaler.
	//
	ADC_setPrescaler(myADCB_BASE, ADC_CLK_DIV_2_0);
	//
	// Sets the timing of the end-of-conversion pulse
	//
	ADC_setInterruptPulseMode(myADCB_BASE, ADC_PULSE_END_OF_CONV);
	//
	// Powers up the analog-to-digital converter core.
	//
	ADC_enableConverter(myADCB_BASE);
	//
	// Delay for 1ms to allow ADC time to power up
	//
	DEVICE_DELAY_US(500);
	//
	// Enable alternate timings for DMA trigger
	//
	ADC_enableAltDMATiming(myADCB_BASE);
	//
	// SOC Configuration: Setup ADC EPWM channel and trigger settings
	//
	// Disables SOC burst mode.
	//
	ADC_disableBurstMode(myADCB_BASE);
	//
	// Sets the priority mode of the SOCs.
	//
	ADC_setSOCPriority(myADCB_BASE, ADC_PRI_ALL_HIPRI);
	//
	// Start of Conversion 0 Configuration
	//
	//
	// Disables Sample Capacitor to reset after each conversion.
	//
	ADC_disableSampleCAPReset(myADCB_BASE, ADC_SOC_NUMBER0);
	//
	// Configures a start-of-conversion (SOC) in the ADC and its interrupt SOC trigger.
	// 	  	SOC number		: 0
	//	  	Trigger			: ADC_TRIGGER_EPWM2_SOCA
	//	  	Channel			: ADC_CH_ADCIN1
	//	 	Sample Window	: 120 SYSCLK cycles
	//		Interrupt Trigger: ADC_INT_SOC_TRIGGER_NONE
	//
	ADC_setupSOC(myADCB_BASE, ADC_SOC_NUMBER0, ADC_TRIGGER_EPWM2_SOCA, ADC_CH_ADCIN1, 120U);
	ADC_setInterruptSOCTrigger(myADCB_BASE, ADC_SOC_NUMBER0, ADC_INT_SOC_TRIGGER_NONE);
	//
	// Start of Conversion 1 Configuration
	//
	//
	// Disables Sample Capacitor to reset after each conversion.
	//
	ADC_disableSampleCAPReset(myADCB_BASE, ADC_SOC_NUMBER1);
	//
	// Configures a start-of-conversion (SOC) in the ADC and its interrupt SOC trigger.
	// 	  	SOC number		: 1
	//	  	Trigger			: ADC_TRIGGER_EPWM2_SOCA
	//	  	Channel			: ADC_CH_ADCIN7
	//	 	Sample Window	: 12 SYSCLK cycles
	//		Interrupt Trigger: ADC_INT_SOC_TRIGGER_NONE
	//
	ADC_setupSOC(myADCB_BASE, ADC_SOC_NUMBER1, ADC_TRIGGER_EPWM2_SOCA, ADC_CH_ADCIN7, 12U);
	ADC_setInterruptSOCTrigger(myADCB_BASE, ADC_SOC_NUMBER1, ADC_INT_SOC_TRIGGER_NONE);
	//
	// ADC Interrupt 1 Configuration
	// 		Source	: ADC_INT_TRIGGER_EOC1
	// 		Interrupt Source: enabled
	//		Continuous Mode	: enabled
	//
	//
	ADC_setInterruptSource(myADCB_BASE, ADC_INT_NUMBER1, ADC_INT_TRIGGER_EOC1);
	ADC_clearInterruptStatus(myADCB_BASE, ADC_INT_NUMBER1);
	ADC_enableContinuousMode(myADCB_BASE, ADC_INT_NUMBER1);
	ADC_enableInterrupt(myADCB_BASE, ADC_INT_NUMBER1);

	//
	// PPB Configuration: Configure high and low limits detection for ADCPPB
	//
	// Post Processing Block 1 Configuration
	// 		Configures a post-processing block (PPB) in the ADC.
	// 		PPB Number				: 1
	// 		SOC/EOC number			: 0
	// 		Calibration Offset		: 0
	// 		Reference Offset		: 2048
	// 		Two's Complement		: Disabled
	// 		Trip High Limit			: 0
	// 		Trip Low Limit			: 0
	// 		Clear PPB Event Flags	: Disabled
	// 		Accumulation Limit		: 0
	// 		SyncInput Source		: ADC_SYNCIN_DISABLE
	// 		Comparator Source		: ADC_PPB_COMPSOURCE_RESULT
	// 		Right Shift				: 0
	// 		Absolute value				: false
	//
	ADC_setupPPB(myADCB_BASE, ADC_PPB_NUMBER1, ADC_SOC_NUMBER0);
	ADC_disablePPBEvent(myADCB_BASE, ADC_PPB_NUMBER1, (ADC_EVT_TRIPHI | ADC_EVT_TRIPLO | ADC_EVT_ZERO));
	ADC_disablePPBEventInterrupt(myADCB_BASE, ADC_PPB_NUMBER1, (ADC_EVT_TRIPHI | ADC_EVT_TRIPLO | ADC_EVT_ZERO));
	ADC_setPPBCalibrationOffset(myADCB_BASE, ADC_PPB_NUMBER1, 0);
	ADC_setPPBReferenceOffset(myADCB_BASE, ADC_PPB_NUMBER1, 2048);
	ADC_disablePPBTwosComplement(myADCB_BASE, ADC_PPB_NUMBER1);
	ADC_setPPBTripLimits(myADCB_BASE, ADC_PPB_NUMBER1, 0, 0);
	ADC_disablePPBEventCBCClear(myADCB_BASE, ADC_PPB_NUMBER1);
	ADC_setPPBCountLimit(myADCB_BASE, ADC_PPB_NUMBER1,0);
	ADC_selectPPBSyncInput(myADCB_BASE, ADC_PPB_NUMBER1,ADC_SYNCIN_DISABLE);
	ADC_selectPPBCompareSource(myADCB_BASE, ADC_PPB_NUMBER1,ADC_PPB_COMPSOURCE_RESULT);
	ADC_setPPBShiftValue(myADCB_BASE, ADC_PPB_NUMBER1,0);
	ADC_disablePPBAbsoluteValue(myADCB_BASE, ADC_PPB_NUMBER1);
	//
	// Post Processing Block 2 Configuration
	// 		Configures a post-processing block (PPB) in the ADC.
	// 		PPB Number				: 2
	// 		SOC/EOC number			: 1
	// 		Calibration Offset		: 0
	// 		Reference Offset		: 2048
	// 		Two's Complement		: Disabled
	// 		Trip High Limit			: 0
	// 		Trip Low Limit			: 0
	// 		Clear PPB Event Flags	: Disabled
	// 		Accumulation Limit		: 0
	// 		SyncInput Source		: ADC_SYNCIN_DISABLE
	// 		Comparator Source		: ADC_PPB_COMPSOURCE_RESULT
	// 		Right Shift				: 0
	// 		Absolute value				: false
	//
	ADC_setupPPB(myADCB_BASE, ADC_PPB_NUMBER2, ADC_SOC_NUMBER1);
	ADC_disablePPBEvent(myADCB_BASE, ADC_PPB_NUMBER2, (ADC_EVT_TRIPHI | ADC_EVT_TRIPLO | ADC_EVT_ZERO));
	ADC_disablePPBEventInterrupt(myADCB_BASE, ADC_PPB_NUMBER2, (ADC_EVT_TRIPHI | ADC_EVT_TRIPLO | ADC_EVT_ZERO));
	ADC_setPPBCalibrationOffset(myADCB_BASE, ADC_PPB_NUMBER2, 0);
	ADC_setPPBReferenceOffset(myADCB_BASE, ADC_PPB_NUMBER2, 2048);
	ADC_disablePPBTwosComplement(myADCB_BASE, ADC_PPB_NUMBER2);
	ADC_setPPBTripLimits(myADCB_BASE, ADC_PPB_NUMBER2, 0, 0);
	ADC_disablePPBEventCBCClear(myADCB_BASE, ADC_PPB_NUMBER2);
	ADC_setPPBCountLimit(myADCB_BASE, ADC_PPB_NUMBER2,0);
	ADC_selectPPBSyncInput(myADCB_BASE, ADC_PPB_NUMBER2,ADC_SYNCIN_DISABLE);
	ADC_selectPPBCompareSource(myADCB_BASE, ADC_PPB_NUMBER2,ADC_PPB_COMPSOURCE_RESULT);
	ADC_setPPBShiftValue(myADCB_BASE, ADC_PPB_NUMBER2,0);
	ADC_disablePPBAbsoluteValue(myADCB_BASE, ADC_PPB_NUMBER2);
}

void myADCC_init(){
	//
	// ADC Initialization: Write ADC configurations and power up the ADC
	//
	// Set the analog voltage reference selection and ADC module's offset trims.
	// This function sets the analog voltage reference to internal (with the reference voltage of 1.65V or 2.5V) or external for ADC
	// which is same as ASysCtl APIs.
	//
	ADC_setVREF(myADCC_BASE, ADC_REFERENCE_EXTERNAL, ADC_REFERENCE_2_5V);
	//
	// Configures the analog-to-digital converter module prescaler.
	//
	ADC_setPrescaler(myADCC_BASE, ADC_CLK_DIV_2_0);
	//
	// Sets the timing of the end-of-conversion pulse
	//
	ADC_setInterruptPulseMode(myADCC_BASE, ADC_PULSE_END_OF_CONV);
	//
	// Powers up the analog-to-digital converter core.
	//
	ADC_enableConverter(myADCC_BASE);
	//
	// Delay for 1ms to allow ADC time to power up
	//
	DEVICE_DELAY_US(500);
	//
	// Enable alternate timings for DMA trigger
	//
	ADC_enableAltDMATiming(myADCC_BASE);
	//
	// SOC Configuration: Setup ADC EPWM channel and trigger settings
	//
	// Disables SOC burst mode.
	//
	ADC_disableBurstMode(myADCC_BASE);
	//
	// Sets the priority mode of the SOCs.
	//
	ADC_setSOCPriority(myADCC_BASE, ADC_PRI_ALL_ROUND_ROBIN);
	//
	// Start of Conversion 0 Configuration
	//
	//
	// Disables Sample Capacitor to reset after each conversion.
	//
	ADC_disableSampleCAPReset(myADCC_BASE, ADC_SOC_NUMBER0);
	//
	// Configures a start-of-conversion (SOC) in the ADC and its interrupt SOC trigger.
	// 	  	SOC number		: 0
	//	  	Trigger			: ADC_TRIGGER_EPWM2_SOCA
	//	  	Channel			: ADC_CH_ADCIN12
	//	 	Sample Window	: 512 SYSCLK cycles
	//		Interrupt Trigger: ADC_INT_SOC_TRIGGER_NONE
	//
	ADC_setupSOC(myADCC_BASE, ADC_SOC_NUMBER0, ADC_TRIGGER_EPWM2_SOCA, ADC_CH_ADCIN12, 512U);
	ADC_setInterruptSOCTrigger(myADCC_BASE, ADC_SOC_NUMBER0, ADC_INT_SOC_TRIGGER_NONE);
	//
	// ADC Interrupt 1 Configuration
	// 		Source	: ADC_INT_TRIGGER_EOC0
	// 		Interrupt Source: enabled
	//		Continuous Mode	: enabled
	//
	//
	ADC_setInterruptSource(myADCC_BASE, ADC_INT_NUMBER1, ADC_INT_TRIGGER_EOC0);
	ADC_clearInterruptStatus(myADCC_BASE, ADC_INT_NUMBER1);
	ADC_enableContinuousMode(myADCC_BASE, ADC_INT_NUMBER1);
	ADC_enableInterrupt(myADCC_BASE, ADC_INT_NUMBER1);
}


//*****************************************************************************
//
// ASYSCTL Configurations
//
//*****************************************************************************
void ASYSCTL_init(){
	//
	// asysctl initialization
	//
	// Disables the temperature sensor output to the ADC.
	//
	ASysCtl_disableTemperatureSensor();
	//
	// Set the analog voltage reference selection to external.
	//
	ASysCtl_setAnalogReferenceExternal( ASYSCTL_ANAREF_VREFHI_ADCA | ASYSCTL_ANAREF_VREFHI_ADCB | ASYSCTL_ANAREF_VREFHI_ADCC | ASYSCTL_ANAREF_VREFHI_ADCD | ASYSCTL_ANAREF_VREFHI_ADCE );

}

//*****************************************************************************
//
// CLB Configurations
//
//*****************************************************************************
void CLB_init(){
	myCLB0_init();
}

void myCLB0_init(){
	CLB_setOutputMask(myCLB0_BASE,
				(0UL << 0UL), true);
	CLB_enableOutputMaskUpdates(myCLB0_BASE);
	//
	// myCLB0 SPI Buffer Configuration
	//
	CLB_disableSPIBufferAccess(myCLB0_BASE);
	CLB_configSPIBufferLoadSignal(myCLB0_BASE, 0);
	CLB_configSPIBufferShift(myCLB0_BASE, 0);
	//
	// myCLB0 CLB_IN0 initialization
	//
	// The following functions configure the CLB input mux and whether the inputs
	// have synchronization or pipeline enabled; check the device manual for more
	// information on when a signal needs to be synchronized or go through a
	// pipeline filter
	//
	CLB_configLocalInputMux(myCLB0_BASE, CLB_IN0, CLB_LOCAL_IN_MUX_GLOBAL_IN);
	CLB_configGlobalInputMux(myCLB0_BASE, CLB_IN0, CLB_GLOBAL_IN_MUX_EPWM1A);
	CLB_configGPInputMux(myCLB0_BASE, CLB_IN0, CLB_GP_IN_MUX_EXTERNAL);
	CLB_enableSynchronization(myCLB0_BASE, CLB_IN0);
	CLB_selectInputFilter(myCLB0_BASE, CLB_IN0, CLB_FILTER_NONE);
	CLB_disableInputPipelineMode(myCLB0_BASE, CLB_IN0);
	//
	// myCLB0 CLB_IN1 initialization
	//
	// The following functions configure the CLB input mux and whether the inputs
	// have synchronization or pipeline enabled; check the device manual for more
	// information on when a signal needs to be synchronized or go through a
	// pipeline filter
	//
	CLB_configLocalInputMux(myCLB0_BASE, CLB_IN1, CLB_LOCAL_IN_MUX_GLOBAL_IN);
	CLB_configGlobalInputMux(myCLB0_BASE, CLB_IN1, CLB_GLOBAL_IN_MUX_EPWM1B);
	CLB_configGPInputMux(myCLB0_BASE, CLB_IN1, CLB_GP_IN_MUX_EXTERNAL);
	CLB_enableSynchronization(myCLB0_BASE, CLB_IN1);
	CLB_selectInputFilter(myCLB0_BASE, CLB_IN1, CLB_FILTER_NONE);
	CLB_disableInputPipelineMode(myCLB0_BASE, CLB_IN1);
	//
	// myCLB0 CLB_IN2 initialization
	//
	// The following functions configure the CLB input mux and whether the inputs
	// have synchronization or pipeline enabled; check the device manual for more
	// information on when a signal needs to be synchronized or go through a
	// pipeline filter
	//
	CLB_configLocalInputMux(myCLB0_BASE, CLB_IN2, CLB_LOCAL_IN_MUX_GLOBAL_IN);
	CLB_configGlobalInputMux(myCLB0_BASE, CLB_IN2, CLB_GLOBAL_IN_MUX_EPWM2A);
	CLB_configGPInputMux(myCLB0_BASE, CLB_IN2, CLB_GP_IN_MUX_EXTERNAL);
	CLB_enableSynchronization(myCLB0_BASE, CLB_IN2);
	CLB_selectInputFilter(myCLB0_BASE, CLB_IN2, CLB_FILTER_NONE);
	CLB_disableInputPipelineMode(myCLB0_BASE, CLB_IN2);
	//
	// myCLB0 CLB_IN3 initialization
	//
	// The following functions configure the CLB input mux and whether the inputs
	// have synchronization or pipeline enabled; check the device manual for more
	// information on when a signal needs to be synchronized or go through a
	// pipeline filter
	//
	CLB_configLocalInputMux(myCLB0_BASE, CLB_IN3, CLB_LOCAL_IN_MUX_GLOBAL_IN);
	CLB_configGlobalInputMux(myCLB0_BASE, CLB_IN3, CLB_GLOBAL_IN_MUX_EPWM2B);
	CLB_configGPInputMux(myCLB0_BASE, CLB_IN3, CLB_GP_IN_MUX_EXTERNAL);
	CLB_enableSynchronization(myCLB0_BASE, CLB_IN3);
	CLB_selectInputFilter(myCLB0_BASE, CLB_IN3, CLB_FILTER_NONE);
	CLB_disableInputPipelineMode(myCLB0_BASE, CLB_IN3);
	//
	// myCLB0 CLB_IN4 initialization
	//
	// The following functions configure the CLB input mux and whether the inputs
	// have synchronization or pipeline enabled; check the device manual for more
	// information on when a signal needs to be synchronized or go through a
	// pipeline filter
	//
	CLB_configLocalInputMux(myCLB0_BASE, CLB_IN4, CLB_LOCAL_IN_MUX_GLOBAL_IN);
	CLB_configGlobalInputMux(myCLB0_BASE, CLB_IN4, CLB_GLOBAL_IN_MUX_EPWM3A);
	CLB_configGPInputMux(myCLB0_BASE, CLB_IN4, CLB_GP_IN_MUX_EXTERNAL);
	CLB_enableSynchronization(myCLB0_BASE, CLB_IN4);
	CLB_selectInputFilter(myCLB0_BASE, CLB_IN4, CLB_FILTER_NONE);
	CLB_disableInputPipelineMode(myCLB0_BASE, CLB_IN4);
	//
	// myCLB0 CLB_IN5 initialization
	//
	// The following functions configure the CLB input mux and whether the inputs
	// have synchronization or pipeline enabled; check the device manual for more
	// information on when a signal needs to be synchronized or go through a
	// pipeline filter
	//
	CLB_configLocalInputMux(myCLB0_BASE, CLB_IN5, CLB_LOCAL_IN_MUX_GLOBAL_IN);
	CLB_configGlobalInputMux(myCLB0_BASE, CLB_IN5, CLB_GLOBAL_IN_MUX_EPWM3B);
	CLB_configGPInputMux(myCLB0_BASE, CLB_IN5, CLB_GP_IN_MUX_EXTERNAL);
	CLB_enableSynchronization(myCLB0_BASE, CLB_IN5);
	CLB_selectInputFilter(myCLB0_BASE, CLB_IN5, CLB_FILTER_NONE);
	CLB_disableInputPipelineMode(myCLB0_BASE, CLB_IN5);
	//
	// myCLB0 CLB_IN6 initialization
	//
	// The following functions configure the CLB input mux and whether the inputs
	// have synchronization or pipeline enabled; check the device manual for more
	// information on when a signal needs to be synchronized or go through a
	// pipeline filter
	//
	CLB_configLocalInputMux(myCLB0_BASE, CLB_IN6, CLB_LOCAL_IN_MUX_GLOBAL_IN);
	CLB_configGlobalInputMux(myCLB0_BASE, CLB_IN6, CLB_GLOBAL_IN_MUX_EPWM4A);
	CLB_configGPInputMux(myCLB0_BASE, CLB_IN6, CLB_GP_IN_MUX_EXTERNAL);
	CLB_enableSynchronization(myCLB0_BASE, CLB_IN6);
	CLB_selectInputFilter(myCLB0_BASE, CLB_IN6, CLB_FILTER_NONE);
	CLB_disableInputPipelineMode(myCLB0_BASE, CLB_IN6);
	//
	// myCLB0 CLB_IN7 initialization
	//
	// The following functions configure the CLB input mux and whether the inputs
	// have synchronization or pipeline enabled; check the device manual for more
	// information on when a signal needs to be synchronized or go through a
	// pipeline filter
	//
	CLB_configLocalInputMux(myCLB0_BASE, CLB_IN7, CLB_LOCAL_IN_MUX_GLOBAL_IN);
	CLB_configGlobalInputMux(myCLB0_BASE, CLB_IN7, CLB_GLOBAL_IN_MUX_EPWM4B);
	CLB_configGPInputMux(myCLB0_BASE, CLB_IN7, CLB_GP_IN_MUX_EXTERNAL);
	CLB_enableSynchronization(myCLB0_BASE, CLB_IN7);
	CLB_selectInputFilter(myCLB0_BASE, CLB_IN7, CLB_FILTER_NONE);
	CLB_disableInputPipelineMode(myCLB0_BASE, CLB_IN7);
	CLB_setGPREG(myCLB0_BASE,0);

	initTILE0(myCLB0_BASE);
	CLB_enableCLB(myCLB0_BASE);
}

//*****************************************************************************
//
// DAC Configurations
//
//*****************************************************************************
void DAC_init(){
	myDAC1_init();
}

void myDAC1_init(){
	//
	// Set DAC reference voltage.
	//
	DAC_setReferenceVoltage(myDAC1_BASE, DAC_REF_ADC_VREFHI);
	//
	// Set DAC gain mode.
	//
	DAC_setGainMode(myDAC1_BASE, DAC_GAIN_ONE);
	//
	// Set DAC load mode.
	//
	DAC_setLoadMode(myDAC1_BASE, DAC_LOAD_SYSCLK);
	//
	// Disable the DAC output
	//
	DAC_disableOutput(myDAC1_BASE);
	//
	// Set the DAC shadow output
	//
	DAC_setShadowValue(myDAC1_BASE, 0U);

	//
	// Delay for buffered DAC to power up.
	//
	DEVICE_DELAY_US(500);
}

//*****************************************************************************
//
// EPWM Configurations
//
//*****************************************************************************
void EPWM_init(){
	myEPWM1_init();
	myEPWM2_init();
	myEPWM3_init();
	myEPWM7_init();
}
void myEPWM1_init(){
    //EPWM_disableGlobalLoad(myEPWM1_BASE);	
    //EPWM_setGlobalLoadTrigger(myEPWM1_BASE, EPWM_GL_LOAD_PULSE_CNTR_ZERO);	
    //EPWM_setGlobalLoadEventPrescale(myEPWM1_BASE, 0);	
    //EPWM_disableGlobalLoadOneShotMode(myEPWM1_BASE);	
    EPWM_setEmulationMode(myEPWM1_BASE, EPWM_EMULATION_FREE_RUN);	
    EPWM_setClockPrescaler(myEPWM1_BASE, EPWM_CLOCK_DIVIDER_1, EPWM_HSCLOCK_DIVIDER_1);	
    //EPWM_setPeriodLoadMode(myEPWM1_BASE, EPWM_PERIOD_SHADOW_LOAD);	
    //EPWM_selectPeriodLoadEvent(myEPWM1_BASE, EPWM_SHADOW_LOAD_MODE_COUNTER_ZERO);	
    EPWM_setTimeBasePeriod(myEPWM1_BASE, 6000);	
    //EPWM_disableGlobalLoadRegisters(myEPWM1_BASE, EPWM_GL_REGISTER_TBPRD_TBPRDHR);	
    //EPWM_setTimeBaseCounter(myEPWM1_BASE, 0);	
    EPWM_setTimeBaseCounterMode(myEPWM1_BASE, EPWM_COUNTER_MODE_UP_DOWN);	
    EPWM_setCountModeAfterSync(myEPWM1_BASE, EPWM_COUNT_MODE_UP_AFTER_SYNC);	
    //EPWM_disablePhaseShiftLoad(myEPWM1_BASE);	
    //EPWM_setPhaseShift(myEPWM1_BASE, 0);	
    EPWM_setSyncInPulseSource(myEPWM1_BASE, EPWM_SYNC_IN_PULSE_SRC_DISABLE);	
    EPWM_enableSyncOutPulseSource(myEPWM1_BASE, EPWM_SYNC_OUT_PULSE_ON_CNTR_ZERO);	
    //EPWM_setOneShotSyncOutTrigger(myEPWM1_BASE, EPWM_OSHT_SYNC_OUT_TRIG_SYNC);	
    //EPWM_setSyncPulseSource(myEPWM1_BASE, HRPWM_PWMSYNC_SOURCE_PERIOD);	
    //EPWM_setCounterCompareValue(myEPWM1_BASE, EPWM_COUNTER_COMPARE_A, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM1_BASE, EPWM_GL_REGISTER_CMPA_CMPAHR);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM1_BASE, EPWM_COUNTER_COMPARE_A, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM1_BASE, EPWM_COUNTER_COMPARE_B, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM1_BASE, EPWM_GL_REGISTER_CMPB_CMPBHR);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM1_BASE, EPWM_COUNTER_COMPARE_B, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM1_BASE, EPWM_COUNTER_COMPARE_C, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM1_BASE, EPWM_GL_REGISTER_CMPC);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM1_BASE, EPWM_COUNTER_COMPARE_C, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM1_BASE, EPWM_COUNTER_COMPARE_D, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM1_BASE, EPWM_GL_REGISTER_CMPD);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM1_BASE, EPWM_COUNTER_COMPARE_D, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableGlobalLoadRegisters(myEPWM1_BASE, EPWM_GL_REGISTER_AQCSFRC);	
    //EPWM_setActionQualifierContSWForceShadowMode(myEPWM1_BASE, EPWM_AQ_SW_SH_LOAD_ON_CNTR_ZERO);	
    //EPWM_setActionQualifierT1TriggerSource(myEPWM1_BASE, EPWM_AQ_TRIGGER_EVENT_TRIG_DCA_1);	
    //EPWM_setActionQualifierT2TriggerSource(myEPWM1_BASE, EPWM_AQ_TRIGGER_EVENT_TRIG_DCA_1);	
    //EPWM_disableGlobalLoadRegisters(myEPWM1_BASE, EPWM_GL_REGISTER_AQCTLA_AQCTLA2);	
    //EPWM_disableActionQualifierShadowLoadMode(myEPWM1_BASE, EPWM_ACTION_QUALIFIER_A);	
    //EPWM_setActionQualifierSWAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE);	
    //EPWM_setActionQualifierContSWForceAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_SW_DISABLED);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_ZERO);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_PERIOD);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_DOWN);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_DOWN);	
    //EPWM_disableGlobalLoadRegisters(myEPWM1_BASE, EPWM_GL_REGISTER_AQCTLB_AQCTLB2);	
    //EPWM_disableActionQualifierShadowLoadMode(myEPWM1_BASE, EPWM_ACTION_QUALIFIER_B);	
    //EPWM_setActionQualifierSWAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE);	
    //EPWM_setActionQualifierContSWForceAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_SW_DISABLED);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_ZERO);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_PERIOD);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_DOWN);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM1_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_DOWN);	
    //EPWM_setRisingEdgeDeadBandDelayInput(myEPWM1_BASE, EPWM_DB_INPUT_EPWMA);	
    //EPWM_setFallingEdgeDeadBandDelayInput(myEPWM1_BASE, EPWM_DB_INPUT_EPWMA);	
    //EPWM_setDeadBandDelayPolarity(myEPWM1_BASE, EPWM_DB_RED, EPWM_DB_POLARITY_ACTIVE_HIGH);	
    //EPWM_setDeadBandDelayPolarity(myEPWM1_BASE, EPWM_DB_FED, EPWM_DB_POLARITY_ACTIVE_HIGH);	
    //EPWM_setDeadBandDelayMode(myEPWM1_BASE, EPWM_DB_RED, false);	
    //EPWM_setRisingEdgeDelayCountShadowLoadMode(myEPWM1_BASE, EPWM_RED_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableRisingEdgeDelayCountShadowLoadMode(myEPWM1_BASE);	
    //EPWM_setRisingEdgeDelayCount(myEPWM1_BASE, 0);	
    //EPWM_setDeadBandDelayMode(myEPWM1_BASE, EPWM_DB_FED, false);	
    //EPWM_setFallingEdgeDelayCountShadowLoadMode(myEPWM1_BASE, EPWM_FED_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableFallingEdgeDelayCountShadowLoadMode(myEPWM1_BASE);	
    //EPWM_setFallingEdgeDelayCount(myEPWM1_BASE, 0);	
    //EPWM_setDeadBandOutputSwapMode(myEPWM1_BASE, EPWM_DB_OUTPUT_A, false);	
    //EPWM_setDeadBandOutputSwapMode(myEPWM1_BASE, EPWM_DB_OUTPUT_B, false);	
    //EPWM_setDeadBandControlShadowLoadMode(myEPWM1_BASE, EPWM_DB_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableDeadBandControlShadowLoadMode(myEPWM1_BASE);	
    //EPWM_setDeadBandCounterClock(myEPWM1_BASE, EPWM_DB_COUNTER_CLOCK_FULL_CYCLE);	
    //EPWM_disableChopper(myEPWM1_BASE);	
    //EPWM_setChopperDutyCycle(myEPWM1_BASE, 0);	
    //EPWM_setChopperFreq(myEPWM1_BASE, 0);	
    //EPWM_setChopperFirstPulseWidth(myEPWM1_BASE, 0);	
    //EPWM_disableTripZoneAdvAction(myEPWM1_BASE);	
    //EPWM_setTripZoneAction(myEPWM1_BASE, EPWM_TZ_ACTION_EVENT_TZA, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM1_BASE, EPWM_TZ_ACTION_EVENT_TZB, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM1_BASE, EPWM_TZ_ACTION_EVENT_DCAEVT1, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM1_BASE, EPWM_TZ_ACTION_EVENT_DCAEVT2, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM1_BASE, EPWM_TZ_ACTION_EVENT_DCBEVT1, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM1_BASE, EPWM_TZ_ACTION_EVENT_DCBEVT2, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZB_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZB_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZA_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZA_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM1_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_selectCycleByCycleTripZoneClearEvent(myEPWM1_BASE, EPWM_TZ_CBC_PULSE_CLR_CNTR_ZERO);	
    //EPWM_selectDigitalCompareTripInput(myEPWM1_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCAH);	
    //EPWM_selectDigitalCompareTripInput(myEPWM1_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCAL);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM1_BASE, EPWM_TZ_DC_OUTPUT_A1, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM1_BASE, EPWM_TZ_DC_OUTPUT_A2, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_disableDigitalCompareADCTrigger(myEPWM1_BASE, EPWM_DC_MODULE_A);	
    //EPWM_disableDigitalCompareSyncEvent(myEPWM1_BASE, EPWM_DC_MODULE_A);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM1_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM1_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM1_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM1_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM1_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM1_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM1_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM1_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_selectDigitalCompareTripInput(myEPWM1_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCBH);	
    //EPWM_selectDigitalCompareTripInput(myEPWM1_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCBL);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM1_BASE, EPWM_TZ_DC_OUTPUT_B1, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM1_BASE, EPWM_TZ_DC_OUTPUT_B2, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_disableDigitalCompareADCTrigger(myEPWM1_BASE, EPWM_DC_MODULE_B);	
    //EPWM_disableDigitalCompareSyncEvent(myEPWM1_BASE, EPWM_DC_MODULE_B);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM1_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM1_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM1_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM1_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM1_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM1_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM1_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM1_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareFilterInput(myEPWM1_BASE, EPWM_DC_WINDOW_SOURCE_DCAEVT1);	
    //EPWM_disableDigitalCompareBlankingWindow(myEPWM1_BASE);	
    //EPWM_setDigitalCompareBlankingEvent(myEPWM1_BASE, EPWM_DC_WINDOW_START_TBCTR_PERIOD);	
    //EPWM_setDigitalCompareWindowOffset(myEPWM1_BASE, 0);	
    //EPWM_setDigitalCompareWindowLength(myEPWM1_BASE, 0);	
    //EPWM_disableDigitalCompareWindowInverseMode(myEPWM1_BASE);	
    //EPWM_disableDigitalCompareCounterCapture(myEPWM1_BASE);	
    //EPWM_setDigitalCompareCounterShadowMode(myEPWM1_BASE, false);	
    //EPWM_disableDigitalCompareEdgeFilter(myEPWM1_BASE);	
    //EPWM_setDigitalCompareEdgeFilterMode(myEPWM1_BASE, EPWM_DC_EDGEFILT_MODE_RISING);	
    //EPWM_setDigitalCompareEdgeFilterEdgeCount(myEPWM1_BASE, EPWM_DC_EDGEFILT_EDGECNT_0);	
    //EPWM_disableValleyCapture(myEPWM1_BASE);	
    //EPWM_setValleyTriggerSource(myEPWM1_BASE, EPWM_VALLEY_TRIGGER_EVENT_SOFTWARE);	
    //EPWM_setValleyTriggerEdgeCounts(myEPWM1_BASE, 0, 0);	
    //EPWM_disableValleyHWDelay(myEPWM1_BASE);	
    //EPWM_setValleySWDelayValue(myEPWM1_BASE, 0);	
    //EPWM_setValleyDelayDivider(myEPWM1_BASE, EPWM_VALLEY_DELAY_MODE_SW_DELAY);	
    EPWM_enableInterrupt(myEPWM1_BASE);	
    EPWM_setInterruptSource(myEPWM1_BASE, EPWM_INT_TBCTR_PERIOD);	
    EPWM_setInterruptEventCount(myEPWM1_BASE, 1);	
    //EPWM_disableInterruptEventCountInit(myEPWM1_BASE);	
    //EPWM_setInterruptEventCountInitValue(myEPWM1_BASE, 0);	
    //EPWM_disableADCTrigger(myEPWM1_BASE, EPWM_SOC_A);	
    //EPWM_setADCTriggerSource(myEPWM1_BASE, EPWM_SOC_A, EPWM_SOC_DCxEVT1);	
    //EPWM_setADCTriggerEventPrescale(myEPWM1_BASE, EPWM_SOC_A, 0);	
    //EPWM_disableADCTriggerEventCountInit(myEPWM1_BASE, EPWM_SOC_A);	
    //EPWM_setADCTriggerEventCountInitValue(myEPWM1_BASE, EPWM_SOC_A, 0);	
    //EPWM_disableADCTrigger(myEPWM1_BASE, EPWM_SOC_B);	
    //EPWM_setADCTriggerSource(myEPWM1_BASE, EPWM_SOC_B, EPWM_SOC_DCxEVT1);	
    //EPWM_setADCTriggerEventPrescale(myEPWM1_BASE, EPWM_SOC_B, 0);	
    //EPWM_disableADCTriggerEventCountInit(myEPWM1_BASE, EPWM_SOC_B);	
    //EPWM_setADCTriggerEventCountInitValue(myEPWM1_BASE, EPWM_SOC_B, 0);	
}
void myEPWM2_init(){
    //EPWM_disableGlobalLoad(myEPWM2_BASE);	
    //EPWM_setGlobalLoadTrigger(myEPWM2_BASE, EPWM_GL_LOAD_PULSE_CNTR_ZERO);	
    //EPWM_setGlobalLoadEventPrescale(myEPWM2_BASE, 0);	
    //EPWM_disableGlobalLoadOneShotMode(myEPWM2_BASE);	
    EPWM_setEmulationMode(myEPWM2_BASE, EPWM_EMULATION_FREE_RUN);	
    EPWM_setClockPrescaler(myEPWM2_BASE, EPWM_CLOCK_DIVIDER_1, EPWM_HSCLOCK_DIVIDER_1);	
    //EPWM_setPeriodLoadMode(myEPWM2_BASE, EPWM_PERIOD_SHADOW_LOAD);	
    //EPWM_selectPeriodLoadEvent(myEPWM2_BASE, EPWM_SHADOW_LOAD_MODE_COUNTER_ZERO);	
    EPWM_setTimeBasePeriod(myEPWM2_BASE, 6000);	
    //EPWM_disableGlobalLoadRegisters(myEPWM2_BASE, EPWM_GL_REGISTER_TBPRD_TBPRDHR);	
    //EPWM_setTimeBaseCounter(myEPWM2_BASE, 0);	
    //EPWM_setTimeBaseCounterMode(myEPWM2_BASE, EPWM_COUNTER_MODE_STOP_FREEZE);	
    //EPWM_setCountModeAfterSync(myEPWM2_BASE, EPWM_COUNT_MODE_DOWN_AFTER_SYNC);	
    //EPWM_disablePhaseShiftLoad(myEPWM2_BASE);	
    //EPWM_setPhaseShift(myEPWM2_BASE, 0);	
    //EPWM_setSyncInPulseSource(myEPWM2_BASE, EPWM_SYNC_IN_PULSE_SRC_SYNCOUT_EPWM1);	
    //EPWM_setOneShotSyncOutTrigger(myEPWM2_BASE, EPWM_OSHT_SYNC_OUT_TRIG_SYNC);	
    //EPWM_setSyncPulseSource(myEPWM2_BASE, HRPWM_PWMSYNC_SOURCE_PERIOD);	
    //EPWM_setCounterCompareValue(myEPWM2_BASE, EPWM_COUNTER_COMPARE_A, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM2_BASE, EPWM_GL_REGISTER_CMPA_CMPAHR);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM2_BASE, EPWM_COUNTER_COMPARE_A, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM2_BASE, EPWM_COUNTER_COMPARE_B, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM2_BASE, EPWM_GL_REGISTER_CMPB_CMPBHR);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM2_BASE, EPWM_COUNTER_COMPARE_B, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM2_BASE, EPWM_COUNTER_COMPARE_C, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM2_BASE, EPWM_GL_REGISTER_CMPC);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM2_BASE, EPWM_COUNTER_COMPARE_C, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM2_BASE, EPWM_COUNTER_COMPARE_D, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM2_BASE, EPWM_GL_REGISTER_CMPD);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM2_BASE, EPWM_COUNTER_COMPARE_D, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableGlobalLoadRegisters(myEPWM2_BASE, EPWM_GL_REGISTER_AQCSFRC);	
    //EPWM_setActionQualifierContSWForceShadowMode(myEPWM2_BASE, EPWM_AQ_SW_SH_LOAD_ON_CNTR_ZERO);	
    //EPWM_setActionQualifierT1TriggerSource(myEPWM2_BASE, EPWM_AQ_TRIGGER_EVENT_TRIG_DCA_1);	
    //EPWM_setActionQualifierT2TriggerSource(myEPWM2_BASE, EPWM_AQ_TRIGGER_EVENT_TRIG_DCA_1);	
    //EPWM_disableGlobalLoadRegisters(myEPWM2_BASE, EPWM_GL_REGISTER_AQCTLA_AQCTLA2);	
    //EPWM_disableActionQualifierShadowLoadMode(myEPWM2_BASE, EPWM_ACTION_QUALIFIER_A);	
    //EPWM_setActionQualifierSWAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE);	
    //EPWM_setActionQualifierContSWForceAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_SW_DISABLED);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_ZERO);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_PERIOD);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_DOWN);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_DOWN);	
    //EPWM_disableGlobalLoadRegisters(myEPWM2_BASE, EPWM_GL_REGISTER_AQCTLB_AQCTLB2);	
    //EPWM_disableActionQualifierShadowLoadMode(myEPWM2_BASE, EPWM_ACTION_QUALIFIER_B);	
    //EPWM_setActionQualifierSWAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE);	
    //EPWM_setActionQualifierContSWForceAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_SW_DISABLED);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_ZERO);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_PERIOD);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_DOWN);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM2_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_DOWN);	
    //EPWM_setRisingEdgeDeadBandDelayInput(myEPWM2_BASE, EPWM_DB_INPUT_EPWMA);	
    //EPWM_setFallingEdgeDeadBandDelayInput(myEPWM2_BASE, EPWM_DB_INPUT_EPWMA);	
    //EPWM_setDeadBandDelayPolarity(myEPWM2_BASE, EPWM_DB_RED, EPWM_DB_POLARITY_ACTIVE_HIGH);	
    //EPWM_setDeadBandDelayPolarity(myEPWM2_BASE, EPWM_DB_FED, EPWM_DB_POLARITY_ACTIVE_HIGH);	
    //EPWM_setDeadBandDelayMode(myEPWM2_BASE, EPWM_DB_RED, false);	
    //EPWM_setRisingEdgeDelayCountShadowLoadMode(myEPWM2_BASE, EPWM_RED_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableRisingEdgeDelayCountShadowLoadMode(myEPWM2_BASE);	
    //EPWM_setRisingEdgeDelayCount(myEPWM2_BASE, 0);	
    //EPWM_setDeadBandDelayMode(myEPWM2_BASE, EPWM_DB_FED, false);	
    //EPWM_setFallingEdgeDelayCountShadowLoadMode(myEPWM2_BASE, EPWM_FED_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableFallingEdgeDelayCountShadowLoadMode(myEPWM2_BASE);	
    //EPWM_setFallingEdgeDelayCount(myEPWM2_BASE, 0);	
    //EPWM_setDeadBandOutputSwapMode(myEPWM2_BASE, EPWM_DB_OUTPUT_A, false);	
    //EPWM_setDeadBandOutputSwapMode(myEPWM2_BASE, EPWM_DB_OUTPUT_B, false);	
    //EPWM_setDeadBandControlShadowLoadMode(myEPWM2_BASE, EPWM_DB_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableDeadBandControlShadowLoadMode(myEPWM2_BASE);	
    //EPWM_setDeadBandCounterClock(myEPWM2_BASE, EPWM_DB_COUNTER_CLOCK_FULL_CYCLE);	
    //EPWM_disableChopper(myEPWM2_BASE);	
    //EPWM_setChopperDutyCycle(myEPWM2_BASE, 0);	
    //EPWM_setChopperFreq(myEPWM2_BASE, 0);	
    //EPWM_setChopperFirstPulseWidth(myEPWM2_BASE, 0);	
    //EPWM_disableTripZoneAdvAction(myEPWM2_BASE);	
    //EPWM_setTripZoneAction(myEPWM2_BASE, EPWM_TZ_ACTION_EVENT_TZA, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM2_BASE, EPWM_TZ_ACTION_EVENT_TZB, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM2_BASE, EPWM_TZ_ACTION_EVENT_DCAEVT1, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM2_BASE, EPWM_TZ_ACTION_EVENT_DCAEVT2, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM2_BASE, EPWM_TZ_ACTION_EVENT_DCBEVT1, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM2_BASE, EPWM_TZ_ACTION_EVENT_DCBEVT2, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZB_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZB_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZA_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZA_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM2_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_selectCycleByCycleTripZoneClearEvent(myEPWM2_BASE, EPWM_TZ_CBC_PULSE_CLR_CNTR_ZERO);	
    //EPWM_selectDigitalCompareTripInput(myEPWM2_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCAH);	
    //EPWM_selectDigitalCompareTripInput(myEPWM2_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCAL);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM2_BASE, EPWM_TZ_DC_OUTPUT_A1, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM2_BASE, EPWM_TZ_DC_OUTPUT_A2, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_disableDigitalCompareADCTrigger(myEPWM2_BASE, EPWM_DC_MODULE_A);	
    //EPWM_disableDigitalCompareSyncEvent(myEPWM2_BASE, EPWM_DC_MODULE_A);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM2_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM2_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM2_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM2_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM2_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM2_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM2_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM2_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_selectDigitalCompareTripInput(myEPWM2_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCBH);	
    //EPWM_selectDigitalCompareTripInput(myEPWM2_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCBL);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM2_BASE, EPWM_TZ_DC_OUTPUT_B1, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM2_BASE, EPWM_TZ_DC_OUTPUT_B2, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_disableDigitalCompareADCTrigger(myEPWM2_BASE, EPWM_DC_MODULE_B);	
    //EPWM_disableDigitalCompareSyncEvent(myEPWM2_BASE, EPWM_DC_MODULE_B);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM2_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM2_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM2_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM2_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM2_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM2_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM2_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM2_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareFilterInput(myEPWM2_BASE, EPWM_DC_WINDOW_SOURCE_DCAEVT1);	
    //EPWM_disableDigitalCompareBlankingWindow(myEPWM2_BASE);	
    //EPWM_setDigitalCompareBlankingEvent(myEPWM2_BASE, EPWM_DC_WINDOW_START_TBCTR_PERIOD);	
    //EPWM_setDigitalCompareWindowOffset(myEPWM2_BASE, 0);	
    //EPWM_setDigitalCompareWindowLength(myEPWM2_BASE, 0);	
    //EPWM_disableDigitalCompareWindowInverseMode(myEPWM2_BASE);	
    //EPWM_disableDigitalCompareCounterCapture(myEPWM2_BASE);	
    //EPWM_setDigitalCompareCounterShadowMode(myEPWM2_BASE, false);	
    //EPWM_disableDigitalCompareEdgeFilter(myEPWM2_BASE);	
    //EPWM_setDigitalCompareEdgeFilterMode(myEPWM2_BASE, EPWM_DC_EDGEFILT_MODE_RISING);	
    //EPWM_setDigitalCompareEdgeFilterEdgeCount(myEPWM2_BASE, EPWM_DC_EDGEFILT_EDGECNT_0);	
    //EPWM_disableValleyCapture(myEPWM2_BASE);	
    //EPWM_setValleyTriggerSource(myEPWM2_BASE, EPWM_VALLEY_TRIGGER_EVENT_SOFTWARE);	
    //EPWM_setValleyTriggerEdgeCounts(myEPWM2_BASE, 0, 0);	
    //EPWM_disableValleyHWDelay(myEPWM2_BASE);	
    //EPWM_setValleySWDelayValue(myEPWM2_BASE, 0);	
    //EPWM_setValleyDelayDivider(myEPWM2_BASE, EPWM_VALLEY_DELAY_MODE_SW_DELAY);	
    //EPWM_disableInterrupt(myEPWM2_BASE);	
    //EPWM_setInterruptSource(myEPWM2_BASE, EPWM_INT_TBCTR_DISABLED);	
    //EPWM_setInterruptEventCount(myEPWM2_BASE, 0);	
    //EPWM_disableInterruptEventCountInit(myEPWM2_BASE);	
    //EPWM_setInterruptEventCountInitValue(myEPWM2_BASE, 0);	
    //EPWM_disableADCTrigger(myEPWM2_BASE, EPWM_SOC_A);	
    //EPWM_setADCTriggerSource(myEPWM2_BASE, EPWM_SOC_A, EPWM_SOC_DCxEVT1);	
    //EPWM_setADCTriggerEventPrescale(myEPWM2_BASE, EPWM_SOC_A, 0);	
    //EPWM_disableADCTriggerEventCountInit(myEPWM2_BASE, EPWM_SOC_A);	
    //EPWM_setADCTriggerEventCountInitValue(myEPWM2_BASE, EPWM_SOC_A, 0);	
    //EPWM_disableADCTrigger(myEPWM2_BASE, EPWM_SOC_B);	
    //EPWM_setADCTriggerSource(myEPWM2_BASE, EPWM_SOC_B, EPWM_SOC_DCxEVT1);	
    //EPWM_setADCTriggerEventPrescale(myEPWM2_BASE, EPWM_SOC_B, 0);	
    //EPWM_disableADCTriggerEventCountInit(myEPWM2_BASE, EPWM_SOC_B);	
    //EPWM_setADCTriggerEventCountInitValue(myEPWM2_BASE, EPWM_SOC_B, 0);	
}
void myEPWM3_init(){
    //EPWM_disableGlobalLoad(myEPWM3_BASE);	
    //EPWM_setGlobalLoadTrigger(myEPWM3_BASE, EPWM_GL_LOAD_PULSE_CNTR_ZERO);	
    //EPWM_setGlobalLoadEventPrescale(myEPWM3_BASE, 0);	
    //EPWM_disableGlobalLoadOneShotMode(myEPWM3_BASE);	
    EPWM_setEmulationMode(myEPWM3_BASE, EPWM_EMULATION_FREE_RUN);	
    EPWM_setClockPrescaler(myEPWM3_BASE, EPWM_CLOCK_DIVIDER_1, EPWM_HSCLOCK_DIVIDER_1);	
    //EPWM_setPeriodLoadMode(myEPWM3_BASE, EPWM_PERIOD_SHADOW_LOAD);	
    //EPWM_selectPeriodLoadEvent(myEPWM3_BASE, EPWM_SHADOW_LOAD_MODE_COUNTER_ZERO);	
    EPWM_setTimeBasePeriod(myEPWM3_BASE, 37500);	
    //EPWM_disableGlobalLoadRegisters(myEPWM3_BASE, EPWM_GL_REGISTER_TBPRD_TBPRDHR);	
    //EPWM_setTimeBaseCounter(myEPWM3_BASE, 0);	
    EPWM_setTimeBaseCounterMode(myEPWM3_BASE, EPWM_COUNTER_MODE_UP_DOWN);	
    EPWM_setCountModeAfterSync(myEPWM3_BASE, EPWM_COUNT_MODE_UP_AFTER_SYNC);	
    //EPWM_disablePhaseShiftLoad(myEPWM3_BASE);	
    //EPWM_setPhaseShift(myEPWM3_BASE, 0);	
    EPWM_setSyncInPulseSource(myEPWM3_BASE, EPWM_SYNC_IN_PULSE_SRC_DISABLE);	
    EPWM_enableSyncOutPulseSource(myEPWM3_BASE, EPWM_SYNC_OUT_PULSE_ON_CNTR_ZERO);	
    //EPWM_setOneShotSyncOutTrigger(myEPWM3_BASE, EPWM_OSHT_SYNC_OUT_TRIG_SYNC);	
    //EPWM_setSyncPulseSource(myEPWM3_BASE, HRPWM_PWMSYNC_SOURCE_PERIOD);	
    //EPWM_setCounterCompareValue(myEPWM3_BASE, EPWM_COUNTER_COMPARE_A, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM3_BASE, EPWM_GL_REGISTER_CMPA_CMPAHR);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM3_BASE, EPWM_COUNTER_COMPARE_A, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM3_BASE, EPWM_COUNTER_COMPARE_B, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM3_BASE, EPWM_GL_REGISTER_CMPB_CMPBHR);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM3_BASE, EPWM_COUNTER_COMPARE_B, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM3_BASE, EPWM_COUNTER_COMPARE_C, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM3_BASE, EPWM_GL_REGISTER_CMPC);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM3_BASE, EPWM_COUNTER_COMPARE_C, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM3_BASE, EPWM_COUNTER_COMPARE_D, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM3_BASE, EPWM_GL_REGISTER_CMPD);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM3_BASE, EPWM_COUNTER_COMPARE_D, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableGlobalLoadRegisters(myEPWM3_BASE, EPWM_GL_REGISTER_AQCSFRC);	
    //EPWM_setActionQualifierContSWForceShadowMode(myEPWM3_BASE, EPWM_AQ_SW_SH_LOAD_ON_CNTR_ZERO);	
    //EPWM_setActionQualifierT1TriggerSource(myEPWM3_BASE, EPWM_AQ_TRIGGER_EVENT_TRIG_DCA_1);	
    //EPWM_setActionQualifierT2TriggerSource(myEPWM3_BASE, EPWM_AQ_TRIGGER_EVENT_TRIG_DCA_1);	
    //EPWM_disableGlobalLoadRegisters(myEPWM3_BASE, EPWM_GL_REGISTER_AQCTLA_AQCTLA2);	
    //EPWM_disableActionQualifierShadowLoadMode(myEPWM3_BASE, EPWM_ACTION_QUALIFIER_A);	
    //EPWM_setActionQualifierSWAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE);	
    //EPWM_setActionQualifierContSWForceAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_SW_DISABLED);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_ZERO);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_PERIOD);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_DOWN);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_DOWN);	
    //EPWM_disableGlobalLoadRegisters(myEPWM3_BASE, EPWM_GL_REGISTER_AQCTLB_AQCTLB2);	
    //EPWM_disableActionQualifierShadowLoadMode(myEPWM3_BASE, EPWM_ACTION_QUALIFIER_B);	
    //EPWM_setActionQualifierSWAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE);	
    //EPWM_setActionQualifierContSWForceAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_SW_DISABLED);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_ZERO);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_PERIOD);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_DOWN);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM3_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_DOWN);	
    //EPWM_setRisingEdgeDeadBandDelayInput(myEPWM3_BASE, EPWM_DB_INPUT_EPWMA);	
    //EPWM_setFallingEdgeDeadBandDelayInput(myEPWM3_BASE, EPWM_DB_INPUT_EPWMA);	
    //EPWM_setDeadBandDelayPolarity(myEPWM3_BASE, EPWM_DB_RED, EPWM_DB_POLARITY_ACTIVE_HIGH);	
    //EPWM_setDeadBandDelayPolarity(myEPWM3_BASE, EPWM_DB_FED, EPWM_DB_POLARITY_ACTIVE_HIGH);	
    //EPWM_setDeadBandDelayMode(myEPWM3_BASE, EPWM_DB_RED, false);	
    //EPWM_setRisingEdgeDelayCountShadowLoadMode(myEPWM3_BASE, EPWM_RED_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableRisingEdgeDelayCountShadowLoadMode(myEPWM3_BASE);	
    //EPWM_setRisingEdgeDelayCount(myEPWM3_BASE, 0);	
    //EPWM_setDeadBandDelayMode(myEPWM3_BASE, EPWM_DB_FED, false);	
    //EPWM_setFallingEdgeDelayCountShadowLoadMode(myEPWM3_BASE, EPWM_FED_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableFallingEdgeDelayCountShadowLoadMode(myEPWM3_BASE);	
    //EPWM_setFallingEdgeDelayCount(myEPWM3_BASE, 0);	
    //EPWM_setDeadBandOutputSwapMode(myEPWM3_BASE, EPWM_DB_OUTPUT_A, false);	
    //EPWM_setDeadBandOutputSwapMode(myEPWM3_BASE, EPWM_DB_OUTPUT_B, false);	
    //EPWM_setDeadBandControlShadowLoadMode(myEPWM3_BASE, EPWM_DB_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableDeadBandControlShadowLoadMode(myEPWM3_BASE);	
    //EPWM_setDeadBandCounterClock(myEPWM3_BASE, EPWM_DB_COUNTER_CLOCK_FULL_CYCLE);	
    //EPWM_disableChopper(myEPWM3_BASE);	
    //EPWM_setChopperDutyCycle(myEPWM3_BASE, 0);	
    //EPWM_setChopperFreq(myEPWM3_BASE, 0);	
    //EPWM_setChopperFirstPulseWidth(myEPWM3_BASE, 0);	
    //EPWM_disableTripZoneAdvAction(myEPWM3_BASE);	
    //EPWM_setTripZoneAction(myEPWM3_BASE, EPWM_TZ_ACTION_EVENT_TZA, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM3_BASE, EPWM_TZ_ACTION_EVENT_TZB, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM3_BASE, EPWM_TZ_ACTION_EVENT_DCAEVT1, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM3_BASE, EPWM_TZ_ACTION_EVENT_DCAEVT2, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM3_BASE, EPWM_TZ_ACTION_EVENT_DCBEVT1, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM3_BASE, EPWM_TZ_ACTION_EVENT_DCBEVT2, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZB_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZB_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZA_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZA_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM3_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_selectCycleByCycleTripZoneClearEvent(myEPWM3_BASE, EPWM_TZ_CBC_PULSE_CLR_CNTR_ZERO);	
    //EPWM_selectDigitalCompareTripInput(myEPWM3_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCAH);	
    //EPWM_selectDigitalCompareTripInput(myEPWM3_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCAL);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM3_BASE, EPWM_TZ_DC_OUTPUT_A1, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM3_BASE, EPWM_TZ_DC_OUTPUT_A2, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_disableDigitalCompareADCTrigger(myEPWM3_BASE, EPWM_DC_MODULE_A);	
    //EPWM_disableDigitalCompareSyncEvent(myEPWM3_BASE, EPWM_DC_MODULE_A);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM3_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM3_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM3_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM3_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM3_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM3_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM3_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM3_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_selectDigitalCompareTripInput(myEPWM3_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCBH);	
    //EPWM_selectDigitalCompareTripInput(myEPWM3_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCBL);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM3_BASE, EPWM_TZ_DC_OUTPUT_B1, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM3_BASE, EPWM_TZ_DC_OUTPUT_B2, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_disableDigitalCompareADCTrigger(myEPWM3_BASE, EPWM_DC_MODULE_B);	
    //EPWM_disableDigitalCompareSyncEvent(myEPWM3_BASE, EPWM_DC_MODULE_B);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM3_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM3_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM3_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM3_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM3_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM3_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM3_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM3_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareFilterInput(myEPWM3_BASE, EPWM_DC_WINDOW_SOURCE_DCAEVT1);	
    //EPWM_disableDigitalCompareBlankingWindow(myEPWM3_BASE);	
    //EPWM_setDigitalCompareBlankingEvent(myEPWM3_BASE, EPWM_DC_WINDOW_START_TBCTR_PERIOD);	
    //EPWM_setDigitalCompareWindowOffset(myEPWM3_BASE, 0);	
    //EPWM_setDigitalCompareWindowLength(myEPWM3_BASE, 0);	
    //EPWM_disableDigitalCompareWindowInverseMode(myEPWM3_BASE);	
    //EPWM_disableDigitalCompareCounterCapture(myEPWM3_BASE);	
    //EPWM_setDigitalCompareCounterShadowMode(myEPWM3_BASE, false);	
    //EPWM_disableDigitalCompareEdgeFilter(myEPWM3_BASE);	
    //EPWM_setDigitalCompareEdgeFilterMode(myEPWM3_BASE, EPWM_DC_EDGEFILT_MODE_RISING);	
    //EPWM_setDigitalCompareEdgeFilterEdgeCount(myEPWM3_BASE, EPWM_DC_EDGEFILT_EDGECNT_0);	
    //EPWM_disableValleyCapture(myEPWM3_BASE);	
    //EPWM_setValleyTriggerSource(myEPWM3_BASE, EPWM_VALLEY_TRIGGER_EVENT_SOFTWARE);	
    //EPWM_setValleyTriggerEdgeCounts(myEPWM3_BASE, 0, 0);	
    //EPWM_disableValleyHWDelay(myEPWM3_BASE);	
    //EPWM_setValleySWDelayValue(myEPWM3_BASE, 0);	
    //EPWM_setValleyDelayDivider(myEPWM3_BASE, EPWM_VALLEY_DELAY_MODE_SW_DELAY);	
    EPWM_enableInterrupt(myEPWM3_BASE);	
    EPWM_setInterruptSource(myEPWM3_BASE, EPWM_INT_TBCTR_PERIOD);	
    EPWM_setInterruptEventCount(myEPWM3_BASE, 1);	
    //EPWM_disableInterruptEventCountInit(myEPWM3_BASE);	
    //EPWM_setInterruptEventCountInitValue(myEPWM3_BASE, 0);	
    //EPWM_disableADCTrigger(myEPWM3_BASE, EPWM_SOC_A);	
    //EPWM_setADCTriggerSource(myEPWM3_BASE, EPWM_SOC_A, EPWM_SOC_DCxEVT1);	
    //EPWM_setADCTriggerEventPrescale(myEPWM3_BASE, EPWM_SOC_A, 0);	
    //EPWM_disableADCTriggerEventCountInit(myEPWM3_BASE, EPWM_SOC_A);	
    //EPWM_setADCTriggerEventCountInitValue(myEPWM3_BASE, EPWM_SOC_A, 0);	
    //EPWM_disableADCTrigger(myEPWM3_BASE, EPWM_SOC_B);	
    //EPWM_setADCTriggerSource(myEPWM3_BASE, EPWM_SOC_B, EPWM_SOC_DCxEVT1);	
    //EPWM_setADCTriggerEventPrescale(myEPWM3_BASE, EPWM_SOC_B, 0);	
    //EPWM_disableADCTriggerEventCountInit(myEPWM3_BASE, EPWM_SOC_B);	
    //EPWM_setADCTriggerEventCountInitValue(myEPWM3_BASE, EPWM_SOC_B, 0);	
}
void myEPWM7_init(){
    //EPWM_disableGlobalLoad(myEPWM7_BASE);	
    //EPWM_setGlobalLoadTrigger(myEPWM7_BASE, EPWM_GL_LOAD_PULSE_CNTR_ZERO);	
    //EPWM_setGlobalLoadEventPrescale(myEPWM7_BASE, 0);	
    //EPWM_disableGlobalLoadOneShotMode(myEPWM7_BASE);	
    EPWM_setEmulationMode(myEPWM7_BASE, EPWM_EMULATION_FREE_RUN);	
    EPWM_setClockPrescaler(myEPWM7_BASE, EPWM_CLOCK_DIVIDER_1, EPWM_HSCLOCK_DIVIDER_1);	
    EPWM_setPeriodLoadMode(myEPWM7_BASE, EPWM_PERIOD_DIRECT_LOAD);	
    //EPWM_selectPeriodLoadEvent(myEPWM7_BASE, EPWM_SHADOW_LOAD_MODE_COUNTER_ZERO);	
    EPWM_setTimeBasePeriod(myEPWM7_BASE, 65535);	
    //EPWM_disableGlobalLoadRegisters(myEPWM7_BASE, EPWM_GL_REGISTER_TBPRD_TBPRDHR);	
    //EPWM_setTimeBaseCounter(myEPWM7_BASE, 0);	
    EPWM_setTimeBaseCounterMode(myEPWM7_BASE, EPWM_COUNTER_MODE_UP);	
    //EPWM_setCountModeAfterSync(myEPWM7_BASE, EPWM_COUNT_MODE_DOWN_AFTER_SYNC);	
    //EPWM_disablePhaseShiftLoad(myEPWM7_BASE);	
    //EPWM_setPhaseShift(myEPWM7_BASE, 0);	
    EPWM_setSyncInPulseSource(myEPWM7_BASE, EPWM_SYNC_IN_PULSE_SRC_DISABLE);	
    //EPWM_setOneShotSyncOutTrigger(myEPWM7_BASE, EPWM_OSHT_SYNC_OUT_TRIG_SYNC);	
    //EPWM_setSyncPulseSource(myEPWM7_BASE, HRPWM_PWMSYNC_SOURCE_PERIOD);	
    //EPWM_setCounterCompareValue(myEPWM7_BASE, EPWM_COUNTER_COMPARE_A, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM7_BASE, EPWM_GL_REGISTER_CMPA_CMPAHR);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM7_BASE, EPWM_COUNTER_COMPARE_A, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM7_BASE, EPWM_COUNTER_COMPARE_B, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM7_BASE, EPWM_GL_REGISTER_CMPB_CMPBHR);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM7_BASE, EPWM_COUNTER_COMPARE_B, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM7_BASE, EPWM_COUNTER_COMPARE_C, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM7_BASE, EPWM_GL_REGISTER_CMPC);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM7_BASE, EPWM_COUNTER_COMPARE_C, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_setCounterCompareValue(myEPWM7_BASE, EPWM_COUNTER_COMPARE_D, 0);	
    //EPWM_disableGlobalLoadRegisters(myEPWM7_BASE, EPWM_GL_REGISTER_CMPD);	
    //EPWM_setCounterCompareShadowLoadMode(myEPWM7_BASE, EPWM_COUNTER_COMPARE_D, EPWM_COMP_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableGlobalLoadRegisters(myEPWM7_BASE, EPWM_GL_REGISTER_AQCSFRC);	
    //EPWM_setActionQualifierContSWForceShadowMode(myEPWM7_BASE, EPWM_AQ_SW_SH_LOAD_ON_CNTR_ZERO);	
    //EPWM_setActionQualifierT1TriggerSource(myEPWM7_BASE, EPWM_AQ_TRIGGER_EVENT_TRIG_DCA_1);	
    //EPWM_setActionQualifierT2TriggerSource(myEPWM7_BASE, EPWM_AQ_TRIGGER_EVENT_TRIG_DCA_1);	
    //EPWM_disableGlobalLoadRegisters(myEPWM7_BASE, EPWM_GL_REGISTER_AQCTLA_AQCTLA2);	
    //EPWM_disableActionQualifierShadowLoadMode(myEPWM7_BASE, EPWM_ACTION_QUALIFIER_A);	
    //EPWM_setActionQualifierSWAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE);	
    //EPWM_setActionQualifierContSWForceAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_SW_DISABLED);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_ZERO);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_PERIOD);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_DOWN);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_A, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_DOWN);	
    //EPWM_disableGlobalLoadRegisters(myEPWM7_BASE, EPWM_GL_REGISTER_AQCTLB_AQCTLB2);	
    //EPWM_disableActionQualifierShadowLoadMode(myEPWM7_BASE, EPWM_ACTION_QUALIFIER_B);	
    //EPWM_setActionQualifierSWAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE);	
    //EPWM_setActionQualifierContSWForceAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_SW_DISABLED);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_ZERO);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_PERIOD);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPA);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_UP_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_TIMEBASE_DOWN_CMPB);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T1_COUNT_DOWN);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_UP);	
    //EPWM_setActionQualifierAction(myEPWM7_BASE, EPWM_AQ_OUTPUT_B, EPWM_AQ_OUTPUT_NO_CHANGE, EPWM_AQ_OUTPUT_ON_T2_COUNT_DOWN);	
    //EPWM_setRisingEdgeDeadBandDelayInput(myEPWM7_BASE, EPWM_DB_INPUT_EPWMA);	
    //EPWM_setFallingEdgeDeadBandDelayInput(myEPWM7_BASE, EPWM_DB_INPUT_EPWMA);	
    //EPWM_setDeadBandDelayPolarity(myEPWM7_BASE, EPWM_DB_RED, EPWM_DB_POLARITY_ACTIVE_HIGH);	
    //EPWM_setDeadBandDelayPolarity(myEPWM7_BASE, EPWM_DB_FED, EPWM_DB_POLARITY_ACTIVE_HIGH);	
    //EPWM_setDeadBandDelayMode(myEPWM7_BASE, EPWM_DB_RED, false);	
    //EPWM_setRisingEdgeDelayCountShadowLoadMode(myEPWM7_BASE, EPWM_RED_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableRisingEdgeDelayCountShadowLoadMode(myEPWM7_BASE);	
    //EPWM_setRisingEdgeDelayCount(myEPWM7_BASE, 0);	
    //EPWM_setDeadBandDelayMode(myEPWM7_BASE, EPWM_DB_FED, false);	
    //EPWM_setFallingEdgeDelayCountShadowLoadMode(myEPWM7_BASE, EPWM_FED_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableFallingEdgeDelayCountShadowLoadMode(myEPWM7_BASE);	
    //EPWM_setFallingEdgeDelayCount(myEPWM7_BASE, 0);	
    //EPWM_setDeadBandOutputSwapMode(myEPWM7_BASE, EPWM_DB_OUTPUT_A, false);	
    //EPWM_setDeadBandOutputSwapMode(myEPWM7_BASE, EPWM_DB_OUTPUT_B, false);	
    //EPWM_setDeadBandControlShadowLoadMode(myEPWM7_BASE, EPWM_DB_LOAD_ON_CNTR_ZERO);	
    //EPWM_disableDeadBandControlShadowLoadMode(myEPWM7_BASE);	
    //EPWM_setDeadBandCounterClock(myEPWM7_BASE, EPWM_DB_COUNTER_CLOCK_FULL_CYCLE);	
    //EPWM_disableChopper(myEPWM7_BASE);	
    //EPWM_setChopperDutyCycle(myEPWM7_BASE, 0);	
    //EPWM_setChopperFreq(myEPWM7_BASE, 0);	
    //EPWM_setChopperFirstPulseWidth(myEPWM7_BASE, 0);	
    //EPWM_disableTripZoneAdvAction(myEPWM7_BASE);	
    //EPWM_setTripZoneAction(myEPWM7_BASE, EPWM_TZ_ACTION_EVENT_TZA, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM7_BASE, EPWM_TZ_ACTION_EVENT_TZB, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM7_BASE, EPWM_TZ_ACTION_EVENT_DCAEVT1, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM7_BASE, EPWM_TZ_ACTION_EVENT_DCAEVT2, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM7_BASE, EPWM_TZ_ACTION_EVENT_DCBEVT1, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAction(myEPWM7_BASE, EPWM_TZ_ACTION_EVENT_DCBEVT2, EPWM_TZ_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZB_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZB_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZA_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvAction(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_TZA_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionA(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT1_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_U, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_setTripZoneAdvDigitalCompareActionB(myEPWM7_BASE, EPWM_TZ_ADV_ACTION_EVENT_DCxEVT2_D, EPWM_TZ_ADV_ACTION_HIGH_Z);	
    //EPWM_selectCycleByCycleTripZoneClearEvent(myEPWM7_BASE, EPWM_TZ_CBC_PULSE_CLR_CNTR_ZERO);	
    //EPWM_selectDigitalCompareTripInput(myEPWM7_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCAH);	
    //EPWM_selectDigitalCompareTripInput(myEPWM7_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCAL);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM7_BASE, EPWM_TZ_DC_OUTPUT_A1, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM7_BASE, EPWM_TZ_DC_OUTPUT_A2, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_disableDigitalCompareADCTrigger(myEPWM7_BASE, EPWM_DC_MODULE_A);	
    //EPWM_disableDigitalCompareSyncEvent(myEPWM7_BASE, EPWM_DC_MODULE_A);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM7_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM7_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM7_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM7_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM7_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM7_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM7_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM7_BASE, EPWM_DC_MODULE_A, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_selectDigitalCompareTripInput(myEPWM7_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCBH);	
    //EPWM_selectDigitalCompareTripInput(myEPWM7_BASE, EPWM_DC_TRIP_TRIPIN1, EPWM_DC_TYPE_DCBL);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM7_BASE, EPWM_TZ_DC_OUTPUT_B1, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_setTripZoneDigitalCompareEventCondition(myEPWM7_BASE, EPWM_TZ_DC_OUTPUT_B2, EPWM_TZ_EVENT_DC_DISABLED);	
    //EPWM_disableDigitalCompareADCTrigger(myEPWM7_BASE, EPWM_DC_MODULE_B);	
    //EPWM_disableDigitalCompareSyncEvent(myEPWM7_BASE, EPWM_DC_MODULE_B);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM7_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM7_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM7_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM7_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_1, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareEventSyncMode(myEPWM7_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_EVENT_INPUT_SYNCED);	
    //EPWM_setDigitalCompareEventSource(myEPWM7_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_EVENT_SOURCE_ORIG_SIGNAL);	
    //EPWM_setDigitalCompareCBCLatchMode(myEPWM7_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_DISABLED);	
    //EPWM_selectDigitalCompareCBCLatchClearEvent(myEPWM7_BASE, EPWM_DC_MODULE_B, EPWM_DC_EVENT_2, EPWM_DC_CBC_LATCH_CLR_CNTR_ZERO);	
    //EPWM_setDigitalCompareFilterInput(myEPWM7_BASE, EPWM_DC_WINDOW_SOURCE_DCAEVT1);	
    //EPWM_disableDigitalCompareBlankingWindow(myEPWM7_BASE);	
    //EPWM_setDigitalCompareBlankingEvent(myEPWM7_BASE, EPWM_DC_WINDOW_START_TBCTR_PERIOD);	
    //EPWM_setDigitalCompareWindowOffset(myEPWM7_BASE, 0);	
    //EPWM_setDigitalCompareWindowLength(myEPWM7_BASE, 0);	
    //EPWM_disableDigitalCompareWindowInverseMode(myEPWM7_BASE);	
    //EPWM_disableDigitalCompareCounterCapture(myEPWM7_BASE);	
    //EPWM_setDigitalCompareCounterShadowMode(myEPWM7_BASE, false);	
    //EPWM_disableDigitalCompareEdgeFilter(myEPWM7_BASE);	
    //EPWM_setDigitalCompareEdgeFilterMode(myEPWM7_BASE, EPWM_DC_EDGEFILT_MODE_RISING);	
    //EPWM_setDigitalCompareEdgeFilterEdgeCount(myEPWM7_BASE, EPWM_DC_EDGEFILT_EDGECNT_0);	
    //EPWM_disableValleyCapture(myEPWM7_BASE);	
    //EPWM_setValleyTriggerSource(myEPWM7_BASE, EPWM_VALLEY_TRIGGER_EVENT_SOFTWARE);	
    //EPWM_setValleyTriggerEdgeCounts(myEPWM7_BASE, 0, 0);	
    //EPWM_disableValleyHWDelay(myEPWM7_BASE);	
    //EPWM_setValleySWDelayValue(myEPWM7_BASE, 0);	
    //EPWM_setValleyDelayDivider(myEPWM7_BASE, EPWM_VALLEY_DELAY_MODE_SW_DELAY);	
    //EPWM_disableInterrupt(myEPWM7_BASE);	
    //EPWM_setInterruptSource(myEPWM7_BASE, EPWM_INT_TBCTR_DISABLED);	
    //EPWM_setInterruptEventCount(myEPWM7_BASE, 0);	
    //EPWM_disableInterruptEventCountInit(myEPWM7_BASE);	
    //EPWM_setInterruptEventCountInitValue(myEPWM7_BASE, 0);	
    //EPWM_disableADCTrigger(myEPWM7_BASE, EPWM_SOC_A);	
    //EPWM_setADCTriggerSource(myEPWM7_BASE, EPWM_SOC_A, EPWM_SOC_DCxEVT1);	
    //EPWM_setADCTriggerEventPrescale(myEPWM7_BASE, EPWM_SOC_A, 0);	
    //EPWM_disableADCTriggerEventCountInit(myEPWM7_BASE, EPWM_SOC_A);	
    //EPWM_setADCTriggerEventCountInitValue(myEPWM7_BASE, EPWM_SOC_A, 0);	
    //EPWM_disableADCTrigger(myEPWM7_BASE, EPWM_SOC_B);	
    //EPWM_setADCTriggerSource(myEPWM7_BASE, EPWM_SOC_B, EPWM_SOC_DCxEVT1);	
    //EPWM_setADCTriggerEventPrescale(myEPWM7_BASE, EPWM_SOC_B, 0);	
    //EPWM_disableADCTriggerEventCountInit(myEPWM7_BASE, EPWM_SOC_B);	
    //EPWM_setADCTriggerEventCountInitValue(myEPWM7_BASE, EPWM_SOC_B, 0);	
}

//*****************************************************************************
//
// GPIO Configurations
//
//*****************************************************************************
void GPIO_init(){
	scope_init();
}

void scope_init(){
	GPIO_setPadConfig(scope, GPIO_PIN_TYPE_STD);
	GPIO_setQualificationMode(scope, GPIO_QUAL_SYNC);
	GPIO_setDirectionMode(scope, GPIO_DIR_MODE_OUT);
	GPIO_setControllerCore(scope, GPIO_CORE_CPU1);
}

//*****************************************************************************
//
// INTERRUPT Configurations
//
//*****************************************************************************
void INTERRUPT_init(){
	
	// Interrupt Settings for INT_myEPWM1
	// ISR need to be defined for the registered interrupts
	Interrupt_register(INT_myEPWM1, &INT_myEPWM1_ISR);
	Interrupt_enable(INT_myEPWM1);
	
	// Interrupt Settings for INT_myEPWM3
	// ISR need to be defined for the registered interrupts
	Interrupt_register(INT_myEPWM3, &INT_myEPWM3_ISR);
	Interrupt_enable(INT_myEPWM3);
	
	// Interrupt Settings for INT_myMCANA_0
	// ISR need to be defined for the registered interrupts
	Interrupt_register(INT_myMCANA_0, &INT_myMCANA_0_ISR);
	Interrupt_enable(INT_myMCANA_0);
	
	// Interrupt Settings for INT_myMCANA_1
	// ISR need to be defined for the registered interrupts
	Interrupt_register(INT_myMCANA_1, &INT_myMCANA_1_ISR);
	Interrupt_enable(INT_myMCANA_1);
}
//*****************************************************************************
//
// MCAN Configurations
//
//*****************************************************************************
void MCAN_SYSCFG_init(){
    myMCANA_init();
}

void myMCANA_init(){
    MCAN_RevisionId revid_myMCANA;
    MCAN_InitParams initParams_myMCANA;
    MCAN_ConfigParams configParams_myMCANA;
    MCAN_StdMsgIDFilterElement stdFiltelem_myMCANA;
    MCAN_MsgRAMConfigParams    msgRAMConfigParams_myMCANA;
    MCAN_BitTimingParams       bitTimes_myMCANA;
    //
    // Initialize MCAN Init parameters.
    //
    initParams_myMCANA.fdMode            = true;
    initParams_myMCANA.brsEnable         = true;
    initParams_myMCANA.txpEnable         = true;
    initParams_myMCANA.efbi              = false;
    initParams_myMCANA.pxhddisable       = false;
    initParams_myMCANA.darEnable         = true;
    initParams_myMCANA.wkupReqEnable     = false;
    initParams_myMCANA.autoWkupEnable    = false;
    initParams_myMCANA.emulationEnable   = true;
    initParams_myMCANA.tdcEnable         = true;
    initParams_myMCANA.wdcPreload        = 255;
    //
    // Transmitter Delay Compensation parameters.
    //
    initParams_myMCANA.tdcConfig.tdcf    = 10;
    initParams_myMCANA.tdcConfig.tdco    = 33;
    //
    // Initialize MCAN Config parameters.
    //
    configParams_myMCANA.monEnable         = false;
    configParams_myMCANA.asmEnable         = false;
    configParams_myMCANA.tsPrescalar       = 15;
    configParams_myMCANA.tsSelect          = 0;
    configParams_myMCANA.timeoutSelect     = MCAN_TIMEOUT_SELECT_RX_FIFO0;
    configParams_myMCANA.timeoutPreload    = 65535;
    configParams_myMCANA.timeoutCntEnable  = false;
    configParams_myMCANA.filterConfig.rrfs = true;
    configParams_myMCANA.filterConfig.rrfe = true;
    configParams_myMCANA.filterConfig.anfe = 2;
    configParams_myMCANA.filterConfig.anfs = 2;
    //
    // Initialize Message RAM Sections Configuration Parameters.
    //
    msgRAMConfigParams_myMCANA.flssa                = myMCANA_MCAN_STD_ID_FILT_START_ADDR;
    //
    // Standard ID Filter List Start Address.
    //
    msgRAMConfigParams_myMCANA.lss                  = myMCANA_MCAN_STD_ID_FILTER_NUM;
    //
    // List Size: Standard ID.
    //
    msgRAMConfigParams_myMCANA.flesa                = myMCANA_MCAN_EXT_ID_FILT_START_ADDR;
    //
    // Extended ID Filter List Start Address.
    //
    msgRAMConfigParams_myMCANA.lse                  = myMCANA_MCAN_EXT_ID_FILTER_NUM;
    //
    // List Size: Extended ID.
    //
    msgRAMConfigParams_myMCANA.txStartAddr          = myMCANA_MCAN_TX_BUFF_START_ADDR;
    //
    // Tx Buffers Start Address.
    //
    msgRAMConfigParams_myMCANA.txBufNum             = myMCANA_MCAN_TX_BUFF_SIZE;
    //
    // Number of Dedicated Transmit Buffers.
    //
    msgRAMConfigParams_myMCANA.txFIFOSize           = 0;
    msgRAMConfigParams_myMCANA.txBufMode            = 0;
    msgRAMConfigParams_myMCANA.txBufElemSize        = 7;
    //
    // Tx Buffer Element Size.
    //
    msgRAMConfigParams_myMCANA.txEventFIFOStartAddr = myMCANA_MCAN_TX_EVENT_START_ADDR;
    //
    // Tx Event FIFO Start Address.
    //
    msgRAMConfigParams_myMCANA.txEventFIFOSize      = myMCANA_MCAN_TX_EVENT_SIZE;
    //
    // Event FIFO Size.
    //
    msgRAMConfigParams_myMCANA.txEventFIFOWaterMark = 0;
    //
    // Level for Tx Event FIFO watermark interrupt.
    //
    msgRAMConfigParams_myMCANA.rxFIFO0startAddr     = myMCANA_MCAN_FIFO_0_START_ADDR;
    //
    // Rx FIFO0 Start Address.
    //
    msgRAMConfigParams_myMCANA.rxFIFO0size          = myMCANA_MCAN_FIFO_0_NUM;
    //
    // Number of Rx FIFO elements.
    //
    msgRAMConfigParams_myMCANA.rxFIFO0waterMark     = 0; // Rx FIFO0 Watermark.
    msgRAMConfigParams_myMCANA.rxFIFO0OpMode        = 1;
    msgRAMConfigParams_myMCANA.rxFIFO1startAddr     = myMCANA_MCAN_FIFO_1_START_ADDR;
    //
    // Rx FIFO1 Start Address.
    //
    msgRAMConfigParams_myMCANA.rxFIFO1size          = myMCANA_MCAN_FIFO_1_NUM;
    //
    // Number of Rx FIFO elements.
    //
    msgRAMConfigParams_myMCANA.rxFIFO1waterMark     = 0; // Level for Rx FIFO 1
                                                  // watermark interrupt.
    msgRAMConfigParams_myMCANA.rxFIFO1OpMode        = 1; // FIFO blocking mode.
    msgRAMConfigParams_myMCANA.rxBufStartAddr       = myMCANA_MCAN_RX_BUFF_START_ADDR;
    //
    // Rx Buffer Start Address.
    //
    msgRAMConfigParams_myMCANA.rxBufElemSize        = 0;
    //
    // Rx Buffer Element Size.
    //
    msgRAMConfigParams_myMCANA.rxFIFO0ElemSize      = 0;
    //
    // Rx FIFO0 Element Size.
    //
    msgRAMConfigParams_myMCANA.rxFIFO1ElemSize      = 7;
    //
    // Rx FIFO1 Element Size.
    //
    //
    // Initialize bit timings.
    //
    bitTimes_myMCANA.nomRatePrescalar   = 5; // Nominal Baud Rate Pre-scaler.
    bitTimes_myMCANA.nomTimeSeg1        = 17; // Nominal Time segment before sample point.
    bitTimes_myMCANA.nomTimeSeg2        = 5; // Nominal Time segment after sample point.
    bitTimes_myMCANA.nomSynchJumpWidth  = 4; // Nominal (Re)Synchronization Jump Width Range.
    bitTimes_myMCANA.dataRatePrescalar  = 1; // Data Baud Rate Pre-scaler.
    bitTimes_myMCANA.dataTimeSeg1       = 21; // Data Time segment before sample point.
    bitTimes_myMCANA.dataTimeSeg2       = 6; // Data Time segment after sample point.
    bitTimes_myMCANA.dataSynchJumpWidth = 5; // Data (Re)Synchronization Jump Width.
    //
    // Get MCANSS Revision ID.
    //
    MCAN_getRevisionId(myMCANA_BASE, &revid_myMCANA);
    //
    // Wait for Memory initialization to be completed.
    //
    while(0 == MCAN_isMemInitDone(myMCANA_BASE));
    //
    // Put MCAN in SW initialization mode.
    //
    MCAN_setOpMode(myMCANA_BASE, MCAN_OPERATION_MODE_SW_INIT);
    //
    // Wait till MCAN is not initialized.
    //
    while (MCAN_OPERATION_MODE_SW_INIT != MCAN_getOpMode(myMCANA_BASE));
    //
    // Initialize MCAN module.
    //
    MCAN_init(myMCANA_BASE, &initParams_myMCANA);
    //
    // Configure MCAN module.
    //
    MCAN_config(myMCANA_BASE, &configParams_myMCANA);
    //
    // Configure Bit timings.
    //
    MCAN_setBitTime(myMCANA_BASE, &bitTimes_myMCANA);
    //
    // Configure Message RAM Sections
    //
    MCAN_msgRAMConfig(myMCANA_BASE, &msgRAMConfigParams_myMCANA);

    stdFiltelem_myMCANA.sft = MCAN_STDFILT_RANGE;
    stdFiltelem_myMCANA.sfec = MCAN_STDFILTEC_FIFO0;
    stdFiltelem_myMCANA.sfid1 = 2;
    stdFiltelem_myMCANA.sfid2 = 2;

    //
    // Configure Standard ID filter element 0
    //
    MCAN_addStdMsgIDFilter(myMCANA_BASE, 0U, &stdFiltelem_myMCANA);
    //
    // Internal loopback mode
    //
    MCAN_lpbkModeEnable(myMCANA_BASE, MCAN_LPBK_MODE_EXTERNAL, false);
    //
    // Take MCAN out of the SW initialization mode
    //
    MCAN_setOpMode(myMCANA_BASE, MCAN_OPERATION_MODE_NORMAL);
    while (MCAN_OPERATION_MODE_NORMAL != MCAN_getOpMode(myMCANA_BASE));
    //
    // Enable MCAN Interrupts
    //
    MCAN_enableIntr(myMCANA_BASE, MCAN_IR_BO_MASK|MCAN_IR_EP_MASK|MCAN_IR_PED_MASK|MCAN_IR_RF0N_MASK|MCAN_IR_TCF_MASK, 1U);
    MCAN_selectIntrLine(myMCANA_BASE, MCAN_IR_RF0N_MASK, MCAN_INTR_LINE_NUM_0);
    MCAN_enableIntrLine(myMCANA_BASE, MCAN_INTR_LINE_NUM_0, 1U);
}

//*****************************************************************************
//
// SPI Configurations
//
//*****************************************************************************
void SPI_init(){
	mySPIB_init();
}

void mySPIB_init(){
	SPI_disableModule(mySPIB_BASE);
	SPI_setConfig(mySPIB_BASE, DEVICE_LSPCLK_FREQ, SPI_PROT_POL0PHA0,
				  SPI_MODE_PERIPHERAL, mySPIB_BITRATE, mySPIB_DATAWIDTH);
	SPI_setPTESignalPolarity(mySPIB_BASE, SPI_PTE_ACTIVE_LOW);
	SPI_enableFIFO(mySPIB_BASE);
	SPI_setFIFOInterruptLevel(mySPIB_BASE, SPI_FIFO_TXEMPTY, SPI_FIFO_RXEMPTY);
	SPI_disableLoopback(mySPIB_BASE);
	SPI_setEmulationMode(mySPIB_BASE, SPI_EMULATION_STOP_MIDWAY);
	SPI_enableModule(mySPIB_BASE);
}

//*****************************************************************************
//
// SYNC Scheme Configurations
//
//*****************************************************************************
void SYNC_init(){
	SysCtl_setSyncOutputConfig(SYSCTL_SYNC_OUT_SRC_EPWM1SYNCOUT);
	//
	// SOCA
	//
	SysCtl_enableExtADCSOCSource(0);
	//
	// SOCB
	//
	SysCtl_enableExtADCSOCSource(0);
}
//*****************************************************************************
//
// SYSCTL Configurations
//
//*****************************************************************************
void SYSCTL_init(){
	//
    // sysctl initialization
	//
    SysCtl_setStandbyQualificationPeriod(2);
    SysCtl_configureType(SYSCTL_USBTYPE, 0, 0);
    SysCtl_configureType(SYSCTL_ECAPTYPE, 0, 0);
    SysCtl_selectErrPinPolarity(0);

    SysCtl_disableMCD();


    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCB, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCB, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCB, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCC, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCC, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCC, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCD, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCD, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCD, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCE, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCE, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ADCE, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS1, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS1, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS1, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS2, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS2, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS2, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS3, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS3, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS3, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS4, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS4, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CMPSS4, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_DACA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_DACA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_DACA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PGA1, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PGA1, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PGA1, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PGA2, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PGA2, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PGA2, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PGA3, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PGA3, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PGA3, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM1, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM1, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM1, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM2, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM2, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM2, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM3, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM3, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM3, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM4, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM4, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM4, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM5, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM5, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM5, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM6, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM6, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM6, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM7, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM7, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM7, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM8, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM8, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM8, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM9, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM9, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM9, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM10, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM10, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM10, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM11, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM11, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM11, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM12, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM12, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EPWM12, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EQEP1, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EQEP1, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EQEP1, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EQEP2, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EQEP2, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EQEP2, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EQEP3, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EQEP3, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_EQEP3, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ECAP1, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ECAP1, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ECAP1, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ECAP2, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ECAP2, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_ECAP2, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CLB1, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CLB1, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CLB1, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CLB2, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CLB2, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_CLB2, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SCIA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SCIA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SCIA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SCIB, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SCIB, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SCIB, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SCIC, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SCIC, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SCIC, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SPIA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SPIA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SPIA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SPIB, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SPIB, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_SPIB, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_I2CA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_I2CA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_I2CA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_I2CB, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_I2CB, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_I2CB, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PMBUSA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PMBUSA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_PMBUSA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_LINA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_LINA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_LINA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_MCANA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_MCANA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_MCANA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_MCANB, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_MCANB, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_MCANB, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_FSIATX, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_FSIATX, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_FSIATX, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_FSIARX, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_FSIARX, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_FSIARX, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_USBA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_USBA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_USBA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_HRPWMA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_HRPWMA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_HRPWMA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_AESA, 
        SYSCTL_ACCESS_CPU1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_AESA, 
        SYSCTL_ACCESS_CLA1, SYSCTL_ACCESS_FULL);
    SysCtl_setPeripheralAccessControl(SYSCTL_ACCESS_AESA, 
        SYSCTL_ACCESS_DMA1, SYSCTL_ACCESS_FULL);

    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_CLA1);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_DMA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_TIMER0);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_TIMER1);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_TIMER2);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_HRCAL);
    SysCtl_disablePeripheral(SYSCTL_PERIPH_CLK_TBCLKSYNC);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_ERAD);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM1);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM2);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM3);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM4);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM5);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM6);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM7);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM8);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM9);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM10);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM11);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPWM12);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_ECAP1);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_ECAP2);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EQEP1);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EQEP2);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EQEP3);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_SCIA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_SCIB);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_SCIC);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_SPIA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_SPIB);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_I2CA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_I2CB);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_MCANA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_MCANB);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_USBA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_NPU);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_ADCA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_ADCB);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_ADCC);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_ADCD);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_ADCE);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_CMPSS1);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_CMPSS2);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_CMPSS3);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_CMPSS4);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_PGA1);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_PGA2);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_PGA3);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_DACA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_CLB1);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_CLB2);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_FSITXA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_FSIRXA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_LINA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_PMBUSA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_DCC0);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_DCC1);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_AESA);
    SysCtl_enablePeripheral(SYSCTL_PERIPH_CLK_EPG1);



}


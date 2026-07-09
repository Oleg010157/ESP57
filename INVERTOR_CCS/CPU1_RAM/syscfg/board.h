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
// MCANB -> myMCANA Pinmux
//
//
// MCANB_RX - GPIO Settings
//
#define GPIO_PIN_MCANB_RX 33
#define myMCANA_MCANRX_GPIO 33
#define myMCANA_MCANRX_PIN_CONFIG GPIO_33_MCANB_RX
//
// MCANB_TX - GPIO Settings
//
#define GPIO_PIN_MCANB_TX 19
#define myMCANA_MCANTX_GPIO 19
#define myMCANA_MCANTX_PIN_CONFIG GPIO_19_MCANB_TX

//*****************************************************************************
//
// MCAN Configurations
//
//*****************************************************************************
#define myMCANA_BASE MCANB_DRIVER_BASE
//
// Defines
//
#define myMCANA_MCAN_STD_ID_FILTER_NUM          (1)
#define myMCANA_MCAN_EXT_ID_FILTER_NUM          (1)
#define myMCANA_MCAN_FIFO_0_NUM                 (0)
#define myMCANA_MCAN_FIFO_1_NUM                 (0)
#define myMCANA_MCAN_TX_BUFF_SIZE               (0)
#define myMCANA_MCAN_TX_EVENT_SIZE              (0)

#define myMCANA_MCAN_STD_ID_FILT_START_ADDR     (0)
#define myMCANA_MCAN_EXT_ID_FILT_START_ADDR     (48)
#define myMCANA_MCAN_FIFO_0_START_ADDR          (548)
#define myMCANA_MCAN_FIFO_1_START_ADDR          (748)
#define myMCANA_MCAN_RX_BUFF_START_ADDR         (948)
#define myMCANA_MCAN_TX_BUFF_START_ADDR         (148)
#define myMCANA_MCAN_TX_EVENT_START_ADDR        (255)

void myMCANA_init();

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
void	MCAN_SYSCFG_init();
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

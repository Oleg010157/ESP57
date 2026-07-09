/*
 * MBOX.h
 *
 *  Created on: 13 но¤б. 2024 г.
 *      Author: Олег Павловский
 */

#ifndef KSP_KSP_HEADERS_MBOX_H_
#define KSP_KSP_HEADERS_MBOX_H_

#include "f28p55x_device.h"
#include "myDCAN.h"

// Команды управления IGBT для CAN
#define IGBT_SEND1_CMD   0xA5A5U
#define IGBT_SEND2_CMD   0xA7A7U
#define IGBT_ON1_CMD     0xA6A6U
#define IGBT_ON2_CMD     0xA4A4U
#define IGBT_OFF1_CMD    0xA3A3U
#define IGBT_OFF2_CMD    0xA2A2U
#define IGBT_OFFALL_CMD  0xA1A1U

extern uint16_t Req_336;



void SendL(void);
void SendErrs(void);
void SendTemp(void);
void SendInfo(void);
void SendStr(int size, char *str);

#endif /* KSP_KSP_HEADERS_MBOX_H_ */

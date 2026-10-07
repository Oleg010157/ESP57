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

// Тест IGBT, команда 338: data1 - код ниже, data2 - маска ключей (для SEND).
// Ответ: data3 = Bridge_ManualState(). Коды взяты от инверторного блока B81;
// коды DC-DC (A3A3, A5A5, A6A6) на этой плате не нужны и убраны.
#define IGBT_SEND_CMD    0xA7A7U // открыть ключи по маске data2
#define IGBT_ON_CMD      0xA4A4U // войти в тест, все ключи закрыты
#define IGBT_OFF_CMD     0xA2A2U // выйти из теста, мост закрыт
#define IGBT_OFFALL_CMD  0xA1A1U // то же, код от прежних двух блоков
#define IGBT_STATE_CMD   0xA0A0U // только ответить состоянием
#define IGBT_ILIM_CMD    0xA8A8U // порог первички на время теста, data2 в 0.1 А, 0 - рабочий

extern uint16_t Req_336;



void SendL(void);
void SendErrs(void);
void SendTemp(void);
void SendInfo(void);
void SendStr(int size, char *str);

#endif /* KSP_KSP_HEADERS_MBOX_H_ */

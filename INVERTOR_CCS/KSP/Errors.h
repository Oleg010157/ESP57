/*
 * Errors.h
 *
 *  Created on: 20 но¤б. 2024 г.
 *      Author: olegp
 */

#ifndef KSP_KSP_HEADERS_ERRORS_H_
#define KSP_KSP_HEADERS_ERRORS_H_

#include <math.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>

#include "ErrorCodes.h"
#include "f28p55x_device.h"

#define CurError 0x8000U
#define OneError 0x4000U
#define AddError 0x0800U

typedef struct
{
    uint16_t SessionCnt : 8;   // Счётчик событий в текущей сессии (по фронту CurrentError)
    uint16_t HistoryCnt : 7;   // Счётчик истории из EEPROM (число сессий появления)
    uint16_t CurrentError : 1; // Флаг текущей ошибки (из окна/снимка)
} ErrD;

typedef union
{
    uint16_t Word;
    ErrD     Pars;
} ErrData;

typedef struct
{
    uint16_t HistoryCount;       // Количество сессий, в которых код хотя бы раз появлялся
    uint16_t LastSessionId;      // SessionId последнего появления ошибки
} ErrHistoryData;

typedef struct
{
    uint16_t  BI;       // Индекс буфера ошибок
    uint16_t  ERI;      // Индекс очистки/записи ошибок
    uint16_t  TSC;      // Счетчик задержки сброса флагов ошибок
    uint16_t  LI;       // Счетчик цикла перебора буфера при обработке флагов
    uint16_t  ACH;      // Индекс канала АЦП
    uint16_t  FOS : 1;  // Флаг результата операции чтения/записи FRAM
    uint16_t  EER : 1;  // Флаг запроса очистки таблицы ошибок
    uint16_t  WER : 1;  // Флаг запроса записи таблицы ошибок
    uint16_t  FOI : 1;  // Флаг активной операции FRAM
    uint16_t  FRE : 1;  // Флаг ошибки FRAM
    uint16_t  FRE_ : 1; // Флаг однократной установки ошибки FRAM
    uint16_t  OFL : 1;  // Флаг запроса загрузки оффсетов из FRAM
    uint16_t  OFS : 1;  // Флаг запроса сохранения оффсетов в FRAM
    uint16_t  OFSZ;     // Размер структуры оффсетов
    uint16_t  OFAC;     // Счетчик адреса
    uint16_t *OPTR;     // Указатель на структуру оффсетов
    uint16_t  ADC : 1;  // Флаг активной операции АЦП
    uint16_t  AGC : 1;  // Флаг преобразования/получения данных с АЦП
} CtrlP;

// структура флагов полученных с регистров сдвига U4,U5


typedef union
{
    uint16_t all;
    struct
    {
        uint16_t vt2 : 1;  // Bit 0
        uint16_t vt1 : 1;  // Bit 1
        uint16_t vt3 : 1;  // Bit 2
        uint16_t vt4 : 1;  // Bit 3
        uint16_t t1 : 1;   // Bit 4
        uint16_t t2 : 1;   // Bit 5
        uint16_t t3 : 1;   // Bit 6
        uint16_t t4 : 1;   // Bit 7
        uint16_t t6 : 1;   // Bit 8
        uint16_t t5 : 1;   // Bit 9
        uint16_t TS1 : 1;  // Bit 10
        uint16_t TS2 : 1;  // Bit 11
        uint16_t Inp4 : 1; // Bit 12
        uint16_t Inp3 : 1; // Bit 13
        uint16_t Inp2 : 1; // Bit 14
        uint16_t Inp1 : 1; // Bit 15
    } bit;
} SHR_Flags;


#define ERR_BUFF_SIZE 100
extern ErrData   ErrBuff[ERR_BUFF_SIZE];
extern ErrHistoryData ErrHistory[ERR_BUFF_SIZE];
extern uint16_t  CurrentErrFlags[ERR_BUFF_SIZE];
extern uint16_t  CurrentErrTxFlags[ERR_BUFF_SIZE];
extern uint16_t  ErrSessionEdgeLatch[ERR_BUFF_SIZE];
extern uint16_t  CurrentSessionId;
extern ErrD      ErrorFlag;
extern uint16_t  CheckLamp;  // период времени в секундах включения лампы ошибок.Горит пока не нуль.
extern uint16_t  CheckLamp0; // постоянное включение лампы ошибок. Горит до ручного сброса.
extern SHR_Flags shr;
extern bool iprold;

void ClearErrorTable(void);

void     ErrHistory_OnError(uint16_t code);

void     ErrHistory_Reset(void);
bool     ErrHistory_HasPending(void);
bool     ErrHistory_GetNextPendingCode(uint16_t *code);
void     ErrHistory_ClearPending(void);
void     SendL(void);
uint16_t readShiftRegister();
void     initShiftRegisterPins();

void Errors_CurrentErrorWindowTick(void);
void RegisterSessionEvent(uint16_t code);

#endif /* KSP_KSP_HEADERS_ERRORS_H_ */

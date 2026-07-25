/*
 * Errors.c
 *
 *  Created on: 20 ноября 2024 года.
 *      Author: Олег Павловский.
 */
#include "board.h"
#include "MBOX.h"
#include "Errors.h"

#pragma DATA_SECTION(ErrHistory, "ramgs0");
#pragma DATA_SECTION(CurrentErrFlags, "ramgs0");
#pragma DATA_SECTION(CurrentErrTxFlags, "ramgs0");

ErrData        ErrBuff[ERR_BUFF_SIZE];
ErrHistoryData ErrHistory[ERR_BUFF_SIZE];
uint16_t       CurrentErrFlags[ERR_BUFF_SIZE];   // окно текущих ошибок
uint16_t       CurrentErrTxFlags[ERR_BUFF_SIZE]; // снимок текущих ошибок
uint16_t       ErrSessionEdgeLatch[ERR_BUFF_SIZE];
uint16_t       CurrentSessionId = 0U;

ErrD     ErrorFlag;
uint16_t CheckLamp  = 10; // период времени в секундах включения лампы ошибок.Горит пока не нуль.
uint16_t CheckLamp0 = 0;  // постоянное включение лампы ошибок. Горит до ручного сброса.

CtrlP CTL;
static uint16_t ErrHistoryPending[(ERR_BUFF_SIZE + 15U) / 16U];

static void ErrHistory_SetPending(uint16_t code)
{
    ErrHistoryPending[code >> 4] |= (uint16_t)(1U << (code & 0x0FU));
}

static void ErrHistory_ClearPendingCode(uint16_t code)
{
    ErrHistoryPending[code >> 4] &= (uint16_t)~(uint16_t)(1U << (code & 0x0FU));
}

bool ErrHistory_HasPending(void)
{
    uint16_t i;

    for (i = 0U; i < (uint16_t)((ERR_BUFF_SIZE + 15U) / 16U); i++)
    {
        if (ErrHistoryPending[i] != 0U)
        {
            return true;
        }
    }

    return false;
}

bool ErrHistory_GetNextPendingCode(uint16_t *code)
{
    uint16_t i;
    uint16_t b;

    for (i = 0U; i < (uint16_t)((ERR_BUFF_SIZE + 15U) / 16U); i++)
    {
        uint16_t word = ErrHistoryPending[i];
        if (word == 0U)
        {
            continue;
        }

        for (b = 0U; b < 16U; b++)
        {
            uint16_t idx = (uint16_t)(i * 16U + b);
            if (idx >= ERR_BUFF_SIZE)
            {
                return false;
            }

            if ((word & (uint16_t)(1U << b)) != 0U)
            {
                *code = idx;
                ErrHistory_ClearPendingCode(idx);
                return true;
            }
        }
    }

    return false;
}

void ErrHistory_ClearPending(void)
{
    memset(&ErrHistoryPending, 0, sizeof(ErrHistoryPending));
}

static void Errors_AttachHistoryToErrBuff(void)
{
    uint16_t i;

    for (i = 0U; i < ERR_BUFF_SIZE; i++)
    {
        uint16_t total = ErrHistory[i].HistoryCount;

        ErrBuff[i].Pars.HistoryCnt = (total > 0x7FU) ? 0x7FU : (uint16_t)total;
    }
}


void ErrHistory_Reset(void)
{
    memset(&ErrHistory, 0, sizeof(ErrHistory));
    ErrHistory_ClearPending();
}

void ErrHistory_OnError(uint16_t code)
{
    // История по коду ошибки:
    // HistoryCount++ только один раз за текущую сессию
    //    (если LastSessionId еще не равен CurrentSessionId).
    if (code >= ERR_BUFF_SIZE)
    {
        return;
    }

    if (ErrHistory[code].LastSessionId != CurrentSessionId)
    {
        if (ErrHistory[code].HistoryCount < 0xFFFFU)
        {
            ErrHistory[code].HistoryCount++;
        }
        ErrHistory[code].LastSessionId = CurrentSessionId;
        ErrHistory_SetPending(code);
    }
}

void ClearErrorTable(void)
{
    if (!CTL.EER && !CTL.WER)
    {
        CTL.FRE = false;
        CTL.EER = true;
        memset(&ErrBuff, 0, sizeof(ErrBuff));
        ErrHistory_Reset();
        memset(&CurrentErrFlags, 0, sizeof(CurrentErrFlags));
        memset(&CurrentErrTxFlags, 0, sizeof(CurrentErrTxFlags));
        memset(&ErrSessionEdgeLatch, 0, sizeof(ErrSessionEdgeLatch));
        CurrentSessionId = 0U;
        CTL.ERI = CTL.EER = 0;

        CheckLamp  = 0;
        CheckLamp0 = 0;
    }
}

void Errors_CurrentErrorWindowTick(void)
{
    uint16_t i;

    for (i = 0U; i < ERR_BUFF_SIZE; i++)
    {
        CurrentErrTxFlags[i] = CurrentErrFlags[i];

        ErrBuff[i].Pars.CurrentError = CurrentErrTxFlags[i];

        if (CurrentErrTxFlags[i] == 0U)
        {
            ErrSessionEdgeLatch[i] = 0U;
        }

        CurrentErrFlags[i] = 0U;
    }
}



/* ================================================================================
 * функция SendErrs запускается в фоне при условии что мбох свободен для передачи нового сообщения.
 * при каждом вызове функции в мбох загружается одно сообщение. При следующем проходе цикла в фоне
 * если мбох свободен выводится следующее сообщение. Таким образом уменьшается время работы программы т.е. пока выводится
 * сообщение по линии CAN фоновая программа продолжает работать.
 */

void SendErrs(void)
{
    static uint16_t err_number = 0; // номер элемента маассива ошибок -> код ошибки

    if (err_number == 0U)
    {
        Errors_AttachHistoryToErrBuff();
    }

    if (err_number < ERR_BUFF_SIZE) //
    {
        if (ErrBuff[err_number].Word != 0 || ErrBuff[err_number + 1].Word != 0)
        // посылаем слово с установленной ошибкой
        {
            Send_mbox(4, err_number, ErrBuff[err_number].Word, ErrBuff[err_number + 1].Word, 32010);
        }
        err_number = err_number + 2;
    }
    else // err_number >= ERR_BUFF_SIZE
    {
        err_number = 0;
        Send_mbox(4, 32011, 32011, 32011, 32011);
        Req_336 = Req_336 & 0xfffd; // сброс флага вывода в фоне кодов из буффера ошибок
    }
}


// -----------------------------------------------------------
// чтение регистров сдвига U4, U5 (обозначение на принц.схеме)
SHR_Flags shr;
bool iprold = 1;       // работа инвертора выключена

uint16_t readShiftRegister()
{
    uint16_t data = 0;
    uint16_t i;
    // Загрузка параллельных входов
 //   GPIO_writePin(SH_LD_PIN, 0);

    DEVICE_DELAY_US(10); // Небольшая задержка
//    GPIO_writePin(SH_LD_PIN, 1);

    // Считывание 16 бит (2 регистра)
    for ( i = 0; i < 16; i++)
    {
  //      GPIO_writePin(SHR_CLOCK, 0);
        DEVICE_DELAY_US(1);
        data <<= 1;
 //       data |= GPIO_readPin(SHR_DATA);
 //       GPIO_writePin(SHR_CLOCK, 1);
        DEVICE_DELAY_US(1);
    }

    return data;
}

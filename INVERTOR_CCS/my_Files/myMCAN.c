#ifdef __clang__
#define __TMS320C28XX__ 1
#endif

// И только ниже идут твои включения файлов:

#include "config_switch.h"
#ifdef MCANA


#include "board.h" // Обязательно подключите, чтобы функция видела myMCANA_BASE
#include "f28p55x_device.h"
#include "f28p55x_pie_defines.h"


#include "hw_mcan.h" // Обязательно должен быть подключен для макросов смещений
#include "mcan.h"
#include "myMCAN.h"
#include "myDCAN.h"



//
// This is Interrupt Service Routine for MCAN interrupt 0.
//
volatile uint32_t mcanRxCount = 0;
volatile uint32_t errorStatus = 0;
volatile uint32_t intrStatus = 0;
volatile uint16_t cana0, cana_Time;
volatile uint16_t merror;

volatile uint32_t    mode = 0U;
MCAN_TxBufElement    txMsg;
MCAN_RxBufElement    rxMsg;
MCAN_RxNewDataStatus newData;

static uint32_t txPayloadAddr[myMCANA_MCAN_TX_BUFF_SIZE] = {0};

__interrupt void INT_myMCANA_0_ISR(void) 
{
    volatile uint16_t d1, d2, d3, cmd;
    uint32_t intrStatus;
    uint32_t reg, idx, level;
    
   cana0 = EPwm7Regs.TBCTR;
    mcanRxCount++;
    
    intrStatus = MCAN_getIntrStatus(myMCANA_BASE);
    
    // ⚡ Запретить MCAN прерывания
    MCAN_enableIntr(myMCANA_BASE, MCAN_INTR_MASK_ALL, 0U);
    
    // ⚡ Очистить статус MCAN
    MCAN_clearIntrStatus(myMCANA_BASE, intrStatus);
    MCAN_clearInterrupt(myMCANA_BASE, 0x1);
    
    // ⚡ Освобождаем для других прерываний
    Interrupt_clearACKGroup(INTERRUPT_ACK_GROUP9);
    
    // Проверка ошибок
    if (intrStatus & ~(MCAN_IR_RF0N_MASK))
    {
        merror++;
        errorStatus = intrStatus & ~(MCAN_IR_RF0N_MASK);
    }

    // FIFO0 обработка
    if (intrStatus & MCAN_IR_RF0N_MASK) 
    {

      while (1) {
        reg = HW_RD_REG32(myMCANA_BASE + MCAN_RXF0S);
        // По-простому извлекаем Get Index (маска 0x3F00, сдвиг на 8 бит вправо)
        idx = (reg & MCAN_RXF0S_F0GI_M) >> MCAN_RXF0S_F0GI_S;

        // По-простому извлекаем Fill Level (маска 0x7F, сдвиг на 0 бит вправо)
        level = (reg & MCAN_RXF0S_F0FL_M) >> MCAN_RXF0S_F0FL_S;

        if (level == 0)
          break;

        MCAN_readMsgRam(myMCANA_BASE, MCAN_MEM_TYPE_FIFO, idx,
                        MCAN_RX_FIFO_NUM_0, &rxMsg);
        //       MCAN_readMsgRamFast(myMCANA_BASE, idx, &rxMsg);

        cmd = (uint16_t)rxMsg.data[1] << 8 | rxMsg.data[0];
        d1 = (uint16_t)rxMsg.data[7] << 8 | rxMsg.data[6];
        d2 = (uint16_t)rxMsg.data[5] << 8 | rxMsg.data[4];
        d3 = (uint16_t)rxMsg.data[3] << 8 | rxMsg.data[2];

        cmd_MBOX28(d1, d2, d3, cmd);

        MCAN_writeRxFIFOAck(myMCANA_BASE, MCAN_RX_FIFO_NUM_0, idx);
      }
    }
    cana_Time = EPwm7Regs.TBCTR - cana0;
    // ⚡ ОПТ: Одна строка вместо двух - разрешить всё, кроме TX Complete
    MCAN_enableIntr(myMCANA_BASE, MCAN_INTR_MASK_ALL & ~MCAN_IR_TC_MASK, 1U);
 //MCAN_enableIntr(myMCANA_BASE, MCAN_IR_RF0N_MASK, 1U);
 // Правильное включение прерывания по новому сообщению в FIFO 0
//MCAN_enableIntr(myMCANA_BASE, MCAN_INTR_SRC_RX_FIFO0_NEW_MSG, 1U);
// ⚡ Корректный ОПТ: Разрешаем всё, кроме TX Complete через правильные макросы DriverLib
//MCAN_enableIntr(myMCANA_BASE, MCAN_INTR_SRC_ALL & ~MCAN_INTR_SRC_TRANS_COMPLETE, 1U);


    
   
}


//
// Массив адресов payload для быстрого доступа
// Индекс массива = TX Buffer Number (0-31)
//





//
// Стандартный CAN. Прямая отправка через номер буфера
// bufNum - номер TX буфера (0-31)
//
void Send_mbox(uint16_t bufNum, uint16_t d1, uint16_t d2, uint16_t d3, uint16_t d4)
{
    if (bufNum >= myMCANA_MCAN_TX_BUFF_SIZE) return;

    uint32_t addr = txPayloadAddr[bufNum];
    if (addr == 0) return;

    // Только 2 записи данных
    HWREG(addr)      = ((uint32_t)d2 << 16) | d1;
    HWREG(addr + 4U) = ((uint32_t)d4 << 16) | d3;

    // Запрос передачи через dedicated Tx Buffer
    MCAN_txBufAddReq(myMCANA_BASE, bufNum);
}


//
// Инициализация TX Buffer
// bufNum - номер TX буфера (0-31)
// msgId  - CAN Message ID, который будет отправляться из этого буфера
// dlc    - Data Length Code (0-8 для Classic, 0-15 для FD)
// brs    - Bit Rate Switching (0/1)
// fdf    - FD Format (0=Classic CAN, 1=CAN FD)
//
void MCAN_initTxBuffer(uint16_t bufNum, uint16_t msgId, uint16_t dlc, uint16_t brs, uint16_t fdf)
{
    if (bufNum >= myMCANA_MCAN_TX_BUFF_SIZE) return;
    
    if (dlc > 15) dlc = 15;
    if (!fdf && dlc > 8) dlc = 8;
    if (!fdf) brs = 0;
    
    uint32_t elemSize = MCAN_getMsgObjSize(MCAN_TX_BUFF_ELEM_SIZE);
    uint32_t elemAddr = myMCANA_MCAN_TX_BUFF_START_ADDR + (bufNum * elemSize * 4U);
    uint32_t baseAddr = myMCANA_BASE + MCAN_MCAN_MSG_MEM;
    
    // Word 0: CAN Message ID - ИСПРАВЛЕНО!
    HWREG(baseAddr + elemAddr) = 
        ((((uint32_t)msgId) << 18U) & MCANSS_TX_BUFFER_ELEM_ID_MASK) |
        ((uint32_t)0U << MCANSS_TX_BUFFER_ELEM_RTR_SHIFT) |
        ((uint32_t)0U << MCANSS_TX_BUFFER_ELEM_XTD_SHIFT) |
        ((uint32_t)0U << MCANSS_TX_BUFFER_ELEM_ESI_SHIFT);
    
    // Word 1: DLC и флаги - ИСПРАВЛЕНО!
    HWREG(baseAddr + elemAddr + 4U) = 
        (((uint32_t)dlc) << MCANSS_TX_BUFFER_ELEM_DLC_SHIFT) |
        (((uint32_t)brs) << MCANSS_TX_BUFFER_ELEM_BRS_SHIFT) |
        (((uint32_t)fdf) << MCANSS_TX_BUFFER_ELEM_FDF_SHIFT) |
        ((uint32_t)0U << MCANSS_TX_BUFFER_ELEM_EFC_SHIFT) |
        ((uint32_t)0U << MCANSS_TX_BUFFER_ELEM_MM_SHIFT);
    
    // Сохраняем адрес payload
    txPayloadAddr[bufNum] = baseAddr + elemAddr + 8U;
}

//
// Инициализация всех используемых TX буферов
//
// TX-секция Message RAM переведена на 64-байтные элементы (SysConfig txBufElemSize=64,
// myMCAN.h MCAN_TX_BUFF_ELEM_SIZE). Размер хранения элемента != длина кадра: мелкие кадры
// (ACK, 132, fast) по-прежнему шлются с DLC=8, просто занимают 64-байтные слоты.
//
// Кадр 131 (Kadr60) упразднён 11.10.2026, медленная запись идёт кадром 140 - в его
// последнем слове теперь счётчик K60cnt. Буфер 3 занят под ответ на пробу скорости.
//
// ACK и ответ о скорости идут БЕЗ BRS: такой кадр весь на 1 Мбит/с и проходит, даже
// когда прибор и ПК разошлись по скорости данных. На этом держится весь откат.
// Буфер 5 - тот же ID 1, но пишет в него только фон: SendAck зовут из прерывания,
// и общий буфер они бы портили друг другу.
//
// Медленная телеметрия консолидирована в компактный кадр ID 140:
// 32 слова = 64 байта -> DLC=15 (FD+BRS), буфер 2. Было 32 байта (DLC=13),
// 21.09.2026 стало 48 (DLC=14) под частоту сети, 08.10.2026 - 64 под запас по m.
// Заполняется в send_REF_kadr() (REF_Kadr.c) и разбирается в UI (Parse140).
//
// Приоритет M_CAN = по наименьшему Message ID. ACK=1 (высший), fast=103/104/110, ошибки=131/132,
// консолидированный медленный=140 (низший) -> большой 64-байтный кадр не задерживает остальные.
//
void MCAN_initAllTxBuffers(void)
{
    //                 buf, msgId, dlc, brs, fdf
    MCAN_initTxBuffer(  0,   1,     8,   0,   1);  // ACK
    MCAN_initTxBuffer(  1, 103,    13,   1,   1);  // быстрый кадр: F1=8б(DLC8),
                                                  // F3_invertor=24б(DLC12),
                                                  // F3_convertor=32б(DLC13).
                                                  // DLC переключается в MBOX28 (MCAN_setFastDLC).
    MCAN_initTxBuffer(  2, 140,    15,   1,   1);  //  REF кадр (64 байта)
    MCAN_initTxBuffer(  3, 133,    15,   1,   1);  // ответ на пробу скорости (64 байта)
    MCAN_initTxBuffer(  4, 132,     8,   1,   1);  // Errors/main
    MCAN_initTxBuffer(  5,   1,     8,   0,   1);  // ответ о скорости
}

//
// Смена DLC быстрого кадра (буфер 1) без трогания payload и адреса — только заголовок (word1).
// Вызывается из MBOX28 при выборе режима: F1-режимы -> dlc=8 (8 байт), F3 -> dlc=12 (24 байта).
// FDF и BRS всегда 1.
//
void MCAN_setFastDLC(uint16_t dlc)
{
    uint32_t addr = txPayloadAddr[1];   // старт payload (фиксирован); заголовок word1 = addr-4
    if (addr == 0) return;

    HWREG(addr - 4U) =
        (((uint32_t)dlc) << MCANSS_TX_BUFFER_ELEM_DLC_SHIFT) |
        ((uint32_t)1U   << MCANSS_TX_BUFFER_ELEM_BRS_SHIFT) |
        ((uint32_t)1U   << MCANSS_TX_BUFFER_ELEM_FDF_SHIFT);
}



volatile bool txCancelPending = false; // выставляется в SendAck, снимается в ISR по TCF

void SendAck(int d1, int d2, int d3, int cmd)
{
    uint32_t txbrp = HW_RD_REG32(myMCANA_BASE + MCAN_TXBRP);
    uint32_t timeout = 100000;

    while ((txbrp & 0x1) && (--timeout))
    {
        txbrp = HW_RD_REG32(myMCANA_BASE + MCAN_TXBRP);
    }

    if (timeout == 0)
    {
        // буфер не освободился штатно — просим железо отменить зависший запрос
        MCAN_txBufCancellationReq(myMCANA_BASE, 0);
        txCancelPending = true;
        return; // следующий вызов SendAck уже увидит свободный буфер (или снова таймаут, если узел в Bus Off)
    }

    uint32_t addr = txPayloadAddr[0];
    HWREG(addr)      = ((uint32_t)d1 << 16) | cmd;
    HWREG(addr + 4U) = ((uint32_t)d3 << 16) | d2;

    MCAN_txBufAddReq(myMCANA_BASE, 0);
}






//
// CAN FD версия (задел на payload > 8 байт).
//
// C28x: байт = 16 бит, поэтому НЕЛЬЗЯ приводить массив к (uint32_t*) — это склеит два
// 16-битных "байтовых" слова в одно 32-битное и обнулит половину payload. Принимаем массив
// 16-битных слов и пакуем ПО ДВА в каждое 32-битное слово Message RAM (как Send_mbox/SendAck).
//
// numWords — число 16-битных слов полезных данных (8 байт = 4 слова).
// Для реального payload > 8 байт: увеличить txBufElemSize в SysConfig и задать FD-DLC
// (dlc 9..15 = 12..64 байт) в MCAN_initTxBuffer для соответствующего буфера.
//
void Send_mbox_FD(uint16_t bufNum, const uint16_t *w, uint16_t numWords)
{
    if (bufNum >= myMCANA_MCAN_TX_BUFF_SIZE) return;

    uint32_t addr = txPayloadAddr[bufNum];
    if (addr == 0) return;

    // Пакуем по 2 слова (16 бит) в каждое 32-битное слово Message RAM.
    // numWords предполагается чётным (64 байта = 32 слова). Прямой цикл без промежуточных
    // переменных — надёжнее по кодогенерации C28x.
    uint16_t i;
    for (i = 0; i < numWords; i += 2U)
    {
        HWREG(addr) = ((uint32_t)w[i + 1U] << 16) | (uint32_t)w[i];
        addr += 4U;
    }

    MCAN_txBufAddReq(myMCANA_BASE, bufNum);
}

__interrupt void INT_myMCANA_1_ISR(void)
{
    uint32_t status = MCAN_getIntrStatus(myMCANA_BASE);

    if (status & MCAN_INTR_SRC_TRANS_CANCEL_FINISH)
    {
        txCancelPending = false;
    }

    if (status & MCAN_INTR_SRC_BUS_OFF_STATUS)
    {
        MCAN_ProtocolStatus ps;
        MCAN_getProtocolStatus(myMCANA_BASE, &ps);
        if (ps.busOffStatus)
        {
            // Bus-Off recovery по Bosch M_CAN: аппаратно INIT уже выставлен при входе
            // в Bus-Off, программно достаточно снять CCCR.INIT — после этого модуль
            // сам ждёт 128 x 11 подряд рецессивных бит и возвращается на шину.
            MCAN_setOpMode(myMCANA_BASE, MCAN_OPERATION_MODE_NORMAL);
        }
    }

    // обязательно очищаем ТОЛЬКО обработанные флаги
    MCAN_clearIntrStatus(myMCANA_BASE, status);

    // Освобождаем группу PIE для обеих линий MCANA (INT_myMCANA_0/1 сидят в одной группе) —
    // без этого следующее прерывание в группе (в т.ч. приём в line0) не придёт.
    Interrupt_clearACKGroup(INT_myMCANA_1_INTERRUPT_ACK_GROUP);
}

//
// Возврат модуля на шину, если он с неё ушёл. Вызывается из фонового цикла.
//
// ЗАЧЕМ. Любая остановка отладчиком уводит MCAN в останов такта, и после
// продолжения бит CCCR.INIT остаётся взведённым — модуль уходит с шины до
// перезагрузки. Симптом со стороны: связь была, остановились посмотреть
// переменную, связь пропала, помогает только перезапуск.
//
// Измерено на плате в K56 26.08.2026 — тот же F28P55x и тот же тайминг,
// перенесённый отсюда без правок: CCCR = 0x4300 до паузы, 0x4301 после паузы и
// продолжения, при ECR = 0 и PSR.BO = 0, то есть без единой ошибки на шине.
// Настройка не спасает: SysConfig ставит initParams.emulationEnable = false,
// приостановка на отладке ВЫКЛЮЧЕНА, а модуль всё равно встаёт.
//
// Почему обработчик линии 1 ниже это не ловит: он снимает INIT только под
// условием ps.busOffStatus, а Bus_Off тут нет — счётчики ошибок нулевые.
//
// Побочно прикрывает и настоящий Bus_Off, если прерывание почему-то не дошло:
// снятие INIT по Bosch M_CAN и есть штатное восстановление.
//
// Вызывать ТОЛЬКО из фона и только после Board_init(): функция снимает INIT
// безусловно и в момент настройки помешала бы ей.
//
void MCAN_keepOnBus(void)
{
    if (MCAN_getOpMode(myMCANA_BASE) != MCAN_OPERATION_MODE_NORMAL)
    {
        MCAN_setOpMode(myMCANA_BASE, MCAN_OPERATION_MODE_NORMAL);
    }
}

//
// Скорость фазы данных на ходу: 2.5 или 5 Мбит/с.
//
// Прибор всегда стартует на 2.5 Мбит/с (SysConfig), так что старая программа с
// ним работает как раньше. На 5 Мбит/с переводит ПК при "Подключить":
//
//   1. CAN_RATE_UP: прибор переходит и ждёт подтверждения 2 с.
//   2. ПК переоткрывает PCAN на 5 Мбит/с и гоняет кадры в обе стороны.
//   3. CAN_RATE_CONFIRM: остаёмся. Нет подтверждения - прибор откатывается сам.
//
// Команды идут без BRS, поэтому доходят при любой настройке быстрой фазы.
//
// ВВЕРХ - ТОЛЬКО ПРОБОЙ. Ошибок шины за секунду набралось CAN_RATE_ERR_LIMIT - прибор
// уходит на 2.5 Мбит/с и сообщает ПК. Сам обратно не идёт, иначе на плохой шине
// скорость прыгала бы туда-сюда. Проба при выключенных ключах не показывает, что
// будет при их работе, поэтому без отката она не спасает.
//
// В меню программы три режима: "Авто" - всё как выше; "2.5" - проба не запускается;
// "5" - проба есть, но откат на ошибки выключен (CAN_RATE_UP с d2 = 1), чтобы на
// стенде видеть сбои, а не их маскировку. Таймаут пробы работает всегда.
//
// Ошибки считает CEL, поле ECR: число приращений TEC и REC, чтение его обнуляет.
// Отладчик его не сбрасывает, а смотреть в Watch надо canErrTotal: в самом CEL
// теперь остаётся только то, что набежало за последние 50 мс.
//
#define CAN_RATE_FAST_DBTP  ((1UL << 23) | (21UL << 8) | (6UL << 4) | 5UL) // TDC, делитель 1, 1+22+7 = 30 tq
#define CAN_RATE_FAST_TDCR  ((16UL << 8) | 10UL) // tdco 16 - середина бита, tdcf 10
#define CAN_RATE_TRIAL_TICKS 40U // 2 с в тиках по 50 мс
#define CAN_RATE_ERR_LIMIT   8U  // ошибок за секунду; единичная безвредна
#define CAN_RATE_WIN_TICKS   20U // секунда в тиках по 50 мс
#define CAN_RATE_ACK_BUF     5U

volatile uint16_t canRateFast = 0; // 1 = сейчас 5 Мбит/с
volatile uint16_t canRateFalls = 0; // сколько раз откатывались сами
volatile uint32_t canErrTotal = 0; // ошибок шины с запуска

static volatile uint16_t rateReq = 0; // 0 - ничего, иначе CAN_RATE_DOWN + 1 или CAN_RATE_UP + 1
static volatile uint16_t rateTrial = 0; // тиков до отката, 0 - проба не идёт
static volatile uint16_t rateHold = 0; // режим "5 Мбит/с" в меню: на ошибки не откатываться
static volatile uint16_t noteOn = 0; // ответ ждёт отправки
static volatile uint16_t noteAction = 0;
static volatile uint16_t noteReason = 0;
static uint16_t rateErrWin = 0;
static uint16_t rateWinTicks = 0;
static uint32_t rateSlowDbtp = 0; // настройка SysConfig, снимается перед первым переходом
static uint32_t rateSlowTdcr = 0;

// Настройка быстрой фазы. Запись в CCE сбрасывает TXBRP, поэтому сначала даём
// уйти ACK и прошлому ответу: иначе пропала бы, например, квитанция на "Отключить".
static void MCAN_setDataPhase(uint32_t dbtp, uint32_t tdcr)
{
    uint32_t guard = 20000UL;
    while ((HW_RD_REG32(myMCANA_BASE + MCAN_TXBRP) &
            ((1UL << 0) | (1UL << CAN_RATE_ACK_BUF))) != 0UL && --guard != 0UL)
    {
    }

    guard = 20000UL;
    MCAN_setOpMode(myMCANA_BASE, MCAN_OPERATION_MODE_SW_INIT);
    while (MCAN_getOpMode(myMCANA_BASE) != MCAN_OPERATION_MODE_SW_INIT && --guard != 0UL)
    {
    }

    HW_WR_FIELD32(myMCANA_BASE + MCAN_CCCR, MCAN_CCCR_CCE, 1U);
    HW_WR_REG32(myMCANA_BASE + MCAN_DBTP, dbtp);
    HW_WR_REG32(myMCANA_BASE + MCAN_TDCR, tdcr);
    MCAN_setOpMode(myMCANA_BASE, MCAN_OPERATION_MODE_NORMAL);
}

// Зовётся из прерывания приёма, поэтому сама скорость меняется в фоне.
void MCAN_rateCmd(uint16_t action, uint16_t seq, uint16_t busy)
{
    switch (action)
    {
        case CAN_RATE_DOWN:
            noteAction = action;
            noteReason = CAN_RATE_OK;
            rateReq = CAN_RATE_DOWN + 1U;
            break;

        case CAN_RATE_UP:
            noteAction = action;
            if (busy != 0U)
            {
                noteReason = CAN_RATE_BUSY;
                noteOn = 1U;
            }
            else
            {
                noteReason = CAN_RATE_OK;
                rateHold = seq; // здесь d2 - не номер пробы, а признак режима "5 Мбит/с"
                rateReq = CAN_RATE_UP + 1U;
            }
            break;

        case CAN_RATE_CONFIRM:
            rateTrial = 0U;
            noteAction = action;
            noteReason = CAN_RATE_OK;
            noteOn = 1U;
            break;

        case CAN_RATE_PROBE:
        {
            uint16_t w[32];
            uint16_t i;
            for (i = 0U; i < 32U; i++)
            {
                w[i] = (uint16_t)(seq + i * 0x0101U);
            }
            Send_mbox_FD(3, w, 32);
            break;
        }

        default:
            break;
    }
}

void MCAN_rateBackground(void)
{
    uint16_t req = rateReq;
    if (req != 0U)
    {
        rateReq = 0U;
        rateErrWin = 0U;
        rateWinTicks = 0U;

        if (req == CAN_RATE_UP + 1U && canRateFast == 0U)
        {
            rateSlowDbtp = HW_RD_REG32(myMCANA_BASE + MCAN_DBTP);
            rateSlowTdcr = HW_RD_REG32(myMCANA_BASE + MCAN_TDCR);
            MCAN_setDataPhase(CAN_RATE_FAST_DBTP, CAN_RATE_FAST_TDCR);
            canRateFast = 1U;
            rateTrial = CAN_RATE_TRIAL_TICKS;
        }
        else if (req == CAN_RATE_DOWN + 1U && canRateFast != 0U)
        {
            MCAN_setDataPhase(rateSlowDbtp, rateSlowTdcr);
            canRateFast = 0U;
            rateTrial = 0U;
        }
        noteOn = 1U;
    }

    if (noteOn != 0U && (HW_RD_REG32(myMCANA_BASE + MCAN_TXBRP) & (1UL << CAN_RATE_ACK_BUF)) == 0UL)
    {
        noteOn = 0U;
        Send_mbox(CAN_RATE_ACK_BUF, CAN_RATE_CMD, noteAction, canRateFast, noteReason);
    }
}

void MCAN_rateTick50ms(void)
{
    uint16_t errs = (uint16_t)((HW_RD_REG32(myMCANA_BASE + MCAN_ECR) & MCAN_ECR_CEL_MASK) >> MCAN_ECR_CEL_SHIFT);
    canErrTotal += errs;

    if (canRateFast == 0U)
        return;

    rateErrWin += errs;
    if (++rateWinTicks >= CAN_RATE_WIN_TICKS)
    {
        rateWinTicks = 0U;
        rateErrWin = 0U;
    }

    if (rateErrWin >= CAN_RATE_ERR_LIMIT && rateHold == 0U)
    {
        canRateFalls++;
        noteAction = CAN_RATE_DOWN;
        noteReason = CAN_RATE_ERRORS;
        rateReq = CAN_RATE_DOWN + 1U;
    }
    else if (rateTrial != 0U && --rateTrial == 0U)
    {
        canRateFalls++;
        noteAction = CAN_RATE_DOWN;
        noteReason = CAN_RATE_TIMEOUT;
        rateReq = CAN_RATE_DOWN + 1U;
    }
}

uint16_t MCAN_rateFlags(void)
{
    return (uint16_t)(4U | (canRateFast != 0U ? 8U : 0U));
}

//
// SysConfig для MCAN генерирует регистрацию INT_myMCANA_1 в PIE (board.c), но НЕ
// генерирует маршрутизацию источников на саму линию 1 внутри периферии MCAN —
// без этих двух вызовов линия 1 физически никогда не поднимется, и
// INT_myMCANA_1_ISR не выполнится ни разу. Вызывать один раз после Board_init().
//
void MCAN_enableLine1Routing(void)
{
    MCAN_selectIntrLine(myMCANA_BASE,
                         MCAN_IR_BO_MASK | MCAN_IR_EP_MASK | MCAN_IR_PED_MASK | MCAN_IR_TCF_MASK,
                         MCAN_INTR_LINE_NUM_1);
    MCAN_enableIntrLine(myMCANA_BASE, MCAN_INTR_LINE_NUM_1, 1U);
}


#endif // MCANA

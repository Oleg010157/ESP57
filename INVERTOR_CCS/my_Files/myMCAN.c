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
// (ACK, 131, 132, fast) по-прежнему шлются с DLC=8, просто занимают 64-байтные слоты.
//
// Медленная телеметрия консолидирована: 8 прежних кадров (100,101,102,106,107,109,118,116)
// = 32 слова = ровно 64 байта -> ОДИН кадр ID 140, DLC=15 (64 байта FD+BRS), буфер 6.
// Заполняется в send_REF_kadr() (REF_Kadr.c) через Send_mbox_FD и разбирается в UI
// (Parse140 -> существующие ProcessFrame100..118).
//
// Приоритет M_CAN = по наименьшему Message ID. ACK=1 (высший), fast=103/104/110, ошибки=131/132,
// консолидированный медленный=140 (низший) -> большой 64-байтный кадр не задерживает остальные.
//
void MCAN_initAllTxBuffers(void)
{
    //                 buf, msgId, dlc, brs, fdf
    MCAN_initTxBuffer(  0,   1,     8,   1,   1);  // ACK
    MCAN_initTxBuffer(  3, 103,     8,   1,   1);  // fast: Ia,Ib,Idc,Udc (после EPWM)
    MCAN_initTxBuffer(  4, 104,     8,   1,   1);  // fast: Va,Vb,Idc,Udc
    MCAN_initTxBuffer(  5, 110,     8,   1,   1);  // fast: Vin, индекс модуляции
    MCAN_initTxBuffer(  6, 140,    15,   1,   1);  // консолидированный медленный кадр (64 байта)
    MCAN_initTxBuffer( 14, 131,     8,   1,   1);  // Kadr60: K60cnt
    MCAN_initTxBuffer( 15, 132,     8,   1,   1);  // Errors/main
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

    uint16_t i;
    uint16_t pairs = (numWords + 1U) / 2U;
    for (i = 0; i < pairs; i++)
    {
        HWREG(addr + (i * 4U)) =
            ((uint32_t)w[(2U * i) + 1U] << 16) | (uint32_t)(w[2U * i] & 0xFFFFU);
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

#ifdef __clang__
#define __TMS320C28XX__ 1
#endif

// И только ниже идут твои включения файлов:

#include "config_switch.h"
#ifdef MCANA


#include "board.h" // Обязательно подключите, чтобы функция видела myMCANA_BASE
#include "f28p55x_device.h"
#include "f28p55x_pie_defines.h"
#include "myMCANA.h"

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
       

        while (1)
        {
            reg = HW_RD_REG32(myMCANA_BASE + MCAN_RXF0S);
            // По-простому извлекаем Get Index (маска 0x3F00, сдвиг на 8 бит вправо)
            idx = (reg & MCAN_RXF0S_F0GI_M) >> MCAN_RXF0S_F0GI_S;

            // По-простому извлекаем Fill Level (маска 0x7F, сдвиг на 0 бит вправо)
            level = (reg & MCAN_RXF0S_F0FL_M) >> MCAN_RXF0S_F0FL_S;

            if (level == 0)
                break;

                  MCAN_readMsgRam(myMCANA_BASE, MCAN_MEM_TYPE_FIFO, idx,
                                 MCAN_RX_FIFO_NUM_0, &rxMsg);
       //     MCAN_readMsgRamFast(myMCANA_BASE, idx, &rxMsg);

            cmd = (uint16_t)rxMsg.data[1] << 8 | rxMsg.data[0];
            d1  = (uint16_t)rxMsg.data[7] << 8 | rxMsg.data[6];
            d2  = (uint16_t)rxMsg.data[5] << 8 | rxMsg.data[4];
            d3  = (uint16_t)rxMsg.data[3] << 8 | rxMsg.data[2];

            cmd_MBOX28(d1, d2, d3, cmd);

            MCAN_writeRxFIFOAck(myMCANA_BASE, MCAN_RX_FIFO_NUM_0, idx);
        }
    }
    cana_Time = EPwm7Regs.TBCTR - cana0;
    // ⚡ ОПТ: Одна строка вместо двух - разрешить всё, кроме TX Complete
 //   MCAN_enableIntr(myMCANA_BASE, MCAN_INTR_MASK_ALL & ~MCAN_IR_TC_MASK, 1U);
 MCAN_enableIntr(myMCANA_BASE, MCAN_IR_RF0N_MASK, 1U);
    
   
}


//
// SendAck - всегда буфер 0
//

//
// Массив адресов payload для быстрого доступа
// Индекс массива = TX Buffer Number (0-31)
//

static uint32_t txPayloadAddr[myMCANA_MCAN_TX_BUFF_SIZE] = {0};



//
// Прямая отправка через номер буфера
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
    uint32_t elemAddr = MCAN_TX_BUFF_START_ADDR + (bufNum * elemSize * 4U);
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
void MCAN_initAllTxBuffers(void)
{
    // Буфер 0: ACK (msgId=1)
    MCAN_initTxBuffer(0, 1, 8, 0, 0);

    // Буфер 1: Flags (msgId=100)
    MCAN_initTxBuffer(6, 100, 8, 0, 0);

    // Буфер 2: Data 101 (msgId=101)
    MCAN_initTxBuffer(7, 101, 8, 0, 0);

    // Буфер 3: Data 102 (msgId=102)
    MCAN_initTxBuffer(8, 102, 8, 0, 0);

    MCAN_initTxBuffer(10, 106, 8, 0, 0);
    MCAN_initTxBuffer(11, 107, 8, 0, 0);
    MCAN_initTxBuffer(12, 118, 8, 0, 0);
    MCAN_initTxBuffer(13, 116, 8, 0, 0);
}


//
// SendAck - всегда буфер 0, ID = 1 (см. MCAN_initAllTxBuffers)
//
void SendAck(int d1, int d2, int d3, int cmd)
{
    uint32_t txbrp = HW_RD_REG32(myMCANA_BASE + MCAN_TXBRP);

    // Если буфер занят - ждём освобождения
    while (txbrp & 0x1)
    {
        txbrp = HW_RD_REG32(myMCANA_BASE + MCAN_TXBRP);
    }

    // Buffer свободен - отправляем ACK
    uint32_t addr = txPayloadAddr[0];
    HWREG(addr)      = ((uint32_t)d1 << 16) | cmd;
    HWREG(addr + 4U) = ((uint32_t)d3 << 16) | d2;

    MCAN_txBufAddReq(myMCANA_BASE, 0);
}

//
// CAN FD версия
//
void Send_mbox_FD(uint16_t bufNum, const uint8_t *data, uint16_t dataLen)
{
    if (bufNum >= myMCANA_MCAN_TX_BUFF_SIZE) return;
    
    uint32_t addr = txPayloadAddr[bufNum];
    if (addr == 0) return;
    
    const uint32_t *data32 = (const uint32_t *)data;
    uint16_t numWords = (dataLen + 3) / 4;
    uint16_t i;
    for ( i = 0; i < numWords; i++)
    {
        HWREG(addr + (i * 4U)) = data32[i];
    }
    
    MCAN_txBufAddReq(myMCANA_BASE, bufNum);
}

#endif // MCANA

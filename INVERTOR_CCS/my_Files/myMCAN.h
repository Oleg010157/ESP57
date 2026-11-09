/*
 * myMCAN.h
 *
 *  Created on: 21.01.2026
 *      Author: Oleg Pavlovsky
 */

#ifndef MYMCAN_H_
#define MYMCAN_H_

//
// Element size selector для Tx Buffer (не зависит от конкретного инстанса,
// используется только как параметр для MCAN_getMsgObjSize())
//
#define MCAN_TX_BUFF_ELEM_SIZE (MCAN_ELEM_SIZE_64BYTES)

//
// Tx Buffer Element — смещения и маски полей (Word 0 / Word 1)
//
#define MCANSS_TX_BUFFER_ELEM_ID_SHIFT                           (0U)
#define MCANSS_TX_BUFFER_ELEM_ID_MASK                            (0x1FFFFFFFU)
#define MCANSS_TX_BUFFER_ELEM_RTR_SHIFT                          (29U)
#define MCANSS_TX_BUFFER_ELEM_RTR_MASK                           (0x20000000U)
#define MCANSS_TX_BUFFER_ELEM_XTD_SHIFT                          (30U)
#define MCANSS_TX_BUFFER_ELEM_XTD_MASK                           (0x40000000U)
#define MCANSS_TX_BUFFER_ELEM_ESI_SHIFT                          (31U)
#define MCANSS_TX_BUFFER_ELEM_ESI_MASK                           (0x80000000U)
#define MCANSS_TX_BUFFER_ELEM_DLC_SHIFT                          (16U)
#define MCANSS_TX_BUFFER_ELEM_DLC_MASK                           (0x000F0000U)
#define MCANSS_TX_BUFFER_ELEM_BRS_SHIFT                          (20U)
#define MCANSS_TX_BUFFER_ELEM_BRS_MASK                           (0x00100000U)
#define MCANSS_TX_BUFFER_ELEM_FDF_SHIFT                          (21U)
#define MCANSS_TX_BUFFER_ELEM_FDF_MASK                           (0x00200000U)
#define MCANSS_TX_BUFFER_ELEM_EFC_SHIFT                          (23U)
#define MCANSS_TX_BUFFER_ELEM_EFC_MASK                           (0x00800000U)
#define MCANSS_TX_BUFFER_ELEM_MM_SHIFT                           (24U)
#define MCANSS_TX_BUFFER_ELEM_MM_MASK                            (0xFF000000U)

//
// Function Prototype.
//
void MCAN_initAllTxBuffers(void);

// Довключает то, что SysConfig не генерирует для второй линии прерываний MCAN:
// маршрутизацию BO/EP/PED/TCF на MCAN_INTR_LINE_NUM_1 и сам выход линии 1.
// Без этого INT_myMCANA_1_ISR зарегистрирован в PIE, но никогда не вызывается.
void MCAN_enableLine1Routing(void);

// Вернуть модуль на шину, если он с неё ушёл. Зовётся из фонового цикла.
// Лечит уход в CCCR.INIT после остановки отладчиком — разбор в myMCAN.c.
void MCAN_keepOnBus(void);

// Есть ли кому слушать на шине: не error passive и не Bus_Off. Без PCAN буфер
// передачи не освобождается никогда - ждать его нельзя. Разбор в myMCAN.c.
bool MCAN_canAlive(void);

void cmd_MBOX28(uint16_t data1, uint16_t data2, uint16_t data3, uint16_t cmd);

// ⚡ УЛЬТРА-FAST версия: без проверок, только чтение 8 байт
// ⚡ Исправленная УЛЬТРА-FAST версия для FIFO0
static inline void MCAN_readMsgRamFast(uint32_t baseAddr,
                                       uint32_t idx,
                                       MCAN_RxBufElement *elem)
{
    // FIFO0 element size: 8-byte data = 16 bytes total (8 header + 8 data)
    uint32_t elemAddr = myMCANA_MCAN_FIFO_0_START_ADDR + (16U * idx) + MCAN_MCAN_MSG_MEM;
    uint32_t addr = baseAddr + elemAddr;
    
    // ⚡ 4 HW_RD_REG32 вместо 6+ в оригинале
    uint32_t w0 = HW_RD_REG32(addr);       // ID + флаги
    uint32_t w1 = HW_RD_REG32(addr + 4U);  // DLC + флаги  
    uint32_t w2 = HW_RD_REG32(addr + 8U);  // data[0-3]
    uint32_t w3 = HW_RD_REG32(addr + 12U); // data[4-7]
    
    // ⚡ Распаковка - используем просто RX макросы (универсальные для FIFO и Buffer)
    elem->id  = (w0 & 0x1FFFFFFFU) >> 0U;   // ID mask (11-bit или 29-bit)
    elem->rtr = (w0 & 0x20000000U) >> 29U;  // RTR bit
    elem->xtd = (w0 & 0x40000000U) >> 30U;  // XTD bit
    elem->esi = (w0 & 0x80000000U) >> 31U;  // ESI bit
    
    elem->rxts = (w1 & 0xFFFF0000U) >> 16U; // RX timestamp
    elem->dlc  = (w1 & 0x000F0000U) >> 16U; // DLC
    elem->brs  = (w1 & 0x00100000U) >> 20U; // BRS bit
    elem->fdf  = (w1 & 0x00200000U) >> 21U; // FDF bit
    elem->fidx = (w1 & 0x7F000000U) >> 24U; // Filter index
    elem->anmf = (w1 & 0x80000000U) >> 31U; // ANMF bit

    // ⚡ Распаковка данных (все 8 байт за раз)
    elem->data[0] = (uint16_t)(w2 & 0xFF);
    elem->data[1] = (uint16_t)((w2 >> 8) & 0xFF);
    elem->data[2] = (uint16_t)((w2 >> 16) & 0xFF);
    elem->data[3] = (uint16_t)((w2 >> 24) & 0xFF);
    elem->data[4] = (uint16_t)(w3 & 0xFF);
    elem->data[5] = (uint16_t)((w3 >> 8) & 0xFF);
    elem->data[6] = (uint16_t)((w3 >> 16) & 0xFF);
    elem->data[7] = (uint16_t)((w3 >> 24) & 0xFF);
}

#endif /* MYMCAN_H_ */

/*
 * myMCAN.h
 *
 *  Created on: 21.01.2026
 *      Author: Oleg Pavlovsky
 */

#ifndef MYMCAN_H_
#define MYMCAN_H_

//
// Function Prototype.
//
void MCAN_initAllTxBuffers(void);

void cmd_MBOX28(uint16_t data1, uint16_t data2, uint16_t data3, uint16_t cmd);

// ⚡ УЛЬТРА-FAST версия: без проверок, только чтение 8 байт
// ⚡ Исправленная УЛЬТРА-FAST версия для FIFO0
static inline void MCAN_readMsgRamFast(uint32_t baseAddr,
                                       uint32_t idx,
                                       MCAN_RxBufElement *elem)
{
    uint32_t elemAddr = (myMCANA_MCAN_FIFO_0_START_ADDR << 2U) + (myMCANA_MCAN_FIFO_0_NUM * 4U * idx) 
                        + MCAN_MCAN_MSG_MEM;
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

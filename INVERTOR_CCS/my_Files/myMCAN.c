#include "board.h" // Обязательно подключите, чтобы функция видела myMCANA_BASE
#include "f28p55x_device.h"
#include "myMCANA.h"

#include "hw_mcan.h" // Обязательно должен быть подключен для макросов смещений

//
// This is Interrupt Service Routine for MCAN interrupt 0.
//
volatile uint32_t mcanRxCount = 0;
volatile uint32_t errorStatus = 0;
volatile uint32_t intrStatus = 0;
 

__interrupt void INT_myMCANA_0_ISR(void) 
{
    volatile uint16_t d1, d2, d3, cmd;
    uint32_t intrStatus;
    uint32_t reg, idx, level;

  // cana0 = EPwm7Regs.TBCTR;

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
   //     merror++;
        errorStatus = intrStatus & ~(MCAN_IR_RF0N_MASK);
    }

    // FIFO0 обработка
    if (intrStatus & MCAN_IR_RF0N_MASK) 
    {
        mcanRxCount++;
        
        while (1)
        {
            reg = HW_RD_REG32(myMCANA_BASE + MCAN_RXF0S);
            idx = HW_GET_FIELD(reg, MCAN_RXF0S_F0GI);
            level = HW_GET_FIELD(reg, MCAN_RXF0S_F0FL);

            if (level == 0)
                break;

      //      MCAN_readMsgRam(MCANA_BASE, MCAN_MEM_TYPE_FIFO, idx, 
      //                     MCAN_RX_FIFO_NUM_0, &rxMsg);
  //          MCAN_readMsgRamFast(MCANA_BASE, idx, &rxMsg);

 //           cmd = (uint16_t)rxMsg.data[1] << 8 | rxMsg.data[0];
  //          d1  = (uint16_t)rxMsg.data[7] << 8 | rxMsg.data[6];
  //          d2  = (uint16_t)rxMsg.data[5] << 8 | rxMsg.data[4];
  //          d3  = (uint16_t)rxMsg.data[3] << 8 | rxMsg.data[2];

   //         cmd_MBOX28(d1, d2, d3, cmd);

            MCAN_writeRxFIFOAck(myMCANA_BASE, MCAN_RX_FIFO_NUM_0, idx);
        }
    }
 //   cana_Time = EPwm7Regs.TBCTR - cana0;
    // ⚡ ОПТ: Одна строка вместо двух - разрешить всё, кроме TX Complete
    MCAN_enableIntr(myMCANA_BASE, MCAN_INTR_MASK_ALL & ~MCAN_IR_TC_MASK, 1U);
    
   
}





//
// SendAck - всегда буфер 0
//
// ⚡ Глобальный счётчик ожиданий (для диагностики)
volatile uint16_t txBuf0WaitCycles[60] = {0};
volatile uint16_t log[60] ={0};
volatile uint16_t tim[60] ={0};
volatile uint16_t cmdt[60] = {0};
volatile uint16_t ilg = 0;
//
// Массив адресов payload для быстрого доступа
// Индекс массива = TX Buffer Number (0-31)
//

static uint32_t txPayloadAddr[MCAN_TX_BUFF_SIZE] = {0};

volatile uint16_t cana0, cana_Time;
//
// SendAck - всегда буфер 0
//
void SendAck(int d1, int d2, int d3, int cmd)
{
  log[ilg] =   EPwm7Regs.TBCTR;
  tim[ilg] = (log[ilg]-log[ilg-1])/120;
  cmdt[ilg] = cmd;

  if (ilg<60)  ilg++;

     uint32_t txbrp = HWREG_BP(MCANA_BASE + MCAN_TXBRP);


    // ⚡ Если буфер занят - считаем ОДИН раз
    if (txbrp & 0x1U)
    {
        txBuf0WaitCycles[ilg] = cmd;  // ← Событие: "буфер был занят"

        // Ждём освобождения
        while (1)
        {

            txbrp = HWREG_BP(MCANA_BASE + MCAN_TXBRP);
            if ((txbrp & 0x1U) == 0U)  // Буфер свободен
                break;
        }
    }

    // Buffer свободен - отправляем ACK
    uint32_t addr = txPayloadAddr[0];
    HWREG(addr)      = ((uint32_t)d1 << 16) | cmd;
    HWREG(addr + 4U) = ((uint32_t)d3 << 16) | d2;

    MCAN_txBufAddReq(MCANA_BASE, 0U);

}

//
// Прямая отправка через номер буфера
// bufNum - номер TX буфера (0-31)
//
void Send_mbox(uint16_t bufNum, uint16_t d1, uint16_t d2, uint16_t d3, uint16_t d4)
{
    if (bufNum >= MCAN_TX_BUFF_SIZE) return;

    uint32_t addr = txPayloadAddr[bufNum];
    if (addr == 0) return;

    // Только 2 записи данных
    HWREG(addr)      = ((uint32_t)d2 << 16) | d1;
    HWREG(addr + 4U) = ((uint32_t)d4 << 16) | d3;

    // Запрос передачи через dedicated Tx Buffer
    MCAN_txBufAddReq(MCANA_BASE, bufNum);
}



#include "config_switch.h"
#if MCANA
//
// Include Files
//

#include "inc/stw_dataTypes.h"
#include "inc/stw_types.h"
#include <string.h>
#include <stdint.h>


#include "mcan.h"
#include "debug.h"
#include "device.h"
#include "driverlib.h"

#include "f28p55x_device.h"
#include "myDCAN.h"
#include "board.h"

//
// Defines
//
#define MCAN_TX_BUFF_ELEM_SIZE (MCAN_ELEM_SIZE_8BYTES)
#define MCAN_TX_BUFF_START_ADDR (myMCANA_MCAN_TX_BUFF_START_ADDR)

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
// Global Variables.
//
volatile uint32_t      isrIntr0Flag = 1U;
volatile uint32_t      isrIntr1Flag = 1U;
volatile uint16_t      monEn        = 0x0;
volatile unsigned long msgCount     = 0;
volatile unsigned long merror       = 0;
//static uint32_t        dataSize[16] = {0, 1, 2, 3, 4, 5, 6, 7, 8, 12, 16, 20, 24, 32, 48, 64};
uint32_t txBufAdress;
uint32_t payloadAddr;
volatile uint16_t cana0, cana_Time;// dbg
// ⚡ Добавить в глобальные переменные
static uint32_t g_fifo0StartAddr = 0U;
static uint32_t g_fifo0ElemSize = 0U;



//
// Function Prototype.
//
void MCAN_initAllTxBuffers(void);
static void        MCANIntrConfig(void);
__interrupt void   myMCAN_0_ISR(void);
void               cmd_MBOX28(uint16_t data1, uint16_t data2, uint16_t data3, uint16_t cmd);

volatile uint32_t mode = 0U;
MCAN_RxBufElement rxMsg;

// ⚡ Вызвать один раз при инициализации
void MCAN_cacheFifo0Params(uint32_t baseAddr)
{
    g_fifo0StartAddr = HW_RD_FIELD32(baseAddr + MCAN_RXF0C, 
                                     MCAN_RXF0C_F0SA);
    g_fifo0ElemSize = MCAN_getMsgObjSize(
                        HW_RD_FIELD32(baseAddr + MCAN_RXESC, 
                                      MCAN_RXESC_F0DS));
}


// ⚡ УЛЬТРА-FAST версия: без проверок, только чтение 8 байт
// ⚡ Исправленная УЛЬТРА-FAST версия для FIFO0
static inline void MCAN_readMsgRamFast(uint32_t baseAddr,
                                       uint32_t idx,
                                       MCAN_RxBufElement *elem)
{
    uint32_t elemAddr = (g_fifo0StartAddr << 2U) + (g_fifo0ElemSize * 4U * idx) 
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
//
// Main
//
void mcan_init(void) {

  //
  // ISR Configuration.
  //
  MCANIntrConfig();

  // SysConfig already configured GPIO, Message RAM, filter, mode and bit timing.
  MCAN_initAllTxBuffers();
  MCAN_cacheFifo0Params(myMCANA_BASE);

  MCAN_enableIntr(myMCANA_BASE, MCAN_INTR_MASK_ALL, 1U);
  MCAN_enableIntr(myMCANA_BASE, MCAN_IR_TC_MASK, 0U);
  MCAN_selectIntrLine(myMCANA_BASE, MCAN_INTR_MASK_ALL,
                      MCAN_INTR_LINE_NUM_0);
  MCAN_enableIntrLine(myMCANA_BASE, MCAN_INTR_LINE_NUM_0, 1U);
}

static void MCANIntrConfig(void) {

//  Interrupt_register(INT_MCANA_0, &myMCAN_0_ISR);
//  Interrupt_enable(INT_MCANA_0);
}

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

   cana0 = EPwm7Regs.TBCTR;

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
        mcanRxCount++;
        
        while (1)
        {
            reg = HW_RD_REG32(myMCANA_BASE + MCAN_RXF0S);
            idx = HW_GET_FIELD(reg, MCAN_RXF0S_F0GI);
            level = HW_GET_FIELD(reg, MCAN_RXF0S_F0FL);

            if (level == 0)
                break;

      //      MCAN_readMsgRam(myMCANA_BASE, MCAN_MEM_TYPE_FIFO, idx, 
      //                     MCAN_RX_FIFO_NUM_0, &rxMsg);
            MCAN_readMsgRamFast(myMCANA_BASE, idx, &rxMsg);

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
    MCAN_enableIntr(myMCANA_BASE, MCAN_INTR_MASK_ALL & ~MCAN_IR_TC_MASK, 1U);
    
   
}


//
// Массив адресов payload для быстрого доступа
// Индекс массива = TX Buffer Number (0-31)
//
#define MAX_TX_BUFFERS myMCANA_MCAN_TX_BUFF_SIZE
static uint32_t txPayloadAddr[MAX_TX_BUFFERS] = {0};

volatile uint16_t cana0, cana_Time;

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
    if (bufNum >= MAX_TX_BUFFERS) return;
    
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
    // Буфер 0: ACK (msgId=2)
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
// Прямая отправка через номер буфера
// bufNum - номер TX буфера (0-31)
//
void Send_mbox(uint16_t bufNum, uint16_t d1, uint16_t d2, uint16_t d3, uint16_t d4)
{
    if (bufNum >= MAX_TX_BUFFERS) return;

    uint32_t addr = txPayloadAddr[bufNum];
    if (addr == 0) return;

    // Только 2 записи данных
    HWREG(addr)      = ((uint32_t)d2 << 16) | d1;
    HWREG(addr + 4U) = ((uint32_t)d4 << 16) | d3;

    // Запрос передачи через dedicated Tx Buffer
    MCAN_txBufAddReq(myMCANA_BASE, bufNum);
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
// SendAck - всегда буфер 0
//
void SendAck(int d1, int d2, int d3, int cmd)
{
  log[ilg] =   EPwm7Regs.TBCTR;
  tim[ilg] = (log[ilg]-log[ilg-1])/120;
  cmdt[ilg] = cmd;

  if (ilg<60)  ilg++;

     uint32_t txbrp = HWREG_BP(myMCANA_BASE + MCAN_TXBRP);


    // ⚡ Если буфер занят - считаем ОДИН раз
    if (txbrp & 0x1U)
    {
        txBuf0WaitCycles[ilg] = cmd;  // ← Событие: "буфер был занят"

        // Ждём освобождения
        while (1)
        {

            txbrp = HWREG_BP(myMCANA_BASE + MCAN_TXBRP);
            if ((txbrp & 0x1U) == 0U)  // Буфер свободен
                break;
        }
    }

    // Buffer свободен - отправляем ACK
    uint32_t addr = txPayloadAddr[0];
    HWREG(addr)      = ((uint32_t)d1 << 16) | cmd;
    HWREG(addr + 4U) = ((uint32_t)d3 << 16) | d2;

    MCAN_txBufAddReq(myMCANA_BASE, 0U);

}

//
// CAN FD версия
//
void Send_mbox_FD(uint16_t bufNum, const uint8_t *data, uint16_t dataLen)
{
    if (bufNum >= MAX_TX_BUFFERS) return;

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

#endif // MCAN






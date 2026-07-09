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

//
// Defines
//
#define MCAN_STD_ID_FILTER_NUM (1U)
#define MCAN_EXT_ID_FILTER_NUM (0U)
#define MCAN_FIFO_0_NUM (64U)
#define MCAN_FIFO_0_ELEM_SIZE (MCAN_ELEM_SIZE_8BYTES)
#define MCAN_FIFO_1_NUM (0U)
#define MCAN_FIFO_1_ELEM_SIZE (MCAN_ELEM_SIZE_8BYTES)
#define MCAN_RX_BUFF_NUM (0U)
#define MCAN_RX_BUFF_ELEM_SIZE (MCAN_ELEM_SIZE_8BYTES)
#define MCAN_TX_BUFF_SIZE (32U)
#define MCAN_TX_FQ_SIZE (0U)
#define MCAN_TX_BUFF_ELEM_SIZE (MCAN_ELEM_SIZE_8BYTES)
#define MCAN_TX_EVENT_SIZE (0U)
#define MCAN_EXT_ID_AND_MASK (0x1FFFFFFFU)
#define MCAN_MSG_INT (0x81200)

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
//  Defining Starting Addresses for Message RAM Sections,
//  (Calculated from Macros based on User defined configuration above)
//
#define MCAN_STD_ID_FILT_START_ADDR (0x0U)
#define MCAN_EXT_ID_FILT_START_ADDR                                            \
  (MCAN_STD_ID_FILT_START_ADDR +                                               \
   ((MCAN_STD_ID_FILTER_NUM * MCANSS_STD_ID_FILTER_SIZE_WORDS * 4U)))
#define MCAN_FIFO_0_START_ADDR                                                 \
  (MCAN_EXT_ID_FILT_START_ADDR +                                               \
   ((MCAN_EXT_ID_FILTER_NUM * MCANSS_EXT_ID_FILTER_SIZE_WORDS * 4U)))
#define MCAN_FIFO_1_START_ADDR                                                 \
  (MCAN_FIFO_0_START_ADDR +                                                    \
   (MCAN_getMsgObjSize(MCAN_FIFO_0_ELEM_SIZE) * 4U * MCAN_FIFO_0_NUM))
#define MCAN_RX_BUFF_START_ADDR                                                \
  (MCAN_FIFO_1_START_ADDR +                                                    \
   (MCAN_getMsgObjSize(MCAN_FIFO_1_ELEM_SIZE) * 4U * MCAN_FIFO_1_NUM))
#define MCAN_TX_BUFF_START_ADDR                                                \
  (MCAN_RX_BUFF_START_ADDR +                                                   \
   (MCAN_getMsgObjSize(MCAN_RX_BUFF_ELEM_SIZE) * 4U * MCAN_RX_BUFF_NUM))
#define MCAN_TX_EVENT_START_ADDR                                               \
  (MCAN_TX_BUFF_START_ADDR + (MCAN_getMsgObjSize(MCAN_TX_BUFF_ELEM_SIZE) *     \
                              4U * (MCAN_TX_BUFF_SIZE + MCAN_TX_FQ_SIZE)))

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
static void        MCANConfig(void);
static void        MCANIntrConfig(void);
__interrupt void   myMCAN_0_ISR(void);
void               cmd_MBOX28(uint16_t data1, uint16_t data2, uint16_t data3, uint16_t cmd);

volatile uint32_t    mode = 0U;
MCAN_TxBufElement    txMsg;
MCAN_RxBufElement    rxMsg;
MCAN_RxNewDataStatus newData;

MCAN_RevisionId            revId;
MCAN_InitParams            initParams;
MCAN_ConfigParams          configParams;
MCAN_MsgRAMConfigParams    msgRAMConfigParams;
MCAN_StdMsgIDFilterElement stdFiltelem;
MCAN_BitTimingParams       bitTimes;

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
  // Initialize device clock and peripherals
  //
  Device_init();

  //
  // Initialize GPIO and unlock the GPIO configuration registers
  //
  Device_initGPIO();

  //
  // Configuring the GPIOs for MCAN.
  //
  GPIO_setPinConfig(DEVICE_GPIO_CFG_MCANRXA);
  GPIO_setPinConfig(DEVICE_GPIO_CFG_MCANTXA);

  //
  // Configure the divisor for the MCAN bit-clock
  //
  SysCtl_setMCANClk(SYSCTL_MCANCLK_DIV_3);

  //
  // ISR Configuration.
  //
  MCANIntrConfig();

 
  //
  // Initialize message to transmit.
  /*
  txMsg.id = ((uint32_t)(0x2)) << 18U; // Identifier Value.
  txMsg.rtr = 0U;                      // Transmit data frame.
  txMsg.xtd = 0U;                      // 11-bit standard identifier.
  txMsg.esi = 0U;   // ESI bit in CAN FD format depends only on error
                    // passive flag.
  txMsg.dlc = 8U;   // CAN + CAN FD: transmit frame has 0-8 data bytes.
  txMsg.brs = 0U;   // CAN FD frames transmitted with bit rate
                    // switching.
  txMsg.fdf = 0U;   // Frame transmitted in CAN FD format.
  txMsg.efc = 0U;   // Store Tx events.
  txMsg.mm = 0xAAU; // Message Marker.
*/
  
  //
  // Configure MCAN.
  //
  MCANConfig();
 
  // Инициализация TX буферов (ДОБАВИТЬ)
  MCAN_initAllTxBuffers();
  // ⚡ ДОБАВИТЬ: кэшируем параметры FIFO0
  MCAN_cacheFifo0Params(MCANA_DRIVER_BASE);
  //
  // Enable Interrupts.
  //
  MCAN_enableIntr(MCANA_DRIVER_BASE, MCAN_INTR_MASK_ALL, 1U);
  // 🛑 Отключаем TX Complete (TC) - не используем
  MCAN_enableIntr(MCANA_DRIVER_BASE, MCAN_IR_TC_MASK, 0U);
  //
  // Select Interrupt Line.
  //
  MCAN_selectIntrLine(MCANA_DRIVER_BASE, MCAN_INTR_MASK_ALL,
                      MCAN_INTR_LINE_NUM_0);

  //
  // Enable Interrupt Line.
  //
  MCAN_enableIntrLine(MCANA_DRIVER_BASE, MCAN_INTR_LINE_NUM_0, 1U);

  //
  // Enable Transmission interrupt.
  //
 // MCAN_txBufTransIntrEnable(MCANA_DRIVER_BASE, 0U, 0U);

  
 
}

//
// This function will configure MCAN module.
//
static void MCANConfig(void) {
 

  //
  //  Initializing all structs to zero to prevent stray values
  //
  memset(&initParams, 0, sizeof(initParams));
  memset(&configParams, 0, sizeof(configParams));
  memset(&msgRAMConfigParams, 0, sizeof(msgRAMConfigParams));
  memset(&stdFiltelem, 0, sizeof(stdFiltelem));
  memset(&bitTimes, 0, sizeof(bitTimes));

  //
  // Initialize MCAN Init parameters.
  //
  initParams.fdMode = 0x1U;          // FD operation enabled.
  initParams.brsEnable = 0x0U;       // Bit rate switching for
                                     // transmissions enabled.
  initParams.txpEnable = 0x0U;       // Transmit pause disabled.
  initParams.efbi = 0x0U;            // Edge filtering disabled.
  initParams.pxhddisable = 0x0U;     // Protocol exception handling enabled
  initParams.darEnable = 0x0U;       // Enable Automatic retransmission of
                                     // messages not transmitted successfully
  initParams.wkupReqEnable = 0x1U;   // Wakeup request is enabled.
  initParams.autoWkupEnable = 0x1U;  // Auto-Wakeup is enabled.
  initParams.emulationEnable = 0x0U; // Emulation/Debug Suspend is enabled.
  initParams.tdcEnable = 0x1U;       // Transmitter Delay Compensation is
                                     // enabled.
  initParams.wdcPreload = 0xFFU;     // Start value of the Message RAM
                                     // Watchdog Counter preload.

  //
  // Transmitter Delay Compensation parameters.
  //
  initParams.tdcConfig.tdcf = 0xAU;
  initParams.tdcConfig.tdco = 0x6U;

  //
  // Initialize MCAN Config parameters.
  //
  configParams.monEnable = monEn;  // Bus Monitoring Mode is disabled
  configParams.asmEnable = 0x0U;   // Normal CAN operation.
  configParams.tsPrescalar = 0xFU; // Prescaler Value.
  configParams.tsSelect = 0x0U;    // Timestamp counter value.
  configParams.timeoutSelect = MCAN_TIMEOUT_SELECT_CONT;
  // Time-out counter source select.
  configParams.timeoutPreload = 0xFFFFU; // Start value of the Timeout
                                         // Counter.
  configParams.timeoutCntEnable = 0x0U;  // Time-out Counter is disabled.
  configParams.filterConfig.rrfs = 0x0U; // Reject all remote frames with
                                         // 29-bit extended IDs.
  configParams.filterConfig.rrfe = 0x0U; // Reject all remote frames with
                                         // 11-bit standard IDs.
  configParams.filterConfig.anfe = 0x1U; // Accept in Rx FIFO 1.
  configParams.filterConfig.anfs = 0x1U; // Accept in Rx FIFO 1.

  //
  // Initialize Message RAM Sections Configuration Parameters.
  //
  msgRAMConfigParams.flssa = MCAN_STD_ID_FILT_START_ADDR;
  // Standard ID Filter List Start Address.
  msgRAMConfigParams.lss = MCAN_STD_ID_FILTER_NUM;
  // List Size: Standard ID.
  msgRAMConfigParams.flesa = MCAN_EXT_ID_FILT_START_ADDR;
  // Extended ID Filter List Start Address.
  msgRAMConfigParams.lse = MCAN_EXT_ID_FILTER_NUM;
  // List Size: Extended ID.
  msgRAMConfigParams.txStartAddr = MCAN_TX_BUFF_START_ADDR;
  // Tx Buffers Start Address.
  msgRAMConfigParams.txBufNum = 32U; // MCAN_TX_BUFF_SIZE;
  // Number of Dedicated Transmit Buffers.
  msgRAMConfigParams.txFIFOSize = MCAN_TX_FQ_SIZE;
  // Number of Tx FIFO or Tx Queue Elements
  msgRAMConfigParams.txBufMode = 0U; // Tx FIFO operation
  msgRAMConfigParams.txBufElemSize = MCAN_TX_BUFF_ELEM_SIZE;
  // Tx Buffer Element Size.
  msgRAMConfigParams.txEventFIFOStartAddr = 0; // MCAN_TX_EVENT_START_ADDR;
  // Tx Event FIFO Start Address.
  msgRAMConfigParams.txEventFIFOSize = 0; // MCAN_TX_BUFF_SIZE;
  // Event FIFO Size.
  msgRAMConfigParams.txEventFIFOWaterMark = 3U;
  // Level for Tx Event FIFO watermark interrupt.
  msgRAMConfigParams.rxFIFO0startAddr = MCAN_FIFO_0_START_ADDR;
  // Rx FIFO0 Start Address.
  msgRAMConfigParams.rxFIFO0size = MCAN_FIFO_0_NUM;
  // Number of Rx FIFO elements.
  msgRAMConfigParams.rxFIFO0waterMark = 3U; // Rx FIFO0 Watermark.
  msgRAMConfigParams.rxFIFO0OpMode = 0U;    // FIFO blocking mode.

  msgRAMConfigParams.rxFIFO1startAddr = MCAN_FIFO_1_START_ADDR;
  // Rx FIFO1 Start Address.
  msgRAMConfigParams.rxFIFO1size = MCAN_FIFO_1_NUM;
  // Number of Rx FIFO elements.
  msgRAMConfigParams.rxFIFO1waterMark = 3U; // Level for Rx FIFO 1
                                            // watermark interrupt.
  msgRAMConfigParams.rxFIFO1OpMode = 0U;    // FIFO blocking mode.

  msgRAMConfigParams.rxBufStartAddr = 0; // MCAN_RX_BUFF_START_ADDR;
  // Rx Buffer Start Address.
  msgRAMConfigParams.rxBufElemSize = 0; // MCAN_RX_BUFF_ELEM_SIZE;
  // Rx Buffer Element Size.
  msgRAMConfigParams.rxFIFO0ElemSize = MCAN_FIFO_0_ELEM_SIZE;
  // Rx FIFO0 Element Size.
  msgRAMConfigParams.rxFIFO1ElemSize = MCAN_FIFO_1_ELEM_SIZE;
  // Rx FIFO1 Element Size.

  //
  // Initialize Rx Buffer Configuration parameters.
  //
  stdFiltelem.sfec =  MCAN_STDFILTEC_FIFO0;             // Store in RX FIFO 0 if filter matches
  stdFiltelem.sft = MCAN_STDFILT_RANGE; // Range filter from SFID1 to SFID2
  stdFiltelem.sfid1 = 0x2U;             // Standard Filter ID 1.
  stdFiltelem.sfid2 = 0x2U;             // Standard Filter ID 2.

  //
  // Initialize bit timings.  MCAN_clk = 40 MGz
  //
  bitTimes.nomRatePrescalar = 1U;  // Nominal Baud Rate Pre-scaler
  bitTimes.nomTimeSeg1 = 9U;       // Nominal Time segment before SP
  bitTimes.nomTimeSeg2 = 8U;       // Nominal Time segment after SP
  bitTimes.nomSynchJumpWidth = 8U; // Nominal SJW

  bitTimes.dataRatePrescalar = 1U;  // Data Baud Rate Pre-scaler
  bitTimes.dataTimeSeg1 = 9U;       // Data Time segment before SP
  bitTimes.dataTimeSeg2 = 8U;       // Data Time segment after SP
  bitTimes.dataSynchJumpWidth = 8U; // Data SJW

  //
  // Get MCANSS Revision ID.
  //
  MCAN_getRevisionId(MCANA_DRIVER_BASE, &revId);

  //
  // Wait for Memory initialization to be completed.
  //
  while (FALSE == MCAN_isMemInitDone(MCANA_DRIVER_BASE))
    ;

  //
  // Put MCAN in SW initialization mode.
  //
  MCAN_setOpMode(MCANA_DRIVER_BASE, MCAN_OPERATION_MODE_SW_INIT);

  //
  // Wait till MCAN is not initialized.
  //
  while (MCAN_OPERATION_MODE_SW_INIT != MCAN_getOpMode(MCANA_DRIVER_BASE))
    ;

  //
  // Initialize MCAN module.
  //
  MCAN_init(MCANA_DRIVER_BASE, &initParams);

  //
  // Configure MCAN module.
  //
  MCAN_config(MCANA_DRIVER_BASE, &configParams);

  //
  // Configure Bit timings.
  //
  MCAN_setBitTime(MCANA_DRIVER_BASE, &bitTimes);

  //
  // Configure Message RAM Sections
  //
  MCAN_msgRAMConfig(MCANA_DRIVER_BASE, &msgRAMConfigParams);

  //
  // Configure Standard ID filter element
  //
  MCAN_addStdMsgIDFilter(MCANA_DRIVER_BASE, 0U, &stdFiltelem);

  //
  // Enable internal loopback mode
  //
  MCAN_lpbkModeEnable(MCANA_DRIVER_BASE, MCAN_LPBK_MODE_EXTERNAL, FALSE);

  //
  // Take MCAN out of the SW initialization mode
  //
  MCAN_setOpMode(MCANA_DRIVER_BASE, MCAN_OPERATION_MODE_NORMAL);

  while (MCAN_OPERATION_MODE_NORMAL != MCAN_getOpMode(MCANA_DRIVER_BASE))
    ;
}


static void MCANIntrConfig(void) {

  Interrupt_initModule();
  Interrupt_initVectorTable();

  Interrupt_register(INT_MCANA_0, &myMCAN_0_ISR);
  Interrupt_enable(INT_MCANA_0);

  Interrupt_enableGlobal();
}

//
// This is Interrupt Service Routine for MCAN interrupt 0.
//
volatile uint32_t mcanRxCount = 0;
volatile uint32_t errorStatus = 0;
volatile uint32_t intrStatus = 0;
 

__interrupt void myMCAN_0_ISR(void) 
{
    volatile uint16_t d1, d2, d3, cmd;
    uint32_t intrStatus;
    uint32_t reg, idx, level;
    
   cana0 = EPwm7Regs.TBCTR;
    
    intrStatus = MCAN_getIntrStatus(MCANA_DRIVER_BASE);
    
    // ⚡ Запретить MCAN прерывания
    MCAN_enableIntr(MCANA_DRIVER_BASE, MCAN_INTR_MASK_ALL, 0U);
    
    // ⚡ Очистить статус MCAN
    MCAN_clearIntrStatus(MCANA_DRIVER_BASE, intrStatus);
    MCAN_clearInterrupt(MCANA_DRIVER_BASE, 0x1);
    
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
            reg = HW_RD_REG32(MCANA_DRIVER_BASE + MCAN_RXF0S);
            idx = HW_GET_FIELD(reg, MCAN_RXF0S_F0GI);
            level = HW_GET_FIELD(reg, MCAN_RXF0S_F0FL);
            
            if (level == 0)
                break;

      //      MCAN_readMsgRam(MCANA_DRIVER_BASE, MCAN_MEM_TYPE_FIFO, idx, 
      //                     MCAN_RX_FIFO_NUM_0, &rxMsg);
            MCAN_readMsgRamFast(MCANA_DRIVER_BASE, idx, &rxMsg);
 
            cmd = (uint16_t)rxMsg.data[1] << 8 | rxMsg.data[0];
            d1  = (uint16_t)rxMsg.data[7] << 8 | rxMsg.data[6];
            d2  = (uint16_t)rxMsg.data[5] << 8 | rxMsg.data[4];
            d3  = (uint16_t)rxMsg.data[3] << 8 | rxMsg.data[2];

            cmd_MBOX28(d1, d2, d3, cmd);

            MCAN_writeRxFIFOAck(MCANA_DRIVER_BASE, MCAN_RX_FIFO_NUM_0, idx);
        }
    }
    cana_Time = EPwm7Regs.TBCTR - cana0;
    // ⚡ ОПТ: Одна строка вместо двух - разрешить всё, кроме TX Complete
    MCAN_enableIntr(MCANA_DRIVER_BASE, MCAN_INTR_MASK_ALL & ~MCAN_IR_TC_MASK, 1U);
    
   
}


//
// Массив адресов payload для быстрого доступа
// Индекс массива = TX Buffer Number (0-31)
//
#define MAX_TX_BUFFERS 32
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
    uint32_t baseAddr = MCANA_DRIVER_BASE + MCAN_MCAN_MSG_MEM;
    
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
    
    // Запрос передачи
    MCAN_txBufAddReq(MCANA_DRIVER_BASE, bufNum);
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
    uint32_t txbrp = HW_RD_REG32(MCANA_DRIVER_BASE + MCAN_TXBRP);
    
    // ⚡ Если буфер занят - считаем ОДИН раз
    if (txbrp & 0x1)
    {
        txBuf0WaitCycles[ilg] = cmd;  // ← Событие: "буфер был занят"
        
        // Ждём освобождения
        while (1)
        {
            txbrp = HW_RD_REG32(MCANA_DRIVER_BASE + MCAN_TXBRP);
            if ((txbrp & 0x1) == 0)  // Буфер свободен
                break;
        }
    }
    
    // Buffer свободен - отправляем ACK
    uint32_t addr = txPayloadAddr[0];
    HWREG(addr)      = ((uint32_t)d1 << 16) | cmd;
    HWREG(addr + 4U) = ((uint32_t)d3 << 16) | d2;
    
    MCAN_txBufAddReq(MCANA_DRIVER_BASE, 0);
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
    
    MCAN_txBufAddReq(MCANA_DRIVER_BASE, bufNum);
}

#endif // MCAN






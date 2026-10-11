/*
 * DCAN.h
 *
 *  Created on: 22.05.2025
 *      Author: Oleg Pavlovsky
 */

#ifndef MYDCAN_H_
#define MYDCAN_H_

#include "f28p55x_device.h"
#include "MBOX.h"

#define mbox1 0x1L
#define mbox2 0x2L
#define mbox3 0x3U
#define mbox4 0x4U
#define mbox5 0x5U
#define mbox6 0x6U

/* eCAN Message Data Register low (MDR_L) word definitions */
struct CANdata_WORDS
{                         // bits  description
    uint16_t LOW_WORD : 16; // 0:15
    uint16_t HI_WORD : 16;  // 31:16
};

/* eCAN Message Data Register low (MDR_L) byte definitions */
struct CANdata_BYTES
{                     // bits   description
    uint16_t BYTE3 : 8; // 31:24
    uint16_t BYTE2 : 8; // 23:16
    uint16_t BYTE1 : 8; // 15:8
    uint16_t BYTE0 : 8; // 7:0
};

// ===========================ADDED MANUALLY=============================
struct CANdata_BITS
{                      // bits   description
    uint16_t BIT_31 : 1; // 32:31
    uint16_t BIT_30 : 1; // 31:30
    uint16_t BIT_29 : 1; // 30:29
    uint16_t BIT_28 : 1; // 29:28
    uint16_t BIT_27 : 1; // 28:27
    uint16_t BIT_26 : 1; // 27:26
    uint16_t BIT_25 : 1; // 26:25
    uint16_t BIT_24 : 1; // 25:24
    uint16_t BIT_23 : 1; // 24:23
    uint16_t BIT_22 : 1; // 23:22
    uint16_t BIT_21 : 1; // 22:21
    uint16_t BIT_20 : 1; // 21:20
    uint16_t BIT_19 : 1; // 20:19
    uint16_t BIT_18 : 1; // 19:18
    uint16_t BIT_17 : 1; // 18:17
    uint16_t BIT_16 : 1; // 17:16
    uint16_t BIT_15 : 1; // 16:15
    uint16_t BIT_14 : 1; // 15:14
    uint16_t BIT_13 : 1; // 14:13
    uint16_t BIT_12 : 1; // 13:12
    uint16_t BIT_11 : 1; // 12:11
    uint16_t BIT_10 : 1; // 11:10
    uint16_t BIT_9 : 1;  // 10:9
    uint16_t BIT_8 : 1;  // 9:8
    uint16_t BIT_7 : 1;  // 8:7
    uint16_t BIT_6 : 1;  // 7:6
    uint16_t BIT_5 : 1;  // 6:5
    uint16_t BIT_4 : 1;  // 5:4
    uint16_t BIT_3 : 1;  // 4:3
    uint16_t BIT_2 : 1;  // 3:2
    uint16_t BIT_1 : 1;  // 2:1
    uint16_t BIT_0 : 1;  // 1:0
};

/* Allow access to the bit fields or entire register */

typedef union
{
    uint32_t               all;
    float                fall;
    struct CANdata_WORDS word;
    struct CANdata_BYTES byte;
    struct CANdata_BITS  bit; // ADDED MANUALLY
} CANdata_REG;

//----------------------------------------------------------------
struct u16
{
    uint16_t low;
    uint16_t hi;
};

typedef union
{
    struct u16 wrd;
    Uint32     all;
} union32;

//----------------------------------------------------------------
typedef union
{
    struct
    {
        uint16_t Lo;
        uint16_t Hi;
    };
    uint32_t Word;
} U32Pair;

//----------------------------------------------------------------------------
typedef struct
{
    uint16_t k60_run : 1;      //1 бит был пуском медленного кадра 131; с 11.10.2026 не
                               //  используется, оставлен ради номеров остальных битов
    uint16_t F1_convertor : 1; //2 бит команда включения/выключения быстрого кадра  CANA для конвертора
    uint16_t F1_invertor : 1;  //3 бит команда включения/выключения быстрого кадра  CANA для инвертора
    uint16_t F3_convertor : 1; //4 бит команда включения/выключения быстрого кадра  CANA для конвертора
    uint16_t F3_invertor : 1;  //5- k60 6 бит команда включения/выключения быстрого кадра  CANA

    uint16_t can_stopTX :1; //6 -внутренний бит остановки передачи в случае ошибки, чтобы не попасть на Bus_off
    uint16_t F3_grid : 1;   //7 бит быстрый кадр контура тока в сети (09.10.2026). После
                            //  can_stopTX, а не рядом с F3_*: вставка сдвинула бы его бит
} CAN_bit;

typedef union
{
    CAN_bit bit;
    uint16_t  all;
} CAN_kadr;


typedef struct {
    uint16_t cmd;
    uint16_t data3;
    uint16_t data2;
    uint16_t data1;
} CAN_Message;

extern uint16_t data1,data2, data3, cmd;

void send_REF_kadr();

// Ключ выбора набора величин в словах 4..7 кадра 140 (REF_Kadr.c).
// Наблюдать все три набора одновременно смысла нет, а слов в кадре нет лишних,
// поэтому один и тот же слот показывает то, что выбрал оператор командой 634.
//   0 = сырые коды АЦП
//   1 = мгновенные физические значения
//   2 = действующие за период сети (считает CLA)
#define TELE_KOD 0
#define TELE_INS 1
#define TELE_RMS 2
extern uint16_t teleSel;
void CAN_rxFIFOread(void);
void SendViaIF1(uint32_t base, uint32_t mailbox, const uint16_t *data);
void Send_mbox(uint16_t ObjID, uint16_t d1, uint16_t d2, uint16_t d3, uint16_t d4);
// Отправка кадра произвольной длины (>8 байт) одним запросом. w — массив 16-битных слов,
// numWords — их число (8 байт = 4 слова, 64 байта = 32 слова). Пакует по 2 слова в 32-битное
// слово Message RAM (C28x: байт = 16 бит). Один MCAN_txBufAddReq в конце.
void Send_mbox_FD(uint16_t bufNum, const uint16_t *w, uint16_t numWords);
// Смена DLC быстрого кадра (буфер 1) при выборе режима в MBOX28: F1 -> 8, F3 -> 12.
void MCAN_setFastDLC(uint16_t dlc);
void SendAck(int d1, int d2, int d3, int cmd);
void CAN_configureIF3ForMailbox1(void);


void readViaIF3AndClearFlags(uint32_t base, uint32_t mailbox);
void initCAN_DMA_IF3_single(void);
void cmd_MBOX28( uint16_t data1, uint16_t data2, uint16_t data3, uint16_t cmd);


volatile extern union word32 rxA, rxB;
volatile extern union word32 txA, txB;

extern CAN_kadr canaKadr;
extern volatile uint16_t rxBuf[4]; // [cmd, data3, data2, data1]

#endif /* MYDCAN_H_ */

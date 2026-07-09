/*
 * SPI_EEPROM.h
 *
 *  Created on: 25 ���. 2024 �.
 *      Author: Oleg Pavlovsky
 */
#include <stdint.h>

#ifndef SPI_EEPROM_H_
#define SPI_EEPROM_H_

#include "myADC.h"
#include "Errors.h"

//
// Defines  EEPROM_ADDR
//
#define offsets_addr 0x0010U  // адресс офсетов в EEPROM (смещён от нуля)
#define OFFSETS_EE_WORDS ((uint16_t)sizeof(ADCpar))
#define KADC_EE_WORDS    ((uint16_t)sizeof(ADCpar))
#define ERRBUFF_EE_WORDS ((uint16_t)(ERR_BUFF_SIZE * sizeof(ErrData)))
#define kadc_addr   (offsets_addr + (uint16_t)(OFFSETS_EE_WORDS * 2U))
#define errors_addr (kadc_addr + (uint16_t)(KADC_EE_WORDS * 2U))
#define session_id_addr (errors_addr + (uint16_t)(ERRBUFF_EE_WORDS * 2U))
#define errors_history_addr (session_id_addr + 2U)

#define NUM_BYTES 8
#define DUMMY_DATA 0
#define NO_DELAY 0

#define BUFFER_SIZE 20

//
// Defines for Chip Select toggle.
//
#define CS_LOW GpioDataRegs.GPACLEAR.bit.GPIO29 = 1 // Pull chip select low.
#define CS_HIGH GpioDataRegs.GPASET.bit.GPIO29 = 1  // Pull chip select high.

//
// SPI EEPROM status
//
#define MSG_STATUS_READY_M 0x0001       // EEPROM is ready (not busy)
#define MSG_STATUS_WRITE_READY_M 0x0002 // EEPROM
#define MSG_STATUS_BUSY 0xFFFF          // EEPROM is busy (internal write)

// #define BUFFULL_FLAG    (uint16_t)(1U << 5)
// #define INT_FLAG    (uint16_t)(1U << 6)
//
//  Opcodes for the EEPROM (16-bit)
//
#define RDSR 0x0500U   // команда чтения регистра статуса EEPROM
#define READ0 0x0300U  // команда чтения EEPROM c адресса 0x000000
#define READ1 0x0301U  // команда чтения EEPROM c адресса 0x010000
#define WRITE0 0x0200U // команда записи в EEPROM c адресса 0x000000
#define WRITE1 0x0201U // команда записи в EEPROM c адресса 0x010000
#define WREN 0x0600U   // команда разрешения записи в EEPROM
#define WRDI 0x0400U   //
#define WRSR 0x0100U   // записать в статус регистр
#define RDID 0x9F00U   // RDID (Device ID Read) Instruction

#define RECALL 0x6000U // команда копирования из nvRAM to SRAM
#define STORE 0x3C00U  // команда копирования из SRAM to nvRAM
#define ASENB 0x5900U  // команда разрешения авто копирования из SRAM to nvRAM
#define ASDISB 0x1900U // команда запрета авто копирования из SRAM to nvRAM

typedef union {
    uint8_t all;
    struct {
        uint8_t RDY   : 1; // Bit 0: Ready. Is set by the device while a STORE or Software RECALL cycle is in progress.
        uint8_t WEN   : 1; // Bit 1: Write Enable
        uint8_t BP0   : 1; // Bit 2: Block Protect bit ‘0’
        uint8_t BP1   : 1; // Bit 3: Block Protect bit ‘1’
        uint8_t RES4  : 1; // Bit 4: Reserved (always 0)
        uint8_t RES5  : 1; // Bit 5: Reserved (always 0)
        uint8_t SLN   : 1; // Bit 6: Serial Number Lock. Set to '1' for locking serial number
        uint8_t WPEN  : 1; // Bit 7: Write Protect Enable
    } bits;
} nvSRAM_StatusReg;


//
// Function Prototypes
//
uint16_t readStatusRegister(uint32_t base);

void spi_saveToEEPROM(uint32_t base, uint16_t cmd, uint16_t address, uint16_t *txdata, uint16_t txnumb);
void spi_readEEPROM(uint32_t base, uint16_t cmd, uint16_t address, uint16_t *rxdata, uint16_t rxnumb);

void readData(uint32_t base, uint16_t cmd, uint16_t address, uint16_t *rxdata, uint16_t numOfWords);

void SPI_FIFO_readEEPROM(uint32_t base, uint16_t cmd, uint16_t address, uint16_t *rxdata, uint16_t numOfWords);
void EEPROM_Init(void);
void sendDummy(void);
void spiWriteHandler(uint32_t base);
void spiReadHandler(uint32_t base);
void checkEE();
extern uint16_t send_EEcomand(uint32_t base, uint16_t cmd);
void EEprom_read_words(uint32_t base, uint16_t cmd, uint16_t address, uint16_t *rxdata, uint16_t rxnumb);
void EEprom_write_words(uint32_t base, uint16_t cmd, uint16_t address, uint16_t *rxdata, uint16_t rxnumb);
__interrupt void INT_mySPIB_RX_ISR(void);

extern uint16_t  TXbuff[];
extern uint16_t  RXbuff[];
extern uint16_t  writeBuffer[];
extern uint16_t *pTxbuf; // указатель на адресс первого слова  буффера ОЗУ для записи данных в EEPROM
extern uint16_t *pRxbuf; // указатель на адресс данных первого слова приёмного буффера в ОЗУ

struct SPI_RUN
{
    uint16_t reading : 1;     // признак что идёт приём  по шине SPI
    uint16_t startread : 1;   // обозначает фазу приёма команды и адресса
    uint16_t writing : 1;     // признак что идёт  передача по шине SPI
    uint16_t firstOffset : 1; //признак первого после включения программы чтения offsets из EEPROM
    uint16_t firstKadc : 1;   //признак первого после включения программы чтения kadc из EEPROM
};

extern struct SPI_RUN         spirun;
extern  nvSRAM_StatusReg  eeStatusReg;

#endif /* SPI_EEPROM_H_ */

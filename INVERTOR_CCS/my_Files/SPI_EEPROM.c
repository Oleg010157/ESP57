/*
 * SPI_EEPROM.c
 *
 *  Created on: 25 ноября 2024 года.
 *      Author: Oleg Pavlovsky
 *  Модифицированна : 04.05.2025-> B2
 */

//
// Included Files
//

#include "board.h"
#include "f28p55x_device.h"
#include "SPI_EEPROM.h"
#include "myADC.h"
#include "Errors.h"

//
// Globals
//
uint16_t         numOfWords;
uint16_t         txBuffer_pos = 0;
uint16_t         rxBuffer_pos = 0;
struct SPI_RUN   spirun;
uint16_t        *pTxbuf; // адресс первого слова  буффера ОЗУ для записи данных в EEPROM
uint16_t        *pRxbuf; // адресс данных первого слова приёмного буффера в ОЗУ
nvSRAM_StatusReg eeStatusReg;

//
// Function to start write data to the EEPROM
// - address is the byte address of the EEPROM
// - txdata is a pointer to an array of data being sent
// - txnumb is the number of characters in the array to send
//
void spi_saveToEEPROM(uint32_t base, uint16_t cmd, uint16_t address, uint16_t *txdata, uint16_t txnumb)
{
    spirun.writing = 1; // захват SPI шины для записи в EEPROM
    //проверяем что  буфер передачи  FIFO пустой т.е. можно начинать передачу
    //иначе просто выходим из программы
    //   if (SPI_getTxFIFOStatus(base) != SPI_FIFO_TXEMPTY)
    {
        //     return;
    }
    // 50 loops, 5 cycles per loop + 9 cycles overhead = 259 cycles/120 = 2.158 us
    SysCtl_delay(50); // задержка необходимая для завершения передачи
                      // последнего слова из TXBUF

    CS_HIGH;
    SPI_resetRxFIFO(base);
    SPI_resetTxFIFO(base);
    CS_LOW;

    // Send the WREN opcode:
    HWREGH(base + SPI_O_TXBUF) = WREN;

    // ожидаем завершения передачи команды WREN:
    while (SPI_getRxFIFOStatus(base) < 1)
    {
    }

    CS_HIGH;

    txBuffer_pos = 0;
    numOfWords   = txnumb;
    pTxbuf       = txdata;

    CS_LOW; // Send the WRITE opcode.
    HWREGH(base + SPI_O_TXBUF) = cmd;
    HWREGH(base + SPI_O_TXBUF) = address;
}

void spi_readEEPROM(uint32_t base, uint16_t cmd, uint16_t address, uint16_t *rxdata, uint16_t rxnumb)
{
    //проверяем что  буфер передачи  FIFO пустой т.е. можно начинать передачу
    //иначе просто выходим из программы
    //   if (SPI_getTxFIFOStatus(base) != SPI_FIFO_TXEMPTY)
    {
        //        return;
    }
    SysCtl_delay(50); // задержка на время передачи одного слова

    CS_HIGH;
    SPI_resetRxFIFO(base);
    SPI_resetTxFIFO(base);

    CS_LOW; // Send the READ opcode and address.
    HWREGH(base + SPI_O_TXBUF) = cmd;
    HWREGH(base + SPI_O_TXBUF) = address;

    // заполняем TXFIFO буффер до полного или до длины пакета
    // в зависимости от того что наступит раньше.
    txBuffer_pos = 0;
    while ((txBuffer_pos < rxnumb) && (txBuffer_pos < 14) && (SPI_getTxFIFOStatus(base) != SPI_FIFO_TXFULL))
    {
        // до тех пор пока есть хоть одна свободная ячейка в буффер TXFIFO
        // и ещё есть данные для передачи -> заполняем буффер TXFIFO
        HWREGH(base + SPI_O_TXBUF) = 0;
        txBuffer_pos++;
    }

    spirun.reading   = 1; // запускаем фоновый обработчик чтения данных
    spirun.startread = 1; // устанавливаем признак начала передачи для того чтобы вырезать из
                          // приёмного FIFO код команды и адресс
    rxBuffer_pos = 0;
    numOfWords   = rxnumb;
    pRxbuf       = rxdata;
}

//==========================================================================
// Обработчик записи  данных в EEPROM по SPI шине
// для работы в фоне.
void spiWriteHandler(uint32_t base)
{
    while ((txBuffer_pos < numOfWords) && (SPI_getTxFIFOStatus(base) != SPI_FIFO_TXFULL))
    {
        // до тех пор пока есть хоть одна свободная ячейка в буффер TXFIFO
        // и ещё есть данные для передачи -> заполняем буффер TXFIFO
        {
            HWREGH(base + SPI_O_TXBUF) = pTxbuf[txBuffer_pos];
            txBuffer_pos++;
        }
    }

    if (txBuffer_pos == numOfWords)

    { // первый вход после завершения передачи всех данных

        spirun.writing = 0;
        //    eeprom.bit.ReadCoef = 1; //переходим на режим чтения
    }
}

//==========================================================================
// Обработчик чтения  данных из EEPROM по SPI шине
// для работы в фоне.

void spiReadHandler(uint32_t base)
{
    volatile uint16_t tmp;
    // до тех пор пока не прочитанны все данные из EEPROM
    // и есть ещё не считанные данные из  буффера  читаем RXFIFO

    if (spirun.startread == 1) //вначале считываем из SPI_O_RXBUF код и адресс
    {
        if (SPI_getRxFIFOStatus(base) > SPI_FIFO_RX2)
        {
            tmp = HWREGH(base + SPI_O_RXBUF);
            tmp = HWREGH(base + SPI_O_RXBUF);

            spirun.startread = 0;
        }
        return; // обязательный оператор. Нужен на случай если не завершена
                // передача команды или адресса.
    }

    while (SPI_getRxFIFOStatus(base) != 0)
    //   while (SpibRegs.SPIFFRX.bit.RXFFST != 0)

    {
        pRxbuf[rxBuffer_pos] = HWREGH(base + SPI_O_RXBUF);
        rxBuffer_pos++;
    }

    // с этого места буффер RXFIFO должен быть пустым
    // дальше проверяем сколько слов DUMMY  было переданно
    // и если это число  меньше требуемого количества слов
    // и в буффере передачи есть свободные ячейки, то заполняем буффер.
    while ((rxBuffer_pos < numOfWords) && (SPI_getTxFIFOStatus(base) < SPI_FIFO_TXFULL))
    //     (SpibRegs.SPIFFTX.bit.TXFFST < 16))
    {
        // до тех пор пока есть хоть одна свободная ячейка и ещё есть
        // данные для передачи -> заполняем буффер TXFIFO нулями
        //  SpibRegs.SPITXBUF = 0;
        HWREGH(base + SPI_O_TXBUF) = 0;
        txBuffer_pos++;
    }

    if (rxBuffer_pos > (numOfWords - 1))

    { // первый вход после завершения приёма всех данных

        spirun.reading = 0;
        if (spirun.firstKadc && (pRxbuf == (uint16_t *)&kadc))
        {
 //           checkEE(); // проверяем контрольную сумму
            spirun.firstKadc = 0;
 //           currentLimit(Imax); // вычисляем уровни срабатывания защиты по току
            LoadOffsetsToPPB(); // загружаем offsets в регистры PPB.
        }
    }
}

// ===========================================================================
// Function to send RDSR opcode and return the status of the EEPROM
//

uint8_t readStatusRegister(uint32_t base)
{
    uint8_t temp;

    CS_LOW;

    // Send RDSR opcode

    //   ожидаем пока будет свободен SPITXBUF
    while (SpibRegs.SPISTS.bit.BUFFULL_FLAG != 0)
    {
    };
    // загружаем команду чтениѝ статусного регистра
    SpibRegs.SPITXBUF = RDSR; // INT_FLAG =0
    //   ожидаем завершения передачи команды RDSR
    while ((SpibRegs.SPISTS.bit.INT_FLAG) == 0)
    {
    };
    // читаем SPIRXBUF и одновременно cбраcываем
    // флаг INT_FLAG = 0
    temp = SpibRegs.SPIRXBUF;

    CS_HIGH;

    return (temp);
}

uint16_t send_EEcomand(uint32_t base, uint16_t cmd)
{
    uint16_t tmp;
    CS_HIGH;
    SPI_resetRxFIFO(base);
    SPI_resetTxFIFO(base);
    CS_LOW;

    // Send the cmd opcode:
    HWREGH(base + SPI_O_TXBUF) = cmd;

    // ожидаем завершения передачи команды cmd:
    while (SPI_getRxFIFOStatus(base) < 1)
    {
    }
    tmp = HWREGH(base + SPI_O_RXBUF);
    CS_HIGH;

    return tmp;
}

void checkEE() //вызывается по завершению чтения из EEPROM калибровок offsets и  kadc
{
    uint16_t crc = ControlSum(&offsets);

    if (crc != offsets.crc)
    {
        if (offsets.crc == 0) // АЦП не калибровалось. Оставляем  дефолтные значения offsets.
        {
            if (ErrBuff[Offsets_NOCAL].Pars.SessionCnt < 0x00FFU)
            {
                ErrBuff[Offsets_NOCAL].Pars.SessionCnt++;
            }
            ErrHistory_OnError(Offsets_NOCAL);
        }
        else // ошибка EEPROM. Переходим на дефолтные значения.
        {
            CheckLamp0 = 1;

            if (ErrBuff[Offsets_ERR].Pars.SessionCnt < 0x00FFU)
            {
                ErrBuff[Offsets_ERR].Pars.SessionCnt++;
            }
            ErrHistory_OnError(Offsets_ERR);
        }

        offsets = offsets_default;
    }
    
    LoadOffsetsToPPB();

    uint16_t ksum = ControlSum(&kadc);
    if (ksum != kadc.crc)
    {
        if (kadc.crc == 0) // Коэф.усиления не калибровались.Установленны дефолтные значения для kadc
        {
            if (ErrBuff[Kadc_NOCAL].Pars.SessionCnt < 0x00FFU)
            {
                ErrBuff[Kadc_NOCAL].Pars.SessionCnt++;
            }
            ErrHistory_OnError(Kadc_NOCAL);
        }
        else // ошибка EEPROM. Переходим на дефолтные значения.
        {
            CheckLamp0 = 1;
            if (ErrBuff[Kadc_ERR].Pars.SessionCnt < 0x00FFU)
            {
                ErrBuff[Kadc_ERR].Pars.SessionCnt++;
            }
            ErrHistory_OnError(Kadc_ERR);
        }

        kadc = kadc_default;
    }

    floatKadc();
}
volatile uint16_t rxsts = 0;

void EEprom_read_words(uint32_t base, uint16_t cmd, uint16_t address, uint16_t *rxdata, uint16_t rxnumb)
{
    // эта прога читает массив 16-бит данных через буфер FIFO;
    // за один SPI-пакет допустимо не более 14 слов данных (2 слова заняты cmd+address)
    uint16_t remaining;
    uint16_t currentAddress;
    uint16_t *dst;

    if ((rxdata == NULL) || (rxnumb == 0U))
    {
        return;
    }

    remaining = rxnumb;
    currentAddress = address;
    dst = rxdata;

    while (remaining > 0U)
    {
        uint16_t i;
        uint16_t chunkWords = (remaining > 14U) ? 14U : remaining;

        CS_HIGH;
        SPI_resetRxFIFO(base);
        SPI_resetTxFIFO(base);
        CS_LOW; // Send the READ opcode and address.

        HWREGH(base + SPI_O_TXBUF) = cmd;
        HWREGH(base + SPI_O_TXBUF) = currentAddress;

        // заполняем TXFIFO буффер DUMMY нулями до длины пакета
        for (i = 0U; i < chunkWords; i++)
        {
            HWREGH(base + SPI_O_TXBUF) = 0U;
        }

        // ожидаем завершения передачи DUMMY нулей
        while (SPI_getRxFIFOStatus(base) < (chunkWords + 2U)) // +2 с учётом cmd+address
        {
        }

        i = HWREGH(base + SPI_O_RXBUF); // выгружаем из rxFIFO код команды
        i = HWREGH(base + SPI_O_RXBUF); // выгружаем из rxFIFO адрес

        // читаем данные из rxFIFO буффера в массив ОЗУ
        for (i = 0U; i < chunkWords; i++)
        {
            dst[i] = HWREGH(base + SPI_O_RXBUF);
        }

        CS_HIGH;

        dst += chunkWords;
        currentAddress = (uint16_t)(currentAddress + (uint16_t)(chunkWords * 2U));
        remaining = (uint16_t)(remaining - chunkWords);
    }
}

void EEprom_write_words(uint32_t base, uint16_t cmd, uint16_t address, uint16_t *rxdata, uint16_t rxnumb)
{
    // эта прога пишет массив 16-бит данных в sRAM EEPROM через буфер SPI-txFIFO;
    // за один SPI-пакет допустимо не более 14 слов данных (2 слова заняты cmd+address)
    uint16_t remaining;
    uint16_t currentAddress;
    uint16_t *src;

    if ((rxdata == NULL) || (rxnumb == 0U))
    {
        return;
    }

    remaining = rxnumb;
    currentAddress = address;
    src = rxdata;

    while (remaining > 0U)
    {
        uint16_t i;
        uint16_t chunkWords = (remaining > 14U) ? 14U : remaining;

        CS_HIGH;
        SPI_resetRxFIFO(base);
        SPI_resetTxFIFO(base);

        // Send the WREN opcode:
        CS_LOW;
        HWREGH(base + SPI_O_TXBUF) = WREN;
        // ожидаем завершения передачи команды WREN:
        while (SPI_getRxFIFOStatus(base) < 1U)
        {
        }
        CS_HIGH;
        i = HWREGH(base + SPI_O_RXBUF); // обнуляем rxFIFO

        CS_LOW; // Send the WRITE opcode.
        HWREGH(base + SPI_O_TXBUF) = cmd;
        HWREGH(base + SPI_O_TXBUF) = currentAddress;

        // заполняем TXFIFO буффер массивом данных
        for (i = 0U; i < chunkWords; i++)
        {
            HWREGH(base + SPI_O_TXBUF) = src[i];
        }

        rxsts = SPI_getRxFIFOStatus(base);
        // ожидаем завершения передачи массива данных:
        while (SPI_getRxFIFOStatus(base) < (chunkWords + 2U)) // +2 с учётом cmd+address
        {
        }

        CS_HIGH;

        src += chunkWords;
        currentAddress = (uint16_t)(currentAddress + (uint16_t)(chunkWords * 2U));
        remaining = (uint16_t)(remaining - chunkWords);
    }
}

//
// End of File
// ===========================================================================Kadc_ERR

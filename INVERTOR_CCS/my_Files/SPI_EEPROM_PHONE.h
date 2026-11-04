

//================== EEPROM  ========================================
// если есть приём-передача переходим к соответствующему обработчику
// иначе  нужно проверить заказ на приём или передачу по шине SPI
if (spirun.writing)
{
    spiWriteHandler(mySPIB_BASE);
    goto endSPI; // выходим из тела работы с EEPROM не проверяя запросы
                 // на приём-передачу
}
if (spirun.reading)
{
    spiReadHandler(mySPIB_BASE);
    goto endSPI; // выходим из тела работы с EEPROM.
}

// если нет приёма-передачи  нужно проверить заказ на приём или
// передачу по шине SPI
// проверяем что буфер передачи FIFO пустой т.е. можно начинать
// передачу иначе просто выходим из программы.
if (SPI_getTxFIFOStatus(mySPIB_BASE) != SPI_FIFO_TXEMPTY)
    goto endSPI;

if (eeprom.all == 0)
    goto endSPI; // если нет запросов на приём или передачу то обходим секцию EEPROM.

if (eeprom.bit.Store)
{
    send_EEcomand(SPIB_BASE, WREN);
    send_EEcomand(SPIB_BASE, STORE); //дополнительный RECALL на случай если не было выключения питания.
    DEVICE_DELAY_US(20000);          // подождать завершения команды RECALL
    eeprom.bit.Store = 0;
    goto endSPI; // если нет запросов на приём или передачу то обходим секцию EEPROM.
}
if (eeprom.bit.Recall)
{
    send_EEcomand(SPIB_BASE, WREN);
    send_EEcomand(SPIB_BASE, RECALL); //дополнительный RECALL на случай если не было выключения питания.
    DEVICE_DELAY_US(20000);           // подождать завершения команды RECALL
    eeprom.bit.Recall = 0;
    eeprom.bit.ReadOffsets = 1;
    eeprom.bit.ReadCoef = 1;
    goto endSPI; // если нет запросов на приём или передачу то обходим секцию EEPROM.
}

if (eeprom.bit.ReadOffsets)
{
    EEprom_read_words(SPIB_BASE, READ0, offsets_addr, &offsets.iPri,
                       (uint16_t)(sizeof(offsets) / sizeof(uint16_t)));
    eeprom.bit.ReadOffsets = 0;
    goto endSPI;
}



if (eeprom.bit.SaveOffsets)
{
    offsets.crc = ControlSum(&offsets);
    EEprom_write_words(SPIB_BASE, WRITE0, offsets_addr, &offsets.iPri,
                        (uint16_t)(sizeof(offsets) / sizeof(uint16_t)));
    eeprom.bit.SaveOffsets = 0;
    goto endSPI;
}

if (eeprom.bit.ReadCoef)
{
     EEprom_read_words(SPIB_BASE, READ0, kadc_addr, &kadc.iPri,
                        (uint16_t)(sizeof(kadc) / sizeof(uint16_t)));
    eeprom.bit.ReadCoef = 0;
    goto endSPI;
}

if (eeprom.bit.SaveCoef)
{
    EEprom_write_words(SPIB_BASE, WRITE0, kadc_addr, &kadc.iPri,
                        (uint16_t)(sizeof(kadc) / sizeof(uint16_t)));
    eeprom.bit.SaveCoef = 0;
    goto endSPI;
}

// История аварий хранится не здесь, а в памяти на шине I2C: см. nv_store.c.




endSPI :;

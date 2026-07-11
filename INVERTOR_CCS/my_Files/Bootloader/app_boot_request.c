/*****************************************************************************
 * app_boot_request.c
 *
 * Функция для основного приложения F280039C:
 * При получении CAN-команды 9999 — отключает силовую часть,
 * запрещает прерывания и напрямую переходит в загрузчик.
 * Загрузчик переинициализирует CAN на 250K и ждёт handshake.
 *
 * Перезагрузка НЕ нужна — код загрузчика уже в RAM
 * (.TI.ramfunc скопирован при старте в Board_init).
 *
 * Вызывается из cmd_MBOX28() при получении команды 9999.
 *
 *****************************************************************************/

#include "driverlib.h"
#include "device.h"

/* bootloader_main() определён в can_bootloader.c, выполняется из RAM */
extern void bootloader_main(void);

/* IGBT disable — определены в board.h / myEPWM.h */
extern void IGBT1_Disable(void);
extern void IGBT2_Disable(void);

#pragma CODE_SECTION(app_request_firmware_update, ".TI.ramfunc")

void app_request_firmware_update(void)
{
    /* 1. Безопасность: отключаем силовую часть */
    IGBT1_Disable();
    IGBT2_Disable();

    /* 2. Запрещаем все прерывания */
    DINT;

    /* 3. Прямой переход в загрузчик.
     *    bootloader_main() переинициализирует CAN ( ID 0x64/0x65)
     *    и начнёт протокол обмена. В конце сделает SysCtl_resetDevice(). */
    bootloader_main();

    /* Сюда не попадём — bootloader_main() завершается reset-ом */
    SysCtl_resetDevice();
}

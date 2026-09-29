# CAN-загрузчик F28P55x

Прошивка прибора по CAN-FD из UI Motor_Control, без JTAG.

| Файл | Что делает |
|---|---|
| `can_bootloader.c` | сам загрузчик: транспорт MCAN и работа с флешем |
| `can_boot_protocol.h` | команды, квитанции, карта флеша |
| `app_boot_request.c` | вход из приложения по команде 9999 |
| `CAN_Bootloader_Protocol.md` | описание протокола — читать первым |

## Что уже сделано в проекте

Ничего доподключать не нужно, всё на месте:

- папка входит в сборку конфигурации **CPU1_FLASH** и исключена из **CPU1_RAM**
  (прошивать флеш из RAM-сборки незачем);
- библиотека Flash API подключена и втянута в `.TI.ramfunc` — GROUP в
  `28p55x_generic_flash_lnk.cmd`, путь поиска в настройках линкера
  (`-i"${COM_TI_C2000WARE_INSTALL_DIR}/libraries/flash_api/f28p55x/lib"`);
- вызов `app_request_firmware_update()` стоит в фоновом цикле `main()`.

## Если правите загрузчик

Проверьте по `CPU1_FLASH/invertor.map`, что у `can_bootloader.obj` и
`app_boot_request.obj` нет ни одной строки с `(.text)`. Появилась — во флеш
уехала функция, которая будет вызвана после стирания этого самого флеша.
Разбор — в конце `CAN_Bootloader_Protocol.md`.

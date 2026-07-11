# CAN Bootloader F280039C

## Integracia v suschestvuyuschiy proekt 3ph_Inverter

### Shag 1: Dobavit faily v CCS
Right-click project -> Add Files:
- Bootloader/can_bootloader.c
- Bootloader/app_boot_request.c
- Bootloader/can_boot_protocol.h

### Shag 2: Include Path
Project -> Properties -> Build -> C2000 Compiler -> Include Options
Add: ${PROJECT_ROOT}/Bootloader

### Shag 3: Flash API Library
Project -> Properties -> Build -> C2000 Linker -> File Search Path
Add lib: C:\ti\C2000Ware_6_00_01_00\libraries\flash_api\f28003x\lib\FAPI_F28003x_EABI_v1.58.10.lib

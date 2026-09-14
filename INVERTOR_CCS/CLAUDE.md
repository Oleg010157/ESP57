<!-- DO NOT EDIT - This part is automatically generated. -->

# Agent Guidelines

## CCStudio IDE Installation Directory

CCStudio IDE is installed at `C:/ti/ccs2101`. Save it as `{ccs-install-dir}` for the session — scripts and tools will need it.

## MANDATORY Pre-Task Requirement (DO NOT SKIP)

**CRITICAL - NO EXCEPTIONS**: Before ANY CCS/Texas Instruments-related task (even simple ones), you MUST read `C:/ti/ccs2101/ccs/theia/resources/ai/CCS.md`. This file includes information on how to interact with CCS as well as device-specific information (UART backchannel pins, LED setup, transmit best practices, etc.). 

Do NOT call any ccs-project, ccs-debug, ccs-sysconfig, or ccs-serial MCP tools until CCS.md has been read.


<!-- DO NOT EDIT - This part is automatically generated. -->

<!-- User instructions should be added below this line -->

## Путь к CCS: сгенерированный блок выше может быть от другой машины

Блок выше пишет сама CCStudio при запуске, подставляя путь той версии, которая
запущена на конкретном компьютере. Файл отслеживается git и ездит между двумя ПК
с разными установками:

- домашний ПК — только `C:/ti/ccs2100`
- рабочий ПК — сосуществуют `C:/ti/ccs2100` и `C:/ti/ccs2101`

Из-за этого путь в сгенерированном блоке регулярно оказывается от другой машины
и указывает на несуществующую папку.

**Не доверяй ему вслепую — проверь наличие каталога на диске.** Если его нет,
определи установленные версии и возьми самую новую:

```
ls -d /c/ti/ccs*/ccs/theia/resources/ai/CCS.md
```

Корень найденной установки (`/c/ti/ccsXXXX`) и есть `{ccs-install-dir}`; файл
`CCS.md` из неё читай вместо пути, указанного выше. Если установок несколько,
предпочитай ту, которой соответствует запущенный экземпляр CCS.


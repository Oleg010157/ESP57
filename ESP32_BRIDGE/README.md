# ESP32_BRIDGE — мост SPI ↔ WiFi для прибора K57

ESP32 — ведомый SPI у F28P55x и прозрачный мост в сеть. Провода, режим SPI и
почему так — `docs/esp32-spi-wiring-RU.md`.

**Сейчас: кадры по SPI, без WiFi.** Прибор раз в 2 мс отдаёт посылку 512 байт
с теми же кадрами, что идут по CAN FD. ESP32 разбирает её и раз в секунду
печатает в свой COM-порт (115200), сколько кадров каждого номера пришло.
Формат — `docs/esp32-spi-protocol-RU.md` и `main/spi_link.h`, сторона прибора —
`INVERTOR_CCS/my_Files/wifi_link.c` (итоги в Watch: `wifiLink`).

## Сборка и прошивка

ESP-IDF v5.4. В PowerShell окружение поднимается скриптом установщика —
`export.ps1` из самого IDF без системного Python не работает:

```
. C:\Espressif\Initialize-Idf.ps1
cd ESP32_BRIDGE
idf.py build
idf.py -p COM<N> flash monitor
```

### В VS Code

Нужно расширение Espressif IDF. Открывать **папку `ESP32_BRIDGE`**, а не весь
ESP57: расширение ищет `CMakeLists.txt` проекта в корне окна. Сборка, прошивка
и монитор — кнопками в нижней строке. Настройки `.vscode` (версия IDF, порт,
IntelliSense) локальные и в репозиторий не попадают. Подсказки по коду
появятся после первой сборки: пути берутся из `build/compile_commands.json`.

На этой же плате жила прошивка ESP_INV (`C:\Work\ESP_INV\firmware`).
Вернуть её — `idf.py -p COM<N> flash` из её каталога.

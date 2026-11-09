cmake_minimum_required(VERSION 3.16)

# Мост SPI <-> WiFi между прибором K57 (F28P55x) и программой на PC.
# Провода и режим SPI - docs/esp32-spi-wiring-RU.md, формат посылки и сети -
# docs/esp32-spi-protocol-RU.md.

# Сеть и пароль WiFi - в sdkconfig.local, а не в sdkconfig. `idf.py
# set-target` УДАЛЯЕТ sdkconfig и собирает его заново из умолчаний, и
# введённое через menuconfig пропадает молча; файл умолчаний idf.py не трогает
# никогда. В репозиторий файл не попадает (.gitignore). Тот же порядок, что в
# ESP_INV, - и по той же причине: там так однажды пропал пароль.
if(NOT EXISTS "${CMAKE_CURRENT_LIST_DIR}/sdkconfig.local")
    file(WRITE "${CMAKE_CURRENT_LIST_DIR}/sdkconfig.local"
"# Локальные настройки: в репозиторий не попадают.\n\
# Сеть и пароль - своё на каждой машине. Впишите и соберите заново.\n\
CONFIG_K57_WIFI_SSID=\"\"\n\
CONFIG_K57_WIFI_PASS=\"\"\n")

    message(WARNING
        "Создан ESP32_BRIDGE/sdkconfig.local - впишите туда сеть и пароль WiFi, "
        "иначе плата поднимет свою точку доступа K57.")
endif()

set(SDKCONFIG_DEFAULTS "sdkconfig.defaults;sdkconfig.local")

include($ENV{IDF_PATH}/tools/cmake/project.cmake)

project(esp32_bridge)

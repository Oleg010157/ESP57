cmake_minimum_required(VERSION 3.16)

# Мост SPI <-> WiFi между прибором K57 (F28P55x) и программой на PC.
# Пока это только проверка SPI-канала, WiFi появится следующим шагом.
# Провода и режим SPI - docs/esp32-spi-wiring-RU.md.

include($ENV{IDF_PATH}/tools/cmake/project.cmake)

project(esp32_bridge)

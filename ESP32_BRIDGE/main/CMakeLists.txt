# Таблицы Motor_Control для страницы моста (tables.c): все CanTxFiles/*.ini
# одним файлом tables.txt. Строки [Settings] - раскладка окна Motor_Control в
# base64, странице не нужна, и выбрасывается. Правка .ini - пересборка моста;
# НОВЫЙ файл в CanTxFiles - `idf.py reconfigure`: GLOB без CONFIGURE_DEPENDS,
# его ESP-IDF не пускает на раннем проходе (режим скрипта).
file(GLOB K57_TABLES "${CMAKE_CURRENT_LIST_DIR}/../../bin/Debug/CanTxFiles/*.ini")
list(SORT K57_TABLES)
set(K57_TABLES_TXT "${CMAKE_CURRENT_BINARY_DIR}/tables.txt")
set(_k57t "")
foreach(_f ${K57_TABLES})
    get_filename_component(_n "${_f}" NAME_WE)
    file(READ "${_f}" _c)
    string(REGEX REPLACE "\\[Settings\\][^\n]*" "" _c "${_c}")
    string(APPEND _k57t "### ${_n}\n${_c}\n")
endforeach()
file(WRITE "${K57_TABLES_TXT}" "${_k57t}")
if(NOT CMAKE_BUILD_EARLY_EXPANSION)
    set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS ${K57_TABLES})
endif()

idf_component_register(
    SRCS
        "main.c"
        "spi_link.c"
        "net_wifi.c"
        "net_link.c"
        "bbox.c"
        "bstore.c"
        "web.c"
        "web_page.c"
        "live.c"
        "tables.c"
    EMBED_TXTFILES
        "page/index.html"
        "../../bin/ErrorCodes.h"
        "${K57_TABLES_TXT}"
    INCLUDE_DIRS
        "."
    REQUIRES
        esp_driver_spi
        esp_driver_gpio
        esp_driver_uart
        vfs
        esp_wifi
        esp_netif
        esp_event
        nvs_flash
        lwip
        esp_partition
        esp_timer
        esp_rom
        heap
        esp_http_server
        app_update
        esp_app_format
        mbedtls
)

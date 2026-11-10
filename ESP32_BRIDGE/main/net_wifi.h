/*
 * net_wifi.h
 *
 * Подключение к сети и запасная точка доступа. Перенесено из ESP_INV
 * (firmware/main/net_wifi.c) почти без изменений.
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

#include "esp_err.h"

/* Поднять WiFi и дождаться результата. ESP_OK - и когда подключились к сети,
 * и когда развернули свою точку доступа: в обоих случаях до моста можно
 * дойти по TCP. */
esp_err_t net_wifi_start(void);

bool        net_wifi_is_sta(void);
bool        net_wifi_is_up(void);
int32_t     net_wifi_rssi(void); /* дБм, 0 в режиме точки доступа */
const char *net_wifi_ip(void);
const char *net_wifi_ssid(void); /* сеть, в которой мост: общая или своя точка */

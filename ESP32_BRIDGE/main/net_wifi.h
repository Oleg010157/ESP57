/*
 * net_wifi.h
 *
 * Своя точка доступа K57 - всегда, и роутер вдобавок, если задан (13.11.2026).
 * Как и почему - в net_wifi.c.
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

#include "esp_err.h"

/* Поднять свою точку и, если роутер задан, начать к нему подключаться. Не
 * ждёт роутера: K57 доступна сразу. */
esp_err_t net_wifi_start(void);

bool        net_wifi_is_sta(void);  /* в роутере */
bool        net_wifi_is_up(void);   /* до моста можно дойти - всегда: своя точка */
int32_t     net_wifi_rssi(void);    /* дБм до роутера, 0 - не в роутере */
const char *net_wifi_ip(void);      /* в роутере - его адрес, иначе адрес K57 */
const char *net_wifi_ssid(void);    /* то же для имени сети */

enum { NET_ROUTER_NONE, NET_ROUTER_CONNECTING, NET_ROUTER_UP, NET_ROUTER_DOWN };

typedef struct {
    char apSsid[33], apIp[16];
    int  apClients;
    char routerSsid[33]; /* пусто - роутер не задан */
    int  state;          /* NET_ROUTER_* */
    int  reason;         /* причина последнего разрыва, wifi_err_reason_t */
    char staIp[16];
    int  rssi, ch;
} net_wifi_status_t;

void net_wifi_status(net_wifi_status_t *s);

/* Задать роутер (пустое имя - забыть). NULL - принято и подключаюсь, иначе
 * причина отказа. Хранится в NVS моста. */
const char *net_wifi_router_set(const char *ssid, const char *pass);

typedef struct {
    char ssid[33];
    int  rssi, ch;
    bool open;
} net_wifi_ap_t;

/* Найти сети вокруг (блокирует на 2-3 с). Число найденных или -1. */
int net_wifi_scan(net_wifi_ap_t *out, int max);

/*
 * net_wifi.h
 *
 * Своя точка доступа K57 - всегда, и роутер вдобавок, если задан (13.11.2026);
 * запомнить можно до четырёх (15.11.2026). Как и почему - в net_wifi.c.
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

#include "esp_err.h"

#define NET_ROUTERS_MAX 4

/* Поднять свою точку и, если роутер задан, начать к нему подключаться. Не
 * ждёт роутера: K57 доступна сразу. */
esp_err_t net_wifi_start(void);

bool        net_wifi_is_sta(void);  /* в роутере */
bool        net_wifi_is_up(void);   /* до моста можно дойти - всегда: своя точка */
int32_t     net_wifi_rssi(void);    /* дБм до роутера, 0 - не в роутере */
const char *net_wifi_ip(void);      /* в роутере - его адрес, иначе адрес K57 */
const char *net_wifi_ssid(void);    /* то же для имени сети */
const char *net_wifi_ap_ip(void);   /* адрес в своей точке, 192.168.4.1 */
const char *net_wifi_sta_ip(void);  /* адрес в роутере, пусто - не в нём */
const char *net_wifi_mac(void);     /* MAC в роутере - закрепить по нему адрес */

/* Адрес клиента (как в sin_addr) - из своей точки K57, а не из сети роутера. */
bool net_wifi_is_ap_peer(uint32_t addr_be);

enum { NET_ROUTER_NONE, NET_ROUTER_CONNECTING, NET_ROUTER_UP, NET_ROUTER_DOWN };

typedef struct {
    char apSsid[33], apIp[16];
    int  apClients;
    char routerSsid[33]; /* текущий; пусто - не выбран */
    int  state;          /* NET_ROUTER_* */
    int  reason;         /* причина последнего разрыва, wifi_err_reason_t */
    char staIp[16];
    int  rssi, ch;
    char mac[18];
    int  saved;          /* роутеров запомнено */
} net_wifi_status_t;

void net_wifi_status(net_wifi_status_t *s);

/* Запомнить роутер (или сменить ему пароль) и подключиться к нему сейчас.
 * NULL - принято, иначе причина отказа. Хранится в NVS моста. */
const char *net_wifi_router_add(const char *ssid, const char *pass);

/* Забыть запомненный роутер. */
const char *net_wifi_router_forget(const char *ssid);

/* Имена запомненных роутеров, первым - заданный последним. */
int net_wifi_routers(char out[][33], int max);

typedef struct {
    char ssid[33];
    int  rssi, ch;
    bool open;
} net_wifi_ap_t;

/* Найти сети вокруг (блокирует на 2-3 с). Число найденных или -1. */
int net_wifi_scan(net_wifi_ap_t *out, int max);

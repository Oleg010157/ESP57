/*
 * ssdp.h
 *
 * Мост - значком «K57» в Проводнике Windows, раздел «Сеть» (ssdp.c).
 */
#pragma once

/* После net_wifi_start: MAC нужен для uuid. */
void ssdp_start(void);

/* uuid:... моста - для описания /upnp.xml (web_page.c). */
const char *ssdp_uuid(void);

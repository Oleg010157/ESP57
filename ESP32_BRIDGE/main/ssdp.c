/*
 * ssdp.c
 *
 * Мост - значком «K57» в Проводнике Windows, раздел «Сеть» (16.11.2026).
 * Щелчок - открывается страница моста. Так же Windows показывает принтеры и
 * роутеры: протокол SSDP (UPnP). Нужен там, где имя k57.local не работает -
 * браузер на Windows; на iPhone, Mac и Android 12+ хватает k57.local.
 *
 * Как устроено. Windows, открывая «Сеть», спрашивает широковещательно
 * (M-SEARCH на 239.255.255.250:1900), мост отвечает, где лежит его описание
 * (LOCATION: http://<адрес>/upnp.xml, web_page.c), а в описании -
 * presentationURL, адрес страницы. Раз в минуту мост и сам объявляет о себе
 * (NOTIFY), чтобы значок появлялся, не дожидаясь вопроса. Адрес - той сети,
 * откуда пришёл вопрос: мост в двух сразу.
 *
 * Windows ищет так только в сети с профилем «Частная» и с включённым
 * сетевым обнаружением; в «Общедоступной» значка не будет.
 */
#include <stdio.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

#include "esp_log.h"
#include "lwip/sockets.h"

#include "net_wifi.h"
#include "ssdp.h"

static const char *TAG = "ssdp";

#define SSDP_PORT 1900
#define SSDP_ADDR "239.255.255.250"
#define NOTIFY_S  60

static char s_uuid[48];

const char *ssdp_uuid(void)
{
    return s_uuid;
}

static void join(int s, const char *ifip)
{
    struct ip_mreq m = {0};
    m.imr_multiaddr.s_addr = inet_addr(SSDP_ADDR);
    m.imr_interface.s_addr = inet_addr(ifip);
    setsockopt(s, IPPROTO_IP, IP_ADD_MEMBERSHIP, &m, sizeof m);
}

static void notify(int s, const char *ifip)
{
    if (ifip[0] == 0) {
        return;
    }
    struct in_addr ia = {.s_addr = inet_addr(ifip)};
    setsockopt(s, IPPROTO_IP, IP_MULTICAST_IF, &ia, sizeof ia);

    static const char *const nt[] = {"upnp:rootdevice", "urn:schemas-upnp-org:device:Basic:1"};
    struct sockaddr_in to = {.sin_family = AF_INET, .sin_port = htons(SSDP_PORT)};
    to.sin_addr.s_addr = inet_addr(SSDP_ADDR);
    for (int i = 0; i < 2; i++) {
        char m[400];
        int  n = snprintf(m, sizeof m,
                          "NOTIFY * HTTP/1.1\r\nHOST: " SSDP_ADDR ":1900\r\nCACHE-CONTROL: max-age=1800\r\n"
                          "LOCATION: http://%s/upnp.xml\r\nNT: %s\r\nNTS: ssdp:alive\r\n"
                          "SERVER: ESP-IDF/5.4 UPnP/1.0 K57/1\r\nUSN: %s::%s\r\n\r\n",
                          ifip, nt[i], s_uuid, nt[i]);
        sendto(s, m, n, 0, (struct sockaddr *)&to, sizeof to);
    }
}

static void ssdp_task(void *arg)
{
    (void)arg;
    int s = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    int one = 1;
    setsockopt(s, SOL_SOCKET, SO_REUSEADDR, &one, sizeof one);
    struct sockaddr_in a = {.sin_family = AF_INET, .sin_port = htons(SSDP_PORT), .sin_addr.s_addr = htonl(INADDR_ANY)};
    if (s < 0 || bind(s, (struct sockaddr *)&a, sizeof a) < 0) {
        ESP_LOGW(TAG, "порт %d не открылся - значка в Проводнике не будет", SSDP_PORT);
        vTaskDelete(NULL);
        return;
    }
    struct timeval tv = {.tv_sec = 1};
    setsockopt(s, SOL_SOCKET, SO_RCVTIMEO, &tv, sizeof tv);

    char joinedSta[16] = "";
    join(s, net_wifi_ap_ip());
    int tick = 0;

    for (;;) {
        /* В роутер мост входит позже и может сменить адрес - вступить в
         * группу и на нём, как только он есть. */
        const char *sta = net_wifi_sta_ip();
        if (sta[0] && strcmp(sta, joinedSta) != 0) {
            join(s, sta);
            strlcpy(joinedSta, sta, sizeof joinedSta);
            tick = 0;
        }
        if (--tick <= 0) {
            tick = NOTIFY_S;
            notify(s, net_wifi_ap_ip());
            notify(s, net_wifi_sta_ip());
        }

        char               buf[512];
        struct sockaddr_in from;
        socklen_t          fl = sizeof from;
        int n = recvfrom(s, buf, sizeof buf - 1, 0, (struct sockaddr *)&from, &fl);
        if (n <= 0) {
            continue;
        }
        buf[n] = 0;
        if (strncmp(buf, "M-SEARCH", 8) != 0) {
            continue;
        }
        /* Что ищут: всё, корневые устройства, наш тип или нас по uuid. */
        const char *st = strcasestr(buf, "\nST:");
        if (st == NULL) {
            continue;
        }
        st += 4;
        while (*st == ' ') {
            st++;
        }
        char want[96];
        size_t k = 0;
        while (st[k] && st[k] != '\r' && st[k] != '\n' && k < sizeof want - 1) {
            want[k] = st[k];
            k++;
        }
        want[k] = 0;
        bool all = strcmp(want, "ssdp:all") == 0;
        if (!all && strcmp(want, "upnp:rootdevice") != 0 &&
            strcmp(want, "urn:schemas-upnp-org:device:Basic:1") != 0 && strcmp(want, s_uuid) != 0) {
            continue;
        }
        const char *ip = net_wifi_is_ap_peer(from.sin_addr.s_addr) ? net_wifi_ap_ip() : net_wifi_sta_ip();
        if (ip[0] == 0) {
            continue;
        }
        const char *answer = all ? "upnp:rootdevice" : want;
        char        m[400];
        int         len = snprintf(m, sizeof m,
                                   "HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=1800\r\nEXT:\r\n"
                                   "LOCATION: http://%s/upnp.xml\r\nSERVER: ESP-IDF/5.4 UPnP/1.0 K57/1\r\n"
                                   "ST: %s\r\nUSN: %s%s%s\r\n\r\n",
                                   ip, answer, s_uuid, strcmp(answer, s_uuid) ? "::" : "",
                                   strcmp(answer, s_uuid) ? answer : "");
        sendto(s, m, len, 0, (struct sockaddr *)&from, fl);
    }
}

void ssdp_start(void)
{
    /* uuid - из MAC: один и тот же у моста всегда, у разных мостов разный. */
    unsigned m[6] = {0};
    sscanf(net_wifi_mac(), "%x:%x:%x:%x:%x:%x", &m[0], &m[1], &m[2], &m[3], &m[4], &m[5]);
    snprintf(s_uuid, sizeof s_uuid, "uuid:4b353700-0000-1000-8000-%02x%02x%02x%02x%02x%02x", m[0], m[1], m[2],
             m[3], m[4], m[5]);
    xTaskCreatePinnedToCore(ssdp_task, "ssdp", 3072, NULL, 3, NULL, 0);
}

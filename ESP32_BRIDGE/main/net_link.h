/*
 * net_link.h
 *
 * Сеть моста: TCP-сервер команд и UDP-телеметрия. Формат - net_proto.h.
 *
 * Задача SPI отдаёт сюда кадры прибора (net_link_frame) и после каждой
 * посылки зовёт net_link_packet_end. Здесь кадры раскладываются: 103 и 140 -
 * в UDP, остальное - в TCP. В сокеты пишет отдельная задача на ядре 0: обмен
 * с прибором не должен ждать сеть ни при каких условиях.
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

typedef struct {
    uint32_t clients;   /* подключений по TCP с начала работы */
    uint32_t cmdIn;     /* кадров от ПК принято по TCP */
    uint32_t udpOut;    /* датаграмм телеметрии отправлено */
    uint32_t udpErr;    /* sendto отказал - датаграмма потеряна в мосте */
    uint32_t tcpOut;    /* кадров отправлено по TCP */
    uint32_t outDrop;   /* очередь в сеть полна - потеряно в мосте */
    bool     connected; /* TCP-клиент есть */
    bool     udpPeer;   /* ПК прислал K57U - знаем, куда слать телеметрию */
} net_link_stat_t;

void net_link_start(void);

/* Из задачи SPI. data - байты кадра в порядке CAN, len <= 64. */
void net_link_frame(uint16_t id, const uint8_t *data, uint8_t len);

/* Из задачи SPI после каждой посылки. f28Drop - слово 3 посылки: кадров, не
 * влезших на приборе. */
void net_link_packet_end(uint16_t f28Drop);

void net_link_get_stat(net_link_stat_t *out);

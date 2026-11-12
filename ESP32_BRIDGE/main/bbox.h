/*
 * bbox.h
 *
 * Чёрный ящик: аварийный осциллограф прибора K57 в мосте.
 *
 * Мост всё время держит в ОЗУ кольцо из последних кадров быстрой ленты - около
 * секунды на 5 кГц. Прибор при новой аварии шлёт запись-пуск 0x7E2; мост
 * дописывает ещё 0,2 с и сохраняет кольцо во флеш, в раздел bbox. Хранятся
 * шесть последних записей, переживают выключение. Motor_Control просит список и
 * скачивает запись по TCP.
 *
 * Что пишется в кольцо:
 *   103    - лента, которую пульт включил себе (её тип прибор сообщает в пуске);
 *   0x7E1  - лента для чёрного ящика, когда пульт ленту не просит: прибор шлёт
 *            набор F3_convertor только сюда, не в CAN и не в сеть.
 *
 * Формат записей и обмен с пультом - docs/esp32-spi-protocol-RU.md, раздел
 * «Чёрный ящик». Сторона прибора - INVERTOR_CCS/my_Files/bbox_trig.c.
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

/* Номера записей. 0x7E0..0x7EF моста и прибора: в пульт кадры прибора с
 * этими номерами не пересылаются. */
#define BBOX_ID_FAST     0x7E1 /* прибор -> мост: кадр ленты для чёрного ящика */
#define BBOX_ID_TRIG     0x7E2 /* прибор -> мост: пуск записи */
#define BBOX_ID_LIST     0x7E8 /* пульт -> мост: список записей */
#define BBOX_ID_ENTRY    0x7E9 /* мост -> пульт: одна запись списка */
#define BBOX_ID_LIST_END 0x7EA /* мост -> пульт: конец списка и состояние */
#define BBOX_ID_READ     0x7EB /* пульт -> мост: скачать запись (номер места) */
#define BBOX_ID_DATA     0x7EC /* мост -> пульт: кусок записи */
#define BBOX_ID_READ_END 0x7ED /* мост -> пульт: запись скачана */
#define BBOX_ID_CLEAR    0x7EE /* пульт -> мост: стереть все записи */
#define BBOX_ID_SAVED    0x7EF /* мост -> пульт: сохранена новая запись */

#define BBOX_IS_OWN_ID(id) (((id) & 0xFFF0u) == 0x7E0u)

typedef struct {
    uint32_t ringBytes;  /* размер кольца */
    uint32_t ringUsed;   /* сейчас в кольце, байт */
    uint32_t ringFrames; /* кадров в кольце */
    uint8_t  state;      /* 0 - пишет, 1 - дописывает после аварии, 2 - сохраняет */
    uint32_t triggers;   /* пусков принято */
    uint32_t saved;      /* записей сохранено */
    uint32_t missed;     /* пусков пропущено: шло сохранение прошлой записи */
    uint32_t saveMs;     /* сколько длилось последнее сохранение, мс */
    bool     noFlash;    /* раздела bbox нет - старая таблица разделов */
} bbox_stat_t;

/* До spi_link_start: кольцо берёт память, пока она есть. */
void bbox_start(void);

/* Из задачи SPI, на каждый кадр прибора. Ничего не ждёт. */
void bbox_frame(uint16_t id, const uint8_t *data, uint8_t len);

/* Пуск из консоли моста - для проверки без прибора. */
void bbox_manual(void);

/* Запрос пульта (BBOX_ID_LIST, _READ, _CLEAR) - из задачи TCP. Ответ уходит
 * сразу, через net_link_tcp_send. */
void bbox_request(uint16_t id, const uint8_t *data, uint8_t len);

void bbox_get_stat(bbox_stat_t *out);

/* Запись в списке - для страницы (web.c). */
typedef struct {
    int      slot;
    uint32_t seq;
    uint16_t code;      /* 0xFFFF - пуск вручную */
    uint16_t frameType;
    uint32_t utc;
    uint16_t session;
    uint32_t frames;
    uint32_t trigFrame;
    uint16_t extra[4];
} bbox_entry_t;

/* Сохранённые записи, до max штук. Читает флеш - не из задачи SPI. */
int bbox_list(bbox_entry_t *out, int max);

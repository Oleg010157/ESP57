/*
 * tables.h
 *
 * Таблицы Motor_Control на странице моста: калибровка АЦП (Offsets, kADC),
 * регуляторы, паспорт силовой части, GridTie, Модель, ЦАП и прочие - всё,
 * что лежит в bin/Debug/CanTxFiles (файлы .ini).
 *
 * Те же файлы, что читает окно таблиц Motor_Control (Stepulyov/TxDataFrm.cs):
 * сборка моста склеивает их в один tables.txt (main/CMakeLists.txt) и вшивает
 * в прошивку. Поправили .ini - после пересборки моста страница покажет то же,
 * что Motor_Control.
 *
 * Строка таблицы: «Имя = Мин,Макс,команда,Мод,Цвет,номер». Обмен - как у окна
 * таблиц:
 *
 *   чтение   (знач, 0,    0,    команда) - прибор отвечает ACK: d1 - значение
 *   запись   (знач, 3333, 0,    команда)
 *   флаг М   (знач, 5555, флаг, команда) - у строк с Мод = 1; у нулей АЦП это
 *                                          «откалибровать ноль» (MBOX28.c, 207..)
 *
 * Мост пропускает со страницы ТОЛЬКО команды из этих таблиц и запись - только
 * в пределах Мин..Макс; как и управление - с паролем и при закрытом сеансе
 * Motor_Control (live.h).
 */
#pragma once

#include <stdbool.h>
#include <stdint.h>

/* Разобрать вшитые таблицы. Один раз при старте. */
void tables_init(void);

/* Из задачи SPI: ответы прибора (кадр 1) на команды таблиц - запомнить. */
void tables_frame(uint16_t id, const uint8_t *data, uint8_t len);

/* Команда таблицы со страницы. op: 'r' чтение, 'w' запись, 'm' флаг М.
 * NULL - отправлено, иначе причина отказа. */
const char *tables_cmd(uint16_t cmd, char op, int value, int flag);

/* Ответы прибора: по команде - значение, флаг, мс с ответа. Перебор для JSON:
 * cb зовётся на каждую команду, на которую ответ уже был. */
void tables_acks(void (*cb)(uint16_t cmd, int16_t val, uint16_t flag, uint32_t age, void *ctx), void *ctx);

/* Сам текст таблиц - для страницы (GET /tables.txt). */
const char *tables_text(int *len);

/* Таблицы, присланные с ПК (Motor_Control): хранятся во флеше моста и берутся
 * вместо вшитых - пересобирать мост ради правки .ini не нужно. Формат текста -
 * тот же, что склеивает сборка (main/CMakeLists.txt): «### имя» и файл.
 * begin стирает место, write дописывает, end закрывает и сразу берёт в работу
 * (false - не легли). clear - назад к вшитым. */
bool tables_store_begin(uint32_t len, uint32_t utc);
bool tables_store_write(const void *data, uint32_t len);
bool tables_store_end(void);
void tables_store_clear(void);

/* true - сейчас в работе присланные с ПК; utc - когда их прислали. */
bool tables_source(uint32_t *utc);

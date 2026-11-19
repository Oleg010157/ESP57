/*
 * nv_store.h
 *
 * История аварий в энергонезависимой памяти на шине I2C. Разбор - в nv_store.c.
 *
 *  Created on: 04.11.2026
 *      Author: Oleg Pavlovsky
 */

#ifndef NV_STORE_H_
#define NV_STORE_H_

#include <stdbool.h>
#include <stdint.h>

// Прочитать историю и начать новую сессию. Зовётся один раз при старте, до
// checkEE(), и может занять десятки миллисекунд.
void NvStore_init(void);

// Записать в память то, что накопилось. Зовётся из фона, не ждёт: один шаг за
// проход.
void NvStore_background(void);

// Поставить на запись всю таблицу истории: после ClearErrorTable().
void NvStore_requestClear(void);

// Поставить на запись счётчики энергии (energy.h): маска по ENERGY_*. Можно и
// из прерывания.
void NvStore_requestEnergy(uint16_t mask);

// Номер сессии взят из памяти. Нет - память не ответила при старте, номер
// временный (1), и его даст мост: bridge_store.h.
bool NvStore_sessionOk(void);

#endif /* NV_STORE_H_ */

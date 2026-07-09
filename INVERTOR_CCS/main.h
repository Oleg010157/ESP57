/*
 * main.h
 *
 *  Created on: 7 окт. 2024 г.
 *      Author: Oleg Pavlovsky
 */

#ifndef MAIN_H_
#define MAIN_H_

void REF_init(REFs *v);
void STOP(void);
void PUSK(void);
void Reset_stop(motor *v); // сброс по кнопке СТОП
void PUSK_Init(motor *v);  // сброс по кнопке ПУСК


#endif /* MAIN_H_ */

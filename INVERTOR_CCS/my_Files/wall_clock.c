/*
 * wall_clock.c
 *
 * Дата и время для журнала аварий. Смысл - в wall_clock.h.
 *
 * Из окна Watch:
 *   wallTime - текущее UTC, секунды с 1970; 0 - время неизвестно;
 *   wallSets - сколько раз пульт присылал время.
 */

#include "wall_clock.h"

volatile uint32_t wallTime = 0;
volatile uint16_t wallSets = 0;

// Команда приходит и из прерывания MCAN, а тик - из фона: запрет прерываний
// на время записи, чтобы тик не прибавил секунду к половине нового значения.
void WallClock_set(uint32_t utc)
{
    uint16_t st = __disable_interrupts();
    wallTime = utc;
    wallSets++;
    __restore_interrupts(st);
}

void WallClock_tick1s(void)
{
    uint16_t st = __disable_interrupts();
    if (wallTime != 0UL)
    {
        wallTime++;
    }
    __restore_interrupts(st);
}

uint32_t WallClock_now(void)
{
    return wallTime;
}

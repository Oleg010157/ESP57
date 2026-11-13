/*
 * energy.c
 *
 * Счётчики энергии. Что и зачем - energy.h.
 */

#include <stdint.h>

#include "energy.h"
#include "nv_store.h"   // NvStore_requestEnergy()
#include "KSP.h"        // motors[] - признак "ключи открыты"
#include "cla_shared.h" // pSec
#include "myDCAN.h"     // SendAck()

#define ENERGY_DT_S     0.05f // шаг, с: таймер фона
#define ENERGY_J_PER_WH 3600.0f
#define ENERGY_SAVE_S   600U  // раз в 10 минут

// В RAMGS2, а не в .bss: RAMLS5 заполнена почти до конца (как в wifi_link.c).
#pragma DATA_SECTION(energyWh, "ramgs2");
volatile uint32_t energyWh[ENERGY_N];

static float    accJ[2];    // доли ватт-часа, Дж: 0 - отдано, 1 - принято
static uint16_t dirty;      // какие счётчики изменились с прошлой записи
static uint16_t saveSec;
static uint16_t keysWas;

static void save(void)
{
    if (dirty != 0U)
    {
        NvStore_requestEnergy(dirty);
        dirty = 0U;
    }
}

void Energy_tick50ms(void)
{
    uint16_t keys = motors[0]->flag.bit.pwm_On;

    if (keys != 0U)
    {
        float    p = pSec;
        uint16_t k = (p >= 0.0f) ? 0U : 1U;

        accJ[k] += ((p >= 0.0f) ? p : -p) * ENERGY_DT_S;
        if (accJ[k] >= ENERGY_J_PER_WH)
        {
            uint32_t wh = (uint32_t)(accJ[k] / ENERGY_J_PER_WH);
            uint16_t st;

            accJ[k] -= (float)wh * ENERGY_J_PER_WH;

            // Сброс приходит командой, то есть из прерывания MCAN: прибавлять,
            // пока он не может вклиниться.
            st = __disable_interrupts();
            energyWh[ENERGY_OUT + k] += wh;
            energyWh[ENERGY_OUT_TRIP + k] += wh;
            dirty |= (uint16_t)((1U << (ENERGY_OUT + k)) | (1U << (ENERGY_OUT_TRIP + k)));
            __restore_interrupts(st);
        }
    }

    // Ключи закрылись - энергия больше не идёт, самое время записать.
    if ((keysWas != 0U) && (keys == 0U))
        save();
    keysWas = keys;
}

void Energy_tick1s(void)
{
    if (++saveSec >= ENERGY_SAVE_S)
    {
        saveSec = 0U;
        save();
    }
}

// Из прерывания MCAN (cmd_MBOX28) - прерывания здесь уже запрещены.
void Energy_cmd(uint16_t d1, uint16_t d2)
{
    uint16_t k;

    if ((d1 == 1U) && (d2 == 3333U))
    {
        energyWh[ENERGY_OUT_TRIP] = 0UL;
        energyWh[ENERGY_IN_TRIP]  = 0UL;
        NvStore_requestEnergy((1U << ENERGY_OUT_TRIP) | (1U << ENERGY_IN_TRIP));
    }

    for (k = 0U; k < ENERGY_N; k++)
        SendAck((int)k, (int)(energyWh[k] & 0xFFFFUL), (int)(energyWh[k] >> 16), ENERGY_CMD);
}

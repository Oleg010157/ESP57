/*
 * bbox_trig.c
 *
 * Чёрный ящик, сторона прибора. Смысл - в bbox_trig.h.
 *
 * Из окна Watch:
 *   bboxSent    - пусков отправлено мосту;
 *   bboxSkipped - аварий, пришедших, пока прошлый пуск ещё ждал фона.
 *                 Мост сам собирает в одну запись аварии, пришедшие следом
 *                 за первой, поэтому такая потеря ничего не стоит.
 */

#include "board.h"
#include "KSP.h" // Errors.h, ErrorCodes.h
#include "myDCAN.h"
#include "1ph_Sin.h"
#include "wall_clock.h"
#include "wifi_link.h"
#include "bbox_trig.h"

volatile uint16_t bboxSent    = 0;
volatile uint16_t bboxSkipped = 0;

static volatile uint16_t bbPending = 0;
static uint16_t          bbWords[6];

uint16_t BBox_frameType(void)
{
    if (canaKadr.bit.F1_convertor) return 1U;
    if (canaKadr.bit.F1_invertor)  return 2U;
    if (canaKadr.bit.F3_invertor)  return 3U;
    if (canaKadr.bit.F3_convertor) return 4U;
    if (canaKadr.bit.F3_grid)      return 6U;
    return 0U;
}

static void bb_arm(uint16_t code)
{
    uint16_t st = __disable_interrupts();

    if (bbPending != 0U)
    {
        bboxSkipped++;
    }
    else
    {
        uint32_t t = WallClock_now();

        bbWords[0] = code;
        bbWords[1] = (uint16_t)(t & 0xFFFFUL);
        bbWords[2] = (uint16_t)(t >> 16);
        bbWords[3] = CurrentSessionId;
        bbWords[4] = BBox_frameType();
        bbWords[5] = sineTick; // по нему пульт найдёт момент аварии в ленте
        bbPending  = 1U;
    }
    __restore_interrupts(st);
}

void BBox_onEvent(uint16_t code)
{
    if ((code >= CANA_Ewarn && code <= CANA_LEC3) || (code >= 50U && code <= 59U))
    {
        return;
    }
    bb_arm(code);
}

void BBox_manual(void)
{
    bb_arm(BBOX_CODE_HAND);
}

void BBox_background(void)
{
    if (bbPending == 0U)
    {
        return;
    }
    WifiLink_push(BBOX_TRIG_ID, bbWords, 6U);
    bboxSent++;
    bbPending = 0U;
}

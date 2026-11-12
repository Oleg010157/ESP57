/*
 * live.c
 *
 * Живое состояние прибора для страницы. Что и зачем - в live.h.
 *
 * Кадр 132 (Send_mbox(4, ...) в приборе): четыре слова d1..d4, номер записи -
 * в d4. 32010 - пара кодов: d1 - код, d2 и d3 - слова ErrBuff для кода и
 * следующего. 32013 - время кода d1: d2 младшее, d3 старшее слово UTC. 32011 -
 * конец списка. 32012 - лампа, d3 - сколько секунд ей гореть. Разбор тот же,
 * что у Motor_Control (Oleg/Kadr60.cs, ProcessFrame132).
 *
 * Список приходит только ненулевыми парами, поэтому собирается заново в
 * s_next и подменяет готовый целиком по записи конца: иначе сброшенная в
 * приборе ошибка висела бы на странице вечно.
 */
#include <string.h>

#include "freertos/FreeRTOS.h"

#include "esp_timer.h"

#include "live.h"
#include "net_link.h"
#include "spi_link.h"

#define CMD_WATCH       344   /* прибору: «только смотреть», MBOX28.c */
#define WATCH_LAMP      1     /* d1: лампа */
#define WATCH_ERRORS    2     /* d1: список ошибок */
#define PAGE_ACTIVE_MS  10000 /* браузер спрашивал не раньше - страница открыта */
#define FRESH_MS        1500  /* кадр 140 не старше - прибор на связи */

static portMUX_TYPE s_mux = portMUX_INITIALIZER_UNLOCKED;

static uint16_t s_w[32];
static int64_t  s_t140, s_tLamp, s_tErr;
static uint16_t s_lamp;

static uint16_t s_err[LIVE_ERR_CODES];
static uint16_t s_next[LIVE_ERR_CODES];
static uint32_t s_nextUtc[LIVE_ERR_CODES];
static uint32_t s_utc[LIVE_ERR_CODES];
static bool     s_haveErr;

static volatile int64_t s_touch;
static bool             s_watching;
static int              s_ticks;

static int64_t now_us(void)
{
    return esp_timer_get_time();
}

static uint16_t le16(const uint8_t *p)
{
    return (uint16_t)(p[0] | (p[1] << 8));
}

void live_frame(uint16_t id, const uint8_t *data, uint8_t len)
{
    if (id == 140 && len >= 64) {
        portENTER_CRITICAL(&s_mux);
        for (int i = 0; i < 32; i++) {
            s_w[i] = le16(data + 2 * i);
        }
        s_t140 = now_us();
        portEXIT_CRITICAL(&s_mux);
        return;
    }
    if (id != 132 || len < 8) {
        return;
    }
    uint16_t d1 = le16(data), d2 = le16(data + 2), d3 = le16(data + 4), d4 = le16(data + 6);

    portENTER_CRITICAL(&s_mux);
    switch (d4) {
    case 32010:
        if (d1 + 1 < LIVE_ERR_CODES) {
            s_next[d1]     = d2;
            s_next[d1 + 1] = d3;
        }
        break;
    case 32013:
        if (d1 < LIVE_ERR_CODES) {
            s_nextUtc[d1] = (uint32_t)d2 | ((uint32_t)d3 << 16);
        }
        break;
    case 32011:
        memcpy(s_err, s_next, sizeof s_err);
        memcpy(s_utc, s_nextUtc, sizeof s_utc);
        memset(s_next, 0, sizeof s_next);
        memset(s_nextUtc, 0, sizeof s_nextUtc);
        s_tErr     = now_us();
        s_haveErr  = true;
        break;
    case 32012:
        s_lamp  = d3;
        s_tLamp = now_us();
        break;
    default:
        break;
    }
    portEXIT_CRITICAL(&s_mux);
}

void live_touch(void)
{
    s_touch = now_us();
}

static bool page_active(void)
{
    return s_touch != 0 && now_us() - s_touch < (int64_t)PAGE_ACTIVE_MS * 1000;
}

/* Сеанс Motor_Control открыт: свежий кадр 140 и в нём sbit.connect. */
static bool session_open(void)
{
    bool open;
    portENTER_CRITICAL(&s_mux);
    open = s_t140 != 0 && now_us() - s_t140 < (int64_t)FRESH_MS * 1000 && (s_w[2] & 1u) != 0;
    portEXIT_CRITICAL(&s_mux);
    return open;
}

void live_tick1s(void)
{
    /* Сеанс Motor_Control открыт - кадры идут по нему; страница только смотрит. */
    s_watching = page_active() && !session_open();
    if (!s_watching) {
        return;
    }
    uint16_t req = WATCH_LAMP;
    if (++s_ticks % 3 == 0) {
        req |= WATCH_ERRORS; /* список - не каждую секунду: это 50 кадров */
    }
    spi_link_send_cmd(CMD_WATCH, req, 0, 0);
}

static uint32_t age_ms(int64_t t)
{
    return t == 0 ? UINT32_MAX : (uint32_t)((now_us() - t) / 1000);
}

void live_get(live_state_t *out)
{
    net_link_stat_t n;
    net_link_get_stat(&n);

    portENTER_CRITICAL(&s_mux);
    memcpy(out->w, s_w, sizeof s_w);
    out->age     = age_ms(s_t140);
    out->lamp    = s_lamp;
    out->lampAge = age_ms(s_tLamp);
    out->errAge  = s_haveErr ? age_ms(s_tErr) : UINT32_MAX;
    portEXIT_CRITICAL(&s_mux);
    out->pult     = n.connected;
    out->session  = session_open();
    out->watching = s_watching;
}

bool live_errors(uint16_t word[LIVE_ERR_CODES], uint32_t utc[LIVE_ERR_CODES])
{
    bool have;
    portENTER_CRITICAL(&s_mux);
    memcpy(word, s_err, sizeof s_err);
    memcpy(utc, s_utc, sizeof s_utc);
    have = s_haveErr;
    portEXIT_CRITICAL(&s_mux);
    return have;
}

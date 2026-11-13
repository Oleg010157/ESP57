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
#include "ehist.h"
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

/* Подтверждённое прибором (ACK, кадр 1: cmd, d1, d2, d3). */
static uint16_t s_mode, s_en, s_iset, s_uref, s_freq;
static int64_t  s_tMode, s_tEn, s_tIset, s_tUref, s_tFreq;
static uint32_t s_energy[4];
static int64_t  s_tEnergy;

#define CMD_ENERGY 345 /* energy.h прибора: ответ - ACK на каждый счётчик */
#define ENERGY_EVERY_S 60 /* без страницы - так часто спрашивать для истории */
static int s_energyTick;

static volatile int64_t s_touch;
static bool             s_watching;
static int              s_ticks;
static int64_t          s_utcBase;   /* UTC минус время работы моста, с; 0 - не знаем */
static int              s_clockTick; /* секунд до следующей 337 */

#define CMD_WALL_CLOCK 337 /* wall_clock.h прибора */
#define CLOCK_EVERY_S  60

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
    if (id == 1 && len >= 8) {
        uint16_t c = le16(data), v = le16(data + 2);
        int64_t  t = now_us();
        portENTER_CRITICAL(&s_mux);
        switch (c) {
        case 630: s_mode = v; s_tMode = t; break;
        case 635: s_en   = v; s_tEn   = t; break;
        case 632: s_iset = v; s_tIset = t; break;
        case 633: s_uref = v; s_tUref = t; break;
        case 624: s_freq = v; s_tFreq = t; break;
        case CMD_ENERGY: /* d1 - номер счётчика, d2/d3 - младшее и старшее слово */
            if (v < 4) {
                s_energy[v] = (uint32_t)le16(data + 4) | ((uint32_t)le16(data + 6) << 16);
                s_tEnergy   = t;
            }
            break;
        default: break;
        }
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

void live_set_time(uint32_t utc)
{
    /* Раньше 2024 года - не время, а ноль или мусор. */
    if (utc < 1704067200u) {
        return;
    }
    bool first = s_utcBase == 0;
    s_utcBase  = (int64_t)utc - now_us() / 1000000;
    if (first) {
        s_clockTick = 0; /* узнали только что - прибору сразу */
    }
}

uint32_t live_utc(void)
{
    return s_utcBase == 0 ? 0 : (uint32_t)(s_utcBase + now_us() / 1000000);
}

void live_push_time(void)
{
    uint32_t t = live_utc();
    /* В сеансе время прибору даёт сам Motor_Control. */
    if (t != 0 && !session_open()) {
        spi_link_send_cmd(CMD_WALL_CLOCK, (uint16_t)t, (uint16_t)(t >> 16), 0);
    }
}

void live_tick1s(void)
{
    /* Раз в минуту, а не однажды: прибор, перезапущенный (прошивка, питание),
     * забывает время, а сам об этом не скажет. Команда - одна в минуту. */
    if (--s_clockTick <= 0) {
        s_clockTick = CLOCK_EVERY_S;
        live_push_time();
    }

    /* История энергии (ehist.h): показания «всего» раз в час. Спрашивать
     * прибор нужно и без страницы - раз в минуту; страница спрашивает сама,
     * раз в 3 с. Чтение безвредно и в сеансе Motor_Control: тот ответ 345
     * понимает и показывает в окне «Счётчики энергии». */
    if (--s_energyTick <= 0) {
        s_energyTick = ENERGY_EVERY_S;
        if (!s_watching) {
            spi_link_send_cmd(CMD_ENERGY, 0, 0, 0);
        }
        portENTER_CRITICAL(&s_mux);
        int64_t  te  = s_tEnergy;
        uint32_t eo  = s_energy[0], ei = s_energy[1];
        portEXIT_CRITICAL(&s_mux);
        uint32_t utc = live_utc();
        if (utc != 0 && te != 0 && now_us() - te < (int64_t)90 * 1000000) {
            ehist_add(utc / 3600, eo, ei);
        }
    }

    /* Сеанс Motor_Control открыт - кадры идут по нему; страница только смотрит. */
    s_watching = page_active() && !session_open();
    if (!s_watching) {
        return;
    }
    uint16_t req = WATCH_LAMP;
    if (++s_ticks % 3 == 0) {
        req |= WATCH_ERRORS; /* список - не каждую секунду: это 50 кадров */
        /* И что задано в приборе - для вкладки «Управление». Без 3333 это
         * только чтение: прибор отвечает текущим значением (MBOX28.c). */
        spi_link_send_cmd(630, 0, 0, 0);
        spi_link_send_cmd(635, 0, 0, 0);
        spi_link_send_cmd(632, 0, 0, 0);
        spi_link_send_cmd(633, 0, 0, 0);
        spi_link_send_cmd(624, 0, 0, 0);
        spi_link_send_cmd(CMD_ENERGY, 0, 0, 0);
    }
    spi_link_send_cmd(CMD_WATCH, req, 0, 0);
}

bool live_session_open(void)
{
    return session_open();
}

void live_get_set(live_set_t *out)
{
    portENTER_CRITICAL(&s_mux);
    out->mode   = s_mode;
    out->gridEn = s_en;
    out->iset   = s_iset;
    out->uref   = s_uref;
    out->freq   = s_freq;
    int64_t tm = s_tMode, te = s_tEn, ti = s_tIset, tu = s_tUref, tf = s_tFreq, tw = s_tEnergy;
    memcpy(out->energy, s_energy, sizeof out->energy);
    portEXIT_CRITICAL(&s_mux);
    out->modeAge = tm == 0 ? UINT32_MAX : (uint32_t)((now_us() - tm) / 1000);
    out->enAge   = te == 0 ? UINT32_MAX : (uint32_t)((now_us() - te) / 1000);
    out->isetAge = ti == 0 ? UINT32_MAX : (uint32_t)((now_us() - ti) / 1000);
    out->urefAge = tu == 0 ? UINT32_MAX : (uint32_t)((now_us() - tu) / 1000);
    out->freqAge = tf == 0 ? UINT32_MAX : (uint32_t)((now_us() - tf) / 1000);
    out->energyAge = tw == 0 ? UINT32_MAX : (uint32_t)((now_us() - tw) / 1000);
}

/*---------------------------------------------------------------------------
 * Управление со страницы
 *
 * Команды - те же и в том же порядке, что у Motor_Control:
 *
 *   mode    - 630 с 3333: 0 АИН, 1 слежение, 2 выход в сеть (прибор сам
 *             откажет в 2 без разрешения 635);
 *   gridEn  - 635 с 3333: разрешение выхода в сеть, 0/1;
 *   iset    - 632 с 3333, 0.01 А;  uref - 633 с 3333, 0.1 В;
 *   freq    - 624 с 3333, Гц - частота своего синуса в АИН;
 *   power   - ключи АИН-1, кнопка «Вкл/Выкл» панели (InvPower_Click в
 *             GridTieUi.cs): 1 - 10 «снять запрет» и 12 «ключи»; 0 - 10
 *             «запретить» и Uref, Iset в ноль, чтобы следующее включение не
 *             началось с полной уставки.
 *
 * Пределы - здесь, а не только в странице: страницу можно и обойти.
 *---------------------------------------------------------------------------*/
#define ISET_MAX 2500 /* 25 А - упор движка Load1 в Motor_Control */
#define UREF_MAX 2500 /* 250 В */
#define FREQ_MIN 40   /* Гц - шкала поля F Hz в Motor_Control */
#define FREQ_MAX 70

const char *live_control(const char *what, int value)
{
    if (session_open()) {
        return "открыт сеанс Motor_Control - управляет он";
    }
    portENTER_CRITICAL(&s_mux);
    bool fresh = s_t140 != 0 && now_us() - s_t140 < (int64_t)3000 * 1000;
    portEXIT_CRITICAL(&s_mux);
    if (!fresh) {
        return "прибор не на связи";
    }

    bool ok = true;
    /* Пуск/Стоп (4/5) нарочно нет: они от стенда с двумя двигателями и
     * дублируют ключи «power» (12.11.2026). */
    if (strcmp(what, "mode") == 0) {
        if (value < 0 || value > 2) {
            return "нет такого режима";
        }
        ok = spi_link_send_cmd(630, (uint16_t)value, 3333, 0);
    } else if (strcmp(what, "gridEn") == 0) {
        ok = spi_link_send_cmd(635, value ? 1 : 0, 3333, 0);
    } else if (strcmp(what, "iset") == 0) {
        if (value < 0 || value > ISET_MAX) {
            return "Iset вне 0..25 А";
        }
        ok = spi_link_send_cmd(632, (uint16_t)value, 3333, 0);
    } else if (strcmp(what, "power") == 0) {
        if (value) {
            ok = spi_link_send_cmd(10, 0, 3333, 0) && spi_link_send_cmd(12, 0, 0, 0);
        } else {
            ok = spi_link_send_cmd(10, 1, 3333, 0) && spi_link_send_cmd(633, 0, 3333, 0) &&
                 spi_link_send_cmd(632, 0, 3333, 0);
        }
    } else if (strcmp(what, "freq") == 0) {
        if (value < FREQ_MIN || value > FREQ_MAX) {
            return "частота вне 40..70 Гц";
        }
        ok = spi_link_send_cmd(624, (uint16_t)value, 3333, 0);
    } else if (strcmp(what, "etrip") == 0) {
        /* Обнулить «с последнего сброса»; «всего» прибор не сбрасывает никогда. */
        ok = spi_link_send_cmd(CMD_ENERGY, 1, 3333, 0);
    } else if (strcmp(what, "uref") == 0) {
        if (value < 0 || value > UREF_MAX) {
            return "Uref вне 0..250 В";
        }
        ok = spi_link_send_cmd(633, (uint16_t)value, 3333, 0);
    } else {
        return "нет такого действия";
    }
    return ok ? NULL : "очередь к прибору полна";
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

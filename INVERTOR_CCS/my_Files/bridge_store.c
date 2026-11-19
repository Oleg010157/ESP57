/*
 * bridge_store.c
 *
 * Журнал и копия настройки в мосте ESP32. Смысл - в bridge_store.h.
 *
 * Из окна Watch:
 *   bstEvents    - событий отдано мосту;
 *   bstEvLost    - событий не влезло в очередь (8 мест) до прохода фона;
 *   bstCopies    - копий настройки отдано;
 *   bstRestore   - итог последнего восстановления, CALIB_RESTORE_* (0 - принято);
 *                  0xFFFF - восстановления не было;
 *   bstSesBridge - номер сессии, данный мостом; 0 - не давал (память цела или
 *                  мост не ответил).
 */

#include "board.h"
#include "KSP.h" // Errors.h: CurrentSessionId, ErrHistory
#include "calib_store.h"
#include "nv_store.h"
#include "wall_clock.h"
#include "wifi_link.h"
#include "myDCAN.h" // SendAck
#include "bridge_store.h"

// Буферы - в RAMGS0: в LS5, куда идёт .bss, места нет (0x6A слов).
#pragma DATA_SECTION(evBuf, "ramgs0");
#pragma DATA_SECTION(blob, "ramgs0");
#pragma DATA_SECTION(rxWords, "ramgs0");

volatile uint16_t bstEvents  = 0;
volatile uint16_t bstEvLost  = 0;
volatile uint16_t bstCopies  = 0;
volatile uint16_t bstRestore = 0xFFFFU;
volatile uint16_t bstSesBridge = 0;

//---------------------------------------------------------------------------
// События: из прерывания - в кольцо, из фона - мосту.
//
// Номер сессии ставится при отправке, а не в кольце: без памяти он до ответа
// моста временный, и события до тех пор ждут в кольце (sesSettled).
//---------------------------------------------------------------------------
#define BST_EV_SLOTS 8U

static uint16_t          evBuf[BST_EV_SLOTS][3]; // код, UTC мл., UTC ст.
static volatile uint16_t evHead = 0, evTail = 0;

void BridgeStore_onEvent(uint16_t code)
{
    uint16_t st   = __disable_interrupts();
    uint16_t next = (uint16_t)((evHead + 1U) % BST_EV_SLOTS);

    if (next == evTail)
    {
        bstEvLost++;
    }
    else
    {
        uint32_t t = WallClock_now();

        if (t == 0UL)
        {
            t = BST_UTC_UPTIME | (WallClock_uptime() & ~BST_UTC_UPTIME);
        }
        evBuf[evHead][0] = code;
        evBuf[evHead][1] = (uint16_t)(t & 0xFFFFUL);
        evBuf[evHead][2] = (uint16_t)(t >> 16);
        evHead           = next;
    }
    __restore_interrupts(st);
}

//---------------------------------------------------------------------------
// Сессия и время задним числом - bridge_store.h
//---------------------------------------------------------------------------
static volatile uint16_t helloDue    = 0;      // послать 0x7D4
static uint16_t          helloSent   = 0;
static uint32_t          helloAt     = 0;      // секунды от включения, когда послан
static volatile uint16_t sesOffer    = 0;      // номер из команды 340, 0 - нет
static uint16_t          sesSettled  = 0;      // события можно отдавать
static uint16_t          markedSes   = 0;      // сессия событий без времени
static uint16_t          markedSent  = 0;      // такие уходили мосту
static uint16_t          anchorSent  = 0;

static void send_hello(void)
{
    uint16_t w[2];

    w[0] = CurrentSessionId;
    w[1] = (uint16_t)((NvStore_sessionOk() ? 0U : BST_HELLO_NOMEM) |
                      ((bstSesBridge != 0U) ? BST_HELLO_BRIDGE : 0U));
    WifiLink_push(BST_ID_HELLO, w, 2U);
}

// Номер от моста - вместо временного. История в ОЗУ уже помечена временным
// номером: переносим её на новый, иначе каждый код посчитался бы дважды.
static void take_session(uint16_t ses)
{
    uint16_t st  = __disable_interrupts(); // RegisterSessionEvent - и из прерывания
    uint16_t old = CurrentSessionId;
    uint16_t i;

    for (i = 0; i < ERR_BUFF_SIZE; i++)
    {
        if (ErrHistory[i].LastSessionId == old)
        {
            ErrHistory[i].LastSessionId = ses;
        }
    }
    CurrentSessionId = ses;
    __restore_interrupts(st);
    bstSesBridge = ses;
}

static void send_event(const uint16_t *e)
{
    uint16_t w[4];
    uint32_t t = ((uint32_t)e[2] << 16) | e[1];

    if ((t & BST_UTC_UPTIME) != 0UL)
    {
        uint32_t boot = WallClock_bootUtc();

        if (boot != 0UL)
        {
            t = boot + (t & ~BST_UTC_UPTIME); // время уже пришло - пересчитаем сами
        }
        else if (!markedSent)
        {
            markedSent = 1U;
            markedSes  = CurrentSessionId;
        }
    }
    w[0] = e[0];
    w[1] = (uint16_t)(t & 0xFFFFUL);
    w[2] = (uint16_t)(t >> 16);
    w[3] = CurrentSessionId;
    WifiLink_push(BST_ID_EVENT, w, 4U);
    bstEvents++;
}

//---------------------------------------------------------------------------
// Копия настройки
//---------------------------------------------------------------------------
static volatile uint16_t sendWhy  = 0xFFFFU; // причина отправки, 0xFFFF - не надо
static uint16_t          bootSent = 0;
static uint16_t          lastSaveCount = 0;

static CalibBlob blob; // не на стеке фона: 47 слов

static void send_copy(uint16_t why)
{
    uint16_t st = calib_peek(&blob);
    uint32_t u0 = SysCtl_getDeviceUID0();
    uint32_t u1 = SysCtl_getDeviceUID1();
    uint32_t t  = WallClock_now();
    uint16_t n  = (uint16_t)sizeof(CalibBlob);
    uint16_t hdr[12];
    uint16_t sum = 0;
    uint16_t i;

    // Простая сумма всех слов записи: мост раскладки CalibBlob не знает и
    // её собственную сумму проверить не может, а посылка к мосту CRC не
    // закрыта (spi_link.h). По этой сумме он поймёт, что собрал запись целой.
    for (i = 0; i < n; i++)
    {
        sum = (uint16_t)(sum + ((const uint16_t *)&blob)[i]);
    }

    hdr[0]  = 1U; // формат
    hdr[1]  = n;
    hdr[2]  = st;
    hdr[3]  = why;
    hdr[4]  = (uint16_t)(u0 & 0xFFFFUL);
    hdr[5]  = (uint16_t)(u0 >> 16);
    hdr[6]  = (uint16_t)(u1 & 0xFFFFUL);
    hdr[7]  = (uint16_t)(u1 >> 16);
    hdr[8]  = (uint16_t)(t & 0xFFFFUL);
    hdr[9]  = (uint16_t)(t >> 16);
    hdr[10] = CALIB_VERSION;
    hdr[11] = sum;
    WifiLink_push(BST_ID_CAL_HDR, hdr, 12U);

    // Пустую или чужую запись не шлём: мост держит прежнюю копию.
    if (st == CALIB_OK)
    {
        const uint16_t *w = (const uint16_t *)&blob;
        uint16_t        piece[31];
        uint16_t        off, k;

        for (off = 0; off < n; off += 30U)
        {
            k        = (uint16_t)((n - off) < 30U ? (n - off) : 30U);
            piece[0] = off;
            for (i = 0; i < k; i++)
            {
                piece[1U + i] = w[off + i];
            }
            WifiLink_push(BST_ID_CAL_DATA, piece, (uint16_t)(1U + k));
        }
    }
    bstCopies++;
}

//---------------------------------------------------------------------------
// Восстановление: слова копятся из команд 342, принимает фон по команде 343.
//---------------------------------------------------------------------------
#define BST_RX_WORDS 64U

static uint16_t          rxWords[BST_RX_WORDS];
static volatile uint16_t applyLen = 0; // 0 - заявки нет

void BridgeStore_cmd(uint16_t cmd, uint16_t d1, uint16_t d2, uint16_t d3)
{
    switch (cmd)
    {
    // Номер сессии от моста. Без ACK: шлёт его мост, ответ ему не нужен.
    case BST_CMD_SESSION:
        sesOffer = d1;
        break;

    // Копию просит и мост, поднявшись: заодно скажем ему свою сессию.
    case BST_CMD_SEND:
        sendWhy  = BST_WHY_ASKED;
        helloDue = 1U;
        SendAck(d1, d2, d3, cmd);
        break;

    case BST_CMD_WORDS:
        if (d3 + 1U < BST_RX_WORDS)
        {
            rxWords[d3]      = d1;
            rxWords[d3 + 1U] = d2;
        }
        SendAck(d1, d2, d3, cmd);
        break;

    case BST_CMD_APPLY:
        // Раздача и флеш - только из фона: команда может прийти из
        // прерывания MCAN. Ответ даст фон, когда сделает.
        applyLen = (d1 != 0U) ? d1 : 0xFFFFU;
        break;

    default:
        break;
    }
}

void BridgeStore_background(void)
{
    // Копия и сессия: при включении - как только мост ответил (раньше слать
    // некуда); копия ещё после каждого сохранения и по просьбе.
    if (!bootSent && wifiLink.rxOk != 0UL)
    {
        bootSent = 1U;
        sendWhy  = BST_WHY_BOOT;
        helloDue = 1U;
    }

    // Сессия
    if (helloDue)
    {
        helloDue = 0U;
        send_hello();
        if (!helloSent)
        {
            helloSent = 1U;
            helloAt   = WallClock_uptime();
        }
    }
    if (sesOffer != 0U)
    {
        uint16_t ses = sesOffer;
        sesOffer     = 0U;
        // Номер из памяти главнее: мост даёт свой, только когда её нет. Дал
        // уже - второй раз не берём, номер сессии не скачет.
        if (!NvStore_sessionOk() && bstSesBridge == 0U)
        {
            take_session(ses);
        }
    }
    if (!sesSettled &&
        (NvStore_sessionOk() || bstSesBridge != 0U ||
         (helloSent && (WallClock_uptime() - helloAt) >= BST_SES_WAIT_S)))
    {
        sesSettled = 1U;
    }

    // События журнала
    while (sesSettled && evTail != evHead)
    {
        send_event(evBuf[evTail]);
        evTail = (uint16_t)((evTail + 1U) % BST_EV_SLOTS);
    }

    // Время пришло, а мосту уже ушли события без него: привязка, по ней он
    // их пересчитает. Одна на включение.
    if (markedSent && !anchorSent)
    {
        uint32_t boot = WallClock_bootUtc();

        if (boot != 0UL)
        {
            uint16_t w[3];

            w[0] = markedSes;
            w[1] = (uint16_t)(boot & 0xFFFFUL);
            w[2] = (uint16_t)(boot >> 16);
            WifiLink_push(BST_ID_TIME, w, 3U);
            anchorSent = 1U;
        }
    }
    if (calibSaveCount != lastSaveCount)
    {
        lastSaveCount = calibSaveCount;
        sendWhy       = BST_WHY_SAVED;
    }
    if (sendWhy != 0xFFFFU)
    {
        uint16_t why = sendWhy;
        sendWhy      = 0xFFFFU;
        send_copy(why);
    }

    // Восстановление
    if (applyLen != 0U)
    {
        uint16_t n = applyLen;
        applyLen   = 0U;
        bstRestore = calib_restore(rxWords, n == 0xFFFFU ? 0U : n);
        SendAck(n, 0, bstRestore, BST_CMD_APPLY);
    }
}

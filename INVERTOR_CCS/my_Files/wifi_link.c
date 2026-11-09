/*
 * wifi_link.c
 *
 * Канал к ESP32 по SPIA через DMA. Смысл и устройство - в wifi_link.h,
 * формат посылки - в docs/esp32-spi-protocol-RU.md.
 */

#include "board.h"
#include "driverlib.h"
#include "device.h"
#include "f28p55x_device.h" // EPwm7Regs - часы для dbg-замера, как у sine_Time
#include "wifi_link.h"

#define WL_MAGIC_F28 0xA55AU
#define WL_MAGIC_ESP 0x5AA5U

#define WL_HDR     4U                        // слова 0..3 - заголовок
#define WL_LAST    (WIFI_LINK_WORDS - 1U)    // слово 255 - CRC (в сторону прибора)
#define WL_PAYLOAD (WL_LAST - WL_HDR)        // 251 слово под кадры
#define WL_MAXDATA 32U                       // 64 байта - больше в CAN FD не бывает

// Раздел места в посылке (251 слово):
//
//   быстрые кадры - не больше 180 слов: ровно 10 кадров по 2 + 16, по одному
//   на такт. Больше им и не нужно, а если посылка не ушла вовремя и кадры
//   копятся, старые быстрые кадры не вытесняют ACK.
//
//   очередь (ACK, 140, 132) - всё остальное, но место, которое быстрые кадры
//   заняли в ПРОШЛОЙ посылке, она в этой не трогает: фон перекладывает очередь
//   в начале двух миллисекунд, а быстрые кадры приходят по одному всё это
//   время. Без этого кадр 140, попавший в посылку первым, вытеснял бы по два
//   быстрых. Нет быстрых кадров - вся посылка очереди, залпу ACK есть куда.
//
// Остаток при полном потоке: 251 - 180 - 34 (кадр 140) = 37 слов, шесть ACK.
//
// Первая редакция (9.11.2026) держала резерв под очередь и отдавала быстрым
// кадрам 251 - 72 = 179 слов. Десятый кадр не влезал НИКОГДА, а 140 сверху
// вытеснял ещё; на стенде не влезала треть потока.
#define WL_FAST_MAX 180U

// Очередь для ACK, 140 и 132. Самый большой залп - разовые выгрузки таблиц
// из MBOX28.c, десятки ACK подряд; 512 слов - это 85 ACK.
#define WL_QUEUE 512U

// DMA у F28P55x достаёт только до RAMGS. RAMGS2 под эти буферы и отдана:
// секция ramgs2 в 28p55x_generic_flash_lnk.cmd.
#pragma DATA_SECTION(txBuf, "ramgs2");
#pragma DATA_SECTION(rxBuf, "ramgs2");
static uint16_t txBuf[2][WIFI_LINK_WORDS];
static uint16_t rxBuf[2][WIFI_LINK_WORDS];

// Начальный адрес для DMA_init() из board.c. Дальше адрес задаётся перед
// каждым обменом - буферов по два.
const void *wifiLinkRxDst = rxBuf[0];

//---------------------------------------------------------------------------
// DMA передачи - не из SysConfig, и вот почему.
//
// События DMA у SPIA - УРОВЕНЬ, а не фронт: DMA снимает флаг запуска в
// начале пачки, и если условие («FIFO передачи <= 8» или «FIFO приёма >= 8»)
// ещё держится, флаг тут же взводится снова, и следует вторая пачка. Измерено
// на стенде 9.11.2026, по регистрам DMA после двух неудачных схем:
//
//   1. Как в примере C2000Ware spi_ex5_loopback_dma: передача от «FIFO
//      передачи <= 8», пачка 8, FIFO пуст на старте. Пустой FIFO держит
//      условие всю первую пачку, за ней сразу вторая, за ней третья - уже в
//      полный FIFO. Лишние записи в полный FIFO молча пропадают: DMA записал
//      256 слов, по проводу ушло 249, приём встал, CS остался низким.
//
//   2. Передача от события ПРИЁМА. Канал приёма своим первым же чтением
//      условие снимает, а канал передачи в FIFO приёма не читает - условие
//      держится всю его пачку, и на одно событие он делает две. Ушло 137
//      слов из 256.
//
// Повтор полностью не убрать: SPI забирает слово из FIFO когда хочет, в том
// числе посреди пачки, и условие на миг выполняется снова. Поэтому сделано
// так, чтобы ЛИШНЯЯ пачка была безвредна, - пачка в ОДНО слово:
//
//   передача, порог 8: пачка начинается при <= 8 словах, лишняя добавит ещё
//            одно - не больше 10 из 16, переполнения нет;
//   приём,   порог 1: лишняя пачка возможна, только если после чтения в
//            FIFO ещё что-то лежит, - читать из пустого FIFO нечем.
//
// 256 пачек на посылку вместо 32 для DMA ничего не стоят: слово по проводу
// идёт 3,8 мкс, пачка - десятки наносекунд.
//
// И ещё одно: первые 16 слов кладёт ПРОЦЕССОР, канал передачи включается
// после. Иначе пустой FIFO держит условие всю первую пачку - та самая
// схема 1.
//
// Канал передачи настроен здесь, а не в SysConfig: длина (240, а не 256) и
// порядок запуска держатся друг за друга и за этот разбор. Канал приёма - в
// SysConfig (wifiSPI_RX_DMA: пачка 1, 256 пачек, порог FIFO приёма 1).
//---------------------------------------------------------------------------
#define WL_TX_DMA_BASE DMA_CH5_BASE
#define WL_PRIME       16U // слов кладёт процессор: весь FIFO
#define WL_BURST       1U  // слов в пачке - разбор выше

volatile WifiLinkStat wifiLink;

static uint16_t fillIdx  = 0; // какой txBuf копится
static uint16_t fillLen  = 0; // слов кадров в нём
static uint16_t fastLen  = 0; // из них быстрых
static uint16_t fastPrev = 0; // быстрых в прошлой посылке - столько и бережём
static uint16_t dropSince = 0; // кадров не влезло с прошлой посылки - уходит в слово 3
static uint16_t seq      = 0;
static uint16_t tickCnt  = 0;

static volatile uint16_t dmaBusy = 0;
static uint16_t          rxIdx   = 0; // куда принимает последний обмен
static volatile uint16_t rxReady = 0; // 0 - разбирать нечего, иначе rxIdx + 1

// Тоже в RAMGS2, хотя DMA её не касается: .bss живёт в RAMLS5, и та
// заполнена почти до конца.
#pragma DATA_SECTION(queue, "ramgs2");
static uint16_t queue[WL_QUEUE];
static uint16_t qHead  = 0; // куда писать
static uint16_t qTail  = 0; // откуда читать
static uint16_t qCount = 0;

// Кадр в посылку. Звать с запрещёнными прерываниями.
static inline void put(uint16_t id, const uint16_t *w, uint16_t n)
{
    uint16_t *p = &txBuf[fillIdx][WL_HDR + fillLen];
    uint16_t  i;

    p[0] = id;
    p[1] = (uint16_t)(n * 2U); // длина в байтах, как DLC у CAN
    for (i = 0U; i < n; i++)
    {
        p[2U + i] = w[i];
    }
    fillLen += n + 2U;
}

void WifiLink_init(void)
{
    // Канал приёма настроен в SysConfig (wifiSPI_RX_DMA), канал передачи -
    // здесь, разбор выше.
    DMA_configAddresses(WL_TX_DMA_BASE, (const void *)(SPIA_BASE + SPI_O_TXBUF),
                        &txBuf[0][WL_PRIME]);
    DMA_configBurst(WL_TX_DMA_BASE, WL_BURST, 1, 0);
    DMA_configTransfer(WL_TX_DMA_BASE, (WIFI_LINK_WORDS - WL_PRIME) / WL_BURST, 1, 0);
    DMA_configWrap(WL_TX_DMA_BASE, 65535U, 0, 65535U, 0);
    DMA_configMode(WL_TX_DMA_BASE, DMA_TRIGGER_SPIATX,
                   DMA_CFG_ONESHOT_DISABLE | DMA_CFG_CONTINUOUS_DISABLE | DMA_CFG_SIZE_16BIT);
    DMA_enableTrigger(WL_TX_DMA_BASE);
    DMA_stopChannel(WL_TX_DMA_BASE);
}

#pragma CODE_SECTION(WifiLink_pushFast, ".TI.ramfunc");
void WifiLink_pushFast(uint16_t id, const uint16_t *w, uint16_t numWords)
{
    uint16_t st = __disable_interrupts();

    if ((fastLen + numWords + 2U <= WL_FAST_MAX) &&
        (fillLen + numWords + 2U <= WL_PAYLOAD))
    {
        put(id, w, numWords);
        fastLen += numWords + 2U;
    }
    else
    {
        wifiLink.fastDrop++;
        dropSince++;
    }

    __restore_interrupts(st);
}

void WifiLink_push(uint16_t id, const uint16_t *w, uint16_t numWords)
{
    uint16_t st;
    uint16_t i;

    if (numWords > WL_MAXDATA)
    {
        return;
    }

    st = __disable_interrupts();

    if (qCount + numWords + 2U <= WL_QUEUE)
    {
        queue[qHead] = id;
        qHead = (qHead + 1U) % WL_QUEUE;
        queue[qHead] = (uint16_t)(numWords * 2U);
        qHead = (qHead + 1U) % WL_QUEUE;
        for (i = 0U; i < numWords; i++)
        {
            queue[qHead] = w[i];
            qHead = (qHead + 1U) % WL_QUEUE;
        }
        qCount += numWords + 2U;
        if (qCount > wifiLink.maxQueue)
        {
            wifiLink.maxQueue = qCount;
        }
    }
    else
    {
        wifiLink.queueDrop++;
        dropSince++;
    }

    __restore_interrupts(st);
}

#pragma CODE_SECTION(WifiLink_tick, ".TI.ramfunc");
void WifiLink_tick(void)
{
    uint16_t *t;
    uint16_t  next;
    uint16_t  t0;

    if (++tickCnt < WIFI_LINK_TICKS)
    {
        return;
    }
    tickCnt = 0U;

    if (dmaBusy)
    {
        wifiLink.busy++;
        return;
    }

    // ESP32 не зарядила транзакцию - посылка ушла бы в никуда. Копим дальше:
    // быстрые кадры упрутся в резерв и начнут пропускаться, остальные ждут.
    if (GPIO_readPin(wifi_rdy) != 0U)
    {
        wifiLink.notReady++;
        return;
    }

    t0 = EPwm7Regs.TBCTR;

    t = txBuf[fillIdx];
    t[0]       = WL_MAGIC_F28;
    t[1]       = seq++;
    t[2]       = (uint16_t)(fillLen * 2U);
    t[3]       = dropSince;
    t[WL_LAST] = 0U;
    dropSince  = 0U;
    if (fillLen > wifiLink.maxFill)
    {
        wifiLink.maxFill = fillLen;
    }

    // Принимаем в другой буфер, не в тот, что, может быть, ещё разбирает фон.
    next = rxIdx ^ 1U;
    if (rxReady == next + 1U)
    {
        wifiLink.rxMissed++;
        rxReady = 0U;
    }
    rxIdx = next;

    // Остаток в FIFO от прерванного обмена (остановка отладчиком посреди
    // посылки) сдвинул бы весь приём на слово.
    SPI_resetRxFIFO(wifiSPI_BASE);
    SPI_resetTxFIFO(wifiSPI_BASE);
    DMA_clearTriggerFlag(wifiSPI_RX_DMA_BASE);

    DMA_configAddresses(WL_TX_DMA_BASE, (const void *)(SPIA_BASE + SPI_O_TXBUF),
                        &t[WL_PRIME]);
    DMA_configAddresses(wifiSPI_RX_DMA_BASE, rxBuf[next], wifiSPI_RX_DMA_ADDRESS);

    // CS программный: держится низким всю посылку. Микросекунда - ведомому
    // ESP32 заметить выбор кадра до первого фронта CLK.
    GPIO_writePin(wifi_cs, 0);
    DEVICE_DELAY_US(1);

    dmaBusy = 1U;
    wifiLink.sent++;
    DMA_startChannel(wifiSPI_RX_DMA_BASE);

    // Порядок обязателен - разбор в начале файла. Сначала FIFO наполняет
    // процессор: 16 записей - доли микросекунды, слово по проводу идёт 3,8 мкс,
    // так что FIFO полон раньше, чем уходит хоть одно слово. Пока FIFO был
    // пуст, флаг запуска канала передачи взвёлся - снимаем его, когда условие
    // уже не держится, и только потом пускаем канал.
    {
        uint16_t i;
        for (i = 0U; i < WL_PRIME; i++)
        {
            HWREGH(SPIA_BASE + SPI_O_TXBUF) = t[i];
        }
    }
    DMA_clearTriggerFlag(WL_TX_DMA_BASE);
    DMA_startChannel(WL_TX_DMA_BASE);

    fillIdx ^= 1U;
    fillLen  = 0U;
    fastPrev = fastLen;
    fastLen  = 0U;

    wifiLink.startTime = EPwm7Regs.TBCTR - t0;
    if (wifiLink.startTime > wifiLink.startMax)
    {
        wifiLink.startMax = wifiLink.startTime;
    }
}

// Конец приёма: последнее слово уже прошло по проводу - поднять CS.
__interrupt void INT_wifiSPI_RX_DMA_ISR(void)
{
    GPIO_writePin(wifi_cs, 1);
    dmaBusy = 0U;
    rxReady = rxIdx + 1U;
    Interrupt_clearACKGroup(INT_wifiSPI_RX_DMA_INTERRUPT_ACK_GROUP);
}

static void drain(void)
{
    for (;;)
    {
        uint16_t st = __disable_interrupts();
        uint16_t n;
        uint16_t keep; // место под быстрые кадры, которые ещё придут
        uint16_t i;
        uint16_t *p;

        if (qCount == 0U)
        {
            __restore_interrupts(st);
            return;
        }

        n = queue[(qTail + 1U) % WL_QUEUE] / 2U + 2U; // кадр целиком, слов
        keep = (fastPrev > fastLen) ? (fastPrev - fastLen) : 0U;
        if (fillLen + n + keep > WL_PAYLOAD)
        {
            __restore_interrupts(st);
            return; // ждёт следующей посылки
        }

        // По одному кадру за раз: запрет прерываний держится не дольше
        // копирования 34 слов.
        p = &txBuf[fillIdx][WL_HDR + fillLen];
        for (i = 0U; i < n; i++)
        {
            p[i]  = queue[qTail];
            qTail = (qTail + 1U) % WL_QUEUE;
        }
        qCount  -= n;
        fillLen += n;

        __restore_interrupts(st);
    }
}

void WifiLink_background(void)
{
    uint16_t r;
    uint16_t st;

    drain();

    st = __disable_interrupts();
    r  = rxReady;
    rxReady = 0U;
    __restore_interrupts(st);

    if (r != 0U)
    {
        const uint16_t *p = rxBuf[r - 1U];

        if (p[0] == WL_MAGIC_ESP)
        {
            wifiLink.rxOk++;
        }
        else
        {
            wifiLink.rxBad++;
        }
    }
}

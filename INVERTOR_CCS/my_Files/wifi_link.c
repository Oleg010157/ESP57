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
#include "myMCAN.h" // cmd_MBOX28, MCAN_holdRx - команды из WiFi
#include "myDCAN.h" // CAN_RATE_CMD

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
#pragma DATA_SECTION(wlTxBuf, "ramgs2");
#pragma DATA_SECTION(wlRxBuf, "ramgs2");
static uint16_t wlTxBuf[2][WIFI_LINK_WORDS];
static uint16_t wlRxBuf[2][WIFI_LINK_WORDS];

// Начальный адрес для DMA_init() из board.c. Дальше адрес задаётся перед
// каждым обменом - буферов по два.
const void *wifiLinkRxDst = wlRxBuf[0];

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

static uint16_t fillIdx  = 0; // какой wlTxBuf копится
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
    uint16_t *p = &wlTxBuf[fillIdx][WL_HDR + fillLen];
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
                        &wlTxBuf[0][WL_PRIME]);
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

    t = wlTxBuf[fillIdx];
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
    DMA_configAddresses(wifiSPI_RX_DMA_BASE, wlRxBuf[next], wifiSPI_RX_DMA_ADDRESS);

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
        p = &wlTxBuf[fillIdx][WL_HDR + fillLen];
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

//---------------------------------------------------------------------------
// Команды от ESP32.
//
// Посылка ESP32 закрыта CRC-16/CCITT-FALSE (0x1021, начальное 0xFFFF) по
// словам 0..254, каждое слово - старший байт, потом младший, как по проводу.
// Битый кадр телеметрии безвреден, битая команда - нет: случайно сложившийся
// номер прибор выполнил бы. Поэтому без верной CRC не выполняется ничего.
//
// Кадр команды - ID 2, 8 байт, слова в порядке CAN: cmd, d3, d2, d1 (так их
// раскладывает MCAN-приёмник в myMCAN.c).
//---------------------------------------------------------------------------
#define WL_CMD_ID   2U
#define WL_MAX_CMDS (WL_PAYLOAD / 6U) // кадр команды - 2 + 4 слова

static const uint16_t crcTab[256] = {
    0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50A5, 0x60C6, 0x70E7, 0x8108, 0x9129, 0xA14A, 0xB16B, 0xC18C, 0xD1AD, 0xE1CE, 0xF1EF,
    0x1231, 0x0210, 0x3273, 0x2252, 0x52B5, 0x4294, 0x72F7, 0x62D6, 0x9339, 0x8318, 0xB37B, 0xA35A, 0xD3BD, 0xC39C, 0xF3FF, 0xE3DE,
    0x2462, 0x3443, 0x0420, 0x1401, 0x64E6, 0x74C7, 0x44A4, 0x5485, 0xA56A, 0xB54B, 0x8528, 0x9509, 0xE5EE, 0xF5CF, 0xC5AC, 0xD58D,
    0x3653, 0x2672, 0x1611, 0x0630, 0x76D7, 0x66F6, 0x5695, 0x46B4, 0xB75B, 0xA77A, 0x9719, 0x8738, 0xF7DF, 0xE7FE, 0xD79D, 0xC7BC,
    0x48C4, 0x58E5, 0x6886, 0x78A7, 0x0840, 0x1861, 0x2802, 0x3823, 0xC9CC, 0xD9ED, 0xE98E, 0xF9AF, 0x8948, 0x9969, 0xA90A, 0xB92B,
    0x5AF5, 0x4AD4, 0x7AB7, 0x6A96, 0x1A71, 0x0A50, 0x3A33, 0x2A12, 0xDBFD, 0xCBDC, 0xFBBF, 0xEB9E, 0x9B79, 0x8B58, 0xBB3B, 0xAB1A,
    0x6CA6, 0x7C87, 0x4CE4, 0x5CC5, 0x2C22, 0x3C03, 0x0C60, 0x1C41, 0xEDAE, 0xFD8F, 0xCDEC, 0xDDCD, 0xAD2A, 0xBD0B, 0x8D68, 0x9D49,
    0x7E97, 0x6EB6, 0x5ED5, 0x4EF4, 0x3E13, 0x2E32, 0x1E51, 0x0E70, 0xFF9F, 0xEFBE, 0xDFDD, 0xCFFC, 0xBF1B, 0xAF3A, 0x9F59, 0x8F78,
    0x9188, 0x81A9, 0xB1CA, 0xA1EB, 0xD10C, 0xC12D, 0xF14E, 0xE16F, 0x1080, 0x00A1, 0x30C2, 0x20E3, 0x5004, 0x4025, 0x7046, 0x6067,
    0x83B9, 0x9398, 0xA3FB, 0xB3DA, 0xC33D, 0xD31C, 0xE37F, 0xF35E, 0x02B1, 0x1290, 0x22F3, 0x32D2, 0x4235, 0x5214, 0x6277, 0x7256,
    0xB5EA, 0xA5CB, 0x95A8, 0x8589, 0xF56E, 0xE54F, 0xD52C, 0xC50D, 0x34E2, 0x24C3, 0x14A0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
    0xA7DB, 0xB7FA, 0x8799, 0x97B8, 0xE75F, 0xF77E, 0xC71D, 0xD73C, 0x26D3, 0x36F2, 0x0691, 0x16B0, 0x6657, 0x7676, 0x4615, 0x5634,
    0xD94C, 0xC96D, 0xF90E, 0xE92F, 0x99C8, 0x89E9, 0xB98A, 0xA9AB, 0x5844, 0x4865, 0x7806, 0x6827, 0x18C0, 0x08E1, 0x3882, 0x28A3,
    0xCB7D, 0xDB5C, 0xEB3F, 0xFB1E, 0x8BF9, 0x9BD8, 0xABBB, 0xBB9A, 0x4A75, 0x5A54, 0x6A37, 0x7A16, 0x0AF1, 0x1AD0, 0x2AB3, 0x3A92,
    0xFD2E, 0xED0F, 0xDD6C, 0xCD4D, 0xBDAA, 0xAD8B, 0x9DE8, 0x8DC9, 0x7C26, 0x6C07, 0x5C64, 0x4C45, 0x3CA2, 0x2C83, 0x1CE0, 0x0CC1,
    0xEF1F, 0xFF3E, 0xCF5D, 0xDF7C, 0xAF9B, 0xBFBA, 0x8FD9, 0x9FF8, 0x6E17, 0x7E36, 0x4E55, 0x5E74, 0x2E93, 0x3EB2, 0x0ED1, 0x1EF0
};

static uint16_t crc16(const uint16_t *w, uint16_t n)
{
    uint16_t crc = 0xFFFFU;
    uint16_t i;

    for (i = 0U; i < n; i++)
    {
        crc = (uint16_t)((crc << 8) ^ crcTab[((crc >> 8) ^ (w[i] >> 8)) & 0xFFU]);
        crc = (uint16_t)((crc << 8) ^ crcTab[((crc >> 8) ^ w[i]) & 0xFFU]);
    }
    return crc;
}

// Команды копируются из буфера DMA до выполнения: команда может ждать буфер
// CAN (SendAck), а через 2 мс DMA начнёт писать в этот же буфер следующий-
// через-один ответ. Массив не на стеке: 164 слова при стеке проекта в 256
// (--stack_size=0x100), поверх которых ещё лягут прерывания.
#pragma DATA_SECTION(cmds, "ramgs2");
static uint16_t cmds[WL_MAX_CMDS][4];

static void rx_parse(const uint16_t *p)
{
    uint16_t nCmd = 0U;
    uint16_t w, end, i;

    if (p[0] != WL_MAGIC_ESP)
    {
        wifiLink.rxBad++;
        return;
    }
    if (crc16(p, WL_LAST) != p[WL_LAST])
    {
        wifiLink.rxCrcBad++;
        return;
    }
    if ((p[2] & 1U) || (p[2] / 2U > WL_PAYLOAD))
    {
        wifiLink.rxCrcBad++; // CRC сошлась, а длина - нет: чужая версия формата
        return;
    }
    wifiLink.rxOk++;

    w   = WL_HDR;
    end = WL_HDR + p[2] / 2U;
    while (w + 2U <= end)
    {
        uint16_t id = p[w];
        uint16_t n  = p[w + 1U] / 2U;

        if (w + 2U + n > end)
        {
            break;
        }
        if (id == WL_CMD_ID && n == 4U && nCmd < WL_MAX_CMDS)
        {
            for (i = 0U; i < 4U; i++)
            {
                cmds[nCmd][i] = p[w + 2U + i];
            }
            nCmd++;
        }
        w += 2U + n;
    }

    for (i = 0U; i < nCmd; i++)
    {
        uint16_t cmd = cmds[i][0];

        // Не по WiFi:
        //   9999 - уход в загрузчик, а он принимает прошивку только по CAN:
        //          прибор встал бы в загрузчике без связи.
        //   7100 - скорость фазы данных CAN, к WiFi отношения не имеет.
        if (cmd == 9999U || cmd == CAN_RATE_CMD)
        {
            wifiLink.cmdRejected++;
            continue;
        }

        MCAN_holdRx(true);
        cmd_MBOX28(cmds[i][3], cmds[i][2], cmds[i][1], cmd); // d1, d2, d3, cmd
        MCAN_holdRx(false);
        wifiLink.cmdIn++;
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
        rx_parse(wlRxBuf[r - 1U]);
    }
}

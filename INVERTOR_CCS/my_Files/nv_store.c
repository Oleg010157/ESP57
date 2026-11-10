/*
 * nv_store.c
 *
 *  Created on: 04.11.2026
 *      Author: Oleg Pavlovsky
 */

#include "driverlib.h"
#include "device.h"
#include "f28p55x_device.h"
#include "Errors.h"
#include "nv_store.h"

//
// История аварий (ErrHistory) и номер сессии в энергонезависимой памяти.
//
// Память - на шине I2C: сейчас 24AA128, потом FM24V02A. Обе отвечают на один адрес
// и принимают два байта адреса, поэтому код один. Разница только в том, что EEPROM
// после записи несколько миллисекунд не отвечает, а FRAM отвечает сразу: конец
// записи ловится опросом адреса, и у FRAM он просто заканчивается с первого раза.
// Сравнение и причины выбора - docs/eeprom-fram-comparison-RU.md.
//
// Модуль I2CB и ножки GPIO28 (SDA), GPIO29 (SCL) настраивает SysConfig, скорость
// тоже задана там. Модуль I2C у F28P55x не быстрее 400 кГц.
//
// Раскладка памяти, значения младшим байтом вперёд:
//
//   0x0000  заголовок: метка, версия, CRC16
//   0x0008  номер сессии, копия 0: номер, 0, CRC16
//   0x0010  номер сессии, копия 1
//   0x0040  ErrHistory[ERR_BUFF_SIZE]: счётчик, последняя сессия, время UTC
//           (два слова, младшее вперёд), CRC16 - 10 байт с шагом 16, до 0x0680
//
// Шаг 8 байт у заголовка и сессии, 16 у истории: запись не пересекает страницу
// EEPROM в 64 байта и влезает в FIFO (16 байт) за одну передачу, поэтому делить
// её не нужно.
//
// Версия 2 (10.11.2026) добавила к истории время. Записи версии 1 лежали с шагом
// 8, и читать их по новой раскладке нельзя: при другой версии в заголовке история
// один раз обнуляется целиком. Номер сессии при этом сохраняется.
//
// Без заголовка (чистая память или другая версия) история не читается: все записи
// обнуляются и встают в очередь. Испорченная запись - та же очередь: неверная CRC,
// нули. Отдельной разметки нет, поэтому пропажа питания посреди первого
// заполнения ничего не ломает - недописанные записи не пройдут CRC.
//
// Номер сессии лежит в двух копиях, запись идёт в ту, что старше. Пропадёт питание
// посреди записи - останется вторая.
//
// Из окна Watch:
//
//   nvFailed     - 1: память не отвечает, запись прекращена до перезагрузки,
//                  поднят код CE01. Прибор работает, история только в ОЗУ;
//   nvBlank      - 1: при старте не нашлось заголовка, память заполнена заново;
//   nvBadRecords - сколько записей при старте не прошли проверку CRC;
//   nvWrites     - сколько записей истории записано за эту сессию;
//   dbgNvRaise   - записать сюда код аварии: поднять её из фона. Для проверки.
//
volatile uint16_t nvFailed     = 0;
volatile uint16_t nvBlank      = 0;
volatile uint16_t nvBadRecords = 0;
volatile uint32_t nvWrites     = 0;
volatile uint16_t dbgNvRaise   = 0;

#define NV_BASE         I2CB_BASE
#define NV_SDA          28U
#define NV_SCL          29U
#define NV_DEV          0x50U // адрес микросхемы на шине

#define NV_ADDR_HEADER  0x0000U
#define NV_ADDR_SESSION 0x0008U
#define NV_ADDR_HISTORY 0x0040U
#define NV_STEP         8U      // заголовок и копии сессии
#define NV_HIST_STEP    16U

#define NV_MAGIC        0x4B35U // "K5"
#define NV_VERSION      2U
#define NV_REC_BYTES    6U      // заголовок и сессия: два значения и CRC16
#define NV_HIST_VALUES  4U      // счётчик, сессия, время мл., время ст.
#define NV_HIST_BYTES   (2U * NV_HIST_VALUES + 2U)

#define NV_WAIT_LOOPS   20000UL // ожидание одного обмена, с запасом на 100 кГц
#define NV_WR_POLLS     200U    // опрос адреса после записи при старте, до 20 мс
#define NV_WR_TICKS     (DEVICE_SYSCLK_FREQ / 50U) // то же в фоне: 20 мс по CpuTimer0
#define NV_MAX_RETRIES  3U

//--------------------------------------------------------------------------------
// Запись: n значений и CRC16, 2n + 2 байт на шине.
//--------------------------------------------------------------------------------
static uint16_t nv_crc16(const uint16_t *bytes, uint16_t len)
{
    uint16_t crc = 0xFFFFU;
    uint16_t i;
    uint16_t b;

    for (i = 0; i < len; i++)
    {
        crc ^= (uint16_t)((bytes[i] & 0xFFU) << 8);
        for (b = 0; b < 8U; b++)
            crc = ((crc & 0x8000U) != 0U) ? (uint16_t)((crc << 1) ^ 0x1021U) : (uint16_t)(crc << 1);
    }
    return crc;
}

static void nv_pack(const uint16_t *v, uint16_t n, uint16_t *bytes)
{
    uint16_t crc;
    uint16_t i;

    for (i = 0; i < n; i++)
    {
        bytes[2U * i]      = v[i] & 0xFFU;
        bytes[2U * i + 1U] = v[i] >> 8;
    }
    crc                = nv_crc16(bytes, 2U * n);
    bytes[2U * n]      = crc & 0xFFU;
    bytes[2U * n + 1U] = crc >> 8;
}

static bool nv_unpack(const uint16_t *bytes, uint16_t n, uint16_t *v)
{
    uint16_t crc = (uint16_t)(bytes[2U * n] | (bytes[2U * n + 1U] << 8));
    uint16_t i;

    if (crc != nv_crc16(bytes, 2U * n))
        return false;

    for (i = 0; i < n; i++)
        v[i] = (uint16_t)(bytes[2U * i] | (bytes[2U * i + 1U] << 8));
    return true;
}

// Два значения - заголовок и сессия.
static void nv_pack2(uint16_t v0, uint16_t v1, uint16_t *bytes)
{
    uint16_t v[2];

    v[0] = v0;
    v[1] = v1;
    nv_pack(v, 2U, bytes);
}

static bool nv_unpack2(const uint16_t *bytes, uint16_t *v0, uint16_t *v1)
{
    uint16_t v[2];

    if (!nv_unpack(bytes, 2U, v))
        return false;

    *v0 = v[0];
    *v1 = v[1];
    return true;
}

//--------------------------------------------------------------------------------
// Обмен по шине.
//--------------------------------------------------------------------------------

// Выход из зависания. Если процессор сбросили посреди обмена, микросхема держит SDA
// в нуле и ждёт тактов. До девяти тактов SCL дочитывают её байт, стоп-условие
// возвращает шину в покой. Ножки на это время - GPIO с открытым стоком.
static void nv_busRecover(void)
{
    uint16_t n = 0;

    I2C_disableModule(NV_BASE);

    GPIO_writePin(NV_SDA, 1);
    GPIO_writePin(NV_SCL, 1);
    GPIO_setPinConfig(GPIO_28_GPIO28);
    GPIO_setPinConfig(GPIO_29_GPIO29);
    GPIO_setPadConfig(NV_SDA, GPIO_PIN_TYPE_PULLUP | GPIO_PIN_TYPE_OD);
    GPIO_setPadConfig(NV_SCL, GPIO_PIN_TYPE_PULLUP | GPIO_PIN_TYPE_OD);
    GPIO_setDirectionMode(NV_SDA, GPIO_DIR_MODE_OUT);
    GPIO_setDirectionMode(NV_SCL, GPIO_DIR_MODE_OUT);
    DEVICE_DELAY_US(20);

    while ((GPIO_readPin(NV_SDA) == 0U) && (n < 9U))
    {
        GPIO_writePin(NV_SCL, 0);
        DEVICE_DELAY_US(5);
        GPIO_writePin(NV_SCL, 1);
        DEVICE_DELAY_US(5);
        n++;
    }

    GPIO_writePin(NV_SCL, 0);
    DEVICE_DELAY_US(5);
    GPIO_writePin(NV_SDA, 0);
    DEVICE_DELAY_US(5);
    GPIO_writePin(NV_SCL, 1);
    DEVICE_DELAY_US(5);
    GPIO_writePin(NV_SDA, 1);
    DEVICE_DELAY_US(5);

    // Обратно модулю. Его скорость задал SysConfig, она не менялась. Длину байта
    // ставим здесь: SysConfig по умолчанию даёт I2C_BITCOUNT_1, то есть один бит.
    GPIO_setDirectionMode(NV_SDA, GPIO_DIR_MODE_IN);
    GPIO_setPadConfig(NV_SDA, GPIO_PIN_TYPE_PULLUP);
    GPIO_setQualificationMode(NV_SDA, GPIO_QUAL_ASYNC);
    GPIO_setDirectionMode(NV_SCL, GPIO_DIR_MODE_IN);
    GPIO_setPadConfig(NV_SCL, GPIO_PIN_TYPE_PULLUP);
    GPIO_setQualificationMode(NV_SCL, GPIO_QUAL_ASYNC);
    GPIO_setPinConfig(GPIO_28_I2CB_SDA);
    GPIO_setPinConfig(GPIO_29_I2CB_SCL);

    I2C_setBitCount(NV_BASE, I2C_BITCOUNT_8);
    I2C_setEmulationMode(NV_BASE, I2C_EMULATION_FREE_RUN);
    I2C_enableModule(NV_BASE);
}

// Начать запись: адрес и данные ложатся в FIFO до старта. Не ждёт.
static void nv_writeStart(uint16_t mem, const uint16_t *bytes, uint16_t len)
{
    uint16_t i;

    I2C_disableFIFO(NV_BASE); // сброс FIFO
    I2C_enableFIFO(NV_BASE);
    I2C_clearStatus(NV_BASE, I2C_STS_NO_ACK | I2C_STS_ARB_LOST |
                             I2C_STS_STOP_CONDITION | I2C_STS_REG_ACCESS_RDY);

    I2C_setTargetAddress(NV_BASE, NV_DEV);
    I2C_setDataCount(NV_BASE, (uint16_t)(2U + len));
    I2C_setConfig(NV_BASE, I2C_CONTROLLER_SEND_MODE);
    I2C_putData(NV_BASE, (uint16_t)(mem >> 8));
    I2C_putData(NV_BASE, (uint16_t)(mem & 0xFFU));
    for (i = 0; i < len; i++)
        I2C_putData(NV_BASE, (uint16_t)(bytes[i] & 0xFFU));

    I2C_sendStartCondition(NV_BASE);
    I2C_sendStopCondition(NV_BASE);
}

// Начать опрос адреса: чтение одного байта. Пока идёт цикл записи, EEPROM не
// отвечает. Байт ловится флагом RRDY, а он бывает только без FIFO. Не ждёт.
static void nv_probeStart(void)
{
    I2C_disableFIFO(NV_BASE);
    I2C_clearStatus(NV_BASE, I2C_STS_NO_ACK | I2C_STS_ARB_LOST |
                             I2C_STS_STOP_CONDITION);

    I2C_setTargetAddress(NV_BASE, NV_DEV);
    I2C_setDataCount(NV_BASE, 1);
    I2C_setConfig(NV_BASE, I2C_CONTROLLER_RECEIVE_MODE);
    I2C_sendStartCondition(NV_BASE);
    I2C_sendStopCondition(NV_BASE);
}

static bool nv_waitStop(void)
{
    uint32_t n;

    for (n = 0; n < NV_WAIT_LOOPS; n++)
    {
        if (!I2C_getStopConditionStatus(NV_BASE))
            return true;
    }
    return false;
}

// Блокирующие обмены - только для старта.
static bool nv_probeWait(void)
{
    uint32_t n;
    uint16_t sts = 0;

    nv_probeStart();

    for (n = 0; n < NV_WAIT_LOOPS; n++)
    {
        sts = I2C_getStatus(NV_BASE);
        if ((sts & (I2C_STS_NO_ACK | I2C_STS_ARB_LOST | I2C_STS_RX_DATA_RDY)) != 0U)
            break;
    }

    if ((sts & I2C_STS_RX_DATA_RDY) != 0U)
    {
        (void)I2C_getData(NV_BASE);
        return nv_waitStop();
    }

    I2C_clearStatus(NV_BASE, I2C_STS_NO_ACK | I2C_STS_ARB_LOST);
    (void)nv_waitStop();
    return false;
}

static bool nv_writeWait(uint16_t mem, const uint16_t *bytes, uint16_t len)
{
    uint16_t i;
    uint32_t n;

    nv_writeStart(mem, bytes, len);

    for (n = 0; n < NV_WAIT_LOOPS; n++)
    {
        if ((I2C_getStatus(NV_BASE) & (I2C_STS_NO_ACK | I2C_STS_ARB_LOST)) != 0U)
        {
            I2C_clearStatus(NV_BASE, I2C_STS_NO_ACK | I2C_STS_ARB_LOST);
            (void)nv_waitStop();
            return false;
        }
        if (!I2C_getStopConditionStatus(NV_BASE))
            break;
    }
    if (n >= NV_WAIT_LOOPS)
        return false;

    for (i = 0; i < NV_WR_POLLS; i++)
    {
        if (nv_probeWait())
            return true;
    }
    return false;
}

// Запись адреса без стопа, повторный старт, приём со стопом.
static bool nv_read(uint16_t mem, uint16_t *bytes, uint16_t len)
{
    uint16_t i;
    uint16_t sts = 0;
    uint32_t n;

    I2C_disableFIFO(NV_BASE);
    I2C_enableFIFO(NV_BASE);
    I2C_clearStatus(NV_BASE, I2C_STS_NO_ACK | I2C_STS_ARB_LOST |
                             I2C_STS_STOP_CONDITION | I2C_STS_REG_ACCESS_RDY);

    I2C_setTargetAddress(NV_BASE, NV_DEV);
    I2C_setDataCount(NV_BASE, 2);
    I2C_setConfig(NV_BASE, I2C_CONTROLLER_SEND_MODE);
    I2C_putData(NV_BASE, (uint16_t)(mem >> 8));
    I2C_putData(NV_BASE, (uint16_t)(mem & 0xFFU));
    I2C_sendStartCondition(NV_BASE);

    for (n = 0; n < NV_WAIT_LOOPS; n++)
    {
        sts = I2C_getStatus(NV_BASE);
        if ((sts & (I2C_STS_REG_ACCESS_RDY | I2C_STS_NO_ACK | I2C_STS_ARB_LOST)) != 0U)
            break;
    }
    if ((sts & I2C_STS_REG_ACCESS_RDY) == 0U || (sts & (I2C_STS_NO_ACK | I2C_STS_ARB_LOST)) != 0U)
    {
        I2C_clearStatus(NV_BASE, I2C_STS_NO_ACK | I2C_STS_ARB_LOST);
        I2C_sendStopCondition(NV_BASE);
        (void)nv_waitStop();
        return false;
    }

    I2C_setDataCount(NV_BASE, len);
    I2C_setConfig(NV_BASE, I2C_CONTROLLER_RECEIVE_MODE);
    I2C_sendStartCondition(NV_BASE);
    I2C_sendStopCondition(NV_BASE);

    if (!nv_waitStop())
        return false;

    for (n = 0; n < NV_WAIT_LOOPS; n++)
    {
        if ((uint16_t)I2C_getRxFIFOStatus(NV_BASE) >= len)
            break;
    }
    if ((uint16_t)I2C_getRxFIFOStatus(NV_BASE) < len)
        return false;

    for (i = 0; i < len; i++)
        bytes[i] = I2C_getData(NV_BASE) & 0xFFU;
    return true;
}

//--------------------------------------------------------------------------------
// Старт.
//--------------------------------------------------------------------------------

// Память не отвечает: прибор работает дальше, история только в ОЗУ.
static void nv_fail(void)
{
    nvFailed = 1U;
    RegisterSessionEvent(CE01);
}

// Номера идут по кругу, ноль пропускается: с ним LastSessionId чистой записи
// совпал бы с текущей сессией, и авария не посчиталась бы.
static uint16_t nv_nextSession(uint16_t id)
{
    return (id == 0xFFFFU) ? 1U : (uint16_t)(id + 1U);
}

static bool nv_header(void)
{
    uint16_t bytes[NV_REC_BYTES];
    uint16_t magic;
    uint16_t version;

    if (!nv_read(NV_ADDR_HEADER, bytes, NV_REC_BYTES))
        return false;

    if (nv_unpack2(bytes, &magic, &version) && (magic == NV_MAGIC) && (version == NV_VERSION))
        return true;

    nvBlank = 1U;
    nv_pack2(NV_MAGIC, NV_VERSION, bytes);
    return nv_writeWait(NV_ADDR_HEADER, bytes, NV_REC_BYTES);
}

static bool nv_history(void)
{
    uint16_t bytes[NV_HIST_BYTES];
    uint16_t v[NV_HIST_VALUES];
    uint16_t code;

    for (code = 0; code < ERR_BUFF_SIZE; code++)
    {
        // Другая раскладка: старое не читается, нули встают в очередь все сразу.
        // Случайно сошедшаяся CRC на чужих байтах дала бы мусорный счётчик.
        if (nvBlank != 0U)
        {
            ErrHistory_MarkPending(code);
            continue;
        }

        if (!nv_read((uint16_t)(NV_ADDR_HISTORY + code * NV_HIST_STEP), bytes, NV_HIST_BYTES))
            return false;

        if (nv_unpack(bytes, NV_HIST_VALUES, v))
        {
            ErrHistory[code].HistoryCount  = v[0];
            ErrHistory[code].LastSessionId = v[1];
            ErrHistory[code].LastTime      = ((uint32_t)v[3] << 16) | v[2];
        }
        else
        {
            // ОЗУ уже обнулено: остаётся записать нули на место порченой записи.
            nvBadRecords++;
            ErrHistory_MarkPending(code);
        }
    }
    return true;
}

static bool nv_session(void)
{
    uint16_t bytes[NV_REC_BYTES];
    uint16_t id[2] = {0U, 0U};
    bool     ok[2] = {false, false};
    uint16_t pad;
    uint16_t i;
    uint16_t newest;

    for (i = 0; i < 2U; i++)
    {
        if (!nv_read((uint16_t)(NV_ADDR_SESSION + i * NV_STEP), bytes, NV_REC_BYTES))
            return false;
        ok[i] = nv_unpack2(bytes, &id[i], &pad) && (id[i] != 0U);
    }

    if (ok[0] && ok[1])
        newest = ((int16_t)(id[1] - id[0]) > 0) ? 1U : 0U; // сравнение на круге
    else
        newest = ok[1] ? 1U : 0U;

    CurrentSessionId = (ok[0] || ok[1]) ? nv_nextSession(id[newest]) : 1U;

    nv_pack2(CurrentSessionId, 0U, bytes);
    return nv_writeWait((uint16_t)(NV_ADDR_SESSION + (newest ^ 1U) * NV_STEP), bytes, NV_REC_BYTES);
}

void NvStore_init(void)
{
    // Таблица лежит в отдельной секции, и нулями при старте её никто не заполнит.
    ErrHistory_Reset();

    // Сессия в ОЗУ на случай отказа памяти. Ноль нельзя, см. nv_nextSession().
    CurrentSessionId = 1U;

    nv_busRecover();

    if (!nv_header() || !nv_history() || !nv_session())
        nv_fail();
}

//--------------------------------------------------------------------------------
// Фон: один шаг за проход, ничего не ждёт.
//--------------------------------------------------------------------------------
typedef enum
{
    NV_IDLE,
    NV_WRITING,    // данные уходят на шину
    NV_POLL_START, // начать опрос адреса
    NV_POLL_WAIT,  // ответ на опрос
    NV_POLL_STOP   // стоп после опроса
} NvState;

static NvState  nvState   = NV_IDLE;
static uint16_t nvCode    = 0U;     // какая запись идёт
static uint16_t nvRetries = 0U;
static bool     nvAcked   = false;
static uint32_t nvT0      = 0UL;    // отсчёт CpuTimer0 с начала записи

// Сколько тактов прошло по CpuTimer0: он считает вниз и перезаряжается раз в 50 мс.
static uint32_t nv_elapsed(void)
{
    uint32_t t1 = CPUTimer_getTimerCount(CPUTIMER0_BASE);

    return (nvT0 >= t1) ? (nvT0 - t1)
                        : (nvT0 + HWREG(CPUTIMER0_BASE + CPUTIMER_O_PRD) + 1UL - t1);
}

// Не вышло: запись снова в очередь. После нескольких неудач подряд - отказ.
static void nv_writeFailed(void)
{
    nvState = NV_IDLE;

    DINT;
    ErrHistory_MarkPending(nvCode);
    EINT;

    nvRetries++;
    if (nvRetries >= NV_MAX_RETRIES)
        nv_fail();
}

void NvStore_background(void)
{
    uint16_t sts;

    if (dbgNvRaise != 0U)
    {
        RegisterSessionEvent(dbgNvRaise);
        dbgNvRaise = 0U;
    }

    if (nvFailed != 0U)
        return;

    switch (nvState)
    {
    case NV_IDLE:
    {
        uint16_t bytes[NV_HIST_BYTES];
        uint16_t v[NV_HIST_VALUES] = {0U, 0U, 0U, 0U};
        bool     got;

        // RegisterSessionEvent() зовётся и из INT_EPWM2_Iu_TZ_ISR, поэтому и флаги
        // очереди, и сама запись снимаются при запрещённых прерываниях.
        DINT;
        got = ErrHistory_GetNextPendingCode(&nvCode);
        if (got)
        {
            v[0] = ErrHistory[nvCode].HistoryCount;
            v[1] = ErrHistory[nvCode].LastSessionId;
            v[2] = (uint16_t)(ErrHistory[nvCode].LastTime & 0xFFFFUL);
            v[3] = (uint16_t)(ErrHistory[nvCode].LastTime >> 16);
        }
        EINT;

        if (!got)
            return;

        nv_pack(v, NV_HIST_VALUES, bytes);
        nvT0 = CPUTimer_getTimerCount(CPUTIMER0_BASE);
        nv_writeStart((uint16_t)(NV_ADDR_HISTORY + nvCode * NV_HIST_STEP), bytes, NV_HIST_BYTES);
        nvState = NV_WRITING;
        break;
    }

    case NV_WRITING:
        if ((I2C_getStatus(NV_BASE) & (I2C_STS_NO_ACK | I2C_STS_ARB_LOST)) != 0U)
        {
            I2C_clearStatus(NV_BASE, I2C_STS_NO_ACK | I2C_STS_ARB_LOST);
            nv_writeFailed();
        }
        else if (!I2C_getStopConditionStatus(NV_BASE))
        {
            nvT0    = CPUTimer_getTimerCount(CPUTIMER0_BASE);
            nvState = NV_POLL_START;
        }
        else if (nv_elapsed() > NV_WR_TICKS)
        {
            nv_writeFailed();
        }
        break;

    case NV_POLL_START:
        nv_probeStart();
        nvState = NV_POLL_WAIT;
        break;

    case NV_POLL_WAIT:
        sts = I2C_getStatus(NV_BASE);
        if ((sts & I2C_STS_RX_DATA_RDY) != 0U)
        {
            (void)I2C_getData(NV_BASE);
            nvAcked = true;
            nvState = NV_POLL_STOP;
        }
        else if ((sts & (I2C_STS_NO_ACK | I2C_STS_ARB_LOST)) != 0U)
        {
            I2C_clearStatus(NV_BASE, I2C_STS_NO_ACK | I2C_STS_ARB_LOST);
            nvAcked = false;
            nvState = NV_POLL_STOP;
        }
        else if (nv_elapsed() > NV_WR_TICKS)
        {
            nv_writeFailed();
        }
        break;

    case NV_POLL_STOP:
        if (I2C_getStopConditionStatus(NV_BASE))
        {
            if (nv_elapsed() > NV_WR_TICKS)
                nv_writeFailed();
        }
        else if (nvAcked)
        {
            nvRetries = 0U;
            nvWrites++;
            nvState = NV_IDLE;
        }
        else if (nv_elapsed() > NV_WR_TICKS)
        {
            nv_writeFailed();
        }
        else
        {
            nvState = NV_POLL_START;
        }
        break;

    default:
        nvState = NV_IDLE;
        break;
    }
}

void NvStore_requestClear(void)
{
    uint16_t code;

    for (code = 0; code < ERR_BUFF_SIZE; code++)
        ErrHistory_MarkPending(code);
}

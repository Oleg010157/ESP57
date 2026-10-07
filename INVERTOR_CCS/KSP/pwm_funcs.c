/*
 * pwm_funcs.c
 *
 *  Created on: 19 ���. 2015 �.
 *      Author: Andrejs.Gasilovs
 *  Modified on 15/09/2025
 *      Author: Oleg Pavlovsky
 */
#include "device.h" // DEVICE_DELAY_US
#include "f28p55x_device.h"
#include "Motor.h"
#include "PARAMS_INOUT.h"
#include "myEPWM.h"

static void bridge_manual_release(void);

/* Эта функция отключает пины от функции EPWM и переводит их в пины вывода общего
 * назначения, после чего выставляет на них уровень, закрывающий транзисторы.
 *
 * Какой это уровень - задаёт IGBT_ACTIVE_HIGH в my_Files/myEPWM.h, ЕДИНСТВЕННОЕ
 * место, где полярность драйверов записана. Раньше здесь было жёстко GPASET
 * ("1 - выключает IGBT"), то есть вариант active-low. Это досталось от старого
 * проекта и противоречит остальной настройке (мёртвое время EPWM и полярность
 * модулятора отвечают active-high). Ошибка в эту сторону не безобидна: при
 * active-high драйверах прежний код не закрывал бы ключи, а ОТКРЫВАЛ их - и
 * именно эта функция вызывается по аварии из grid_tie_background().
 *
 * ЧТО КОМУ ПРИНАДЛЕЖИТ (с 07.10.2026). H-мост - это EPWM1 (плечо A) и EPWM2
 * (плечо B), пины по SysConfig (board.h): GPIO0/GPIO1 и GPIO2/GPIO3. Весь мост
 * целиком ведут IGBT1_Disable()/PWM1_On(): канал АИН-1 и есть этот мост, и
 * кнопка питания в Motor_Control шлёт только команды канала 1 (10 и 12).
 *
 * Раньше раскладка была от B81, где за "1" стоял DC-DC (EPWM1 + EPWM5 на
 * GPIO16/17), а за "2" - трёхфазный инвертор (EPWM2..4, EPWM2B на GPIO40).
 * На этой плате это давало полмоста: PWM1_On() выводил только плечо A, верх
 * плеча B (GPIO2) оставался закрытым, а низ (GPIO3) не закрывался никогда -
 * его не трогала ни одна функция, и EPWM2B модулировал постоянно, в том числе
 * после СТОПа и аварии. GPIO16/17 здесь аналоговые входы A25/C24.
 *
 * IGBT2_Disable()/PWM2_On() остались при пинах EPWM3/EPWM4 бывшего АИН-2 -
 * что к ним подключено на этой плате, не выяснено, поведение не менялось.
 *
 * IGBT1_Disable() заодно снимает ручное управление (тест IGBT): кто бы ни
 * закрыл мост - СТОП, Отключить, авария, загрузчик - тест кончается вместе с
 * ним, а не остаётся висеть с подменённым мёртвым временем.
 */
void IGBT2_Disable(void)
{
    DINT;
    EALLOW;

    // EPWM3  ================================================================
    GpioCtrlRegs.GPAMUX1.bit.GPIO14 = 0; // 0=GPIO,  1=EPWM3A,
    GpioCtrlRegs.GPADIR.bit.GPIO14  = 1; // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO14);

    GpioCtrlRegs.GPAMUX1.bit.GPIO15 = 0; // 0=GPIO,  1=EPWM3B,
    GpioCtrlRegs.GPADIR.bit.GPIO15  = 1; // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO15);

    // EPWM4 =================================================================
    GpioCtrlRegs.GPAMUX2.bit.GPIO22 = 0; // 0=GPIO,  2=EPWM4A,
    GpioCtrlRegs.GPADIR.bit.GPIO22  = 1;
    IGBT_OFF_SET(GPA, GPIO22);

    GpioCtrlRegs.GPAMUX1.bit.GPIO7 = 0; // 0=GPIO,  1=EPWM4B,
    GpioCtrlRegs.GPADIR.bit.GPIO7  = 1;
    IGBT_OFF_SET(GPA, GPIO7);

    EDIS;

    motor2.flag.bit.pwm_On = 0;
    EINT;
}

void IGBT1_Disable(void)
{

    DINT;
    EALLOW;

    // EPWM1 ===========================================================
    GpioCtrlRegs.GPAMUX1.bit.GPIO0 = 0;  // 0=GPIO,  1=EPWM1A
    GpioCtrlRegs.GPADIR.bit.GPIO0  = 1;  // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO0);

    GpioCtrlRegs.GPAMUX1.bit.GPIO1 = 0;  // 0=GPIO,  1=EPWM1B
    GpioCtrlRegs.GPADIR.bit.GPIO1  = 1;  // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO1);

    // EPWM2 ===========================================================
    GpioCtrlRegs.GPAMUX1.bit.GPIO2 = 0;  // 0=GPIO,  1=EPWM2A
    GpioCtrlRegs.GPADIR.bit.GPIO2  = 1;  // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO2);

    GpioCtrlRegs.GPAMUX1.bit.GPIO3 = 0;  // 0=GPIO,  1=EPWM2B
    GpioCtrlRegs.GPADIR.bit.GPIO3  = 1;  // 1=OUTput,  0=INput
    IGBT_OFF_SET(GPA, GPIO3);

    EDIS;

    // Пины уже закрыты как GPIO, поэтому возврат мёртвого времени и снятие
    // форсажа идут без спешки и без риска: на ключи они больше не влияют.
    if (igbt_manually)
        bridge_manual_release();

    motor1.flag.bit.pwm_On = 0;
    EINT;
}
//===========================================

void PWM2_On(void)
{

    if (motor2.mbit.bit.AIN_Disable == 1)
    {
        motor2.flag.bit.pwm_On = 0;
        return;
    }
    DINT;

    EALLOW;
    GpioCtrlRegs.GPAMUX1.bit.GPIO14 = 1; // 0=GPIO,  1=EPWM3A,
    GpioCtrlRegs.GPAMUX1.bit.GPIO15 = 1; // 0=GPIO,  1=EPWM3B,
    GpioCtrlRegs.GPAMUX2.bit.GPIO22 = 2;  // 0=GPIO,  2=EPWM4A,
    GpioCtrlRegs.GPAMUX1.bit.GPIO7  = 1; // 0=GPIO,  1=EPWM4B,

    EDIS;

    motor2.flag.bit.pwm_On = 1;
    EINT;
    //------------------------------
}

void PWM1_On(void)
{
    // Во время теста IGBT мёртвое время обойдено (см. Bridge_ManualEnable),
    // и модуляция пошла бы на мост без пауз - сквозной ток на каждом такте.
    // Сначала тест закрыть, потом пускать.
    if ((motor1.mbit.bit.AIN_Disable == 1) || igbt_manually)
    {
        motor1.flag.bit.pwm_On = 0;
        return;
    }
    DINT;

    EALLOW;
    GpioCtrlRegs.GPAMUX1.bit.GPIO0 = 1; // EPWM1A, плечо A верх
    GpioCtrlRegs.GPAMUX1.bit.GPIO1 = 1; // EPWM1B, плечо A низ
    GpioCtrlRegs.GPAMUX1.bit.GPIO2 = 1; // EPWM2A, плечо B верх
    GpioCtrlRegs.GPAMUX1.bit.GPIO3 = 1; // EPWM2B, плечо B низ
    EDIS;

    motor1.flag.bit.pwm_On = 1;
    EINT;
    //--------------------------------
}

//======================================================================
// ТЕСТ IGBT: ручное управление ключами моста (команда 338, Motor_Control:
// Тест -> Тест IGBT). На мост подают малое напряжение, открывают ключи по
// одному или диагональю и смотрят ток по датчику первички.
//
// КАК ВЕДУТСЯ КЛЮЧИ. Пины остаются на EPWM, уровень задаёт непрерывный
// программный форсаж action qualifier (AQCSFRC). Через GPIO, как было на B81,
// нельзя: trip-zone действует на выход модуля EPWM, а пин в режиме GPIO от
// него отрезан - токовая защита компараторов (TRIP5, TRIP_Init) в тесте
// молчала бы. Так она закрывает ключи теста ровно как при работе.
//
// Форсаж стоит ДО блока мёртвого времени, а тот строит B из A. Чтобы ключи
// плеча управлялись порознь, на время теста мёртвое время обходится
// (DBCTL.OUT_MODE = 0). Поэтому сквозной ток в тесте исключает уже не
// железо, а этот код:
//   - маска с обоими ключами одного плеча не исполняется, мост закрывается;
//   - смена ключей идёт в два шага: сначала закрыть лишние, выждать паузу
//     длиннее мёртвого времени, только потом открыть новые;
//   - пуск моста (PWM1_On) во время теста запрещён.
// DBCTL и режим загрузки форсажа сохраняются при входе и возвращаются при
// выходе как были. Выход - любым закрытием моста, см. IGBT1_Disable().
//======================================================================

#if IGBT_ACTIVE_HIGH
#define CSF_ON  2U // форсаж HIGH - ключ открыт
#define CSF_OFF 1U // форсаж LOW  - ключ закрыт
#else
#define CSF_ON  1U
#define CSF_OFF 2U
#endif

// Пауза между закрытием одних ключей и открытием других. Мёртвое время в
// SysConfig - 150 тактов TBCLK, 2 мкс; берём с запасом на выключение IGBT.
#define BRIDGE_SWAP_US 10U

static uint16_t dbctl1Saved, dbctl2Saved;
static uint16_t rldcsf1Saved, rldcsf2Saved;

static uint16_t csf(uint16_t on)
{
    return on ? CSF_ON : CSF_OFF;
}

// Одна запись на модуль: оба ключа плеча меняются разом, без промежуточного
// состояния. RLDCSF = 3 при входе - форсаж действует немедленно, не ждёт нуля
// счётчика.
static void bridge_force(uint16_t mask)
{
    EPwm1Regs.AQCSFRC.all = csf(mask & 0x1U) | (csf(mask & 0x2U) << 2);
    EPwm2Regs.AQCSFRC.all = csf(mask & 0x4U) | (csf(mask & 0x8U) << 2);
    igbt.all = mask;
}

// Зовётся из IGBT1_Disable(), когда пины уже переведены в GPIO и закрыты.
static void bridge_manual_release(void)
{
    EPwm1Regs.AQCSFRC.all = 0U; // форсаж снят, выходы снова от модулятора
    EPwm2Regs.AQCSFRC.all = 0U;
    EPwm1Regs.AQSFRC.bit.RLDCSF = rldcsf1Saved;
    EPwm2Regs.AQSFRC.bit.RLDCSF = rldcsf2Saved;
    EPwm1Regs.DBCTL.all = dbctl1Saved; // мёртвое время на место
    EPwm2Regs.DBCTL.all = dbctl2Saved;

    igbt.all      = 0U;
    igbt_manually = 0U;
}

uint16_t Bridge_ManualEnable(void)
{
    uint16_t s;

    if (igbt_manually)
    {
        // Повторный вход - тот же старт: всё закрыто, авария забыта.
        s = __disable_interrupts();
        bridge_force(0U);
        igbt_tripped = 0U;
        __restore_interrupts(s);
        return 1U;
    }

    // Мост модулирует - отказ. Тест не должен молча гасить работающий
    // инвертор: сначала кнопкой питания, потом тест.
    if (motor1.flag.bit.pwm_On)
        return 0U;

    IGBT1_Disable(); // пины в GPIO, ключи закрыты

    s = __disable_interrupts();

    dbctl1Saved  = EPwm1Regs.DBCTL.all;
    dbctl2Saved  = EPwm2Regs.DBCTL.all;
    rldcsf1Saved = EPwm1Regs.AQSFRC.bit.RLDCSF;
    rldcsf2Saved = EPwm2Regs.AQSFRC.bit.RLDCSF;

    EPwm1Regs.AQSFRC.bit.RLDCSF = 3U;
    EPwm2Regs.AQSFRC.bit.RLDCSF = 3U;
    bridge_force(0U);

    // Обход мёртвого времени: A и B идут с форсажа напрямую.
    EPwm1Regs.DBCTL.bit.OUT_MODE  = 0U;
    EPwm1Regs.DBCTL.bit.OUTSWAP   = 0U;
    EPwm1Regs.DBCTL.bit.DEDB_MODE = 0U;
    EPwm2Regs.DBCTL.bit.OUT_MODE  = 0U;
    EPwm2Regs.DBCTL.bit.OUTSWAP   = 0U;
    EPwm2Regs.DBCTL.bit.DEDB_MODE = 0U;

    igbt_manually = 1U;
    igbt_tripped  = 0U;

    // Пины назад на EPWM - уже под форсажем "всё закрыто".
    EALLOW;
    GpioCtrlRegs.GPAMUX1.bit.GPIO0 = 1;
    GpioCtrlRegs.GPAMUX1.bit.GPIO1 = 1;
    GpioCtrlRegs.GPAMUX1.bit.GPIO2 = 1;
    GpioCtrlRegs.GPAMUX1.bit.GPIO3 = 1;
    EDIS;

    __restore_interrupts(s);
    return 1U;
}

void Bridge_ManualDisable(void)
{
    IGBT1_Disable(); // закрывает пины и сам снимает ручной режим
}

uint16_t Bridge_ManualApply(uint16_t mask)
{
    uint16_t s, keep;

    if (!igbt_manually || igbt_tripped)
        return 0U;

    mask &= BRIDGE_KEYS;

    if (((mask & BRIDGE_LEG_A) == BRIDGE_LEG_A) || ((mask & BRIDGE_LEG_B) == BRIDGE_LEG_B))
    {
        // Сквозная маска. Сюда её не пускает и интерфейс, так что пришла она
        // по ошибке - и самое безопасное, что тут можно сделать, всё закрыть.
        s = __disable_interrupts();
        bridge_force(0U);
        __restore_interrupts(s);
        return 0U;
    }

    keep = igbt.all & mask; // что остаётся открытым

    if (keep != igbt.all)
    {
        s = __disable_interrupts();
        bridge_force(keep);
        __restore_interrupts(s);

        if (mask != keep)
            DEVICE_DELAY_US(BRIDGE_SWAP_US);
    }

    if (mask != keep)
    {
        // Авария могла прийти за время паузы - тогда не открываем.
        s = __disable_interrupts();
        if (!igbt_tripped)
            bridge_force(mask);
        __restore_interrupts(s);
    }

    return igbt_tripped ? 0U : 1U;
}

// Из INT_EPWM2_Iu_TZ_ISR. Сам trip-zone ключи уже закрыл, но его защёлку
// фон снимает раз в секунду (TRIP_Clear), и форсаж открыл бы ключи снова -
// на тот же ток. Поэтому форсаж в ноль, а дальнейшие маски не исполняются
// до нового входа в тест.
void Bridge_ManualTrip(void)
{
    bridge_force(0U);
    igbt_tripped = 1U;
}

// Слово состояния для Motor_Control: биты 0..3 - открытые ключи (VT1..VT4),
// бит 8 - тест включён, бит 9 - тест прерван токовой защитой.
uint16_t Bridge_ManualState(void)
{
    return igbt.all | (igbt_manually ? 0x0100U : 0U) | (igbt_tripped ? 0x0200U : 0U);
}

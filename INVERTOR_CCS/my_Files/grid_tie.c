/*
 * grid_tie.c
 *
 *  Created on: 17.08.2026
 *      Author: Oleg Pavlovsky
 *
 *  Отдача мощности в сеть: токовый контур (grid-following).
 *
 *  Топология: АКБ 24..60 В -> H-мост (EPWM1 = плечо A, EPWM2 = плечо B)
 *  -> повышающий трансформатор 50 Гц 3 кВт -> сеть 220 В.
 *
 *  Синхронизация (частота, фаза, амплитуда) живёт не здесь, а в 1ph_Sin.c
 *  и myADC.c - см. my_Files/U220_sync.txt. Отсюда берётся только готовый
 *  угол сети theta = sineB.angle + sineB.offset.
 *
 *  Почему контур тока, а не напряжения: два источника напряжения, включённые
 *  параллельно, обмениваются током, равным их разности, делённой на импеданс.
 *  Управлять этим током нельзя - можно только надеяться. Источник тока задаёт
 *  ток явно, и мощность становится уставкой, а не следствием.
 *
 *  Подробное описание - my_Files/grid_tie.txt
 */

#include "board.h"
#include <math.h>
#include "F28P55x_Device.h"

#include "KSP.h"     // asut(), Errors.h, ErrorCodes.h
#include "myADC.h"   // kadcf - масштабы каналов АЦП
#include "1ph_Sin.h" // sineB: угол сети от PLL
#include "grid_tie.h"

//---------------------------------------------------------------------------
// Внешние связи
//---------------------------------------------------------------------------
extern uint16_t daca;        // 1ph_Sin.c: имитация U220 (стенд)
extern float    u220freq;    // 1ph_Sin.c: измеренная частота сети, Гц
extern float    phaseErr;    // 1ph_Sin.c: ошибка фазы в единицах sin()
extern float    u220amp;     // myADC.c:   амплитуда сети, коды АЦП
extern uint16_t u220_kod;    // myADC.c:   сырой отсчёт канала U220, 0..4095
extern uint16_t u220hi_kod;  // myADC.c:   верхняя губа вилки огибающей
extern uint16_t u220low_kod; // myADC.c:   нижняя губа вилки огибающей

extern void IGBT1_Disable(void);
extern void IGBT2_Disable(void);

struct GridTie gt;
uint16_t       gridMode = 0; // 0 = старый режим синусоиды, 1 = сетевой

// TBCLK1 = 150 МГц / 2 (HSPCLKDIV=2 по сбросу) = 75 МГц, режим UP_DOWN даёт
// 2*TBPRD тактов на период. Отсюда Ts = TBPRD / 37.5e6.
#define TB1_TICKS_PER_SECOND_HALF 37.5e6f

//---------------------------------------------------------------------------
// Начальные значения.
//
// Все коэффициенты - float в структуре gt, чтобы крутить их в отладчике
// на живой цели, не пересобирая проект.
//
// Kp = 2*PI*f_bw*L, где f_bw ~ 300 Гц - потолок полосы при Ts = 200 мкс
// и полной задержке контура около 1.5 такта (АЦП стартует по тому же
// событию, что и прерывание, поэтому в начале ISR готов отсчёт предыдущего
// периода; плюс полпериода на загрузку CMPA из теневого регистра).
//
// Ki = Kp*w_r, w_r ~ 2*PI*20 - нулевая ошибка на 50 Гц примерно за 50 мс.
//---------------------------------------------------------------------------
void grid_tie_init(void)
{
    gt.State       = GT_OFF;
    gt.fault       = 0;
    gt.faultNew    = 0;
    gt.shutdownReq = 0;
    gt.syncCnt     = 0;
    gt.syncNeed    = 25; // 25 периодов сети = 0.5 с
    gt.startDelay  = 0;
    gt.acContactor = 0;

    gt.IsetRef  = 0.0f;
    gt.Iset     = 0.0f;
    gt.IsetSlew = 0.1f; // А за период сети: с нуля до 5 А за секунду
    gt.Iq       = 0.0f;

    gt.u220Zero = 2048.0f; // до первого захвата; дальше - середина вилки
    gt.kU220    = 0.152f;  // В/код: 311 В пика на 2048 кодов. Уточнить при тарировке
    gt.n     = 7.0f;   // Uвтор/Uперв
    gt.iSign = 1.0f;

    gt.UdcSrc   = 0;     // на стенде АКБ нет - берём константу
    gt.UdcFixed = 48.0f;
    gt.Udc      = 48.0f;

    gt.pr.Kp   = 15.0f;   // = 2*PI*300*0.008
    gt.pr.Ki   = 1900.0f; // = Kp * 2*PI*20
    gt.pr.KiTs = 0.0f;    // считается в grid_tie_onGridZero()
    gt.pr.leak = 0.002f;
    gt.pr.step = 0.0f;
    gt.pr.x1   = 0.0f;
    gt.pr.x2   = 0.0f;
    gt.pr.xMax = 100.0f; // В на вторичке: падение на рассеянии много меньше
    gt.pr.out  = 0.0f;

    gt.mMax = 0.95f;
    gt.m    = 0.0f;

    gt.mDC     = 0.0f;
    gt.kDC     = 0.0005f; // установление около секунды
    gt.mDCmax  = 0.02f;
    gt.iPriAcc = 0.0f;
    gt.iPriCnt = 0;

    gt.ImaxPri   = 100.0f; // уточнить по датчику первички
    gt.ImaxSec   = 25.0f;  // 3 кВт -> 19.3 А пика, запас 30 %
    gt.UdcMin    = 20.0f;
    gt.UdcMax    = 65.0f;
    gt.freqMin   = 47.0f;
    gt.freqMax   = 53.0f;
    gt.ampMin    = 1200.0f;
    gt.ampMax    = 2000.0f;
    gt.phaseOk   = 0.10f;
    gt.satCount  = 0;
    gt.satLimit  = 10; // из 100 тактов периода
    gt.satLast   = 0;
    gt.noGrid    = 0;
    gt.noGridMax = 150; // 1.5 периода сети

    gt.Ts     = 200e-6f;
    gt.refSrc = 0; // стенд: опора - daca

    gt.simPlant    = 1; // железа нет, работаем на модели
    gt.Lsim        = 0.008f;
    gt.Rsim        = 0.5f;
    gt.iSim        = 0.0f;
    gt.uDCinject   = 0.0f;
    gt.uSecApplied = 0.0f;
}

//---------------------------------------------------------------------------
// Авария. Вызывается из прерывания, поэтому здесь ничего тяжёлого:
// закрыть выход (m = 0 даст нулевое напряжение на мосту в течение одного
// периода ШИМ) и оставить заявку фону. Ни IGBT*_Disable(), ни
// RegisterSessionEvent() из ISR звать нельзя - первые делают EINT внутри
// себя (вложенное прерывание посреди ISR), вторая лезет в журнал и историю.
//---------------------------------------------------------------------------
void grid_tie_fault(uint16_t code)
{
    if (gt.State != GT_FAULT)
    {
        gt.fault    = code;
        gt.faultNew = 1;
    }
    gt.State       = GT_FAULT;
    gt.Iset        = 0.0f;
    gt.m           = 0.0f;
    gt.acContactor = 0;
    gt.shutdownReq = 1;

    gt.pr.x1 = 0.0f;
    gt.pr.x2 = 0.0f;
}

void grid_tie_start(void)
{
    if (gt.State == GT_OFF)
    {
        gt.syncCnt     = 0;
        gt.startDelay  = 0;
        gt.acContactor = 0; // замкнётся только после набора синхронизма
        gt.Iset        = 0.0f;
        gt.pr.x1       = 0.0f;
        gt.pr.x2       = 0.0f;
        gt.mDC         = 0.0f;
        gt.iSim        = 0.0f;
        gt.State       = GT_SYNC;
    }
}

void grid_tie_stop(void)
{
    gt.State       = GT_OFF;
    gt.fault       = 0;
    gt.faultNew    = 0;
    gt.Iset        = 0.0f;
    gt.m           = 0.0f;
    gt.syncCnt     = 0;
    gt.startDelay  = 0;
    gt.acContactor = 0;
    gt.pr.x1       = 0.0f;
    gt.pr.x2       = 0.0f;
    gt.shutdownReq = 1;
}

//---------------------------------------------------------------------------
// Фоновая часть: то, чему не место в прерывании.
// Вызывается из while(1) в main.c.
//---------------------------------------------------------------------------
void grid_tie_background(void)
{
    if (gt.shutdownReq)
    {
        gt.shutdownReq = 0;
        IGBT1_Disable();
        IGBT2_Disable();
    }

    if (gt.faultNew)
    {
        gt.faultNew = 0;
        RegisterSessionEvent(gt.fault);
    }
}

//---------------------------------------------------------------------------
// Такт контура тока. Вызывается из INT_myEPWM1_ISR, 100 раз за период сети.
//
// Порядок важен: сначала модель отрабатывает ПРЕДЫДУЩИЙ индекс модуляции
// (тот, что реально стоял в CMPA последний период), и только потом считается
// новый. Иначе модель окажется на такт быстрее реальности.
//---------------------------------------------------------------------------
#pragma CODE_SECTION(grid_tie_step, ".TI.ramfunc");

void grid_tie_step(void)
{
    //-----------------------------------------------------------------
    // 1. Измерения
    //-----------------------------------------------------------------
    // Напряжение сети. Берём СЫРОЙ отсчёт и вычитаем измеренный ноль.
    // ADCPPB1RESULT здесь не годится: в ADCPPB1OFFREF лежит калибровочный
    // офсет канала (110), а не середина шкалы, так что "знаковый" результат
    // PPB несёт постоянное смещение почти в 2000 кодов. С ним индекс
    // модуляции полпериода стоит в упоре по mMax.
    gt.uGridKod = (int16_t)((float)u220_kod - gt.u220Zero);
    gt.uGrid    = gt.kU220 * (float)gt.uGridKod;

    // Напряжение АКБ. Мгновенное, не сглаженное - см. комментарий в grid_tie.h.
    if (gt.UdcSrc)
    {
        gt.Udc = kadcf.Vin * (float)(int16_t)AdcaResultRegs.ADCPPB2RESULT.bit.PPBRESULT;
    }
    else
    {
        gt.Udc = gt.UdcFixed;
    }

    // Токи. ADCB стартует по тому же событию EPWM1, что и это прерывание,
    // поэтому здесь готов результат ПРЕДЫДУЩЕГО периода. Это осознанная
    // задержка на такт, она заложена в расчёт Kp.
    gt.iPriKod = (int16_t)AdcbResultRegs.ADCPPB1RESULT.bit.PPBRESULT;
    gt.iSecKod = (int16_t)AdcbResultRegs.ADCPPB2RESULT.bit.PPBRESULT;
    gt.iPri    = gt.iSign * kadcf.Ia * (float)gt.iPriKod;
    gt.iSec    = gt.iSign * kadcf.Ib * (float)gt.iSecKod;

    //-----------------------------------------------------------------
    // 2. Модель вместо железа
    //-----------------------------------------------------------------
    if (gt.simPlant)
    {
        gt.uSecApplied = gt.m * gt.n * gt.Udc;

        if (gt.acContactor)
        {
            // L*di/dt = u_мост - u_сеть - R*i, всё приведено к вторичке.
            // uDCinject имитирует асимметрию мёртвого времени.
            gt.iSim += (gt.uSecApplied + gt.uDCinject - gt.uGrid - gt.Rsim * gt.iSim) *
                       (gt.Ts / gt.Lsim);
        }
        else
        {
            // Контактор разомкнут - цепи нет, ток равен нулю по определению.
            // Мост при этом модулирует вхолостую и набирает сетевое напряжение,
            // чтобы к моменту замыкания разность на контактах была нулевой.
            gt.iSim = 0.0f;
        }

        gt.iSec = gt.iSim;
        gt.iPri = gt.iSim * gt.n;
    }

    //-----------------------------------------------------------------
    // 3. Защиты. Работают всегда, в любом состоянии.
    //-----------------------------------------------------------------
    if (++gt.noGrid > gt.noGridMax)
    {
        // Захватов eCAP нет больше полутора периодов. Без этой проверки
        // TBPRD и step замирают на последних значениях, а мост продолжает
        // работать "по памяти" - в сеть так нельзя.
        gt.noGrid = gt.noGridMax; // не даём переполниться
        if (gt.State == GT_RAMP || gt.State == GT_RUN)
            grid_tie_fault(gt_noGrid);
    }

    if (gt.State == GT_RAMP || gt.State == GT_RUN)
    {
        if (fabsf(gt.iPri) > gt.ImaxPri)
            grid_tie_fault(gt_overIpri);
        else if (fabsf(gt.iSec) > gt.ImaxSec)
            grid_tie_fault(gt_overIsec);
        else if (gt.UdcSrc && ((gt.Udc < gt.UdcMin) || (gt.Udc > gt.UdcMax)))
            grid_tie_fault(gt_UdcRange);
    }

    //-----------------------------------------------------------------
    // 4. Контур тока
    //-----------------------------------------------------------------
    // GT_SYNC тоже считает контур, но с нулевой уставкой. Смысл: мост уже
    // модулирует и активно держит нулевой ток, то есть его напряжение само
    // подстраивается под сетевое. К моменту замыкания контактора разность
    // на его контактах нулевая, и переход в GT_RAMP получается безударным -
    // уставка просто начинает расти от нуля.
    if (gt.State == GT_SYNC || gt.State == GT_RAMP || gt.State == GT_RUN)
    {
        // Угол сети - тот же, по которому идёт вся синхронизация.
        // Берём ДО приращения, как это делает sine_invertor1().
        float theta = sineB.angle + sineB.offset;
        float s     = sinf(theta);

        gt.iref = (gt.State == GT_SYNC) ? 0.0f : (gt.Iset * s);
        if ((gt.Iq != 0.0f) && (gt.State != GT_SYNC))
            gt.iref += gt.Iq * cosf(theta);

        gt.ierr = gt.iref - gt.iSec;

        // PR: пропорциональная часть плюс резонатор ровно на частоте сети.
        // Состояние сохраняем - при упоре по m его придётся откатить.
        float x1old = gt.pr.x1;
        float x2old = gt.pr.x2;

        gt.pr.x1 += gt.pr.step * gt.pr.x2 + gt.pr.KiTs * gt.ierr - gt.pr.leak * gt.pr.x1;
        gt.pr.x2 -= gt.pr.step * gt.pr.x1;

        if (gt.pr.x1 > gt.pr.xMax)
            gt.pr.x1 = gt.pr.xMax;
        else if (gt.pr.x1 < -gt.pr.xMax)
            gt.pr.x1 = -gt.pr.xMax;

        gt.pr.out = gt.pr.Kp * gt.ierr + gt.pr.x1;

        // Прямая связь по сети снимает с регулятора всю основную синусоиду:
        // PR остаётся отрабатывать только падение на индуктивности рассеяния,
        // а это единицы вольт.
        gt.uSec = gt.uGrid + gt.pr.out;

        // Коэффициент трансформации входит ровно один раз, здесь.
        float denom = gt.n * gt.Udc;
        gt.m        = (denom > 1.0f) ? (gt.uSec / denom + gt.mDC) : 0.0f;

        if ((gt.m > gt.mMax) || (gt.m < -gt.mMax))
        {
            gt.m = (gt.m > 0.0f) ? gt.mMax : -gt.mMax;

            // Упор. Интегратор в упоре наматывать не должен - откатываем.
            gt.pr.x1 = x1old;
            gt.pr.x2 = x2old;
            gt.satCount++;
        }

        // Накопитель среднего тока первички для контура подавления DC.
        gt.iPriAcc += gt.iPri;
        gt.iPriCnt++;
    }
    else
    {
        // GT_OFF / GT_FAULT: оба плеча на 50 %, напряжение моста равно нулю.
        gt.m      = 0.0f;
        gt.pr.out = 0.0f;
    }

    // Отсчёт четверти периода от нуля напряжения до замыкания контактора -
    // см. взвод startDelay в grid_tie_onGridZero(). Поток есть интеграл
    // напряжения, поэтому замыкание в максимуме даёт нулевое начальное
    // смещение потока.
    if ((gt.State == GT_SYNC) && (gt.startDelay > 0))
    {
        if (--gt.startDelay == 0)
        {
            gt.Iset        = 0.0f;
            gt.acContactor = 1;
            gt.State       = GT_RAMP;
        }
    }

    //-----------------------------------------------------------------
    // 5. Вывод в компараторы
    //
    // Режим UP_DOWN, UP_CMPA -> HIGH, DOWN_CMPA -> LOW, значит доля высокого
    // уровня плеча = 1 - CMPA/TBPRD. При CMPA_A = TBPRD*(1-m)/2 получается
    // dA = (1+m)/2, dB = (1-m)/2 и U_AB = m*Udc.
    //
    // Окна открытого состояния обоих плеч отцентрированы по вершине счётчика,
    // поэтому узкое вложено в широкое: U_AB принимает только 0 и +Udc (либо
    // 0 и -Udc). Это трёхуровневая модуляция, частота пульсации вдвое выше
    // несущей. Ломать это соотношение CMPA нельзя.
    //-----------------------------------------------------------------
    {
        uint16_t tbprd = EPwm1Regs.TBPRD;
        float    ca    = (float)tbprd * 0.5f * (1.0f - gt.m);

        if (ca < 0.0f)
            ca = 0.0f;
        else if (ca > (float)tbprd)
            ca = (float)tbprd;

        cmpaA = (uint16_t)ca;
        cmpaB = tbprd - cmpaA;

        EPwm1Regs.CMPA.bit.CMPA = cmpaA;
        EPwm2Regs.CMPA.bit.CMPA = cmpaB;
    }

    //-----------------------------------------------------------------
    // 6. Угол.
    //
    // В обычном режиме его двигают sine_invertor1/2. Раз мы их заменили,
    // двигать обязаны сами - иначе встанет весь PLL: и подстройка фазы,
    // и dacb, и детектор нуля.
    //-----------------------------------------------------------------
    sineB.angle = asut(sineB.angle + sineB.step);
}

//---------------------------------------------------------------------------
// Раз за период сети, из INT_U220_zero_ECAP_ISR.
// Здесь живёт всё медленное: автомат, нарастание уставки, контур DC.
//---------------------------------------------------------------------------
void grid_tie_onGridZero(void)
{
    // Сеть есть - сторож перезаряжен.
    gt.noGrid = 0;

    //-----------------------------------------------------------------
    // Пересчёт периода дискретизации и коэффициентов, зависящих от него.
    // TBPRD правится тем же прерыванием при каждом захвате, так что Ts
    // надо освежать здесь же.
    //-----------------------------------------------------------------
    gt.Ts      = (float)EPwm1Regs.TBPRD * (1.0f / TB1_TICKS_PER_SECOND_HALF);
    gt.pr.KiTs = gt.pr.Ki * gt.Ts;
    gt.pr.step = sineB.step; // резонанс автоматически едет за частотой сети

    // Ноль канала U220 - середина вилки огибающей. Вилка живёт в myADC.c
    // и обхватывает реальный сигнал сверху и снизу, так что её середина и
    // есть измеренный ноль. Обновляем раз за период: чаще незачем, а к
    // калибровочному офсету PPB привязываться нельзя (см. grid_tie.h).
    gt.u220Zero = ((float)u220hi_kod + (float)u220low_kod) * 0.5f;

    //-----------------------------------------------------------------
    // Контур подавления постоянной составляющей.
    //
    // Работает по току ПЕРВИЧКИ: только там виден ток намагничивания.
    // С трансформатором на выходе DC-смещение не даёт просто постоянный
    // ток, как с дросселем, - оно намагничивает сердечник, и тот за
    // несколько периодов уходит в насыщение.
    //-----------------------------------------------------------------
    if (gt.iPriCnt > 0)
    {
        gt.iPriMean = gt.iPriAcc / (float)gt.iPriCnt;
        gt.iPriAcc  = 0.0f;
        gt.iPriCnt  = 0;

        if (gt.State == GT_RAMP || gt.State == GT_RUN)
        {
            gt.mDC -= gt.kDC * gt.iPriMean;

            if (gt.mDC > gt.mDCmax)
                gt.mDC = gt.mDCmax;
            else if (gt.mDC < -gt.mDCmax)
                gt.mDC = -gt.mDCmax;
        }
    }

    //-----------------------------------------------------------------
    // Качество синхронизма за прошедший период
    //-----------------------------------------------------------------
    uint16_t gridOk = ((u220freq >= gt.freqMin) && (u220freq <= gt.freqMax) &&
                       (u220amp >= gt.ampMin) && (u220amp <= gt.ampMax) &&
                       (fabsf(phaseErr) < gt.phaseOk))
                          ? 1U
                          : 0U;

    //-----------------------------------------------------------------
    // Систематический упор по m - это не помеха, а диагноз: либо просел
    // АКБ, либо мал коэффициент трансформации, либо сердечник не тянет.
    // Тихо портить форму тока в такой ситуации нельзя.
    //-----------------------------------------------------------------
    gt.satLast  = gt.satCount;
    gt.satCount = 0;

    switch (gt.State)
    {
        case GT_SYNC:
            if (gridOk)
            {
                if (gt.syncCnt < gt.syncNeed)
                    gt.syncCnt++;

                // Условие взвода - именно ">=", а не "==". syncNeed объявлен
                // крутилкой отладчика, и если его понизить на ходу, счётчик
                // окажется уже выше порога: со строгим равенством пуск после
                // этого не наступит никогда.
                // Повторный взвод невозможен: замыкание контактора выводит
                // автомат из GT_SYNC и ставит acContactor.
                if ((gt.syncCnt >= gt.syncNeed) && (gt.startDelay == 0) && (gt.acContactor == 0))
                {
                    // Пуск смещён на четверть периода от нуля напряжения,
                    // то есть в его максимум: поток - интеграл напряжения,
                    // и замыкание в максимуме даёт нулевое начальное смещение.
                    // Досчитывает startDelay уже grid_tie_step().
                    gt.startDelay = sineB.resolution / 4;
                }
            }
            else
            {
                gt.syncCnt    = 0;
                gt.startDelay = 0;
            }
            break;

        case GT_RAMP:
            if (!gridOk)
            {
                grid_tie_fault(gt_syncLost);
                break;
            }
            if (gt.satLast > gt.satLimit)
            {
                grid_tie_fault(gt_mSat);
                break;
            }

            // Рампа в обе стороны. Скачок вниз перегрузки не даёт, но резкий
            // сброс тока - это ступенька напряжения на индуктивности рассеяния
            // и лишний бросок в сердечник. Спускаемся так же плавно.
            if (gt.Iset < gt.IsetRef)
            {
                gt.Iset += gt.IsetSlew;
                if (gt.Iset >= gt.IsetRef)
                {
                    gt.Iset  = gt.IsetRef;
                    gt.State = GT_RUN;
                }
            }
            else if (gt.Iset > gt.IsetRef)
            {
                gt.Iset -= gt.IsetSlew;
                if (gt.Iset <= gt.IsetRef)
                {
                    gt.Iset  = gt.IsetRef;
                    gt.State = GT_RUN;
                }
            }
            else
            {
                gt.State = GT_RUN;
            }
            break;

        case GT_RUN:
            if (!gridOk)
            {
                grid_tie_fault(gt_syncLost);
                break;
            }
            if (gt.satLast > gt.satLimit)
            {
                grid_tie_fault(gt_mSat);
                break;
            }

            // Уставку можно менять на ходу, но только через нарастание.
            if (gt.Iset != gt.IsetRef)
                gt.State = GT_RAMP;
            break;

        case GT_OFF:
        case GT_FAULT:
        default:
            gt.syncCnt = 0;
            break;
    }
}

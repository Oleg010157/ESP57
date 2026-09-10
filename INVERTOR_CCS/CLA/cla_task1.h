#ifndef DCDC_H
#define DCDC_H

#include <stdint.h>

//---------------------------------------------------------------------------
// Заготовка контура DC/DC.
//
// Сам контур снят с Cla1Task1: в K57 для него нет ни своего ШИМ (выходные пины
// назначены только EPWM1/EPWM2 - это мост инвертора), ни отдельного канала
// измерения Udc. Подробно - в шапке CLA/Task1.cla.
//
// Файл оставлен ради этих двух функций: когда у преобразователя появится
// железо, регулятор будет собираться из них, а не писаться заново. Обе
// static inline, так что неиспользуемыми ничего не стоят.
//
// Структуры DCShared, PI_Controller и DutyControl вместе с объектами dc, pi и
// dctl удалены. Результаты измерений теперь плоские переменные в cla_shared.h.
//---------------------------------------------------------------------------

typedef struct {
    float Kp;
    float Ki;
    float integral;
    float outMin;
    float outMax;
} PI_Controller;

static inline float
PI_update(PI_Controller *pi, float setpoint, float measured)
{
    float error = setpoint - measured;
    pi->integral += error;

    // Анти-насыщение интегратора
    if (pi->integral > pi->outMax) pi->integral = pi->outMax;
    if (pi->integral < pi->outMin) pi->integral = pi->outMin;

    float output = pi->Kp * error + pi->Ki * pi->integral;

    // Ограничение выхода
    if (output > pi->outMax) output = pi->outMax;
    if (output < pi->outMin) output = pi->outMin;

    return output;
}

// ограничение скорости изменения duty
static inline float slewLimit(float target, float current, float maxStep)
{
    float delta = target - current;

    if (delta > maxStep)
        return current + maxStep;
    else if (delta < -maxStep)
        return current - maxStep;
    else
        return target;
}

#endif // DCDC_H

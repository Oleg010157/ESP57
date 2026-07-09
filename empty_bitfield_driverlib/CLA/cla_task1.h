#ifndef DCDC_H
#define DCDC_H

#include <stdint.h>
#include "f28p55x_device.h"
#include "myADC.h"

typedef struct
{
    int16_t Udc_kod;  //код напряжения звена постоянного тока после ац преобразования и смещения нуля.
    float   Udc_volt; // напряжение звена постоянного тока, Вольт.
    float   Udc_rf;   // сглаженный Udc_volt, Вольт
    float   Udc_ref;   // заданный Udc_volt, Вольт
   
    int   Idc_kod; //код тока на выходе DCDC преобразователя после ац преобразования и смещения нуля.нуля.
    float Idc_amp; // ток фазы А, Ампер.
    float Idc_rf;  // сглаженный Idc_amp, Ампер
    float Idc_sum; // сумма квадратов Idc_amp,
    float Idc_rms; // сглаженный Idc_amp,
    float Idc_max;  // максимальный ток Idc_amp, Ампер

    int16_t Vin_kod;  //код напряжения на входе DC/DC преобразователя.
    float   Vin_volt; // напряжение на входе DC/DC преобразователя, Вольт.
    float   Vin_rf;   // сглаженный Vin_volt, Вольт
    

} DCShared;

extern volatile DCShared dc;

// API
void DCDC_init(void);
void DCDC_setDuty(float duty);   // duty [0..1]
void DCDC_faultStop(void);       // аварийная остановка

typedef struct {
    float Kp;
    float Ki;
    float integral;
    float outMin;
    float outMax;
} PI_Controller;
extern PI_Controller pi;

typedef enum
{
    STATE_PRECHARGE,
    STATE_RUN
} DCDC_State;

typedef struct
{
    uint16_t State;          // STATE_PRECHARGE = 0, STATE_RUN = 1
    uint32_t prechargeTicks; // счётчик тиков для выдержки запуска и выхода на режим
    uint32_t prechargeLimit; // = 100000; // 50 кГц * 2 сек Время выхода на режим в тиках
   
    float    dutyControl;    // = 0; // переменная управления скважностью мостовой схемы ключей
    float    duty;           // скважностью мостовой схемы ключей
   
    uint16_t CMPA;

} DutyControl;
extern DutyControl dctl;

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

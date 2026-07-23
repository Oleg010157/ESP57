/*
 * REF_Kadr.c
 *
 *  Created on: 25 июл. 2017 г.
 *      Author: olegs.pavlovskis
 */
#include "KSP.h"


#include "board.h"
#include "myDCAN.h"
#include "Errors.h"
#include "myADC.h"
#include "cla_shared.h"


extern motor *mK60;
extern double IGBT_T[20]; // массив значений температуры транзисторов IGBT
extern uint16_t canLossCounter;
extern STEND_bit sbit;
extern REFs REF;
extern uint16_t cantxdata[8];

char data[8];
const uint16_t *pd;

volatile union word32 txA, txB;
float dbgP1a, dbgP2a;

int P15Kadr = -1; // число определяет набор параметров 15-го MBOX
int P16Kadr = -1;   // число определяет набор параметров 16-го MBOX
int P8Kadr = -1;   // число определяет набор параметров 16-го MBOX
int P101Kadr = -1;   // число определяет набор параметров 101-го MBOX
int P102Kadr = -1;   // число определяет набор параметров 102-го MBOX
int P106Kadr = -1;   // число определяет набор параметров 102-го MBOX

extern union PCflag sharp;
union CCS ccs;
extern SHR_Flags shrTrip;

const uint16_t *msgData;



//#pragma CODE_SECTION(send_REF_kadr, ".text:board")

void send_REF_kadr()
{
    uint16_t d1, d2, d3, d4;

    // Консолидированный медленный кадр: 8 блоков x 4 слова = 32 слова = 64 байта.
    // Каждый блок пишется в свой срез tele[] вместо отдельного Send_mbox; в конце — один
    // Send_mbox_FD(6, ...). Порядок блоков = порядок исполнения ниже (100,101,102,106,107,
    // 109,118,116). Нулевая инициализация: если какой-то #if-блок выключен, его слот = 0.
    // d1..d4 намеренно остаются общими локальными — перенос значений между блоками сохранён
    // как в исходном коде (некоторые case задают не все d1..d3).
    uint16_t tele[32] = {0};

    //100 -------------------------------------------------------------------------
#if (ref_kadr_mask & BIT5) == BIT5    // Отправляем ошибки и флаги
  
    d1 = overload.all; 
    d2 = motor2.error.all; //ccs.u16.u2;
    d3 = shr.all;
    d4 = shrTrip.all;
    shrTrip.all = 0xFFFF; // сбрасываем флаги аварийнного отключения
    tele[0] = d1; tele[1] = d2; tele[2] = d3; tele[3] = d4;   // блок 0 -> ID 100

#endif

// 101 MsgID -В этом кадре в UI выводятся медленные параметры DC-DC конвертора  ----------------------------------------------------------------
#if (ref_kadr_mask & BIT6)==BIT6

    d4 = ++P101Kadr;

    switch (P101Kadr)
    {
    case 0:
        d1 = rounding(cla.Ia_rf * 100);
        d2 = rounding(cla.Ib_rf * 100);
        d3 = rounding(cla.Ic_rf * 100);
        break;
    case 1:
        d1 = (uint16_t) (cla.Ia_rms * 100 + 0.5);
        d2 = (uint16_t) (cla.Ib_rms * 100 + 0.5);
        d3 = (uint16_t) (cla.Ic_rms * 100 + 0.5);
        break;
    case 2:
        d1 = rounding(Vin_volt * 100);
        d2 = (uint16_t) (Vin_rf *100);
        d3 = (uint16_t) (motor1.mSabc + 0.5);
        break;
    case 3:
        d1 = rounding(Udc_volt * 10); 
        d2 = rounding(Udc_rf * 10);
        d3 = (uint16_t) (motor2.clark.Um * 10 + 0.5);
        break;
    case 4:
        d1 = rounding(Idc_amp * 10);
        d2 = rounding(Idc_rf * 10);
        d3 = rounding(Idc_rms * 10);
        P101Kadr = -1;
        break;
    }
    tele[4] = d1; tele[5] = d2; tele[6] = d3; tele[7] = d4;   // блок 1 -> ID 101 (цикл P101Kadr)

#endif //  if (ref_kadr_mask & BIT6)==BIT6

// 102  MsgID ------------------------------------------------------------------------------------
#if (ref_kadr_mask & BIT7)==BIT7

    d4 = ++P102Kadr;
    switch (d4)
    {
    case 0:
        d1 = rounding(cla.Ia_rf * 100);
        d2 = rounding(cla.Ib_rf * 100);
        d3 = rounding(cla.Ic_rf * 100);
        break;

    case 1:
        d1 = (uint16_t) (cla.Ia_rms * 100 + 0.5);
        d2 = (uint16_t) (cla.Ib_rms * 100 + 0.5);
        d3 = (uint16_t) (cla.Ic_rms * 100 + 0.5);
        break;

    case 2:
        d1 = Udc_kod; 
        d2 = rounding(motor2.pabc_rf);
        d3 = (uint16_t) (motor2.mSabc + 0.5);
        break;

    case 3:
        d1 = rounding(Udc_rf * 10);
        d2 = rounding(motor2.slip.absF * 1000);
        d3 = rounding(motor2.clark.UmF * 50);
        break;

    case 4:
        d1 = core_temp;
        d2 = core_temp;
        d3 = core_temp;
        P102Kadr = -1;
        break;

    }
    tele[8] = d1; tele[9] = d2; tele[10] = d3; tele[11] = d4; // блок 2 -> ID 102 (цикл P102Kadr)

#endif //  if (ref_kadr_mask & BIT7)==BIT7

// 106 MsgID -------------------------------------------------------------
#if (ref_kadr_mask & BIT9)==BIT9

    d4 = ++P106Kadr;
    switch (d4)
    {
    case 0:
        d1 = rounding(motor1.park.IdF * 100);
        d2 = rounding(motor1.park.IqF * 100);
        d3 = rounding(motor2.park.IdF * 100);
        break;

    case 1:
        d1 = rounding(motor2.park.IqF * 100);
        d2 = rounding(motor1.park.VdF * 10); // среднее значение тока от 2-го выпрямителя
        d3 = rounding(motor1.park.VqF * 10);
        break;
    case 2:
        d1 = rounding(motor2.park.VdF * 10);
        d2 = rounding(motor2.park.VqF * 10);
        //          d3 = rounding(motor2.ipark.Vd_max * 10);
        //          p1063 = rounding(motor2.ipark.Vq_max * 10);
        break;
    case 3:
        d1 = rounding(Idc_rf * 100); // среднее значение тока от 1-го выпрямителя
        d2 = rounding(Idc_rms * 100); // среднее значение тока от 2-го выпрямителя
        d3 = rounding(stend.Pa_stend); // активная мощность от или к источнику питания
        P106Kadr = -1;
        break;
    }
    tele[12] = d1; tele[13] = d2; tele[14] = d3; tele[15] = d4; // блок 3 -> ID 106 (цикл P106Kadr)
#endif

    // 107  MsgID -------------------------------------------------------------
#if ((ref_kadr_mask & BIT8) == BIT8)

    d1 = rounding(motor1.ramp.ref * 0.02f); // заданная частота поля статора speedRef+sAbsfref1;
    d2 = rounding(motor1.freq * 5);  // фактическая частота поля
    d3 = rounding(motor2.ramp.ref * 0.02f); // заданная частота поля статора speedRef+sAbsfref1;
    d4 = rounding(motor2.freq * 5);  // фактическая частота поля

    tele[16] = d1; tele[17] = d2; tele[18] = d3; tele[19] = d4; // блок 4 -> ID 107 (частоты поля)

#endif
//BIT9   MsgID = 115  10-ый  Mbox для передачи кодов ошибок в фоне.

//BIT10  MsgID = 109  
#if (ref_kadr_mask & BIT10)==BIT10

    d1 = (uint16_t) (motor1.ipark.gain * 1000 + 0.5); //выходное результирующее значение индекса модуляции напряжения;
    d2 = (uint16_t) (motor2.ipark.gain * 1000 + 0.5); //выходное результирующее значение индекса модуляции напряжения;
    d3 = motor1.flag.all;
    d4 = motor2.flag.all;
    tele[20] = d1; tele[21] = d2; tele[22] = d3; tele[23] = d4; // блок 5 -> ID 109 (gain/flags)
#endif



// 118  MsgID выводим float суммарной по всем трём фазам активной мощности, Ватт
#if (ref_kadr_mask & BIT11)==BIT11
    {
        union word32 f118L, f118H;

        f118L.f32 = motor1.mPabc;
        f118H.f32 = motor2.mPabc;
        d1 = f118L.u16.Low;
        d2 = f118L.u16.Hi;
        d3 = f118H.u16.Low;
        d4 = f118H.u16.Hi;
        tele[24] = d1; tele[25] = d2; tele[26] = d3; tele[27] = d4; // блок 6 -> ID 118 (мощность)
    }
#endif

//------------------------------------------------------------------------------
// 116  MsgID  для передачи медленных параметров
#if (ref_kadr_mask & BIT12)==BIT12

    d4 = ++P16Kadr;
    switch (P16Kadr)
    {
    case 0:
        d1 = motor1.mode.all;
        d2 = motor2.mode.all;
        d3 = sbit.all;
        break;

    case 1:
        d2 = rounding(mK60->rg.em2 * 1000);
        d3 = mK60->rg.offset;

        break;

    case 2:
        //       d1 = mK60->qep.angle;       //rounding(mK60->piRPM.i1*10);
        d2 = rounding(mK60->piId.i1 * 10);
        d3 = rounding(mK60->piIq.i1 * 10);
        break;

    case 3:
        d1 = REF.speedRef;
        d2 = REF.currentMax;
        d3 = REF.voltMax;

        break;
    case 4:

        d1 = motor1.error.all;
        d2 = motor2.error.all;

        break;

    case 5:
        d1 = motor1.typeID.motID;
        d2 = motor2.typeID.motID;
        break;

    case 6:
        d1 = motor1.mbit.all;
        d2 = motor2.mbit.all;
        d3 = rounding(mK60->rg.Out * 1000);
//        break;

 //   case 7:
//        d1 = motor1.flag.all;
//        d2 = motor2.flag.all;
        //       d3 = sflag.all;

        P16Kadr = -1;  // !!! сброс счётчика всегда переносится в последний case
        break;
    } //switch (P16Kadr)

    tele[28] = d1; tele[29] = d2; tele[30] = d3; tele[31] = d4; // блок 7 -> ID 116 (цикл P16Kadr)

#endif

    // Один запрос передачи заполненного 64-байтного кадра (ID 140, буфер 6).
    Send_mbox_FD(6, tele, 32);

} // end  send_REF_kadr()






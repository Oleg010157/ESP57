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
    // Консолидированный медленный кадр: 8 блоков x 4 слова = 32 слова = 64 байта.
    // Пишем НАПРЯМУЮ в tele[] (без общих d1..d4) — исключён перенос "остатка" между блоками.
    // Незаданные в конкретном case поля остаются 0 (массив обнулён). В конце — один
    // Send_mbox_FD(2, ...). Порядок блоков: 100,101,102,106,107,109,118,116.
    uint16_t tele[32] = {0};

    //100 -------------------------------------------------------------------------
#if (ref_kadr_mask & BIT5) == BIT5    // Отправляем ошибки и флаги
  
    tele[0] = overload.all;
    tele[1] = motor2.error.all;
    tele[2] = shr.all;
    tele[3] = shrTrip.all;
    shrTrip.all = 0xFFFF; // сбрасываем флаги аварийнного отключения  // блок 0 -> ID 100

#endif

// 101 MsgID -В этом кадре в UI выводятся медленные параметры DC-DC конвертора  ----------------------------------------------------------------
#if (ref_kadr_mask & BIT6)==BIT6

    tele[7] = ++P101Kadr;                                    // блок 1 -> ID 101 (цикл P101Kadr)

    switch (P101Kadr)
    {
    case 0:
        tele[4] = rounding(cla.Ia_rf * 100);
        tele[5] = rounding(cla.Ib_rf * 100);
        tele[6] = rounding(cla.Ic_rf * 100);
        break;
    case 1:
        tele[4] = (uint16_t) (cla.Ia_rms * 100 + 0.5);
        tele[5] = (uint16_t) (cla.Ib_rms * 100 + 0.5);
        tele[6] = (uint16_t) (cla.Ic_rms * 100 + 0.5);
        break;
    case 2:
        tele[4] = rounding(Vin_volt * 100);
        tele[5] = (uint16_t) (Vin_rf *100);
        tele[6] = (uint16_t) (motor1.mSabc + 0.5);
        break;
    case 3:
        tele[4] = rounding(Udc_volt * 10);
        tele[5] = rounding(Udc_rf * 10);
        tele[6] = (uint16_t) (motor2.clark.Um * 10 + 0.5);
        break;
    case 4:
        tele[4] = rounding(Idc_amp * 10);
        tele[5] = rounding(Idc_rf * 10);
        tele[6] = rounding(Idc_rms * 10);
        P101Kadr = -1;
        break;
    }

#endif //  if (ref_kadr_mask & BIT6)==BIT6

// 102  MsgID ------------------------------------------------------------------------------------
#if (ref_kadr_mask & BIT7)==BIT7

    tele[11] = ++P102Kadr;                                   // блок 2 -> ID 102 (цикл P102Kadr)
    switch (P102Kadr)
    {
    case 0:
        tele[8]  = rounding(cla.Ia_rf * 100);
        tele[9]  = rounding(cla.Ib_rf * 100);
        tele[10] = rounding(cla.Ic_rf * 100);
        break;

    case 1:
        tele[8]  = (uint16_t) (cla.Ia_rms * 100 + 0.5);
        tele[9]  = (uint16_t) (cla.Ib_rms * 100 + 0.5);
        tele[10] = (uint16_t) (cla.Ic_rms * 100 + 0.5);
        break;

    case 2:
        tele[8]  = Udc_kod;
        tele[9]  = rounding(motor2.pabc_rf);
        tele[10] = (uint16_t) (motor2.mSabc + 0.5);
        break;

    case 3:
        tele[8]  = rounding(Udc_rf * 10);
        tele[9]  = rounding(motor2.slip.absF * 1000);
        tele[10] = rounding(motor2.clark.UmF * 50);
        break;

    case 4:
        tele[8]  = core_temp;
        tele[9]  = core_temp;
        tele[10] = core_temp;
        P102Kadr = -1;
        break;

    }

#endif //  if (ref_kadr_mask & BIT7)==BIT7

// 106 MsgID -------------------------------------------------------------
#if (ref_kadr_mask & BIT9)==BIT9

    tele[15] = ++P106Kadr;                                     // блок 3 -> ID 106 (цикл P106Kadr)
    switch (P106Kadr)
    {
    case 0:
        tele[12] = rounding(motor1.park.IdF * 100);
        tele[13] = rounding(motor1.park.IqF * 100);
        tele[14] = rounding(motor2.park.IdF * 100);
        break;

    case 1:
        tele[12] = rounding(motor2.park.IqF * 100);
        tele[13] = rounding(motor1.park.VdF * 10); // среднее значение тока от 2-го выпрямителя
        tele[14] = rounding(motor1.park.VqF * 10);
        break;
    case 2:
        tele[12] = rounding(motor2.park.VdF * 10);
        tele[13] = rounding(motor2.park.VqF * 10);
        // tele[14] не задаётся -> 0 (UI в case 2 его не читает)
        break;
    case 3:
        tele[12] = rounding(Idc_rf * 100); // среднее значение тока от 1-го выпрямителя
        tele[13] = rounding(Idc_rms * 100); // среднее значение тока от 2-го выпрямителя
        tele[14] = rounding(stend.Pa_stend); // активная мощность от или к источнику питания
        P106Kadr = -1;
        break;
    }
#endif

    // 107  MsgID -------------------------------------------------------------
#if ((ref_kadr_mask & BIT8) == BIT8)

    tele[16] = rounding(motor1.ramp.ref * 0.02f); // заданная частота поля статора m1
    tele[17] = rounding(motor1.freq * 5);         // фактическая частота поля m1
    tele[18] = rounding(motor2.ramp.ref * 0.02f); // заданная частота поля статора m2
    tele[19] = rounding(motor2.freq * 5);         // фактическая частота поля m2  (блок 4 -> ID 107)

#endif
//BIT9   MsgID = 115  10-ый  Mbox для передачи кодов ошибок в фоне.

//BIT10  MsgID = 109  
#if (ref_kadr_mask & BIT10)==BIT10

    tele[20] = (uint16_t) (motor1.ipark.gain * 1000 + 0.5); // индекс модуляции m1
    tele[21] = (uint16_t) (motor2.ipark.gain * 1000 + 0.5); // индекс модуляции m2
    tele[22] = motor1.flag.all;
    tele[23] = motor2.flag.all;                             // блок 5 -> ID 109 (gain/flags)
#endif



// 118  MsgID выводим float суммарной по всем трём фазам активной мощности, Ватт
#if (ref_kadr_mask & BIT11)==BIT11
    {
        union word32 f118L, f118H;

        f118L.f32 = motor1.mPabc;
        f118H.f32 = motor2.mPabc;
        tele[24] = f118L.u16.Low;
        tele[25] = f118L.u16.Hi;
        tele[26] = f118H.u16.Low;
        tele[27] = f118H.u16.Hi;   // блок 6 -> ID 118 (float мощность m1/m2)
    }
#endif

//------------------------------------------------------------------------------
// 116  MsgID  для передачи медленных параметров
#if (ref_kadr_mask & BIT12) == BIT12

  tele[31] = ++P16Kadr; // блок 7 -> ID 116 (цикл P16Kadr)
  switch (P16Kadr)
  {
  case 0:
    tele[28] = motor1.mode.all;
    tele[29] = motor2.mode.all;
    tele[30] = sbit.all;
    break;

  case 1:
    tele[29] = rounding(mK60->rg.em2 * 1000);
    tele[30] = mK60->rg.offset;
    // tele[28] (A_SPI) не задаётся -> 0; при необходимости дописать
    break;

  case 2:
    // tele[28] = mK60->qep.angle;  // A_QEP: при необходимости раскомментировать
    tele[29] = rounding(mK60->piId.i1 * 10);
    tele[30] = rounding(mK60->piIq.i1 * 10);
    break;

  case 3:
    tele[28] = REF.speedRef;
    tele[29] = REF.currentMax;
    tele[30] = REF.voltMax;
    break;

  case 4:
    tele[28] = motor1.error.all;
    tele[29] = motor2.error.all;
    break;

  case 5:
    tele[28] = motor1.typeID.motID;
    tele[29] = motor2.typeID.motID;
    break;

  case 6:
    tele[28] = motor1.mbit.all;
    tele[29] = motor2.mbit.all;
    tele[30] = rounding(mK60->rg.Out * 1000);
    P16Kadr = -1; // !!! сброс счётчика всегда переносится в последний case
    break;

  } // switch (P16Kadr)


#endif

    // Один запрос передачи заполненного 64-байтного кадра (ID 140, буфер 2).
    Send_mbox_FD(2, tele, 32);

} // end  send_REF_kadr()






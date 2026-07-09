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

    //100 -------------------------------------------------------------------------
#if (ref_kadr_mask & BIT5) == BIT5    // Отправляем ошибки и флаги
  
    d1 = overload.all; 
    d2 = motor2.error.all; //ccs.u16.u2;
    d3 = shr.all;
    d4 = shrTrip.all;
    shrTrip.all = 0xFFFF; // сбрасываем флаги аварийнного отключения
    Send_mbox(6, d1, d2, d3, d4);
     
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
    Send_mbox(7, d1, d2, d3, d4);

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
    Send_mbox(8, d1, d2, d3, d4);

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
    Send_mbox(10, d1, d2, d3, d4);
#endif

    // 107  MsgID -------------------------------------------------------------
#if ((ref_kadr_mask & BIT8) == BIT8)

    d1 = rounding(motor1.ramp.ref * 0.02f); // заданная частота поля статора speedRef+sAbsfref1;
    d2 = rounding(motor1.freq * 5);  // фактическая частота поля
    d3 = rounding(motor2.ramp.ref * 0.02f); // заданная частота поля статора speedRef+sAbsfref1;
    d4 = rounding(motor2.freq * 5);  // фактическая частота поля

    Send_mbox(9, d1, d2, d3, d4);

#endif
//BIT9   MsgID = 115  10-ый  Mbox для передачи кодов ошибок в фоне.

//BIT10  MsgID = 109  
#if (ref_kadr_mask & BIT10)==BIT10

    d1 = (uint16_t) (motor1.ipark.gain * 1000 + 0.5); //выходное результирующее значение индекса модуляции напряжения;
    d2 = (uint16_t) (motor2.ipark.gain * 1000 + 0.5); //выходное результирующее значение индекса модуляции напряжения;
    d3 = motor1.flag.all;
    d4 = motor2.flag.all;
    Send_mbox(11, d1, d2, d3, d4);
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
        Send_mbox(12, d1, d2, d3, d4);
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

    Send_mbox(13, d1, d2, d3, d4);
    
#endif

} // end  send_REF_kadr()






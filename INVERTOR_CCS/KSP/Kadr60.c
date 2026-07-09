/*
 * Kadr60.c
 *
 *  Created on: 24 мая 2016 г. Author: olegs.pavlovskis
 *      
 * modifed 15.07.2025 Author: olegs.pavlovskis
 * в связи с перходом на DCAN.
 * Kadr60.c запускается - каждые 60 мс в фоне по событию Таймер 1
 *  Заполняет ячейки САNA MBOX- ов
 */
#include "f28p55x_device.h"  // define uint16_t, Uint32
#include "FLAGZ.h"
#include "Konstanty.h"
#include "myDCAN.h"



void IGBT1_Disable();
void IGBT1_Disable();

void Send_kadr60(void);

void Rec_filtr_Init(motor *m);



extern double t40_Tnm; // измеренный крутящий момент в н*м





extern long f1ref, f2ref;   // заданная частота поля АИН1, АИН2   0.1 об/мин
extern int hold_rpm;
uint16_t canLossCounter = 0;    // счетчик потерь последовательных кадров по CANA

uint16_t K60cnt = 0;
extern motor *mK60;


//---------------------------------------------------------------------------------------------

//-------------------------------------------------------------------------------------------------
void Send_kadr60() // заполняет область CANA Mbox значениями переменых для вывода в Кадр60
{
    uint16_t d1, d2, d3, d4;
 //   uint16_t grup4 = K60cnt & 0x3; // определяет номер текущей группы  параметров для Mbox




//BIT3   MsgID = 115  10-ый  Mbox ---------------------------------------------
#if (kmask60 & BIT3)== BIT3
   
#endif

// 104 ---------------------------------------------
#if (kmask60 & BIT4)==BIT4
    p1040 = rounding(motor2.clark.pa * 0.1);
    p1041 = rounding(motor2.clark.mPx * 0.1);
    p1042 = rounding(motor2.mPF*10000);
    p1043 = (uint16_t)(motor2.mSabc+0.5);
#endif

// 105 ---------------------------------------------
#if (kmask60 & BIT5)==BIT5
    switch (grup4)
    {
    case 0:
        p1050 = Tooths;
        p1051 = rounding(stend.Ugen_rms * 10);
        p1052 = 1052;
        p1053 = 1053;
        break;
    case 1:
        p1050 = rounding(motor1.Ir_av * 100);
        p1051 = rounding(motor1.Ir_rms * 100);
        p1052 = rounding(motor2.Ir_av * 100);
        p1053 = rounding(motor2.Ir_rms * 100);
        break;
    case 2:
        p1050 = rounding(motor1.ipark.Vd0_max * 50);
        p1051 = rounding(motor1.ipark.Vq0_max * 50);
        p1052 = rounding(motor1.ipark.Udx * 50);
        p1053 = rounding(motor1.ipark.Uqx * 50);
        break;
    case 3:
        p1050 = rounding(motor2.ipark.Vd0_max * 50);
        p1051 = rounding(motor2.ipark.Vq0_max * 50);
        p1052 = rounding(motor2.ipark.Udx * 50);
        p1053 = rounding(motor2.ipark.Uqx * 50);
        break;
    }

#endif


// 107  -------------------------------------------------------------
#if (kmask60 & BIT7)==BIT7

    p1070 = rounding(motor1.ramp.ref * 0.02); // заданная частота поля статора speedRef+sAbsfref1;
    p1071 = rounding(motor1.freq * 5);  // фактическая частота поля
    p1072 = rounding(motor2.ramp.ref * 0.02); // заданная частота поля статора speedRef+sAbsfref1;
    p1073 = rounding(motor2.freq * 5);  // фактическая частота поля

#endif


// 109 --------------------------------------------------------------
#if (kmask60 & BIT9)==BIT9


    p1090 = (uint16_t) (motor1.ipark.gain * 50000 + 0.5);
    p1091 = (uint16_t) (motor2.ipark.gain * 50000 + 0.5);
    p1092 = motor1.flag.all; //hold_rpm;
    p1093 = motor2.flag.all; //GPars.IGen_RMS;
#endif

// 110 ------------------------------------------------------------------
#if (kmask60 & BIT10)==BIT10
#if T40B
    p1100 = rounding(t40_Tnm);
    p1101 = rounding(torq);
    p1102 = rounding(freqTorq/10);

#else
    p1100 = 0;
    p1101 = 0;
#endif
    p1103 =  cla.Udc_volt>0? (uint16_t)(cla.Udc_volt*50 + 0.5) : 0;
    #endif

// 111
#if (kmask60 & BIT11)==BIT11

    w111L = cap1R;
//    p1110 = rounding(motor1.park.fuF * 1000);
//    p1111 = rounding(motor1.park.fiF * 1000);
    p1112 = GpioDataRegs.GPADAT.bit.GPIO24? 1:0; //rounding(motor2.park.fuF * 1000); GPIO24
    p1113 = GpioDataRegs.GPADAT.bit.GPIO25? 1:0; //rounding(motor2.park.fiF * 1000);
#endif

// 112
#if (kmask60 & BIT12)==BIT12
    p1120 = rounding(mK60->piRPM.Out*10);
    p1121 = rounding(mK60->piIq.Out*10);
    p1122 = rounding(mK60->piId.Out*10);
    p1123 = rounding(Ia_rf * 10);  //REF.speedRef_KVU;

#endif

// 113 !!! используется для подтверждения команд по mbox28
#if (kmask60 & BIT13)==BIT13

#endif

// 114  -------------------------------------
#if (kmask60 & BIT14)==BIT14

    p1140 = (uint16_t)(motor1.clark.UmF * 50 +0.5);
    p1141 = ToothCnt;    //номер зуба
    p1142 = IsrTicker;
    p1143 = (uint16_t)(motor2.clark.UmF * 50 +0.5);
#endif

// 115, 116 mbox  используются для передачи медленных параметров   в программе REF_Kadr.c


// 117
#if (kmask60 & BIT17)==BIT17
    p1170 = rounding(motor1.slip.absF * 500);
    p1171 = rounding(motor2.slip.absF * 500);
    p1172 = rounding(Idc_rf * 10); // сумарное среднее значение тока питания АИН-ов
    p1173 = rounding(stend.Pa_stend*0.1);    // активная мощность потребляемая стендом

#endif

// 118  выводим float суммарной по всем трём фазам активной мощности, Ватт
#if (kmask60 & BIT18)==BIT18

    f118L = motor1.mPabc;
    f118H = motor2.mPabc;

#endif

// 119  выводим float полной мощности суммарной по всем трём фазам , Вольт-Ампер
#if (kmask60 & BIT19)==BIT19
    p1190 = rounding(motor1.park.VdF * 10);
    p1191 = rounding(motor1.park.VqF * 10);
    p1192 = rounding(motor2.park.VdF * 10);
    p1193 = rounding(motor2.park.VqF * 10);
#endif

// 120 выводим float полной мощности суммарной по всем трём фазам , Вольт-Ампер
#if (kmask60 & BIT20)==BIT20
    f120L = motor1.mSabc;
    f120H = motor2.mSabc;

//    p1200 = rounding(motor1.ipark.puVd * 1000);
//    p1201 = rounding(motor1.ipark.puVq * 1000);
//    p1202 = rounding(motor2.ipark.puVd * 1000);
//    p1203 = Tooths; //количество точек для вывода по F9
#endif

// 121
#if (kmask60 & BIT21)==BIT21
    p1210 = rounding(mK60->piRPM.Ref*10);
    p1211 = rounding(mK60->piId.Ref*10);
    p1212 = rounding(mK60->piIq.Ref*10);
    p1212 = rounding(mK60->piIm.Ref*10);

#endif

// 122
#if (kmask60 & BIT22)==BIT22
    p1220 = rounding(motor2.qep.pr * 5); // Обороты с QEP2 за часть оборота;
    p1221 = rounding(motor2.qep.fr* 5);    // Обороты с QEP2 за часть оборота;
    p1222 = rounding(motor2.qep.n3 * 5);     // Обороты с QEP2 за часть оборота
    p1223 = 223;

#endif

// 123
#if (kmask60 & BIT23)==BIT23
    p1230 = rounding(motor1.park.IdF * 10);
    p1231 = rounding(motor1.park.IqF * 10);
    p1232 = rounding(motor1.park.DcosF * 1000);
    p1233 = rounding(motor1.clark.ImF * 10);
#endif

// 124
#if (kmask60 & BIT24)==BIT24
    p1240 = rounding(motor2.park.IdF * 10);
    p1241 = rounding(motor2.park.IqF * 10);
    p1242 = rounding(motor2.park.DcosF * 1000);
    p1243 = rounding(motor2.clark.ImF * 10);
#endif

// 125
#if (kmask60 & BIT25)==BIT25
    p1250 = rounding(motor1.Ia_rms*50);
    p1251 = rounding(motor1.park.Pz);
    p1252 = rounding(motor1.park.mPx);
    p1253 = rounding(motor1.park.fui * 1000);
#endif

// 126
#if (kmask60 & BIT26)==BIT26
    p1260 = rounding(motor2.Ia_rms*50);
    p1261 = rounding(motor2.park.Pz);
    p1262 = rounding(motor2.park.mPx);
    p1263 = rounding(motor2.park.fui * 1000);
#endif

// 127
#if (kmask60 & BIT27)==BIT27
    p1270 = rounding(motor1.qep.pr * 5);    // Обороты с QEP1 за часть оборота;
    p1271 = rounding(motor1.qep.fr * 5);    // Обороты с QEP1 кол-во импульсов за константное время;rounding(motor1.se.rpm * 5);
    p1272 = rounding(motor1.qep.n3 * 5);    // Обороты с QEP1 за часть оборота
    p1273 = 273;

#endif

// 128 mbox работает на приём команд от Motor Control
#if (kmask60 & BIT28)==BIT28
    p1280 = aa0;
    p1281 = aa1;
    p1282 = aa2;
    p1283 = aa3;
#endif

// 129  Работает на приём команд от CANC
#if (kmask60 & BIT29)==BIT29
    p1290 = aa0;
    p1291 = aa1;
    p1292 = aa2;
    p1293 = aa3;
#endif

// 131
#if (kmask60 & BIT30) == BIT30
    d1 = 1;
    d2 = 2;        //motor1.Ia;
    d3 = 3;        //motor1.Ib;
    d4 = ++K60cnt; //   !!!!  в конец кадра
    Send_mbox(31, d1, d2, d3, d4);
#endif

//BIT32  MsgID = 132  32-ый  Mbox для передачи кодов ошибок в фоне.
}


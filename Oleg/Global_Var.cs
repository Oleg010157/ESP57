using System;
using System.Drawing;
using System.Windows.Forms;
namespace ICDIBasic
{
    #region Глобальные переменные

    public static class Общак
    {

        public static Color Crem = Color.FromArgb(212, 208, 200);
        public static Color Crem2 = Color.PaleGoldenrod;
        public static ushort SAbs = 0;
        public static ushort SAbsMax = 50;
        public static ushort SAbsMax2 = 50;
        public static ushort SRelMax = 5;
        public static ushort SpeedRef = 0;

        public static short[] TxTable = new short[100];

        public static ushort Kuk1 = 0, Kuk2 = 0, Kuk3 = 0;


    }
    #endregion Глобальные переменные

    #region Переменные мотора принятые из CCS
    public class mccs
    {
        public ushort bits;
        public ushort flags;
        public ushort errors; // биты неисправностей при которых выключаются IGBT

        public ushort gain;
        public short freq;



        public short ccs3;
        public short ccs4;

        public short Vd;            // фильтрованное напряжение по оси d
        public short Vq;            // фильтрованное напряжение по оси q
        public ushort Um;
        public short IpVd;
        public short IpVq;
        public short Vd0_max;
        public short Vq0_max;
        public short Vd_max;
        public short Vq_max;
        public short Udx;       // падение напряжения на реактивном сопротивлениии
        public short Uqx;
        public short Ed;
        public short Eq;

        public short Id;
        public short IdF;
        public short Id_max;
        public short Imag;
        public short Iq;
        public short IqF;
        public short Iq_max;
        public ushort Im;
        public short Dcos;

        public short uId;
        public short uIdF;
        public short uImag;
        public short uIq;
        public short uIqF;
        public ushort uIm;
        public short uDcos;
        public short uVd;
        public short uVq;
        public ushort Uab;     // линейное напряжение между фазами a-b
        public double Urms;     // линейное rms напряжение между фазами 
        public double UrmsF;     // линейное rms напряжение между фазами для индикации на форме

        public float  mPabc;        // активная мощность мотора, Вт ->  средние значение за один период напряжения.   
        public double mPabcF =0;    // сглаженная mPabc активная мощность для вывода  на дисплей
        public ushort mPx;  // реактивная мощность мотора, Вт ->  средние значение за один период напряжения.

        public ushort mSabc;  // полная мощность мотора, Вт ->  средние значение за один период напряжения.
        public ushort mSabcF;  // сглаженная полная мощность мотора, Вт ->  средние значение за один период напряжения.
        public ushort mPaz;  // полная мощность фазы А, Вт ->  средние значение за один период напряжения.
        public double mPF;  // коэф. мощности фазы А, Вт ->  mPF = mPabc/mSabc средние значение за один период напряжения.

        public ushort umode;

        public short Ia;
        public short Ib;
        public short Ic;
        public short In;
        public short Ir;
      

        public short Va;
        public short Vb;
        public short Vc;

        public short Ia_av;
        public short Ib_av;
        public short Ic_av;
        public short In_av;
        public short Ir_av;
     

        public ushort Ia_rms;
        public ushort Ib_rms;
        public ushort Ic_rms;
        public ushort In_rms;
        public ushort Ir_rms;
     


        public short c_pa;
        public short c_px;
        public ushort c_pz;
        public short c_cos;
        public short c_paF;
        public short c_pxF;
        public ushort c_pzF;
        public short c_cosF;
        public double PF;

        public ushort c_Um;
        public ushort c_Im;
        public ushort c_UmF;
        public ushort c_ImF;



        public short WslipF;
        public short Tnm;
        public short Tnm_ofset = -1300;
        public short slip_abs;
        public float slip_absF;
        public short slip_proc;
        public short slip_procF;

        public short We;
        public short A_em;

        public ushort tooths;
        public short rgOut;
        public short radian;

        public ushort VT1;
        public ushort VT2;
        public ushort VT3;
        public ushort VT4;
        public ushort VT5;

    }
    #endregion Переменные мотора принятые из CCS

    public struct SHR_Flags
    {
        public ushort all;

        public SHR_Flags(ushort value)
        {
            all = value;
        }

        public bitMask bit => new bitMask(all);

        public struct bitMask
        {
            private readonly ushort _all;

            public bitMask(ushort value)
            {
                _all = value;
            }

            public bool VT2 => (_all & (1 << 0)) != 0;
            public bool VT1 => (_all & (1 << 1)) != 0;
            public bool VT3 => (_all & (1 << 2)) != 0;
            public bool VT4 => (_all & (1 << 3)) != 0;
            public bool T1 => (_all & (1 << 4)) != 0;
            public bool T2 => (_all & (1 << 5)) != 0;
            public bool T3 => (_all & (1 << 6)) != 0;
            public bool T4 => (_all & (1 << 7)) != 0;
            public bool T6 => (_all & (1 << 8)) != 0;
            public bool T5 => (_all & (1 << 9)) != 0;
            public bool TS1 => (_all & (1 << 10)) != 0;
            public bool TS2 => (_all & (1 << 11)) != 0;
            public bool Inp4 => (_all & (1 << 12)) != 0;
            public bool Inp3 => (_all & (1 << 13)) != 0;
            public bool Inp2 => (_all & (1 << 14)) != 0;
            public bool Inp1 => (_all & (1 << 15)) != 0;
        }
    }

    #region  Переменные Все принятые из CCS

    public class Parkinson
    {
        public short Id;
        public short Iq;
        //     public short IdF;
        public short IqF;
        public short Dcos;
        public float Phase;
        public ushort Im;
        public float Pa;
        public float Px;
        public float Pz;
        public float IpVd;
        public float IpVq;
        public short Ud;
        public short Uq;
    }

    public static class ccs
    {
        //100
        public static ushort overload = 0;
        public static ushort bit1 = 0;
        public static ushort shr = 0xffff;     // Все биты установлены — система в норме
        public static ushort shrTrip = 0xffff; // Все биты установлены — система в норме

        public static SHR_Flags shrFlags => new SHR_Flags(shr);
        public static SHR_Flags shrTripFlags => new SHR_Flags(shrTrip);

        //101
        public static short Vin = 0;        // измеренное напряжение на входе DC/DC преобразователя, Вольт.
        public static short Vin_av = 0;     // сглаженный Vin, Вольт
        public static short Vin_kod = 0;    // код АЦП праобразования Vin


        public static short Idc = 0;        // измеренный ток на входе инвертора, Ампер.
        public static short Idc_av = 0;     // сглаженный Idc, Ампер
        public static short Idc_kod = 0;    // код АЦП праобразования Idс.

        public static short Udc = 0;        // измеренное напряжение на входе инвертора, Вольт.
        public static short Udc_av = 0;     // сглаженный Udc, Вольт
        public static short Udc_kod = 0;    // код АЦП праобразования Udc.

        public static short core_temp=0;      // температура ядра F280039C в Цельсиях

        //140 Однофазный мост 48->220. Четыре канала приходят одним набором,
        //    какой именно - говорит teleSel (он же ключ команды 634):
        //    0 = коды АЦП, 1 = мгновенные значения, 2 = rms.
        //    В кодах величины целые, в остальных наборах - В или А.
        public static double iPri = 0;      // ток первичной обмотки (сторона 48 В)
        public static double iSec = 0;      // ток вторичной обмотки (ток в сеть)
        public static double u220 = 0;      // напряжение сети после контактора
        public static double uSec = 0;      // напряжение на выходе моста, до контактора
        public static ushort teleSel = 2;   // эхо ключа из кадра: что именно в четвёрке выше

        public static double u220freq = 0;  // измеренная частота сети, Гц. Прибор, а не
                                            // уставка: считается по захвату нуля U220
                                            // всегда, независимо от режима моста

        public static double UdcBat = 0;    // напряжение на входе моста (АКБ или иной
                                            // источник), Вольт. В кадре зовётся uBat
        // Мощности на выходе инвертора: пара uSec и iSec. Считает CLA раз за
        // период сети (CLA/Task1.cla), кадр 140 везёт их словами 17..20.
        //
        // S не равно корню из суммы квадратов P и Q, если ток искажён: разницу
        // даёт мощность искажений. PF потому и зовётся коэффициентом мощности,
        // а не косинусом фи: он считается как P/S и искажения видит.
        public static double gtP = 0;       // активная, Вт. + отдаёт, - принимает
        public static double gtQ = 0;       // реактивная, вар. + индуктивная нагрузка
        public static double gtS = 0;       // полная, В*А
        public static double gtPF = 0;      // коэффициент мощности, -1..+1

        // Постоянная составляющая в трансформаторе: что видим и чем давим.
        // Кадр 140, слова 21 и 22. Разбор - INVERTOR_CCS/my_Files/grid_tie.txt, 3.1.
        //
        // Постоянка на первичке намагничивает сердечник, а поток есть интеграл
        // напряжения, то есть растёт без границ. Свободного запаса у сердечника
        // около 0.035 В*с, поэтому вольт постоянки съедает его за два периода.
        //
        // iPriAv - средний ток ПЕРВИЧКИ за период, Ампер. Именно первички: ток
        // намагничивания течёт только там и во вторичку не трансформируется
        // в принципе. Считает CLA тем же окном, что и действующие значения.
        //
        // uDCcomp - сколько постоянки мост ВЫЧИТАЕТ, Вольт. Внутри прошивки это
        // безразмерная поправка gt.mDC к индексу модуляции; здесь она умножена
        // на напряжение звена, чтобы число сравнивалось напрямую: 0.48 В - полная
        // ошибка мёртвого времени, 0.96 В - потолок, дальше контуру нечем спасать
        // сердечник.
        //
        // Не путать с M1.ccs.Ia_av из кадра 101: там средний ток фазы A мотора,
        // величина совсем другая.
        public static double iPriAv = 0;    // средний ток первички за период, А
        public static double uDCcomp = 0;   // поправка постоянки, приведённая к Вольтам

        public static double gtIset = 0;    // сетевой режим: что реально отдаём (поле Iset), А rms
        public static double gtIsetRef = 0; // туда идёт рампа: уставка (движок Iзад), А rms
        public static ushort gtState = 0;   // автомат подключения: 0 OFF, 1 SYNC, 2 RAMP, 3 RUN, 4 FAULT
        public static ushort gtFault = 0;   // код последней аварии сетевого режима
        public static ushort sineGain = 0;  // sineB.gain - амплитуда синуса в кодах

        // Запас по индексу модуляции, кадр 140, слова 24..25. Поле "m" окна.
        // Пик - то, что контур ПРОСИЛ за период, до ограничения по mMax (0.95),
        // упор - сколько тактов из ста стояло в потолке. Больше gt.satLimit (10)
        // за период - авария 64.
        public static double gtMPeak = 0;   // пик |m| за период сети
        public static ushort gtSatLast = 0; // тактов в упоре по m за период

        // Быстрый кадр F3_convertor (CAN ID 103, 16 слов) - сырая лента для
        // Wscope. Слова лежат массивом, а не разбираются по именованным полям,
        // и это сознательно: разбирать нечего. Кадр приходит 5000 раз в секунду
        // и целиком уходит в файл тем же порядком слов, каким пришёл, - имена
        // каналам даёт не программа, а bin/Wscope/F3_convertor.ini.
        //
        // Раньше слова раскладывались по полям M1.ccs/M2.ccs с моторными
        // именами (Ia, Vb, radian, gain...), выбранными наугад: величины в них
        // лежали совсем не те, что в названии, а поле gain при этом ещё и
        // показывается в окне. Массив убирает и путаницу, и наложение.
        //
        // Последнее слово - счётчик тактов, он же признак готовности записи,
        // см. ccs.k60Cnt и CanaAsync.cs. Раскладка - в прошивке, отправка
        // кадра в my_Files/1ph_Sin.c.
        public static short[] f3 = new short[16];

        // Медленная запись "Kadr60" - кадр 140 целиком, все 32 слова, тоже
        // сырой лентой и по той же причине: он уходит в файл тем же порядком
        // слов, а имена каналам даёт bin/Wscope/Kadr60.ini. Кладёт его сюда
        // Parse140 - тот же, что разбирает кадр для окна, так что в файле
        // ровно то, что на экране. Последнее слово - счётчик K60cnt, он же
        // признак готовности записи.
        public static ushort[] k60 = new ushort[32];

        public static ushort P101Kadr = 0;  // номер группы параметров по 108 mbox

        //103

        public static short Px1_cl = 0;
        public static short Pz1_cl = 0;
        public static short cos1_cl = 0;

        public static short Pa1_abc = 0;
        public static short Pa2_abc = 0;

        public static short Ia_kod = 0;
        public static short Ib_kod = 0;
        public static short Ic_kod = 0;
      
        public static ushort index = 0;
       

        //104
        public static short Pa2_cl = 0;
        public static short Px2_cl = 0;
        public static short Pz2_cl = 0;
        public static short cos2_cl = 0;

        //105
  
        

        public static short dbg2 = 0;
        public static short dbg3 = 0;
        public static short dbg4 = 0;
        //106
        public static short Ir1 = 0;       // среднее значение тока от 1-го выпрямителя
        public static short Ir2 = 0;       // среднее значение тока от 2-го выпрямителя
        public static short Ir1_rms = 0; // сумарное среднее значение тока питания АИН-ов
        public static short Ir2_rms = 0;  // активная мощность потребляемая стендом

        //107
        public static short f1_ref = 0;
        public static short f1_act = 0;
        public static short f2_ref = 0;
        public static short f2_act = 0;

        //108  используется под CANC
        public static ushort P8Kadr = 0;  // номер группы параметров по 108 mbox
        //public static short R1_c = 0;
        //public static short R2_c = 0;
        // public static ushort Udc_c = 0;
        //public static ushort I1m_c = 0;

        //109

        public static short fadd = 0;
        public static ushort IGen_RMS = 0;


        //110
    

        //111
        public static short EPSv1 = 0;
        public static short EPSi1 = 0;
        public static short EPSv2 = 0;
        public static short EPSi2 = 0;

        //112
        public static short piR_Out = 0;
        public static short piQ_Out = 0;
        public static short piD_Out = 0;
        public static ushort Rkvu_Ref = 0;

        //114
        public static short R1_dg = 0;
        public static ushort ToothCnt = 0;
        public static ushort IsrTicker = 0;

        //115
        //0
        public static ushort piR_Umin = 0;
        public static ushort piD_Umin = 0;
        public static short piQ_Umin = 0;
        //1
        public static ushort piE_dumax = 0;
        public static ushort piE_Umax = 0;
        public static ushort piR_Kp = 0;
        //2
        public static ushort piD_Kp = 0;
        public static ushort piD_Ki = 0;

        //3
        public static ushort piD_dumax = 0;
        public static ushort piD_Umax = 0;
        public static ushort piR_Ki = 0;
        //4    
        public static ushort piQ_Kp = 0;
        public static ushort piQ_Ki = 0;

        //5
        public static ushort piQ_dumax = 0;
        public static ushort piQ_Umax = 0;

        //6
        public static ushort piR_dumax = 0;
        public static ushort piR_Umax = 0;
        public static ushort piK_qd = 0;
        //7
        public static ushort piM_Kp = 0;
        public static ushort piM_Ki = 0;

        //8
        public static ushort piM_dumax = 0;
        public static ushort piM_Umax = 0;


        //116  mbox медлнных параметров
        //0
        public static ushort umode1 = 0;
        public static ushort umode2 = 0;
        public static ushort sbit = 0;

        //1
        public static short A_SPI = 0; // угол  Резолвера через SPI от 0 до 4095
        public static ushort A_em2 = 0;
        public static short A_em0 = 0;       // положение ротора при нулевом угле вектора напряжения статора т.е. Rangle_SPI (rgOut = 0 )
        public static short A_rg = 0;
        //2
        public static short piR_i = 0;
        public static short piD_i = 0;
        public static short piQ_i = 0;

        //3
        public static ushort speedRef = 0;
        public static ushort slipRef = 0;
        public static ushort slipMax = 0;
        //4
        public static ushort Ig_Ref = 0;
        public static ushort Ig_Lim = 0;
        public static ushort Ig_Hst = 0;
        //5
        public static ushort m1_ID = 0;
        public static ushort m2_ID = 0;
        public static ushort cur_Tr = 0;

        //6
        // public static ushort M1.ccs.bits = 0;
        // public static ushort M2.ccs.bits = 0;
        //  public static ushort sbit = 0;

        //7
        // public static ushort M1.ccs.bits = 0;
        // public static ushort M2.ccs.bits = 0;
        //public static ushort sbit = 0;
        //-------------------------------------------------------------------------   

        //117
        public static short Igen_rf = 0;
        public static ushort Igen_rms = 0;
        public static short Istend_rf = 0;
        public static short Pa_stend = 0;

        //118
        public static ushort A_rg1 = 0;    //  угол вектора напряжения статора от оси фазы А , эл.градус*10  т.е. от 0 -> 3600
        public static ushort A_rg2 = 0;    //  угол вектора напряжения статора от магнитной оси ротора, эл.градус*10  т.е. от 0 -> 3600
        public static short A_em = 0;
        public static ushort A_QEP1 = 0;
        public static ushort A_QEP2 = 0;
        public static short A_QEP = 0;

        //119
        //     public static short U1d = 0;
        //     public static short U1q = 0;
        //      public static short U2d = 0;
        //      public static short U2q = 0;

        // 120
        public static ushort f9_tooths = 1024;

        //121
        public static short piR_Ref = 0;
        public static short piD_Ref = 0;
        public static short piQ_Ref = 0;
        public static short piM_Ref = 0;

        //122
        public static short QEP2_pr = 0;
        public static short QEP2_fr = 0;
        public static short QEP2_n3 = 0;
        public static short QEP2_dg = 0;

        //123
        public static short pk1_Id = 0;
        public static short pk1_Iq = 0;
        public static short pk1_cos = 0;
        //      public static ushort pk1_Im = 0;

        //124
        public static short pk2_Id = 0;
        public static short pk2_Iq = 0;
        public static short pk2_cos = 0;
        //      public static ushort pk2_Im = 0;

        //125
        public static short pk1_Pa = 0;
        public static short pk1_Pz = 0;
        public static short pk1_Px = 0;
        public static short pk1_fui = 0;

        //126
        public static short pk2_Pa = 0;
        public static short pk2_Pz = 0;
        public static short pk2_Px = 0;
        public static short pk2_fui = 0;

        //127
        public static short QEP1_pr = 0;            // Обороты с QEP1 за часть оборота
        public static short QEP1_fr = 0;               // Обороты с QEP1 за фиксированное время
        public static short QEP1_n3 = 0;           // Обороты с QEP1 за полный оборот
        public static short QEP1_dg = 0;      // угол ротора с QEP1

        //130 
        public static short cur_We = 0;
        public static short cur_Imag = 0;
        public static short cur1_slip = 0;
        public static short cur2_slip = 0;


        //131
        public static short piR_Fbk = 0;
        public static short piD_Fbk = 0;
        public static short piQ_Fbk = 0;
        public static ushort k60Cnt = 0;
    }

    public class Mbit_Label
    {
        public Label DG;
    }

    #endregion Переменные Все принятые из CCS

    #region  Переменные  принятые из CANC

    public static class Canc
    {
        //100
        public static short I1A = 0;
        public static short I1B = 0;
        public static short I1C = 0;
        public static short I1N = 0;

        //101
        public static short V1A = 0;
        public static short V1B = 0;
        public static short V1C = 0;
        public static short Udc = 0;


        //102
        public static short Pk1Id = 0;
        public static short Pk1Iq = 0;
        public static short Pk1Im = 0;
        public static ushort Flags = 0;

        //103
        public static short I2A = 0;
        public static short I2B = 0;
        public static short I2C = 0;
        public static short I2N = 0;

        //104
        public static short V2A = 0;
        public static short V2B = 0;
        public static short V2C = 0;
        public static short F1Act = 0;

        //105
        public static short Pk2Id = 0;
        public static short Pk2Iq = 0;
        public static short Pk2Im = 0;
        public static short F2Act = 0;

        //106
        public static short R1Fr = 0;       // среднее значение тока от 1-го выпрямителя
        public static short R2Fr = 0;       // среднее значение тока от 2-го выпрямителя
        public static short R1Pr = 0; // сумарное среднее значение тока питания АИН-ов
        public static short R2Pr = 0;  // активная мощность потребляемая стендом

        //107
        public static short R1Dangl = 0;
        public static short R2Dangl = 0;
        public static short A1Qep = 0;
        public static short A2Qep = 0;

        //108
        public static ushort Timefr = 0;
        public static ushort K60Cnt = 0;
        public static ushort Cntfr = 0;
        public static ushort IsrTicker = 0;

    }
    #endregion Переменные принятые из CANC


}

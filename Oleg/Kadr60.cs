using FlagMask;
using Peak.Can.Basic;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static EasyModbus.ModbusClient;
using static EthLogger.AlStEthLog;
using static ICDIBasic.Form1;

namespace ICDIBasic
{
    public partial class Form1
    {
        /* Kadr60 **Параметры медленного кадра** */
        /***Прием данных с CANA - кадр 60ms***/

        private readonly int[] _regBuffer = new int[2];

        private void Kadr60Rx(TPCANMsgFD msg)
        {
            ushort u1 = (ushort)((msg.DATA[1] << 8) | msg.DATA[0]);
            ushort u2 = (ushort)((msg.DATA[3] << 8) | msg.DATA[2]);
            ushort u3 = (ushort)((msg.DATA[5] << 8) | msg.DATA[4]);
            ushort u4 = (ushort)((msg.DATA[7] << 8) | msg.DATA[6]);

            short s1 = (short)u1;
            short s2 = (short)u2;
            short s3 = (short)u3;
            short s4 = (short)u4;

            ushort grup4 = (ushort)(ccs.k60Cnt & 3);

          
            switch (msg.ID)
            {
                case 103:
                    // Живой осциллограф. Окно закрыто - вызов стоит одну
                    // проверку ссылки на null.
                    //
                    // Стоит ЗДЕСЬ, а не внутри ProcessFrame103, намеренно: тот
                    // начинается с if (!spl_running) return, то есть молчит,
                    // пока не идёт запись ленты в файл. Осциллографу запись не
                    // нужна - смотреть на форму тока хочется и без неё.
                    ScopeForm.Feed(msg);

                    ProcessFrame103(msg);
                    break;
                case 140:
                    Parse140(msg);
                    break;
                case 101:
                    ProcessFrame101(u1, u2, u3, u4, s1, s2, s3);
                    break;
                case 102:
                    ProcessFrame102(u1, u2, u3, u4, s1, s2, s3);
                    break;
                
                case 104:
                    ProcessFrame104(s1, s2, s3, s4);
                    break;
                case 105:
                    ProcessFrame105(s1, s2, s3, s4, u2, u3, u4, grup4);
                    break;
                case 106:
                    ProcessFrame106(s1, s2, s3, s4, u4);
                    break;
                case 107:
                    ProcessFrame107(s1, s2, s3, s4);
                    break;
                case 108:
                    ProcessFrame108(u1, u2, u3, u4);
                    break;
                case 109:
                    ProcessFrame109(u1, u2, u3, u4);
                    break;
                case 110:
                    ProcessFrame110(s1, s2, s3, s4, u2, u3, u4);
                    break;
                case 111:
                    ProcessFrame111(s1, s2, s3, s4);
                    break;
                case 112:
                    ProcessFrame112(s1, s2, s3, s4);
                    break;
                case 114:
                    ProcessFrame114(u2, u3);
                    break;
                case 115:
                    ProcessFrame115(u1, u2, u3, u4, s3);
                    break;
                case 116:
                    ProcessFrame116(u1, u2, u3, u4, s1, s2, s3);
                    break;
                case 117:
                    ProcessFrame117(s1, s2, s3, s4);
                    break;
                case 118:
                    ProcessFrame118(s1, s2, s3, s4);
                    break;
                case 119:
                    ProcessFrame119(s1, s2, s3, s4);
                    break;
                case 120:
                    ProcessFrame120(s1, u2, u4);
                    break;
                case 121:
                    ProcessFrame121(s1, s2, s3, s4);
                    break;
                case 122:
                    ProcessFrame122(s1, s2, s3, s4);
                    break;
                case 123:
                    ProcessFrame123(s1, s2, s3, u4);
                    break;
                case 124:
                    ProcessFrame124(s1, s2, s3, u4);
                    break;
                case 125:
                    ProcessFrame125(s2, s3, s4);
                    break;
                case 126:
                    ProcessFrame126(s1, s2, s3, s4);
                    break;
                case 127:
                    ProcessFrame127(s1, s2, s3, s4);
                    break;
                case 132:
                    ProcessFrame132(u1, u2, u3, u4);
                    break;


            }
        }

        /// <summary>
        /// Компактный медленный кадр ID 140: 64 байта = 32 слова.
        /// Формат MCU (KSP/REF_Kadr.c, send_REF_kadr):
        /// 0..3   = error1,error2,sbit,shrTrip
        /// 4..7   = iPri,iSec,u220,uSec - набор задаёт ключ teleSel (слово 13)
        /// 8..9   = Udc входа моста (0.1 В), CoreT
        /// 10     = gt.State | gt.fault &lt;&lt; 8
        /// 11     = gt.Iset (0.01 А)
        /// 12..15 = sineB.gain, teleSel, flags1, flags2
        /// 16     = измеренная частота сети (0.01 Гц)
        /// 17..19 = мощности на выходе инвертора: P (Вт), Q (вар), S (В*А)
        /// 20     = коэффициент мощности P/S (0.0001)
        /// 21..22 = постоянка в трансформаторе: iPri_av (0.01 А), mDC*Udc (0.001 В)
        /// 23     = gt.IsetRef (0.01 А) - уставка тока в сеть
        /// 24..25 = запас по m: пик |m| за период (0.0001), тактов в упоре
        /// 26..30 = резерв
        /// 31     = K60cnt, счётчик отправленных кадров (прошивка от 11.10.2026)
        ///
        /// Кадр целиком, все 32 слова, ещё и ложится в ccs.k60: это медленная
        /// запись в файл, набор «Kadr60» в поле «F2 Кадр».
        ///
        /// Слова 4..7: токи в единицах 0.01 А (прошивка от 11.10.2026, до того
        /// 0.1 А), напряжения в 0.1 В; в наборе кодов АЦП - целые коды.
        /// Знаковые величины MCU кладёт как int16_t.
        /// </summary>
        private void Parse140(TPCANMsgFD msg)
        {
            byte[] d = msg.DATA;

            ushort W(int w) => (ushort)((d[w * 2 + 1] << 8) | d[w * 2]);

            ProcessFrame100(W(0), W(1), W(2), W(3));

            ccs.teleSel = W(13);

            // Коды АЦП показываем как есть. u220 в кодах беззнаковый (сырой
            // отсчёт 0..4095), остальные три - знаковые.
            //
            // У физики цена слова РАЗНАЯ: токи по 0.01 А, напряжения по 0.1 В.
            // Токи мельче с прошивки от 11.10.2026 - при 0.1 А действующий
            // ток прыгал в окне между 5.2 и 5.3. Напряжениям сотые не дать:
            // мгновенное значение сети не влезло бы в слово (REF_Kadr.c).
            // Узнать по кадру, старая прошивка или новая, нельзя: со старой
            // токи покажутся вдесятеро меньше настоящих.
            if (ccs.teleSel == TeleKod)
            {
                ccs.iPri = (short)W(4);
                ccs.iSec = (short)W(5);
                ccs.u220 = W(6);
                ccs.uSec = (short)W(7);
            }
            else
            {
                ccs.iPri = (short)W(4) * 0.01;
                ccs.iSec = (short)W(5) * 0.01;
                ccs.u220 = (short)W(6) * 0.1;
                ccs.uSec = (short)W(7) * 0.1;
            }

            ccs.UdcBat    = (short)W(8) * 0.1;
            ccs.core_temp = (short)W(9);

            ushort gt = W(10);
            ccs.gtState = (ushort)(gt & 0xFF);
            ccs.gtFault = (ushort)(gt >> 8);

            ccs.gtIset   = W(11) * 0.01;
            ccs.sineGain = W(12);

            // Частота сети появилась в кадре вместе с расширением 32 -> 48 байт
            // (DLC 13 -> 14). На прошивке до 21.09.2026 слова 16 в кадре нет, и
            // прочитать его - значит показать мусор из хвоста буфера как частоту.
            //
            // Мощности приехали в тот же хвост 23.09.2026, постоянка в
            // трансформаторе 24.09.2026, и оба той же проверкой прикрыты: на
            // прошивке постарше слова 17..22 придут нулями, и поля честно
            // покажут нули, а не содержимое буфера.
            if (msg.DLC >= 14)
            {
                ccs.u220freq = W(16) * 0.01;

                ccs.gtP  = (short)W(17);
                ccs.gtQ  = (short)W(18);
                ccs.gtS  = (short)W(19);
                ccs.gtPF = (short)W(20) * 0.0001;

                // Постоянка в трансформаторе. Обе величины знаковые: знак
                // тока говорит, в какую сторону уехал поток, а знак поправки -
                // в какую сторону контур его тянет обратно. В исправном
                // контуре они противоположны; совпали - полярность датчика
                // первички неверна (gt.iSign), и обратная связь стала
                // положительной. Это самый быстрый способ убить трансформатор,
                // см. grid_tie.txt, 3.1.4.
                ccs.iPriAv  = (short)W(21) * 0.01;   // А
                ccs.uDCcomp = (short)W(22) * 0.001;  // В

                // Уставка тока в сеть, слово 23 (прошивка от 06.10.2026).
                // Беззнаковая: команда 632 приносит её из uint16, минуса
                // у неё не бывает.
                //
                // Делитель тот же, что у gtIset в слове 11, и это важно не
                // только для единиц: при равных словах оба double выходят
                // битово одинаковыми, и сравнение на равенство в Id_view
                // честно отвечает "рампа доехала".
                ccs.gtIsetRef = W(23) * 0.01;
            }

            // Запас по индексу модуляции, слова 24..25 (прошивка от
            // 08.10.2026, кадр вырос до 64 байт, DLC 15). На прошивке
            // постарше слов нет, и поле честно покажет ноль.
            if (msg.DLC >= 15)
            {
                ccs.gtMPeak   = W(24) * 0.0001; // пик |m| за период
                ccs.gtSatLast = W(25);          // тактов в упоре из ста

                // МЕДЛЕННАЯ ЗАПИСЬ (набор «Kadr60», прошивка от 11.10.2026).
                // Кадр уходит в файл сырым, тем же порядком слов: разбирать
                // по смыслу тут нечего, имена каналам даёт
                // bin/Wscope/Kadr60.ini. То есть в файле ровно то, что в окне.
                for (int i = 0; i < ccs.k60.Length; i++)
                    ccs.k60[i] = W(i);

                // Счётчик - последнее слово кадра; по его изменению запись
                // считается готовой (CAN_RxData в ASt_MainF.cs).
                //
                // ТОЛЬКО ПОД СВОЙ НАБОР. ccs.k60Cnt один на все наборы, и
                // при быстрой ленте в нём такт из кадра 103. Тронь его здесь
                // безусловно - каждый кадр 140 вставлял бы в быструю запись
                // лишний отсчёт, раз в 50 мс.
                //
                // На прошивке постарше слово 31 - ноль: счётчик стоит, и
                // запись «Kadr60» не идёт вовсе.
                if (f2kadrName == "Kadr60")
                    ccs.k60Cnt = ccs.k60[ccs.k60.Length - 1];
            }
            else
            {
                ccs.gtMPeak   = 0;
                ccs.gtSatLast = 0;
            }

            // Индикатор "gain" рядом с движком Gain1 на однофазном мосте
            // показывает sineB.gain: модуляция здесь одна, и это она.
            M1.ccs.gain = ccs.sineGain;

            M1.ccs.flags = W(14);
            M2.ccs.flags = W(15);
        }

        #region Frame Processors

        private void ProcessFrame100(ushort u1, ushort u2, ushort u3, ushort u4)
        {
            ccs.overload = u1;
            ccs.bit1 = u2;
            ccs.shr = u3;
            ccs.shrTrip = u4;

            BeginInvoke(new Action(() => { U4_U5_Viewer(ccs.shr); }));

            _refreshImage2 = true; // флаг анимации RX-индикатора CAN (поворот стрелки при приёме кадра)

            ShowOverLoadErrors(u4);
        }

        private void ProcessFrame101(ushort u1, ushort u2, ushort u3, ushort u4, short s1, short s2, short s3)
        {
            switch (u4)
            {
                case 0:
                    M1.ccs.Ia_av = s1;
                    M1.ccs.Ib_av = s2;
                    M1.ccs.Ic_av = s3;
                    break;
                case 1:
                    M1.ccs.Ia_rms = u1;
                    M1.ccs.Ib_rms = u2;
                    M1.ccs.Ic_rms = u3;
                    break;
                case 2:
                    ccs.Vin = s1;
                    ccs.Vin_av = s2;
                    M1.ccs.mSabc = u3;
                    M1.ccs.mPF = (u3 == 0) ? 0 : (s1 / (double)u3);
                    break;
                case 3:
                    ccs.Udc_av = s2;
                    M2.ccs.Urms = u3 / 100.0 * sqrt32;
                    M2.ccs.UrmsF = M1.ccs.UrmsF * 0.95 + M1.ccs.Urms * 0.05;
                    break;
                case 4:
                    ccs.Idc = s1;
                    ccs.Idc_av = s2;
                    ccs.Idc_kod = s3;
                    break;
            }
        }

        private void ProcessFrame102(ushort u1, ushort u2, ushort u3, ushort u4, short s1, short s2, short s3)
        {
            switch (u4)
            {
                case 0:
                    M2.ccs.Ia_av = s1;
                    M2.ccs.Ib_av = s2;
                    M2.ccs.Ic_av = s3;
                    break;
                case 1:
                    M2.ccs.Ia_rms = u1;
                    M2.ccs.Ib_rms = u2;
                    M2.ccs.Ic_rms = u3;
                    break;
                case 2:
                    ccs.Udc_av = s1;
                    M2.ccs.mSabc = u3;
                    M2.ccs.PF = (u3 == 0) ? 0 : s1 / (double)u3;
                    break;
                case 3:
                    M2.ccs.c_pxF = s1;
                    M2.ccs.c_UmF = u3;
                    M2.ccs.Urms = u3 / 50.0 * sqrt32;
                    break;
                case 4:
                    ccs.core_temp = s1;
                    M2.ccs.In_rms = u2;
                    M2.ccs.c_cosF = s3;
                    break;
            }
        }

        private void ProcessFrame103(TPCANMsgFD msg)
        {
            if (!spl_running) return;

            byte[] d = msg.DATA;

            // Чтение 16-битного слова по абсолютному индексу слова (0..31).
            ushort U(uint w) => (ushort)((d[w * 2 + 1] << 8) | d[w * 2]);
            short  S(uint w) =>  (short)((d[w * 2 + 1] << 8) | d[w * 2]);


            if (f2kadrName == "F3_invertor")
            {
                M2.ccs.Ia = S(0);
                M2.ccs.Ib = S(1);
                M2.ccs.Ic = S(2);
                M2.ccs.Va = S(3);

                M1.ccs.Vb = S(4);
                M1.ccs.Vc = S(5);
                M1.ccs.radian = S(6);
                M1.ccs.rgOut = S(7);

                M1.ccs.freq = S(8);
                M1.ccs.gain = U(9);
                ccs.IsrTicker = U(10);
                ccs.k60Cnt = U(11);
            }
            if (f2kadrName == "F3_convertor" || f2kadrName == "F3_grid")
            {
                // Шестнадцать слов лентой, без разбора по смыслу: кадр целиком
                // уходит в файл тем же порядком, а имена каналам даёт
                // bin/Wscope/<имя набора>.ini. Раскладка - в прошивке,
                // my_Files/1ph_Sin.c, у самой отправки.
                //
                // F3_grid (контур тока изнутри, 09.10.2026) той же длины и с
                // тем же счётчиком в последнем слове, поэтому разбор общий:
                // наборы различаются только картой каналов.
                //
                // Кадр расширен с 12 слов до 16 (23.09.2026): добавились
                // напряжение на выходе инвертора, напряжение и ток АКБ и
                // расчётный ток намагничивания. Читаем ровно столько слов,
                // сколько принёс кадр: со старой прошивкой их 12, и последние
                // четыре остаются нулями вместо мусора из хвоста буфера.
                int words = msg.DLC >= 13 ? 16 : 12;

                for (int i = 0; i < words; i++)
                    ccs.f3[i] = S((uint)i);

                for (int i = words; i < ccs.f3.Length; i++)
                    ccs.f3[i] = 0;

                // Счётчик тактов - ВСЕГДА последнее слово кадра: по его
                // изменению запись считается готовой (CanaAsync.cs).
                ccs.k60Cnt = (ushort)ccs.f3[words - 1];

                // Со старой прошивкой счётчик пришёл двенадцатым словом, а
                // запись всё равно идёт по шестнадцать. Кладём его и в
                // последнее слово: четыре новых канала останутся пустыми, но
                // лента будет писаться, а не стоять.
                if (words < ccs.f3.Length)
                    ccs.f3[ccs.f3.Length - 1] = ccs.f3[words - 1];
            }
            else if (f2kadrName == "F1_invertor")
            {
                ccs.Ia_kod = S(0);
                ccs.Ib_kod = S(1);
                ccs.Ic_kod = S(2);
                ccs.k60Cnt = U(3);
            }
            else if (f2kadrName == "F1_convertor")
            {
                ccs.Idc_kod = S(0); 
                ccs.Udc_kod = S(1);
                ccs.Vin_kod = S(2); 
                ccs.k60Cnt  = U(3);
            }
        }

        private void ProcessFrame104(short s1, short s2, short s3, short s4)
        {
            if (spl_running && f2kadrName == "F3_invertor")
            {
                M1.ccs.Vb = s1;
                M1.ccs.Vc = s2;
                M1.ccs.radian = s3;
                M1.ccs.rgOut = s4;
            }
            else if (!spl_running)
            {
                M2.ccs.c_pa = s1;
                M2.ccs.c_px = s2;
            }
        }

        private void ProcessFrame105(short s1, short s2, short s3, short s4, ushort u2, ushort u3, ushort u4, ushort grup4)
        {
            switch (grup4)
            {
                case 0:
                    ccs.Vin = 100;// s2;
                    break;
                case 1:
                    M1.ccs.Ir_av = s1;
                    M1.ccs.Ir_rms = u2;
                    M2.ccs.Ir_av = s3;
                    M2.ccs.Ir_rms = u4;
                    break;
                case 2:
                    M1.ccs.Vd0_max = s1;
                    M1.ccs.Vq0_max = s2;
                    M1.ccs.Udx = s3;
                    M1.ccs.Uqx = s4;
                    break;
                case 3:
                    M2.ccs.Vd0_max = s1;
                    M2.ccs.Vq0_max = s2;
                    M2.ccs.Udx = s3;
                    M2.ccs.Uqx = s4;
                    break;
            }
        }

        private void ProcessFrame106(short s1, short s2, short s3, short s4, ushort u4)
        {
            switch (u4)
            {
                case 0:
                    M1.ccs.IdF = s1;
                    M1.ccs.IqF = s2;
                    M2.ccs.IdF = s3;
                    break;
                case 1:
                    M2.ccs.IqF = s1;
                    M1.ccs.Vd = s2;
                    M1.ccs.Vq = s3;
                    break;
                case 2:
                    M2.ccs.Vd = s1;
                    M2.ccs.Vq = s2;
                    break;
            }
        }

        private void ProcessFrame107(short s1, short s2, short s3, short s4)
        {
            ccs.f1_ref = s1;
            ccs.f1_act = M1.ccs.freq = s2;
            ccs.f2_ref = s3;
            ccs.f2_act = M2.ccs.freq = s4;
        }

        private void ProcessFrame108(ushort u1, ushort u2, ushort u3, ushort u4)
        {
            ccs.P8Kadr = u1;
            switch (u1)
            {
                case 0:
                    M1.ccs.VT1 = u2;
                    M1.ccs.VT2 = u3;
                    M1.ccs.VT3 = u4;
                    break;
                case 1:
                    M1.ccs.VT4 = u2;
                    M1.ccs.VT5 = u3;
                    M2.ccs.VT1 = u4;
                    break;
                case 2:
                    M2.ccs.VT2 = u2;
                    M2.ccs.VT3 = u3;
                    M2.ccs.VT4 = u4;
                    break;
                case 3:
                    M2.ccs.VT5 = u2;
                    break;
            }
        }

        private void ProcessFrame109(ushort u1, ushort u2, ushort u3, ushort u4)
        {
            M1.ccs.gain = u1;
            M2.ccs.gain = u2;
            M1.ccs.flags = u3;
            M2.ccs.flags = u4;
        }

        private void ProcessFrame110(short s1, short s2, short s3, short s4, ushort u2, ushort u3, ushort u4)
        {
           
            if (spl_running && f2kadrName == "F3_invertor")
            {
                M1.ccs.freq = s1;
                M1.ccs.gain = u2;
                ccs.IsrTicker = u3;
                ccs.k60Cnt = u4;
            }
        }

        private void ProcessFrame111(short s1, short s2, short s3, short s4)
        {
            ccs.EPSv1 = s1;
            ccs.EPSi1 = s2;
            ccs.EPSv2 = s3;
            ccs.EPSi2 = s4;
        }

        private void ProcessFrame112(short s1, short s2, short s3, short s4)
        {
            ccs.piR_Out = s1;
            ccs.piQ_Out = s2;
            ccs.piD_Out = s3;
            M1.ccs.Ia_av = s4;
        }

        private void ProcessFrame114(ushort u2, ushort u3)
        {
            ccs.ToothCnt = u2;
            ccs.IsrTicker = u3;
        }

        private void ProcessFrame115(ushort u1, ushort u2, ushort u3, ushort u4, short s3)
        {
            switch (u4)
            {
                case 0:
                    ccs.piR_Umin = u1;
                    ccs.piD_Umin = u2;
                    ccs.piQ_Umin = s3;
                    break;
                case 1:
                    ccs.piE_dumax = u1;
                    ccs.piE_Umax = u2;
                    ccs.piR_Kp = u3;
                    break;
                case 2:
                    ccs.piD_Kp = u1;
                    ccs.piD_Ki = u2;
                    ccs.piD_Ref = s3;
                    break;
                case 3:
                    ccs.piD_dumax = u1;
                    ccs.piD_Umax = u2;
                    ccs.piR_Ki = u3;
                    break;
                case 4:
                    ccs.piQ_Kp = u1;
                    ccs.piQ_Ki = u2;
                    ccs.piQ_Ref = s3;
                    break;
                case 5:
                    ccs.piQ_dumax = u1;
                    ccs.piQ_Umax = u2;
                    ccs.piR_Ref = s3;
                    break;
                case 6:
                    ccs.piR_dumax = u1;
                    ccs.piR_Umax = u2;
                    ccs.piK_qd = u3;
                    break;
                case 7:
                    ccs.piM_Kp = u1;
                    ccs.piM_Ki = u2;
                    break;
                case 8:
                    ccs.piM_dumax = u1;
                    ccs.piM_Umax = u2;
                    break;
            }
        }

        private void ProcessFrame116(ushort u1, ushort u2, ushort u3, ushort u4, short s1, short s2, short s3)
        {
            switch (u4)
            {
                case 0:
                    M1.ccs.umode = u1;
                    M2.ccs.umode = u2;
                    ccs.sbit = u3;
                    // Обновить состояние UI, зависящее от sbit (в UI-потоке)
                    if (IsHandleCreated)
                    {
                        BeginInvoke((Action)(() => sbit_check()));
                    }
                    break;
                case 1:
                    ccs.A_SPI = s1;
                    ccs.A_em = s2;
                    ccs.A_em0 = s3;
                    break;
                case 2:
                    ccs.A_QEP = s1;
                    ccs.piD_i = s2;
                    ccs.piQ_i = s3;
                    break;
                case 3:
                    ccs.speedRef = u1;
                    ccs.slipRef = u2;
                    ccs.slipMax = u3;
                    break;
                case 4:
                    M1.ccs.errors = u1;
                    M2.ccs.errors = u2;
                    break;
                case 5:
                    ccs.m1_ID = u1;
                    ccs.m2_ID = u2;
                    break;
                case 6:
                    M1.ccs.bits = u1;
                    M2.ccs.bits = u2;
                    ccs.A_rg = s3;
                    // Обновляем UI, зависящий от битов мотор-статуса
                    if (IsHandleCreated)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            mbit_check(M1);
                            mbit_check(M2);
                        }));
                    }
                    break;
                case 7:
                    //         d1 = motor1.flag.all;
                    //        d2 = motor2.flag.all;
                    break;
            }
        }

        private void ProcessFrame117(short s1, short s2, short s3, short s4)
        {
            M1.ccs.slip_abs = s1;
            M1.ccs.slip_absF = M1.ccs.slip_absF * 0.9f + s1 * 0.1f;
            M2.ccs.slip_abs = s2;
            M2.ccs.slip_absF = M2.ccs.slip_absF * 0.9f + s2 * 0.1f;
            ccs.Istend_rf = s3;
            ccs.Pa_stend = s4;
        }

        private void ProcessFrame118(short s1, short s2, short s3, short s4)
        {
            _regBuffer[0] = s1;
            _regBuffer[1] = s2;
            M1.ccs.mPabc = ConvertRegistersToFloat(_regBuffer);
            M1.ccs.mPabcF = M1.ccs.mPabcF * 0.95 + M1.ccs.mPabc * 0.05;

            _regBuffer[0] = s3;
            _regBuffer[1] = s4;
            M2.ccs.mPabc = ConvertRegistersToFloat(_regBuffer);
            M2.ccs.mPabcF = M2.ccs.mPabcF * 0.95 + M2.ccs.mPabc * 0.05;
        }

        private void ProcessFrame119(short s1, short s2, short s3, short s4)
        {
            M1.ccs.Vd = s1;
            M1.ccs.Vq = s2;
            M2.ccs.Vd = s3;
            M2.ccs.Vq = s4;
        }

        private void ProcessFrame120(short s1, ushort u2, ushort u4)
        {
            M2.ccs.IpVd = s1;
            M2.ccs.tooths = _tooths = u2;
            if (ccs.f9_tooths != u4)
            {
                if (frm9 != null)
                {
                    f9run = false;
                }
            }
            ccs.f9_tooths = u4;
        }

        private void ProcessFrame121(short s1, short s2, short s3, short s4)
        {
            VdRef = s1;
            VdOut = s2;
            VqOut = s3;
            RpmOut = s4;
        }

        private void ProcessFrame122(short s1, short s2, short s3, short s4)
        {
            ccs.QEP2_pr = s1;
            ccs.QEP2_fr = s2;
            ccs.QEP2_n3 = s3;
            ccs.QEP2_dg = s4;
            if (_approxLoadSabs2 != null)
            {
                _approxLoadSabs2.XIn = s2 * 0.2;
            }
        }

        private void ProcessFrame123(short s1, short s2, short s3, ushort u4)
        {
            ccs.pk1_Id = M1.ccs.Id = s1;
            ccs.pk1_Iq = M1.ccs.Iq = s2;
            ccs.pk1_cos = M1.ccs.Dcos = s3;
        }

        private void ProcessFrame124(short s1, short s2, short s3, ushort u4)
        {
            ccs.pk2_Id = M2.ccs.Id = s1;
            ccs.pk2_Iq = M2.ccs.Iq = s2;
            ccs.pk2_cos = M2.ccs.Dcos = s3;
            M2.ccs.c_ImF = u4;
        }

        private void ProcessFrame125(short s2, short s3, short s4)
        {
            ccs.pk1_Pz = s2;
            ccs.pk1_Px = s3;
            ccs.pk1_fui = s4;
        }

        private void ProcessFrame126(short s1, short s2, short s3, short s4)
        {
            ccs.pk2_Pa = s1;
            ccs.pk2_Pz = s2;
            ccs.pk2_Px = s3;
            ccs.pk2_fui = s4;
        }

        private void ProcessFrame127(short s1, short s2, short s3, short s4)
        {
            ccs.QEP1_pr = s1;
            ccs.QEP1_fr = s2;
            ccs.QEP1_n3 = s3;
            ccs.QEP1_dg = s4;
            if (_approxLoadSabs1 != null)
            {
                _approxLoadSabs1.XIn = s2 * 0.2;
            }
        }

        private void ProcessFrame132(ushort u1, ushort u2, ushort u3, ushort u4)
        {
            if (u4 == 32012)
            {
                bool isError = u3 != 0;
                BeginInvoke(new Action(() => 
                { 
                    pb_ErrView.Visible = pb_ErrView.Enabled = isError;
                }));
            }

            if (_errFrm != null)
            {
                if (u4 == 32010)
                {
                    _errFrm._errorsArray[u1].Word = u2;
                    _errFrm._errorsArray[u1 + 1].Word = u3;
                }
                else if (u4 == 32011)
                {
                    _errFrm._enTableUpd = true;
                    BeginInvoke(new Action(() => { _errFrm.UpdateErrorList(); }));
                }
            }
        }

        #endregion

        #region Flag and State Checks

        public void mbit_check(Motor m)
        {
            ushort bits = m.ccs.bits;
            bool isDG = (m.DG.Text == "Г");

            UpdateControlColor((mbits.Gen_Engine_SW & bits) != 0, isDG, (Control)m.DG);
            UpdateControlColor((mbits.AIN_Disable & bits) != 0, !m.AIN_Enable.Checked, m.AIN_Enable);
        }

        private void UpdateControlColor(bool deviceState, bool uiState, Control control)
        {
            control.BackColor = (deviceState == uiState) ? Color.Empty : Color.Yellow;
        }
        private void sbit_check()
        {
            ushort sbit_val = ccs.sbit;
            bool isConnected = (sbit_val & sbit.Connect) != 0;
            bool shouldConnect = tsmConnect.Text == "Отключить";

            if (isConnected == shouldConnect)
            {
                tsmConnect.BackColor = isConnected ? Color.Salmon : Color.DarkSeaGreen;
            }
            else
            {
      //          tsmConnect.BackColor = Color.Yellow;
            }

            UpdateBitCheckColor(btnStartStop, (sbit_val & sbit.Pusk) != 0, btnStartStop.Text == @"Стоп");
        }

        private void UpdateBitCheckColor(Control control, bool deviceState, bool uiState)
        {
            control.BackColor = (deviceState == uiState) ? Color.Empty : Color.Yellow;
        }

        public void mflag_check(Motor m)
        {
            uint flags = m.ccs.flags;

            bool pwmOn = (flags & mflag.pwm_On) != 0;

            // У однофазного моста (M1) отдельного поля-флага нет - состояние
            // ключей показывает сама кнопка btnInvPower, её ведёт GridUi_Refresh().
            if (m.AIN_Flag != null)
            {
                m.AIN_Flag.Text = pwmOn ? @"Вкл." : @"Выкл.";
                m.AIN_Flag.BackColor = pwmOn ? Color.Green : Color.Salmon;
            }

            m.Vd_Value.BackColor = ((flags & mflag.Vd_sat) != 0) ? Color.Yellow : Color.Empty;
            m.Vq_Value.BackColor = ((flags & mflag.Vq_sat) != 0) ? Color.Yellow : Color.Empty;
            m.Id_Value.BackColor = ((flags & mflag.Id_sat) != 0) ? Color.Yellow : Color.Empty;
            m.Iq_Value.BackColor = ((flags & mflag.Iq_sat) != 0) ? Color.Yellow : Color.Empty;
            m.load.nud.BackColor = ((flags & mflag.Irpm_sat) != 0) ? Color.LightGreen : Color.Empty;
        }

        public void mMode_check(Motor m)
        {
            var x = m.load;
            var y = m.gain;
            var ccsMode = m.ccs.umode;

            var loadH = ccsMode & 0xF;
            var loadL = (ccsMode >> 4) & 0xF;      // ✅ Bit shift вместо деления
            int umode = (ccsMode >> 8) & 0xF;      // ✅ Аналогично

            UpdateModeColor(x.modeH, x.modeH_kod, loadH);
            UpdateModeColor(x.modeL, x.modeL_kod, loadL);
            UpdateModeColor(y.modeH, y.modeH_kod, umode);
        }

        private void UpdateModeColor(Control control, int deviceCode, int uiCode)
        {
            control.BackColor = (deviceCode == uiCode) ? Color.Empty : Color.Yellow;
        }

        #endregion
    }
}

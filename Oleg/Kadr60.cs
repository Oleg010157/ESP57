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
                case 131:
                    ProcessFrame131(s1, s2, s3, u4);
                    break;
                case 132:
                    ProcessFrame132(u1, u2, u3, u4);
                    break;


            }
        }

        /// <summary>
        /// Консолидированный медленный кадр ID 140 (64 байта) = 8 блоков по 4 слова.
        /// Каждый блок разбирается тем же ProcessFrameXXX, что и раньше отдельный 8-байтный кадр.
        /// Порядок блоков задаётся MCU (REF_Kadr.c send_REF_kadr): 100,101,102,106,107,109,118,116.
        /// Порядок байт little-endian — как в Kadr60Rx.
        /// </summary>
        private void Parse140(TPCANMsgFD msg)
        {
            byte[] d = msg.DATA;

            // Чтение 16-битного слова по абсолютному индексу слова (0..31).
            ushort W(int w) => (ushort)((d[w * 2 + 1] << 8) | d[w * 2]);

            // Блок 0 (слова 0..3)   -> ID 100  (msg не используется в теле ProcessFrame100)
            ProcessFrame100(W(0), W(1), W(2), W(3));
            // Блок 1 (слова 4..7)   -> ID 101  (цикл по u4)
            ProcessFrame101(W(4), W(5), W(6), W(7), (short)W(4), (short)W(5), (short)W(6));
            // Блок 2 (слова 8..11)  -> ID 102  (цикл по u4)
            ProcessFrame102(W(8), W(9), W(10), W(11), (short)W(8), (short)W(9), (short)W(10));
            // Блок 3 (слова 12..15) -> ID 106  (цикл по u4)
            ProcessFrame106((short)W(12), (short)W(13), (short)W(14), (short)W(15), W(15));
            // Блок 4 (слова 16..19) -> ID 107
            ProcessFrame107((short)W(16), (short)W(17), (short)W(18), (short)W(19));
            // Блок 5 (слова 20..23) -> ID 109
            ProcessFrame109(W(20), W(21), W(22), W(23));
            // Блок 6 (слова 24..27) -> ID 118
            ProcessFrame118((short)W(24), (short)W(25), (short)W(26), (short)W(27));
            // Блок 7 (слова 28..31) -> ID 116  (цикл по u4)
            ProcessFrame116(W(28), W(29), W(30), W(31), (short)W(28), (short)W(29), (short)W(30));
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

        private void ProcessFrame131(short s1, short s2, short s3, ushort u4)
        {
            M1.ccs.We = s1;
            M2.ccs.We = s2;
            M1.ccs.WslipF = s3;
            ccs.k60Cnt = u4;
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
            UpdateControlColor((mbits.FOC_mode & bits) != 0, m.FOC.Checked, m.FOC);
            UpdateControlColor((mbits.AIN_Disable & bits) != 0, !m.AIN_Enable.Checked, m.AIN_Enable);
            UpdateControlColor((mbits.revers & bits) != 0, m.Revers.Checked, m.Revers);
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

            UpdateBitCheckColor(Reverse_cbox, (sbit_val & sbit.Reverse) != 0, Reverse_cbox.Checked);
            UpdateBitCheckColor(ResolverInverse, (sbit_val & sbit.ResolInverse) != 0, ResolverInverse.Checked);
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
            m.AIN_Flag.Text = pwmOn ? @"Вкл." : @"Выкл.";
            m.AIN_Flag.BackColor = pwmOn ? Color.Green : Color.Salmon;

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

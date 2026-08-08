using System.Reflection;

namespace ICDIBasic
{
    public partial class Form1
    {
        private static ushort FrlM; //Количество параметров ushort в кадре

        /* Выбор файла параметров медленного F2 кадра */
        private void F60GetData()
        {
            switch (f2kadrName)
            {
                case "SPL_Power":
                    SPL_Power(motK60);
                    break;
                case "SPL_Yokogawa":
                    SPL_Yokogawa(motK60);
                    break;
                case "SPL_IGBT":
                    SPL_IGBT(motK60);
                    break;
                case "F3_invertor":
                    F3_invertor();
                    break;
                case "F1_convertor":
                    F1_convertor();
                    break;
                case "F1_invertor":
                    F1_invertor();
                    break;
                case "SPL_ABB":
                    SPL_ABB();
                    break;
                default:
                    SPL_Power(motK60);
                    break;
            }
        }

        private void F3_invertor()
        {
            spL[0] = (ushort)M1.ccs.Ia; //
            spL[1] = (ushort)M1.ccs.Ib; //
            spL[2] = (ushort)M1.ccs.Ic; //
            spL[3] = (ushort)M1.ccs.Va; //

            spL[4] = (ushort)M1.ccs.Vb; //
            spL[5] = (ushort)M1.ccs.Vc; //
            spL[6] = (ushort)M1.ccs.radian; //
            spL[7] = (ushort)M1.ccs.rgOut; //

            spL[8] = (ushort)M1.ccs.freq; //
            spL[9] = (ushort)M1.ccs.gain; //
            spL[10] = ccs.IsrTicker;
      
            spL[11] = ccs.k60Cnt;

            FrlM = 12;

        }

        private void F1_convertor()
        {
            spL[0] = (ushort)ccs.Idc_kod; //
            spL[1] = (ushort)ccs.Udc_kod; //
            spL[2] = (ushort)ccs.Vin_kod; //
            spL[3] = ccs.k60Cnt;
         

            FrlM = 4;
        }

        private void F1_invertor()
        {
            spL[0] = (ushort)ccs.Ia_kod; //
            spL[1] = (ushort)ccs.Ib_kod; //
            spL[2] = (ushort)ccs.Idc_kod; //
            spL[3] = ccs.k60Cnt;

            FrlM = 4;
        }

        private void SPL_ABB()

        {
            spL[0] = 0;// (ushort)M1.ccs.Ia; //
            spL[1] = 1;// (ushort)M1.ccs.Ib; //
            spL[2] = 2;// (ushort)M1.ccs.Ic; //
            spL[3] = ccs.k60Cnt;

            FrlM = 4;
        }
        private void SPL_Power(Motor m)
        {
            spL[0] = (ushort)m.ccs.Ia_rms; //
            spL[1] = (ushort)m.ccs.Ib_rms; //
            spL[2] = (ushort)m.ccs.Ic_rms; //
            spL[3] = (ushort)m.ccs.Ir_rms; //
            spL[4] = (ushort)m.ccs.Ir_av; //

            spL[5] = (ushort)m.ccs.c_UmF; //``
            spL[6] = (ushort)m.ccs.c_ImF; //
            spL[7] = (ushort)m.ccs.gain; //
            spL[8] = (ushort)m.ccs.freq; //
            spL[9] = (ushort)m.ccs.slip_abs; //

            spL[10] = (ushort)m.ccs.mPabc; //
            spL[11] = (ushort)m.ccs.c_paF; //
            spL[12] = (ushort)m.ccs.c_pzF; //
            spL[13] = (ushort)m.ccs.c_pxF; //
            spL[14] = (ushort)m.ccs.c_cosF; //

            spL[15] = (ushort)m.ccs.Urms; //
            spL[16] = (ushort)m.ccs.Imag; //
            spL[17] = (ushort)m.ccs.We; //
            spL[18] = (ushort)m.ccs.WslipF; //
            spL[19] = (ushort)m.ccs.flags; //

            spL[20] = (ushort)ccs.Udc; //
            spL[21] = (ushort)m.ccs.bits; //
            spL[22] = (ushort)ccs.sbit; //
            spL[23] = (ushort)ccs.IsrTicker; //
            spL[24] = (ushort)ccs.k60Cnt; //

            spL[25] = (ushort)11; //
            spL[26] = (ushort)22; //
            spL[27] = (ushort)ccs.Pa_stend; //
            spL[28] = (ushort)ccs.Istend_rf; //

            FrlM = 29;

        }

        private void SPL_Yokogawa(Motor m)
        {
            // Parametrs from AIN1 SPSU
            spL[0] = (ushort)M1.ccs.Ia_rms; //
            spL[1] = (ushort)M1.ccs.Ib_rms; //
            spL[2] = (ushort)M1.ccs.Ic_rms; //
            spL[3] = (ushort)M1.ccs.Ir_rms; //
            spL[4] = (ushort)M1.ccs.Ir_av; //

            spL[5] = (ushort)M1.ccs.c_UmF; //
            spL[6] = (ushort)M1.ccs.c_ImF; //
            spL[7] = (ushort)M1.ccs.gain; //
            spL[8] = (ushort)M1.ccs.freq; //
            spL[9] = (ushort)M1.ccs.slip_abs; //

            spL[10] = (ushort)M1.ccs.mPabc; //
            spL[11] = (ushort)M1.ccs.c_paF; //
            spL[12] = (ushort)M1.ccs.c_pzF; //
            spL[13] = (ushort)M1.ccs.mSabc;    // полная мощность фазы А, Вт ->  средние значение за один период напряжения.
            spL[14] = (ushort)M1.ccs.mPaz; //

            spL[15] = (ushort)(M1.ccs.Urms * 10);
            spL[16] = (ushort)ccs.QEP1_n3; // M1.ccs.Imag; //
            spL[17] = (ushort)ccs.QEP2_n3;//M1.ccs.We; //
            spL[18] = (ushort)M1.ccs.WslipF; //
            spL[19] = (ushort)M1.ccs.flags; //

            spL[20] = (ushort)ccs.Udc; //
            spL[21] = (ushort)M1.ccs.bits; //
            spL[22] = (ushort)ccs.sbit; //
            spL[23] = (ushort)ccs.IsrTicker; //
            spL[24] = (ushort)ccs.k60Cnt; //

            spL[25] = (ushort)ccs.QEP2_fr; //  обороты с датчика T40F ( ШПСУ 6-ой участок)
            spL[26] = (ushort)22; //
            spL[27] = (ushort)ccs.Pa_stend; //
            spL[28] = (ushort)ccs.Istend_rf; //
            spL[29] = (ushort)11;

            // Parametrs from Yokogawa
            spL[30] = Yokogawa.Update_count;
            spL[31] = (ushort)(Yokogawa.UrmsA * 10);
            spL[32] = (ushort)(Yokogawa.UrmsB * 10);
            spL[33] = (ushort)(Yokogawa.IrmsA * 10);
            spL[34] = (ushort)(Yokogawa.IrmsB * 10);

            spL[35] = (ushort)(Yokogawa.PaA);
            spL[36] = (ushort)(Yokogawa.PaB);
            spL[37] = (ushort)(Yokogawa.LambdaA * 1000);
            spL[38] = (ushort)(Yokogawa.LambdaB * 1000);
            spL[39] = (ushort)(Yokogawa.fI1 * 10);

            spL[40] = (ushort)(Yokogawa.fI3 * 10);
            spL[41] = (ushort)(Yokogawa.Speed * 10);
            spL[42] = (ushort)(Yokogawa.Torque);
            spL[43] = (ushort)(Yokogawa.fU5);

            spL[44] = YokoLAN;
            spL[45] = (ushort)(Yokogawa.F2);
            spL[46] = (ushort)(Yokogawa.F15 * 10);
            spL[47] = (ushort)22;
            spL[48] = (ushort)(Yokogawa.F1);

            spL[49] = (ushort)(Yokogawa.P1);
            spL[50] = (ushort)(Yokogawa.P1);
            spL[51] = (ushort)(Yokogawa.P3);
            spL[52] = (ushort)(Yokogawa.P4);
            spL[53] = (ushort)(Yokogawa.SΣA);



            // Parametrs from AIN2 SPSU
            spL[54] = (ushort)M2.ccs.Ia_rms; //
            spL[55] = (ushort)M2.ccs.Ib_rms; //
            spL[56] = (ushort)M2.ccs.Ic_rms; //
            spL[57] = (ushort)M2.ccs.Ir_rms; //
            spL[58] = (ushort)M2.ccs.Ir_av; //

            spL[59] = (ushort)M2.ccs.c_UmF; //
            spL[60] = (ushort)M2.ccs.c_ImF; //
            spL[61] = (ushort)M2.ccs.gain; //
            spL[62] = (ushort)M2.ccs.freq; //
            spL[63] = (ushort)M2.ccs.slip_abs; //

            spL[64] = (ushort)M2.ccs.mPabc; //
            spL[65] = (ushort)M2.ccs.c_paF; //
            spL[66] = (ushort)M2.ccs.c_pzF; //
            spL[67] = (ushort)M2.ccs.c_pxF; //
            spL[68] = (ushort)M2.ccs.c_cosF; //

            spL[69] = (ushort)(M2.ccs.Urms * 10);
            spL[70] = (ushort)M2.ccs.Imag; //
            spL[71] = (ushort)M2.ccs.We; //
            spL[72] = (ushort)M2.ccs.WslipF; //
            spL[73] = (ushort)M2.ccs.flags; //

            spL[74] = (ushort)ccs.Udc; //
            spL[75] = (ushort)M2.ccs.bits; //
            spL[76] = (ushort)ccs.QEP1_fr; //
            spL[77] = (ushort)M1.ccs.Ia_av; //

            spL[78] = (ushort)(Yokogawa.SΣB);

            FrlM = 79;

        }
        private void SPL_IGBT(Motor m)
        {
            spL[0] = (ushort)M1.ccs.VT1; //
            spL[1] = (ushort)M1.ccs.VT2;
            spL[2] = (ushort)M1.ccs.VT3;
            spL[3] = (ushort)M1.ccs.VT4;
            spL[4] = (ushort)M1.ccs.VT5;

            spL[5] = (ushort)M2.ccs.VT1;
            spL[6] = (ushort)M2.ccs.VT2;
            spL[7] = (ushort)M2.ccs.VT3;
            spL[8] = (ushort)M2.ccs.VT4;
            spL[9] = (ushort)M2.ccs.VT5;

            spL[10] = (ushort)ccs.IsrTicker; //
            spL[11] = (ushort)ccs.k60Cnt; //
            spL[12] = (ushort)ccs.P8Kadr; //

            FrlM = 13;

        }

    }
}

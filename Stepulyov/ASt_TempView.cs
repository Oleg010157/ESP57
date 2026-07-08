using Peak.Can.Basic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ICDIBasic.Stepulyov
{
    public partial class TemperatureView : Form
    {
        private readonly List<Control> _cList = new List<Control>();

        private readonly float[] _temps = new float[20];

        private bool _str;
        public TemperatureView()
        {
            InitializeComponent();
        }


        public void SetTemps(byte[] m)
        {
            try
            {
                var d1 = BitConverter.ToInt16(new[] { m[3], m[2] }, 0);
                var d2 = BitConverter.ToInt16(new[] { m[1], m[0] }, 0);
                var d3 = BitConverter.ToInt16(new[] {m[7], m[6] }, 0);
                var d4 = BitConverter.ToUInt16(new[] { m[5], m[4] }, 0);

                if (d1 == 32003 && d2 == 32003 && d3 == 32003 && d4 == 32003) _str = true;
                if (!_str) return;
                if (d4 == 32004)
                {
                    _temps[d1] = d2;
                    _temps[d1 + 1] = d3;
                }
                if (d1 != 32005 || d2 != 32005 || d3 != 32005 || d4 != 32005) return;
                _str = false;
                Upd();
            }
            catch
            {
                // ignored
            }
        }
        private void Upd()
        {
            var vtext = new string[_temps.Length];
            for (ushort i = 0; i < _temps.Length; i++)
            {
                _temps[i] = (float)Math.Round(_temps[i], 1);
                if (i >= 11) continue;
                vtext[i] = $"{_temps[i]:0.0}\u00B0C";

            }
            lbVT1_1.Text = $@"VT1: {vtext[0]}";

            lbVT2_1.Text = $@"VT2: {vtext[1]}";

            lbVT3_1.Text = $@"VT3: {vtext[2]}";

            lbVT4_1.Text = $@"VT4: {vtext[3]}";

            lbVT5_1.Text = $@"VT5: {vtext[4]}";

            lbVT5_2.Text = $@"VT5: {vtext[9]}";

            lbVT4_2.Text = $@"VT4: {vtext[8]}";

            lbVT3_2.Text = $@"VT3: {vtext[7]}";

            lbVT2_2.Text = $@"VT2: {vtext[6]}";

            lbVT1_2.Text = $@"VT1: {vtext[5]}";

            lbADC.Text = $@"T АЦП: {vtext[10]}";


            lbMot1_1.Text = $@"М1 обм: {_temps[12]:0.0}°C";

            lbMot2_1.Text = $@"М2 обм: {_temps[14]:0.0}°C";


            lbIut1.Text = $@"Iут {(_temps[15] / 10):0.0} uA";

        }
        private void S_Size(Size s)
        {
            Size = s;
            MaximumSize = s;
            MinimumSize = s;
            Size = s;
        }

    }
}
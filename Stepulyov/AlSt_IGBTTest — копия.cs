using Peak.Can.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ICDIBasic.Stepulyov
{
    public partial class AlStIgbtTest : Form
    {
        private readonly List<CheckBox> _cbl = new List<CheckBox>();

        public Form1.DSend128 Send128;
        short igbtnum = 0;
        public AlStIgbtTest()
        {
            InitializeComponent();
            Load += AlSt_IGBTTest_Load;
            btnImitOn.Click += BtnImitOn_Click;
            btnImitOff.Click += BtnImitOff_Click;
            btnImitOff.Enabled = false;
            Closing += (h, k) =>
            {
              //  Send128(0xA7, 0, 0, 338); // Выкл все IGBT
                Send128(0xA6, 21, 0, 338); // Выкл ручн. упр.
            };
        }

        private void BtnImitOn_Click(object sender, EventArgs e)
        {
            igbtnum = 21;
            Send128(0xA5, (ushort)igbtnum, 0, 338); // включаем верхнии ключи инвертора
        }
        private void BtnImitOff_Click(object sender, EventArgs e)
        {
            //          Send128(0xA7, 0, 0, 338); // Выкл все IGBT
            igbtnum = 21;
            Send128(0xA6, (ushort)igbtnum, 0, 338); // Выкл ручн. упр.
        }
        
        private void CbVT1_1_Click(object sender, EventArgs e)
        {
            var c = sender as CheckBox;
           
            if (c != null && c.Name == @"cbVT1_1") igbtnum |= 64; 
            if (c != null && c.Name == @"cbVT2_1") igbtnum |= 128;
            if (c != null && c.Name == @"cbVT3_1") igbtnum |= 256;
            if (c != null && c.Name == @"cbVT4_1") igbtnum |= 512;
            if (c != null && c.Name == @"cbVT5_1") igbtnum |= 1024;
            if (c != null && c.Name == @"cbVT6_1") igbtnum |= 2048;

            if (c != null && c.Name == @"cbVT1_2")
            {
                igbtnum |= 1;   // установить бит 0
                igbtnum &= ~2;  // сбросить бит 1
            }
            if (c != null && c.Name == @"cbVT2_2")
            {
                igbtnum |= 2;   // установить бит 1
                igbtnum &= ~1;  // сбросить бит 0
            }
            if (c != null && c.Name == @"cbVT3_2")
            {
                igbtnum |= 4;   // установить бит 2
                igbtnum &= ~8;  // сбросить бит 3
            }
            if (c != null && c.Name == @"cbVT4_2")
            {
                igbtnum |= 8;   // установить бит 3
                igbtnum &= ~4;  // сбросить бит 2
            }
            if (c != null && c.Name == @"cbVT5_2")
            {
                igbtnum |= 16;  // установить бит 4
                igbtnum &= ~32; // сбросить бит 5
            }
            if (c != null && c.Name == @"cbVT6_2")
            {
                igbtnum |= 32;  // установить бит 5
                igbtnum &= ~16; // сбросить бит 4
            }

 //           if (c != null && c.Name == @"cbVT7_1") igbtnum |= 4096;
 //           if (c != null && c.Name == @"cbVT7_2") igbtnum |= 8192;

            Send128(0xA7, (ushort)igbtnum, 0, 338);
        }
        private void AlSt_IGBTTest_Load(object sender, EventArgs e)
        {
            foreach (var c in gbAIN1.Controls.OfType<CheckBox>())
                _cbl.Add(c);
            foreach (var c in gbAIN2.Controls.OfType<CheckBox>())
                _cbl.Add(c);
         //   foreach (var c in _cbl)
              //  c.Click += CbVT1_1_Click;
        }
        public void FeedBack(byte[] m)
        {
            var cmd = BitConverter.ToUInt16(new[] {m[5], m[4] }, 0);
            if (cmd != 338) return;
            var d1 = BitConverter.ToInt16(new[] {m[3], m[2] }, 0);
            var d2 = BitConverter.ToInt16(new[] {m[1], m[0] }, 0);
            var d3 = BitConverter.ToInt16(new[] {m[7], m[6] }, 0);
            switch (d1)
            {
                case 0xA5:
                    btnImitOn.Enabled = false;
                    btnImitOff.Enabled = !btnImitOn.Enabled;
                    break;
                case 0xA6:
                    btnImitOn.Enabled = true;
                    btnImitOff.Enabled = !btnImitOn.Enabled;
                    break;
                case 0xA7:
                    cbVT1_1.Checked = Convert.ToBoolean(d3 & 1);
                    cbVT2_1.Checked = Convert.ToBoolean((d3 >> 1) & 1);
                    cbVT3_1.Checked = Convert.ToBoolean((d3 >> 2) & 1);
                    cbVT4_1.Checked = Convert.ToBoolean((d3 >> 3) & 1);
                    cbVT5_1.Checked = Convert.ToBoolean((d3 >> 4) & 1);
                    cbVT6_1.Checked = Convert.ToBoolean((d3 >> 5) & 1);

                    cbVT1_2.Checked = Convert.ToBoolean((d3 >> 6) & 1);
                    cbVT2_2.Checked = Convert.ToBoolean((d3 >> 7) & 1);
                    cbVT3_2.Checked = Convert.ToBoolean((d3 >> 8) & 1);
                    cbVT4_2.Checked = Convert.ToBoolean((d3 >> 9) & 1);
                    cbVT5_2.Checked = Convert.ToBoolean((d3 >> 10) & 1);
                    cbVT6_2.Checked = Convert.ToBoolean((d3 >> 11) & 1);
                    cbVT7_1.Checked = Convert.ToBoolean((d3 >> 12) & 1);
                    cbVT7_2.Checked = Convert.ToBoolean((d3 >> 13) & 1);
                    break;
            }
        }

        private void cbVT1_2_CheckedChanged(object sender, EventArgs e)
        {
            if (cbVT1_2.Checked) igbtnum |= 1;   // установить бит 0
            else igbtnum &= ~1;  // сбросить бит 0
        }

        private void cbVT2_2_CheckedChanged(object sender, EventArgs e)
        {
            if (cbVT2_2.Checked) igbtnum |= 2;   // установить бит 1
            else igbtnum &= ~2;  // сбросить бит 1
        }

        private void cbVT3_2_CheckedChanged(object sender, EventArgs e)
        {
            if (cbVT3_2.Checked) igbtnum |= 4;   // установить бит 3
            else igbtnum &= ~4;  // сбросить бит 3
        }

        private void cbVT4_2_CheckedChanged(object sender, EventArgs e)
        {
            if (cbVT4_2.Checked) igbtnum |= 8;   // установить бит 4
            else igbtnum &= ~8;  // сбросить бит 4
        }

        private void cbVT5_2_CheckedChanged(object sender, EventArgs e)
        {
            if (cbVT5_2.Checked) igbtnum |= 16;   // установить бит 5
            else igbtnum &= ~16;  // сбросить бит 5
        }

        private void cbVT6_2_CheckedChanged(object sender, EventArgs e)
        {
            if (cbVT6_2.Checked) igbtnum |= 32;   // установить бит 6
            else igbtnum &= ~32;  // сбросить бит 6
        }
    }
}
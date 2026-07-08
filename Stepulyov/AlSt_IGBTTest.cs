using Peak.Can.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;
using CheckBox = System.Windows.Forms.CheckBox;
using GroupBox = System.Windows.Forms.GroupBox;

namespace ICDIBasic.Stepulyov
{
    public partial class AlStIgbtTest : Form
    {

        public Form1.DSend128 Send128;
        // список команд
        public ushort send1 = 0xA5A5; 
        public ushort send2 = 0xA7A7;
        public ushort on1 = 0xA6A6;
        public ushort on2 = 0xA4A4;
        public ushort off1 = 0xA3A3;
        public ushort off2 = 0xA2A2;
        public ushort offall = 0xA1A1;

        short igbtnum = 0;
        private bool _suppressAutoSend;
        public AlStIgbtTest()
        {
            InitializeComponent();
            Load += AlSt_IGBTTest_Load;

            Closing += (h, k) =>
            {
                Send128(0xA6, 0, 0, 338); // Выкл ручн. упр.IGBT
            };
        }
        private void AlSt_IGBTTest_Load(object sender, EventArgs e)
        {
            ResetCheckboxes(gbAIN1);
            gbAIN1.Enabled = false;
            ResetCheckboxes(gbAIN2);
            gbAIN2.Enabled = false;
        }

        private void OnOff_IGBT1_Click(object sender, EventArgs e)
        {
            ResetCheckboxes(gbAIN1);
            OnOff_IGBT1.BackColor = System.Drawing.Color.Yellow;
            if (OnOff_IGBT1.Text == "Вкл")
            {
                gbAIN1.Enabled = true;
                igbtnum &= ~((short)(64 | 128 | 256 | 512 | 1024 | 2048 | 4096));
                Send128(on1, 0, 0, 338); // Вкл все ключи IGBT
                OnOff_IGBT1.Text = "Выкл";
            }
            else
            {
                gbAIN1.Enabled = false;
                Send128(off1, 0, 0, 338); // Выкл все ключи
                OnOff_IGBT1.Text = "Вкл";
            }
        }

        private void OnOff_IGBT2_Click(object sender, EventArgs e)
        {
            ResetCheckboxes(gbAIN2);
            OnOff_IGBT2.BackColor = System.Drawing.Color.Yellow;
            if (OnOff_IGBT2.Text == "Вкл")
            {
                gbAIN2.Enabled = true;
                igbtnum &= ~((short)(1 | 2 | 4 | 8 | 16 | 32));
                Send128(on2, 0, 0, 338); // Вкл все ключи IGBT
                OnOff_IGBT2.Text = "Выкл";
            }
            else
            {
                gbAIN2.Enabled = false;
                Send128(off2, 0, 0, 338); // Выкл все ключи IGBT
                OnOff_IGBT2.Text = "Вкл";
            }
        }

        private void ResetCheckboxes(GroupBox groupBox)
        {
            _suppressAutoSend = true;
            try
            {
                foreach (Control control in groupBox.Controls)
                {
                    if (control is CheckBox checkbox)
                    {
                        checkbox.Checked = false;
                        checkbox.Enabled = true;
                    }
                }
            }
            finally
            {
                _suppressAutoSend = false;
            }
        }

        private void SendIgbt1ConfigIfNeeded()
        {
            if (_suppressAutoSend || !gbAIN1.Enabled)
            {
                return;
            }

            Send128(send1, (ushort)igbtnum, 0, 338);
        }

        private void SendIgbt2ConfigIfNeeded()
        {
            if (_suppressAutoSend || !gbAIN2.Enabled)
            {
                return;
            }

            Send128(send2, (ushort)igbtnum, 0, 338);
        }


        private void cbVT1_2_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbVT1_2.Checked)
            {
                igbtnum &= ~1; // сбросить бит 0 VT1-0ff
                cbVT2_2.Enabled = true; // разблокировать чекбокс VT2
            }
            else
            {
                igbtnum |= 1; // установить бит 0 -> включить VT1
                cbVT2_2.Enabled = false; // блокировать чекбокс VT2
            }

            SendIgbt2ConfigIfNeeded();
        }

        private void cbVT2_2_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbVT2_2.Checked)
            {
                igbtnum &= ~2; // сбросить бит 1 -> выключить VT2
                cbVT1_2.Enabled = true; // разблокировать чекбокс VT1
            }
            else
            {
                igbtnum |= 2; // установить бит 1 -> включить VT2
                cbVT1_2.Enabled = false; // блокировать чекбокс VT1
            }

            SendIgbt2ConfigIfNeeded();

        }

        private void cbVT3_2_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbVT3_2.Checked)
            {
                igbtnum &= ~4; // сбросить бит 2 -> выключить VT3
                cbVT4_2.Enabled = true; // разблокировать чекбокс VT4
            }
            else
            {
                igbtnum |= 4; // установить бит 2 -> включить VT3
                cbVT4_2.Enabled = false; // блокировать чекбокс VT4
            }

            SendIgbt2ConfigIfNeeded();
            
        }

        private void cbVT4_2_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbVT4_2.Checked)
            {
                igbtnum &= ~8; // сбросить бит 3 -> выключить VT4
                cbVT3_2.Enabled = true; // разблокировать чекбокс VT3
            }
            else
            {
                igbtnum |= 8; // установить бит 3 -> включить VT4
                cbVT3_2.Enabled = false; // блокировать чекбокс VT3
            }

            SendIgbt2ConfigIfNeeded();
        }

        private void cbVT5_2_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbVT5_2.Checked)
            {
                igbtnum &= ~16; //  -> выключить VT5
                cbVT6_2.Enabled = true; // разблокировать чекбокс VT6
            }
            else
            {
                igbtnum |= 16; // -> включить VT5
                cbVT6_2.Enabled = false; // блокировать чекбокс VT6
            }

            SendIgbt2ConfigIfNeeded();
        }

        private void cbVT6_2_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbVT6_2.Checked)
            {
                igbtnum &= ~32; //  -> выключить VT6
                cbVT5_2.Enabled = true; // разблокировать чекбокс VT5
            }
            else
            {
                igbtnum |= 32; //  -> включить VT6
                cbVT5_2.Enabled = false; // блокировать чекбокс VT5
            }

            SendIgbt2ConfigIfNeeded();
        }

        private void cbVT1_1_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbVT1_1.Checked)
            {
                igbtnum &= ~64; // сбросить бит 6
                cbVT2_1.Enabled = true; // разблокировать VT2
            }
            else
            {
                igbtnum |= 64; // установить бит 6
                cbVT2_1.Enabled = false; // блокировать VT2
            }

            SendIgbt1ConfigIfNeeded();
        }

        private void cbVT2_1_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbVT2_1.Checked)
            {
                igbtnum &= ~128; // сбросить бит 7
                cbVT1_1.Enabled = true; // разблокировать VT1
            }
            else
            {
                igbtnum |= 128; // установить бит 7
                cbVT1_1.Enabled = false; // блокировать VT1
            }

            SendIgbt1ConfigIfNeeded();
        }

        private void cbVT3_1_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbVT3_1.Checked)
            {
                igbtnum &= ~256; // сбросить бит 8
                cbVT4_1.Enabled = true; // разблокировать VT4
            }
            else
            {
                igbtnum |= 256; // установить бит 8
                cbVT4_1.Enabled = false; // блокировать VT4
            }

            SendIgbt1ConfigIfNeeded();
        }

        private void cbVT4_1_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbVT4_1.Checked)
            {
                igbtnum &= ~512; // сбросить бит 9
                cbVT3_1.Enabled = true; // разблокировать VT3
            }
            else
            {
                igbtnum |= 512; // установить бит 9
                cbVT3_1.Enabled = false; // блокировать VT3
            }

            SendIgbt1ConfigIfNeeded();
        }

        private void cbVT5_1_CheckedChanged(object sender, EventArgs e)
        {
            if (cbVT5_1.Checked) igbtnum |= 1024;   // установить бит 10
            else igbtnum &= ~1024;  // сбросить бит 10

            SendIgbt1ConfigIfNeeded();
        }

        private void cbVT6_1_CheckedChanged(object sender, EventArgs e)
        {
            if (cbVT6_1.Checked) igbtnum |= 2048;   // установить бит 11
            else igbtnum &= ~2048;  // сбросить бит 11

            SendIgbt1ConfigIfNeeded();
        }

        private void cbVT7_1_CheckedChanged(object sender, EventArgs e)
        {
            if (cbVT7_1.Checked) igbtnum |= 4096;   // установить бит 12
            else igbtnum &= ~4096;  // сбросить бит 12

            SendIgbt1ConfigIfNeeded();
        }


    }
}
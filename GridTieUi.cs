using System;
using System.Drawing;
using System.Windows.Forms;

namespace ICDIBasic
{
    public partial class Form1
    {
        public const ushort CmdGridMode = 630;
        public const ushort CmdGridRun = 631;
        public const ushort CmdIsetRef = 632;
        public const ushort CmdUrefRms = 633;

        private CheckBox _chkGridMode;
        private NumericUpDown _nudIset;
        private NumericUpDown _nudUref;
        private NumericUpDown _nudFreq;

        private void InitGridTieUi()
        {
            var gb = new GroupBox
            {
                Text = "1ф 48→220",
                Location = new Point(4, 68),
                Size = new Size(342, 58),
                Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular),
                TabStop = false
            };

            _chkGridMode = new CheckBox
            {
                Text = "Сеть",
                AutoSize = true,
                Location = new Point(8, 22),
                TabIndex = 1
            };
            _chkGridMode.Click += GridMode_Click;

            var btnStart = new Button
            {
                Text = "Пуск",
                Location = new Point(58, 18),
                Size = new Size(48, 24),
                TabIndex = 2
            };
            btnStart.Click += GridStart_Click;

            var btnStop = new Button
            {
                Text = "Стоп",
                Location = new Point(108, 18),
                Size = new Size(48, 24),
                TabIndex = 3
            };
            btnStop.Click += GridStop_Click;

            _nudIset = MakeNud(0, 25, 0.1m, 2, 0);
            _nudUref = MakeNud(0, 250, 1m, 1, 220);
            _nudFreq = MakeNud(40, 70, 1m, 0, 50);

            gb.Controls.Add(_chkGridMode);
            gb.Controls.Add(btnStart);
            gb.Controls.Add(btnStop);
            AddLabeledNud(gb, "Iset A", _nudIset, 162, GridIset_Commit);
            AddLabeledNud(gb, "Uref V", _nudUref, 222, GridUref_Commit);
            AddLabeledNud(gb, "F Hz", _nudFreq, 282, GridFreq_Commit);

            groupBox_AIN1.Controls.Add(gb);
            gb.BringToFront();
        }

        private static NumericUpDown MakeNud(decimal min, decimal max, decimal step, int decimals, decimal value)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Increment = step,
                DecimalPlaces = decimals,
                Value = value,
                Size = new Size(52, 20),
                TabStop = true
            };
        }

        private static void AddLabeledNud(Control parent, string caption, NumericUpDown nud, int x, EventHandler commit)
        {
            var lb = new Label
            {
                Text = caption,
                AutoSize = true,
                Location = new Point(x, 10)
            };
            nud.Location = new Point(x, 26);
            nud.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    commit(s, e);
                }
            };
            nud.Leave += commit;
            parent.Controls.Add(lb);
            parent.Controls.Add(nud);
        }

        private void GridMode_Click(object sender, EventArgs e)
        {
            ushort mode = (ushort)(_chkGridMode.Checked ? 1 : 0);
            uint_128(mode, 3333, 0, CmdGridMode);
        }

        private void GridStart_Click(object sender, EventArgs e)
        {
            _chkGridMode.Checked = true;
            uint_128(2, 3333, 0, CmdGridRun);
        }

        private void GridStop_Click(object sender, EventArgs e)
        {
            uint_128(1, 3333, 0, CmdGridRun);
        }

        private void GridIset_Commit(object sender, EventArgs e)
        {
            ushort d1 = (ushort)Math.Round(_nudIset.Value * 100m);
            uint_128(d1, 3333, 0, CmdIsetRef);
        }

        private void GridUref_Commit(object sender, EventArgs e)
        {
            ushort d1 = (ushort)Math.Round(_nudUref.Value * 10m);
            uint_128(d1, 3333, 0, CmdUrefRms);
        }

        private void GridFreq_Commit(object sender, EventArgs e)
        {
            uint_128((ushort)_nudFreq.Value, 3333, 0, 624);
        }
    }
}

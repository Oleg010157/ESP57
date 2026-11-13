using System;
using System.Drawing;
using System.Windows.Forms;

namespace ICDIBasic
{
    /*
     * Счётчики энергии прибора, кВт·ч: «Измерения → Счётчики энергии…».
     *
     * Считает и хранит их сам прибор (INVERTOR_CCS/my_Files/energy.h): по P на
     * выходе инвертора, только при открытых ключах, отдано и принято раздельно,
     * у каждого «всего» и «с последнего сброса». Команда 345: d1 = 0 - прочитать,
     * d1 = 1 и d2 = 3333 - обнулить «с сброса»; ответ - четыре ACK, d1 - номер
     * счётчика, d2/d3 - младшее и старшее слово Вт·ч. Ходит одинаково по CAN и
     * через WiFi-мост. То же на странице моста, панель «Инвертор».
     *
     * Окно отдельное, а не на панели инвертора: та свёрстана в дизайнере
     * плотно, и вставлять в неё из кода - значит наложить одно на другое.
     */
    public partial class Form1
    {
        private const ushort CmdEnergy = 345;
        private EnergyForm _energy;

        private void Energy_InitMenu(ToolStripMenuItem parent)
        {
            var mi = new ToolStripMenuItem("Счётчики энергии…");
            parent.DropDownItems.Add(mi);
            mi.Click += (s, e) =>
            {
                if (_energy == null || _energy.IsDisposed)
                {
                    _energy = new EnergyForm();
                    _energy.Ask += () => uint_128(0, 0, 0, CmdEnergy);
                    _energy.ResetAsked += () => uint_128(1, 3333, 0, CmdEnergy);
                }
                _energy.Show(this);
                _energy.Activate();
                uint_128(0, 0, 0, CmdEnergy);
            };
        }

        // ACK 345 - MainWindow.cs: d1 - номер счётчика, d2/d3 - Вт·ч.
        private void Energy_Ack(ushort k, ushort lo, ushort hi)
        {
            if (_energy != null && !_energy.IsDisposed)
                _energy.Set(k, (uint)lo | ((uint)hi << 16));
        }
    }

    internal sealed class EnergyForm : Form
    {
        private static readonly string[] Names =
            { "Отдано, всего", "Принято, всего", "Отдано с сброса", "Принято с сброса" };

        private readonly Label[] _v = new Label[4];
        private readonly Label _note = new Label { AutoSize = true, ForeColor = Color.DimGray };
        private readonly Timer _t = new Timer { Interval = 2000 };

        public event Action Ask;
        public event Action ResetAsked;

        public EnergyForm()
        {
            Text = "Счётчики энергии";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(330, 190);
            Font = new Font("Microsoft Sans Serif", 10F);

            for (int i = 0; i < 4; i++)
            {
                Controls.Add(new Label { Text = Names[i], Location = new Point(12, 14 + 28 * i), AutoSize = true });
                _v[i] = new Label
                {
                    Text = "—", Location = new Point(170, 12 + 28 * i), Size = new Size(148, 22),
                    TextAlign = ContentAlignment.MiddleRight, Font = new Font(Font, FontStyle.Bold)
                };
                Controls.Add(_v[i]);
            }

            var reset = new Button { Text = "Сбросить «с сброса»", Location = new Point(12, 128), Size = new Size(180, 28) };
            reset.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Обнулить счётчики «с сброса»? «Всего» останутся как есть.",
                                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    ResetAsked?.Invoke();
            };
            Controls.Add(reset);
            _note.Location = new Point(12, 164);
            _note.Text = "жду ответа прибора…";
            Controls.Add(_note);

            _t.Tick += (s, e) => Ask?.Invoke();
            Shown += (s, e) => _t.Start();
            FormClosed += (s, e) => _t.Dispose();
        }

        public void Set(int k, uint wh)
        {
            if (k < 0 || k >= _v.Length) return;
            _v[k].Text = (wh / 1000.0).ToString("0.000") + " кВт·ч";
            _note.Text = "обновлено " + DateTime.Now.ToString("HH:mm:ss");
        }
    }
}

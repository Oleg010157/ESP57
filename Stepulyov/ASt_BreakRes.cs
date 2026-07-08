using System;
using System.Drawing;
using System.Windows.Forms;

namespace ICDIBasic.Stepulyov
{
    public partial class AStBreakRes : Form
    {
        private readonly byte _csw;
        private int _brRes1;
        private int _brRes2;
        private bool _sym;
        public Form1.DSend128 Ds128;
        public AStBreakRes(byte id)
        {
            _csw = id;
            InitializeComponent();
        }
        private void BrResForm_Load(object sender, EventArgs e)
        {
            cbSymmetry.CheckedChanged += (a, s) =>
            {
                _sym = cbSymmetry.Checked;
                nudBrRes2.Enabled = tbBrRes2.Enabled = !_sym;
                if (_sym)
                {
                    nudBrRes2.Value = nudBrRes1.Value;
                    tbBrRes2.Value = tbBrRes1.Value;
                    _brRes2 = _brRes1;
                }
                Ds128((ushort)_brRes1, (ushort)_brRes2, 0, _csw);
            };
            tbBrRes1.ValueChanged += (a, s) =>
            {
                _brRes1 = tbBrRes1.Value;
                nudBrRes1.Value = _brRes1;
                if (_sym)
                {
                    nudBrRes2.Value = tbBrRes2.Value = _brRes1;
                    _brRes2 = _brRes1;
                }
                Ds128((ushort)_brRes1, (ushort)_brRes2, 0, _csw);
            };
            tbBrRes2.ValueChanged += (a, s) =>
            {
                if (!_sym)
                {
                    _brRes2 = tbBrRes2.Value;
                    nudBrRes2.Value = _brRes2;
                }
                Ds128((ushort)_brRes1, (ushort)_brRes2, 0, _csw);
            };
            nudBrRes1.ValueChanged += (a, s) =>
            {
                _brRes1 = (int)nudBrRes1.Value;
                tbBrRes1.Value = _brRes1;
                if (!_sym) return;
                nudBrRes2.Value = tbBrRes2.Value = _brRes1;
                _brRes2 = _brRes1;
            };
            nudBrRes2.ValueChanged += (a, s) =>
            {
                if (_sym) return;
                _brRes2 = (int)nudBrRes2.Value;
                tbBrRes2.Value = _brRes2;
            };
        }
        public void SetLoc(Point p)
        {
            if (!checkBox1.Checked) return;
            Location = p;
            Focus();
        }
    }
}
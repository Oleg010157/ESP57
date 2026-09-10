using System;
using System.Windows.Forms;

namespace ICDIBasic
{
    // Обработчики панели "1ф 48→220" (gbGridTie).
    // Сами контролы живут в MainWindow.Designer.cs и правятся в конструкторе форм.
    public partial class Form1
    {
        public const ushort CmdGridMode = 630;
        public const ushort CmdGridRun = 631;
        public const ushort CmdIsetRef = 632;
        public const ushort CmdUrefRms = 633;
        public const ushort CmdGridFreq = 624;

        private void GridNud_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            if (sender == nudGridIset) GridIset_Commit(sender, EventArgs.Empty);
            else if (sender == nudGridUref) GridUref_Commit(sender, EventArgs.Empty);
            else if (sender == nudGridFreq) GridFreq_Commit(sender, EventArgs.Empty);
        }

        private void GridMode_Click(object sender, EventArgs e)
        {
            ushort mode = (ushort)(chkGridMode.Checked ? 1 : 0);
            uint_128(mode, 3333, 0, CmdGridMode);
        }

        private void GridStart_Click(object sender, EventArgs e)
        {
            chkGridMode.Checked = true;
            uint_128(2, 3333, 0, CmdGridRun);
        }

        private void GridStop_Click(object sender, EventArgs e)
        {
            uint_128(1, 3333, 0, CmdGridRun);
        }

        private void GridIset_Commit(object sender, EventArgs e)
        {
            ushort d1 = (ushort)Math.Round(nudGridIset.Value * 100m);
            uint_128(d1, 3333, 0, CmdIsetRef);
        }

        private void GridUref_Commit(object sender, EventArgs e)
        {
            ushort d1 = (ushort)Math.Round(nudGridUref.Value * 10m);
            uint_128(d1, 3333, 0, CmdUrefRms);
        }

        private void GridFreq_Commit(object sender, EventArgs e)
        {
            uint_128((ushort)nudGridFreq.Value, 3333, 0, CmdGridFreq);
        }
    }
}

using System;
using System.Windows.Forms;

namespace ICDIBasic
{
    public partial class ResistorChange : Form
    {
        public ResistorChange()
        {
            InitializeComponent();
        }
        public Form1 Frm1Resistor;


        #region Изменения ограничений по мин и макс
        private void voltMin_ValueChanged(object sender, EventArgs e)
        {
            voltMax.Minimum = voltMin.Value;
        }

        private void voltMax_ValueChanged(object sender, EventArgs e)
        {
            voltMin.Maximum = voltMax.Value;
        }
        #endregion 

        #region Сохранение настроек
        private void button_save_Click(object sender, EventArgs e)
        {
            Frm1Resistor.Change_VoltLims();
            this.Hide();
        }
        #endregion

        #region Form closing events
        private void Resistor_change_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }
        #endregion

    }
}

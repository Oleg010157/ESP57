using System;
using System.Windows.Forms;

namespace ICDIBasic
{
    public partial class FormEngName : Form
    {
        public EngineLog Frmengine;


        public FormEngName()
        {
            InitializeComponent();
        }


        #region Окно базы данных
        private void button_chose_Click(object sender, EventArgs e)
        {
            MessageBox.Show("В данный момент база данных пуста...",
            "Important Note",
            MessageBoxButtons.OK,
            MessageBoxIcon.Exclamation,
            MessageBoxDefaultButton.Button1);
        }
        #endregion



        #region Выход из формы и сохранение

        private void button_save_Click(object sender, EventArgs e)
        {
            Frmengine.label7.Text = textBox1.Text;
            this.Hide();
        }

        #endregion


        #region закрытие окна
        private void f_engname_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel = true;  // Команда позволяет возвращаться к данному окну многократно
        }
        #endregion

    }
}

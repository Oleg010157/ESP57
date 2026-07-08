using System;
using System.Windows.Forms;

namespace ICDIBasic
{

    public partial class EngineLog : Form
    {

        #region конструкторы 
        private string _engineName;
        public string PassEngineName
        {
            get { return _engineName; }
            set { _engineName = value; }
        }

        private int _maxSAbs;
        public int PassMaxSAbs
        {
            get { return _maxSAbs; }
            set { _maxSAbs = value; }
        }

        #endregion
        public EngineLog()
        {
            InitializeComponent();
        }

        #region Выбор парамтеров по умолчанию
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                textBox_pole.Text = "2";
                textBox2.Text = "0.4";
                textBox3.Text = "0.2";
                textBox4.Text = "0.316423";
                textBox5.Text = "0.316423";
                textBox6.Text = "2.5";
            }
        }
        #endregion


        #region Ввод входных параметров вручную.
        private void button2_Click(object sender, EventArgs e)
        {
            _fNameinput.Frmengine = this;
            _fNameinput.Show();
        }
        #endregion

        FormEngName _fNameinput = new FormEngName();
        public Form1 Frm1;

        #region Сохранение параметров
        private void button1_Click(object sender, EventArgs e)
        {
            /*           float pass = 0;                               // переменная для передачи максимального скольжения
                       int pass2 = 0;
                       if (float.TryParse(textBox6.Text, out pass))  // проверка -- передаем ли мы число
                       {
                           if (radioButton_abs1.Checked)
                           {
                               ((Form1)frm1).change_type = false;
                               ((Form1)frm1).ref_max = pass;
                               ((Form1)frm1).max_sAbs_change();
                           }
                           else if (radioButton_percent1.Checked)
                           {
                               ((Form1)frm1).change_type = true;
                               ((Form1)frm1).ref_max = pass;
                               ((Form1)frm1).max_sAbs_change();
                           }
                       }
                       if (int.TryParse(textBox_pole.Text, out pass2))  
                       {
                           if (pass2 > 0)
                               ((Form1)frm1).pole_pairs = (short)pass2;        // Передача новых данных о частоте
                           else
                               ((Form1)frm1).pole_pairs = 2;
                       }
                       ((Form1)frm1).groupBox_АИН1.Text = "Двигатель 1 " + label7.Text;
                       ((Form1)frm1).groupBox_AIN2.Text = "Двигатель 2 " + label7.Text;
                       this.Hide();
             */
        }
        #endregion

        #region Закрытие окна
        private void Engine_log_FormClosing(object sender, FormClosingEventArgs e)
        {
            int distance;
            if (int.TryParse(textBox6.Text, out distance))
            {
                _maxSAbs = distance;
            }
            else { _maxSAbs = 0; }
            this.Hide();
            e.Cancel = true;  // Команда позволяет возвращаться к данному окну многократно
        }
        #endregion

        #region Выбор процентов для Макс. Скольжения
        private void radioButton_percent1_CheckedChanged(object sender, EventArgs e)
        {
            int pass = 0;
            if (int.TryParse(textBox6.Text, out pass))  // проверка -- передаем ли мы число
            {
                if (pass >= 100)
                    textBox6.Text = "100";
            }
        }
        #endregion 

        private void Engine_log_Load(object sender, EventArgs e)
        {

        }

    }
}

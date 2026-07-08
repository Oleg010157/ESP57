using System;
using System.Windows.Forms;

namespace ICDIBasic
{
    public partial class form_max_sAbs : Form
    {
        public form_max_sAbs()
        {
            InitializeComponent();
        }

        public Form1 frm1_sabs;


        #region Сохранение параметров максимального скоьжения
        /*
        private void button_save_Click(object sender, EventArgs e)
        {
            decimal pass = 0;
            if (decimal.TryParse(text_sAbs_max.Text, out pass))  // проверка -- передаем ли мы число
            {
                Общак.sAbs_max = pass;
            }
            else text_sAbs_max.Text = "введите число";

           

        }
         */ 
        #endregion


        #region закрытие формы
        private void button_cancel_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void form_max_sAbs_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }

        #endregion

      

        private void form_max_sAbs_Load(object sender, EventArgs e)
        {
            text_sAbs_max.Text = Convert.ToString(Общак.sAbs_max);
            text_sRel_max.Text = Convert.ToString(Общак.sRel_max);
        }



        private void text_sabs_max_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ushort pass = 0;
                if (ushort.TryParse(text_sAbs_max.Text, out pass))  // проверка -- передаем ли мы число
                {
                    Общак.sAbs_max = pass;
                    frm1_sabs.NUD_Load.Maximum = Общак.sAbs_max;
                    frm1_sabs.TrackBar_Load.Maximum = Общак.sAbs_max;
                }
                else
                {
                    text_sAbs_max.Text = "введите число";
                }
            }
        }

        private void text_sRel_max_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ushort pass = 0;
                if (ushort.TryParse(text_sRel_max.Text, out pass))  // проверка -- передаем ли мы число
                {
                    Общак.sRel_max = pass;
                    frm1_sabs.NUD_Load.Maximum = Общак.sRel_max;
                    frm1_sabs.TrackBar_Load.Maximum = Общак.sRel_max;
                }
                else
                {
                    text_sRel_max.Text = "введите число";
                }
                    
            }
        }
    }

}

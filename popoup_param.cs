using System;
using System.Windows.Forms;

namespace ICDIBasic
{
    public partial class popoup_param : Form
    {
        public popoup_param()
        {
            InitializeComponent();
        }

        private void popoup_param_Load(object sender, EventArgs e)
        {

        }

        public Form1 frm1params;


 #region Выбор показываемых параметров Первый двигатель
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Urms.Checked == false) { frm1params.Udc1_value.Visible = false; frm1params.input1.Visible = false; }
            else if (checkBox_Urms.Checked == true) { frm1params.Udc1_value.Visible = true; frm1params.input1.Visible = true; }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            
            if (checkBox_Irms.Checked == false) { frm1params.Urms1_value.Visible = false; frm1params.U1_Text.Visible = false; }
            else if (checkBox_Irms.Checked == true) { frm1params.Urms1_value.Visible = true; frm1params.U1_Text.Visible = true; }
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Iu.Checked == false) { frm1params.I1a_value.Visible = false; frm1params.input2.Visible = false; }
            else if (checkBox_Iu.Checked == true) { frm1params.I1a_value.Visible = true; frm1params.input2.Visible = true; }
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Iv.Checked == false) { frm1params.I1b_value.Visible = false; frm1params.input3.Visible = false; }
            else if (checkBox_Iv.Checked == true) { frm1params.I1b_value.Visible = true; frm1params.input3.Visible = true; }
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Iw.Checked == false) { frm1params.I1c_value.Visible = false; frm1params.input4.Visible = false; }
            else if (checkBox_Iw.Checked == true) { frm1params.I1c_value.Visible = true; frm1params.input4.Visible = true; }
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_f.Checked == false) { frm1params.label_fref1.Visible = false; frm1params.fref1_value.Visible = false;  }
            else if (checkBox_f.Checked == true) { frm1params.label_fref1.Visible = true; frm1params.fref1_value.Visible = true;  }
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_P.Checked == false) { frm1params.panel1.Visible = false;}
            else if (checkBox_P.Checked == true) { frm1params.panel1.Visible = true; }
        }
        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            if (!checkBox_dbg.Checked) 
            {
                frm1params.active_dbg(false);
            }
            else
            {
                frm1params.active_dbg(true);
            }
        }

 #endregion 


 #region Выбор показываемых параметров Второй двигатель
        private void checkBox_Urms2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Urms2.Checked == false) { frm1params.groupBox_p21.Visible = false; }
            else if (checkBox_Urms2.Checked == true) { frm1params.groupBox_p21.Visible = true; }
        }
        private void checkBox_Irms2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Irms2.Checked == false) { frm1params.groupBox_p22.Visible = false; }
            else if (checkBox_Irms2.Checked == true) { frm1params.groupBox_p22.Visible = true; }
        }

        private void checkBox_Iu2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Iu2.Checked == false) { frm1params.groupBox_p23.Visible = false; }
            else if (checkBox_Iu2.Checked == true) { frm1params.groupBox_p23.Visible = true; }
        }

        private void checkBox_Iv2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Iv2.Checked == false) { frm1params.groupBox_p24.Visible = false; }
            else if (checkBox_Iv2.Checked == true) { frm1params.groupBox_p24.Visible = true; }
        }

        private void checkBox_Iw2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Iw2.Checked == false) { frm1params.groupBox_p25.Visible = false; }
            else if (checkBox_Iw2.Checked == true) { frm1params.groupBox_p25.Visible = true; }
        }

        private void checkBox_f2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_f2.Checked == false) { frm1params.groupBox_p26.Visible = false; }
            else if (checkBox_f2.Checked == true) { frm1params.groupBox_p26.Visible = true; }
        }

        private void checkBox_P2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_P2.Checked == false) { frm1params.panel20.Visible = false; }
            else if (checkBox_P2.Checked == true) { frm1params.panel20.Visible = true; }
        }

        private void checkBox_dbg2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_dbg2.Checked == false) { frm1params.panel4.Visible = false; }
            else if (checkBox_dbg2.Checked == true) { frm1params.panel4.Visible = true; }
        }
        #endregion

 #region Закрытие окна
        private void popoup_param_FormClosing(object sender, FormClosingEventArgs e)
        {
            дляВторогоДвигателяToolStripMenuItem.Checked = false;
            дляПервогоДвигателяToolStripMenuItem.Checked = false;
            this.Hide();
            e.Cancel = true;
        }
  #endregion 

 #region Закрытие самих окошек
        private void дляПервогоДвигателяToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (дляВторогоДвигателяToolStripMenuItem.Checked == true)
            {
                дляВторогоДвигателяToolStripMenuItem.Checked = false;
                panel2.Visible = false;
                panel1.Visible = true;
            }
            else
                дляПервогоДвигателяToolStripMenuItem.Checked = true;
        }

        private void дляВторогоДвигателяToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (дляПервогоДвигателяToolStripMenuItem.Checked == true)
            {
                дляПервогоДвигателяToolStripMenuItem.Checked = false;
                panel2.Visible = true;
                panel1.Visible = false;
            }
            else
                дляВторогоДвигателяToolStripMenuItem.Checked = true;
        }
  #endregion 
    }
}

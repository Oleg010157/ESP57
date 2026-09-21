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


 // Первый канал - однофазный мост, а не двигатель. Имена контролов в Form1
 // достались от моторной панели и со смыслом полей не совпадают, поэтому
 // здесь пара "чекбокс - что он прячет" выписана явно:
 //
 //      checkBox_Vin   Vin  - Vin_value   + Vin
 //      checkBox_U220  U220 - Urms1_value + Udc_name
 //      checkBox_Usec  Usec - I1a_value   + Idc_label
 //      checkBox_Ipri  Ipri - I1b_value   + input3
 //      checkBox_Isec  Isec - I1c_value   + input4
 #region Выбор показываемых параметров Первый канал - однофазный мост
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Vin.Checked == false) { frm1params.Vin_value.Visible = false; frm1params.Vin.Visible = false; }
            else if (checkBox_Vin.Checked == true) { frm1params.Vin_value.Visible = true; frm1params.Vin.Visible = true; }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {

            if (checkBox_U220.Checked == false) { frm1params.Urms1_value.Visible = false; frm1params.Udc_name.Visible = false; }
            else if (checkBox_U220.Checked == true) { frm1params.Urms1_value.Visible = true; frm1params.Udc_name.Visible = true; }
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Usec.Checked == false) { frm1params.I1a_value.Visible = false; frm1params.Idc_label.Visible = false; }
            else if (checkBox_Usec.Checked == true) { frm1params.I1a_value.Visible = true; frm1params.Idc_label.Visible = true; }
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Ipri.Checked == false) { frm1params.I1b_value.Visible = false; frm1params.input3.Visible = false; }
            else if (checkBox_Ipri.Checked == true) { frm1params.I1b_value.Visible = true; frm1params.input3.Visible = true; }
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Isec.Checked == false) { frm1params.I1c_value.Visible = false; frm1params.input4.Visible = false; }
            else if (checkBox_Isec.Checked == true) { frm1params.I1c_value.Visible = true; frm1params.input4.Visible = true; }
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

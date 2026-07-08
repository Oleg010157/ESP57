using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ICDIBasic
{

    public partial class FormImitation : Form
    {

        #region Основные данные и параметры
        public FormImitation()
        {
            InitializeComponent();
        }

        private Form1 frm1Imit;
        public ushort Udci = 0, Udci2 = 0, Iu1 = 0, Iw1 = 0, Iv1 = 0, Iu2 = 0, Iw2 = 0, Iv2 = 0;
        //        ushort _udcFlag = 20, _iu1Flag = 21, _iw1Flag = 22, _iv1Flag = 23, _iu2Flag = 24, _iw2Flag = 25, _iv2Flag = 26;
        int _initValue = 2060;
        int _i = 1;
        Control _currentControl, _deselectControl;


        public float Dbg = 1;
        public short[] ErrorIm = { 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1 };
        private ushort _cmdNr;  // номер параметра для передачи в CCS.

        NumericUpDown _nudCurrent = new NumericUpDown();  // создаёт виртуальный NUD для связи с Tbar.
        List<NumericUpDown> _acpActive = new List<NumericUpDown>();
        List<NumericUpDown> _acpAll = new List<NumericUpDown>();
        List<CheckBox> _checkAll = new List<CheckBox>();
        private bool _imitationError;

        public Form1 Frm1Imit { get => frm1Imit; set => frm1Imit = value; }

        // Загрузка формы
        #region Заполнение Листов
        private void Form_imitation_Load(object sender, EventArgs e)
        {
            _acpActive.Add(adcmirror0);
            _acpActive.Add(adcmirror1);
            _acpActive.Add(adcmirror2);
            _acpActive.Add(adcmirror5);
            _acpActive.Add(adcmirror10);
            _acpActive.Add(adcmirror13);
            _acpActive.Add(adcmirror14);
            _acpActive.Add(adcmirror15);

            _acpAll.Add(adcmirror0);
            _acpAll.Add(adcmirror1);
            _acpAll.Add(adcmirror2);
            _acpAll.Add(adcmirror3);
            _acpAll.Add(adcmirror4);
            _acpAll.Add(adcmirror5);
            _acpAll.Add(adcmirror6);
            _acpAll.Add(adcmirror7);
            _acpAll.Add(adcmirror8);
            _acpAll.Add(adcmirror9);
            _acpAll.Add(adcmirror10);
            _acpAll.Add(adcmirror11);
            _acpAll.Add(adcmirror12);
            _acpAll.Add(adcmirror13);
            _acpAll.Add(adcmirror14);
            _acpAll.Add(adcmirror15);

            _checkAll.Add(checkBox_adc0);
            _checkAll.Add(checkBox_adc1);
            _checkAll.Add(checkBox_adc2);
            _checkAll.Add(checkBox_adc3);
            _checkAll.Add(checkBox_adc4);
            _checkAll.Add(checkBox_adc5);
            _checkAll.Add(checkBox_adc6);
            _checkAll.Add(checkBox_adc7);
            _checkAll.Add(checkBox_adc8);
            _checkAll.Add(checkBox_adc9);
            _checkAll.Add(checkBox_adc10);
            _checkAll.Add(checkBox_adc11);
            _checkAll.Add(checkBox_adc12);
            _checkAll.Add(checkBox_adc13);
            _checkAll.Add(checkBox_adc14);
            _checkAll.Add(checkBox_adc15);
            _checkAll.Add(checkBox_oboroti);
            _checkAll.Add(checkBox_DBG);

        }
        #endregion

        #endregion

        #region Процедура сброса всего окна
        public void RESET_imitparams()
        {
            foreach (Control c in this.groupBox_ADC.Controls)
            {

                switch (c.GetType().ToString())
                {
                    case "System.Windows.Forms.TrackBar":
                        ((TrackBar)c).Value = 0;
                        break;
                    case "System.Windows.Forms.CheckBox":
                        (
                        (CheckBox)c).Checked = false;
                        break;
                    case "System.Windows.Forms.NumericUpDown":
                        (
                        (NumericUpDown)c).Value = 0;
                        break;
                }
            }
            foreach (Control c in this.groupBox1.Controls)
            {
                c.Enabled = false;
                if (c.GetType().ToString() == "System.Windows.Forms.CheckBox")
                {
                    ((CheckBox)c).Checked = false;
                }
            }
        }

        #endregion

        #region Имитация ошибок
        private void checkBox14_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Err_Ctrl.Checked)
            {
                _imitationError = true;
                foreach (Control c in this.groupBox1.Controls)
                {
                    switch (c.GetType().ToString())
                    {
                        case "System.Windows.Forms.CheckBox":
                            (
                            (CheckBox)c).Enabled = true;
                            break;
                        case "System.Windows.Forms.Panel":
                            ((Panel)c).BackColor = Color.Red;
                            break;
                    }
                }
                check_err.Enabled = true;
                Frm1Imit.cmd_128(37);
            }
            else
            {
                check_err.Checked = false;
                if (checkBox_DBG.Checked == false)
                    _imitationError = false;
                foreach (Control c in this.groupBox1.Controls)
                {
                    switch (c.GetType().ToString())
                    {
                        case "System.Windows.Forms.CheckBox":
                            (
                            (CheckBox)c).Checked = false;
                            ((CheckBox)c).Enabled = false;
                            break;
                        case "System.Windows.Forms.Panel":
                            ((Panel)c).BackColor = Color.Red;
                            break;
                    }
                }
                check_err.Enabled = false;
                Frm1Imit.cmd_128(47);
            }
        }

        private void check_err_CheckedChanged(object sender, EventArgs e)   // Включаем/выключаем сразу все ошибки
        {
            if (check_err.Checked)
            {
                foreach (Control c in this.groupBox1.Controls)
                {
                    if (c is CheckBox)
                        ((CheckBox)c).Checked = true;
                }
            }
            else
            {
                foreach (Control c in this.groupBox1.Controls)
                {
                    if (c is CheckBox)
                        ((CheckBox)c).Checked = false;
                }
            }
        }
        // ================================ОШИБКИ========================================
        #region Появление ошибки на одном из индикаторов
        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Err11.Checked)
            {
                panel0.BackColor = Color.Green;
                ErrorIm[0] = 0;
            }
            else
            {
                panel0.BackColor = Color.Red;
                ErrorIm[0] = 1;
            }
            error_change();
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked)
            {
                panel1.BackColor = Color.Green;
                ErrorIm[1] = 0;
            }
            else
            {
                panel1.BackColor = Color.Red;
                ErrorIm[1] = 1;
            }
            error_change();
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox4.Checked)
            {
                panel2.BackColor = Color.Green;
                ErrorIm[2] = 0;
            }
            else
            {
                panel2.BackColor = Color.Red;
                ErrorIm[2] = 1;
            }
            error_change();
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox5.Checked)
            {
                panel3.BackColor = Color.Green;
                ErrorIm[3] = 0;
            }
            else
            {
                panel3.BackColor = Color.Red;
                ErrorIm[3] = 1;
            }
            error_change();
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox6.Checked)
            {
                panel4.BackColor = Color.Green;
                ErrorIm[4] = 0;
            }
            else
            {
                panel4.BackColor = Color.Red;
                ErrorIm[4] = 1;
            }
            error_change();
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox7.Checked)
            {
                panel5.BackColor = Color.Green;
                ErrorIm[5] = 0;
            }
            else
            {
                panel5.BackColor = Color.Red;
                ErrorIm[5] = 1;
            }
            error_change();
        }

        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Err21.Checked)
            {
                panel11.BackColor = Color.Green;
                ErrorIm[7] = 0;
            }
            else
            {
                panel11.BackColor = Color.Red;
                ErrorIm[7] = 1;
            }
            error_change();
        }

        private void checkBox9_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Err22.Checked)
            {
                panel10.BackColor = Color.Green;
                ErrorIm[8] = 0;
            }
            else
            {
                panel10.BackColor = Color.Red;
                ErrorIm[8] = 1;
            }
            error_change();
        }

        private void checkBox10_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Err23.Checked)
            {
                panel9.BackColor = Color.Green;
                ErrorIm[9] = 0;
            }
            else
            {
                panel9.BackColor = Color.Red;
                ErrorIm[9] = 1;
            }
            error_change();
        }

        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Err24.Checked)
            {
                panel8.BackColor = Color.Green;
                ErrorIm[10] = 0;
            }
            else
            {
                panel8.BackColor = Color.Red;
                ErrorIm[10] = 1;
            }
            error_change();
        }

        private void checkBox12_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Err25.Checked)
            {
                panel7.BackColor = Color.Green;
                ErrorIm[11] = 0;
            }
            else
            {
                panel7.BackColor = Color.Red;
                ErrorIm[11] = 1;
            }
            error_change();
        }

        private void checkBox13_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_Err26.Checked)
            {
                panel6.BackColor = Color.Green;
                ErrorIm[12] = 0;
            }
            else
            {
                panel6.BackColor = Color.Red;
                ErrorIm[12] = 1;
            }
            error_change();
        }
        #endregion

        #endregion

        #region Closing params

        private void Form_imitation_FormClosing(object sender, FormClosingEventArgs e)
        {
            Frm1Imit.имитацияДанныхToolStripMenuItem1.Enabled = true;
            Frm1Imit.cmd_128(29);
            Frm1Imit.AdcDirect = false;
            this.Hide();
            e.Cancel = true;
        }


        #endregion

        #region Включение режима имитации АЦП структуры
        private void checkBox_adc0_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_offset.Checked)            // Если включена регулировка АЦП оффсета
            {
                if (checkBox_adc0.Checked)
                    adcmirror0.Enabled = true;      // Даем возможность изменять оффсет
                else
                    adcmirror0.Enabled = false;     // Исключаем её
            }
            else                                    // Если оффсет не включен
            {
                if (checkBox_adc0.Checked)          // Включаем режим регулирования
                {
                    _currentControl = adcmirror0;   // Передаем выбранный элемент переменной
                    set_up_control(_currentControl);// Активируем, присваиваем значение тракбару, присваиваем начальное значение
                    Iu1 = (ushort)adcmirror0.Value; // Переменная, хранит текущие данные
                    Frm1Imit.ImitationIu1 = true; // Активируем отправку данных в контроллер из основной формы
                    Frm1Imit.cmd_128(31);       // и соответствующий флаг
                }
                else
                {
                    _deselectControl = adcmirror0;  // снова передаем переменную
                    set_down_control(_deselectControl);
                    Frm1Imit.ImitationIu1 = false;
                    Frm1Imit.cmd_128(41);
                }
            }
        }

        private void checkBox_adc1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_offset.Checked)
            {
                if (checkBox_adc1.Checked)
                    adcmirror1.Enabled = true;
                else
                    adcmirror1.Enabled = false;
            }
            else
            {
                if (checkBox_adc1.Checked)
                {
                    _currentControl = adcmirror1;
                    set_up_control(_currentControl);
                    Iw1 = (ushort)adcmirror1.Value;
                    Frm1Imit.ImitationIw1 = true;
                    Frm1Imit.cmd_128(32);
                }
                else
                {
                    _deselectControl = adcmirror1;
                    set_down_control(_deselectControl);
                    Frm1Imit.ImitationIw1 = false;
                    Frm1Imit.cmd_128(42);
                }
            }
        }

        private void checkBox_adc2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_offset.Checked)
            {
                if (checkBox_adc2.Checked)
                    adcmirror2.Enabled = true;
                else
                    adcmirror2.Enabled = false;
            }
            else
            {
                if (checkBox_adc2.Checked)
                {
                    _currentControl = adcmirror2;
                    set_up_control(_currentControl);
                    Iv1 = (ushort)adcmirror2.Value;
                    Frm1Imit.ImitationIv1 = true;
                    Frm1Imit.cmd_128(33);
                }
                else
                {
                    _deselectControl = adcmirror2;
                    set_down_control(_deselectControl);
                    Frm1Imit.ImitationIv1 = false;
                    Frm1Imit.cmd_128(43);
                }
            }
        }

        private void checkBox_adc5_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_offset.Checked)
            {
                if (checkBox_adc5.Checked)
                    adcmirror5.Enabled = true;
                else
                    adcmirror5.Enabled = false;
            }
            else
            {
                if (checkBox_adc5.Checked)
                {
                    _currentControl = adcmirror5;
                    adcmirror5.Enabled = true;
                    adcmirror5.Value = 0;
                    trackBar_immit.Value = (int)adcmirror5.Value;
                    Udci = (ushort)adcmirror5.Value;
                    Frm1Imit.ImitationUdc = true;
                    Frm1Imit.cmd_128(30);
                }
                else
                {
                    _deselectControl = adcmirror5;
                    set_down_control(_deselectControl);
                    Frm1Imit.ImitationUdc = false;
                    Frm1Imit.cmd_128(40);
                }
            }
        }

        private void checkBox_adc10_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_offset.Checked)
            {
                if (checkBox_adc10.Checked)
                    adcmirror10.Enabled = true;
                else
                    adcmirror10.Enabled = false;
            }
            else
            {
                if (checkBox_adc10.Checked)
                {
                    _currentControl = adcmirror10;
                    adcmirror10.Enabled = true;
                    adcmirror10.Value = 0;
                    trackBar_immit.Value = (int)adcmirror10.Value;
                    Udci2 = (ushort)adcmirror10.Value;
                }
                else
                {
                    _deselectControl = adcmirror10;
                    set_down_control(_deselectControl);
                }
            }
        }



        private void checkBox_adc13_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_offset.Checked)
            {
                if (checkBox_adc13.Checked)
                    adcmirror13.Enabled = true;
                else
                    adcmirror13.Enabled = false;
            }
            else
            {
                if (checkBox_adc13.Checked)
                {
                    _currentControl = adcmirror13;
                    set_up_control(_currentControl);
                    Iu2 = (ushort)adcmirror13.Value;
                    Frm1Imit.ImitationIu2 = true;
                    Frm1Imit.cmd_128(34);
                }
                else
                {
                    _deselectControl = adcmirror13;
                    set_down_control(_deselectControl);
                    Frm1Imit.ImitationIu2 = false;
                    Frm1Imit.cmd_128(44);
                }
            }
        }

        private void checkBox_adc14_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_offset.Checked)
            {
                if (checkBox_adc14.Checked)
                    adcmirror14.Enabled = true;
                else
                    adcmirror14.Enabled = false;
            }
            else
            {
                if (checkBox_adc14.Checked)
                {
                    _currentControl = adcmirror14;
                    set_up_control(_currentControl);
                    Iw2 = (ushort)adcmirror14.Value;
                    Frm1Imit.ImitationIw2 = true;
                    Frm1Imit.cmd_128(35);
                }
                else
                {
                    _deselectControl = adcmirror14;
                    set_down_control(_deselectControl);
                    Frm1Imit.ImitationIw2 = false;
                    Frm1Imit.cmd_128(45);
                }
            }
        }

        private void checkBox_adc15_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_offset.Checked)
            {
                if (checkBox_adc15.Checked)
                    adcmirror15.Enabled = true;
                else
                    adcmirror15.Enabled = false;
            }
            else
            {
                if (checkBox_adc15.Checked)
                {
                    _currentControl = adcmirror15;
                    set_up_control(_currentControl);
                    Iv2 = (ushort)adcmirror15.Value;
                    Frm1Imit.ImitationIv2 = true;
                    Frm1Imit.cmd_128(36);
                }
                else
                {
                    _deselectControl = adcmirror15;
                    set_down_control(_deselectControl);
                    Frm1Imit.ImitationIv2 = false;
                    Frm1Imit.cmd_128(46);
                }
            }
        }



        private void set_up_control(Control c)
        {
            c.Enabled = true;
            ((NumericUpDown)c).Value = _initValue;
            trackBar_immit.Value = (int)((NumericUpDown)c).Value;
        }

        private void set_down_control(Control c)
        {
            c.Enabled = false;
            ((NumericUpDown)c).Value = 0;
        }

        #endregion

        #region Изменение имитационных данных кода с помощью NumericUpDown
        private void adcmirror0_ValueChanged(object sender, EventArgs e)
        {
            if (checkBox_code.Checked)
            {
                Iu1 = (ushort)adcmirror0.Value;
                if (_currentControl == adcmirror0)
                    trackBar_immit.Value = Iu1;
                //             Frm1Imit.send_129(Iu1, 0, 0, _iu1Flag);
            }
        }

        private void adcmirror1_ValueChanged(object sender, EventArgs e)
        {
            if (checkBox_code.Checked)
            {
                if (checkBox_code.Checked)
                    Iw1 = (ushort)adcmirror1.Value;
                if (_currentControl == adcmirror1)
                    trackBar_immit.Value = Iw1;
                //               Frm1Imit.send_129(Iw1, 0, 0, _iw1Flag);
            }
        }

        private void adcmirror2_ValueChanged(object sender, EventArgs e)
        {
            if (checkBox_code.Checked)
            {
                Iv1 = (ushort)adcmirror2.Value;
                if (_currentControl == adcmirror2)
                    trackBar_immit.Value = Iv1;
                //               Frm1Imit.send_129(Iv1, 0, 0, _iv1Flag);
            }
        }

        private void adcmirror5_ValueChanged(object sender, EventArgs e)
        {
            if (checkBox_code.Checked)
            {
                Udci = (ushort)adcmirror5.Value;
                if (_currentControl == adcmirror5)
                    trackBar_immit.Value = Udci;
                //               Frm1Imit.send_129(Udci, 0, 0, _udcFlag);
            }
        }

        private void adcmirror13_ValueChanged(object sender, EventArgs e)
        {
            if (checkBox_code.Checked)
            {
                Iu2 = (ushort)adcmirror13.Value;
                if (_currentControl == adcmirror13)
                    trackBar_immit.Value = Iu2;
                //               Frm1Imit.send_129(Iu2, 0, 0, _iu2Flag);
            }
        }

        private void adcmirror14_ValueChanged(object sender, EventArgs e)
        {
            if (checkBox_code.Checked)
            {
                Iw2 = (ushort)adcmirror14.Value;
                if (_currentControl == adcmirror14)
                    trackBar_immit.Value = Iw2;
                //             Frm1Imit.send_129(Iw2, 0, 0, _iw2Flag);
            }
        }
        private void adcmirror15_ValueChanged(object sender, EventArgs e)
        {
            if (checkBox_code.Checked)
            {
                Iv2 = (ushort)adcmirror15.Value;
                if (_currentControl == adcmirror15)
                    trackBar_immit.Value = Iv2;
                //                Frm1Imit.send_129(Iv2, 0, 0, _iv2Flag);
            }
        }
        #endregion

        #region Изменение показаний с помощью движка
        private void trackBar_moment_ValueChanged_1(object sender, EventArgs e)
        {

        }

        private void trackBar_immit_ValueChanged(object sender, EventArgs e)
        {
            _nudCurrent.Value = trackBar_immit.Value;
        }
        #endregion

        #region Выбор режима имитации
        // Имитация кода АЦП преобразования
        private void checkBox_codeVal_CheckedChanged_1(object sender, EventArgs e)
        {
            if (checkBox_code.Checked)
            {
                _initValue = 2060;
                if (checkBox_physVal.Checked)
                {
                    trackBar_immit.Enabled = true;
                    checkBox_physVal.Checked = false;
                    foreach (NumericUpDown c in _acpActive)     // Обнуляем все значения
                        c.Value = 0;
                }
                if (checkBox_volt.Checked) checkBox_volt.Checked = false;
                checkBox_offset.Enabled = true;  // Checkbox для задания offset значений 
                Frm1Imit.AdcCodeSwitch = "Def";
                groupBox_ADC.Enabled = true;
                foreach (Control c in this.groupBox_ADC.Controls)
                {
                    if (c.GetType().ToString() == "System.Windows.Forms.CheckBox")
                    {
                        ((CheckBox)c).Text = "ADC " + (16 - _i);
                        _i++;
                    }
                }
                _i = 1;
            }
            else
            {
                if (checkBox_physVal.Checked == false)
                    //                  groupBox_ADC.Enabled = false;
                    checkBox_offset.Enabled = false;
                foreach (Control c in this.groupBox_ADC.Controls)
                {
                    if (c.GetType().ToString() == "System.Windows.Forms.CheckBox")
                    {
                        ((CheckBox)c).Checked = false;
                    }
                }
            }
        }


        // Имитация физических данных для АЦП
        private void checkBox22_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_physVal.Checked)
            {
                if (checkBox_code.Checked) checkBox_code.Checked = false;
                if (checkBox_volt.Checked) checkBox_volt.Checked = false;
                Frm1Imit.AdcCodeSwitch = "Fizika";
                _initValue = 0;
                trackBar_immit.Enabled = false;
                groupBox_ADC.Enabled = true;
                checkBox_adc0.Text = "Iu1";
                checkBox_adc1.Text = "Iw1";
                checkBox_adc2.Text = "Iv1";
                checkBox_adc3.Text = "ADC3";
                checkBox_adc4.Text = "ADC4";
                checkBox_adc5.Text = "Udc";
                checkBox_adc6.Text = "ADC6";
                checkBox_adc7.Text = "ADC7";
                checkBox_adc8.Text = "ADC8";
                checkBox_adc9.Text = "ADC9";
                checkBox_adc10.Text = "ADC10";
                checkBox_adc11.Text = "ADC11";
                checkBox_adc12.Text = "ADC12";
                checkBox_adc13.Text = "Iu2";
                checkBox_adc14.Text = "Iw2";
                checkBox_adc15.Text = "Iv2";
                foreach (Control c in this.groupBox_ADC.Controls)
                {
                    if (c.GetType().ToString() == "System.Windows.Forms.Label")
                    {
                        ((Label)c).Visible = true;
                    }
                }
                trackBar_immit.Value = 0;
            }
            else
            {
                Frm1Imit.AdcCodeSwitch = "Def";
                if (!checkBox_code.Checked)
                {
                    //                   groupBox_ADC.Enabled = false;
                }
                foreach (Control c in this.groupBox_ADC.Controls)
                {
                    if (c.GetType().ToString() == "System.Windows.Forms.CheckBox")
                    {
                        ((CheckBox)c).Checked = false;
                    }
                    if (c.GetType().ToString() == "System.Windows.Forms.Label")
                    {
                        ((Label)c).Visible = false;
                    }
                }
            }
        }

        #endregion Конец режима имитации

        #region Кнопка Сброс
        private void button2_Click(object sender, EventArgs e)
        {
            Frm1Imit.cmd_128(48);
            RESET_imitparams();
        }
        #endregion

        #region DBG -- изменение фазы
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_DBG.Checked)
            {
                NUD_DBG.Enabled = true;
                _imitationError = true;
                _currentControl = NUD_DBG;
                trackBar_immit.Value = 2500;
                trackBar_immit.Minimum = 0;
                Dbg = 1;
            }
            else
            {
                NUD_DBG.Enabled = false;
                if (checkBox_Err_Ctrl.Checked == false)
                    _imitationError = false;
                trackBar_immit.Minimum = -5000;
                NUD_DBG.Value = 0;
                Dbg = 1;
            }
        }

        private void DBGUpDown1_ValueChanged(object sender, EventArgs e)
        {
            Dbg = (float)NUD_DBG.Value + 1;
            if (_currentControl == NUD_DBG)
                trackBar_immit.Value = (int)(2500 + NUD_DBG.Value * 2500);
            error_change();
        }

        private void error_change()
        {
            /*
            if (_imitationError)
   /            Frm1Imit.send_129((ushort)(1 * ErrorIm[0] + 2 * ErrorIm[1] + 4 * ErrorIm[2] + 8 * ErrorIm[3] + 16 * ErrorIm[4] +
            +32 * ErrorIm[5] + 128 * ErrorIm[7] + 256 * ErrorIm[8] + 512 * ErrorIm[9] + 1024 * ErrorIm[10] +
            +2048 * ErrorIm[11] + 4096 * ErrorIm[12]), 0, (ushort)(Dbg * 1000), 31);
            */
        }


        #endregion

        #region Показания приходящих данных в вольтах
        private void checkBox15_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_volt.Checked)
            {
                if (checkBox_code.Checked)         // Если был включен режим отображения физики или кода
                    checkBox_code.Checked = false;
                if (checkBox_physVal.Checked)
                {
                    adcmirror0.DecimalPlaces = 0;
                    adcmirror1.DecimalPlaces = 0;
                    adcmirror2.DecimalPlaces = 0;
                    adcmirror5.DecimalPlaces = 0;
                    adcmirror10.DecimalPlaces = 0;
                    adcmirror13.DecimalPlaces = 0;
                    adcmirror14.DecimalPlaces = 0;
                    adcmirror15.DecimalPlaces = 0;
                    checkBox_physVal.Checked = false;
                }
                Frm1Imit.AdcCodeSwitch = "Volt";
            }
            else
                Frm1Imit.AdcCodeSwitch = "Def";
        }
        #endregion

        #region Включение регулирования калибровочных значений
        private void checkBox16_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_offset.Checked)
            {
                button_saveOff.Enabled = true;
                Frm1Imit.DoOffset = true;
                Frm1Imit.AdcCodeSwitch = "Offset";
            }
            else
            {
                button_saveOff.Enabled = false;
                foreach (Control c in this.groupBox_ADC.Controls)
                {
                    if (c.GetType().ToString() == "System.Windows.Forms.CheckBox")
                    {
                        ((CheckBox)c).Checked = false;
                    }
                }
                Frm1Imit.AdcCodeSwitch = "Def";
            }
        }
        #endregion

        #region Offset_Values_check
        private void button1_Click(object sender, EventArgs e)
        {
            Frm1Imit.cmd_128(50);
            for (int j = 0; j < 16; j++)
            {
                if (_checkAll[j].Checked == true)               // Присваиваем новые значения для оффсетов
                    Frm1Imit.AdcOffset[j] = (int)_acpAll[j].Value;
            }
        }
        #endregion

        #region RPM_IMIT
        private void checkBox_oboroti_CheckedChanged(object sender, EventArgs e)
        {

            if (checkBox_oboroti.Checked)
            {

                NUD_rpmimit.Enabled = true;
                _nudCurrent = NUD_rpmimit;
                _cmdNr = 13;
                refresh_Tbar(_cmdNr);
            }
            else
            {
                NUD_rpmimit.Enabled = false;
                trackBar_immit.Enabled = false;

            }
        }
        private void NUD_current_ValueChanged(object sender, EventArgs e)
        {
            trackBar_immit.Value = (int)_nudCurrent.Value;
            imit_value_send(_cmdNr);
        }

        private void imit_value_send(ushort cmd)
        {

            //         Frm1Imit.send_Int16_129((short)_nudCurrent.Value, 0, 0, cmd);
        }

        private void refresh_Tbar(ushort cmd)
        {
            imit_value_send(cmd);
            trackBar_immit.Enabled = true;
            trackBar_immit.Minimum = (int)_nudCurrent.Minimum;
            trackBar_immit.Maximum = (int)_nudCurrent.Maximum;
            trackBar_immit.Value = (int)_nudCurrent.Value;
        }

        private void clr_checkBoxs()
        {
            foreach (CheckBox c in _checkAll)
            {
                c.Checked = false;
            }
        }

        #endregion

        private void checkBox_code_CheckedChanged(object sender, EventArgs e)
        {

        }


    }
}

using ButtonTable;
using CANRxDataTable;
using CANTxDataTable;
using FlagMask;
using ICDIBasic.Properties;
using ICDIBasic.Stepulyov;
using Peak.Can.Basic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.Keys;
using Label = System.Windows.Forms.Label;
using TPCANHandle = System.UInt16;

namespace ICDIBasic
{
    public partial class Form1 : Form
    {

        public Form1()
        {
            InitializeComponent();
            BackColor = Общак.Crem;

            KeyPreview = true;
            SyncCanLogUi();

        }

        



        

        public ABB inv1 = new ABB();
        public ABB inv2 = new ABB();



        public Motor motK60;  // указатель на объект класса Motor

        // CAN UI logger fields moved to `CanUiLogger.cs` (partial class) to keep logging logic in one file.

        
        



        private void KVU_CheckedChanged(object sender, EventArgs e)
        {
            //       KVU_switch.BackColor = Color.Yellow;


            // Load1 здесь не трогаем: у него один режим, Iзад (см. инициализацию).
            if (Separate_cbox.Checked)
            {
                Load2_ModeH.Text = @"RPM";
                Tbar_speed.Enabled = false;
                NUD_RPM.Enabled = false;
                button_Field_RPM.Enabled = false;
            }
            else
            {
                Load2_ModeH.Text = @"S abs";
                Tbar_speed.Enabled = true;
                NUD_RPM.Enabled = true;
                button_Field_RPM.Enabled = true;
            }
            Load_Mode_Refresh(M1);
            Load_Mode_Refresh(M2);
            /*
            ushort d1 = 0;
            if (KVU_switch.Checked && CandActive) d1 = 1;
            uint_128(d1, 0, 0, 68);
            */
        }

        //----------------------------------------------------------------------------------------------------------------
        // DG1 убрана вместе с командой 60: канал 1 - однофазный мост 48→220,
        // выбор "двигатель/генератор" к нему не относится. Для АИН-2 всё как было.
        private void DG2_Click(object sender, EventArgs e)
        {
            if (DG2.Text == "Д")
            {
                DG2.Text = "Г";
            }
            else
            {
                DG2.Text = "Д";
            }

            DG_send(DG2, 61);
        }

        private void DG_send(Label dg, ushort cmd)
        {
            ushort d1 = 0;
            dg.BackColor = Color.Yellow;
            if (dg.Text == "Г")
            {
                d1 = 1;
            }

            uint_128(d1, 0, 0, cmd);
        }

        private void button_Field_Click(object sender, EventArgs e)
        {
            if (button_Field_RPM.BackColor != Color.Yellow) // если клавиша жёлтая то не меняем ни название ни номер клавиши
            {
                switch (button_Field_RPM.Text)
                {
                    case "Поле":
                        button_Field_RPM.Text = "RPM1";
                        _rpmMode = 1;
                        break;
                    case "RPM1":
                        button_Field_RPM.Text = "RPM2";
                        _rpmMode = 2;
                        break;
                    case "RPM2":
                        button_Field_RPM.Text = "Поле";
                        _rpmMode = 0;
                        break;
                }
            }

            button_Field_RPM.BackColor = Color.Yellow;
            uint_128(_rpmMode, 3333, 0, 59);
        }

        /**************************************************************************************/
        private void cBox_Gain2_Enable_CheckedChanged(object sender, EventArgs e)
        {
            ushort d1;
            if (cBox_Gain_Limit.Checked)
            {
                d1 = 0;
            }
            else
            {
                d1 = 1;
            }

            uint_128(d1, 0, 0, 83);
        }

        private void resetPiREGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cmd_128(52);
        }

        private void Imax_Value_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Imax_Send();
            }

        }

        private void Imax_Send()
        {
            ushort d1, d3 = 0;
            ushort pass;
            Imax_Value.BackColor = Color.Yellow;
            if (ushort.TryParse(Imax_Value.Text, out pass))
            {
                d1 = pass;
            }
            else
            {
                Imax_Value.Text = "Число?";
                d1 = 500;
                d3 = 1;
            }
            int_128(d1, 3333, d3, 53);
        }

        #region Прием пакетов от КВУ по Ethernet - массив uint
        private void RxEthM(uint[] m)
        {
        }
        #endregion


        #region Основной таймер -   meterTimer

        private void meterTimer_Tick(object sender, EventArgs e)
        {

            AinTableUpdate();

      //      mflag_check(M1);
      //      mflag_check(M2);
      //      mMode_check(M1);
      //      mMode_check(M2);
     //       sbit_check();
     //       mbit_check(M1);  // проверка битов управления АИН
     //       mbit_check(M2);

            #region ======== Вывод  данных на окно индикации ==========

            Udc_value.Text = $@"{ccs.Udc_av * 0.1f:0.0}"; // фильтрованное напряжение на входе инвертора

            Invertor_view(); // приборы моста: Vin, Usec, Ipri, Isec, U220

            // Ic первой машины сюда больше не пишется: ячейку занял Isec моста,
            // и фазы C у однофазного моста нет. Вывод затирал бы кадр 140.
            Ir1_value.Text = $@"{M1.ccs.In_rms * 0.01:0.00}";

            if (I2rms_selector.Text == @"rms")
            {
                I2a_value.Text = $@"{M2.ccs.Ia_rms * 0.01:0.00}";
                I2b_value.Text = $@"{M2.ccs.Ib_rms * 0.01:0.00}";
                I2c_value.Text = $@"{M2.ccs.Ic_rms * 0.01:0.00}";
                //     Ir2_value.Text = $@"{M2.ccs.Ir_rms * 0.01:0.00}";
                Ir2_value.Text = $@"{11 * 1:0}";
            }
            else
            {
                I2a_value.Text = $@"{M2.ccs.Ia_av * 0.01:0.00}";
                I2b_value.Text = $@"{M2.ccs.Ib_av * 0.01:0.00}";
                I2c_value.Text = $@"{M2.ccs.Ic_av * 0.01:0.00}";
                Ir2_value.Text = $@"{M2.ccs.Ir_av * 0.01:0.00}";
            }

            //------------------------------------------------------------------------------------------
            // Первый канал - мост, не машина: ячейку "f.поля" занял прибор "F сети"
            // (пишется в Invertor_view), а "Uref" убран - уставка видна в шапке
            // панели, на движке nudGridUref. Обороты (rpm1) заняла постоянная
            // составляющая тока первички - см. Invertor_view().
            //------------------------------------------------------------------------------------------
            fref2_value.Text = $@"{ccs.f2_ref / 2.0:0}"; // Заданная частота поля второй машины об/мин
            field2_value.Text = $@"{ccs.f2_act * 0.2:0}";
            rpm2_value.Text = $@"{ccs.QEP2_n3 * 0.2:0.0}";
            Im2_value.Text = $@"{M2.ccs.c_ImF * 0.01:0.00}";
            //---------------------------------------------------------------------------------
            //      S1_abs.Text = $@"{M1.ccs.slip_absF * 0.001:0.00}";
            Torq_value.Text = $@"{22:0}";

            epsv1.Text = $@"{ccs.core_temp :0}";
            epsi1.Text = $@"{ccs.EPSi1 * 0.0572958:0.0}";
            //      epsv2.Text = $@"{ccs.EPSv2 * 0.0572958:0.0}";
            //      epsi2.Text = $@"{ccs.EPSi2 * 0.0572958:0.0}";
            epsv2.Text = $@"{ccs.EPSv2:0}";
            epsi2.Text = $@"{ccs.EPSi2:0}";

            sproc_view(M1);
            sproc_view(M2);

            Vd_view(M1);
            Vd_view(M2);
            Vq_view(M1);
            Vq_view(M2);
            Urms_view(M1);
            Urms_view(M2);
            Id_view(M1);
            Id_view(M2);
            Iq_view(M1);
            Iq_view(M2);

            // Кнопка питания моста и авария автомата - из кадра 140, который
            // идёт постоянно. Ответов на команды тут ждать нечего.
            GridUi_Refresh();
            //---------------------------------------------------------------------------------------
            // Мощности однофазного моста. Первый канал - это он, моторных
            // величин у него нет, и четвёрка полей до 23.09.2026 стояла
            // в нулях.
            //
            // Источник - кадр 140, слова 17..20: считает их CLA раз за период
            // сети по паре uSec и iSec, то есть по выходу инвертора. При
            // замкнутом контакторе это тот же узел, что и сеть, - значит та же
            // четвёрка показывает отдачу в сеть, отдельных полей не нужно.
            Pa1_value.Text = $@"{ccs.gtP:0}";
            cos1_clark_value.Text = $@"{ccs.gtQ:0}";
            Px1_value.Text = $@"{ccs.gtS:0}";
            PF1_value.Text = $@"{ccs.gtPF:0.0000}";

            // Постоянная составляющая в трансформаторе уехала в Invertor_view():
            // её место теперь среди измерений тракта, а не среди мощностей.

            Pa2_value.Text = $@"{M2.ccs.mPabcF:0}";
            P2abc_value.Text = $@"{M2.ccs.mPabc:0}";
            cos2_val.Text = $@"{M2.ccs.c_cosF:0}";
            PF2_value.Text = $@"{M2.ccs.PF:0.0000}";

            value_gain1_ccs.Text = $@"{M1.ccs.gain:####0}";
            label_gain2_ccs.Text = $@"{M2.ccs.gain:####0}";
            /************************************************************************************************/

            #endregion   ======== Вывод  данных на окно индикации ==========

            #region  Основной таймер Передача данных в форму имитации и из нее в CCS в зависимости от ключей
            if (AdcDirect)
            {
                adc_imit_select(AdcCodeSwitch);
                transmit_to_form_imit();
            }
            #endregion

            // Передаем данные в форму 3, если необходимо хотя бы 1 график отобразить
            if (_f3Win.checkBox1.Checked || _f3Win.checkBox2.Checked || _f3Win.checkBox3.Checked)
            {
                define_params_form3();
            }
            #region Основной таймер.
            _ = Convert.ToString(SharpFlags, 2);
            #endregion Анализ флагов CCS.

            //---------------------------------------------------------------------------------
            #region Основной таймер. Монитор соответствия ШАРП -> CCS.
            
            

                switch (_timerCount)
                {
                    case 0:

                        break;
                    case 1:

                        break;
                    case 2:
                        /*  bytes[4] = _motId1.PolePair;
                          bytes[5] = _motId1.Index;
                          par3 = BitConverter.ToUInt16(bytes, 4);
                          uint_128(_motId1.UfNorma, _motId1.ToothNum, par3, 81);
                          */
                        break;
                    case 3:
                        /*     bytes[4] = _motId2.PolePair;
                             bytes[5] = _motId2.Index;
                             par3 = BitConverter.ToUInt16(bytes, 4);
                             uint_128(_motId2.UfNorma, _motId2.ToothNum, par3, 82);
                             */
                        break;
                }

            _timerCount++;
            if (_timerCount > 3)
            {
                _timerCount = 0;
            }
            #endregion Основной таймер. Повторение команд для  соответствия ШАРП -> CCS.


        }

        #endregion Основной таймер. 

        #region Режим Бутлоудера
        #region Вызов загрузки
        private void updateFlashToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tsmConnect.Text != @"Отключить")
            {
                return;
            }

            // Сохраняем CAN handle
            byte savedCh = Ch;

            // ЗДЕСЬ МЫ ПРИБОР НЕ ТРОГАЕМ. Только открываем диалог выбора файла.
            //
            // Команду 9999 и отключение UI делает EnterBootloaderMode() из
            // рабочего потока — уже после того, как файл выбран, разобран и
            // подтверждён человеком. Раньше всё это стояло здесь, до диалога:
            // прибор уходил в загрузчик (гасил IGBT, останавливал приложение)
            // ещё до того, как пользователь увидел список файлов. Отменить
            // выбор или передумать было нельзя - прибор оставался в загрузчике
            // до удачной прошивки или снятия питания.
            //
            // Теперь отмена на любом шаге до подтверждения оставляет прибор
            // работающим, а связь - живой.
            Obl(savedCh);
        }
        #endregion
        #endregion

        #region Функции вывода параметров на экран дисплея
        //---------------------------------------------------------------------------------
        private void sproc_view(Motor m)
        {
            switch (m.SabsName.Text)
            {
                case @"Sabs":
                    m.SabsValue.Text = $@"{m.ccs.slip_absF * 0.001:0.0000}";
                    break;

                case @"S%":
                    m.SprocValue.Text = $@"{m.ccs.slip_procF * 0.01:0.00}";
                    break;
                case @"Wslip":
                    m.SprocValue.Text = $@"{m.ccs.WslipF * 0.001:0.00}";
                    break;
            }
        }
        //---------------------------------------------------------------------------------
        // Приборы однофазного моста из кадра 140. Столбцы панели поделены по
        // стороне схемы: слева АИН (АКБ, мост, трансформатор), справа сеть.
        // Внутри столбца порядок один - сначала напряжения, потом токи:
        //
        //      Vin   (gb13)        U220   (gb14)
        //      Usec  (gb17)        F сети (gb16)
        //      Ipri  (gb18)        I1_av  (gb20)
        //      Isec  (gb19)        Ucor   (gb_Ucor)
        //
        // Правый столбец ниже частоты занят постоянной составляющей, и стоит
        // она НАПРОТИВ Ipri намеренно: I1_av - это среднее ровно того тока,
        // что показано слева, а Ucor - то, чем это среднее давят. Читается
        // парой, не приходится искать по панели.
        //
        // Имена контролов достались от моторной панели и со смыслом полей не
        // совпадают - смотреть надо на эту таблицу, а не на I1a/I1b/Vin.
        //
        // Что лежит в четырёх каналах моста - коды АЦП, мгновенные значения
        // или rms - решает ключ teleSel, и подпись рядом ставится по эху ключа
        // из кадра, а не по нажатию оператора: пока прибор не ответил, метка
        // не должна обещать того, чего в полях нет. Vin в эту тройку не
        // входит: его MCU шлёт отдельным словом и всегда в 0.1 В.
        //
        // В прошивке этот канал зовётся uBat, но подпись в окне - Vin: мост
        // питается не обязательно от АКБ, а поле показывает вход как таковой.
        //---------------------------------------------------------------------------------
        private void Invertor_view()
        {
            // Подпись ключа ставит ACK 634, а не этот таймер: иначе жёлтая
            // метка "жду ответа" гасла бы сама собой на первом же кадре.
            // Отсюда берётся только формат - в кодах дробная часть не нужна.
            var f = ccs.teleSel == TeleKod ? "0" : "0.00";

            Vin_value.Text   = $@"{ccs.UdcBat:##0.0}"; // Vin, вход моста
            I1a_value.Text   = ccs.uSec.ToString(f);
            I1b_value.Text   = ccs.iPri.ToString(f);
            I1c_value.Text   = ccs.iSec.ToString(f);

            Urms1_value.Text = ccs.u220.ToString(f);

            // Частота сети мерится по захвату нуля U220 и в набор teleSel не
            // входит: она всегда в герцах, что бы ни стояло в ключе.
            field1_value.Text = $@"{ccs.u220freq:0.00}";

            //-------------------------------------------------------------
            // Постоянная составляющая в трансформаторе. Кадр 140, слова 21
            // и 22; разбор - INVERTOR_CCS/my_Files/grid_tie.txt, 3.1.
            //
            // В набор teleSel не входят, как и частота: обе всегда в своих
            // единицах, что бы ни стояло в ключе. Цена деления та же, что
            // на проводе, - 0.01 А и 0.001 В; показывать точнее, чем
            // приехало, смысла нет.
            //
            // I1_av занял ячейку оборотов (rpm1_value): у моста оборотов
            // нет, а место напротив Ipri - лучшее из возможных. Ucor сидит
            // в своей ячейке, заведённой под него.
            //
            // В исправном контуре знаки у них ПРОТИВОПОЛОЖНЫ: ток уехал в
            // одну сторону, поправка тянет в другую. Совпали - неверна
            // полярность датчика первички, обратная связь стала
            // положительной, и контур гонит поток туда же, куда он ушёл.
            //-------------------------------------------------------------
            rpm1_value.Text = $@"{ccs.iPriAv:0.00}";
            Ucor_value.Text = $@"{ccs.uDCcomp:0.000}";
        }

        private void Vd_view(Motor m)
        {
            switch (m.Vd_Name.Text)
            {
                case @"Vd":
                    m.Vd_Value.Text = $@"{m.ccs.Vd * 0.1:##0.0}";
                    break;
                case @"uVd":
                    m.Vd_Value.Text = $@"{m.ccs.uVd * 0.1:##0.0}";
                    break;
                case @"m": // запас по индексу модуляции, вместо Udc - тот дублировал Vin
                    // Пока упора нет - одно число, пик запроса за период. В
                    // упоре через дробь число тактов в потолке: "0.968/12"
                    // значит, что двенадцать тактов из ста стояли в mMax, и при
                    // больше десяти это авария 64.
                    m.Vd_Value.Text = ccs.gtSatLast == 0
                        ? $@"{ccs.gtMPeak:0.000}"
                        : $@"{ccs.gtMPeak:0.000}/{ccs.gtSatLast}";
                    break;
            }
        }
        private void Vq_view(Motor m)
        {
            switch (m.Vq_Name.Text)
            {
                case @"Vq":
                    m.Vq_Value.Text = $@"{m.ccs.Vq * 0.1:##0.0}";
                    break;
                case @"uVq":
                    m.Vq_Value.Text = $@"{m.ccs.uVq * 0.1:##0.0}";
                    break;
                case @"Err": // код последней аварии сетевого режима
                    m.Vq_Value.Text = $@"{ccs.gtFault:0}";
                    break;
            }
        }
        private void Urms_view(Motor m)
        {
            switch (m.Urms_Name.Text)
            {
                case @"Urms":
                    m.Urms_Value.Text = $@"{m.ccs.Urms:0.00}";
                    break;
                case @"Um":
                    m.Urms_Value.Text = $@"{m.ccs.c_UmF / 50.0:0.00}";
                    break;
            }
        }

        private void Id_view(Motor m)
        {
            switch (m.Id_Name.Text)
            {
                case @"Id":
                    m.Id_Value.Text = $@"{m.ccs.IdF * 0.01:0.00}";
                    break;
                case @"uId":
                    m.Id_Value.Text = $@"{m.ccs.uIdF * 0.1:0.00}";
                    break;
                case @"Iset": // сетевой режим: ток после нарастания, а не уставка
                    // Пара "факт -> уставка", пока они расходятся. Одно число
                    // на главный вопрос не отвечает: стоящее 2.40 значит и
                    // "доехали", и "рампа встала", и "в SYNC ток держим нулём,
                    // хотя уставка поднята". Сошлись - показываем одно, иначе
                    // стрелка висела бы на экране всё время работы.
                    //
                    // В паре один знак после запятой, у одиночного числа два.
                    // Поле AutoSize, а справа на 183 точках стоит подпись Ud -
                    // на пару остаётся 85 точек, и десять знаков Cambria 14
                    // в них не влезают. Сотые на ходу всё равно шум: рампа
                    // идёт 5 А/с и перебирает их по пять за кадр.
                    m.Id_Value.Text = ccs.gtIsetRef == ccs.gtIset
                        ? $@"{ccs.gtIset:0.00}"
                        : $@"{ccs.gtIset:0.0}→{ccs.gtIsetRef:0.0}";
                    break;
            }
        }

        private void Iq_view(Motor m)
        {
            switch (m.Iq_Name.Text)
            {
                case @"Iq":
                    m.Iq_Value.Text = $@"{m.ccs.IqF * 0.01:0.00}";
                    break;
                case @"uIq":
                    m.Iq_Value.Text = $@"{m.ccs.uIqF * 0.1:0.00}";
                    break;
                case @"GT": // состояние автомата подключения к сети
                    m.Iq_Value.Text = ccs.gtState < GtStateName.Length
                        ? GtStateName[ccs.gtState]
                        : $@"{ccs.gtState:0}";
                    break;
            }
        }
        #endregion Функции вывода параметров на экран дисплея

        #region Загрузка формы Form1
        /*************************************************************************************************/
        private void Form1_Load(object sender, EventArgs e)
        {
            AIN2_motorName.Text = Settings.Default.AIN2_motor;

            mFast_Motor.Text = Settings.Default.mFast;

            F2_motK60.Text = Settings.Default.mK60;
            tKadr60.Text = Settings.Default.SPL_update_ms;
            if (F2_motK60.Text == "M1")
            {
                motK60 = M1;
            }
            else
            {
                motK60 = M2;
            }

            TbInit();
            M1_def();
            M2_def();



            groupBox_AIN2.Size = groupBox_AIN1.Size;
            groupBox_AIN2.Location = new Point(groupBox_AIN1.Location.X + groupBox_AIN1.Width,
                groupBox_AIN1.Location.Y);
            gbAIN1_2.Size = groupBox_AIN1.Size;
            gbAIN2_2.Size = groupBox_AIN2.Size;
            gbAIN1_2.Location = groupBox_AIN1.Location;
            gbAIN2_2.Location = groupBox_AIN2.Location;
            gbAIN1_2.BackColor = Color.Black;
            gbAIN2_2.BackColor = Color.Black;
            var ffm = new FontFamily("Cambria");
            var FF = new Font(ffm, 14.0F, FontStyle.Bold);
            gbAIN1_2.Font = FF;
            gbAIN2_2.Font = FF;
            dgvAIN1.BackgroundColor = gbAIN1_2.BackColor;
            dgvAIN2.BackgroundColor = gbAIN2_2.BackColor;
            groupBox_AIN1.Visible = groupBox_AIN2.Visible = true;
            gbAIN1_2.Visible = gbAIN2_2.Visible = false;
            groupBox_AIN1.MouseClick += GroupBox_AIN1_MouseClick;
            gbAIN1_2.MouseClick += GroupBox_AIN1_MouseClick;
            groupBox_AIN2.MouseClick += GroupBox_AIN2_MouseClick;
            gbAIN2_2.MouseClick += GroupBox_AIN2_MouseClick;
            _ain1Table = new AinTable.AinTable(ref dgvAIN1);
            _ain2Table = new AinTable.AinTable(ref dgvAIN2);
            AinTableInit();

            #region  Загрузка настроек  Settings.Default
            //---------------------------------------------------------------------------------
            BForm1Load = true;
            Separate_cbox.Checked = Settings.Default.Separate;


            Imax_Value.Text = Settings.Default.IGBT_max;
            textBox_температура.Text = Settings.Default.IGBT_temp_max;
            Gain_Sym.Checked = Settings.Default.Symmetry;
            Load_Sym.Checked = Settings.Default.LSymmetry;
            // SlipMax_Value - теперь поле "Рампа" (Slew_Init): значение не из
            // настроек, а из прибора.
            SlipMax_Value2.Text = Settings.Default.S_max2;
            DG2.Text = Settings.Default.ДГ2;

            M1.load.modeH_kod = Settings.Default.Load1_Hkod;
            M1.load.modeL_kod = Settings.Default.Load1_Lkod;
            M2.load.modeH_kod = Settings.Default.Load2_Hkod;
            M2.load.modeL_kod = Settings.Default.Load2_Lkod;

            // Разрешение на выход в сеть из настроек НЕ восстанавливаем: это
            // блокировка, и поднимать её должен оператор в этом сеансе, а не
            // файл настроек с прошлого. Галочку поставит ответ прибора на 635.
            M2.AIN_Enable.Checked = Settings.Default.AIN2_Enable;
            //--------------------------------------------------------------------------------- 
            #endregion   Загрузка настроек  Settings.Default

#if CAN_LOG
            // Автоматически включаем логирование CAN с момента запуска
            ToggleCanLogging(true);
            SyncCanLogUi();
#endif

            _M_INit();

        }
        #endregion Загрузка формы Form1



        private void GroupBox_AIN1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.X < 80 || e.Y > 15)
            {
                return;
            }

            groupBox_AIN1.Visible = !groupBox_AIN1.Visible;
            gbAIN1_2.Visible = !groupBox_AIN1.Visible;
        }

        private void GroupBox_AIN2_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.X < 80 || e.Y > 15)
            {
                return;
            }

            groupBox_AIN2.Visible = !groupBox_AIN2.Visible;
            gbAIN2_2.Visible = !groupBox_AIN2.Visible;

        }

        #region class NUD+Tbar
        public class NT
        {
            public Dgt Adjust;
            public ushort cmd;
            public ushort cmd_LL;
            public ushort kNT;
            public ushort modeH_kod;
            public ushort modeL_kod; // номер режима кнопки modeL

            public Button modeH; // верхняя кнопка переключения режима нагрузки
            public Button modeL; // нижняя кнопка переключения режима нагрузки
            public NumericUpDown nud;
            public TrackBar tbar;

            public Label unit; // единица измерения
        }
        public class Motor
        {
            public mccs ccs = new mccs();
            public NT gain = new NT();
            public NT load = new NT();
            public Parkinson park = new Parkinson();
            public CheckBox AIN_Enable;
            public TextBox AIN_Flag;
            public GroupBox AIN;
            public Label DG;
            public Label SprocName;
            public Label SprocValue;
            public Label SabsName;
            public Label SabsValue;

            public Label Vd_Name;
            public Label Vd_Value;
            public Label Vq_Name;
            public Label Vq_Value;
            public Label Id_Name;
            public Label Id_Value;
            public Label Iq_Name;
            public Label Iq_Value;
            public Label Urms_Name;
            public Label Urms_Value;
            public Button Name;
            public MotPar Par;

        }

        public Motor M1 = new Motor();

        public Motor M2 = new Motor();

        private void M1_def()
        {
            M1.ccs.UrmsF = 0;
            // ---------------  Gain -----------------------------
            M1.gain.nud = NUD_Gain1;
            M1.gain.tbar = Tbar_Gain1;

            M1.gain.modeH = Gain1_ModeH;
            M1.gain.modeL = Gain1_ModeL;



            M1.gain.unit = label_GainBar1_unit;
            //           M1.gain.kNT = 1;
            M1.gain.cmd = 75;
            //           M1.gain.modeH_kod = 0;

            _tblGain1.Init();
            M1.gain.Adjust = _tblGain1;
            _tblGain1.TableChanged += _tblGain1_TableChanged;
            M1.gain.modeH.Text = Settings.Default.Gain1_HText;
            M1.gain.modeL.Text = Settings.Default.Gain1_LText;



            // ---------------  Load -----------------------------
            M1.load.nud = NUD_Load1;
            M1.load.tbar = Tbar_Load1;
            M1.load.modeH = Load1_ModeH;

            M1.load.modeL = Load1_ModeL;

            M1.load.kNT = 100;
            M1.load.cmd = 73;
            M1.load.cmd_LL = 22;



            _tblLoad1.Init();
            _tblLoad1.TableChanged += _tblLoad1_TableChanged;

            M1.load.Adjust = _tblLoad1;
            M1.load.modeL.Text = Settings.Default.load1_Ltext;

            // Load1 у однофазного моста - ТОЛЬКО уставка тока в сеть (09.10.2026).
            // Моторные режимы (скольжение, Iq, gain, RPM) и нижняя кнопка
            // RPM/Поле слали команду 73, а та писала motors[0]->ref.sAbs, которого
            // в прошивке инвертора никто не читает. Подпись из настроек больше не
            // берётся: там может лежать "S abs" или прежнее "IsetRef".
            //
            // Симметрия Load1 -> Load2 снята с тем же основанием: копировать
            // уставку тока во второй движок незачем, а Copy_NT заодно перенёс бы
            // туда и команду 632. Галка скрыта и выключена - этим глохнут сразу
            // все места, где она копирует.
            Load1_ModeL.Visible = false;
            Load_Sym.Checked = false;
            Load_Sym.Visible = false;
            toolTip1.SetToolTip(Load1_ModeH,
                "IsetRef - уставка тока в сеть, А rms (gt.IsetRef, команда 632).\n" +
                "Куда идёт рампа: поле Iset на панели догоняет это число\n" +
                "шагами IsetSlew раз за период сети.\n" +
                "Задать можно и в АИН - применится, когда мост выйдет в сеть.");
            M1.load.modeH.Text = LoadIsetRef;
            Slew_Init(); // поле под движком: было S_max, стало "Рампа"
            //--------------------------------------------------------------
            M1.AIN = groupBox_AIN1;

            // M1.AIN_Flag пустой: у однофазного моста отдельного поля-флага
            // нет, состояние ключей показывает сама кнопка btnInvPower.
            M1.AIN_Enable = AIN1_Enable;

            M1.SabsName = S1abs_label;
            M1.SabsValue = S1_abs;

            M1.SprocName = S1proc_label;
            M1.SprocValue = S1_proc;

            M1.Vd_Name = Vd1_name;
            M1.Vd_Value = Vd1_value;
            M1.Vq_Name = Vq1_name;
            M1.Vq_Value = Vq1_value;

            M1.Id_Name = Id1_name;
            M1.Id_Value = Id1_value;
            M1.Iq_Name = Iq1_name;
            M1.Iq_Value = Iq1_value;

            M1.Urms_Name = Udc_name;
            M1.Urms_Value = Udc_value;

            // M1.Name и M1.DG остаются пустыми: на канале 1 однофазный мост,
            // ни типа двигателя, ни признака "двигатель/генератор" у него нет.
            // Общий код, который разыменовывает эти поля, проверяет их на null.
            toolTip1.SetToolTip(AIN1_Enable, "Разрешение на подключение к сети.\n" +
                "Блокировка, а не команда: сама по себе контактор не замыкает,\n" +
                "она лишь пропускает режим \"Выход в сеть\".");

            // Постоянная составляющая в трансформаторе - величины непривычные,
            // и без подсказки по ним не прочитать. Вешаем и на подпись, и на
            // значение: наводят мышь на число, а не на слово.
            string hintI1av =
                "Постоянная составляющая тока ПЕРВИЧКИ, Ампер.\n" +
                "Среднее за период, считает CLA.\n\n" +
                "Норма - ноль. Всё, что от него отличается, намагничивает\n" +
                "сердечник: поток есть интеграл напряжения и растёт без границ.\n" +
                "Мерится именно первичка - ток намагничивания течёт только там\n" +
                "и во вторичку не трансформируется.\n\n" +
                "При закрытых ключах показывает смещение нуля датчика: точность\n" +
                "подавления не может быть лучше этого числа.";

            string hintUcor =
                "Сколько постоянки мост ВЫЧИТАЕТ, Вольт.\n" +
                "Встречный перекос полуволн, которым давят уход потока.\n\n" +
                "0.48 В - полная ошибка мёртвого времени\n" +
                "0.96 В - потолок полномочий контура, дальше давить нечем\n\n" +
                "Знак должен быть ПРОТИВОПОЛОЖЕН знаку I1_av. Совпали - неверна\n" +
                "полярность датчика первички, и обратная связь стала\n" +
                "положительной.";

            toolTip1.SetToolTip(rpm1_Text, hintI1av);
            toolTip1.SetToolTip(rpm1_value, hintI1av);
            toolTip1.SetToolTip(Ucor_Text, hintUcor);
            toolTip1.SetToolTip(Ucor_value, hintUcor);

            // Слот Vd у M1 насовсем отдан "m" - двойной щелчок по подписи его
            // не перебирает, так что подсказка одна и ставится один раз.
            string hintM =
                "Запас по индексу модуляции, кадр 140, слова 24-25.\n\n" +
                "Число - пик |m| за период сети: сколько контур ПРОСИЛ,\n" +
                "до ограничения. Потолок mMax = 0.95.\n" +
                "    0.90 - запас есть\n" +
                "    0.94 - следующий шаг Iset упрётся\n" +
                "    0.97 - уже в упоре, просим больше, чем можно\n\n" +
                "Через дробь - тактов в упоре из ста: \"0.968/12\".\n" +
                "Больше десяти за период - авария 64.\n\n" +
                "Упор лечится железом, а не регулятором: не хватает\n" +
                "напряжения звена или коэффициента трансформации.\n" +
                "В острове число - амплитуда gain/2048 плюс поправка\n" +
                "постоянки, упора там не считаем.";

            toolTip1.SetToolTip(Vd1_name, hintM);
            toolTip1.SetToolTip(Vd1_value, hintM);

            // Стартовая подсказка под ту надпись, с которой кнопка создана
            // в Designer. Дальше её меняет ответ прибора - GridMode_Ack().
            GridMode_Hint();

            // Таймеры отложенной посылки - см. GridFreq_Changed/GridUref_Changed.
            GridFreq_Init();
            GridUref_Init();

            GridTie_Hints();

            // Подсказки слотов нижнего ряда. Ставить их на старте обязательно:
            // слот Id/uId/Iset открывается с надписью, оставшейся от прошлого
            // сеанса, а слоты GT и Err у M1 вообще не листаются - для них этот
            // вызов единственный.
            //
            // Только для M1: поля M2 привязываются в своём блоке ниже по файлу,
            // и здесь они ещё null. Для M2 такие же вызовы стоят в конце того
            // блока.
            Id_Name_Hint(M1);
            Iq_Name_Hint(M1);
            Vq_Name_Hint(M1);
        }

        private void _tblLoad1_TableChanged(Dgt sender, EventArgs e)
        {

            Load_Mode_Refresh(M1);
            if (Load_Sym.Checked)
            {
                M2.load.Adjust = M1.load.Adjust; // _tblLoad1;
                Load_Mode_Refresh(M2);
            }
        }

        private void _tblGain1_TableChanged(Dgt sender, EventArgs e)
        {

            Gain_Mode_Refresh(M1);
            if (Gain_Sym.Checked)
            {
                M2.gain.Adjust = M1.gain.Adjust; // _tblGain1;
                Gain_Mode_Refresh(M2);
            }
        }

        private void M2_def()
        {
            // ---------------  Gain -----------------------------
            M2.gain.nud = NUD_Gain2;
            M2.gain.tbar = Tbar_Gain2;

            M2.gain.modeH = Gain2_ModeH;
            M2.gain.modeL = Gain2_ModeL;

            M2.gain.modeH.Text = Settings.Default.Gain2_HText;
            M2.gain.modeL.Text = Settings.Default.Gain2_LText;

            M2.gain.unit = label_GainBar2_unit;

            M2.gain.cmd = 76;


            _tblGain2.Init();
            _tblGain2.TableChanged += _tblGain2_TableChanged;
            M2.gain.Adjust = Gain_Sym.Checked ? _tblGain1 : _tblGain2;



            // ---------------  Load -----------------------------
            M2.load.nud = NUD_Load2;
            M2.load.tbar = Tbar_Load2;

            M2.load.modeH = Load2_ModeH;


            M2.load.modeL = Load2_ModeL;


            M2.load.kNT = 100;
            M2.load.cmd = 77;
            M2.load.cmd_LL = 23;

            _tblLoad2.Init();
            _tblLoad2.TableChanged += _tblLoad2_TableChanged;
            M2.load.Adjust = _tblLoad2;
            M2.load.modeL.Text = Settings.Default.load2_Ltext;
            M2.load.modeH.Text = Settings.Default.load2_Htext;

            //--------------------------------------------------------------
            M2.AIN = groupBox_AIN2;
            M2.AIN_Flag = AIN2_flag;
            M2.DG = DG2;
            M2.AIN_Enable = AIN2_Enable;

            M2.SabsName = S2abs_label;
            M2.SabsValue = S2_abs;
            M2.SprocName = Torq_label;
            M2.SprocValue = Torq_value;

            M2.Vd_Name = Vd2_name;
            M2.Vd_Value = Vd2_value;
            M2.Vq_Name = Vq2_name;
            M2.Vq_Value = Vq2_value;
            M2.Id_Name = Id2_name;
            M2.Id_Value = Id2_value;
            M2.Iq_Name = Iq2_name;
            M2.Iq_Value = Iq2_value;
            M2.Urms_Name = Urms2_name;
            M2.Urms_Value = Urms2_value;

            M2.Name = AIN2_motorName;
            M2.Par = SetMotorPar(M2.Name);

            // Стартовые подсказки слотов нижнего ряда - только здесь, после
            // привязки полей. В блоке M1 их ставить нельзя: он выполняется
            // раньше, и поля M2 там ещё null.
            Id_Name_Hint(M2);
            Iq_Name_Hint(M2);
            Vq_Name_Hint(M2);
        }

        private void _tblLoad2_TableChanged(Dgt sender, EventArgs e)
        {
            if (Load_Sym.Checked)
            {
                return;
            }

            M2.load.Adjust = _tblLoad2;
            Load_Mode_Refresh(M2);
        }

        private void _tblGain2_TableChanged(Dgt sender, EventArgs e)
        {
            if (Gain_Sym.Checked)
            {
                return;
            }

            M2.gain.Adjust = _tblGain2;
            Gain_Mode_Refresh(M2);
        }
        #endregion class NUD+Tbar
        #region Основные Параметры и переменные
        public byte CanAId = 0xA; // ID CAN адаптера A - основная коммуникация
        public byte CanBId = 0xB; // ID CAN адаптера B - быстрый кадр
        public byte CanCId = 0xC; // ID CAN адаптера C - управление платой СВТГ
        public byte CanDId = 0xD; // ID CAN адаптера D - Имитатор дизеля, связь с КВУ J1939
        public bool CanA_Ok;
        // устанавливается при успешном соединении  по первому нажатию кнопки "Подключить". Сбрасывается по подверждению команды  "Отключить" 
        public bool CanB_Ok;
        #region глобальные статические переменные и константы
        public static double sqrt2 = Math.Sqrt(2);
        public static double sqrt3 = Math.Sqrt(3);
        public readonly double sqrt32 = sqrt3 / sqrt2;

        public class MotPar
        {
            public string name;
            public string Type;
            public ushort Index; // 0-TAD350, 1-TAD170, 2- TAD170.1   
            public ushort PolePair; // число пар полюсов
            public ushort ToothNum; // число зубьев счётного диска

            public ushort Rs; // Stator resistance (mOhm)
            public ushort Rr; //  Rotor resistance (mOhm)
            public ushort Ls; //  Stator inductance (mkH)
            public ushort Lr; //   Rotor inductance (mkH)
            public ushort Lm; // Main inductance (mkH)
            public ushort Inom; // Nominal phase current (Amp)
            public ushort Unom; // Nominal phase to phase voltage (Volt)
            public ushort Fnom; // Nominal rotor speed (rpm)
            public ushort Pnom; // Nominal aktiv Power (Wt)
            public ushort Snom; // Slip at nominal mode (rpm*100)

            public ushort UfNorma; // u/f *1000
            public ushort tip; // ассинхронный =0, синхронный =1, DC = 2
        };
        private static readonly MotPar DC = new MotPar
        {
            name = "ДПТ",
            Type = "DC",
            Index = 7,

            tip = 2,

            Rs = 20500,
            Rr = 11300,
            Ls = 430,
            Lr = 300,
            Lm = 6000,
            Inom = 237,
            Unom = 530,
            Fnom = 1290
        };
        private static readonly MotPar TAD350 = new MotPar
        {
            name = "ТАД-350",
            Type = "IM",
            Index = 0,
            ToothNum = 242,
            PolePair = 3,
            UfNorma = 1100,
            tip = 0,

            Rs = 20500,
            Rr = 11300,
            Ls = 430,
            Lr = 300,
            Lm = 6000,
            Inom = 237,
            Unom = 530,
            Fnom = 1290
        };

        private static readonly MotPar TAD170 = new MotPar
        {
            name = "ТАД-170",
            Type = "IM",
            Index = 1,
            ToothNum = 80,
            PolePair = 2,
            UfNorma = 411,
            tip = 0,

            Rs = 20500,
            Rr = 12000,
            Ls = 430,
            Lr = 300,
            Lm = 9660,
            Inom = 237,
            Unom = 530,
            Fnom = 1290,
            Pnom = 170

        };
        private static readonly MotPar DTAN160 = new MotPar
        {
            name = "ДТАН-160",
            Index = 2,
            ToothNum = 256,
            PolePair = 2,
            UfNorma = 280,
            tip = 0
        };
        private static readonly MotPar SRD = new MotPar
        {
            name = "СРД",
            Type = "SynRm",
            tip = 1,
            Index = 3,

            ToothNum = 80,
            PolePair = 2,
            Inom = 237,
            Unom = 530,
            Fnom = 1290,
            Pnom = 170,

            Rs = 24550,   // mkOm   сопротивление одной фазы статорной обмотки
            Rr = 32767,
            Ls = 5030,     // мкГн   Ld - индуктивность вдоль магнитной оси ротора 
            Lr = 1290,       // мкГн   Lq -  индуктивность перпендикулярно  магнитной оси ротора 
            Lm = 9660
        };

        private static readonly MotPar SGT750 = new MotPar
        {
            name = "СГТ-750",
            Index = 4,
            ToothNum = 1,
            PolePair = 4,
            UfNorma = 280,
            tip = 1
        };

        private static readonly MotPar TAD800 = new MotPar
        {
            name = "ТАД800",
            Index = 5,
            ToothNum = 192,
            PolePair = 3,
            UfNorma = 411,
            tip = 0
        };
        private static readonly MotPar GST7 = new MotPar
        {
            name = "7ГСТ",
            Index = 6,
            ToothNum = 1,
            PolePair = 3,
            UfNorma = 411,
            tip = 1
        };

        #endregion
        private bool _adcTransmit;
        private readonly int _graphMax = 1500; // максимум для параметров отображаемых на графиках
                                               // -------------------------------   Основные переменные  ----------------------------------
                                               //     public ushort PolePairs = 3;
        public float V2Angl = 0, I2Angl = 0, Em2Angl = 0, No2Angl = 0;
        public int VqRef = 0, VdRef = 0;
        public int VqFbk = 0, VdFbk = 0;
        public int RpmDulim = 0, VqDulim = 0, VdDulim = 0;
        public int RpmDu = 0, VqDu = 0, VdDu = 0;
        public int RpmDumax = 0, VqDumax = 0, VdDumax = 0;
        public int VqI1 = 0, VdI1 = 0;
        public int RpmUi = 0, VqUi = 0, VdUi = 0;
        public int RpmOut = 0, VqOut = 0, VdOut = 0;
        public int[] AdcOffset = { 2062, 2064, 2065, 0, 0, 13, 0, 0, 0, 0, 6, 0, 0, 2069, 2070, 2068 };
        public int MotPoles;
        public uint ParSelector; // определяет конфигурацию параметров на окне индикаци
        #region  Переменные имитации ------------------------------------
        public bool ImitationUdc,
            ImitationIu1,
            ImitationIw1,
            ImitationIv1,
            ImitationIu2,
            ImitationIw2,
            ImitationIv2;
        //   public bool StartWorking; //устанавливается при подключении к CAN, сбрасывается при отключении
        public bool BForm1Load;
        public int[] AdcPin = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public decimal[] AdcTemp = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private bool _refreshImage = true, _refreshImage2 = true;
        private readonly string _ccsChar = string.Empty;
        private bool _firstFlags = false; //первый приём флагов после подсоединения CANA.
        private bool _udcIgbtOffMessageEnable;
        private bool _currentErrorMessageEnable;
        private bool IGBToverTempErrorMessageEnable;
        private uint _ccsFlags; // битовые поля с признаками текущих установленных режимов из CCS.
        public uint SharpFlags = 0, SharpFlagsLast = 0;
        public bool AdcDirect;
        // true, если идет перессылка прямы показаний АЦП в форму имитации и если не передаем мощность соответственно
        public bool DoOffset;
        public string AdcCodeSwitch = "Def"; // Переменная для выбора типа передачи данных имитации
        public ushort SLimitFlag, XxFlag;
        //Флаги отправляемых данных:
        #endregion Переменные имитации
        public bool FlagGrphOpen;
        public int UfConst, UfConst2 = 0, Uf2 = 0;
        public string[] TempDbg; //для переключения дбг параметров
        private ushort _timerCount; // счётчик циклов основного таймера для повторения команд состояния кнопок.
        public const int UGain = 0, UUrms = 1, UUf = 2, UId = 3, UIm = 4, UIdq = 5, UIrpm = 6;
        public const int Kgain = 1, KUrms = 1, KId = 10, KUF = 1;
        public const int KSabs = 100, KSproc = 100, KIq = 10;
        private AinTable.AinTable _ain1Table;
        private AinTable.AinTable _ain2Table;
        private bool _startstop; // Состояние кнопки старт-стоп 1-Работа 0-Выкл
        public decimal kGain1 = Kgain;
        public decimal kGain2 = Kgain;
        //      private bool firstYoko = false;
        #endregion   Основные Параметры и переменные
        #region Таблица АИНов    Чёрное Окно
        private void AinTableInit()
        {
            // АИН1 

            _ain1Table.LeftAdd("U1", "1", Color.Yellow);
            _ain1Table.LeftAdd("I1", "1", Color.Orange);

            _ain1Table.LeftAdd("r11", "1", Color.Empty);  // пустая строка

            _ain1Table.LeftAdd("P1", "1", Color.SkyBlue);
            _ain1Table.LeftAdd("S1", "1", Color.SkyBlue);
            _ain1Table.LeftAdd("Q1", "1", Color.SkyBlue);
            _ain1Table.LeftAdd("r12", "1", Color.Empty);  // пустая строка

            _ain1Table.LeftAdd("PF1", "1", Color.SkyBlue);

            _ain1Table.RightAdd("U2", "1", Color.Yellow);
            _ain1Table.RightAdd("I2", "1", Color.Orange);
            _ain1Table.RightAdd("r21", "1", Color.Empty);  // пустая строка

            _ain1Table.RightAdd("P2", "1", Color.SkyBlue);
            _ain1Table.RightAdd("S2", "1", Color.SkyBlue);
            _ain1Table.RightAdd("Q2", "1", Color.SkyBlue);
            _ain1Table.RightAdd("r22", "1", Color.Empty);  // пустая строка

            _ain1Table.RightAdd("PF2", "1", Color.SkyBlue);

            // АИН2 
            _ain2Table.LeftAdd("U1", "1", Color.Yellow);
            _ain2Table.LeftAdd("I1", "1", Color.Orange);

            _ain2Table.LeftAdd("P1", "1", Color.SkyBlue);
            _ain2Table.LeftAdd("S1", "1", Color.SkyBlue);
            _ain2Table.LeftAdd("Q1", "1", Color.SkyBlue);
            _ain2Table.LeftAdd("PF1", "1", Color.SkyBlue);

            _ain2Table.RightAdd("U2", "1", Color.Yellow);
            _ain2Table.RightAdd("I2", "1", Color.Orange);

            _ain2Table.RightAdd("P2", "1", Color.SkyBlue);
            _ain2Table.RightAdd("S2", "1", Color.SkyBlue);
            _ain2Table.RightAdd("Q2", "1", Color.SkyBlue);
            _ain2Table.RightAdd("PF2", "1", Color.SkyBlue);

        }

        private void AinTableUpdate()
        {

            double I1a = M1.ccs.Ia_rms * 0.01;
            double I1b = M1.ccs.Ib_rms * 0.01;
            double I1c = M1.ccs.Ic_rms * 0.01;
            double I1 = (I1a + I1b + I1c) / 3;
            double U1 = M1.ccs.Urms;
            double S1 = I1 * U1 * sqrt3 / 1000;
            double P1 = M1.ccs.mPabc * 0.01;
            double Q1 = Math.Sqrt((S1 * S1) - (P1 * P1));
            double PF1 = (S1 == 0) ? 0 : P1 / S1;

            double I2a = M2.ccs.Ia_rms * 0.01;
            double I2b = M2.ccs.Ib_rms * 0.01;
            double I2c = M2.ccs.Ic_rms * 0.01;
            double I2 = (I2a + I2b + I2c) / 3;
            double U2 = M2.ccs.Urms;
            double S2 = I2 * U2 * sqrt3 / 1000;
            double P2 = M2.ccs.mPabc * 0.01;
            double Q2 = Math.Sqrt((S2 * S2) - (P2 * P2));
            double PF2 = (S2 == 0) ? 0 : P2 / S2;

            _ain1Table.SetVal("U1", $@"{U1:0.00}");
            _ain1Table.SetVal("U2", $@"{U2:0.00}");

            _ain1Table.SetVal("I1", $@"{I1:0.00}");
            _ain1Table.SetVal("I2", $@"{I2:0.00}");

            _ain1Table.SetVal("P1", $@"{P1:0.00}");
            _ain1Table.SetVal("S1", $@"{S1:0.00}");
            _ain1Table.SetVal("Q1", $@"{Q1:0.00}");
            _ain1Table.SetVal("PF1", $@"{PF1:0.000}");

            _ain1Table.SetVal("P2", $@"{P2:0.00}");
            _ain1Table.SetVal("S2", $@"{S2:0.00}");
            _ain1Table.SetVal("Q2", $@"{Q2:0.00}");
            _ain1Table.SetVal("PF2", $@"{PF2:0.000}");
            //-----------------------------------------------------------------------------------------
            _ain2Table.SetVal("I1", $@"{I1:0.0}");
            _ain2Table.SetVal("I2", $@"{I2:0.0}");

            _ain1Table.Update();
            _ain2Table.Update();
        }
        #endregion

        #region Меню Подключить/Отключить
        //------------------------------ CONNECT MENU -----------------------------------------------------

        // CAN UI logger implementation moved to CanUiLogger.cs

        //------------------------------------------------------------------------------------------
        // функция refresh_FORM1() посылает в CCS текущее положение кнопок и данных Motor Control
        private void refresh_Form1()
        {
         //   _bRefresh = true;
            readHiTemp();
            //-----------------------------------------------------
            rpm_ramp_send();
            Slew_Read(); // поле "Рампа" - бывшее SlipMax_Value, читает прибор
            slipMax_send(SlipMax_Value2);
            SpeedRef_send();
            tKadr60_send();
            TapeSync(); // лента, брошенная прошлым экземпляром программы
            DG_send(DG2, 61);

            Imax_Send();
            // Спрашиваем всё состояние однофазного моста и ключ телеметрии:
            // надписи на кнопках, галочку и индикатор поставят ответы. Своего
            // мнения о состоянии прибора у GUI при подключении нет.
            uint_128(0, 0, 0, CmdGridMode);
            uint_128(0, 0, 0, CmdGridEn);
            uint_128(0, 0, 0, CmdTeleSel);
            I2rms_selector.Text = "rms";
            uint_128(0, 0, 0, 17);

            button_Field_RPM.BackColor = Color.Yellow;
            button_Field_RPM.Text = "Поле";
            _rpmMode = 0;
            uint_128(_rpmMode, 3333, 0, 59);

            M2.Name.BackColor = Color.Yellow;
            uint_128(M2.Par.Index, M2.Par.ToothNum, M2.Par.PolePair, 820);  //запускаем последовательность команд для  изменения параметров двигателя АИН2


            _tblGain1_TableChanged(null, null);
            _tblLoad1_TableChanged(null, null);
            _tblGain2_TableChanged(null, null);
            _tblLoad2_TableChanged(null, null);


            uint_128(M1.load.modeL_kod, 3333, 0, M1.load.cmd_LL);
            M1.ccs.freq = 0;
            Load_Mode_Refresh(M1);

            uint_128(M2.load.modeL_kod, 3333, 0, M2.load.cmd_LL);
            M2.ccs.freq = 0;
            Load_Mode_Refresh(M2);

            Load_Sym_Click(null, null);
            if (Gain_Sym.Checked)
            {
                Copy_NT(M2.gain, M1.gain);
            }

            if (Load_Sym.Checked)
            {
                Copy_NT(M2.load, M1.load);
            }

            // Галочку Enable однофазного моста НЕ дёргаем: её состояние
            // приходит ответом на чтение 635 выше. Дёрнуть - значит послать
            // прибору то, что GUI помнит с прошлого сеанса, то есть навязать
            // разрешение на выход в сеть, которого оператор сейчас не давал.
            AIN2_Enable_Click(null, null);


            mFast_Motor.BackColor = Color.Yellow;
            ushort d1 = 1;
            if (mFast_Motor.Text == "M2")
            {
                d1 = 2;
            }

            uint_128(d1, 3333, 0, 25);

            F2_motK60.BackColor = Color.Yellow;
            d1 = 1;
            if (F2_motK60.Text == "M2")
            {
                d1 = 2;
            }

            uint_128(d1, 3333, 0, 26);


            //-----------------------------------------------------
  //          _bRefresh = false;
          
            BForm1Load = false;
            AIN2_motorName.Enabled = true;
            KVU_CheckedChanged(null, null);

            timer_1sek.Enabled = true;
            // C#
            Console.WriteLine($"HighResolution = {Stopwatch.IsHighResolution}");
            Console.WriteLine($"Stopwatch.Frequency = {Stopwatch.Frequency} ticks/sec");
            Console.WriteLine($"1 tick = {1.0 / Stopwatch.Frequency} sec = {1_000_000.0 / Stopwatch.Frequency} µs");
        }

        #endregion Меню Подключить/Отключить

        #region CANA - Unified Send Function

        /// <summary>
        /// Универсальная функция отправки CAN команды
        /// </summary>
        /// <param name="cmd">Код команды</param>
        /// <param name="d1">Данные 1 (по умолчанию 0)</param>
        /// <param name="d2">Данные 2 (по умолчанию 0)</param>
        /// <param name="d3">Данные 3 (по умолчанию 0)</param>
        /// <param name="skipConnectionCheck">true = отправить даже если отключены (для bootloader)</param>
        public void SendCanCommand(ushort cmd, int d1 = 0, int d2 = 0, int d3 = 0, bool skipConnectionCheck = false)
        {
            // ⏱️ CRITICAL: Capture TX timestamp as early as possible
            var txTs = Stopwatch.GetTimestamp();

            // Проверка подключения (пропускаем для bootloader команд)
            if (!skipConnectionCheck && tsmConnect.Text != "Отключить")
            {
                return;
            }

            ushort d1u = unchecked((ushort)d1);
            ushort d2u = unchecked((ushort)d2);
            ushort d3u = unchecked((ushort)d3);

            var txMsg = new TPCANMsgFD
            {
                DATA = new byte[64],
                ID = 2,
                DLC = 8,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_FD | TPCANMessageType.PCAN_MESSAGE_BRS
            };

            // Упаковка данных (Little-Endian)
            txMsg.DATA[0] = (byte)(cmd & 0xFF);
            txMsg.DATA[1] = (byte)((cmd >> 8) & 0xFF);
            txMsg.DATA[2] = (byte)(d3u & 0xFF);
            txMsg.DATA[3] = (byte)((d3u >> 8) & 0xFF);
            txMsg.DATA[4] = (byte)(d2u & 0xFF);
            txMsg.DATA[5] = (byte)((d2u >> 8) & 0xFF);
            txMsg.DATA[6] = (byte)(d1u & 0xFF);
            txMsg.DATA[7] = (byte)((d1u >> 8) & 0xFF);

            var result = PCANBasic.WriteFD(Ch, ref txMsg);
            if (result != TPCANStatus.PCAN_ERROR_OK)
            {
                MessageBox.Show(GetFormatedError(result));
            }
            else
            {
                _refreshImage = true;
                // ⏱️ Pass pre-captured timestamp to avoid measurement errors
                AppendUiCanLogLine("TX", txMsg.ID, cmd, d1u, d2u, d3u, txTs);
            }
        }

        // ✅ Обёртки для обратной совместимости
        public void uint_128(ushort data1, ushort data2, ushort data3, ushort command)
            => SendCanCommand(command, data1, data2, data3);

        public void int_128(int d1, int d2, int d3, ushort cmd)
            => SendCanCommand(cmd, d1, d2, d3);

        public void cmd_128(ushort command)
            => SendCanCommand(command, skipConnectionCheck: true); // ← ВАЖНО! Без проверки подключения

       
        private string GetFormatedError(TPCANStatus error)
        {
            StringBuilder strTemp;
            // Creates a buffer big enough for a error-text
            //
            strTemp = new StringBuilder(256);
            // Gets the text using the GetErrorText API function
            // If the function success, the translated error is returned. If it fails,
            // a text describing the current error is returned.
            //
            if (PCANBasic.GetErrorText(error, 0, strTemp) != TPCANStatus.PCAN_ERROR_OK)
            {
                return $"An error occurred. Error-code's text ({error:X}) couldn't be retrieved";
            }

            return strTemp.ToString();
        }
        #endregion CANA

        #region Дизайн текущего окна -- регион содержит подрегионы
        // Пункт "токи iPri, iSec, iBat". Маска 7 = 1|2|4, см. AdcStartCal в myADC.h.
        //
        // Раньше здесь стояло uint_128(1, 0, 0, 70): маска 1 от трёхфазного проекта
        // (там это были токи фаз Iabc) и data2 = 0. С тех пор как команда 70 обзавелась
        // условием data2 == 3333, посылка с нулём не выполнялась вовсе - прошивка
        // только отвечала текущей маской, то есть нулём. Пункт меню молчал.
        private void откалиброватьТокиToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uint_128(7, 3333, 0, 70);
        }

        #region Шестеренка и стрелочки
        private void timer_tick_Tick_1(object sender, EventArgs e)
        {

            if (_refreshImage)  //вращает стрелку Tx
            {
                pb_CANa_Tx.Image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                pb_CANa_Tx.Refresh();
            }
            _refreshImage = false;// !_refreshImage;
            if (_refreshImage2)
            {
                pb_CANa_Rx.Image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                pb_CANa_Rx.Refresh();
            }
            _refreshImage2 = false;

            //      Console.WriteLine("Очередь: " + mbox13FIFO.Count);

            // Обрабатываем сообщения из FIFO, уже в основном потоке
            while (mbox13FIFO.Count > 0)
            {
                _ = new byte[8];
                byte[] msg13 = mbox13FIFO.Dequeue();

                // Обновление температурной формы
                _tempsFrm?.SetTemps(msg13);

                // Раздача кадра таблицам 
                foreach (var t in _txFl)
                {
                    t.RxFb(msg13);
                }

                foreach (var t in _txFlT)
                {
                    t.RxFb(msg13);
                }

                // Передача в ADC-инфо блок
                _adcInfoKsp?.Ut.Invoke(msg13);



                // Вызов обработки в старом стиле при необходимости
                // Mbox13Rx(msg13);
            }

        }
        #endregion
        #region Анализ флагов CCS
        public void CCSflag_Analiz(string pwmIn)
        {



            if (_firstFlags)
            {
                //----------------------------------------------------------------------------------------------
                if ((ccsFlag.Rt1On & _ccsFlags) != 0) // выход управления тормозного резистора  RT1
                {
                    pb_RT1.Image = _brOn;
                    pb_RT1.Update();
                }
                else
                {
                    pb_RT1.Image = _brOff;
                    pb_RT1.Update();
                }
                //----------------------------------------------------------------------------------------------
                if ((ccsFlag.Rt2On & _ccsFlags) != 0) // выход управления тормозного резистора RT2
                {
                    pb_RT2.Image = _brOn;
                    pb_RT2.Update();
                }
                else
                {
                    pb_RT2.Image = _brOff;
                    pb_RT2.Update();
                }
                //----------------------------------------------------------------------------------------------
                Ip3.BackColor = (ccsFlag.Ain1Trig & _ccsFlags) != 0 ? Color.Salmon : Color.PaleGreen;
                // Триггер АИН1 Мотор
                //      GUon_off.BackColor = (ccsFlag.Ain2Trig & _ccsFlags) != 0 ? Color.Salmon : Color.PaleGreen;
                // Триггер АИН2 Мотор
                Ip4.BackColor = (ccsFlag.Rt1Trig & _ccsFlags) != 0 ? Color.Salmon : Color.PaleGreen;
                // Триггер АИН1 RT1
                //       on_off.BackColor = (ccsFlag.Rt2Trig & _ccsFlags) != 0 ? Color.Salmon : Color.PaleGreen;
                // Триггер АИН1 RT2
            }
            else
            {
                _firstFlags = true;
            }

        }
        #endregion
        #region параметры для вывода графиков третьего окна
        public void define_params_form3()
        {
            if (_f3Win.comboBox1.SelectedIndex >= 0)
            {
                _f3Win.PassIn1 = define_for_grph(_f3Win.comboBox1.SelectedIndex);
            }

            if (_f3Win.comboBox2.SelectedIndex >= 0)
            {
                _f3Win.PassIn2 = define_for_grph(_f3Win.comboBox2.SelectedIndex);
            }

            if (_f3Win.comboBox3.SelectedIndex >= 0)
            {
                _f3Win.PassIn3 = define_for_grph(_f3Win.comboBox3.SelectedIndex);
            }
        }

        private float define_for_grph(int grphIn)
        {
            float grphVal = 0;
            switch (grphIn)
            {
                case 0:
                    grphVal = M1.ccs.Ia_rms;
                    break;
                case 1:
                    grphVal = M1.ccs.Ic_rms;
                    break;
                case 2:
                    grphVal = M1.ccs.Ib_rms;
                    break;
                case 3:
                    grphVal = ccs.Udc;
                    break;
                case 4:
                    grphVal = M2.ccs.Ia_rms;
                    break;
                case 5:
                    grphVal = M2.ccs.Ic_rms;
                    break;
                case 6:
                    grphVal = M2.ccs.Ib_rms;
                    break;
                case 7:
                    grphVal = M1.ccs.c_pa;
                    break;
                case 8:
                    grphVal = ccs.Px1_cl;
                    break;
                case 9:
                    grphVal = ccs.Pz1_cl;
                    break;
                case 10:
                    grphVal = ccs.Pa2_cl;
                    break;
                case 11:
                    grphVal = ccs.Px2_cl;
                    break;
                case 12:
                    grphVal = ccs.Pz2_cl;
                    break;
                case 13:
                    grphVal = M1.ccs.slip_absF * 0.01f;
                    break;
                case 14:
                    grphVal = M2.ccs.slip_absF * 0.01f;
                    break;
                default:
                    grphVal = 0;
                    break;
            }
            if (grphVal >= _graphMax)
            {
                grphVal = _graphMax;
            }

            if (grphVal <= -1 * _graphMax)
            {
                grphVal = -1 * _graphMax;
            }

            return grphVal;
        }
        #endregion
        #region Окно отображаемых параметров при режиме индикации
        // button_AIN1_param_Click убран вместе с кнопкой AIN1_motorName и
        // командами 810..813: на канале 1 стоит однофазный мост, паспорт
        // двигателя ему не нужен. Её место заняла кнопка питания btnInvPower.
        private void button_AIN2_param_Click(object sender, EventArgs e)
        {
            if (M2.Name.BackColor != Color.Yellow)  // если кнопка жёлтая то повторяем посылку параметров для того же двигателя
            {
                M2.Name.BackColor = Color.Yellow;
                Change_motor(M2);
            }

            uint_128(M2.Par.Index, M2.Par.ToothNum, M2.Par.PolePair, 820);

        }

        public void Change_motor(Motor m)
        {
            /* Byte name;		// 0-TAD350, 1-TAD170, 2- GST7, 3- СРД, 4-СГТ-750 , 5-TAD800, 6- 7GST, 7- DC
             UInt16 toothNum;	// число зубьев счётного диска
             Byte pole_pair;	// число пар полюсов
             UInt16 uf_norma;
             */
            // У однофазного моста (M1) кнопки типа двигателя нет - выбирать
            // нечего. Функция общая для обоих каналов, поэтому проверка здесь.
            if (m.Name == null) return;

            switch (m.Name.Text)
            {
                case "ТАД-350":
                    m.Par = TAD170;
                    break;
                case "ТАД-170":
                    m.Par = SRD;
                    break;
                case "СРД":
                    m.Par = DC;
                    break;
                case "ДПТ":
                    m.Par = TAD350;
                    break;

                default:
                    m.Par = TAD350;
                    break;
            }
            m.Name.Text = m.Par.name;
        }
        /*
            if (b.Text == "ТАД-350")
                {
                    m.Par = TAD170;
                    b.Text = "ТАД-170";
                }
                if (b.Text == "ТАД-170")
                {
                    m.Par = SRD;
                    b.Text = "СРД";
                }
                if (b.Text == "СРД")
                {
                    m.Par = TAD350;
                    b.Text = "ТАД-350";
                }

                m.Name.Text = m.Par.name;
            }*/
        //------------------------------------------------------------------------------------------------------------------------------------------------------------
        /*
            if (mot.Text == "7ГСТ")
            {
                m = SRD;
                mot.Text = m.name;
                m.Name.Text = m.Par.name;
                //--------------------------------------------------------------------------------------------------------------
                // меняем вместо Sabs выводим EPSi (эпсилон- угол между магнитной осью и вектором тока статора)
                // вместо S% выводим EPSv (эпсилон- угол между магнитной осью и вектором напряжения статора)
                return m;
            }
            if (mot.Text == "СРД")
            {
                m = SGT750;
                mot.Text = m.name;
                return m;
            }
            if (mot.Text == "СГТ-750")
            {
                m = DTAN160;
                mot.Text = m.name;
                //--------------------------------------------------------------------------------------------------------------
                // меняем вместо Sabs выводим EPSi (эпсилон- угол между магнитной осью ротора и вектором тока статора)
                // вместо S% выводим EPSv (эпсилон- угол между магнитной осью и вектором напряжения статора)
                return m;
            }
            if (mot.Text == "ДТАН-160")
            {
                m = TAD350;
                mot.Text = m.name;
                return m;
            }
            m = TAD350;
            return m;
        }
        */

        private MotPar SetMotorPar(Button mot)
        {

            MotPar m = TAD350;

            switch (mot.Text)
            {
                case "ТАД-350":
                    m = TAD350;
                    break;
                case "ТАД-170":
                    m = TAD170;
                    break;
                case "7ГСТ":
                    m = GST7;
                    break;
                case "СРД":
                    m = SRD;
                    break;
                case "СГТ-750":
                    m = SGT750;
                    break;
                case "ДТАН-160":
                    m = DTAN160;
                    break;
                case "ДПТ":
                    m = DC;
                    break;
            }

            return m;
        }

       #endregion
        #region Открытие окна осциллографа быстрой ленты
        private void tsmScope_Click(object sender, EventArgs e)
        {
            // Одно окно на всю программу: второе смотрело бы на то же кольцо и
            // делило бы с первым курсор и заморозку. Открытое поднимается.
            //
            // Подключение к данным живёт в самом окне (ScopeForm.Feed вызывается
            // из Kadr60Rx), поэтому здесь ничего передавать не надо. Ленту окно
            // просит себе само - TapeStart внутри Open.
            ScopeForm.Open(this);
        }

       #endregion
        #region Открытие окна графиков в окне индикации
        private void button_ain1_plot_Click(object sender, EventArgs e)
        {
            if (!FlagGrphOpen)
            {
                _f3Win.Show();
                _f3Win.Frm3Form1 = this;
                _f3Win.timer1.Enabled = true;
                _f3Win.checkBox1.Checked = true;
                _f3Win.checkBox2.Checked = true;
                _f3Win.checkBox3.Checked = true;
                _f3Win.comboBox1.SelectedIndex = 0;
                _f3Win.comboBox2.SelectedIndex = 1;
                _f3Win.comboBox3.SelectedIndex = 2;
                FlagGrphOpen = true;
            }
        }

        private void button_ain2_plot_Click(object sender, EventArgs e)
        {
            if (!FlagGrphOpen)
            {
                _f3Win.Show();
                _f3Win.Frm3Form1 = this;
                _f3Win.timer1.Enabled = true;
                _f3Win.checkBox1.Checked = true;
                _f3Win.checkBox2.Checked = true;
                _f3Win.checkBox3.Checked = true;
                _f3Win.comboBox1.SelectedIndex = 4;
                _f3Win.comboBox2.SelectedIndex = 5;
                _f3Win.comboBox3.SelectedIndex = 6;
                FlagGrphOpen = true;
            }
        }
        #endregion
        #endregion

        #region Новые окна -- регион соддержит подрегионы
        #region Новые формы -- окна
        private readonly Form3 _f3Win = new Form3();
        private readonly EngineLog _f3En = new EngineLog();
        // Объявление окна параметров двигателя --> Окно параметров двигателя

        // Объявление окна max_sAbs--> Окно установки максимального скольжения
        private readonly FormImitation _fImit = new FormImitation(); // Объявление окна имитации--> Окно имитаци

        // Объявление окна пер. воздействия и проверки транзисторов --> Окно Окно Периодического воздействия
        private readonly ResistorChange _fResis = new ResistorChange();
        // Форма регулирования срабатывания защитного резистора
        private bool _bRefresh;
        private ushort _rpmMode; // Режим "Поле"
        public static object Sabs { get; private set; }

        #endregion
        #region Окно параметров двигателя
        private void установкиДвигателяToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _f3En.Frm1 = this;
            _f3En.Show();
        }
        #endregion
        #region Окно срабатывания резистора
        private void срабатываниеРезистораToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _fResis.Frm1Resistor = this;
            _fResis.Show();
        }
        #endregion
        #region Окно имитации
        private void имитацияДанныхToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            _fImit.Frm1Imit = this; //frm1_imit = адрес объекта f_imit класса
            cmd_128(28); // Посылаем флаг, чтобы началась отправка данных напрямую от АЦП пинов
            AdcDirect = true;
            _adcTransmit = true;
            имитацияДанныхToolStripMenuItem1.Enabled = false;
            _fImit.Show();
        }

        private void сбросToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (имитацияДанныхToolStripMenuItem1.Checked)
            {
                ImitationUdc = false;
                ImitationIu1 = false;
                ImitationIu2 = false;
                ImitationIw1 = false;
                ImitationIw2 = false;
                ImitationIv1 = false;
                ImitationIv2 = false;
                _fImit.RESET_imitparams();
                _fImit.Hide();
            }
        }
        #endregion
        #region Окно графиков
        private void aDCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!FlagGrphOpen)
            {
                _f3Win.Show();
                _f3Win.Frm3Form1 = this;
                _f3Win.timer1.Enabled = true;

                FlagGrphOpen = true;
            }
        }
        #endregion
        #region Функция Окна имитации для различных режимов
        private void adc_imit_select(string switchAdc)
        {
            var iter = 0;
            if (_adcTransmit)
            {
                switch (switchAdc)
                {
                    case "Volt":
                        for (iter = 0; iter < 16; iter += 1)
                        {
                            AdcTemp[iter] = (int)((float)AdcPin[iter] * 3000 / 4096);
                        }

                        break;
                    case "Fizika":
                        for (iter = 0; iter < 3; iter += 1)
                        {
                            _fImit.adcmirror0.DecimalPlaces = 2;
                            _fImit.adcmirror1.DecimalPlaces = 2;
                            _fImit.adcmirror2.DecimalPlaces = 2;
                            AdcTemp[iter] = (AdcPin[iter] - AdcOffset[iter]) * 0.6838M;
                        }
                        AdcTemp[3] = (int)(AdcPin[3] * 0.4132);
                        AdcTemp[4] = (int)(AdcPin[4] * 0.4132);
                        _fImit.adcmirror5.DecimalPlaces = 2;
                        _fImit.adcmirror5.Value = (decimal)((AdcPin[5] - AdcOffset[5]) * 0.4132);
                        for (iter = 6; iter < 10; iter += 1)
                        {
                            AdcTemp[iter] = (int)(AdcPin[iter] * 0.4132);
                        }

                        _fImit.adcmirror10.DecimalPlaces = 2;
                        AdcTemp[10] = (decimal)((AdcPin[10] - AdcOffset[10]) * 0.4132);
                        AdcTemp[11] = (int)(AdcPin[11] * 0.4132);
                        AdcTemp[12] = (int)(AdcPin[12] * 0.4132);
                        for (iter = 13; iter < 16; iter += 1)
                        {
                            AdcTemp[iter] = (AdcPin[iter] - AdcOffset[iter]) * 0.6838M;
                        }

                        break;
                    case "Offset":
                        if (DoOffset) // Выполнить всего 1 раз
                        {
                            for (iter = 0; iter < 16; iter += 1)
                            {
                                AdcTemp[iter] = AdcOffset[iter];
                            }

                            DoOffset = false;
                        }
                        break;
                    default:
                        for (iter = 0; iter < 16; iter += 1)
                        {
                            AdcTemp[iter] = AdcPin[iter];
                        }

                        break;
                }
            }
        }


        public void transmit_to_form_imit()
        {
            if (_adcTransmit)
            {
                if (!_fImit.checkBox_adc0.Checked)
                {
                    _fImit.adcmirror0.Value = AdcTemp[0];
                }

                if (!_fImit.checkBox_adc1.Checked)
                {
                    _fImit.adcmirror1.Value = AdcTemp[1];
                }

                if (!_fImit.checkBox_adc2.Checked)
                {
                    _fImit.adcmirror2.Value = AdcTemp[2];
                }

                if (!_fImit.checkBox_adc3.Checked)
                {
                    _fImit.adcmirror3.Value = AdcTemp[3];
                }

                if (!_fImit.checkBox_adc4.Checked)
                {
                    _fImit.adcmirror4.Value = AdcTemp[4];
                }

                if (!_fImit.checkBox_adc5.Checked)
                {
                    _fImit.adcmirror5.Value = AdcTemp[5];
                }

                if (!_fImit.checkBox_adc6.Checked)
                {
                    _fImit.adcmirror6.Value = AdcTemp[6];
                }

                if (!_fImit.checkBox_adc7.Checked)
                {
                    _fImit.adcmirror7.Value = AdcTemp[7];
                }

                if (!_fImit.checkBox_adc8.Checked)
                {
                    _fImit.adcmirror8.Value = AdcTemp[8];
                }

                if (!_fImit.checkBox_adc9.Checked)
                {
                    _fImit.adcmirror9.Value = AdcTemp[9];
                }

                if (!_fImit.checkBox_adc10.Checked)
                {
                    _fImit.adcmirror10.Value = AdcTemp[10];
                }

                if (!_fImit.checkBox_adc11.Checked)
                {
                    _fImit.adcmirror11.Value = AdcTemp[11];
                }

                if (!_fImit.checkBox_adc12.Checked)
                {
                    _fImit.adcmirror12.Value = AdcTemp[12];
                }

                if (!_fImit.checkBox_adc13.Checked)
                {
                    _fImit.adcmirror13.Value = AdcTemp[13];
                }

                if (!_fImit.checkBox_adc14.Checked)
                {
                    _fImit.adcmirror14.Value = AdcTemp[14];
                }

                if (!_fImit.checkBox_adc15.Checked)
                {
                    _fImit.adcmirror15.Value = AdcTemp[15];
                }
            }
        }
        #endregion
        #endregion

        #region  Ограничения по токам\напряжением и другие параметры
        public void Change_VoltLims()
        {
            int_128((short)_fResis.voltMax.Value, 3333, 0, 55);
            int_128((short)_fResis.voltMin.Value, 3333, 0, 56);
        }

        //------------------------------------------------------------------------------------------------
        private void tKadr60_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                tKadr60_send();
            }
        }

        private void tKadr60_send()
        {
            // Передаём в CCS время цикла передачи медленного кадра в мс
            ushort pass = 0;
            ushort data1 = 0;
            tKadr60.BackColor = Color.Yellow;
            if (ushort.TryParse(tKadr60.Text, out pass))
            {
                data1 = pass; // проверка -- передаем ли мы число
            }
            else
            {
                tKadr60.Text = "Число?";
            }

            uint_128(data1, 3333, 0, 1);
        }

        private void tKadr60_Enter(object sender, EventArgs e)
        {
            tKadr60.BackColor = Color.Empty;
            tKadr60.ForeColor = Color.Black;
        }

        private void tKadr60_Leave(object sender, EventArgs e)
        {
            if (tKadr60.ForeColor == Color.Black)
            {
                tKadr60.BackColor = Color.Yellow;
                uint_128(0, 0, 0, 1);
            }
        }

        #region RPM_ramp
        //----------------------------------------------------------------------------------------------------------------------------
        private void rpm_ramp_Enter(object sender, EventArgs e)
        {
            rpm_ramp.ForeColor = Color.Black;
        }

        private void rpm_ramp_Leave(object sender, EventArgs e)
        {
            rpm_ramp.BackColor = Color.Yellow;
            uint_128(0, 0, 0, 6);
        }

        private void rpm_ramp_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                rpm_ramp_send();
            }

            if (e.KeyCode == Delete)
            {
                rpm_ramp_off();
            }
        }

        private void rpm_ramp_send()
        {
            // Передаём в CCS шаг изменения скорости частоты поля
            ushort pass = 0;
            ushort data1 = 0;
            ushort data3 = 0; // включает рамп
            rpm_ramp.BackColor = Color.Yellow;
            if (ushort.TryParse(rpm_ramp.Text, out pass)) // проверка -- передаем ли мы число
            {
                data1 = (ushort)(pass * 5);
                data3 = 0;
                uint_128(data1, 3333, data3, 6);
            }
            else
            {
                if (rpm_ramp.Text == "off")
                {
                    rpm_ramp_off();
                }
                else
                {
                    rpm_ramp.Text = "Число?";
                }
            }
        }

        private void rpm_ramp_off()
        {
            rpm_ramp.Text = "off";
            rpm_ramp.BackColor = Color.Yellow;
            uint_128(0, 3333, 1, 6);
        }

        //---------------------------------------------------------------------------------------------------
        #endregion RPM_ramp

        private void Imax_Value_Leave(object sender, EventArgs e)
        {
            if (Imax_Value.ForeColor == Color.Black)
            {
                Imax_Value.BackColor = Color.Yellow;
                uint_128(0, 0, 0, 53);
            }
        }
        private void Imax_Value_Enter(object sender, EventArgs e)
        {
            Imax_Value.ForeColor = Color.Black;
        }
        #endregion
        #region Обработка ошибок  драйверов IGBT
        public void U4_U5_Viewer(ushort shr)
        {
            Ip1.BackColor = (shrFlag.Inp1 & shr) != 0 ? Color.Salmon : Color.PaleGreen;
            Ip2.BackColor = (shrFlag.Inp2 & shr) == 0 ? Color.Salmon : Color.PaleGreen;
            Ip3.BackColor = (shrFlag.Inp3 & shr) == 0 ? Color.Salmon : Color.PaleGreen;
            Ip4.BackColor = (shrFlag.Inp4 & shr) == 0 ? Color.Salmon : Color.PaleGreen;

            error_color(DCDC_T1, shrFlag.VT1);
            error_color(DCDC_T2, shrFlag.VT2);
            error_color(DCDC_T3, shrFlag.VT3);
            error_color(DCDC_T4, shrFlag.VT4);
            error_color(T1sixpack, shrFlag.T1);
            error_color(T2sixpack, shrFlag.T2);
            error_color(T3sixpack, shrFlag.T3);
            error_color(T4sixpack, shrFlag.T4);
            error_color(T5sixpack, shrFlag.T5);
            error_color(T6sixpack, shrFlag.T6);
        }

        public void error_color(Control c, uint mask)
        {
            c.BackColor = (mask & ccs.shr) == 0 ? Color.Salmon : Color.PaleGreen;
        }


        #endregion  Обработка ошибок оптических драйверов IGBT
        #region Перегрузка по токам
        //------------------------------------------------------------------------------------------------------
        private void ShowOverLoadErrors(ushort shrTrip)
        {
            List<string> errors = new List<string>(16);
            ushort shr = (ushort)(shrTrip | shrFlag.Inp1 | shrFlag.Inp2 | shrFlag.Inp3 | shrFlag.Inp4 | shrFlag.TS1);
           
            
            if (shr == 0xFFFF)
            {
                _currentErrorMessageEnable = true;
                return;
            }

            if (!_currentErrorMessageEnable)
            {
                return;
            }

           
            {
                if ((shr & shrFlag.VT1) == 0)
                {
                    errors.Add("convertor: авария драйвера T1");
                }

                if ((shr & shrFlag.VT2) == 0)
                {
                    errors.Add("convertor: авария драйвера T2");
                }

                if ((shr & shrFlag.VT3) == 0)
                {
                    errors.Add("convertor: авария драйвера T3");
                }

                if ((shr & shrFlag.VT4) == 0)
                {
                    errors.Add("convertor: авария драйвера T4");
                }

                if ((shr & shrFlag.T1) == 0)
                {
                    errors.Add("invertor: авария драйвера T1");
                }

                if ((shr & shrFlag.T2) == 0)
                {
                    errors.Add("invertor: авария драйвера T2");
                }

                if ((shr & shrFlag.T3) == 0)
                {
                    errors.Add("invertor: авария драйвера T3");
                }

                if ((shr & shrFlag.T4) == 0)
                {
                    errors.Add("invertor: авария драйвера T4");
                }

                if ((shr & shrFlag.T5) == 0)
                {
                    errors.Add("invertor: авария драйвера T5");
                }

                if ((shr & shrFlag.T6) == 0)
                {
                    errors.Add("invertor: авария драйвера T6");
                }

                if ((shr & shrFlag.TS2) == 0)
                {
                    errors.Add("TRIP4: неизвестный");
                }
             
            }

           if (errors.Count == 0)
            {
               errors.Add("Сработала защита");
            }

            _currentErrorMessageEnable = false;
            _udcIgbtOffMessageEnable = false;
            IGBToverTempErrorMessageEnable = false;

            string message = "Сработали защиты:\n- " + string.Join("\n- ", errors);

            void ShowProtectionDialog()
            {
            //    if (isIgbtOverTemperature)
            //    {
             //       IGBT_Temperatures_Menu_Click(null, null);
            //    }

                _ = MessageBox.Show(this, message, "Аварийное отключение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (InvokeRequired)
            {
                Invoke((Action)ShowProtectionDialog);
            }
            else
            {
                ShowProtectionDialog();
            }
        }
        
        private void ShowOverLoadErrors(ushort protectionFlags, ushort shrFlags, ushort shrTripFlags)
        {
            ushort driverFaultFlags = shrTripFlags != 0 ? shrTripFlags : shrFlags;
            SHR_Flags.bitMask trip = new SHR_Flags(driverFaultFlags).bit;
            bool hasTripSnapshot = driverFaultFlags != 0;
            bool hasTripFault = hasTripSnapshot &&
                                (trip.VT1 || trip.VT2 || trip.VT3 || trip.VT4 ||
                                 trip.T1 || trip.T2 || trip.T3 || trip.T4 || trip.T5 || trip.T6 ||
                                 trip.TS1 || trip.TS2);

            if (protectionFlags == 0 && !hasTripFault)
            {
                _currentErrorMessageEnable = true;
                return;
            }

            if (!_currentErrorMessageEnable)
            {
                return;
            }

            List<string> errors = new List<string>(16);

            if ((protectionFlags & errorFlag.Ia_Overload) != 0)
            {
                errors.Add("Перегрузка по току Ia");
            }

            if ((protectionFlags & errorFlag.Ib_Overload) != 0)
            {
                errors.Add("Перегрузка по току Ib");
            }

            if ((protectionFlags & errorFlag.Ic_Overload) != 0)
            {
                errors.Add("Перегрузка по току Ic");
            }

            if ((protectionFlags & errorFlag.Idc_Overload) != 0)
            {
                errors.Add("Перегрузка по току Idc");
            }

            if ((protectionFlags & errorFlag.Udc_Overload) != 0)
            {
                errors.Add("Превышен допустимый порог по напряжению Udc");
            }

            if ((protectionFlags & errorFlag.Vin_Overload) != 0)
            {
                errors.Add("Превышен допустимый порог по напряжению Vin");
            }

            bool isIgbtOverTemperature = false;
            if ((protectionFlags & errorFlag.IGBT_OverTemperature) != 0)
            {
                errors.Add("Перегрев модуля IGBT");
                isIgbtOverTemperature = true;
            }

            if (hasTripSnapshot)
            {
                if (trip.VT1)
                {
                    errors.Add("convertor: авария драйвера T1");
                }

                if (trip.VT2)
                {
                    errors.Add("convertor: авария драйвера T2");
                }

                if (trip.VT3)
                {
                    errors.Add("convertor: авария драйвера T3");
                }

                if (trip.VT4)
                {
                    errors.Add("convertor: авария драйвера T4");
                }

                if (trip.T1)
                {
                    errors.Add("invertor: авария драйвера T1");
                }

                if (trip.T2)
                {
                    errors.Add("invertor: авария драйвера T2");
                }

                if (trip.T3)
                {
                    errors.Add("invertor: авария драйвера T3");
                }

                if (trip.T4)
                {
                    errors.Add("invertor: авария драйвера T4");
                }

                if (trip.T5)
                {
                    errors.Add("invertor: авария драйвера T5");
                }

                if (trip.T6)
                {
                    errors.Add("invertor: авария драйвера T6");
                }

                if (trip.TS1)
                {
                    errors.Add("TRIP4: сработал TS1");
                }

                if (trip.TS2)
                {
                    errors.Add("TRIP4: сработал TS2");
                }
            }

            if (errors.Count == 0)
            {
                errors.Add("Сработала защита");
            }

            _currentErrorMessageEnable = false;
            _udcIgbtOffMessageEnable = false;
            IGBToverTempErrorMessageEnable = false;

            string message = "Сработали защиты:\n- " + string.Join("\n- ", errors);

            void ShowProtectionDialog()
            {
                if (isIgbtOverTemperature)
                {
                    IGBT_Temperatures_Menu_Click(null, null);
                }

                _ = MessageBox.Show(this, message, "Аварийное отключение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (InvokeRequired)
            {
                Invoke((Action)ShowProtectionDialog);
            }
            else
            {
                ShowProtectionDialog();
            }
        }

        //------------------------------------------------------------------------------------------------------
        #endregion
        #region Saturation block
        public int Satur(int value, int min, int max)
        {
            if (value >= max)
            {
                value = max;
            }
            else if (value <= min)
            {
                value = min;
            }

            return value;
        }

        public decimal Satur(decimal value, decimal min, decimal max)
        {
            if (value >= max)
            {
                value = max;
            }
            else if (value <= min)
            {
                value = min;
            }

            return value;
        }
        #endregion
        #region Режим Стабилизации Udc
        private void label_Irms_DoubleClick(object sender, EventArgs e)
        {
            if (I2rms_selector.Text == "rms")
            {
                I2rms_selector.Text = "av";
                ParSelector = ParSelector | Bit.Bit1; // set bit 1
                uint_128(1, 0, 0, 17);
            }
            else
            {
                I2rms_selector.Text = "rms"; //bit.I2av =1
                ParSelector = ParSelector & ~Bit.Bit1; // clr bit 0
                uint_128(0, 0, 0, 17);
            }
        }
        #endregion

        #region SlipMax
        //------------------------------------------------------------------------------------------------------------------
        private void SlipMax_Value_Enter(object sender, EventArgs e)
        {
            ((TextBox)sender).ForeColor = Color.Black;
        }

        private void SlipMax_Value_Leave(object sender, EventArgs e)
        {
            if (!(sender is TextBox))
            {
                return;
            }

            uint_128(0, 0, 0, ((TextBox)sender).Name.Equals(@"SlipMax_Value") ? (ushort)50 : (ushort)51);
            ((TextBox)sender).BackColor = Color.Yellow;
        }

        private void SlipMax_Value_KeyUp(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox))
            {
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                slipMax_send(sender);
            }

            if (e.KeyCode == Delete)
            {
                slipMax_off(sender);
            }
        }

        private void slipMax_send(object sender)
        {
            if (!(sender is TextBox))
            {
                return;
            }
            // Передаём в CCS ограничение по скольжению
            ushort pass = 0;
            ((TextBox)sender).BackColor = Color.Yellow;
            if (ushort.TryParse(((TextBox)sender).Text, out pass)) // проверка -- передаем ли мы число
            {
                var data1 = pass;
                const ushort data3 = 1; // включает ограничение
                uint_128(data1, 3333, data3,
                    ((TextBox)sender).Name.Equals(@"SlipMax_Value") ? (ushort)50 : (ushort)51);
            }
            else
            {
                if (((TextBox)sender).Text == @"off")
                {
                    slipMax_off(sender);
                }
                else
                {
                    ((TextBox)sender).Text = @"Число?";
                }
            }
        }

        private void slipMax_off(object sender)
        {
            if (!(sender is TextBox))
            {
                return;
            } ((TextBox)sender).Text = @"off";
            ((TextBox)sender).BackColor = Color.Yellow;
            uint_128(0, 3333, 0, ((TextBox)sender).Name.Equals(@"SlipMax_Value") ? (ushort)50 : (ushort)51);
        }

        //------------------------------------------------------------------------------------------------
        #endregion SlipMax

        #region MBOX13   /*  Прием 2-го мэйлбокса */

      
        public void Mbox13Rx(byte[] m, ushort cmd, ushort d1, ushort d2, ushort d3)
        {
#if CAN_LOG
            AppendUiCanLogLine("RX13", 1, cmd, d1, d2, d3);
#endif
            // Принимаем  значения переменных
            // data1 - значение
            // data2 - флаг чтения/записи - 3333 - запись
            // data3 - Мод
            //   foreach (var t in _txFl) Invoke(new Action(() => t.RxFb(m)));
            //  foreach (var t in _txFlT) Invoke(new Action(() => t.RxFb(m)));

       

            ushort pass = 0;
            ushort val = 0;
            string txt = string.Empty;

            switch (cmd)
            {
                case 0:
                    {
                      
                        // Настоящий ACK на ping имеет d1=55, d2=55.
                        // Старый застрявший кадр из TX-буфера MCU имеет другие d1/d2 — игнорируем.
                        if (d1 == 55 && d2 == 55)
                        {
                            _mcuAlreadyConnected = (d3 & 0x01) == 1;
                            _mcuUsesFD           = (d3 & 0x02) != 0;
                            _canaM.BackColor = Color.LightGreen;
                            CanA_Ok = tsmConnect.Enabled = true;
                        }
                    }
                    break;
                case 1: // принимаем tKadr60
                    if (d2 != 3333) // чтение из CCS
                    {
                        tKadr60.Text = Convert.ToString(d1);
                        tKadr60.BackColor = Color.Empty;
                        tKadr60.ForeColor = Color.Green;
                        break;
                    }
                    if (ushort.TryParse(tKadr60.Text, out pass))
                    {
                        if (pass == d1)
                        {
                            tKadr60.BackColor = Color.Empty;
                            tKadr60.ForeColor = Color.Green;
                        }
                    }

                    _ = zero.Focus();
                    break;
                case 2: // кнопка "Подключить"
                    tsmConnect.Text = "Отключить";
                    tsmConnect.BackColor = Color.Salmon; // connected cmd recived CCS
                    connect_CAN();
                    break;
                case 3: // кнопка "Отключить"
                    disconnect_CAN();
                    CanA_Ok = false;
                    tsmConnect.Text = "Подключить";
                    tsmConnect.BackColor = Color.DarkSeaGreen; // disconnected cmd recived CCS
                    break;

                case 6: // принимаем rpm_ramp
                    rpm_ramp.BackColor = Color.Empty;
                    rpm_ramp.ForeColor = Color.Green;
                    if (d3 == 1)
                    {
                        rpm_ramp.Text = "off";
                    }
                    else
                    {
                        rpm_ramp.Text = Convert.ToString(d1 / 5);
                    }

                    _ = zero.Focus();
                    break;

                // 10 и 12 - ключи однофазного моста. Отдельно разбирать нечего:
                // кнопка btnInvPower ведётся за битом pwm_On из кадра 140,
                // который идёт постоянно. Ответ нужен лишь как подтверждение,
                // что команда дошла.
                case 10:
                case 12:
                    break;

                case 11: // ACK AIN2 Enable/Disable
                    AIN2_Enable.BackColor = Color.Empty;
                    AIN2_Enable.Checked   = (d1 == 0);
                    break;

                case 61: // ACK DG2
                    DG2.BackColor = Color.Empty;
                    DG2.Text      = (d1 == 1) ? "Г" : "Д";
                    break;

                case 22: // кнопка "Load_Mode_L"
                    M1.load.modeL.BackColor = Color.Empty;
                    break;
                case 23: // кнопка "Load_Mode_L" M2
                    M2.load.modeL.BackColor = Color.Empty;
                    break;

                case 24: // принимаем уставки КУК-ов
                    Общак.Kuk1 = d1;
                    Общак.Kuk2 = d2;
                    Общак.Kuk3 = d3;
                    break;

                case 25: // кнопка "mFast_Change"
                    mFast_Motor.BackColor = Color.Empty;
                    break;

                case 26: // кнопка " F2_motK60"
                    F2_motK60.BackColor = Color.Empty;
                    break;

                case 50:

                    SlipMax_Value.BackColor = Color.Empty;
                    SlipMax_Value.ForeColor = Color.Green;
                    SlipMax_Value.Text = d3 == 0 ? "off" : Convert.ToString(d1);
                    _ = zero.Focus();
                    break;
                case 51:
                    SlipMax_Value2.BackColor = Color.Empty;
                    SlipMax_Value2.ForeColor = Color.Green;
                    SlipMax_Value2.Text = d3 == 0 ? "off" : Convert.ToString(d1);
                    _ = zero.Focus();
                    break;

                case 53: // принимаем значение Imax
                    if (d2 != 3333) // чтение из CCS
                    {
                        Imax_Value.Text = Convert.ToString(d1);
                        Imax_Value.BackColor = Color.Empty;
                        Imax_Value.ForeColor = Color.Green;
                        break;
                    }
                    if (ushort.TryParse(Imax_Value.Text, out pass))
                    {
                        val = pass;
                    }
                    else
                    {
                        Imax_Value.Text = "Число?";
                        break;
                    }
                    if (d1 == pass)
                    {
                        Imax_Value.BackColor = Color.Empty;
                        Imax_Value.ForeColor = Color.Green;
                    }
                    else
                    {
                        Imax_Value.BackColor = Color.Yellow;
                    }
                    _ = zero.Focus();
                    break;
                case 55: // принимаем значение voltMax
                    _fResis.voltMax.ForeColor = Color.Red;
                    _fResis.voltMax.Value = Satur(d1, _fResis.voltMax.Minimum, _fResis.voltMax.Maximum);
                    //       zero.Focus();
                    break;
                case 56: // принимаем значение voltMin
                    _fResis.voltMin.ForeColor = Color.Red;
                    _fResis.voltMin.Value = Satur(d1, _fResis.voltMin.Minimum, _fResis.voltMin.Maximum);
                    //       zero.Focus();
                    break;
                case 59: //подтверждаем выбранный режим управления заданными оборотами
                    if (_rpmMode == d1)
                    {
                        button_Field_RPM.BackColor = Color.Empty;
                    }
                    else
                    {
                        button_Field_RPM.BackColor = Color.Yellow;
                    }

                    break;
                case 72: // принимаем значение SpeedRef
                    // Пока движок отдан под частоту, обороты в него класть
                    // нельзя: ответ на 72 может прийти и позже подмены -
                    // запоздавшим, или в ответ на чей-то чужой запрос.
                    if (_freqOnSpeed) break;

                    NUD_RPM.ValueChanged -= NUD_RPM_ValueChanged;
                    ccs_To_NUD(NUD_RPM, Tbar_speed, d1, d2, d3, 1);
                    NUD_RPM.ValueChanged += NUD_RPM_ValueChanged;
                    break;
                case 73: // принимаем значение sAbs1
                    NUD_Load1.ValueChanged -= NUD_Load1_ValueChanged;
                    Tbar_Load1.ValueChanged -= Tbar_Load1_ValueChanged;
                    ccs_To_NUD(M1.load, d1, d2, d3);
                    NUD_Load1.ValueChanged += NUD_Load1_ValueChanged;
                    Tbar_Load1.ValueChanged += Tbar_Load1_ValueChanged;
                    break;
                case 77: // принимаем значение sAbs2
                    NUD_Load2.ValueChanged -= NUD_Load2_ValueChanged;
                    Tbar_Load2.ValueChanged -= Tbar_Load2_ValueChanged;
                    ccs_To_NUD(M2.load, d1, d2, d3);
                    NUD_Load2.ValueChanged += NUD_Load2_ValueChanged;
                    Tbar_Load2.ValueChanged += Tbar_Load2_ValueChanged;
                    break;
                case 75: // принимаем значение gain1
                    NUD_Gain1.ValueChanged -= NUD_Gain1_ValueChanged;
                    Tbar_Gain1.ValueChanged -= Tbar_Gain1_ValueChanged;
                    Gain1_ModeH.BackColor = Gain1_ModeL.BackColor = Color.Empty;
                    ccs_To_NUD(M1.gain, d1, d2, d3);
                    NUD_Gain1.ValueChanged += NUD_Gain1_ValueChanged;
                    Tbar_Gain1.ValueChanged += Tbar_Gain1_ValueChanged;

                   break;
                case 76: // принимаем значение gain2
                    NUD_Gain2.ValueChanged -= NUD_Gain2_ValueChanged;
                    Tbar_Gain2.ValueChanged -= Tbar_Gain2_ValueChanged;
                    Gain2_ModeH.BackColor = Gain2_ModeL.BackColor = Color.Empty;
                    ccs_To_NUD(M2.gain, d1, d2, d3);
                    NUD_Gain2.ValueChanged += NUD_Gain2_ValueChanged;
                    Tbar_Gain2.ValueChanged += Tbar_Gain2_ValueChanged;

                    break;

                //---- однофазный мост 48->220 --------------------------------
                case 624: // ACK частоты синусоиды sineB
                    GridFreq_Ack(d1, d2);
                    break;

                case 630: // ACK режима моста: d1 = GRID_MODE_*, d3 = gt.State
                    GridMode_Ack(d1);
                    break;

                case 635: // ACK разрешения выхода в сеть: d1 = gt.gridEn
                    GridEn_Ack(d1);
                    break;

                case 632: // уставка тока в сеть, А rms - движок Load1 (Iзад)
                    NUD_Load1.ValueChanged -= NUD_Load1_ValueChanged;
                    Tbar_Load1.ValueChanged -= Tbar_Load1_ValueChanged;
                    ccs_To_NUD(M1.load, d1, d2, d3);
                    NUD_Load1.ValueChanged += NUD_Load1_ValueChanged;
                    Tbar_Load1.ValueChanged += Tbar_Load1_ValueChanged;
                    break;

                case 633: // уставка Urms острова - движок Gain1 в режиме Uref
                    NUD_Gain1.ValueChanged -= NUD_Gain1_ValueChanged;
                    Tbar_Gain1.ValueChanged -= Tbar_Gain1_ValueChanged;
                    Gain1_ModeH.BackColor = Gain1_ModeL.BackColor = Color.Empty;
                    ccs_To_NUD(M1.gain, d1, d2, d3);
                    NUD_Gain1.ValueChanged += NUD_Gain1_ValueChanged;
                    Tbar_Gain1.ValueChanged += Tbar_Gain1_ValueChanged;
                    GridUref_Ack(d1, d2); // то же число в поле U rms на панели моста
                    break;

                case CmdIsetSlew: // 644, рампа уставки тока - поле "Рампа"
                    Slew_Ack(d1, d2);
                    break;

                case 634: // ACK ключа телеметрии
                    TeleSel_Ack(d1);
                    break;

                case 100: // подтверждение команды для запуска записи фаст spf кадра по нажатию F3

                    spf_Tooth_F9 = true;
                    spf_Start(ref spf);
                    toolTip1.SetToolTip(pb_FastKadr, "F3");
                  
                    break;

                case 101:  // подтверждение команды для запуска записи sp2 кадра по нажатию F9

                    sp2_Tooth_F9 = true;
                    spf_Start(ref sp2);
                    toolTip1.SetToolTip(pb_FastKadr, "F9");
                  
                    break;

                case 103:  // подтверждение команды останова потока данных по СANB
                    spf_running = false;

                    break;

                case 338: // тест IGBT: d3 - открытые ключи и флаги теста
                    igbt?.FeedBack(d1, d2, d3);
                    break;

                //---------------------------------------------------------------------------------------------------------------------------
                //---------------------------------------------------------------------------------------------------------------------------
                // Цепочка 810..813 (паспорт двигателя АИН-1) удалена вместе с
                // кнопкой AIN1_motorName: на канале 1 однофазный мост.
                //-----------------------------------------------------------------------------------------------------------------------------
                case 820: // первая команда обновления параметров двигателя
                    uint_128(M2.Par.Rs, M2.Par.Rr, M2.Par.Ls, 821);
                    break;
                case 821:
                    uint_128(M2.Par.Lr, M2.Par.Lm, M2.Par.Inom, 822);
                    break;
                case 822:
                    uint_128(M2.Par.Unom, M2.Par.Fnom, M2.Par.Pnom, 823);
                    break;
                case 823:    // последняя команда обновления параметров двигателя

                    if (M2.Par.Index == d1)
                    {
                        M2.Name.BackColor = Color.LightGray;
                    }
                    //           else M2.Name.BackColor = Color.Yellow;
                    break;
                //--------------------------------------------------------------------------------------------------------------------------------------
                // команды для получения инфо прошивки КСП. Ответ на 333 команду 
                case 32000:
                    IdRcvd = _srx = false;
                    var ch = BitConverter.ToChar(new[] { m[7], m[6] }, 0);
                    if (ch == '\0')
                    {
                        ch = '\xFF';
                    }

                    _ = _s0.Append(ch);

                    break;

                case 32001:
                    spf.mboxCnt = MboxCnt(d1, d2);
                    sp2.mboxCnt = MboxCnt(d3, 0);
                    IdRcvd = _srx = false;

                    var infoText = NormalizeKspInfoString(_s0.ToString());
                    var s = SplitFirmwareInfoFields(infoText);
                    KspIdString = s.Length > 0 ? NormalizeKspVersion(s[0]) : string.Empty;
                    spf.FrameString = s.Length > 1 ? s[1].Replace(".h", ".ini") : string.Empty;
                    sp2.FrameString = s.Length > 2 ? s[2].Replace(".h", ".ini") : string.Empty;
                    BuildDate = s.Length > 3 ? SanitizeFirmwareVersionText(s[3]) : string.Empty;
                    BuildTime = s.Length > 4 ? SanitizeFirmwareVersionText(s[4]) : string.Empty;

                    if (s.Length > 4)
                    {
                        var rawDate = $"{BuildDate} {BuildTime}".Trim();
                        if (DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces,
                                out var parsedDate) ||
                            DateTime.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces,
                                out parsedDate))
                        {
                            Date = parsedDate.ToString("dd.MM.yyyy HH:mm:ss");
                        }
                        else
                        {
                            Date = rawDate;
                        }
                    }
                    else
                    {
                        BuildDate = string.Empty;
                        BuildTime = string.Empty;
                        Date = @"--------------";
                    }

                    _srx = s.Length >= 3;
                    _s0.Length = 0;

                    break;

                case 32002:
                    Kuk1 = Convert.ToString(BitConverter.ToUInt16(new[] { m[3], m[2] }, 0), 2).PadLeft(16, '0');
                    Kuk2 = Convert.ToString(BitConverter.ToUInt16(new[] { m[1], m[0] }, 0), 2).PadLeft(16, '0');
                    Kuk3 = Convert.ToString(BitConverter.ToUInt16(new[] { m[7], m[6] }, 0), 2).PadLeft(16, '0');
                    Kk1 = Kuk1;
                    Kk2 = Kuk2;
                    Kk3 = Kuk3;
                    Kuk1 = Reverse(Kuk1);
                    Kuk2 = Reverse(Kuk2);
                    Kuk3 = Reverse(Kuk3);
                    IdRcvd = _srx;
                    break;
                    //--------------------------------------------------------------------------------------------------------------------------------------
            }

            AppendUiCanLogLine("RX_done", 1, cmd, d1, d2, d3);
            //     _newGenF?.GData(msg13);
            //     _tempsFrm?.Invoke(new Action(() => _tempsFrm.SetTemps(msg13)));


            //     _adcInfoKsp?.Ut.Invoke(msg13);


        }

        private static string NormalizeKspInfoString(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var swapped = SwapUtf16ByteOrder(value);
            var originalScore = ScoreKspInfoString(value);
            var swappedScore = ScoreKspInfoString(swapped);

            if (CountCjkChars(value) >= 3 && CountCjkChars(swapped) < CountCjkChars(value))
            {
                return swapped;
            }

            return swappedScore > originalScore ? swapped : value;
        }

        private static string[] SplitFirmwareInfoFields(string infoText)
        {
            if (string.IsNullOrEmpty(infoText)) return Array.Empty<string>();

            // Если строка была выбрана в варианте с перестановкой байт,
            // разделитель 0x00FF превращается в 0xFF00.
            var normalized = infoText
                .Replace('\uFF00', '\xFF')
                .Replace('\0', '\xFF');

            return normalized.Split(new[] { '\xFF' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string SwapUtf16ByteOrder(string value)
        {
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                chars[i] = (char)(((c & 0x00FF) << 8) | ((c & 0xFF00) >> 8));
            }

            return new string(chars);
        }

        private static int ScoreKspInfoString(string value)
        {
            var score = 0;
            foreach (var c in value)
            {
                if (c == '\xFF')
                {
                    score += 3;
                }
                else if ((c >= 'А' && c <= 'я') || c == 'Ё' || c == 'ё' ||
                         (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                         char.IsDigit(c) || c == ' ' || c == '.' || c == ':' || c == '_' ||
                         c == '-' || c == '/')
                {
                    score++;
                }
                else if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    score -= 2;
                }
                else if (char.IsControl(c))
                {
                    score -= 3;
                }
            }

            return score;
        }

        private static int CountCjkChars(string value)
        {
            var count = 0;
            foreach (var c in value)
            {
                if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    count++;
                }
            }

            return count;
        }

        private static string NormalizeKspVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var normalized = value.Trim();
            var firstPipeIndex = normalized.IndexOf('|');
            if (firstPipeIndex > 0)
            {
                normalized = normalized.Substring(0, firstPipeIndex).Trim();
            }

            normalized = normalized.Replace("->", " ");

            var flashIndex = normalized.IndexOf("FLASH", StringComparison.OrdinalIgnoreCase);
            if (flashIndex >= 0)
            {
                normalized = normalized.Substring(0, flashIndex + "FLASH".Length);
            }
            else
            {
                var headerExtIndex = normalized.IndexOf(".h", StringComparison.OrdinalIgnoreCase);
                if (headerExtIndex > 0)
                {
                    var delimiterIndex = normalized.LastIndexOf('I', headerExtIndex);
                    if (delimiterIndex > 0)
                    {
                        normalized = normalized.Substring(0, delimiterIndex);
                    }
                    else
                    {
                        var spaceIndex = normalized.LastIndexOf(' ', headerExtIndex);
                        if (spaceIndex > 0)
                        {
                            normalized = normalized.Substring(0, spaceIndex);
                        }
                    }
                }
            }

            while (normalized.Contains("  "))
            {
                normalized = normalized.Replace("  ", " ");
            }

            normalized = normalized.Trim(' ', '|', 'I', ';', '-');
            normalized = SanitizeFirmwareVersionText(normalized);

            return normalized;
        }

        private static string SanitizeFirmwareVersionText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if ((c >= 'А' && c <= 'я') || c == 'Ё' || c == 'ё' ||
                    (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                    char.IsDigit(c) || c == ' ' || c == '.' || c == ':' || c == '_' || c == '-' || c == '/')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append(' ');
                }
            }

            var cleaned = sb.ToString();
            while (cleaned.Contains("  "))
            {
                cleaned = cleaned.Replace("  ", " ");
            }

            var tokens = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var filtered = new List<string>(tokens.Length);
            foreach (var token in tokens)
            {
                if (token.Length == 1 && (token.Equals("I", StringComparison.Ordinal) || token.Equals("i", StringComparison.Ordinal)))
                {
                    continue;
                }

                filtered.Add(token);
            }

            return string.Join(" ", filtered).Trim();
        }


        //-----------------------------------------------------------------------------------------------------------------------------------
        private void ccs_To_NUD(NT x, int d1, int d2, int d3)
        {
            if (d2 != 3333)
            {
                x.nud.Value = Satur(d1 / x.kNT, x.nud.Minimum, x.nud.Maximum);
                x.tbar.Value = Satur(d1, x.tbar.Minimum, x.tbar.Maximum);
                x.nud.BackColor = Color.Empty;
                x.nud.ForeColor = Color.Green;
            }
            else
            {
                if (x.tbar.Value == d1)
                {
                    x.nud.BackColor = Color.Empty;
                    x.nud.ForeColor = Color.Green;
                }
                if (x.modeH_kod == d3)
                {
                    x.modeH.BackColor = Color.Empty;
                }
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------

        private void ccs_To_NUD(NumericUpDown nud, TrackBar tbar, int d1, int d2, int d3, decimal kNT)
        {
            if (d2 != 3333)
            {
                nud.Value = Satur(d1 / kNT, nud.Minimum, nud.Maximum);
                tbar.Value = Satur(d1, tbar.Minimum, tbar.Maximum);
                nud.BackColor = Color.Empty;
                nud.ForeColor = Color.Green;
            }
            else
            {
                if (tbar.Value == d1)
                {
                    nud.BackColor = Color.Empty;
                    nud.ForeColor = Color.Green;
                }
            }
        }

        //------------------------------------------------------------------------------------------------------------------------------------
        #endregion MBOX13

        #region Пуск-Стоп кнопка

        private void btnStartStop_Click(object sender, EventArgs e)
        {
            btnStartStop.BackColor = Color.Yellow;
            if (btnStartStop.Text == @"Стоп")
            {
                StopAin();
                откалиброватьToolStripMenuItem.Enabled = true;

            }
            else
            {
                StartAin();
                откалиброватьToolStripMenuItem.Enabled = false;
            }
        }

        private void StartAin()
        {
            tsmConnect.Enabled = tsmBootLoader.Enabled = false;

            cmd_128(4); // нажата Кнопка ПУСК

            _currentErrorMessageEnable = true;
            _udcIgbtOffMessageEnable = true;
            IGBToverTempErrorMessageEnable = true;

            control_Reset(M1);
            control_Reset(M2);

            _startstop = true;
            btnStartStop.Text = @"Стоп";
        }

        private void control_Reset(Motor m)
        {
            // фиксируем выбранный тип мотора по первому нажатию "Пуск".
            // У однофазного моста (M1) кнопки типа нет - фиксировать нечего.
            if (m.Name != null) m.Name.Enabled = false;

            if (m.load.modeH.Text != @"RPM")
            {
                m.load.nud.Value = 0;
                m.load.tbar.Value = 0;
                m.load.nud.BackColor = Color.Yellow;
                uint_128(0, 3333, m.load.modeH_kod, m.load.cmd);
            }
            if (m.gain.modeH.Text != @"Id")
            {
                m.gain.nud.Value = 0;
                m.gain.nud.BackColor = Color.Yellow;
                m.gain.tbar.Value = Satur(0, m.gain.tbar.Minimum, m.gain.tbar.Maximum);
                uint_128(0, 3333, m.gain.modeH_kod, m.gain.cmd);

            }

        }
        private void StopAin()
        {
            cmd_128(5); // Кнопка СТОП
            tsmBootLoader.Enabled = tsmConnect.Enabled = true;

            _startstop = false;
            btnStartStop.Text = @"Пуск";
        }

        /***************************************************************************************/
        #endregion  Пуск-Стоп кнопка

        #region SpeedRef
        private void NUD_RPM_ValueChanged(object sender, EventArgs e)
        {
            Tbar_speed.ValueChanged -= Tbar_speed_ValueChanged;

            SpeedRef_send();
            Tbar_speed.ValueChanged += Tbar_speed_ValueChanged;
        }

        private void SpeedRef_send()
        {
            NUD_RPM.BackColor = Color.Yellow;
            var value = (int)NUD_RPM.Value;
            Tbar_speed.Value = Satur(value, Tbar_speed.Minimum, Tbar_speed.Maximum);

            // В АИН этот движок задаёт не обороты, а частоту моста - см.
            // SpeedSlider_ToFreq в GridTieUi.cs. Развилка одна на оба органа:
            // и движок, и поле приходят сюда через свои ValueChanged.
            if (_freqOnSpeed)
            {
                // Поле F Hz ведём за движком БЕЗ повторной посылки: команда
                // уже уходит отсюда, а вторая подняла бы второй жёлтый, гасить
                // который нечем - ответ на 624 придёт один.
                //
                // Без этого поле осталось бы со старым числом, и сверка в
                // GridFreq_Ack шла бы с ним: прибор подтверждает новое, поле
                // помнит прежнее, совпадения нет - жёлтый не гаснет никогда.
                nudGridFreq.ValueChanged -= GridFreq_Changed;
                nudGridFreq.Value = Satur((decimal)value, nudGridFreq.Minimum, nudGridFreq.Maximum);
                nudGridFreq.ValueChanged += GridFreq_Changed;

                nudGridFreq.BackColor = Color.Yellow;
                uint_128((ushort)value, 3333, 0, CmdGridFreq);
                return;
            }

            int_128(value, 3333, _rpmMode, 72);
        }

        private void NUD_RPM_Enter(object sender, EventArgs e)
        {
            NUD_RPM.ForeColor = Color.Black;
        }

        private void NUD_RPM_Leave(object sender, EventArgs e)
        {
            // Читающая посылка (d2 = 0) - но спрашивать надо о том, чем движок
            // управляет сейчас. В АИН это частота, и запрос оборотов вернул бы
            // в поле обороты, затерев герцы.
            if (_freqOnSpeed)
                uint_128(0, 0, 0, CmdGridFreq);
            else
                int_128(0, 0, 0, 72); // только чтение speedRef из CCS

            NUD_RPM.ForeColor = Color.Black;
        }

        //Изменяется ползунок частоты/оборотов
        private void Tbar_speed_ValueChanged(object sender, EventArgs e)
        {
            NUD_RPM.ValueChanged -= NUD_RPM_ValueChanged;
            decimal value = Tbar_speed.Value;

            NUD_RPM.Value = Satur((int)value, NUD_RPM.Minimum, NUD_RPM.Maximum);
            SpeedRef_send();
            NUD_RPM.ValueChanged += NUD_RPM_ValueChanged;
        }
        #endregion
        #region GAIN
        #region Gain_ModeH_Button_Click
        private void Gain1_ModeH_Click(object sender, EventArgs e)
        {
            Gain_Mode_Switch(M1);    // Выбираем новый режим
            Gain_Mode_Refresh(M1); // Обновляем параметры для нового режима
            if (Gain_Sym.Checked)
            {
                Copy_NT(M2.gain, M1.gain);
                int_128(M1.gain.tbar.Value, 3333, M1.gain.modeH_kod, 76);
            }
        }

        private void Gain2_ModeH_Click(object sender, EventArgs e)
        {
            Gain_Mode_Switch(M2); //Выбираем новый режим
            Gain_Mode_Refresh(M2); // Обновляем параметры для нового режима

        }

        public void Gain_Mode_Switch(Motor x)
        {
            var up = x.gain.modeH;
            var down = x.gain.modeL;
            // --------------------------------------------------------------------------------
            if (x.Par.Type == "DC")
            {
                if (down.Text == @"U")
                {
                    switch (up.Text)
                    {
                        case @"gain":
                            up.Text = @"gain";
                            break;
                        default:
                            up.Text = @"gain";
                            break;

                    }
                }
                //------------------------------------------------------------------ 
                else if (down.Text == @"I")
                {
                    switch (up.Text)
                    {
                        case @"Id":
                            up.Text = @"Id";
                            break;
                        default:
                            up.Text = @"Id";
                            break;
                    }
                }
                else
                {
                    up.Text = @"gain2";
                }
            }

            //---------------------------------------------------------------------------------------
            else  //  if IM motor
            {
                if (down.Text == @"U")
                {
                    switch (up.Text)
                    {
                        case @"gain":
                            up.Text = @"Urms";
                            break;
                        case @"Urms":
                            up.Text = @"U/F";
                            break;
                        // Движок Gain1 - он же задатчик действующего напряжения
                        // однофазного моста в режиме острова. У мотора 2 такого
                        // режима нет: команда 633 одна на прибор.
                        case @"U/F":
                            up.Text = x == M1 ? @"Uref" : @"gain";
                            break;
                        case @"Uref":
                            up.Text = @"gain";
                            break;
                    }
                }

                //------------------------------------------------------------------ 
                else if (down.Text == @"I")
                {
                    switch (up.Text)
                    {
                        case @"Id":
                            up.Text = @"Im";
                            break;
                        case @"Im":
                            up.Text = @"Id";
                            break;

                    }
                }
            }
        }
        #endregion

        public void Gain_modeL_Switch(Motor m)
        {
            var x = m.gain;
            x.modeL.BackColor = Color.Yellow;
            //------------------------------------------------------------------ 
            switch (x.modeL.Text)
            {
                case @"U":
                    x.modeL.Text = @"I";
                    x.modeH.Text = @"Id";
                    break;
                case @"I":
                    x.modeL.Text = @"U";
                    x.modeH.Text = @"gain";
                    break;
            }
        }

        #region Gain1 
        //========================================================================
        private void NUD_Gain1_Enter(object sender, EventArgs e)
        {
            NUD_Gain1.ForeColor = Color.Black;
            NUD_Gain1.ValueChanged += NUD_Gain1_ValueChanged;
        }

        private void NUD_Gain1_ValueChanged(object sender, EventArgs e)
        {
            NUD_ValueChanged(M1.gain);
        }

        private void NUD_Gain1_Leave(object sender, EventArgs e)
        {
            NUD_Gain1.ValueChanged -= NUD_Gain1_ValueChanged;
            Tbar_Gain1.ValueChanged -= Tbar_Gain1_ValueChanged;
            NUD_Leave(M1.gain);
        }

        private void NUD_Gain1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                var d1 = (ushort)(NUD_Gain1.Value * M1.gain.kNT);
                Tbar_Gain1.Value = Satur(d1, Tbar_Gain1.Minimum, Tbar_Gain1.Maximum);
                if (Gain_Sym.Checked)
                {
                    M2.gain.nud.Value = M1.gain.nud.Value;
                    M2.gain.tbar.Value = M1.gain.tbar.Value;
                }
            }
            else
            {
                NUD_Gain1.ForeColor = Color.Black;
            }
        }

        private void Tbar_Gain1_Scroll(object sender, EventArgs e)
        {
            Tbar_Scroll(M1.gain);
          
        }

        private void Tbar_Gain1_ValueChanged(object sender, EventArgs e)
        {
            Tbar_Send(M1.gain);
            if (Gain_Sym.Checked)
            {
                M2.gain.nud.Value = Satur(M1.gain.nud.Value, M2.gain.nud.Minimum, M2.gain.nud.Maximum);
                M2.gain.tbar.Value = Satur(M1.gain.tbar.Value, M2.gain.tbar.Minimum, M2.gain.tbar.Maximum);
            }
        }

        private void Gain1_ModeL_Click(object sender, EventArgs e)
        {
            Gain_modeL_Switch(M1);
            Gain_Mode_Refresh(M1);
            if (Gain_Sym.Checked)
            {
                Copy_NT(M2.gain, M1.gain);
                Gain_Mode_Refresh(M2);
            }
        }
        #endregion Gain1 
        #region Gain2
        //============================================================================
        private void NUD_Gain2_Enter(object sender, EventArgs e)
        {
            NUD_Gain2.ForeColor = Color.Black;
            NUD_Gain2.ValueChanged += NUD_Gain2_ValueChanged;
        }

        private void NUD_Gain2_ValueChanged(object sender, EventArgs e)
        {
            NUD_ValueChanged(M2.gain);
        }

        private void NUD_Gain2_Leave(object sender, EventArgs e)
        {
            NUD_Gain2.ValueChanged -= NUD_Gain2_ValueChanged;
            Tbar_Gain2.ValueChanged -= Tbar_Gain2_ValueChanged;
            NUD_Leave(M2.gain);
        }

        private void NUD_Gain2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                var d1 = (ushort)(NUD_Gain2.Value * M2.gain.kNT);
                Tbar_Gain2.Value = Satur(d1, Tbar_Gain2.Minimum, Tbar_Gain2.Maximum);
                NUD_Gain2.ValueChanged += NUD_Gain2_ValueChanged;
            }
            else
            {
                NUD_Gain2.ForeColor = Color.Black;
            }
        }

        private void Tbar_Gain2_Scroll(object sender, EventArgs e)
        {
            Tbar_Scroll(M2.gain);
        }

        private void Tbar_Gain2_ValueChanged(object sender, EventArgs e)
        {
            Tbar_Send(M2.gain);
        }

        private void Gain2_ModeL_Click(object sender, EventArgs e)
        {
            Gain_modeL_Switch(M2);
            Gain_Mode_Refresh(M2);
        }
        #endregion
        #region Функции режимов Gain
        public void Gain_Mode_Refresh(Motor m)
        {
            NT x = m.gain;

            x.modeH.BackColor = Color.Yellow;
            x.modeL.BackColor = Color.Yellow;
            switch (x.modeH.Text)
            {
                case @"gain":
                    x.modeH_kod = 0;
                    x.kNT = Kgain;
                    x.unit.Text = "%";
                    x.nud.DecimalPlaces = 0;
                    x.nud.Increment = 1M;
                    x.nud.Maximum = x.Adjust.Pars[0].Value;
                    NT_Mode_Change(x, m.ccs.gain, 1);
                    break;

                case @"Urms":
                    x.modeH_kod = 1;
                    x.kNT = KUrms;
                    x.unit.Text = "V";
                    x.nud.DecimalPlaces = 0;
                    x.nud.Increment = 1M;
                    x.nud.Maximum = x.Adjust.Pars[1].Value;
                    NT_Mode_Change(x, (int)m.ccs.Urms, 1m);
                    break;

                case @"U/F":
                    x.modeH_kod = 2;
                    x.kNT = KUF;
                    x.unit.Text = "mV";
                    x.nud.DecimalPlaces = 0;
                    x.nud.Increment = 1M;
                    x.nud.Maximum = x.Adjust.Pars[2].Value;
                    double freq = Math.Abs(m.ccs.freq) / 5.0; // привидение частоты к об/мин
                    if (freq < 1)
                    {
                        freq = 1;
                    }

                    int nudval = (int)((m.ccs.Urms * 1000.0 / freq) + 0.5);
                    NT_Mode_Change(x, nudval, 1m);
                    break;

                case "Id":
                    x.modeH_kod = 3;
                    x.kNT = KId;
                    x.unit.Text = "A";
                    x.nud.DecimalPlaces = 0;
                    x.nud.Increment = 1m;
                    x.nud.Maximum = x.Adjust.Pars[3].Value;
                    NT_Mode_Change(x, m.ccs.Id, 10m);
                    break;

                case "Im":
                    x.modeH_kod = 4;
                    x.kNT = KId;
                    x.unit.Text = "A";
                    x.nud.DecimalPlaces = 0;
                    x.nud.Increment = 1m;
                    x.nud.Maximum = x.Adjust.Pars[4].Value;
                    NT_Mode_Change(x, m.ccs.Im, 10m);
                    break;

                // Действующее напряжение однофазного моста в режиме острова.
                // kNT = 10 даёт шаг движка 0.1 В - ровно единицу команды 633,
                // то есть движок ходит без округлений на всю свою длину.
                case "Uref":
                    x.cmd = CmdUrefRms;
                    x.modeH_kod = 0;
                    x.kNT = 10;
                    x.unit.Text = "V";
                    x.nud.DecimalPlaces = 1;
                    x.nud.Increment = 0.1m;
                    x.nud.Maximum = 250m;
                    NT_Mode_Change(x, (int)Math.Round(_urefSet * 10m), 10m, false);
                    break;

            }

            // Вернуть движку его обычную команду, если ушли из инверторного
            // режима: cmd - поле NT, и переключение режима его больше не
            // восстанавливало бы само.
            if (x.modeH.Text != @"Uref")
                x.cmd = m == M1 ? (ushort)75 : (ushort)76;
        }

        public void UF_Mode(Motor m)
        {
            var x = m.gain;
            x.modeH_kod = 2;
            x.kNT = KUF;
            x.unit.Text = "mV";
            x.nud.DecimalPlaces = 0;
            x.nud.Increment = 1m;
            x.nud.Maximum = x.Adjust.Pars[2].Value;
            double freq = Math.Abs(m.ccs.freq) / 5.0; // привидение частоты к об/мин
            if (freq < 1)
            {
                freq = 1;
            }

            var nudval = (int)((m.ccs.Urms * 1000.0 / freq) + 0.5);
            NT_Mode_Change(x, nudval, 1m);
        }

        public void Id_Mode(Motor m)
        {
            var x = m.gain;
            x.modeH_kod = 3;
            x.kNT = KId;
            x.unit.Text = "A";
            x.nud.DecimalPlaces = 0;
            x.nud.Increment = 1m;
            x.nud.Maximum = x.Adjust.Pars[3].Value;
            NT_Mode_Change(x, m.ccs.Id, 10m);
        }

        public void Im_Mode(Motor m)
        {
            var x = m.gain;
            x.modeH_kod = 4;
            x.kNT = KId;
            x.unit.Text = "A";
            x.nud.DecimalPlaces = 0;
            x.nud.Increment = 1m;
            x.nud.Maximum = x.Adjust.Pars[4].Value;
            NT_Mode_Change(x, m.ccs.Im, 10m);
        }

        private void Gain_Symmetry_Click(object sender, EventArgs e)
        {
            if (Gain_Sym.Checked)
            {
                Tbar_Gain2.Enabled = NUD_Gain2.Enabled = Gain2_ModeH.Enabled = Gain2_ModeL.Enabled = false;
                M2.gain.Adjust = _tblGain1;
                Copy_NT(M2.gain, M1.gain); // ->  int_128(M1.gain.tbar.Value, 3333, M1.gain.mode, 76);
            }
            else
            {
                Tbar_Gain2.Enabled = NUD_Gain2.Enabled = Gain2_ModeH.Enabled = Gain2_ModeL.Enabled = true;
                M2.gain.Adjust = _tblGain2;
                Gain_Mode_Refresh(M2);
            }
        }
        #endregion
        #endregion GAIN

        #region LOAD
        #region Load_Mode  Функции для управления движками задания нагрузки 
        private void Load_ModeH_Switch(Motor m)     // Эта  функция меняет название верхней кнопки переключения режимов задания нагрузки. 
        {
            var x = m.load;
            var up = x.modeH;
            var low = x.modeL;
            up.BackColor = Color.Yellow;
            //--------------------------------------------------------------------------------------------------------------------
            if (m.Par.Type == "DC")
            {
                if (low.Text == @"U")
                {
                    switch (up.Text)
                    {
                        case @"gain":              // задание модуляции напряжения якоря
                        default:
                            up.Text = @"gain";
                            break;
                    }
                }
                else if (low.Text == @"I")
                {
                    switch (up.Text)
                    {
                        case @"Iq":              // задание модуляции напряжения якоря
                        default:
                            up.Text = @"Iq";
                            break;
                    }
                }
                else
                {
                    // up.Text = @"gain";
                    low.Text = @"U";
                }
            }
            //--------------------------------------------------------------------------------------------------------------------------
            else
            {

                if (!Separate_cbox.Checked)
                {
                    switch (up.Text)
                    {

                        case @"S abs":              // задание абсолютного скольжения
                            up.Text = @"Iq";
                            break;
                        case @"Iq":                    // задание Iq для ПИ-регулятора  Iq 
                            up.Text = @"Iq rpm";
                            break;
                        case @"Iq rpm":           // задание ограничения Iq  для ПИ-регулятора  Iq -> RPM ( т.е. поддерживаем заданные обороты изменяя Iq )
                            up.Text = @"Iq freq";
                            break;
                        case @"Iq freq":
                            up.Text = @"S abs";
                            break;

                        default:
                            up.Text = @"S abs";
                            break;
                    }
                }
            }
        }

        private void Load_ModeH_Mode(Motor m)     // Эта  функция в соответствии с  названием верхней кнопки определяет параметры  режимов задания нагрузки. 
        {
            var x = m.load;
            var up = m.load.modeH;  // TextBox верхней кнопки нагрузки

            switch (up.Text)
            {
                case @"S abs":              // задание абсолютного скольжения
                    x.modeH_kod = 0;
                    x.kNT = 100;
                    x.nud.DecimalPlaces = 2;
                    break;
                case @"Iq":                    // задание Iq для ПИ-регулятора  Iq 
                    x.modeH_kod = 1;
                    x.kNT = 1;
                    x.nud.DecimalPlaces = 0;
                    break;
                case @"Iq rpm":           // задание ограничения Iq  для ПИ-регулятора  Iq -> RPM ( т.е. поддерживаем заданные обороты изменяя Iq )

                    x.modeH_kod = 2;
                    break;
                case @"Iq freq":

                    x.modeH_kod = 3;
                    break;

                default:
                    up.Text = @"S abs";
                    x.modeH_kod = 0;
                    break;
            }
            x.nud.Value = 0;
            x.tbar.Value = 0;
        }
        private void Load_ModeL_Switch(Motor m)
        {
            var x = m.load;
            var up = x.modeH;
            var low = x.modeL;
            up.BackColor = Color.Yellow;

            //--------------------------------------------------------------
            if (m.Par.Type == "DC")
            {
                switch (x.modeL.Text)
                {
                    case @"U":
                        up.Text = @"Iq";
                        low.Text = @"I";
                        x.modeL_kod = 0;
                        break;

                    case @"I":
                    default:
                        up.Text = @"gain";
                        low.Text = @"U";
                        x.modeL_kod = 0;
                        break;
                }
            }
            //---------------------------------------------------------------
            else
            {
                switch (x.modeL.Text)
                {
                    case @"RPM":
                        low.Text = @"Поле";
                        x.modeL_kod = 0;
                        break;

                    default:
                    case @"Поле":
                        low.Text = @"RPM";
                        x.modeL_kod = 1;
                        break;
                }
            }
            uint_128(x.modeL_kod, 3333, 0, x.cmd_LL);
        }

        // Галочка Enable у однофазного моста - это РАЗРЕШЕНИЕ на выход в сеть,
        // а не включение ключей: ключами командует btnInvPower. Разрешение
        // ничего не замыкает само, оно лишь снимает блокировку с кнопки
        // "Выход в сеть" - см. GridTieUi.cs.
        private void AIN1_Enable_Click(object sender, EventArgs e)
        {
            AIN1_Enable.BackColor = Color.Yellow;
            uint_128((ushort)(AIN1_Enable.Checked ? 1 : 0), 3333, 0, CmdGridEn);
        }

        private void AIN2_Enable_Click(object sender, EventArgs e)
        {
            AIN2_Enable.BackColor = Color.Yellow;
            ushort d1 = 1;
            if (AIN2_Enable.Checked)
            {
                d1 = 0;
            }

            uint_128(d1, 0, 0, 11);
        }

        private void AIN2_flag_DoubleClick(object sender, EventArgs e)
        {
            ushort d1 = 0;
            if (btnStartStop.Text == "Стоп")
            {
                d1 = 1;
                uint_128(d1, 0, 0, 13);
            }
        }

        private void S1procent_label_DoubleClick(object sender, EventArgs e)
        {
            Label s = S1abs_label;
            Sproc_Text_Change(s);
        }

        private void S2_procent_label_DoubleClick(object sender, EventArgs e)
        {
            Label s = S2abs_label;
            Sproc_Text_Change(s);

        }
        private void Sproc_Text_Change(Label s)
        {
            switch (s.Text)
            {
                case @"Sabs":
                    s.Text = @"S%";
                    break;

                case @"S%":
                    s.Text = @"Wslip";
                    break;

                case @"Wslip":
                    s.Text = @"Sabs";
                    break;
            }

        }

        private void Udc_name_DoubleClick(object sender, EventArgs e)
        {
            Label s = Udc_name;
            Urms_name_Change(s);
        }
        private void Urms_name_Change(Label s)
        {
            switch (s.Text)
            {
                case @"Urms":
                    s.Text = @"Um";
                    break;

                case @"Um":
                    s.Text = @"Urms";
                    break;
            }
        }

        private void Urms2_name_DoubleClick(object sender, EventArgs e)
        {
            Label s = Urms2_name;
            Urms_name_Change(s);
        }

        private void Id1_name_DoubleClick(object sender, EventArgs e)
        {

            Id_Name_switch(M1);
        }

        // Нижний ряд панели АИН1 - это ещё и приборы однофазного моста: Iset,
        // GT, Udc и Err. Третья позиция в цикле есть только у M1: у мотора 2
        // сетевого режима нет, и ставить ему подпись, под которой никогда
        // ничего не появится, незачем.
        //
        // Листаются из этих четырёх только два: Id/uId/Iset и Vd/uVd/Udc.
        // Слоты GT и Err у M1 отданы мосту насовсем - состояние автомата и код
        // аварии нужны всё время, пока мост в сети, а не по случаю: именно по
        // ним видно, почему встала рампа и почему покраснела кнопка режима.
        // Их двойной щелчок снят, см. Iq_Name_switch и Vq_name_Change.
        private void Id_Name_switch(Motor m)
        {
            switch (m.Id_Name.Text)
            {
                case @"Id":
                    m.Id_Name.Text = @"uId";
                    break;
                case @"uId":
                    m.Id_Name.Text = m == M1 ? @"Iset" : @"Id";
                    break;
                default:
                    m.Id_Name.Text = @"Id";
                    break;
            }

            Id_Name_Hint(m);
        }

        //-------------------------------------------------------------------
        // Подсказка под тем, что слот показывает сейчас. Устроена так же, как
        // Iq_Name_Hint, и по той же причине ставится при СМЕНЕ имени, а не
        // в Id_view: тот зовётся из таймера, и SetToolTip на каждом тике
        // гасил бы подсказку прямо под курсором.
        //
        // Iset расписан подробно, остальные два слота - одной строкой.
        // Причина: в нём стоит либо одно число, либо пара со стрелкой, и без
        // объяснения смена формата читается как сбой, а не как рампа. Плюс
        // это единственное поле ряда, где показана УСТАВКА, а не измерение, -
        // перепутать их тут стоит дорого.
        //-------------------------------------------------------------------
        private void Id_Name_Hint(Motor m)
        {
            // Та же проверка, что в Iq_Name_Hint: поля мотора привязываются
            // двумя разными блоками, и на старте половина их ещё null.
            if (m == null || m.Id_Name == null || m.Id_Value == null)
                return;

            string hint;

            switch (m.Id_Name.Text)
            {
                case @"Iset":
                    hint =
                        "Ток в сеть: сколько отдаём и сколько заказано, А rms.\n" +
                        "Действующее значение - сравнивается с Isec напрямую.\n" +
                        "Двойной щелчок переключает слот.\n" +
                        "\n" +
                        "10.00     - одно число: доехали, Iset = Iзад.\n" +
                        "2.4→10.0  - пара: слева Iset, справа уставка Iзад.\n" +
                        "            Идёт рампа, числа ещё не сошлись.\n" +
                        "\n" +
                        "Ни то, ни другое НЕ измерение - это уставки. Правое\n" +
                        "число ставите вы движком Load1 с подписью Iзад\n" +
                        "(команда 632, gt.IsetRef), левое - сколько из него\n" +
                        "разрешила рампа прямо сейчас (gt.Iset). Измеренный\n" +
                        "ток в сеть - в поле Isec, и в работе он равен Iset.\n" +
                        "\n" +
                        "Рампа идёт шагами gt.IsetSlew за период сети: 0.1 А\n" +
                        "на 50 Гц - это 5 А rms/с. Работает в обе стороны, сброс\n" +
                        "уставки тоже спускается плавно: резкий сброс тока -\n" +
                        "это ступенька напряжения на индуктивности рассеяния\n" +
                        "и лишний бросок в сердечник.\n" +
                        "\n" +
                        "Ноль слева при поднятой уставке - не сбой: в SYNC\n" +
                        "контактор разомкнут, и ток держится нулём, пока\n" +
                        "автомат не уйдёт в RAMP. Где он сейчас - в соседнем\n" +
                        "слоте GT.";
                    break;

                case @"uId":
                    hint = "Напряжение по оси d, В.\nДвойной щелчок переключает слот.";
                    break;

                default:
                    hint = "Ток по оси d, А.\nДвойной щелчок переключает слот.";
                    break;
            }

            toolTip1.SetToolTip(m.Id_Name, hint);
            toolTip1.SetToolTip(m.Id_Value, hint);
        }

        // Только для M2. У M1 этот слот больше не листается: он насовсем отдан
        // приборам моста и всегда показывает GT, см. Iq_view.
        private void Iq_Name_switch(Motor m)
        {
            m.Iq_Name.Text = m.Iq_Name.Text == @"Iq" ? @"uIq" : @"Iq";

            Iq_Name_Hint(m);
        }

        //-------------------------------------------------------------------
        // Подсказка под тем, что слот показывает сейчас.
        //
        // Ставится при СМЕНЕ имени, а не в Iq_view: тот зовётся из таймера, и
        // SetToolTip на каждом тике гасил бы подсказку прямо под курсором.
        // У M1 слот не листается вовсе, и подсказка ставится один раз на старте.
        //
        // GT расписан по состояниям: буквы в поле короткие, а автомат за ними
        // решает, замкнут ли контактор и идёт ли ток в сеть, - помнить это
        // наизусть незачем.
        //-------------------------------------------------------------------
        private void Iq_Name_Hint(Motor m)
        {
            // Поля мотора привязываются к контролам двумя разными блоками, и
            // зовут эту функцию в том числе оттуда. Проверка не про аккуратность
            // - она про порядок инициализации, который из тела функции не виден.
            if (m == null || m.Iq_Name == null || m.Iq_Value == null)
                return;

            string hint;

            switch (m.Iq_Name.Text)
            {
                case @"GT":
                    hint =
                        "Состояние автомата подключения к сети (enum GridState,\n" +
                        "my_Files/grid_tie.h).\n" +
                        "\n" +
                        "OFF   - автомат стоит. Это режим АИН либо мост выключен:\n" +
                        "        за сетью не следим, контактор разомкнут.\n" +
                        "\n" +
                        "SYNC  - ловим сеть: ждём подряд идущих хороших периодов\n" +
                        "        (gt.syncNeed) по частоте, амплитуде и фазе.\n" +
                        "        Контактор разомкнут, тока в сеть нет.\n" +
                        "\n" +
                        "RAMP  - захват состоялся, ток поднимается от нуля к\n" +
                        "        уставке со скоростью gt.IsetSlew.\n" +
                        "\n" +
                        "RUN   - рабочий режим, ток держится на уставке Iset.\n" +
                        "\n" +
                        "FAULT - авария, мост закрыт. Код в gt.fault; защёлкнут\n" +
                        "        до следующего пуска автомата.";
                    break;

                case @"uIq":
                    hint = "Напряжение по оси q, В.\nДвойной щелчок переключает слот.";
                    break;

                default:
                    hint = "Ток по оси q, А.\nДвойной щелчок переключает слот.";
                    break;
            }

            toolTip1.SetToolTip(m.Iq_Name, hint);
            toolTip1.SetToolTip(m.Iq_Value, hint);
        }

        private void Id2_name_DoubleClick(object sender, EventArgs e)
        {

            Id_Name_switch(M2);
        }

        private void Iq2_name_DoubleClick(object sender, EventArgs e)
        {

            Iq_Name_switch(M2);
        }
        //------------------------------------------------------------------------------------------------------
        private void Vd2_name_DoubleClick(object sender, EventArgs e)
        {
            Vd_name_Change(M2);
        }

        // Только для M2, как и Vq_name_Change: у M1 этот слот насовсем отдан
        // запасу по индексу модуляции и всегда показывает m, см. Vd_view.
        private void Vd_name_Change(Motor m)
        {
            switch (m.Vd_Name.Text)
            {
                case @"Vd":
                    m.Vd_Name.Text = @"uVd";
                    break;
                default:
                    m.Vd_Name.Text = @"Vd";
                    break;
            }
        }
        // Только для M2, как и Iq_Name_switch: у M1 этот слот насовсем отдан
        // коду аварии сетевого режима и всегда показывает Err, см. Vq_view.
        private void Vq_name_Change(Motor m)
        {
            m.Vq_Name.Text = m.Vq_Name.Text == @"Vq" ? @"uVq" : @"Vq";

            Vq_Name_Hint(m);
        }

        //-------------------------------------------------------------------
        // Подсказка слота Vq/uVq/Err, устроена как Iq_Name_Hint.
        //
        // Err расписан по кодам: в поле стоит голое число, и расшифровать его
        // иначе как заглянув в bin/ErrorCodes.h неоткуда. Слот этот у M1
        // теперь постоянный, так что подсказка - единственная легенда к нему.
        //-------------------------------------------------------------------
        private void Vq_Name_Hint(Motor m)
        {
            if (m == null || m.Vq_Name == null || m.Vq_Value == null)
                return;

            string hint;

            switch (m.Vq_Name.Text)
            {
                case @"Err":
                    hint =
                        "Код последней аварии сетевого режима, gt.fault.\n" +
                        "Таблица - bin/ErrorCodes.h, аварии ставит\n" +
                        "grid_tie_fault() в my_Files/grid_tie.c.\n" +
                        "\n" +
                        "0  - аварий не было.\n" +
                        "60 - пропала сеть: нет захватов нуля U220 дольше\n" +
                        "     полутора периодов.\n" +
                        "61 - превышен ток первички трансформатора.\n" +
                        "62 - превышен ток вторички, то есть ток в сеть.\n" +
                        "63 - напряжение АКБ вне рабочего окна.\n" +
                        "64 - упор индекса модуляции: не хватает Udc или\n" +
                        "     витков, уставка недостижима.\n" +
                        "65 - потерян синхронизм с сетью по частоте,\n" +
                        "     амплитуде или фазе.\n" +
                        "68 - ключи закрыты при замкнутом контакторе: кнопкой\n" +
                        "     питания или компараторной защитой (её авария -\n" +
                        "     в своём индикаторе).\n" +
                        "\n" +
                        "Код защёлкивается и живёт до пуска автомата заново:\n" +
                        "сбрасывает его только grid_tie_stop(), то есть\n" +
                        "переключение режима моста в АИН. Поэтому ненулевое\n" +
                        "число здесь при зелёной кнопке - это память о прошлой\n" +
                        "аварии, а не текущая. Что сейчас - в слоте GT.";
                    break;

                case @"uVq":
                    hint = "Напряжение по оси q, В.\nДвойной щелчок переключает слот.";
                    break;

                default:
                    hint = "Напряжение по оси q, В - фильтрованное (ccs.Vq).\n" +
                           "Двойной щелчок переключает слот.";
                    break;
            }

            toolTip1.SetToolTip(m.Vq_Name, hint);
            toolTip1.SetToolTip(m.Vq_Value, hint);
        }


        private void Vq2_name_DoubleClick(object sender, EventArgs e)
        {
            Vq_name_Change(M2);
        }

        private void Load_Mode_Refresh(Motor m)
        {
            var x = m.load;
            var up = x.modeH;
            up.BackColor = Color.Yellow;
            switch (up.Text)
            {



                case @"S abs":
                    x.modeH_kod = 0;
                    x.kNT = KSabs;
                    x.nud.DecimalPlaces = 2;
                    x.nud.Increment = 0.01M;
                    x.nud.Maximum = x.Adjust.Pars[0].Value;
                    NT_Mode_Change(x, m.ccs.slip_abs, 100);
                    break;
                case @"S %":
                    x.modeH_kod = 1;
                    x.kNT = KSproc;
                    x.nud.DecimalPlaces = 2;
                    x.nud.Increment = 0.01M;
                    x.nud.Maximum = x.Adjust.Pars[1].Value;
                    NT_Mode_Change(x, m.ccs.slip_procF, 100);
                    break;
                case @"S rpm":
                    x.modeH_kod = 2;
                    x.kNT = KSabs;
                    x.nud.DecimalPlaces = 2;
                    x.nud.Increment = 0.01M;
                    x.nud.Maximum = x.Adjust.Pars[0].Value;
                    NT_Mode_Change(x, m.ccs.slip_abs, 100);
                    break;
                case @"Iq":
                    x.modeH_kod = 3;
                    x.kNT = 10;
                    x.nud.DecimalPlaces = 1;
                    x.nud.Increment = 0.1M;
                    x.nud.Maximum = x.Adjust.Pars[3].Value;
                    NT_Mode_Change(x, m.ccs.Iq, 10);
                    break;
                case @"Iq / Id":
                    x.modeH_kod = 4;
                    x.kNT = 10;
                    x.nud.DecimalPlaces = 1;
                    x.nud.Increment = 0.1M;
                    x.nud.Maximum = 100;  // x.Adjust.Pars[3].Value;
                    NT_Mode_Change(x, m.ccs.Iq / m.ccs.Id, 1);
                    break;
                case @"Iq rpm":
                    x.modeH_kod = 5;
                    x.kNT = 10;
                    x.nud.DecimalPlaces = 0;
                    x.nud.Increment = 1M;
                    x.nud.Maximum = x.Adjust.Pars[3].Value;
                    NT_Mode_Change(x, m.ccs.Iq, 10);
                    break;

                case @"Iq freq":
                    x.modeH_kod = 6;
                    x.kNT = 10;
                    x.nud.DecimalPlaces = 0;
                    x.nud.Increment = 1M;
                    x.nud.Maximum = x.Adjust.Pars[3].Value;
                    NT_Mode_Change(x, m.ccs.Iq, 10);
                    break;

                case @"gain":
                    x.modeH_kod = 7;
                    x.kNT = 1;
                    x.nud.DecimalPlaces = 0;
                    x.nud.Increment = 1M;
                    x.nud.Maximum = x.Adjust.Pars[0].Value;
                    NT_Mode_Change(x, m.ccs.slip_abs, 100);
                    break;

                case @"RPM":
                    x.modeH_kod = 8;
                    x.kNT = 1;
                    x.nud.DecimalPlaces = 0;
                    x.nud.Increment = 1;
                    x.nud.Maximum = 6000;
                    NT_Mode_Change(x, m.ccs.freq, 5);
                    break;

                // Уставка тока в сеть, ДЕЙСТВУЮЩЕЕ значение (до 08.10.2026 -
                // амплитуда, и подпись была Iset, как у поля рампы на панели).
                // kNT = 100 даёт шаг движка 0.01 А - единицу команды 632.
                //
                // Предел движка - из таблицы под "*" (строка TblLoad1IsetMax),
                // по умолчанию 17 А rms. До 09.10.2026 был зашит 17.7 - прежние
                // 25 А амплитуды через sqrt2; таблица у Dgt целочисленная.
                // Согласовать предел с защитами решено позже, на железе.
                //
                // Подпись "Iзад", полное имя IsetRef - в подсказке: так зовутся
                // поле в прошивке (gt.IsetRef) и канал в ленте F3_grid. Прежняя
                // подпись Iref столкнулась бы в ленте с мгновенным заданием iRef:
                // Wscope и осциллограф регистр в именах не различают.
                case LoadIsetRef:
                    x.cmd = CmdIsetRef;
                    x.modeH_kod = 0;
                    x.kNT = 100;
                    x.nud.DecimalPlaces = 2;
                    x.nud.Increment = 0.01M;
                    x.nud.Maximum = x.Adjust.Pars[0].Value;
                    NT_Mode_Change(x, (int)Math.Round(_isetSet * 100m), 100m, false);
                    break;

                default:
                    x.modeH_kod = 0;
                    x.kNT = KSabs;
                    x.nud.DecimalPlaces = 2;
                    x.nud.Increment = 0.01M;
                    x.nud.Maximum = x.Adjust.Pars[0].Value;
                    NT_Mode_Change(x, m.ccs.slip_abs, 100);
                    break;


            }

            // См. такой же хвост в Gain_Mode_Refresh: команда - поле NT,
            // и уход из инверторного режима должен её вернуть.
            if (up.Text != LoadIsetRef)
                x.cmd = m == M1 ? (ushort)73 : (ushort)77;
        }

        private void F2kadr_text_Click(object sender, EventArgs e)
        {

        }


        private void mFast_Click(object sender, EventArgs e)
        {
            int d1 = 1;
            d1 = (mFast_Motor.Text == "M1") ? 1 : 2;
            if (mFast_Motor.BackColor != Color.Yellow)
            {
                if (mFast_Motor.Text == "M1")
                {
                    mFast_Motor.Text = "M2";
                    d1 = 2;
                }
                else
                {
                    mFast_Motor.Text = "M1";
                    d1 = 1;
                }
            }

            uint_128((ushort)d1, 3333, 0, 25);
            mFast_Motor.BackColor = Color.Yellow;
        }

        private void F2_click(object sender, EventArgs e)
        {
            ushort d1 = 1;

            if (F2_motK60.BackColor != Color.Yellow)
            {
                if (F2_motK60.Text == "M1")
                {
                    F2_motK60.Text = "M2";
                    motK60 = M2;
                    d1 = 2;
                }
                else
                {
                    F2_motK60.Text = "M1";
                    motK60 = M1;
                    d1 = 1;
                }
            }

            uint_128(d1, 3333, 0, 26);
            F2_motK60.BackColor = Color.Yellow;
        }

        private void Send_motK60()
        {
            ushort d1 = 1;
            if (F2_motK60.Text == "M2")
            {
                d1 = 2;
            }
            /*
{
   d1 = 1;
   motK60 = M1;
else
{
   d1 = 2;
   motK60 = M2;
}
*/
            uint_128(d1, 3333, 0, 26);
            F2_motK60.BackColor = Color.Yellow;
        }

        private void Load1_ModeH_TextChanged(object sender, EventArgs e)
        {

            Load_Mode_Refresh(M1); // определяет параметры для установленного режима
            Tbar_Send(M1.load); // кнопки в жёлтый -> посылает в CCS 
            if (Load_Sym.Checked)
            {
                Copy_NT(M2.load, M1.load); //  -> Tbar_Load2_ValueChange()
                Load_Mode_Refresh(M2); // определяет параметры для установленного режима
            }
        }

        private void Pa2_value_Click(object sender, EventArgs e)
        {

        }



        private void NT_Mode_Change(NT x, int ccs, decimal Kfiz, bool resetLoad = true)
        {
            // int ccs - текущее значение параметра из CCS
            // Kfiz - коэффициент преобразования параметра из CCS в физику. Физическое значение = ccs / Kfiz .
            // resetLoad - сбрасывать ли нагрузку М2 в конце. Инверторные режимы
            //   (Uref, Iset) к моторам отношения не имеют, и сброс чужой
            //   нагрузки при смене их масштаба - действие исподтишка.
            x.tbar.Maximum = (int)(x.nud.Maximum * x.kNT);
            var val = Math.Abs(ccs / Kfiz);
            x.nud.Value = Satur(val, x.nud.Minimum, x.nud.Maximum);
            val = x.nud.Value * x.kNT;
            var tbar = x.tbar.Value;
            x.tbar.Value = Satur((int)val, x.tbar.Minimum, x.tbar.Maximum);
            if (x.tbar.Value == tbar)
            {
                uint_128((ushort)x.tbar.Value, 3333, x.modeH_kod, x.cmd); //принудительно посылаем команду когда нет изменения tbar
            }

            // !!! всегда сбрасываем нагрузку
            if (resetLoad)
                uint_128(M2.load.modeL_kod, 3333, 0, M2.load.cmd_LL);
        }

        private void Load_Sym_Click(object sender, EventArgs e)
        {
            if (Load_Sym.Checked)
            {
                M2.load.tbar.Enabled = M2.load.nud.Enabled = Load2_ModeH.Enabled = Load2_ModeL.Enabled = false;
                M2.load.Adjust = _tblLoad1;
                Copy_NT(M2.load, M1.load);
                int_128(M1.load.tbar.Value, 3333, M1.load.modeH_kod, M2.load.cmd);
                uint_128(M2.load.modeL_kod, 3333, 0, M2.load.cmd_LL);
            }
            else
            {
                M2.load.tbar.Enabled = M2.load.nud.Enabled = Load2_ModeH.Enabled = Load2_ModeL.Enabled = true;
                //      M2.load.Adjust = _tblLoad2;
                //       Load_Mode_Refresh(M2);
            }
        }

        private void Copy_NT(NT x2, NT x1)
        {
            x2.modeH.Text = x1.modeH.Text;
            x2.modeL.Text = x1.modeL.Text;
            x2.modeH_kod = x1.modeH_kod;
            x2.kNT = x1.kNT;
            x2.modeL_kod = x1.modeL_kod;
            x2.nud.Minimum = x1.nud.Minimum;
            x2.nud.Maximum = x1.nud.Maximum;
            x2.nud.Value = x1.nud.Value;
            x2.nud.Increment = x1.nud.Increment;
            x2.nud.DecimalPlaces = x1.nud.DecimalPlaces;
            x2.tbar.Minimum = x1.tbar.Minimum;
            x2.tbar.Maximum = x1.tbar.Maximum;
            x2.tbar.Value = x1.tbar.Value; // запускает событие  x2.tbar.Value_ValueChanged -> жёлтый NUD -> send to CCS
        }
        #endregion  Load_Mode

        #region LOAD1  События
        private void Tbar_Load1_Scroll(object sender, EventArgs e)
        {
            Tbar_Scroll(M1.load);
        }

        private void Tbar_Load1_ValueChanged(object sender, EventArgs e)
        {
            Tbar_Send(M1.load);
            if (Load_Sym.Checked)
            {
                M2.load.tbar.Value = Satur(M1.load.tbar.Value, M2.load.tbar.Minimum, M2.load.tbar.Maximum);
            }
        }


        private void NUD_Load1_Enter(object sender, EventArgs e)
        {
            NUD_Load1.ValueChanged += NUD_Load1_ValueChanged;
            NUD_Load1.ForeColor = Color.Black;
        }

        private void NUD_Load1_KeyPress(object sender, KeyPressEventArgs e)
        {
            NUD_Load1.ForeColor = Color.Black;
            if (e.KeyChar == (char)Keys.Enter)
            {
                var d1 = (ushort)(NUD_Load1.Value * M1.load.kNT);
                Tbar_Load1.Value = Satur(d1, Tbar_Load1.Minimum, Tbar_Load1.Maximum);
                NUD_Load1.ValueChanged += NUD_Load1_ValueChanged;
                if (Load_Sym.Checked)
                {
                    NUD_Load2.Value = Satur(NUD_Load1.Value, NUD_Load2.Minimum, NUD_Load2.Maximum);
                    Tbar_Load2.Value = Satur(Tbar_Load1.Value, Tbar_Load2.Minimum, Tbar_Load2.Maximum);
                }
            }
        }

        private void
            NUD_Load1_ValueChanged(object sender,
                EventArgs e) //это событие используется для передачи нагрузки от NUD к Tbar и затем в CCS
        {
            NUD_ValueChanged(M1.load);
            if (!Load_Sym.Checked)
            {
                return;
            }

            NUD_Load2.Value = Satur(NUD_Load1.Value, NUD_Load2.Minimum, NUD_Load2.Maximum);
            Tbar_Load2.Value = Satur(Tbar_Load1.Value, Tbar_Load2.Minimum, Tbar_Load2.Maximum);
        }

        private void NUD_Load1_Leave(object sender, EventArgs e)
        {
            NUD_Leave(M1.load);
        }

        private void Load1_ModeH_Click(object sender, EventArgs e)
        {
            // Режим у Load1 один - Iзад, перебирать нечего. См. инициализацию.
        }


        private void Load1_ModeL_Click(object sender, EventArgs e)
        {
            Load_ModeL_Switch(M1);   // меняет текст нижней и верхней кнопки
            if (Load_Sym.Checked)
            {
                Copy_NT(M2.load, M1.load);
                uint_128(M2.load.modeL_kod, 3333, 0, M2.load.cmd_LL);
            }
        }
        #endregion LOAD1

        #region LOAD2 События
        private void Tbar_Load2_Scroll(object sender, EventArgs e)
        {
            Tbar_Scroll(M2.load); // значение в NUD и Tbar  -> вызывает Tbar_Load2_ValueChanged
        }

        private void Tbar_Load2_ValueChanged(object sender, EventArgs e)
        {
            Tbar_Send(M2.load); // кнопки в жёлтый->посылает в CCS
        }
        #region textBox_температура
        private void textBox_температура_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                readHiTemp();
            }
        }

        private void textBox_температура_Enter(object sender, EventArgs e)
        {
            textBox_температура.ForeColor = Color.Black;
        }

        private void textBox_температура_Leave(object sender, EventArgs e)
        {
            if (textBox_температура.ForeColor == Color.Black)
            {
                textBox_температура.BackColor = Color.Yellow;

            }
        }

        private void textBox_температура_TextChanged(object sender, EventArgs e)
        {
            textBox_температура.ForeColor = Color.Black;
        }
        #endregion textBox_температура

        private void NUD_Load2_Enter(object sender, EventArgs e)
        {
            NUD_Load2.ValueChanged += NUD_Load2_ValueChanged;
            NUD_Load2.ForeColor = Color.Black;
        }

        private void управлениеToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            управлениеToolStripMenuItem.DropDown.AutoClose = false;
            TopMost = false;
        }

        private void Form1_Click(object sender, EventArgs e)
        {
            управлениеToolStripMenuItem.DropDown.Close();
            управлениеToolStripMenuItem.DropDown.AutoClose = true;

        }

        private void управлениеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            управлениеToolStripMenuItem.DropDown.AutoClose = true;
            //dbg  TopMost = true;

        }

          private void NUD_Load2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                var d1 = (ushort)(M2.load.nud.Value * M2.load.kNT);
                M2.load.tbar.Value = d1;
                NUD_Load2.ValueChanged += NUD_Load2_ValueChanged;
            }
            else
            {
                NUD_Load2.ForeColor = Color.Black;
            }
        }

        // Пункт "напряжение АКБ uBat". Маска 16.
        //
        // Был "датчик Udc" с маской 2 и data2 = 0. Имя Udc досталось от проекта, где
        // у преобразователя было своё звено постоянного тока; в K57 на входе моста
        // стоит АКБ, и это канал uBat (A4, PPB2). Маска 2 указывала на iSec.
        private void откалиброватьUbatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uint_128(16, 3333, 0, 70);
        }

        // Пункт "напряжение сети u220". Маска 8.
        //
        // Был "датчик входного напр. Vin" с маской 4 и data2 = 0. Входное напряжение
        // в K57 - это сеть, канал u220 (A0, PPB1). Маска 4 указывала на iBat.
        //
        // Канал вынесен отдельным пунктом намеренно: на стенде на A0 сидит отладочная
        // синусоида с ЦАП, и калибровать по ней ноль сети смысла нет - см. шапку
        // Offsets.ini. Поэтому "все датчики" на стенде не всегда уместны.
        private void откалиброватьU220ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uint_128(8, 3333, 0, 70);
        }


        private void откалиброватьВсеДатчикиToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 63 - маска всех шести каналов (ADC_CAL_ALL в myADC.h):
            // 1 iPri, 2 iSec, 4 iBat, 8 u220, 16 uBat, 32 uSec.
            // 3333 во втором поле означает запись: без него прошивка команду
            // игнорирует и только отвечает текущим состоянием маски.
            uint_128(63, 3333, 0, 70);
        }

        private void сохранитьКалибровкиToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uint_128(0, 0, 0, 204);
        }

        private void rECALLNvRAMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uint_128(0, 0, 0, 205);
        }


       
        private void testFD_Click(object sender, EventArgs e)
        {
            
            TestCanFd2M();
        }

        private void TestCanFd2M()
        {
            // Возьмём, например, USB канал 1
            TPCANHandle ch = PCANBasic.PCAN_USBBUS1;
            // снять классическую инициализацию, чтобы FD смог захватить канал
            PCANBasic.Uninitialize(ch);

            uint features;
            if (PCANBasic.GetValue(ch, TPCANParameter.PCAN_CHANNEL_FEATURES, out features, sizeof(uint)) != TPCANStatus.PCAN_ERROR_OK ||
                (features & PCANBasic.FEATURE_FD_CAPABLE) == 0)
            {
                MessageBox.Show("Канал не поддерживает CAN-FD.", "FD 2M test",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Строка битрейта для CAN-FD:
            // f_clock = 80 МГц (для USB FD это типично, см. документацию PEAK)
            // nom_*  = фазa арбитража (здесь ~500 kbit/s)
            // data_* = фазa данных (здесь ~2 Mbit/s)
            //
            // Пример параметров PEAK для 500k / 2M (часто используется такой набор):
            //
            //   f_clock=80000000
            //   nom_brp=4   nom_tseg1=63  nom_tseg2=16  nom_sjw=16   -> ~500 kbit/s
            //   data_brp=2  data_tseg1=7  data_tseg2=2 data_sjw=2    -> ~2 Mbit/s
            // 1M/1M CAN-FD:
            //   f_clock=80000000
            //   nom_brp=1  nom_tseg1=63  nom_tseg2=16  nom_sjw=16   -> ~1 Mbit/s
            //   data_brp=1 data_tseg1=31 data_tseg2=8  data_sjw=8   -> ~1 Mbit/s
            //
            string fdBitrate =
            //    "f_clock=80000000," +
            //    "nom_brp=4,nom_tseg1=63,nom_tseg2=16,nom_sjw=16," +
            //    "data_brp=2,data_tseg1=7,data_tseg2=2,data_sjw=2";
         "f_clock=40000000," +
                "nom_brp=2,nom_tseg1=10,nom_tseg2=9,nom_sjw=8," +
                "data_brp=2,data_tseg1=10,data_tseg2=9,data_sjw=8";

            // Инициализируем FD‑канал (НЕ трогая ваш классический Initialize)
            var sts = PCANBasic.InitializeFD(ch, fdBitrate);
            if (sts != TPCANStatus.PCAN_ERROR_OK)
            {
                MessageBox.Show($"CAN-FD init error: {sts}", "FD 2M test");
                return;
            }

            // Пробуем отправить один простой FD‑кадр
            var msg = new TPCANMsgFD
            {
                ID = 0x1,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_FD | TPCANMessageType.PCAN_MESSAGE_BRS,
                DLC = 8,
                DATA = new byte[64]
            };
            msg.DATA[0] = 0x11;
            msg.DATA[1] = 0x22;
            msg.DATA[2] = 0x33;
            msg.DATA[3] = 0x44;
            msg.DATA[4] = 0x55;
            msg.DATA[5] = 0x66;
            msg.DATA[6] = 0x77;
            msg.DATA[7] = 0x88;

            sts = PCANBasic.WriteFD(ch, ref msg);
            if (sts != TPCANStatus.PCAN_ERROR_OK)
            {
                MessageBox.Show($"CAN-FD write error: {sts}", "FD 2M test");
            }
            else
            {
                MessageBox.Show("FD frame sent (nom≈500k, data≈2M)", "FD 2M test");
            }

            PCANBasic.Uninitialize(ch);

        }
        

        private void NUD_Load2_ValueChanged(object sender, EventArgs e)
        {
            NUD_ValueChanged(M2.load);
        }

        private void NUD_Load2_Leave(object sender, EventArgs e)
        {
            NUD_Load2.ForeColor = Color.Black;
            NUD_Load2.BackColor = Color.Yellow;
            uint_128(0, 0, 0, 77); // Read from CCS
        }

        private void Load2_ModeH_Click(object sender, EventArgs e)
        {
            Load_ModeH_Switch(M2);
            Load_Mode_Refresh(M2);


            Tbar_Send(M2.load);
        }

        private void Load2_ModeL_Click(object sender, EventArgs e)
        {
            Load_ModeL_Switch(M2);
        }
        #endregion LOAD2
        #endregion

        #region Таблицы под кнопками параметров движков
        private Dgt _tblGain1, _tblGain2, _tblLoad1, _tblLoad2;

        // Имя строки таблицы Load1. По нему же TblLoad1LoadFx узнаёт, что
        // сохранённая таблица - нынешняя, а не прежняя моторная.
        private const string TblLoad1IsetMax = "Iзад max, А rms";

        private void TbInit()
        {
            _tblGain1 = new Dgt(btnTblGain1, this, TblGain1SaveFx, TblGain1LoadFx);
            _tblGain2 = new Dgt(btnTblGain2, this, TblGain2SaveFx, TblGain2LoadFx);
            _tblLoad1 = new Dgt(btnTblLoad1, this, TblLoad1SaveFx, TblLoad1LoadFx);
            _tblLoad2 = new Dgt(btnTblLoad2, this, TblLoad2SaveFx, TblLoad2LoadFx);

            #region Таблица Gain1
            _tblGain1.Pars.Add(new Dgt.Params { Name = "Gain Max", Value = 1000 });
            _tblGain1.Pars.Add(new Dgt.Params { Name = "Urms Max", Value = 1000 });
            _tblGain1.Pars.Add(new Dgt.Params { Name = "U/F Max", Value = 1000 });
            _tblGain1.Pars.Add(new Dgt.Params { Name = "Id Max", Value = 1000 });
            _tblGain1.Pars.Add(new Dgt.Params { Name = "Id Min", Value = 0 });
            _tblGain1.Pars.Add(new Dgt.Params { Name = "Im Max", Value = 1000 });
            #endregion
            #region Таблица Gain2
            _tblGain2.Pars.Add(new Dgt.Params { Name = "Gain Max", Value = 1000 });
            _tblGain2.Pars.Add(new Dgt.Params { Name = "Urms Max", Value = 1000 });
            _tblGain2.Pars.Add(new Dgt.Params { Name = "U/F Max", Value = 1000 });
            _tblGain2.Pars.Add(new Dgt.Params { Name = "Id Max", Value = 1000 });
            _tblGain2.Pars.Add(new Dgt.Params { Name = "Im Max", Value = 1000 });
            #endregion
            #region Таблица Load1
            // Одна строка - предел движка Iзад. Моторные строки (S abs, S %,
            // S rpm, Iq) ушли вместе с режимами 09.10.2026. Значение у Dgt
            // целое, поэтому в амперах без дробей: прежний зашитый предел
            // 17.7 А rms стал 17. Это предел движка, а не защита прибора.
            _tblLoad1.Pars.Add(new Dgt.Params { Name = TblLoad1IsetMax, Value = 17 });
            #endregion
            #region Таблица Load2
            _tblLoad2.Pars.Add(new Dgt.Params { Name = "S abs Max", Value = 50 });
            _tblLoad2.Pars.Add(new Dgt.Params { Name = "S % Max", Value = 10 });
            _tblLoad2.Pars.Add(new Dgt.Params { Name = "S rpm Max", Value = 50 });
            _tblLoad2.Pars.Add(new Dgt.Params { Name = "Iq Max", Value = 500 });
            #endregion
        }

        #region Сохранение параметров таблиц
        private static void TblGain1SaveFx(string p)
        {
            Settings.Default.TblGain1 = p;
            Settings.Default.Save();
        }

        private static void TblGain2SaveFx(string p)
        {
            Settings.Default.TblGain2 = p;
            Settings.Default.Save();
        }

        private static void TblLoad1SaveFx(string p)
        {
            Settings.Default.TblLoad1 = p;
            Settings.Default.Save();
        }

        private static void TblLoad2SaveFx(string p)
        {
            Settings.Default.TblLoad2 = p;
            Settings.Default.Save();
        }
        #endregion
        #region Загрузка параметров таблиц из настроек
        private static string TblGain1LoadFx()
        {
            return Settings.Default.TblGain1;
        }

        private static string TblGain2LoadFx()
        {
            return Settings.Default.TblGain2;
        }

        private static string TblLoad1LoadFx()
        {
            // Таблица Load1 с 09.10.2026 из одной строки (TblLoad1IsetMax), а
            // в настройках может лежать прежняя моторная из четырёх. Dgt.Init()
            // на сохранённом списке длиннее своего падает выходом индекса за
            // границу - проверено на самой ButtonTable.dll, - и вместе с ним
            // падал бы запуск программы. Чужой состав - берём умолчания.
            string s = Settings.Default.TblLoad1;
            if (string.IsNullOrEmpty(s)) return s;

            try
            {
                var p = Dgt.LoadFromXmlString(s);
                if (p.Count == 1 && p[0].Name == TblLoad1IsetMax) return s;
            }
            catch (Exception)
            {
                // Не разобрался - тоже чужой состав.
            }

            return null;
        }

        private static string TblLoad2LoadFx()
        {
            return Settings.Default.TblLoad2;
        }
        #endregion
        #endregion Таблицы под кнопками параметров движков


        #region NUD_all
        private void NUD_ValueChanged(NT x)
        {
            //1. отстёгивается  при открытии формы и по событию 
            //2.
            //передаёт значение с  NUDa на Tbar
            // Tbar.ValueChanged срабатывает и посылает значение в CCS
            if (x.nud.Focused)
            {
                var d1 = (ushort)(x.nud.Value * x.kNT);
                x.tbar.Value = Satur(d1, x.tbar.Minimum, x.tbar.Maximum);
            }
        }

        private void NUD_Leave(NT x)
        {
            //1. в обработчике события необходимо отключить события по NUD_ValueChanged и Tbar_Send
            //2. посылаем в CCS команду чтения значения параметра
            //3 после приёма параметра из CCS ( Mbox13) значение записывается в NUD
            x.nud.ForeColor = Color.Black;
            x.nud.BackColor = Color.Yellow;
            uint_128(0, 0, 0, x.cmd); // Read from CCS
        }
        #endregion NUD_all
        #region Tbar_alll
        private void Tbar_Scroll(NT x)
        {
            //1. !!! при входе в это событие NUD_ValueChanged отстёгнут
            //2.
            if (x.kNT == 0)
            {
                x.kNT = 1;
            }

            var d1 = x.tbar.Value / (decimal)x.kNT;
            x.nud.Value = Satur(d1, x.nud.Minimum, x.nud.Maximum);

            // Форсируем отрисовку значения, не дожидаясь освобождения очереди UI-потока.
            // Присваивание Value делает только Invalidate(); WM_PAINT — предпоследний по приоритету
            // в GetMessage (ниже posted-сообщений от BeginInvoke и ниже ввода мыши), поэтому при
            // идущей телеметрии цифра в NUD обновлялась рывками — кадры 140 непрерывно кладут
            // BeginInvoke в очередь и она не пустеет.
            // Refresh() = Invalidate(true) + Update(); дочерний TextBox с цифрой обновляем отдельно,
            // т.к. UpdateWindow родителя на детей не распространяется.
            x.nud.Refresh();
            foreach (Control c in x.nud.Controls) c.Update();
        }

        private void Tbar_Send(NT x)
        {
            x.nud.ForeColor = Color.Black;
            x.nud.BackColor = Color.Yellow;

            // Уставки инвертора обратно из прибора не приходят: в кадре 140
            // для них нет слова. Запоминаем последнее посланное - иначе после
            // ухода в моторный режим и обратно движок встал бы в ноль, а мост
            // продолжал бы работать со старой уставкой.
            if (x.cmd == CmdUrefRms)
            {
                _urefSet = x.nud.Value;

                // Поле U rms на панели моста ведём за движком - без повторной
                // посылки, команда уходит строкой ниже. Иначе сверка в
                // GridUref_Ack шла бы со старым числом поля, совпадения не
                // было бы никогда и жёлтый на нём не гас бы.
                nudGridUref.ValueChanged -= GridUref_Changed;
                nudGridUref.Value = Satur(x.nud.Value, nudGridUref.Minimum, nudGridUref.Maximum);
                nudGridUref.ValueChanged += GridUref_Changed;
                nudGridUref.BackColor = Color.Yellow;
            }
            else if (x.cmd == CmdIsetRef) _isetSet = x.nud.Value;

            uint_128((ushort)x.tbar.Value, 3333, x.modeH_kod, x.cmd);
        }

        private void write_to_CCS(NumericUpDown nud, ushort value, ushort cmd)
        {
            nud.ForeColor = Color.Black;
            nud.BackColor = Color.Yellow;
            uint_128(value, 3333, 0, cmd);
        }

        private void read_from_CCS(NumericUpDown nud, ushort cmd)
        {
            nud.ForeColor = Color.Black;
            nud.BackColor = Color.Yellow;
            uint_128(0, 0, 0, cmd);
        }
        #endregion Tbar_all

        #region timer_1sek

        private ushort HiTempBox = 0;    // время паузы в секундах после нажатия кнопки ОК в MessageBox
        private int IGBT_alarm = 0;   // порог температуры модуля IGBT, для включения вентилятора
        private MyMsgBox IGBTmsg = new MyMsgBox("Предупреждение!!!");



        public ushort[] adcOffsets = new ushort[16];
        public void timer_1sek_Tick(object sender, EventArgs e)
        {
            if (_adcInfoKsp != null)
            {
                adcOffsets = _adcInfoKsp._adcOffsets;
            }

            /* Лента быстрого кадра - снять, если её больше никто не просит.
             * Разбор того, почему это стоит здесь, а не в spl_Stop, - в
             * ASt_MainF.cs, регион «Лента быстрого кадра: кто её просит».
             * Коротко: spl_Stop зовут из потока CAN, а команду CAN посылать
             * можно только из UI-потока. */
            TapeRelease();

            /* Набор кадра заперт на время записи: сменить его на ходу значит
             * дописать в тот же .spl другую раскладку. Стоит тоже здесь и по той
             * же причине - запись останавливают и из потока CAN по ошибке, и
             * Spl_Writers_Stop при закрытии программы. */
            if (F2_Kadr_Name.Enabled == spl_running) F2_Kadr_Name.Enabled = !spl_running;

            // Warning Message OverTemperature---------------------------------------------------------------------------------------------------------
            // тело формирования предупреждения о перегреве IGBT

            string ain1 = null;
            string ain2 = null;
            ushort alarm = 0;
            if (M1.ccs.VT1 > IGBT_alarm) { ain1 = ain1 + "    VT1"; alarm = 2; }
            if (M1.ccs.VT2 > IGBT_alarm) { ain1 = ain1 + "   VT2"; alarm = 3; }
            if (M1.ccs.VT3 > IGBT_alarm) { ain1 = ain1 + "   VT3"; alarm = 4; }
            if (M1.ccs.VT4 > IGBT_alarm) { ain1 = ain1 + "   VT4"; alarm = 5; }
            if (ain1 != null) { ain1 = "AIN1: " + ain1 + "\n"; }

            if (M2.ccs.VT1 > IGBT_alarm) { ain2 = ain2 + "   VT1"; alarm = 6; }
            if (M2.ccs.VT2 > IGBT_alarm) { ain2 = ain2 + "   VT2"; alarm = 7; }
            if (M2.ccs.VT3 > IGBT_alarm) { ain2 = ain2 + "   VT3"; alarm = 8; }
            if (M2.ccs.VT4 > IGBT_alarm) { ain2 = ain2 + "   VT4"; alarm = 9; }
            if (ain2 != null) { ain2 = "AIN2:  " + ain2; }

            if (alarm != 0)
            {

                if (IGBTmsg.isClosed)
                {
                    HiTempBox = 0;
                    IGBTmsg.isClosed = false;
                }

                HiTempBox++;
                if (HiTempBox > 20)
                {
                    IGBTmsg.SetBounds(1080, 0, 308, 150); // располагаем в правом верхнем углу от основной формы
                    IGBTmsg.Show("Высокая температура модуля IGBT:\n" + ain1 + ain2 + " \n " + "\nВключите вентилятор охлаждения ШПСУ ");
                    //               Console.Beep();
                    if (IGBTmsg.label1.BackColor == Color.Yellow)  // мигание формы
                    {
                        IGBTmsg.label1.BackColor = DefaultBackColor;
                    }
                    else
                    {
                        IGBTmsg.label1.BackColor = Color.Yellow;
                    }
                }
            }

            else
            {
                HiTempBox = 0;
                IGBTmsg.Visible = false;
            }

            //----------------------------------------------------------------------------------------------------------------
            // тело сообщения  о выключении  IGBT в связи с перегревом модулей             

            if (IGBToverTempErrorMessageEnable)
            {
                IGBToverTempErrorMessageEnable = false;  // защёлка для однакратного вывода  MessageBox

                if ((M1.ccs.errors & errorFlag.IGBT_OverTemperature) != 0)
                {
                    IGBT_Temperatures_Menu_Click(null, null);

                    _ = MessageBox.Show("Включите вентилятор охлаждения ШПСУ", "Перегрев модуля IGBT", MessageBoxButtons.OK, MessageBoxIcon.Error);

                }
            }
        }
        private void readHiTemp()
        {
            if (int.TryParse(textBox_температура.Text, out int pass))
            {
                IGBT_alarm = pass * 10;
                textBox_температура.ForeColor = Color.Green;
                textBox_температура.BackColor = Color.Empty;
                //             IGBToverTempErrorMessageEnable = true;
            }
            else
            {
                textBox_температура.BackColor = Color.Yellow;
                textBox_температура.ForeColor = Color.Black;
                textBox_температура.Text = "Число?";
            }
        }

        #endregion timer_1sek


        #region Закрытие формы Form1

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (tsmConnect.Text == @"Подключить" && !_startstop)
            {

                Spl_Writers_Stop(); //Останавливаем запись .spl .spf  файлов
                if (CanA_Ok)
                {
                    cmd_128(7); // Действия CCS совершаемые при закрытии формы Form1
                    CanaDisconnect();
                }

                // помещаем текущее значение в переменную
                Settings.Default.Separate = Separate_cbox.Checked;

                Settings.Default.AIN2_motor = AIN2_motorName.Text;
                Settings.Default.IGBT_max = Imax_Value.Text;
                Settings.Default.IGBT_temp_max = textBox_температура.Text;
                Settings.Default.Symmetry = Gain_Sym.Checked;
                Settings.Default.LSymmetry = Load_Sym.Checked;
                // S_max больше не сохраняем: поле стало "Рампой" и живёт прибором.
                Settings.Default.S_max2 = SlipMax_Value2.Text;
                Settings.Default.ДГ2 = DG2.Text;

                Settings.Default.Load1_Hkod = M1.load.modeH_kod;
                Settings.Default.load1_Htext = M1.load.modeH.Text;

                Settings.Default.Load1_Lkod = M1.load.modeL_kod;
                Settings.Default.load1_Ltext = M1.load.modeL.Text;

                Settings.Default.Load2_Hkod = M2.load.modeH_kod;
                Settings.Default.load2_Htext = M2.load.modeH.Text;

                Settings.Default.Load2_Lkod = M2.load.modeL_kod;
                Settings.Default.load2_Ltext = M2.load.modeL.Text;

                Settings.Default.Gain1_HText = M1.gain.modeH.Text;
                Settings.Default.Gain1_LText = M1.gain.modeL.Text;

                Settings.Default.Gain2_HText = M2.gain.modeH.Text;
                Settings.Default.Gain2_LText = M2.gain.modeL.Text;

                Settings.Default.AIN2_Enable = M2.AIN_Enable.Checked;
                Settings.Default.mFast = mFast_Motor.Text;
                Settings.Default.mK60 = F2_motK60.Text;
                Settings.Default.SPL_update_ms = tKadr60.Text;
                Settings.Default.Save(); // сохраняем файл с настройками
            }
            else
            {
                if (tsmConnect.Text != @"Подключить" && _startstop)
                {
                    _ = MessageBox.Show(@"Для выхода из программы:
1) Остановите работу АИНов, нажав кнопку СТОП
2) Отключите интерфейс CAN, нажав кнопку ОТКЛЮЧИТЬ",
                        @"Завершение работы", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                if (tsmConnect.Text != @"Подключить" && !_startstop)
                {
                    _ = MessageBox.Show(@"Для выхода из программы отключите интерфейс CAN, нажав кнопку ОТКЛЮЧИТЬ",
                        @"Завершение работы", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                e.Cancel = true;
            }
        }

        /**************************************************************************************************/
        #endregion Закрытие формы Form1
    }
}

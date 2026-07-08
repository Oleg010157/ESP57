using Peak.Can.Basic;
using System;
using System.Drawing;
using System.Windows.Forms;
using ZedGraph;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using ToolTip = System.Windows.Forms.ToolTip;

namespace ICDIBasic
{
    public partial class Tooth_F9 : Form
    {
        Form1 frm1;  // адресс ссылка на Form1
        IniFile Tooth_sp2_f9_config = new IniFile(Application.StartupPath + "\\SPF" + "\\Tooth_sp2_f9_config.ini");
        IniFile FastKadr_spf_f9_config = new IniFile(Application.StartupPath + "\\SPF" + "\\FastKadr_spf_f9_config.ini");
        public Tooth_F9(Form1 fm, ushort t)
        {
            frm1 = fm;
            if (frm1.sp2_Tooth_F9 == true)
            {
                if (t < 256)
                    tooths = t;
                else tooths = 256;
            }
            else if (frm1.spf_Tooth_F9 == true)
            {
                f9_kadr = t;
            }
            InitializeComponent();
            if (frm1.sp2_Tooth_F9 == true)
            {
                Num_kadr.Value = tooths;
            }
            else if (frm1.spf_Tooth_F9 == true)
            {
                Num_kadr.Value = f9_kadr;
            }
        }
        #region Параметры


        public short toothSyncLvl;
        public bool _ToothMarkerSet;
        public TPCANTimestamp _ToothTimestamp;
        public bool _ToothUpd;
        public  ushort tooths; // количество зубъев счётного диска
        ushort nbyte; //  количество байт в одном кадре графика

        public ushort toothSyncRange;
        public byte[,] buff; // двумерный массив [i,j] буфер данных из потока. где i-номер зуба, j- номер параметра в кадре 
        public DateTime toothPreviousDate;

        public string seconds;
        public Point location;
        private GraphPane pn2;
        private GraphPane pn;
        private System.Windows.Forms.ToolTip tooltip;
        public int cbindex1;
        public int cbindex2;
        public int cbindex3;
        public int cbindex4;
        public int cbindex5;
        public int cbindex6;
        public int cbindex7;
        public int cbindex8;
        public decimal numUpDown1;
        public decimal numUpDown2;
        public decimal numUpDown3;
        public decimal numUpDown4;
        public decimal numUpDown5;
        public decimal numUpDown6;
        public decimal numUpDown7;
        public decimal numUpDown8;
        private Graphics graph;

        public static ushort f9_kadr;
        private IniFile config;

        #endregion


        private void Tooth_F9_Load(object sender, EventArgs e)
        {
            formF9_Init();
        }

        #region Запуск формы F9

        private void formF9_Init()
        {
           
            nbyte = (ushort)(frm1._frL * 2 + 1); 

            if (frm1.sp2_Tooth_F9 == true)
            {
                buff = new byte[frm1.tooth, nbyte];
                //Num_kadr.Enabled = false;
                udCh.Enabled = true;
            }
            else if (frm1.spf_Tooth_F9 == true)
            {
                buff = new byte[frm1.kadr, nbyte];
                //Num_kadr.Enabled = true;
                udCh.Enabled = false;
            }

            pictureBox55.Image = (Image)frm1.pb_CANa_Rx.Image.Clone();
            Spl_Tooth_Resize(this, null); // меняет размеры экземпляр формы Tooth_F9
            InitGraph(ZGct, tooths); // создает фому и оформление графиков
            Text += @" Запущен Tooth кадр и Tooth_F9 form"; // меняет название заголовка с MotorControl на SPL_Tooth_Test

            /////
            cb_tm.CheckedChanged += (h, j) => TopMost = cb_tm.Checked; // экземпляр формы Tooth_F9 будет всегда поверх все окон

            button1.Click += (h, j) =>
           {
               frm1.cmd_128(102); // посылает команду на контролер перезапуска передачи даных
           };

            /////
            udLvl.ValueChanged += (a, s) => toothSyncLvl = (short)udLvl.Value; // меняет номер зуба с которого будет начинатся график

            udRng.ValueChanged += (a, s) => toothSyncRange = (ushort)udRng.Value; // ???


            Load_IniConfig_Tooth(); // Считывает сохранёные настройки для формы Tooth_f9
            hints(); // всплывающие подсказки
  //          frm1.f9StartToolStripMenuItem.BackColor = Color.LightGreen;

        }
        #endregion


        #region Построение графика
        public void InitGraph(ZedGraphControl zgc, ushort tooths)
        {
            MasterPane masterPane = zgc.MasterPane; // Создаем экземпляр класса MasterPane, который представляет собой область, на которой "лежать" все графики (экземпляры класса GraphPane)
            pn = zgc.GraphPane; // панель для рисования / панель для графика 




            pn2 = new GraphPane(); // создает вторую панель для графика 
            if (frm1.sp2_Tooth_F9 == true)
            {
                pn.XAxis.Scale.Max = pn2.XAxis.Scale.Max = tooths; // максимальное значение для оси х
            }
            else if (frm1.spf_Tooth_F9 == true)
            {
                pn.XAxis.Scale.Max = pn2.XAxis.Scale.Max = frm1.kadr;
            }
            zgc.IsEnableHZoom = false;   // запрещает горизонтальное масштабирование
            zgc.IsEnableVZoom = true;   // разрешить вертикальное масштабирование (мышкой можно выделить облость чтобы приблизить)
            pn.Fill.Type = pn2.Fill.Type = FillType.Solid; // Закрашивает задний фон всего компонента ZedGraph                                              
            pn.Fill.Color = pn2.Fill.Color = BackColor;       // Заливка будет сплошная
            pn.Chart.Fill.Type = pn2.Chart.Fill.Type = FillType.Solid; // Закрасит задний фон графика 
            pn.Chart.Fill.Color = pn2.Chart.Fill.Color = Color.Black;  // Закрасит задний фон графика 
            pn.IsFontsScaled = pn2.IsFontsScaled = false; // запрещает масштабирование шрифтов

            pn.XAxis.Scale.FontSpec.Size = pn2.XAxis.Scale.FontSpec.Size =                     // Устанфвливает размеры шрифтов для меток вдоль оси Х
                pn.YAxis.Scale.FontSpec.Size = pn2.YAxis.Scale.FontSpec.Size =                 // Устанфвливает размеры шрифтов для меток вдоль оси Y
                    pn.XAxis.Title.FontSpec.Size = pn2.XAxis.Title.FontSpec.Size =             // Устанфвливает размеры шрифтов для подписей по оси X
                        pn.YAxis.Title.FontSpec.Size = pn2.YAxis.Title.FontSpec.Size =         // Устанфвливает размеры шрифтов для подписей по оси Y
                                pn.Legend.FontSpec.Size = pn2.Legend.FontSpec.Size =           // Устанфвливает размеры шрифта для легенды
                                    pn.Title.FontSpec.Size = pn2.Title.FontSpec.Size = 15.0f;  // Устанфвливает размеры шрифта для общего заголовка

            ZGct.GraphPane.Title.IsVisible =                                           // Устанавливает видимость для общего заголовка
                pn2.Title.IsVisible =                                                                 // Устанавливает видимость для общего заголовка
                ZGct.GraphPane.XAxis.IsVisible =                                       // Устанавливает видимость для оси Х
                    ZGct.GraphPane.YAxis.IsVisible =                                   // Устанавливает видимость для оси Y
                       ZGct.GraphPane.Legend.IsVisible =                               // Устанфвливает видимость для легенды
                       pn2.Legend.IsVisible = false;                                                  // Устанфвливает видимость для легенды

            zgc.MasterPane.InnerPaneGap = 2; // управляет размером промежутка/зазор между графиками

            pn.Title.Text = pn2.Title.Text = @"Тест"; // Текст для общего заголовка
            if (frm1.sp2_Tooth_F9 == true)
            {
                pn.XAxis.Title.Text = pn2.XAxis.Title.Text = @"Зуб"; // Текст для заголовка оси Х
            }
            else if (frm1.spf_Tooth_F9 == true)
            {
                pn.XAxis.Title.Text = pn2.XAxis.Title.Text = @"Кадр"; // Текст для заголовка оси Х
            }

            pn.YAxis.Title.Text = pn2.YAxis.Title.Text = @"Значение"; // Текст для заголовка оси Y

            #region Лямбды CheckBoxes

            Num_kadr.ValueChanged += (a, s) =>
            {
                if (frm1.sp2_Tooth_F9 == true)
                {
                    frm1.tooth = (ushort)Num_kadr.Value;
                    buff = new byte[frm1.tooth, nbyte];
                    pn.XAxis.Scale.Max = pn2.XAxis.Scale.Max = frm1.tooth;
                    frm1.toothFrameCounter = 0;
                }
                else if (frm1.spf_Tooth_F9 == true)
                {
                    frm1.kadr = (ushort)Num_kadr.Value;
                    buff = new byte[frm1.kadr, nbyte];
                    pn.XAxis.Scale.Max = pn2.XAxis.Scale.Max = frm1.kadr;
                    frm1.kadrCounter = 0;
                }
            };

            cb_autoScale.CheckedChanged += (a, s) =>
            {
                if (cb_autoScale.Checked == true)
                {
                    pn.YAxis.Scale.MinAuto = false;
                    pn.YAxis.Scale.MaxAuto = false;
                }
                else
                {
                    pn.YAxis.Scale.MinAuto = true;
                    pn.YAxis.Scale.MaxAuto = true;
                }

            };

            cb_autoScale2.CheckedChanged += (a, s) =>
            {
                if (cb_autoScale2.Checked == true)
                {
                    pn2.YAxis.Scale.MinAuto = false;
                    pn2.YAxis.Scale.MaxAuto = false;
                }
                else
                {
                    pn2.YAxis.Scale.MinAuto = true;
                    pn2.YAxis.Scale.MaxAuto = true;
                }

            };

            cbXY.CheckedChanged += (a, s) =>
            {
                Visible_XY(); // делает оси со значениями видемыми
            };

            cb_сетка.CheckedChanged += (a, s) =>
            {
                Visible_сетка();     // делает сетку видимой               
            };
            chBox_GrUpdEn.CheckedChanged += (a, s) =>
            {
                if (chBox_GrUpdEn.Checked)
                {
                    cb_Hline1.Enabled = true;
                    cb_Hline2.Enabled = true;
                    cb_Vline1.Enabled = true;
                    cb_Vline2.Enabled = true;
                    cb_H2line1.Enabled = true;
                    cb_H2line2.Enabled = true;
                    cb_V2line1.Enabled = true;
                    cb_V2line2.Enabled = true;
                }
                else
                {
                    cb_Hline1.Enabled = false;
                    cb_Hline2.Enabled = false;
                    cb_Vline1.Enabled = false;
                    cb_Vline2.Enabled = false;
                    cb_H2line1.Enabled = false;
                    cb_H2line2.Enabled = false;
                    cb_V2line1.Enabled = false;
                    cb_V2line2.Enabled = false;
                }
            };

            cbc1.CheckedChanged += (a, s) =>
            {
                if (cb_Hline1.Checked || cb_Hline2.Checked)
                {
                    line_cb();
                }
            };
            cbc2.CheckedChanged += (a, s) =>
            {
                if (cb_Hline1.Checked || cb_Hline2.Checked)
                {

                    line_cb();
                }
            };
            cbc3.CheckedChanged += (a, s) =>
            {
                if (cb_Hline1.Checked || cb_Hline2.Checked)
                {

                    line_cb();
                }
            };
            cbc4.CheckedChanged += (a, s) =>
            {
                if (cb_Hline1.Checked || cb_Hline2.Checked)
                {

                    line_cb();
                }
            };

            cbc5.CheckedChanged += (a, s) =>
            {
                if (cb_H2line1.Checked || cb_H2line2.Checked)
                {

                    line_cb2();
                }
            };
            cbc6.CheckedChanged += (a, s) =>
            {
                if (cb_H2line1.Checked || cb_H2line2.Checked)
                {

                    line_cb2();
                }
            };
            cbc7.CheckedChanged += (a, s) =>
            {
                if (cb_H2line1.Checked || cb_H2line2.Checked)
                {

                    line_cb2();
                }
            };
            cbc8.CheckedChanged += (a, s) =>
            {
                if (cb_H2line1.Checked || cb_H2line2.Checked)
                {

                    line_cb2();
                }
            };



            comboBox1.SelectedIndexChanged += (a, s) =>
            {
                if (cb_Hline1.Checked || cb_Hline2.Checked)
                {
                    line_cb();
                }
            };
            comboBox2.SelectedIndexChanged += (a, s) =>
            {
                if (cb_Hline1.Checked || cb_Hline2.Checked)
                {
                    line_cb();
                }
            };
            comboBox3.SelectedIndexChanged += (a, s) =>
            {
                if (cb_Hline1.Checked || cb_Hline2.Checked)
                {
                    line_cb();
                }
            };
            comboBox4.SelectedIndexChanged += (a, s) =>
            {
                if (cb_Hline1.Checked || cb_Hline2.Checked)
                {
                    line_cb();
                }
            };

            comboBox5.SelectedIndexChanged += (a, s) =>
            {
                if (cb_H2line1.Checked || cb_H2line2.Checked)
                {
                    line_cb2();
                }
            };
            comboBox6.SelectedIndexChanged += (a, s) =>
            {
                if (cb_H2line1.Checked || cb_H2line2.Checked)
                {
                    line_cb2();
                }
            };
            comboBox7.SelectedIndexChanged += (a, s) =>
            {
                if (cb_H2line1.Checked || cb_H2line2.Checked)
                {
                    line_cb2();
                }
            };
            comboBox8.SelectedIndexChanged += (a, s) =>
            {
                if (cb_H2line1.Checked || cb_H2line2.Checked)
                {
                    line_cb2();
                }
            };



            cb_Hline1.CheckedChanged += (a, s) =>
            {
                if (cb_Hline1.Checked)
                {
                    line_cb();
                }
            };
            cb_Hline2.CheckedChanged += (a, s) =>
            {
                if (cb_Hline2.Checked)
                {
                    line_cb();
                }
            };

            cb_H2line1.CheckedChanged += (a, s) =>
            {
                if (cb_H2line1.Checked)
                {
                    line_cb2();
                }
            };
            cb_H2line2.CheckedChanged += (a, s) =>
            {
                if (cb_H2line2.Checked)
                {
                    line_cb2();
                }
            };

            #endregion


            if (masterPane.PaneList.Count == 1)
            {
                masterPane.Add(pn); // первый график добавляется в область, на которой будут "лежать" все графики

                if (cb_2graph.Checked)
                {
                    masterPane.Add(pn2); // второй график добавляется в область, на которой будут "лежать" все графики
                }
                zgc.MasterPane.PaneList.RemoveAt(0); // удаляет из списка графиков первый график
            }


            using (Graphics grph = CreateGraphics())
            {
                masterPane.ReSize(grph);// заставляет перерисовыет все размеры
            }

        }

        public void CreateGraph(ZedGraphControl zgc)
        {
            var l0 = new PointPairList(); // список точек графика                                           
            var l1 = new PointPairList(); // список точек графика
            var l2 = new PointPairList(); // список точек графика
            var l3 = new PointPairList(); // список точек графика
            var l4 = new PointPairList(); // список точек графика
            var l5 = new PointPairList(); // список точек графика                                           
            var l6 = new PointPairList(); // список точек графика                                           
            var l7 = new PointPairList(); // список точек графика                                           
            var l8 = new PointPairList(); // список точек графика                                           

            Invoke(new Action(() =>
            {
                if (frm1.sp2_Tooth_F9 == true)
                {
                    for (var i = 0; i < frm1.tooth; i++) // Заполняет список точек
                    {
                        if (cbc1.Checked & cbindex1 >= 1) { l0 = ToothGraph_sp2(i, cbindex1); l1.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc2.Checked & cbindex2 >= 1) { l0 = ToothGraph_sp2(i, cbindex2); l2.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc3.Checked & cbindex3 >= 1) { l0 = ToothGraph_sp2(i, cbindex3); l3.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc4.Checked & cbindex4 >= 1) { l0 = ToothGraph_sp2(i, cbindex4); l4.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc5.Checked & cbindex5 >= 1) { l0 = ToothGraph_sp2(i, cbindex5); l5.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc6.Checked & cbindex6 >= 1) { l0 = ToothGraph_sp2(i, cbindex6); l6.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc7.Checked & cbindex7 >= 1) { l0 = ToothGraph_sp2(i, cbindex7); l7.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc8.Checked & cbindex8 >= 1) { l0 = ToothGraph_sp2(i, cbindex8); l8.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                    }
                }


                if (frm1.spf_Tooth_F9 == true)
                {
                    for (var i = 0; i < frm1.kadr; i++) // Заполняет список точек
                    {
                        if (cbc1.Checked & cbindex1 >= 1) { l0 = ToothGraph_spf(i, cbindex1); l1.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc2.Checked & cbindex2 >= 1) { l0 = ToothGraph_spf(i, cbindex2); l2.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc3.Checked & cbindex3 >= 1) { l0 = ToothGraph_spf(i, cbindex3); l3.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc4.Checked & cbindex4 >= 1) { l0 = ToothGraph_spf(i, cbindex4); l4.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc5.Checked & cbindex5 >= 1) { l0 = ToothGraph_spf(i, cbindex5); l5.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc6.Checked & cbindex6 >= 1) { l0 = ToothGraph_spf(i, cbindex6); l6.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc7.Checked & cbindex7 >= 1) { l0 = ToothGraph_spf(i, cbindex7); l7.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                        if (cbc8.Checked & cbindex8 >= 1) { l0 = ToothGraph_spf(i, cbindex8); l8.Add(l0); } //по индексу комбобокса выбератся какие значения будут выводится на графике
                    }
                }
            }));

            if (chBox_GrUpdEn.Checked)
            {
                pn.CurveList.Clear();  // Очищает список кривых
                pn.GraphObjList.Clear();

            }
            if (!chBox_GrUpdEn.Checked)
            {
                if (cb_Hline1.Checked)
                {
                    //pn.CurveList.RemoveAt(pn.CurveList.Count - 1);
                    pn.GraphObjList.RemoveAt(pn.GraphObjList.Count - 1);
                }
                if (cb_Hline2.Checked)
                {
                    //pn.CurveList.RemoveAt(pn.CurveList.Count - 1);
                    pn.GraphObjList.RemoveAt(pn.GraphObjList.Count - 1);
                }
                if (cb_Vline1.Checked)
                {
                    pn.GraphObjList.RemoveAt(pn.GraphObjList.Count - 1);
                }
                if (cb_Vline2.Checked)
                {
                    pn.GraphObjList.RemoveAt(pn.GraphObjList.Count - 1);
                }


                if (cb_H2line1.Checked)
                {
                    //pn.CurveList.RemoveAt(pn.CurveList.Count - 1);
                    pn2.GraphObjList.RemoveAt(pn2.GraphObjList.Count - 1);
                }
                if (cb_H2line2.Checked)
                {
                    //pn.CurveList.RemoveAt(pn.CurveList.Count - 1);
                    pn2.GraphObjList.RemoveAt(pn2.GraphObjList.Count - 1);
                }
                if (cb_V2line1.Checked)
                {
                    pn2.GraphObjList.RemoveAt(pn2.GraphObjList.Count - 1);
                }
                if (cb_V2line2.Checked)
                {
                    pn2.GraphObjList.RemoveAt(pn2.GraphObjList.Count - 1);
                }

            }
            if (chBox_GrUpdEn.Checked && cb_2graph.Checked)
            {
                pn2.CurveList.Clear();// Очищает список кривых
                pn2.GraphObjList.Clear();
            }
                if (chBox_GrUpdEn.Checked)
                {
                    if (cbc1.Checked) pn.AddCurve(cbc1.Text, l1, cbc1.BackColor, SymbolType.None); // дабавляет линию графика со значениями
                    if (cbc2.Checked) pn.AddCurve(cbc2.Text, l2, cbc2.BackColor, SymbolType.None);  // дабавляет линию графика со значениями
                    if (cbc3.Checked) pn.AddCurve(cbc3.Text, l3, cbc3.BackColor, SymbolType.None);  // дабавляет линию графика со значениями
                    if (cbc4.Checked) pn.AddCurve(cbc4.Text, l4, cbc4.BackColor, SymbolType.None);  // дабавляет линию графика со значениями
                    if (cbc5.Checked & cb_2graph.Checked) pn2.AddCurve(cbc3.Text, l5, cbc5.BackColor, SymbolType.None);  // дабавляет линию графика со значениями
                    if (cbc6.Checked & cb_2graph.Checked) pn2.AddCurve(cbc4.Text, l6, cbc6.BackColor, SymbolType.None);  // дабавляет линию графика со значениями
                    if (cbc7.Checked & cb_2graph.Checked) pn2.AddCurve(cbc4.Text, l7, cbc7.BackColor, SymbolType.None);  // дабавляет линию графика со значениями
                    if (cbc8.Checked & cb_2graph.Checked) pn2.AddCurve(cbc4.Text, l8, cbc8.BackColor, SymbolType.None);  // дабавляет линию графика со значениями
                }
         
            zgc.AxisChange();//обновляет данные об осях.
            

            if (cb_Hline1.Checked)
            {
                LineObj Hline1 = new LineObj(pn.XAxis.Scale.Min, (double)numUpDown1, pn.XAxis.Scale.Max, (double)numUpDown1);
                Hline1.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                Hline1.Line.Color = Color.WhiteSmoke;
                Hline1.Line.Width = 2.0f;
                pn.GraphObjList.Add(Hline1);
            };
            if (cb_Hline2.Checked)
            {
                LineObj Hline2 = new LineObj(pn.XAxis.Scale.Min, (double)numUpDown2, pn.XAxis.Scale.Max, (double)numUpDown2);
                Hline2.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                Hline2.Line.Color = Color.WhiteSmoke;
                Hline2.Line.Width = 2.0f;
                pn.GraphObjList.Add(Hline2);
            };

            if (cb_Vline1.Checked)
            {
                LineObj Vline1 = new LineObj((double)numUpDown3, pn.YAxis.Scale.Min, (double)numUpDown3, pn.YAxis.Scale.Max);
                Vline1.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                Vline1.Line.Color = Color.WhiteSmoke;
                Vline1.Line.Width = 2.0f;
                pn.GraphObjList.Add(Vline1);
            };

            if (cb_Vline2.Checked)
            {
                LineObj Vline2 = new LineObj((double)numUpDown4, pn.YAxis.Scale.Min, (double)numUpDown4, pn.YAxis.Scale.Max);
                Vline2.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                Vline2.Line.Color = Color.WhiteSmoke;
                Vline2.Line.Width = 2.0f;
                pn.GraphObjList.Add(Vline2);
            };


            if (cb_2graph.Checked && cb_H2line1.Checked)
            {
                LineObj H2line1 = new LineObj(pn2.XAxis.Scale.Min, (double)numUpDown5, pn2.XAxis.Scale.Max, (double)numUpDown5);
                H2line1.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                H2line1.Line.Color = Color.WhiteSmoke;
                H2line1.Line.Width = 2.0f;
                pn2.GraphObjList.Add(H2line1);
            };
            if (cb_2graph.Checked && cb_H2line2.Checked)
            {
                LineObj H2line2 = new LineObj(pn2.XAxis.Scale.Min, (double)numUpDown6, pn2.XAxis.Scale.Max, (double)numUpDown6);
                H2line2.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                H2line2.Line.Color = Color.WhiteSmoke;
                H2line2.Line.Width = 2.0f;
                pn2.GraphObjList.Add(H2line2);
            };

            if (cb_2graph.Checked && cb_V2line1.Checked)
            {
                LineObj V2line1 = new LineObj((double)numUpDown7, pn2.YAxis.Scale.Min, (double)numUpDown7, pn2.YAxis.Scale.Max);
                V2line1.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                V2line1.Line.Color = Color.WhiteSmoke;
                V2line1.Line.Width = 2.0f;
                pn2.GraphObjList.Add(V2line1);
            };
            if (cb_2graph.Checked && cb_V2line2.Checked)
            {
                LineObj V2line2 = new LineObj((double)numUpDown8, pn2.YAxis.Scale.Min, (double)numUpDown8, pn2.YAxis.Scale.Max);
                V2line2.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                V2line2.Line.Color = Color.WhiteSmoke;
                V2line2.Line.Width = 2.0f;
                pn2.GraphObjList.Add(V2line2);
            };

                zgc.Invalidate();// Обновляет график

                var curTime = DateTime.Now;
                if (curTime != toothPreviousDate) // проверяет не равно ли текущее время предыдущему , если нет разницы между текущим  и предыдущим времени значит не будет итервала времени
                {
                    var span = curTime - toothPreviousDate; // вычисляет интервал времени или как часто/быстро обновляется график
                    toothPreviousDate = curTime; // заменяет предыдущее время на текущее,необхолимо для вычислени интервала времени
                    seconds = $@"Инт. обн: {span.Seconds}.{span.Milliseconds} s";
                    location = new Point(pictureBox55.Location.X - label1.Size.Width - 10, 5);

                }


                frm1.tooth_enGrUpd = false; // на график выведен масив buff и можно его заного заполнять (масив buff незаполнен)
            
        }


        #endregion

        #region Изменение размеров формы и перерисовка графика
        public void Spl_Tooth_Resize(object sender, EventArgs e)
        {
            if (!cb_2graph.Checked)
            {
                ZGct.Location = new Point(0, udRng.Location.Y + udRng.Size.Height + 10);
                ZGct.Size = new Size(ClientRectangle.Width, ClientRectangle.Height - (udRng.Location.Y + udRng.Size.Height + 10));
                ZGct.Refresh();
            }
            if (cb_2graph.Checked)
            {
                ZGct.Location = new Point(0, udRng.Location.Y + udRng.Size.Height + 25);
                ZGct.Size = new Size(ClientRectangle.Width, ClientRectangle.Height - (udRng.Location.Y + udRng.Size.Height + 25));
                ZGct.Refresh();
            }
        }



        #endregion

        private void Tooth_F9_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
      
            Save_IniConfig_Tooth(); // сохроняет в ini фаил настройки формы Tooth_f9
            frm1.f9run = false; // 
  //          frm1.f9del = true;
      //                 frm1.frm9 = null;
            frm1.frm9.Hide();
  
        }
        public void Tooth_F9_Form_NeededToStop()
        {
            

            Save_IniConfig_Tooth(); // сохроняет в ini фаил настройки формы Tooth_f9
            frm1.frm9.Hide();
            frm1.f9run = false; // 
                                //          frm1.f9del = true;
            frm1.frm9 = null;
        }
        private void Tooth_F9_FormClosed(object sender, FormClosedEventArgs e)
        {
            frm1.frm9 = null;           
        }

        private PointPairList ToothGraph_sp2(int i, int SelectedIndex)
        {
            var pll = new PointPairList();
            double a = 0;
            switch (SelectedIndex)
            {
                #region  mbox0 -----------------------------------------

                case 1:  // Ia1              
                    a = (short)(buff[i, 1] * 256 + buff[i, 2]) * 0.1;
                    break;
                case 2:  // Ia2
                    a = (short)(buff[i, 3] * 256 + buff[i, 4]) * 0.1;
                    break;
                case 3:  // Ib1
                    a = (short)(buff[i, 5] * 256 + buff[i, 6]) * 0.1;
                    break;
                case 4:  //Ib2
                    a = (short)(buff[i, 7] * 256 + (buff[i, 8])) * 0.1;
                    break;

                #endregion

                #region  mbox1 -----------------------------------------

                case 5:  // Ic1
                    a = (short)(buff[i, 9] * 256 + (buff[i, 10])) * 0.1;
                    break;
                case 6: // Ic2
                    a = (short)(buff[i, 11] * 256 + (buff[i, 12])) * 0.1;
                    break;
                case 7: // Udc 
                    a = (short)(buff[i, 13] * 256 + (buff[i, 14])) * 0.1;
                    break;
                case 8: // Ir1 
                    a = (short)(buff[i, 15] * 256 + (buff[i, 16])) * 0.1;
                    break;

                #endregion

                #region  mbox2 -----------------------------------------

                case 9: // Ir2 
                    a = (short)(buff[i, 17] * 256 + (buff[i, 18])) * 0.1;
                    break;
                case 10: // isrlast
                    a = ((buff[i, 19] * 256) + (buff[i, 20]) - frm1.adcOffsets[9]) * 0.6878;
                    break;
                case 11: // IsrTick 
                    a = ((buff[i, 21] * 256) + (buff[i, 22]) - frm1.adcOffsets[0]) * 0.6878;
                    break;
                case 12: // Tcount
                    a = ((buff[i, 23] * 256) + (buff[i, 24])) /*- frm1.adcOffsets[0]) * 0.6878*/;
                    break;
                default:
                    break;

                #endregion
            }
            pll.Add(i, a);
            return pll;
        }

        private PointPairList ToothGraph_spf(int i, int SelectedIndex)
        {
            var pll = new PointPairList();
            double a = 0;
            if (SelectedIndex < 13)
            {

                switch (SelectedIndex)
                {
                    #region  mbox0 -----------------------------------------

                    case 1:  // Ia1              
                        a = (short)(buff[i, 1] * 256 + buff[i, 2]);
                        break;
                    case 2:  // Ia2
                        a = (short)(buff[i, 3] * 256 + buff[i, 4]);
                        break;
                    case 3:  // Ib1
                        a = (short)(buff[i, 5] * 256 + buff[i, 6]);
                        break;
                    case 4:  //Ib2
                        a = (short)(buff[i, 7] * 256 + (buff[i, 8]));
                        break;

                    #endregion

                    #region  mbox1 -----------------------------------------

                    case 5:  // Ic1
                        a = (short)(buff[i, 9] * 256 + (buff[i, 10]));
                        break;
                    case 6: // Ic2
                        a = (short)(buff[i, 11] * 256 + (buff[i, 12]));
                        break;
                    case 7: // Udc 
                        a = (short)(buff[i, 13] * 256 + (buff[i, 14]));
                        break;
                    case 8: // Ir1 
                        a = (short)(buff[i, 15] * 256 + (buff[i, 16]));
                        break;

                    #endregion

                    #region  mbox2 -----------------------------------------

                    case 9: // Ir2 
                        a = (short)(buff[i, 17] * 256 + (buff[i, 18]));
                        break;
                    case 10: // isrlast
                        a = (buff[i, 19] * 256) + (buff[i, 20]);
                        break;
                    case 11: // IsrTick 
                        a = (buff[i, 21] * 256) + (buff[i, 22]);
                        break;
                    case 12: // Tcount
                        a = (buff[i, 23] * 256) + (buff[i, 24]);
                        break;
                    default:
                        break;

                        #endregion

                }
            }
            else
            {
                a = (buff[i,(SelectedIndex * 2 - 1)] * 256) + (buff[i, SelectedIndex * 2]);
            }
            pll.Add(i, a);
            return pll;
        }

        private void Load_IniConfig_Tooth()
        {
            #region Считывание сохранёные настройки с Tooth_f9_config.ini фаила для ComboBox,CheckBox,Colors,NumericUpDown
            if (frm1.sp2_Tooth_F9 == true)
            {
                config = Tooth_sp2_f9_config;
            }
            else if (frm1.spf_Tooth_F9 == true)
            {
                config = FastKadr_spf_f9_config;
            }
            #region ComboboxText
                // Считывает сохранёную колекцию для ComboBox
           
                if (config.KeyExists("comboBoxText", "ComboBox"))
                {
                    comboBox1.Items.Clear(); comboBox2.Items.Clear(); comboBox3.Items.Clear(); comboBox4.Items.Clear();
                    comboBox5.Items.Clear(); comboBox6.Items.Clear(); comboBox7.Items.Clear(); comboBox8.Items.Clear();
                    comboBox1.Items.AddRange(config.ReadINI("ComboBox", "comboBoxText").Replace(" ", string.Empty).Split(','));
                    comboBox2.Items.AddRange(config.ReadINI("ComboBox", "comboBoxText").Replace(" ", string.Empty).Split(','));
                    comboBox3.Items.AddRange(config.ReadINI("ComboBox", "comboBoxText").Replace(" ", string.Empty).Split(','));
                    comboBox4.Items.AddRange(config.ReadINI("ComboBox", "comboBoxText").Replace(" ", string.Empty).Split(','));
                    comboBox5.Items.AddRange(config.ReadINI("ComboBox", "comboBoxText").Replace(" ", string.Empty).Split(','));
                    comboBox6.Items.AddRange(config.ReadINI("ComboBox", "comboBoxText").Replace(" ", string.Empty).Split(','));
                    comboBox7.Items.AddRange(config.ReadINI("ComboBox", "comboBoxText").Replace(" ", string.Empty).Split(','));
                    comboBox8.Items.AddRange(config.ReadINI("ComboBox", "comboBoxText").Replace(" ", string.Empty).Split(','));
                }
            
                #endregion

            #region CheckBox
                // Считывает сохранёные настройки его состояния (true/False) для чекбоксов 
                if (config.KeyExists("CheckBox_cbc1", "CheckBox"))
            {
                cbc1.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_cbc1").Replace(" ", string.Empty));
            }

            if (config.KeyExists("CheckBox_cbc2", "CheckBox"))
            {
                cbc2.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_cbc2").Replace(" ", string.Empty));
            }

            if (config.KeyExists("CheckBox_cbc3", "CheckBox"))
            {
                cbc3.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_cbc3").Replace(" ", string.Empty));
            }

            if (config.KeyExists("CheckBox_cbc4", "CheckBox"))
            {
                cbc4.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_cbc4").Replace(" ", string.Empty));
            }

            if (config.KeyExists("CheckBox_cbc5", "CheckBox"))
            {
                cbc5.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_cbc5").Replace(" ", string.Empty));
            }

            if (config.KeyExists("CheckBox_cbc6", "CheckBox"))
            {
                cbc6.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_cbc6").Replace(" ", string.Empty));
            }
            if (config.KeyExists("CheckBox_cbc7", "CheckBox"))
            {
                cbc7.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_cbc7").Replace(" ", string.Empty));
            }
            if (config.KeyExists("CheckBox_cbc8", "CheckBox"))
            {
                cbc8.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_cbc8").Replace(" ", string.Empty));
            }

            if (config.KeyExists("CheckBox_chBox_GrUpdEn", "CheckBox"))
            {
                chBox_GrUpdEn.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_chBox_GrUpdEn").Replace(" ", string.Empty));
            }

            if (config.KeyExists("CheckBox_cb_tm", "CheckBox"))
            {
                cb_tm.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_cb_tm").Replace(" ", string.Empty));
            }
            if (config.KeyExists("CheckBox_cbXY", "CheckBox"))
            {
                cbXY.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_cbXY").Replace(" ", string.Empty));
            }

            if (config.KeyExists("CheckBox_сетка", "CheckBox"))
            {
                cb_сетка.Checked = bool.Parse(config.ReadINI("CheckBox", "CheckBox_сетка").Replace(" ", string.Empty));
            }
            #endregion

            #region Colors
            // Считывает сохранёные настройки цвета для чекбокса а с чекбокса берутса цвет для линии графика (цвет чекбокса = цвету линии грфика)
            if (config.KeyExists("cbc1_color", "Colors"))
            {
                string[] cbc_color1 = (config.ReadINI("Colors", "cbc1_color").Replace("Color [A=255, R=", string.Empty).Replace("G=", string.Empty).Replace("B=", string.Empty).Replace("]", string.Empty).Replace(" ", string.Empty).Split(','));
                cbc1.BackColor = Color.FromArgb(Convert.ToInt16(cbc_color1[0]), Convert.ToInt16(cbc_color1[1]), Convert.ToInt16(cbc_color1[2]));
            }

            if (config.KeyExists("cbc2_color", "Colors"))
            {
                string[] cbc_color2 = (config.ReadINI("Colors", "cbc2_color").Replace("Color [A=255, R=", string.Empty).Replace("G=", string.Empty).Replace("B=", string.Empty).Replace("]", string.Empty).Replace(" ", string.Empty).Split(','));
                cbc2.BackColor = Color.FromArgb(Convert.ToInt16(cbc_color2[0]), Convert.ToInt16(cbc_color2[1]), Convert.ToInt16(cbc_color2[2]));
            }

            if (config.KeyExists("cbc3_color", "Colors"))
            {
                string[] cbc_color3 = (config.ReadINI("Colors", "cbc3_color").Replace("Color [A=255, R=", string.Empty).Replace("G=", string.Empty).Replace("B=", string.Empty).Replace("]", string.Empty).Replace(" ", string.Empty).Split(','));
                cbc3.BackColor = Color.FromArgb(Convert.ToInt16(cbc_color3[0]), Convert.ToInt16(cbc_color3[1]), Convert.ToInt16(cbc_color3[2]));
            }

            if (config.KeyExists("cbc4_color", "Colors"))
            {
                string[] cbc_color4 = (config.ReadINI("Colors", "cbc4_color").Replace("Color [A=255, R=", string.Empty).Replace("G=", string.Empty).Replace("B=", string.Empty).Replace("]", string.Empty).Replace(" ", string.Empty).Split(','));
                cbc4.BackColor = Color.FromArgb(Convert.ToInt16(cbc_color4[0]), Convert.ToInt16(cbc_color4[1]), Convert.ToInt16(cbc_color4[2]));
            }
            #endregion

            #region  ComboBoxIndex

                // Считывание сохранёные индех для ComboBox
                if (config.KeyExists("ComboBoxIndex1", "ComboBox"))
                {
                    int cbIndex1 = int.Parse(config.ReadINI("comboBox", "ComboBoxIndex1"));
                    if (comboBox1.Items.Count > cbIndex1)
                    {
                        comboBox1.SelectedIndex = cbIndex1;
                    }
                    else
                    {
                        comboBox1.SelectedIndex = 0;
                    }
                }

                if (config.KeyExists("ComboBoxIndex2", "ComboBox"))
                {
                    int cbIndex2 = int.Parse(config.ReadINI("comboBox", "ComboBoxIndex2"));
                    if (comboBox2.Items.Count > cbIndex2)
                    {
                        comboBox2.SelectedIndex = cbIndex2;
                    }
                    else
                    {
                        comboBox2.SelectedIndex = 0;
                    }
                }

                if (config.KeyExists("ComboBoxIndex3", "ComboBox"))
                {
                    int cbIndex3 = int.Parse(config.ReadINI("comboBox", "ComboBoxIndex3"));
                    if (comboBox3.Items.Count > cbIndex3)
                    {
                        comboBox3.SelectedIndex = cbIndex3;
                    }
                    else
                    {
                        comboBox3.SelectedIndex = 0;
                    }
                }

                if (config.KeyExists("ComboBoxIndex4", "ComboBox"))
                {
                    int cbIndex4 = int.Parse(config.ReadINI("comboBox", "ComboBoxIndex4"));
                    if (comboBox4.Items.Count > cbIndex4)
                    {
                        comboBox4.SelectedIndex = cbIndex4;
                    }
                    else
                    {
                        comboBox4.SelectedIndex = 0;
                    }
                }

                if (config.KeyExists("ComboBoxIndex5", "ComboBox"))
                {
                    int cbIndex5 = int.Parse(config.ReadINI("comboBox", "ComboBoxIndex5"));
                    if (comboBox5.Items.Count > cbIndex5)
                    {
                        comboBox5.SelectedIndex = cbIndex5;
                    }
                    else
                    {
                        comboBox5.SelectedIndex = 0;
                    }
                }

                if (config.KeyExists("ComboBoxIndex6", "ComboBox"))
                {
                    int cbIndex6 = int.Parse(config.ReadINI("comboBox", "ComboBoxIndex6"));
                    if (comboBox6.Items.Count > cbIndex6)
                    {
                        comboBox6.SelectedIndex = cbIndex6;
                    }
                    else
                    {
                        comboBox6.SelectedIndex = 0;
                    }
                }

                if (config.KeyExists("ComboBoxIndex7", "ComboBox"))
                {
                    int cbIndex7 = int.Parse(config.ReadINI("comboBox", "ComboBoxIndex7"));
                    if (comboBox7.Items.Count > cbIndex7)
                    {
                        comboBox7.SelectedIndex = cbIndex7;
                    }
                    else
                    {
                        comboBox7.SelectedIndex = 0;
                    }
                }

                if (config.KeyExists("ComboBoxIndex8", "ComboBox"))
                {
                    int cbIndex8 = int.Parse(config.ReadINI("comboBox", "ComboBoxIndex8"));
                    if (comboBox8.Items.Count > cbIndex8)
                    {
                        comboBox8.SelectedIndex = cbIndex8;
                    }
                    else
                    {
                        comboBox8.SelectedIndex = 0;
                    }
                }

            #endregion

            #region NumericUpDown

            // Считывает сохранёное значение для NumericUpDown
            if (config.KeyExists("NumericUpDown_udLvl", "NumericUpDown"))
            {
                udLvl.Value = int.Parse(config.ReadINI("NumericUpDown", "NumericUpDown_udLvl").Replace(" ", string.Empty));
            }

            if (config.KeyExists("NumericUpDown_udRng", "NumericUpDown"))
            {
                udRng.Value = int.Parse(config.ReadINI("NumericUpDown", "NumericUpDown_udRng").Replace(" ", string.Empty));
            }

            if (config.KeyExists("NumericUpDown_udCh", "NumericUpDown"))
            {
                udCh.Value = int.Parse(config.ReadINI("NumericUpDown", "NumericUpDown_udCh").Replace(" ", string.Empty));
            }

            #endregion

            #endregion

        }

        private void Save_IniConfig_Tooth()
        {
            #region Запись настроек ComboBox,CheckBox,NumericUpDown,Colors в Tooth_f9_config.ini фаил 
            if (frm1.sp2_Tooth_F9 == true)
            {
                config = Tooth_sp2_f9_config;
            }
            else if (frm1.spf_Tooth_F9 == true)
            {
                config = FastKadr_spf_f9_config;
            }

            // Сохраняет настройки последнего состояния контролов

            config.Write("ComboBox", "ComboBoxIndex1", comboBox1.SelectedIndex.ToString());
            config.Write("ComboBox", "ComboBoxIndex2", comboBox2.SelectedIndex.ToString());
            config.Write("ComboBox", "ComboBoxIndex3", comboBox3.SelectedIndex.ToString());
            config.Write("ComboBox", "ComboBoxIndex4", comboBox4.SelectedIndex.ToString());
            config.Write("ComboBox", "ComboBoxIndex5", comboBox5.SelectedIndex.ToString());
            config.Write("ComboBox", "ComboBoxIndex6", comboBox6.SelectedIndex.ToString());
            config.Write("ComboBox", "ComboBoxIndex7", comboBox7.SelectedIndex.ToString());
            config.Write("ComboBox", "ComboBoxIndex8", comboBox8.SelectedIndex.ToString());


            config.Write("CheckBox", "CheckBox_cbc1", cbc1.Checked.ToString());
            config.Write("CheckBox", "CheckBox_cbc2", cbc2.Checked.ToString());
            config.Write("CheckBox", "CheckBox_cbc3", cbc3.Checked.ToString());
            config.Write("CheckBox", "CheckBox_cbc4", cbc4.Checked.ToString());
            config.Write("CheckBox", "CheckBox_cbc5", cbc5.Checked.ToString());
            config.Write("CheckBox", "CheckBox_cbc6", cbc6.Checked.ToString());
            config.Write("CheckBox", "CheckBox_cbc7", cbc7.Checked.ToString());
            config.Write("CheckBox", "CheckBox_cbc8", cbc8.Checked.ToString());

            config.Write("CheckBox", "CheckBox_cbXY", cbXY.Checked.ToString());
            config.Write("CheckBox", "CheckBox_сетка", cb_сетка.Checked.ToString());
            config.Write("CheckBox", "CheckBox_chBox_GrUpdEn", chBox_GrUpdEn.Checked.ToString());
            config.Write("CheckBox", "CheckBox_cb_tm", cb_tm.Checked.ToString());

            config.Write("NumericUpDown", "NumericUpDown_udLvl", udLvl.Value.ToString());
            config.Write("NumericUpDown", "NumericUpDown_udRng", udRng.Value.ToString());
            config.Write("NumericUpDown", "NumericUpDown_udCh", udCh.Value.ToString());

            config.Write("Colors", "cbc1_color", cbc1.BackColor.ToString());
            config.Write("Colors", "cbc2_color", cbc2.BackColor.ToString());
            config.Write("Colors", "cbc3_color", cbc3.BackColor.ToString());
            config.Write("Colors", "cbc4_color", cbc4.BackColor.ToString());

            #endregion

        }

        private void Visible_XY()
        {
            // делает видимой / не видимой оси XY

            ZGct.GraphPane.XAxis.IsVisible =
                      ZGct.GraphPane.YAxis.IsVisible = cbXY.Checked; // делает видимым  оси X и Y                
            pn2.XAxis.IsVisible =
                     pn2.YAxis.IsVisible = cbXY.Checked;

            ZGct.Refresh();
        }

        private void Visible_сетка()
        {
            // делает видимой / не видимой сетку графика

            ZGct.GraphPane.XAxis.MajorGrid.IsVisible = cb_сетка.Checked;
            ZGct.GraphPane.XAxis.MajorGrid.DashOn = 1;
            ZGct.GraphPane.XAxis.MajorGrid.DashOff = 10;
            ZGct.GraphPane.YAxis.MajorGrid.IsVisible = cb_сетка.Checked;
            ZGct.GraphPane.YAxis.MajorGrid.DashOn = 1;
            ZGct.GraphPane.YAxis.MajorGrid.DashOff = 10;
            ZGct.GraphPane.XAxis.MajorGrid.Color = Color.White;
            ZGct.GraphPane.YAxis.MajorGrid.Color = Color.White;

            pn2.XAxis.MajorGrid.IsVisible = cb_сетка.Checked;
            pn2.XAxis.MajorGrid.DashOn = 1;
            pn2.XAxis.MajorGrid.DashOff = 10;
            pn2.YAxis.MajorGrid.IsVisible = cb_сетка.Checked;
            pn2.YAxis.MajorGrid.DashOn = 1;
            pn2.YAxis.MajorGrid.DashOff = 10;
            pn2.XAxis.MajorGrid.Color = Color.White;
            pn2.YAxis.MajorGrid.Color = Color.White;


        }

        private void hints()
        {
            // Всплывающие подсказки
            tooltip = new ToolTip();
            tooltip.SetToolTip(cbXY, "Делает видимой / невидимо оси XY");
            tooltip.SetToolTip(cb_сетка, "делает видимой / невидимо сетку");
            tooltip.SetToolTip(cb_tm, "Определяет будет ли окно всегда поверх всех других окон");
            tooltip.SetToolTip(chBox_GrUpdEn, "Останавливает график");
            tooltip.SetToolTip(button1, "Посылает команду в контролер '102'");
            tooltip.SetToolTip(cbc1, "Разрешает / запрещает отображение линии график");
            tooltip.SetToolTip(cbc2, "Разрешает / запрещает отображение линии график");
            tooltip.SetToolTip(cbc3, "Разрешает / запрещает отображение линии график");
            tooltip.SetToolTip(cbc4, "Разрешает / запрещает отображение линии график");
            tooltip.SetToolTip(udLvl, "Меняет номер зуба с которого будет начинатся отображение график");
            tooltip.SetToolTip(udCh, "??? номер канала ???");
            tooltip.SetToolTip(udRng, "???");
            tooltip.SetToolTip(cb_2graph, "Делит окно на два графика");
            tooltip.SetToolTip(cb_Hline1, "Добавляет первую горизонтальную линию (1H)");
            tooltip.SetToolTip(cb_Hline2, "Добавляет вторую горизонтальную линию (2H)");
            tooltip.SetToolTip(cb_Vline1, "Добавляет первую вертикальную линию (1V)");
            tooltip.SetToolTip(cb_Vline2, "Добавляет вторую вертикальную линию (2V)");
            tooltip.SetToolTip(cb_H2line1, "Добавляет первую горизонтальную линию для второго графика (1H)");
            tooltip.SetToolTip(cb_H2line2, "Добавляет вторую горизонтальную линию для второго графика (2H)");
            tooltip.SetToolTip(cb_V2line1, "Добавляет первую вертикальную линию для второго графика (1V)");
            tooltip.SetToolTip(cb_V2line2, "Добавляет вторую вертикальную линию для второго графика (2V)");
            tooltip.SetToolTip(numericUpDown1, "Двигает первую горизонтальную линию (1H)");
            tooltip.SetToolTip(numericUpDown2, "Двигает вторую горизонтальную линию (2H)");
            tooltip.SetToolTip(numericUpDown3, "Двигает первую вертикальную линию (1V)");
            tooltip.SetToolTip(numericUpDown4, "Двигает вторую вертикальную линию (2V)");
            tooltip.SetToolTip(numericUpDown5, "Двигает первую горизонтальную линию на втором графике (1H)");
            tooltip.SetToolTip(numericUpDown6, "Двигает вторую горизонтальную линию на втором графике (2H)");
            tooltip.SetToolTip(numericUpDown7, "Двигает первую вертикальную линию на втором графике (1V)");
            tooltip.SetToolTip(numericUpDown8, "Двигает вторую вертикальную линию на втором графике (2V)");
            tooltip.SetToolTip(cb_autoScale, "Включает / Выключает,Авто изменение шкалы Y");
            tooltip.SetToolTip(cb_autoScale2, "Включает / Выключает,Авто изменение шкалы Y для второго графика");

            if (frm1.sp2_Tooth_F9 == true)
            {
                tooltip.SetToolTip(Num_kadr, "Количество зубьев");
            }
            else if (frm1.spf_Tooth_F9 == true)
            {
                tooltip.SetToolTip(Num_kadr, "Количество кадров");
            }
            
        }

        private void cb_2graph_CheckedChanged(object sender, EventArgs e)
        {
            if (!cb_2graph.Checked)
            {
                cbc5.Visible = cbc6.Visible = cbc7.Visible = cbc8.Visible = false;
                comboBox5.Visible = comboBox6.Visible = comboBox7.Visible = comboBox8.Visible = false;
                cb_H2line1.Visible = cb_H2line2.Visible = cb_V2line1.Visible = cb_V2line2.Visible = cb_autoScale2.Visible = false;
                numericUpDown5.Visible = numericUpDown6.Visible = numericUpDown7.Visible = numericUpDown8.Visible = false;

                ZGct.Location = new Point(0, udRng.Location.Y + udRng.Size.Height + 10);
                ZGct.Size = new Size(ClientRectangle.Width, ClientRectangle.Height - (udRng.Location.Y + udRng.Size.Height + 10));
                ZGct.Refresh();
            }
            if (cb_2graph.Checked)
            {
                cbc5.Visible = cbc6.Visible = cbc7.Visible = cbc8.Visible = true;
                comboBox5.Visible = comboBox6.Visible = comboBox7.Visible = comboBox8.Visible = true;
                cb_H2line1.Visible = cb_H2line2.Visible = cb_V2line1.Visible = cb_V2line2.Visible = cb_autoScale2.Visible = true;
                numericUpDown5.Visible = numericUpDown6.Visible = numericUpDown7.Visible = numericUpDown8.Visible = true;

                ZGct.Location = new Point(0, udRng.Location.Y + udRng.Size.Height + 25);
                ZGct.Size = new Size(ClientRectangle.Width, ClientRectangle.Height - (udRng.Location.Y + udRng.Size.Height + 25));
                ZGct.Refresh();
            }

            if (!cb_2graph.Checked)
            {

                if (ZGct.MasterPane.PaneList.Count == 2)
                {
                    ZGct.MasterPane.PaneList.RemoveAt(0); // удаляет из списка графиков первый график чтобы небыло больше одного графика
                }

                InitGraph(ZGct, tooths); // нужно чтобы пересоздать оформления графика

                ZGct.MasterPane.ReSize(graph);
                ZGct.Refresh();
            }

            if (cb_2graph.Checked)
            {
                using (Graphics g = CreateGraphics())
                {
                    ZGct.MasterPane.SetLayout(g, PaneLayout.SingleColumn); // Графики будут размещены в один столбец друг под другом
                    InitGraph(ZGct, tooths);  // нужно чтобы пересоздать оформления графиков
                }
                ZGct.Refresh();


            }
            Visible_XY(); // делает видимой / не видимой оси XY
            Visible_сетка(); // делает видимой / не видимой сетку графика
        }

        #region numericUpDown_ValueChanged
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            numUpDown1 = numericUpDown1.Value;
        }
        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            numUpDown2 = numericUpDown2.Value;
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            numUpDown3 = numericUpDown3.Value;
        }

        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            numUpDown4 = numericUpDown4.Value;
        }

        private void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            numUpDown5 = numericUpDown5.Value;
        }

        private void numericUpDown6_ValueChanged(object sender, EventArgs e)
        {
            numUpDown6 = numericUpDown6.Value;
        }

        private void numericUpDown7_ValueChanged(object sender, EventArgs e)
        {
            numUpDown7 = numericUpDown7.Value;
        }

        private void numericUpDown8_ValueChanged(object sender, EventArgs e)
        {
            numUpDown8 = numericUpDown8.Value;
        }

        private void cb_Vline1_CheckedChanged(object sender, EventArgs e)
        {
            numUpDown3 = numericUpDown3.Value;
            //          CreateGraph(ZGct);
        }

        private void cb_Vline2_CheckedChanged(object sender, EventArgs e)
        {
            numUpDown4 = numericUpDown4.Value;
        }

        private void cb_V2line1_CheckedChanged(object sender, EventArgs e)
        {
            numUpDown7 = numericUpDown7.Value;
        }

        private void cb_V2line2_CheckedChanged(object sender, EventArgs e)
        {
            numUpDown8 = numericUpDown8.Value;
       }
        #endregion

        private void line_cb()
        {
            if (cb_Hline1.Checked & cb_Hline2.Checked)
            {
                numericUpDown1.Value = (decimal)(pn.YAxis.Scale.Min);
                numericUpDown2.Value = (decimal)(pn.YAxis.Scale.Min);

                numericUpDown1.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2;
                numericUpDown2.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2;

                numericUpDown1.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2;
                numericUpDown2.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2;

                numericUpDown1.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2 - 2;
                numericUpDown2.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2 + 2;

            }
            else if (cb_Hline1.Checked & !cb_Hline2.Checked)
            {
                numericUpDown1.Value = (decimal)(pn.YAxis.Scale.Min);
                numericUpDown1.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2;
                numericUpDown1.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2;
                numericUpDown1.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2;
            }
            if (cb_Hline2.Checked & !cb_Hline1.Checked)
            {
                numericUpDown2.Value = (decimal)(pn.YAxis.Scale.Min);
                numericUpDown2.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2;
                numericUpDown2.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2;
                numericUpDown2.Value = (decimal)(pn.YAxis.Scale.Min + pn.YAxis.Scale.Max) / 2;
            }

        }

        private void line_cb2()
        {
            if (cb_H2line1.Checked & cb_H2line2.Checked)
            {
                numericUpDown5.Value = (decimal)(pn2.YAxis.Scale.Min);
                numericUpDown6.Value = (decimal)(pn2.YAxis.Scale.Min);

                numericUpDown5.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2;
                numericUpDown6.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2;

                numericUpDown5.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2;
                numericUpDown6.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2;

                numericUpDown5.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2 - 2;
                numericUpDown6.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2 + 2;

            }
            else if (cb_H2line1.Checked & !cb_H2line2.Checked)
            {
                numericUpDown5.Value = (decimal)(pn2.YAxis.Scale.Min);
                numericUpDown5.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2;
                numericUpDown5.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2;
                numericUpDown5.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2;
            }
            if (cb_H2line2.Checked & !cb_H2line1.Checked)
            {
                numericUpDown6.Value = (decimal)(pn2.YAxis.Scale.Min);
                numericUpDown6.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2;
                numericUpDown6.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2;
                numericUpDown6.Value = (decimal)(pn2.YAxis.Scale.Min + pn2.YAxis.Scale.Max) / 2;
            }

        }

    }
}


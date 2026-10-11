using System;
using System.Drawing;
using System.Windows.Forms;
using ZedGraph;

namespace ICDIBasic
{
    /*
     * Окно «Графики» - самописец на три кривые.
     *
     * Досталось от моторного приложения и до 11.10.2026 предлагало на выбор
     * токи фаз и скольжения двух двигателей - величины, которых в однофазном
     * мосте нет: их заполняли кадры 101 и 102, а прибор их больше не шлёт.
     * Теперь рисует то, что приходит кадром 140 и что показывает главное окно.
     *
     * ВЕЛИЧИНЫ БЕРЁТ САМО, по своему таймеру, из тех же полей ccs, откуда их
     * читает главное окно (их заполняет Parse140, Oleg/Kadr60.cs). Раньше их
     * сюда перекладывал главный таймер через три свойства и таблицу в
     * MainWindow.cs; список имён при этом жил в дизайнере тремя копиями, а
     * таблица значений - в другом файле, и сходились они только по номеру
     * строки. Теперь таблица одна - Channels ниже.
     *
     * У КАЖДОЙ КРИВОЙ СВОЯ ОСЬ, того же цвета: первая слева, вторая справа,
     * третья - вторая слева. На общей оси ток в десять ампер рядом с мощностью
     * в две тысячи ватт лежал бы в нуле.
     *
     * Свежее кадра 140 значения не бывают: он идёт раз в 50 мс. Ставить таймер
     * окна чаще бессмысленно - точки повторятся.
     */
    public partial class Form3 : Form
    {
        #region Величины

        /// <summary>Одна величина, которую можно положить на график.</summary>
        private sealed class Chan
        {
            public readonly string Name;
            public readonly string Unit;

            /// <summary>Знаков после запятой в поле текущего значения - как в главном окне.</summary>
            public readonly int Digits;

            public readonly Func<double> Get;

            /// <summary>
            /// Одно из слов 4..7 кадра 140: что в нём лежит, решает переключатель
            /// «код / мгн / rms» главного окна (ccs.teleSel).
            /// </summary>
            public readonly bool Tele;

            public Chan(string name, string unit, int digits, Func<double> get, bool tele = false)
            {
                Name = name;
                Unit = unit;
                Digits = digits;
                Get = get;
                Tele = tele;
            }
        }

        /*
         * Имена короткие, какими эти величины зовут в прошивке и в главном
         * окне. В записи (bin/Wscope/Kadr60.ini) часть из них названа длиннее -
         * pSec, iSecTel, - там это плата за общий на все шаблоны размах Wscope,
         * здесь её платить не за что. Порядок на работу не влияет: наборы по
         * кнопкам выбираются по имени (Choose).
         */
        private static readonly Chan[] Channels =
        {
            new Chan("iPri",    "А",   1, () => ccs.iPri, true), // ток первички
            new Chan("iSec",    "А",   1, () => ccs.iSec, true), // ток вторички = ток в сеть
            new Chan("u220",    "В",   1, () => ccs.u220, true), // сеть после контактора
            new Chan("uSec",    "В",   1, () => ccs.uSec, true), // выход моста, до контактора
            new Chan("uBat",    "В",   1, () => ccs.UdcBat),     // вход моста, АКБ
            new Chan("Iset",    "А",   2, () => ccs.gtIset),     // уставка после рампы
            new Chan("IsetRef", "А",   2, () => ccs.gtIsetRef),  // уставка с движка Iзад
            new Chan("freq",    "Гц",  2, () => ccs.u220freq),   // частота сети
            new Chan("P",       "Вт",  0, () => ccs.gtP),
            new Chan("Q",       "вар", 0, () => ccs.gtQ),
            new Chan("S",       "В·А", 0, () => ccs.gtS),
            new Chan("PF",      "",    4, () => ccs.gtPF),       // коэффициент мощности P/S
            new Chan("iPri_av", "А",   2, () => ccs.iPriAv),     // постоянка в первичке
            new Chan("uDCcomp", "В",   3, () => ccs.uDCcomp),    // чем её давит мост
            new Chan("mPeak",   "",    3, () => ccs.gtMPeak),    // пик |m| за период сети
            new Chan("satLast", "",    0, () => ccs.gtSatLast),  // тактов в упоре по m
            new Chan("coreT",   "°C",  0, () => ccs.core_temp),  // температура кристалла
            new Chan("state",   "",    0, () => ccs.gtState),    // автомат: 0 OFF .. 4 FAULT
        };

        /// <summary>Подпись оси: имя и единица, для слов 4..7 - ещё и набор.</summary>
        private static string Title(Chan c)
        {
            if (!c.Tele) return c.Unit.Length == 0 ? c.Name : c.Name + ", " + c.Unit;

            switch (ccs.teleSel)
            {
                case Form1.TeleKod: return c.Name + ", код АЦП";
                case Form1.TeleIns: return c.Name + " мгн, " + c.Unit;
                default:            return c.Name + " rms, " + c.Unit;
            }
        }

        /// <summary>Коды АЦП - целые, дробной части у них нет.</summary>
        private static int DigitsOf(Chan c)
        {
            return c.Tele && ccs.teleSel == Form1.TeleKod ? 0 : c.Digits;
        }

        /// <summary>
        /// Выбрать величины для кривых по именам - по одному имени на кривую.
        /// Неизвестное имя оставляет кривую без величины.
        /// </summary>
        public void Choose(params string[] names)
        {
            for (int i = 0; i < Curves && i < names.Length; i++)
            {
                string name = names[i];
                _combo[i].SelectedIndex = Array.FindIndex(Channels, c => c.Name == name);
            }
        }

        #endregion

        #region Конструкторы

        private const int Curves = 3;

        /// <summary>
        /// Предел поля текущего значения. NumericUpDown бросает исключение на
        /// числе вне своих границ, а прежние ±1500 мощность моста перекрывает.
        /// На саму кривую предел не действует.
        /// </summary>
        private const double ValueLimit = 100000;

        private static readonly Color[] CurveColor = { Color.Red, Color.Yellow, Color.Purple };

        /* Подписи осей стоят на серебристом поле, и жёлтый на нём не читается.
         * Кривая остаётся жёлтой - она на чёрном, - а ось берёт тёмный тон того
         * же цвета. */
        private static readonly Color[] AxisColor = { Color.Red, Color.DarkGoldenrod, Color.Purple };

        private readonly CheckBox[] _box;
        private readonly ComboBox[] _combo;
        private readonly NumericUpDown[] _nud;
        private readonly Axis[] _axis = new Axis[Curves];

        /// <summary>Набор слов 4..7, под который подписаны оси. Сменился - переподписать.</summary>
        private int _teleShown = -1;

        public Form1 Frm3Form1;  // адресc на объект типа Form1

        bool _stpStr;
        #endregion

        public Form3()
        {
            for (int i = 0; i < Curves; i++)
                _data[i] = new RollingPointPairList(_capacity);

            InitializeComponent();

            Text = "Графики";

            _box = new[] { checkBox1, checkBox2, checkBox3 };
            _combo = new[] { comboBox1, comboBox2, comboBox3 };
            _nud = new[] { numericUpDown1, numericUpDown2, numericUpDown3 };

            foreach (NumericUpDown n in _nud)
            {
                n.Minimum = (decimal)-ValueLimit;
                n.Maximum = (decimal)ValueLimit;
            }

            PrepareGraph();

            for (int i = 0; i < Curves; i++)
            {
                int k = i;

                // Только из списка: набранное руками имя величиной не станет.
                _combo[i].DropDownStyle = ComboBoxStyle.DropDownList;

                foreach (Chan c in Channels) _combo[i].Items.Add(c.Name);

                _combo[i].SelectedIndexChanged += (s, e) => ChannelChanged(k);
            }

            numericUpDown4.Value = timer1.Interval;
            numericUpDown5.Value = (decimal)_step;

            // Всплывающие подсказки
            ToolTip t = new ToolTip();
            t.SetToolTip(numericUpDown4, " Скорость обновления графика в миллисекундах (Мин = 15.6 мс)");//15.6 мс - самое быстрое, что может таймер (ограничение из-за винды)
            t.SetToolTip(numericUpDown5, " Шаг, с которым будет обновляться график ");
        }


        #region PrepareGraph
        public void PrepareGraph()
        {
            // Получим панель для рисования
            GraphPane pane = zedGraph2.GraphPane;

            // Очистим список кривых на тот случай, если до этого сигналы уже были нарисованы
            pane.CurveList.Clear();

            // Закрасим фон всего компонента ZedGraph
            // Заливка будет сплошная
            pane.Fill.Type = FillType.Solid;
            pane.Fill.Color = Color.Silver;

            // Закрасим область графика (его фон) в черный цвет
            pane.Chart.Fill.Type = FillType.Solid;
            pane.Chart.Fill.Color = Color.Black;

            // Включим показ оси на уровне X = 0, чтобы видеть цвет осей
            pane.XAxis.MajorGrid.IsZeroLine = true;
            // Установим цвет осей
            pane.XAxis.Color = Color.Gray;

            // Включим сетку
            pane.XAxis.MajorGrid.IsVisible = true;
            // Установим цвет для сетки
            pane.XAxis.MajorGrid.Color = Color.White;

            // Три оси Y, по одной на кривую: левая, правая и вторая левая.
            // Вторую левую панель сама не заводит.
            if (pane.YAxisList.Count < 2) pane.AddYAxis("");

            _axis[0] = pane.YAxis;
            _axis[1] = pane.Y2Axis;
            _axis[2] = pane.YAxisList[1];

            for (int i = 0; i < Curves; i++)
            {
                // Добавим кривую пока еще без каких-либо точек
                LineItem curve = pane.AddCurve("", _data[i], CurveColor[i], SymbolType.None);

                // толщина линии графика
                curve.Line.Width = 2.0F;

                if (i == 1) curve.IsY2Axis = true;
                if (i == 2) curve.YAxisIndex = 1;

                Axis a = _axis[i];

                a.Color = Color.Gray;
                a.Title.FontSpec.FontColor = AxisColor[i];
                a.Scale.FontSpec.FontColor = AxisColor[i];

                // Риски только на своей стороне: чужие на противоположном краю
                // читались бы как деления соседней оси.
                a.MajorTic.IsOpposite = false;
                a.MinorTic.IsOpposite = false;

                // Без множителя «10^3» в подписи: ватты должны читаться ваттами.
                a.Scale.MagAuto = false;

                a.MajorGrid.Color = Color.White;
            }

            // название заголовков
            pane.Title.Text = "Графики";
            pane.XAxis.Title.Text = "время [сек]";

            // Включаем отображение сетки напротив мелких рисок по оси X
            pane.XAxis.MinorGrid.IsVisible = true;

            // Устанавливает цвет для сетки напротив мелких рисок по оси X
            pane.XAxis.MinorGrid.Color = Color.White;

            // Меняем количество мелких рисок  между крупными рисками
            pane.XAxis.Scale.MinorStep = 10.0 * _step;

            // Меняем длину пропусков/пробелов в сетке (пикселя - пропуск)
            pane.XAxis.MinorGrid.DashOff = 35;

            ShowAxes();

            // Вызываем метод AxisChange (), чтобы обновить данные об осях.
            zedGraph2.AxisChange();

            // Обновляем график
            zedGraph2.Invalidate();
        }

        /// <summary>
        /// Ось видна, пока включена её кривая. Горизонтальная сетка - одна, от
        /// первой видимой оси: три сетки с разным шагом закрыли бы сами кривые.
        /// </summary>
        private void ShowAxes()
        {
            bool grid = false;

            for (int i = 0; i < Curves; i++)
            {
                bool on = _box != null && _box[i].Checked;

                _axis[i].IsVisible = on;
                _axis[i].MajorGrid.IsVisible = on && !grid;
                _axis[i].MajorGrid.IsZeroLine = on && !grid;

                if (on) grid = true;
            }
        }

        /// <summary>
        /// Сменили величину кривой. Старые точки убираем: под новым именем и в
        /// новом масштабе они читались бы как её собственная история.
        /// </summary>
        private void ChannelChanged(int i)
        {
            _data[i].Clear();
            SetTitle(i);
        }

        private void SetTitle(int i)
        {
            int sel = _combo[i].SelectedIndex;

            _axis[i].Title.Text = sel < 0 ? "" : Title(Channels[sel]);
        }
        #endregion

        #region Основные Параметры
        int _capacity = 500; // Длина массива _data

        // Текущее значение на графике
        double _currentx = 0; // Первая точка масива/графика по оси х
        double _step = 0.1; //шаг с которым будет менятся точки масива/графика
        /// <summary>
        /// Здесь храним данные
        /// </summary>
        private readonly RollingPointPairList[] _data = new RollingPointPairList[Curves];
        #endregion

        #region Timer+GraphRefresh functions
        private void timer1_Tick(object sender, EventArgs e)
        {
            _currentx += _step; // Значени по оси х (время)
            // Рассчитаем интервал по оси X, который нужно отобразить на графике
            double xmin = _currentx - _capacity * _step;
            double xmax = _currentx;

            // Переключатель «код / мгн / rms» в главном окне: у слов 4..7
            // сменился смысл, подписи осей обязаны сказать это сразу.
            if (_teleShown != ccs.teleSel)
            {
                _teleShown = ccs.teleSel;
                for (int i = 0; i < Curves; i++) SetTitle(i);
            }

            ShowAxes();

            for (int i = 0; i < Curves; i++)
            {
                if (!_box[i].Checked) continue;

                int sel = _combo[i].SelectedIndex;
                if (sel < 0) continue;

                Chan c = Channels[sel];
                double v = c.Get();

                if (double.IsNaN(v) || double.IsInfinity(v)) v = 0;

                _nud[i].DecimalPlaces = DigitsOf(c);
                _nud[i].Value = (decimal)Math.Max(-ValueLimit, Math.Min(ValueLimit, v));

                _data[i].Add(_currentx, v);
            }

            GraphPane pane = zedGraph2.GraphPane;
            pane.XAxis.Scale.Min = xmin;
            pane.XAxis.Scale.Max = xmax;


            if (!_stpStr)   // Если не нажата кнопка паузы
            {
                // Обновим оси
                zedGraph2.AxisChange();

                // Обновим сам график
                zedGraph2.Invalidate();
            }
        }
        #endregion

        /* Здесь стоял обработчик масштабирования, зажимавший ось Y в 0...1200 -
         * под обороты и токи двигателя. Реактивная мощность, постоянка в
         * первичке и её поправка бывают отрицательными, и он срезал бы их при
         * первом же движении мышью. Вернуть автомасштаб после своего - правая
         * кнопка мыши на графике. */

        #region Кнопка фиксации
        private void button1_Click(object sender, EventArgs e)
        {
            // Останавливает текущее изображение графика / Останавливает обновление изображения графика  ( но сам график продолжает записоватся в массив при нажатие продолжить он его тутже выведет на экран )
            if (!_stpStr)
            { _stpStr = true; button1.BackColor = Color.LightGreen; button1.Text = "Продолжить"; }
            else
            { _stpStr = false; button1.BackColor = Color.Khaki; button1.Text = "Зафиксировать"; }
        }
        #endregion

        #region closingoptions
        private void Form3_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            Frm3Form1.inv1.U = 10; // dbg
            Frm3Form1.FlagGrphOpen = false;
            e.Cancel = true; // Cancel closing
        }
        #endregion



        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            // Скорость обновления графика в миллисекундах. 15.6 мс - самое быстрое, что может таймер (ограничение из-за винды)
            timer1.Interval = (int)numericUpDown4.Value;
        }

        public void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            //Шаг, с которым будет обновляться график
            _step = Convert.ToDouble(numericUpDown5.Value);
            zedGraph2.GraphPane.XAxis.Scale.MinorStep = 10.0 * _step;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // Очищает график и запускает с нулувой точки
            _currentx = 0;
            for (int i = 0; i < Curves; i++) _data[i].Clear();
        }

    }
}

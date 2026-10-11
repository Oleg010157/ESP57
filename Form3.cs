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

            /// <summary>
            /// Наименьший размах оси, в единицах величины. Уже него масштаб не
            /// сжимается - см. FitAxis.
            /// </summary>
            public readonly double MinSpan;

            public readonly Func<double> Get;

            /// <summary>
            /// Одно из слов 4..7 кадра 140: что в нём лежит, решает переключатель
            /// «код / мгн / rms» главного окна (ccs.teleSel).
            /// </summary>
            public readonly bool Tele;

            /// <summary>
            /// Бывает отрицательной. У остальных ось не уходит ниже нуля: ток
            /// 0.3 А на шкале от -0.7 читался бы как знакопеременный.
            /// </summary>
            public readonly bool Signed;

            public Chan(string name, string unit, int digits, double minSpan, Func<double> get,
                        bool tele = false, bool signed = false)
            {
                Name = name;
                Unit = unit;
                Digits = digits;
                MinSpan = minSpan;
                Get = get;
                Tele = tele;
                Signed = signed;
            }
        }

        /*
         * Имена короткие, какими эти величины зовут в прошивке и в главном
         * окне. В записи (bin/Wscope/Kadr60.ini) часть из них названа длиннее -
         * pSec, iSecTel, - там это плата за общий на все шаблоны размах Wscope,
         * здесь её платить не за что. Порядок на работу не влияет: наборы по
         * кнопкам выбираются по имени (Choose).
         *
         * Четвёртое число - наименьший размах оси. Подобран так, чтобы шаг
         * квантования слова в кадре (0.1 А, 0.1 В, 1 Вт) занимал на поле
         * проценты высоты, а не всю её; менять можно свободно, на данные он
         * не действует.
         */
        private static readonly Chan[] Channels =
        {
            new Chan("iPri",    "А",   1, 5,   () => ccs.iPri, true), // ток первички
            new Chan("iSec",    "А",   1, 2,   () => ccs.iSec, true), // ток вторички = ток в сеть
            new Chan("u220",    "В",   1, 20,  () => ccs.u220, true), // сеть после контактора
            new Chan("uSec",    "В",   1, 20,  () => ccs.uSec, true), // выход моста, до контактора
            new Chan("uBat",    "В",   1, 5,   () => ccs.UdcBat),     // вход моста, АКБ
            new Chan("Iset",    "А",   2, 2,   () => ccs.gtIset),     // уставка после рампы
            new Chan("IsetRef", "А",   2, 2,   () => ccs.gtIsetRef),  // уставка с движка Iзад
            new Chan("freq",    "Гц",  2, 1,   () => ccs.u220freq),   // частота сети
            new Chan("P",       "Вт",  0, 200, () => ccs.gtP, signed: true),
            new Chan("Q",       "вар", 0, 200, () => ccs.gtQ, signed: true),
            new Chan("S",       "В·А", 0, 200, () => ccs.gtS),
            new Chan("PF",      "",    4, 0.2, () => ccs.gtPF, signed: true),     // коэффициент мощности P/S
            new Chan("iPri_av", "А",   2, 0.5, () => ccs.iPriAv, signed: true),   // постоянка в первичке
            new Chan("uDCcomp", "В",   3, 0.5, () => ccs.uDCcomp, signed: true),  // чем её давит мост
            new Chan("mPeak",   "",    3, 0.2, () => ccs.gtMPeak),    // пик |m| за период сети
            new Chan("satLast", "",    0, 20,  () => ccs.gtSatLast),  // тактов в упоре по m
            new Chan("coreT",   "°C",  0, 10,  () => ccs.core_temp, signed: true), // температура кристалла
            new Chan("state",   "",    0, 5,   () => ccs.gtState),    // автомат: 0 OFF .. 4 FAULT
        };

        /// <summary>Размах в кодах АЦП, когда слова 4..7 переключены на коды.</summary>
        private const double KodMinSpan = 200;

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

        private static readonly Color[] CurveColor = { Color.Red, Color.Yellow, Color.Purple };

        /* Подписи осей стоят на серебристом поле, и жёлтый на нём не читается.
         * Кривая остаётся жёлтой - она на чёрном, - а ось берёт тёмный тон того
         * же цвета. */
        private static readonly Color[] AxisColor = { Color.Red, Color.DarkGoldenrod, Color.Purple };

        private readonly CheckBox[] _box;
        private readonly ComboBox[] _combo;

        /* Текущие значения - обычные поля только для чтения. Раньше здесь
         * стояли NumericUpDown: элемент для ВВОДА числа, со стрелками, которые
         * меняли показание на десятую долю секунды, до следующего отсчёта, и
         * ни на что не влияли. */
        private readonly TextBox[] _val;

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
            _val = new[] { value1, value2, value3 };

            // Поле только для чтения по умолчанию серое и на сером окне
            // теряется; показание должно читаться так же, как в главном окне.
            foreach (TextBox b in _val) b.BackColor = SystemColors.Window;

            PrepareGraph();

            zedGraph2.ZoomEvent += OnZoom;

            for (int i = 0; i < Curves; i++)
            {
                int k = i;

                // Только из списка: набранное руками имя величиной не станет.
                _combo[i].DropDownStyle = ComboBoxStyle.DropDownList;

                foreach (Chan c in Channels) _combo[i].Items.Add(c.Name);

                _combo[i].SelectedIndexChanged += (s, e) => ChannelChanged(k);
            }

            numericUpDown4.Value = timer1.Interval;
            PeriodChanged();

            // Всплывающие подсказки
            ToolTip t = new ToolTip();
            t.SetToolTip(numericUpDown4,
                "Как часто на график ложится новая точка, мс.\n" +
                "На графике " + _capacity + " точек, поэтому период задаёт и ширину окна:\n" +
                "100 мс - 50 секунд, 600 мс - 5 минут.\n" +
                "Кадр от прибора приходит раз в 50 мс - чаще ставить незачем.");
        }

        /// <summary>Ширина окна по времени при нынешнем периоде, секунды.</summary>
        private double SpanSec
        {
            get { return _capacity * timer1.Interval / 1000.0; }
        }

        /// <summary>Период сменился: подпись ширины окна и шаг мелкой сетки.</summary>
        private void PeriodChanged()
        {
            int sec = (int)Math.Round(SpanSec);

            labelSpan.Text = "Окно: " +
                (sec < 120 ? sec + " с"
                           : sec % 60 == 0 ? sec / 60 + " мин"
                                           : sec / 60 + " мин " + sec % 60 + " с");

            // Мелкая сетка по времени - пятьдесят делений на окно, как и было
            zedGraph2.GraphPane.XAxis.Scale.MinorStep = SpanSec / 50;
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
            pane.XAxis.Scale.MinorStep = SpanSec / 50;

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

        /*
         * ВРЕМЯ НА ОСИ - НАСТОЯЩЕЕ, по часам, а не по счёту тиков.
         *
         * Раньше к оси на каждом тике прибавлялся «шаг» из отдельного поля, и
         * секунды на ней были верны, только пока шаг вручную держали равным
         * периоду таймера: поставь период 50 мс при шаге 0.10 - и график шёл
         * вдвое быстрее времени. Поле шага убрано. Заодно ушла и вторая
         * неправда: таймер окна тикает не точно, сто миллисекунд у него бывают
         * ста девятью, и за минуту ось по счёту тиков отставала на секунды.
         *
         * Пускается с первой точкой, а не с программой: иначе ось начиналась
         * бы с того, сколько программа уже работает.
         */
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// Здесь храним данные
        /// </summary>
        private readonly RollingPointPairList[] _data = new RollingPointPairList[Curves];
        #endregion

        #region Timer+GraphRefresh functions
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!_clock.IsRunning) _clock.Start();

            _currentx = _clock.Elapsed.TotalSeconds; // Значени по оси х (время)
            // Рассчитаем интервал по оси X, который нужно отобразить на графике
            double xmin = _currentx - SpanSec;
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

                // Текст меняем, только когда он другой: иначе поле мерцает
                string text = v.ToString("F" + DigitsOf(c));
                if (_val[i].Text != text) _val[i].Text = text;

                _data[i].Add(_currentx, v);
            }

            GraphPane pane = zedGraph2.GraphPane;
            pane.XAxis.Scale.Min = xmin;
            pane.XAxis.Scale.Max = xmax;


            if (!_stpStr)   // Если не нажата кнопка паузы
            {
                // Масштаб, выставленный мышью, не трогаем - см. OnZoom
                if (!_userZoom)
                    for (int i = 0; i < Curves; i++)
                        if (_box[i].Checked) FitAxis(i);

                // Обновим оси
                zedGraph2.AxisChange();

                // Обновим сам график
                zedGraph2.Invalidate();
            }
        }

        /*
         * СВОЙ МАСШТАБ ОСИ Y ВМЕСТО АВТОМАСШТАБА ZedGraph.
         *
         * Автомасштаб растягивает на всю высоту то, что есть, а у ровного
         * сигнала есть только шаг квантования. 11.10.2026, сетевой режим:
         * uSec стоит на 219.4...219.5 В - один шаг слова в 0.1 В, - P гуляет
         * на три ватта из 1407, и поле заполнено частоколом от края до края.
         * Выглядит как авария, а это ровный режим.
         *
         * Поэтому размах оси не бывает меньше Chan.MinSpan. Пока сигнал
         * меняется сильнее - ось идёт за ним с запасом в десятую часть, как и
         * раньше; стоит ровно - остаётся линией посреди поля.
         *
         * Границы округляются до шага сетки: иначе они ползли бы на каждом
         * тике вслед за минимумом и максимумом, и подписи оси мелькали бы.
         */
        private void FitAxis(int i)
        {
            int sel = _combo[i].SelectedIndex;
            RollingPointPairList d = _data[i];

            if (sel < 0 || d.Count == 0) return;

            Chan c = Channels[sel];

            double lo = double.MaxValue;
            double hi = double.MinValue;

            for (int k = 0; k < d.Count; k++)
            {
                double y = d[k].Y;

                if (y < lo) lo = y;
                if (y > hi) hi = y;
            }

            bool kod = c.Tele && ccs.teleSel == Form1.TeleKod;

            // Слова 4..7 в мгновенных и в кодах знакопеременны, в rms - нет
            bool signed = c.Signed || (c.Tele && ccs.teleSel != Form1.TeleRms);

            double need = Math.Max((hi - lo) * 1.1, kod ? KodMinSpan : c.MinSpan);
            double mid = (hi + lo) / 2;

            double min = mid - need / 2;
            double max = mid + need / 2;

            if (!signed && lo >= 0 && min < 0)
            {
                min = 0;
                max = need;
            }

            double q = Nice(need / 4);

            min = Math.Floor(min / q) * q;
            max = Math.Ceiling(max / q) * q;

            Scale s = _axis[i].Scale;

            if (s.Min != min) s.Min = min;
            if (s.Max != max) s.Max = max;
        }

        /// <summary>Ближайшее сверху «круглое» число: 1, 2 или 5 на степень десяти.</summary>
        private static double Nice(double x)
        {
            double p = Math.Pow(10, Math.Floor(Math.Log10(x)));
            double m = x / p;

            return (m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10) * p;
        }

        /// <summary>Оператор сам выставил масштаб мышью - свой не навязываем.</summary>
        private bool _userZoom;

        /*
         * Любое действие мышью над масштабом. Различить нужно два случая, и
         * ZedGraph различает их сам: после рамки, колеса и сдвига у оси стоит
         * ручной масштаб, после «Set Scale to Default» из меню правой кнопки -
         * автоматический. Второе и есть просьба вернуть всё как было.
         *
         * Раньше здесь стоял обработчик, зажимавший ось Y в 0...1200 - под
         * обороты и токи двигателя. Реактивная мощность, постоянка в первичке
         * и её поправка бывают отрицательными, и он срезал бы их при первом же
         * движении мышью.
         */
        private void OnZoom(ZedGraphControl sender, ZoomState oldState, ZoomState newState)
        {
            Scale s = _axis[0].Scale;

            _userZoom = !(s.MinAuto && s.MaxAuto);
        }
        #endregion

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
            // Период обновления графика в миллисекундах. Точки, набранные с
            // прежним периодом, остаются на своих местах: время на оси идёт
            // по часам, а не по счёту точек.
            timer1.Interval = (int)numericUpDown4.Value;
            PeriodChanged();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // Очищает график и запускает с нулувой точки. Заодно возвращает
            // свой масштаб осей, если его меняли мышью.
            _userZoom = false;
            _currentx = 0;
            _clock.Reset();
            for (int i = 0; i < Curves; i++) _data[i].Clear();
        }

    }
}

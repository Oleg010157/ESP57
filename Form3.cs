using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
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
         * Четвёртое число - наименьший размах оси; менять можно свободно, на
         * данные он не действует.
         *
         * ПРАВИЛО ПОДБОРА: ШАГ КВАНТОВАНИЯ - НЕ БОЛЬШЕ ПРОЦЕНТА ВЫСОТЫ, то
         * есть размах не меньше ста шагов слова в кадре. Слово в 0.01 А
         * требует 1 А, в 0.1 В - 10 В, в 1 Вт - 100 Вт. У счётных величин
         * (satLast, state) правило не действует: там единица и есть событие.
         *
         * Токи iPri и iSec до прошивки от 11.10.2026 ехали по 0.1 А, и на оси
         * в 2 А ток, дрожащий между 5.2 и 5.3, рисовался полосой в двадцатую
         * часть поля. Лечилось это не размахом, а ценой слова: теперь она
         * 0.01 А, и прежние 2 А правилу удовлетворяют с запасом.
         */
        private static readonly Chan[] Channels =
        {
            new Chan("iPri",    "А",   2, 5,   () => ccs.iPri, true), // ток первички
            new Chan("iSec",    "А",   2, 2,   () => ccs.iSec, true), // ток вторички = ток в сеть
            new Chan("u220",    "В",   1, 20,  () => ccs.u220, true), // сеть после контактора
            new Chan("uSec",    "В",   1, 20,  () => ccs.uSec, true), // выход моста, до контактора
            new Chan("uBat",    "В",   1, 10,  () => ccs.UdcBat),     // вход моста, АКБ
            new Chan("Iset",    "А",   2, 2,   () => ccs.gtIset),     // уставка после рампы; размах как у
            new Chan("IsetRef", "А",   2, 2,   () => ccs.gtIsetRef),  // iSec, чтобы ампер выглядел одинаково
            new Chan("freq",    "Гц",  2, 1,   () => ccs.u220freq),   // частота сети
            new Chan("P",       "Вт",  0, 200, () => ccs.gtP, signed: true),
            new Chan("Q",       "вар", 0, 200, () => ccs.gtQ, signed: true),
            new Chan("S",       "В·А", 0, 200, () => ccs.gtS),
            new Chan("PF",      "",    4, 0.2, () => ccs.gtPF, signed: true),     // коэффициент мощности P/S
            new Chan("iPri_av", "А",   2, 1,   () => ccs.iPriAv, signed: true),   // постоянка в первичке
            new Chan("uDCcomp", "В",   3, 0.5, () => ccs.uDCcomp, signed: true),  // чем её давит мост
            new Chan("mPeak",   "",    3, 0.2, () => ccs.gtMPeak),    // пик |m| за период сети
            new Chan("satLast", "",    0, 100, () => ccs.gtSatLast),  // тактов в упоре по m, из ста
            new Chan("coreT",   "°C",  0, 100, () => ccs.core_temp, signed: true), // температура кристалла
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

        /// <summary>Поля границ оси, см. регион «Границы осей».</summary>
        private readonly TextBox[] _min;
        private readonly TextBox[] _max;

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
            _min = new[] { min1, min2, min3 };
            _max = new[] { max1, max2, max3 };

            // Поле только для чтения по умолчанию серое и на сером окне
            // теряется; показание должно читаться так же, как в главном окне.
            foreach (TextBox b in _val) b.BackColor = SystemColors.Window;

            LoadBounds();
            PrepareGraph();

            zedGraph2.ZoomEvent += OnZoom;

            // Всплывающие подсказки
            ToolTip t = new ToolTip();

            for (int i = 0; i < Curves; i++)
            {
                int k = i;

                // Только из списка: набранное руками имя величиной не станет.
                _combo[i].DropDownStyle = ComboBoxStyle.DropDownList;

                foreach (Chan c in Channels) _combo[i].Items.Add(c.Name);

                _combo[i].SelectedIndexChanged += (s, e) => ChannelChanged(k);

                foreach (TextBox b in new[] { _min[i], _max[i] })
                {
                    b.Leave += (s, e) => BoundEdited(k);
                    b.KeyDown += (s, e) =>
                    {
                        if (e.KeyCode != Keys.Enter) return;

                        e.SuppressKeyPress = true; // без системного «динь»
                        BoundEdited(k);
                    };
                }

                t.SetToolTip(_min[i],
                    "Нижняя граница оси этой величины.\n" +
                    "Пусто - подбирается сама. Ввод - Enter.");
                t.SetToolTip(_max[i],
                    "Верхняя граница оси этой величины.\n" +
                    "Пусто - подбирается сама. Ввод - Enter.");
            }

            numericUpDown4.Value = timer1.Interval;
            PeriodChanged();

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
            ShowBounds(i);
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

            // Границы, заданные оператором, старше подобранных. Каждая сама
            // по себе: заданная одна - вторая остаётся автоматической.
            double[] b;

            if (_bounds.TryGetValue(c.Name, out b))
            {
                bool hasLo = !double.IsNaN(b[0]);
                bool hasHi = !double.IsNaN(b[1]);

                if (hasLo) min = b[0];
                if (hasHi) max = b[1];

                // Задана одна граница, и сигнал целиком по ту сторону от неё:
                // автоматическая вторая оказалась бы «внутри». Отступаем от
                // заданной на наименьший размах - ось обязана быть осью.
                if (max <= min)
                {
                    if (hasLo) max = min + need;
                    else       min = max - need;
                }
            }

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

        #region Границы осей

        /*
         * ГРАНИЦЫ ОСИ РУКАМИ - поля «Мин» и «Макс» в строке кривой, по образцу
         * окна настройки осциллографа (Scope/ScopeConfigForm.cs).
         *
         * Пустое поле - граница подбирается сама (FitAxis). Отдельной галочки
         * «авто» нет по той же причине, что и там: пустая клетка и так
         * читается как «не задано».
         *
         * ЗАЧЕМ, если есть мышь. Рамка мышью растягивает ВСЕ ТРИ ОСИ РАЗОМ:
         * она задаёт прямоугольник на экране, а каждая ось пересчитывает его
         * в свои единицы. Растянуть одну кривую, не тронув две другие, ею
         * нельзя. Поля дают каждой величине свою шкалу.
         *
         * ГРАНИЦЫ ПРИНАДЛЕЖАТ ВЕЛИЧИНЕ, А НЕ СТРОКЕ. Набранные для тока 0...10
         * не должны достаться мощности, когда в той же строке сменят величину,
         * и должны вернуться, когда ток выберут снова - в любой строке.
         *
         * Хранятся между запусками рядом с настройками вида осциллографа, в
         * bin/Debug/Scope/Graphs.view.ini. Файл пользовательский и в git не
         * ездит, как и те.
         */

        /// <summary>Границы по имени величины: [0] - мин, [1] - макс, NaN - не задана.</summary>
        private readonly Dictionary<string, double[]> _bounds = new Dictionary<string, double[]>();

        private static string BoundsFile
        {
            get { return Path.Combine(ScopeView.Dir, "Graphs.view.ini"); }
        }

        private static string BoundText(double v)
        {
            return double.IsNaN(v) ? "" : v.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Разобрать поле. Пустое - «не задано», и это не ошибка. Запятая и
        /// точка годятся обе: набирают как привычнее.
        /// </summary>
        private static bool TryBound(string text, out double v)
        {
            v = double.NaN;

            string s = text.Trim().Replace(',', '.');
            if (s.Length == 0) return true;

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                && !double.IsInfinity(v)) return true;

            v = double.NaN;
            return false;
        }

        /// <summary>Показать в строке границы выбранной в ней величины.</summary>
        private void ShowBounds(int i)
        {
            int sel = _combo[i].SelectedIndex;
            double[] b = null;

            if (sel >= 0) _bounds.TryGetValue(Channels[sel].Name, out b);

            _min[i].Text = b == null ? "" : BoundText(b[0]);
            _max[i].Text = b == null ? "" : BoundText(b[1]);

            _min[i].BackColor = _max[i].BackColor = SystemColors.Window;
        }

        /// <summary>
        /// Оператор закончил ввод в одном из двух полей строки (Enter или уход
        /// из поля). Негодное не принимаем и не стираем: поле краснеет, ось
        /// остаётся прежней, исправить можно на месте.
        /// </summary>
        private void BoundEdited(int i)
        {
            int sel = _combo[i].SelectedIndex;
            if (sel < 0) return;

            double lo, hi;

            bool okLo = TryBound(_min[i].Text, out lo);
            bool okHi = TryBound(_max[i].Text, out hi);

            // Обе заданы - низ обязан быть ниже верха. С NaN сравнение ложно,
            // то есть одна заданная граница порядок не нарушает.
            bool order = !(lo >= hi);

            _min[i].BackColor = okLo && order ? SystemColors.Window : Color.Salmon;
            _max[i].BackColor = okHi && order ? SystemColors.Window : Color.Salmon;

            if (!okLo || !okHi || !order) return;

            string name = Channels[sel].Name;
            double[] was;

            // Ничего не изменилось - это просто уход из поля, файл не трогаем.
            // Сравниваем текстом: NaN самому себе не равен.
            if (_bounds.TryGetValue(name, out was)
                    ? BoundText(was[0]) == BoundText(lo) && BoundText(was[1]) == BoundText(hi)
                    : double.IsNaN(lo) && double.IsNaN(hi))
                return;

            if (double.IsNaN(lo) && double.IsNaN(hi)) _bounds.Remove(name);
            else _bounds[name] = new[] { lo, hi };

            SaveBounds();

            // Набранное в поле старше масштаба, оставленного мышью
            _userZoom = false;

            // Та же величина может стоять и в другой строке
            for (int j = 0; j < Curves; j++)
                if (j != i && _combo[j].SelectedIndex == sel) ShowBounds(j);
        }

        private void LoadBounds()
        {
            try
            {
                if (!File.Exists(BoundsFile)) return;

                foreach (string raw in File.ReadAllLines(BoundsFile, Encoding.UTF8))
                {
                    string s = raw.Trim();
                    if (s.Length == 0 || s[0] == ';') continue;

                    int eq = s.IndexOf('=');
                    if (eq <= 0) continue;

                    string name = s.Substring(0, eq).Trim();
                    string[] p = s.Substring(eq + 1).Split(',');

                    // Имя, которого в таблице уже нет, - от прошлой версии
                    if (p.Length != 2 || Array.FindIndex(Channels, c => c.Name == name) < 0) continue;

                    double lo, hi;
                    if (!TryBound(p[0], out lo) || !TryBound(p[1], out hi) || lo >= hi) continue;
                    if (double.IsNaN(lo) && double.IsNaN(hi)) continue;

                    _bounds[name] = new[] { lo, hi };
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void SaveBounds()
        {
            try
            {
                var sb = new StringBuilder();

                sb.AppendLine("; Границы осей окна «Графики»: <величина>=<мин>,<макс>.");
                sb.AppendLine("; Пустая граница подбирается сама. Пишет программа.");

                foreach (Chan c in Channels)
                {
                    double[] b;
                    if (_bounds.TryGetValue(c.Name, out b))
                        sb.AppendLine(c.Name + "=" + BoundText(b[0]) + "," + BoundText(b[1]));
                }

                Directory.CreateDirectory(ScopeView.Dir);
                File.WriteAllText(BoundsFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
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

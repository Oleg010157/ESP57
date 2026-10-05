using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Peak.Can.Basic;

namespace ICDIBasic
{
    /*
     * Окно осциллографа быстрой ленты.
     *
     * Перечень каналов НЕ ЗАШИТ: он читается из того же .ini, что и настройки
     * Wscope, по имени выбранного набора кадра. Поэтому окно работает по любому
     * быстрому кадру, а разойтись с Wscope в масштабах и знаковости физически не
     * может - таблица у них одна. Разбор - IniChannelMap.
     *
     * Кадр 103 приходит во ВТОРИЧНОМ потоке (CAN_RxData в ASt_MainF.cs), а окно
     * может быть и закрыто. Поэтому подключение сделано статическим методом Feed:
     * в Kadr60Rx стоит один его вызов, и когда окна нет, он стоит ровно одну
     * проверку ссылки на null.
     *
     * Осциллограф только НАБЛЮДАЕТ. Запись ленты в файл для Wscope живёт своей
     * жизнью (spl_Start/spl_Stop), и осциллограф не зависит от того, идёт она или
     * нет: ProcessFrame103 молчит, пока запись не запущена, а смотреть на форму
     * тока хочется и без неё.
     */
    internal sealed class ScopeForm : Form
    {
        /// <summary>
        /// Кольцо вместе с набором кадра, для которого прочитан шаблон.
        ///
        /// ОДНОЙ ССЫЛКОЙ, и это важно. Поток CAN читает и то, и другое; лежи они
        /// в двух полях, он мог бы на один кадр взять новый набор со старым
        /// кольцом и разобрать кадр чужой картой. Получились бы правдоподобные
        /// неверные числа - то самое, от чего вся эта затея и защищает.
        /// </summary>
        private sealed class Source
        {
            public Source(ScopeRing ring, string frame)
            {
                Ring = ring;
                Frame = frame;
            }

            public readonly ScopeRing Ring;
            public readonly string Frame;
        }

        /// <summary>Живое окно. volatile: ссылку пишет поток окна, читает поток CAN.</summary>
        private static volatile ScopeForm _live;

        private volatile Source _src;

        private ScopeControl _scope;
        private readonly Panel _host;
        private readonly Label _msg;

        private readonly ToolStripComboBox _window;
        private readonly ToolStripButton _btnSync;
        private readonly ToolStripButton _btnFreeze;
        private readonly ToolStripButton _btnCursor;
        private readonly ToolStripButton _btnReset;
        private readonly ToolStripButton _btnChannels;

        /// <summary>Карта текущего шаблона. Нужна окну настройки и пересборке панели.</summary>
        private IniChannelMap _map;

        /// <summary>Вторая панель: выбор канала запуска и кнопки каналов. Пересобирается на каждый шаблон.</summary>
        private readonly ToolStrip _chTool;

        /// <summary>Выбор канала запуска. Живёт один шаблон, как и кнопки каналов.</summary>
        private ToolStripComboBox _trig;

        private ToolStripButton _slope;
        private ToolStripTextBox _level;

        private readonly List<int> _trigCh = new List<int>();
        private readonly List<ToolStripButton> _chButtons = new List<ToolStripButton>();

        /// <summary>Канал каждой кнопки: кнопки идут по занятым слотам, а не по каналам.</summary>
        private readonly List<int> _btnCh = new List<int>();

        /// <summary>Признаки «канал пришёл», снятые с кольца под замком.</summary>
        private bool[] _liveCh = new bool[0];

        private readonly ToolStripStatusLabel _stFrame;
        private readonly ToolStripStatusLabel _stTick;
        private readonly ToolStripStatusLabel _stCount;
        private readonly ToolStripStatusLabel _stGaps;
        private readonly ToolStripStatusLabel _stHz;

        private readonly Timer _timer;

        /// <summary>Набор, для которого собрано окно. Пусто - шаблона нет.</summary>
        private string _frame;

        private int _liveShown = -1;

        /*-------------------------------------------------------------------
         * Подключение к данным
         *------------------------------------------------------------------*/

        /// <summary>
        /// Вызывается ИЗ ПОТОКА CAN на каждый кадр 103. Окна нет или шаблон не
        /// прочитан - выходим немедленно.
        /// </summary>
        public static void Feed(TPCANMsgFD msg)
        {
            ScopeForm f = _live;
            if (f == null || _broken) return;

            try
            {
                Source s = f._src;
                if (s == null) return;

                // Кадр 103 везёт несколько разных раскладок (F3_convertor,
                // F3_invertor, F1_*), имён внутри кадра нет, и различить их можно
                // только по тому, какой набор выбран в поле «F2 Кадр». Если его
                // переключили, а окно ещё не успело перечитать шаблон, разбирать
                // нечем: чужая карта дала бы правдоподобную чушь.
                if (Form1.f2kadrName != s.Frame) return;

                /* Кадр приходит извне, а читаем мы из него фиксированное число
                 * байт. Обычные пути дают ровно 64 (PCANBasic маршалит массив по
                 * SizeConst, PcanFdMessageConverter выделяет столько же), но
                 * проверка стоит дешевле разбирательства. */
                if (!s.Ring.Map.Fits(msg.DATA, msg.DLC)) return;

                s.Ring.Append(msg);
            }
            catch (Exception ex)
            {
                // ЗАЩИТА ЗАПИСИ, А НЕ ОСЦИЛЛОГРАФА.
                //
                // Мы стоим в Kadr60Rx ПЕРЕД ProcessFrame103, а весь вызов
                // CAN_RxData обёрнут глухим catch {} в CANA.cs. Значит исключение
                // отсюда не всплыло бы нигде, но унесло бы с собой разбор кадра и
                // запись ленты в файл - и повторялось бы на каждом кадре, то есть
                // лента встала бы совсем. Ради картинки на экране ломать запись
                // нельзя: она и есть результат опыта.
                //
                // Поэтому осциллограф выключается до закрытия окна - один раз, а
                // не глотает ошибку на каждом кадре, - и причина уходит в строку
                // состояния. Молча пропадать он не должен: пустое поле без
                // объяснения хуже, чем пустое поле с причиной.
                _broken = true;
                _brokenWhy = ex.GetType().Name + ": " + ex.Message;
            }
        }

        /// <summary>Осциллограф отключён из-за ошибки разбора. Запись при этом цела.</summary>
        private static volatile bool _broken;
        private static volatile string _brokenWhy;

        /// <summary>Открыть окно или поднять уже открытое.</summary>
        public static void Open(IWin32Window owner)
        {
            if (_live != null)
            {
                if (_live.WindowState == FormWindowState.Minimized)
                    _live.WindowState = FormWindowState.Normal;

                _live.Activate();
                return;
            }

            /* Новое окно - новая попытка: прошлое могло отключиться из-за кадра
             * старой прошивки или сбоя на шине, и держать осциллограф мёртвым до
             * перезапуска программы незачем. */
            _broken = false;
            _brokenWhy = null;

            var f = new ScopeForm();
            _live = f;
            f.Show(owner);
        }

        /*-------------------------------------------------------------------
         * Окно
         *------------------------------------------------------------------*/

        private ScopeForm()
        {
            _host = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 22, 27) };

            _msg = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(170, 180, 190),
                BackColor = Color.FromArgb(18, 22, 27),
            };
            _host.Controls.Add(_msg);

            _window = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 80,
                AutoSize = false,
            };
            _window.Items.AddRange(new object[] { "20 мс", "50 мс", "100 мс", "200 мс", "500 мс", "1 с" });
            _window.SelectedIndexChanged += WindowChanged;
            _window.SelectedIndex = 2;

            // Включена по умолчанию: без синхронизации бегущая синусоида уезжает
            // вбок, и смотреть на неё невозможно. Выключают её, когда как раз и
            // надо увидеть уход - например, расхождение частоты с сетью.
            _btnSync = new ToolStripButton("Синхр.")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                CheckOnClick = true,
                Checked = true,
                ToolTipText = "Пуск развёртки по переходу выбранного канала через ноль вверх",
            };
            _btnSync.CheckedChanged += (s, e) => { if (_scope != null) _scope.Sync = _btnSync.Checked; };

            _btnFreeze = new ToolStripButton("Заморозить")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                CheckOnClick = true,
            };
            _btnFreeze.CheckedChanged += (s, e) =>
            {
                if (_scope == null) return;

                _scope.Frozen = _btnFreeze.Checked;

                // Замораживают, чтобы что-то измерить, поэтому курсор включается
                // сам. Обратно не выключается: если оператор оставил его на
                // бегущей картинке, значит он ему там и нужен.
                if (_btnFreeze.Checked)
                {
                    _btnCursor.Checked = true;
                    _scope.Focus(); /* чтобы сразу работали стрелки */
                }
            };

            _btnCursor = new ToolStripButton("Курсор")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                CheckOnClick = true,
                ToolTipText = "Мышь ведёт курсор, щелчок ставит метку, пробел - тоже, Esc убирает. " +
                              "Стрелки двигают на отсчёт, с Shift - на двадцать пять",
            };
            _btnCursor.CheckedChanged += (s, e) =>
            {
                if (_scope == null) return;

                _scope.ShowCursor = _btnCursor.Checked;
                if (_btnCursor.Checked) _scope.Focus();
            };

            _btnReset = new ToolStripButton("Сбросить счётчики")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
            };
            _btnReset.Click += (s, e) =>
            {
                Source src = _src;
                if (src == null) return;

                src.Ring.Reset();
                if (_scope != null) _scope.Invalidate();
            };

            _btnChannels = new ToolStripButton("Каналы…")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Какие каналы показывать, в каких границах и каким цветом",
            };
            _btnChannels.Click += ChannelsClick;

            var tool = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Renderer = new ToolStripSystemRenderer(),
            };
            tool.Items.Add(new ToolStripLabel("Окно:"));
            tool.Items.Add(_window);
            tool.Items.Add(_btnSync);
            tool.Items.Add(_btnFreeze);
            tool.Items.Add(_btnCursor);
            tool.Items.Add(_btnReset);
            tool.Items.Add(new ToolStripSeparator());
            tool.Items.Add(_btnChannels);

            _chTool = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Renderer = new ToolStripSystemRenderer(),
            };

            _stFrame = NewStatus(260);
            _stTick = NewStatus(120);
            _stCount = NewStatus(140);
            _stGaps = NewStatus(230);
            _stHz = NewStatus(150);

            var status = new StatusStrip { SizingGrip = true };
            status.Items.AddRange(new ToolStripItem[] { _stFrame, _stTick, _stCount, _stGaps, _stHz });

            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 640);
            MinimumSize = new Size(720, 440);
            Text = "Осциллограф быстрой ленты";
            StartPosition = FormStartPosition.CenterParent;

            // Порядок добавления важен: закреплённый по краю элемент забирает
            // место у добавленных РАНЬШЕ, поэтому заполняющий идёт первым, а из
            // двух верхних панелей выше окажется добавленная позже.
            Controls.Add(_host);
            Controls.Add(_chTool);
            Controls.Add(tool);
            Controls.Add(status);

            Rebuild();

            /* 25 Гц на перерисовку - чаще, чем обновляется экран, показывать
             * некому. Строка состояния - раз в пять кадров: счётчики, меняющиеся
             * 25 раз в секунду, читать невозможно. */
            _timer = new Timer { Interval = 40 };
            _timer.Tick += Tick;
            _timer.Start();
        }

        private static ToolStripStatusLabel NewStatus(int width)
        {
            return new ToolStripStatusLabel
            {
                AutoSize = false,
                Width = width,
                TextAlign = ContentAlignment.MiddleLeft,
            };
        }

        /*-------------------------------------------------------------------
         * Пересборка под выбранный набор кадра
         *------------------------------------------------------------------*/

        /// <summary>
        /// Перечитать шаблон и собрать окно заново.
        ///
        /// Порядок важен: сначала гасим ссылку, по которой кормит поток CAN, и
        /// только потом разбираем и собираем. Иначе он мог бы попасть на
        /// полусобранное кольцо.
        /// </summary>
        private void Rebuild()
        {
            /* Вид уходящего шаблона сохраняем: переключение кадра не должно
             * стоить настройки, которую только что сделали. */
            SaveView();

            _src = null;

            if (_scope != null)
            {
                _host.Controls.Remove(_scope);
                _scope.Dispose();
                _scope = null;
            }

            ClearChannelTool();

            _map = null;
            _frame = Form1.f2kadrName;
            _liveShown = -1;

            string err;
            IniChannelMap map = IniChannelMap.Load(_frame, out err);

            if (map == null)
            {
                string nl = Environment.NewLine;

                /* Самый частый случай - лента ещё не пущена. Кадр при этом УЖЕ
                 * выбран в поле «F2 Кадр», и говорить «набор не выбран» неверно:
                 * f2kadrName присваивается только по F2, а до того пусто, хотя в
                 * списке стоит имя. Человек смотрит на выбранный кадр и читает,
                 * что кадр не выбран. */
                _msg.Text = string.IsNullOrEmpty(_frame)
                    ? "Лента не запущена." + nl + nl +
                      "Нажмите F2 в главном окне - набор берётся из поля «F2 Кадр»." + nl +
                      "Alt+F2 останавливает ленту." + nl + nl +
                      "Чтобы сменить кадр: Alt+F2, выбрать новый, затем F2."
                    : err + nl + nl +
                      "Шаблоны каналов лежат в " + IniChannelMap.Dir;

                _msg.Visible = true;

                Text = "Осциллограф быстрой ленты";
                return;
            }

            _msg.Visible = false;
            _map = map;
            _liveCh = new bool[map.Count];

            var ring = new ScopeRing(map);

            /* Вид: свой файл, иначе настройки Wscope, иначе первые три канала.
             * Подробности - в ScopeView. */
            _scope = new ScopeControl(ring, ScopeView.Load(map)) { Dock = DockStyle.Fill };
            _scope.Sync = _btnSync.Checked;
            _scope.ShowCursor = _btnCursor.Checked;
            _scope.Frozen = _btnFreeze.Checked;
            ApplyWindow();

            _host.Controls.Add(_scope);

            BuildChannelTool(map);

            Text = "Осциллограф быстрой ленты - " + map.Template;

            /* Публикуем последней строкой: с этого момента поток CAN начинает
             * кормить уже готовое кольцо. */
            _src = new Source(ring, _frame);
        }

        private void BuildChannelTool(IniChannelMap map)
        {
            /* Канал запуска выбирается, а не зашит: шаблон произвольный, и какой
             * из каналов есть напряжение сети, заранее неизвестно. По умолчанию
             * карта предлагает uGrid/U220/Va, если такое имя в ней нашлось. */
            _trig = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 110,
                AutoSize = false,
                ToolTipText = "Канал, по переходу которого через ноль вверх пускается развёртка",
            };

            /* Фронт и уровень. Спад нужен не для симметрии: у пилы вроде
             * tick256 резкое событие - это сброс с 255 в ноль, а не плавный
             * подъём, и цепляться разумнее за него. */
            _slope = new ToolStripButton("↑")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Фронт: ↑ по возрастанию, ↓ по спаду",
                AutoSize = false,
                Width = 26,
            };
            _slope.Click += SlopeClick;

            _level = new ToolStripTextBox
            {
                Width = 70,
                AutoSize = false,
                ToolTipText = "Уровень запуска в единицах канала. Пусто - подбирается сам: " +
                              "ноль, если сигнал его пересекает, иначе середина размаха",
            };
            _level.KeyDown += LevelKeyDown;
            _level.LostFocus += (s, e) => ApplyLevel();

            _chTool.Items.Add(new ToolStripLabel("Синхр. по:"));
            _chTool.Items.Add(_trig);
            _chTool.Items.Add(_slope);
            _chTool.Items.Add(new ToolStripLabel("уровень:"));
            _chTool.Items.Add(_level);
            _chTool.Items.Add(new ToolStripSeparator());

            for (int i = 0; i < map.Count; i++)
            {
                if (i == map.TickIndex) continue; /* счётчик такта - не сигнал */

                _trigCh.Add(i);
                _trig.Items.Add(map[i].Name);
            }

            /* Канал запуска - из сохранённого вида, если он там есть; иначе то,
             * что предлагает карта (uGrid/U220/Va). */
            ScopeView view = _scope.View;

            int want = string.IsNullOrEmpty(view.TrigChannel)
                ? map.DefaultTrigger
                : ScopeView.IndexOf(map, view.TrigChannel);

            if (want < 0) want = map.DefaultTrigger;

            /* Обработчик вешаем ПОСЛЕ наполнения списка и выбора: иначе он
             * сработал бы на каждое добавление, лазил в ещё не готовый _trigCh и
             * заодно сбросил бы только что прочитанный уровень. */
            int def = _trigCh.IndexOf(want);
            if (def >= 0) _trig.SelectedIndex = def;
            else if (_trig.Items.Count > 0) _trig.SelectedIndex = 0;

            if (_trig.SelectedIndex >= 0)
            {
                int ci = _trigCh[_trig.SelectedIndex];

                _scope.TrigChannel = ci;
                view.TrigChannel = map[ci].Name;
            }

            _scope.TrigLevel = view.TrigLevel;
            _scope.TrigRising = view.TrigRising;

            _level.Text = float.IsNaN(view.TrigLevel)
                ? ""
                : view.TrigLevel.ToString("0.#####", CultureInfo.InvariantCulture);

            _slope.Text = view.TrigRising ? "↑" : "↓";

            _trig.SelectedIndexChanged += TrigChanged;

            /* Кнопка на КАЖДЫЙ ЗАНЯТЫЙ СЛОТ, а не на каждый канал кадра. Что
             * показывать вообще - решает окно «Каналы…»; кнопки здесь только
             * гасят и возвращают кривую, не трогая настройку слота. Иначе на
             * пятнадцати каналах панель превращается в частокол, в котором
             * выбранные три не найти.
             *
             * Красятся в цвет линии: подпись у графика и кнопка должны читаться
             * как одно и то же. */
            for (int g = 0; g < ScopeView.Groups; g++)
                for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                {
                    ScopeSlot sl = view[g, s];
                    if (sl.Empty) continue;

                    int gg = g, ss = s;
                    int ci = ScopeView.IndexOf(map, sl.Name);
                    IniChannel ch = map[ci];

                    var b = new ToolStripButton(ch.Name)
                    {
                        DisplayStyle = ToolStripItemDisplayStyle.Text,
                        CheckOnClick = true,
                        Checked = _scope.IsVisibleSlot(g, s),
                        ForeColor = Darken(sl.Color),
                        ToolTipText = (g == 0 ? "верхнее поле" : "нижнее поле") + ", " + Where(ch),
                    };
                    b.CheckedChanged += (sn, e) =>
                    {
                        if (_scope != null) _scope.SetVisibleSlot(gg, ss, b.Checked);
                    };

                    _chButtons.Add(b);
                    _btnCh.Add(ci);
                    _chTool.Items.Add(b);
                }
        }

        /// <summary>
        /// Снять элементы панели каналов с освобождением: Items.Clear() только
        /// отвязывает, а панель пересобирается на каждое переключение набора и
        /// на каждую правку в окне «Каналы…». Копию списка берём потому, что
        /// Dispose сам вынимает элемент из коллекции.
        /// </summary>
        private void ClearChannelTool()
        {
            var old = new ToolStripItem[_chTool.Items.Count];
            _chTool.Items.CopyTo(old, 0);

            _chTool.Items.Clear();
            foreach (ToolStripItem it in old) it.Dispose();

            _chButtons.Clear();
            _btnCh.Clear();
            _trigCh.Clear();

            _trig = null;
            _slope = null;
            _level = null;
        }

        /// <summary>Откуда канал берётся - для подсказки на кнопке.</summary>
        private static string Where(IniChannel ch)
        {
            switch (ch.Kind)
            {
                case ChKind.Constant: return "постоянная";
                case ChKind.Mask: return "вычисляемый, маска";
                default:
                    return "слово " + ch.Word +
                           (Math.Abs(ch.K - 1f) < 1e-9f ? ", код" : ", x" + ch.K);
            }
        }

        /*-------------------------------------------------------------------
         * Настройка вида
         *------------------------------------------------------------------*/

        private void ChannelsClick(object sender, EventArgs e)
        {
            if (_scope == null || _map == null) return;

            using (var d = new ScopeConfigForm(_map, _scope.View))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;

                _scope.SetView(d.Result);

                /* Кнопки строятся по слотам, значит набор кнопок изменился. */
                ClearChannelTool();
                BuildChannelTool(_map);

                SaveView();
            }
        }

        /// <summary>Записать вид текущего шаблона. Без шаблона записывать нечего.</summary>
        private void SaveView()
        {
            if (_scope == null || _map == null) return;

            _scope.View.Save(_map.Template);
        }

        /// <summary>Цвет линии на тёмном фоне слишком светлый для подписи на светлой панели.</summary>
        private static Color Darken(Color c)
        {
            return Color.FromArgb((int)(c.R * 0.62f), (int)(c.G * 0.62f), (int)(c.B * 0.62f));
        }

        private void TrigChanged(object sender, EventArgs e)
        {
            if (_scope == null) return;

            int i = _trig.SelectedIndex;
            if (i < 0 || i >= _trigCh.Count) return;

            _scope.TrigChannel = _trigCh[i];

            /* Уровень задан в единицах ПРЕЖНЕГО канала: вольты на амперах - это
             * не настройка, а ловушка. Сбрасываем в авто. */
            _scope.TrigLevel = float.NaN;
            _level.Text = "";

            _scope.View.TrigChannel = _map[_trigCh[i]].Name;
            _scope.View.TrigLevel = float.NaN;
        }

        private void SlopeClick(object sender, EventArgs e)
        {
            if (_scope == null) return;

            bool rising = !_scope.TrigRising;

            _scope.TrigRising = rising;
            _scope.View.TrigRising = rising;
            _slope.Text = rising ? "↑" : "↓";
        }

        private void LevelKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            e.SuppressKeyPress = true; /* иначе окно звякнет */
            ApplyLevel();
        }

        /// <summary>
        /// Снять уровень из поля. Пусто или мусор - автоуровень: отказывать
        /// окном тут не за что, пустое поле и есть «подбирай сам».
        /// </summary>
        private void ApplyLevel()
        {
            if (_scope == null || _level == null) return;

            string s = _level.Text.Trim().Replace(',', '.');

            float v;
            bool ok = s.Length > 0 &&
                      float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

            float level = ok ? Parsed(s) : float.NaN;

            _scope.TrigLevel = level;
            _scope.View.TrigLevel = level;

            if (!ok && s.Length > 0) _level.Text = "";
        }

        private static float Parsed(string s)
        {
            float v;
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
            return v;
        }

        private void WindowChanged(object sender, EventArgs e)
        {
            ApplyWindow();
            if (_scope != null) _scope.Invalidate();
        }

        private void ApplyWindow()
        {
            if (_scope == null) return;

            switch (_window.SelectedIndex)
            {
                case 0: _scope.WindowMs = 20; break;
                case 1: _scope.WindowMs = 50; break;
                case 2: _scope.WindowMs = 100; break;
                case 3: _scope.WindowMs = 200; break;
                case 4: _scope.WindowMs = 500; break;
                default: _scope.WindowMs = 1000; break;
            }
        }

        /*-------------------------------------------------------------------
         * Таймер
         *------------------------------------------------------------------*/
        private int _tickN;

        private void Tick(object sender, EventArgs e)
        {
            if (_scope != null) _scope.Invalidate();

            if (++_tickN < 5) return;
            _tickN = 0;

            /* Набор кадра переключают в главном окне, и узнать об этом можно
             * только посмотрев. Пять раз в секунду - с запасом: переключение
             * делается руками. */
            if (Form1.f2kadrName != _frame) { Rebuild(); return; }

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            Source src = _src;

            if (_broken)
            {
                /* Запись ленты в файл при этом идёт - это надо сказать прямо,
                 * иначе отключившийся осциллограф выглядит как сорванный опыт. */
                _stFrame.Text = "осциллограф отключён, запись идёт";
                _stFrame.ForeColor = Color.FromArgb(170, 40, 40);
                _stFrame.ToolTipText = _brokenWhy;
                return;
            }

            if (src == null)
            {
                _stFrame.Text = "шаблон не прочитан";
                _stFrame.ForeColor = Color.FromArgb(160, 90, 0);
                _stTick.Text = _stCount.Text = _stGaps.Text = _stHz.Text = "";
                return;
            }

            long received, gaps;
            int gapMax;
            uint tick;
            float hz;
            int live;
            bool wrongFrame;

            /* Счётчики - long, и на 32-битной машине их чтение без замка не
             * атомарно. */
            lock (src.Ring.Gate)
            {
                received = src.Ring.Received;
                gaps = src.Ring.Gaps;
                gapMax = src.Ring.GapMax;
                tick = src.Ring.Tick;
                hz = src.Ring.Hz;
                live = src.Ring.LiveCount;
                wrongFrame = src.Ring.WrongFrame;

                src.Ring.CopyLive(_liveCh);
            }

            IniChannelMap map = src.Ring.Map;

            if (wrongFrame)
            {
                /* Красным и первым делом: на шине кадр, который этим шаблоном не
                 * разобрать. Рисовать нечего, и это правильно - чужие числа под
                 * правильными именами были бы хуже пустого поля. */
                _stFrame.Text = "на шине не " + map.Template + " - нужен Alt+F2, затем F2";
                _stFrame.ForeColor = Color.FromArgb(170, 40, 40);
                _stFrame.ToolTipText =
                    "Имя набора сменилось, а команда прибору не ушла: F2 посылает её " +
                    "только когда лента стоит. Прибор продолжает слать прежний кадр.";
            }
            else
            {
                string s = "кадр " + map.Template;
                if (map.Skipped > 0) s += ", вычисл. " + map.Skipped + " мимо";

                _stFrame.Text = map.Warning == null ? s : s + " (!)";
                _stFrame.ForeColor = map.Warning == null
                    ? SystemColors.ControlText
                    : Color.FromArgb(160, 90, 0);
                _stFrame.ToolTipText = map.Warning;
            }

            _stTick.Text = "такт " + tick;
            _stCount.Text = "отсч. " + received;

            /* Доля и самый большой разрыв - рядом с числом. По одному числу
             * разрывов не отличить «много пропусков по одному такту» от
             * «несколько проспанных пачек», а лечатся они совершенно
             * по-разному. Доля сравнивается с расчётной 0.4 % из
             * grid_tie.txt 6.2. */
            _stGaps.Text = "разрывы " + gaps;

            if (gaps > 0 && received > 0)
                _stGaps.Text += " · " + (100.0 * gaps / received).ToString("0.##") +
                                "% · макс " + gapMax;

            _stGaps.ForeColor = gaps > 0 ? Color.FromArgb(160, 90, 0) : SystemColors.ControlText;

            /* Частота такта измерена, а не задана: по CAN она не передаётся, а
             * ось времени без неё не построить. Стоит здесь, чтобы было видно,
             * чему верит развёртка. */
            _stHz.Text = "такт " + hz.ToString("0") + " Гц";

            /* Каналы, которых в кадре нет, гасим и отключаем. Старая прошивка
             * присылает меньше слов, и последние каналы существуют только как
             * нули - плоскую линию нельзя показывать так, будто это измерение.
             *
             * Кнопка проверяется по СВОЕМУ каналу, а не по своему номеру. Номер
             * кнопки - это порядок занятых слотов, и сравнивать его с числом
             * живых каналов было нельзя: при неполном кадре гасло не то. */
            if (live == _liveShown) return;
            _liveShown = live;

            for (int i = 0; i < _chButtons.Count; i++)
            {
                int ci = _btnCh[i];
                bool has = ci >= 0 && ci < _liveCh.Length && _liveCh[ci];

                _chButtons[i].Enabled = has;
                if (!has && _chButtons[i].Checked) _chButtons[i].Checked = false;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveView();

            /* Гасим ДО base: с этого момента поток CAN перестаёт трогать кольцо. */
            _src = null;
            _live = null;

            _timer.Stop();
            _timer.Dispose();

            base.OnFormClosed(e);
        }
    }
}

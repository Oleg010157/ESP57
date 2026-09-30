using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Peak.Can.Basic;

namespace ICDIBasic
{
    /*
     * Окно осциллографа быстрой ленты.
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
        /// <summary>Набор кадра, который окно умеет читать. Кадр 103 везёт и другие раскладки.</summary>
        private const string Frame = "F3_convertor";

        /// <summary>
        /// Живое окно. volatile: ссылку пишет поток окна, а читает поток CAN.
        /// </summary>
        private static volatile ScopeForm _live;

        private readonly ScopeRing _ring = new ScopeRing();
        private readonly ScopeControl _scope;

        private readonly ToolStripComboBox _window;
        private readonly ToolStripButton _btnSync;
        private readonly ToolStripButton _btnFreeze;
        private readonly ToolStripButton _btnCursor;
        private readonly ToolStripButton _btnReset;
        private readonly List<ToolStripButton> _chButtons = new List<ToolStripButton>();

        private readonly ToolStripStatusLabel _stFrame;
        private readonly ToolStripStatusLabel _stTick;
        private readonly ToolStripStatusLabel _stCount;
        private readonly ToolStripStatusLabel _stGaps;
        private readonly ToolStripStatusLabel _stHz;

        private readonly Timer _timer;

        /// <summary>Сколько каналов было живо в прошлый раз - чтобы не трогать кнопки зря.</summary>
        private int _liveShown = -1;

        /*-------------------------------------------------------------------
         * Подключение к данным
         *------------------------------------------------------------------*/

        /// <summary>
        /// Вызывается ИЗ ПОТОКА CAN на каждый кадр 103. Окна нет - выходим
        /// немедленно.
        /// </summary>
        public static void Feed(TPCANMsgFD msg)
        {
            ScopeForm f = _live;
            if (f == null) return;

            // Кадр 103 везёт четыре разные раскладки (F3_convertor, F3_invertor,
            // F1_*), имён внутри кадра нет, и различить их можно только по тому,
            // какой набор выбран в поле «F2 Кадр». Разобрать чужую раскладку по
            // нашей таблице значит показать правдоподобную чушь.
            //
            // До первого нажатия F2 имя пустое - там же оно и присваивается.
            if (Form1.f2kadrName != Frame) return;

            /* Кадр приходит извне, а читаем мы из него фиксированные тридцать два
             * байта. Обычные пути дают ровно 64 байта (PCANBasic маршалит массив
             * по SizeConst, PcanFdMessageConverter выделяет столько же), но
             * исключение из потока приёма CAN искать потом пришлось бы долго. */
            byte[] d = msg.DATA;
            if (d == null || d.Length < 2 * F3Channels.WordsIn(msg.DLC)) return;

            f._ring.Append(msg);
        }

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

            var f = new ScopeForm();
            _live = f;
            f.Show(owner);
        }

        /*-------------------------------------------------------------------
         * Окно
         *------------------------------------------------------------------*/

        private ScopeForm()
        {
            _scope = new ScopeControl(_ring) { Dock = DockStyle.Fill };

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
                ToolTipText = "Пуск развёртки по переходу uGrid через ноль вверх",
            };
            _btnSync.CheckedChanged += (s, e) => _scope.Sync = _btnSync.Checked;

            _btnFreeze = new ToolStripButton("Заморозить")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                CheckOnClick = true,
            };
            _btnFreeze.CheckedChanged += (s, e) =>
            {
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
                _scope.ShowCursor = _btnCursor.Checked;
                if (_btnCursor.Checked) _scope.Focus();
            };

            _btnReset = new ToolStripButton("Сбросить счётчики")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
            };
            _btnReset.Click += (s, e) => { _ring.Reset(); _scope.Invalidate(); };

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

            // Кнопки каналов красятся в цвет линии: подпись рядом с графиком и
            // кнопка должны читаться как одно и то же, иначе на пятнадцати
            // каналах приходится сверять названия.
            for (int i = 0; i < F3Channels.Count; i++)
            {
                int idx = i;
                F3Channel ch = F3Channels.All[i];

                var b = new ToolStripButton(ch.Name)
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    CheckOnClick = true,
                    Checked = _scope.IsVisibleChannel(i),
                    ForeColor = Darken(ch.Color),
                    ToolTipText = "слово " + i + (string.IsNullOrEmpty(ch.Unit) ? "" : ", " + ch.Unit),
                };
                b.CheckedChanged += (s, e) => _scope.SetVisibleChannel(idx, b.Checked);

                _chButtons.Add(b);
                tool.Items.Add(b);
            }

            _stFrame = NewStatus(240);
            _stTick = NewStatus(120);
            _stCount = NewStatus(140);
            _stGaps = NewStatus(130);
            _stHz = NewStatus(150);

            var status = new StatusStrip { SizingGrip = true };
            status.Items.AddRange(new ToolStripItem[] { _stFrame, _stTick, _stCount, _stGaps, _stHz });

            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 620);
            MinimumSize = new Size(720, 420);
            Text = "Осциллограф - кадр 103, " + Frame;
            StartPosition = FormStartPosition.CenterParent;

            // Порядок добавления важен: закреплённый по краю элемент забирает
            // место у добавленных РАНЬШЕ, поэтому заполняющий идёт первым.
            Controls.Add(_scope);
            Controls.Add(tool);
            Controls.Add(status);

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

        /// <summary>Цвет линии на тёмном фоне слишком светлый для подписи на светлой панели.</summary>
        private static Color Darken(Color c)
        {
            return Color.FromArgb((int)(c.R * 0.62f), (int)(c.G * 0.62f), (int)(c.B * 0.62f));
        }

        private void WindowChanged(object sender, EventArgs e)
        {
            switch (_window.SelectedIndex)
            {
                case 0: _scope.WindowMs = 20; break;
                case 1: _scope.WindowMs = 50; break;
                case 2: _scope.WindowMs = 100; break;
                case 3: _scope.WindowMs = 200; break;
                case 4: _scope.WindowMs = 500; break;
                default: _scope.WindowMs = 1000; break;
            }

            _scope.Invalidate();
        }

        private int _tickN;

        private void Tick(object sender, EventArgs e)
        {
            _scope.Invalidate();

            if (++_tickN < 5) return;
            _tickN = 0;

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            long received, gaps;
            uint tick;
            float hz;
            int live;

            /* Счётчики - long, и на 32-битной машине их чтение без замка не
             * атомарно. */
            lock (_ring.Gate)
            {
                received = _ring.Received;
                gaps = _ring.Gaps;
                tick = _ring.Tick;
                hz = _ring.Hz;
                live = _ring.Channels;
            }

            /* Выбранный набор кадра - первым делом. Если он не тот, ленты нет и
             * не будет, и это единственное, что нужно знать, глядя на пустое
             * поле. Клавиша названа не для красоты: F2 и выбирает набор, и
             * командует прошивке его слать - без неё кадра на шине нет вовсе. */
            bool ok = Form1.f2kadrName == Frame;

            _stFrame.Text = ok ? "кадр " + Frame : "выберите " + Frame + " и F2";
            _stFrame.ForeColor = ok ? SystemColors.ControlText : Color.FromArgb(160, 90, 0);

            _stTick.Text = "такт " + tick;
            _stCount.Text = "отсч. " + received;

            _stGaps.Text = "разрывы " + gaps;
            _stGaps.ForeColor = gaps > 0 ? Color.FromArgb(160, 90, 0) : SystemColors.ControlText;

            /* Частота такта измерена, а не задана: по CAN она не передаётся, а
             * ось времени без неё не построить. Стоит здесь, чтобы было видно,
             * чему верит развёртка. */
            _stHz.Text = "такт " + hz.ToString("0") + " Гц";

            /* Каналы, которых в кадре нет, гасим и отключаем. Старая прошивка
             * присылает двенадцать слов вместо шестнадцати, и последние каналы
             * существуют только как нули - плоскую линию нельзя показывать так,
             * будто это измерение. */
            if (live == _liveShown) return;
            _liveShown = live;

            for (int i = 0; i < _chButtons.Count; i++)
            {
                bool has = i < live;

                _chButtons[i].Enabled = has;
                if (!has && _chButtons[i].Checked) _chButtons[i].Checked = false;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            /* Ссылку убираем ДО base: с этого момента поток CAN перестаёт
             * трогать кольцо. */
            _live = null;

            _timer.Stop();
            _timer.Dispose();

            base.OnFormClosed(e);
        }
    }
}

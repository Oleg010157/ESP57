using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ICDIBasic
{
    /*
     * Осциллограф быстрой телеметрии - кадр 103, набор F3_convertor.
     *
     * Перенесён из проекта ESP_INV (c:\Work\ESP_INV, ui/EspInv.App/ScopeControl.cs),
     * где писался для однофазного инвертора на ESP32. Данные там приходили пачками
     * по UDP, здесь - по одному отсчёту на кадр CAN, и это единственное, что
     * поменялось по существу: рисование, синхронизация и курсор те же.
     *
     * Три вещи здесь сделаны не так, как просится, и каждая по своей причине.
     *
     * 1. Отсчёты кладутся в кольцо ПРЯМО ИЗ ПОТОКА ПРИЁМА, без Invoke - см.
     *    ScopeRing. Кадры идут до пяти тысяч в секунду; маршалить каждый в поток
     *    окна значит забить очередь сообщений и получить неотзывчивое окно на
     *    ровном месте. Окно само зовёт Invalidate() по таймеру 25 Гц - показывать
     *    чаще, чем обновляется экран, всё равно некому.
     *
     * 2. Когда отсчётов больше, чем пикселей, рисуется огибающая min/max, а не
     *    каждая точка. Полсекунды при 5 кГц - это 2500 точек на 800 пикселей:
     *    полилиния по всем точкам стоит втрое дороже и при этом теряет пики,
     *    потому что GDI+ соединяет точки, а не показывает размах внутри пикселя.
     *
     * 3. У каждого канала свой масштаб. Общая ось смысла не имеет: вольты, амперы
     *    и безразмерный индекс модуляции в одной шкале означают, что видно будет
     *    только вольты. Масштаб округляется до 1/2/5 и отпускается вниз лишь при
     *    двукратном запасе - иначе он пересчитывается на каждой перерисовке и
     *    картинка дышит.
     */
    internal sealed class ScopeControl : Control
    {
        private const int Ch = F3Channels.Count;

        private readonly ScopeRing _ring;

        private readonly bool[] _visible = new bool[Ch];
        private readonly float[] _scale = new float[Ch];

        /* Снимок для рисования: приёмный поток не должен ждать GDI+. */
        private readonly float[] _snapV = new float[ScopeRing.Capacity * Ch];
        private readonly uint[] _snapT = new uint[ScopeRing.Capacity];

        /// <summary>Часы для сдерживания таблицы отсчёта. Stopwatch, а не TickCount: тот переполняется.</summary>
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        public ScopeControl(ScopeRing ring)
        {
            _ring = ring;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            /* Клавиатура нужна курсору: стрелками он двигается по одному отсчёту,
             * а мышью так точно не попасть. */
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;

            BackColor = Color.FromArgb(18, 22, 27);

            for (int c = 0; c < Ch; c++)
            {
                _scale[c] = 1.0f;
                _visible[c] = F3Channels.All[c].Shown;
            }
        }

        /// <summary>Ширина окна по времени, мс. Ноль здесь дал бы окно в два отсчёта.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int WindowMs { get; set; } = 100;

        /// <summary>
        /// Частота такта управления, которой верит ось времени. Не задаётся, а
        /// измеряется по такту - см. ScopeRing.
        /// </summary>
        public float CtrlHz { get; private set; }

        /*-----------------------------------------------------------------------
         * Синхронизация развёртки
         *
         * То же самое, что «запуск» на обычном осциллографе. Без него окно - это
         * просто последние N отсчётов кольца: каждую перерисовку граница окна
         * попадает в случайную точку периода сети, и синусоида уезжает вбок.
         * Смотреть на такое нельзя - глазу не за что зацепиться.
         *
         * С синхронизацией левый край окна ставится на переход uGrid через ноль
         * вверх. Точка привязки всегда в одной и той же фазе сети, и картинка
         * стоит, хотя данные продолжают идти.
         *
         * Почему именно uGrid: всё в этом приборе привязано к сети - и фаза
         * модулятора, и задание тока. Синхронизация по любому другому каналу
         * дала бы ту же самую точку, только через лишнее звено.
         *
         * Плата за это - задержка. Левый край берётся не у самого свежего
         * отсчёта, а у последнего перехода, после которого набралось целое окно,
         * то есть картинка отстаёт максимум на период сети (20 мс). Для глаза
         * это незаметно, а для измерений есть заморозка.
         *----------------------------------------------------------------------*/

        private bool _sync = true;
        private bool _synced; /* нашли ли переход в этот раз - для подписи */

        /// <summary>Пускать развёртку по переходу uGrid через ноль вверх.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Sync
        {
            get { return _sync; }
            set { _sync = value; Invalidate(); }
        }

        /*-----------------------------------------------------------------------
         * Курсор и метка
         *
         * Курсор работает и на бегущей картинке. Без синхронизации отсчёт под ним
         * менялся 25 раз в секунду и прочитать его было нельзя; с синхронизацией
         * развёртка стоит, и под курсором каждый раз одна и та же фаза сети -
         * значение дрожит только на шум измерения.
         *
         * Мельтешение цифр всё-таки осталось бы: даже честный шум в последнем
         * знаке, обновляемый 25 раз в секунду, читать невозможно. Поэтому таблица
         * пересобирается 5 раз в секунду. Врать при этом не приходится:
         * показывается настоящий отсчёт, просто реже. Сдвинули курсор - таблица
         * пересобирается сразу, без ожидания.
         *
         * Позиции хранятся ИНДЕКСАМИ отсчёта, а не пикселями. Иначе мышь и стрелки
         * задавали бы курсор в разных единицах, и при смене ширины окна он
         * уезжал бы не туда.
         *----------------------------------------------------------------------*/
        private int _cursorIdx = -1; /* -1 = не поставлен */
        private int _markIdx = -1;   /* -1 = метки нет, разность не считается */
        private int _snapN;          /* сколько отсчётов было в последнем снимке */

        private bool _showCursor;

        /// <summary>Показывать курсор. Метка живёт только вместе с ним.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowCursor
        {
            get { return _showCursor; }
            set
            {
                _showCursor = value;

                if (!value) { _cursorIdx = -1; _markIdx = -1; }

                Invalidate();
            }
        }

        /// <summary>Картинка стоит: кольцо не пополняется. Флаг живёт в кольце - его читает приёмный поток.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Frozen
        {
            get { return _ring.Frozen; }
            set { _ring.Frozen = value; Invalidate(); }
        }

        public bool IsVisibleChannel(int i)
        {
            return _visible[i];
        }

        public void SetVisibleChannel(int i, bool on)
        {
            _visible[i] = on;
            Invalidate();
        }

        /*-----------------------------------------------------------------------
         * Снимок кольца
         *----------------------------------------------------------------------*/

        /// <summary>Значение канала в снимке.</summary>
        private float Snap(int i, int c)
        {
            return _snapV[i * Ch + c];
        }

        /// <summary>Шаг между отсчётами в тактах, из кольца.</summary>
        private int _step = 1;

        /// <summary>Сколько каналов в кадре настоящие; остальные - нули старой прошивки.</summary>
        private int _live = Ch;

        /*-----------------------------------------------------------------------
         * Поиск точки запуска. Вызывается только под замком кольца.
         *----------------------------------------------------------------------*/

        /// <summary>
        /// Возраст отсчёта, который станет левым краем окна, или -1, если
        /// зацепиться не за что.
        /// </summary>
        private int FindTrigger(int n, int count)
        {
            // Порог берётся от размаха самого сигнала, а не задаётся в вольтах:
            // масштаб канала uGrid зависит от тарировки kU220, и зашитый порог
            // пришлось бы менять вместе с ней.
            float peak = 0f;
            for (int a = 0; a < count; a++)
            {
                float v = Math.Abs(_ring.At(a, F3Channels.Trig));
                if (v > peak) peak = v;
            }

            if (peak < 1e-3f) return -1; /* сети нет - синхронизировать не по чему */

            float arm = -0.05f * peak;

            // Идём от старых отсчётов к новым и запоминаем последний переход:
            // нужен самый свежий, после которого ещё помещается целое окно.
            // Гистерезис обязателен - без него развёртка цеплялась бы за каждое
            // дрожание около нуля и картинка прыгала бы на полпериода.
            bool armed = false;
            int best = -1;

            for (int a = count - 1; a >= n - 1; --a)
            {
                float v = _ring.At(a, F3Channels.Trig);

                if (v < arm) armed = true;
                else if (armed && v >= 0f) { armed = false; best = a; }
            }

            // Истинный переход лежит МЕЖДУ отсчётами, и почти никогда не попадает
            // в отсчёт ровно. Если этого не учесть, левый край гуляет на такт -
            // 0,2 мс из 100 мс окна, полтора пикселя, - и картинка мелко дрожит.
            // Само по себе это было незаметно; стало видно, когда рядом появилась
            // неподвижная черта курсора, относительно которой дрожание сразу
            // читается. Поэтому доля отсчёта считается линейной интерполяцией и
            // потом сдвигает всю картинку по горизонтали.
            //
            // Ровно то же самое делает прошивка в sine1ph_on_zero(): там переход
            // между тактами тоже интерполируется, иначе фаза модулятора дёргалась
            // бы на такт. Задача одна и та же, и решается она одинаково.
            _trigFrac = 0f;

            if (best >= 0 && best + 1 < count)
            {
                float cur = _ring.At(best, F3Channels.Trig);
                float prev = _ring.At(best + 1, F3Channels.Trig);
                float d = cur - prev;

                if (d > 0f) _trigFrac = Clamp(cur / d, 0f, 1f);
            }

            return best;
        }

        /*-----------------------------------------------------------------------
         * Управление курсором
         *----------------------------------------------------------------------*/
        private Rectangle PlotRect()
        {
            return new Rectangle(MarginL, MarginT,
                                 Math.Max(1, Width - MarginL - MarginR),
                                 Math.Max(1, Height - MarginT - MarginB));
        }

        /*-----------------------------------------------------------------------
         * Ось X
         *
         * Место отсчёта на экране определяется его НОМЕРОМ ТАКТА, а не позицией в
         * массиве. Разница видна ровно тогда, когда что-то потерялось: если
         * нанизывать подряд, картинка молча сжимается по времени и синусоида
         * рвётся на куски с разной фазой - выглядит как сорванная синхронизация,
         * хотя развёртка тут ни при чём. По такту потерянный кусок остаётся
         * пустым, а всё остальное стоит на своих местах.
         *
         * Ради этого sineTick и лежит последним словом каждого кадра.
         *
         * Считается один раз за перерисовку, и все - кривые, курсор, мышь -
         * пользуются одной и той же осью, иначе они разъедутся.
         *----------------------------------------------------------------------*/
        private uint _baseTick;  /* такт отсчёта, попавшего на левый край */
        private float _trigFrac; /* доля такта до истинного перехода через ноль */
        private float _xk = 1f;  /* пикселей на такт */
        private float _x0;       /* x нулевого отсчёта окна */
        private float _spanMs;   /* сколько времени на самом деле показано */

        /// <summary>
        /// Смещение отсчёта от левого края в ТАКТАХ УПРАВЛЕНИЯ - как есть, без
        /// приведения к шагу.
        ///
        /// Это то самое число, ради которого счётчик тактов вообще едет в каждом
        /// кадре. Шагая курсором по одному отсчёту, по нему видно плотность
        /// прихода: растёт на 1 - дошло всё; больше - кадр не выехал на шину или
        /// потерялся по дороге. Поделить это на шаг значит стереть ровно ту
        /// разницу, которую и надо увидеть.
        ///
        /// Знаковое приведение обязательно: развёрнутый такт - это uint32, и на
        /// разности его переполнение пройдёт незаметно только так.
        /// </summary>
        private int OffTicks(int i)
        {
            return (int)(_snapT[i] - _baseTick);
        }

        /// <summary>То же смещение, но в отсчётах - для оси X.</summary>
        private float Off(int i)
        {
            return OffTicks(i) / (float)Math.Max(1, _step);
        }

        private float IdxToX(int i)
        {
            return _x0 + Off(i) * _xk;
        }

        /// <summary>Пиксель -> индекс отсчёта. Обратное к <see cref="IdxToX"/>.</summary>
        private int XToIdx(int x, int n)
        {
            if (n < 2 || _xk <= 0f) return 0;

            /* Двоичный поиск, а не деление: при потерях отсчёты стоят на оси
             * неравномерно, и прямой формулы больше нет. */
            float want = (x - _x0) / _xk;

            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (Off(mid) < want) lo = mid + 1; else hi = mid;
            }

            if (lo > 0 && Math.Abs(Off(lo - 1) - want) < Math.Abs(Off(lo) - want)) --lo;
            return lo;
        }

        /*
         * Мышь ведёт курсор, пока его не начали двигать стрелками.
         *
         * Без этого стрелки бесполезны ровно тогда, когда нужны больше всего.
         * Чтобы дать клавиатуре фокус, по графику надо щёлкнуть, и после щелчка
         * указатель остаётся лежать на графике. Дальше любое дрожание руки - это
         * MouseMove, курсор прыгает обратно под указатель, и кажется, что стрелки
         * не работают вовсе. Так и было.
         *
         * Поэтому клавиатура забирает курсор себе, а мышь получает его назад,
         * только если указатель действительно уехал - дальше, чем на дрожание.
         * Щелчок отдаёт управление мыши сразу: это уже осознанное действие.
         */
        private Point _mouseAnchor;
        private bool _keyOwnsCursor;

        /// <summary>Насколько надо сдвинуть мышь, чтобы она снова вела курсор, px.</summary>
        private const int MouseWakePx = 4;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_showCursor || _snapN < 2) return;

            if (_keyOwnsCursor)
            {
                int dx = e.X - _mouseAnchor.X;
                int dy = e.Y - _mouseAnchor.Y;

                if (dx * dx + dy * dy < MouseWakePx * MouseWakePx) return;

                _keyOwnsCursor = false;
            }

            Rectangle plot = PlotRect();
            if (e.X < plot.Left || e.X > plot.Right) return;

            int i = XToIdx(e.X, _snapN);
            if (i == _cursorIdx) return;

            _cursorIdx = i;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus(); /* без фокуса стрелки до нас не дойдут */

            _keyOwnsCursor = false; /* щелчок - осознанное действие мышью */

            if (!_showCursor || _snapN < 2) return;

            Rectangle plot = PlotRect();
            if (e.X < plot.Left || e.X > plot.Right) return;

            /* Левая кнопка прибивает метку туда, где стоит курсор, правая её
             * убирает. Так измерение делается одним движением: подвели, щёлкнули,
             * повели дальше - разность считается на ходу. */
            if (e.Button == MouseButtons.Left) _markIdx = XToIdx(e.X, _snapN);
            else if (e.Button == MouseButtons.Right) _markIdx = -1;

            Invalidate();
        }

        /* Стрелки и пробел по умолчанию уходят на перемещение фокуса и нажатие
         * кнопки. Забираем их себе - иначе курсор нельзя двигать по одному
         * отсчёту. */
        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Space)
                return true;

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!_showCursor || _cursorIdx < 0 || _snapN < 2) return;

            /* Пробел ставит метку в текущий курсор. Мышью точнее одного пикселя
             * не попасть, а стрелками - можно, и мерить период надо именно так. */
            if (e.KeyCode == Keys.Space) { _markIdx = _cursorIdx; e.Handled = true; Invalidate(); return; }
            if (e.KeyCode == Keys.Escape) { _markIdx = -1; e.Handled = true; Invalidate(); return; }

            /* Shift ускоряет: перебирать пять тысяч отсчётов по одному невозможно. */
            int step = e.Shift ? 25 : 1;

            int i = _cursorIdx;
            if (e.KeyCode == Keys.Left) i = _cursorIdx - step;
            else if (e.KeyCode == Keys.Right) i = _cursorIdx + step;

            i = Clamp(i, 0, _snapN - 1);
            if (i == _cursorIdx) return;

            _cursorIdx = i;

            /* Курсор теперь наш. Запоминаем, где лежит указатель: пока он не
             * уедет от этой точки, MouseMove курсор не трогает. */
            _keyOwnsCursor = true;
            _mouseAnchor = PointToClient(MousePosition);

            e.Handled = true;
            Invalidate();
        }

        /*-----------------------------------------------------------------------
         * Рисование
         *----------------------------------------------------------------------*/
        private const int MarginL = 52;
        private const int MarginR = 8;
        private const int MarginT = 8;
        private const int MarginB = 20;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            var plot = new Rectangle(MarginL, MarginT,
                                     Math.Max(1, Width - MarginL - MarginR),
                                     Math.Max(1, Height - MarginT - MarginB));

            DrawGrid(g, plot);

            int want;
            int n;

            lock (_ring.Gate)
            {
                /* Частота и шаг берутся из кольца здесь же, под замком: от них
                 * зависит, сколько отсчётов войдёт в окно. */
                _step = _ring.Step;
                _live = _ring.Channels;
                CtrlHz = _ring.Hz;

                want = Clamp((int)(WindowMs * CtrlHz / 1000f / Math.Max(1, _step)),
                             2, ScopeRing.Capacity);

                int count = _ring.Count;
                n = Math.Min(count, want);

                // Возраст левого края окна: 0 - самый свежий отсчёт. Без
                // синхронизации это просто последние n отсчётов.
                int age0 = n - 1;

                _synced = false;
                _trigFrac = 0f;

                if (_sync && n >= 2)
                {
                    int t = FindTrigger(n, count);
                    if (t >= 0) { age0 = t; _synced = true; }
                }

                // Копируем под замком, рисуем уже снаружи.
                //
                // Берём не n отсчётов подряд, а те, что укладываются в окно ПО
                // ТАКТАМ. При потерях эти два числа расходятся: n отсчётов
                // растянуты на большее время, и хвост уехал бы за правый край.
                uint baseTick = n >= 1 ? _ring.TickAt(age0) : 0u;
                long limit = (long)(want - 1) * Math.Max(1, _step);

                int k = 0;
                for (int i = 0; i < n; i++)
                {
                    uint tick = _ring.TickAt(age0 - i);
                    if ((int)(tick - baseTick) > limit) break;

                    _snapT[k] = tick;
                    _ring.CopyTo(age0 - i, _snapV, k * Ch);
                    k++;
                }

                n = k;
            }

            /* Курсор адресуется индексом в этом снимке, поэтому его размер надо
             * знать обработчикам мыши и клавиш. */
            _snapN = n;
            if (_cursorIdx >= n) _cursorIdx = n - 1;
            if (_markIdx >= n) _markIdx = n - 1;

            /* Ставим курсор в середину, а не ждём мыши: иначе включивший его и не
             * тронувший мышь не увидит ничего и не догадается, что он есть. */
            if (_showCursor && _cursorIdx < 0 && n >= 2) _cursorIdx = n / 2;

            if (n < 2)
            {
                TextRenderer.DrawText(g, "нет данных", Font, plot,
                                      Color.FromArgb(120, 130, 140),
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            /* Масштаб задаётся ВЫБРАННЫМ окном, а не числом набранных отсчётов.
             * Иначе при потерях картинка ужималась бы по горизонтали, подгоняя
             * себя под ширину, и потеря снова оказалась бы незаметной. */
            _baseTick = _snapT[0];
            _xk = (float)(plot.Width - 1) / Math.Max(1, want - 1);
            _x0 = plot.Left + _trigFrac * _xk;
            _spanMs = want * 1000f * Math.Max(1, _step) / CtrlHz;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            /* Сдвиг выносит последний отсчёт на долю такта за правый край. Проще
             * обрезать по рамке, чем ужимать окно на один отсчёт. */
            g.SetClip(plot);

            int legendY = plot.Top + 4;
            for (int c = 0; c < Ch; c++)
            {
                if (!_visible[c] || c >= _live) continue;

                float scale = UpdateScale(c, n);
                DrawChannel(g, plot, c, scale, n);
                DrawLegend(g, plot, c, scale, ref legendY);
            }

            DrawTrigMark(g, plot);

            g.ResetClip();
            g.SmoothingMode = SmoothingMode.Default;
            DrawTimeAxis(g, plot, n);
            DrawCursors(g, plot, n);
        }

        /*-----------------------------------------------------------------------
         * Курсор, метка и таблица отсчёта
         *----------------------------------------------------------------------*/

        /// <summary>Строка таблицы: подпись и до трёх чисел, каждое своим столбцом.</summary>
        private sealed class Row
        {
            public Row(string label, string cur, string mark, string delta, Color color)
            {
                Label = label;
                Cur = cur;
                Mark = mark;
                Delta = delta;
                Color = color;
            }

            public string Label { get; private set; }
            public string Cur { get; private set; }
            public string Mark { get; private set; }
            public string Delta { get; private set; }
            public Color Color { get; private set; }
        }

        private readonly List<Row> _rows = new List<Row>();

        private long _rowsMs;      /* когда таблица собиралась в последний раз */
        private int _rowsCur = -2; /* и при каких позициях курсора и метки */
        private int _rowsMark = -2;

        private static readonly Color CurColor = Color.FromArgb(220, 224, 232);
        private static readonly Color MarkColor = Color.FromArgb(214, 176, 96);
        private static readonly Color DimColor = Color.FromArgb(150, 158, 168);

        private void DrawCursors(Graphics g, Rectangle plot, int n)
        {
            if (!_showCursor || _cursorIdx < 0) return;

            int i = Clamp(_cursorIdx, 0, n - 1);
            int m = _markIdx >= 0 ? Clamp(_markIdx, 0, n - 1) : -1;

            /* Метка - точками и другим цветом. Двумя одинаковыми линиями пришлось
             * бы вспоминать, какая из них какая, а в таблице столбцы подписаны
             * ровно этими же цветами. */
            if (m >= 0)
            {
                float xm = IdxToX(m);
                using (var pen = new Pen(MarkColor) { DashStyle = DashStyle.Dot })
                {
                    g.DrawLine(pen, xm, plot.Top, xm, plot.Bottom);
                }
            }

            float x = IdxToX(i);
            using (var pen = new Pen(CurColor) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pen, x, plot.Top, x, plot.Bottom);
            }

            BuildRows(i, m);
            DrawReadout(g, plot, x, m >= 0);
        }

        /// <summary>
        /// Пересобрать таблицу. Не чаще 5 раз в секунду, пока курсор стоит:
        /// честный шум в последнем знаке, обновляемый 25 раз в секунду, читать
        /// невозможно. Сдвинули курсор - пересобираем сразу, это ответ на
        /// действие, и ждать тут нечего.
        /// </summary>
        private void BuildRows(int i, int m)
        {
            long now = Clock.ElapsedMilliseconds;

            bool moved = i != _rowsCur || m != _rowsMark;
            if (!moved && _rows.Count > 0 && now - _rowsMs < 200) return;

            _rowsMs = now; _rowsCur = i; _rowsMark = m;
            _rows.Clear();

            /* По тактам, а не по местам в массиве: иначе после потери отсчётов
             * курсор показывал бы время, которого на картинке нет. */
            int ni = OffTicks(i);
            int nm = m >= 0 ? OffTicks(m) : 0;
            float ti = ni * 1000f / CtrlHz;
            float tm = nm * 1000f / CtrlHz;

            /* Такт - от начала развёртки, а не сквозной номер прошивки: сквозной
             * глазу ничего не говорит, он есть в строке состояния. Зато при
             * включённой синхронизации ноль - это переход сети через ноль, и
             * время читается прямо как фаза.
             *
             * Считается в тактах управления, не в отсчётах: шагая курсором по
             * одному, по приросту этого числа видно, что доходит. Единица - всё;
             * больше - кадр не выехал на шину или потерялся по дороге. Это тот
             * самый приём, которым в Wscope ловили провалы в канале. */
            _rows.Add(new Row("такт", ni.ToString(), m >= 0 ? nm.ToString() : "",
                              m >= 0 ? Signed(ni - nm) : "", DimColor));

            _rows.Add(new Row("мс", ti.ToString("0.##"), m >= 0 ? tm.ToString("0.##") : "",
                              m >= 0 ? Signed(ti - tm) : "", DimColor));

            /* 1/Δt: так меряют период. Метку на переход через ноль, курсор на
             * следующий такой же - и частота видна без арифметики. */
            if (m >= 0 && m != i)
            {
                _rows.Add(new Row("1/Δt, Гц", "", "",
                                  (1000f / Math.Abs(ti - tm)).ToString("0.###"), DimColor));
            }

            for (int c = 0; c < Ch; c++)
            {
                if (!_visible[c] || c >= _live) continue;

                F3Channel ch = F3Channels.All[c];
                string label = string.IsNullOrEmpty(ch.Unit) ? ch.Name : ch.Name + ", " + ch.Unit;
                float vc = Snap(i, c);

                if (m < 0) { _rows.Add(new Row(label, Sig4(vc), "", "", ch.Color)); continue; }

                float vm = Snap(m, c);
                _rows.Add(new Row(label, Sig4(vc), Sig4(vm), Signed(vc - vm), ch.Color));
            }
        }

        private static string Signed(int v)
        {
            return v > 0 ? "+" + v : v.ToString();
        }

        private static string Signed(float v)
        {
            return (v > 0f ? "+" : "") + Sig4(v);
        }

        /// <summary>
        /// Самое длинное, что может выдать <see cref="Sig4"/> со знаком: знак,
        /// ноль, запятая и пять знаков после неё. По ней и меряются числовые
        /// столбцы, чтобы ширина рамки не зависела от текущих значений.
        /// </summary>
        private const string WidestNum = "-0.00000";

        private void DrawReadout(Graphics g, Rectangle plot, float x, bool withMark)
        {
            /* Без метки столбец один, и шапка не нужна: подписывать нечего. */
            int cols = withMark ? 4 : 2;
            string[] head = { "", "курсор", "метка", "Δ" };
            Color[] headColor = { DimColor, CurColor, MarkColor, DimColor };

            const int Gap = 12, Pad = 8;

            // Ширины столбцов считаются заранее: числа выравниваются вправо, чтобы
            // разряды стояли под разрядами, а для этого нужна ширина столбца.
            //
            // Числовые столбцы меряются по САМОМУ ШИРОКОМУ, что вообще может выдать
            // Sig4, а не по тому, что стоит в них сейчас. Иначе рамка дёргается на
            // каждом переходе через разряд: 9,999 длиннее, чем 10,00, строка
            // короче - и рамка на глазах меняет ширину и уезжает вбок, потому что
            // прижата к правому краю. Столбец подписей так мерить нельзя и не
            // нужно: подписи меняются только вместе с набором каналов.
            int numW = TextRenderer.MeasureText(WidestNum, Font).Width;

            var w = new int[4];
            int boxW = Pad * 2 + Gap * (cols - 1);

            for (int c = 0; c < cols; c++)
            {
                int max = withMark ? TextRenderer.MeasureText(head[c], Font).Width : 0;

                if (c == 0)
                {
                    foreach (Row r in _rows)
                    {
                        int wd = TextRenderer.MeasureText(r.Label, Font).Width;
                        if (wd > max) max = wd;
                    }
                }
                else if (numW > max) max = numW;

                w[c] = max;
                boxW += max;
            }

            int lineH = Font.Height + 2;
            int boxH = (_rows.Count + (withMark ? 1 : 0)) * lineH + 8;

            /* Рамка уходит в сторону от курсора: иначе она закрывает ровно тот
             * участок кривой, ради которого курсор и поставили. */
            int boxX = (x > plot.Left + plot.Width / 2) ? plot.Left + 6 : plot.Right - boxW - 6;
            int boxY = plot.Top + 6;

            /* Легенда живёт в левом верхнем углу. Если рамка встала туда же,
             * опускаем её под легенду. */
            if (boxX < plot.Left + 12) boxY = plot.Top + 6 + VisibleCount() * 15 + 6;

            var box = new Rectangle(boxX, boxY, boxW, boxH);

            using (var back = new SolidBrush(Color.FromArgb(230, 12, 16, 20)))
            using (var edge = new Pen(Color.FromArgb(70, 82, 94)))
            {
                g.FillRectangle(back, box);
                g.DrawRectangle(edge, box);
            }

            int y = box.Top + 4;

            if (withMark) DrawRow(g, box, w, cols, head, headColor, ref y, lineH);

            foreach (Row r in _rows)
            {
                string[] cells = { r.Label, r.Cur, r.Mark, r.Delta };
                var colors = new[] { r.Color, r.Color, r.Color, r.Color };
                DrawRow(g, box, w, cols, cells, colors, ref y, lineH);
            }
        }

        private void DrawRow(Graphics g, Rectangle box, int[] w, int cols,
                             string[] cells, Color[] colors, ref int y, int lineH)
        {
            int x = box.Left + 8;

            for (int c = 0; c < cols; c++)
            {
                var cell = new Rectangle(x, y, w[c], lineH);

                TextRenderer.DrawText(g, cells[c], Font, cell, colors[c],
                                      (c == 0 ? TextFormatFlags.Left : TextFormatFlags.Right)
                                      | TextFormatFlags.NoPadding);

                x += w[c] + 12;
            }

            y += lineH;
        }

        /// <summary>
        /// Метка запуска: уголок на левом краю, ровно на нулевой линии. Стоит
        /// там же, где физически произошёл переход, - видно, что развёртка
        /// зацепилась, а не просто показывает хвост кольца.
        /// </summary>
        private void DrawTrigMark(Graphics g, Rectangle plot)
        {
            if (!_synced) return;

            float midY = plot.Top + plot.Height * 0.5f;

            PointF[] tri =
            {
                new PointF(plot.Left + 1, midY - 5f),
                new PointF(plot.Left + 9, midY),
                new PointF(plot.Left + 1, midY + 5f),
            };

            using (var b = new SolidBrush(F3Channels.All[F3Channels.Trig].Color))
            {
                g.FillPolygon(b, tri);
            }
        }

        private int VisibleCount()
        {
            int k = 0;
            for (int c = 0; c < Ch; c++) if (_visible[c] && c < _live) k++;
            return k;
        }

        /// <summary>Четыре значащие цифры.</summary>
        private static string Sig4(float v)
        {
            float a = Math.Abs(v);

            return a >= 1000f ? v.ToString("0")
                 : a >= 100f ? v.ToString("0.#")
                 : a >= 10f ? v.ToString("0.##")
                 : a >= 1f ? v.ToString("0.###")
                 : v.ToString("0.#####");
        }

        /// <summary>Масштаб канала: округлён до 1/2/5, вниз отпускается при двукратном запасе.</summary>
        private float UpdateScale(int c, int n)
        {
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                float v = Math.Abs(Snap(i, c));
                if (v > peak) peak = v;
            }

            float want = Nice(peak);
            float cur = _scale[c];

            if (want > cur || want < cur * 0.5f) cur = want;
            _scale[c] = cur;
            return cur;
        }

        private static float Nice(float v)
        {
            if (!(v > 0f)) return 1f;

            float e = (float)Math.Pow(10.0, Math.Floor(Math.Log10(v)));
            float m = v / e;
            float n = m <= 1f ? 1f : m <= 2f ? 2f : m <= 5f ? 5f : 10f;
            return n * e;
        }

        private void DrawChannel(Graphics g, Rectangle plot, int c, float scale, int n)
        {
            float midY = plot.Top + plot.Height * 0.5f;
            float ky = -plot.Height * 0.5f / scale;

            using (var pen = new Pen(F3Channels.All[c].Color, 1f))
            {
                // Точек меньше, чем пикселей - рисуем настоящую полилинию:
                // огибающая здесь выродилась бы в набор отдельных точек и
                // потеряла форму.
                if (n <= plot.Width)
                {
                    var pts = new PointF[n];
                    int len = 0, run = 0;

                    for (int i = 0; i < n; i++)
                    {
                        // Дырка в номерах тактов - разрыв и в линии. Соединить
                        // прямой через потерянные отсчёты значит нарисовать то,
                        // чего не измеряли, - а на синусоиде такая прямая
                        // выглядит убедительно и врёт.
                        if (i > 0 && Math.Abs(Off(i) - Off(i - 1) - 1f) > 0.5f)
                        {
                            Flush(g, pen, pts, run, len - run);
                            run = len;
                        }

                        pts[len++] = new PointF(IdxToX(i), midY + ClampY(Snap(i, c) * ky, plot.Height));
                    }

                    Flush(g, pen, pts, run, len - run);
                    return;
                }

                float prevY = float.NaN;

                int col = 0;
                float lo = float.MaxValue, hi = float.MinValue;

                for (int i = 0; i <= n; i++)
                {
                    int cx = i < n ? (int)(IdxToX(i) - plot.Left) : -1;

                    if (cx != col || i == n)
                    {
                        if (lo <= hi)
                        {
                            float x = plot.Left + col;
                            float y0 = midY + ClampY(hi * ky, plot.Height);
                            float y1 = midY + ClampY(lo * ky, plot.Height);

                            // Соединяем со столбцом слева: без этого на крутых
                            // участках получается пунктир из вертикальных штрихов.
                            if (!float.IsNaN(prevY)) g.DrawLine(pen, x - 1, prevY, x, y0);
                            g.DrawLine(pen, x, y0, x, y1);
                            prevY = y1;
                        }

                        if (i == n) break;

                        // Столбцы через один и дальше - это пропущенные отсчёты,
                        // и соединять через них нечего.
                        if (cx - col > 1) prevY = float.NaN;

                        col = cx;
                        lo = float.MaxValue;
                        hi = float.MinValue;
                    }

                    float v = Snap(i, c);
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }
            }
        }

        /// <summary>
        /// Нарисовать кусок полилинии. Когда потерь нет - а это обычный случай -
        /// кусок один и массив уходит в GDI+ целиком, без копирования.
        /// </summary>
        private static void Flush(Graphics g, Pen pen, PointF[] pts, int from, int count)
        {
            if (count < 2) return;

            if (from == 0 && count == pts.Length) { g.DrawLines(pen, pts); return; }

            /* GDI+ рисует массив целиком, поэтому отрезок нужен ровно своей
             * длины. Диапазонов (pts[a..b]) в .NET Framework нет. */
            var seg = new PointF[count];
            Array.Copy(pts, from, seg, 0, count);
            g.DrawLines(pen, seg);
        }

        private static float ClampY(float y, int h)
        {
            float lim = h * 0.5f;
            return y > lim ? lim : y < -lim ? -lim : y;
        }

        /// <summary>Math.Clamp появился только в .NET Core.</summary>
        private static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private void DrawGrid(Graphics g, Rectangle plot)
        {
            using (var thin = new Pen(Color.FromArgb(38, 46, 54)))
            using (var axis = new Pen(Color.FromArgb(70, 82, 94)))
            {
                for (int i = 0; i <= 8; i++)
                {
                    int y = plot.Top + plot.Height * i / 8;
                    g.DrawLine(i == 4 ? axis : thin, plot.Left, y, plot.Right, y);
                }

                for (int i = 0; i <= 10; i++)
                {
                    int x = plot.Left + plot.Width * i / 10;
                    g.DrawLine(thin, x, plot.Top, x, plot.Bottom);
                }

                g.DrawRectangle(axis, plot);
            }

            // Подписи вертикальной оси - в долях полной шкалы: у каждого канала
            // она своя, поэтому единственное общее число здесь относительное.
            var grey = Color.FromArgb(120, 130, 140);
            TextRenderer.DrawText(g, "+1", Font, new Point(4, plot.Top - 2), grey);
            TextRenderer.DrawText(g, " 0", Font, new Point(4, plot.Top + plot.Height / 2 - 8), grey);
            TextRenderer.DrawText(g, "-1", Font, new Point(4, plot.Bottom - 15), grey);
        }

        private void DrawLegend(Graphics g, Rectangle plot, int c, float scale, ref int y)
        {
            F3Channel ch = F3Channels.All[c];

            var box = new Rectangle(plot.Left + 8, y + 6, 10, 3);
            using (var b = new SolidBrush(ch.Color)) g.FillRectangle(b, box);

            string unit = string.IsNullOrEmpty(ch.Unit) ? "" : " " + ch.Unit;
            TextRenderer.DrawText(g, ch.Name + " ±" + scale.ToString("0.###") + unit, Font,
                                  new Point(plot.Left + 22, y), ch.Color);
            y += 15;
        }

        private void DrawTimeAxis(Graphics g, Rectangle plot, int n)
        {
            var grey = Color.FromArgb(120, 130, 140);

            /* Правая подпись прижимается к краю выравниванием, а не отступом в
             * фиксированные 60 px: при крупном шрифте или значении «1000 мс»
             * такой отступ не спасает, и хвост уезжает за край. */
            var right = new Rectangle(plot.Left, plot.Bottom + 3, plot.Width, MarginB - 3);

            TextRenderer.DrawText(g, _spanMs.ToString("0.#") + " мс", Font, right, grey,
                                  TextFormatFlags.Right | TextFormatFlags.NoPadding);

            /* Сколько тактов покрыто и сколько отсчётов на них пришло. Пока всё
             * доходит, числа совпадают; как только они разошлись - часть кадров
             * потерялась, и на картинке будут разрывы. */
            int ticks = (int)Math.Round((double)Off(n - 1)) + 1;

            string left = ticks == n
                ? n + " отсч., шаг " + _step
                : n + " из " + ticks + " отсч., шаг " + _step;

            TextRenderer.DrawText(g, left, Font, right,
                                  ticks == n ? grey : Color.FromArgb(200, 150, 90),
                                  TextFormatFlags.Left | TextFormatFlags.NoPadding);

            /* Состояние запуска - посередине. «Нет сигнала» надо показывать
             * отдельно от «свободного хода»: картинка в обоих случаях уезжает, но
             * причины разные, и во втором случае это выбор оператора. */
            string text;
            Color color;

            if (!_sync)
            {
                text = "своб. ход";
                color = grey;
            }
            else if (_synced)
            {
                text = "синхр. " + F3Channels.All[F3Channels.Trig].Name + " ↑";
                color = F3Channels.All[F3Channels.Trig].Color;
            }
            else
            {
                text = "синхр.: нет сигнала";
                color = Color.FromArgb(200, 150, 90);
            }

            TextRenderer.DrawText(g, text, Font, right, color,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }
    }
}

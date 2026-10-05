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
     * Осциллограф быстрой ленты.
     *
     * Перенесён из проекта ESP_INV (c:\Work\ESP_INV, ui/EspInv.App/ScopeControl.cs),
     * где писался для однофазного инвертора на ESP32. Данные там приходили пачками
     * по UDP, здесь - по одному отсчёту на кадр CAN.
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
     * 3. У каждой кривой свой размах по вертикали. Общая ось смысла не имеет:
     *    вольты, амперы и безразмерный индекс модуляции в одной шкале означают,
     *    что видно будет только вольты. Размах берётся из настройки слота, а
     *    если он не задан - подбирается сам, с округлением до 1/2/5.
     *
     * ДВЕ ГРУППЫ рисуются ОДНИМ контролом, а не двумя. Ось времени,
     * синхронизация, курсор и метка у групп общие; два отдельных контрола
     * пришлось бы синхронизировать между собой, и первая же рассинхронизация
     * читалась бы как потеря данных.
     */
    internal sealed class ScopeControl : Control
    {
        private readonly ScopeRing _ring;
        private readonly IniChannelMap _map;
        private ScopeView _view;

        /// <summary>Каналов в шаблоне.</summary>
        private readonly int _ch;

        /* Снимок для рисования: приёмный поток не должен ждать GDI+. */
        private readonly float[] _snapV;
        private readonly uint[] _snapT = new uint[ScopeRing.Capacity];

        /// <summary>Канал каждого слота, -1 для пустого. Пересчитывается при смене вида.</summary>
        private readonly int[,] _slotCh = new int[ScopeView.Groups, ScopeView.SlotsPerGroup];

        /// <summary>Показывать ли кривую слота. Кнопки панели гасят её, не трогая настройку.</summary>
        private readonly bool[,] _shown = new bool[ScopeView.Groups, ScopeView.SlotsPerGroup];

        /// <summary>Подобранный масштаб слота, когда размах не задан.</summary>
        private readonly float[,] _auto = new float[ScopeView.Groups, ScopeView.SlotsPerGroup];

        /// <summary>
        /// Границы слота в этой перерисовке. Считаются ОДИН раз за кадр, до
        /// рисования: подписи оси берут границы первой кривой группы, и к их
        /// появлению те должны быть уже посчитаны.
        /// </summary>
        private readonly float[,] _lo = new float[ScopeView.Groups, ScopeView.SlotsPerGroup];
        private readonly float[,] _hi = new float[ScopeView.Groups, ScopeView.SlotsPerGroup];

        /// <summary>Часы для сдерживания таблицы отсчёта. Stopwatch, а не TickCount: тот переполняется.</summary>
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        public ScopeControl(ScopeRing ring, ScopeView view)
        {
            _ring = ring;
            _map = ring.Map;
            _ch = _map.Count;

            _snapV = new float[ScopeRing.Capacity * _ch];
            _live = new bool[_ch];

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            /* Клавиатура нужна курсору: стрелками он двигается по одному отсчёту,
             * а мышью так точно не попасть. */
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;

            BackColor = Color.FromArgb(18, 22, 27);

            _trig = _map.DefaultTrigger;

            SetView(view);
        }

        /// <summary>Применить настройку вида: какие каналы в каких слотах, их цвета и размах.</summary>
        public void SetView(ScopeView view)
        {
            _view = view;

            for (int g = 0; g < ScopeView.Groups; g++)
                for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                {
                    _slotCh[g, s] = ScopeView.IndexOf(_map, view[g, s].Name);
                    _shown[g, s] = true;
                    _auto[g, s] = 1f;

                    /* Не нули: пока данных нет, подписи оси всё равно рисуются,
                     * а нулевой размах дал бы деление на ноль в Y(). */
                    _lo[g, s] = -1f;
                    _hi[g, s] = 1f;
                }

            Invalidate();
        }

        public ScopeView View { get { return _view; } }

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
         * С синхронизацией левый край окна ставится на переход выбранного канала
         * через ноль вверх. Точка привязки всегда в одной и той же фазе сети, и
         * картинка стоит, хотя данные продолжают идти.
         *
         * Плата за это - задержка. Левый край берётся не у самого свежего
         * отсчёта, а у последнего перехода, после которого набралось целое окно,
         * то есть картинка отстаёт максимум на период сети (20 мс). Для глаза
         * это незаметно, а для измерений есть заморозка.
         *----------------------------------------------------------------------*/

        private bool _sync = true;
        private bool _synced; /* нашли ли переход в этот раз - для подписи */

        /// <summary>Пускать развёртку по переходу канала запуска через ноль вверх.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Sync
        {
            get { return _sync; }
            set { _sync = value; Invalidate(); }
        }

        /// <summary>Канал, по которому идёт запуск развёртки.</summary>
        public int TrigChannel
        {
            get { return _trig; }
            set { _trig = value; Invalidate(); }
        }

        private int _trig;

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

        public bool IsVisibleSlot(int g, int s)
        {
            return _shown[g, s];
        }

        public void SetVisibleSlot(int g, int s, bool on)
        {
            _shown[g, s] = on;
            Invalidate();
        }

        /*-----------------------------------------------------------------------
         * Снимок кольца
         *----------------------------------------------------------------------*/

        /// <summary>Значение канала в снимке.</summary>
        private float Snap(int i, int c)
        {
            return _snapV[i * _ch + c];
        }

        /// <summary>Шаг между отсчётами в тактах, из кольца.</summary>
        private int _step = 1;

        /// <summary>
        /// Какие каналы кадр реально принёс; остальные - нули старой прошивки.
        /// Снимается под замком вместе со снимком кольца.
        /// </summary>
        private readonly bool[] _live;

        /// <summary>Виден ли слот: занят, не погашен и канал реально приходит.</summary>
        private bool Live(int g, int s)
        {
            int c = _slotCh[g, s];
            return c >= 0 && _live[c] && _shown[g, s];
        }

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
            // масштаб канала зависит от тарировки, и зашитый порог пришлось бы
            // менять вместе с ней.
            float peak = 0f;
            for (int a = 0; a < count; a++)
            {
                float v = Math.Abs(_ring.At(a, _trig));
                if (v > peak) peak = v;
            }

            if (peak < 1e-3f) return -1; /* сигнала нет - синхронизировать не по чему */

            float arm = -0.05f * peak;

            // Идём от старых отсчётов к новым и запоминаем последний переход:
            // нужен самый свежий, после которого ещё помещается целое окно.
            // Гистерезис обязателен - без него развёртка цеплялась бы за каждое
            // дрожание около нуля и картинка прыгала бы на полпериода.
            bool armed = false;
            int best = -1;

            for (int a = count - 1; a >= n - 1; --a)
            {
                float v = _ring.At(a, _trig);

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
                float cur = _ring.At(best, _trig);
                float prev = _ring.At(best + 1, _trig);
                float d = cur - prev;

                if (d > 0f) _trigFrac = Clamp(cur / d, 0f, 1f);
            }

            return best;
        }

        /*-----------------------------------------------------------------------
         * Поля групп
         *----------------------------------------------------------------------*/
        private const int MarginL = 52;
        private const int MarginR = 8;
        private const int MarginT = 8;
        private const int MarginB = 20;

        /// <summary>Зазор между группами.</summary>
        private const int GroupGap = 8;

        private readonly Rectangle[] _rect = new Rectangle[ScopeView.Groups];
        private readonly bool[] _used = new bool[ScopeView.Groups];

        /// <summary>
        /// Разложить высоту по группам. Пустая группа места не занимает: если
        /// вторая не используется, первая получает всё поле целиком.
        /// </summary>
        private void LayoutGroups()
        {
            int left = MarginL;
            int width = Math.Max(1, Width - MarginL - MarginR);
            int top = MarginT;
            int height = Math.Max(1, Height - MarginT - MarginB);

            int used = 0;
            for (int g = 0; g < ScopeView.Groups; g++)
            {
                _used[g] = _view.GroupUsed(g);
                if (_used[g]) used++;
            }

            if (used == 0) { _used[0] = true; used = 1; } /* нечего показывать - рисуем пустое поле */

            int each = (height - GroupGap * (used - 1)) / used;
            int y = top;

            for (int g = 0; g < ScopeView.Groups; g++)
            {
                if (!_used[g]) { _rect[g] = Rectangle.Empty; continue; }

                _rect[g] = new Rectangle(left, y, width, Math.Max(1, each));
                y += each + GroupGap;
            }
        }

        /// <summary>Всё поле графика - для мыши: ось X у групп общая.</summary>
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
         * пользуются одной и той же осью, иначе они разъедутся. Группы делят её
         * между собой.
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            LayoutGroups();

            int want;
            int n;

            lock (_ring.Gate)
            {
                /* Частота и шаг берутся из кольца здесь же, под замком: от них
                 * зависит, сколько отсчётов войдёт в окно. */
                _step = _ring.Step;
                _ring.CopyLive(_live);
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
                    _ring.CopyTo(age0 - i, _snapV, k * _ch);
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

            Rectangle all = PlotRect();

            if (n < 2)
            {
                for (int gr = 0; gr < ScopeView.Groups; gr++)
                    if (_used[gr]) DrawGrid(g, gr, _rect[gr]);

                TextRenderer.DrawText(g, "нет данных", Font, all,
                                      Color.FromArgb(120, 130, 140),
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            /* Масштаб по горизонтали задаётся ВЫБРАННЫМ окном, а не числом
             * набранных отсчётов. Иначе при потерях картинка ужималась бы,
             * подгоняя себя под ширину, и потеря снова оказалась бы незаметной. */
            _baseTick = _snapT[0];
            _xk = (float)(all.Width - 1) / Math.Max(1, want - 1);
            _x0 = all.Left + _trigFrac * _xk;
            _spanMs = want * 1000f * Math.Max(1, _step) / CtrlHz;

            for (int gr = 0; gr < ScopeView.Groups; gr++)
            {
                if (!_used[gr]) continue;

                Rectangle r = _rect[gr];

                /* Размах СНАЧАЛА, по всем показанным слотам, и только потом
                 * рисование: подписи оси берут границы первой кривой, и к их
                 * появлению те обязаны быть посчитаны. */
                for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                    if (Live(gr, s)) Range(gr, s, n, out _lo[gr, s], out _hi[gr, s]);

                DrawGrid(g, gr, r);

                g.SmoothingMode = SmoothingMode.AntiAlias;

                /* Сдвиг выносит последний отсчёт на долю такта за правый край.
                 * Проще обрезать по рамке, чем ужимать окно на один отсчёт. */
                g.SetClip(r);

                int legendY = r.Top + 4;

                for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                {
                    if (!Live(gr, s)) continue;

                    DrawChannel(g, r, gr, s, _lo[gr, s], _hi[gr, s], n);
                    DrawLegend(g, r, gr, s, _lo[gr, s], _hi[gr, s], ref legendY);
                }

                g.ResetClip();
                g.SmoothingMode = SmoothingMode.Default;
            }

            DrawTrigMark(g);
            DrawTimeAxis(g, all, n);
            DrawCursors(g, all, n);
        }

        /*-----------------------------------------------------------------------
         * Размах слота
         *----------------------------------------------------------------------*/

        /// <summary>
        /// Границы кривой. Заданы в настройке - берём как есть; не заданы -
        /// подбираем по окну, с округлением до 1/2/5 и симметрично нулю.
        ///
        /// Подобранный масштаб отпускается вниз только при двукратном запасе -
        /// иначе он пересчитывается на каждой перерисовке и картинка дышит.
        /// </summary>
        private void Range(int g, int s, int n, out float lo, out float hi)
        {
            ScopeSlot sl = _view[g, s];

            if (!sl.Auto) { lo = sl.Min; hi = sl.Max; return; }

            int c = _slotCh[g, s];

            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                float v = Math.Abs(Snap(i, c));
                if (v > peak) peak = v;
            }

            float want = Nice(peak);
            float cur = _auto[g, s];

            if (want > cur || want < cur * 0.5f) cur = want;
            _auto[g, s] = cur;

            lo = -cur;
            hi = cur;
        }

        private static float Nice(float v)
        {
            if (!(v > 0f)) return 1f;

            float e = (float)Math.Pow(10.0, Math.Floor(Math.Log10(v)));
            float m = v / e;
            float n = m <= 1f ? 1f : m <= 2f ? 2f : m <= 5f ? 5f : 10f;
            return n * e;
        }

        /// <summary>
        /// Значение -> пиксель внутри поля группы. За границами обрезается по
        /// рамке: уход за край видно по тому, что кривая легла на неё.
        /// </summary>
        private static float Y(Rectangle r, float v, float lo, float hi)
        {
            float t = (v - lo) / (hi - lo);

            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;

            return r.Bottom - t * (r.Height - 1);
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

        private void DrawCursors(Graphics g, Rectangle all, int n)
        {
            if (!_showCursor || _cursorIdx < 0) return;

            int i = Clamp(_cursorIdx, 0, n - 1);
            int m = _markIdx >= 0 ? Clamp(_markIdx, 0, n - 1) : -1;

            /* Черта идёт через ОБЕ группы: отсчёт под курсором один и тот же, и
             * разрывать его по полям значило бы предлагать сверять глазом две
             * разные вертикали. */
            if (m >= 0)
            {
                float xm = IdxToX(m);
                using (var pen = new Pen(MarkColor) { DashStyle = DashStyle.Dot })
                    foreach (Rectangle r in Used()) g.DrawLine(pen, xm, r.Top, xm, r.Bottom);
            }

            float x = IdxToX(i);
            using (var pen = new Pen(CurColor) { DashStyle = DashStyle.Dash })
                foreach (Rectangle r in Used()) g.DrawLine(pen, x, r.Top, x, r.Bottom);

            BuildRows(i, m);
            DrawReadout(g, all, x, m >= 0);
        }

        private IEnumerable<Rectangle> Used()
        {
            for (int g = 0; g < ScopeView.Groups; g++)
                if (_used[g]) yield return _rect[g];
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
                              m >= 0 ? Signed(ti - tm, "0.##") : "", DimColor));

            /* 1/Δt: так меряют период. Метку на переход через ноль, курсор на
             * следующий такой же - и частота видна без арифметики. */
            if (m >= 0 && m != i)
            {
                _rows.Add(new Row("1/Δt, Гц", "", "",
                                  (1000f / Math.Abs(ti - tm)).ToString("0.###"), DimColor));
            }

            for (int g = 0; g < ScopeView.Groups; g++)
                for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                {
                    if (!Live(g, s)) continue;

                    int c = _slotCh[g, s];
                    IniChannel ch = _map[c];
                    float vc = Snap(i, c);

                    /* Печатаем с точностью, ОБЪЯВЛЕННОЙ В .ini (поле format), а
                     * не по своей эвристике. В этом и смысл общего с Wscope
                     * формата: числа под курсором должны совпадать с тем, что
                     * Wscope показывает на той же записи. */
                    Color col = _view[g, s].Color;

                    if (m < 0)
                    {
                        _rows.Add(new Row(ch.Name, vc.ToString(ch.Format), "", "", col));
                        continue;
                    }

                    float vm = Snap(m, c);
                    _rows.Add(new Row(ch.Name, vc.ToString(ch.Format), vm.ToString(ch.Format),
                                      Signed(vc - vm, ch.Format), col));
                }
        }

        private static string Signed(int v)
        {
            return v > 0 ? "+" + v : v.ToString();
        }

        private static string Signed(float v, string fmt)
        {
            return (v > 0f ? "+" : "") + v.ToString(fmt);
        }

        /// <summary>
        /// Самое широкое число, которое вообще могут выдать показанные каналы:
        /// знак, пять разрядов целой части (беззнаковый код 16 бит даёт не
        /// больше 65535) и самое большое объявленное число знаков после запятой.
        ///
        /// Меряется по НЕМУ, а не по тому, что стоит в столбце сейчас. Иначе
        /// рамка дёргается на каждом переходе через разряд: 9,999 длиннее, чем
        /// 10,00, строка короче - и рамка на глазах меняет ширину и уезжает
        /// вбок, потому что прижата к правому краю.
        /// </summary>
        private string WidestNum()
        {
            int digits = 0;

            for (int g = 0; g < ScopeView.Groups; g++)
                for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                {
                    if (!Live(g, s)) continue;

                    int d = _map[_slotCh[g, s]].Digits;
                    if (d > digits) digits = d;
                }

            return "-00000" + (digits > 0 ? "." + new string('0', digits) : "");
        }

        private void DrawReadout(Graphics g, Rectangle all, float x, bool withMark)
        {
            /* Без метки столбец один, и шапка не нужна: подписывать нечего. */
            int cols = withMark ? 4 : 2;
            string[] head = { "", "курсор", "метка", "Δ" };
            Color[] headColor = { DimColor, CurColor, MarkColor, DimColor };

            const int Gap = 12, Pad = 8;

            // Ширины столбцов считаются заранее: числа выравниваются вправо, чтобы
            // разряды стояли под разрядами, а для этого нужна ширина столбца.
            int numW = TextRenderer.MeasureText(WidestNum(), Font).Width;

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
            int boxX = (x > all.Left + all.Width / 2) ? all.Left + 6 : all.Right - boxW - 6;
            int boxY = all.Top + 6;

            /* Легенда живёт в левом верхнем углу первой группы. Если рамка встала
             * туда же, опускаем её под легенду. */
            if (boxX < all.Left + 12) boxY = all.Top + 6 + VisibleCount(0) * 15 + 6;

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
        /// Метка запуска: уголок на левом краю той группы, где стоит канал
        /// запуска. Видно, что развёртка зацепилась, а не просто показывает
        /// хвост кольца.
        /// </summary>
        private void DrawTrigMark(Graphics g)
        {
            if (!_synced) return;

            /* Ищем группу, в которой показан канал запуска. Нет такой - ставим в
             * первую: синхронизация-то работает, и сказать об этом надо. */
            int gr = 0;
            Color col = Color.FromArgb(120, 130, 140);

            for (int gg = 0; gg < ScopeView.Groups && col.A == 255 && gr == 0; gg++)
                for (int s = 0; s < ScopeView.SlotsPerGroup; s++)
                    if (_used[gg] && Live(gg, s) && _slotCh[gg, s] == _trig)
                    {
                        gr = gg;
                        col = _view[gg, s].Color;
                        break;
                    }

            if (!_used[gr]) return;

            Rectangle r = _rect[gr];
            float midY = r.Top + r.Height * 0.5f;

            PointF[] tri =
            {
                new PointF(r.Left + 1, midY - 5f),
                new PointF(r.Left + 9, midY),
                new PointF(r.Left + 1, midY + 5f),
            };

            using (var b = new SolidBrush(col)) g.FillPolygon(b, tri);
        }

        private int VisibleCount(int g)
        {
            int k = 0;
            for (int s = 0; s < ScopeView.SlotsPerGroup; s++) if (Live(g, s)) k++;
            return k;
        }

        private void DrawChannel(Graphics g, Rectangle r, int gr, int s,
                                 float lo, float hi, int n)
        {
            int c = _slotCh[gr, s];

            using (var pen = new Pen(_view[gr, s].Color, 1f))
            {
                // Точек меньше, чем пикселей - рисуем настоящую полилинию:
                // огибающая здесь выродилась бы в набор отдельных точек и
                // потеряла форму.
                if (n <= r.Width)
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

                        pts[len++] = new PointF(IdxToX(i), Y(r, Snap(i, c), lo, hi));
                    }

                    Flush(g, pen, pts, run, len - run);
                    return;
                }

                float prevY = float.NaN;

                int col = 0;
                float mn = float.MaxValue, mx = float.MinValue;

                for (int i = 0; i <= n; i++)
                {
                    int cx = i < n ? (int)(IdxToX(i) - r.Left) : -1;

                    if (cx != col || i == n)
                    {
                        if (mn <= mx)
                        {
                            float x = r.Left + col;
                            float y0 = Y(r, mx, lo, hi);
                            float y1 = Y(r, mn, lo, hi);

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
                        mn = float.MaxValue;
                        mx = float.MinValue;
                    }

                    float v = Snap(i, c);
                    if (v < mn) mn = v;
                    if (v > mx) mx = v;
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

        private static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private void DrawGrid(Graphics g, int gr, Rectangle r)
        {
            using (var thin = new Pen(Color.FromArgb(38, 46, 54)))
            using (var axis = new Pen(Color.FromArgb(70, 82, 94)))
            {
                for (int i = 0; i <= 8; i++)
                    g.DrawLine(thin, r.Left, r.Top + r.Height * i / 8, r.Right, r.Top + r.Height * i / 8);

                for (int i = 0; i <= 10; i++)
                    g.DrawLine(thin, r.Left + r.Width * i / 10, r.Top, r.Left + r.Width * i / 10, r.Bottom);

                g.DrawRectangle(axis, r);

                /* Подписи оси - по ПЕРВОЙ показанной кривой группы, её же цветом.
                 * У каждой кривой свой размах, и общих чисел тут быть не может;
                 * без цвета было бы непонятно, к чему они относятся. Остальные
                 * читают свой размах в легенде. */
                int first = -1;
                for (int s = 0; s < ScopeView.SlotsPerGroup && first < 0; s++)
                    if (Live(gr, s)) first = s;

                if (first < 0) return;

                /* Границы берём ГОТОВЫЕ. Здесь стоял вызов Range с нулём
                 * отсчётов - он не только возвращал ±1 вместо настоящих чисел,
                 * но и ЗАТИРАЛ запомненный масштаб: при пустом окне поиск
                 * размаха не выполняется, Nice(0) даёт единицу, и правило
                 * «отпускать вниз только при двукратном запасе» срабатывало
                 * против самого себя. Кривые рисовались верно - их размах
                 * пересчитывался следующей же строкой, - а подписи оси врали, и
                 * сдерживания масштаба не было вовсе. */
                float lo = _lo[gr, first];
                float hi = _hi[gr, first];

                /* Нулевую линию выделяем, если ноль внутри размаха: по ней видно
                 * знак, а на несимметричной шкале середина поля нулём не является. */
                if (lo < 0f && hi > 0f)
                {
                    float zy = Y(r, 0f, lo, hi);
                    g.DrawLine(axis, r.Left, zy, r.Right, zy);
                }

                IniChannel ch = _map[_slotCh[gr, first]];
                Color col = _view[gr, first].Color;

                TextRenderer.DrawText(g, hi.ToString(ch.Format), Font,
                                      new Point(2, r.Top - 2), col);
                TextRenderer.DrawText(g, lo.ToString(ch.Format), Font,
                                      new Point(2, r.Bottom - 15), col);
            }
        }

        private void DrawLegend(Graphics g, Rectangle r, int gr, int s,
                                float lo, float hi, ref int y)
        {
            IniChannel ch = _map[_slotCh[gr, s]];
            Color col = _view[gr, s].Color;

            var box = new Rectangle(r.Left + 8, y + 6, 10, 3);
            using (var b = new SolidBrush(col)) g.FillRectangle(b, box);

            /* Размах рядом с именем: на двух группах по шесть кривых иначе не
             * понять, в каких границах нарисована каждая. Единиц в .ini нет -
             * поля для них там не предусмотрено. */
            string text = ch.Name + "  " + lo.ToString(ch.Format) + "…" + hi.ToString(ch.Format);
            if (_view[gr, s].Auto) text += " (авто)";

            TextRenderer.DrawText(g, text, Font, new Point(r.Left + 22, y), col);
            y += 15;
        }

        private void DrawTimeAxis(Graphics g, Rectangle all, int n)
        {
            var grey = Color.FromArgb(120, 130, 140);

            /* Правая подпись прижимается к краю выравниванием, а не отступом в
             * фиксированные 60 px: при крупном шрифте или значении «1000 мс»
             * такой отступ не спасает, и хвост уезжает за край. */
            var right = new Rectangle(all.Left, all.Bottom + 3, all.Width, MarginB - 3);

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
                text = "синхр. " + _map[_trig].Name + " ↑";
                color = Color.FromArgb(150, 180, 210);
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

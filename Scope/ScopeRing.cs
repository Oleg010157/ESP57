using System;
using System.Diagnostics;
using Peak.Can.Basic;

namespace ICDIBasic
{
    /*
     * Кольцо отсчётов быстрой ленты.
     *
     * Живёт между приёмным потоком CAN и окном осциллографа, и поэтому делает
     * ровно три вещи, которые нельзя делать в окне.
     *
     * 1. ПОПОЛНЯЕТСЯ ИЗ ПОТОКА ПРИЁМА, без Invoke. Кадры идут по одному на такт
     *    управления, это до пяти тысяч в секунду; маршалить каждый в поток окна
     *    значит забить очередь сообщений и получить неотзывчивое окно на ровном
     *    месте. Окно само копирует то, что нужно для картинки, по таймеру.
     *
     * 2. РАЗВОРАЧИВАЕТ ТАКТ. sineTick в прошивке - uint16_t, он переполняется
     *    каждые 65536 тактов, то есть раз в тринадцать секунд на 5 кГц. Ось
     *    времени и поиск разрывов требуют монотонного счётчика, поэтому такт
     *    накапливается в 32 бита по беззнаковой разности соседних значений.
     *    Пауза длиннее 65536 тактов развернётся неверно - но это уже после
     *    долгого молчания, и на картинке будет честный разрыв.
     *
     * 3. МЕРЯЕТ ЧАСТОТУ ТАКТА. Ось времени без неё не построить, а по CAN
     *    частота не передаётся: номинал 5 кГц, но в слежении за сетью TBPRD
     *    переписывается каждый период сети. Считается по тактам и стенным
     *    часам - Δтакт / Δвремя, - то есть по тому единственному, что в ленте
     *    есть.
     */
    internal sealed class ScopeRing
    {
        /// <summary>Ёмкость кольца - секунда при 5 кГц. Столько же, сколько самое широкое окно.</summary>
        public const int Capacity = 5000;

        /// <summary>
        /// Номинальная частота такта управления, Гц. Прерывание ADCB INT1,
        /// my_Files/grid_tie.h. Настоящая измеряется по такту, см. MeasureRate.
        /// </summary>
        public const float NominalHz = 5000f;

        private readonly IniChannelMap _map;

        /// <summary>Каналов в шаблоне. Разное число у разных кадров, отсюда и не const.</summary>
        private readonly int _n;

        public ScopeRing(IniChannelMap map)
        {
            _map = map;
            _n = map.Count;

            _v = new float[Capacity * _n];
            _one = new float[_n];

            _live = new bool[_n];
            for (int i = 0; i < _n; i++) _live[i] = true;
        }

        /// <summary>Карта каналов, по которой разобран кадр.</summary>
        public IniChannelMap Map { get { return _map; } }

        /// <summary>
        /// Замок кольца. Окно берёт его на время копирования снимка: приёмный
        /// поток не должен ждать GDI+, а картинка не должна собираться из
        /// половины старых и половины новых отсчётов.
        /// </summary>
        public object Gate { get { return _lock; } }

        private readonly object _lock = new object();

        /* Плоские массивы вместо массива структур. Отсчёт - это пятнадцать
         * чисел, и обращаются к ним по НОМЕРУ КАНАЛА: канал соответствует слову
         * кадра, и в таком виде таблица каналов остаётся данными, а не набором
         * делегатов к полям структуры. */
        private readonly float[] _v;
        private readonly uint[] _t = new uint[Capacity];

        private int _head;
        private int _count;

        /// <summary>Буфер разбора одного кадра. Под _lock, поэтому один на всех.</summary>
        private readonly float[] _one;

        private ushort _last16;
        private uint _tick32;
        private bool _have;

        private long _received;
        private long _gaps;
        private readonly bool[] _live;

        private bool _frozen;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private bool _rateHave;
        private long _rateMs;
        private uint _rateTick;
        private float _hz = NominalHz;

        /*-------------------------------------------------------------------
         * Всё ниже читается ПОД Gate. Счётчики - long, и на 32-битной машине
         * чтение без замка не атомарно.
         *------------------------------------------------------------------*/

        /// <summary>Сколько отсчётов лежит в кольце.</summary>
        public int Count { get { return _count; } }

        /// <summary>Отсчётов принято с момента сброса.</summary>
        public long Received { get { return _received; } }

        /// <summary>
        /// Разрывов в счётчике такта. Это НЕ то же самое, что пропуски,
        /// известные прошивке: она считает их в telDrop, но telDrop по CAN не
        /// едет и живёт только в отладчике. Зато прошивка сама и говорит, зачем
        /// этот счётчик здесь: telDrop нулевой, а в ленте дырки - значит теряет
        /// адаптер или Motor_Control (my_Files/1ph_Sin.c).
        /// </summary>
        public long Gaps { get { return _gaps; } }

        /// <summary>
        /// Самый большой разрыв, в тактах. Различает две совершенно разные беды,
        /// которые по одному числу разрывов не отличить:
        ///
        ///   макс 2      - пропуски по одному такту. Прошивка не успела отдать
        ///                 кадр в занятый буфер; при 0.4 % это кадр 140 раз в
        ///                 50 мс, см. grid_tie.txt 6.2;
        ///   макс сотни  - приёмник проспал пачку. Переполнилась очередь
        ///                 адаптера или встало приложение.
        /// </summary>
        public int GapMax { get { return _gapMax; } }

        private int _gapMax;

        /// <summary>
        /// На шине идёт кадр, который этим шаблоном не разобрать - он длиннее.
        /// Так бывает, когда «F2 Кадр» переключили на ходу: имя сменилось, а
        /// команда прибору не ушла, и он продолжает слать прежний кадр.
        /// </summary>
        public bool WrongFrame { get { return _wrongFrame; } }

        private bool _wrongFrame;

        /// <summary>
        /// Пришёл ли канал на самом деле. Со старой прошивкой кадр приходит
        /// двенадцатью словами, и последние каналы существуют только как нули.
        ///
        /// Поканально, а не числом: вычисляемые каналы стоят в карте ПОСЛЕ
        /// измеряемых, и «живые идут подряд от начала» не выполняется.
        /// </summary>
        public bool IsLive(int ch)
        {
            return _live[ch];
        }

        /// <summary>Скопировать признаки под замком - окну для одной перерисовки.</summary>
        public void CopyLive(bool[] dst)
        {
            Array.Copy(_live, dst, _n);
        }

        /// <summary>Сколько каналов живо - для строки состояния.</summary>
        public int LiveCount
        {
            get
            {
                int n = 0;
                foreach (bool b in _live) if (b) n++;
                return n;
            }
        }

        /// <summary>Измеренная частота такта управления, Гц.</summary>
        public float Hz { get { return _hz; } }

        /// <summary>Последний принятый такт, развёрнутый в 32 бита.</summary>
        public uint Tick { get { return _tick32; } }

        /// <summary>
        /// Шаг между отсчётами в тактах. Прореживание telDiv в прошивке
        /// ОБЪЯВЛЕНО, НО НЕ РЕАЛИЗОВАНО - строки, которая бы его читала, в коде
        /// нет, - поэтому шаг всегда единица. Оставлен отдельным понятием: если
        /// telDiv однажды заработает, менять придётся здесь, а не в рисовании.
        /// </summary>
        public int Step { get { return 1; } }

        /// <summary>Значение канала по «возрасту»: 0 - самый свежий отсчёт.</summary>
        public float At(int age, int ch)
        {
            int i = (_head - 1 - age + 2 * Capacity) % Capacity;
            return _v[i * _n + ch];
        }

        /// <summary>Такт отсчёта по тому же «возрасту».</summary>
        public uint TickAt(int age)
        {
            return _t[(_head - 1 - age + 2 * Capacity) % Capacity];
        }

        /// <summary>Скопировать отсчёт в приёмник окна.</summary>
        public void CopyTo(int age, float[] dst, int at)
        {
            int i = (_head - 1 - age + 2 * Capacity) % Capacity;
            Array.Copy(_v, i * _n, dst, at, _n);
        }

        /*-------------------------------------------------------------------
         * Заморозка и сброс - из потока окна.
         *------------------------------------------------------------------*/

        /// <summary>Картинка стоит: кольцо не пополняется, счётчики идут.</summary>
        public bool Frozen
        {
            get { lock (_lock) return _frozen; }
            set { lock (_lock) _frozen = value; }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _head = 0;
                _count = 0;
                _have = false;
                _received = 0;
                _gaps = 0;
                _gapMax = 0;
                _rateHave = false;
            }
        }

        /*-------------------------------------------------------------------
         * Приём. Вызывается ИЗ ПОТОКА CAN.
         *------------------------------------------------------------------*/

        /// <summary>
        /// Положить кадр 103 в кольцо. Ничего, кроме разбора и копирования,
        /// здесь быть не должно.
        /// </summary>
        public void Append(TPCANMsgFD msg)
        {
            lock (_lock)
            {
                /* Кадр длиннее, чем знает шаблон, - значит это не тот кадр.
                 * Разбирать его нашей картой нельзя: получатся чужие числа под
                 * правильными именами. Молчим и говорим об этом окну. */
                if (!_map.LengthMatches(msg.DLC)) { _wrongFrame = true; return; }

                _wrongFrame = false;

                _map.FillLive(msg.DLC, _live);

                ushort t16 = _map.Decode(msg.DATA, msg.DLC, _one, 0);

                if (!_have)
                {
                    _tick32 = t16;
                    _have = true;
                }
                else
                {
                    // Беззнаковая разность: переполнение uint16 она учитывает
                    // сама, и это единственный способ считать её правильно.
                    ushort d = (ushort)(t16 - _last16);

                    // Такт не сдвинулся - это тот же самый отсчёт, пришедший
                    // дважды. Сохранить его значит поставить два отсчёта на
                    // одно место оси времени; ни считать, ни рисовать его не
                    // надо.
                    if (d == 0) return;

                    _tick32 += d;

                    // Пропуск. Прошивка молча пропускает такт, если буфер
                    // передачи ещё занят предыдущим кадром, а в ленте пропуск
                    // неотличим от настоящей паузы в сигнале - видно его только
                    // по дырке в такте.
                    if (d != Step)
                    {
                        _gaps++;
                        if (d > _gapMax) _gapMax = d;
                    }
                }

                _last16 = t16;
                _received++;

                MeasureRate();

                // В заморозке кольцо не пополняется, а счётчики - пополняются:
                // связь никуда не делась, и разрывы надо считать и тогда, когда
                // на них никто не смотрит.
                if (_frozen) return;

                Array.Copy(_one, 0, _v, _head * _n, _n);
                _t[_head] = _tick32;

                _head = (_head + 1) % Capacity;
                if (_count < Capacity) _count++;
            }
        }

        /// <summary>
        /// Частота такта по тактам и стенным часам. Окно в секунду: короче -
        /// и в числе видна дискретность часов, длиннее - и оно не успевает за
        /// перестройкой TBPRD в слежении за сетью.
        /// </summary>
        private void MeasureRate()
        {
            long now = _clock.ElapsedMilliseconds;

            if (!_rateHave)
            {
                _rateHave = true;
                _rateMs = now;
                _rateTick = _tick32;
                return;
            }

            long dt = now - _rateMs;
            if (dt < 1000) return;

            float hz = (_tick32 - _rateTick) * 1000f / dt;

            _rateMs = now;
            _rateTick = _tick32;

            // Отбрасываем заведомую чушь. Она получается на первом окне после
            // долгого молчания: развёрнутый такт там мог перепрыгнуть через
            // переполнение, и разность тактов не соответствует времени.
            if (hz < 100f || hz > 200000f) return;

            // Сглаживание. Без него число в строке состояния дёргается в
            // последнем знаке на каждом окне, и понять, стоит ли оно на 5 кГц,
            // невозможно.
            _hz = 0.7f * _hz + 0.3f * hz;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ICDIBasic
{
    /// <summary>Откуда канал берёт значение.</summary>
    internal enum ChKind
    {
        /// <summary>Слово кадра: код * K + A.</summary>
        Measured,

        /// <summary>Постоянная из шаблона, вида <c>Null = @0,0</c>.</summary>
        Constant,

        /// <summary>Младшие разряды другого канала, вида <c>tick256 = @0,[tick] and 255</c>.</summary>
        Mask,
    }

    /// <summary>
    /// Один канал, как он объявлен в .ini Wscope.
    /// </summary>
    internal sealed class IniChannel
    {
        /// <summary>Измеряемый канал: слово кадра.</summary>
        public IniChannel(string name, int word, bool signed, int digits,
                          float k, float a, Color color)
        {
            Kind = ChKind.Measured;
            Name = name;
            Word = word;
            Signed = signed;
            Digits = digits;
            K = k;
            A = a;
            Color = color;
        }

        /// <summary>Вычисляемый канал той формы, которую мы умеем считать.</summary>
        public IniChannel(string name, ChKind kind, int digits, Color color,
                          float value, int src, int mask)
        {
            Kind = kind;
            Name = name;
            Digits = digits;
            Color = color;
            Value = value;
            Src = src;
            Mask = mask;

            K = 1f;
            Word = src >= 0 ? -1 : -1;
        }

        public ChKind Kind { get; private set; }

        /// <summary>Имя из .ini - то же, что в Wscope и в прошивке.</summary>
        public string Name { get; private set; }

        /// <summary>
        /// Номер слова в кадре. Выведен из позиции байт, см. IniChannelMap.
        /// У вычисляемых слова нет, там -1.
        /// </summary>
        public int Word { get; private set; }

        public bool Signed { get; private set; }

        /// <summary>Знаков после запятой, поле format. По нему печатается таблица курсора.</summary>
        public int Digits { get; private set; }

        /// <summary>значение = код * K + A. У вычисляемых K = 1, A = 0.</summary>
        public float K { get; private set; }

        public float A { get; private set; }

        public Color Color { get; private set; }

        /// <summary>Постоянная - для <see cref="ChKind.Constant"/>.</summary>
        public float Value { get; private set; }

        /// <summary>Номер канала-источника - для <see cref="ChKind.Mask"/>.</summary>
        public int Src { get; private set; }

        /// <summary>Маска - для <see cref="ChKind.Mask"/>.</summary>
        public int Mask { get; private set; }

        public bool Calculated { get { return Kind != ChKind.Measured; } }

        /// <summary>Формат для ToString: столько знаков, сколько объявлено в файле.</summary>
        public string Format
        {
            get { return Digits <= 0 ? "0" : "0." + new string('0', Digits); }
        }
    }

    /*
     * Перечень и формат каналов - из того же .ini, что читает Wscope.
     *
     * ЗАЧЕМ. Раскладку кадра обязаны знать несколько мест: прошивка, разбор в
     * Kadr60.cs и настройки Wscope. Имён внутри кадра нет, читается он по номеру
     * слова, и расхождение проявляется не ошибкой, а молча неверными числами на
     * графике. Пока у осциллографа была своя зашитая таблица, он был ещё одним
     * таким местом; теперь он читает тот же файл, что Wscope, и разойтись с ним
     * физически не может.
     *
     * ПОЗИЦИИ БАЙТ В .ini - ЭТО ФАЙЛ, А НЕ ШИНА. Самая важная тонкость здесь.
     * .ini описывает раскладку в записанном .spl, а рекордер хранит слова
     * СТАРШИМ БАЙТОМ ВПЕРЁД: ASt_MainF.cs меняет байты местами перед
     * Buffer.BlockCopy. Живой осциллограф читает кадр CAN, а он младшим байтом
     * вперёд. Взять позиции байт напрямую - значит получить каждый канал
     * перевёрнутым, то есть правдоподобный мусор.
     *
     * Поэтому из позиции выводится НОМЕР СЛОВА:
     *
     *     слово i = (nHi - 1) / 2,   код = (d[2i+1] << 8) | d[2i]
     *
     * Во всех шаблонах проекта пары идут строго (2i+1, 2i+2) с нечётным nHi -
     * проверено, исключений нет. Нерегулярную пару считаем ошибкой файла, а не
     * пытаемся угадать.
     *
     * ВЫЧИСЛЯЕМЫЕ КАНАЛЫ (@) - ТОЛЬКО ДВУХ ПРОСТЕЙШИХ ВИДОВ, и это граница,
     * проведённая намеренно.
     *
     *     Null    = @0,0                 постоянная
     *     tick256 = @0,[tick] and 255    младшие разряды другого канала
     *     isr256  = @0,[IsrTic] and $FF  то же, маска шестнадцатеричная
     *
     * Их видно по тексту выражения целиком, угадывать нечего - поэтому считаем.
     * Всё прочее в языке Wscope разбирать не беремся: там состояние (БИХ-фильтры,
     * накопители, бегущий min/max), история отсчётов [x~-1], условия if(a;b;c) и
     * номер отсчёта (@@). Посчитать их наполовину верно значит нарисовать
     * правдоподобную ложь - худший вид ошибки измерения. Такие каналы остаются в
     * списке Calculated: окно показывает их красным и объясняет, что их считает
     * только Wscope.
     *
     * В ССЫЛКАХ ВИДА [x] БЕРЁТСЯ СЫРОЙ КОД, а не физика - так устроен язык
     * Wscope ([`x] дало бы значение после K и A). Для маски это единственно
     * верно: младший байт счётчика есть свойство кода, а не величины.
     */
    internal sealed class IniChannelMap
    {
        /// <summary>Секции, в которых лежат каналы. [New] и прочие игнорируем.</summary>
        private static readonly string[] Sections = { "parleft", "parright", "parlist" };

        /// <summary>Имена, которыми в шаблонах проекта называют счётчик такта.</summary>
        private static readonly string[] TickNames = { "tick", "isrtic", "isr", "k60cnt" };

        private readonly List<IniChannel> _ch = new List<IniChannel>();

        private IniChannelMap() { }

        public int Count { get { return _ch.Count; } }

        public IniChannel this[int i] { get { return _ch[i]; } }

        /// <summary>Имя шаблона без расширения.</summary>
        public string Template { get; private set; }

        /// <summary>
        /// Сколько вычисляемых каналов пропущено - показываем, а не умалчиваем.
        ///
        /// Номер отсчёта (@@, @@@) сюда НЕ входит. Это служебная величина самого
        /// Wscope, она возникает только при проигрывании файла, и на живой ленте
        /// ей просто неоткуда взяться - пропуск тут ничего не значит. А число
        /// стоит в строке состояния ради другого: чтобы было видно, что в
        /// шаблоне есть кривая, которую рисует только Wscope. Со служебными в
        /// нём оно горело всегда и приучало себя не замечать.
        /// </summary>
        public int Skipped { get { return _calc.Count - _service; } }

        /// <summary>Сколько из Calculated - номер отсчёта, см. Skipped.</summary>
        private int _service;

        /// <summary>
        /// Имена вычисляемых каналов, в порядке файла. Мы их не считаем, но в
        /// списке каналов показываем - красным, как это делает Wscope: оператор
        /// должен видеть, что в шаблоне они есть, и не искать их глазами зря.
        /// </summary>
        public IList<string> Calculated { get { return _calc; } }

        private readonly List<string> _calc = new List<string>();

        /// <summary>Индекс канала такта: с наибольшим номером слова.</summary>
        public int TickIndex { get; private set; }

        /// <summary>Сколько слов занимает кадр по этому шаблону, включая такт.</summary>
        public int Words { get { return _ch[TickIndex].Word + 1; } }

        /// <summary>Предупреждение, с которым работать можно. Пусто - всё чисто.</summary>
        public string Warning { get; private set; }

        /// <summary>
        /// Канал, по которому разумно пускать развёртку: напряжение сети, если
        /// такое имя есть. Иначе первый канал, кроме такта - оператор поменяет.
        /// </summary>
        public int DefaultTrigger
        {
            get
            {
                string[] want = { "ugrid", "u220", "va" };

                foreach (string w in want)
                    for (int i = 0; i < _ch.Count; i++)
                        if (i != TickIndex && _ch[i].Name.ToLowerInvariant() == w) return i;

                return TickIndex == 0 && _ch.Count > 1 ? 1 : 0;
            }
        }

        /*-------------------------------------------------------------------
         * Загрузка
         *------------------------------------------------------------------*/

        /// <summary>Каталог шаблонов. Тот же, из которого читает Wscope.</summary>
        public static string Dir
        {
            get { return Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "Wscope")); }
        }

        /// <summary>
        /// Прочитать шаблон по имени кадра. Имя файла строится из имени набора -
        /// так предписывает шапка самого F3_convertor.ini. Ошибку возвращаем
        /// текстом, а не исключением: показать её надо в окне.
        /// </summary>
        public static IniChannelMap Load(string frame, out string error)
        {
            return Load(frame, Dir, out error);
        }

        /// <summary>
        /// То же, но с явным каталогом. Нужно офлайновому прогону: у него
        /// Application.StartupPath указывает на себя, а не на bin/Debug.
        /// </summary>
        public static IniChannelMap Load(string frame, string dir, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(frame))
            {
                /* Не «не выбран»: в поле «F2 Кадр» имя стоит, но f2kadrName
                 * ставит TapeStart, а он молчит без связи с прибором. Окно
                 * скажет это понятнее, у него есть место на объяснение. */
                error = "лента не запущена";
                return null;
            }

            string path = Path.Combine(dir, frame + ".ini");

            if (!File.Exists(path))
            {
                // Это не теоретический случай: F1_invertor.ini в bin/Wscope
                // просто отсутствует.
                error = "нет шаблона " + frame + ".ini";
                return null;
            }

            try
            {
                return Parse(ReadLines(path), frame, out error);
            }
            catch (IOException ex)
            {
                error = "шаблон не читается: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Текст файла. Кодировка определяется ПО СОДЕРЖИМОМУ, а не по BOM.
        ///
        /// В папке лежат шаблоны всех трёх видов: F3_convertor.ini в UTF-8 без
        /// BOM, F3_invertor.ini и большинство прочих в cp1251, встречается и
        /// чистый ASCII. BOM нет ни у одного, так что отличить их можно только
        /// попыткой разбора.
        ///
        /// Приём простой и надёжный: русский текст в cp1251 почти никогда не
        /// складывается в правильные последовательности UTF-8 - у кириллицы там
        /// байты 0xC0..0xFF, а продолжающий байт UTF-8 обязан быть 0x80..0xBF.
        /// Поэтому строгий разбор UTF-8 на файле cp1251 бросает исключение, и мы
        /// честно переходим к cp1251. Чистый ASCII проходит как UTF-8 и даёт тот
        /// же результат.
        ///
        /// Имена каналов везде ASCII, так что на разбор кодировка почти не
        /// влияет. Но мусор вместо текста в комментариях - это мусор в глазах
        /// того, кто полезет разбираться.
        /// </summary>
        internal static string ReadText(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
                return new UTF8Encoding(false).GetString(b, 3, b.Length - 3);

            try
            {
                return new UTF8Encoding(false, true).GetString(b);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(1251).GetString(b);
            }
        }

        private static string[] ReadLines(string path)
        {
            return ReadText(File.ReadAllBytes(path))
                   .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        }

        private static IniChannelMap Parse(string[] lines, string frame, out string error)
        {
            error = null;

            var map = new IniChannelMap();
            map.Template = frame;

            bool inSection = false;
            var seen = new Dictionary<int, string>();
            var calc = new List<string[]>();

            foreach (string raw in lines)
            {
                string s = raw;

                /* Комментарий отрезаем первым делом: в нём бывает и запятая, и
                 * знак равенства, и кириллица. */
                int c = s.IndexOf(';');
                if (c >= 0) s = s.Substring(0, c);

                s = s.Trim();
                if (s.Length == 0) continue;

                if (s[0] == '[')
                {
                    inSection = Array.IndexOf(Sections, s.Trim('[', ']').ToLowerInvariant()) >= 0;
                    continue;
                }

                if (!inSection) continue;

                int eq = s.IndexOf('=');
                if (eq <= 0) continue;

                string name = s.Substring(0, eq).Trim();
                string val = s.Substring(eq + 1).Trim();

                if (name.Length == 0 || val.Length == 0) continue;

                /* Вычисляемый канал. Откладываем: разобрать его можно только
                 * после измеряемых - маске нужен номер канала-источника. */
                if (val[0] == '@') { calc.Add(new string[] { name, val }); continue; }

                IniChannel ch = ParseChannel(name, val);
                if (ch == null) continue;

                if (seen.ContainsKey(ch.Word))
                {
                    error = "в шаблоне " + frame + ".ini слово " + ch.Word +
                            " занято дважды: " + seen[ch.Word] + " и " + ch.Name;
                    return null;
                }

                seen[ch.Word] = ch.Name;
                map._ch.Add(ch);
            }

            if (map._ch.Count < 2)
            {
                error = "в шаблоне " + frame + ".ini нет измеряемых каналов";
                return null;
            }

            /* Такт - канал с наибольшим номером слова. Прошивка этого прямо
             * требует: величина, способная повториться у соседних отсчётов, на
             * последнем месте заставила бы запись стоять. */
            int last = 0;
            for (int i = 1; i < map._ch.Count; i++)
                if (map._ch[i].Word > map._ch[last].Word) last = i;

            map.TickIndex = last;

            /* Имя сверяем, но не упираемся: шаблон может назвать счётчик иначе,
             * и это повод предупредить, а не отказаться работать. */
            string tick = map._ch[last].Name.ToLowerInvariant();
            if (Array.IndexOf(TickNames, tick) < 0)
                map.Warning = "последнее слово называется «" + map._ch[last].Name +
                              "» - если это не счётчик такта, ось времени неверна";

            /* Вычисляемые - последними, когда измеряемые уже на местах: маске
             * нужен номер канала-источника, и он обязан быть МЕНЬШЕ её
             * собственного, иначе порядок вычисления в Decode не сойдётся. */
            map.AddCalculated(calc);

            return map;
        }

        /// <summary>
        /// Разобрать вычисляемые. Что умеем - дописываем настоящими каналами,
        /// что нет - в список Calculated, окно покажет их красным.
        /// </summary>
        private void AddCalculated(List<string[]> calc)
        {
            foreach (string[] e in calc)
            {
                IniChannel ch = ParseCalculated(e[0], e[1]);

                if (ch == null)
                {
                    /* В список красных идёт и номер отсчёта - в окне «Каналы» он
                     * есть и в Wscope, - но в счётчик пропусков нет. */
                    _calc.Add(e[0]);
                    if (e[1].Trim().StartsWith("@@")) _service++;
                    continue;
                }

                _ch.Add(ch);
            }
        }

        /// <summary>
        /// Правая часть вычисляемого: <c>@[^]format,выражение</c>.
        ///
        /// Понимаем ровно два выражения - постоянную и маску другого канала.
        /// Всё остальное возвращает null, и канал уходит в красные: см. шапку о
        /// том, почему половинчатый разбор здесь хуже отказа.
        /// </summary>
        private IniChannel ParseCalculated(string name, string val)
        {
            int comma = val.IndexOf(',');
            if (comma < 0) return null; /* @@ и @@@ - номер отсчёта, не наше */

            string fmt = val.Substring(1, comma - 1).Trim();
            if (fmt.StartsWith("^")) fmt = fmt.Substring(1).Trim();

            int digits;
            if (!int.TryParse(fmt, NumberStyles.Integer, CultureInfo.InvariantCulture, out digits))
                return null;

            string e = val.Substring(comma + 1).Trim();
            if (e.Length == 0) return null;

            /* Постоянная: Null = @0,0 */
            float v;
            if (float.TryParse(e, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return new IniChannel(name, ChKind.Constant, digits, Color.Empty, v, -1, 0);

            /* Маска: tick256 = @0,[tick] and 255 */
            string src;
            int mask;
            if (!TryMask(e, out src, out mask)) return null;

            int si = -1;
            for (int i = 0; i < _ch.Count; i++)
                if (string.Equals(_ch[i].Name, src, StringComparison.OrdinalIgnoreCase)) { si = i; break; }

            /* Источник должен быть измеряемым и уже разобранным: цепочки
             * вычисляемых друг из друга мы не раскручиваем. */
            if (si < 0 || _ch[si].Calculated) return null;

            return new IniChannel(name, ChKind.Mask, digits, Color.Empty, 0f, si, mask);
        }

        /// <summary>
        /// <c>[имя] and число</c>, число десятичное или шестнадцатеричное с $.
        /// Скобки вокруг допускаются: в шаблонах пишут и так, и так.
        /// </summary>
        private static bool TryMask(string e, out string src, out int mask)
        {
            src = null;
            mask = 0;

            e = e.Trim();
            if (e.StartsWith("(") && e.EndsWith(")")) e = e.Substring(1, e.Length - 2).Trim();

            int lb = e.IndexOf('[');
            int rb = e.IndexOf(']');
            if (lb != 0 || rb < 2) return false;

            src = e.Substring(1, rb - 1).Trim();

            /* Обратная кавычка значила бы «после K и A», а маска имеет смысл
             * только над сырым кодом - такое выражение не наше. */
            if (src.StartsWith("`")) return false;

            string rest = e.Substring(rb + 1).Trim();

            if (!rest.StartsWith("and", StringComparison.OrdinalIgnoreCase)) return false;

            rest = rest.Substring(3).Trim();
            if (rest.Length == 0) return false;

            if (rest[0] == '$')
                return int.TryParse(rest.Substring(1), NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out mask);

            return int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out mask);
        }

        /// <summary>
        /// Разобрать правую часть: <c>[^]format,K,A,nHi,nLo[,color]</c>.
        /// Негодную строку молча пропускаем - в шаблонах попадается всякое.
        /// </summary>
        private static IniChannel ParseChannel(string name, string val)
        {
            string[] p = val.Split(',');
            if (p.Length < 5) return null;

            string fmt = p[0].Trim();

            bool signed = fmt.StartsWith("^");
            if (signed) fmt = fmt.Substring(1).Trim();

            int digits;
            float k, a;
            int nHi, nLo;

            if (!int.TryParse(fmt, NumberStyles.Integer, CultureInfo.InvariantCulture, out digits)) return null;
            if (!TryFloat(p[1], out k)) return null;
            if (!TryFloat(p[2], out a)) return null;
            if (!int.TryParse(p[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out nHi)) return null;
            if (!int.TryParse(p[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out nLo)) return null;

            /* Позиция байт - 1-based, старший байт первым, и описывает ФАЙЛ.
             * Отсюда номер слова; нерегулярную пару не угадываем. */
            if (nHi < 1 || nHi % 2 == 0 || nLo != nHi + 1) return null;

            int word = (nHi - 1) / 2;

            return new IniChannel(name, word, signed, digits, k, a, Palette(word));
        }

        private static bool TryFloat(string s, out float v)
        {
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        /*-------------------------------------------------------------------
         * Разбор кадра
         *------------------------------------------------------------------*/

        /// <summary>
        /// Какие каналы кадр реально принёс. Со старой прошивкой слов меньше, и
        /// последние каналы существуют только как нули - окно их гасит и
        /// отключает кнопки, чтобы плоскую линию не прочитали как измерение.
        ///
        /// Массивом, а не счётчиком: вычисляемые каналы дописаны ПОСЛЕ
        /// измеряемых, и «живые идут подряд от начала» больше не выполняется -
        /// постоянная жива всегда, а маска настолько, насколько жив её источник.
        /// </summary>
        public void FillLive(byte dlc, bool[] dst)
        {
            int words = DlcWords(dlc);

            for (int i = 0; i < _ch.Count; i++)
            {
                IniChannel ch = _ch[i];

                dst[i] = ch.Kind == ChKind.Constant ? true
                       : ch.Kind == ChKind.Mask ? dst[ch.Src] /* Src < i всегда */
                       : ch.Word < words;
            }
        }

        /// <summary>Сколько каналов живо - для строки состояния.</summary>
        public int LiveCount(byte dlc)
        {
            var live = new bool[_ch.Count];
            FillLive(dlc, live);

            int n = 0;
            foreach (bool b in live) if (b) n++;
            return n;
        }

        /// <summary>
        /// Хватает ли данных кадра на те слова, которые мы собираемся читать.
        /// Проверка живёт здесь, потому что здесь же и знание, сколько слов
        /// будет прочитано.
        /// </summary>
        public bool Fits(byte[] d, byte dlc)
        {
            return d != null && d.Length >= 2 * DlcWords(dlc);
        }

        /// <summary>
        /// Тот ли это кадр, для которого прочитан шаблон.
        ///
        /// ЗАЧЕМ. Имя набора и кадр на шине могут разойтись. Вне записи это
        /// длится один такт - TapeStart посылает команду 1001 сам, по смене
        /// набора в поле. А на время записи набор заперт намеренно, и прибор
        /// продолжает слать прежний кадр - см. регион «Лента быстрого кадра»
        /// в ASt_MainF.cs. Разобрать 16-словный кадр шаблоном на 4 слова
        /// технически можно - вот только числа выйдут чужие, под правильными
        /// именами. Это худший вид ошибки измерения, и ловится он единственным,
        /// что в кадре есть независимо от имени, - его ДЛИНОЙ.
        ///
        /// Кадр КОРОЧЕ шаблона - не беда, а старая прошивка: недостающие каналы
        /// гасятся, см. FillLive.
        /// </summary>
        public bool LengthMatches(byte dlc)
        {
            return DlcBytes(dlc) / 2 <= Words;
        }

        /// <summary>Длина кадра в словах по DLC, но не больше, чем знает шаблон.</summary>
        private int DlcWords(byte dlc)
        {
            int bytes = DlcBytes(dlc);
            int words = bytes / 2;

            return words > Words ? Words : words;
        }

        /// <summary>DLC -> байты данных. Таблица CAN FD, та же, что в PcanFdMessageConverter.</summary>
        private static int DlcBytes(byte dlc)
        {
            if (dlc <= 8) return dlc;

            switch (dlc)
            {
                case 9: return 12;
                case 10: return 16;
                case 11: return 20;
                case 12: return 24;
                case 13: return 32;
                case 14: return 48;
                default: return 64;
            }
        }

        /// <summary>
        /// Разобрать кадр в физику: <see cref="Count"/> значений в
        /// <paramref name="dst"/> начиная с <paramref name="at"/>. Возвращает
        /// код такта.
        ///
        /// Байты читаются ПО НОМЕРУ СЛОВА и младшим вперёд - так лежит кадр CAN.
        /// Позиции из .ini описывают файл, где рекордер уже перевернул слова.
        /// </summary>
        public ushort Decode(byte[] d, byte dlc, float[] dst, int at)
        {
            int words = DlcWords(dlc);

            for (int i = 0; i < _ch.Count; i++)
            {
                IniChannel ch = _ch[i];

                if (ch.Kind == ChKind.Constant) { dst[at + i] = ch.Value; continue; }

                if (ch.Kind == ChKind.Mask)
                {
                    /* Маска берёт СЫРОЙ КОД источника, без K и A: младший байт
                     * счётчика есть свойство кода, а не величины. И код
                     * беззнаковый - «младший байт» у отрицательного числа иначе
                     * не определить.
                     *
                     * Источник всегда измеряемый и объявлен РАНЬШЕ (см. разбор),
                     * поэтому его слово уже известно. */
                    int w = _ch[ch.Src].Word;

                    dst[at + i] = w < words
                        ? (((d[w * 2 + 1] << 8) | d[w * 2]) & ch.Mask)
                        : 0f;

                    continue;
                }

                if (ch.Word >= words) { dst[at + i] = 0f; continue; }

                int raw = (d[ch.Word * 2 + 1] << 8) | d[ch.Word * 2];
                if (ch.Signed) raw = (short)raw;

                dst[at + i] = raw * ch.K + ch.A;
            }

            int t = _ch[TickIndex].Word;

            /* Такт берём сырым кодом: разворот в 32 бита и поиск разрывов
             * работают со счётчиком, а не с физикой. K у него всюду 1, но
             * полагаться на это незачем. */
            if (t >= words) return 0;

            return (ushort)((d[t * 2 + 1] << 8) | d[t * 2]);
        }

        /*-------------------------------------------------------------------
         * Цвета
         *------------------------------------------------------------------*/

        /*
         * Поле color в .ini есть, но во всех шаблонах проекта оно равно 1 -
         * пользоваться им нельзя. Поэтому палитра своя, по номеру слова: цвет
         * канала не должен меняться от того, сколько каналов видно.
         *
         * Сгруппирована по смыслу настолько, насколько это возможно, не зная
         * физики: холодные, тёплые, сиреневые и серые идут вперемешку так, чтобы
         * соседние слова были различимы. На пятнадцати каналах полностью
         * различимых цветов не набрать, и это терпимо: у каждой кривой рядом
         * стоит подпись, а кнопки покрашены теми же цветами.
         */
        private static readonly Color[] Colors =
        {
            Color.FromArgb(121, 134, 203), Color.FromArgb(100, 181, 246),
            Color.FromArgb(255, 213,  79), Color.FromArgb(129, 199, 132),
            Color.FromArgb(255, 138, 101), Color.FromArgb(186, 104, 200),
            Color.FromArgb(158, 158, 158), Color.FromArgb(229, 115, 115),
            Color.FromArgb(236,  64, 122), Color.FromArgb(161, 136, 127),
            Color.FromArgb( 38, 198, 218), Color.FromArgb(212, 225,  87),
            Color.FromArgb(255, 183,  77), Color.FromArgb( 77, 182, 172),
            Color.FromArgb(144, 164, 174), Color.FromArgb(174, 213, 129),
            Color.FromArgb(179, 157, 219), Color.FromArgb(240, 98, 146),
            Color.FromArgb(128, 222, 234), Color.FromArgb(255, 204, 128),
        };

        private static Color Palette(int word)
        {
            return Colors[word % Colors.Length];
        }
    }
}

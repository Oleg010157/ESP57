using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ICDIBasic
{
    /// <summary>
    /// Один измеряемый канал, как он объявлен в .ini Wscope.
    /// </summary>
    internal sealed class IniChannel
    {
        public IniChannel(string name, int word, bool signed, int digits,
                          float k, float a, Color color)
        {
            Name = name;
            Word = word;
            Signed = signed;
            Digits = digits;
            K = k;
            A = a;
            Color = color;
        }

        /// <summary>Имя из .ini - то же, что в Wscope и в прошивке.</summary>
        public string Name { get; private set; }

        /// <summary>Номер слова в кадре. Выведен из позиции байт, см. IniChannelMap.</summary>
        public int Word { get; private set; }

        public bool Signed { get; private set; }

        /// <summary>Знаков после запятой, поле format. По нему печатается таблица курсора.</summary>
        public int Digits { get; private set; }

        /// <summary>значение = код * K + A.</summary>
        public float K { get; private set; }

        public float A { get; private set; }

        public Color Color { get; private set; }

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
     * ВЫЧИСЛЯЕМЫЕ КАНАЛЫ (@) НЕ РАЗБИРАЕМ. Это свой язык Wscope - выражения со
     * состоянием, историей отсчётов и ссылками двух видов ([x] - сырой код,
     * [`x] - значение после K и A). В быстрых шаблонах вычисляемый канал ровно
     * один, младший байт такта, и осциллографу он не нужен: такт развёрнут в 32
     * бита, отсчёты стоят на оси по такту, на дырке линия рвётся, разрывы
     * считаются отдельно. Остальное живёт только в медленных шаблонах.
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

        /// <summary>Сколько вычисляемых каналов пропущено - показываем, а не умалчиваем.</summary>
        public int Skipped { get; private set; }

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
                error = "набор кадра не выбран";
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
                return Parse(File.ReadAllLines(path, PickEncoding(path)), frame, out error);
            }
            catch (IOException ex)
            {
                error = "шаблон не читается: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// UTF-8 при наличии BOM, иначе cp1251.
        ///
        /// Шаблоны в проекте разные: F3_convertor.ini намеренно ASCII,
        /// SPL_Power.ini в UTF-8, остальные в cp1251. Имена каналов везде ASCII,
        /// кириллица только в комментариях, но читать её как Latin-1 значит
        /// получить мусор там, где человек ждёт текст.
        /// </summary>
        private static Encoding PickEncoding(string path)
        {
            var bom = new byte[3];

            using (FileStream fs = File.OpenRead(path))
            {
                if (fs.Read(bom, 0, 3) == 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                    return new UTF8Encoding(true);
            }

            return Encoding.GetEncoding(1251);
        }

        private static IniChannelMap Parse(string[] lines, string frame, out string error)
        {
            error = null;

            var map = new IniChannelMap();
            map.Template = frame;

            bool inSection = false;
            var seen = new Dictionary<int, string>();

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

                /* Вычисляемый канал. Считаем и идём дальше - см. шапку. */
                if (val[0] == '@') { map.Skipped++; continue; }

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

            return map;
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
        /// Сколько каналов шаблона кадр реально принёс. Со старой прошивкой слов
        /// меньше, и последние каналы существуют только как нули - окно их гасит
        /// и отключает кнопки, чтобы плоскую линию не прочитали как измерение.
        /// </summary>
        public int LiveCount(byte dlc)
        {
            int words = DlcWords(dlc);

            int n = 0;
            for (int i = 0; i < _ch.Count; i++)
                if (_ch[i].Word < words) n++;

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

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
    /// Один слот: какой канал показан, каким цветом и в каких границах.
    /// </summary>
    internal sealed class ScopeSlot
    {
        /// <summary>Имя канала или пусто - слот свободен.</summary>
        public string Name;

        public Color Color;

        /// <summary>
        /// Границы по вертикали. Обе NaN - масштаб подбирается сам.
        ///
        /// Авто оставлено намеренно: пока размах канала неизвестен, зашитые
        /// границы дают либо плоскую линию, либо уход за край, и первое, что
        /// приходится делать, - лезть в настройку. Задали границы - стоят
        /// намертво, и картинку можно сравнивать между запусками.
        /// </summary>
        public float Min = float.NaN;

        public float Max = float.NaN;

        public bool Auto
        {
            get { return float.IsNaN(Min) || float.IsNaN(Max) || Max <= Min; }
        }

        public bool Empty
        {
            get { return string.IsNullOrEmpty(Name); }
        }

        public ScopeSlot Clone()
        {
            return new ScopeSlot { Name = Name, Color = Color, Min = Min, Max = Max };
        }
    }

    /*
     * Что показано на осциллографе и как.
     *
     * РАСКЛАДКА ВЗЯТА У Wscope: две группы по шесть слотов, нумерация 0..5 и
     * 256..261. Не потому, что шесть - хорошее число, а потому, что тогда
     * настройки переносятся из WScope*.ini один в один, вместе с номерами
     * слотов, и оператору не приходится выставлять заново то, что он уже
     * выставил.
     *
     * ОТКУДА БЕРЁТСЯ ВИД, по порядку:
     *   1. свой файл Scope/<шаблон>.view.ini, если он есть;
     *   2. иначе импорт из WScope E.ini - цвета и размах, уже настроенные в Wscope;
     *   3. иначе первые три канала карты, авто-масштаб, палитра по номеру слова.
     *
     * В ФАЙЛ Wscope МЫ НЕ ПИШЕМ. Он переписывает его целиком при выходе: писали
     * бы туда и мы - правки одной программы молча пропадали бы при закрытии
     * другой. Читать можно, это безопасно.
     */
    internal sealed class ScopeView
    {
        /// <summary>Слотов в группе. Столько же, сколько у Wscope - см. выше.</summary>
        public const int SlotsPerGroup = 6;

        public const int Groups = 2;

        /// <summary>Нумерация слотов второй группы у Wscope начинается отсюда.</summary>
        private const int Group2Base = 256;

        private readonly ScopeSlot[][] _slots;

        public ScopeView()
        {
            _slots = new ScopeSlot[Groups][];

            for (int g = 0; g < Groups; g++)
            {
                _slots[g] = new ScopeSlot[SlotsPerGroup];
                for (int s = 0; s < SlotsPerGroup; s++) _slots[g][s] = new ScopeSlot();
            }
        }

        public ScopeSlot this[int group, int slot]
        {
            get { return _slots[group][slot]; }
        }

        /*-------------------------------------------------------------------
         * Запуск развёртки
         *
         * Хранится вместе с видом, и канал - тоже. Уровень задан в единицах
         * КОНКРЕТНОГО канала: вольты, запомненные без имени канала, после
         * перезапуска легли бы на амперы.
         *------------------------------------------------------------------*/

        /// <summary>Канал запуска по имени. Пусто - взять из карты по умолчанию.</summary>
        public string TrigChannel;

        /// <summary>Уровень в единицах канала. NaN - подбирается сам.</summary>
        public float TrigLevel = float.NaN;

        public bool TrigRising = true;

        /// <summary>Есть ли в группе хоть один занятый слот. Пустая группа не рисуется.</summary>
        public bool GroupUsed(int g)
        {
            for (int s = 0; s < SlotsPerGroup; s++)
                if (!_slots[g][s].Empty) return true;

            return false;
        }

        public ScopeView Clone()
        {
            var v = new ScopeView();

            for (int g = 0; g < Groups; g++)
                for (int s = 0; s < SlotsPerGroup; s++)
                    v._slots[g][s] = _slots[g][s].Clone();

            v.TrigChannel = TrigChannel;
            v.TrigLevel = TrigLevel;
            v.TrigRising = TrigRising;

            return v;
        }

        /// <summary>Убрать из слотов каналы, которых в карте нет.</summary>
        public void DropUnknown(IniChannelMap map)
        {
            for (int g = 0; g < Groups; g++)
                for (int s = 0; s < SlotsPerGroup; s++)
                {
                    ScopeSlot sl = _slots[g][s];
                    if (!sl.Empty && IndexOf(map, sl.Name) < 0) sl.Name = null;
                }

            /* Канал запуска - из другого шаблона. Вместе с ним уходит и уровень:
             * он задан в единицах того канала и к чужому отношения не имеет. */
            if (!string.IsNullOrEmpty(TrigChannel) && IndexOf(map, TrigChannel) < 0)
            {
                TrigChannel = null;
                TrigLevel = float.NaN;
            }
        }

        /// <summary>Номер канала в карте по имени, без учёта регистра. -1, если нет.</summary>
        public static int IndexOf(IniChannelMap map, string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;

            for (int i = 0; i < map.Count; i++)
                if (string.Equals(map[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;

            return -1;
        }

        /*-------------------------------------------------------------------
         * Умолчание
         *------------------------------------------------------------------*/

        /// <summary>
        /// Первые три канала карты, кроме такта: рисовать все пятнадцать значит
        /// не увидеть ни одного, а какие нужны - знает только оператор.
        /// Счётчик такта как кривая бесполезен, его дело - ось времени.
        /// </summary>
        public static ScopeView Default(IniChannelMap map)
        {
            var v = new ScopeView();

            int s = 0;
            for (int i = 0; i < map.Count && s < 3; i++)
            {
                if (i == map.TickIndex) continue;

                v._slots[0][s].Name = map[i].Name;
                v._slots[0][s].Color = map[i].Color;
                s++;
            }

            return v;
        }

        /*-------------------------------------------------------------------
         * Свой файл
         *------------------------------------------------------------------*/

        /// <summary>Каталог своих настроек вида. Рядом с программой, не у Wscope.</summary>
        public static string Dir
        {
            get { return Path.Combine(Application.StartupPath, "Scope"); }
        }

        /// <summary>
        /// Прочитать вид для шаблона: свой файл, иначе импорт из Wscope, иначе
        /// умолчание. Каналы, которых в карте нет, отбрасываются в любом случае.
        /// </summary>
        public static ScopeView Load(IniChannelMap map)
        {
            return Load(map, Dir, IniChannelMap.Dir);
        }

        /// <summary>
        /// То же, но с явными каталогами. Нужно офлайновому прогону: у него
        /// Application.StartupPath указывает на себя, а не на bin/Debug.
        /// </summary>
        public static ScopeView Load(IniChannelMap map, string ownDir, string wscopeDir)
        {
            ScopeView v = ReadOwn(Path.Combine(ownDir, map.Template + ".view.ini"));

            if (v == null) v = WscopeImport.Read(map, wscopeDir);
            if (v == null) v = Default(map);

            v.DropUnknown(map);
            v.FillColors(map);

            return v;
        }

        /// <summary>Каналу без заданного цвета дать палитру по номеру слова.</summary>
        private void FillColors(IniChannelMap map)
        {
            for (int g = 0; g < Groups; g++)
                for (int s = 0; s < SlotsPerGroup; s++)
                {
                    ScopeSlot sl = _slots[g][s];
                    if (sl.Empty || sl.Color.ToArgb() != 0) continue;

                    int i = IndexOf(map, sl.Name);
                    sl.Color = i >= 0 ? map[i].Color : Color.Gray;
                }
        }

        private static ScopeView ReadOwn(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var v = new ScopeView();
                /* Пишем мы его в UTF-8, но читаем так же, как все .ini проекта:
                 * файл могли открыть и сохранить редактором в cp1251. */
                string[] lines = IniChannelMap.ReadText(File.ReadAllBytes(path))
                                 .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

                bool trigger = false;

                foreach (string raw in lines)
                {
                    string s = raw;

                    int c = s.IndexOf(';');
                    if (c >= 0) s = s.Substring(0, c);

                    s = s.Trim();
                    if (s.Length == 0) continue;

                    if (s[0] == '[')
                    {
                        trigger = string.Equals(s.Trim('[', ']'), "Trigger",
                                                StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    int eq = s.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = s.Substring(0, eq).Trim();
                    string val = s.Substring(eq + 1).Trim();

                    if (trigger)
                    {
                        if (string.Equals(key, "Channel", StringComparison.OrdinalIgnoreCase))
                            v.TrigChannel = val.Length == 0 ? null : val;
                        else if (string.Equals(key, "Level", StringComparison.OrdinalIgnoreCase))
                            v.TrigLevel = ParseOrNan(val);
                        else if (string.Equals(key, "Rising", StringComparison.OrdinalIgnoreCase))
                            v.TrigRising = val != "0";

                        continue;
                    }

                    /* Ключ - "g.s", значение - "имя,цвет,min,max". Пустые min и
                     * max означают авто, как и в окне настройки. */
                    string[] k = key.Split('.');
                    if (k.Length != 2) continue;

                    int g, slot;
                    if (!int.TryParse(k[0], out g) || !int.TryParse(k[1], out slot)) continue;
                    if (g < 0 || g >= Groups || slot < 0 || slot >= SlotsPerGroup) continue;

                    string[] p = val.Split(',');
                    if (p.Length < 2) continue;

                    ScopeSlot sl = v._slots[g][slot];

                    sl.Name = p[0].Trim();
                    if (sl.Name.Length == 0) { sl.Name = null; continue; }

                    int argb;
                    if (int.TryParse(p[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out argb))
                        sl.Color = Color.FromArgb(argb);

                    if (p.Length > 2) sl.Min = ParseOrNan(p[2]);
                    if (p.Length > 3) sl.Max = ParseOrNan(p[3]);
                }

                return v;
            }
            catch (IOException)
            {
                /* Настройки вида - не повод не показать график. */
                return null;
            }
        }

        private static float ParseOrNan(string s)
        {
            float v;
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                ? v : float.NaN;
        }

        /// <summary>Сохранить вид для шаблона. Молча: сорванное сохранение не повод для окна с ошибкой.</summary>
        public void Save(string template)
        {
            Save(template, Dir);
        }

        public void Save(string template, string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var sb = new StringBuilder();

                sb.AppendLine("; Вид осциллографа для шаблона " + template + ".");
                sb.AppendLine("; Пишется окном «Каналы…». Ключ - группа.слот,");
                sb.AppendLine("; значение - имя канала, цвет (ARGB), минимум, максимум.");
                sb.AppendLine("; Пустые минимум и максимум означают автоматический масштаб.");
                sb.AppendLine();
                sb.AppendLine("[Slots]");

                for (int g = 0; g < Groups; g++)
                    for (int s = 0; s < SlotsPerGroup; s++)
                    {
                        ScopeSlot sl = _slots[g][s];
                        if (sl.Empty) continue;

                        sb.AppendLine(g + "." + s + "=" + sl.Name + "," +
                                      sl.Color.ToArgb().ToString(CultureInfo.InvariantCulture) + "," +
                                      Num(sl.Min) + "," + Num(sl.Max));
                    }

                sb.AppendLine();
                sb.AppendLine("; Запуск развёртки. Пустой уровень - подбирается сам:");
                sb.AppendLine("; ноль, если сигнал его пересекает, иначе середина размаха.");
                sb.AppendLine("[Trigger]");

                if (!string.IsNullOrEmpty(TrigChannel)) sb.AppendLine("Channel=" + TrigChannel);

                sb.AppendLine("Level=" + Num(TrigLevel));
                sb.AppendLine("Rising=" + (TrigRising ? "1" : "0"));

                File.WriteAllText(Path.Combine(dir, template + ".view.ini"),
                                  sb.ToString(), new UTF8Encoding(false));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string Num(float v)
        {
            return float.IsNaN(v) ? "" : v.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    /*
     * Импорт настроек вида из Wscope.
     *
     * Формат разобран реверсом по файлам и окну Config: исходников Wscope
     * (Delphi) больше нет. Записан здесь и в шапке bin/Wscope/F3_convertor.ini.
     *
     *     [ChList]   Ch<слот>=<имя канала>, слоты 0..5 и 256..261
     *     [Colors]   Ch<слот>=<TColor>, причём TColor у Delphi это $00BBGGRR,
     *                то есть BGR наоборот
     *     [<канал>]  k, YAdd - размах по вертикали, В СЫРЫХ КОДАХ:
     *                    k = 255 / (rawMax - rawMin)      YAdd = -rawMin
     *
     * Обратно: rawMin = -YAdd, rawMax = 255/k - YAdd, дальше в физику через K и
     * A канала. Сверено со скриншотом окна Config: iMag k=0.6375 YAdd=200 дают
     * коды -200..200, что при K=0.01 есть -2.00..2.00 А - ровно как показано.
     *
     * Цвет лежит ПО СЛОТУ, а не по каналу: в [Colors] есть записи и для пустых
     * слотов. Берём цвет только тех слотов, в которых канал стоит.
     */
    internal static class WscopeImport
    {
        /// <summary>Набор настроек для записей .spl - того самого кадра F2.</summary>
        private const string File_ = "WScopeE.ini";

        private const int Group2Base = 256;

        public static ScopeView Read(IniChannelMap map)
        {
            return Read(map, IniChannelMap.Dir);
        }

        public static ScopeView Read(IniChannelMap map, string dir)
        {
            string path = Path.Combine(dir, File_);
            if (!File.Exists(path)) return null;

            try
            {
                var chList = new Dictionary<int, string>();
                var colors = new Dictionary<int, int>();
                var kv = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);

                string section = "";
                /* Кодировка - по содержимому, одним способом со всеми .ini
                 * проекта: Wscope пишет свой файл в cp1251, но полагаться на
                 * это незачем, см. IniChannelMap.ReadText. */
                string[] lines = IniChannelMap.ReadText(System.IO.File.ReadAllBytes(path))
                                 .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

                foreach (string raw in lines)
                {
                    string s = raw;

                    int c = s.IndexOf(';');
                    if (c >= 0) s = s.Substring(0, c);

                    s = s.Trim();
                    if (s.Length == 0) continue;

                    if (s[0] == '[') { section = s.Trim('[', ']'); continue; }

                    int eq = s.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = s.Substring(0, eq).Trim();
                    string val = s.Substring(eq + 1).Trim();

                    if (string.Equals(section, "ChList", StringComparison.OrdinalIgnoreCase))
                    {
                        int slot;
                        if (TrySlot(key, out slot) && val.Length > 0) chList[slot] = val;
                    }
                    else if (string.Equals(section, "Colors", StringComparison.OrdinalIgnoreCase))
                    {
                        int slot, col;
                        if (TrySlot(key, out slot) &&
                            int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out col))
                            colors[slot] = col;
                    }
                    else if (section.Length > 0)
                    {
                        /* Секция канала: k и YAdd. */
                        float f;
                        if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                            continue;

                        float[] a;
                        if (!kv.TryGetValue(section, out a))
                        {
                            a = new float[] { float.NaN, float.NaN };
                            kv[section] = a;
                        }

                        if (string.Equals(key, "k", StringComparison.OrdinalIgnoreCase)) a[0] = f;
                        else if (string.Equals(key, "YAdd", StringComparison.OrdinalIgnoreCase)) a[1] = f;
                    }
                }

                if (chList.Count == 0) return null;

                var v = new ScopeView();
                bool any = false;

                foreach (KeyValuePair<int, string> e in chList)
                {
                    int slot = e.Key;

                    int g = slot >= Group2Base ? 1 : 0;
                    int s = slot >= Group2Base ? slot - Group2Base : slot;

                    if (s < 0 || s >= ScopeView.SlotsPerGroup) continue;

                    /* Список в WScopeE.ini общий на все шаблоны: там лежит то,
                     * что смотрели в прошлый раз, может быть и от другого кадра.
                     * Чужие имена пропускаем. */
                    int ci = ScopeView.IndexOf(map, e.Value);
                    if (ci < 0) continue;

                    ScopeSlot sl = v[g, s];
                    sl.Name = map[ci].Name; /* имя берём из карты, с её регистром */
                    any = true;

                    int col;
                    if (colors.TryGetValue(slot, out col)) sl.Color = FromTColor(col);

                    float[] a;
                    if (kv.TryGetValue(e.Value, out a)) Range(map[ci], a[0], a[1], sl);
                }

                return any ? v : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>«Ch0» -> 0. Всё прочее - не слот.</summary>
        private static bool TrySlot(string key, out int slot)
        {
            slot = -1;

            if (key.Length < 3 || (key[0] != 'C' && key[0] != 'c')) return false;
            if (key[1] != 'h' && key[1] != 'H') return false;

            return int.TryParse(key.Substring(2), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out slot);
        }

        /// <summary>
        /// k и YAdd в границы канала, в физике. Нули и мусор - оставляем авто.
        /// </summary>
        private static void Range(IniChannel ch, float k, float yAdd, ScopeSlot sl)
        {
            if (float.IsNaN(k) || float.IsNaN(yAdd) || k <= 0f) return;

            float rawMin = -yAdd;
            float rawMax = 255f / k - yAdd;

            if (!(rawMax > rawMin)) return;

            sl.Min = rawMin * ch.K + ch.A;
            sl.Max = rawMax * ch.K + ch.A;
        }

        /// <summary>
        /// Delphi TColor - это $00BBGGRR: синий в старшем байте, красный в
        /// младшем. Прочитать его как RGB значит поменять местами красный с
        /// синим, то есть молча перекрасить все кривые.
        /// </summary>
        public static Color FromTColor(int c)
        {
            return Color.FromArgb((c >> 0) & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF);
        }
    }
}

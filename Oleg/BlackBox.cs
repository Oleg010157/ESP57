using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ICDIBasic
{
    /*
     * Чёрный ящик: аварийные записи быстрой ленты, которые хранит WiFi-мост.
     *
     * Сам осциллограф живёт в мосте (ESP32_BRIDGE/main/bbox.c): он всё время
     * держит в памяти последние ~0,75 с ленты прибора, а по аварии дописывает
     * ещё 0,2 с и сохраняет во флеш. Здесь - список этих записей и перевод
     * их в .spl для Wscope, с меткой ровно в момент аварии.
     *
     * АВТОАРХИВ (с 10.11.2026). В мосте мест четыре, на ПК - сколько угодно.
     * Пока пульт подключён через мост, каждая новая запись скачивается сама
     * (мост сообщает о ней записью 0x7EF), а после подключения докачивается
     * всё, что накопилось без пульта. Архив - SPL\spl_data\BlackBox, по файлу
     * на запись; имя содержит номер записи, поэтому дважды одна запись не
     * качается. Окно «Чёрный ящик» берёт файлы оттуда же.
     *
     * Обмен с мостом ведёт один BlackBoxClient - и для окна, и для архива: два
     * скачивания сразу перемешали бы куски записей. Запросы идут по очереди.
     *
     * Только через мост: по CAN записей не достать, они не в приборе. Формат
     * обмена - docs/esp32-spi-protocol-RU.md, раздел «Чёрный ящик».
     */
    public partial class Form1
    {
        private readonly ToolStripMenuItem _bboxMenu = new ToolStripMenuItem();
        private const string BboxMenuText = "Чёрный ящик (WiFi-мост)…";
        private BlackBoxForm _bboxFrm;
        private BlackBoxClient _bbox;
        private int _bboxNew; // записей, скачанных в архив, пока окно было закрыто

        private string BlackBoxDir => Path.Combine(spl_Dir, "BlackBox");

        private void BlackBox_InitMenu(ToolStripMenuItem parent)
        {
            _bboxMenu.Text = BboxMenuText;
            parent.DropDownItems.Add(_bboxMenu);
            _bboxMenu.Click += (s, e) =>
            {
                if (_bbox == null)
                {
                    MessageBox.Show(this,
                        "Записи чёрного ящика хранит WiFi-мост.\n" +
                        "Включите «Связь через WiFi (мост K57)» и подключитесь.",
                        "Чёрный ящик", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _bboxNew = 0;
                _bboxMenu.Text = BboxMenuText;
                _bboxMenu.Font = new Font(_bboxMenu.Font, FontStyle.Regular);
                if (_bboxFrm == null || _bboxFrm.IsDisposed)
                {
                    _bboxFrm = new BlackBoxForm(_bbox, () => uint_128(0, 0, 0, 339), BlackBox_OpenWscope);
                    _bboxFrm.FormClosed += (a, b) => _bboxFrm = null;
                    _bboxFrm.Show(this);
                }
                else
                {
                    _bboxFrm.Activate();
                }
            };
        }

        // Новый мост - новый клиент: из CANA.cs, при создании WifiLink.
        private void BlackBox_Attach(WifiLink link)
        {
            _bbox = new BlackBoxClient(link, BlackBoxDir, BlackBox_CreateSplHeader);
            _bbox.Archived += (e, path, fresh) =>
            {
                if (!fresh) return;
                BeginInvoke(new Action(() =>
                {
                    if (_bboxFrm != null && !_bboxFrm.IsDisposed) return; // окно само обновит список
                    _bboxNew++;
                    _bboxMenu.Text = $"{BboxMenuText}  — новых: {_bboxNew}";
                    _bboxMenu.Font = new Font(_bboxMenu.Font, FontStyle.Bold);
                }));
            };
            link.BridgeRecord += _bbox.OnRecord;
            // Подключились (мост сообщил, в какой он сети) - докачать накопленное.
            link.NetInfo += () => _bbox?.SyncAll();
        }

        // Связь через мост закрыта: окно держит старого клиента - закрыть и его.
        // Зовут и не из потока окна (переинициализация CAN), отсюда BeginInvoke.
        private void BlackBox_Detach()
        {
            _bbox = null;
            var f = _bboxFrm;
            if (f == null || f.IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() => { if (!f.IsDisposed) f.Close(); }));
        }

        // Тот же заголовок, что у записи по F2: Wscope читает их одинаково.
        private void BlackBox_CreateSplHeader(ushort words, string iniFile, string path)
        {
            SharedFx.Spl_Header_Create(words, GetSplInfo(), iniFile, path);
        }

        // Open_WScope молча ничего не делает, пока идёт запись по F2 или F5:
        // Wscope держал бы открытым файл, который ещё пишется. Окну чёрного
        // ящика надо знать, открылся ли он, - отсюда false.
        private bool BlackBox_OpenWscope(string path)
        {
            if (spl_running || spf_running) return false;
            Open_WScope(path.Contains(" ") ? "\"" + path + "\"" : path);
            return true;
        }
    }

    /// <summary>Заголовок записи - bb_hdr_t в bbox.c.</summary>
    internal sealed class BlackBoxEntry
    {
        public int Slot;
        public uint Seq;
        public ushort Code;
        public ushort FrameType;
        public uint Utc;
        public ushort Session;
        public ushort TrigTick;
        public uint Frames;
        public uint DataBytes;
        public uint DataCrc;
        public uint TrigFrame;
        public ushort[] Extra = new ushort[4];

        public static BlackBoxEntry Parse(byte[] d, int slot)
        {
            // d - заголовок с нулевого байта
            return new BlackBoxEntry
            {
                Slot = slot,
                Seq = BitConverter.ToUInt32(d, 8),
                Code = BitConverter.ToUInt16(d, 12),
                FrameType = BitConverter.ToUInt16(d, 14),
                Utc = BitConverter.ToUInt32(d, 16),
                Session = BitConverter.ToUInt16(d, 20),
                TrigTick = BitConverter.ToUInt16(d, 22),
                Frames = BitConverter.ToUInt32(d, 24),
                DataBytes = BitConverter.ToUInt32(d, 28),
                DataCrc = BitConverter.ToUInt32(d, 32),
                TrigFrame = BitConverter.ToUInt32(d, 36),
                Extra = new[]
                {
                    BitConverter.ToUInt16(d, 40), BitConverter.ToUInt16(d, 42),
                    BitConverter.ToUInt16(d, 44), BitConverter.ToUInt16(d, 46)
                },
            };
        }
    }

    /// <summary>
    /// Обмен с мостом по чёрному ящику и архив на диске. Запросы - по очереди,
    /// по одному в полёте; ответ не пришёл за 15 с - запрос снят, идёт следующий.
    /// События зовутся из потока приёма WifiLink.
    /// </summary>
    internal sealed class BlackBoxClient
    {
        // Номера записей - ESP32_BRIDGE/main/bbox.h.
        private const uint IdList = 0x7E8;
        private const uint IdEntry = 0x7E9;
        private const uint IdListEnd = 0x7EA;
        private const uint IdRead = 0x7EB;
        private const uint IdData = 0x7EC;
        private const uint IdReadEnd = 0x7ED;
        private const uint IdClear = 0x7EE;
        private const uint IdSaved = 0x7EF;

        public const int HdrSize = 64;
        public const ushort CodeHand = 0xFFFF;
        public const int FrameRate = 5000; // кадров в секунду: такт прибора 200 мкс

        private enum OpKind { List, Read, Clear }

        private sealed class Op
        {
            public OpKind Kind;
            public BlackBoxEntry Entry;
            public bool Sync; // список ради архива: недостающее - скачать
            public Action<bool, string, string, bool> Done; // ok, путь, сводка, CRC сошлась
        }

        private readonly WifiLink _link;
        private readonly string _dir;
        private readonly Action<ushort, string, string> _createHeader;
        private readonly Dictionary<ushort, string> _errText = LoadErrorTexts();

        private readonly object _lock = new object();
        private readonly Queue<Op> _queue = new Queue<Op>();
        private Op _cur;
        private readonly System.Threading.Timer _timeout;
        private readonly List<BlackBoxEntry> _pending = new List<BlackBoxEntry>();
        private byte[] _buf;

        /// <summary>Список пришёл: записи (новые сверху) и состояние моста.</summary>
        public event Action<List<BlackBoxEntry>, string> ListReady;

        /// <summary>Запись легла в архив: запись, путь, true - скачана только что.</summary>
        public event Action<BlackBoxEntry, string, bool> Archived;

        public BlackBoxClient(WifiLink link, string dir, Action<ushort, string, string> createHeader)
        {
            _link = link;
            _dir = dir;
            _createHeader = createHeader;
            _timeout = new System.Threading.Timer(_ => Timeout());
        }

        public Dictionary<ushort, string> ErrText => _errText;

        public void RequestList() => Enqueue(new Op { Kind = OpKind.List });

        /// <summary>Список и скачивание всего, чего ещё нет в архиве.</summary>
        public void SyncAll() => Enqueue(new Op { Kind = OpKind.List, Sync = true });

        public void Clear() => Enqueue(new Op { Kind = OpKind.Clear });

        /// <summary>Запись в архив; уже есть - сразу путь к файлу, без моста.</summary>
        public void Download(BlackBoxEntry e, Action<bool, string, string, bool> done)
        {
            string path = ArchivePath(e);
            if (File.Exists(path))
            {
                done?.Invoke(true, path, Summary(e), true);
                return;
            }
            Enqueue(new Op { Kind = OpKind.Read, Entry = e, Done = done });
        }

        // В имени - и контрольная сумма записи (e.DataCrc): номер записи мост
        // начинает заново с 1 после смены раскладки флеша (12.11.2026, места
        // по 128 кБ) или его стирания, и без суммы новая «n1» без времени
        // совпала бы с давно скачанной - и не попала бы в архив.
        public string ArchivePath(BlackBoxEntry e)
        {
            string when = e.Utc != 0
                ? DateTimeOffset.FromUnixTimeSeconds(e.Utc).LocalDateTime.ToString("dd.MM.yyyy_HH.mm.ss", CultureInfo.InvariantCulture)
                : "время-неизвестно";
            return Path.Combine(_dir,
                $"BB_{when}_{(e.Code == CodeHand ? "hand" : e.Code.ToString())}_n{e.Seq}_{e.DataCrc:X8}.spl");
        }

        //-------------------------------------------------------------------
        private void Enqueue(Op op)
        {
            lock (_lock)
            {
                _queue.Enqueue(op);
                if (_cur == null) StartNext();
            }
        }

        // Под _lock.
        private void StartNext()
        {
            _cur = _queue.Count > 0 ? _queue.Dequeue() : null;
            if (_cur == null) return;

            bool sent;
            switch (_cur.Kind)
            {
                case OpKind.List:
                    _pending.Clear();
                    sent = _link.SendBridge(IdList, new byte[0]);
                    break;
                case OpKind.Clear:
                    _pending.Clear();
                    sent = _link.SendBridge(IdClear, new byte[0]);
                    break;
                default:
                    _buf = new byte[HdrSize + _cur.Entry.DataBytes];
                    sent = _link.SendBridge(IdRead, new[] { (byte)_cur.Entry.Slot });
                    break;
            }
            if (!sent)
            {
                // Связи нет - очередь сбросить: после подключения SyncAll
                // спросит заново.
                var done = _cur.Done;
                _queue.Clear();
                _cur = null;
                done?.Invoke(false, null, "нет связи с мостом", false);
                return;
            }
            _timeout.Change(15000, System.Threading.Timeout.Infinite);
        }

        private void Timeout()
        {
            Action<bool, string, string, bool> done = null;
            lock (_lock)
            {
                if (_cur == null) return;
                done = _cur.Done;
                _buf = null;
                StartNext();
            }
            done?.Invoke(false, null, "мост не ответил", false);
        }

        private void Finish()
        {
            _timeout.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            StartNext();
        }

        /// <summary>Из потока приёма WifiLink - все записи моста.</summary>
        public void OnRecord(uint id, byte[] d)
        {
            List<BlackBoxEntry> list = null;
            bool sync = false;
            BlackBoxEntry readEntry = null;
            byte[] readBuf = null;
            bool readOk = false;
            Action<bool, string, string, bool> readDone = null;

            lock (_lock)
            {
                switch (id)
                {
                    case IdEntry:
                        if (d.Length >= 2 + 60)
                        {
                            var h = new byte[HdrSize];
                            Array.Copy(d, 2, h, 0, 60);
                            _pending.Add(BlackBoxEntry.Parse(h, d[0]));
                        }
                        return;

                    case IdListEnd:
                        list = _pending.OrderByDescending(x => x.Seq).ToList();
                        _pending.Clear();
                        sync = _cur != null && _cur.Kind == OpKind.List && _cur.Sync;
                        if (_cur != null && (_cur.Kind == OpKind.List || _cur.Kind == OpKind.Clear)) Finish();
                        break;

                    case IdData:
                        if (_buf == null || d.Length < 4) return;
                        uint at = BitConverter.ToUInt32(d, 0);
                        int n = d.Length - 4;
                        if (at + n <= _buf.Length) Array.Copy(d, 4, _buf, at, n);
                        return;

                    case IdReadEnd:
                        if (_cur == null || _cur.Kind != OpKind.Read) return;
                        readEntry = _cur.Entry;
                        readBuf = _buf;
                        readDone = _cur.Done;
                        readOk = d.Length >= 2 && d[1] == 1 && readBuf != null;
                        _buf = null;
                        Finish();
                        break;

                    case IdSaved:
                        break;

                    default:
                        return;
                }
            }

            if (id == IdSaved)
            {
                SyncAll(); // новая запись в мосте - в архив
                return;
            }

            if (list != null)
            {
                ListReady?.Invoke(list, DescribeState(d));
                if (sync)
                    foreach (var e in list.Where(x => !File.Exists(ArchivePath(x))).Reverse())
                        Enqueue(new Op { Kind = OpKind.Read, Entry = e });
                return;
            }

            if (readEntry != null)
            {
                if (!readOk)
                {
                    readDone?.Invoke(false, null, "запись не скачалась целиком", false);
                    return;
                }
                bool crcOk = Crc32(readBuf, HdrSize, (int)readEntry.DataBytes) == readEntry.DataCrc;
                try
                {
                    string path = WriteSpl(readEntry, readBuf, out string info);
                    readDone?.Invoke(true, path, info, crcOk);
                    Archived?.Invoke(readEntry, path, true);
                }
                catch (Exception ex)
                {
                    readDone?.Invoke(false, null, "не удалось сохранить: " + ex.Message, false);
                }
            }
        }

        private static string DescribeState(byte[] d)
        {
            if (d.Length < 20) return "";
            if (d[2] != 0) return "В мосте нет раздела под записи - прошейте мост заново (новая таблица разделов).";
            string[] st = { "пишет", "дописывает хвост аварии", "сохраняет" };
            uint ring = BitConverter.ToUInt32(d, 4);
            uint frames = BitConverter.ToUInt32(d, 8);
            uint missed = BitConverter.ToUInt32(d, 16);
            return $"Мост {(d[1] < 3 ? st[d[1]] : "?")}: кольцо {ring / 1024} кБ, сейчас в нём " +
                   $"{frames / (double)FrameRate:0.00} с ленты" +
                   (missed > 0 ? $"; пропущено пусков: {missed}" : "");
        }

        public static string TimeText(uint utc) =>
            utc == 0
                ? "время неизвестно"
                : DateTimeOffset.FromUnixTimeSeconds(utc).LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss");

        /// <summary>
        /// Время записи без времени (прибор ещё не знал часов, wall_clock.h) -
        /// от ближайшей по номеру записи с временем, сначала из той же сессии
        /// (ses == null - сессий нет), со значком «≈». Как на странице моста
        /// (fillTimes): день и час тут важнее точности (13.11.2026).
        /// </summary>
        public static string ApproxTimeText(int i, IList<uint> seq, IList<uint> utc, IList<uint> ses)
        {
            int best = -1;
            for (int pass = 0; pass < 2 && best < 0; pass++)
                for (int k = 0; k < seq.Count; k++)
                {
                    if (utc[k] == 0 || (pass == 0 && (ses == null || ses[k] != ses[i]))) continue;
                    if (best < 0 || Math.Abs((long)seq[k] - seq[i]) < Math.Abs((long)seq[best] - seq[i])) best = k;
                }
            return best < 0 ? "время неизвестно" : "≈ " + TimeText(utc[best]);
        }

        public string CodeText(BlackBoxEntry e)
        {
            if (e.Code == CodeHand) return "пуск вручную";
            string s = _errText.TryGetValue(e.Code, out var t) ? t : "код " + e.Code;
            var more = e.Extra.Where(x => x != 0).ToList();
            if (more.Count > 0) s += " (следом: " + string.Join(", ", more) + ")";
            return s;
        }

        private string Summary(BlackBoxEntry e) => $"Запись {e.Seq}: {CodeText(e)}, {TimeText(e.Utc)}.";

        /// <summary>Тип ленты 103 - код команды 1001 в приборе (MBOX28.c).</summary>
        public static string FrameName(ushort type)
        {
            switch (type)
            {
                case 1: return "F1_convertor";
                case 2: return "F1_invertor";
                case 3: return "F3_invertor";
                case 4: return "F3_convertor";
                case 6: return "F3_grid";
                default: return null;
            }
        }

        /*
         * Кольцо моста - элементы «байт len | kind << 7, затем len байт кадра»
         * (bbox.c). kind 1 - кадр 0x7E1, набор F3_convertor; kind 0 - лента 103
         * того типа, что был включён у пульта. Если пульт посреди окна сменил
         * ленту, в записи окажутся кадры двух видов: в .spl идут только те, что
         * одного вида с кадром, на котором пришла авария.
         *
         * В .spl - как у записи по F2 (CanaAsync.cs): байт метки, затем слова
         * кадра старшим байтом вперёд. Метка 0x55 - обычный кадр; на кадре
         * аварии стоит 1 - Wscope покажет её, как метку пробела.
         */
        private string WriteSpl(BlackBoxEntry e, byte[] buf, out string info)
        {
            var frames = new List<(int kind, byte[] data)>();
            int pos = HdrSize, end = HdrSize + (int)e.DataBytes;
            while (pos < end)
            {
                int len = buf[pos] & 0x7F, kind = buf[pos] >> 7;
                if (pos + 1 + len > end) break;
                var f = new byte[len];
                Array.Copy(buf, pos + 1, f, 0, len);
                frames.Add((kind, f));
                pos += 1 + len;
            }
            if (frames.Count == 0) throw new InvalidDataException("запись пустая");

            int trig = (int)Math.Min(e.TrigFrame, (uint)frames.Count - 1);
            int tKind = frames[trig].kind, tLen = frames[trig].data.Length;
            string name = tKind == 1 ? "F3_convertor" : FrameName(e.FrameType) ?? (tLen == 32 ? "F3_convertor" : null);
            if (name == null) throw new InvalidDataException("неизвестен тип ленты");

            // Сам момент аварии: кадр, чей счётчик тактов (последнее слово)
            // равен тому, что прибор прислал в пуске. Пуск доходит до моста на
            // несколько миллисекунд позже, поэтому ищем назад от кадра прихода.
            int mark = trig;
            if (e.Code != CodeHand)
            {
                for (int i = trig; i >= 0 && i >= trig - 200; i--)
                {
                    var d = frames[i].data;
                    if (frames[i].kind == tKind && d.Length == tLen && d.Length >= 2 &&
                        BitConverter.ToUInt16(d, d.Length - 2) == e.TrigTick)
                    {
                        mark = i;
                        break;
                    }
                }
            }

            if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
            string path = ArchivePath(e);
            string tmp = path + ".tmp";

            // Сначала во временный файл: оборвись запись посреди - в архиве не
            // останется недописанного .spl, который потом считался бы готовым.
            ushort words = (ushort)(tLen / 2);
            if (File.Exists(tmp)) File.Delete(tmp);
            _createHeader(words, name + ".ini", tmp);

            int written = 0, before = 0;
            using (var fs = new FileStream(tmp, FileMode.Append, FileAccess.Write))
            {
                var rec = new byte[1 + tLen];
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i].kind != tKind || frames[i].data.Length != tLen) continue;
                    var d = frames[i].data;
                    rec[0] = i == mark ? (byte)1 : (byte)0x55;
                    for (int w = 0; w < words; w++)
                    {
                        rec[1 + 2 * w] = d[2 * w + 1];
                        rec[2 + 2 * w] = d[2 * w];
                    }
                    fs.Write(rec, 0, rec.Length);
                    if (i < mark) before++;
                    written++;
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);

            info = Summary(e) + "\n" +
                   $"Лента {name}: {written} кадров, до аварии {before / (double)FrameRate:0.000} с, " +
                   $"после {(written - before) / (double)FrameRate:0.000} с. Авария - метка 1.";
            return path;
        }

        // CRC-32 как у zlib - её считает и мост (esp_rom_crc32_le с нуля).
        private static uint Crc32(byte[] b, int off, int n)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = off; i < off + n && i < b.Length; i++)
            {
                crc ^= b[i];
                for (int k = 0; k < 8; k++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
            return ~crc;
        }

        // Описания кодов - из того же ErrorCodes.h, что у окна ошибок.
        internal static Dictionary<ushort, string> LoadErrorTexts()
        {
            var map = new Dictionary<ushort, string>();
            try
            {
                foreach (var line in File.ReadAllLines(Path.Combine(Application.StartupPath, "..", "ErrorCodes.h")))
                {
                    if (!line.StartsWith("#define") || !line.Contains("//")) continue;
                    var parts = line.Split(new[] { "//" }, 2, StringSplitOptions.None);
                    var def = parts[0].Replace("#define", "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (def.Length >= 2 && ushort.TryParse(def[1], out var code))
                        map[code] = parts[1].Trim();
                }
            }
            catch
            {
                // нет файла - покажем просто номера
            }
            return map;
        }
    }

    internal sealed class BlackBoxForm : Form
    {
        private readonly BlackBoxClient _bb;
        private readonly Action _manual;
        private readonly Func<string, bool> _openWscope;

        private readonly ListView _list = new ListView();
        private readonly Label _status = new Label();
        private readonly Button _btnRefresh = new Button { Text = "Обновить" };
        private readonly Button _btnView = new Button { Text = "Смотреть в Wscope" };
        private readonly Button _btnSave = new Button { Text = "Показать файл" };
        private readonly Button _btnFolder = new Button { Text = "Папка архива" };
        private readonly Button _btnManual = new Button { Text = "Записать сейчас" };
        private readonly Button _btnClear = new Button { Text = "Стереть в мосте" };

        public BlackBoxForm(BlackBoxClient bb, Action manual, Func<string, bool> openWscope)
        {
            _bb = bb;
            _manual = manual;
            _openWscope = openWscope;

            Text = "Чёрный ящик - аварийные записи моста";
            Size = new Size(900, 380);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft Sans Serif", 10F);

            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.MultiSelect = false;
            _list.HideSelection = false;
            _list.Dock = DockStyle.Fill;
            _list.Columns.Add("№", 50);
            _list.Columns.Add("Время", 160);
            _list.Columns.Add("Код", 55);
            _list.Columns.Add("Авария", 300);
            _list.Columns.Add("До / после, с", 110);
            _list.Columns.Add("Лента", 110);
            _list.Columns.Add("В архиве", 80);
            _list.DoubleClick += (s, e) => Open(true);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(4) };
            foreach (var b in new[] { _btnRefresh, _btnView, _btnSave, _btnFolder, _btnManual, _btnClear })
            {
                b.AutoSize = true;
                bar.Controls.Add(b);
            }
            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.TextAlign = ContentAlignment.MiddleLeft;

            Controls.Add(_list);
            Controls.Add(_status);
            Controls.Add(bar);

            _btnRefresh.Click += (s, e) => Refresh_();
            _btnView.Click += (s, e) => Open(true);
            _btnSave.Click += (s, e) => Open(false);
            _btnFolder.Click += (s, e) =>
            {
                string dir = Path.GetDirectoryName(_bb.ArchivePath(new BlackBoxEntry()));
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            };
            _btnManual.Click += (s, e) =>
            {
                // Пуск даёт прибор (команда 339): в записи будут его время и
                // сессия. Мост сохранит её через 0,2 с и сам сообщит - запись
                // скачается в архив, список обновится.
                _manual();
                _status.Text = "Пуск отправлен прибору, ждём запись…";
            };
            _btnClear.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Стереть все записи чёрного ящика в мосте?\nАрхив на этом компьютере останется.",
                        "Чёрный ящик", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    _bb.Clear();
            };

            _bb.ListReady += OnList;
            _bb.Archived += OnArchived;
            FormClosed += (s, e) =>
            {
                _bb.ListReady -= OnList;
                _bb.Archived -= OnArchived;
            };
            Load += (s, e) => Refresh_();
        }

        private void Refresh_()
        {
            _status.Text = "Запрос списка…";
            _bb.SyncAll();
        }

        private void OnList(List<BlackBoxEntry> entries, string state)
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(() => ShowList(entries, state)));
        }

        private void OnArchived(BlackBoxEntry e, string path, bool fresh)
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                foreach (ListViewItem it in _list.Items)
                    if (((BlackBoxEntry)it.Tag).Seq == e.Seq)
                        it.SubItems[6].Text = "да";
                _status.Text = "В архив: " + path;
            }));
        }

        private void ShowList(List<BlackBoxEntry> entries, string state)
        {
            int selSeq = _list.SelectedItems.Count > 0 ? (int)((BlackBoxEntry)_list.SelectedItems[0].Tag).Seq : -1;
            _list.BeginUpdate();
            _list.Items.Clear();
            var seqs = entries.Select(x => x.Seq).ToList();
            var utcs = entries.Select(x => x.Utc).ToList();
            for (int ei = 0; ei < entries.Count; ei++)
            {
                var e = entries[ei];
                var it = new ListViewItem(e.Seq.ToString());
                it.SubItems.Add(e.Utc != 0 ? BlackBoxClient.TimeText(e.Utc)
                                           : BlackBoxClient.ApproxTimeText(ei, seqs, utcs, null));
                it.SubItems.Add(e.Code == BlackBoxClient.CodeHand ? "—" : e.Code.ToString());
                it.SubItems.Add(_bb.CodeText(e));
                double before = e.TrigFrame / (double)BlackBoxClient.FrameRate;
                double after = (e.Frames - Math.Min(e.TrigFrame, e.Frames)) / (double)BlackBoxClient.FrameRate;
                it.SubItems.Add($"{before:0.00} / {after:0.00}");
                it.SubItems.Add(e.FrameType == 0 ? "F3_convertor" : BlackBoxClient.FrameName(e.FrameType) ?? "?");
                it.SubItems.Add(File.Exists(_bb.ArchivePath(e)) ? "да" : "качается…");
                it.Tag = e;
                if (e.Seq == selSeq) it.Selected = true;
                _list.Items.Add(it);
            }
            _list.EndUpdate();
            if (_list.SelectedItems.Count == 0 && _list.Items.Count > 0) _list.Items[0].Selected = true;
            _status.Text = (entries.Count == 0 ? "В мосте записей нет. " : $"В мосте записей: {entries.Count}. ") + state;
        }

        /// <summary>Открыть выбранную запись из архива (докачав, если её там нет); view - в Wscope.</summary>
        private void Open(bool view)
        {
            if (_list.SelectedItems.Count == 0) return;
            var e = (BlackBoxEntry)_list.SelectedItems[0].Tag;
            _status.Text = $"Запись {e.Seq}…";
            _bb.Download(e, (ok, path, info, crcOk) =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() => Opened(view, ok, path, info, crcOk)));
            });
        }

        private void Opened(bool view, bool ok, string path, string info, bool crcOk)
        {
            if (!ok)
            {
                _status.Text = "Не получилось: " + info + ".";
                return;
            }
            _status.Text = (crcOk ? "" : "ВНИМАНИЕ: CRC записи не сошлась. ") + path;
            if (!crcOk)
                MessageBox.Show(this, "Контрольная сумма записи не сошлась - данные могут быть испорчены.",
                                "Чёрный ящик", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (view)
            {
                if (!_openWscope(path))
                    _status.Text = "Идёт запись ленты (F2/F5) - Wscope откроется после её остановки. Файл: " + path;
                return;
            }
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }
    }
}

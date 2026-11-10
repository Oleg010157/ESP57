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
     * держит в памяти последние ~0,8 с ленты прибора, а по аварии дописывает
     * ещё 0,2 с и сохраняет во флеш. Здесь - список этих записей и перевод
     * выбранной в .spl для Wscope, с меткой ровно в момент аварии.
     *
     * Только через мост: по CAN записей не достать, они не в приборе. Формат
     * обмена - docs/esp32-spi-protocol-RU.md, раздел «Чёрный ящик».
     */
    public partial class Form1
    {
        private readonly ToolStripMenuItem _bboxMenu = new ToolStripMenuItem("Чёрный ящик (WiFi-мост)…");
        private BlackBoxForm _bboxFrm;

        private void BlackBox_InitMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Add(_bboxMenu);
            _bboxMenu.Click += (s, e) =>
            {
                if (_wifi == null)
                {
                    MessageBox.Show(this,
                        "Записи чёрного ящика хранит WiFi-мост.\n" +
                        "Включите «Связь через WiFi (мост K57)» и подключитесь.",
                        "Чёрный ящик", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (_bboxFrm == null || _bboxFrm.IsDisposed)
                {
                    _bboxFrm = new BlackBoxForm(_wifi, spl_Dir, BlackBox_CreateSplHeader,
                                                () => uint_128(0, 0, 0, 339), BlackBox_OpenWscope);
                    _bboxFrm.FormClosed += (a, b) => _bboxFrm = null;
                    _bboxFrm.Show(this);
                }
                else
                {
                    _bboxFrm.Activate();
                }
            };
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

        // Из потока приёма WifiLink.
        private void BlackBox_OnRecord(uint id, byte[] data)
        {
            _bboxFrm?.OnRecord(id, data);
        }
    }

    internal sealed class BlackBoxForm : Form
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

        private const int HdrSize = 64;
        private const ushort CodeHand = 0xFFFF;
        private const int FrameRate = 5000; // кадров в секунду: такт прибора 200 мкс

        /// <summary>Заголовок записи - bb_hdr_t в bbox.c.</summary>
        private sealed class Entry
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

            public static Entry Parse(byte[] d, int slot)
            {
                // d - заголовок с нулевого байта
                return new Entry
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

        private readonly WifiLink _link;
        private readonly string _dir;
        private readonly Action<ushort, string, string> _createHeader;
        private readonly Action _manual;
        private readonly Func<string, bool> _openWscope;
        private readonly Dictionary<ushort, string> _errText = LoadErrorTexts();

        private readonly ListView _list = new ListView();
        private readonly Label _status = new Label();
        private readonly Button _btnRefresh = new Button { Text = "Обновить" };
        private readonly Button _btnView = new Button { Text = "Смотреть в Wscope" };
        private readonly Button _btnSave = new Button { Text = "Сохранить для Wscope" };
        private readonly Button _btnManual = new Button { Text = "Записать сейчас" };
        private readonly Button _btnClear = new Button { Text = "Стереть все" };

        // Список копится в потоке приёма и уходит в окно по записи конца.
        private readonly object _lock = new object();
        private readonly List<Entry> _pending = new List<Entry>();
        private Entry _reading;
        private byte[] _buf;
        private bool _viewAfter; // скачанное сразу открыть в Wscope - только поток окна

        public BlackBoxForm(WifiLink link, string splDir, Action<ushort, string, string> createHeader,
                            Action manual, Func<string, bool> openWscope)
        {
            _link = link;
            _dir = splDir;
            _createHeader = createHeader;
            _manual = manual;
            _openWscope = openWscope;

            Text = "Чёрный ящик - аварийные записи моста";
            Size = new Size(820, 380);
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
            _list.DoubleClick += (s, e) => Download(true);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(4) };
            foreach (var b in new[] { _btnRefresh, _btnView, _btnSave, _btnManual, _btnClear })
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

            _btnRefresh.Click += (s, e) => RequestList();
            _btnView.Click += (s, e) => Download(true);
            _btnSave.Click += (s, e) => Download(false);
            _btnManual.Click += (s, e) =>
            {
                // Пуск даёт прибор (команда 339): в записи будут его время и
                // сессия. Мост сохранит её через 0,2 с и сам сообщит - список
                // обновится по записи 0x7EF.
                _manual();
                _status.Text = "Пуск отправлен прибору, ждём запись…";
            };
            _btnClear.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Стереть все записи чёрного ящика в мосте?", "Чёрный ящик",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    Send(IdClear, new byte[0]);
            };

            Load += (s, e) => RequestList();
        }

        private void Send(uint id, byte[] data)
        {
            if (!_link.SendBridge(id, data))
                _status.Text = "Нет связи с мостом.";
        }

        private void RequestList()
        {
            lock (_lock) _pending.Clear();
            _status.Text = "Запрос списка…";
            Send(IdList, new byte[0]);
        }

        /// <summary>Из потока приёма WifiLink.</summary>
        public void OnRecord(uint id, byte[] d)
        {
            if (IsDisposed) return;

            switch (id)
            {
                case IdEntry:
                    if (d.Length >= 2 + 60)
                    {
                        var h = new byte[HdrSize];
                        Array.Copy(d, 2, h, 0, 60);
                        lock (_lock) _pending.Add(Entry.Parse(h, d[0]));
                    }
                    break;

                case IdListEnd:
                {
                    List<Entry> got;
                    lock (_lock)
                    {
                        got = _pending.OrderByDescending(x => x.Seq).ToList();
                        _pending.Clear();
                    }
                    string st = DescribeState(d);
                    BeginInvoke(new Action(() => ShowList(got, st)));
                    break;
                }

                case IdData:
                    lock (_lock)
                    {
                        if (_buf == null || d.Length < 4) break;
                        uint at = BitConverter.ToUInt32(d, 0);
                        int n = d.Length - 4;
                        if (at + n <= _buf.Length) Array.Copy(d, 4, _buf, at, n);
                    }
                    break;

                case IdReadEnd:
                {
                    byte[] buf;
                    Entry e;
                    lock (_lock)
                    {
                        buf = _buf;
                        e = _reading;
                        _buf = null;
                        _reading = null;
                    }
                    bool ok = d.Length >= 2 && d[1] == 1 && buf != null && e != null;
                    BeginInvoke(new Action(() => Downloaded(ok, e, buf)));
                    break;
                }

                case IdSaved:
                    BeginInvoke(new Action(RequestList));
                    break;
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

        private void ShowList(List<Entry> entries, string state)
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var e in entries)
            {
                var it = new ListViewItem(e.Seq.ToString());
                it.SubItems.Add(TimeText(e.Utc));
                it.SubItems.Add(e.Code == CodeHand ? "—" : e.Code.ToString());
                it.SubItems.Add(CodeText(e));
                double before = e.TrigFrame / (double)FrameRate;
                double after = (e.Frames - Math.Min(e.TrigFrame, e.Frames)) / (double)FrameRate;
                it.SubItems.Add($"{before:0.00} / {after:0.00}");
                it.SubItems.Add(e.FrameType == 0 ? "F3_convertor" : FrameName(e.FrameType) ?? "?");
                it.Tag = e;
                _list.Items.Add(it);
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0) _list.Items[0].Selected = true;
            _status.Text = (entries.Count == 0 ? "Записей нет. " : $"Записей: {entries.Count}. ") + state;
        }

        private static string TimeText(uint utc) =>
            utc == 0
                ? "время неизвестно"
                : DateTimeOffset.FromUnixTimeSeconds(utc).LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss");

        private string CodeText(Entry e)
        {
            if (e.Code == CodeHand) return "пуск вручную";
            string s = _errText.TryGetValue(e.Code, out var t) ? t : "код " + e.Code;
            var more = e.Extra.Where(x => x != 0).ToList();
            if (more.Count > 0) s += " (следом: " + string.Join(", ", more) + ")";
            return s;
        }

        /// <summary>Тип ленты 103 - код команды 1001 в приборе (MBOX28.c).</summary>
        private static string FrameName(ushort type)
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

        /// <summary>Скачать выбранную запись в .spl; view - сразу открыть в Wscope.</summary>
        private void Download(bool view)
        {
            if (_list.SelectedItems.Count == 0) return;
            var e = (Entry)_list.SelectedItems[0].Tag;
            _viewAfter = view;
            lock (_lock)
            {
                _reading = e;
                _buf = new byte[HdrSize + e.DataBytes];
            }
            _status.Text = $"Скачиваю запись {e.Seq}, {(HdrSize + e.DataBytes) / 1024} кБ…";
            Send(IdRead, new[] { (byte)e.Slot });
        }

        private void Downloaded(bool ok, Entry e, byte[] buf)
        {
            if (!ok)
            {
                _status.Text = "Запись не скачалась целиком - попробуйте ещё раз.";
                return;
            }

            bool crcOk = Crc32(buf, HdrSize, (int)e.DataBytes) == e.DataCrc;
            try
            {
                string path = WriteSpl(e, buf, out string info);
                _status.Text = (crcOk ? "" : "ВНИМАНИЕ: CRC записи не сошлась. ") + "Сохранено: " + path;

                if (_viewAfter)
                {
                    if (!crcOk)
                        MessageBox.Show(this, "Контрольная сумма записи не сошлась - данные могут быть испорчены.",
                                        "Чёрный ящик", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    if (!_openWscope(path))
                        _status.Text = "Идёт запись ленты (F2/F5) - Wscope откроется после её остановки. " +
                                       "Файл сохранён: " + path;
                    return;
                }

                if (MessageBox.Show(this,
                        (crcOk ? "" : "ВНИМАНИЕ: контрольная сумма не сошлась, данные могут быть испорчены.\n\n") +
                        info + "\n\n" + path + "\n\nПоказать файл в папке?",
                        "Чёрный ящик", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
            }
            catch (Exception ex)
            {
                _status.Text = "Не удалось сохранить: " + ex.Message;
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
        private string WriteSpl(Entry e, byte[] buf, out string info)
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
            DateTime when = e.Utc != 0 ? DateTimeOffset.FromUnixTimeSeconds(e.Utc).LocalDateTime : DateTime.Now;
            string path = Path.Combine(_dir, "BB_" + when.ToString("dd.MM.yyyy_HH.mm.ss", CultureInfo.InvariantCulture) +
                                              "_" + (e.Code == CodeHand ? "hand" : e.Code.ToString()) + ".spl");

            ushort words = (ushort)(tLen / 2);
            _createHeader(words, name + ".ini", path);

            int written = 0, before = 0;
            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write))
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

            info = $"Запись {e.Seq}: {CodeText(e)}, {TimeText(e.Utc)}.\n" +
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
        private static Dictionary<ushort, string> LoadErrorTexts()
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
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ICDIBasic
{
    /*
     * Журнал событий и копия настройки прибора, которые хранит WiFi-мост.
     *
     * Главное хранилище - в самом приборе: история аварий в EEPROM, настройка
     * во флеше. Мост добавляет длинный журнал (каждое событие отдельной
     * строкой, около 3800 строк) и копию настройки на случай замены платы.
     * Восстановление - только отсюда, вручную: командами прибору 342 (слова
     * записи) и 343 (принять). Прибор проверяет запись сам и берёт её, только
     * когда ключи закрыты.
     *
     * Мост - ESP32_BRIDGE/main/bstore.c, прибор - my_Files/bridge_store.c,
     * формат - docs/esp32-spi-protocol-RU.md, «Журнал и копия настройки».
     */
    public partial class Form1
    {
        private readonly ToolStripMenuItem _storeMenu = new ToolStripMenuItem("Журнал и копия настройки (WiFi-мост)…");
        private BridgeStoreForm _storeFrm;

        private void BridgeStore_InitMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Add(_storeMenu);
            _storeMenu.Click += (s, e) =>
            {
                if (_wifi == null)
                {
                    MessageBox.Show(this,
                        "Журнал и копию настройки хранит WiFi-мост.\n" +
                        "Включите «Связь через WiFi (мост K57)» и подключитесь.",
                        "Журнал", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (_storeFrm == null || _storeFrm.IsDisposed)
                {
                    _storeFrm = new BridgeStoreForm(_wifi, (cmd, d1, d2, d3) => uint_128(d1, d2, d3, cmd));
                    _storeFrm.FormClosed += (a, b) => _storeFrm = null;
                    _storeFrm.Show(this);
                }
                else
                {
                    _storeFrm.Activate();
                }
            };
        }

        // Из потока приёма WifiLink - записи моста 0x7D0..0x7DF.
        private void BridgeStore_OnRecord(uint id, byte[] data)
        {
            _storeFrm?.OnRecord(id, data);
        }

        // Из Mbox13Rx - ответы прибора на 341..343.
        private void BridgeStore_OnAck(ushort cmd, ushort d1, ushort d2, ushort d3)
        {
            _storeFrm?.OnAck(cmd, d1, d2, d3);
        }
    }

    internal sealed class BridgeStoreForm : Form
    {
        // Мост: ESP32_BRIDGE/main/bstore.h
        private const uint IdCalHdr = 0x7D2;   // прибор: заголовок живой записи
        private const uint IdCalData = 0x7D3;  // прибор: слова живой записи
        private const uint IdJRead = 0x7D8;
        private const uint IdJRecs = 0x7D9;
        private const uint IdJEnd = 0x7DA;
        private const uint IdJClear = 0x7DB;
        private const uint IdCopy = 0x7DC;
        private const uint IdCopyHdr = 0x7DD;
        private const uint IdCopyData = 0x7DE;
        private const uint IdCopyEnd = 0x7DF;

        // Прибор: my_Files/bridge_store.h
        private const ushort CmdSend = 341;
        private const ushort CmdWords = 342;
        private const ushort CmdApply = 343;

        /// <summary>Запись настройки: заголовок из 12 слов и сами слова.</summary>
        private sealed class Calib
        {
            public ushort[] Hdr = new ushort[12];
            public ushort[] Words;
            public bool[] Got;

            public int N => Hdr[1];
            public ushort Status => Hdr[2];
            public uint Uid0 => (uint)(Hdr[4] | (Hdr[5] << 16));
            public uint Uid1 => (uint)(Hdr[6] | (Hdr[7] << 16));
            public uint Utc => (uint)(Hdr[8] | (Hdr[9] << 16));
            public ushort Version => Hdr[10];
            public bool Complete => Words != null && Got.All(x => x);

            public void SetHdr(byte[] d)
            {
                for (int i = 0; i < 12 && 2 * i + 1 < d.Length; i++) Hdr[i] = BitConverter.ToUInt16(d, 2 * i);
                Words = N > 0 && N <= 64 ? new ushort[N] : null;
                Got = Words != null ? new bool[N] : new bool[0];
            }

            public void SetData(byte[] d)
            {
                if (Words == null || d.Length < 4) return;
                int off = BitConverter.ToUInt16(d, 0);
                for (int i = 0; 2 + 2 * i + 1 < d.Length && off + i < N; i++)
                {
                    Words[off + i] = BitConverter.ToUInt16(d, 2 + 2 * i);
                    Got[off + i] = true;
                }
            }

            public string Describe()
            {
                string why;
                switch (Hdr[3])
                {
                    case 0: why = "при включении"; break;
                    case 1: why = "после сохранения"; break;
                    default: why = "по запросу"; break;
                }
                string when = Utc == 0
                    ? "время неизвестно"
                    : DateTimeOffset.FromUnixTimeSeconds(Utc).LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss");
                return $"{when} ({why}), кристалл {Uid1:X8}-{Uid0:X8}, версия записи {Version}, {N} слов";
            }
        }

        private readonly WifiLink _link;
        private readonly Action<ushort, ushort, ushort, ushort> _cmd; // cmd, d1, d2, d3
        private readonly Dictionary<ushort, string> _errText = BlackBoxForm.LoadErrorTexts();

        private readonly ListView _list = new ListView();
        private readonly Label _jStatus = new Label();
        private readonly TextBox _copyText = new TextBox();
        private readonly Button _btnRestore = new Button { Text = "Восстановить в прибор…" };

        private readonly object _lock = new object();
        private readonly List<byte[]> _rows = new List<byte[]>();
        private Calib _copy, _live;
        private bool _copyAbsent;

        // Восстановление: слова уходят по одной команде, следующая - по ACK.
        private ushort[] _restoreWords;
        private int _restoreAt = -1;
        private readonly Timer _restoreTimer = new Timer { Interval = 1500 };

        public BridgeStoreForm(WifiLink link, Action<ushort, ushort, ushort, ushort> cmd)
        {
            _link = link;
            _cmd = cmd;

            Text = "Журнал и копия настройки - WiFi-мост";
            Size = new Size(860, 460);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft Sans Serif", 10F);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildJournalTab());
            tabs.TabPages.Add(BuildCopyTab());
            Controls.Add(tabs);

            _restoreTimer.Tick += (s, e) => RestoreFailed("прибор не ответил");
            Load += (s, e) =>
            {
                RequestJournal();
                RequestCopies();
            };
            FormClosed += (s, e) => _restoreTimer.Dispose();
        }

        //-------------------------------------------------------------------
        // Журнал
        //-------------------------------------------------------------------
        private TabPage BuildJournalTab()
        {
            var page = new TabPage("Журнал событий");
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.Dock = DockStyle.Fill;
            _list.Columns.Add("№", 60);
            _list.Columns.Add("Время", 160);
            _list.Columns.Add("Код", 55);
            _list.Columns.Add("Событие", 380);
            _list.Columns.Add("Сессия", 70);
            _list.Columns.Add("Повторы", 110);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(4) };
            var bRefresh = new Button { Text = "Обновить", AutoSize = true };
            var bCsv = new Button { Text = "Сохранить в CSV…", AutoSize = true };
            var bClear = new Button { Text = "Стереть журнал", AutoSize = true };
            bar.Controls.AddRange(new Control[] { bRefresh, bCsv, bClear });
            bRefresh.Click += (s, e) => RequestJournal();
            bCsv.Click += (s, e) => SaveCsv();
            bClear.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Стереть весь журнал событий в мосте?\nВ приборе история аварий останется.",
                        "Журнал", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    lock (_lock) _rows.Clear();
                    Send(IdJClear);
                }
            };
            _jStatus.Dock = DockStyle.Bottom;
            _jStatus.Height = 24;

            page.Controls.Add(_list);
            page.Controls.Add(_jStatus);
            page.Controls.Add(bar);
            return page;
        }

        private void Send(uint id, byte[] data = null)
        {
            if (!_link.SendBridge(id, data ?? new byte[0]))
                _jStatus.Text = "Нет связи с мостом.";
        }

        private void RequestJournal()
        {
            lock (_lock) _rows.Clear();
            _jStatus.Text = "Читаю журнал…";
            Send(IdJRead);
        }

        private void ShowJournal(List<byte[]> rows, uint cap, uint lost)
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            // Новые сверху: смотрят обычно, что было последним.
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                var r = rows[i];
                uint seq = BitConverter.ToUInt32(r, 0), utc = BitConverter.ToUInt32(r, 4);
                ushort code = BitConverter.ToUInt16(r, 8), ses = BitConverter.ToUInt16(r, 10);
                ushort cnt = BitConverter.ToUInt16(r, 12);
                var it = new ListViewItem(seq.ToString());
                it.SubItems.Add(TimeText(utc));
                it.SubItems.Add(code.ToString());
                it.SubItems.Add(_errText.TryGetValue(code, out var t) ? t : "код " + code);
                it.SubItems.Add(ses.ToString());
                it.SubItems.Add(cnt == 0 ? "" : $"ещё {cnt} раз за минуту");
                if (cnt != 0) it.ForeColor = Color.DimGray;
                _list.Items.Add(it);
            }
            _list.EndUpdate();
            _jStatus.Text = $"Строк: {rows.Count} из {cap}. Один код пишется не чаще раза в минуту, " +
                            "повторы - строкой «ещё N раз»." + (lost > 0 ? $" Потеряно событий: {lost}." : "");
        }

        private static string TimeText(uint utc) =>
            utc == 0
                ? "время неизвестно"
                : DateTimeOffset.FromUnixTimeSeconds(utc).LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss");

        private void SaveCsv()
        {
            List<byte[]> rows;
            lock (_lock) rows = _rows.ToList();
            if (rows.Count == 0) return;
            using (var dlg = new SaveFileDialog
                   {
                       Filter = "CSV (*.csv)|*.csv",
                       FileName = "Журнал_" + DateTime.Now.ToString("dd.MM.yyyy_HH.mm") + ".csv"
                   })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var sb = new StringBuilder("№;Время;Код;Событие;Сессия;Повторов за минуту\r\n");
                foreach (var r in rows)
                {
                    ushort code = BitConverter.ToUInt16(r, 8);
                    sb.Append(BitConverter.ToUInt32(r, 0)).Append(';')
                      .Append(TimeText(BitConverter.ToUInt32(r, 4))).Append(';')
                      .Append(code).Append(';')
                      .Append((_errText.TryGetValue(code, out var t) ? t : "").Replace(';', ',')).Append(';')
                      .Append(BitConverter.ToUInt16(r, 10)).Append(';')
                      .Append(BitConverter.ToUInt16(r, 12)).Append("\r\n");
                }
                // Excel по-русски читает CSV в cp1251 с разделителем «;».
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.GetEncoding(1251));
                _jStatus.Text = "Сохранено: " + dlg.FileName;
            }
        }

        //-------------------------------------------------------------------
        // Копия настройки
        //-------------------------------------------------------------------
        private TabPage BuildCopyTab()
        {
            var page = new TabPage("Копия настройки");
            _copyText.Multiline = true;
            _copyText.ReadOnly = true;
            _copyText.ScrollBars = ScrollBars.Vertical;
            _copyText.Dock = DockStyle.Fill;
            _copyText.Font = new Font("Consolas", 10F);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(4) };
            var bRefresh = new Button { Text = "Обновить", AutoSize = true };
            _btnRestore.AutoSize = true;
            _btnRestore.Enabled = false;
            bar.Controls.AddRange(new Control[] { bRefresh, _btnRestore });
            bRefresh.Click += (s, e) => RequestCopies();
            _btnRestore.Click += (s, e) => Restore();

            page.Controls.Add(_copyText);
            page.Controls.Add(bar);
            return page;
        }

        private void RequestCopies()
        {
            lock (_lock)
            {
                _copy = null;
                _live = null;
                _copyAbsent = false;
            }
            _copyText.Text = "Спрашиваю мост и прибор…";
            Send(IdCopy);
            _cmd(CmdSend, 0, 0, 0); // прибор пришлёт свою запись, мост передаст её сюда
        }

        private void ShowCopies()
        {
            Calib copy, live;
            bool absent;
            lock (_lock)
            {
                copy = _copy;
                live = _live;
                absent = _copyAbsent;
            }

            var sb = new StringBuilder();
            sb.AppendLine("КОПИЯ В МОСТЕ");
            if (copy != null && copy.Complete) sb.AppendLine("  " + copy.Describe());
            else sb.AppendLine(absent ? "  копии нет - прибор ещё ни разу не присылал годную запись" : "  …");
            sb.AppendLine();
            sb.AppendLine("СЕЙЧАС В ПРИБОРЕ (во флеше)");
            if (live == null) sb.AppendLine("  …");
            else if (live.Status == 1) sb.AppendLine("  пусто - прибор не настроен");
            else if (live.Status == 2) sb.AppendLine("  запись не годится (другая версия или испорчена)");
            else if (live.Complete) sb.AppendLine("  " + live.Describe());
            else sb.AppendLine("  …");
            sb.AppendLine();

            bool canRestore = copy != null && copy.Complete;
            if (canRestore && live != null && live.Complete)
            {
                var diff = Enumerable.Range(0, Math.Min(copy.N, live.N)).Where(i => copy.Words[i] != live.Words[i]).ToList();
                if (copy.N != live.N)
                    sb.AppendLine($"Длина записи разная ({copy.N} и {live.N} слов): копия от другой версии прошивки, восстановить её прибор не даст.");
                else if (diff.Count == 0)
                    sb.AppendLine("Копия совпадает с тем, что в приборе. Восстанавливать нечего.");
                else
                    sb.AppendLine($"Отличаются слов: {diff.Count} (номера {string.Join(", ", diff.Take(20))}{(diff.Count > 20 ? ", …" : "")}).");
                if (copy.Uid0 != live.Uid0 || copy.Uid1 != live.Uid1)
                    sb.AppendLine("ВНИМАНИЕ: копия снята с ДРУГОГО кристалла - другой платы прибора.");
            }
            _copyText.Text = sb.ToString();
            _btnRestore.Enabled = canRestore && _restoreAt < 0;
        }

        private void Restore()
        {
            Calib copy, live;
            lock (_lock)
            {
                copy = _copy;
                live = _live;
            }
            if (copy == null || !copy.Complete) return;

            string warn = "";
            if (live != null && live.Complete && (copy.Uid0 != live.Uid0 || copy.Uid1 != live.Uid1))
                warn = "\n\nКОПИЯ СНЯТА С ДРУГОЙ ПЛАТЫ. Нули и масштабы АЦП у каждой платы свои - " +
                       "после восстановления их надо будет проверить.";
            if (MessageBox.Show(this,
                    "Записать в прибор копию настройки из моста?\n\n" + copy.Describe() +
                    "\n\nВ неё входят нули и масштабы АЦП, паспорт силовой части и коэффициенты регуляторов. " +
                    "Прибор примет её, только если мост не модулирует." + warn,
                    "Восстановление настройки", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _restoreWords = copy.Words.ToArray();
            _restoreAt = 0;
            _btnRestore.Enabled = false;
            SendNextWords();
        }

        private void SendNextWords()
        {
            _restoreTimer.Stop();
            if (_restoreAt < _restoreWords.Length)
            {
                int at = _restoreAt;
                ushort w0 = _restoreWords[at];
                ushort w1 = at + 1 < _restoreWords.Length ? _restoreWords[at + 1] : (ushort)0;
                _copyText.Text = $"Передаю копию прибору: слово {at} из {_restoreWords.Length}…";
                _cmd(CmdWords, w0, w1, (ushort)at);
            }
            else
            {
                _copyText.Text = "Прибор проверяет копию…";
                _cmd(CmdApply, (ushort)_restoreWords.Length, 0, 0);
            }
            _restoreTimer.Start();
        }

        private void RestoreFailed(string why)
        {
            _restoreTimer.Stop();
            _restoreAt = -1;
            MessageBox.Show(this, "Восстановление не выполнено: " + why + ".", "Восстановление настройки",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
            ShowCopies();
        }

        /// <summary>Из потока приёма ответов прибора.</summary>
        public void OnAck(ushort cmd, ushort d1, ushort d2, ushort d3)
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                if (_restoreAt < 0) return;
                if (cmd == CmdWords && d3 == _restoreAt)
                {
                    _restoreAt += 2;
                    SendNextWords();
                }
                else if (cmd == CmdApply)
                {
                    _restoreTimer.Stop();
                    _restoreAt = -1;
                    string[] res =
                    {
                        "", "не та длина записи - копия от другой версии прошивки",
                        "запись не сошлась (метка, версия или сумма)",
                        "мост модулирует - закройте ключи и повторите"
                    };
                    if (d3 == 0)
                    {
                        MessageBox.Show(this, "Прибор принял копию настройки и сохраняет её во флеш.",
                                        "Восстановление настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RequestCopies();
                    }
                    else
                    {
                        RestoreFailed(d3 < res.Length ? res[d3] : "код " + d3);
                    }
                }
            }));
        }

        //-------------------------------------------------------------------
        /// <summary>Из потока приёма WifiLink.</summary>
        public void OnRecord(uint id, byte[] d)
        {
            if (IsDisposed) return;
            switch (id)
            {
                case IdJRecs:
                    lock (_lock)
                        for (int i = 0; i + 16 <= d.Length; i += 16)
                        {
                            var r = new byte[16];
                            Array.Copy(d, i, r, 0, 16);
                            _rows.Add(r);
                        }
                    break;

                case IdJEnd:
                {
                    List<byte[]> rows;
                    lock (_lock) rows = _rows.ToList();
                    uint cap = d.Length >= 8 ? BitConverter.ToUInt32(d, 4) : 0;
                    uint lost = d.Length >= 12 ? BitConverter.ToUInt32(d, 8) : 0;
                    BeginInvoke(new Action(() => ShowJournal(rows, cap, lost)));
                    break;
                }

                case IdCopyHdr:
                    lock (_lock)
                    {
                        _copy = new Calib();
                        _copy.SetHdr(d);
                    }
                    break;
                case IdCopyData:
                    lock (_lock) _copy?.SetData(d);
                    break;
                case IdCopyEnd:
                    lock (_lock) _copyAbsent = d.Length < 1 || d[0] == 0;
                    BeginInvoke(new Action(ShowCopies));
                    break;

                case IdCalHdr:
                    lock (_lock)
                    {
                        _live = new Calib();
                        _live.SetHdr(d);
                    }
                    // Пустая или чужая запись слов не несёт - показать сразу.
                    if (_live.Status != 0) BeginInvoke(new Action(ShowCopies));
                    break;
                case IdCalData:
                    bool done;
                    lock (_lock)
                    {
                        _live?.SetData(d);
                        done = _live != null && _live.Complete;
                    }
                    if (done) BeginInvoke(new Action(ShowCopies));
                    break;
            }
        }
    }
}

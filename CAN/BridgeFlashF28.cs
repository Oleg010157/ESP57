using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ICDIBasic
{
    /*
     * Прошивка прибора через WiFi-мост - без PCAN.
     *
     * Не пересылка кадров загрузчику, как по CAN (Bootloader/Bootloader.cs), а
     * два шага. Сначала образ целиком уходит в мост (POST /api/f28img) и там
     * проверяется - прибор в это время работает как работал. Потом мост сам
     * прошивает прибор по SPI (POST /api/f28go): тот же протокол загрузчика,
     * но по проводам, так что обрыв WiFi посреди прошивки ничего не портит.
     * Ход - GET /api/f28. Сторона моста - ESP32_BRIDGE/main/fwup.c.
     *
     * Образ («K57F», формат - в fwup.h) режется из .out ровно так же, как для
     * CAN (Cnv2 и BuildLoadRuns в Bootloader.cs) и как на странице моста
     * (page/index.html, fwParse): сверено 12.11.2026 на invertor.out байт в байт.
     */
    public partial class Form1
    {
        private readonly ToolStripMenuItem _bridgeF28Menu = new ToolStripMenuItem("Прошить прибор через WiFi-мост…");

        private const uint F28FlashStart = 0x080000; // BANK0, как APP_START_ADDR загрузчика
        private const uint F28FlashEnd = 0x0FFFFF;   // BANK3; BANK4 с калибровками не трогается

        private void BridgeF28_InitMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Add(_bridgeF28Menu);
            _bridgeF28Menu.Click += async (s, e) => await BridgeF28_Run();
        }

        private sealed class F28Image
        {
            public byte[] Data;
            public uint Entry;
            public int Segments;
            public int Bytes;
        }

        /// <summary>Образ K57F из ELF .out прибора.</summary>
        private static F28Image BridgeF28_Build(byte[] b, string name, DateTime fileUtc)
        {
            uint U32(long o) => BitConverter.ToUInt32(b, (int)o);
            ushort U16(long o) => BitConverter.ToUInt16(b, (int)o);

            if (b.Length < 52 || U32(0) != 0x464C457F)
                throw new InvalidDataException("не ELF - нужен invertor.out из CPU1_FLASH");
            if (b[4] != 1 || b[5] != 1)
                throw new InvalidDataException("ждётся ELF32 младшим байтом вперёд");
            uint phoff = U32(28), shoff = U32(32);
            int phent = U16(42), phnum = U16(44), shent = U16(46), shnum = U16(48);

            // Вход - символ _c_int00, как у CAN-загрузки: e_entry здесь - code_start.
            long entry = -1;
            var want = Encoding.ASCII.GetBytes("_c_int00\0");
            for (int i = 0; i < shnum && entry < 0; i++)
            {
                long sh = shoff + (long)i * shent;
                if (U32(sh + 4) != 2) continue; // SHT_SYMTAB
                uint off = U32(sh + 16), size = U32(sh + 20), link = U32(sh + 24), es = U32(sh + 36);
                if (es == 0 || link >= shnum) continue;
                uint so = U32(shoff + link * shent + 16), ss = U32(shoff + link * shent + 20);
                for (uint k = 0; k * es < size; k++)
                {
                    uint n = U32(off + k * es);
                    if (n + want.Length > ss) continue;
                    int j = 0;
                    while (j < want.Length && b[so + n + j] == want[j]) j++;
                    if (j == want.Length) { entry = U32(off + k * es + 4); break; }
                }
            }
            if (entry < F28FlashStart || entry > F28FlashEnd)
                throw new InvalidDataException("нет символа _c_int00 во флеше - это не флеш-сборка прибора");

            var segs = new List<(uint pa, uint off, uint fsz)>();
            uint lo = uint.MaxValue, hi = 0;
            for (int i = 0; i < phnum; i++)
            {
                long ph = phoff + (long)i * phent;
                uint pa = U32(ph + 12), fsz = U32(ph + 16);
                if (U32(ph) != 1 || fsz == 0) continue; // PT_LOAD с данными
                uint end = pa + fsz / 2;
                if (end <= F28FlashStart) continue;     // целиком в ОЗУ - не флеш
                if (end - 1 > F28FlashEnd)
                    throw new InvalidDataException("в файле есть данные за BANK3 - калибровки (BANK4) прошивкой не пишутся");
                segs.Add((pa, U32(ph + 4), fsz));
                lo = Math.Min(lo, Math.Max(pa, F28FlashStart));
                hi = Math.Max(hi, end);
            }
            if (segs.Count == 0) throw new InvalidDataException("в файле нет данных для флеша");
            lo &= ~31u;
            hi = (hi + 31) & ~31u;

            var img = Enumerable.Repeat((byte)0xFF, (int)(hi - lo) * 2).ToArray();
            foreach (var (pa, off, fsz) in segs)
            {
                uint skip = pa < lo ? (lo - pa) * 2 : 0;
                Buffer.BlockCopy(b, (int)(off + skip), img, (int)((pa - lo) * 2 + skip), (int)(fsz - skip));
            }

            // Сегменты по 64 байта, сплошные 0xFF пропускаются - BuildLoadRuns.
            var runs = new List<(int start, int len)>();
            int runStart = -1;
            for (int p = 0; p <= img.Length; p += 64)
            {
                bool ff = p == img.Length;
                if (!ff)
                {
                    ff = true;
                    for (int k = p; k < p + 64; k++) if (img[k] != 0xFF) { ff = false; break; }
                }
                if (!ff && runStart < 0) runStart = p;
                if (ff && runStart >= 0) { runs.Add((runStart, p - runStart)); runStart = -1; }
            }

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int body = runs.Sum(r => 8 + r.len);
                w.Write(0x4637354Bu); // 'K57F'
                w.Write((ushort)1);
                w.Write((ushort)64);
                w.Write((uint)entry);
                w.Write((uint)runs.Count);
                w.Write((uint)body);
                w.Write((uint)new DateTimeOffset(fileUtc).ToUnixTimeSeconds());
                var nb = Encoding.UTF8.GetBytes(name);
                while (nb.Length > 40) nb = Encoding.UTF8.GetBytes(name = name.Substring(0, name.Length - 1));
                w.Write(nb);
                w.Write(new byte[40 - nb.Length]);
                foreach (var (start, len) in runs)
                {
                    w.Write(lo + (uint)start / 2);
                    w.Write((uint)len);
                    w.Write(img, start, len);
                }
                return new F28Image { Data = ms.ToArray(), Entry = (uint)entry, Segments = runs.Count, Bytes = runs.Sum(r => r.len) };
            }
        }

        /// <summary>Поле плоского JSON моста: строка (с \" и \\) или число.</summary>
        private static string BridgeJsonField(string json, string name)
        {
            var key = "\"" + name + "\":";
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return "";
            i += key.Length;
            if (i < json.Length && json[i] == '"')
            {
                var sb = new StringBuilder();
                for (i++; i < json.Length && json[i] != '"'; i++)
                {
                    if (json[i] == '\\' && i + 1 < json.Length) i++;
                    sb.Append(json[i]);
                }
                return sb.ToString();
            }
            int j = json.IndexOfAny(new[] { ',', '}' }, i);
            return j > i ? json.Substring(i, j - i) : "";
        }

        // Шаги - как FWUP_PH_* в ESP32_BRIDGE/main/fwup.h.
        private static readonly string[] F28Phases =
            { "проверка", "вход в загрузчик", "стирание", "запись", "сверка и пуск", "ответ прибора" };

        /// <summary>Время по шагам из ответа моста: "up", "ph":[..], "total".</summary>
        private static string BridgeF28_Times(string json)
        {
            string Sec(double ms) => (ms / 1000).ToString("0.0");
            double.TryParse(BridgeJsonField(json, "up"), out double up);
            double.TryParse(BridgeJsonField(json, "total"), out double total);
            var sb = new StringBuilder("в мост " + Sec(up) + " с");
            int i = json.IndexOf("\"ph\":[", StringComparison.Ordinal);
            int j = i < 0 ? -1 : json.IndexOf(']', i);
            if (j > i)
            {
                var ph = json.Substring(i + 6, j - i - 6).Split(',');
                for (int k = 0; k < ph.Length && k < F28Phases.Length; k++)
                    if (double.TryParse(ph[k], out double v))
                        sb.Append("\n").Append(F28Phases[k]).Append(' ').Append(Sec(v)).Append(" с");
            }
            sb.Append("\nвсего по часам моста ").Append(Sec(up + total)).Append(" с");
            return sb.ToString();
        }

        private static string BridgeErrorText(WebException ex)
        {
            if (!(ex.Response is HttpWebResponse r)) return ex.Message;
            if (r.StatusCode == HttpStatusCode.Unauthorized) return "неверный пароль";
            if (r.StatusCode == HttpStatusCode.NotFound) return "в мосте старая прошивка - обновите мост";
            using (var sr = new StreamReader(r.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                return sr.ReadToEnd();
        }

        private async Task BridgeF28_Run()
        {
            const string caption = "Прошивка прибора через мост";
            if (_wifi == null)
            {
                MessageBox.Show(this, "Прошивка идёт через WiFi-мост: включите «Связь через WiFi (мост K57)».\n" +
                                      "По CAN - «Прошивка» в меню, как раньше.",
                                caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string host = _wifi.Host;

            string file;
            using (var dlg = new OpenFileDialog
                   {
                       Title = "Прошивка прибора - invertor.out",
                       Filter = "Прошивка прибора (*.out)|*.out",
                       FileName = "invertor.out"
                   })
            {
                var dir = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "..", "INVERTOR_CCS", "CPU1_FLASH"));
                if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                file = dlg.FileName;
            }

            F28Image img;
            try
            {
                img = BridgeF28_Build(File.ReadAllBytes(file), Path.GetFileName(file), File.GetLastWriteTimeUtc(file));
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is ArgumentException || ex is IOException)
            {
                MessageBox.Show(this, "Файл не годится: " + ex.Message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pass = BridgeAskPassword();
            if (pass == null) return;

            if (MessageBox.Show(this,
                    $"Прошить прибор файлом\n{file}\n({img.Bytes / 1024} кБ, {img.Segments} сегм., " +
                    $"{File.GetLastWriteTime(file):dd.MM.yyyy HH:mm})?\n\n" +
                    "Сначала файл уйдёт в мост, потом мост сам прошьёт прибор по SPI - около полуминуты.\n" +
                    "Сеанс с прибором должен быть закрыт («Отключить»), ключи - закрыты.\n" +
                    "Питание прибора во время прошивки не снимать.",
                    caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            string auth = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("k57:" + pass));
            // Полный цикл - от начала передачи образа до ответа новой прошивки.
            var cycle = System.Diagnostics.Stopwatch.StartNew();
            using (var prog = new BridgeUpdateForm { Text = caption })
            {
                prog.Show(this);

                // 1. Образ - в мост.
                using (var wc = new WebClient { Encoding = Encoding.UTF8 })
                {
                    wc.Headers[HttpRequestHeader.Authorization] = auth;
                    wc.Headers[HttpRequestHeader.ContentType] = "application/octet-stream";
                    wc.Headers["X-K57-Time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                    wc.UploadProgressChanged += (s, e) =>
                        prog.Show($"Передаю образ в мост: {e.BytesSent / 1024} из {img.Data.Length / 1024} кБ",
                                  (int)(100L * e.BytesSent / Math.Max(1, img.Data.Length)));
                    try
                    {
                        await wc.UploadDataTaskAsync("http://" + host + "/api/f28img", "POST", img.Data);
                    }
                    catch (WebException ex)
                    {
                        prog.Close();
                        MessageBox.Show(this, "Мост не принял образ: " + BridgeErrorText(ex), caption,
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                // 2. Пуск прошивки.
                using (var wc = new WebClient { Encoding = Encoding.UTF8 })
                {
                    wc.Headers[HttpRequestHeader.Authorization] = auth;
                    try
                    {
                        await wc.UploadStringTaskAsync("http://" + host + "/api/f28go", "POST", "");
                    }
                    catch (WebException ex)
                    {
                        prog.Close();
                        MessageBox.Show(this, "Образ в мосте, но прошивка не начата: " + BridgeErrorText(ex) +
                                              "\n\nПовторить можно отсюда же или со страницы моста, вкладка «Прошивка».",
                                        caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                // 3. Ход. Мост ведёт прошивку сам; отсюда только смотрим. Пропала
                //    связь с мостом - прошивка от этого не встанет.
                string state = "run", msg = "", last = "";
                var until = DateTime.Now.AddMinutes(3);
                while (state == "run" && DateTime.Now < until)
                {
                    await Task.Delay(500);
                    using (var wc = new WebClient { Encoding = Encoding.UTF8 })
                    {
                        try
                        {
                            var t = wc.DownloadStringTaskAsync("http://" + host + "/api/f28");
                            if (await Task.WhenAny(t, Task.Delay(3000)) != t) continue;
                            var json = await t;
                            last = json;
                            state = BridgeJsonField(json, "state");
                            msg = BridgeJsonField(json, "msg");
                            int.TryParse(BridgeJsonField(json, "pct"), out int pct);
                            prog.Show(msg, pct);
                        }
                        catch (WebException) { /* мост занят флешем или WiFi мигнул - спросим ещё */ }
                    }
                }
                cycle.Stop();
                prog.Close();

                MessageBox.Show(this,
                    state == "ok" ? msg + "\n\nВремя:\n" + BridgeF28_Times(last) +
                                    $"\nвесь цикл здесь {cycle.Elapsed.TotalSeconds:0.0} с" :
                    state == "run" ? "Мост не дождался конца за 3 минуты: " + msg + "\nПосмотрите на странице моста, вкладка «Прошивка»." :
                    msg + "\n\nОбраз остался в мосте - повторить можно со страницы моста или отсюда.",
                    caption, MessageBoxButtons.OK, state == "ok" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }
    }
}

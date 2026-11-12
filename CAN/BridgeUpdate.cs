using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ICDIBasic
{
    /*
     * Обновление прошивки WiFi-моста по сети - без USB-кабеля.
     *
     * Мост принимает файл build\esp32_bridge.bin запросом POST /update
     * (ESP32_BRIDGE/main/web.c), пишет его в свободное из двух мест под
     * программу и перезапускается. Новая прошивка сначала ПРОБНАЯ: если за
     * минуту она себя не подтвердит (не поднялась, зависла), мост сам вернётся
     * к прежней. Связь WifiLink после перезапуска восстанавливает сама.
     *
     * Пароль - пользователь k57 и CONFIG_K57_ADMIN_PASS моста; не задан -
     * пароль сети K57.
     */
    public partial class Form1
    {
        private readonly ToolStripMenuItem _bridgeUpdMenu = new ToolStripMenuItem("Обновить прошивку WiFi-моста…");

        private void BridgeUpdate_InitMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Add(_bridgeUpdMenu);
            _bridgeUpdMenu.Click += async (s, e) => await BridgeUpdate_Run();
        }

        private static string BridgeVersionText(string json)
        {
            // Разбор без библиотеки JSON: поля плоские, кавычек внутри нет.
            string Field(string name)
            {
                var key = "\"" + name + "\":";
                int i = json.IndexOf(key, StringComparison.Ordinal);
                if (i < 0) return "?";
                i += key.Length;
                bool quoted = i < json.Length && json[i] == '"';
                if (quoted) i++;
                int j = quoted ? json.IndexOf('"', i) : json.IndexOfAny(new[] { ',', '}' }, i);
                return j > i ? json.Substring(i, j - i) : "?";
            }
            return $"{Field("version")} от {Field("date")} {Field("time")}, место {Field("partition")}" +
                   (Field("pending") == "true" ? " - пробная, ещё не подтвердилась" : "");
        }

        private static async Task<string> BridgeGetVersion(string host)
        {
            using (var wc = new WebClient { Encoding = Encoding.UTF8 })
            {
                var t = wc.DownloadStringTaskAsync("http://" + host + "/version");
                if (await Task.WhenAny(t, Task.Delay(3000)) != t) return null;
                try { return BridgeVersionText(await t); }
                catch { return null; }
            }
        }

        private async Task BridgeUpdate_Run()
        {
            if (_wifi == null)
            {
                MessageBox.Show(this, "Обновление идёт через WiFi-мост: включите «Связь через WiFi (мост K57)».",
                                "Прошивка моста", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string host = _wifi.Host;
            string before = await BridgeGetVersion(host);
            if (before == null)
            {
                MessageBox.Show(this, $"Мост {host} не отвечает по HTTP.\nВозможно, в нём прошивка без обновления по WiFi - " +
                                      "её надо один раз залить по USB (idf.py flash).",
                                "Прошивка моста", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string file;
            using (var dlg = new OpenFileDialog
                   {
                       Title = "Прошивка моста - esp32_bridge.bin",
                       Filter = "Прошивка ESP32 (*.bin)|*.bin",
                       FileName = "esp32_bridge.bin"
                   })
            {
                var build = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "..", "ESP32_BRIDGE", "build"));
                if (Directory.Exists(build)) dlg.InitialDirectory = build;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                file = dlg.FileName;
            }

            byte[] image = File.ReadAllBytes(file);
            // Образ ESP32 начинается с 0xE9. Не он - мост всё равно отверг бы
            // файл, но лучше сказать сразу, до стирания места под программу.
            if (image.Length < 1024 || image[0] != 0xE9)
            {
                MessageBox.Show(this, "Это не прошивка ESP32: нужен build\\esp32_bridge.bin, а не bootloader.bin и не " +
                                      "partition-table.bin.", "Прошивка моста", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pass = BridgeAskPassword();
            if (pass == null) return;

            if (MessageBox.Show(this,
                    $"Сейчас в мосте:\n{before}\n\nЗалить\n{file}\n({image.Length / 1024} кБ, " +
                    $"{File.GetLastWriteTime(file):dd.MM.yyyy HH:mm})?\n\n" +
                    "Пока пишется флеш, живая лента прервётся; потом мост перезапустится и связь вернётся сама.",
                    "Прошивка моста", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            using (var prog = new BridgeUpdateForm())
            using (var wc = new WebClient { Encoding = Encoding.UTF8 })
            {
                prog.Show(this);
                wc.Headers[HttpRequestHeader.Authorization] =
                    "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("k57:" + pass));
                wc.Headers[HttpRequestHeader.ContentType] = "application/octet-stream";
                wc.UploadProgressChanged += (s, e) =>
                    prog.Show($"Передаю: {e.BytesSent / 1024} из {image.Length / 1024} кБ",
                              (int)(100L * e.BytesSent / Math.Max(1, image.Length)));

                string answer;
                try
                {
                    var reply = await wc.UploadDataTaskAsync("http://" + host + "/update", "POST", image);
                    answer = Encoding.UTF8.GetString(reply);
                }
                catch (WebException ex)
                {
                    string body = "";
                    if (ex.Response is HttpWebResponse r)
                    {
                        using (var sr = new StreamReader(r.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                            body = sr.ReadToEnd();
                        if (r.StatusCode == HttpStatusCode.Unauthorized) body = "неверный пароль";
                    }
                    prog.Close();
                    MessageBox.Show(this, "Прошивка не залита: " + (body.Length > 0 ? body : ex.Message),
                                    "Прошивка моста", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Мост перезапускается. Ждём, пока он поднимется и пока новая
                // прошивка подтвердит себя (20 с работы, web.c).
                string after = null;
                for (int i = 0; i < 60; i++)
                {
                    prog.Show(i < 5 ? "Мост перезапускается…" : $"Жду, пока новая прошивка подтвердится… {i} с", 100);
                    await Task.Delay(1000);
                    if (i < 5) continue;
                    after = await BridgeGetVersion(host);
                    if (after != null && !after.Contains("пробная")) break;
                }
                prog.Close();

                MessageBox.Show(this,
                    "Мост ответил: " + answer + "\n\nБыло:\n" + before + "\n\nСтало:\n" +
                    (after ?? "мост пока не отвечает - если через минуту не появится, он вернулся к прежней прошивке"),
                    "Прошивка моста", MessageBoxButtons.OK,
                    after != null && !after.Contains("пробная") ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }

        private string BridgeAskPassword()
        {
            using (var f = new Form
                   {
                       Text = "Пароль моста",
                       FormBorderStyle = FormBorderStyle.FixedDialog,
                       StartPosition = FormStartPosition.CenterParent,
                       ClientSize = new Size(360, 110),
                       MaximizeBox = false,
                       MinimizeBox = false
                   })
            {
                var lbl = new Label
                {
                    Text = "Пароль на обновление (если не задан особо - пароль сети K57):",
                    Location = new Point(10, 10),
                    Size = new Size(340, 20)
                };
                var box = new TextBox { Location = new Point(10, 34), Width = 340, UseSystemPasswordChar = true };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(190, 72) };
                var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Location = new Point(275, 72) };
                f.Controls.AddRange(new Control[] { lbl, box, ok, cancel });
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                return f.ShowDialog(this) == DialogResult.OK ? box.Text : null;
            }
        }
    }

    internal sealed class BridgeUpdateForm : Form
    {
        private readonly Label _text = new Label { Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ProgressBar _bar = new ProgressBar { Dock = DockStyle.Top, Height = 22 };

        public BridgeUpdateForm()
        {
            Text = "Прошивка моста";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 70);
            ControlBox = false;
            Padding = new Padding(8);
            Controls.Add(_bar);
            Controls.Add(_text);
        }

        public void Show(string text, int percent)
        {
            _text.Text = text;
            _bar.Value = Math.Max(0, Math.Min(100, percent));
        }
    }
}

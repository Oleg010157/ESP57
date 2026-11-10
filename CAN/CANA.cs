/*
 В этом файле собраны переменные и методы для:
 подключения CAN A адаптера,
 асинхронного приёма данных от КСП через CAN A
*/

using Peak.Can.Basic;
using System.Collections.Concurrent;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ICDIBasic
{
    public partial class Form1
    {
        #region CAN A — подключение и приём

        private AutoResetEvent _re;
        private bool _rden;
        public byte Ch;
        private readonly BackgroundWorker _crw = new BackgroundWorker { WorkerSupportsCancellation = true };
        private bool _crwHooked;
        // true если последний ping (cmd=0) вернул d3 bit0=1 (MCU уже в состоянии connect)
        private bool _mcuAlreadyConnected;
        // true если MCU передаёт телеметрию по CAN FD (ID=140, CANA_FD=1), false — классика (IDs 100-132)
        private bool _mcuUsesFD;
        // Queue for bootloader responses (MCU -> PC ID 0x32)
        private readonly ConcurrentQueue<ushort> _bootRespQueue = new ConcurrentQueue<ushort>();
        private readonly AutoResetEvent _bootRespEvent = new AutoResetEvent(false);

        // WiFi-мост вместо PCAN (CAN/WifiLink.cs). null - работаем через PCAN.
        // Выбор канала - пункт меню «Связь через WiFi», хранится в link.ini.
        private WifiLink _wifi;

        /* link.ini рядом с программой:
         *     link=wifi           или link=can (так же, как без файла)
         *     host=192.168.4.1    адрес моста; по умолчанию - его своя точка K57
         * Файл, а не Settings: адрес моста правят руками на месте, а формат
         * user.config для этого неудобен. */
        private static readonly string LinkIniPath =
            System.IO.Path.Combine(Application.StartupPath, "link.ini");

        private bool _linkLoaded;
        private bool _linkWifiSel;
        private string _linkHost = WifiLink.DefaultHost;

        /// <summary>Выбран WiFi-мост (из link.ini, читается при первом обращении).</summary>
        private bool LinkWifi
        {
            get { LoadLinkIni(); return _linkWifiSel; }
            set { LoadLinkIni(); _linkWifiSel = value; }
        }

        /// <summary>Адрес моста, если выбран WiFi; null - работаем через PCAN.</summary>
        private string LinkWifiHost => LinkWifi ? _linkHost : null;

        private void LoadLinkIni()
        {
            if (_linkLoaded) return;
            _linkLoaded = true;
            try
            {
                if (!System.IO.File.Exists(LinkIniPath)) return;
                foreach (var raw in System.IO.File.ReadAllLines(LinkIniPath))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("link=", StringComparison.OrdinalIgnoreCase))
                        _linkWifiSel = line.Substring(5).Trim().Equals("wifi", StringComparison.OrdinalIgnoreCase);
                    else if (line.StartsWith("host=", StringComparison.OrdinalIgnoreCase) && line.Length > 5)
                        _linkHost = line.Substring(5).Trim();
                }
            }
            catch
            {
                // нечитаемый файл - работаем как без него, через PCAN
            }
        }

        private void SaveLinkIni()
        {
            try
            {
                System.IO.File.WriteAllText(LinkIniPath,
                    "# Связь с прибором: link=wifi - через мост ESP32, link=can - через PCAN.\r\n" +
                    $"link={(LinkWifi ? "wifi" : "can")}\r\n" +
                    $"host={_linkHost}\r\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить {LinkIniPath}: {ex.Message}", "Связь",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Подписи пунктов меню - по выбранному каналу: «CAN A» при работе через
        // WiFi путало (замечание оператора, 10.11.2026). Делают они в обоих
        // случаях одно: CanaConnect() / CanI(), а CanI() сам выбирает канал.
        private void UpdateLinkMenuText()
        {
            _checkCanAMenu.Text = LinkWifi ? @"Проверить связь WiFi" : @"Проверить связь CANA";
            _reInitCanAMenu.Text = LinkWifi ? @"Переподключить WiFi" : @"Реинициализация CAN A";
        }

        // Пункт меню «Связь через WiFi (мост K57)». Менять канал посреди
        // сеанса нельзя: сеанс прибора и лента привязаны к тому, кто их открыл.
        private void WifiMenu_Click(object sender, EventArgs e)
        {
            if (tsmConnect.Text == @"Отключить")
            {
                MessageBox.Show("Сначала отключитесь от прибора, потом меняйте канал.", "Связь",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                _wifiMenu.Checked = LinkWifi;
                return;
            }

            CanaDisconnect();

            // Поток приёма PCAN уже вышел из цикла, но IsBusy у BackgroundWorker
            // снимается только в RunWorkerCompleted, а его доставляет очередь
            // сообщений окна. Мы сейчас в обработчике меню, очередь стоит - и
            // ожидание в WifiI() не кончалось никогда: мост отвечал, а связь
            // отбрасывалась по таймауту (стенд, 10.11.2026). Даём окну
            // разобрать сообщения, пока поток не освободится.
            for (var i = 0; i < 100 && _crw.IsBusy; i++)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }

            LinkWifi = !LinkWifi;
            _wifiMenu.Checked = LinkWifi;
            UpdateLinkMenuText();
            SaveLinkIni();
            CanaConnect();
            CanRate_ShowTitle(); // и обратно на скорость CAN, если вернулись к PCAN

            if (LinkWifi && !CanaLinkUp)
            {
                MessageBox.Show(
                    (LinkWifiHost == WifiLink.DefaultHost
                        ? $"Мост {LinkWifiHost} не ответил по TCP {WifiLink.TcpPort}.\r\n"
                        : $"Мост не ответил по TCP {WifiLink.TcpPort} ни на {LinkWifiHost}, ни на {WifiLink.DefaultHost}.\r\n") +
                    "Компьютер должен быть в сети K57 (своя точка моста, пароль k57bridge) " +
                    "или в одной сети с мостом. Адрес моста - в link.ini рядом с программой.",
                    "Связь через WiFi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Канал поднят: PCAN нашёл адаптер или мост ответил по TCP. Ch == 0
        // по-прежнему значит «нет PCAN», но не «нет связи».
        private bool CanaLinkUp => _wifi != null ? _wifi.Connected : Ch != 0;

        private void CanaDisconnect()
        {
            _crw.CancelAsync();
            _rden = CanA_Ok = tsmConnect.Enabled = false;
            _canaM.BackColor = Color.Yellow;
            if (_wifi != null)
            {
                _wifi.Dispose();
                _wifi = null;
            }
            PCANBasic.Uninitialize(Ch);
            Ch = 0;
        }

        private void CanaConnect()
        {
            CanA_Ok = false;
            CanaM_TextChanged(this, null);

            var connected = CanI();
            if (!connected)
            {
                Thread.Sleep(300);
                connected = CanI();
            }

            if (connected)
            {
                _canaM.BackColor = Color.Yellow;
            }
            else
            {
                CanA_Ok = false;
                _canaM.BackColor = Color.Salmon;
            }
        }

        private void ForceReinitCanA()
        {
            _rden = false;

            if (_crw.IsBusy && _crw.WorkerSupportsCancellation)
            {
                _crw.CancelAsync();
                for (var i = 0; i < 300 && _crw.IsBusy; i++)
                {
                    Thread.Sleep(10);
                }
            }

            if (Ch != 0)
            {
                PCANBasic.Uninitialize(Ch);
                Ch = 0;
            }
            if (_wifi != null)
            {
                _wifi.Dispose();
                _wifi = null;
            }

            CanA_Ok = false;

            // Реинициализация без промежуточного CanaConnect(), чтобы не получать
            // ложный "лосось" при первом клике.
            var connected = CanI();
            if (!connected)
            {
                Thread.Sleep(250);
                connected = CanI();
            }

            if (!connected || !CanaLinkUp)
            {
                _canaM.BackColor = Color.Salmon;
                MessageBox.Show(
                    "Не удалось инициализировать CAN A. Проверьте, что канал не занят (например, PCAN-View).",
                    "CAN A",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _canaM.BackColor = Color.Yellow;
            CanA_Ok = true;
            MessageBox.Show("Инициализация CAN A выполнена.", "CAN A", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tsmConnect.BackColor = Color.Yellow;
            // If bootloader left the form title as the final "Готово!", replace it
            // with the normal application title shown after a user-initiated connect.
            if (Text == @"Готово!")
            {
                Text = "Motor Control";
            }

            var connect = tsmConnect.Text == @"Подключить";
            if (connect)
            {
                await CanRate_BeforeConnect();
            }
            cmd_128(connect ? (ushort)2 : (ushort)3);
        }

        private void ReInitCanAMenu_Click(object sender, EventArgs e)
        {
            _canaM.BackColor = Color.Yellow;
            PulseCanCheckMenu();

            if (CanI())
                cmd_128(0);
        }

        // Время для журнала аварий прибора: своих часов у него нет, дальше он
        // считает секунды сам (INVERTOR_CCS/my_Files/wall_clock.h). UTC, секунды
        // с 1970: d1 - младшее слово, d2 - старшее, d3 = 1 - время верное.
        private void SendWallClock()
        {
            uint utc = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            uint_128((ushort)(utc & 0xFFFF), (ushort)(utc >> 16), 1, 337);
        }

        private void connect_CAN()
        {
            _throttleCanTraffic = true;
            try
            {
                timer_tick.Enabled = true; // Начинает функционировать основной таимер
                debugToolStripMenuItem.Enabled = true; // Включаем меню debug
                гToolStripMenuItem.Enabled = true; // GRAPH == Enabled
                btnStartStop.Visible = true;

                StopAin();
                установкиДвигателяToolStripMenuItem.Enabled = false;
                cBox_Gain_Limit.Checked = true;
                meterTimer.Enabled = true;

                // Отправляем данные по ограниченям тока и напряжений
                uint_128((ushort)_fResis.voltMax.Value, 3333, 0, 55);
                uint_128((ushort)_fResis.voltMin.Value, 3333, 0, 56);

                Tbar_Gain1.Value = Tbar_Gain1.Minimum;
                NUD_Gain1.Value = NUD_Gain1.Minimum;
                Tbar_Gain2.Value = Tbar_Gain2.Minimum;
                NUD_Gain2.Value = NUD_Gain2.Minimum;
                Gain_Mode_Refresh(M1);
                int_128(M1.gain.tbar.Value, 3333, M1.gain.modeH_kod, 75);

                if (Gain_Sym.Checked)
                {
                    Copy_NT(M2.gain, M1.gain);
                }

                Gain_Mode_Refresh(M2);
                int_128(M2.gain.tbar.Value, 3333, M2.gain.modeH_kod, 76);

                // Обнуляем задание при подключении, чтобы сеанс не начинался с
                // числа, оставшегося от прошлого. Когда движок переключён на
                // частоту (SpeedSlider_ToFreq), тем же органом крутят герцы:
                // его шкала начинается с 40, ноль в неё не лезет, и WinForms
                // бросает ArgumentOutOfRangeException. Обороты в этом режиме
                // лежат в заначке SpeedSlider_ToFreq - обнуляем её, и вернувшись
                // к оборотам оператор получит ноль, а не прошлое задание.
                if (_freqOnSpeed)
                {
                    _nudRpmVal = Satur(0m, _nudRpmMin, _nudRpmMax);
                    _rpmVal    = Satur(0, _rpmMin, _rpmMax);
                }
                else
                {
                    NUD_RPM.Value = Satur(0, NUD_RPM.Minimum, NUD_RPM.Maximum);
                }

                uint_128(0xA6, 0, 0, 338); // !!! Выкл ручн. упр. IGBT
                SendWallClock();
                refresh_Form1();

                if (!IdRcvd)
                {
                    var delayedFirmwareInfoRequest = new System.Windows.Forms.Timer { Interval = 1200 };
                    delayedFirmwareInfoRequest.Tick += (s, e) =>
                    {
                        delayedFirmwareInfoRequest.Stop();
                        delayedFirmwareInfoRequest.Dispose();
                        if (_rden && !IdRcvd)
                        {
                            cmd_128(333); // Запрос после завершения стартовых ACK, чтобы не мешать инициализации.
                        }
                    };
                    delayedFirmwareInfoRequest.Start();
                }
            }
            finally
            {
                _throttleCanTraffic = false;
            }
        }

        private void disconnect_CAN()
        {
            timer_tick.Enabled = false;
            pb_CANa_Tx.Visible = true; // Если индикатор стал невидимым в момент выключения
            pb_CANa_Rx.Visible = true;
            установкиДвигателяToolStripMenuItem.Enabled = true;
            meterTimer.Enabled = false;
            btnStartStop.Visible = false;
            NUD_Gain1.ForeColor = Color.Black;
            NUD_Gain1.BackColor = Color.Empty;
            NUD_Gain2.ForeColor = Color.Black;
            NUD_Gain2.BackColor = Color.Empty;
            NUD_Load1.ForeColor = Color.Black;
            NUD_Load1.BackColor = Color.Empty;
            rpm_ramp.ForeColor = Color.Black;
            NUD_Load1.BackColor = Color.Empty;
            Imax_Value.ForeColor = Color.Black;
            Imax_Value.BackColor = Color.Empty;
            NUD_RPM.ForeColor = Color.Black;
            NUD_RPM.BackColor = Color.Empty;
            tKadr60.ForeColor = Color.Black;
            tKadr60.BackColor = Color.Empty;
            Spl_Writers_Stop();   //Останавливаем запись .spl .spf  файлов
            _firstFlags = false;
            AIN2_motorName.Enabled = false;
        }

        // метод поиска и подключения CAN адаптера к одному из свободных USB-портов: 
        private bool CanI()
        {
            if (LinkWifiHost != null) return WifiI();

            var cha = new[] { (byte)81, (byte)82, (byte)83, (byte)84, (byte)85, (byte)86, (byte)87, (byte)88 };

            if (Ch != 0 && _crw.IsBusy)
            {
                _rden = true;
                cmd_128(0);
                return true;
            }

            _re = new AutoResetEvent(false);

            /* ===================== ПАМЯТКА ПО НАСТРОЙКЕ CAN FD =====================
               Базовая частота (Clock) PCAN адаптера жестко зафиксирована на: 80 МГц.

               Формула для расчета итоговой скорости: 
               Скорость = f_clock / (brp * (1 + tseg1 + tseg2))
               Точка сэмплирования (Sample Point) = (1 + tseg1) / (1 + tseg1 + tseg2)

               Здесь задаются две скорости в одной строке:
               1) nom_*  - Арбитражная фаза (скорость заголовка кадра). Всегда 1 Мбит/с.
               2) data_* - Фаза данных (полезная нагрузка). Включается флагом BRS.

               Менять частоту можно просто закомментировав текущую строку "var fdBitrate"
               и раскомментировав нужную из списка ниже.
               ======================================================================= */

            // 1 Мбит/с: 1 Мбит/с арбитраж / 1 Мбит/с данные (Sample Point 80%) -- Без ускорения данных
            // var fdBitrate = "f_clock=80000000,nom_brp=1,nom_tseg1=63,nom_tseg2=16,nom_sjw=16,data_brp=1,data_tseg1=63,data_tseg2=16,data_sjw=16";

            // 2 Мбит/с: 1 Мбит/с арбитраж / 2 Мбит/с данные (Sample Point 80%) --
           // var fdBitrate = "f_clock=80000000,nom_brp=1,nom_tseg1=63,nom_tseg2=16,nom_sjw=16,data_brp=1,data_tseg1=31,data_tseg2=8,data_sjw=8";

            // 4 Мбит/с: 1 Мбит/с арбитраж / 4 Мбит/с данные (Sample Point 80%) --
        //     var fdBitrate = "f_clock=80000000,nom_brp=1,nom_tseg1=63,nom_tseg2=16,nom_sjw=16,data_brp=1,data_tseg1=15,data_tseg2=4,data_sjw=4";

            // 5 Мбит/с 1 Мбит/с арбитраж / 5 Мбит/с данные (Sample Point 75%) --
            //    var fdBitrate = CanBitrateFast;

            // 2.5 Мбит/с: 1 Мбит/с арбитраж / 2.5 Мбит/с данные (SP 75%) -- 80/(1*32)=2.5 Мбит/с
            // Сеанс всегда начинается отсюда, на 5 Мбит/с переходим пробой (CanaRate.cs).
            var fdBitrate = CanBitrateSlow;
            _canFast = false;
            // f_clock=150 МГц (эквивалентный прескалер), nom (SP=52%): tseg1=12, tseg2=12, sjw=11 | data (SP=53.3%): tseg1=7, tseg2=7, sjw=6
            //     var fdBitrate = "f_clock=150000000,nom_brp=6,nom_tseg1=12,nom_tseg2=12,nom_sjw=11,data_brp=2,data_tseg1=7,data_tseg2=7,data_sjw=6";
            //  8 Мбит/с: 1 Мбит/с арбитраж / 8 Мбит/с данные (Sample Point 80%) --
            // var fdBitrate = "f_clock=80000000,nom_brp=1,nom_tseg1=63,nom_tseg2=16,nom_sjw=16,data_brp=1,data_tseg1=7,data_tseg2=2,data_sjw=2";

            try
            {
                uint iBuffer;
                TPCANStatus stsResult;
                foreach (var t in cha)
                {
                    stsResult = PCANBasic.GetValue(t, TPCANParameter.PCAN_CHANNEL_CONDITION, out iBuffer, sizeof(uint));
                    if (stsResult != TPCANStatus.PCAN_ERROR_OK) continue;
                    System.Diagnostics.Debug.WriteLine($"[PCAN] ch={t} cond={iBuffer} (1=AVAILABLE,2=OCCUPIED,0=UNAVAIL)");
                    if (iBuffer != PCANBasic.PCAN_CHANNEL_AVAILABLE) continue;

                    stsResult = PCANBasic.InitializeFD(t, fdBitrate);
                    if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PCAN] InitializeFD ch={t} error: {stsResult} (0x{(uint)stsResult:X8})");
                        continue;
                    }

                    stsResult = PCANBasic.GetValue(t, TPCANParameter.PCAN_DEVICE_ID, out iBuffer, sizeof(uint));
                    if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                    {
                        // устройство не вернуло номер — размонтировать и продолжить поиск
                        PCANBasic.Uninitialize(t);
                        continue;
                    }
                    System.Diagnostics.Debug.WriteLine($"[PCAN] ch={t} DeviceID={iBuffer} (0x{iBuffer:X2}), CanAId={CanAId} (0x{CanAId:X2})");
                    if (iBuffer == CanAId)
                    {
                        PCANBasic.Uninitialize(t);
                        Ch = t;
                        break;
                    }
                    PCANBasic.Uninitialize(t);
                }

                // проверяем, найден ли канал
                if (Ch == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[PCAN] Канал не найден (Ch==0 после поиска)");
                    return false;
                }

                // инициализация найденного канала
                stsResult = PCANBasic.InitializeFD(Ch, fdBitrate);
                if (stsResult != TPCANStatus.PCAN_ERROR_OK) return false;

                // корректное получение дескриптора события для SetValue (без усечения на x64)
                var handlePtr = _re.SafeWaitHandle.DangerousGetHandle();
                ulong eventHandle = unchecked((ulong)handlePtr.ToInt64());
                stsResult = PCANBasic.SetValue(Ch, TPCANParameter.PCAN_RECEIVE_EVENT, ref eventHandle, sizeof(ulong));
                if (stsResult != TPCANStatus.PCAN_ERROR_OK) return false;
                if (_crw == null) return false;
                if (!_crwHooked)
                {
                    _crw.DoWork += Crw_DoWork;
                    _crwHooked = true;
                }
                if (_crw.IsBusy)
                {
                    for (var i = 0; i < 200 && _crw.IsBusy; i++)
                    {
                        Thread.Sleep(10);
                    }

                    if (_crw.IsBusy)
                    {
                        PCANBasic.Uninitialize(Ch);
                        Ch = 0;
                        return false;
                    }
                }

                _rden = true;
                _crw.RunWorkerAsync();

                // запуск обмена с контроллером: посылаем несколько раз,
                // чтобы не зависеть от единичной потери первого кадра после старта UI
         //       for (var ping = 0; ping < 5; ping++)
                {
                    cmd_128(0);
         //           Thread.Sleep(20);
                }
                return true;
            }
            catch
            {
                Ch = 0;
                return false;
            }
        }

        /*
         * Попросить Windows подключиться к сети моста.
         *
         * Профиль K57 стоит на «подключаться автоматически», а Windows всё
         * равно в неё не возвращается: когда есть кабель с интернетом, она
         * сводит к минимуму одновременные подключения и сеть без интернета
         * сама не берёт. Оператору приходилось каждый раз подключать K57
         * руками (10.11.2026). Явный «netsh wlan connect» Windows выполняет и
         * держит - как ручное подключение. Прав администратора не нужно.
         *
         * Профиль должен уже быть: его оставляет первое ручное подключение к
         * K57. Нет профиля - netsh откажет, и всё останется как было.
         */
        private static void JoinBridgeAp(string ssid)
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", $"wlan connect name=\"{ssid}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit(5000);
                    System.Diagnostics.Debug.WriteLine($"[WiFi] netsh wlan connect {ssid}: {p?.StandardOutput.ReadToEnd().Trim()}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WiFi] netsh: {ex.Message}");
            }
        }

        // Подключение через WiFi-мост. Повторяет CanI() для PCAN: тот же поток
        // приёма, тот же _re, тот же пинг cmd_128(0) в конце.
        private bool WifiI()
        {
            if (_wifi != null && _wifi.Connected && _crw.IsBusy)
            {
                _rden = true;
                cmd_128(0);
                return true;
            }

            _wifi?.Dispose();
            _wifi = null;

            _re = new AutoResetEvent(false);

            // Имя сети мост шлёт сразу после подключения, из потока приёма, -
            // заголовок обновится, когда окно до него доберётся.
            WifiLink NewLink(string host)
            {
                var l = new WifiLink(host, _re);
                l.NetInfo += () => BeginInvoke(new Action(() => CanRate_ShowTitle()));
                l.BridgeRecord += BlackBox_OnRecord; // чёрный ящик моста, Oleg/BlackBox.cs
                // Обрыв и возврат связи WifiLink переживает сам; окну - только
                // сказать оператору, что сейчас связи нет.
                l.LinkChanged += () => BeginInvoke(new Action(() =>
                    CanRate_ShowTitle(_wifi != null && !_wifi.Connected ? "нет связи, переподключаюсь" : null)));
                // Своя точка моста: долгий обрыв - скорее всего, Windows не
                // вернулась в K57 сама (перезагрузка моста), - просим её.
                if (host == WifiLink.DefaultHost) l.Rejoin = () => JoinBridgeAp(WifiLink.DefaultApSsid);
                return l;
            }

            WifiLink link = null;
            if (LinkWifiHost != WifiLink.DefaultHost)
            {
                link = NewLink(LinkWifiHost);
                if (!link.Connect())
                {
                    System.Diagnostics.Debug.WriteLine($"[WiFi] мост {LinkWifiHost} не ответил");
                    link = null;
                }
            }

            // Не вошёл в общую сеть за 8 попыток - мост поднимает свою точку
            // K57 и живёт на 192.168.4.1. Так бывает при слабом сигнале до
            // роутера (стенд, 10.11.2026), и править link.ini на каждый такой
            // случай незачем: адрес точки постоянный.
            if (link == null)
            {
                link = NewLink(WifiLink.DefaultHost);
                if (!link.Connect())
                {
                    // Компьютер не в сети K57 - просим Windows подключиться и
                    // ждём, пока она войдёт и получит адрес. 5 секунд не
                    // хватило: на стенде подключилось только со второго нажатия
                    // (10.11.2026) - WPA2 плюс DHCP бывает и 8-10 секунд. Ждём
                    // до 15, проверяя каждую секунду, и выходим сразу, как мост
                    // ответил.
                    Cursor.Current = Cursors.WaitCursor;
                    JoinBridgeAp(WifiLink.DefaultApSsid);
                    var ok = false;
                    for (var i = 0; i < 15 && !ok; i++)
                    {
                        Thread.Sleep(1000);
                        link = NewLink(WifiLink.DefaultHost);
                        ok = link.Connect(1000);
                    }
                    Cursor.Current = Cursors.Default;
                    if (!ok)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WiFi] и на {WifiLink.DefaultHost} не ответил");
                        return false;
                    }
                }
            }

            if (!_crwHooked)
            {
                _crw.DoWork += Crw_DoWork;
                _crwHooked = true;
            }
            for (var i = 0; i < 200 && _crw.IsBusy; i++) Thread.Sleep(10);
            if (_crw.IsBusy)
            {
                link.Dispose();
                return false;
            }

            _wifi = link;
            _rden = true;
            _crw.RunWorkerAsync();
            CanRate_ShowTitle(); // в заголовке - «WiFi <адрес>» вместо скорости CAN
            cmd_128(0);
            return true;
        }

        private void Crw_DoWork(object sender, DoWorkEventArgs e)  // асинхронный приём данных от КСП через CANA
        {
            while (_rden)
            {
                if (_canReopening)
                {
                    Thread.Sleep(1);
                    continue;
                }

                if (!_re.WaitOne(10)) continue;

                // WiFi-мост: те же кадры, из своей очереди (CAN/WifiLink.cs).
                var wifi = _wifi;
                if (wifi != null)
                {
                    while (_rden && wifi.TryRead(out var wm)) CanaRxFrame(wm);
                    continue;
                }

                TPCANStatus stsResult;
                do
                {
                    stsResult = PCANBasic.ReadFD(Ch, out var canMsg, out _);
                    if (stsResult != TPCANStatus.PCAN_ERROR_OK) continue;
                    CanaRxFrame(canMsg);
                } while (_rden && !_canReopening && (stsResult & TPCANStatus.PCAN_ERROR_QRCVEMPTY) == 0);
            }
        }

        // Разбор одного кадра - общий для PCAN и WiFi-моста.
        private void CanaRxFrame(TPCANMsgFD canMsg)
        {
                    var data = canMsg.DATA;
                    switch (canMsg.ID)
                    {

                        case 1:
                        {
                            ushort cmd = (ushort)(data[0] | (data[1] << 8));
                            ushort d1  = (ushort)(data[2] | (data[3] << 8));
                            ushort d2  = (ushort)(data[4] | (data[5] << 8));
                            ushort d3  = (ushort)(data[6] | (data[7] << 8));

                            AppendUiCanLogLine("RX", 1, cmd, d1, d2, d3);

                            _ = this.BeginInvoke(new Action(() => 
                     
                            {
                                Mbox13Rx(data, cmd, d1, d2, d3);
                                foreach (var t in _txFl) t.RxFb(cmd, (short)d1, d3);
                            }));
                            break;
                        }
                        case 0x32:
                            _bootRespQueue.Enqueue((ushort)(data[0] | (data[1] << 8)));
                            _bootRespEvent.Set();
                            break;


                        case CanRateProbeReplyId:
                            CanRate_OnProbeReply(canMsg);
                            break;

                        default:  // 103 (быстрый), 131 (Kadr60), 132 (Errors) и др.
                            if (canMsg.ID == 140)
                                Volatile.Write(ref _canLastTele, Environment.TickCount);
                            try { CAN_RxData(canMsg); } catch { }
                            break;
                    }
        }

        #endregion
    }
}

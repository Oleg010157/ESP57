/*
 Скорость фазы данных CAN A: 2.5 или 5 Мбит/с.

 Прибор стартует на 2.5 Мбит/с. На 5 Мбит/с он и программа переходят вместе, по
 пробе при нажатии "Подключить". Разбор со стороны прибора - myMCAN.c.

 Режим выбирается в меню "Подключения". Вниз, на 2.5, он переключает сразу, вверх -
 при следующем "Подключить":
   Авто - проба; не прошла или потом пошли ошибки - работаем на 2.5 Мбит/с;
   5    - проба; не прошла - сообщение и 2.5 Мбит/с. Откат на ошибки выключен,
          чтобы на стенде видеть сбои, а не их маскировку;
   2.5  - пробы нет, всё как у заказчика.

 Служебные кадры идут без BRS, то есть целиком на 1 Мбит/с, и доходят, даже когда
 прибор и программа разошлись по скорости данных. Главный здесь прибор: программа
 только следует за его ответом.
*/

using ICDIBasic.Properties;
using Peak.Can.Basic;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ICDIBasic
{
    public partial class Form1
    {
        #region CAN A — скорость фазы данных

        // 1 Мбит/с арбитраж / 2.5 Мбит/с данные, SP 75 %: 80/(1+23+8)
        private const string CanBitrateSlow =
            "f_clock=80000000,nom_brp=1,nom_tseg1=63,nom_tseg2=16,nom_sjw=16,data_brp=1,data_tseg1=23,data_tseg2=8,data_sjw=4";
        // 1 Мбит/с арбитраж / 5 Мбит/с данные, SP 75 %: 80/(1+11+4)
        private const string CanBitrateFast =
            "f_clock=80000000,nom_brp=1,nom_tseg1=63,nom_tseg2=16,nom_sjw=16,data_brp=1,data_tseg1=11,data_tseg2=4,data_sjw=4";

        // Обязаны совпадать с CAN_RATE_* в myDCAN.h
        private const ushort CanRateCmd = 7100;
        private const ushort CanRateDown = 0;
        private const ushort CanRateUp = 1;
        private const ushort CanRateConfirm = 2;
        private const ushort CanRateProbe = 3;
        private const ushort CanRateErrors = 1;
        private const ushort CanRateTimeout = 2;
        private const ushort CanRateBusy = 3;
        private const uint CanRateProbeReplyId = 133;
        // Чужой ID: фильтр прибора его отбрасывает, но кадр всё равно проверяется на
        // шине. Так 64 байта от ПК идут через приёмник прибора без разбора в прерывании,
        // где Rx FIFO рассчитан на 8 байт.
        private const uint CanRateFillerId = 0x7F0;

        private const int CanRateReplyMs = 300;
        private const int CanRateProbeReplyMs = 100;
        private const int CanRateProbeRounds = 30;
        private const int CanRateWatchdogMs = 1000;
        private const string CanRateTitleMark = " — CAN ";

        private const string CanSpeedAuto = "Auto";
        private const string CanSpeedFast = "5";
        private const string CanSpeedSlow = "2.5";

        private readonly ToolStripMenuItem _canSpeedMenu = new ToolStripMenuItem("Скорость CAN A")
        {
            ToolTipText = "2.5 Мбит/с включается сразу, 5 Мбит/с - при следующем нажатии «Подключить»"
        };

        private volatile bool _canFast;
        private volatile bool _canReopening;
        private bool _canRateTrial;
        private bool _mcuCan5M;
        private int _canLastTele;
        private ushort _canRateWaitAction;
        private TaskCompletionSource<ushort[]> _canRateNote;
        private volatile TaskCompletionSource<bool> _canProbe;
        private volatile int _canProbeSeq;

        private void CanRate_InitMenu(ToolStripMenuItem parent)
        {
            foreach (var mode in new[] { CanSpeedAuto, CanSpeedFast, CanSpeedSlow })
            {
                var m = mode;
                var item = new ToolStripMenuItem(CanRate_ModeName(m))
                {
                    Tag = m,
                    Checked = Settings.Default.CanASpeedMode == m
                };
                item.Click += (s, e) => CanRate_SelectMode(m);
                _canSpeedMenu.DropDownItems.Add(item);
            }

            parent.DropDownItems.Add(_canSpeedMenu);
        }

        private static string CanRate_ModeName(string mode)
        {
            if (mode == CanSpeedFast) return "5 Мбит/с";
            if (mode == CanSpeedSlow) return "2.5 Мбит/с";
            return "Авто";
        }

        private void CanRate_SelectMode(string mode)
        {
            Settings.Default.CanASpeedMode = mode;
            Settings.Default.Save();
            foreach (ToolStripMenuItem item in _canSpeedMenu.DropDownItems)
            {
                item.Checked = (string)item.Tag == mode;
            }

            var connected = tsmConnect.Text == @"Отключить";
            if (mode == CanSpeedSlow && _canFast)
            {
                // Вниз можно сразу и под телеметрией. Канал переоткроет ответ прибора.
                CanRate_Send(CanRateDown, 0, false);
            }
            else if (mode != CanSpeedSlow && connected && !_canFast)
            {
                // Вверх на ходу нельзя: пока ПК переоткрывает канал, кадры телеметрии
                // на 5 Мбит/с уходили бы ему в ошибку. Только пробой при подключении.
                CanRate_ShowTitle("5 Мбит/с - при следующем подключении");
            }
            else
            {
                CanRate_ShowTitle();
            }
        }

        // Через WiFi-мост скорость CAN к связи с ПК отношения не имеет, и в
        // заголовке вместо неё - адрес моста (CAN/WifiLink.cs).
        private const string WifiTitleMark = " — WiFi ";

        // Имя сети - его мост сообщает сам при подключении. По одному адресу не
        // понять, через роутер связь или напрямую, а от этого зависит, тянет ли
        // эфир ленту (docs/esp32-spi-protocol-RU.md, «Что на ПК»).
        private string WifiTitleNet()
        {
            var name = _wifi?.NetName;
            if (string.IsNullOrEmpty(name)) return "";
            return _wifi.NetIsOwnAp ? $"{name} (точка моста), " : $"{name}, ";
        }

        // Скорость и причина отката - в заголовке окна, чтобы не гадать, на чём работаем.
        private void CanRate_ShowTitle(string why = null)
        {
            var title = Text;
            foreach (var mark in new[] { CanRateTitleMark, WifiTitleMark })
            {
                var i = title.IndexOf(mark, StringComparison.Ordinal);
                if (i >= 0) title = title.Substring(0, i);
            }
            title += _wifi != null
                ? WifiTitleMark + WifiTitleNet() + _wifi.Host
                : CanRateTitleMark + (_canFast ? "5" : "2.5") + " Мбит/с";
            Text = why == null ? title : title + " (" + why + ")";
        }

        // PCAN меняет скорость только переоткрытием канала. Приём при этом не
        // останавливаем: дождаться BackgroundWorker из потока окна нельзя, он сообщает о
        // завершении через очередь этого же окна. Он просто не читает, пока идёт смена.
        private bool CanRate_Reopen(bool fast)
        {
            if (Ch == 0) return false;

            _canReopening = true;
            try
            {
                PCANBasic.Uninitialize(Ch);
                var sts = PCANBasic.InitializeFD(Ch, fast ? CanBitrateFast : CanBitrateSlow);
                if (sts != TPCANStatus.PCAN_ERROR_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"[PCAN] смена скорости fast={fast}: {sts}");
                    return false;
                }

                var handlePtr = _re.SafeWaitHandle.DangerousGetHandle();
                ulong eventHandle = unchecked((ulong)handlePtr.ToInt64());
                PCANBasic.SetValue(Ch, TPCANParameter.PCAN_RECEIVE_EVENT, ref eventHandle, sizeof(ulong));
                _canFast = fast;
                return true;
            }
            finally
            {
                _canReopening = false;
            }
        }

        private bool CanRate_Send(ushort action, ushort d2, bool brs)
        {
            var msg = new TPCANMsgFD
            {
                DATA = new byte[64],
                ID = 2,
                DLC = 8,
                MSGTYPE = brs
                    ? TPCANMessageType.PCAN_MESSAGE_FD | TPCANMessageType.PCAN_MESSAGE_BRS
                    : TPCANMessageType.PCAN_MESSAGE_FD
            };
            msg.DATA[0] = (byte)(CanRateCmd & 0xFF);
            msg.DATA[1] = (byte)(CanRateCmd >> 8);
            msg.DATA[4] = (byte)(d2 & 0xFF);
            msg.DATA[5] = (byte)(d2 >> 8);
            msg.DATA[6] = (byte)(action & 0xFF);
            msg.DATA[7] = (byte)(action >> 8);
            return PCANBasic.WriteFD(Ch, ref msg) == TPCANStatus.PCAN_ERROR_OK;
        }

        // Шаблоны тяжёлые по-разному: 0x55 без вставленных бит, 0x00 и 0xFF с ними.
        private bool CanRate_SendFiller(int round)
        {
            var msg = new TPCANMsgFD
            {
                DATA = new byte[64],
                ID = CanRateFillerId,
                DLC = 15,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_FD | TPCANMessageType.PCAN_MESSAGE_BRS
            };
            var fill = round % 3 == 0 ? (byte)0x55 : round % 3 == 1 ? (byte)0x00 : (byte)0xFF;
            for (var i = 0; i < 64; i++)
            {
                msg.DATA[i] = fill;
            }
            return PCANBasic.WriteFD(Ch, ref msg) == TPCANStatus.PCAN_ERROR_OK;
        }

        // Послать команду и дождаться ответа прибора о ней. null - ответа нет.
        private async Task<ushort[]> CanRate_Request(ushort action, ushort d2, bool brs)
        {
            var note = new TaskCompletionSource<ushort[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _canRateWaitAction = action;
            _canRateNote = note;

            if (!CanRate_Send(action, d2, brs)) return null;

            var done = await Task.WhenAny(note.Task, Task.Delay(CanRateReplyMs));
            _canRateNote = null;
            return done == note.Task ? note.Task.Result : null;
        }

        // Ответ прибора о скорости: { действие, скорость, причина }. Поток окна.
        private void CanRate_OnNote(ushort action, ushort rate, ushort reason)
        {
            if (_canRateNote != null && action == _canRateWaitAction)
            {
                _canRateNote.TrySetResult(new[] { action, rate, reason });
            }

            if (_canRateTrial || !_canFast || rate != 0) return;

            // Прибор ушёл на 2.5 Мбит/с без нашей пробы: следуем за ним.
            CanRate_Reopen(false);
            CanRate_ShowTitle(reason == CanRateErrors ? "откат: ошибки шины"
                : reason == CanRateTimeout ? "откат: нет подтверждения" : null);
        }

        // Ответ на первый ping: что прибор умеет и на какой он скорости. Поток окна.
        private void CanRate_OnPing(ushort d3)
        {
            _mcuCan5M = (d3 & 0x04) != 0;
            if (_canRateTrial) return;

            var mcuFast = (d3 & 0x08) != 0;
            if (mcuFast && !_canFast)
            {
                // Прошлый сеанс оборвался на 5 Мбит/с, а новый всегда начинается на 2.5.
                CanRate_Send(CanRateDown, 0, false);
            }
            else if (!mcuFast && _canFast)
            {
                CanRate_Reopen(false);
                CanRate_ShowTitle();
            }
        }

        // Ответ на проверочный кадр. Рабочий поток приёма.
        private void CanRate_OnProbeReply(TPCANMsgFD msg)
        {
            var probe = _canProbe;
            if (probe == null) return;

            var seq = _canProbeSeq;
            var ok = msg.DLC == 15;
            for (var i = 0; ok && i < 32; i++)
            {
                var word = (ushort)(msg.DATA[2 * i] | (msg.DATA[2 * i + 1] << 8));
                ok = word == (ushort)(seq + i * 0x0101);
            }
            probe.TrySetResult(ok);
        }

        private async Task<bool> CanRate_Probes()
        {
            for (var round = 0; round < CanRateProbeRounds; round++)
            {
                var probe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _canProbeSeq = round;
                _canProbe = probe;

                if (!CanRate_SendFiller(round) || !CanRate_Send(CanRateProbe, (ushort)round, true))
                    return false;

                var done = await Task.WhenAny(probe.Task, Task.Delay(CanRateProbeReplyMs));
                _canProbe = null;
                if (done != probe.Task || !probe.Task.Result)
                    return false;
            }

            return PCANBasic.GetStatus(Ch) == TPCANStatus.PCAN_ERROR_OK;
        }

        // Проба 5 Мбит/с. Возвращает причину неудачи, null - всё прошло.
        private async Task<string> CanRate_TryFast(bool hold)
        {
            var up = await CanRate_Request(CanRateUp, hold ? (ushort)1 : (ushort)0, false);
            if (up == null) return "прибор не ответил";
            if (up[1] == 0) return up[2] == CanRateBusy ? "идёт телеметрия" : "прибор не перешёл";

            if (!CanRate_Reopen(true)) return "адаптер не принял скорость";
            if (!await CanRate_Probes()) return "ошибки на шине";

            var confirm = await CanRate_Request(CanRateConfirm, 0, true);
            return confirm != null && confirm[1] != 0 ? null : "прибор не подтвердил";
        }

        // Зовётся перед командой "Подключить". Окно на время пробы не замирает, поэтому
        // кнопка выключена: второе нажатие сюда не пройдёт.
        private async Task CanRate_BeforeConnect()
        {
            var mode = Settings.Default.CanASpeedMode;
            if (mode == CanSpeedSlow || Ch == 0)
            {
                CanRate_ShowTitle();
                return;
            }

            var hold = mode == CanSpeedFast;
            string why;
            if (_canFast)
            {
                why = null;
            }
            else if (!_mcuCan5M)
            {
                why = "прошивка не умеет 5 Мбит/с";
            }
            else
            {
                _canRateTrial = true;
                tsmConnect.Enabled = false;
                try
                {
                    why = await CanRate_TryFast(hold);
                    if (why != null)
                    {
                        CanRate_Send(CanRateDown, 0, false);
                        if (_canFast && !CanRate_Reopen(false))
                        {
                            CanA_Ok = false;
                            _canaM.BackColor = System.Drawing.Color.Salmon;
                        }
                    }
                }
                finally
                {
                    _canRateTrial = false;
                    tsmConnect.Enabled = true;
                }
            }

            Volatile.Write(ref _canLastTele, Environment.TickCount);
            CanRate_ShowTitle(why);

            if (hold && why != null)
            {
                MessageBox.Show("Не удалось перейти на 5 Мбит/с: " + why + ".\nРаботаем на 2.5 Мбит/с.",
                    "Скорость CAN A", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Страховка на случай, если ответ об откате потерялся: кадр 140 идёт каждые
        // 50 мс, секунда тишины на 5 Мбит/с - повод вернуться. Зовётся по таймеру окна.
        private void CanRate_Watchdog()
        {
            if (!_canFast || _canRateTrial || Settings.Default.CanASpeedMode != CanSpeedAuto) return;
            if (Environment.TickCount - Volatile.Read(ref _canLastTele) < CanRateWatchdogMs) return;

            CanRate_Send(CanRateDown, 0, false);
            CanRate_Reopen(false);
            CanRate_ShowTitle("откат: нет телеметрии");
        }

        #endregion
    }
}

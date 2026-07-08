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
        // true если последний ping (cmd=0) вернул d3=1 (MCU уже в состоянии connect)
        private bool _mcuAlreadyConnected;
        // Queue for bootloader responses (MCU -> PC ID 0x32)
        private readonly ConcurrentQueue<ushort> _bootRespQueue = new ConcurrentQueue<ushort>();
        private readonly AutoResetEvent _bootRespEvent = new AutoResetEvent(false);

        private void CanaDisconnect()
        {
            _crw.CancelAsync();
            _rden = CanA_Ok = tsmConnect.Enabled = false;
            _canaM.BackColor = Color.Yellow;
            PCANBasic.Uninitialize(Ch);
            Ch = 0;
        }

        private void CanaConnect()
        {
            CanA_Ok = false;
            CanaM_TextChanged(this, null);

            if (CanI())
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

            CanA_Ok = false;

            // Реинициализация без промежуточного CanaConnect(), чтобы не получать
            // ложный "лосось" при первом клике.
            var connected = CanI();
            if (!connected)
            {
                Thread.Sleep(250);
                connected = CanI();
            }

            if (!connected || Ch == 0)
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

        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tsmConnect.BackColor = Color.Yellow;
            // If bootloader left the form title as the final "Готово!", replace it
            // with the normal application title shown after a user-initiated connect.
            if (Text == @"Готово!")
            {
                Text = "Motor Control";
            }
            cmd_128(tsmConnect.Text == @"Подключить" ? (ushort)2 : (ushort)3);
        }

        private void ReInitCanAMenu_Click(object sender, EventArgs e)
        {
            _canaM.BackColor = Color.Yellow;
            PulseCanCheckMenu();

            if (CanI())
                cmd_128(0);
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

                NUD_RPM.Value = 0;
                uint_128(0xA6, 0, 0, 338); // !!! Выкл ручн. упр. IGBT
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
            AIN1_motorName.Enabled = false;
            AIN2_motorName.Enabled = false;
        }

        // метод поиска и подключения CAN адаптера к одному из свободных USB-портов: 
        private bool CanI()
        {
            var cha = new[] { (byte)81, (byte)82, (byte)83, (byte)84, (byte)85, (byte)86, (byte)87, (byte)88 };

            if (Ch != 0 && _crw.IsBusy)
            {
                _rden = true;
                cmd_128(0);
                return true;
            }

            _re = new AutoResetEvent(false);
            
            try
            {
                uint iBuffer;
                TPCANStatus stsResult;
                foreach (var t in cha)
                {
                    stsResult = PCANBasic.GetValue(t, TPCANParameter.PCAN_CHANNEL_CONDITION, out iBuffer, sizeof(uint));
                    if (stsResult != TPCANStatus.PCAN_ERROR_OK) continue;
                    if (iBuffer != PCANBasic.PCAN_CHANNEL_AVAILABLE) continue;

                    stsResult = PCANBasic.Initialize(t, TPCANBaudrate.PCAN_BAUD_1M);
                    if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                    {
                        // логировать stsResult при необходимости и пробовать следующий канал
                        continue;
                    }

                    stsResult = PCANBasic.GetValue(t, TPCANParameter.PCAN_DEVICE_ID, out iBuffer, sizeof(uint));
                    if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                    {
                        // устройство не вернуло номер — размонтировать и продолжить поиск
                        PCANBasic.Uninitialize(t);
                        continue;
                    }
                    if (iBuffer == CanAId)
                    {
                        PCANBasic.Uninitialize(t);
                        Ch = t;
                        break;
                    }
                    PCANBasic.Uninitialize(t);
                }

                // проверяем, найден ли канал
                if (Ch == 0) return false;

                // инициализация найденного канала
                stsResult = PCANBasic.Initialize(Ch, TPCANBaudrate.PCAN_BAUD_1M);
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

        private void Crw_DoWork(object sender, DoWorkEventArgs e)  // асинхронный приём данных от КСП через CANA
        {
            while (_rden)
            {
               if (!_re.WaitOne(10)) continue;

                TPCANStatus stsResult;

                do
                {
                    stsResult = PCANBasic.Read(Ch, out var canMsg, out TPCANTimestamp ts);
                    if (stsResult != TPCANStatus.PCAN_ERROR_OK) continue;

                    // If this is a bootloader response (MCU -> PC ID 0x32), enqueue for waiting callers
                    if (canMsg.ID == 0x32)
                    {
                        // Normalize to 8 bytes and extract first 16-bit word
                        var len = canMsg.LEN;
                        byte b0 = len > 0 ? canMsg.DATA[0] : (byte)0;
                        byte b1 = len > 1 ? canMsg.DATA[1] : (byte)0;
                        ushort bootVal = (ushort)(b0 | (b1 << 8));
                        _bootRespQueue.Enqueue(bootVal);
                        _bootRespEvent.Set();
                        continue;
                    }

                    if (canMsg.ID == 1)
                    {
                        // Normalize to 8 bytes: some UIs send DLC=2 etc. — pad with zeros.
                        var len = canMsg.LEN;
                        byte[] data8 = new byte[8];
                        for (int i = 0; i < 8; i++) data8[i] = (i < len) ? canMsg.DATA[i] : (byte)0;

                        ushort cmd = BitConverter.ToUInt16(new[] { data8[0], data8[1] }, 0);
                        ushort d1 = BitConverter.ToUInt16(new[] { data8[2], data8[3] }, 0);
                        ushort d2 = BitConverter.ToUInt16(new[] { data8[4], data8[5] }, 0);
                        ushort d3 = BitConverter.ToUInt16(new[] { data8[6], data8[7] }, 0);

                        AppendUiCanLogLine("RX", 1, cmd, d1, d2, d3);

                        _ = this.BeginInvoke(new Action(() =>
                        {
                            Mbox13Rx(data8, cmd, d1, d2, d3);
                            foreach (var t in _txFl) t.RxFb(cmd, (short)d1, d3);
                        }));

                        continue;
                    }

                    try
                    {
                        CAN_RxData(canMsg);
                    }
                    catch
                    {
                        // ignored
                    }
                } while (_rden && !Convert.ToBoolean(stsResult & TPCANStatus.PCAN_ERROR_QRCVEMPTY));
            }
        }

        #endregion
    }
}

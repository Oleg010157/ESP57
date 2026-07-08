using Peak.Can.Basic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ICDIBasic
{
    public partial class Form1
    {
        private string _selectedFileName = string.Empty;
        private byte _canHandle;
        private OpenFileDialog _bofd;
        private readonly BackgroundWorker _flWrk = new BackgroundWorker();
        private uint _checksum;
        private uint _imgStartAddr;
        private uint _appEntryAddr;
        private System.Windows.Forms.Timer _verifyTimer;
        private int _loadProgress = -1;

        // F280039C: приложение начинается с Bank0 Sec2, загрузчик в Sec0-1
        private const uint FStartAddr = 0x082000; // Bank0 Sec2 — начало области приложения
        private const uint FEndAddr = 0x0AFFFF;   // Bank2 Sec15 — конец flash

        private const ushort AckLoadReadyV2 = 0x0ACC;
        private const ushort AckLoadReadyLegacy = 0x07CC;
        private const ushort AckEraseRange = 0x0DCC;
        private const ushort CmdSetChunkV2 = 0xCC0A;
        private const ushort AckSetChunkV2 = 0x0BCC;
        private const ushort AckChunkV2 = 0x0CCC;
        private const ushort DefaultChunkBlocksV2 = 16;

        #region 03 - Загрузчик
        private void FlWrk_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var swTotal = Stopwatch.StartNew();
                long eraseMs = 0;
                long loadMs = 0;
                long verifyMs = 0;
                long rebootMs = 0;
                long blockAckTicks = 0;
                long blockWriteTicks = 0;
                uint blockCount = 0;
                uint ackWaitEvents = 0;

                Thread.Sleep(500);
                Cl();

                // Ensure user selected .out file BEFORE reinitializing CAN and entering bootloader flow.
                if (string.IsNullOrWhiteSpace(_selectedFileName))
                {
                    string chosen = null;
                    try
                    {
                        Invoke(new Action(() =>
                        {
                            using (var ofd = new OpenFileDialog())
                            {
                                ofd.Filter = "OUT files|*.out";
                                ofd.InitialDirectory = Application.StartupPath;
                                ofd.Title = "Выберите .out файл для загрузки";
                                if (ofd.ShowDialog(this) == DialogResult.OK)
                                    chosen = ofd.FileName;
                            }
                        }));
                    }
                    catch
                    {
                        chosen = null;
                    }

                    if (string.IsNullOrWhiteSpace(chosen))
                    {
                        // No file chosen — abort before touching CAN
                        BeginInvoke(new Action(() => Text = @"Операция прервана: файл не выбран"));
                        return;
                    }

                    _selectedFileName = chosen;
                }

                // Before reinitializing CAN, attempt to copy/convert the selected .out and validate it.
                var pOut = $"{Application.StartupPath}\\Ldr.out";
                var pBin = $"{Application.StartupPath}\\Ldr.bin";
                byte[] fileCs = null;
                uint cycles = 0;
                bool convOk = false;

                while (!convOk)
                {
                    try
                    {
                        // Quick validation: check .out/ELF signature before attempting conversion.
                        try
                        {
                            using (var fsSig = new FileStream(_selectedFileName, FileMode.Open, FileAccess.Read))
                            {
                                if (fsSig.Length < 2) throw new InvalidDataException("File too short for signature check");
                                int b0 = fsSig.ReadByte();
                                int b1 = fsSig.ReadByte();
                                ushort sig = (ushort)(b1 << 8 | b0);
                                // Valid signatures: TI .out = 0x08AA (little-endian), ELF = 0x457F (0x7F 'E')
                                if (sig != 0x08AA && sig != 0x457F)
                                    throw new InvalidDataException($"Unsupported file signature: 0x{sig:X4}");
                            }
                        }
                        catch (Exception sigEx)
                        {
                            throw new InvalidOperationException($"Signature check failed: {sigEx.Message}", sigEx);
                        }

                        File.Copy(_selectedFileName, pOut, true);

                        Cnv2(pOut, pBin);
                        if (!File.Exists(pBin)) throw new Exception("Converted bin not found");

                        fileCs = ReadFile(pBin);
                        cycles = (uint)fileCs.Length;
                        _checksum = (_imgStartAddr - FStartAddr) * 2 * 0xFF;
                        for (uint bytepos = 0; bytepos < cycles; bytepos++) _checksum += fileCs[bytepos];
                        convOk = true;
                    }
                    catch (Exception ex)
                    {
                        // Log conversion error for debugging
                        try { File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_error.log"),
                                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Conversion error for '{_selectedFileName}': {ex}\n"); }
                        catch { }
                        // Conversion failed — ask user to Retry (choose another file) or Cancel before touching CAN
                        var retryRes = DialogResult.None;
                        try
                        {
                            Invoke(new Action(() =>
                            {
                                retryRes = MessageBox.Show(this,
                                    "Неправильный файл или ошибка конвертации .out. Выберите 'Повторить' чтобы выбрать другой файл, или 'Отмена' чтобы прервать.",
                                    "Ошибка файла",
                                    MessageBoxButtons.RetryCancel,
                                    MessageBoxIcon.Error,
                                    MessageBoxDefaultButton.Button1);
                            }));
                        }
                        catch
                        {
                            retryRes = MessageBox.Show(
                                "Неправильный файл или ошибка конвертации .out. Выберите 'Повторить' чтобы выбрать другой файл, или 'Отмена' чтобы прервать.",
                                "Ошибка файла",
                                MessageBoxButtons.RetryCancel,
                                MessageBoxIcon.Error,
                                MessageBoxDefaultButton.Button1);
                        }

                        if (retryRes == DialogResult.Retry)
                        {
                            string sel = null;
                            try
                            {
                                Invoke(new Action(() =>
                                {
                                    using (var ofd2 = new OpenFileDialog())
                                    {
                                        ofd2.Filter = "OUT files|*.out";
                                        ofd2.InitialDirectory = Application.StartupPath;
                                        ofd2.Title = "Выберите .out файл для загрузки";
                                        if (ofd2.ShowDialog(this) == DialogResult.OK)
                                            sel = ofd2.FileName;
                                    }
                                }));
                            }
                            catch
                            {
                                sel = null;
                            }

                            if (!string.IsNullOrWhiteSpace(sel))
                            {
                                _selectedFileName = sel;
                                // Ensure we attempt conversion for the newly selected file
                                convOk = false;
                                continue; // retry conversion loop with new file
                            }
                            else
                            {
                                // user didn't choose new file — abort before CAN
                                BeginInvoke(new Action(() => Text = @"Операция прервана: файл не выбран"));
                                return;
                            }
                        }
                        else
                        {
                            // Cancel — abort before touching CAN
                            BeginInvoke(new Action(() => Text = @"Операция прервана: конвертация не удалась"));
                            return;
                        }
                    }
                }

                if (fileCs == null)
                    throw new InvalidOperationException("Converted image buffer is empty.");

                List<LoadRun> loadRuns = BuildLoadRuns(fileCs, _imgStartAddr);
                if (loadRuns.Count == 0)
                    throw new InvalidOperationException("Converted image contains no programmable data blocks.");

                cycles = 0;
                _checksum = 0;
                foreach (var run in loadRuns)
                {
                    cycles += (uint)run.Data.Length;
                    for (int i = 0; i < run.Data.Length; i++)
                        _checksum += run.Data[i];
                }

                // PCAN уже инициализирован на 1 Мбит/с — загрузчик работает на той же скорости
                BeginInvoke(new Action(() => Text = @"CAN OK"));
                Invoke(new Action(MnSet));
                Thread.Sleep(500);

                short T = 0;

                // Handshake: проверка связи с загрузчиком F280039C
                // MCU уже перезагрузился в режим загрузчика (команда 9999 отправлена ранее)
                T = 0;
                BeginInvoke(new Action(() => Text = @"Ожидание загрузчика..."));
                Thread.Sleep(1000); // Дополнительная пауза для стабилизации
                // Stop normal CAN receive worker to avoid frame races during bootloader session.
                _rden = false;
                if (_crw.WorkerSupportsCancellation)
                    _crw.CancelAsync();
                for (var stopWait = 0; stopWait < 200 && _crw.IsBusy; stopWait++)
                    Thread.Sleep(10);

                // Ensure PCAN channel is configured to 1Mbit/s before handshake.
                try
                {
                    // Reinitialize interface to 1M to recover from error-passive states
                    PCANBasic.Uninitialize(_canHandle);
                    Thread.Sleep(50);
                    PCANBasic.Initialize(_canHandle, TPCANBaudrate.PCAN_BAUD_1M);
                    // Restore receive event so background receiver keeps working
                    var hp = _re.SafeWaitHandle.DangerousGetHandle();
                    ulong ev = unchecked((ulong)hp.ToInt64());
                    PCANBasic.SetValue(_canHandle, TPCANParameter.PCAN_RECEIVE_EVENT, ref ev, sizeof(ulong));
                }
                catch
                {
                    // best-effort — continue to send handshake even if reinit fails
                }

                // Drain any stale MCU diagnostic frames (0xEF01, 0xEF02) sent on bootloader
                // startup before starting the handshake, so they don't confuse Read_32().
                while (_bootRespQueue.TryDequeue(out _)) { }

                // CRITICAL: Also drain CAN RX hardware buffer to ensure clean state before handshake
                var drainSwHandshake = Stopwatch.StartNew();
                while (drainSwHandshake.ElapsedMilliseconds < 200)
                {
                    var drainSts = PCANBasic.Read(_canHandle, out _, out _);
                    if ((drainSts & TPCANStatus.PCAN_ERROR_QRCVEMPTY) != 0)
                        break;
                    Thread.Sleep(1);
                }

                Send_33(0xCC01);
                Thread.Sleep(50); // Give MCU time to process handshake on fast machines
                while (Read_32() != 0x02CC)
                {
                    T++;
                    Thread.Sleep(10);
                    Send_33(0xCC01);
                    if (T > 3000) throw new Exception();
                }

                // Стирание flash (Bank0 Sec2-15, Bank1, Bank2)
                Thread.Sleep(10);
                T = 0;

            erase_start:
                // Drain any stale boot responses so we wait for the ACK_ERASE produced by this request.
                while (_bootRespQueue.TryDequeue(out _)) { }

                // CRITICAL: Drain any stale CAN frames that may be in the hardware RX buffer
                // This prevents old frames from being misinterpreted as responses to new commands
                var drainSw = Stopwatch.StartNew();
                while (drainSw.ElapsedMilliseconds < 200)
                {
                    var drainSts = PCANBasic.Read(_canHandle, out _, out _);
                    if ((drainSts & TPCANStatus.PCAN_ERROR_QRCVEMPTY) != 0)
                        break; // Queue is empty, safe to proceed
                    Thread.Sleep(1);
                }

                Thread.Sleep(10);
                ushort resp;
                try { File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sending CMD_ERASE (0xCC02)\n"); } catch { }
                Send_33(0xCC02);
                Thread.Sleep(50); // Give MCU time to process erase command on fast machines

                {
                    uint eraseStart = loadRuns[0].StartAddr;
                    uint eraseEnd = eraseStart;

                    foreach (var run in loadRuns)
                    {
                        if (run.StartAddr < eraseStart)
                            eraseStart = run.StartAddr;

                        uint runEnd = run.StartAddr + (uint)(run.Data.Length / AddrSize);
                        if ((run.Data.Length & 1) != 0)
                            runEnd += 1U;
                        if (runEnd > 0)
                            runEnd -= 1U;

                        if (runEnd > eraseEnd)
                            eraseEnd = runEnd;
                    }

                    if (eraseStart < FStartAddr) eraseStart = FStartAddr;
                    if (eraseEnd > FEndAddr) eraseEnd = FEndAddr;
                    if (eraseEnd < eraseStart) eraseEnd = eraseStart;

                    SendEraseRange33(eraseStart, eraseEnd);

                    var swEraseRangeAck = Stopwatch.StartNew();
                    bool gotEraseRangeAck = false;
                    while (swEraseRangeAck.ElapsedMilliseconds < 1500)
                    {
                        resp = Read_32();
                        if (resp == AckEraseRange)
                        {
                            gotEraseRangeAck = true;
                            break;
                        }
                        Thread.Sleep(1);
                    }

                    if (!gotEraseRangeAck)
                        throw new InvalidOperationException("Timeout waiting ACK_ERASE_RANGE from MCU.");
                }

                BeginInvoke(new Action(() => Text = @"Очистка FLASH..."));
                var swErase = Stopwatch.StartNew();

                // Erase: ждём ACK_ERASE (0x05CC).
                // MCU шлёт комбинированные маркеры 0xEBbs (b=банк, s=сектор).
                while (true)
                {
                    resp = Read_32();
                    if (resp != 0)
                    {
                        bool importantEraseMarker =
                            resp == 0x05CC ||
                            (resp & 0xFF00) == 0xEB00 ||
                            (resp & 0xFF00) == 0xD000 ||
                            (resp & 0xFF00) == 0xD100 ||
                            (resp & 0xFF00) == 0xD200 ||
                            resp == 0xEE00 || resp == 0xEE01 || resp == 0xEE02 || resp == 0xDEAD;

                        if (importantEraseMarker)
                        {
                            try { File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Erase loop received: 0x{resp:X4}\n"); } catch { }
                        }
                    }
                    if (resp == 0x05CC) break;

                    // Combined bank+sector marker: 0xEB0s (Bank0), 0xEB1s (Bank1), 0xEB2s (Bank2)
                        if ((resp & 0xFF00) == 0xEB00)
                    {
                        var bank = (resp >> 4) & 0x0F;
                        var sec = resp & 0x0F;
                        BeginInvoke(new Action(() => Text = $@"Очистка FLASH... Bank {bank} Sector {sec}"));
                    }
                    // Timeout marker: 0xD0nn/0xD1nn/0xD2nn
                    else if ((resp & 0xFF00) == 0xD000 || (resp & 0xFF00) == 0xD100 || (resp & 0xFF00) == 0xD200)
                    {
                        if (TryRecoverAfterMcuError(resp, ref fileCs, ref cycles, ref loadRuns, ref pOut, ref pBin))
                            goto erase_start;

                        throw new InvalidOperationException("Операция прервана пользователем после ошибки MCU.");
                    }

                    T++;
                    Thread.Sleep(10);
                    if (T > 60000) // timeout ~10 minutes
                        throw new Exception();
                }
                eraseMs = swErase.ElapsedMilliseconds;

                // Отправка адреса начала и размера образа
                Thread.Sleep(10);
                T = 0;
                var addrHi = (ushort)((_imgStartAddr >> 16) & 0xFFFF);
                var addrLo = (ushort)(_imgStartAddr & 0xFFFF);
                var sizeHi = (ushort)((cycles >> 16) & 0xFFFF);
                var sizeLo = (ushort)(cycles & 0xFFFF);
                _loadProgress = 0;
                BeginInvoke(new Action(() => Text = @"Загрузка...0%"));

                // Start a UI timer to show elapsed time during load phase
                var swLoadTimer = Stopwatch.StartNew();
                BeginInvoke(new Action(() =>
                {
                    if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
                    _verifyTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                    _verifyTimer.Tick += (s, ev) => Text = $@"Загрузка...{_loadProgress}% ({swLoadTimer.ElapsedMilliseconds / 1000}s)";
                    _verifyTimer.Start();
                }));

                // F280039C: передаём по 8 байт (Flash API программирует блоками по 64 бита)
                // и ждём ACK_BLOCK после каждого блока. Это убирает зависимость
                // от таймингов и исключает зависание из-за потери кадра.
                var canMsg = new TPCANMsg
                {
                    DATA = new byte[8],
                    ID = 0x33,
                    LEN = 8,
                    MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD
                };
                bool useChunkAckV2 = false;
                var swLoad = Stopwatch.StartNew();
                int lastProgress = -1;
                uint sentBytes = 0;

                foreach (var run in loadRuns)
                {
                    ushort lastLoadReadyResp = 0;
                    addrHi = (ushort)((run.StartAddr >> 16) & 0xFFFF);
                    addrLo = (ushort)(run.StartAddr & 0xFFFF);
                    sizeHi = (ushort)(((uint)run.Data.Length >> 16) & 0xFFFF);
                    sizeLo = (ushort)((uint)run.Data.Length & 0xFFFF);

                    try { File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Load segment start: addr=0x{run.StartAddr:X6}, bytes={run.Data.Length}\n"); } catch { }

                    bool gotLoadReady = false;
                    for (short loadTry = 0; loadTry < 8 && !gotLoadReady; loadTry++)
                    {
                        Send_33(0xCC07);
                        Thread.Sleep(50); // Give MCU time to process load command on fast machines

                        var swLoadReadyWait = Stopwatch.StartNew();
                        while (swLoadReadyWait.ElapsedMilliseconds < 5000)
                        {
                            resp = Read_32();
                            if (resp != 0)
                                lastLoadReadyResp = resp;
                            if (resp == AckLoadReadyV2 || resp == AckLoadReadyLegacy)
                            {
                                gotLoadReady = true;
                                try
                                {
                                    File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"),
                                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CMD_LOAD ACK received: 0x{resp:X4}\n");
                                }
                                catch { }
                                break;
                            }
                            Thread.Sleep(1);
                        }

                        if (!gotLoadReady)
                            continue;

                        useChunkAckV2 = false;
                        SendLoadHeader33(addrHi, addrLo, sizeHi, sizeLo);
                    }

                    if (!gotLoadReady)
                    {
                        try
                        {
                            File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"),
                                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Load segment timeout waiting CMD_LOAD ACK: addr=0x{run.StartAddr:X6}, bytes={run.Data.Length}, lastLoadReadyResp=0x{lastLoadReadyResp:X4}\n");
                        }
                        catch { }

                        throw new InvalidOperationException(
                            $"Таймаут ACK на CMD_LOAD (addr=0x{run.StartAddr:X6}, size={run.Data.Length}, last=0x{lastLoadReadyResp:X4}).");
                    }

                    for (uint byteIndex = 0; byteIndex < run.Data.Length; byteIndex += 8)
                    {
                        var remaining = (uint)Math.Min(8, run.Data.Length - byteIndex);
                        for (int j = 0; j < 8; j++)
                            canMsg.DATA[j] = j < remaining ? run.Data[byteIndex + j] : (byte)0xFF;

                        var wrT0 = Stopwatch.GetTimestamp();
                        var stsResult = PCANBasic.Write(_canHandle, ref canMsg);
                        var wrT1 = Stopwatch.GetTimestamp();
                        if (stsResult != TPCANStatus.PCAN_ERROR_OK) throw new Exception();
                        blockWriteTicks += (wrT1 - wrT0);

                        ackWaitEvents++;
                        var swBlockAckTimeout = Stopwatch.StartNew();
                        ushort lastBlockResp = 0;
                        var swBlockAck = Stopwatch.StartNew();
                        while (true)
                        {
                            resp = Read_32();
                            if (resp != 0)
                                lastBlockResp = resp;
                            if (resp == 0x06CC) break;
                            if ((resp & 0xFF00) == 0xDF00)
                            {
                                if (TryRecoverAfterMcuError(resp, ref fileCs, ref cycles, ref loadRuns, ref pOut, ref pBin))
                                    goto erase_start;

                                throw new InvalidOperationException("Операция прервана пользователем после ошибки MCU.");
                            }
                            if (resp == 0xDEAD)
                            {
                                if (TryRecoverAfterMcuError(resp, ref fileCs, ref cycles, ref loadRuns, ref pOut, ref pBin))
                                    goto erase_start;

                                throw new InvalidOperationException("Операция прервана пользователем после ошибки MCU.");
                            }

                            Thread.Sleep(1);
                            if (swBlockAckTimeout.ElapsedMilliseconds > 5000)
                            {
                                try
                                {
                                    File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"),
                                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ACK_BLOCK timeout: segAddr=0x{run.StartAddr:X6}, byteIndex={byteIndex}, lastResp=0x{lastBlockResp:X4}\n");
                                }
                                catch { }

                                throw new InvalidOperationException(
                                    $"Таймаут ACK_BLOCK (segAddr=0x{run.StartAddr:X6}, offset={byteIndex}, last=0x{lastBlockResp:X4}).");
                            }
                        }

                        blockAckTicks += swBlockAck.ElapsedTicks;
                        blockCount++;
                        sentBytes += remaining;

                        int progress = Convert.ToInt16((sentBytes / (float)cycles) * 100);
                        if (progress != lastProgress)
                        {
                            lastProgress = progress;
                            _loadProgress = progress;
                            BeginInvoke(new Action(() => Text = $@"Загрузка...{progress}% ({swLoadTimer.ElapsedMilliseconds / 1000}s)"));
                        }
                    }

                    var swLoadDoneAck = Stopwatch.StartNew();
                    ushort lastLoadDoneResp = 0;
                    while (true)
                    {
                        resp = Read_32();
                        if (resp != 0)
                            lastLoadDoneResp = resp;
                        if (resp == 0x07CC) break;
                        if ((resp & 0xFF00) == 0xDF00)
                        {
                            if (TryRecoverAfterMcuError(resp, ref fileCs, ref cycles, ref loadRuns, ref pOut, ref pBin))
                                goto erase_start;

                            throw new InvalidOperationException("Операция прервана пользователем после ошибки MCU.");
                        }
                        if (resp == 0xDEAD)
                        {
                            if (TryRecoverAfterMcuError(resp, ref fileCs, ref cycles, ref loadRuns, ref pOut, ref pBin))
                                goto erase_start;

                            throw new InvalidOperationException("Операция прервана пользователем после ошибки MCU.");
                        }

                        Thread.Sleep(1);
                        if (swLoadDoneAck.ElapsedMilliseconds > 5000)
                        {
                            try
                            {
                                File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"),
                                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ACK_LOAD timeout: segAddr=0x{run.StartAddr:X6}, lastResp=0x{lastLoadDoneResp:X4}\n");
                            }
                            catch { }

                            throw new InvalidOperationException(
                                $"Таймаут ACK_LOAD (segAddr=0x{run.StartAddr:X6}, last=0x{lastLoadDoneResp:X4}).");
                        }
                    }

                    try { File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Load segment done: addr=0x{run.StartAddr:X6}, bytes={run.Data.Length}\n"); } catch { }
                }

                loadMs = swLoad.ElapsedMilliseconds;

                // Stop load UI timer
                BeginInvoke(new Action(() =>
                {
                    if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
                }));

                // Show connected state in UI now that programming finished
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        Text = @"CAN OK";
                    }
                    catch { }
                }));

                // Верификация контрольной суммы
                var swVerify = Stopwatch.StartNew();
                Thread.Sleep(50); // Give MCU time to prepare for verify on fast machines
                T = 0;
                Send_33(0xCC08);

                // Start UI timer to show elapsed time during verify-start wait
                const int verifyAckTimeoutMs = 10000; // 10s
                var swVerAck = Stopwatch.StartNew();
                BeginInvoke(new Action(() =>
                {
                    if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
                    _verifyTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                    _verifyTimer.Tick += (s, ev) => Text = $@"Ожидание подтверждения верификации... {swVerAck.ElapsedMilliseconds / 1000}s";
                    _verifyTimer.Start();
                }));

                while (true)
                {
                    var verResp = Read_32();
                    if (verResp == 0x09CC) break;
                    Thread.Sleep(1);
                    if (swVerAck.ElapsedMilliseconds >= verifyAckTimeoutMs)
                    {
                        // stop timer before throwing
                        BeginInvoke(new Action(() =>
                        {
                            if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
                        }));
                        throw new Exception("Timeout waiting for verify start ACK from MCU.");
                    }
                }

                // Stop verify-start timer
                BeginInvoke(new Action(() =>
                {
                    if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
                }));

                T = 0;
                Send_33((ushort)((_checksum >> 16) & 0xFFFF));
                Send_33((ushort)((_checksum >> 0) & 0xFFFF));

                // Start UI timer for checksum result wait
                const int verifyRespTimeoutMs = 10000; // 10s
                var swVerifyResp = Stopwatch.StartNew();
                BeginInvoke(new Action(() =>
                {
                    if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
                    _verifyTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                    _verifyTimer.Tick += (s, ev) => Text = $@"Проверка... {swVerifyResp.ElapsedMilliseconds / 1000}s";
                    _verifyTimer.Start();
                }));

                // Wait for checksum result with a bounded timeout to avoid long hangs
                while (swVerifyResp.ElapsedMilliseconds < verifyRespTimeoutMs)
                {
                    var r = Read_32();
                    if (r == 0x08CC)
                    {
                        // Verification OK — stop timer and continue to reboot sequence below.
                        BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
                            }
                            catch { }
                        }));
                        try { File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Verify OK\n"); } catch { }
                        // Break out to proceed with the normal reboot/finalization flow below.
                        break;
                    }
                    if (r == 0x0ECC) // ACK_NOVERIFY: MCU reports checksum mismatch
                    {
                        // stop timer before showing dialog
                        BeginInvoke(new Action(() =>
                        {
                            if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
                        }));

                        // Ask user: 0 = Повторить, 1 = Выбрать другой .out, 2 = Отмена/Выход
                        var decision = AskRetryOrSelectOther();

                        if (decision == 0)
                        {
                            // User requested quick retry — do NOT reinitialize CAN or perform extra handshake.
                            // Drain stale responses and jump to erase_start.
                            try { File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] User selected Retry -> immediate erase_start\n"); } catch { }

                            // Drain stale responses so erase_start waits for fresh ACK_ERASE
                            while (_bootRespQueue.TryDequeue(out _)) { }

                            goto erase_start;
                        }
                        else if (decision == 1)
                        {
                            // User chose to select another .out file. Ask for file on UI thread.
                            string newFile = null;
                            try
                            {
                                Invoke(new Action(() =>
                                {
                                    using (var ofd = new OpenFileDialog())
                                    {
                                        ofd.Filter = "OUT files|*.out";
                                        ofd.InitialDirectory = Application.StartupPath;
                                        if (ofd.ShowDialog(this) == DialogResult.OK)
                                            newFile = ofd.FileName;
                                    }
                                }));
                            }
                            catch
                            {
                                // fallback: no file chosen
                                newFile = null;
                            }

                            if (!string.IsNullOrWhiteSpace(newFile))
                            {
                                // Replace selected file and re-run conversion before restarting erase/load
                                _selectedFileName = newFile;
                                // Force re-conversion of the newly selected file
                                convOk = false;
                                // Allow user to retry conversion or pick another file if conversion/copy fails.
                                while (!convOk)
                                {
                                    try
                                    {
                                        // copy to temporary pOut location and reconvert here so later code picks updated file
                                        pOut = $"{Application.StartupPath}\\Ldr.out";
                                        pBin = $"{Application.StartupPath}\\Ldr.bin";
                                        File.Copy(_selectedFileName, pOut, true);
                                        Cnv2(pOut, pBin);
                                        // Recompute checksum and related values used later
                                        fileCs = ReadFile(pBin);
                                        cycles = (uint)fileCs.Length;
                                        _checksum = (_imgStartAddr - FStartAddr) * 2 * 0xFF;
                                        for (uint bytepos = 0; bytepos < cycles; bytepos++) _checksum += fileCs[bytepos];
                                        convOk = true;
                                    }
                                    catch (Exception)
                                    {
                                        // Conversion failed — ask user to Retry (select another file) or Cancel.
                                        var retryRes = DialogResult.None;
                                        try
                                        {
                                            Invoke(new Action(() =>
                                            {
                                                retryRes = MessageBox.Show(this,
                                                    "Не удалось сконвертировать или скопировать выбранный .out файл. Выберите 'Повторить' чтобы выбрать другой файл, или 'Отмена' чтобы прервать.",
                                                    "Ошибка конвертации",
                                                    MessageBoxButtons.RetryCancel,
                                                    MessageBoxIcon.Error,
                                                    MessageBoxDefaultButton.Button1);
                                            }));
                                        }
                                        catch
                                        {
                                            retryRes = MessageBox.Show(
                                                "Не удалось сконвертировать или скопировать выбранный .out файл. Выберите 'Повторить' чтобы выбрать другой файл, или 'Отмена' чтобы прервать.",
                                                "Ошибка конвертации",
                                                MessageBoxButtons.RetryCancel,
                                                MessageBoxIcon.Error,
                                                MessageBoxDefaultButton.Button1);
                                        }

                                        if (retryRes == DialogResult.Retry)
                                        {
                                            // Ask user to pick another .out
                                            string sel = null;
                                            try
                                            {
                                                Invoke(new Action(() =>
                                                {
                                                    using (var ofd2 = new OpenFileDialog())
                                                    {
                                                        ofd2.Filter = "OUT files|*.out";
                                                        ofd2.InitialDirectory = Application.StartupPath;
                                                        if (ofd2.ShowDialog(this) == DialogResult.OK)
                                                            sel = ofd2.FileName;
                                                    }
                                                }));
                                            }
                                            catch
                                            {
                                                sel = null;
                                            }

                                            if (!string.IsNullOrWhiteSpace(sel))
                                            {
                                                _selectedFileName = sel;
                                                // loop and attempt conversion again
                                                continue;
                                            }
                                            else
                                            {
                                                // No new file chosen — treat as cancel
                                                throw new InvalidOperationException("User cancelled file selection during conversion retry.");
                                            }
                                        }
                                        else
                                        {
                                            // User chose Cancel — abort without reboot
                                            throw new InvalidOperationException("Conversion cancelled by user after failure.");
                                        }
                                    }
                                }

                                // Restart erase sequence on MCU
                                goto erase_start;
                            }
                            else
                            {
                                // User cancelled file selection: abort without reboot
                                throw new InvalidOperationException("User cancelled file selection after verify failure (no MCU reboot).");
                            }
                        }
                        else
                        {
                            // User chose to cancel/exit: abort without reboot to avoid leaving MCU in unreachable state
                            throw new InvalidOperationException("User cancelled operation after verify failure (no MCU reboot).");
                        }
                    }

                    T++;
                    Thread.Sleep(1);
                    if (T > 30000) throw new Exception();
                }
                // If we exited the checksum wait due to timeout, inform user
                if (swVerifyResp.ElapsedMilliseconds >= verifyRespTimeoutMs)
                {
                    try { File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Verify result timeout after {swVerifyResp.ElapsedMilliseconds}ms\n"); } catch { }
                    throw new InvalidOperationException("Таймаут ожидания результата верификации от MCU.");
                }

                verifyMs = swVerify.ElapsedMilliseconds;

                // Перезапуск MCU
                var swReboot = Stopwatch.StartNew();
                Thread.Sleep(50); // Give MCU time to prepare for reboot on fast machines
                T = 0;
                Send_33(0xFE17);
                while (Read_32() != 0x17FE)
                {
                    T++;
                    Thread.Sleep(1);
                    if (T > 30000) throw new Exception();
                }

                if (_appEntryAddr < FStartAddr || _appEntryAddr > FEndAddr)
                {
                    throw new InvalidOperationException($"Invalid app entry before reboot: 0x{_appEntryAddr:X8}");
                }

                try
                {
                    File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sending entryAddr=0x{_appEntryAddr:X8}{Environment.NewLine}");
                }
                catch
                {
                }

                T = 0;
                SendRebootEntry33((ushort)((_appEntryAddr >> 16) & 0xFFFF), (ushort)(_appEntryAddr & 0xFFFF));
                Thread.Sleep(50); // Give MCU time to process reboot entry on fast machines
                Invoke(new Action(() => Text = @"Перезапуск MCU..."));
                while (Read_32() != 0x18FE)
                {
                    T++;
                    Thread.Sleep(1);
                    if (T > 30000) throw new Exception();
                }

                // MCU echoes received entry address in one 8-byte frame: [entryHi, entryLo, 0, 0].
                {
                    bool gotEcho = TryRead_32_FrameWords(5000, out ushort echo0, out ushort echo1, out _, out _);
                    try
                    {
                        if (gotEcho)
                        {
                            var echoedEntry = ((uint)echo0 << 16) | echo1;
                            File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"),
                                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MCU echo entryAddr=0x{echoedEntry:X8} (hi=0x{echo0:X4}, lo=0x{echo1:X4}), sent=0x{_appEntryAddr:X8}{Environment.NewLine}");
                        }
                        else
                        {
                            File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"),
                                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MCU echo entryAddr timeout (single frame), sent=0x{_appEntryAddr:X8}{Environment.NewLine}");
                        }
                    }
                    catch
                    {
                    }
                }

                rebootMs = swReboot.ElapsedMilliseconds;

                swTotal.Stop();
                var kb = cycles / 1024.0;
                var loadSec = Math.Max(loadMs / 1000.0, 0.001);
                var kbPerSec = kb / loadSec;
                var avgAckUs = blockCount == 0 ? 0 : (blockAckTicks * 1_000_000.0 / Stopwatch.Frequency) / blockCount;
                var avgWriteUs = blockCount == 0 ? 0 : (blockWriteTicks * 1_000_000.0 / Stopwatch.Frequency) / blockCount;
                var perfLine =
                    $"[BL PERF] total={swTotal.ElapsedMilliseconds}ms erase={eraseMs}ms load={loadMs}ms verify={verifyMs}ms reboot={rebootMs}ms blocks={blockCount} ackWaits={ackWaitEvents} mode={(useChunkAckV2 ? "chunk" : "legacy")} avgAck={avgAckUs:F1}us avgWrite={avgWriteUs:F1}us speed={kbPerSec:F1}KB/s";
                Debug.WriteLine(perfLine);
                Trace.WriteLine(perfLine);
                try
                {
                    File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_perf.log"),
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {perfLine}{Environment.NewLine}");
                }
                catch
                {
                    // ignored
                }

                // Finalization anchor: verification-success path jumps here (goto done)
done:
                Thread.Sleep(500);
                // PCAN остаётся на 1M — MnReset() переподключит UI
                Cl();
                Thread.Sleep(1000);
            }
            catch (InvalidOperationException ex)
            {
                BeginInvoke(new Action(() => Text = ex.Message));
                BeginInvoke(new Action(() =>
                {
                    if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
                }));
                Cl();
            }
            catch
            {
                BeginInvoke(new Action(() => Text = @"Ошибка загрузки"));
                BeginInvoke(new Action(() =>
                {
                    if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
                }));
                Cl();
            }

            BeginInvoke(new Action(() =>
            {
                if (_verifyTimer != null) { _verifyTimer.Stop(); _verifyTimer.Dispose(); _verifyTimer = null; }
            }));

            if (_canHandle != 0)
            {
                PCANBasic.Uninitialize(_canHandle);
                _canHandle = 0;
            }
            Ch = 0;
            _rden = false;

            Invoke(new Action(MnReset));
            // Update final status after UI reset to avoid intermediate flicker
            BeginInvoke(new Action(() => Text = @"Готово!"));
            Cl();
        }

        public static byte[] ReadFile(string filePath)
        {
            byte[] buffer;
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            try
            {
                var length = (int)fileStream.Length;
                buffer = new byte[length];
                int count;
                var sum = 0;
                while ((count = fileStream.Read(buffer, sum, length - sum)) > 0) sum += count;
            }
            finally
            {
                fileStream.Close();
            }
            return buffer;
        }

        private const byte AddrSize = sizeof(ushort);

        private class SectionInfo
        {
            public uint Addr { get; set; }
            public ushort Len { get; set; }
        }

        private class LoadRun
        {
            public uint StartAddr { get; set; }
            public byte[] Data { get; set; }
        }

        private static uint SwapUInt32(uint dword)
        {
            return ((dword & 0x0000FFFF) << 16) | ((dword & 0xFFFF0000) >> 16);
        }

        private static List<LoadRun> BuildLoadRuns(byte[] image, uint baseAddr)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));

            var runs = new List<LoadRun>();
            int runStart = -1;

            for (int byteIndex = 0; byteIndex < image.Length; byteIndex += 8)
            {
                int remaining = Math.Min(8, image.Length - byteIndex);
                bool blockAllFF = true;
                for (int j = 0; j < remaining; j++)
                {
                    if (image[byteIndex + j] != 0xFF)
                    {
                        blockAllFF = false;
                        break;
                    }
                }

                if (!blockAllFF)
                {
                    if (runStart < 0)
                        runStart = byteIndex;
                }
                else if (runStart >= 0)
                {
                    int runLength = byteIndex - runStart;
                    var runData = new byte[runLength];
                    Buffer.BlockCopy(image, runStart, runData, 0, runLength);

                    runs.Add(new LoadRun
                    {
                        StartAddr = baseAddr + (uint)(runStart / AddrSize),
                        Data = runData
                    });

                    runStart = -1;
                }
            }

            if (runStart >= 0)
            {
                int runLength = image.Length - runStart;
                var runData = new byte[runLength];
                Buffer.BlockCopy(image, runStart, runData, 0, runLength);

                runs.Add(new LoadRun
                {
                    StartAddr = baseAddr + (uint)(runStart / AddrSize),
                    Data = runData
                });
            }

            return runs;
        }

        private static bool TryResolveElfSymbolAddress(BinaryReader rd, uint shoff, ushort shentsz, ushort shnum, string symbolName, out uint symbolAddr)
        {
            symbolAddr = 0;
            if (shoff == 0 || shentsz == 0 || shnum == 0)
                return false;

            for (int i = 0; i < shnum; i++)
            {
                rd.BaseStream.Seek(shoff + (uint)i * shentsz, SeekOrigin.Begin);
                rd.ReadUInt32(); // sh_name
                var shType = rd.ReadUInt32();
                rd.ReadUInt32(); // sh_flags
                rd.ReadUInt32(); // sh_addr
                var shOffset = rd.ReadUInt32();
                var shSize = rd.ReadUInt32();
                var shLink = rd.ReadUInt32();
                rd.ReadUInt32(); // sh_info
                rd.ReadUInt32(); // sh_addralign
                var shEntSize = rd.ReadUInt32();

                // SHT_SYMTAB=2, SHT_DYNSYM=11
                if ((shType != 2U && shType != 11U) || shSize == 0 || shEntSize == 0)
                    continue;

                if (shLink >= shnum)
                    continue;

                rd.BaseStream.Seek(shoff + shLink * shentsz, SeekOrigin.Begin);
                rd.ReadUInt32(); // str_sh_name
                rd.ReadUInt32(); // str_sh_type
                rd.ReadUInt32(); // str_sh_flags
                rd.ReadUInt32(); // str_sh_addr
                var strOffset = rd.ReadUInt32();
                var strSize = rd.ReadUInt32();

                if (strSize == 0)
                    continue;

                rd.BaseStream.Seek(strOffset, SeekOrigin.Begin);
                var strTable = rd.ReadBytes((int)strSize);

                var symCount = shSize / shEntSize;
                for (uint s = 0; s < symCount; s++)
                {
                    rd.BaseStream.Seek(shOffset + s * shEntSize, SeekOrigin.Begin);
                    var stName = rd.ReadUInt32();
                    var stValue = rd.ReadUInt32();
                    rd.ReadUInt32(); // st_size
                    rd.ReadByte();   // st_info
                    rd.ReadByte();   // st_other
                    rd.ReadUInt16(); // st_shndx

                    if (stName >= strTable.Length)
                        continue;

                    int nameStart = (int)stName;
                    int nameEnd = nameStart;
                    while (nameEnd < strTable.Length && strTable[nameEnd] != 0)
                        nameEnd++;

                    var name = Encoding.ASCII.GetString(strTable, nameStart, nameEnd - nameStart);
                    if (name == symbolName)
                    {
                        symbolAddr = stValue;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryRecoverAfterMcuError(ushort errorWord, ref byte[] fileCs, ref uint cycles, ref List<LoadRun> loadRuns, ref string pOut, ref string pBin)
        {
            string details;
            if ((errorWord & 0xFF00) == 0xD000 || (errorWord & 0xFF00) == 0xD100 || (errorWord & 0xFF00) == 0xD200)
            {
                var bank = (errorWord & 0xFF00) == 0xD000 ? 0 : ((errorWord & 0xFF00) == 0xD100 ? 1 : 2);
                var sector = errorWord & 0x00FF;
                details = $"Таймаут стирания FLASH: Bank {bank}, Sector {sector} (0x{errorWord:X4}).";
            }
            else if ((errorWord & 0xFF00) == 0xDF00)
            {
                details = $"Ошибка программирования FLASH (Fapi): 0x{errorWord:X4}.";
            }
            else
            {
                details = $"Фатальная ошибка загрузчика MCU: 0x{errorWord:X4}.";
            }

            try
            {
                File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MCU error: 0x{errorWord:X4}{Environment.NewLine}");
            }
            catch
            {
            }

            var decision = AskRecoveryAction("Ошибка загрузки", details + "\n\nВыберите действие:");
            if (decision == 0)
            {
                while (_bootRespQueue.TryDequeue(out _)) { }
                return true;
            }

            if (decision == 1)
            {
                if (!TrySelectAndConvertOutFile(ref fileCs, ref cycles, ref loadRuns, ref pOut, ref pBin))
                    return false;

                while (_bootRespQueue.TryDequeue(out _)) { }
                return true;
            }

            return false;
        }

        private bool TrySelectAndConvertOutFile(ref byte[] fileCs, ref uint cycles, ref List<LoadRun> loadRuns, ref string pOut, ref string pBin)
        {
            string newFile = null;
            try
            {
                Invoke(new Action(() =>
                {
                    using (var ofd = new OpenFileDialog())
                    {
                        ofd.Filter = "OUT files|*.out";
                        ofd.InitialDirectory = Application.StartupPath;
                        ofd.Title = "Выберите .out файл для загрузки";
                        if (ofd.ShowDialog(this) == DialogResult.OK)
                            newFile = ofd.FileName;
                    }
                }));
            }
            catch
            {
                newFile = null;
            }

            if (string.IsNullOrWhiteSpace(newFile))
                return false;

            _selectedFileName = newFile;

            bool convOk = false;
            while (!convOk)
            {
                try
                {
                    File.Copy(_selectedFileName, pOut, true);

                    Cnv2(pOut, pBin);
                    if (!File.Exists(pBin))
                        throw new InvalidOperationException("Converted bin not found");

                    fileCs = ReadFile(pBin);
                    loadRuns = BuildLoadRuns(fileCs, _imgStartAddr);
                    if (loadRuns.Count == 0)
                        throw new InvalidOperationException("Converted image contains no programmable data blocks.");

                    cycles = 0;
                    _checksum = 0;
                    foreach (var run in loadRuns)
                    {
                        cycles += (uint)run.Data.Length;
                        for (int i = 0; i < run.Data.Length; i++)
                            _checksum += run.Data[i];
                    }

                    convOk = true;
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_error.log"),
                            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Conversion error for '{_selectedFileName}': {ex}{Environment.NewLine}");
                    }
                    catch
                    {
                    }

                    var retryRes = DialogResult.None;
                    try
                    {
                        Invoke(new Action(() =>
                        {
                            retryRes = MessageBox.Show(this,
                                "Не удалось подготовить выбранный .out файл. Выберите 'Повторить' чтобы выбрать другой файл, или 'Отмена' чтобы прервать.",
                                "Ошибка файла",
                                MessageBoxButtons.RetryCancel,
                                MessageBoxIcon.Error,
                                MessageBoxDefaultButton.Button1);
                        }));
                    }
                    catch
                    {
                        retryRes = MessageBox.Show(
                            "Не удалось подготовить выбранный .out файл. Выберите 'Повторить' чтобы выбрать другой файл, или 'Отмена' чтобы прервать.",
                            "Ошибка файла",
                            MessageBoxButtons.RetryCancel,
                            MessageBoxIcon.Error,
                            MessageBoxDefaultButton.Button1);
                    }

                    if (retryRes != DialogResult.Retry)
                        return false;

                    string sel = null;
                    try
                    {
                        Invoke(new Action(() =>
                        {
                            using (var ofd2 = new OpenFileDialog())
                            {
                                ofd2.Filter = "OUT files|*.out";
                                ofd2.InitialDirectory = Application.StartupPath;
                                ofd2.Title = "Выберите .out файл для загрузки";
                                if (ofd2.ShowDialog(this) == DialogResult.OK)
                                    sel = ofd2.FileName;
                            }
                        }));
                    }
                    catch
                    {
                        sel = null;
                    }

                    if (string.IsNullOrWhiteSpace(sel))
                        return false;

                    _selectedFileName = sel;
                }
            }

            return true;
        }

        private void Cnv2(string nput1, string oUtput1)
        {
            var rd = new BinaryReader(File.Open(nput1, FileMode.Open));
            var signature = rd.ReadUInt16();
            if (signature == 0x08AA)
            {
                rd.BaseStream.Seek(16, SeekOrigin.Current);
                rd.ReadUInt32();
                var minAddr = uint.MaxValue;
                var maxAddr = uint.MinValue;
                ushort secLen;
                var secInfoList = new List<SectionInfo>();
                do
                {
                    secLen = (ushort)(rd.ReadUInt16() * AddrSize);
                    if (secLen <= 0) continue;
                    var secAddr = SwapUInt32(rd.ReadUInt32());
                    secInfoList.Add(new SectionInfo { Addr = secAddr, Len = secLen });
                    rd.BaseStream.Seek(secLen, SeekOrigin.Current);
                    if (minAddr > secAddr) minAddr = secAddr;
                    if (maxAddr < secAddr + secLen) maxAddr = secAddr + secLen;
                } while (secLen > 0);
                if (minAddr < maxAddr)
                {
                    var wr = new BinaryWriter(File.Open(oUtput1, FileMode.Create));
                    var memSize = (maxAddr - minAddr) * AddrSize;
                    for (uint bt = 0; bt < memSize; ++bt) wr.Write((byte)0xFF);
                    rd.BaseStream.Seek(22, SeekOrigin.Begin);
                    foreach (var sectionInfo in secInfoList)
                    {
                        rd.BaseStream.Seek(6, SeekOrigin.Current);
                        wr.BaseStream.Seek((sectionInfo.Addr - minAddr) * AddrSize, SeekOrigin.Begin);
                        var arr = new byte[sectionInfo.Len];
                        rd.Read(arr, 0, sectionInfo.Len);
                        wr.Write(arr, 0, sectionInfo.Len);
                    }
                    wr.Close();
                    _imgStartAddr = minAddr;
                }
                else
                {
                    throw new Exception();
                }
            }
            else if (signature == 0x457F) // ELF magic: 0x7F, 'E' (F280039C EABI)
            {
                if (rd.ReadUInt16() != 0x464C) // 'L', 'F'
                {
                    rd.Close();
                    throw new Exception();
                }
                var identTail = rd.ReadBytes(12); // e_ident[4..15]
                if (identTail.Length != 12)
                {
                    rd.Close();
                    throw new InvalidOperationException("Неправильный файл: повреждён заголовок ELF.");
                }

                var eiClass = identTail[0]; // EI_CLASS
                var eiData = identTail[1];  // EI_DATA
                if (eiClass != 1 || eiData != 1)
                {
                    rd.Close();
                    throw new InvalidOperationException("Неправильный файл: ожидается ELF32 little-endian.");
                }

                rd.BaseStream.Seek(16, SeekOrigin.Begin); // начало e_type
                var eType = rd.ReadUInt16(); // e_type
                rd.ReadUInt16(); // e_machine
                rd.ReadUInt32(); // e_version
                var rawEntryAddr = rd.ReadUInt32(); // e_entry (не используем как fallback)
                var phoff = rd.ReadUInt32();    // e_phoff
                var shoff = rd.ReadUInt32();    // e_shoff
                rd.ReadUInt32();                // e_flags
                rd.ReadUInt16();                // e_ehsize
                var phentsz = rd.ReadUInt16();  // e_phentsize
                var phnum = rd.ReadUInt16();    // e_phnum
                var shentsz = rd.ReadUInt16();  // e_shentsize
                var shnum = rd.ReadUInt16();    // e_shnum
                rd.ReadUInt16();                // e_shstrndx

                var segs = new List<(uint addr, uint fsz, uint off)>();
                uint minA = uint.MaxValue, maxA = uint.MinValue;

                for (int i = 0; i < phnum; i++)
                {
                    rd.BaseStream.Seek(phoff + (uint)i * phentsz, SeekOrigin.Begin);
                    var pt = rd.ReadUInt32(); // p_type
                    var poff = rd.ReadUInt32(); // p_offset (host bytes)
                    rd.ReadUInt32();             // p_vaddr
                    var pa = rd.ReadUInt32(); // p_paddr (target word addr)
                    var fsz = rd.ReadUInt32(); // p_filesz (host bytes)

                    if (pt != 1 || fsz == 0) continue; // PT_LOAD only

                    var wc = fsz / AddrSize; // host bytes -> target words
                    segs.Add((pa, fsz, poff));
                    if (minA > pa) minA = pa;
                    if (maxA < pa + wc) maxA = pa + wc;
                }

                if (minA >= maxA || segs.Count == 0)
                {
                    rd.Close();
                    throw new Exception();
                }

                if (!TryResolveElfSymbolAddress(rd, shoff, shentsz, shnum, "_c_int00", out uint symEntry))
                {
                    throw new InvalidOperationException("Неправильный файл: символ _c_int00 не найден. Выберите другой .out.");
                }

                _appEntryAddr = symEntry;

                try
                {
                    File.AppendAllText(Path.Combine(Application.StartupPath, "Bootloader_trace.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ELF entry decode: raw=0x{rawEntryAddr:X8}, symbol(_c_int00)=0x{_appEntryAddr:X8}, minA=0x{minA:X6}, maxA=0x{maxA:X6}{Environment.NewLine}");
                }
                catch
                {
                }

                if (_appEntryAddr < FStartAddr || _appEntryAddr > FEndAddr)
                {
                    throw new InvalidOperationException(
                        $"Invalid application entry address decoded from ELF: 0x{_appEntryAddr:X8}. " +
                        $"Expected within [0x{FStartAddr:X6}..0x{FEndAddr:X6}].");
                }

                // Clip to application area — Sec0-1 (0x080000-0x081FFF) are not erased
                // and must not be programmed by the bootloader.
                uint effectiveMinA = minA < FStartAddr ? FStartAddr : minA;

                var wr = new BinaryWriter(File.Open(oUtput1, FileMode.Create));
                var sz = (maxA - effectiveMinA) * AddrSize;
                for (uint bt = 0; bt < sz; bt++) wr.Write((byte)0xFF);

                foreach (var s in segs)
                {
                    uint segEndWord = s.addr + s.fsz / AddrSize;
                    if (segEndWord <= effectiveMinA) continue; // entirely in bootloader area

                    uint skipWords = s.addr < effectiveMinA ? (effectiveMinA - s.addr) : 0U;
                    uint skipBytes = skipWords * AddrSize;

                    rd.BaseStream.Seek(s.off + skipBytes, SeekOrigin.Begin);
                    wr.BaseStream.Seek((s.addr + skipWords - effectiveMinA) * AddrSize, SeekOrigin.Begin);
                    var dataLen = (int)(s.fsz - skipBytes);
                    if (dataLen <= 0) continue;
                    var d = new byte[dataLen];
                    rd.Read(d, 0, dataLen);
                    wr.Write(d, 0, dataLen);
                }

                wr.Close();
                _imgStartAddr = effectiveMinA;
            }
            else
            {
                rd.Close();
                throw new Exception();
            }
            rd.Close();
        }

        private void Send_33(ushort msg)
        {
            // Use bootloader PC->MCU ID (set to 0x33)
            const uint idNum = 0x33;
            // Always send full 8-byte classic CAN frames to match DMA/bootloader expectations
            var canMsg = new TPCANMsg
            {
                DATA = new byte[8],
                ID = idNum,
                LEN = 8,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD
            };
            // Place 16-bit word into first two bytes, rest remain zero
            canMsg.DATA[0] = (byte)(msg & 0x00FF);
            canMsg.DATA[1] = (byte)((msg >> 8) & 0x00FF);
            var stsResult = PCANBasic.Write(_canHandle, ref canMsg);
            if (stsResult == TPCANStatus.PCAN_ERROR_OK) return;
            throw new Exception();
        }

        private void SendEraseRange33(uint startAddr, uint endAddr)
        {
            var canMsg = new TPCANMsg
            {
                DATA = new byte[8],
                ID = 0x33,
                LEN = 8,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD
            };

            ushort startHi = (ushort)((startAddr >> 16) & 0xFFFF);
            ushort startLo = (ushort)(startAddr & 0xFFFF);
            ushort endHi = (ushort)((endAddr >> 16) & 0xFFFF);
            ushort endLo = (ushort)(endAddr & 0xFFFF);

            canMsg.DATA[0] = (byte)(startHi & 0x00FF);
            canMsg.DATA[1] = (byte)((startHi >> 8) & 0x00FF);
            canMsg.DATA[2] = (byte)(startLo & 0x00FF);
            canMsg.DATA[3] = (byte)((startLo >> 8) & 0x00FF);
            canMsg.DATA[4] = (byte)(endHi & 0x00FF);
            canMsg.DATA[5] = (byte)((endHi >> 8) & 0x00FF);
            canMsg.DATA[6] = (byte)(endLo & 0x00FF);
            canMsg.DATA[7] = (byte)((endLo >> 8) & 0x00FF);

            var stsResult = PCANBasic.Write(_canHandle, ref canMsg);
            if (stsResult == TPCANStatus.PCAN_ERROR_OK) return;
            throw new Exception();
        }

        private void SendRebootEntry33(ushort entryHi, ushort entryLo)
        {
            var canMsg = new TPCANMsg
            {
                DATA = new byte[8],
                ID = 0x33,
                LEN = 8,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD
            };

            canMsg.DATA[0] = (byte)(entryHi & 0x00FF);
            canMsg.DATA[1] = (byte)((entryHi >> 8) & 0x00FF);
            canMsg.DATA[2] = (byte)(entryLo & 0x00FF);
            canMsg.DATA[3] = (byte)((entryLo >> 8) & 0x00FF);

            var stsResult = PCANBasic.Write(_canHandle, ref canMsg);
            if (stsResult == TPCANStatus.PCAN_ERROR_OK) return;
            throw new Exception();
        }

        private void SendSetChunk33(ushort blocksPerChunk)
        {
            var canMsg = new TPCANMsg
            {
                DATA = new byte[8],
                ID = 0x33,
                LEN = 8,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD
            };

            canMsg.DATA[0] = (byte)(CmdSetChunkV2 & 0x00FF);
            canMsg.DATA[1] = (byte)((CmdSetChunkV2 >> 8) & 0x00FF);
            canMsg.DATA[2] = (byte)(blocksPerChunk & 0x00FF);
            canMsg.DATA[3] = (byte)((blocksPerChunk >> 8) & 0x00FF);

            var stsResult = PCANBasic.Write(_canHandle, ref canMsg);
            if (stsResult == TPCANStatus.PCAN_ERROR_OK) return;
            throw new Exception();
        }



        private void SendLoadHeader33(ushort addrHi, ushort addrLo, ushort sizeHi, ushort sizeLo)
        {
            var canMsg = new TPCANMsg
            {
                DATA = new byte[8],
                ID = 0x33,
                LEN = 8,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD
            };

            canMsg.DATA[0] = (byte)(addrHi & 0x00FF);
            canMsg.DATA[1] = (byte)((addrHi >> 8) & 0x00FF);
            canMsg.DATA[2] = (byte)(addrLo & 0x00FF);
            canMsg.DATA[3] = (byte)((addrLo >> 8) & 0x00FF);
            canMsg.DATA[4] = (byte)(sizeHi & 0x00FF);
            canMsg.DATA[5] = (byte)((sizeHi >> 8) & 0x00FF);
            canMsg.DATA[6] = (byte)(sizeLo & 0x00FF);
            canMsg.DATA[7] = (byte)((sizeLo >> 8) & 0x00FF);

            var stsResult = PCANBasic.Write(_canHandle, ref canMsg);
            if (stsResult == TPCANStatus.PCAN_ERROR_OK) return;
            throw new Exception();
        }

        private ushort Read_32()
        {
            // Non-blocking check first: AutoResetEvent collapses multiple Set() calls into one,
            // so already-queued items (e.g. MCU diagnostic 0xEF01/0xEF02 before 0x02CC) must be
            // drained without waiting on the event, otherwise WaitOne consumes the single signal
            // for 0xEF01 and times out while 0x02CC sits uncollected in the queue.
            if (_bootRespQueue.TryDequeue(out ushort qPre))
                return qPre;

            // During firmware update _crw is intentionally stopped to avoid races.
            // In that mode waiting on _bootRespEvent is pointless and adds 300 ms per call.
            if (_crw.IsBusy && !_flWrk.IsBusy)
            {
                ushort queued;
                if (_bootRespEvent.WaitOne(300))
                {
                    if (_bootRespQueue.TryDequeue(out queued))
                        return queued;
                }
            }

            // Fallback: single non-blocking poll from PCAN queue.
            // Keep this path fast because bootloader loops call Read_32() very frequently.
            const ushort msgid = 0x32;
            var sts = PCANBasic.Read(_canHandle, out var canMsg, out var _);
            if (sts != TPCANStatus.PCAN_ERROR_OK)
                return 0;

            if (canMsg.ID != msgid || canMsg.LEN < 2)
                return 0;

            ushort rcvdWord = canMsg.DATA[0];
            ushort temp = canMsg.DATA[1];
            temp <<= 8;
            rcvdWord |= temp;
            return rcvdWord;
        }

        private bool TryRead_32_FrameWords(int timeoutMs, out ushort w0, out ushort w1, out ushort w2, out ushort w3)
        {
            w0 = 0;
            w1 = 0;
            w2 = 0;
            w3 = 0;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var sts = PCANBasic.Read(_canHandle, out var canMsg, out var _);
                if (sts == TPCANStatus.PCAN_ERROR_OK)
                {
                    if (canMsg.ID == 0x32 && canMsg.LEN >= 4)
                    {
                        w0 = (ushort)(canMsg.DATA[0] | (canMsg.DATA[1] << 8));
                        w1 = (ushort)(canMsg.DATA[2] | (canMsg.DATA[3] << 8));
                        if (canMsg.LEN >= 6)
                            w2 = (ushort)(canMsg.DATA[4] | (canMsg.DATA[5] << 8));
                        if (canMsg.LEN >= 8)
                            w3 = (ushort)(canMsg.DATA[6] | (canMsg.DATA[7] << 8));
                        return true;
                    }

                    continue;
                }

                if ((sts & TPCANStatus.PCAN_ERROR_QRCVEMPTY) != 0)
                {
                    Thread.Sleep(1);
                    continue;
                }
            }

            return false;
        }

        private void Binit()
        {
            Cl();
            _flWrk.DoWork += FlWrk_DoWork;
            _bofd = new OpenFileDialog();
            _bofd.FileOk += (d, c) =>
            {
                _selectedFileName = _bofd.FileName;
                _flWrk.RunWorkerAsync();
            };
        }

        // Show a Russian dialog on UI thread with three choices:
        // 0 = Повторить, 1 = Выбрать другой .out, 2 = Отмена/Выход
        private int AskRecoveryAction(string caption, string message)
        {
            DialogResult res = DialogResult.None;
            try
            {
                // Ensure dialog shown on UI thread
                Invoke(new Action(() =>
                {
                    // Create a MessageBox with custom button labels is not supported directly,
                    // so use a simple form to present three options with localized captions.
                    using (var dlg = new Form())
                    {
                        dlg.Text = caption;
                        dlg.StartPosition = FormStartPosition.CenterParent;
                        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                        dlg.MinimizeBox = false;
                        dlg.MaximizeBox = false;
                        dlg.ClientSize = new System.Drawing.Size(460, 150);

                        // Icon on the left
                        var pic = new PictureBox
                        {
                            Size = new System.Drawing.Size(32, 32),
                            Location = new System.Drawing.Point(12, 16),
                            SizeMode = PictureBoxSizeMode.CenterImage,
                            Image = SystemIcons.Warning.ToBitmap()
                        };
                        dlg.Controls.Add(pic);

                        var lbl = new Label();
                        lbl.AutoSize = false;
                        lbl.Text = message;
                        lbl.Size = new System.Drawing.Size(400, 48);
                        lbl.Location = new System.Drawing.Point(56, 12);
                        lbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                        dlg.Controls.Add(lbl);

                        // Flow panel for buttons aligned to the right
                        var flp = new FlowLayoutPanel
                        {
                            FlowDirection = FlowDirection.RightToLeft,
                            Location = new System.Drawing.Point(12, 80),
                            Size = new System.Drawing.Size(dlg.ClientSize.Width - 24, 48),
                            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                            WrapContents = false,
                            Padding = new Padding(0),
                            Margin = new Padding(0)
                        };

                        var btnCancel = new Button();
                        btnCancel.Text = "Прервать (без ребута)";
                        btnCancel.DialogResult = DialogResult.Cancel;
                        btnCancel.Size = new System.Drawing.Size(140, 30);
                        btnCancel.Margin = new Padding(8, 10, 8, 10);

                        var btnSelect = new Button();
                        btnSelect.Text = "Выбрать другой .out";
                        btnSelect.DialogResult = DialogResult.Retry;
                        btnSelect.Size = new System.Drawing.Size(140, 30);
                        btnSelect.Margin = new Padding(8, 10, 8, 10);

                        var btnRetry = new Button();
                        btnRetry.Text = "Повторить";
                        btnRetry.DialogResult = DialogResult.Yes;
                        btnRetry.Size = new System.Drawing.Size(110, 30);
                        btnRetry.Margin = new Padding(8, 10, 8, 10);

                        // Add in order (right-to-left)
                        flp.Controls.Add(btnCancel);
                        flp.Controls.Add(btnSelect);
                        flp.Controls.Add(btnRetry);

                        dlg.Controls.Add(flp);

                        dlg.AcceptButton = btnRetry;
                        dlg.CancelButton = btnCancel;

                        res = dlg.ShowDialog(this);
                    }
                }));
            }
            catch
            {
                // If invoke fails (rare), fall back to MessageBox with Yes/No and treat No as Cancel.
                res = MessageBox.Show(
                    message + "\nНажмите 'Да' чтобы повторить, 'Нет' чтобы выбрать другой .out.",
                    caption,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1);
            }

            if (res == DialogResult.Yes) return 0; // Повторить
            if (res == DialogResult.Retry) return 1; // Выбрать другой .out
            return 2; // Отмена/Выход
        }

        private int AskRetryOrSelectOther()
        {
            return AskRecoveryAction("Проверка контрольной суммы", "Несовпадение контрольной суммы. Выберите действие:");
        }



        private void MnSet()
        {
            _oNm = Text;
            tsmConnect.Text = @"Загрузка";
            tsmConnect.BackColor = Color.Aqua;
            tsmConnect.Click -= connectToolStripMenuItem_Click;
        }

        private void MnReset()
        {
            Text = _oNm;
            tsmConnect.Text = @"Подключить";
            if (_rden && Ch != 0)
            {
                tsmConnect.BackColor = Color.LightGreen;
                cmd_128(0);
            }
            else
            {
                tsmConnect.BackColor = _connectDefaultBackColor;
                CanaConnect();
            }

            tsmConnect.Click -= connectToolStripMenuItem_Click;
            tsmConnect.Click += connectToolStripMenuItem_Click;
        }

        private void Obl(byte h)
        {
            Cl();
            _canHandle = h;
            _bofd.FileName = "*.out";
            // CAN остаётся на 1M — не отключаем
            _bofd.ShowDialog();
        }

        private static void Cl()
        {
            foreach (var file in Directory.GetFiles(Application.StartupPath, "*.b00").Where(item => item.EndsWith(".b00")))
                File.Delete(file);
            foreach (var file in Directory.GetFiles(Application.StartupPath, "*.bin").Where(item => item.EndsWith(".bin")))
                File.Delete(file);
            foreach (var file in Directory.GetFiles(Application.StartupPath, "*.out").Where(item => item.EndsWith(".out")))
                File.Delete(file);
        }
        #endregion
    }
}

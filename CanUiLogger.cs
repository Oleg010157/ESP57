using Peak.Can.Basic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;

namespace ICDIBasic
{
    public partial class Form1
    {
        // Always-available flags used by UI code. Actual logging behavior is
        // enabled only when the CAN_LOG symbol is defined.
        private bool _logCanTraffic = false;
        private bool _throttleCanTraffic = false;
        private const int CanThrottleDelayMs = 1;
        private const int CanLogAutoOffTimeoutMs = 30 * 1000;
        private readonly System.Windows.Forms.Timer _canLogAutoOffTimer = new System.Windows.Forms.Timer();
        private bool _canLogAutoOffTimerInitialized;

        // Checkbox change handler for CAN logging UI
        private void chkCanLog_CheckedChanged(object sender, EventArgs e)
        {
#if CAN_LOG
            ToggleCanLogging(chkCanLog.Checked);
            SyncCanLogUi();
#else
            SyncCanLogUi();
#endif
        }

        private void EnsureCanLogAutoOffTimerInitialized()
        {
            if (_canLogAutoOffTimerInitialized)
            {
                return;
            }

            _canLogAutoOffTimer.Interval = CanLogAutoOffTimeoutMs;
            _canLogAutoOffTimer.Tick += CanLogAutoOffTimer_Tick;
            _canLogAutoOffTimerInitialized = true;
        }

        private void CanLogAutoOffTimer_Tick(object sender, EventArgs e)
        {
            _canLogAutoOffTimer.Stop();
#if CAN_LOG
            if (!_logCanTraffic)
            {
                return;
            }

            ToggleCanLogging(false);
            SyncCanLogUi();
#endif
        }

        // Helper: find latest can_*.tsv in folder
        private static string GetLatestCanLog(string folder)
        {
            if (!Directory.Exists(folder))
            {
                Debug.WriteLine($"CAN log folder does not exist: {folder}");
                return null;
            }

            var fi = new DirectoryInfo(folder).GetFiles("can_*.tsv");
            if (fi.Length == 0)
            {
                Debug.WriteLine($"No can_*.log files found in {folder}");
                return null;
            }

            Array.Sort(fi, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            var latestPath = fi[0].FullName;
            Debug.WriteLine($"Latest CAN log found: {latestPath}");
            return latestPath;
        }

        // UI command: convert last raw log using external tool (canlog.bat)
        private void convertLastCanLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CanLogTools");

            // Check if folder exists
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, 
                    $"CanLogTools folder not found:\n{folder}\n\n" +
                    "Enable CAN logging first to create this folder.",
                    "Folder Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var latest = GetLatestCanLog(folder);
            if (latest == null)
            {
                MessageBox.Show(this, 
                    $"No can_*.log files found in:\n{folder}\n\n" +
                    "Please enable CAN logging and capture some data first.",
                    "No Log Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var batPath = Path.Combine(folder, "canlog.bat");
            if (!File.Exists(batPath))
            {
                MessageBox.Show(this,
                    "canlog.bat not found in CanLogTools folder.\n" +
                    "Enable CAN logging first — the tools are copied automatically when logging is enabled.",
                    "Convert log", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = batPath,
                Arguments = $"\"{latest}\"",
                UseShellExecute = true,
                WorkingDirectory = folder
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to start conversion: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // After starting conversion, also open the folder so user sees results.
            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                // Open folder with explorer.exe using ProcessStartInfo for reliability
                var folderPsi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = folder,
                    UseShellExecute = true
                };
                Process.Start(folderPsi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to open CanLogTools folder: " + ex.Message);
                // Alternative: try to open folder directly (works on .NET Framework)
                try
                {
                    System.Diagnostics.Process.Start(folder);
                }
                catch
                {
                    // If that also fails, just log it
                    Debug.WriteLine("Alternative method to open folder also failed");
                }
            }
        }

        // Synchronize UI checkbox for CAN logging
        private void SyncCanLogUi()
        {
            if (chkCanLog is null)
            {
                return;
            }

            chkCanLog.CheckedChanged -= chkCanLog_CheckedChanged;
            try
            {
                chkCanLog.Checked = _logCanTraffic;
                chkCanLog.Text = _logCanTraffic ? "CAN логирование: ВКЛ" : "CAN логирование: ВЫКЛ";
#if CAN_LOG
                chkCanLog.Enabled = true;
#else
                chkCanLog.Enabled = false;
#endif
            }
            finally
            {
                chkCanLog.CheckedChanged += chkCanLog_CheckedChanged;
            }
        }

#if CAN_LOG
        // ═══════════════════════════════════════════════════════════════════════════════════
        // CAN EVENT LOGGING SUBSYSTEM
        // ═══════════════════════════════════════════════════════════════════════════════════
        //
        // Real-time logging of CAN bus events with automatic delay calculations.
        //
        // LOGGED EVENTS:
        //   TX        - Command transmitted to CAN bus
        //   RX        - Intermediate acknowledgment received
        //   RX13      - Mailbox 13 detailed response received
        //   RX_done   - Final response processed (command complete)
        //
        // OUTPUT:
        //   Single .tsv file per session: Documents\CanLogTools\can_YYYYMMDD_HHmmss_fff.tsv
        //
        // DELAY CALCULATIONS (microseconds):
        //   TxRx_us        = time(RX) - time(TX same cmd)       [shown in RX row]
        //   Rx13_Rx_us     = time(RX13) - time(RX same cmd)     [shown in RX13 row]
        //   RxDone_Rx13_us = time(RX_done) - time(RX13 same cmd) [shown in RX_done row]
        //   RxDone_Tx_us   = time(RX_done) - time(TX same cmd)   [shown in RX_done row - TOTAL RESPONSE TIME]
        //
        // TIMING:
        //   - Precision: Stopwatch.GetTimestamp() (high-resolution counter)
        //   - Time base: Relative to logging session start
        //   - Conversion: ticks * 1,000,000 / Stopwatch.Frequency = microseconds
        //
        // EVENT CORRELATION:
        //   All delays are matched by command ID (cmd) to ensure accurate round-trip timing
        //   in multi-command scenarios. RX events are linked only to their corresponding TX.
        //
        // EXAMPLE:
        //   TX   cmd=75         TxRx_us=        (delay in RX row)
        //   RX   cmd=75         TxRx_us=234     (234 μs from TX)
        //   RX13 cmd=75         Rx13_Rx_us=145  (145 μs from RX)
        //   RX_done cmd=75      RxDone_Rx13_us=89, RxDone_Tx_us=468
        //   
        //   Verification: 234 + 145 + 89 = 468 ✓
        //
        // ═══════════════════════════════════════════════════════════════════════════════════

        // Simple in-memory CAN logging: collect entries to List, write to file on stop.
        // Each entry captures: timestamp (Stopwatch.GetTimestamp), label, CAN ID, and optional data.
        
        private sealed class LogEntry
        {
            public long TsTicksNow { get; set; }
            public string Label { get; set; } = string.Empty;
            public uint Id { get; set; }
            public ushort? Cmd { get; set; }
            public ushort? D1 { get; set; }
            public ushort? D2 { get; set; }
            public ushort? D3 { get; set; }
        }

        private readonly object _canLogLock = new();
        private List<LogEntry> _canLogEntries = new();
        private string _canLogPath = string.Empty;
        private long _canLogBaseTs;
        private long _canLogSeq;

        // Calibration: measure logging overhead by comparing consecutive timestamps
        private long _overheadAccum = 0;
        private int _overheadSamples = 0;

        /// <summary>
        /// Get average overhead per logging event (for diagnostics)
        /// </summary>
        public double GetAverageLoggingOverheadUs()
        {
            if (_overheadSamples == 0)
                return 0;

            long avgTicks = _overheadAccum / _overheadSamples;
            return (avgTicks * 1_000_000.0) / Stopwatch.Frequency;
        }

        // Toggle logging. Present only when CAN_LOG is defined.
        private void ToggleCanLogging(bool? enabled = null)
        {
            var newValue = enabled ?? !_logCanTraffic;
            if (newValue == _logCanTraffic)
            {
                // No change needed
                return;
            }

            if (newValue)
            {
                EnsureCanLogAutoOffTimerInitialized();
                _canLogAutoOffTimer.Stop();
                _canLogAutoOffTimer.Start();

                // Start logging: init list and base timestamp
                _logCanTraffic = true;
                lock (_canLogLock)
                {
                    _canLogEntries.Clear();
                    _canLogSeq = 0;
                    _canLogBaseTs = Stopwatch.GetTimestamp();
                    _canLogPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "CanLogTools",
                        $"can_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");

                    // Create folder with error reporting
                    try
                    {
                        var folderPath = Path.GetDirectoryName(_canLogPath)!;
                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                            Debug.WriteLine($"CAN log folder created: {folderPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to create CAN log folder: {ex.Message}");
                        _logCanTraffic = false;
                        MessageBox.Show(
                            $"Cannot create CanLogTools folder:\n{ex.Message}\n\nLogging disabled.",
                            "CAN Logging Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }
                }

                // Log start marker
                var ts = Stopwatch.GetTimestamp();
                LogCanEvent("LOG_START", 0, ts, null, null, null, null);
            }
            else
            {
                _canLogAutoOffTimer.Stop();

                // Stop logging and write to file
                var ts = Stopwatch.GetTimestamp();
                LogCanEvent("LOG_STOP", 0, ts, null, null, null, null);
                WriteCanLogToFile();
                _logCanTraffic = false;
            }
        }

        // Log a CAN event to in-memory list
        // IMPORTANT: Caller should capture timestamp BEFORE calling this to minimize overhead
        private void LogCanEvent(string label, uint id, long tsTicksNow, ushort? cmd, ushort? d1, ushort? d2, ushort? d3)
        {
            if (!_logCanTraffic)
                return;

            lock (_canLogLock)
            {
                var entry = new LogEntry
                {
                    TsTicksNow = tsTicksNow,
                    Label = label,
                    Id = id,
                    Cmd = cmd,
                    D1 = d1,
                    D2 = d2,
                    D3 = d3
                };

                _canLogEntries.Add(entry);
            }
        }

        // Write all collected entries to file with delay calculations as columns
        // 
        // CRITICAL: All delays are calculated by matching command IDs (cmd field) to ensure
        // correct correlation between TX and its corresponding RX/RX13/RX_done in multi-command
        // scenarios. This prevents mixing delays across different command sequences.
        //
        // Output file format (semicolon-separated):
        //   N;Time_ms;dt_us;Dir;ID;cmd;d1;d2;d3;TxRx_us;Rx13_Rx_us;RxDone_Rx13_us;RxDone_Tx_us
        //
        // Time columns:
        //   Time_ms = absolute time since logging start (milliseconds, 3 decimals)
        //   dt_us = delta from previous event (microseconds)
        //   TxRx_us, Rx13_Rx_us, RxDone_Rx13_us, RxDone_Tx_us = delay columns (microseconds)
        //
        // ACCURACY NOTE:
        //   - Timestamps are captured at LogCanEvent call time, NOT at actual CAN event time
        //   - Logging overhead (~500-2000 ns per event) is NOT subtracted
        //   - Actual CAN bus latencies will be LOWER than reported by 1-3 µs
        //   - Use log data for RELATIVE timing analysis, not absolute latency measurement
        //
        private void WriteCanLogToFile()
        {
            lock (_canLogLock)
            {
                if (_canLogEntries.Count == 0 || string.IsNullOrEmpty(_canLogPath))
                {
                    Debug.WriteLine("CAN log: No entries or invalid path");
                    return;
                }

                try
                {
                    // Change extension from .log to .tsv
                    var tsvPath = Path.ChangeExtension(_canLogPath, ".tsv");
                    var tsvDirectory = Path.GetDirectoryName(tsvPath);

                    // Ensure directory exists before writing
                    if (!Directory.Exists(tsvDirectory))
                    {
                        Debug.WriteLine($"CAN log directory not found: {tsvDirectory}. Creating...");
                        Directory.CreateDirectory(tsvDirectory);
                    }

                    var sbLog = new StringBuilder();
                    sbLog.AppendLine("N;Time_ms;dt_us;Dir;ID;cmd;d1;d2;d3;TxRx_us;Rx13_Rx_us;RxDone_Rx13_us;RxDone_Tx_us");

                    // Helper: convert ticks to microseconds
                    long TicksToUs(long ticks) => ticks * 1_000_000L / Stopwatch.Frequency;

                    // Calculate logging overhead from consecutive event timestamps
                    // This gives us real measured overhead
                    long minDtTicks = long.MaxValue;
                    long sumSmallDtTicks = 0;
                    int smallDtCount = 0;

                    for (int i = 1; i < _canLogEntries.Count; i++)
                    {
                        long dtTicks = _canLogEntries[i].TsTicksNow - _canLogEntries[i-1].TsTicksNow;

                        // Only consider very small deltas (< 10 µs) as they're likely just logging overhead
                        if (dtTicks > 0 && dtTicks < Stopwatch.Frequency / 100_000)  // < 10 µs
                        {
                            minDtTicks = Math.Min(minDtTicks, dtTicks);
                            sumSmallDtTicks += dtTicks;
                            smallDtCount++;
                        }
                    }

                    double measuredOverheadUs = (minDtTicks == long.MaxValue) ? 0 : TicksToUs(minDtTicks);
                    double avgSmallDtUs = (smallDtCount > 0) ? TicksToUs(sumSmallDtTicks / smallDtCount) : 0;

                    // Add metadata as comments
                    sbLog.AppendLine($"# Log session: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sbLog.AppendLine($"# Stopwatch.Frequency: {Stopwatch.Frequency} ticks/sec (resolution: {1_000_000.0 / Stopwatch.Frequency:F3} µs)");
                    sbLog.AppendLine($"# IsHighResolution: {Stopwatch.IsHighResolution}");
                    sbLog.AppendLine($"# Total entries: {_canLogEntries.Count}");
                    sbLog.AppendLine($"#");
                    sbLog.AppendLine($"# MEASURED LOGGING OVERHEAD (from this session):");
                    sbLog.AppendLine($"#   Minimum dt: {measuredOverheadUs:F3} µs");
                    sbLog.AppendLine($"#   Average small dt (< 10µs): {avgSmallDtUs:F3} µs");
                    sbLog.AppendLine($"#");
                    sbLog.AppendLine($"# NOTE: Use these values to estimate actual CAN latencies:");
                    sbLog.AppendLine($"#   Actual_latency ≈ Logged_latency - {measuredOverheadUs:F3} µs");
                    sbLog.AppendLine($"#");
                    sbLog.AppendLine($"# WARNING: Delays include:");
                    sbLog.AppendLine($"#   - Lock acquisition time");
                    sbLog.AppendLine($"#   - List insertion overhead");
                    sbLog.AppendLine($"#   - Thread context switch latency");
                    sbLog.AppendLine($"#   - Time between actual CAN event and logging call");
                    sbLog.AppendLine($"# For RELATIVE timing analysis, these errors cancel out.");
                    sbLog.AppendLine();

                    // Pre-calculate all delays for quick lookup
                    var txEntries = _canLogEntries.Where(e => e.Label == "TX").ToList();
                    var rxEntries = _canLogEntries.Where(e => e.Label == "RX").ToList();
                    var rx13Entries = _canLogEntries.Where(e => e.Label == "RX13").ToList();
                    var rxDoneEntries = _canLogEntries.Where(e => e.Label == "RX_done").ToList();

                    long seqNum = 1;
                    long prevTsTicksNow = _canLogBaseTs;  // Start from log base time

                    foreach (var entry in _canLogEntries)
                    {
                        // Calculate time since log start in microseconds, convert to ms with decimals
                        long tsDeltaUs = TicksToUs(entry.TsTicksNow - _canLogBaseTs);
                        double tsDeltaMs = tsDeltaUs / 1000.0;

                        // Calculate dt_us as time between current and previous event
                        long dtUs = TicksToUs(entry.TsTicksNow - prevTsTicksNow);
                        prevTsTicksNow = entry.TsTicksNow;

                        // Calculate delays for this entry (delay is in the row of the LATER event)
                        // IMPORTANT: All searches use 'LastOrDefault(... && t.Cmd == entry.Cmd)'
                        // to match events by command ID. This ensures correct pairing in multi-command
                        // scenarios where multiple TX/RX sequences are interleaved.
                        long? txRxDelayUs = null;
                        long? rx13RxDelayUs = null;
                        long? rxDoneRx13DelayUs = null;
                        long? rxDoneTxDelayUs = null;

                        // RX row contains delay from previous TX (same command)
                        if (entry.Label == "RX")
                        {
                            var prevTx = txEntries.LastOrDefault(t => t.TsTicksNow < entry.TsTicksNow && t.Cmd == entry.Cmd);
                            if (prevTx != null)
                                txRxDelayUs = TicksToUs(entry.TsTicksNow - prevTx.TsTicksNow);
                        }
                        // RX13 row contains delay from previous RX (same command)
                        else if (entry.Label == "RX13")
                        {
                            var prevRx = rxEntries.LastOrDefault(r => r.TsTicksNow < entry.TsTicksNow && r.Cmd == entry.Cmd);
                            if (prevRx != null)
                                rx13RxDelayUs = TicksToUs(entry.TsTicksNow - prevRx.TsTicksNow);
                        }
                        // RX_done row contains delays from previous RX13 and previous TX (same command, response time)
                        else if (entry.Label == "RX_done")
                        {
                            var prevRx13 = rx13Entries.LastOrDefault(r => r.TsTicksNow < entry.TsTicksNow && r.Cmd == entry.Cmd);
                            if (prevRx13 != null)
                                rxDoneRx13DelayUs = TicksToUs(entry.TsTicksNow - prevRx13.TsTicksNow);

                            var prevTx = txEntries.LastOrDefault(t => t.TsTicksNow < entry.TsTicksNow && t.Cmd == entry.Cmd);
                            if (prevTx != null)
                                rxDoneTxDelayUs = TicksToUs(entry.TsTicksNow - prevTx.TsTicksNow);
                        }

                        // Write row
                        sbLog.Append(seqNum.ToString(CultureInfo.InvariantCulture));
                        sbLog.Append(';');
                        sbLog.Append(tsDeltaMs.ToString("F3", CultureInfo.InvariantCulture));
                        sbLog.Append(';');
                        sbLog.Append(dtUs.ToString(CultureInfo.InvariantCulture));
                        sbLog.Append(';');
                        sbLog.Append(entry.Label);
                        sbLog.Append(';');
                        sbLog.Append(entry.Id.ToString(CultureInfo.InvariantCulture));
                        sbLog.Append(';');
                        sbLog.Append(entry.Cmd?.ToString(CultureInfo.InvariantCulture) ?? "");
                        sbLog.Append(';');
                        sbLog.Append(entry.D1?.ToString(CultureInfo.InvariantCulture) ?? "");
                        sbLog.Append(';');
                        sbLog.Append(entry.D2?.ToString(CultureInfo.InvariantCulture) ?? "");
                        sbLog.Append(';');
                        sbLog.Append(entry.D3?.ToString(CultureInfo.InvariantCulture) ?? "");
                        sbLog.Append(';');
                        sbLog.Append(txRxDelayUs?.ToString(CultureInfo.InvariantCulture) ?? "");
                        sbLog.Append(';');
                        sbLog.Append(rx13RxDelayUs?.ToString(CultureInfo.InvariantCulture) ?? "");
                        sbLog.Append(';');
                        sbLog.Append(rxDoneRx13DelayUs?.ToString(CultureInfo.InvariantCulture) ?? "");
                        sbLog.Append(';');
                        sbLog.Append(rxDoneTxDelayUs?.ToString(CultureInfo.InvariantCulture) ?? "");
                        sbLog.AppendLine();

                        seqNum++;
                    }

                    File.WriteAllText(tsvPath, sbLog.ToString(), Encoding.UTF8);
                    Debug.WriteLine($"CAN log written: {tsvPath} ({_canLogEntries.Count} entries)");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to write CAN log: {ex.Message}");
                }
            }
        }

        // Public wrapper for logging CAN events
        // NOTE: Caller can optionally pass pre-captured timestamp for maximum accuracy.
        // If not provided, timestamp is captured at this call time.
        public void AppendUiCanLogLine(string dir, uint id, ushort? cmd = null, ushort? d1 = null, ushort? d2 = null, ushort? d3 = null, long? preCapturedTs = null)
        {
#if CAN_LOG
            var ts = preCapturedTs ?? Stopwatch.GetTimestamp();
            LogCanEvent(dir, id, ts, cmd, d1, d2, d3);
#endif
        }

#endif
    }
}

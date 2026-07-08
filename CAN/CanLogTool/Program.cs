using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

return CanLogTool.Program.Run(args);

namespace CanLogTool
{
    internal static class Program
    {

        private sealed class Options
        {
            public string InputPath { get; set; }
            public string OutputPath { get; set; }
            public bool IncludeComments { get; set; }
            public bool Strict { get; set; }
        }

        private sealed class Row
        {
            public long N { get; set; }
            public long TsTicks { get; set; }
            public string Dir { get; set; } = string.Empty;
            public uint Id { get; set; }
            public ushort? Cmd { get; set; }
            public ushort? D1 { get; set; }
            public ushort? D2 { get; set; }
            public ushort? D3 { get; set; }
        }

        private readonly struct Key : IEquatable<Key>
        {
            public Key(ushort cmd)
            {
                Cmd = cmd;
            }

            public ushort Cmd { get; }

            public bool Equals(Key other) => Cmd == other.Cmd;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => Cmd.GetHashCode();
        }

        internal static int Run(string[] args)
        {
            try
            {
                var options = ParseArgs(args);
                if (options is null)
                {
                    PrintUsage();
                    return 2;
                }

                if (string.IsNullOrWhiteSpace(options.InputPath) || !File.Exists(options.InputPath))
                {
                    Console.Error.WriteLine("Input file not found.");
                    return 2;
                }

                var lines = File.ReadLines(options.InputPath);
                var (frequency, dataRows, commentLines) = ParseLog(lines, options.Strict);
                if (dataRows.Count == 0)
                {
                    Console.Error.WriteLine("No data rows found.");
                    return 1;
                }

                using var writer = CreateWriter(options.OutputPath);

                if (options.IncludeComments)
                {
                    foreach (var c in commentLines)
                    {
                        writer.WriteLine(c);
                    }
                }

                writer.WriteLine("N\tTime_ms\tdt_us\tDir\tID\tcmd\td1\td2\td3\tTxRx_us\tmbox13\tcmd_done");

                var baseTs = dataRows[0].TsTicks;
                long? prevTs = null;

                var lastTxByKey = new Dictionary<Key, long>();
                var lastRxByKey = new Dictionary<Key, long>();

                foreach (var r in dataRows)
                {
                    var timeUs = ToUs(r.TsTicks - baseTs, frequency);
                    var timeMs = timeUs / 1000.0;
                    long? dtUs = prevTs is null ? null : ToUs(r.TsTicks - prevTs.Value, frequency);
                    prevTs = r.TsTicks;

                    long? txRxUs = null;
                    long? rxDoneMinusTxUs = null;
                    long? rx13MinusRxUs = null;

                    if (r.Cmd is ushort cmd)
                    {
                        var key = new Key(cmd);

                        if (string.Equals(r.Dir, "TX", StringComparison.OrdinalIgnoreCase))
                        {
                            lastTxByKey[key] = r.TsTicks;
                        }
                        else if (string.Equals(r.Dir, "RX", StringComparison.OrdinalIgnoreCase))
                        {
                            if (lastTxByKey.TryGetValue(key, out var txTs))
                            {
                                txRxUs = ToUs(r.TsTicks - txTs, frequency);
                            }
                            lastRxByKey[key] = r.TsTicks;
                        }
                        else if (string.Equals(r.Dir, "RX13", StringComparison.OrdinalIgnoreCase))
                        {
                            if (lastRxByKey.TryGetValue(key, out var rxTs))
                            {
                                rx13MinusRxUs = ToUs(r.TsTicks - rxTs, frequency);
                            }
                        }
                        else if (string.Equals(r.Dir, "RX_done", StringComparison.OrdinalIgnoreCase))
                        {
                            if (lastTxByKey.TryGetValue(key, out var txTs))
                            {
                                rxDoneMinusTxUs = ToUs(r.TsTicks - txTs, frequency);
                            }
                        }
                    }

                    writer.Write(r.N.ToString(CultureInfo.InvariantCulture));
                    writer.Write("\t");
                    writer.Write(timeMs.ToString("0.###", CultureInfo.InvariantCulture));
                    writer.Write("\t");
                    writer.Write(dtUs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                    writer.Write("\t");
                    writer.Write(r.Dir);
                    writer.Write("\t");
                    writer.Write(r.Id.ToString(CultureInfo.InvariantCulture));
                    writer.Write("\t");
                    writer.Write(r.Cmd?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                    writer.Write("\t");
                    writer.Write(r.D1?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                    writer.Write("\t");
                    writer.Write(r.D2?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                    writer.Write("\t");
                    writer.Write(r.D3?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                    writer.Write("\t");
                    writer.Write(txRxUs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                    writer.Write("\t");
                    writer.Write(rx13MinusRxUs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                    writer.Write("\t");
                    writer.Write(rxDoneMinusTxUs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                    writer.WriteLine();
                }

                return 0;
            }
            catch (FormatException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }

        private static TextWriter CreateWriter(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Console.Out;
            }

            return new StreamWriter(File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read));
        }

        private static (long frequency, List<Row> rows, List<string> comments) ParseLog(IEnumerable<string> lines, bool strict)
        {
            long frequency = 0;
            var comments = new List<string>();
            var rows = new List<Row>();

            foreach (var raw in lines)
            {
                var line = (raw ?? string.Empty).TrimEnd('\r', '\n');
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '#')
                {
                    comments.Add(line);
                    const string prefix = "# Stopwatch.Frequency=";
                    if (line.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        var part = line.Substring(prefix.Length);
                        var idx = part.IndexOf(' ');
                        var num = idx < 0 ? part : part.Substring(0, idx);
                        if (long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out var f))
                        {
                            frequency = f;
                        }
                    }

                    continue;
                }

                var cols = line.Split('\t');
                if (cols.Length < 4)
                {
                    if (strict)
                    {
                        throw new FormatException("Invalid data row: expected at least 4 TAB-separated columns.");
                    }
                    continue;
                }

                var r = new Row
                {
                    N = ParseLong(cols[0]),
                    TsTicks = ParseLong(cols[1]),
                    Dir = cols[2],
                    Id = ParseUInt(cols[3])
                };

                // Raw format only: N ts dir id cmd d1 d2 d3
                // (TAB-separated, cmd is a dedicated column)
                if (cols.Length < 8)
                {
                    if (strict)
                    {
                        throw new FormatException("Invalid data row: expected 8 TAB-separated columns (N ts dir id cmd d1 d2 d3).");
                    }

                    continue;
                }

                r.Cmd = TryParseUShort(cols, 4);
                r.D1 = TryParseUShort(cols, 5);
                r.D2 = TryParseUShort(cols, 6);
                r.D3 = TryParseUShort(cols, 7);

                rows.Add(r);
            }

            if (frequency <= 0)
            {
                frequency = 10_000_000;
            }

            return (frequency, rows, comments);
        }

        private static ushort? TryParseUShort(string[] cols, int idx)
        {
            if (idx >= cols.Length)
            {
                return null;
            }

            var s = cols[idx];
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            if (ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }

            return null;
        }

        private static long ParseLong(string s)
        {
            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                throw new FormatException("Invalid integer value: " + s);
            }

            return v;
        }

        private static uint ParseUInt(string s)
        {
            if (!uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                throw new FormatException("Invalid uint value: " + s);
            }

            return v;
        }

        private static double ToMs(long ticks, long frequency) => ticks * (1000.0 / frequency);

        private static long ToUs(long ticks, long frequency)
        {
            var us = ticks * (1_000_000.0 / frequency);
            return (long)Math.Round(us, MidpointRounding.AwayFromZero);
        }

        private static Options ParseArgs(string[] args)
        {
            if (args.Length == 0)
            {
                return null;
            }

            var o = new Options();
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (string.Equals(a, "-i", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--in", StringComparison.OrdinalIgnoreCase))
                {
                    o.InputPath = NextValue(args, ref i);
                }
                else if (string.Equals(a, "-o", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase))
                {
                    o.OutputPath = NextValue(args, ref i);
                }
                else if (string.Equals(a, "--include-comments", StringComparison.OrdinalIgnoreCase))
                {
                    o.IncludeComments = true;
                }
                else if (string.Equals(a, "--strict", StringComparison.OrdinalIgnoreCase))
                {
                    o.Strict = true;
                }
                else if (string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/?", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                else if (!a.StartsWith("-", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(o.InputPath))
                {
                    o.InputPath = a;
                }
                else
                {
                    throw new FormatException("Unknown argument: " + a);
                }
            }

            return o;
        }

        private static string NextValue(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                throw new FormatException("Missing value for " + args[i]);
            }

            i++;
            return args[i];
        }

        private static void PrintUsage()
        {
            Console.WriteLine("CanLogTool - CAN UI log post-processor");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  CanLogTool --in <can_xxx.log> [--out <out.tsv>] [--include-comments] [--strict]");
            Console.WriteLine();
            Console.WriteLine("Output columns (TAB-separated):");
            Console.WriteLine("  N, Time_ms, dt_us, Dir, ID, cmd, d1, d2, d3, rx_done_tx_us, rx13_rx_us");
        }
    }
}

using System;
using System.Diagnostics;
using Peak.Can.Basic;

namespace ICDIBasic
{
    /// <summary>
    /// Класс для профилирования производительности Kadr60Rx вне зависимости от сетевой latency.
    /// Используется для измерения чистого времени обработки CAN-кадров.
    /// </summary>
    public class Kadr60Benchmark
    {
        private readonly Form1 _form;

        public Kadr60Benchmark(Form1 form)
        {
            _form = form ?? throw new ArgumentNullException(nameof(form));
        }

        /// <summary>
        /// Запускает профилирование методом генерации тестовых CAN-сообщений
        /// </summary>
        /// <param name="iterations">Количество полных циклов CAN-кадров для обработки</param>
        public void RunBenchmark(int iterations = 100)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  KADR60RX BENCHMARK - Measuring Pure Processing Time  ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝\n");

            // Сбрасываем предыдущие данные
            _form.ResetFrameTimings();

            // Перемешиваем ID кадров реалистично
            int[] frameIds = { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 130, 131 };
            Random rand = new Random(42); // Фиксированный seed для воспроизводимости

            Console.WriteLine($"Processing {iterations} complete frame cycles...");
            Console.WriteLine($"Testing {frameIds.Length} different frame types\n");

            var totalStopwatch = Stopwatch.StartNew();

            for (int cycle = 0; cycle < iterations; cycle++)
            {
                // Случайный порядок кадров (реалистичное поведение CAN)
                for (int i = frameIds.Length - 1; i > 0; i--)
                {
                    int randomIndex = rand.Next(i + 1);
                    int temp = frameIds[i];
                    frameIds[i] = frameIds[randomIndex];
                    frameIds[randomIndex] = temp;
                }

                // Обрабатываем каждый ID кадра
                foreach (int frameId in frameIds)
                {
                    TPCANMsg msg = CreateTestMessage((uint)frameId);
                    _form.Kadr60Rx(msg);
                }
            }

            totalStopwatch.Stop();

            long totalMicros = totalStopwatch.Elapsed.Ticks / 10;
            long perFrameMicros = totalMicros / (frameIds.Length * iterations);

            Console.WriteLine("\n╔════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║ Total elapsed: {totalStopwatch.Elapsed.TotalMilliseconds,8:F2} ms                    ║");
            Console.WriteLine($"║ Per frame avg: {perFrameMicros,8} µs              ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝\n");

            // Выводим подробный отчёт
            _form.PrintFrameTimingsReport();
        }

        /// <summary>
        /// Запускает микро-тест одного конкретного кадра
        /// </summary>
        /// <param name="frameId">ID кадра для тестирования</param>
        /// <param name="iterations">Количество повторений</param>
        public void BenchmarkSingleFrame(int frameId, int iterations = 10000)
        {
            Console.WriteLine($"\n╔════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║  Testing Frame {frameId:000} ({iterations} iterations)");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝\n");

            _form.ResetFrameTimings();

            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                TPCANMsg msg = CreateTestMessage((uint)frameId);
                _form.Kadr60Rx(msg);
            }

            sw.Stop();

            long totalMicros = sw.Elapsed.Ticks / 10;
            long avgMicros = totalMicros / iterations;
            double framespersec = iterations / sw.Elapsed.TotalSeconds;

            Console.WriteLine($"Total time:     {sw.Elapsed.TotalMilliseconds:F2} ms");
            Console.WriteLine($"Average time:   {avgMicros} µs per frame");
            Console.WriteLine($"Throughput:     {framespersec:F0} frames/sec\n");

            _form.PrintFrameTimingsReport();
        }

        private static TPCANMsg CreateTestMessage(uint frameId)
        {
            return new TPCANMsg
            {
                ID = frameId,
                LEN = 8,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD,
                DATA = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }
            };
        }

        /// <summary>
        /// Стресс-тест: максимальная нагрузка на время обработки
        /// </summary>
        public void StressTest(int durationSeconds = 5)
        {
            Console.WriteLine($"\n╔════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║  STRESS TEST - {durationSeconds} seconds");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝\n");

            _form.ResetFrameTimings();

            int[] frameIds = { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 130, 131 };
            Random rand = new Random();

            var sw = Stopwatch.StartNew();
            long frameCount = 0;

            while (sw.Elapsed.TotalSeconds < durationSeconds)
            {
                int frameId = frameIds[rand.Next(frameIds.Length)];
                TPCANMsg msg = CreateTestMessage((uint)frameId);
                _form.Kadr60Rx(msg);
                frameCount++;
            }

            sw.Stop();

            double framespersec = frameCount / sw.Elapsed.TotalSeconds;

            Console.WriteLine($"Frames processed: {frameCount}");
            Console.WriteLine($"Total time:       {sw.Elapsed.TotalMilliseconds:F2} ms");
            Console.WriteLine($"Throughput:       {framespersec:F0} frames/sec\n");

            _form.PrintFrameTimingsReport();
        }
    }
}

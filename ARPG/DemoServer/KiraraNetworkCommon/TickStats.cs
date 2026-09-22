using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Kirara.Network
{
    /// <summary>
    /// Per-tick timing, broken down by stage. Only touched from the tick thread.
    /// Enable with the --tick-stats flag or KIRARA_TICK_STATS=1; prints one line per report window.
    /// </summary>
    public static class TickStats
    {
        public static bool Enabled { get; set; }
        /// <summary>True while tick update callbacks run, so stage timers ignore work done by message handlers.</summary>
        public static bool InUpdate { get; set; }
        public static int ReportIntervalMs { get; set; } = 5000;

        private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

        // Accumulated between two ticks (message handling runs between ticks).
        private static long _inputTicks;
        // Accumulated inside the current tick's update callbacks.
        private static long _simTicks;
        private static long _serializeTicks;
        private static long _sendTicks;

        private static long _msgsInWindow;
        private static long _bytesOutInWindow;
        private static long _windowStart = Stopwatch.GetTimestamp();
        private static readonly List<Sample> Samples = new(4096);

        private struct Sample
        {
            public double Input, Sim, Serialize, Send, Total;
        }

        public static void AddInput(long ticks, int msgCount)
        {
            _inputTicks += ticks;
            _msgsInWindow += msgCount;
        }

        public static void AddSim(long ticks) => _simTicks += ticks;
        public static void AddSerialize(long ticks) => _serializeTicks += ticks;

        public static void AddSend(long ticks, int bytes)
        {
            _sendTicks += ticks;
            _bytesOutInWindow += bytes;
        }

        /// <summary>Called once per tick after all update callbacks ran. updateTicks = wall time of the callbacks.</summary>
        public static void EndTick(long updateTicks)
        {
            double input = _inputTicks * MsPerTick;
            Samples.Add(new Sample
            {
                Input = input,
                Sim = _simTicks * MsPerTick,
                Serialize = _serializeTicks * MsPerTick,
                Send = _sendTicks * MsPerTick,
                Total = input + updateTicks * MsPerTick,
            });
            _inputTicks = _simTicks = _serializeTicks = _sendTicks = 0;

            long now = Stopwatch.GetTimestamp();
            double windowMs = (now - _windowStart) * MsPerTick;
            if (windowMs >= ReportIntervalMs)
            {
                Report(windowMs);
                Samples.Clear();
                _msgsInWindow = 0;
                _bytesOutInWindow = 0;
                _windowStart = now;
            }
        }

        private static void Report(double windowMs)
        {
            if (Samples.Count == 0) return;
            double secs = windowMs / 1000.0;
            string Stage(string name, Func<Sample, double> f)
            {
                var v = Samples.Select(f).OrderBy(x => x).ToArray();
                return $"{name} p50={Pct(v, 0.50):F3} p95={Pct(v, 0.95):F3}";
            }
            var total = Samples.Select(s => s.Total).OrderBy(x => x).ToArray();
            Console.WriteLine(
                $"[tick] n={Samples.Count} ({Samples.Count / secs:F1}/s) in={_msgsInWindow / secs:F0} msg/s out={_bytesOutInWindow / secs / 1024:F0} KiB/s | " +
                $"total p50={Pct(total, 0.50):F3} p95={Pct(total, 0.95):F3} p99={Pct(total, 0.99):F3} max={total[^1]:F3} ms | " +
                $"{Stage("input", s => s.Input)} | {Stage("sim", s => s.Sim)} | {Stage("serialize", s => s.Serialize)} | {Stage("send", s => s.Send)}");
        }

        private static double Pct(double[] sorted, double p)
        {
            if (sorted.Length == 0) return 0;
            int i = (int)Math.Ceiling(p * sorted.Length) - 1;
            return sorted[Math.Clamp(i, 0, sorted.Length - 1)];
        }
    }
}

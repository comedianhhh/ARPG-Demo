using System;
using ZZZServer.Math;

namespace ZZZServer.Tests
{
    public static class DeterminismTest
    {
        public static void Run()
        {
            Console.WriteLine("=== Determinism & Fixed-Point Verification ===");

            // Test 1: Cross-platform math representation
            var fp1 = FixedPoint.FromDouble(1.5);
            var fp2 = FixedPoint.FromDouble(2.2);
            var result = fp1 + fp2;

            Console.WriteLine($"FixedPoint Addition: 1.5 + 2.2 = {result} (Raw Value: {result.RawValue})");
            if (result.RawValue == FixedPoint.FromDouble(3.7).RawValue)
            {
                Console.WriteLine("[PASS] Addition matches representation precisely.");
            }
            else
            {
                Console.WriteLine("[FAIL] Addition precision error!");
            }

            // Test 2: Deterministic Random Number Generation
            var rand1 = new DeterministicRandom(1337);
            var rand2 = new DeterministicRandom(1337);

            bool randPass = true;
            for (int i = 0; i < 5; i++)
            {
                uint val1 = rand1.Next();
                uint val2 = rand2.Next();
                Console.WriteLine($"Random seed 1337 step {i}: Bot1={val1}, Bot2={val2}");
                if (val1 != val2)
                {
                    randPass = false;
                }
            }

            if (randPass)
            {
                Console.WriteLine("[PASS] Pseudo-Random stream is 100% deterministic.");
            }
            else
            {
                Console.WriteLine("[FAIL] Random streams desynced!");
            }
        }
    }
}

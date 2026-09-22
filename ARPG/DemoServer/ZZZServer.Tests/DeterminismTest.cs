using System;
using ZZZServer.FixedMath;

namespace ZZZServer.Tests
{
    /// <summary>
    /// Two layers:
    /// 1. Exact-value checks on the 32.32 type (the cases a lossy implementation gets wrong).
    /// 2. A 10,000-tick fixed-point simulation reduced to one 64-bit state hash. Same seed must give the
    ///    same hash on every run, every OS and every CPU — CI runs it on Windows x64, Linux x64 and macOS arm64
    ///    and compares against the golden hash.
    /// </summary>
    public static class DeterminismTest
    {
        public const ulong GoldenHash = 0x30C551C977A55FF9UL; // first computed on Windows x64 / .NET 8; CI checks every platform against it

        public static bool Run(ulong? expectedHash = null)
        {
            Console.WriteLine("=== Determinism & Fixed-Point Verification ===");
            int failures = 0;

            void Check(string name, bool ok, string detail = "")
            {
                Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}{(detail.Length > 0 ? " — " + detail : "")}");
                if (!ok) failures++;
            }

            // Tiny fractions must survive multiplication (a 16-bit pre-shift would truncate this to zero).
            var tiny = FixedPoint.FromRaw(12345);
            Check("tiny * 3 keeps all 32 fractional bits", (tiny * FixedPoint.FromInt(3)).RawValue == 37035,
                $"raw {(tiny * FixedPoint.FromInt(3)).RawValue}");

            // 40000 * 40000 = 1.6e9: fits the 32-bit integer part, but the raw 64x64 product overflows 64 bits.
            var big = FixedPoint.FromInt(40000);
            Check("40000 * 40000 via 128-bit intermediate", big * big == FixedPoint.FromInt(1_600_000_000));

            Check("1.5 * 2 == 3", FixedPoint.FromDouble(1.5) * FixedPoint.FromInt(2) == FixedPoint.FromInt(3));
            Check("3 / 2 == 1.5", FixedPoint.FromInt(3) / FixedPoint.FromInt(2) == FixedPoint.FromDouble(1.5));
            Check("-7 / 2 == -3.5", FixedPoint.FromInt(-7) / FixedPoint.FromInt(2) == FixedPoint.FromDouble(-3.5));
            Check("sqrt(9) == 3", FixedPoint.Sqrt(FixedPoint.FromInt(9)) == FixedPoint.FromInt(3));
            Check("sqrt(2) == floor(sqrt(2) * 2^32)", FixedPoint.Sqrt(FixedPoint.FromInt(2)).RawValue == 6074000999L,
                $"raw {FixedPoint.Sqrt(FixedPoint.FromInt(2)).RawValue}");

            ulong first = SimulationHash(seed: 1337, bodies: 256, ticks: 10_000);
            ulong second = SimulationHash(seed: 1337, bodies: 256, ticks: 10_000);
            Console.WriteLine($"Simulation hash (seed 1337, 256 bodies, 10,000 ticks): {first:X16}");
            Check("same seed, same hash (in-process rerun)", first == second);
            Check("different seed, different hash", SimulationHash(seed: 1338, bodies: 256, ticks: 10_000) != first);

            ulong expected = expectedHash ?? GoldenHash;
            if (expected != 0)
            {
                Check("matches expected hash", first == expected, $"expected {expected:X16}");
            }

            Console.WriteLine(failures == 0 ? "All determinism checks passed." : $"{failures} determinism check(s) FAILED.");
            return failures == 0;
        }

        /// <summary>
        /// Bodies in a box: random impulses, damping (multiply), bounces off the walls, and a distance check
        /// between neighbours (sqrt). Every tick's full state is folded into an FNV-1a hash.
        /// </summary>
        public static ulong SimulationHash(uint seed, int bodies, int ticks)
        {
            var rng = new DeterministicRandom(seed);
            var pos = new Vector3Fixed[bodies];
            var vel = new Vector3Fixed[bodies];
            var dt = FixedPoint.FromInt(1) / FixedPoint.FromInt(50);
            var damping = FixedPoint.FromInt(98) / FixedPoint.FromInt(100);
            var half = FixedPoint.FromRaw(1L << 31);
            var bound = FixedPoint.FromInt(100);
            var contact = FixedPoint.FromInt(2);

            for (int i = 0; i < bodies; i++)
            {
                pos[i] = new Vector3Fixed(rng.NextFixedPoint() * bound, FixedPoint.Zero, rng.NextFixedPoint() * bound);
            }

            ulong hash = 14695981039346656037UL;
            for (int t = 0; t < ticks; t++)
            {
                for (int i = 0; i < bodies; i++)
                {
                    var impulse = new Vector3Fixed(rng.NextFixedPoint() - half, FixedPoint.Zero, rng.NextFixedPoint() - half);
                    var v = vel[i] + impulse;
                    v = new Vector3Fixed(v.X * damping, v.Y, v.Z * damping);
                    var p = pos[i] + new Vector3Fixed(v.X * dt, v.Y * dt, v.Z * dt);

                    if (p.X < FixedPoint.Zero || p.X > bound) { v.X = -v.X; p.X = FixedPoint.Min(FixedPoint.Max(p.X, FixedPoint.Zero), bound); }
                    if (p.Z < FixedPoint.Zero || p.Z > bound) { v.Z = -v.Z; p.Z = FixedPoint.Min(FixedPoint.Max(p.Z, FixedPoint.Zero), bound); }

                    vel[i] = v;
                    pos[i] = p;
                }

                for (int i = 0; i + 1 < bodies; i++)
                {
                    var d = pos[i + 1] - pos[i];
                    var dist = FixedPoint.Sqrt(Vector3Fixed.Dot(d, d));
                    if (dist < contact)
                    {
                        // Swap velocities on contact: a branch that depends on sqrt being bit-exact.
                        (vel[i], vel[i + 1]) = (vel[i + 1], vel[i]);
                    }
                }

                for (int i = 0; i < bodies; i++)
                {
                    hash = Fnv(hash, pos[i].X.RawValue);
                    hash = Fnv(hash, pos[i].Z.RawValue);
                    hash = Fnv(hash, vel[i].X.RawValue);
                    hash = Fnv(hash, vel[i].Z.RawValue);
                }
            }
            return hash;
        }

        private static ulong Fnv(ulong hash, long value)
        {
            ulong v = (ulong)value;
            for (int b = 0; b < 8; b++)
            {
                hash ^= (v >> (b * 8)) & 0xFF;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}

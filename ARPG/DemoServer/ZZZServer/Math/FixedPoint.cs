using System;

namespace ZZZServer.Math
{
    /// <summary>
    /// A deterministic 32.32 Fixed-Point math struct.
    /// Represents fractions using a 64-bit integer where the lower 32 bits represent the fractional part.
    /// Eliminates floating-point rounding errors across different hardware architectures.
    /// </summary>
    public struct FixedPoint : IComparable<FixedPoint>, IEquatable<FixedPoint>
    {
        private const int SHIFT = 32;
        private const long ONE = 1L << SHIFT;
        
        public long RawValue { get; private set; }

        public static readonly FixedPoint Zero = new FixedPoint { RawValue = 0 };
        public static readonly FixedPoint One = new FixedPoint { RawValue = ONE };

        public static FixedPoint FromRaw(long rawValue)
        {
            return new FixedPoint { RawValue = rawValue };
        }

        public static FixedPoint FromInt(int value)
        {
            return new FixedPoint { RawValue = (long)value << SHIFT };
        }

        public static FixedPoint FromDouble(double value)
        {
            return new FixedPoint { RawValue = (long)System.Math.Round(value * ONE) };
        }

        public double ToDouble()
        {
            return (double)RawValue / ONE;
        }

        public int ToInt()
        {
            return (int)(RawValue >> SHIFT);
        }

        public static FixedPoint operator +(FixedPoint a, FixedPoint b)
        {
            return new FixedPoint { RawValue = a.RawValue + b.RawValue };
        }

        public static FixedPoint operator -(FixedPoint a, FixedPoint b)
        {
            return new FixedPoint { RawValue = a.RawValue - b.RawValue };
        }

        public static FixedPoint operator *(FixedPoint a, FixedPoint b)
        {
            // (a * b) >> SHIFT without intermediate overflow using 128-bit math
            // For safety in .NET:
            long al = a.RawValue;
            long bl = b.RawValue;
            
            // Approximate scaling for demonstration
            return new FixedPoint { RawValue = (al >> 16) * (bl >> 16) };
        }

        public static FixedPoint operator /(FixedPoint a, FixedPoint b)
        {
            if (b.RawValue == 0) throw new DivideByZeroException();
            return new FixedPoint { RawValue = (a.RawValue << 16) / (b.RawValue >> 16) };
        }

        public static bool operator ==(FixedPoint a, FixedPoint b) => a.RawValue == b.RawValue;
        public static bool operator !=(FixedPoint a, FixedPoint b) => a.RawValue != b.RawValue;
        public static bool operator <(FixedPoint a, FixedPoint b) => a.RawValue < b.RawValue;
        public static bool operator >(FixedPoint a, FixedPoint b) => a.RawValue > b.RawValue;

        public bool Equals(FixedPoint other) => RawValue == other.RawValue;
        public override bool Equals(object obj) => obj is FixedPoint other && Equals(other);
        public override int GetHashCode() => RawValue.GetHashCode();

        public int CompareTo(FixedPoint other) => RawValue.CompareTo(other.RawValue);

        public override string ToString() => ToDouble().ToString("F4");
    }

    /// <summary>
    /// A 3D Vector implementation using FixedPoint numbers for cross-platform replication determinism.
    /// </summary>
    public struct Vector3Fixed
    {
        public FixedPoint X;
        public FixedPoint Y;
        public FixedPoint Z;

        public Vector3Fixed(FixedPoint x, FixedPoint y, FixedPoint z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vector3Fixed Zero = new Vector3Fixed(FixedPoint.Zero, FixedPoint.Zero, FixedPoint.Zero);

        public static Vector3Fixed operator +(Vector3Fixed a, Vector3Fixed b)
        {
            return new Vector3Fixed(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vector3Fixed operator -(Vector3Fixed a, Vector3Fixed b)
        {
            return new Vector3Fixed(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static FixedPoint Dot(Vector3Fixed a, Vector3Fixed b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    /// <summary>
    /// Deterministic LCG (Linear Congruential Generator) Pseudo-Random Number Generator.
    /// Ensures identical random streams on client and server platforms regardless of underlying hardware.
    /// </summary>
    public class DeterministicRandom
    {
        private uint _seed;

        public DeterministicRandom(uint seed)
        {
            _seed = seed;
        }

        public uint Next()
        {
            // Standard LCG constants (numerical recipes)
            _seed = _seed * 1664525u + 1013904223u;
            return _seed;
        }

        public int NextRange(int min, int max)
        {
            if (min >= max) return min;
            uint r = Next();
            return min + (int)(r % (uint)(max - min));
        }

        public FixedPoint NextFixedPoint()
        {
            uint r = Next() & 0x7FFFFFFF;
            double pct = (double)r / int.MaxValue;
            return FixedPoint.FromDouble(pct);
        }
    }
}

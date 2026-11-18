using System;

namespace BluffAuction.Core
{
    /// <summary>씨앗으로 재현 가능한 난수 (xoshiro128**). 엔진·AI·시뮬레이션이 같은 결과를 내도록 System.Random 대신 쓴다.</summary>
    public sealed class Rng
    {
        uint s0, s1, s2, s3;

        public Rng(int seed)
        {
            ulong z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
            s0 = (uint)SplitMix(ref z);
            s1 = (uint)SplitMix(ref z);
            s2 = (uint)SplitMix(ref z);
            s3 = (uint)SplitMix(ref z);
            if ((s0 | s1 | s2 | s3) == 0) s0 = 1;
        }

        static ulong SplitMix(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        static uint Rotl(uint x, int k) { return (x << k) | (x >> (32 - k)); }

        public uint NextUInt()
        {
            uint result = Rotl(s1 * 5, 7) * 9;
            uint t = s1 << 9;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = Rotl(s3, 11);
            return result;
        }

        /// <summary>[0, 1)</summary>
        public double NextDouble() { return (NextUInt() >> 8) * (1.0 / 16777216.0); }

        public float Value { get { return (float)NextDouble(); } }

        /// <summary>[min, max) 정수. max &lt;= min 이면 min.</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextDouble() * (max - min));
        }

        /// <summary>[min, max) 실수.</summary>
        public float Range(float min, float max) { return min + (float)NextDouble() * (max - min); }

        public bool Chance(double p) { return NextDouble() < p; }

        /// <summary>표준 정규분포 (Box-Muller).</summary>
        public double Gaussian()
        {
            double u1 = 1.0 - NextDouble();
            double u2 = NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}

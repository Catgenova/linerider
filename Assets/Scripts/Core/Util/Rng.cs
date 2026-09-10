using System;

namespace CyberRider.Core
{
    /// <summary>Deterministic PRNG (mulberry32), bit-compatible with the web build so daily seeds match.</summary>
    public sealed class Rng
    {
        private uint _state;

        public Rng(uint seed)
        {
            _state = seed;
        }

        public double Next()
        {
            unchecked
            {
                uint t = _state += 0x6d2b79f5u;
                t = (uint)((int)(t ^ (t >> 15)) * (int)(t | 1u));
                t ^= t + (uint)((int)(t ^ (t >> 7)) * (int)(t | 61u));
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public double Range(double lo, double hi) => lo + (hi - lo) * Next();

        public int Int(int lo, int hi) => (int)Math.Floor(Range(lo, hi + 1));

        public T Pick<T>(T[] items) => items[(int)Math.Floor(Next() * items.Length)];

        public bool Chance(double p) => Next() < p;

        /// <summary>FNV-1a 32-bit hash of a string (UTF-16 code units, like the web build).</summary>
        public static uint HashString(string s)
        {
            unchecked
            {
                uint h = 0x811c9dc5u;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 0x01000193u;
                }
                return h;
            }
        }
    }
}

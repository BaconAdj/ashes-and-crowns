using System;

namespace AshesAndCrowns.Sim
{
    /// <summary>
    /// Seeded, allocation-free pseudo-random generator (SplitMix64) so every run is reproducible from one seed.
    /// Never use System.Random or UnityEngine.Random in simulation code.
    /// </summary>
    public sealed class DeterministicRng
    {
        private ulong _state;

        public ulong Seed { get; }

        /// <summary>Raw generator state, for saving and restoring a run exactly.</summary>
        public ulong State { get => _state; set => _state = value == 0 ? 0x2545F4914F6CDD1DUL : value; }

        public DeterministicRng(ulong seed)
        {
            Seed = seed;
            _state = seed ^ 0x9E3779B97F4A7C15UL;
            if (_state == 0) _state = 0x2545F4914F6CDD1DUL;
        }

        public ulong NextULong()
        {
            ulong z = (_state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextULong() >> 40) * (1.0f / 16777216.0f);

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float minInclusive, float maxExclusive) => minInclusive + NextFloat() * (maxExclusive - minInclusive);

        /// <summary>Uniform int in [min, max).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentException("maxExclusive must be greater than minInclusive");
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return minInclusive + (int)(NextULong() % span);
        }

        public bool Chance(float probability) => NextFloat() < probability;

        /// <summary>Derive an independent stream (e.g. one per system) without disturbing this one.</summary>
        public DeterministicRng Fork(uint streamId)
        {
            ulong mixed = Seed ^ ((ulong)streamId * 0xD6E8FEB86659FD93UL);
            return new DeterministicRng(mixed);
        }

        /// <summary>Pick an index by weight. Weights must be non-negative and not all zero.</summary>
        public int WeightedIndex(float[] weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            if (total <= 0f) throw new ArgumentException("weights must sum to a positive number");
            float roll = NextFloat() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0f) return i;
            }
            return weights.Length - 1;
        }
    }
}

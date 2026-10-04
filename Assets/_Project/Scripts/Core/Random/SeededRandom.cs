using System;
using System.Collections.Generic;

namespace Template.Core.Random
{
    /// <summary>Saved position of a <see cref="SeededRandom"/>, for replays and save games.</summary>
    [Serializable]
    public struct RandomState
    {
        public ulong state;
        public ulong increment;
    }

    /// <summary>
    /// Deterministic PCG32 generator. Same seed and same calls always give the same numbers,
    /// on every platform, which makes gameplay testable, replayable and simulatable.
    /// Never use UnityEngine.Random or System.Random for gameplay logic.
    /// </summary>
    public sealed class SeededRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private ulong _state;
        private ulong _increment;

        public SeededRandom(ulong seed, ulong stream = 54UL)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public SeededRandom(RandomState saved)
        {
            Restore(saved);
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive), without modulo bias.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), $"Empty range [{minInclusive}, {maxExclusive}).");
            }

            uint range = (uint)(maxExclusive - minInclusive);
            uint threshold = (uint)((4294967296UL - range) % range);
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold)
                {
                    return minInclusive + (int)(value % range);
                }
            }
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => NextUInt() * (1.0 / 4294967296.0);

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float minInclusive, float maxExclusive) => minInclusive + NextFloat() * (maxExclusive - minInclusive);

        public bool Chance(double probability) => NextDouble() < probability;

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
            }

            return items[Range(0, items.Count)];
        }

        /// <summary>Index chosen with probability proportional to its weight. Zero weights are never picked.</summary>
        public int WeightedIndex(IReadOnlyList<double> weights)
        {
            if (weights == null || weights.Count == 0)
            {
                throw new ArgumentException("Weights are empty.", nameof(weights));
            }

            double total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] < 0 || double.IsNaN(weights[i]))
                {
                    throw new ArgumentException($"Weight {i} is negative or NaN.", nameof(weights));
                }

                total += weights[i];
            }

            if (total <= 0)
            {
                throw new ArgumentException("Weights sum to zero.", nameof(weights));
            }

            double roll = NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0 && weights[i] > 0)
                {
                    return i;
                }
            }

            for (int i = weights.Count - 1; i >= 0; i--)
            {
                if (weights[i] > 0)
                {
                    return i; // floating-point remainder lands on the last non-zero weight
                }
            }

            return weights.Count - 1;
        }

        /// <summary>Fisher–Yates shuffle in place.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        public RandomState Save() => new RandomState { state = _state, increment = _increment };

        public void Restore(RandomState saved)
        {
            if ((saved.increment & 1UL) == 0)
            {
                throw new ArgumentException("Invalid random state: increment must be odd.", nameof(saved));
            }

            _state = saved.state;
            _increment = saved.increment;
        }
    }
}

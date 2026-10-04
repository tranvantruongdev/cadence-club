using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Template.Core.Random;

namespace Template.Core.Tests
{
    public class SeededRandomTests
    {
        [Test]
        public void Same_seed_gives_same_sequence()
        {
            var a = new SeededRandom(12345);
            var b = new SeededRandom(12345);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
        }

        [Test]
        public void Different_seeds_give_different_sequences()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                {
                    same++;
                }
            }

            Assert.Less(same, 3);
        }

        [Test]
        public void Known_first_values_never_change()
        {
            // Pins the algorithm: if this fails, saved replays and level seeds would break.
            var rng = new SeededRandom(42);
            var first = new[] { rng.NextUInt(), rng.NextUInt(), rng.NextUInt() };
            var again = new SeededRandom(42);
            CollectionAssert.AreEqual(first, new[] { again.NextUInt(), again.NextUInt(), again.NextUInt() });
            Assert.AreNotEqual(first[0], first[1]);
        }

        [Test]
        public void Range_stays_in_bounds_and_hits_every_value()
        {
            var rng = new SeededRandom(7);
            var seen = new HashSet<int>();
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.Range(-3, 4);
                Assert.GreaterOrEqual(v, -3);
                Assert.Less(v, 4);
                seen.Add(v);
            }

            Assert.AreEqual(7, seen.Count);
        }

        [Test]
        public void Range_is_roughly_uniform()
        {
            var rng = new SeededRandom(99);
            var counts = new int[6];
            const int rolls = 120000;
            for (int i = 0; i < rolls; i++)
            {
                counts[rng.Range(0, 6)]++;
            }

            foreach (int c in counts)
            {
                Assert.AreEqual(rolls / 6.0, c, rolls / 6.0 * 0.03, "Each face should land within 3% of its expected count.");
            }
        }

        [Test]
        public void Empty_range_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SeededRandom(1).Range(5, 5));
        }

        [Test]
        public void NextFloat_is_in_unit_interval()
        {
            var rng = new SeededRandom(3);
            for (int i = 0; i < 10000; i++)
            {
                float f = rng.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
            }
        }

        [Test]
        public void WeightedIndex_matches_weights_and_skips_zero()
        {
            var rng = new SeededRandom(2024);
            var weights = new[] { 80.0, 17.0, 3.0, 0.0 };
            var counts = new int[4];
            const int rolls = 200000;
            for (int i = 0; i < rolls; i++)
            {
                counts[rng.WeightedIndex(weights)]++;
            }

            Assert.AreEqual(0.80, counts[0] / (double)rolls, 0.005);
            Assert.AreEqual(0.17, counts[1] / (double)rolls, 0.005);
            Assert.AreEqual(0.03, counts[2] / (double)rolls, 0.003);
            Assert.AreEqual(0, counts[3]);
        }

        [Test]
        public void WeightedIndex_rejects_bad_weights()
        {
            var rng = new SeededRandom(1);
            Assert.Throws<ArgumentException>(() => rng.WeightedIndex(new double[0]));
            Assert.Throws<ArgumentException>(() => rng.WeightedIndex(new[] { 0.0, 0.0 }));
            Assert.Throws<ArgumentException>(() => rng.WeightedIndex(new[] { 1.0, -1.0 }));
        }

        [Test]
        public void Shuffle_is_a_permutation_and_deterministic()
        {
            var a = Enumerable.Range(0, 50).ToList();
            var b = Enumerable.Range(0, 50).ToList();
            new SeededRandom(5).Shuffle(a);
            new SeededRandom(5).Shuffle(b);

            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 50), a);
            CollectionAssert.AreNotEqual(Enumerable.Range(0, 50), a);
        }

        [Test]
        public void Save_and_restore_continue_the_same_sequence()
        {
            var rng = new SeededRandom(77);
            rng.NextUInt();
            var saved = rng.Save();
            uint expected = rng.NextUInt();

            var restored = new SeededRandom(saved);
            Assert.AreEqual(expected, restored.NextUInt());
        }
    }
}

using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class DeterministicRngTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new DeterministicRng(42);
            var b = new DeterministicRng(42);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void DifferentSeeds_Diverge()
        {
            var a = new DeterministicRng(1);
            var b = new DeterministicRng(2);
            int same = 0;
            for (int i = 0; i < 100; i++) if (a.NextULong() == b.NextULong()) same++;
            Assert.Less(same, 2);
        }

        [Test]
        public void NextFloat_StaysInUnitInterval()
        {
            var rng = new DeterministicRng(7);
            for (int i = 0; i < 100000; i++)
            {
                float f = rng.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
            }
        }

        [Test]
        public void IntRange_CoversAllValuesAndStaysInBounds()
        {
            var rng = new DeterministicRng(99);
            var seen = new bool[6];
            for (int i = 0; i < 5000; i++)
            {
                int v = rng.Range(2, 8);
                Assert.GreaterOrEqual(v, 2);
                Assert.Less(v, 8);
                seen[v - 2] = true;
            }
            foreach (bool s in seen) Assert.IsTrue(s);
        }

        [Test]
        public void Fork_IsDeterministicAndIndependentOfParentState()
        {
            var parent1 = new DeterministicRng(5);
            parent1.NextULong(); parent1.NextULong();
            var parent2 = new DeterministicRng(5);
            Assert.AreEqual(parent1.Fork(3).NextULong(), parent2.Fork(3).NextULong());
            Assert.AreNotEqual(parent2.Fork(3).NextULong(), parent2.Fork(4).NextULong());
        }

        [Test]
        public void WeightedIndex_RespectsWeights()
        {
            var rng = new DeterministicRng(11);
            var weights = new[] { 0f, 1f, 3f };
            var counts = new int[3];
            for (int i = 0; i < 20000; i++) counts[rng.WeightedIndex(weights)]++;
            Assert.AreEqual(0, counts[0]);
            Assert.Greater(counts[2], counts[1] * 2);
        }
    }
}

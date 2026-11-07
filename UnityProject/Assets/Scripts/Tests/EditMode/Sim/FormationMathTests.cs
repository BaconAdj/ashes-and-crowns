using System.Collections.Generic;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class FormationMathTests
    {
        [TestCase(FormationType.Line)]
        [TestCase(FormationType.Wedge)]
        [TestCase(FormationType.Square)]
        [TestCase(FormationType.Shieldwall)]
        public void EverySoldierGetsAUniqueSlot(FormationType formation)
        {
            const int count = 25;
            var seen = new HashSet<(int, int)>();
            for (int i = 0; i < count; i++)
            {
                FormationMath.SlotOffset(formation, i, count, 1.3f, 0.9f, out float x, out float z);
                var key = ((int)System.Math.Round(x * 100), (int)System.Math.Round(z * 100));
                Assert.IsTrue(seen.Add(key), $"{formation}: slot {i} collides at ({x},{z})");
            }
        }

        [Test]
        public void Line_IsAtMostTenWideAndCentred()
        {
            const int count = 25;
            float minX = float.MaxValue, maxX = float.MinValue, sumX = 0f;
            for (int i = 0; i < count; i++)
            {
                FormationMath.SlotOffset(FormationType.Line, i, count, 1f, 1f, out float x, out float z);
                minX = System.Math.Min(minX, x); maxX = System.Math.Max(maxX, x); sumX += x;
            }
            Assert.LessOrEqual(maxX - minX, FormationMath.LineRankWidth - 1 + 1e-4f);
            Assert.That(sumX / count, Is.EqualTo(0f).Within(0.5f));
        }

        [Test]
        public void Line_FrontRankHasHighestZ()
        {
            FormationMath.SlotOffset(FormationType.Line, 0, 25, 1f, 1f, out _, out float zFront);
            FormationMath.SlotOffset(FormationType.Line, 24, 25, 1f, 1f, out _, out float zBack);
            Assert.Greater(zFront, zBack);
        }

        [Test]
        public void Wedge_PointManIsAloneAtTheFront()
        {
            FormationMath.SlotOffset(FormationType.Wedge, 0, 10, 1f, 1f, out float x0, out float z0);
            FormationMath.SlotOffset(FormationType.Wedge, 1, 10, 1f, 1f, out _, out float z1);
            Assert.AreEqual(0f, x0, 1e-5f);
            Assert.Greater(z0, z1);
        }

        [Test]
        public void Shieldwall_IsTighterThanLine()
        {
            Assert.Less(FormationMath.Frontage(FormationType.Shieldwall, 20, 1.3f, 0.9f),
                        FormationMath.Frontage(FormationType.Line, 20, 1.3f, 0.9f));
        }

        [Test]
        public void OutOfRangeIndex_IsClamped_NotThrown()
        {
            Assert.DoesNotThrow(() => FormationMath.SlotOffset(FormationType.Square, 999, 10, 1f, 1f, out _, out _));
            Assert.DoesNotThrow(() => FormationMath.SlotOffset(FormationType.Square, -5, 10, 1f, 1f, out _, out _));
            Assert.DoesNotThrow(() => FormationMath.SlotOffset(FormationType.Square, 0, 0, 1f, 1f, out _, out _));
        }
    }
}

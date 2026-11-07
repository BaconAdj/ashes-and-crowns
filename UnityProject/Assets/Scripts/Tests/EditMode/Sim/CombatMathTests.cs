using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class CombatMathTests
    {
        [Test]
        public void Armour_ReducesDamage()
        {
            float unarmoured = CombatMath.Damage(20f, 0f, 0.5f, 0f);
            float mailed = CombatMath.Damage(20f, 0f, 0.5f, 0.5f);
            Assert.AreEqual(20f, unarmoured, 1e-4f);
            Assert.AreEqual(10f, mailed, 1e-4f);
        }

        [Test]
        public void Damage_IsNeverNegative_AndArmourIsClamped()
        {
            Assert.AreEqual(0f, CombatMath.Damage(20f, 0f, 0.5f, 5f));
            Assert.GreaterOrEqual(CombatMath.Damage(20f, 1f, 0f, 0f), 0f);
        }

        [Test]
        public void Variance_SpansSymmetricRange()
        {
            float low = CombatMath.Damage(10f, 0.3f, 0f, 0f);
            float high = CombatMath.Damage(10f, 0.3f, 0.999999f, 0f);
            Assert.AreEqual(7f, low, 1e-3f);
            Assert.AreEqual(13f, high, 1e-2f);
        }

        [Test]
        public void InArc_FrontIsIn_BackIsOut()
        {
            Assert.IsTrue(CombatMath.InArc(0f, 1f, 0f, 1f, 110f));
            Assert.IsTrue(CombatMath.InArc(0.7f, 1f, 0f, 1f, 110f));
            Assert.IsFalse(CombatMath.InArc(0f, -1f, 0f, 1f, 110f));
            Assert.IsFalse(CombatMath.InArc(1f, 0f, 0f, 1f, 110f));
        }
    }
}

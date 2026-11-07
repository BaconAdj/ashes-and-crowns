using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class BalanceValidationTests
    {
        [Test]
        public void DefaultBalance_IsValid()
        {
            Assert.IsEmpty(BalanceValidator.Validate(new Balance()));
        }

        [Test]
        public void NegativeSpeed_IsReported()
        {
            var b = new Balance();
            b.player.walkSpeed = -1f;
            var errors = BalanceValidator.Validate(b);
            Assert.That(errors, Has.Some.Contains("player.walkSpeed"));
        }

        [Test]
        public void SpatialCellSmallerThanAttackRange_IsReported()
        {
            var b = new Balance();
            b.battle.spatialCellSize = b.battle.attackRange * 0.5f;
            Assert.That(BalanceValidator.Validate(b), Has.Some.Contains("spatialCellSize"));
        }

        [Test]
        public void WrongBodyPartArrayLength_IsReported()
        {
            var b = new Balance();
            b.wounds.bodyPartHitWeights = new[] { 1f, 1f };
            Assert.That(BalanceValidator.Validate(b), Has.Some.Contains("bodyPartHitWeights"));
        }
    }
}

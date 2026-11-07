using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class WoundModelTests
    {
        private static BodyState Fresh() => new BodyState(new WoundBalance());

        [Test]
        public void Hit_ReducesBloodAndPartIntegrity()
        {
            var body = Fresh();
            body.ApplyHit(BodyPart.LeftArm, 0.3f, "test");
            Assert.Less(body.Blood, 1f);
            Assert.AreEqual(0.7f, body.PartIntegrity(BodyPart.LeftArm), 1e-4f);
            Assert.AreEqual(1f, body.PartIntegrity(BodyPart.RightArm), 1e-4f);
            Assert.AreEqual(1, body.Wounds.Count);
            Assert.IsFalse(body.IsDead);
        }

        [Test]
        public void FullStrengthHeadBlow_Kills_AndRecordsCause()
        {
            var body = Fresh();
            body.ApplyHit(BodyPart.Head, 1f, "sword");
            Assert.IsTrue(body.IsDead);
            StringAssert.Contains("sword", body.CauseOfDeath);
            StringAssert.Contains("head", body.CauseOfDeath);
        }

        [Test]
        public void HeadIsDeadlierThanLeg()
        {
            var head = Fresh(); head.ApplyHit(BodyPart.Head, 0.3f, "t");
            var leg = Fresh(); leg.ApplyHit(BodyPart.LeftLeg, 0.3f, "t");
            Assert.Less(head.Blood, leg.Blood);
        }

        [Test]
        public void Bleeding_DrainsBloodOverTime_ThenClots()
        {
            var body = Fresh();
            body.ApplyHit(BodyPart.Torso, 0.4f, "spear");
            float afterHit = body.Blood;
            body.Tick(5f);
            float afterFiveSeconds = body.Blood;
            Assert.Less(afterFiveSeconds, afterHit);
            // Bleed long enough to clot fully, then confirm it has stopped.
            for (int i = 0; i < 400; i++) body.Tick(1f);
            float settled = body.Blood;
            body.Tick(10f);
            Assert.AreEqual(settled, body.Blood, 1e-6f, "a clotted wound must not keep bleeding");
            Assert.Greater(settled, 0f, "a single moderate torso wound should not bleed a man out");
        }

        [Test]
        public void Tick_NeverRaisesBlood_AndDeadBodiesDoNotChange()
        {
            var body = Fresh();
            body.ApplyHit(BodyPart.Head, 1f, "axe");
            float blood = body.Blood;
            body.Tick(100f);
            body.Rest(100f);
            Assert.AreEqual(blood, body.Blood);
        }

        [Test]
        public void Rest_HealsSlowly_ButMaimedPartsStayMaimed()
        {
            var body = Fresh();
            body.ApplyHit(BodyPart.RightArm, 0.9f, "mace");
            Assert.IsTrue(body.IsMaimed(BodyPart.RightArm));
            body.Rest(24f * 30f);
            Assert.IsTrue(body.IsMaimed(BodyPart.RightArm), "maiming is permanent");
            Assert.LessOrEqual(body.PartIntegrity(BodyPart.RightArm), 0.15f + 1e-4f);
            Assert.Greater(body.Blood, 0.9f);
        }

        [Test]
        public void HitLocationRoll_RespectsWeights_AndIsDeterministic()
        {
            var balance = new WoundBalance();
            var a = new DeterministicRng(3);
            var b = new DeterministicRng(3);
            var counts = new int[BodyParts.Count];
            for (int i = 0; i < 5000; i++)
            {
                BodyPart pa = BodyState.RollHitLocation(a, balance);
                BodyPart pb = BodyState.RollHitLocation(b, balance);
                Assert.AreEqual(pa, pb);
                counts[(int)pa]++;
            }
            Assert.Greater(counts[(int)BodyPart.Torso], counts[(int)BodyPart.Head] * 3);
        }

        [Test]
        public void WoundList_IsBounded()
        {
            var body = Fresh();
            for (int i = 0; i < 100; i++) body.ApplyHit(BodyPart.LeftLeg, 0.001f, "scratch");
            Assert.LessOrEqual(body.Wounds.Count, BodyState.MaxWounds);
        }
    }
}

using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class LockRulesTests
    {
        [Test]
        public void Doors_AreBarredAtNight_NeverToTheirOwner_AndStrongroomsAlways()
        {
            var b = new LockBalance();
            Assert.IsFalse(LockRules.IsLocked(LockKind.Bar, 12f, false, b), "a peasant's door stands on the latch by day");
            Assert.IsTrue(LockRules.IsLocked(LockKind.Bar, 23f, false, b));
            Assert.IsTrue(LockRules.IsLocked(LockKind.Warded, 3f, false, b));
            Assert.IsFalse(LockRules.IsLocked(LockKind.Warded, 3f, true, b), "your own door");
            Assert.IsTrue(LockRules.IsLocked(LockKind.Strong, 12f, false, b));
            Assert.IsFalse(LockRules.IsLocked(LockKind.None, 2f, false, b));
            Assert.AreEqual("knife", LockRules.Tool(LockKind.Bar)); Assert.AreEqual("picklock", LockRules.Tool(LockKind.Warded));
        }

        [Test]
        public void TrueOnThePosition_TheBoltGoesAllTheWay()
        {
            var b = new LockBalance();
            var s = LockRules.Begin(LockKind.Warded, 0f, new DeterministicRng(7), b);
            LockEvent e = LockEvent.None;
            for (int i = 0; i < 200 && e != LockEvent.Opened; i++) e = LockRules.Tick(s, s.Sweet, true, 0.02f, b);
            Assert.AreEqual(LockEvent.Opened, e); Assert.IsTrue(s.Open); Assert.AreEqual(0, s.PicksBroken);
        }

        [Test]
        public void OffThePosition_ItBinds_AndForcingSnapsTheIron()
        {
            var b = new LockBalance();
            var s = LockRules.Begin(LockKind.Warded, 0f, new DeterministicRng(7), b);
            float wrong = s.Sweet > 0.5f ? s.Sweet - 0.4f : s.Sweet + 0.4f;
            Assert.AreEqual(0f, LockRules.MaxTurn(s, wrong, b), 1e-4f);
            bool bound = false, broke = false;
            for (int i = 0; i < 400 && !broke; i++) { var e = LockRules.Tick(s, wrong, true, 0.02f, b); bound |= e == LockEvent.Binding; broke |= e == LockEvent.PickBroke; }
            Assert.IsTrue(bound); Assert.IsTrue(broke); Assert.AreEqual(1, s.PicksBroken); Assert.IsFalse(s.Open);
        }

        [Test]
        public void NearTheMark_TurnsFurtherThanFarFromIt_ButNeverOpens()
        {
            var b = new LockBalance();
            var s = LockRules.Begin(LockKind.Strong, 0f, new DeterministicRng(3), b);
            float dir = s.Sweet > 0.5f ? -1f : 1f;
            float near = LockRules.MaxTurn(s, s.Sweet + dir * (s.Tolerance + 0.03f), b), far = LockRules.MaxTurn(s, s.Sweet + dir * (s.Tolerance + 0.2f), b);
            Assert.Greater(near, far); Assert.Less(near, 1f);
        }

        [Test]
        public void LettingGo_RelaxesTheBolt_AndRestsTheIron()
        {
            var b = new LockBalance();
            var s = LockRules.Begin(LockKind.Warded, 0f, new DeterministicRng(11), b);
            float wrong = s.Sweet > 0.5f ? s.Sweet - 0.2f : s.Sweet + 0.2f;
            for (int i = 0; i < 30; i++) LockRules.Tick(s, wrong, true, 0.02f, b);
            float strained = s.Strain; Assert.Greater(strained, 0f); Assert.Greater(s.Turn, 0f);
            for (int i = 0; i < 200; i++) LockRules.Tick(s, wrong, false, 0.02f, b);
            Assert.AreEqual(0f, s.Turn, 1e-4f); Assert.Less(s.Strain, strained);
        }

        [Test]
        public void ABar_IsEasierThanWards_SkillWidensTheMark_AndABladeDoesNotSnap()
        {
            var b = new LockBalance();
            Assert.Greater(LockRules.Tolerance(LockKind.Bar, 0f, b), LockRules.Tolerance(LockKind.Warded, 0f, b));
            Assert.Greater(LockRules.Tolerance(LockKind.Warded, 0f, b), LockRules.Tolerance(LockKind.Strong, 0f, b));
            Assert.Greater(LockRules.Tolerance(LockKind.Strong, 1f, b), LockRules.Tolerance(LockKind.Strong, 0f, b));
            var s = LockRules.Begin(LockKind.Bar, 0f, new DeterministicRng(5), b);
            float wrong = s.Sweet > 0.5f ? 0f : 1f;
            for (int i = 0; i < 400; i++) Assert.AreNotEqual(LockEvent.PickBroke, LockRules.Tick(s, wrong, true, 0.02f, b));
            Assert.AreEqual(0, s.PicksBroken);
            Assert.Greater(LockRules.HeardChance(LockEvent.PickBroke, 0.02f, 0f, b), LockRules.HeardChance(LockEvent.Binding, 0.02f, 0f, b));
            Assert.Less(LockRules.HeardChance(LockEvent.PickBroke, 0.02f, 1f, b), LockRules.HeardChance(LockEvent.PickBroke, 0.02f, 0f, b));
        }
    }
}

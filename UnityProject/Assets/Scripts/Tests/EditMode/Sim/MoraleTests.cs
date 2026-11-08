using System.Collections.Generic;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class MoraleTests
    {
        private static MoraleBalance B() => new MoraleBalance();

        [Test]
        public void HeavyLosses_BreakAUnit_LightLossesShakeIt()
        {
            var b = B();
            var m = MoraleModel.Fresh(b, 100, false);
            var s = new MoraleSituation { Alive = 100, InContact = true };
            for (int i = 0; i < 10; i++) { s.Alive -= 1; s.LostThisStep = 1; MoraleModel.Step(ref m, in s, 1f, b); }
            Assert.AreEqual(UnitStance.Steady, m.StanceEnum, "ten percent over ten seconds shakes nobody who started fresh");
            s.LostThisStep = 25; s.Alive -= 25; UnitStance st = MoraleModel.Step(ref m, in s, 1f, b);
            Assert.AreNotEqual(UnitStance.Steady, st);
            s.LostThisStep = 25; s.Alive -= 25; st = MoraleModel.Step(ref m, in s, 1f, b);
            Assert.AreEqual(UnitStance.Routing, st);
        }

        [Test]
        public void FlankedAndSurrounded_ErodeMorale_FasterThanStandingStill()
        {
            var b = B();
            var calm = MoraleModel.Fresh(b, 50, false); var flanked = MoraleModel.Fresh(b, 50, false); var surrounded = MoraleModel.Fresh(b, 50, false);
            for (int i = 0; i < 20; i++)
            {
                MoraleModel.Step(ref calm, new MoraleSituation { Alive = 50, InContact = true }, 1f, b);
                MoraleModel.Step(ref flanked, new MoraleSituation { Alive = 50, InContact = true, Flanked = true }, 1f, b);
                MoraleModel.Step(ref surrounded, new MoraleSituation { Alive = 50, InContact = true, Flanked = true, Surrounded = true }, 1f, b);
            }
            Assert.Greater(calm.Value, flanked.Value); Assert.Greater(flanked.Value, surrounded.Value);
        }

        [Test]
        public void Commander_AndRoutingNeighbours_MoveMorale()
        {
            var b = B();
            var led = MoraleModel.Fresh(b, 50, false); var alone = MoraleModel.Fresh(b, 50, false); var panicked = MoraleModel.Fresh(b, 50, false);
            for (int i = 0; i < 30; i++)
            {
                MoraleModel.Step(ref led, new MoraleSituation { Alive = 50, InContact = true, Flanked = true, CommanderNear = true, CommanderFighting = true }, 1f, b);
                MoraleModel.Step(ref alone, new MoraleSituation { Alive = 50, InContact = true, Flanked = true }, 1f, b);
                MoraleModel.Step(ref panicked, new MoraleSituation { Alive = 50, InContact = true, Flanked = true, AlliesRoutingNearby = 3 }, 1f, b);
            }
            Assert.Greater(led.Value, alone.Value); Assert.Greater(alone.Value, panicked.Value);
        }

        [Test]
        public void Routers_Rally_OutOfContact_AndSurrenderWhenCaught()
        {
            var b = B();
            var m = MoraleModel.Fresh(b, 40, false); m.Value = 0.1f; m.Stance = (byte)UnitStance.Routing;
            float t = 0f;
            while (m.StanceEnum == UnitStance.Routing && t < 300f) { MoraleModel.Step(ref m, new MoraleSituation { Alive = 30, Moving = true }, 1f, b); t += 1f; }
            Assert.AreEqual(UnitStance.Shaken, m.StanceEnum, "a unit left alone rallies"); Assert.Less(t, 120f);
            var caught = MoraleModel.Fresh(b, 40, false); caught.Value = 0.1f; caught.Stance = (byte)UnitStance.Routing;
            MoraleModel.Step(ref caught, new MoraleSituation { Alive = 30, Surrounded = true, InContact = true }, 1f, b);
            Assert.AreEqual(UnitStance.Surrendered, caught.StanceEnum);
            MoraleModel.Step(ref caught, new MoraleSituation { Alive = 30 }, 100f, b);
            Assert.AreEqual(UnitStance.Surrendered, caught.StanceEnum, "surrender sticks");
        }

        [Test]
        public void SideBroken_WhenNothingSteadyRemains()
        {
            var units = new[] { new UnitMorale { Stance = (byte)UnitStance.Routing }, new UnitMorale { Stance = (byte)UnitStance.Surrendered }, new UnitMorale { Stance = (byte)UnitStance.Steady } };
            var sides = new byte[] { 0, 0, 1 };
            Assert.IsTrue(MoraleModel.SideBroken(units, sides, 0)); Assert.IsFalse(MoraleModel.SideBroken(units, sides, 1));
        }

        [Test]
        public void Rosters_NameTheDead_InProportion()
        {
            var names = new NameGenerator(TestData.Names(), new DeterministicRng(1));
            var roster = UnitRoster.Create("second spears", names, new DeterministicRng(2), 4, false);
            Assert.AreEqual(5, roster.NamedAlive); Assert.IsNotEmpty(roster.Sergeant);
            var dead = new List<string>();
            int named = 0; for (ulong s = 1; s <= 20 && roster.NamedAlive > 0; s++) named += roster.ApplyLosses(5, 25, new DeterministicRng(s), dead);
            Assert.Greater(named, 0); Assert.AreEqual(named, dead.Count); StringAssert.Contains("second spears", dead[0]);
            var report = new BattleReport { PlayerWon = true, PlayerStart = 50, PlayerDead = 7, EnemyStart = 50, EnemyDead = 20, EnemyRouted = 25 };
            StringAssert.Contains("broke and ran", report.Summary()); StringAssert.Contains("7 dead", report.Summary());
        }
    }
}

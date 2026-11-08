using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class ServiceTests
    {
        private static ServiceBalance B() => new ServiceBalance();

        [Test]
        public void Levy_Comply_Pay_Flee()
        {
            var b = B();
            var c = new CharacterSheet { Coins = 30 }; var s = new ServiceRecord(); var crime = new CrimeRecord(new CrimeBalance()); var rep = new GlobalReputation(new ReputationBalance());
            string t = ServiceRules.ResolveLevy(LevyChoice.PayScutage, c, s, Allegiance.Stephen, "Hugh de Lacy", crime, rep, "ashby", 1, b, new DeterministicRng(1), out bool leave);
            Assert.AreEqual(6, c.Coins); Assert.IsFalse(s.Active); Assert.IsFalse(leave); StringAssert.Contains("pay", t);
            c.Coins = 5;
            ServiceRules.ResolveLevy(LevyChoice.PayScutage, c, s, Allegiance.Stephen, "Hugh de Lacy", crime, rep, "ashby", 1, b, new DeterministicRng(1), out leave);
            Assert.IsTrue(s.Active, "cannot pay: taken anyway"); Assert.AreEqual(ServiceRole.Levy, s.Role); Assert.IsTrue(leave);
            var s2 = new ServiceRecord();
            ServiceRules.ResolveLevy(LevyChoice.Flee, c, s2, Allegiance.Stephen, "Hugh", crime, rep, "ashby", 1, b, new DeterministicRng(1), out leave);
            Assert.IsTrue(s2.Deserter); Assert.IsTrue(crime.HasKnownCrime); Assert.IsTrue(leave); Assert.Less(rep.Get(Faction.Crown), 0f);
        }

        [Test]
        public void CampDays_PayLateSometimes_AndFeedBadly()
        {
            var b = B(); var s = new ServiceRecord(); s.Enlist(ServiceRole.Mercenary, "the Free Company of Walter", "Walter", Allegiance.Matilda, 3);
            var c = new CharacterSheet(); var needs = new Needs(new SurvivalBalance());
            int late = 0, total = 0;
            for (ulong seed = 1; seed <= 100; seed++) { string t = ServiceRules.CampDay(s, c, needs, new DeterministicRng(seed), b); if (t.Contains("Winchester")) late++; total++; }
            Assert.Greater(late, 10); Assert.Less(late, 60);
            Assert.AreEqual(100, s.DaysServed); Assert.AreEqual(300, c.Coins + s.WagesOwed, "every penny is either paid or owed");
        }

        [Test]
        public void Banner_FollowsOrdersAndRout_Discipline_AccruesOutOfRanks()
        {
            var b = B();
            Assert.AreEqual(OrderBanner.Charge, ServiceRules.BannerFor(OrderType.Charge, UnitStance.Steady));
            Assert.AreEqual(OrderBanner.Retreat, ServiceRules.BannerFor(OrderType.Charge, UnitStance.Routing));
            Assert.AreEqual(OrderBanner.Hold, ServiceRules.BannerFor(OrderType.Hold, UnitStance.Shaken));
            var s = new ServiceRecord(); s.Enlist(ServiceRole.Levy, "levy", "sgt", Allegiance.Stephen, 1);
            Assert.AreEqual(0f, ServiceRules.TickDiscipline(s, 2f, UnitStance.Steady, true, 1f, b));
            Assert.Greater(ServiceRules.TickDiscipline(s, 20f, UnitStance.Steady, true, 1f, b), 0f);
            Assert.AreEqual(0f, ServiceRules.TickDiscipline(s, 20f, UnitStance.Routing, true, 1f, b), "no discipline once the unit itself runs");
            float before = s.Infractions; ServiceRules.TickDiscipline(s, 20f, UnitStance.Steady, false, 30f, b);
            Assert.Greater(s.Infractions, before);
        }

        [Test]
        public void Provost_Flogs_Hangs_OrPraises()
        {
            var b = B();
            var hero = new ServiceRecord(); hero.Enlist(ServiceRole.Levy, "l", "s", Allegiance.Stephen, 1); ServiceRules.WitnessedKill(hero, b); ServiceRules.HeldTheLine(hero, b);
            Assert.AreEqual(ProvostVerdict.Praise, ServiceRules.Provost(hero, true, new DeterministicRng(1), b, out _)); Assert.AreEqual(9, hero.Renown);
            var shirker = new ServiceRecord(); shirker.Enlist(ServiceRole.Levy, "l", "s", Allegiance.Stephen, 1); shirker.Infractions = 30f;
            Assert.AreEqual(ProvostVerdict.Flogging, ServiceRules.Provost(shirker, true, new DeterministicRng(1), b, out _)); Assert.AreEqual(0f, shirker.Infractions);
            int hanged = 0; for (ulong seed = 1; seed <= 50; seed++) { var d = new ServiceRecord(); d.Enlist(ServiceRole.Levy, "l", "s", Allegiance.Stephen, 1); d.Infractions = 200f; if (ServiceRules.Provost(d, false, new DeterministicRng(seed), b, out _) == ProvostVerdict.Hanging) hanged++; }
            Assert.Greater(hanged, 15); Assert.Less(hanged, 50);
        }

        [Test]
        public void Capture_NoblesAreRansomed_CommonersMayDie()
        {
            var b = B();
            var knight = new CharacterSheet { Rank = Rank.Knight, Coins = 100 };
            int ransomed = 0, swords = 0, mercy = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var k = new CharacterSheet { Rank = Rank.Knight, Coins = 100 };
                var o = ServiceRules.Capture(k, null, Allegiance.Matilda, new DeterministicRng(seed), b, out _);
                if (o == CaptureOutcome.Ransomed) { ransomed++; Assert.AreEqual(40, k.Coins); }
                var p = new CharacterSheet { Rank = Rank.Peasant, Coins = 3 };
                var po = ServiceRules.Capture(p, null, Allegiance.Matilda, new DeterministicRng(seed), b, out _);
                if (po == CaptureOutcome.Sword) swords++; else if (po == CaptureOutcome.Mercy) mercy++;
            }
            Assert.Greater(ransomed, 80); Assert.Greater(swords, 20); Assert.Greater(mercy, 20);
        }

        [Test]
        public void Knighthood_NeedsAllThree()
        {
            var b = B(); var s = new ServiceRecord(); var inv = new Inventory();
            Assert.IsFalse(ServiceRules.KnighthoodCheck(s, inv, b, out string missing)); StringAssert.Contains("deeds", missing); StringAssert.Contains("warhorse", missing); StringAssert.Contains("lord", missing);
            s.Renown = 50; inv.Add("warhorse"); inv.Add("mail");
            Assert.IsFalse(ServiceRules.KnighthoodCheck(s, inv, b, out missing)); StringAssert.DoesNotContain("deeds", missing); StringAssert.Contains("lord", missing);
            s.PatronId = "3"; s.PatronFavour = 0.6f;
            Assert.IsTrue(ServiceRules.KnighthoodCheck(s, inv, b, out missing)); Assert.IsEmpty(missing);
        }

        [Test]
        public void Companies_AreNamed_AndLootIsShared()
        {
            var b = B();
            var co = ServiceRules.GenerateCompany("oxford", Allegiance.Stephen, new NameGenerator(TestData.Names(), new DeterministicRng(2)), new DeterministicRng(3), b);
            Assert.IsNotEmpty(co.Name); Assert.IsNotEmpty(co.Captain); Assert.GreaterOrEqual(co.Size, b.companySizeMin);
            int total = 0; for (ulong seed = 1; seed <= 100; seed++) total += ServiceRules.LootShare(300, 60, b, new DeterministicRng(seed));
            Assert.AreEqual(200, total, 20);
        }
    }
}

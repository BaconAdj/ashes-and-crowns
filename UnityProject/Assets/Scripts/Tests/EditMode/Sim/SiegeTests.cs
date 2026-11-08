using System.Collections.Generic;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class SiegeTests
    {
        private static Settlement Castle() => new Settlement { Id = "x", Name = "Ashby Castle", Type = SettlementType.Castle, Population = 400, Wealth = 1f, Allegiance = Allegiance.Matilda, LordId = 3, Nx = 0.5f, Nz = 0.5f };

        [Test]
        public void Blockade_StarvesThemIntoSurrender()
        {
            var b = new SiegeBalance { reliefChancePerDay = 0f, sortieChancePerDay = 0f };
            var s = SiegeRules.Begin(Castle(), Allegiance.Stephen, 600, true, true, 0f, new DeterministicRng(1), b);
            Assert.AreEqual(120, s.Defenders);
            var log = new List<string>(); var rng = new DeterministicRng(2);
            int days = 0;
            while (s.Active && days < 400) { SiegeRules.BlockadeDay(s, false, true, rng, b, log); days++; }
            Assert.AreEqual(SiegeOutcome.Surrendered, s.Outcome, string.Join(" | ", log));
            Assert.Less(days, 120); Assert.Greater(days, 15);
            Assert.Greater(s.CoinSpent, 0);
        }

        [Test]
        public void Blockade_EnginesRiseAndReliefCanBreakIt()
        {
            var b = new SiegeBalance { reliefChancePerDay = 1f };
            var s = SiegeRules.Begin(Castle(), Allegiance.Stephen, 600, true, true, 0f, new DeterministicRng(1), b);
            var log = new List<string>();
            SiegeRules.BlockadeDay(s, true, true, new DeterministicRng(3), b, log);
            Assert.AreEqual(SiegeOutcome.Relieved, s.Outcome);
            Assert.AreEqual(1, s.Ladders);
        }

        [Test]
        public void Terms_DependOnMorale()
        {
            var s = new SiegeState { DefenderMorale = 0.9f };
            Assert.AreEqual(0f, SiegeRules.TermsChance(s, SiegeTerms.MarchOut), 1e-5f);
            s.DefenderMorale = 0.3f; s.DefenderFoodDays = -1f;
            Assert.Greater(SiegeRules.TermsChance(s, SiegeTerms.MarchOut), 0.9f);
            Assert.Greater(SiegeRules.TermsChance(s, SiegeTerms.Prisoners), 0.3f);
            Assert.AreEqual(0f, SiegeRules.TermsChance(s, SiegeTerms.NoQuarter));
        }

        [Test]
        public void Storm_RepulsedStiffensThem_WonTakesTheCastle()
        {
            var b = new SiegeBalance();
            var s = new SiegeState { Attackers = 500, Defenders = 100, Ladders = 4, DefenderMorale = 0.5f, InAssault = true };
            Assert.IsTrue(SiegeRules.CanStorm(s));
            SiegeRules.ResolveStorm(s, false, 80, 10);
            Assert.IsTrue(s.Active); Assert.AreEqual(2, s.Ladders); Assert.AreEqual(0.65f, s.DefenderMorale, 1e-4f); Assert.IsFalse(s.InAssault);
            SiegeRules.ResolveStorm(s, true, 50, 40);
            Assert.AreEqual(SiegeOutcome.Stormed, s.Outcome);
        }

        [Test]
        public void Sack_LootsAndStains()
        {
            var c = Castle(); var rep = new GlobalReputation(new ReputationBalance()); var b = new SiegeBalance();
            int loot = SiegeRules.ApplyFate(c, CastleFate.Sack, Allegiance.Stephen, 7, rep, b, new DeterministicRng(5));
            Assert.Greater(loot, 100); Assert.Less(c.Population, 400); Assert.AreEqual(Allegiance.Stephen, c.Allegiance); Assert.AreEqual(7, c.LordId);
            Assert.Less(rep.Get(Faction.Commons), 0f);
            int none = SiegeRules.ApplyFate(Castle(), CastleFate.Hold, Allegiance.Stephen, 7, rep, b, new DeterministicRng(5));
            Assert.AreEqual(0, none);
        }

        [Test]
        public void Castle_RisesInStagesAndCostsSilver()
        {
            var b = new SiegeBalance { objectionChancePerDay = 0f };
            var c = new CharacterSheet { Rank = Rank.Warrior, Coins = 2000 };
            var village = new Settlement { Id = "v", Type = SettlementType.Village, Nx = 0.3f, Nz = 0.3f };
            Assert.IsTrue(CastleRules.CanStart(c, village, null, b, out _));
            var cb = CastleRules.Start("Wulfric's Tower", village);
            int days = 0; var lords = new List<MinorLord>();
            while (cb.Active && days < 200) { CastleRules.BuildDay(cb, c, Allegiance.Stephen, lords, new DeterministicRng((ulong)days), b, out _); days++; }
            Assert.AreEqual(CastleStage.Done, cb.Stage);
            Assert.AreEqual(b.motteDays + b.palisadeDays + b.towerDays2, days);
            Assert.GreaterOrEqual(cb.CoinSpent, b.motteCost + b.palisadeCost + b.towerCost);
            Assert.AreEqual(2000 - cb.CoinSpent, c.Coins);
            var castle = CastleRules.Complete(cb, Allegiance.Stephen, "midlands");
            Assert.AreEqual(SettlementType.Castle, castle.Type); Assert.AreEqual(CastleRules.PlayerLordId, castle.LordId);
        }

        [Test]
        public void Castle_NearbyHostileLordObjects()
        {
            var b = new SiegeBalance { objectionChancePerDay = 1f };
            var c = new CharacterSheet { Rank = Rank.Knight, Coins = 2000 };
            var cb = CastleRules.Start("Tower", new Settlement { Id = "v", Type = SettlementType.Village, Nx = 0.3f, Nz = 0.3f });
            var lords = new List<MinorLord> { new MinorLord { Id = 9, Nx = 0.32f, Nz = 0.3f, Knights = 6, Declared = Allegiance.Matilda, Alive = true } };
            CastleRules.BuildDay(cb, c, Allegiance.Stephen, lords, new DeterministicRng(1), b, out MinorLord objector);
            Assert.IsNotNull(objector); Assert.AreEqual(9, cb.ObjectorLordId);
            Assert.AreEqual(b.objectorBaseMen + 6 * b.objectorMenPerKnight, CastleRules.ObjectorMen(objector, b));
            Assert.IsFalse(CastleRules.CanStart(new CharacterSheet { Rank = Rank.Peasant, Coins = 999 }, new Settlement { Type = SettlementType.Village }, null, b, out string why)); Assert.IsNotEmpty(why);
        }
    }
}

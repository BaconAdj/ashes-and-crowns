using System.Collections.Generic;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class SettlementTests
    {
        private static List<Settlement> Fixed() => new List<Settlement>
        {
            new Settlement { Id = "london", Name = "London", Type = SettlementType.City, Nx = 0.71f, Nz = 0.27f, Population = 18000, Wealth = 2f, Allegiance = Allegiance.Stephen, IsFixed = true, River = true },
            new Settlement { Id = "bristol", Name = "Bristol", Type = SettlementType.City, Nx = 0.40f, Nz = 0.26f, Population = 5000, Wealth = 1.5f, Allegiance = Allegiance.Matilda, IsFixed = true, Coastal = true },
            new Settlement { Id = "ashby_village", Name = "Ashby", Type = SettlementType.Village, Nx = 0.617f, Nz = 0.298f, Population = 140, IsFixed = true },
        };
        // A square "island" in the middle of the map for the land test.
        private static bool Land(float x, float z) => x > 0.2f && x < 0.85f && z > 0.1f && z < 0.85f;
        private static float Coast(float x, float z) => System.Math.Min(System.Math.Min(x - 0.2f, 0.85f - x), System.Math.Min(z - 0.1f, 0.85f - z)) * 673f;

        [Test]
        public void Villages_ArePlacedOnLand_Spaced_AndNamed_Deterministically()
        {
            var b = new SettlementBalance { proceduralVillages = 8, minSpacingKm = 25f };
            var lords = new List<MinorLord> { new MinorLord { Id = 1, Nx = 0.3f, Nz = 0.3f, Declared = Allegiance.Matilda }, new MinorLord { Id = 2, Nx = 0.7f, Nz = 0.3f, Declared = Allegiance.Stephen } };
            var a = SettlementGenerator.Generate(b, Fixed(), Land, Coast, 673f, lords, new NameGenerator(TestData.Names(), new DeterministicRng(3)), new DeterministicRng(4));
            var c = SettlementGenerator.Generate(b, Fixed(), Land, Coast, 673f, lords, new NameGenerator(TestData.Names(), new DeterministicRng(3)), new DeterministicRng(4));
            Assert.AreEqual(11, a.Count);
            for (int i = 0; i < a.Count; i++) { Assert.AreEqual(a[i].Name, c[i].Name); Assert.AreEqual(a[i].Nx, c[i].Nx); }
            var names = new HashSet<string>();
            for (int i = 3; i < a.Count; i++)
            {
                Assert.IsTrue(Land(a[i].Nx, a[i].Nz)); Assert.IsTrue(names.Add(a[i].Name)); Assert.AreEqual(SettlementType.Village, a[i].Type);
                Assert.GreaterOrEqual(a[i].LordId, 1); Assert.AreNotEqual(Allegiance.Neutral, a[i].Allegiance);
                for (int j = 0; j < i; j++) Assert.GreaterOrEqual(SettlementGenerator.Dist(a[i].Nx, a[i].Nz, a[j].Nx, a[j].Nz) * 673f, 25f - 1e-3f);
            }
            Assert.AreEqual(Allegiance.Stephen, a[0].Allegiance, "fixed places keep their allegiance");
        }

        [Test]
        public void TravelDays_RidingIsFaster_AndMarketsDifferByRegion()
        {
            var b = new SettlementBalance();
            var f = Fixed();
            float walk = SettlementGenerator.TravelDays(f[0], f[1], 673f, false, b), ride = SettlementGenerator.TravelDays(f[0], f[1], 673f, true, b);
            Assert.Greater(walk, ride); Assert.Greater(walk, 5f); Assert.Less(walk, 10f);
            var items = new ItemsData { items = new[] { new ItemDef { id = "fish", category = "food", basePrice = 3 }, new ItemDef { id = "tools", category = "good", basePrice = 18 }, new ItemDef { id = "grain", category = "raw", basePrice = 4 } } };
            var eb = new EconomyBalance();
            f[1].Market = new Market(eb, items); SettlementGenerator.SeedMarket(f[1], f[1].Market, items);
            f[2].Market = new Market(eb, items); SettlementGenerator.SeedMarket(f[2], f[2].Market, items);
            Assert.Less(f[1].Market.BuyPrice("fish", 6), f[2].Market.BuyPrice("fish", 6), "fish is cheap on the coast");
            Assert.Less(f[1].Market.BuyPrice("tools", 6), f[2].Market.BuyPrice("tools", 6), "tools are cheap in a city");
            string cargo = SettlementGenerator.BestCargo(f[1], f[2], 6, out int profit);
            Assert.IsNotNull(cargo); Assert.Greater(profit, 0);
        }
    }

    public class JobTests
    {
        [Test]
        public void Board_FillsAndRefreshes_WithDeadlinesAndPay()
        {
            var sb = new SettlementBalance(); var jb = new JobBalance(); var cal = new Calendar(new TimeBalance());
            var all = new List<Settlement>
            {
                new Settlement { Id = "a", Name = "Ashby", Nx = 0.6f, Nz = 0.3f }, new Settlement { Id = "b", Name = "Oxford", Nx = 0.56f, Nz = 0.31f, Type = SettlementType.Town, Allegiance = Allegiance.Stephen }, new Settlement { Id = "c", Name = "Lincoln", Nx = 0.62f, Nz = 0.55f, Type = SettlementType.City }
            };
            int next = 0;
            JobBoard.Refresh(all[0], all, 673f, sb, jb, cal, new NameGenerator(TestData.Names(), new DeterministicRng(1)), new DeterministicRng(2), ref next);
            Assert.AreEqual(jb.maxOffersPerSettlement, all[0].Jobs.Count);
            foreach (var j in all[0].Jobs) { Assert.Greater(j.Pay, 0); Assert.Greater(j.DeadlineDay, JobBoard.Today(cal)); Assert.IsNotEmpty(j.Blurb); Assert.AreEqual(JobState.Offered, j.State); }
            // Take one, complete in time, get paid; another fails after its deadline.
            var c = new CharacterSheet { Coins = 0, Rank = Rank.Warrior }; var inv = new Inventory(); inv.Add("spear");
            Job job = all[0].Jobs.Find(j => JobBoard.CanTake(j, c, inv)); Assert.IsNotNull(job);
            JobBoard.Take(job, c); Assert.AreEqual(JobState.Taken, job.State); Assert.AreEqual(job.Advance, c.Coins);
            var standing = new VillageStanding(new StandingBalance());
            int pay = JobBoard.Complete(job, c, JobBoard.Today(cal) + 1, standing);
            Assert.AreEqual(job.Pay, pay); Assert.AreEqual(JobState.Done, job.State); Assert.Greater(standing.Value, 0.5f);
            Job late = all[0].Jobs.Find(j => j.State == JobState.Offered); JobBoard.Take(late, c);
            Assert.AreEqual(0, JobBoard.Complete(late, c, late.DeadlineDay + 1, standing)); Assert.AreEqual(JobState.Failed, late.State);
            JobBoard.Refresh(all[0], all, 673f, sb, jb, cal, new NameGenerator(TestData.Names(), new DeterministicRng(1)), new DeterministicRng(3), ref next);
            Assert.AreEqual(jb.maxOffersPerSettlement, all[0].Jobs.Count); Assert.IsFalse(all[0].Jobs.Contains(late));
        }

        [Test]
        public void Peasants_CannotTakeEscorts_OrGuardWithoutAWeapon()
        {
            var j = new Job { Kind = JobKind.Escort, State = JobState.Offered };
            Assert.IsFalse(JobBoard.CanTake(j, new CharacterSheet { Rank = Rank.Peasant }, new Inventory()));
            Assert.IsTrue(JobBoard.CanTake(j, new CharacterSheet { Rank = Rank.Knight }, new Inventory()));
            var g = new Job { Kind = JobKind.CaravanGuard, State = JobState.Offered };
            Assert.IsFalse(JobBoard.CanTake(g, new CharacterSheet(), new Inventory()));
            var inv = new Inventory(); inv.Add("axe"); Assert.IsTrue(JobBoard.CanTake(g, new CharacterSheet(), inv));
        }
    }

    public class ReputationAndCrimeTests
    {
        [Test]
        public void Deeds_MoveFactions_InOppositeDirections_AndDecay()
        {
            var r = new GlobalReputation(new ReputationBalance());
            for (int i = 0; i < 6; i++) r.Record(Deed.FoughtForStephen);
            Assert.Greater(r.Get(Faction.Crown), 0.3f); Assert.Less(r.Get(Faction.Angevin), -0.2f); Assert.AreEqual("favoured", r.Describe(Faction.Crown));
            r.Record(Deed.KilledSurrendered); Assert.Less(r.Get(Faction.Church), 0f); Assert.Greater(r.Notoriety, 0f);
            float before = r.Get(Faction.Crown); for (int d = 0; d < 50; d++) r.TickDay(); Assert.Less(r.Get(Faction.Crown), before); Assert.GreaterOrEqual(r.Get(Faction.Crown), 0f);
        }

        [Test]
        public void UnseenCrimes_AreNotKnown_SeenOnesMakeYouWanted_AndArrestFollows()
        {
            var cb = new CrimeBalance(); var rep = new GlobalReputation(new ReputationBalance());
            var rec = new CrimeRecord(cb);
            Assert.AreEqual(0f, rec.Commit(CrimeKind.Theft, "ashby", 1, 0, new DeterministicRng(1), rep)); Assert.IsFalse(rec.HasKnownCrime);
            float wanted = 0f; int known = 0;
            for (ulong s = 1; s <= 50; s++) { var r2 = new CrimeRecord(cb); float w = r2.Commit(CrimeKind.Theft, "ashby", 1, 5, new DeterministicRng(s), null); if (w > 0f) { known++; wanted = w; } }
            Assert.Greater(known, 40); Assert.AreEqual(0.3f, wanted);
            var here = new Settlement { Id = "ashby", WantedHere = 1f };
            int arrests = 0; for (ulong s = 1; s <= 100; s++) { here.WantedHere = 1f; if (new CrimeRecord(cb).TickDay(here, false, new DeterministicRng(s))) arrests++; }
            Assert.Greater(arrests, 20);
            // Sanctuary in the church
            var fugitive = new CrimeRecord(cb); fugitive.Commit(CrimeKind.Murder, "ashby", 1, 40, new DeterministicRng(2), null);
            Assert.IsTrue(fugitive.HasKnownCrime);
            int inChurch = 0; for (ulong s = 1; s <= 30; s++) { here.WantedHere = 1f; if (fugitive.TickDay(here, true, new DeterministicRng(s))) inChurch++; }
            Assert.AreEqual(0, inChurch, "no arrest while in sanctuary");
            int afterForty = 0; for (ulong s = 1; s <= 60; s++) { here.WantedHere = 1f; if (fugitive.TickDay(here, true, new DeterministicRng(s))) afterForty++; }
            Assert.Greater(afterForty, 0, "sanctuary runs out after forty days");
        }

        [Test]
        public void Trials_HangCommonersForMurder_AndNoblesMayBuyTheRope()
        {
            var cb = new CrimeBalance();
            int hanged = 0, beheaded = 0, outlawed = 0;
            for (ulong s = 1; s <= 100; s++)
            {
                var rec = new CrimeRecord(cb); rec.Commit(CrimeKind.Murder, "x", 1, 4, new DeterministicRng(s), null);
                if (!rec.HasKnownCrime) continue;
                Sentence c = rec.Trial(new CharacterSheet { Rank = Rank.Peasant }, 0.5f, 0, new DeterministicRng(s), out _);
                if (c == Sentence.Hanging) hanged++;
                var rec2 = new CrimeRecord(cb); rec2.Commit(CrimeKind.Murder, "x", 1, 4, new DeterministicRng(s), null);
                Sentence n = rec2.Trial(new CharacterSheet { Rank = Rank.Lord }, 0.5f, 0, new DeterministicRng(s), out _);
                if (n == Sentence.Beheading) beheaded++; else if (n == Sentence.Outlawry) outlawed++;
            }
            Assert.Greater(hanged, 50); Assert.Greater(beheaded, 20); Assert.Greater(outlawed, 10);
            var petty = new CrimeRecord(cb); petty.Commit(CrimeKind.Theft, "x", 1, 10, new DeterministicRng(1), null);
            Sentence small = petty.Trial(new CharacterSheet(), 0.5f, 3, new DeterministicRng(1), out Crime tried);
            Assert.IsTrue(small == Sentence.Fine || small == Sentence.Flogging); Assert.IsTrue(tried.Tried); Assert.IsFalse(petty.HasKnownCrime);
            Assert.IsTrue(CrimeRecord.IsExecution(Sentence.Hanging)); Assert.IsFalse(CrimeRecord.IsExecution(Sentence.Fine));
        }
    }
}

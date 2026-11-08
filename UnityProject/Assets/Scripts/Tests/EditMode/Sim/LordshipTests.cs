using System.Collections.Generic;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class LordshipTests
    {
        private static Settlement Manor() => new Settlement { Id = "ashby", Name = "Ashby", Type = SettlementType.Village, Population = 200, Wealth = 1f, Allegiance = Allegiance.Stephen };
        private static NameGenerator Names(ulong seed) => new NameGenerator(new NamesData { englishMale = new[] { "Wulfric", "Godwin" }, englishFemale = new[] { "Eadgyth" }, normanMale = new[] { "Robert", "Hugh" }, normanFemale = new[] { "Mabel" }, houses = new[] { "de Lacy" }, epithets = new[] { "the Red" }, placeRoots = new[] { "Ash" }, placeSuffixes = new[] { "by" }, traits = new[] { "proud" } }, new DeterministicRng(seed));

        [Test]
        public void Estate_HarshTaxFillsTheChestAndRaisesUnrest()
        {
            var b = new LordshipBalance { crisisChancePerDay = 0f };
            var s = Manor(); var rng = new DeterministicRng(3);
            var light = EstateRules.Grant(s, Names(1), rng); light.Tax = TaxPolicy.Light; light.StewardHonesty = 1f;
            var harsh = EstateRules.Grant(s, Names(1), rng); harsh.Tax = TaxPolicy.Harsh; harsh.StewardHonesty = 1f;
            int t0 = light.Treasury, t1 = harsh.Treasury;
            for (int d = 0; d < 30; d++) { EstateRules.DayTick(light, s, true, rng, b, null); EstateRules.DayTick(harsh, s, true, rng, b, null); }
            Assert.Greater(harsh.Treasury - t1, light.Treasury - t0);
            Assert.Greater(harsh.Unrest, light.Unrest);
            Assert.Less(light.Unrest, 0.2f);
        }

        [Test]
        public void Estate_DishonestStewardSkims_RevoltAtHighUnrest()
        {
            var b = new LordshipBalance { crisisChancePerDay = 0f };
            var s = Manor(); var rng = new DeterministicRng(4);
            var e = EstateRules.Grant(s, Names(2), rng); e.StewardHonesty = 0.2f; e.Tax = TaxPolicy.Harsh; e.Unrest = 0.8f;
            var log = new List<string>();
            for (int d = 0; d < 40; d++) EstateRules.DayTick(e, s, false, rng, b, log);
            Assert.Greater(e.SkimmedThisMonth, 0);
            Assert.IsTrue(e.InRevolt, string.Join("|", log));
        }

        [Test]
        public void Crisis_ResolvesWithConsequences()
        {
            var b = new LordshipBalance(); var s = Manor(); var rng = new DeterministicRng(5);
            var e = EstateRules.Grant(s, Names(3), rng); e.PendingCrisis = EstateCrisis.Blight; float u = e.Unrest; float g = e.Granary;
            Assert.IsNotEmpty(EstateRules.Describe(EstateCrisis.Blight, e)); Assert.AreEqual(3, EstateRules.Options(EstateCrisis.Blight).Length);
            string t = EstateRules.Resolve(e, s, 0, new CharacterSheet(), new GlobalReputation(new ReputationBalance()), Names(3), rng, b);
            Assert.IsNotEmpty(t); Assert.Less(e.Unrest, u); Assert.Less(e.Granary, g); Assert.AreEqual(EstateCrisis.None, e.PendingCrisis);
            e.PendingCrisis = EstateCrisis.StewardCaught; string old = e.StewardName;
            EstateRules.Resolve(e, s, 0, new CharacterSheet(), new GlobalReputation(new ReputationBalance()), Names(9), rng, b);
            Assert.AreNotEqual(string.Empty, e.StewardName); Assert.GreaterOrEqual(e.StewardHonesty, 0.6f);
        }

        [Test]
        public void ManorCourt_SittingAndVerdicts()
        {
            var rng = new DeterministicRng(6); var e = new Estate { DaysSinceCourt = 20, Treasury = 0 };
            var cases = ManorCourt.Sitting(3, Names(4), rng);
            Assert.AreEqual(3, cases.Count); foreach (var c in cases) Assert.IsNotEmpty(c.Text);
            var rep = new GlobalReputation(new ReputationBalance());
            string fined = ManorCourt.Judge(e, new CourtMatter { Kind = CourtCase.Poaching, Pennies = 6 }, Verdict.Fine, rep);
            Assert.AreEqual(6, e.Treasury); Assert.AreEqual(0, e.DaysSinceCourt); Assert.IsNotEmpty(fined);
            float u = e.Unrest;
            ManorCourt.Judge(e, new CourtMatter { Kind = CourtCase.Poaching, Pennies = 6 }, Verdict.Hang, rep);
            Assert.Greater(e.Unrest, u); Assert.Less(rep.Get(Faction.Church), 0f);
        }

        [Test]
        public void Allegiance_SecretDealsGetDiscovered()
        {
            var b = new LordshipBalance { secretDealDiscoveryPerDay = 0.2f };
            var a = new LordAllegiance { Declared = Allegiance.Stephen };
            Assert.AreEqual(0, AllegianceRules.DealSecretly(a, Allegiance.Stephen, b));
            Assert.AreEqual(b.secretDealPennies, AllegianceRules.DealSecretly(a, Allegiance.Matilda, b));
            var rng = new DeterministicRng(7); int day = 0; bool found = false;
            while (day < 200 && !found) { found = AllegianceRules.DayTick(a, rng, b); day++; }
            Assert.IsTrue(found); Assert.IsTrue(a.Discovered);
            var rep = new GlobalReputation(new ReputationBalance()); var crime = new CrimeRecord(new CrimeBalance());
            AllegianceRules.OnDiscovered(a, crime, rep, "ashby", 10, rng);
            Assert.IsTrue(crime.HasKnownCrime); Assert.LessOrEqual(rep.Get(Faction.Crown), -0.5f); Assert.AreEqual(Allegiance.Neutral, a.SecretDeal);
        }

        [Test]
        public void Suit_CharismaWinsTheHeiress()
        {
            var b = new LordshipBalance(); var father = new MinorLord { Id = 2, House = "de Lacy", Declared = Allegiance.Stephen };
            var s = SuitRules.Begin(father, Names(8), "ashby");
            var charming = new CharacterSheet { Rank = Rank.Knight }; charming.Attributes[(int)Attribute.Charisma] = 16;
            var rng = new DeterministicRng(8);
            for (int i = 0; i < 12 && s.Favour < b.suitConsentFavour; i++) SuitRules.Visit(s, charming, 0.8f, rng, b);
            Assert.GreaterOrEqual(s.Favour, b.suitConsentFavour);
            Assert.IsTrue(SuitRules.Propose(s, charming, 0, b, out string yes)); Assert.IsNotEmpty(yes);
            var dull = new CharacterSheet { Rank = Rank.Peasant };
            Assert.IsFalse(SuitRules.Propose(new Suit { Favour = 1f }, dull, 0, b, out _));
            var wife = SuitRules.Wed(s, 50, Allegiance.Stephen, rng);
            Assert.AreEqual(Relation.Spouse, wife.Relation); Assert.IsTrue(s.Married);
        }

        [Test]
        public void Realm_SeasonsMoveTheWar()
        {
            var b = new LordshipBalance { campaignFlipChance = 1f, lordSwitchChancePerSeason = 0.5f };
            var lords = new List<MinorLord>();
            for (int i = 0; i < 10; i++) lords.Add(new MinorLord { Id = i, GivenName = "L" + i, House = "H", Declared = i < 8 ? Allegiance.Stephen : Allegiance.Matilda, Loyalty = 0.2f, Ambition = 0.9f, Knights = 20, Alive = true });
            var towns = new List<Settlement> { new Settlement { Id = "a", Name = "A", Allegiance = Allegiance.Matilda }, new Settlement { Id = "b", Name = "B", Allegiance = Allegiance.Stephen } };
            var r = RealmRules.Fresh(b); var log = new List<string>(); var rng = new DeterministicRng(9);
            for (int i = 0; i < 6; i++) RealmRules.Season(r, lords, towns, rng, b, log);
            Assert.AreEqual(6, r.SeasonsElapsed); Assert.Greater(log.Count, 0);
            int stephenLords = 0; foreach (var l in lords) if (l.Declared == Allegiance.Stephen) stephenLords++;
            Assert.GreaterOrEqual(stephenLords, 8);   // the weak side drifts toward the strong one
            string t = RealmRules.Act(r, MonarchAct.Tallage, null, lords, b, rng);
            Assert.IsNotEmpty(t); Assert.Less(lords[0].Loyalty, 1f);
            int before = r.CrownTreasury; RealmRules.Act(r, MonarchAct.GrantLand, lords[9], lords, b, rng);
            Assert.AreEqual(before - b.grantLandCost, r.CrownTreasury); Assert.Greater(lords[9].Loyalty, 0.2f);
        }
    }
}

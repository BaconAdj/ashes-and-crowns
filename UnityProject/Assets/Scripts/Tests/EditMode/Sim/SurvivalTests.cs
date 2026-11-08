using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class SurvivalTests
    {
        [Test]
        public void Hunger_Drains_ThenStarvationKills()
        {
            var b = new SurvivalBalance();
            var needs = new Needs(b);
            var body = new BodyState(new WoundBalance());
            needs.Tick(10f, body);
            Assert.Less(needs.Hunger, 0.85f); Assert.AreEqual(1f, body.Blood, "no starvation while there is food in the belly");
            needs.Tick(400f, body);
            Assert.IsTrue(needs.Starving); Assert.IsTrue(body.IsDead); Assert.AreEqual("hunger", body.CauseOfDeath);
        }

        [Test]
        public void Eating_Restores_AndClamps()
        {
            var needs = new Needs(new SurvivalBalance());
            needs.Tick(20f, null);
            needs.Eat(0.3f); Assert.Greater(needs.Hunger, 0.5f);
            needs.Eat(5f); Assert.AreEqual(1f, needs.Hunger);
        }

        [Test]
        public void Wounds_CanTurnSeptic_AndSepsisKillsOrClears()
        {
            int deaths = 0, infections = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var body = new BodyState(new WoundBalance());
                body.ApplyHit(BodyPart.LeftLeg, 0.5f, "spear");
                var rng = new DeterministicRng(seed);
                bool everInfected = false;
                for (int d = 0; d < 30 && !body.IsDead; d++) { body.TickDay(rng); if (body.HasInfection) everInfected = true; }
                if (everInfected) infections++;
                if (body.IsDead) { deaths++; Assert.AreEqual("wound fever", body.CauseOfDeath); }
            }
            Assert.Greater(infections, 20); Assert.Greater(deaths, 0); Assert.Less(deaths, 100);
        }

        [Test]
        public void TreatedWounds_InfectFarLess()
        {
            int untreated = 0, treated = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var a = new BodyState(new WoundBalance()); a.ApplyHit(BodyPart.Torso, 0.4f, "t");
                var b = new BodyState(new WoundBalance()); b.ApplyHit(BodyPart.Torso, 0.4f, "t"); b.TreatAll();
                var ra = new DeterministicRng(seed); var rb = new DeterministicRng(seed);
                for (int d = 0; d < 6; d++) { a.TickDay(ra); b.TickDay(rb); }
                if (a.HasInfection || a.Blood < 0.75f) untreated++;
                if (b.HasInfection || b.Blood < 0.75f) treated++;
            }
            Assert.Greater(untreated, treated * 2);
        }

        [Test]
        public void Disease_IsRareInAVillage_CommonerInCamp_AndPlagueFollowsOutbreak()
        {
            var b = new DiseaseBalance();
            int village = 0, camp = 0, plague = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var v = new DiseaseState(b); var body = new BodyState(new WoundBalance());
                for (int d = 0; d < 30; d++) v.TickDay(body, 0f, false, true, new DeterministicRng(seed * 31 + (ulong)d));
                if (v.Current != Sickness.None || body.Blood < 1f) village++;
                var c = new DiseaseState(b); body = new BodyState(new WoundBalance());
                for (int d = 0; d < 30; d++) c.TickDay(body, 0f, true, false, new DeterministicRng(seed * 37 + (ulong)d));
                if (c.Current != Sickness.None || body.Blood < 1f) camp++;
                var p = new DiseaseState(b); body = new BodyState(new WoundBalance());
                for (int d = 0; d < 30; d++) p.TickDay(body, 1f, false, false, new DeterministicRng(seed * 41 + (ulong)d));
                if (p.Current == Sickness.Plague || body.CauseOfDeath == "the plague") plague++;
            }
            Assert.Greater(camp, village); Assert.Greater(plague, village);
        }
    }

    public class FestivalTests
    {
        [Test]
        public void JulianEaster_MatchesKnownDates()
        {
            Festivals.JulianEaster(1136, out int m, out int d); Assert.AreEqual(3, m); Assert.AreEqual(22, d);
            Festivals.JulianEaster(1135, out m, out d); Assert.AreEqual(4, m); Assert.AreEqual(7, d);
            Festivals.JulianEaster(1141, out m, out d); Assert.AreEqual(3, m); Assert.AreEqual(30, d);
        }

        [Test]
        public void FixedFeasts_Sundays_AndMassWindow()
        {
            Assert.AreEqual(Feast.Christmas, Festivals.FeastOn(1135, 12, 25));
            Assert.AreEqual(Feast.Michaelmas, Festivals.FeastOn(1136, 9, 29));
            Assert.AreEqual(Feast.Sunday, Festivals.FeastOn(1135, 12, 22));
            Assert.AreEqual(Feast.None, Festivals.FeastOn(1135, 12, 23));
            Assert.AreEqual(Feast.Easter, Festivals.FeastOn(1136, 3, 22));
            Assert.AreEqual(Feast.Whitsun, Festivals.FeastOn(1136, 5, 10));
            Assert.IsTrue(Festivals.IsFair(Feast.Michaelmas)); Assert.IsFalse(Festivals.IsFair(Feast.Sunday));
            var t = new TimeBalance(); var c = new Calendar(t); // Sunday 09:00
            Assert.IsTrue(Festivals.IsMassWindow(c, t));
            c.AdvanceHours(3.5f); Assert.IsFalse(Festivals.IsMassWindow(c, t));
            c.AdvanceHours(24f); Assert.IsFalse(Festivals.IsMassWindow(c, t), "Monday");
        }
    }

    public class EconomyTests
    {
        private static ItemsData Items() => new ItemsData { items = new[]
        {
            new ItemDef { id = "grain", name = "Grain", category = "raw", basePrice = 4, weight = 25 },
            new ItemDef { id = "bread", name = "Bread", category = "food", basePrice = 1, nutrition = 0.25f, weight = 0.6f },
            new ItemDef { id = "tools", name = "Tools", category = "good", basePrice = 18, weight = 6 },
            new ItemDef { id = "sword", name = "Sword", category = "weapon", basePrice = 60, weight = 1.4f, damage = 24 },
        } };

        private static Market Fresh() { var m = new Market(new EconomyBalance(), Items()); m.Stock("grain", 40f); m.Stock("bread", 60f); m.Stock("tools", 6f); m.Stock("sword", 1f); return m; }

        [Test]
        public void Prices_FollowStock_Season_AndStanding()
        {
            var m = Fresh();
            int summer = m.BuyPrice("bread", 8), winter = m.BuyPrice("bread", 3);
            Assert.GreaterOrEqual(winter, summer);
            int before = m.BuyPrice("grain", 7);
            m.Find("grain").Stock = 8f;
            Assert.Greater(m.BuyPrice("grain", 7), before, "scarcity raises prices");
            Assert.LessOrEqual(m.BuyPrice("tools", 7, 1f), m.BuyPrice("tools", 7, 0f), "the respected pay less");
            Assert.Less(m.SellPrice("tools", 7), m.BuyPrice("tools", 7), "merchants keep a spread");
        }

        [Test]
        public void ForagingArmy_TriplesGrain_AndItRecovers()
        {
            var m = Fresh();
            int calm = m.BuyPrice("grain", 7);
            m.ApplyShock(WarShock.Foraged, 1f);
            int shocked = m.BuyPrice("grain", 7);
            Assert.GreaterOrEqual(shocked, calm * 3);
            Assert.AreEqual(m.BuyPrice("sword", 7), Fresh().BuyPrice("sword", 7), "swords are not food");
            for (int d = 0; d < 60; d++) m.TickDay();
            Assert.Less(m.BuyPrice("grain", 7), shocked / 2);
            Assert.AreEqual(WarShock.None, m.Shock);
        }

        [Test]
        public void BuyAndSell_MoveCoinsStockAndInventory()
        {
            var m = Fresh(); var sheet = new CharacterSheet { Coins = 20 }; var inv = new Inventory();
            Assert.IsTrue(m.Buy("bread", 4, 7, 0.5f, sheet, inv, out int paid));
            Assert.AreEqual(4, inv.Count("bread")); Assert.AreEqual(20 - paid, sheet.Coins); Assert.AreEqual(56f, m.Find("bread").Stock);
            Assert.IsFalse(m.Buy("sword", 1, 7, 0.5f, sheet, inv, out _), "cannot afford a sword");
            Assert.IsTrue(m.Sell("bread", 2, 7, 0.5f, sheet, inv, out int earned));
            Assert.AreEqual(2, inv.Count("bread")); Assert.GreaterOrEqual(earned, 0);
            Assert.IsFalse(m.Sell("bread", 9, 7, 0.5f, sheet, inv, out _));
        }

        [Test]
        public void Haggling_RewardsSkill_PunishesGreed()
        {
            var m = Fresh();
            int novice = 0, master = 0, greedy = 0;
            for (ulong s = 1; s <= 300; s++)
            {
                if (m.Haggle(0.85f, true, 5f, 0.5f, new DeterministicRng(s))) novice++;
                if (m.Haggle(0.85f, true, 80f, 0.5f, new DeterministicRng(s))) master++;
                if (m.Haggle(0.4f, true, 80f, 0.5f, new DeterministicRng(s))) greedy++;
            }
            Assert.Greater(master, novice); Assert.Less(greedy, novice);
            Assert.IsTrue(m.Haggle(1f, true, 0f, 0.5f, new DeterministicRng(1)), "paying the asking price always works");
        }
    }

    public class TradeTests
    {
        private static TradeDef Farmer() => new TradeDef { id = "farmer", skill = "Farming", tool = "plough", product = "grain", input = "", baseUnitsPerDay = 1.2f, dangerPerDay = 0.01f, seasonByMonth = new[] { 0.2f, 0.3f, 0.8f, 1f, 1f, 0.9f, 1f, 2.2f, 2f, 1f, 0.5f, 0.2f }, momentKind = "rhythm" };
        private static TradeDef Smith() => new TradeDef { id = "blacksmith", skill = "Smithing", tool = "hammer", product = "tools", input = "iron", baseUnitsPerDay = 0.8f, dangerPerDay = 0.03f, seasonByMonth = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }, momentKind = "rhythm" };

        [Test]
        public void Harvest_OutproducesWinter_AndSkillAndToolsMatter()
        {
            var cb = new CharacterBalance();
            int harvest = 0, winter = 0, noTool = 0, novice = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var w = new CharacterSheet(); w.Set(Skill.Farming, 50f); var inv = new Inventory(); inv.Add("plough");
                harvest += TradeWork.WorkDay(Farmer(), w, inv, null, 8, 0f, 0.5f, cb, null, new DeterministicRng(seed)).UnitsProduced;
                w = new CharacterSheet(); w.Set(Skill.Farming, 50f); inv = new Inventory(); inv.Add("plough");
                winter += TradeWork.WorkDay(Farmer(), w, inv, null, 1, 0f, 0.5f, cb, null, new DeterministicRng(seed)).UnitsProduced;
                w = new CharacterSheet(); w.Set(Skill.Farming, 50f); inv = new Inventory();
                var r = TradeWork.WorkDay(Farmer(), w, inv, null, 8, 0f, 0.5f, cb, null, new DeterministicRng(seed)); noTool += r.UnitsProduced; StringAssert.Contains("plough", r.Note);
                w = new CharacterSheet(); inv = new Inventory(); inv.Add("plough");
                novice += TradeWork.WorkDay(Farmer(), w, inv, null, 8, 0f, 0.5f, cb, null, new DeterministicRng(seed)).UnitsProduced;
            }
            Assert.Greater(harvest, winter * 3); Assert.Greater(harvest, noTool * 2); Assert.Greater(harvest, novice);
        }

        [Test]
        public void Smithing_ConsumesIron_GrowsSkill_AndCanInjure()
        {
            var cb = new CharacterBalance();
            var w = new CharacterSheet(); w.Attributes[(int)Attribute.Strength] = 7;
            var inv = new Inventory(); inv.Add("hammer"); inv.Add("iron", 2);
            var body = new BodyState(new WoundBalance());
            int produced = 0, injuries = 0; float skillBefore = w.Get(Skill.Smithing);
            for (ulong seed = 1; seed <= 60; seed++)
            {
                inv.Add("iron", 1);
                var r = TradeWork.WorkDay(Smith(), w, inv, null, 5, 0f, 1f, cb, body, new DeterministicRng(seed));
                produced += r.UnitsProduced; if (r.Injured) injuries++;
                Assert.AreEqual(r.UnitsProduced, r.InputsConsumed);
            }
            Assert.Greater(produced, 20); Assert.AreEqual(produced, inv.Count("tools")); Assert.Greater(w.Get(Skill.Smithing), skillBefore);
            Assert.Greater(injuries, 0); Assert.Greater(body.Wounds.Count, 0);
        }

        [Test]
        public void Apprenticeship_TakesSeasons_AndCharismaHelps()
        {
            int charming = 0, dull = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var c = new CharacterSheet(); c.Attributes[(int)Attribute.Charisma] = 9;
                var d = new CharacterSheet(); d.Attributes[(int)Attribute.Charisma] = 3;
                if (Apprenticeship.TryConvince(c, 0.3f, 0.6f, new DeterministicRng(seed), out _)) charming++;
                if (Apprenticeship.TryConvince(d, 0.3f, 0.6f, new DeterministicRng(seed), out _)) dull++;
            }
            Assert.Greater(charming, dull);
            var app = new Apprenticeship(); app.Begin("blacksmith", 3, 2f);
            Assert.IsTrue(app.Active);
            int days = 0; while (!app.WorkedDay(0.5f)) days++;
            Assert.AreEqual(181, days); Assert.IsFalse(app.Active); Assert.Greater(app.Competence, 0.9f);
        }

        [Test]
        public void Standing_MovesWithDeeds_AndDecaysTowardNeutral()
        {
            var s = new VillageStanding(new StandingBalance());
            for (int i = 0; i < 10; i++) s.AttendedMass();
            Assert.IsTrue(s.Respected); Assert.AreEqual("respected", s.Describe());
            s.TheftSeen(); s.TheftSeen();
            Assert.IsFalse(s.Respected);
            float v = s.Value; for (int d = 0; d < 100; d++) s.TickDay();
            Assert.Greater(System.Math.Abs(0.5f - v), System.Math.Abs(0.5f - s.Value));
            var t = new VillageStanding(new StandingBalance()); for (int i = 0; i < 5; i++) t.BrawlStarted(); Assert.IsTrue(t.Despised);
        }
    }
}

using System.Collections.Generic;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class IntrigueTests
    {
        [Test]
        public void Plots_GrowFromHostileRelativesAndPower()
        {
            var b = new IntrigueBalance();
            var lord = new CharacterSheet { Rank = Rank.Lord }; lord.Attributes[(int)Attribute.Cunning] = 8;
            var family = new List<Npc> { new Npc { Id = 1, Name = "Edric", Relation = Relation.Heir, Disposition = -0.6f } };
            int plots = 0, usurp = 0;
            for (int i = 0; i < 2000; i++) { var p = IntrigueRules.Roll(lord, family, null, new Estate(), 0.2f, new DeterministicRng((ulong)i + 1), b); if (p != null) { plots++; if (p.Kind == PlotKind.Usurp) usurp++; } }
            Assert.Greater(plots, 20); Assert.Greater(usurp, 0);
            int quiet = 0; var peasant = new CharacterSheet { Rank = Rank.Peasant };
            for (int i = 0; i < 2000; i++) if (IntrigueRules.Roll(peasant, new List<Npc>(), null, null, 0f, new DeterministicRng((ulong)i + 1), b) != null) quiet++;
            Assert.Less(quiet, plots);
        }

        [Test]
        public void Warnings_ComeInOrderAndStopAtThree()
        {
            var b = new IntrigueBalance { warningChancePerDay = 1f };
            var p = new Plot { Kind = PlotKind.Poison, Plotter = PlotterKind.Relative, PlotterName = "Edith" };
            var c = new CharacterSheet(); c.Attributes[(int)Attribute.Cunning] = 8;
            var rng = new DeterministicRng(2);
            string w1 = IntrigueRules.Warning(p, c, rng, b), w2 = IntrigueRules.Warning(p, c, rng, b), w3 = IntrigueRules.Warning(p, c, rng, b), w4 = IntrigueRules.Warning(p, c, rng, b);
            Assert.IsNotNull(w1); Assert.IsTrue(w2.Contains("Edith")); Assert.IsNotNull(w3); Assert.IsNull(w4); Assert.AreEqual(3, p.Warnings.Count);
        }

        [Test]
        public void Counters_TasterFoilsPoison_GuardHelps()
        {
            var b = new IntrigueBalance { tasterFoilsPoison = 1f };
            var c = new CharacterSheet { Coins = 100 }; c.Attributes[(int)Attribute.Cunning] = 8;
            var p = new Plot { Kind = PlotKind.Poison };
            IntrigueRules.Act(p, Counter.Taster, c, null, null, null, new DeterministicRng(1), b, out bool ended);
            Assert.IsTrue(p.Tasted); Assert.AreEqual(100 - b.tasterCostPennies, c.Coins); Assert.IsFalse(ended);
            Assert.AreEqual(PlotOutcome.Foiled, IntrigueRules.Resolve(p, c, 0, new DeterministicRng(3), b, out string t)); Assert.IsNotEmpty(t);
            int deaths = 0; for (int i = 0; i < 500; i++) if (IntrigueRules.Resolve(new Plot { Kind = PlotKind.Knife }, c, 0, new DeterministicRng((ulong)i), b, out _) == PlotOutcome.Dead) deaths++;
            int guarded = 0; for (int i = 0; i < 500; i++) if (IntrigueRules.Resolve(new Plot { Kind = PlotKind.Knife, Guarded = true }, c, 0, new DeterministicRng((ulong)i), b, out _) == PlotOutcome.Dead) guarded++;
            Assert.Greater(deaths, guarded); Assert.Greater(deaths, 50);
        }

        [Test]
        public void Confront_RightAndWrong()
        {
            var b = new IntrigueBalance { confrontCunningWeight = 1f };
            var c = new CharacterSheet(); c.Attributes[(int)Attribute.Cunning] = 12;
            var guilty = new Npc { Id = 7, Name = "Osbert", Disposition = -0.5f }; var innocent = new Npc { Id = 8, Name = "Hild", Disposition = 0.5f };
            var p = new Plot { Kind = PlotKind.Knife, Plotter = PlotterKind.Relative, PlotterId = 7 };
            string wrong = IntrigueRules.Act(p, Counter.Confront, c, null, null, innocent, new DeterministicRng(1), b, out bool e1);
            Assert.IsFalse(e1); Assert.Less(innocent.Disposition, 0.5f); Assert.IsTrue(wrong.Contains("Hild"));
            IntrigueRules.Act(p, Counter.Confront, c, null, null, guilty, new DeterministicRng(1), b, out bool e2);
            Assert.IsTrue(e2); Assert.IsTrue(p.Exposed);
        }

        [Test]
        public void VillageEvents_ShockTheMarket()
        {
            var b = new VillageEventBalance();
            var items = new ItemsData { items = new[] { new ItemDef { id = "grain", name = "grain", category = "food", basePrice = 2, weight = 1 } } };
            var s = new Settlement { Id = "v", Population = 100, Wealth = 1f, Market = new Market(new EconomyBalance(), items) };
            string t = VillageEvents.Apply(VillageEventKind.Raid, s, new DeterministicRng(1), b, out int raiders);
            Assert.GreaterOrEqual(raiders, b.raiderCountMin); Assert.Less(s.Population, 100); Assert.AreEqual(WarShock.Raided, s.Market.Shock); Assert.IsNotEmpty(t);
            VillageEvents.Apply(VillageEventKind.Refugees, s, new DeterministicRng(2), b, out _);
            Assert.AreEqual(WarShock.Refugees, s.Market.Shock);
            int none = 0; for (int i = 0; i < 1000; i++) if (VillageEvents.Roll(s, 0f, new DeterministicRng((ulong)i), b) == VillageEventKind.None) none++;
            Assert.Greater(none, 900);
        }

        [Test]
        public void Strings_FallBackToEnglish()
        {
            L.Load(new StringsData { language = "fr", keys = new[] { "ON FOOT", "DEAD" }, values = new[] { "À PIED", "MORT" } });
            Assert.AreEqual("À PIED", L.T("ON FOOT")); Assert.AreEqual("RIDING", L.T("RIDING")); Assert.AreEqual("fr", L.Language);
            L.Clear(); Assert.AreEqual("ON FOOT", L.T("ON FOOT"));
        }
    }
}

using System.IO;
using System.Text;
using System.Text.Json;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Harness
{
    /// <summary>
    /// The balance pass across the five starts: every committed background eats from its purse (and its trade) for sixty days
    /// at base prices, with the body, needs and disease models running. Nobody may starve; peasants must earn their bread.
    /// </summary>
    public class StartBalanceTests
    {
        private static readonly JsonSerializerOptions Opt = new JsonSerializerOptions { IncludeFields = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        private static string Data(string file)
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md"))) dir = Path.GetDirectoryName(dir);
            return File.ReadAllText(Path.Combine(dir, "UnityProject", "Assets", "Data", file));
        }

        [Test]
        public void EveryStart_SurvivesSixtyDaysAtBasePrices()
        {
            var balance = JsonSerializer.Deserialize<Balance>(Data("balance.json"), Opt);
            var backgrounds = JsonSerializer.Deserialize<BackgroundsData>(Data("backgrounds.json"), Opt);
            var items = JsonSerializer.Deserialize<ItemsData>(Data("items.json"), Opt);
            var trades = JsonSerializer.Deserialize<TradesData>(Data("trades.json"), Opt);
            var bread = items.Find("pottage"); Assert.IsNotNull(bread);
            var report = new StringBuilder("\nstart                      coins  day60  fed%  alive\n");
            int failures = 0;
            foreach (var rank in backgrounds.ranks)
            foreach (var bg in rank.backgrounds)
            {
                var body = new BodyState(balance.wounds);
                var choice = new CharacterFactory.Choice { Name = "Test", Rank = System.Enum.Parse<Rank>(rank.id, true), BackgroundId = bg.id, Allocation = new[] { 7, 7, 6, 6 } };
                var sheet = CharacterFactory.Create(choice, backgrounds, body);
                var needs = new Needs(balance.survival); var disease = new DiseaseState(balance.disease); var inv = new Inventory();
                var rng = new DeterministicRng(MapGeometry.StableHash(bg.id));
                TradeDef trade = string.IsNullOrEmpty(sheet.Trade) ? null : trades.Find(sheet.Trade);
                if (trade != null && !string.IsNullOrEmpty(trade.tool)) inv.Add(trade.tool);
                int fedDays = 0;
                var manor = new Settlement { Id = "manor", Name = "the manor", Population = 200, Wealth = 1f };
                var estate = sheet.Rank == Rank.Lord ? EstateRules.Grant(manor, null, rng) : null;
                if (estate != null) estate.StewardHonesty = 0.7f;
                for (int day = 0; day < 60 && !body.IsDead; day++)
                {
                    int month = 1 + (3 + day / 30) % 12;   // start in April
                    if (trade != null)
                    {
                        if (!string.IsNullOrEmpty(trade.input) && inv.Count(trade.input) < 2 && sheet.Coins >= items.Find(trade.input).basePrice) { sheet.Coins -= items.Find(trade.input).basePrice; inv.Add(trade.input); }
                        var work = TradeWork.WorkDay(trade, sheet, inv, needs, month, 0f, 0.5f, balance.character, body, rng);
                        int produced = inv.Count(work.Product);
                        if (produced > 0 && items.Find(work.Product) != null) { sheet.Coins += produced * items.Find(work.Product).basePrice; inv.Remove(work.Product, produced); }
                    }
                    // the rank's living: a sellsword's wage, a household knight's keep, a manor's rent
                    if (trade == null)
                    {
                        if (sheet.Rank == Rank.Warrior && !rng.Chance(balance.service.latePayChance)) sheet.Coins += balance.service.mercenaryWagePerDay;
                        else if (sheet.Rank == Rank.Knight) sheet.Coins += balance.service.householdWagePerDay;
                        else if (sheet.Rank == Rank.Lord) { EstateRules.DayTick(estate, manor, true, rng, balance.lordship, null); if (estate.Treasury >= 10) { estate.Treasury -= 10; sheet.Coins += 10; } }
                    }
                    // two meals a day when the purse allows
                    for (int meal = 0; meal < 2; meal++) if (sheet.Coins >= bread.basePrice) { sheet.Coins -= bread.basePrice; needs.Eat(bread.nutrition); }
                    if (needs.Hunger > 0.3f) fedDays++;
                    needs.Tick(24f, body);
                    needs.Sleep(balance.survival.sleepHoursPerNight);
                    disease.TickDay(body, 0f, false, true, rng);
                    body.TickDay(rng);
                }
                bool ok = !body.IsDead && fedDays >= 45;
                if (!ok) failures++;
                report.Append($"{rank.id + "/" + bg.id,-26} {bg.coins,5} {sheet.Coins,6} {fedDays * 100 / 60,4}%  {(body.IsDead ? "DEAD: " + body.CauseOfDeath : "yes")}\n");
            }
            TestContext.WriteLine(report.ToString());
            Assert.AreEqual(0, failures, report.ToString());
        }
    }
}

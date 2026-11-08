using AshesAndCrowns.Save;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Save
{
    public class SaveV4Tests
    {
        [Test]
        public void SurvivalFields_RoundTrip()
        {
            var s = RunStateFactory.Sample(withBattle: false);
            s.Hunger = 0.4f; s.Rest = 0.7f; s.SicknessKind = 2; s.SickDays = 3;
            s.InventoryIds = new[] { "bread", "plough" }; s.InventoryCounts = new[] { 3, 1 };
            s.StandingValue = 0.66f; s.MassesAttended = 4; s.MassesMissed = 1;
            s.ApprenticeTrade = "blacksmith"; s.ApprenticeMaster = 7; s.ApprenticeSeasons = 1.5f; s.ApprenticeCompetence = 0.3f;
            s.MarketIds = new[] { "grain", "ale" }; s.MarketStocks = new[] { 12.5f, 40f }; s.MarketShock = 1; s.MarketShockStrength = 0.8f; s.MarketWealth = 0.9f;
            s.Outbreak = 0.2f; s.LastMassDay = 2135810;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.AreEqual(0.4f, b.Hunger); Assert.AreEqual(2, b.SicknessKind); Assert.AreEqual(3, b.SickDays);
            CollectionAssert.AreEqual(s.InventoryIds, b.InventoryIds); CollectionAssert.AreEqual(s.InventoryCounts, b.InventoryCounts);
            Assert.AreEqual(0.66f, b.StandingValue); Assert.AreEqual("blacksmith", b.ApprenticeTrade); Assert.AreEqual(1.5f, b.ApprenticeSeasons);
            CollectionAssert.AreEqual(s.MarketStocks, b.MarketStocks); Assert.AreEqual(1, b.MarketShock); Assert.AreEqual(0.9f, b.MarketWealth);
            Assert.AreEqual(2135810, b.LastMassDay);
        }

        [Test]
        public void Version3Files_LoadWithDefaults()
        {
            var s = RunStateFactory.Sample(); s.Version = 3;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.AreEqual(0.85f, b.Hunger); Assert.IsEmpty(b.InventoryIds); Assert.AreEqual(-1, b.LastMassDay); Assert.AreEqual(1f, b.MarketWealth);
        }
    }
}

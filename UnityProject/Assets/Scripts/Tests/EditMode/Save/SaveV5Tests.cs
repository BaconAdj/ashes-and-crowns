using AshesAndCrowns.Save;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Save
{
    public class SaveV5Tests
    {
        [Test]
        public void England_RoundTrips()
        {
            var s = RunStateFactory.Sample(withBattle: false);
            s.Settlements = new[] { new SettlementSnapshot { Id = "v1", Name = "Ashford", Type = 0, Nx = 0.5f, Nz = 0.4f, Population = 120, Wealth = 0.9f, Allegiance = 2, LordId = 3, WantedHere = 0.4f, Coastal = true,
                GoodIds = new[] { "fish", "ale" }, GoodStocks = new[] { 30f, 12f }, GoodBaselines = new[] { 25f, 10f }, GoodDemands = new[] { 0.4f, 1f }, Shock = 1, ShockStrength = 0.5f,
                Jobs = new[] { new JobSnapshot { Id = 4, Kind = 1, FromId = "v1", ToId = "london", Employer = "Wat the carter", Blurb = "Carry a letter.", Pay = 6, DeadlineDay = 2135800, Danger = 0.2f } } } };
            s.Reputation = new[] { 0.3f, -0.2f, 0.1f, 0f }; s.Notoriety = 0.15f;
            s.Crimes = new[] { new CrimeSnapshot { Kind = 0, Where = "v1", Day = 2135790, Witnesses = 2, Known = true } };
            s.Outlawed = false; s.SanctuaryDaysLeft = 12f; s.SanctuaryExpired = false;
            s.ActiveJob = new JobSnapshot { Id = 9, Kind = 3, FromId = "v1", ToId = "v1", Pay = 20, DaysRequired = 10, DaysServed = 3, State = 1 };
            s.NextJobId = 9; s.HomeSettlementId = "v1";
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.AreEqual(1, b.Settlements.Length); Assert.AreEqual("Ashford", b.Settlements[0].Name); Assert.AreEqual(0.4f, b.Settlements[0].WantedHere); Assert.IsTrue(b.Settlements[0].Coastal);
            CollectionAssert.AreEqual(s.Settlements[0].GoodStocks, b.Settlements[0].GoodStocks); CollectionAssert.AreEqual(s.Settlements[0].GoodDemands, b.Settlements[0].GoodDemands);
            Assert.AreEqual("Wat the carter", b.Settlements[0].Jobs[0].Employer); Assert.AreEqual(2135800, b.Settlements[0].Jobs[0].DeadlineDay);
            CollectionAssert.AreEqual(s.Reputation, b.Reputation); Assert.AreEqual(0.15f, b.Notoriety);
            Assert.AreEqual(1, b.Crimes.Length); Assert.IsTrue(b.Crimes[0].Known); Assert.AreEqual(12f, b.SanctuaryDaysLeft);
            Assert.AreEqual(3, b.ActiveJob.DaysServed); Assert.AreEqual(9, b.NextJobId); Assert.AreEqual("v1", b.HomeSettlementId);
        }

        [Test]
        public void Version4Files_LoadWithoutEngland()
        {
            var s = RunStateFactory.Sample(); s.Version = 4;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.IsEmpty(b.Settlements); Assert.IsNull(b.ActiveJob); Assert.AreEqual(string.Empty, b.HomeSettlementId);
        }
    }
}

using AshesAndCrowns.Save;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Save
{
    public class SaveV9Tests
    {
        [Test]
        public void EstateSuitRealm_RoundTrip()
        {
            var s = RunStateFactory.Sample(withBattle: false);
            s.HasEstate = true; s.EstateSettlementId = "ashby"; s.EstateName = "Ashby"; s.StewardName = "Hugh the steward"; s.EstateTax = 2; s.EstateCrisis = 3; s.StewardHonesty = 0.4f; s.EstateUnrest = 0.33f; s.EstateTreasury = 420; s.EstateGarrison = 25; s.EstateRevolt = true;
            s.LordDeclared = 1; s.LordSecret = 2; s.LordDeals = 2; s.LordSuspicion = 0.5f;
            s.HasSuit = true; s.SuitHeiress = "Mabel de Lacy"; s.SuitFather = 4; s.SuitFavour = 0.8f; s.SuitConsented = true;
            s.HasRealm = true; s.RealmTreasury = 2200; s.RealmSeasons = 3; s.RealmChurch = -0.2f; s.RealmChronicle = new[] { "The king took Oxford." };
            s.DaysIntoSeason = 40; s.NextNpcId = 103; s.PendingDemand = 2;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.IsTrue(b.HasEstate); Assert.AreEqual("Hugh the steward", b.StewardName); Assert.AreEqual(2, b.EstateTax); Assert.AreEqual(420, b.EstateTreasury); Assert.IsTrue(b.EstateRevolt);
            Assert.AreEqual(2, b.LordSecret); Assert.AreEqual(0.5f, b.LordSuspicion);
            Assert.AreEqual("Mabel de Lacy", b.SuitHeiress); Assert.IsTrue(b.SuitConsented);
            Assert.AreEqual(2200, b.RealmTreasury); Assert.AreEqual(1, b.RealmChronicle.Length); Assert.AreEqual(40, b.DaysIntoSeason); Assert.AreEqual(103, b.NextNpcId); Assert.AreEqual(2, b.PendingDemand);
        }

        [Test]
        public void Version8Files_LoadWithoutLordship()
        {
            var s = RunStateFactory.Sample(withBattle: false); s.Version = 8;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.IsFalse(b.HasEstate); Assert.IsFalse(b.HasRealm); Assert.AreEqual(0, b.NextNpcId);
        }
    }
}

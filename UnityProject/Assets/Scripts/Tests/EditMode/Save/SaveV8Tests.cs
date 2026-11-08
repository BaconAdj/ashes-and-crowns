using AshesAndCrowns.Save;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Save
{
    public class SaveV8Tests
    {
        [Test]
        public void SiegeAndCastle_RoundTrip()
        {
            var s = RunStateFactory.Sample(withBattle: true);
            s.HasSiege = true; s.SiegeTargetId = "oxford"; s.SiegeTargetName = "Oxford"; s.SiegeBesieger = 1; s.SiegePlayerAttacking = true; s.SiegePlayerCommands = true; s.SiegeDay = 12; s.SiegeAttackers = 540; s.SiegeDefenders = 88;
            s.SiegeFood = 7.5f; s.SiegeDefMorale = 0.41f; s.SiegeLadders = 4; s.SiegeRamBuilt = true; s.SiegeInAssault = true; s.SiegeLordId = 5; s.SiegeRelief = 0.3f;
            s.HasCastle = true; s.CastleName = "Wulfric's Tower"; s.CastleSiteId = "ashby_village"; s.CastleNx = 0.4f; s.CastleNz = 0.6f; s.CastleStage = 2; s.CastleDays = 9; s.CastleCoin = 300; s.CastleObjector = -1; s.Garrison = 14;
            s.Battle.SiegeGateOpen = true; s.Battle.RamSeconds = 12.5f;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.IsTrue(b.HasSiege); Assert.AreEqual("Oxford", b.SiegeTargetName); Assert.AreEqual(540, b.SiegeAttackers); Assert.AreEqual(0.41f, b.SiegeDefMorale); Assert.IsTrue(b.SiegeRamBuilt); Assert.IsTrue(b.SiegeInAssault);
            Assert.IsTrue(b.HasCastle); Assert.AreEqual(2, b.CastleStage); Assert.AreEqual(300, b.CastleCoin); Assert.AreEqual(14, b.Garrison);
            Assert.IsTrue(b.Battle.SiegeGateOpen); Assert.AreEqual(12.5f, b.Battle.RamSeconds);
        }

        [Test]
        public void Version7Files_LoadWithoutSiege()
        {
            var s = RunStateFactory.Sample(withBattle: true); s.Version = 7;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.IsFalse(b.HasSiege); Assert.IsFalse(b.HasCastle); Assert.IsFalse(b.Battle.SiegeGateOpen);
        }
    }
}

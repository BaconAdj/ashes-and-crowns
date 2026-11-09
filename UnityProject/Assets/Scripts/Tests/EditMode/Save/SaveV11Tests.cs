using AshesAndCrowns.Save;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Save
{
    public class SaveV11Tests
    {
        [Test]
        public void Duty_RoundTrips()
        {
            var s = RunStateFactory.Sample(withBattle: false);
            s.HasDuty = true; s.DutyKind = 2; s.DutyState = 1; s.DutyToId = "ox"; s.DutyToName = "Oxford"; s.DutyText = "Sealed."; s.DutyGivenDay = 1000; s.DutyDueDay = 1005; s.DutyPay = 8; s.DutyDangerous = true;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.IsTrue(b.HasDuty); Assert.AreEqual(2, b.DutyKind); Assert.AreEqual("Oxford", b.DutyToName); Assert.AreEqual(1005L, b.DutyDueDay); Assert.IsTrue(b.DutyDangerous);
            var old = RunStateFactory.Sample(withBattle: false); old.Version = 10;
            Assert.IsFalse(SaveCodec.Decode(SaveCodec.Encode(old)).HasDuty);
        }
    }
}

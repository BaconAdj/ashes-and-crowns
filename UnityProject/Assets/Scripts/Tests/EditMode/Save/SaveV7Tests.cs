using AshesAndCrowns.Save;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Save
{
    public class SaveV7Tests
    {
        [Test]
        public void Service_RoundTrips()
        {
            var s = RunStateFactory.Sample(withBattle: false);
            s.ServiceRole = 2; s.ServiceSide = 1; s.ServiceCompany = "the Free Company of Walter"; s.ServiceCaptain = "Walter Malet"; s.ServicePatron = "3";
            s.ServiceWage = 3; s.ServiceDays = 12; s.ServiceOwed = 6; s.ServiceBattles = 1; s.ServiceRenown = 9; s.ServiceKills = 3; s.ServiceLines = 1; s.ServiceInfractions = 4.5f; s.ServicePatronFavour = 0.3f; s.ServiceDeserter = false; s.ServiceInCamp = true;
            s.Companies = new[] { new CompanySnapshot { Name = "the Oxford Spears", Captain = "Hugh", BasedAtId = "oxford", Employer = 1, Size = 80, ContractDaysLeft = 40, Reputation = 0.6f } };
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.AreEqual(2, b.ServiceRole); Assert.AreEqual("Walter Malet", b.ServiceCaptain); Assert.AreEqual(9, b.ServiceRenown); Assert.AreEqual(4.5f, b.ServiceInfractions); Assert.IsTrue(b.ServiceInCamp);
            Assert.AreEqual(1, b.Companies.Length); Assert.AreEqual("the Oxford Spears", b.Companies[0].Name); Assert.AreEqual(40, b.Companies[0].ContractDaysLeft);
        }

        [Test]
        public void Version6Files_LoadWithoutService()
        {
            var s = RunStateFactory.Sample(); s.Version = 6;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.AreEqual(0, b.ServiceRole); Assert.IsEmpty(b.Companies);
        }
    }
}

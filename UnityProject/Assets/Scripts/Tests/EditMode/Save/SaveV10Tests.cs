using AshesAndCrowns.Save;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Save
{
    public class SaveV10Tests
    {
        [Test]
        public void Plot_RoundTrips()
        {
            var s = RunStateFactory.Sample(withBattle: false);
            s.HasPlot = true; s.PlotKind = 1; s.PlotterKind = 0; s.PlotterId = 3; s.PlotDaysUntil = 5; s.PlotWarnings = 2; s.PlotterName = "Edric"; s.PlotGuarded = true; s.PlotWarningLines = new[] { "The dog would not eat.", "A knife is missing." };
            s.PendingRaiders = 3; s.EstateDaysThisMonth = 12; s.WarHeat = 0.4f;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.IsTrue(b.HasPlot); Assert.AreEqual("Edric", b.PlotterName); Assert.AreEqual(5, b.PlotDaysUntil); Assert.AreEqual(2, b.PlotWarningLines.Length); Assert.IsTrue(b.PlotGuarded);
            Assert.AreEqual(3, b.PendingRaiders); Assert.AreEqual(0.4f, b.WarHeat);
        }

        [Test]
        public void Version9Files_LoadWithoutPlot()
        {
            var s = RunStateFactory.Sample(withBattle: false); s.Version = 9;
            var b = SaveCodec.Decode(SaveCodec.Encode(s));
            Assert.IsFalse(b.HasPlot); Assert.AreEqual(0, b.PendingRaiders);
        }
    }
}

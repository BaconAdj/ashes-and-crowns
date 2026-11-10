using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class VillageDayTests
    {
        [Test]
        public void Weekday_SleepsWorksEatsDrinks()
        {
            var b = new VillageDayBalance();
            Assert.AreEqual(VillagerActivity.Asleep, VillageDay.At(2f, false, false, 0, b));
            Assert.AreEqual(VillagerActivity.Rising, VillageDay.At(5.5f, false, false, 0, b));
            Assert.AreEqual(VillagerActivity.Working, VillageDay.At(9f, false, false, 0, b));
            Assert.AreEqual(VillagerActivity.Eating, VillageDay.At(12f, false, false, 0, b));
            Assert.AreEqual(VillagerActivity.Working, VillageDay.At(15f, false, false, 0, b));
            Assert.AreEqual(VillagerActivity.Idling, VillageDay.At(18f, false, false, 0, b));
            Assert.AreEqual(VillagerActivity.Home, VillageDay.At(20f, false, false, 0, b));
            Assert.AreEqual(VillagerActivity.Drinking, VillageDay.At(20f, false, false, 1, b));
            Assert.AreEqual(VillagerActivity.Asleep, VillageDay.At(22f, false, false, 1, b));
        }

        [Test]
        public void Mass_OverridesEverything_AndSundayIsNoWork()
        {
            var b = new VillageDayBalance();
            Assert.AreEqual(VillagerActivity.Praying, VillageDay.At(9.5f, true, true, 2, b));
            Assert.AreEqual(VillagerActivity.Praying, VillageDay.At(9.5f, false, true, 2, b));
            for (float h = 6f; h < 21.5f; h += 0.5f) Assert.AreNotEqual(VillagerActivity.Working, VillageDay.At(h, true, false, 1, b), "no work on Sunday at " + h);
            Assert.IsTrue(VillageDay.Indoors(VillagerActivity.Asleep)); Assert.IsFalse(VillageDay.Indoors(VillagerActivity.Working)); Assert.IsFalse(VillageDay.Indoors(VillagerActivity.Idling));
            Assert.IsTrue(VillageDay.Describe(VillagerActivity.Working, "blacksmith").Contains("anvil"));
        }
    }
}

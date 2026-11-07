using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class CalendarTests
    {
        private static Calendar Start() => new Calendar(new TimeBalance());

        [Test]
        public void StartsOnStephensCoronationDay_WhichWasASunday()
        {
            var c = Start();
            Assert.AreEqual(1135, c.Year); Assert.AreEqual(12, c.Month); Assert.AreEqual(22, c.Day);
            Assert.AreEqual(Weekday.Sunday, c.Weekday);
            Assert.IsTrue(c.IsSunday);
        }

        [Test]
        public void AdvancingPastMidnight_RollsTheDate_AndWeekday()
        {
            var c = Start();
            c.AdvanceHours(24f - c.HourOfDay + 0.5f);
            Assert.AreEqual(23, c.Day); Assert.AreEqual(Weekday.Monday, c.Weekday);
            Assert.AreEqual(0.5f, c.HourOfDay, 1e-3f);
        }

        [Test]
        public void MonthAndYearRollOver_WithJulianLeapYears()
        {
            var c = Start();
            c.AdvanceHours(24f * 10f); // 22 Dec + 10 days = 1 Jan 1136
            Assert.AreEqual(1136, c.Year); Assert.AreEqual(1, c.Month); Assert.AreEqual(1, c.Day);
            Assert.AreEqual(29, Calendar.DaysInMonth(1136, 2), "1136 is a Julian leap year");
            Assert.AreEqual(28, Calendar.DaysInMonth(1135, 2));
        }

        [Test]
        public void Bells_RingAtTheCanonicalHours_InOrder()
        {
            var c = Start(); // 09:00 = Terce exactly; next is Sext at 12
            Assert.AreEqual("Sext", c.NextBellName);
            Assert.AreEqual(3f, c.HoursUntilNextBell, 1e-3f);
            int rung = c.AdvanceHours(3.01f);
            Assert.AreEqual(1, rung);
            Assert.AreEqual("Sext", c.LastBellRungName);
            rung = c.AdvanceHours(24f);
            Assert.AreEqual(6, rung, "a full day rings every bell once");
        }

        [Test]
        public void Bells_AcrossMidnight_WrapToPrime()
        {
            var c = Start();
            c.AdvanceHours(13f); // 22:00
            Assert.AreEqual("Prime", c.NextBellName);
            Assert.AreEqual(8f, c.HoursUntilNextBell, 1e-3f);
        }

        [Test]
        public void SeasonFollowsMonth()
        {
            var c = Start();
            Assert.AreEqual(Season.Winter, c.Season);
            c.AdvanceHours(24f * 100f);
            Assert.AreEqual(Season.Spring, c.Season);
        }

        [Test]
        public void DayOfYear_And_FormattedDate()
        {
            var c = Start();
            Assert.AreEqual(356, c.DayOfYear);
            StringAssert.Contains("Sunday", c.LongDate());
            StringAssert.Contains("22", c.LongDate());
            StringAssert.Contains("December", c.LongDate());
            StringAssert.Contains("1135", c.LongDate());
        }

        [Test]
        public void TotalHours_IsMonotonic_AndSerialisable()
        {
            var c = Start();
            double before = c.TotalHours;
            c.AdvanceHours(37.25f);
            Assert.AreEqual(before + 37.25, c.TotalHours, 1e-3);
            var copy = Calendar.FromTotalHours(new TimeBalance(), c.TotalHours);
            Assert.AreEqual(c.Year, copy.Year); Assert.AreEqual(c.Month, copy.Month); Assert.AreEqual(c.Day, copy.Day);
            Assert.AreEqual(c.HourOfDay, copy.HourOfDay, 1e-3f);
        }
    }
}

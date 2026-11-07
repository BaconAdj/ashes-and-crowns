using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class DaylightTests
    {
        [Test]
        public void MidsummerDayIsLongerThanMidwinter()
        {
            float june = Daylight.DayLengthHours(172);
            float dec = Daylight.DayLengthHours(356);
            Assert.Greater(june, 15.5f); Assert.Less(june, 17.5f);
            Assert.Greater(dec, 7f); Assert.Less(dec, 8.5f);
        }

        [Test]
        public void SunIsBelowHorizonAtMidnight_AndAboveAtNoon()
        {
            Assert.Less(Daylight.SunElevationDegrees(0f, 172), 0f);
            Assert.Greater(Daylight.SunElevationDegrees(12f, 172), 55f);
            Assert.Greater(Daylight.SunElevationDegrees(12f, 356), 10f);
            Assert.Less(Daylight.SunElevationDegrees(12f, 356), 20f);
        }

        [Test]
        public void SunriseAndSunsetBracketNoon()
        {
            Daylight.SunriseSunset(80, out float rise, out float set);
            Assert.Less(rise, 12f); Assert.Greater(set, 12f);
            Assert.AreEqual(12f, (rise + set) * 0.5f, 0.05f);
        }

        [Test]
        public void DaylightFactor_IsZeroAtNight_OneAtNoon_SmoothAtDusk()
        {
            Assert.AreEqual(0f, Daylight.DaylightFactor(1f, 172), 1e-4f);
            Assert.AreEqual(1f, Daylight.DaylightFactor(12f, 172), 1e-2f);
            Daylight.SunriseSunset(172, out _, out float set);
            float dusk = Daylight.DaylightFactor(set, 172);
            Assert.Greater(dusk, 0f); Assert.Less(dusk, 0.6f);
        }
    }
}

using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class ActivityRulesTests
    {
        [Test]
        public void Prayer_FirstHourIsWorthTheRate_ThenHalves_ThenCaps()
        {
            var b = new ActivityBalance { prayFavourPerHour = 0.05f, prayHalfLifeHours = 1f, prayMaxPerDay = 0.09f };
            float first = ActivityRules.PrayerGain(0f, 1f, b);
            float second = ActivityRules.PrayerGain(1f, 1f, b);
            Assert.AreEqual(0.05f, first, 1e-4f);
            Assert.AreEqual(0.025f, second, 1e-4f);
            float allDay = ActivityRules.PrayerGain(0f, 12f, b);
            Assert.AreEqual(0.09f, allDay, 1e-4f, "capped");
            Assert.AreEqual(0f, ActivityRules.PrayerGain(12f, 1f, b), 1e-6f, "nothing more after the cap");
            // Many small steps add up to the same as one long kneel.
            float sum = 0f; for (int i = 0; i < 60; i++) sum += ActivityRules.PrayerGain(i / 60f, 1f / 60f, b);
            Assert.AreEqual(first, sum, 1e-3f);
            Assert.AreEqual(0f, ActivityRules.PrayerGain(0f, 0f, b));
        }

        [Test]
        public void Beats_ScoreByDistance_AndMissesCost()
        {
            var b = new ActivityBalance { beatZoneWidth = 0.2f, beatMissPenalty = 0.6f };
            Assert.AreEqual(1f, ActivityRules.BeatQuality(0.5f, 0.2f), 1e-5f);
            Assert.AreEqual(0.5f, ActivityRules.BeatQuality(0.6f, 0.2f), 1e-5f);
            Assert.Greater(ActivityRules.BeatQuality(0.65f, 0.2f), 0f); Assert.Less(ActivityRules.BeatQuality(0.65f, 0.2f), 0.5f);
            Assert.AreEqual(0f, ActivityRules.BeatQuality(0.0f, 0.2f));
            float perfect = ActivityRules.DayQuality(6f, 6, 6, b);
            float lazy = ActivityRules.DayQuality(3f, 3, 6, b);
            float sloppy = ActivityRules.DayQuality(3f, 6, 6, b);
            Assert.AreEqual(1f, perfect, 1e-5f);
            Assert.Less(lazy, sloppy, "three perfect beats and three ignored score below six half-good ones");
            Assert.AreEqual(0.5f, ActivityRules.DayQuality(0f, 0, 0, b));
        }

        [Test]
        public void Marker_PingPongs()
        {
            Assert.AreEqual(0f, ActivityRules.MarkerPosition(0f, 2f), 1e-5f);
            Assert.AreEqual(0.5f, ActivityRules.MarkerPosition(1f, 2f), 1e-5f);
            Assert.AreEqual(1f, ActivityRules.MarkerPosition(2f, 2f), 1e-5f);
            Assert.AreEqual(0.5f, ActivityRules.MarkerPosition(3f, 2f), 1e-5f);
            Assert.AreEqual(0f, ActivityRules.MarkerPosition(4f, 2f), 1e-5f);
        }
    }
}

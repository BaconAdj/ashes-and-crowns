using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class WeatherModelTests
    {
        [Test]
        public void SameSeed_SameWeatherHistory()
        {
            var b = new WeatherBalance();
            var a = new WeatherModel(b, new DeterministicRng(9));
            var c = new WeatherModel(b, new DeterministicRng(9));
            for (int i = 0; i < 200; i++)
            {
                a.AdvanceHours(3f, 1); c.AdvanceHours(3f, 1);
                Assert.AreEqual(a.Current, c.Current);
            }
        }

        [Test]
        public void ChangesOnlyAtTheInterval()
        {
            var b = new WeatherBalance { changeIntervalHours = 6f };
            var w = new WeatherModel(b, new DeterministicRng(2));
            int changes = 0; WeatherKind last = w.Current;
            for (int i = 0; i < 24; i++) { w.AdvanceHours(1f, 1); if (w.Current != last) { changes++; last = w.Current; } }
            Assert.LessOrEqual(changes, 4);
        }

        [Test]
        public void SnowIsAWinterThing()
        {
            var b = new WeatherBalance();
            int snowJan = 0, snowJul = 0;
            var w = new WeatherModel(b, new DeterministicRng(5));
            for (int i = 0; i < 2000; i++) { w.AdvanceHours(b.changeIntervalHours, 1); if (w.Current == WeatherKind.Snow) snowJan++; }
            w = new WeatherModel(b, new DeterministicRng(5));
            for (int i = 0; i < 2000; i++) { w.AdvanceHours(b.changeIntervalHours, 7); if (w.Current == WeatherKind.Snow) snowJul++; }
            Assert.Greater(snowJan, 50);
            Assert.AreEqual(0, snowJul);
        }

        [Test]
        public void Intensity_BlendsTowardTarget_WithoutOvershoot()
        {
            var b = new WeatherBalance();
            var w = new WeatherModel(b, new DeterministicRng(3));
            for (int i = 0; i < 500; i++)
            {
                w.AdvanceHours(0.1f, 1);
                Assert.GreaterOrEqual(w.Precipitation, 0f); Assert.LessOrEqual(w.Precipitation, 1f);
                Assert.GreaterOrEqual(w.FogDensityFactor, 0f);
            }
        }

        [Test]
        public void MonthOutOfRange_IsClamped()
        {
            var w = new WeatherModel(new WeatherBalance(), new DeterministicRng(1));
            Assert.DoesNotThrow(() => w.AdvanceHours(10f, 0));
            Assert.DoesNotThrow(() => w.AdvanceHours(10f, 13));
        }
    }
}

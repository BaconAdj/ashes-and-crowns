using AshesAndCrowns.Game;
using AshesAndCrowns.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AshesAndCrowns.Tests.PlayMode
{
    public class WorldClockTests
    {
        [Test]
        public void Tick_AdvancesCalendar_AndRaisesBells()
        {
            var go = new GameObject("clock");
            var clock = go.AddComponent<WorldClock>();
            var balance = new Balance();
            clock.Init(balance, new DeterministicRng(1));
            int bells = 0; string last = null;
            clock.BellRang += (i, n) => { bells++; last = n; };
            clock.Tick(balance.time.realSecondsPerGameHour * 3.5f); // 09:00 -> 12:30, Sext rings
            Assert.AreEqual(1, bells);
            Assert.AreEqual("Sext", last);
            Assert.AreEqual(12.5f, clock.Calendar.HourOfDay, 1e-2f);
            Assert.IsNotNull(clock.Weather);
            Object.DestroyImmediate(go);
        }
    }
}

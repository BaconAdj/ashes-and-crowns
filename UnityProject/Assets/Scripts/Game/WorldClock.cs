using System;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// Advances the Sim calendar and weather from real time. One per scene. Everything that depends on
    /// time of day or weather reads from here; nothing else owns a clock.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class WorldClock : MonoBehaviour
    {
        [Tooltip("Multiplier on balance.time.realSecondsPerGameHour for testing. 1 = normal.")]
        public float timeScale = 1f;

        public Calendar Calendar { get; private set; }
        public WeatherModel Weather { get; private set; }
        public bool IsReady => Calendar != null;

        public event Action<int, string> BellRang; // index, name

        private Balance _balance;

        public void Init(Balance balance, DeterministicRng rng, double? resumeTotalHours = null)
        {
            _balance = balance;
            Calendar = resumeTotalHours.HasValue ? Calendar.FromTotalHours(balance.time, resumeTotalHours.Value) : new Calendar(balance.time);
            Weather = new WeatherModel(balance.weather, rng.Fork(21));
            // Settle the weather so the first frame is not the constructor default.
            Weather.AdvanceHours(balance.weather.changeIntervalHours * 2f, Calendar.Month);
        }

        private void Update()
        {
            if (Calendar == null) return;
            Tick(Time.deltaTime * timeScale);
        }

        /// <summary>Restore from a save: exact calendar, weather spell and weather rng.</summary>
        public void Resume(double totalHours, WeatherSnapshot weather, ulong weatherRngState)
        {
            Calendar = Calendar.FromTotalHours(_balance.time, totalHours);
            Weather.Import(weather);
            Weather.RngState = weatherRngState;
        }

        /// <summary>Advance by real seconds. Public so tests can drive it without frames.</summary>
        public void Tick(float realSeconds)
        {
            if (realSeconds <= 0f) return;
            float hours = realSeconds / _balance.time.realSecondsPerGameHour;
            int rung = Calendar.AdvanceHours(hours);
            Weather.AdvanceHours(hours, Calendar.Month);
            if (rung > 0) BellRang?.Invoke(Calendar.LastBellRung, Calendar.LastBellRungName);
        }

        /// <summary>Jump the clock, for debugging and for the Phase 2 catch-up. Rings no bells.</summary>
        public void SkipHours(float hours)
        {
            Calendar.AdvanceHours(hours);
            Weather.AdvanceHours(hours, Calendar.Month);
        }
    }
}

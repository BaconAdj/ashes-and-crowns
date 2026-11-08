using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// Runs the body's slow clock: hunger per game hour; once a day infection rolls, disease, market restock,
    /// standing decay and the missed-mass check. Everything it touches lives in RunSession.Context.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class SurvivalSystem : MonoBehaviour
    {
        public WorldClock clock;
        public PlayerVitals vitals;
        public Transform home;      // sleeping within homeRadius counts as resting safely
        private Balance _balance;
        private bool _subscribed;
        public bool RestingAtHome;  // set by VillageLife while asleep
        public bool InCamp;         // Phase 8

        private void Start()
        {
            _balance = GameBootstrap.Instance.Balance;
            if (clock != null && !_subscribed) { clock.HoursPassed += OnHours; clock.DaysPassed += OnDays; _subscribed = true; }
        }

        private void OnDestroy() { if (clock != null && _subscribed) { clock.HoursPassed -= OnHours; clock.DaysPassed -= OnDays; } }

        private void OnHours(float hours)
        {
            var ctx = RunSession.Context;
            if (ctx == null || ctx.Needs == null || vitals == null || vitals.IsDead) return;
            ctx.Needs.Tick(hours, vitals.Body);
            if (vitals.Body.IsDead) vitals.NotifyDeadFromBody();
        }

        private void OnDays(int days)
        {
            var ctx = RunSession.Context;
            if (ctx == null || ctx.Needs == null || vitals == null) return;
            var rng = GameBootstrap.Instance.Rng.Fork(51);
            rng.State ^= (ulong)clock.Calendar.TotalHours * 2654435761UL;
            for (int d = 0; d < days && !vitals.Body.IsDead; d++)
            {
                vitals.Body.TickDay(rng);
                ctx.Disease.TickDay(vitals.Body, ctx.Outbreak, InCamp, RestingAtHome, rng);
                ctx.Market.TickDay();
                ctx.Standing.TickDay();
                ctx.Outbreak = Mathf.Max(0f, ctx.Outbreak - _balance.disease.settlementOutbreakDecayPerDay);
            }
            // Missed Sunday mass? Checked the day after a Sunday or feast.
            var cal = clock.Calendar;
            long today = Calendar.JulianDayNumber(cal.Year, cal.Month, cal.Day);
            long yesterday = today - 1;
            Feast f = Festivals.FeastOn(cal.Year, cal.Month, cal.Day == 1 ? cal.Day : cal.Day - 1);
            if ((yesterday + 1) % 7 == 0 && ctx.LastMassDay != yesterday && home != null) ctx.Standing.MissedMass();
            if (vitals.Body.IsDead) vitals.NotifyDeadFromBody();
        }
    }
}

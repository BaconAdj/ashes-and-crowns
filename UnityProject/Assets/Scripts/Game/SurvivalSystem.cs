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
        public VillageLife village; // for the current settlement and arrests
        private Balance _balance;
        private bool _subscribed;
        public bool RestingAtHome;  // set by VillageLife while asleep
        public bool InCamp;         // Phase 8
        /// <summary>Set during the daily tick; VillageLife performs the arrest from its own Update (never inside the clock event).</summary>
        public Settlement PendingArrest;

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
                ctx.Reputation?.TickDay();
                InCamp = ctx.Service != null && ctx.Service.Active && ctx.Service.InCamp;
                foreach (var co in ctx.Companies) if (co.ContractDaysLeft > 0) co.ContractDaysLeft--;
                var homeS = ctx.Home;
                if (ctx.PendingLevyAt == null && ctx.Service != null && !ctx.Service.Active && !ctx.Service.Deserter && ctx.Character.Rank <= Rank.Warrior && homeS != null && ctx.CurrentSettlementId == homeS.Id && rng.Chance(_balance.service.levyDemandChancePerDay)) ctx.PendingLevyAt = homeS;
                foreach (var s in ctx.Settlements) { if (s.Market != ctx.Market) s.Market.TickDay(); s.Outbreak = Mathf.Max(0f, s.Outbreak - _balance.disease.settlementOutbreakDecayPerDay); if (s.WantedHere > 0f) s.WantedHere = Mathf.Max(0f, s.WantedHere - _balance.crime.wantedDecayPerDay); }
                var here = ctx.Current;
                if (ctx.Crime != null && here != null && village != null && PendingArrest == null && ctx.Crime.TickDay(here, village.InChurch, rng)) PendingArrest = here;
                if (ctx.ActiveJob != null && ctx.ActiveJob.State == JobState.Taken && JobBoard.Today(clock.Calendar) > ctx.ActiveJob.DeadlineDay)
                { ctx.ActiveJob.State = JobState.Failed; ctx.Standing.DebtDefaulted(); ctx.Reputation.Record(Deed.BrokeContract); ctx.ActiveJob = null; }
            }
            // Missed Sunday mass? Checked the day after a Sunday or feast.
            var cal = clock.Calendar;
            long today = Calendar.JulianDayNumber(cal.Year, cal.Month, cal.Day);
            long yesterday = today - 1;
            if ((yesterday + 1) % 7 == 0 && ctx.LastMassDay != yesterday && home != null) ctx.Standing.MissedMass();
            if (vitals.Body.IsDead) vitals.NotifyDeadFromBody();
        }
    }
}

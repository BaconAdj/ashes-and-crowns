using System.Collections.Generic;
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
                if (ctx.Estate != null)
                {
                    var manor = ctx.FindSettlement(ctx.Estate.SettlementId);
                    var elog = new List<string>(2);
                    EstateRules.DayTick(ctx.Estate, manor, ctx.CurrentSettlementId == ctx.Estate.SettlementId, rng, _balance.lordship, elog);
                    ctx.PendingNotices.AddRange(elog);
                    if (++ctx.EstateDaysThisMonth >= 30) { ctx.EstateDaysThisMonth = 0; EstateRules.EndMonth(ctx.Estate); }
                    if (ctx.PendingDemand == Allegiance.Neutral && AllegianceRules.DemandArrives(ctx.Lordship, rng, _balance.lordship, out Allegiance from)) ctx.PendingDemand = from;
                }
                if (AllegianceRules.DayTick(ctx.Lordship, rng, _balance.lordship))
                {
                    var here = ctx.Current;
                    AllegianceRules.OnDiscovered(ctx.Lordship, ctx.Crime, ctx.Reputation, here != null ? here.Id : "road", JobBoard.Today(clock.Calendar), rng);
                    if (here != null) here.WantedHere = 1f;
                    ctx.PendingNotices.Add("Your courier was taken on the road with your letters on him. By tonight every hall in the shire knows whose seal was on them. That is treason, and the rope for it.");
                }
                if (ctx.Realm == null) ctx.Realm = RealmRules.Fresh(_balance.lordship);
                if (++ctx.DaysIntoSeason >= _balance.lordship.seasonDays)
                {
                    ctx.DaysIntoSeason = 0;
                    var rlog = new List<string>(4);
                    RealmRules.Season(ctx.Realm, ctx.Baronage, ctx.Settlements, rng, _balance.lordship, rlog);
                    ctx.Realm.Chronicle.AddRange(rlog); if (ctx.Realm.Chronicle.Count > 40) ctx.Realm.Chronicle.RemoveRange(0, ctx.Realm.Chronicle.Count - 40);
                    ctx.WarHeat = Mathf.Clamp01(rlog.Count / 3f);
                    ctx.PendingNotices.AddRange(rlog);
                }
                // the knife in the dark
                if (ctx.Plot == null) ctx.Plot = IntrigueRules.Roll(ctx.Character, ctx.Family, ctx.Baronage, ctx.Estate, ctx.Reputation.Notoriety, rng.Fork(5), _balance.intrigue);
                else
                {
                    ctx.Plot.DaysUntil--;
                    string w = IntrigueRules.Warning(ctx.Plot, ctx.Character, rng.Fork(6), _balance.intrigue);
                    if (w != null && ctx.PendingWhisper == null) ctx.PendingWhisper = w;
                }
                // war comes to the village
                var hereS = ctx.Current;
                if (hereS != null && ctx.Siege == null && ctx.PendingRaiders == 0)
                {
                    var ev = VillageEvents.Roll(hereS, ctx.WarHeat, rng.Fork(7), _balance.villageEvents);
                    if (ev != VillageEventKind.None) { string t = VillageEvents.Apply(ev, hereS, rng.Fork(8), _balance.villageEvents, out int raiders); ctx.PendingNotices.Add(t); if (raiders > 0) ctx.PendingRaiders = raiders; }
                }
                if (ctx.Castle != null && ctx.Castle.Active && ctx.PendingObjector == null && ctx.Siege == null && ctx.CurrentSettlementId == ctx.Castle.SiteSettlementId)
                {
                    string built = CastleRules.BuildDay(ctx.Castle, ctx.Character, ctx.Service.Side, ctx.Baronage, rng, _balance.siege, out MinorLord objector);
                    if (!string.IsNullOrEmpty(built)) ctx.PendingNotices.Add(built);
                    if (objector != null) ctx.PendingObjector = objector;
                    if (ctx.Castle.Stage == CastleStage.Done)
                    {
                        var site = ctx.FindSettlement(ctx.Castle.SiteSettlementId);
                        var castle = CastleRules.Complete(ctx.Castle, ctx.Service.Side, site != null ? site.Region : string.Empty);
                        castle.Market = new Market(_balance.economy, ctx.Items);
                        ctx.Settlements.Add(castle); ctx.PendingNewSettlement = castle;
                        ctx.Character.Rank = Rank.Lord; ctx.HomeSettlementId = castle.Id;
                        if (ctx.Estate == null) { ctx.Estate = EstateRules.Grant(castle, new NameGenerator(ctx.Names, rng.Fork(3)), rng.Fork(4)); if (ctx.Lordship.Declared == Allegiance.Neutral) ctx.Lordship.Declared = ctx.Service.Side; }
                        ctx.PendingNotices.Add($"You are lord of {castle.Name}. Nobody licensed it. Someone will mind.");
                    }
                }
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

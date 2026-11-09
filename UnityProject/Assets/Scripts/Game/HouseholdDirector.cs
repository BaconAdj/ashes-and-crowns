using System.Collections.Generic;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// A household knight's days: report at your lord's seat for a duty; escorts and messages are rides across the map with
    /// ambushes on the way; patrol and guard are a day each with their own trouble; the feast is a room full of knives.
    /// </summary>
    public sealed class HouseholdDirector : MonoBehaviour
    {
        public WorldClock clock;
        public HudController hud;
        public SaveManager saveManager;
        public VillageLife village;
        public EncounterDirector encounter;
        public PlayerVitals vitals;

        public bool IsOpen => _open;
        private bool _open;
        private Balance _balance;
        private Duty _pendingAfterFight;
        private Settlement _pendingArrival;

        private void Start() { _balance = GameBootstrap.Instance.Balance; }

        private void Update()
        {
            var ctx = RunSession.Context;
            if (_balance == null || ctx == null || _open || vitals == null || vitals.IsDead) return;
            if ((village != null && village.IsBusy) || (saveManager != null && saveManager.IsQuitScreenOpen)) return;
            if (hud != null && hud.DialogueOpen) return;
            if (_pendingArrival != null) { var a = _pendingArrival; _pendingArrival = null; if (ctx.Current == a) OnArrived(a); return; }
            var d = ctx.Duty;
            if (d != null && d.State == DutyState.Given && HouseholdRules.Overdue(d, JobBoard.Today(clock.Calendar)))
            {
                string t = HouseholdRules.Fail(d, ctx.Service, _balance.household); ctx.Duty = null;
                Save(); Show("Too late", t, ("...", null));
            }
        }

        private void LateUpdate()
        {
            if (_pendingAfterFight == null || encounter == null || encounter.InFight || vitals == null || vitals.IsDead) return;
            var d = _pendingAfterFight; _pendingAfterFight = null;
            Finish(d);
        }

        /// <summary>Is this settlement the seat of the player's patron?</summary>
        public bool AtPatronSeat(RunContext ctx, Settlement s)
        {
            if (s == null || ctx.Service.Role != ServiceRole.Household || !int.TryParse(ctx.Service.PatronId, out int pid)) return false;
            var lord = ctx.LordById(pid);
            return s.LordId == pid || (lord != null && lord.NearLocationId == s.Id);
        }

        /// <summary>From the village menu at the seat: ask for the day's duty.</summary>
        public void ReportForDuty()
        {
            var ctx = RunSession.Context; var s = ctx.Current;
            if (ctx.Duty != null && ctx.Duty.State == DutyState.Given) { Show("The hall", "You already have your orders: " + ctx.Duty.Text, ("...", null)); return; }
            int.TryParse(ctx.Service.PatronId, out int pid); var lord = ctx.LordById(pid);
            var rng = Rng(181);
            var duty = HouseholdRules.Assign(lord, s, ctx.Settlements, JobBoard.Today(clock.Calendar), new NameGenerator(ctx.Names, rng), rng, _balance.household);
            if (duty == null)
            {
                ctx.Service.WagesOwed = 0; ctx.Character.Coins += ctx.Service.WagePerDay;
                clock.SkipHours(24f);
                Save(); Show("The hall", "Nothing today. You eat at the low table, mend a strap, and listen to the steward complain about the price of candles.", ("...", null));
                return;
            }
            string who = lord != null ? lord.FullName : "Your lord";
            Show(who, duty.Text,
                ("Take the duty", () => { ctx.Duty = duty; Save(); if (duty.Kind == DutyKind.Patrol || duty.Kind == DutyKind.Guard || duty.Kind == DutyKind.Feast) DoDayDuty(duty); else hud.Toast($"Ride to {duty.ToName}. {duty.DueDay - JobBoard.Today(clock.Calendar)} days."); }),
                ("Refuse", () => { string t = HouseholdRules.Refuse(duty, ctx.Service, _balance.household); Save(); Show(who, t, ("...", null)); }));
        }

        private void DoDayDuty(Duty duty)
        {
            var ctx = RunSession.Context; var b = _balance.household; var rng = Rng(182);
            switch (duty.Kind)
            {
                case DutyKind.Patrol:
                    clock.SkipHours(10f);
                    if (rng.Chance(b.patrolBanditChance) && encounter != null) { _pendingAfterFight = duty; encounter.Ambush(rng.Range(2, 4), rng); hud.Toast("The lights in the wood were not charcoal burners."); return; }
                    Finish(duty); return;
                case DutyKind.Guard:
                    clock.SkipHours(14f);
                    if (rng.Chance(b.guardWhisperChance) && ctx.Plot != null) { string w = IntrigueRules.Warning(ctx.Plot, ctx.Character, rng, _balance.intrigue); if (w != null) hud.Whisper(w); }
                    Finish(duty); return;
                default:
                    clock.SkipHours(6f);
                    string t = HouseholdRules.Feast(ctx.Character, ctx.Service, rng, b, out _);
                    ctx.Character.Use(Skill.Leadership, _balance.character);
                    duty.State = DutyState.Done; ctx.Duty = null; ctx.Service.PatronFavour = Mathf.Min(1f, ctx.Service.PatronFavour + b.favourPerDuty);
                    Save(); Show("The feast", t, ("...", null)); return;
            }
        }

        /// <summary>Called when the player arrives in a settlement.</summary>
        public void OnArrived(Settlement s)
        {
            var ctx = RunSession.Context; var d = ctx.Duty; if (d == null || d.State != DutyState.Given || s == null) return;
            if (d.Kind != DutyKind.Escort && d.Kind != DutyKind.Message) return;
            if ((village != null && village.IsBusy) || (hud != null && hud.DialogueOpen)) { _pendingArrival = s; return; }   // another dialogue is up: settle this next frame
            var b = _balance.household; var rng = Rng(183);
            if (s.Id == d.ToId)
            {
                if (d.Kind == DutyKind.Message && s.Allegiance != ctx.Service.Side && s.Allegiance != Allegiance.Neutral && rng.Chance(b.messageCaptureChanceInHostileTown))
                {
                    CaptureOutcome o = ServiceRules.Capture(ctx.Character, ctx.Reputation, s.Allegiance, rng, _balance.service, out string cap);
                    string fail = HouseholdRules.Fail(d, ctx.Service, b); ctx.Duty = null;
                    if (o == CaptureOutcome.Sword) { Show("Taken at the gate", "They find the letter before you can eat it. " + cap, ("...", () => { vitals.Body.Drain(1f, "a spy's death at " + s.Name); vitals.NotifyDeadFromBody(); })); return; }
                    if (o == CaptureOutcome.Mercy) ctx.Inventory.Clear();
                    Save(); Show("Taken at the gate", "They find the letter before you can eat it. " + cap + "\n\n" + fail, ("...", null)); return;
                }
                Finish(d); return;
            }
            float chance = d.Kind == DutyKind.Escort ? b.escortAmbushChance : b.messageAmbushChance;
            if (encounter != null && !encounter.InFight && rng.Chance(chance * 0.5f)) { encounter.Ambush(rng.Range(2, 4), rng); hud.Toast(d.Kind == DutyKind.Escort ? "Men step out of the hedge for the carts." : "Riders. They know what you carry."); }
        }

        private void Finish(Duty d)
        {
            var ctx = RunSession.Context;
            string t = HouseholdRules.Complete(d, ctx.Service, ctx.Character, _balance.household); ctx.Duty = null;
            Save(); Show("Duty done", t, ("...", null));
        }

        private DeterministicRng Rng(uint stream) { var r = GameBootstrap.Instance.Rng.Fork(stream); r.State ^= (ulong)clock.Calendar.TotalHours; return r; }
        private void Save() => saveManager?.SaveNow(AffairsChoice.None);
        private void Show(string title, string body, params (string label, System.Action act)[] options)
        {
            _open = true;
            hud.ShowDialogue(title, body, options, i => { _open = false; hud.HideDialogue(); options[i].act?.Invoke(); });
        }
    }
}

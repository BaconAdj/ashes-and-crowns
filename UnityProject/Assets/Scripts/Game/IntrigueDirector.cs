using System.Collections.Generic;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// The knife in the dark. Plots are rolled daily by the survival tick; this shows the warnings (quietly), lets you act on
    /// a suspicion, and resolves the day it comes: poison, a knife, riders at the ford, an heir in your chair, a gate left open.
    /// </summary>
    public sealed class IntrigueDirector : MonoBehaviour
    {
        public WorldClock clock;
        public HudController hud;
        public SaveManager saveManager;
        public VillageLife village;
        public EncounterDirector encounter;
        public PlayerVitals vitals;

        public bool IsOpen => _open;

        private Balance _balance;
        private bool _open;

        private void Start() { _balance = GameBootstrap.Instance.Balance; }

        private void Update()
        {
            var ctx = RunSession.Context;
            if (_balance == null || ctx == null || _open || vitals == null || vitals.IsDead) return;
            if (village != null && village.IsBusy) return;
            if (saveManager != null && saveManager.IsQuitScreenOpen) return;
            if (hud != null && hud.DialogueOpen) return;
            if (ctx.PendingWhisper != null) { hud.Whisper(ctx.PendingWhisper); AudioStubs.Play("whisper_warning"); ctx.PendingWhisper = null; }
            if (ctx.PendingRaiders > 0 && encounter != null) { int n = ctx.PendingRaiders; ctx.PendingRaiders = 0; AudioStubs.Play("village_raid_screams"); encounter.Ambush(n, Rng(171)); hud.Toast("Raiders in the lanes. They have torches."); return; }
            if (ctx.Plot != null && ctx.Plot.DaysUntil <= 0) { ResolvePlot(); return; }
            hud.SetWatched(ctx.Plot != null && ctx.Plot.WarningsGiven >= 2);
        }

        private DeterministicRng Rng(uint stream) { var r = GameBootstrap.Instance.Rng.Fork(stream); r.State ^= (ulong)clock.Calendar.TotalHours; return r; }

        /// <summary>From the village menu: act on what you have noticed.</summary>
        public void Suspicions()
        {
            var ctx = RunSession.Context; var p = ctx.Plot; var b = _balance.intrigue;
            if (p == null || p.WarningsGiven == 0) { Show("Suspicions", "Nothing you can put a name to. Yet.", ("...", null)); return; }
            string body = "What you have noticed:\n- " + string.Join("\n- ", p.Warnings);
            var opts = new List<(string, System.Action)>
            {
                ($"Double the guard ({b.guardCostPennies} pennies)", () => Act(Counter.DoubleGuard, null)),
                ($"Keep a taster ({b.tasterCostPennies} pennies)", () => Act(Counter.Taster, null)),
            };
            foreach (var n in ctx.Family)
            {
                if (!n.Alive || n.Note == "sent away" || opts.Count >= 6) continue;
                var who = n;
                opts.Add(($"Confront {who.Name}", () => Act(Counter.Confront, who)));
            }
            opts.Add(("Do nothing and watch", null));
            Show("Suspicions", body, opts.ToArray());
        }

        private void Act(Counter action, Npc accused)
        {
            var ctx = RunSession.Context;
            string t = IntrigueRules.Act(ctx.Plot, action, ctx.Character, ctx.Family, ctx.Baronage, accused, Rng(172), _balance.intrigue, out bool ended);
            if (ended) { ctx.Plot = null; hud.SetWatched(false); }
            saveManager?.SaveNow(AffairsChoice.None);
            Show("Afterwards", t, ("...", null));
        }

        private void ResolvePlot()
        {
            var ctx = RunSession.Context; var p = ctx.Plot; ctx.Plot = null; hud.SetWatched(false);
            int garrison = ctx.Estate != null ? ctx.Estate.GarrisonMen : ctx.Garrison;
            PlotOutcome o = IntrigueRules.Resolve(p, ctx.Character, garrison, Rng(173), _balance.intrigue, out string text);
            switch (o)
            {
                case PlotOutcome.Dead:
                    Show("The night", text, ("...", () => { vitals.Body.Drain(1f, p.Kind == PlotKind.Poison ? "poison, at your own table" : "a knife in the dark"); vitals.NotifyDeadFromBody(); }));
                    return;
                case PlotOutcome.Wounded:
                    vitals.TakeHitAt(BodyPart.Torso, p.Kind == PlotKind.Poison ? 0.3f : 0.45f, p.Kind == PlotKind.Poison ? "poison" : "an assassin's knife");
                    break;
                case PlotOutcome.Nothing:   // ambush: fight it out on the road
                    if (encounter != null) { AudioStubs.Play("ambush_riders"); encounter.Ambush(3, Rng(174)); hud.Toast(text); return; }
                    break;
                case PlotOutcome.Lost:
                    if (ctx.Estate != null)
                    {
                        var s = ctx.FindSettlement(ctx.Estate.SettlementId);
                        if (s != null) { s.LordId = p.Plotter == PlotterKind.Vassal || p.Plotter == PlotterKind.Rival ? p.PlotterId : -1; }
                        ctx.Estate = null; if (ctx.Character.Rank == Rank.Lord) ctx.Character.Rank = Rank.Knight;
                        if (p.Plotter == PlotterKind.Relative) foreach (var n in ctx.Family) if (n.Id == p.PlotterId) { n.Disposition = -1f; n.Note = "took your hall"; }
                        text += " You leave by the postern with what you can carry. The hall is theirs.";
                        ctx.Reputation.Record(Deed.BrokeContract);
                    }
                    break;
                case PlotOutcome.Foiled:
                    if (p.Plotter == PlotterKind.Relative) foreach (var n in ctx.Family) if (n.Id == p.PlotterId) { n.Disposition = -1f; n.Note = "exposed as a plotter"; }
                    if (p.Plotter == PlotterKind.Vassal || p.Plotter == PlotterKind.Rival) { var l = ctx.LordById(p.PlotterId); if (l != null) l.FeudWith = -2; }
                    ctx.Service.Renown += 2;
                    break;
            }
            if (vitals.IsDead) return;
            saveManager?.SaveNow(AffairsChoice.None);
            Show("The night", text, ("...", null));
        }

        private void Show(string title, string body, params (string label, System.Action act)[] options)
        {
            _open = true;
            hud.ShowDialogue(title, body, options, i => { _open = false; hud.HideDialogue(); options[i].act?.Invoke(); });
        }
    }
}

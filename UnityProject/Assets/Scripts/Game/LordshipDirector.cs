using System.Collections.Generic;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// The hall: steward's accounts, policy, manor court, secret dealings, the king's council. Crises and demands arrive
    /// through the daily tick and are put to you here. Nothing happens in the estate without a decision you can be blamed for.
    /// </summary>
    public sealed class LordshipDirector : MonoBehaviour
    {
        public WorldClock clock;
        public HudController hud;
        public SaveManager saveManager;
        public VillageLife village;
        public SiegeDirector siege;
        public PlayerVitals vitals;

        public bool IsOpen => _open;

        private Balance _balance;
        private bool _open;
        private List<CourtMatter> _docket;
        private int _docketIndex;

        private void Start() { _balance = GameBootstrap.Instance.Balance; }

        private void Update()
        {
            var ctx = RunSession.Context;
            if (_balance == null || ctx == null || _open || vitals == null || vitals.IsDead) return;
            if ((village != null && village.IsBusy) || (siege != null && siege.IsOpen)) return;
            if (saveManager != null && saveManager.IsQuitScreenOpen) return;
            if (ctx.Estate != null && ctx.Estate.PendingCrisis != EstateCrisis.None && AtEstate(ctx)) { Crisis(); return; }
            if (ctx.Estate != null && ctx.PendingDemand != Allegiance.Neutral && AtEstate(ctx)) { Demand(); return; }
        }

        private bool AtEstate(RunContext ctx) => ctx.Estate != null && ctx.CurrentSettlementId == ctx.Estate.SettlementId;
        private Settlement EstateSettlement(RunContext ctx) => ctx.FindSettlement(ctx.Estate.SettlementId);
        private DeterministicRng Rng(uint stream) { var r = GameBootstrap.Instance.Rng.Fork(stream); r.State ^= (ulong)clock.Calendar.TotalHours; return r; }

        /// <summary>Make the player lord of a settlement (service grant, dowry, a finished castle).</summary>
        public void Grant(Settlement s, string how)
        {
            var ctx = RunSession.Context;
            ctx.Estate = EstateRules.Grant(s, new NameGenerator(ctx.Names, Rng(151)), Rng(152));
            s.LordId = CastleRules.PlayerLordId;
            if (ctx.Character.Rank < Rank.Lord) ctx.Character.Rank = Rank.Lord;
            ctx.HomeSettlementId = s.Id;
            if (ctx.Lordship.Declared == Allegiance.Neutral && ctx.Service.Side != Allegiance.Neutral) ctx.Lordship.Declared = ctx.Service.Side;
            s.Allegiance = ctx.Lordship.Declared;
            hud.Toast($"You are lord of {s.Name}" + (string.IsNullOrEmpty(how) ? "." : " " + how));
            saveManager?.SaveNow(AffairsChoice.None);
        }

        // ------------------------------------------------------------------ the hall
        public void Hall()
        {
            var ctx = RunSession.Context; var e = ctx.Estate; var s = EstateSettlement(ctx); if (e == null || s == null) return;
            string body = $"{e.Name}. {s.Population} souls, {e.GarrisonMen} spears, {e.Treasury} pennies in the chest. Granary: {(int)e.Granary} days. " +
                          $"Unrest: {Describe(e.Unrest)}{(e.InRevolt ? " (IN REVOLT)" : "")}. Steward {e.StewardName}: this month {e.IncomeThisMonth} in" + (e.SkimmedThisMonth > 0 && e.StewardHonesty < 0.6f ? ", and the tallies do not add up" : "") + $". Last court {e.DaysSinceCourt} days ago. Declared for {Side(ctx.Lordship.Declared)}.";
            var opts = new List<(string, System.Action)>
            {
                ($"Taxes: {e.Tax} (change)", () => Policy()),
                ("Hold manor court", () => Court()),
                ("Take silver from the chest (50 pennies)", () => { if (e.Treasury >= 50) { e.Treasury -= 50; ctx.Character.Coins += 50; hud.Toast("Fifty pennies in your purse."); } else hud.Toast("The chest is thin."); Save(); }),
                ("Hire ten spears for the garrison (30 pennies from the chest)", () => { if (e.Treasury >= 30) { e.Treasury -= 30; e.GarrisonMen += 10; hud.Toast($"Garrison: {e.GarrisonMen}."); } else hud.Toast("The chest is thin."); Save(); }),
            };
            if (e.InRevolt) opts.Add(("Put down the revolt with the garrison", () => { var rng = Rng(153); bool ok = rng.Chance(Mathf.Clamp01(e.GarrisonMen / 40f)); e.GarrisonMen -= rng.Range(1, 5); if (ok) { e.InRevolt = false; e.Unrest = 0.45f; s.Population = (int)(s.Population * 0.95f); ctx.Reputation.Record(Deed.Murder); Show("The revolt", "Twelve dead at the tithe barn and the ringleaders hanging from the lych-gate. It is quiet now. It will be quiet for a long time.", ("...", null)); } else Show("The revolt", "The garrison is driven back to the hall with stones. Tonight you sleep in mail.", ("...", null)); Save(); }));
            opts.Add(("Allegiance: declare or deal", () => Allegiances()));
            if (ctx.Character.Rank == Rank.Monarch) opts.Add(("Hold the king's council", () => Council()));
            opts.Add(("Leave the hall", null));
            Show("The hall of " + e.Name, body, opts.ToArray());
        }

        private void Policy()
        {
            var ctx = RunSession.Context; var e = ctx.Estate;
            Show("Policy", $"Taxes are {e.Tax}; justice is {e.Justice}. Light taxes quiet the villeins and starve the chest; harsh ones do the reverse. Harsh justice keeps the road safe and the priest unhappy.",
                ("Light taxes", () => { e.Tax = TaxPolicy.Light; Save(); Hall(); }), ("Customary taxes", () => { e.Tax = TaxPolicy.Customary; Save(); Hall(); }), ("Harsh taxes", () => { e.Tax = TaxPolicy.Harsh; Save(); Hall(); }),
                ($"Justice: {(e.Justice == JusticePolicy.Harsh ? "make it lenient" : e.Justice == JusticePolicy.Lenient ? "make it customary" : "make it harsh")}", () => { e.Justice = e.Justice == JusticePolicy.Harsh ? JusticePolicy.Lenient : e.Justice == JusticePolicy.Lenient ? JusticePolicy.Customary : JusticePolicy.Harsh; Save(); Hall(); }));
        }

        private void Court()
        {
            var ctx = RunSession.Context;
            _docket = ManorCourt.Sitting(_balance.lordship.courtCasesPerSitting, new NameGenerator(ctx.Names, Rng(154)), Rng(155));
            _docketIndex = 0;
            clock.SkipHours(3f);
            NextCase();
        }

        private void NextCase()
        {
            var ctx = RunSession.Context; var e = ctx.Estate;
            if (_docket == null || _docketIndex >= _docket.Count) { e.DaysSinceCourt = 0; Save(); Show("Court rises", "The reeve rolls up his sticks. The hall empties, arguing.", ("...", null)); return; }
            var m = _docket[_docketIndex++];
            Show($"Manor court: {m.Kind}", m.Text,
                ("Dismiss", () => Judge(m, Verdict.Dismiss)), ("Fine", () => Judge(m, Verdict.Fine)), ("Flog", () => Judge(m, Verdict.Flog)), ("Hang", () => Judge(m, Verdict.Hang)), ("Settle", () => Judge(m, Verdict.Settle)));
        }

        private void Judge(CourtMatter m, Verdict v)
        {
            var ctx = RunSession.Context;
            string t = ManorCourt.Judge(ctx.Estate, m, v, ctx.Reputation);
            if (v == Verdict.Hang && !(m.Kind == CourtCase.Theft && m.Pennies > 12)) ctx.Standing.FightWon();   // feared, not loved: standing counts it as a kind of victory
            Show("Verdict", t, ("Next", NextCase));
        }

        private void Crisis()
        {
            var ctx = RunSession.Context; var e = ctx.Estate; var s = EstateSettlement(ctx);
            var k = e.PendingCrisis; string[] o = EstateRules.Options(k);
            Show("Trouble on the manor", EstateRules.Describe(k, e),
                (o[0], () => Resolve(0)), (o[1], () => Resolve(1)), (o[2], () => Resolve(2)));
        }

        private void Resolve(int choice)
        {
            var ctx = RunSession.Context;
            string t = EstateRules.Resolve(ctx.Estate, EstateSettlement(ctx), choice, ctx.Character, ctx.Reputation, new NameGenerator(ctx.Names, Rng(156)), Rng(157), _balance.lordship);
            Save();
            Show("Afterwards", t, ("...", null));
        }

        private void Demand()
        {
            var ctx = RunSession.Context; var from = ctx.PendingDemand; ctx.PendingDemand = Allegiance.Neutral;
            Show("A messenger under a banner", $"A rider with {Side(from)}'s badge and a writ: {_balance.lordship.demandPennies} pennies toward the war, or the men to match, by the week's end. He waits in the yard with his hand on his sword.",
                ("Pay", () => { Show("The messenger", AllegianceRules.AnswerDemand(ctx.Lordship, from, true, ctx.Estate, ctx.Reputation, _balance.lordship), ("...", null)); Save(); }),
                ("Refuse", () => { Show("The messenger", AllegianceRules.AnswerDemand(ctx.Lordship, from, false, ctx.Estate, ctx.Reputation, _balance.lordship), ("...", null)); Save(); }));
        }

        private void Allegiances()
        {
            var ctx = RunSession.Context; var a = ctx.Lordship; var e = ctx.Estate;
            string body = $"You are declared for {Side(a.Declared)}. " + (a.SecretDeal != Allegiance.Neutral ? $"You have taken {Side(a.SecretDeal)}'s silver {a.DealsTaken} time(s). Suspicion: {Describe(a.Suspicion)}." : "Nobody has your secret letters, because there are none.") +
                $" The Crown thinks of you: {Describe01(ctx.Reputation.Get(Faction.Crown))}; the Angevins: {Describe01(ctx.Reputation.Get(Faction.Angevin))}.";
            var opts = new List<(string, System.Action)>();
            if (a.Declared != Allegiance.Stephen) opts.Add(("Declare for King Stephen", () => { AllegianceRules.Declare(a, Allegiance.Stephen, ctx.Reputation); SetEstateSide(); Save(); Hall(); }));
            if (a.Declared != Allegiance.Matilda) opts.Add(("Declare for the Empress", () => { AllegianceRules.Declare(a, Allegiance.Matilda, ctx.Reputation); SetEstateSide(); Save(); Hall(); }));
            if (a.Declared != Allegiance.Neutral) opts.Add(("Stand neutral (both sides will press you)", () => { a.Declared = Allegiance.Neutral; SetEstateSide(); Save(); Hall(); }));
            if (a.Declared == Allegiance.Stephen) opts.Add(($"Treat secretly with the Empress ({_balance.lordship.secretDealPennies} pennies)", () => { e.Treasury += AllegianceRules.DealSecretly(a, Allegiance.Matilda, _balance.lordship); Save(); Show("A letter by night", "A man you do not know takes your seal and leaves a purse. If it is found on him, you hang.", ("...", null)); }));
            if (a.Declared == Allegiance.Matilda) opts.Add(($"Treat secretly with the king ({_balance.lordship.secretDealPennies} pennies)", () => { e.Treasury += AllegianceRules.DealSecretly(a, Allegiance.Stephen, _balance.lordship); Save(); Show("A letter by night", "A man you do not know takes your seal and leaves a purse. If it is found on him, you hang.", ("...", null)); }));
            opts.Add(("Back", () => Hall()));
            Show("Allegiance", body, opts.ToArray());
        }

        private void SetEstateSide() { var ctx = RunSession.Context; var s = EstateSettlement(ctx); if (s != null) s.Allegiance = ctx.Lordship.Declared; }

        private void Council()
        {
            var ctx = RunSession.Context; var r = ctx.Realm; var b = _balance.lordship;
            RealmRules.Measure(r, ctx.Baronage, ctx.Settlements);
            MinorLord weakest = null; foreach (var l in ctx.Baronage) if (l.Alive && l.Declared == Allegiance.Stephen && (weakest == null || l.Loyalty < weakest.Loyalty)) weakest = l;
            string body = $"Treasury: {r.CrownTreasury} pennies. The Church leans {(r.ChurchFavour >= 0 ? "to you" : "to the Empress")} ({Describe01(r.ChurchFavour)}). Strength: yours {(int)r.StephenStrength}, hers {(int)r.MatildaStrength}. Towns: {r.SettlementsStephen} to {r.SettlementsMatilda}." + (weakest != null ? $" Least loyal: {weakest.FullName} ({Describe(1f - weakest.Loyalty)} restless)." : string.Empty);
            Show("The king's council", body,
                ($"Grant land to {(weakest != null ? weakest.FullName : "a baron")} ({b.grantLandCost})", () => Act(MonarchAct.GrantLand, weakest)),
                ($"Take {(weakest != null ? weakest.FullName : "a baron")}'s son as hostage", () => Act(MonarchAct.TakeHostage, weakest)),
                ($"Pardon {(weakest != null ? weakest.FullName : "a traitor")}", () => Act(MonarchAct.Pardon, weakest)),
                ("Debase the coinage", () => Act(MonarchAct.DebaseCoin, null)),
                ("Levy a tallage", () => Act(MonarchAct.Tallage, null)),
                ($"Appease the bishops ({b.appeaseChurchCost})", () => Act(MonarchAct.AppeaseChurch, null)),
                ("Rise", () => Hall()));
        }

        private void Act(MonarchAct act, MinorLord lord)
        {
            var ctx = RunSession.Context;
            string t = RealmRules.Act(ctx.Realm, act, lord, ctx.Baronage, _balance.lordship, Rng(158));
            Save();
            Show("Council", t, ("...", () => Council()));
        }

        private static string Side(Allegiance a) => a == Allegiance.Stephen ? "King Stephen" : a == Allegiance.Matilda ? "the Empress" : "nobody";
        private static string Describe(float v) => v < 0.2f ? "quiet" : v < 0.5f ? "muttering" : v < 0.75f ? "angry" : "dangerous";
        private static string Describe01(float v) => v < -0.3f ? "hostile" : v < 0f ? "cool" : v < 0.3f ? "indifferent" : "warm";
        private void Save() => saveManager?.SaveNow(AffairsChoice.None);

        private void Show(string title, string body, params (string label, System.Action act)[] options)
        {
            _open = true;
            hud.ShowDialogue(title, body, options, i => { _open = false; hud.HideDialogue(); options[i].act?.Invoke(); });
        }
    }
}

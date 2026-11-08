using System.Collections.Generic;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// The blockade, day by day, from whichever side you are on: the commander chooses, the grunt watches the captain choose,
    /// the castle-builder holds the walls. Storms load the siege scene; this picks up the pieces when you come back.
    /// </summary>
    public sealed class SiegeDirector : MonoBehaviour
    {
        public WorldClock clock;
        public HudController hud;
        public SaveManager saveManager;
        public VillageLife village;
        public PlayerVitals vitals;
        public SettlementSpawner spawner;

        public bool IsOpen => _open;

        private Balance _balance;
        private bool _open;
        private readonly List<string> _log = new List<string>(8);
        private readonly List<(string label, System.Action act)> _opts = new List<(string, System.Action)>(6);

        private void Start() { _balance = GameBootstrap.Instance.Balance; }

        private void Update()
        {
            var ctx = RunSession.Context;
            if (_balance == null || ctx == null || _open || vitals == null || vitals.IsDead) return;
            if (village != null && village.IsBusy) return;
            if (saveManager != null && saveManager.IsQuitScreenOpen) return;
            if (ctx.PendingNewSettlement != null && spawner != null) { spawner.AddSite(ctx.PendingNewSettlement, GameBootstrap.Instance.World.overworld.terrainSizeMetres); ctx.PendingNewSettlement = null; }
            if (ctx.PendingNotices.Count > 0) { var lines = string.Join("\n\n", ctx.PendingNotices); ctx.PendingNotices.Clear(); Show("Word reaches you", lines, ("...", null)); return; }
            if (ctx.PendingObjector != null) { Objection(ctx.PendingObjector); return; }
            if (ctx.Siege == null) return;
            if (ctx.Siege.Active && !ctx.Siege.InAssault) { Menu(); return; }
            if (!ctx.Siege.Active) Aftermath();
        }

        // ------------------------------------------------------------------ starting a siege
        public void BeginAsCommander(Settlement target, Allegiance side, int men)
        {
            var ctx = RunSession.Context;
            var lord = ctx.LordById(target.LordId);
            float relief = lord != null ? lord.Knights / 10f : 0.3f;
            ctx.Siege = SiegeRules.Begin(target, side, men, playerAttacking: true, playerCommands: true, relief, GameBootstrap.Instance.Rng.Fork(141), _balance.siege);
            ctx.Service.InCamp = true;
            saveManager?.SaveNow(AffairsChoice.None);
        }

        public void BeginAsGrunt(Settlement target)
        {
            var ctx = RunSession.Context;
            var lord = ctx.LordById(target.LordId);
            float relief = lord != null ? lord.Knights / 10f : 0.3f;
            int men = Mathf.Clamp(SiegeRules.Garrison(target, _balance.siege) * 4, 200, _balance.battle.soldiersPerSide);
            ctx.Siege = SiegeRules.Begin(target, ctx.Service.Side, men, playerAttacking: true, playerCommands: false, relief, GameBootstrap.Instance.Rng.Fork(142), _balance.siege);
            ctx.Service.InCamp = true;
            saveManager?.SaveNow(AffairsChoice.None);
        }

        private void Objection(MinorLord lord)
        {
            var ctx = RunSession.Context; var cb = ctx.Castle;
            int men = CastleRules.ObjectorMen(lord, _balance.siege);
            int fee = Mathf.Max(20, lord.Knights * 20);
            var opts = new List<(string, System.Action)>
            {
                ("Pull it down", () => { ctx.PendingObjector = null; ctx.Castle = null; hud.Toast("The ditch is filled in by evening. The silver is gone."); saveManager?.SaveNow(AffairsChoice.None); }),
                ("Hold what you built", () =>
                {
                    ctx.PendingObjector = null;
                    var stub = new Settlement { Id = "castle_" + cb.SiteSettlementId, Name = cb.Name, Type = SettlementType.Castle, Population = 30, Wealth = 0.8f, LordId = CastleRules.PlayerLordId, Nx = cb.Nx, Nz = cb.Nz };
                    var s = SiegeRules.Begin(stub, lord.Declared, men, playerAttacking: false, playerCommands: true, 0f, GameBootstrap.Instance.Rng.Fork(143), _balance.siege);
                    s.Defenders = Mathf.Max(5, ctx.Garrison + 5); s.DefenderFoodDays = 30f; s.LordId = lord.Id; s.DefenderMorale = 0.8f;
                    ctx.Siege = s; saveManager?.SaveNow(AffairsChoice.None);
                })
            };
            if (ctx.Character.Coins >= fee) opts.Insert(1, ($"Pay him {fee} pennies to look away", () => { ctx.Character.Coins -= fee; ctx.PendingObjector = null; cb.ObjectorLordId = -1; hud.Toast(lord.FullName + " takes the silver and rides home. For now."); saveManager?.SaveNow(AffairsChoice.None); }));
            Show(lord.FullName + " objects", $"{lord.FullName} rides in with {men} men and looks at your ditch. \"Whose licence? The king's? The Empress's? I thought not.\" You have {ctx.Garrison} hired spears and {ctx.Character.Coins} pennies.", opts.ToArray());
        }

        // ------------------------------------------------------------------ the daily menu
        private void Menu()
        {
            var ctx = RunSession.Context; var s = ctx.Siege; var b = _balance.siege;
            string title = (s.PlayerAttacking ? "The siege of " : "Besieged in ") + s.TargetName + $", day {s.Day}";
            _opts.Clear();
            if (s.PlayerAttacking && s.PlayerCommands)
            {
                _opts.Add(("Wait a day: let hunger work", () => Day(false)));
                _opts.Add(($"Offer terms: march out with their arms ({(int)(SiegeRules.TermsChance(s, SiegeTerms.MarchOut) * 100)} in a hundred)", () => Terms(SiegeTerms.MarchOut)));
                _opts.Add(($"Offer terms: yield as prisoners ({(int)(SiegeRules.TermsChance(s, SiegeTerms.Prisoners) * 100)} in a hundred)", () => Terms(SiegeTerms.Prisoners)));
                if (SiegeRules.CanStorm(s)) _opts.Add(("Storm the walls", Storm));
                _opts.Add(("Abandon the siege", () => { SiegeRules.Abandon(s); saveManager?.SaveNow(AffairsChoice.None); }));
                Show(title, SiegeRules.Describe(s, b), _opts.ToArray());
            }
            else if (s.PlayerAttacking)
            {
                _opts.Add(("Another day in the lines", () => Day(true)));
                _opts.Add(("Slip away in the night (desert)", () => { SiegeRules.Abandon(s); village?.DesertService(); }));
                Show(title, SiegeRules.Describe(s, b) + "\n\nThe captain decides. You dig, carry ladders, and wait.", _opts.ToArray());
            }
            else
            {
                string body = $"Day {s.Day}. {s.Attackers} outside under {(ctx.LordById(s.LordId)?.FullName ?? "a lord")}'s banner. {s.Defenders} of yours on the palisade. Food for {Mathf.Max(0, (int)s.DefenderFoodDays)} days. Sick: {(int)(s.DefenderSickness * 100)} in a hundred.";
                _opts.Add(("Hold another day", () => Day(false)));
                _opts.Add(("Sortie: open the gate and fight them outside", () => { s.GateBroken = true; Storm(); }));
                _opts.Add(("Yield the castle", () => { s.Outcome = SiegeOutcome.Surrendered; saveManager?.SaveNow(AffairsChoice.None); }));
                Show(title, body, _opts.ToArray());
            }
        }

        private void Day(bool grunt)
        {
            var ctx = RunSession.Context; var s = ctx.Siege; var b = _balance.siege;
            var rng = GameBootstrap.Instance.Rng.Fork(144); rng.State ^= (ulong)clock.Calendar.TotalHours;
            bool raining = clock != null && clock.Weather != null && clock.Weather.Precipitation > 0.2f;
            _log.Clear();
            if (grunt) _log.Add(ServiceRules.CampDay(ctx.Service, ctx.Character, ctx.Needs, rng, _balance.service));
            bool paid = s.PlayerAttacking ? !(grunt && ctx.Service.WagesOwed > 6) : ctx.Character.Coins >= s.Defenders;
            if (!s.PlayerAttacking) ctx.Character.Coins = Mathf.Max(0, ctx.Character.Coins - s.Defenders / 2);   // feeding hired spears
            if (s.PlayerAttacking) SiegeRules.BlockadeDay(s, raining, paid, rng, b, _log);
            else
            {
                // From inside: the same arithmetic with the roles swapped. Their captain decides whether to storm.
                var mirror = new SiegeState { Attackers = s.Attackers, Defenders = s.Defenders, DefenderFoodDays = s.DefenderFoodDays, DefenderMorale = s.DefenderMorale, DefenderSickness = s.DefenderSickness, AttackerSickness = s.AttackerSickness, Ladders = s.Ladders, RamDays = s.RamDays, TowerDays = s.TowerDays, RamBuilt = s.RamBuilt, TowerBuilt = s.TowerBuilt, ReliefStrength = 0f, Day = s.Day };
                SiegeRules.BlockadeDay(mirror, raining, true, rng, b, _log);
                s.Day = mirror.Day; s.Attackers = mirror.Attackers; s.Defenders = mirror.Defenders; s.DefenderFoodDays = mirror.DefenderFoodDays; s.DefenderSickness = mirror.DefenderSickness; s.AttackerSickness = mirror.AttackerSickness;
                s.DefenderMorale = Mathf.Max(0.3f, mirror.DefenderMorale); s.Ladders = mirror.Ladders; s.RamDays = mirror.RamDays; s.TowerDays = mirror.TowerDays; s.RamBuilt = mirror.RamBuilt; s.TowerBuilt = mirror.TowerBuilt;
                if (mirror.Outcome == SiegeOutcome.Surrendered && s.Defenders <= 0) s.Outcome = SiegeOutcome.Surrendered;
                else if (SiegeRules.CaptainAbandons(mirror)) { s.Outcome = SiegeOutcome.Abandoned; _log.Add("At dawn their camp is empty: they have had enough of the rain."); }
                else if (SiegeRules.CaptainStorms(mirror, b)) { _log.Add("Horns before light. They are coming over the wall."); }
            }
            clock.SkipHours(24f);
            if (vitals.IsDead) return;
            string text = _log.Count > 0 ? string.Join("\n", _log) : "Nothing. Rain on the tents, and the walls.";
            bool stormNow = !s.PlayerAttacking && s.Active && _log.Count > 0 && _log[_log.Count - 1].StartsWith("Horns");
            bool captainStorms = grunt && s.Active && SiegeRules.CaptainStorms(s, b);
            if (grunt && s.Active && !captainStorms && SiegeRules.CaptainOffersTerms(s, b)) { bool ok = SiegeRules.OfferTerms(s, SiegeTerms.MarchOut, rng, b); text += ok ? "\nThe captain sent a herald; they have accepted terms and will march out tomorrow." : "\nThe captain sent a herald. They laughed at him."; }
            if (grunt && s.Active && SiegeRules.CaptainAbandons(s)) { SiegeRules.Abandon(s); text += "\nThe captain has had enough. We march at first light."; }
            saveManager?.SaveNow(AffairsChoice.None);
            if (stormNow || captainStorms) Show(captainStorms ? "The captain gives the word" : "They are coming", text + "\n\n" + (captainStorms ? "Ladders up at dawn. You are in the second wave." : "Every man to the palisade."), ("To the walls", Storm));
            else Show("The day passes", text, ("...", null));
        }

        private void Terms(SiegeTerms terms)
        {
            var ctx = RunSession.Context; var s = ctx.Siege;
            var rng = GameBootstrap.Instance.Rng.Fork(145); rng.State ^= (ulong)clock.Calendar.TotalHours;
            bool ok = SiegeRules.OfferTerms(s, terms, rng, _balance.siege);
            clock.SkipHours(6f);
            saveManager?.SaveNow(AffairsChoice.None);
            Show("The herald returns", ok ? "They accept. Tomorrow the gate opens." : "They spat at his feet. The wall seems taller tonight.", ("...", null));
        }

        private void Storm()
        {
            var ctx = RunSession.Context; var s = ctx.Siege;
            s.InAssault = true;
            RunSession.PendingBattle = new BattleLaunch { Siege = true, PlayerAttacking = s.PlayerAttacking, AsGrunt = s.PlayerAttacking && !s.PlayerCommands, Side = s.PlayerAttacking ? s.Besieger : ctx.Service.Side, CompanyName = ctx.Service.CompanyName, CompanySize = 60 };
            village?.CarryAcrossScenes();
            saveManager?.SaveNow(AffairsChoice.None);
            SceneManager.LoadScene("Phase9_Siege");
        }

        // ------------------------------------------------------------------ afterwards
        private void Aftermath()
        {
            var ctx = RunSession.Context; var s = ctx.Siege; var b = _balance.siege;
            var rng = GameBootstrap.Instance.Rng.Fork(146); rng.State ^= (ulong)clock.Calendar.TotalHours;
            var target = ctx.FindSettlement(s.TargetId);
            bool taken = s.Outcome == SiegeOutcome.Stormed || s.Outcome == SiegeOutcome.Surrendered;
            if (s.PlayerAttacking && taken && target != null)
            {
                if (s.PlayerCommands)
                {
                    int patron = int.TryParse(ctx.Service.PatronId, out int pid) ? pid : -1;
                    if (s.Outcome == SiegeOutcome.Surrendered) ctx.Service.Renown += b.renownForSurrender;
                    Show(target.Name + " is yours to dispose of", $"{(s.Outcome == SiegeOutcome.Stormed ? "Your men are inside the walls and looking at you." : "The garrison files out under its terms and your men look at you.")} What is it to be?",
                        ("Hold it for your lord", () => { SiegeRules.ApplyFate(target, CastleFate.Hold, s.Besieger, patron, ctx.Reputation, b, rng); ctx.Service.PatronFavour = Mathf.Min(1f, ctx.Service.PatronFavour + 0.3f); Finish("Your lord's banner goes up over the gate. He will remember who put it there."); }),
                        ("Garrison it and keep the people quiet", () => { SiegeRules.ApplyFate(target, CastleFate.Garrison, s.Besieger, patron, ctx.Reputation, b, rng); ctx.Service.PatronFavour = Mathf.Min(1f, ctx.Service.PatronFavour + 0.2f); Finish("Forty men stay. The town pays its taxes to a new master and hates him quietly."); }),
                        ("Give it to the men: sack it", () => { int loot = SiegeRules.ApplyFate(target, CastleFate.Sack, s.Besieger, patron, ctx.Reputation, b, rng); int share = Mathf.RoundToInt(loot * b.commanderLootShare); ctx.Character.Coins += share; Finish($"Three days of it. Your share is {share} pennies. The chroniclers will have a word for you."); }));
                }
                else
                {
                    bool sack = rng.Chance(0.6f);
                    int loot = SiegeRules.ApplyFate(target, sack ? CastleFate.Sack : CastleFate.Garrison, s.Besieger, -1, sack ? null : ctx.Reputation, b, rng);
                    int share = sack ? Mathf.Max(1, loot / Mathf.Max(1, s.Attackers)) : 0;
                    if (sack) Show("The sack of " + target.Name, "The captain lets the men loose. Doors going in, screaming in the lanes, a church on fire by noon.",
                        ($"Take your share ({share} pennies)", () => { ctx.Character.Coins += share; ctx.Reputation.Record(Deed.Sacked); Finish("You come out with silver and something you will not talk about."); }),
                        ("Take no part", () => { ctx.Reputation.Record(Deed.HelpedRefugees); Finish("You sit by the gate with your spear across your knees while it happens. A priest sees you.") ; }));
                    else Show(target.Name + " has fallen", "The captain keeps the men in hand; a garrison is left and the company marches on.", ("...", () => Finish(string.Empty)));
                }
                return;
            }
            if (!s.PlayerAttacking && taken)
            {
                // Your own walls, lost.
                var objector = ctx.LordById(s.LordId);
                if (ctx.Castle != null) ctx.Castle = null;
                var built = ctx.FindSettlement("castle_" + (s.TargetId.StartsWith("castle_") ? s.TargetId.Substring(7) : s.TargetId)) ?? target;
                if (built != null) { built.LordId = objector != null ? objector.Id : -1; built.Allegiance = objector != null ? objector.Declared : Allegiance.Neutral; }
                if (ctx.Estate != null && built != null && ctx.Estate.SettlementId == built.Id) ctx.Estate = null;
                if (ctx.Character.Rank == Rank.Lord) ctx.Character.Rank = Rank.Knight;
                ctx.Garrison = 0;
                CaptureOutcome o = ServiceRules.Capture(ctx.Character, ctx.Reputation, objector != null ? objector.Declared : Allegiance.Stephen, rng, _balance.service, out string capText);
                string text = (s.Outcome == SiegeOutcome.Stormed ? "They came over the palisade before the sun was up. " : "You opened the gate yourself. ") + capText;
                if (o == CaptureOutcome.Sword) { Finish(text); vitals.Body.Drain(1f, "the sword, at the gate of your own castle"); vitals.NotifyDeadFromBody(); return; }
                if (o == CaptureOutcome.Mercy) ctx.Inventory.Clear();
                Finish(text);
                return;
            }
            string why = s.Outcome == SiegeOutcome.Relieved ? (s.PlayerAttacking ? "A relieving army on the ridge: the siege is broken and the camp burns behind you as you go." : "Banners on the ridge behind them: someone came. They are gone by noon.")
                : s.PlayerAttacking ? "The siege is abandoned. Mud, debt, and nothing to show." : "They have gone home. Your ditch is still yours.";
            if (s.PlayerAttacking && s.PlayerCommands && s.Outcome != SiegeOutcome.Relieved) ctx.Service.PatronFavour = Mathf.Max(0f, ctx.Service.PatronFavour - 0.15f);
            Finish(why);
        }

        private void Finish(string text)
        {
            var ctx = RunSession.Context;
            ctx.Siege = null;
            ctx.Service.InCamp = ctx.Service.Active && ctx.Service.Role != ServiceRole.Household;
            saveManager?.SaveNow(AffairsChoice.None);
            if (!string.IsNullOrEmpty(text)) Show("Afterwards", text, ("...", null));
        }

        private void Show(string title, string body, params (string label, System.Action act)[] options)
        {
            _open = true;
            hud.ShowDialogue(title, body, options, i => { _open = false; hud.HideDialogue(); options[i].act?.Invoke(); });
        }
    }
}

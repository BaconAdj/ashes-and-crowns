using System.Collections.Generic;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// F10 in the England scene: a testing menu that jumps straight to late-phase content (coins, days, rank, service, estate,
    /// siege, raid, plot, levy, teleport). Development only; it is compiled out of release builds.
    /// </summary>
    public sealed class DevCheats : MonoBehaviour
    {
        public WorldClock clock;
        public HudController hud;
        public SaveManager saveManager;
        public VillageLife village;
        public SiegeDirector siege;
        public LordshipDirector lordship;
        public SettlementSpawner spawner;
        public PlayerController player;
        public HorseController horse;
        public PlayerVitals vitals;

        public bool IsOpen => _open;
        private bool _open;
        private Balance _balance;

        private void Start() { _balance = GameBootstrap.Instance.Balance; }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var kb = Keyboard.current; var ctx = RunSession.Context;
            if (kb == null || ctx == null || _open || vitals == null || vitals.IsDead) return;
            if ((village != null && village.IsBusy) || (saveManager != null && saveManager.IsQuitScreenOpen)) return;
            if (kb.f10Key.wasPressedThisFrame) Menu();
#endif
        }

        private void Menu()
        {
            var ctx = RunSession.Context; var c = ctx.Character; var s = ctx.Current;
            var opts = new List<(string, System.Action)>
            {
                ("+100 pennies", () => { c.Coins += 100; Done("Silver appears, as it does."); }),
                ("Skip a day", () => { clock.SkipHours(24f); Done("A day passes."); }),
                ("Skip thirty days", () => { clock.SkipHours(24f * 30f); Done("A month passes. Check the chronicle lines."); }),
                ("Make me a knight (mail, warhorse, renown, a patron)", () => { c.Rank = Rank.Knight; ctx.Inventory.Add("mail"); ctx.Inventory.Add("warhorse"); ctx.Inventory.Add("sword"); ctx.Service.Renown = Mathf.Max(ctx.Service.Renown, _balance.service.knighthoodRenown + 20); ctx.Service.PatronFavour = 1f; if (string.IsNullOrEmpty(ctx.Service.PatronId) && ctx.Baronage.Count > 0) ctx.Service.PatronId = ctx.Baronage[0].Id.ToString(); Done("Sir. Seek an audience at a castle for a command, land or an heiress."); }),
                ("Enlist with a company (Stephen's)", () => { ctx.Service.Enlist(ServiceRole.Mercenary, "the Free Company of the Road", "Walter Malet", Allegiance.Stephen, _balance.service.mercenaryWagePerDay); Done("Sellsword. Report to the muster at an alehouse."); }),
                ("Make this settlement my estate", () => { if (s != null && lordship != null) { lordship.Grant(s, "by the tester's grace."); Done("Press E near the alehouse: Go up to the hall."); } else Done("Stand in a settlement first."); }),
                ("Besiege the nearest hostile castle (as commander)", () => { var t = VillageLife.NearestHostileCastle(ctx, Allegiance.Stephen); if (t != null && siege != null) { c.Rank = c.Rank < Rank.Knight ? Rank.Knight : c.Rank; siege.BeginAsCommander(t, Allegiance.Stephen, 400); Done("The siege menu opens each day."); } else Done("No hostile castle on the map."); }),
                ("Raiders in the village now", () => { ctx.PendingRaiders = 3; Done("Torches."); }),
                ("A plot against me (warnings in two days)", () => { ctx.Plot = new Plot { Kind = PlotKind.Knife, Plotter = ctx.Family.Count > 0 ? PlotterKind.Relative : PlotterKind.Stranger, PlotterId = ctx.Family.Count > 0 ? ctx.Family[0].Id : -1, PlotterName = ctx.Family.Count > 0 ? ctx.Family[0].Name : "someone", DaysUntil = 4 }; if (ctx.Family.Count > 0) ctx.Family[0].Disposition = -0.6f; Done("Watch the italic line under your vitals over the next days."); }),
                ("The levy at my door", () => { if (ctx.Home != null) { ctx.PendingLevyAt = ctx.Home; Done("Armed men, when you are home."); } else Done("No home village."); }),
                ("Teleport to the nearest town or castle", () => Teleport(SettlementType.Town)),
                ("Teleport home", () => Teleport(SettlementType.Village)),
            };
            _open = true;
            hud.ShowDialogue("Tester's menu (F10)", $"{c.Name}, {c.Rank}, {c.Coins} pennies, day {ctx.DaysIntoSeason} of the season. Save format is v{AshesAndCrowns.Save.RunState.CurrentVersion}.", opts.ToArray(), i => { _open = false; hud.HideDialogue(); opts[i].Item2?.Invoke(); });
        }

        private void Teleport(SettlementType atLeast)
        {
            var ctx = RunSession.Context; if (spawner == null) { Done("No spawner."); return; }
            SettlementSite best = null; float bd = float.MaxValue; Vector3 me = player.transform.position;
            foreach (var site in spawner.Sites)
            {
                var s = ctx.FindSettlement(site.settlementId); if (s == null) continue;
                bool ok = atLeast == SettlementType.Village ? s.Id == ctx.HomeSettlementId : s.Type >= atLeast;
                if (!ok) continue;
                Transform t = site.alehouse != null ? site.alehouse : site.centre != null ? site.centre : site.transform;
                float d = (t.position - me).sqrMagnitude; if (d < bd) { bd = d; best = site; }
            }
            if (best == null) { Done("Nowhere to go."); return; }
            Transform target = best.alehouse != null ? best.alehouse : best.centre != null ? best.centre : best.transform;
            Vector3 p = target.position + new Vector3(6f, 0.5f, 6f);
            var cc = player.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
            player.transform.position = p; if (cc != null) cc.enabled = true;
            if (horse != null && !horse.Mounted) horse.transform.position = p + new Vector3(4f, 0f, 0f);
            Done("You are at " + best.settlementId + ".");
        }

        private void Done(string text) { hud.Toast(text); saveManager?.SaveNow(AffairsChoice.None); }
    }
}

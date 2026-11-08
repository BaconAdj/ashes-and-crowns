using System.Text;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Game
{
    /// <summary>C: character and skills. F: family. B: the baronage. H: holdings and troops. Alt+D: the war. The same key closes; opening the quit screen closes them too.</summary>
    public sealed class InfoPanels : MonoBehaviour
    {
        public HudController hud;
        public SaveManager saveManager;
        public ModeManager modes;            // optional: no panels while commanding (C is charge there)
        private int _open = -1;
        private readonly StringBuilder _sb = new StringBuilder(2048);

        public bool IsOpen => _open >= 0;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || RunSession.Context == null) return;
            if (saveManager != null && saveManager.IsQuitScreenOpen) { if (IsOpen) { _open = -1; hud.HideInfoPanel(); } return; }
            if (modes != null && modes.Mode == GameMode.Command) return;
            if (kb.cKey.wasPressedThisFrame) Toggle(0);
            else if (kb.fKey.wasPressedThisFrame) Toggle(1);
            else if (kb.bKey.wasPressedThisFrame) Toggle(2);
            else if (kb.hKey.wasPressedThisFrame && !(modes != null && modes.Mode == GameMode.Command)) Toggle(3);
            else if (kb.dKey.wasPressedThisFrame && kb.leftAltKey.isPressed) Toggle(4);
        }

        private void Toggle(int panel)
        {
            if (_open == panel) { _open = -1; hud.HideInfoPanel(); return; }
            _open = panel;
            switch (panel)
            {
                case 0: hud.ShowInfoPanel("Character", CharacterText()); break;
                case 1: hud.ShowInfoPanel("Family", FamilyText()); break;
                case 3: hud.ShowInfoPanel("Holdings and troops", HoldingsText()); break;
                case 4: hud.ShowInfoPanel("The war", WarText()); break;
                default: hud.ShowInfoPanel("The Baronage", BaronageText()); break;
            }
        }

        private string CharacterText()
        {
            var c = RunSession.Context.Character; _sb.Clear();
            _sb.Append(c.Name).Append(", ").Append(c.Rank.ToString().ToLowerInvariant());
            if (!string.IsNullOrEmpty(c.Trade)) _sb.Append(" (").Append(c.Trade).Append(')');
            _sb.Append(", aged ").Append(c.AgeYears).Append(".  ").Append(c.Coins).Append(" pennies.");
            if (c.Wanted) _sb.Append("  Wanted.");
            _sb.Append("\n\nStrength ").Append(c.Get(Attribute.Strength)).Append("   Endurance ").Append(c.Get(Attribute.Endurance))
               .Append("   Charisma ").Append(c.Get(Attribute.Charisma)).Append("   Cunning ").Append(c.Get(Attribute.Cunning)).Append("\n\n");
            foreach (Skill s in Skills.All)
            {
                float v = c.Get(s);
                if (v < 0.5f) continue;
                _sb.Append(Skills.DisplayName(s)).Append(' ').Append(v.ToString("F0")).Append("   ");
            }
            var ctx = RunSession.Context;
            if (ctx.Reputation != null)
            {
                _sb.Append("\n\nCrown: ").Append(ctx.Reputation.Describe(Faction.Crown)).Append("   Angevins: ").Append(ctx.Reputation.Describe(Faction.Angevin))
                   .Append("   Church: ").Append(ctx.Reputation.Describe(Faction.Church)).Append("   Commons: ").Append(ctx.Reputation.Describe(Faction.Commons));
                if (ctx.Reputation.Notoriety > 0.05f) _sb.Append("\nNotoriety ").Append((ctx.Reputation.Notoriety * 100f).ToString("F0")).Append('%');
                if (ctx.Crime != null && ctx.Crime.Outlawed) _sb.Append("\nYOU ARE OUTLAWED.");
                if (ctx.ActiveJob != null && ctx.ActiveJob.State == JobState.Taken) { var to = ctx.FindSettlement(ctx.ActiveJob.ToId); _sb.Append("\nWork: ").Append(JobBoard.KindName(ctx.ActiveJob.Kind)).Append(" to ").Append(to != null ? to.Name : ctx.ActiveJob.ToId).Append(", ").Append(ctx.ActiveJob.Pay).Append(" pennies."); }
                if (ctx.Needs != null) _sb.Append("\nBelly ").Append((ctx.Needs.Hunger * 100f).ToString("F0")).Append("%   rest ").Append((ctx.Needs.Rest * 100f).ToString("F0")).Append('%');
            }
            return _sb.ToString();
        }

        private string HoldingsText()
        {
            var ctx = RunSession.Context; _sb.Clear();
            var sv = ctx.Service;
            _sb.Append("Purse: ").Append(ctx.Character.Coins).Append(" pennies.\n");
            if (sv.Active) _sb.Append("Service: ").Append(sv.Role).Append(" with ").Append(sv.CompanyName).Append(" for ").Append(sv.Side).Append(", ").Append(sv.WagePerDay).Append("d a day, ").Append(sv.WagesOwed).Append(" owed, renown ").Append(sv.Renown).Append(", discipline marks ").Append(sv.Infractions.ToString("F0")).Append(".\n");
            else _sb.Append("No service. Renown ").Append(sv.Renown).Append(".\n");
            if (ctx.Estate != null)
            {
                var e = ctx.Estate; var s = ctx.FindSettlement(e.SettlementId);
                _sb.Append("\nESTATE: ").Append(e.Name).Append(s != null ? ", " + s.Population + " souls" : "").Append(".\nChest ").Append(e.Treasury).Append("d, granary ").Append((int)e.Granary).Append(" days, garrison ").Append(e.GarrisonMen)
                   .Append(" spears, taxes ").Append(e.Tax).Append(", justice ").Append(e.Justice).Append(".\nUnrest ").Append((e.Unrest * 100f).ToString("F0")).Append('%').Append(e.InRevolt ? " IN REVOLT" : "").Append(". Steward ").Append(e.StewardName).Append(": ").Append(e.IncomeThisMonth).Append("d in this month.\n");
            }
            if (ctx.Castle != null && ctx.Castle.Active) _sb.Append("\nBUILDING: ").Append(ctx.Castle.Name).Append(", ").Append(ctx.Castle.Stage).Append(" stage, ").Append(ctx.Castle.CoinSpent).Append("d spent, ").Append(ctx.Garrison).Append(" hired spears.\n");
            if (ctx.Siege != null) _sb.Append("\nSIEGE: ").Append(ctx.Siege.TargetName).Append(", day ").Append(ctx.Siege.Day).Append(", ").Append(ctx.Siege.Attackers).Append(" outside, ").Append(ctx.Siege.Defenders).Append(" within.\n");
            if (ctx.Suit != null && !ctx.Suit.Married) _sb.Append("\nCourting ").Append(ctx.Suit.Heiress).Append(": favour ").Append((ctx.Suit.Favour * 100f).ToString("F0")).Append("%.\n");
            if (ctx.Inventory != null)
            {
                _sb.Append("\nCARRIED: ");
                foreach (string id in new[] { "sword", "spear", "axe", "knife", "bow", "shield", "helm", "gambeson", "mail", "warhorse" }) { int n = ctx.Inventory.Count(id); if (n > 0) _sb.Append(id).Append(n > 1 ? " x" + n : "").Append("  "); }
            }
            return _sb.ToString();
        }

        private string WarText()
        {
            var ctx = RunSession.Context; _sb.Clear();
            var a = ctx.Lordship;
            _sb.Append("You are declared for ").Append(a.Declared == Allegiance.Stephen ? "King Stephen" : a.Declared == Allegiance.Matilda ? "the Empress" : "nobody").Append('.');
            if (a.SecretDeal != Allegiance.Neutral) _sb.Append(" You have taken the other side's silver ").Append(a.DealsTaken).Append(" time(s).");
            if (ctx.Realm != null)
            {
                var r = ctx.Realm; RealmRules.Measure(r, ctx.Baronage, ctx.Settlements);
                _sb.Append("\n\nThe king holds ").Append(r.SettlementsStephen).Append(" places, the Empress ").Append(r.SettlementsMatilda).Append(". Strength ").Append((int)r.StephenStrength).Append(" to ").Append((int)r.MatildaStrength)
                   .Append(". The Church leans ").Append(r.ChurchFavour >= 0.1f ? "to the king" : r.ChurchFavour <= -0.1f ? "to the Empress" : "nowhere").Append(". Seasons of war: ").Append(r.SeasonsElapsed).Append(".\n");
                int from = System.Math.Max(0, r.Chronicle.Count - 8);
                if (r.Chronicle.Count > 0) _sb.Append("\nLately:\n");
                for (int i = from; i < r.Chronicle.Count; i++) _sb.Append("- ").Append(r.Chronicle[i]).Append('\n');
            }
            else _sb.Append("\n\nNo news of the war yet.");
            return _sb.ToString();
        }

        private string FamilyText()
        {
            _sb.Clear();
            var fam = RunSession.Context.Family;
            if (fam.Count == 0) _sb.Append("You have no one.");
            foreach (var n in fam)
                _sb.Append(n.Name).Append(", your ").Append(FamilyGenerator.Describe(n.Relation, n.Female)).Append(", ").Append(n.Age)
                   .Append(n.Alive ? "" : " (dead)").Append(". ").Append(n.TraitA).Append(", ").Append(n.TraitB).Append(".\n");
            return _sb.ToString();
        }

        private string BaronageText()
        {
            _sb.Clear();
            var ctx = RunSession.Context;
            if (ctx.Figures != null)
            {
                foreach (var f in ctx.Figures.major) _sb.Append(f.name).Append(", ").Append(f.title).Append(".\n");
                _sb.Append('\n');
            }
            foreach (var l in ctx.Baronage)
                _sb.Append(l.FullName).Append(" of ").Append(l.SeatName).Append(", ").Append(l.Knights).Append(" knights, declared for ")
                   .Append(l.Declared == Allegiance.Neutral ? "no one" : l.Declared.ToString()).Append(". ").Append(l.TraitA).Append(", ").Append(l.TraitB).Append(".\n");
            return _sb.ToString();
        }
    }
}

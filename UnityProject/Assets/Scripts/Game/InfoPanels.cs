using System.Text;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Game
{
    /// <summary>C: character and skills. F: family. B: the baronage and the great figures. The same key closes; opening the quit screen closes them too.</summary>
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
        }

        private void Toggle(int panel)
        {
            if (_open == panel) { _open = -1; hud.HideInfoPanel(); return; }
            _open = panel;
            switch (panel)
            {
                case 0: hud.ShowInfoPanel("Character", CharacterText()); break;
                case 1: hud.ShowInfoPanel("Family", FamilyText()); break;
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

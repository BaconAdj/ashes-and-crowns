using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
using System.Collections.Generic;
using AshesAndCrowns.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace AshesAndCrowns.UI
{
    /// <summary>
    /// UI Toolkit HUD built entirely in code (no UXML asset dependency yet). Phase 0 shows blood, stamina,
    /// wounds, mode and hints, army counts, a threat indicator, fps, and the death screen.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudController : MonoBehaviour
    {
        private static readonly Color Parchment = new Color(0.78f, 0.74f, 0.66f);
        private static readonly Color Blood = new Color(0.45f, 0.08f, 0.07f);
        private static readonly Color Iron = new Color(0.35f, 0.37f, 0.40f);
        private static readonly Color Panel = new Color(0.04f, 0.04f, 0.045f, 0.72f);

        private Balance _balance;
        private PlayerVitals _vitals;
        private MeleeController _melee;
        private BattleDirector _director;
        private CommandController _command;

        private VisualElement _root, _bloodFill, _staminaFill, _hitFlash, _death, _chronicle, _quit;
        private Label _chronicleTitle, _chronicleBody, _quitTitle;
        private VisualElement _quitButtons;
        private Label _wounds, _mode, _hint, _armies, _threat, _fps, _deathText, _commandSummary, _clock, _bellToast;
        private float _bellToastUntil;
        private float _nextTextUpdate;
        private float _fpsAccum; private int _fpsFrames; private float _fpsValue;
        private float _vantageDistance;
        private string _modeName = "On foot";

        public void Init(Balance balance, PlayerVitals vitals, MeleeController melee, BattleDirector director, CommandController command)
        {
            _balance = balance; _vitals = vitals; _melee = melee; _director = director; _command = command;
            Build();
        }

        public void SetVantageDistance(float metresOutsideRadius) => _vantageDistance = metresOutsideRadius;

        /// <summary>Date line under the army panel. Call at low cadence (strings allocate).</summary>
        public void SetClock(string text) { if (_clock != null) _clock.text = text; }

        public void ShowBell(string bellName)
        {
            if (_bellToast == null) return;
            _bellToast.text = "The bells ring " + bellName + ".";
            _bellToast.style.display = DisplayStyle.Flex;
            _bellToastUntil = Time.unscaledTime + 6f;
        }

        public void SetMode(System.Enum mode)
        {
            _modeName = mode.ToString();
            ApplyModeText();
        }

        private void ApplyModeText()
        {
            if (_mode == null) return;
            switch (_modeName)
            {
                case "Command":
                    _mode.text = "IN COMMAND";
                    _hint.text = "Left-click a banner to select a unit. Right-click: move. C: charge. H: hold. 1-4: formation. Arrows pan, wheel zooms.\nWalk or ride off the hill and you lose command.";
                    break;
                case "Riding":
                    _mode.text = "RIDING";
                    _hint.text = "W/S speed, A/D steer, Shift gallop, E dismount. LMB swing, RMB block.";
                    break;
                case "Dead":
                    _mode.text = "DEAD";
                    _hint.text = string.Empty;
                    break;
                default:
                    _mode.text = "ON FOOT";
                    _hint.text = "WASD move, Shift sprint, E mount the horse. LMB swing, RMB block. Esc to leave your affairs.";
                    break;
            }
        }

        public void ShowDeath(string cause, double daysSurvived, int kills, int menLost, IReadOnlyList<string> chronicle, bool neverWoke)
        {
            if (_death == null) return;
            HideChronicle(); HideQuitScreen();
            _death.style.display = DisplayStyle.Flex;
            var sb = new System.Text.StringBuilder(512);
            if (chronicle != null) { foreach (string line in chronicle) sb.Append(line).Append('\n'); sb.Append('\n'); }
            sb.Append(neverWoke ? "You did not wake.\n\n" : "Here ends the life of a nameless rider.\n\n");
            sb.Append("Cause of death: ").Append(cause).Append('\n');
            sb.Append("Days survived: ").Append(daysSurvived.ToString("F1")).Append("    Men you killed: ").Append(kills).Append("    Men lost under your banner: ").Append(menLost).Append("\n\n");
            sb.Append("The war did not notice. The save is gone.\n\n[ R ]  Begin Again");
            _deathText.text = sb.ToString();
        }

        public void ShowChronicle(string title, IReadOnlyList<string> lines)
        {
            if (_chronicle == null) return;
            _chronicleTitle.text = title;
            var sb = new System.Text.StringBuilder(512);
            foreach (string line in lines) sb.Append(line).Append("\n\n");
            sb.Append("(press any key)");
            _chronicleBody.text = sb.ToString();
            _chronicle.style.display = DisplayStyle.Flex;
        }

        public void HideChronicle() { if (_chronicle != null) _chronicle.style.display = DisplayStyle.None; }

        public void ShowQuitScreen(string title, IList<(string label, AffairsChoice choice)> options, System.Action<AffairsChoice> onChoose, System.Action onStay)
        {
            if (_quit == null) return;
            _quitTitle.text = title;
            _quitButtons.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                var (label, choice) = options[i];
                var b = MakeButton($"{i + 1}.  {label}");
                b.clicked += () => onChoose(choice);
                _quitButtons.Add(b);
            }
            var stay = MakeButton("Stay (Esc)");
            stay.clicked += () => onStay();
            _quitButtons.Add(stay);
            _quit.style.display = DisplayStyle.Flex;
        }

        public void HideQuitScreen() { if (_quit != null) _quit.style.display = DisplayStyle.None; }

        private void Build()
        {
            var doc = GetComponent<UIDocument>();
            _root = doc.rootVisualElement;
            _root.Clear();
            _root.style.flexGrow = 1;
            _root.pickingMode = PickingMode.Ignore;

            // Hit flash
            _hitFlash = new VisualElement { pickingMode = PickingMode.Ignore };
            _hitFlash.style.position = Position.Absolute; _hitFlash.style.left = 0; _hitFlash.style.right = 0; _hitFlash.style.top = 0; _hitFlash.style.bottom = 0;
            _hitFlash.style.backgroundColor = new Color(0.5f, 0f, 0f, 0f);
            _root.Add(_hitFlash);

            // Top-left: vitals
            var vitalsPanel = MakePanel(16, 16, null, null, 300);
            vitalsPanel.Add(MakeLabel("BLOOD", 11, Parchment));
            _bloodFill = MakeBar(vitalsPanel, Blood);
            vitalsPanel.Add(MakeLabel("STAMINA", 11, Parchment));
            _staminaFill = MakeBar(vitalsPanel, Iron);
            _wounds = MakeLabel("No wounds.", 12, Parchment); _wounds.style.whiteSpace = WhiteSpace.Normal;
            vitalsPanel.Add(_wounds);
            _root.Add(vitalsPanel);

            // Top-right: mode + hint
            var modePanel = MakePanel(16, null, 16, null, 520);
            _mode = MakeLabel("ON FOOT", 18, Parchment); _mode.style.unityFontStyleAndWeight = FontStyle.Bold;
            _hint = MakeLabel(string.Empty, 12, Parchment); _hint.style.whiteSpace = WhiteSpace.Normal;
            modePanel.Add(_mode); modePanel.Add(_hint);
            _root.Add(modePanel);

            // Top-centre: armies / order banner slot
            var armyPanel = MakePanel(16, null, null, null, 420);
            armyPanel.style.left = new Length(50, LengthUnit.Percent); armyPanel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            armyPanel.style.alignItems = Align.Center;
            _armies = MakeLabel("Your men: --   Enemy: --", 14, Parchment);
            _commandSummary = MakeLabel(string.Empty, 12, Parchment); _commandSummary.style.whiteSpace = WhiteSpace.Normal;
            _threat = MakeLabel(string.Empty, 13, new Color(0.8f, 0.55f, 0.45f));
            _clock = MakeLabel(string.Empty, 12, new Color(0.7f, 0.66f, 0.58f)); _clock.style.whiteSpace = WhiteSpace.Normal; _clock.style.unityTextAlign = TextAnchor.MiddleCenter;
            _bellToast = MakeLabel(string.Empty, 15, Parchment); _bellToast.style.unityFontStyleAndWeight = FontStyle.Italic; _bellToast.style.display = DisplayStyle.None;
            armyPanel.Add(_armies); armyPanel.Add(_commandSummary); armyPanel.Add(_threat); armyPanel.Add(_clock); armyPanel.Add(_bellToast);
            _root.Add(armyPanel);

            // Bottom-right: fps
            var perfPanel = MakePanel(null, null, 16, 16, 180);
            _fps = MakeLabel("-- fps", 12, Parchment);
            perfPanel.Add(_fps);
            _root.Add(perfPanel);

            // Death screen
            _death = new VisualElement { pickingMode = PickingMode.Ignore };
            _death.style.position = Position.Absolute; _death.style.left = 0; _death.style.right = 0; _death.style.top = 0; _death.style.bottom = 0;
            _death.style.backgroundColor = new Color(0.02f, 0.02f, 0.02f, 0.92f);
            _death.style.alignItems = Align.Center; _death.style.justifyContent = Justify.Center;
            _death.style.display = DisplayStyle.None;
            _deathText = MakeLabel(string.Empty, 20, Parchment);
            _deathText.style.whiteSpace = WhiteSpace.Normal; _deathText.style.unityTextAlign = TextAnchor.MiddleCenter; _deathText.style.maxWidth = 720;
            _death.Add(_deathText);
            _root.Add(_death);

            // Chronicle ("While you slept...")
            _chronicle = MakeOverlay(0.85f);
            _chronicleTitle = MakeLabel(string.Empty, 24, Parchment); _chronicleTitle.style.unityFontStyleAndWeight = FontStyle.Bold; _chronicleTitle.style.marginBottom = 18;
            _chronicleBody = MakeLabel(string.Empty, 17, Parchment); _chronicleBody.style.whiteSpace = WhiteSpace.Normal; _chronicleBody.style.maxWidth = 760; _chronicleBody.style.unityTextAlign = TextAnchor.MiddleCenter;
            _chronicle.Add(_chronicleTitle); _chronicle.Add(_chronicleBody);
            _root.Add(_chronicle);

            // Quit screen ("Leave your affairs")
            _quit = MakeOverlay(0.8f);
            _quit.pickingMode = PickingMode.Position;
            _quitTitle = MakeLabel(string.Empty, 24, Parchment); _quitTitle.style.unityFontStyleAndWeight = FontStyle.Bold; _quitTitle.style.marginBottom = 6;
            var quitHint = MakeLabel("The world keeps moving while you are gone. How do you leave things?", 14, new Color(0.7f, 0.66f, 0.58f)); quitHint.style.marginBottom = 18;
            _quitButtons = new VisualElement(); _quitButtons.style.width = 560;
            _quit.Add(_quitTitle); _quit.Add(quitHint); _quit.Add(_quitButtons);
            _root.Add(_quit);

            ApplyModeText();
            _vitals.Wounded += (_, severity) => _hitFlash.style.backgroundColor = new Color(0.5f, 0f, 0f, Mathf.Clamp01(0.25f + severity * 2f));
        }

        private void Update()
        {
            if (_root == null || _vitals == null) return;
            // Bars every frame (cheap style writes), text at 8 Hz to avoid string churn.
            _bloodFill.style.width = new Length(Mathf.Clamp01(_vitals.Body.Blood) * 100f, LengthUnit.Percent);
            _staminaFill.style.width = new Length(Mathf.Clamp01(_vitals.Stamina / _vitals.StaminaMax) * 100f, LengthUnit.Percent);
            Color flash = _hitFlash.style.backgroundColor.value;
            if (flash.a > 0f) { flash.a = Mathf.Max(0f, flash.a - Time.deltaTime * 1.5f); _hitFlash.style.backgroundColor = flash; }

            _fpsAccum += Time.unscaledDeltaTime; _fpsFrames++;
            if (Time.unscaledTime < _nextTextUpdate) return;
            _nextTextUpdate = Time.unscaledTime + 0.125f;
            if (_fpsAccum > 0f) _fpsValue = _fpsFrames / _fpsAccum; _fpsAccum = 0f; _fpsFrames = 0;

            if (_bellToast.style.display == DisplayStyle.Flex && Time.unscaledTime > _bellToastUntil) _bellToast.style.display = DisplayStyle.None;
            if (_director != null && _director.IsReady)
            {
                _fps.text = $"{_fpsValue:F0} fps   {_director.PlayerAlive + _director.EnemyAlive} soldiers";
                _armies.text = $"Your men: {_director.PlayerAlive}   Enemy: {_director.EnemyAlive}   (lost {_director.PlayerLosses} / {_director.EnemyLosses})";
                _commandSummary.text = _command != null && _command.enabled ? _command.Summary : (_vantageDistance > 0f ? $"Command vantage {_vantageDistance:F0} m away" : string.Empty);
                int near = _director.CountWithin(_vitals.transform.position, _balance.melee.enemyEngagePlayerRange, Side.Enemy);
                _threat.text = near == 0 ? string.Empty : near == 1 ? "An enemy closes on you." : $"{near} enemies close on you.";
            }
            else
            {
                _fps.text = $"{_fpsValue:F0} fps";
                _armies.text = string.Empty; _commandSummary.text = string.Empty; _threat.text = string.Empty;
            }
            _wounds.text = WoundsText();
        }

        private string WoundsText()
        {
            var wounds = _vitals.Body.Wounds;
            if (wounds.Count == 0) return "No wounds.";
            var sb = new System.Text.StringBuilder(64);
            int shown = 0;
            for (int i = wounds.Count - 1; i >= 0 && shown < 4; i--, shown++)
            {
                var w = wounds[i];
                sb.Append(w.Severity > 0.5f ? "Grievous " : w.Severity > 0.2f ? "Deep " : "Shallow ");
                sb.Append(BodyParts.Name(w.Part));
                if (w.Bleeding > 0.02f) sb.Append(", bleeding");
                if (_vitals.Body.IsMaimed(w.Part)) sb.Append(" (maimed)");
                sb.Append('\n');
            }
            if (wounds.Count > 4) sb.Append("...and ").Append(wounds.Count - 4).Append(" more");
            return sb.ToString();
        }

        private static VisualElement MakeOverlay(float alpha)
        {
            var o = new VisualElement { pickingMode = PickingMode.Ignore };
            o.style.position = Position.Absolute; o.style.left = 0; o.style.right = 0; o.style.top = 0; o.style.bottom = 0;
            o.style.backgroundColor = new Color(0.02f, 0.02f, 0.02f, alpha);
            o.style.alignItems = Align.Center; o.style.justifyContent = Justify.Center;
            o.style.display = DisplayStyle.None;
            return o;
        }

        private static Button MakeButton(string text)
        {
            var b = new Button { text = text };
            b.style.fontSize = 16; b.style.color = Parchment; b.style.backgroundColor = new Color(0.12f, 0.11f, 0.10f, 0.95f);
            b.style.borderBottomColor = b.style.borderTopColor = b.style.borderLeftColor = b.style.borderRightColor = new Color(0.35f, 0.32f, 0.27f);
            b.style.borderBottomWidth = b.style.borderTopWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 1;
            b.style.paddingTop = b.style.paddingBottom = 10; b.style.paddingLeft = b.style.paddingRight = 16;
            b.style.marginBottom = 8; b.style.unityTextAlign = TextAnchor.MiddleLeft;
            return b;
        }

        private static VisualElement MakePanel(float? top, float? left, float? right, float? bottom, float width)
        {
            var p = new VisualElement { pickingMode = PickingMode.Ignore };
            p.style.position = Position.Absolute;
            if (top.HasValue) p.style.top = top.Value; if (left.HasValue) p.style.left = left.Value;
            if (right.HasValue) p.style.right = right.Value; if (bottom.HasValue) p.style.bottom = bottom.Value;
            p.style.width = width;
            p.style.backgroundColor = Panel;
            p.style.paddingLeft = p.style.paddingRight = 12; p.style.paddingTop = p.style.paddingBottom = 8;
            p.style.borderBottomLeftRadius = p.style.borderBottomRightRadius = p.style.borderTopLeftRadius = p.style.borderTopRightRadius = 2;
            return p;
        }

        private static Label MakeLabel(string text, int size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size; l.style.color = color; l.style.marginBottom = 2;
            return l;
        }

        private static VisualElement MakeBar(VisualElement parent, Color fill)
        {
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.height = 10; track.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f); track.style.marginBottom = 6;
            var f = new VisualElement { pickingMode = PickingMode.Ignore };
            f.style.height = new Length(100, LengthUnit.Percent); f.style.width = new Length(100, LengthUnit.Percent); f.style.backgroundColor = fill;
            track.Add(f); parent.Add(track);
            return f;
        }
    }
}

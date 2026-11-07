using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
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

        private VisualElement _root, _bloodFill, _staminaFill, _hitFlash, _death;
        private Label _wounds, _mode, _hint, _armies, _threat, _fps, _deathText, _commandSummary;
        private float _nextTextUpdate;
        private float _fpsAccum; private int _fpsFrames; private float _fps;
        private float _vantageDistance;
        private string _modeName = "On foot";

        public void Init(Balance balance, PlayerVitals vitals, MeleeController melee, BattleDirector director, CommandController command)
        {
            _balance = balance; _vitals = vitals; _melee = melee; _director = director; _command = command;
            Build();
        }

        public void SetVantageDistance(float metresOutsideRadius) => _vantageDistance = metresOutsideRadius;

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
                    _hint.text = "WASD move, Shift sprint, E mount the horse. LMB swing, RMB block. Esc frees the mouse.";
                    break;
            }
        }

        public void ShowDeath(string cause, float daysSurvived, int kills, int menLost)
        {
            if (_death == null) return;
            _death.style.display = DisplayStyle.Flex;
            _deathText.text =
                "Here ends the life of a nameless rider.\n\n" +
                $"Cause of death: {cause}\n" +
                $"Battles: 1    Men you killed: {kills}    Men lost under your banner: {menLost}\n" +
                $"Time on the field: {daysSurvived * 24f * 60f:F0} minutes of a single grey day\n\n" +
                "The war did not notice.\n\n[ R ]  Begin Again";
        }

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
            armyPanel.Add(_armies); armyPanel.Add(_commandSummary); armyPanel.Add(_threat);
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
            if (_fpsAccum > 0f) _fps = _fpsFrames / _fpsAccum; _fpsAccum = 0f; _fpsFrames = 0;

            _fps.text = $"{_fps:F0} fps   {_director.PlayerAlive + _director.EnemyAlive} soldiers";
            _armies.text = $"Your men: {_director.PlayerAlive}   Enemy: {_director.EnemyAlive}   (lost {_director.PlayerLosses} / {_director.EnemyLosses})";
            _commandSummary.text = _command != null && _command.enabled ? _command.Summary : (_vantageDistance > 0f ? $"Command vantage {_vantageDistance:F0} m away" : string.Empty);
            int near = _director.CountWithin(_vitals.transform.position, _balance.melee.enemyEngagePlayerRange, Side.Enemy);
            _threat.text = near == 0 ? string.Empty : near == 1 ? "An enemy closes on you." : $"{near} enemies close on you.";
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

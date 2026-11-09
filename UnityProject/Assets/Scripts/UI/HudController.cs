using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
using System.Collections.Generic;
using AshesAndCrowns.Sim;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

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

        private VisualElement _root, _bloodFill, _staminaFill, _hungerFill, _hitFlash, _death, _chronicle, _quit, _narration, _info, _dialogue, _market, _moment, _momentTrack, _momentMarker, _momentZone;
        private Label _orderBanner, _orderShout, _prompt, _standing, _toast, _dialogueTitle, _dialogueBody, _marketTitle, _marketMsg, _momentTitle, _momentHint;
        private VisualElement _dialogueButtons, _marketList, _mapOverlay, _mapImage;
        private Label _mapCaption;
        private float _toastUntil;
        private System.Action<float> _momentDone; private float _momentT, _momentSpeed, _momentZoneWidth; private int _momentBeats; private float _momentQuality;
        private Label _narrationText, _infoTitle, _infoBody;
        private Label _chronicleTitle, _chronicleBody, _quitTitle;
        private VisualElement _quitButtons;
        private Label _wounds, _mode, _hint, _armies, _threat, _fps, _deathText, _commandSummary, _clock, _bellToast, _whisper, _watched;
        private float _whisperUntil;
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
            _bellToast.text = L.T("The bells ring ") + bellName + ".";
            _bellToast.style.display = DisplayStyle.Flex;
            _bellToastUntil = Time.unscaledTime + 6f;
        }

        public void SetMode(System.Enum mode)
        {
            _modeName = mode.ToString();
            ApplyModeText();
        }

        public void SetMode(string modeName) { _modeName = modeName; ApplyModeText(); }

        private void ApplyModeText()
        {
            if (_mode == null) return;
            switch (_modeName)
            {
                case "Command":
                    _mode.text = L.T("IN COMMAND");
                    _hint.text = L.T("Left-click a banner to select a unit. Right-click: move. C: charge. H: hold. 1-4: formation. Arrows pan, wheel zooms.\nWalk or ride off the hill and you lose command.");
                    break;
                case "Riding":
                    _mode.text = L.T("RIDING");
                    _hint.text = L.T("W/S speed, A/D steer, Shift gallop, E dismount. LMB swing, RMB block.");
                    break;
                case "Ranks":
                    _mode.text = L.T("IN THE RANKS");
                    _hint.text = L.T("You do not command. Watch the banner and the sergeant. Hold your place in the ring; break ranks and the provost will hear of it.\nLMB swing, RMB block, Space dodge.");
                    break;
                case "Dead":
                    _mode.text = L.T("DEAD");
                    _hint.text = string.Empty;
                    break;
                default:
                    _mode.text = L.T("ON FOOT");
                    _hint.text = L.T("WASD move, Shift sprint, E mount the horse. LMB swing, RMB block. Esc to leave your affairs.");
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
            var stay = MakeButton(L.T("Stay (Esc)"));
            stay.clicked += () => onStay();
            _quitButtons.Add(stay);
            _quit.style.display = DisplayStyle.Flex;
        }

        public void ShowNarration(string line, float blackness)
        {
            if (_narration == null) return;
            _narration.style.display = DisplayStyle.Flex;
            _narration.style.backgroundColor = new Color(0f, 0f, 0f, Mathf.Clamp01(0.25f + blackness * 0.75f));
            _narrationText.text = line;
        }

        public void HideNarration() { if (_narration != null) _narration.style.display = DisplayStyle.None; }

        public void ShowInfoPanel(string title, string body)
        {
            if (_info == null) return;
            _infoTitle.text = title; _infoBody.text = body;
            _info.style.display = DisplayStyle.Flex;
        }

        public void HideInfoPanel() { if (_info != null) _info.style.display = DisplayStyle.None; }

        public void ShowMap(Texture2D map, IReadOnlyList<Settlement> settlements, Vector2 playerNormalized, string highlightId)
        {
            if (_mapOverlay == null) return;
            _mapImage.style.backgroundImage = new StyleBackground(map);
            _mapImage.Clear();
            foreach (var s in settlements)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                float size = s.Type == Sim.SettlementType.City ? 12f : s.Type == Sim.SettlementType.Town ? 9f : 6f;
                dot.style.position = Position.Absolute; dot.style.width = size; dot.style.height = size;
                dot.style.left = new Length(s.Nx * 100f, LengthUnit.Percent); dot.style.bottom = new Length(s.Nz * 100f, LengthUnit.Percent);
                dot.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(50, LengthUnit.Percent));
                dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = size;
                dot.style.backgroundColor = s.Id == highlightId ? new Color(0.85f, 0.7f, 0.3f) : s.Allegiance == Sim.Allegiance.Stephen ? new Color(0.25f, 0.3f, 0.5f) : s.Allegiance == Sim.Allegiance.Matilda ? new Color(0.55f, 0.2f, 0.18f) : new Color(0.2f, 0.2f, 0.2f);
                _mapImage.Add(dot);
                var name = new Label(s.Name) { pickingMode = PickingMode.Ignore };
                name.style.position = Position.Absolute; name.style.fontSize = s.Type >= Sim.SettlementType.Town ? 12 : 10; name.style.color = new Color(0.12f, 0.1f, 0.08f);
                name.style.left = new Length(s.Nx * 100f, LengthUnit.Percent); name.style.bottom = new Length(s.Nz * 100f, LengthUnit.Percent);
                name.style.translate = new Translate(8, 4);
                _mapImage.Add(name);
            }
            var you = new VisualElement { pickingMode = PickingMode.Ignore };
            you.style.position = Position.Absolute; you.style.width = 10; you.style.height = 10; you.style.backgroundColor = Parchment;
            you.style.left = new Length(playerNormalized.x * 100f, LengthUnit.Percent); you.style.bottom = new Length(playerNormalized.y * 100f, LengthUnit.Percent);
            you.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(50, LengthUnit.Percent));
            you.style.rotate = new Rotate(45f);
            _mapImage.Add(you);
            _mapCaption.text = L.T("Blue: for Stephen.  Red: for Matilda.  Gold: where your work takes you.  The pale mark is you.  (M closes)");
            _mapOverlay.style.display = DisplayStyle.Flex;
        }

        public void HideMap() { if (_mapOverlay != null) _mapOverlay.style.display = DisplayStyle.None; }

        /// <summary>The field after the battle: who broke, who died, and the names. Uses the chronicle overlay; any key closes it.</summary>
        public void ShowBattleReport(BattleReport r)
        {
            var lines = new List<string>(8) { r.Summary() };
            if (r.NamedDead.Count > 0) { lines.Add("Among the dead:"); int n = 0; foreach (string name in r.NamedDead) { if (n++ >= 12) { lines.Add("...and others whose names the sergeants knew."); break; } lines.Add(name); } }
            lines.Add("(press any key)");
            ShowChronicle(r.PlayerWon ? "The field is yours" : r.Draw ? "The field" : "The field is lost", lines);
            _reportOpen = true;
        }
        private bool _reportOpen;

        public void SetOrderBanner(string banner, string shout)
        {
            if (_orderBanner == null) return;
            _orderBanner.text = banner; _orderShout.text = shout;
            _orderBanner.style.display = string.IsNullOrEmpty(banner) && string.IsNullOrEmpty(shout) ? DisplayStyle.None : DisplayStyle.Flex;
            _orderShout.style.display = _orderBanner.style.display;
        }

        public void SetPrompt(string text) { if (_prompt != null && _prompt.text != text) _prompt.text = text; }
        public void SetHunger(float value01) { if (_hungerFill != null) _hungerFill.style.width = new Length(Mathf.Clamp01(value01) * 100f, LengthUnit.Percent); }
        public void SetStandingLine(string text) { if (_standing != null) _standing.text = text; }
        /// <summary>The paranoia line: a warning shown quietly, in italics, for a while. Attentive players read it; others see nothing.</summary>
        public void Whisper(string text) { if (_whisper == null) return; _whisper.text = text; _whisper.style.display = DisplayStyle.Flex; _whisperUntil = Time.unscaledTime + 9f; }
        public void SetWatched(bool on) { if (_watched != null) _watched.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; }

        public void Toast(string text) { if (_toast == null) return; _toast.text = text; _toast.style.display = DisplayStyle.Flex; _toastUntil = Time.unscaledTime + 4f; }

        public void ShowDialogue(string title, string body, (string label, System.Action act)[] options, System.Action<int> onPick)
        {
            if (_dialogue == null) return;
            _dialogueTitle.text = title; _dialogueBody.text = body; _dialogueButtons.Clear();
            for (int i = 0; i < options.Length; i++) { int idx = i; var b = MakeButton($"{i + 1}.  {options[i].label}"); b.clicked += () => onPick(idx); _dialogueButtons.Add(b); }
            _dialogue.style.display = DisplayStyle.Flex;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        /// <summary>True while any director's dialogue is on screen; the panel is shared, so nobody opens over somebody else.</summary>
        public bool DialogueOpen => _dialogue != null && _dialogue.style.display == DisplayStyle.Flex;

        public void HideDialogue()
        {
            if (_dialogue == null) return;
            _dialogue.style.display = DisplayStyle.None;
            if (_market.style.display != DisplayStyle.Flex) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        public void ShowMarket(Market market, ItemsData items, Inventory inventory, int month, float standing, System.Func<string, bool, string> onBuy, System.Func<string, string> onSell, System.Action onClose)
        {
            if (_market == null) return;
            _marketTitle.text = L.T("The market");
            _marketMsg.text = market.Shock != WarShock.None ? "Prices are strange: " + market.Shock + "." : "Prices are the usual, for the season.";
            void Rebuild()
            {
                _marketList.Clear();
                foreach (var g in market.Goods)
                {
                    var def = items.Find(g.Id); if (def == null) continue;
                    string id = g.Id;
                    var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.marginBottom = 2;
                    var name = MakeLabel($"{def.name}  (have {inventory.Count(id)}, stall {Mathf.RoundToInt(g.Stock)})", 13, Parchment); name.style.width = 330; row.Add(name);
                    int buy = market.BuyPrice(id, month, standing), sell = market.SellPrice(id, month, standing);
                    var bb = MakeButton($"Buy {buy}d"); bb.style.width = 100; bb.style.marginBottom = 0; bb.clicked += () => { _marketMsg.text = onBuy(id, false); Rebuild(); }; row.Add(bb);
                    var hb = MakeButton(L.T("Haggle")); hb.style.width = 90; hb.style.marginBottom = 0; hb.clicked += () => { _marketMsg.text = onBuy(id, true); Rebuild(); }; row.Add(hb);
                    var sb = MakeButton($"Sell {sell}d"); sb.style.width = 100; sb.style.marginBottom = 0; sb.clicked += () => { _marketMsg.text = onSell(id); Rebuild(); }; row.Add(sb);
                    _marketList.Add(row);
                }
                var close = MakeButton(L.T("Leave the market")); close.clicked += () => { _market.style.display = DisplayStyle.None; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; onClose(); };
                _marketList.Add(close);
            }
            Rebuild();
            _market.style.display = DisplayStyle.Flex;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        /// <summary>The hands-on moment: a marker sweeps a track; press Space inside the zone. Three beats. Quality 0..1.</summary>
        public void ShowMoment(string kind, string tradeId, System.Action<float> onDone)
        {
            if (_moment == null) { onDone(0.5f); return; }
            _momentDone = onDone; _momentBeats = 0; _momentQuality = 0f; _momentT = 0f;
            _momentSpeed = kind == "stalk" ? 0.45f : kind == "timing" ? 0.7f : 1.0f;
            _momentTitle.text = kind == "stalk" ? "Stalk the deer" : kind == "timing" ? "Judge the moment" : "Keep the rhythm";
            _momentHint.text = kind == "stalk" ? "Press Space when the marker is in the still patch. Three times." : "Press Space when the marker crosses the band. Three beats.";
            _momentZoneWidth = kind == "stalk" ? 0.14f : 0.18f;
            _momentZone.style.width = new Length(_momentZoneWidth * 100f, LengthUnit.Percent); _momentZone.style.left = new Length((0.5f - _momentZoneWidth / 2f) * 100f, LengthUnit.Percent);
            _moment.style.display = DisplayStyle.Flex;
        }

        private void UpdateMoment()
        {
            if (_moment == null || _momentDone == null) return;
            _momentT += Time.unscaledDeltaTime * _momentSpeed;
            float pos = Mathf.PingPong(_momentT, 1f);
            _momentMarker.style.left = new Length(pos * 100f, LengthUnit.Percent);
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                float d = Mathf.Abs(pos - 0.5f);
                float half = _momentZoneWidth / 2f;
                float beat = d <= half ? 1f - d / half * 0.5f : Mathf.Max(0f, 0.3f - d);
                _momentQuality += beat; _momentBeats++;
                _momentHint.text = beat > 0.6f ? "Good." : beat > 0.2f ? "Nearly." : "Clumsy.";
                if (_momentBeats >= 3)
                {
                    _moment.style.display = DisplayStyle.None;
                    var done = _momentDone; _momentDone = null;
                    done?.Invoke(Mathf.Clamp01(_momentQuality / 3f));
                }
            }
        }

        public void HideQuitScreen() { if (_quit != null) _quit.style.display = DisplayStyle.None; }

        private void Build()
        {
            var doc = GetComponent<UIDocument>();
            if (doc.panelSettings == null) Debug.LogError("[hud] the HUD's UIDocument has no Panel Settings: nothing on screen will render. Run Ashes and Crowns > Setup Project, then Rebuild the scene.");
            VisualElement panelRoot = doc.rootVisualElement;
            panelRoot.style.flexGrow = 1;
            panelRoot.pickingMode = PickingMode.Ignore;
            // The HUD owns one container under the panel root. Other screens (character creation) share
            // the same document, so never Clear() the panel root: whichever Start() runs first must survive.
            if (_root != null) _root.RemoveFromHierarchy();
            _root = new VisualElement { name = "hud", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute; _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;
            panelRoot.Insert(0, _root);

            // Hit flash
            _hitFlash = new VisualElement { pickingMode = PickingMode.Ignore };
            _hitFlash.style.position = Position.Absolute; _hitFlash.style.left = 0; _hitFlash.style.right = 0; _hitFlash.style.top = 0; _hitFlash.style.bottom = 0;
            _hitFlash.style.backgroundColor = new Color(0.5f, 0f, 0f, 0f);
            _root.Add(_hitFlash);

            // Top-left: vitals
            var vitalsPanel = MakePanel(16, 16, null, null, 300);
            vitalsPanel.Add(MakeLabel(L.T("BLOOD"), 11, Parchment));
            _bloodFill = MakeBar(vitalsPanel, Blood);
            vitalsPanel.Add(MakeLabel(L.T("STAMINA"), 11, Parchment));
            _staminaFill = MakeBar(vitalsPanel, Iron);
            _wounds = MakeLabel(L.T("No wounds."), 12, Parchment); _wounds.style.whiteSpace = WhiteSpace.Normal;
            vitalsPanel.Add(_wounds);
            _root.Add(vitalsPanel);

            // Top-right: mode + hint
            var modePanel = MakePanel(16, null, 16, null, 520);
            _mode = MakeLabel(L.T("ON FOOT"), 18, Parchment); _mode.style.unityFontStyleAndWeight = FontStyle.Bold;
            _hint = MakeLabel(string.Empty, 12, Parchment); _hint.style.whiteSpace = WhiteSpace.Normal;
            modePanel.Add(_mode); modePanel.Add(_hint);
            _root.Add(modePanel);

            // Top-centre: armies / order banner slot
            var armyPanel = MakePanel(16, null, null, null, 420);
            armyPanel.style.left = new Length(50, LengthUnit.Percent); armyPanel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            armyPanel.style.alignItems = Align.Center;
            _orderBanner = MakeLabel(string.Empty, 30, new Color(0.85f, 0.78f, 0.6f)); _orderBanner.style.unityFontStyleAndWeight = FontStyle.Bold; _orderBanner.style.display = DisplayStyle.None;
            _orderShout = MakeLabel(string.Empty, 13, Parchment); _orderShout.style.unityFontStyleAndWeight = FontStyle.Italic; _orderShout.style.display = DisplayStyle.None;
            armyPanel.Add(_orderBanner); armyPanel.Add(_orderShout);
            _armies = MakeLabel(L.T("Your men: --   Enemy: --"), 14, Parchment);
            _commandSummary = MakeLabel(string.Empty, 12, Parchment); _commandSummary.style.whiteSpace = WhiteSpace.Normal;
            _threat = MakeLabel(string.Empty, 13, new Color(0.8f, 0.55f, 0.45f));
            _clock = MakeLabel(string.Empty, 12, new Color(0.7f, 0.66f, 0.58f)); _clock.style.whiteSpace = WhiteSpace.Normal; _clock.style.unityTextAlign = TextAnchor.MiddleCenter;
            _bellToast = MakeLabel(string.Empty, 15, Parchment); _bellToast.style.unityFontStyleAndWeight = FontStyle.Italic; _bellToast.style.display = DisplayStyle.None;
            armyPanel.Add(_armies); armyPanel.Add(_commandSummary); armyPanel.Add(_threat); armyPanel.Add(_clock); armyPanel.Add(_bellToast);
            _root.Add(armyPanel);

            // Bottom-right: fps
            var perfPanel = MakePanel(null, null, 16, 16, 180);
            _fps = MakeLabel(L.T("-- fps"), 12, Parchment);
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
            var quitHint = MakeLabel(L.T("The world keeps moving while you are gone. How do you leave things?"), 14, new Color(0.7f, 0.66f, 0.58f)); quitHint.style.marginBottom = 18;
            _quitButtons = new VisualElement(); _quitButtons.style.width = 560;
            _quit.Add(_quitTitle); _quit.Add(quitHint); _quit.Add(_quitButtons);
            _root.Add(_quit);

            // Narration band (opening)
            _narration = MakeOverlay(0.9f);
            _narration.style.justifyContent = Justify.FlexEnd; _narration.style.paddingBottom = 90;
            _narrationText = MakeLabel(string.Empty, 22, Parchment); _narrationText.style.whiteSpace = WhiteSpace.Normal; _narrationText.style.maxWidth = 900; _narrationText.style.unityTextAlign = TextAnchor.MiddleCenter; _narrationText.style.unityFontStyleAndWeight = FontStyle.Italic;
            _narration.Add(_narrationText);
            _root.Add(_narration);

            // Info panel (character / family / baronage)
            _info = MakePanel(70, null, null, null, 760);
            _info.style.left = new Length(50, LengthUnit.Percent); _info.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _info.style.maxHeight = new Length(75, LengthUnit.Percent);
            _info.style.display = DisplayStyle.None;
            _infoTitle = MakeLabel(string.Empty, 18, Parchment); _infoTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _infoBody = MakeLabel(string.Empty, 13, Parchment); _infoBody.style.whiteSpace = WhiteSpace.Normal;
            var scroll = new ScrollView(); scroll.style.maxHeight = 520; scroll.Add(_infoBody);
            _info.Add(_infoTitle); _info.Add(scroll);
            _root.Add(_info);

            // Hunger bar under stamina, standing line, prompt, toast
            vitalsPanel.Add(MakeLabel(L.T("BELLY"), 11, Parchment));
            _hungerFill = MakeBar(vitalsPanel, new Color(0.45f, 0.38f, 0.2f));
            _standing = MakeLabel(string.Empty, 12, new Color(0.7f, 0.66f, 0.58f)); _standing.style.whiteSpace = WhiteSpace.Normal; vitalsPanel.Add(_standing);
            _whisper = MakeLabel(string.Empty, 12, new Color(0.62f, 0.58f, 0.66f)); _whisper.style.unityFontStyleAndWeight = FontStyle.Italic; _whisper.style.whiteSpace = WhiteSpace.Normal; _whisper.style.display = DisplayStyle.None; vitalsPanel.Add(_whisper);
            _watched = MakeLabel(L.T("You are watched."), 11, new Color(0.55f, 0.45f, 0.5f)); _watched.style.display = DisplayStyle.None; vitalsPanel.Add(_watched);
            var promptPanel = MakePanel(null, null, null, 70, 520); promptPanel.style.left = new Length(50, LengthUnit.Percent); promptPanel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0); promptPanel.style.alignItems = Align.Center;
            _prompt = MakeLabel(string.Empty, 15, Parchment); promptPanel.Add(_prompt);
            _toast = MakeLabel(string.Empty, 14, new Color(0.8f, 0.72f, 0.5f)); _toast.style.display = DisplayStyle.None; promptPanel.Add(_toast);
            _root.Add(promptPanel);

            // Dialogue
            _dialogue = MakeOverlay(0.55f); _dialogue.pickingMode = PickingMode.Position;
            var dbox = new VisualElement(); dbox.style.width = 700; dbox.style.backgroundColor = Panel; dbox.style.paddingTop = dbox.style.paddingBottom = 16; dbox.style.paddingLeft = dbox.style.paddingRight = 20;
            _dialogueTitle = MakeLabel(string.Empty, 20, Parchment); _dialogueTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _dialogueBody = MakeLabel(string.Empty, 14, Parchment); _dialogueBody.style.whiteSpace = WhiteSpace.Normal; _dialogueBody.style.marginBottom = 12;
            _dialogueButtons = new VisualElement();
            dbox.Add(_dialogueTitle); dbox.Add(_dialogueBody); dbox.Add(_dialogueButtons); _dialogue.Add(dbox);
            _root.Add(_dialogue);

            // Market
            _market = MakeOverlay(0.7f); _market.pickingMode = PickingMode.Position;
            var mbox = new VisualElement(); mbox.style.width = 720; mbox.style.backgroundColor = Panel; mbox.style.paddingTop = mbox.style.paddingBottom = 16; mbox.style.paddingLeft = mbox.style.paddingRight = 20;
            _marketTitle = MakeLabel(string.Empty, 20, Parchment); _marketTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _marketMsg = MakeLabel(string.Empty, 13, new Color(0.8f, 0.72f, 0.5f)); _marketMsg.style.marginBottom = 8;
            var mscroll = new ScrollView(); mscroll.style.maxHeight = 520; _marketList = new VisualElement(); mscroll.Add(_marketList);
            mbox.Add(_marketTitle); mbox.Add(_marketMsg); mbox.Add(mscroll); _market.Add(mbox);
            _root.Add(_market);

            // Hands-on moment
            _moment = MakeOverlay(0.35f);
            _momentTitle = MakeLabel(string.Empty, 22, Parchment); _momentTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _momentHint = MakeLabel(string.Empty, 14, Parchment); _momentHint.style.marginBottom = 14;
            _momentTrack = new VisualElement(); _momentTrack.style.width = 600; _momentTrack.style.height = 26; _momentTrack.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            _momentZone = new VisualElement(); _momentZone.style.position = Position.Absolute; _momentZone.style.top = 0; _momentZone.style.bottom = 0; _momentZone.style.backgroundColor = new Color(0.45f, 0.38f, 0.2f, 0.9f);
            _momentMarker = new VisualElement(); _momentMarker.style.position = Position.Absolute; _momentMarker.style.top = -4; _momentMarker.style.width = 6; _momentMarker.style.height = 34; _momentMarker.style.backgroundColor = Parchment;
            _momentTrack.Add(_momentZone); _momentTrack.Add(_momentMarker);
            _moment.Add(_momentTitle); _moment.Add(_momentHint); _moment.Add(_momentTrack);
            _root.Add(_moment);

            // Map
            _mapOverlay = MakeOverlay(0.85f);
            _mapImage = new VisualElement { pickingMode = PickingMode.Ignore };
            _mapImage.style.width = 720; _mapImage.style.height = 720; _mapImage.style.backgroundColor = new Color(0.16f, 0.17f, 0.19f);
            _mapCaption = MakeLabel(string.Empty, 12, Parchment); _mapCaption.style.marginTop = 8;
            _mapOverlay.Add(_mapImage); _mapOverlay.Add(_mapCaption);
            _root.Add(_mapOverlay);

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
            UpdateMoment();
            if (_toast != null && _toast.style.display == DisplayStyle.Flex && Time.unscaledTime > _toastUntil) _toast.style.display = DisplayStyle.None;
            if (_whisper != null && _whisper.style.display == DisplayStyle.Flex && Time.unscaledTime > _whisperUntil) _whisper.style.display = DisplayStyle.None;
            if (Time.unscaledTime < _nextTextUpdate) return;
            _nextTextUpdate = Time.unscaledTime + 0.125f;
            if (_fpsAccum > 0f) _fpsValue = _fpsFrames / _fpsAccum; _fpsAccum = 0f; _fpsFrames = 0;

            if (_bellToast.style.display == DisplayStyle.Flex && Time.unscaledTime > _bellToastUntil) _bellToast.style.display = DisplayStyle.None;
            if (_reportOpen) { var kb = UnityEngine.InputSystem.Keyboard.current; if (kb != null && kb.anyKey.wasPressedThisFrame) { _reportOpen = false; HideChronicle(); } }
            if (_director != null && _director.IsReady)
            {
                _fps.text = $"{_fpsValue:F0} fps   {_director.PlayerAlive + _director.EnemyAlive} soldiers";
                _armies.text = $"Your men: {_director.PlayerAlive}   Enemy: {_director.EnemyAlive}   (lost {_director.PlayerLosses} / {_director.EnemyLosses})";
                _commandSummary.text = _command != null && _command.enabled ? _command.Summary : (_vantageDistance > 0f ? $"Command vantage {_vantageDistance:F0} m away" : string.Empty);
                int near = _director.CountWithin(_vitals.transform.position, _balance.melee.enemyEngagePlayerRange, Side.Enemy);
                _threat.text = near == 0 ? string.Empty : near == 1 ? L.T("An enemy closes on you.") : $"{near} enemies close on you.";
            }
            else
            {
                _fps.text = $"{_fpsValue:F0} fps";
                _armies.text = string.Empty; _commandSummary.text = FightLine(); _threat.text = _melee != null ? _melee.LastEvent : string.Empty;
            }
            _wounds.text = WoundsText();
        }

        private string FightLine()
        {
            if (_melee == null || _melee.Duelists.Count == 0) return string.Empty;
            var sb = new System.Text.StringBuilder(96);
            foreach (var d in _melee.Duelists)
            {
                if (d == null) continue;
                sb.Append(d.displayName).Append(d.Dead ? " (dead)" : d.Yielded ? " (yields)" : $"  blood {Mathf.RoundToInt(d.Body.Blood * 100)}%").Append("   ");
            }
            if (_melee.IsBlocking) sb.Append("\nGuard: ").Append(_melee.Fighter.Dir);
            return sb.ToString();
        }

        private string WoundsText()
        {
            var wounds = _vitals.Body.Wounds;
            if (wounds.Count == 0) return L.T("No wounds.");
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

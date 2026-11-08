using System;
using System.Collections.Generic;
using AshesAndCrowns.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace AshesAndCrowns.UI
{
    /// <summary>Full character creation on one screen: name, sex, rank, background, attributes, appearance. Built in code.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CharacterCreationUI : MonoBehaviour
    {
        private static readonly Color Parchment = new Color(0.78f, 0.74f, 0.66f);
        private static readonly Color Dim = new Color(0.55f, 0.52f, 0.46f);
        private static readonly string[] AttributeNames = { "Strength", "Endurance", "Charisma", "Cunning" };
        private static readonly string[] Presets = { "Weathered", "Gaunt", "Broad", "Young" };

        private BackgroundsData _data;
        private Action<CharacterFactory.Choice> _onBegin;
        private VisualElement _root, _overlay, _backgroundList, _rankRow;
        private TextField _name;
        private Toggle _female;
        private Label _points, _error, _blurb;
        private readonly Label[] _attrValues = new Label[4];
        private Slider _height, _build, _skin, _hair;
        private readonly CharacterFactory.Choice _choice = new CharacterFactory.Choice();
        private readonly List<Button> _bgButtons = new List<Button>();
        private readonly List<Button> _rankButtons = new List<Button>();
        private readonly List<Button> _presetButtons = new List<Button>();

        public bool IsOpen => _overlay != null && _overlay.style.display == DisplayStyle.Flex;

        public void Show(BackgroundsData data, Action<CharacterFactory.Choice> onBegin)
        {
            _data = data; _onBegin = onBegin;
            if (_overlay == null) Build();
            _choice.Name = string.Empty; _choice.Female = false; _choice.Rank = Rank.Peasant; _choice.Allocation = new[] { data.attributeBase, data.attributeBase, data.attributeBase, data.attributeBase };
            _choice.AppearancePreset = 0; _choice.Height = 1.72f; _choice.Build = 0.5f; _choice.SkinTone = 0.5f; _choice.HairTone = 0.4f;
            _name.value = string.Empty; _female.value = false;
            SelectRank(Rank.Peasant);
            RefreshAttributes();
            _overlay.style.display = DisplayStyle.Flex;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void Hide() { if (_overlay != null) _overlay.style.display = DisplayStyle.None; }

        private void Build()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute; _overlay.style.left = 0; _overlay.style.right = 0; _overlay.style.top = 0; _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.03f, 0.03f, 0.035f, 0.96f);
            _overlay.style.alignItems = Align.Center; _overlay.style.justifyContent = Justify.FlexStart; _overlay.style.paddingTop = 24;
            _root.Add(_overlay);

            var title = L("One life in a collapsing kingdom", 26, Parchment); title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.marginBottom = 4;
            _overlay.Add(title);
            _overlay.Add(L("England, December 1135. Choose who you were before the war found you.", 13, Dim));

            var columns = new VisualElement(); columns.style.flexDirection = FlexDirection.Row; columns.style.marginTop = 16; columns.style.width = 1180;
            _overlay.Add(columns);
            var left = Column(columns, 360); var middle = Column(columns, 440); var right = Column(columns, 340);

            // ---- left: identity + rank
            left.Add(L("NAME", 11, Dim));
            _name = new TextField { maxLength = 32 }; _name.style.marginBottom = 8; _name.RegisterValueChangedCallback(e => _choice.Name = e.newValue);
            left.Add(_name);
            _female = new Toggle("Woman") ; _female.style.color = Parchment; _female.RegisterValueChangedCallback(e => _choice.Female = e.newValue);
            left.Add(_female);
            left.Add(L("RANK  (the only difficulty setting)", 11, Dim));
            _rankRow = new VisualElement(); left.Add(_rankRow);
            foreach (RankDef r in _data.ranks)
            {
                var rank = (Rank)Array.IndexOf(_data.ranks, r);
                var b = Btn(r.name); b.clicked += () => SelectRank(rank); _rankButtons.Add(b); _rankRow.Add(b);
            }

            // ---- middle: backgrounds
            middle.Add(L("BACKGROUND", 11, Dim));
            _backgroundList = new VisualElement(); middle.Add(_backgroundList);
            _blurb = L(string.Empty, 13, Parchment); _blurb.style.whiteSpace = WhiteSpace.Normal; _blurb.style.marginTop = 8; _blurb.style.minHeight = 70;
            middle.Add(_blurb);

            // ---- right: attributes + appearance
            right.Add(L("ATTRIBUTES", 11, Dim));
            _points = L(string.Empty, 12, Parchment); right.Add(_points);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.marginBottom = 4;
                var name = L(AttributeNames[i], 14, Parchment); name.style.width = 120; row.Add(name);
                var minus = Btn("-"); minus.style.width = 34; minus.clicked += () => Nudge(idx, -1); row.Add(minus);
                _attrValues[i] = L("4", 16, Parchment); _attrValues[i].style.width = 36; _attrValues[i].style.unityTextAlign = TextAnchor.MiddleCenter; row.Add(_attrValues[i]);
                var plus = Btn("+"); plus.style.width = 34; plus.clicked += () => Nudge(idx, +1); row.Add(plus);
                right.Add(row);
            }
            right.Add(L("APPEARANCE", 11, Dim));
            var presets = new VisualElement(); presets.style.flexDirection = FlexDirection.Row; presets.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < Presets.Length; i++) { int idx = i; var b = Btn(Presets[i]); b.style.width = 150; b.clicked += () => SelectPreset(idx); _presetButtons.Add(b); presets.Add(b); }
            right.Add(presets);
            _height = SliderRow(right, "Height", 1.55f, 1.92f, 1.72f, v => _choice.Height = v);
            _build = SliderRow(right, "Build", 0f, 1f, 0.5f, v => _choice.Build = v);
            _skin = SliderRow(right, "Skin", 0f, 1f, 0.5f, v => _choice.SkinTone = v);
            _hair = SliderRow(right, "Hair", 0f, 1f, 0.4f, v => _choice.HairTone = v);

            _error = L(string.Empty, 13, new Color(0.8f, 0.45f, 0.4f)); _error.style.marginTop = 10; _overlay.Add(_error);
            var begin = Btn("Begin this life"); begin.style.marginTop = 10; begin.style.width = 300; begin.style.fontSize = 18; begin.style.unityTextAlign = TextAnchor.MiddleCenter;
            begin.clicked += OnBeginClicked;
            _overlay.Add(begin);
            _overlay.Add(L("There is no save slot. When you die, this life is gone.", 12, Dim));
        }

        private void SelectRank(Rank rank)
        {
            _choice.Rank = rank;
            for (int i = 0; i < _rankButtons.Count; i++) Highlight(_rankButtons[i], i == (int)rank);
            _backgroundList.Clear(); _bgButtons.Clear();
            RankDef def = _data.Rank(rank);
            if (def == null) return;
            for (int i = 0; i < def.backgrounds.Length; i++)
            {
                BackgroundDef bg = def.backgrounds[i];
                var b = Btn(bg.name); b.clicked += () => SelectBackground(bg); _bgButtons.Add(b); _backgroundList.Add(b);
            }
            if (def.backgrounds.Length > 0) SelectBackground(def.backgrounds[0]);
        }

        private void SelectBackground(BackgroundDef bg)
        {
            _choice.BackgroundId = bg.id;
            foreach (var b in _bgButtons) Highlight(b, b.text == bg.name);
            string bonus = string.Empty;
            for (int i = 0; i < bg.attributeKeys.Length; i++) bonus += (i > 0 ? ", " : "") + "+" + bg.attributeValues[i] + " " + bg.attributeKeys[i];
            string skills = string.Empty;
            for (int i = 0; i < bg.skillKeys.Length; i++) skills += (i > 0 ? ", " : "") + bg.skillKeys[i] + " " + bg.skillValues[i];
            _blurb.text = bg.blurb + "\n\n" + (bonus.Length > 0 ? bonus + ".  " : "") + (skills.Length > 0 ? "Skills: " + skills + ".  " : "") + bg.coins + " pennies." +
                          (bg.family != null && bg.family.Length > 0 ? "  Kin: " + string.Join(", ", bg.family) + "." : "  No kin.");
            if (bg.fixedFigure != null && bg.fixedFigure.Length > 0 && !string.IsNullOrEmpty(bg.name)) { _name.value = bg.name; _choice.Name = bg.name; _female.value = bg.fixedFigure == "matilda"; }
        }

        private void SelectPreset(int idx) { _choice.AppearancePreset = idx; for (int i = 0; i < _presetButtons.Count; i++) Highlight(_presetButtons[i], i == idx); }

        private void Nudge(int attribute, int delta)
        {
            int v = _choice.Allocation[attribute] + delta;
            if (v < _data.attributeMin || v > _data.attributeMax) return;
            int total = 0; foreach (int a in _choice.Allocation) total += a;
            if (delta > 0 && total >= CharacterFactory.Budget(_data)) return;
            _choice.Allocation[attribute] = v;
            RefreshAttributes();
        }

        private void RefreshAttributes()
        {
            int total = 0; foreach (int a in _choice.Allocation) total += a;
            for (int i = 0; i < 4; i++) _attrValues[i].text = _choice.Allocation[i].ToString();
            _points.text = (CharacterFactory.Budget(_data) - total) + " points left to spend";
        }

        private void OnBeginClicked()
        {
            _choice.Name = _name.value;
            string problem = CharacterFactory.Validate(_choice, _data);
            if (problem != null) { _error.text = problem; return; }
            _error.text = string.Empty;
            Hide();
            _onBegin?.Invoke(_choice);
        }

        // ---- widgets
        private static VisualElement Column(VisualElement parent, float width) { var c = new VisualElement(); c.style.width = width; c.style.marginLeft = 10; c.style.marginRight = 10; parent.Add(c); return c; }
        private static Label L(string text, int size, Color color) { var l = new Label(text); l.style.fontSize = size; l.style.color = color; l.style.marginTop = 6; return l; }
        private static Button Btn(string text)
        {
            var b = new Button { text = text };
            b.style.fontSize = 14; b.style.color = Parchment; b.style.backgroundColor = new Color(0.12f, 0.11f, 0.10f, 0.95f);
            b.style.borderBottomColor = b.style.borderTopColor = b.style.borderLeftColor = b.style.borderRightColor = new Color(0.35f, 0.32f, 0.27f);
            b.style.borderBottomWidth = b.style.borderTopWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 1;
            b.style.paddingTop = b.style.paddingBottom = 7; b.style.paddingLeft = b.style.paddingRight = 12; b.style.marginBottom = 5; b.style.unityTextAlign = TextAnchor.MiddleLeft;
            return b;
        }
        private static void Highlight(Button b, bool on) { b.style.backgroundColor = on ? new Color(0.32f, 0.26f, 0.18f, 0.98f) : new Color(0.12f, 0.11f, 0.10f, 0.95f); }
        private static Slider SliderRow(VisualElement parent, string label, float min, float max, float value, Action<float> onChange)
        {
            var s = new Slider(label, min, max) { value = value }; s.style.color = Parchment; s.style.marginTop = 4;
            s.RegisterValueChangedCallback(e => onChange(e.newValue));
            parent.Add(s); return s;
        }
    }
}

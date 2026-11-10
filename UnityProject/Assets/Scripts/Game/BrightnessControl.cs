using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AshesAndCrowns.Game
{
    /// <summary>Player brightness: = / + brightens, - darkens, 0 resets. Shifts the grade's exposure on the scene volume and
    /// remembers the choice in PlayerPrefs. Not a gameplay value, so it lives outside balance.json.</summary>
    public sealed class BrightnessControl : MonoBehaviour
    {
        public const string PrefKey = "ashes.brightness";
        public HudController hud;
        public float step = 0.25f, min = -1f, max = 2.5f;

        private Balance _balance;
        private ColorAdjustments _grade;
        private float _offset;

        public float Offset => _offset;

        private void Start()
        {
            _balance = GameBootstrap.Instance != null ? GameBootstrap.Instance.Balance : null;
            var volume = FindFirstObjectByType<Volume>();
            if (volume != null && volume.profile != null && volume.profile.TryGet(out ColorAdjustments ca)) _grade = ca;
            _offset = Mathf.Clamp(PlayerPrefs.GetFloat(PrefKey, 0f), min, max);
            Apply(false);
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) Nudge(step);
            else if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) Nudge(-step);
            else if (kb.digit0Key.wasPressedThisFrame && kb.shiftKey.isPressed) { _offset = 0f; Apply(true); }
        }

        private void Nudge(float by)
        {
            _offset = Mathf.Clamp(_offset + by, min, max);
            Apply(true);
        }

        private void Apply(bool announce)
        {
            if (_grade != null && _balance != null) _grade.postExposure.Override(_balance.look.postExposure + _offset);
            PlayerPrefs.SetFloat(PrefKey, _offset);
            if (announce && hud != null) hud.ShowToast("Brightness " + (_offset >= 0f ? "+" : "") + _offset.ToString("0.00") + "  ( = brighter, - darker, Shift+0 reset )");
        }
    }
}

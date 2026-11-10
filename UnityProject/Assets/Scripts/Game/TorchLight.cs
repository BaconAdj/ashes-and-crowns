using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// A torch in the off hand. T lights one from the pack (a torch is an item you buy or make); it burns for a couple
    /// of game hours and is then gone. Night is legible without one thanks to the moon floor, but a torch is how you
    /// see faces, doors and the road. Flicker is deterministic noise, no allocations.
    /// </summary>
    public sealed class TorchLight : MonoBehaviour
    {
        public WorldClock clock;
        public HudController hud;
        public Transform socket;          // where the flame sits (defaults to this transform, 1.3 m up, slightly ahead)

        private Balance _balance;
        private Light _light;
        private float _hoursLeft;
        private float _flicker;

        public bool Lit => _light != null && _light.enabled;
        public float HoursLeft => _hoursLeft;

        private void Start()
        {
            _balance = GameBootstrap.Instance != null ? GameBootstrap.Instance.Balance : null;
            if (_balance == null) return;
            var go = new GameObject("TorchFlame");
            go.transform.SetParent(socket != null ? socket : transform, false);
            if (socket == null) go.transform.localPosition = new Vector3(0.35f, 1.3f, 0.25f);
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point; _light.range = _balance.look.torchRange; _light.intensity = _balance.look.torchIntensity;
            var c = _balance.look.torchColor; _light.color = new Color(c[0], c[1], c[2]);
            _light.shadows = LightShadows.None;
            _light.enabled = false;
            if (clock != null) clock.HoursPassed += OnHours;
        }

        private void OnDestroy() { if (clock != null) clock.HoursPassed -= OnHours; }

        private void OnHours(float h)
        {
            if (!Lit) return;
            _hoursLeft -= h;
            if (_hoursLeft <= 0f) { _light.enabled = false; hud?.ShowToast(L.T("Your torch gutters out.")); }
        }

        private void Update()
        {
            if (_light == null) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked) Toggle();
            if (!Lit) return;
            _flicker += Time.deltaTime * 11f;
            float n = Mathf.PerlinNoise(_flicker, 0.37f);
            _light.intensity = _balance.look.torchIntensity * (0.82f + 0.36f * n);
        }

        private void Toggle()
        {
            if (Lit) { _light.enabled = false; hud?.ShowToast(L.T("You stub the torch out. ") + Describe()); return; }
            var ctx = RunSession.Context;
            if (_hoursLeft <= 0f)
            {
                if (ctx == null || ctx.Inventory.Count("torch") <= 0)
                {
                    hud?.ShowToast(L.T("You have no torch. The market sells them for a penny; a chandler or your own tallow will do."));
                    return;
                }
                ctx.Inventory.Remove("torch");
                _hoursLeft = _balance.look.torchBurnHours;
            }
            _light.enabled = true;
            hud?.ShowToast(L.T("You strike a light. ") + Describe());
        }

        private string Describe()
        {
            var ctx = RunSession.Context;
            int spare = ctx != null ? ctx.Inventory.Count("torch") : 0;
            return (Lit ? $"This one has about {Mathf.CeilToInt(_hoursLeft * 60f)} minutes. " : string.Empty) + spare + (spare == 1 ? " torch" : " torches") + " in the pack.";
        }
    }
}

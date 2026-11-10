using AshesAndCrowns.Combat;
using AshesAndCrowns.UI;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Runtime driver for the England scene: clock, weather, bells, village spawn.</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class Phase1Scene : MonoBehaviour
    {
        public Terrain terrain;
        public WorldClock clock;
        public DayNightController dayNight;
        public WeatherController weather;
        public BellTower bells;
        public Material rainMaterial, snowMaterial;
        public PlayerController player;
        public HorseController horse;
        public PlayerVitals vitals;
        public ThirdPersonCamera thirdPersonCamera;
        public HudController hud;
        public PerfProbe perf;
        public Transform spawnPoint;
        public Transform horseSpawnPoint;
        public SaveManager saveManager;
        public VillageLife villageLife;
        public MeleeController melee;

        private float _nextClockText;

        private void Start()
        {
            var boot = GameBootstrap.Instance;
            var balance = boot.Balance;
            clock.Init(balance, boot.Rng);
            dayNight.Init(balance);
            weather.Init(balance, rainMaterial, snowMaterial);
            weather.follow = Camera.main != null ? Camera.main.transform : player.transform;

            PlaceOnGround(player.transform, spawnPoint.position, 0.2f);
            PlaceOnGround(horse.transform, horseSpawnPoint.position, 0.2f);
            vitals.Init(balance, boot.Rng);
            player.Init(balance, boot.Input);
            horse.Init(balance, boot.Input);
            thirdPersonCamera.Init(balance, boot.Input);
            if (melee != null) melee.Init(balance, boot.Rng);
            hud.Init(balance, vitals, melee, null, null);
            bells.hud = hud;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            hud.SetMode(GameMode.OnFoot);
        }

        /// <summary>Called by the SaveManager after a resume so camera and HUD match the restored mount state.</summary>
        public void RefreshAfterResume()
        {
            player.enabled = !horse.Mounted;
            thirdPersonCamera.target = horse.Mounted ? horse.transform : player.transform;
            thirdPersonCamera.SetRiding(horse.Mounted);
            if (melee != null) melee.mounted = horse.Mounted;
            hud.SetMode(horse.Mounted ? GameMode.Riding : GameMode.OnFoot);
        }

        private float _nextHorseCheck;

        /// <summary>A peasant has no horse. One appears when you own a rouncey or a warhorse, or were born to the saddle.</summary>
        public static bool OwnsHorse(RunContext ctx) => ctx != null && (ctx.Character.Rank >= Rank.Knight || ctx.Inventory.Count("horse") > 0 || ctx.Inventory.Count("warhorse") > 0);

        private void Update()
        {
            var input = GameBootstrap.Instance.Input;
            if (vitals.IsDead) return;
            if (horse != null && Time.unscaledTime >= _nextHorseCheck)
            {
                _nextHorseCheck = Time.unscaledTime + 0.5f;
                bool owned = OwnsHorse(RunSession.Context);
                if (horse.gameObject.activeSelf != owned && !(horse.Mounted && !owned)) horse.gameObject.SetActive(owned);
                else if (horse.Mounted && !owned) { horse.Dismount(player.transform); player.enabled = true; thirdPersonCamera.target = player.transform; thirdPersonCamera.SetRiding(false); hud.SetMode(GameMode.OnFoot); horse.gameObject.SetActive(false); }
            }
            if (saveManager != null && saveManager.IsQuitScreenOpen) return;
            if (villageLife != null && villageLife.IsBusy) return;
            if (input.Mount.WasPressedThisFrame() && !(melee != null && melee.GuardMode) && !(villageLife != null && villageLife.HasPlaceInReach))
            {
                if (horse.Mounted) { horse.Dismount(player.transform); player.enabled = true; }
                else if (horse.gameObject.activeSelf && Vector3.Distance(player.transform.position, horse.transform.position) <= GameBootstrap.Instance.Balance.player.mountRadius)
                { horse.Mount(player.transform); player.enabled = false; }
                thirdPersonCamera.target = horse.Mounted ? horse.transform : player.transform;
                thirdPersonCamera.SetRiding(horse.Mounted);
                if (melee != null) melee.mounted = horse.Mounted;
                hud.SetMode(horse.Mounted ? GameMode.Riding : GameMode.OnFoot);
            }
            // Debug: ] skips an hour, [ skips a day, so day/night and weather can be checked quickly.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.rightBracketKey.wasPressedThisFrame) clock.SkipHours(1f);
                if (kb.leftBracketKey.wasPressedThisFrame) clock.SkipHours(24f);
            }
            if (Time.unscaledTime >= _nextClockText && clock.IsReady)
            {
                _nextClockText = Time.unscaledTime + 1f;
                var c = clock.Calendar;
                hud.SetClock($"{c.LongDate()}\n{c.HourOfDay:00.0}h  {c.Season}  |  next bell {c.NextBellName} in {c.HoursUntilNextBell:F1} h  |  {weather.Description}");
            }
        }

        private void PlaceOnGround(Transform t, Vector3 at, float lift)
        {
            float y = terrain.SampleHeight(at) + terrain.transform.position.y + lift;
            t.position = new Vector3(at.x, y, at.z);
        }
    }
}

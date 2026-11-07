using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
using AshesAndCrowns.UI;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// Runtime entry point for the Phase 0 battlefield. The editor-side scene builder places the objects;
    /// this wires them with balance data at play time so nothing in the scene depends on asset GUIDs.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class Phase0Scene : MonoBehaviour
    {
        public Terrain terrain;
        public BattleDirector director;
        public UnitBanners banners;
        public CommandController command;
        public ModeManager modes;
        public PlayerController player;
        public HorseController horse;
        public PlayerVitals vitals;
        public MeleeController melee;
        public ThirdPersonCamera thirdPersonCamera;
        public HudController hud;
        public Light sun;
        public PerfProbe perf;

        private void Start()
        {
            var boot = GameBootstrap.Instance;
            var balance = boot.Balance;
            var field = boot.World.phase0Battlefield;

            ApplyLook(balance);

            var playerCentre = new Vector3(field.playerArmyCentre.x, 0f, field.playerArmyCentre.z);
            var enemyCentre = new Vector3(field.enemyArmyCentre.x, 0f, field.enemyArmyCentre.z);
            director.playerCharacter = player.transform;
            director.Setup(balance, boot.Rng, terrain, playerCentre, enemyCentre);
            banners.Build();
            command.Init(balance);

            Place(player.transform, field.playerSpawn.x, field.playerSpawn.z, 0.2f);
            Place(horse.transform, field.horseSpawn.x, field.horseSpawn.z, 0.2f);
            horse.transform.rotation = Quaternion.Euler(0f, 20f, 0f);

            vitals.Init(balance, boot.Rng);
            melee.Init(balance, boot.Rng);
            player.Init(balance, boot.Input);
            horse.Init(balance, boot.Input);
            thirdPersonCamera.Init(balance, boot.Input);
            hud.Init(balance, vitals, melee, director, command);
            modes.Init(balance, boot.Input);
            if (perf != null) perf.director = director;
        }

        private void Place(Transform t, float x, float z, float lift)
        {
            float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y + lift;
            t.position = new Vector3(x, y, z);
        }

        private void ApplyLook(Sim.Balance balance)
        {
            var look = balance.look;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = look.fogDensity;
            RenderSettings.fogColor = Col(look.fogColor);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Col(look.ambientColor);
            if (sun != null)
            {
                sun.color = Col(look.sunColor); sun.intensity = look.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(look.sunPitchDegrees, look.sunYawDegrees, 0f);
            }
        }

        private static Color Col(float[] c) => c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2]) : Color.grey;
    }
}

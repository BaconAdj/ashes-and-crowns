using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
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
        public GruntMode grunt;
        public SiegeField siegeField;   // only in the siege scene

        private bool _ended, _waitingReturn;
        private Sim.BattleReport _report;

        private void Start()
        {
            var boot = GameBootstrap.Instance;
            var balance = boot.Balance;
            var field = boot.World.phase0Battlefield;

            ApplyLook(balance);

            var playerCentre = new Vector3(field.playerArmyCentre.x, 0f, field.playerArmyCentre.z);
            var enemyCentre = new Vector3(field.enemyArmyCentre.x, 0f, field.enemyArmyCentre.z);
            director.playerCharacter = player.transform;
            var ctx = RunSession.Context;
            var launch = RunSession.PendingBattle; RunSession.PendingBattle = null; RunSession.LastBattleLaunch = launch;
            var siege = ctx != null ? ctx.Siege : null;
            var resume = ctx == null ? RunSession.PendingResume : null;   // cold launch straight into a saved storm: the context is restored later by SaveManager
            bool siegeBattle = siegeField != null && siege != null && ((launch != null && launch.Siege) || siege.InAssault);
            bool resumeSiege = siegeField != null && resume != null && resume.HasSiege && resume.SiegeInAssault;
            SiegeLayout layout = default; int playerCount = -1, enemyCount = -1; bool playerAttacking = true;
            if (siegeBattle)
            {
                playerAttacking = siege.PlayerAttacking;
                layout = Layout(balance.siege, siege.PlayerAttacking, siege.GateBroken, siege.RamBuilt, siege.TowerBuilt, siege.Ladders);
                SiegeCentres(balance.siege, siege.PlayerAttacking, siege.Attackers, siege.Defenders, out playerCentre, out enemyCentre, out playerCount, out enemyCount);
                siege.InAssault = true;
            }
            else if (resumeSiege)
            {
                playerAttacking = resume.SiegePlayerAttacking; siegeBattle = true;
                layout = Layout(balance.siege, resume.SiegePlayerAttacking, resume.SiegeGateBroken, resume.SiegeRamBuilt, resume.SiegeTowerBuilt, resume.SiegeLadders);
                SiegeCentres(balance.siege, resume.SiegePlayerAttacking, resume.SiegeAttackers, resume.SiegeDefenders, out playerCentre, out enemyCentre, out playerCount, out enemyCount);
            }
            director.Setup(balance, boot.Rng, terrain, playerCentre, enemyCentre, DataFiles.LoadNames(), playerCount, enemyCount, layout);
            director.BattleEnded += OnBattleEnded;
            if (siegeBattle) { siegeField.Show(layout, terrain); director.GateBroken += siegeField.OnGateBroken; director.GateBroken += () => AudioStubs.Play("siege_gate_crash"); AudioStubs.Play("siege_horns"); }
            bool asGrunt = launch != null ? launch.AsGrunt : (ctx != null && ctx.Service.Active && ctx.Service.Role != ServiceRole.Commander && !(siege != null && siege.PlayerCommands));
            if (asGrunt && grunt != null) { grunt.enabled = true; director.AiCommandsPlayerSide = true; }
            else if (grunt != null) grunt.enabled = false;
            banners.Build();
            command.Init(balance);

            if (siegeBattle && !playerAttacking)
            {
                // Defending your own walls: you and the vantage stand inside the castle.
                float wz = balance.siege.wallZ, gx = balance.siege.gateX;
                Place(player.transform, gx - 6f, wz + 14f, 0.2f); Place(horse.transform, gx + 8f, wz + 24f, 0.2f);
                player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                if (command.vantage != null) { command.vantage.position = new Vector3(gx, terrain.SampleHeight(new Vector3(gx, 0f, wz + 30f)) + terrain.transform.position.y, wz + 30f); command.vantage.rotation = Quaternion.Euler(0f, 180f, 0f); }
            }
            else
            {
                Place(player.transform, field.playerSpawn.x, field.playerSpawn.z, 0.2f);
                Place(horse.transform, field.horseSpawn.x, field.horseSpawn.z, 0.2f);
            }
            horse.transform.rotation = Quaternion.Euler(0f, 20f, 0f);

            vitals.Init(balance, boot.Rng);
            melee.Init(balance, boot.Rng);
            player.Init(balance, boot.Input);
            horse.Init(balance, boot.Input);
            thirdPersonCamera.Init(balance, boot.Input);
            hud.Init(balance, vitals, melee, director, command);
            modes.Init(balance, boot.Input);
            if (grunt != null && grunt.enabled) grunt.Init(balance);
            if (perf != null) perf.director = director;
        }

        private void OnBattleEnded(Sim.BattleReport report)
        {
            var ctx = RunSession.Context;
            if (ctx != null && ctx.Siege != null && ctx.Siege.InAssault)
            {
                var s = ctx.Siege;
                bool attackersWon = s.PlayerAttacking ? report.PlayerWon : (!report.PlayerWon && !report.Draw);
                int attDead = s.PlayerAttacking ? report.PlayerDead : report.EnemyDead, defDead = s.PlayerAttacking ? report.EnemyDead : report.PlayerDead;
                s.GateBroken |= director.Siege.GateOpen == 1;
                SiegeRules.ResolveStorm(s, attackersWon, attDead, defDead);
                if (s.Outcome == SiegeOutcome.Stormed && s.PlayerAttacking && s.PlayerCommands) ctx.Service.Renown += GameBootstrap.Instance.Balance.siege.renownForStorm;
            }
            if (grunt == null || !grunt.enabled)
            {
                hud.ShowBattleReport(report);
                _report = report; _ended = true;
                if (ctx != null) { _waitingReturn = true; hud.SetOrderBanner(string.Empty, "Press R to march home."); }
            }
        }

        private void LateUpdate()
        {
            if (!_waitingReturn || vitals.IsDead) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.rKey.wasPressedThisFrame) return;
            _waitingReturn = false;
            var ctx = RunSession.Context;
            var lines = new System.Collections.Generic.List<string>(4);
            if (_report != null) lines.Add(_report.Summary());
            if (ctx != null && ctx.Siege != null) lines.Add(ctx.Siege.Outcome == SiegeOutcome.Stormed ? "The walls are taken." : ctx.Siege.Active ? "The storm failed. Back to the lines." : "The siege is over.");
            RunSession.ReturnChronicle = lines;
            if (vitals.Body != null) RunSession.CarriedBody = vitals.Body.Export();
            UnityEngine.SceneManagement.SceneManager.LoadScene("Phase1_England");
        }

        private static SiegeLayout Layout(Sim.SiegeBalance sb, bool playerAttacking, bool gateBroken, bool ramBuilt, bool towerBuilt, int ladders) => new SiegeLayout
        {
            Active = 1, AttackersArePlayer = (byte)(playerAttacking ? 1 : 0), GateOpen = (byte)(gateBroken ? 1 : 0), HasRam = (byte)(ramBuilt ? 1 : 0),
            WallZ = sb.wallZ, WallHalf = sb.wallHalfThickness, GateX = sb.gateX, GateHalf = sb.gateHalfWidth, LadderHalf = sb.ladderHalfWidth, LadderSpacing = sb.ladderSpacing,
            LadderCount = Mathf.Min(sb.maxLadders + 2, ladders + (towerBuilt ? 2 : 0)), LadderVulnerability = sb.ladderVulnerability
        };

        private static void SiegeCentres(Sim.SiegeBalance sb, bool playerAttacking, int attackers, int defenders, out Vector3 playerCentre, out Vector3 enemyCentre, out int playerCount, out int enemyCount)
        {
            var att = new Vector3(sb.gateX, 0f, sb.attackerCentreZ); var def = new Vector3(sb.gateX, 0f, sb.defenderCentreZ);
            playerCentre = playerAttacking ? att : def; enemyCentre = playerAttacking ? def : att;
            playerCount = playerAttacking ? attackers : defenders; enemyCount = playerAttacking ? defenders : attackers;
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

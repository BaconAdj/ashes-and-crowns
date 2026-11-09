using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Game;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>
    /// Builds the Phase 0 vertical-slice battlefield from nothing: procedural terrain with a command hill,
    /// cold light and a grim post-processing volume, a rider and a horse made of primitives, the battle
    /// director with two armies, the command vantage, and the HUD. Deterministic: the same inputs rebuild
    /// the same scene, so the scene file is disposable.
    /// </summary>
    public static class Phase0SceneBuilder
    {
        private const string LitShader = SceneKit.LitShader;
        private const string UnlitShader = SceneKit.UnlitShader;
        private const string TerrainShader = SceneKit.TerrainShader;

        public static void Build() => Build(false);

        /// <summary>siege: the same field with a curtain wall across it (Phase 9), saved as its own scene.</summary>
        public static void Build(bool siege)
        {
            Balance balance = DataFiles.LoadBalance();
            WorldData world = DataFiles.LoadWorld();
            Phase0Battlefield field = world.phase0Battlefield;
            ProjectBootstrap.EnsureUrp();
            PanelSettings panel = ProjectBootstrap.EnsureUiToolkit();

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { Debug.LogWarning("[Ashes and Crowns] scene build cancelled by user"); return; }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- palette (muted: mud, iron, blood, cold light)
            Material mud = SceneKit.Mat("Mud", LitShader, new Color(0.30f, 0.27f, 0.22f), 0.05f, 0.95f);
            Material ironPlayer = SceneKit.Mat("SoldierPlayer", LitShader, new Color(0.36f, 0.39f, 0.45f), 0.35f, 0.75f, instancing: true);
            Material ironEnemy = SceneKit.Mat("SoldierEnemy", LitShader, new Color(0.46f, 0.30f, 0.27f), 0.30f, 0.80f, instancing: true);
            Material leather = SceneKit.Mat("Leather", LitShader, new Color(0.24f, 0.18f, 0.13f), 0.0f, 0.85f);
            Material steel = SceneKit.Mat("Steel", LitShader, new Color(0.55f, 0.57f, 0.60f), 0.9f, 0.35f);
            Material bay = SceneKit.Mat("HorseBay", LitShader, new Color(0.26f, 0.17f, 0.11f), 0.0f, 0.8f);
            Material darkWood = SceneKit.Mat("DarkWood", LitShader, new Color(0.18f, 0.14f, 0.10f), 0.0f, 0.9f);
            Material stone = SceneKit.Mat("Stone", LitShader, new Color(0.38f, 0.38f, 0.37f), 0.0f, 0.95f);
            Material bannerPlayer = SceneKit.Mat("BannerPlayer", LitShader, new Color(0.22f, 0.27f, 0.42f), 0.0f, 0.9f);
            Material bannerEnemy = SceneKit.Mat("BannerEnemy", LitShader, new Color(0.45f, 0.16f, 0.14f), 0.0f, 0.9f);
            Material ring = SceneKit.Mat("SelectionRing", UnlitShader, new Color(0.85f, 0.80f, 0.65f), 0f, 1f);

            // ---- light and sky
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;
            sun.color = SceneKit.Col(balance.look.sunColor); sun.intensity = balance.look.sunIntensity;
            sunGo.transform.rotation = Quaternion.Euler(balance.look.sunPitchDegrees, balance.look.sunYawDegrees, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = SceneKit.Col(balance.look.ambientColor);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = balance.look.fogDensity; RenderSettings.fogColor = SceneKit.Col(balance.look.fogColor);

            // ---- terrain
            Terrain terrain = BuildTerrain(field, mud, siege ? balance.siege : null);

            // ---- post-processing volume
            SceneKit.BuildVolume(balance, Paths.VolumeProfile);

            // ---- cameras
            var camGo = new GameObject("GameplayCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = SceneKit.Col(balance.look.fogColor);
            cam.farClipPlane = 700f; cam.nearClipPlane = 0.1f; cam.fieldOfView = 58f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            var tpc = camGo.AddComponent<ThirdPersonCamera>();

            var cmdCamGo = new GameObject("CommandCamera");
            var cmdCam = cmdCamGo.AddComponent<Camera>();
            cmdCam.clearFlags = CameraClearFlags.SolidColor; cmdCam.backgroundColor = SceneKit.Col(balance.look.fogColor);
            cmdCam.farClipPlane = 900f; cmdCam.fieldOfView = 50f; cmdCam.enabled = false;
            cmdCam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // ---- bootstrap + scene driver
            var bootGo = new GameObject("GameBootstrap");
            bootGo.AddComponent<GameBootstrap>();
            var sceneGo = new GameObject(siege ? "Phase9Scene" : "Phase0Scene");
            var driver = sceneGo.AddComponent<Phase0Scene>();
            var perf = sceneGo.AddComponent<PerfProbe>();

            // ---- player
            var playerGo = new GameObject("Player");
            var pcc = playerGo.AddComponent<CharacterController>();
            pcc.height = balance.player.height; pcc.radius = 0.35f; pcc.center = new Vector3(0f, balance.player.height * 0.5f, 0f); pcc.slopeLimit = 55f;
            SceneKit.Primitive(PrimitiveType.Capsule, "Body", playerGo.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.55f, 0.75f, 0.55f), leather);
            SceneKit.Primitive(PrimitiveType.Sphere, "Head", playerGo.transform, new Vector3(0f, 1.78f, 0f), new Vector3(0.3f, 0.32f, 0.3f), steel);
            var weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(playerGo.transform, false); weaponPivot.localPosition = new Vector3(0.38f, 1.2f, 0.1f);
            SceneKit.Primitive(PrimitiveType.Cube, "Blade", weaponPivot, new Vector3(0f, 0f, 0.55f), new Vector3(0.05f, 0.12f, 1.0f), steel);
            var vitals = playerGo.AddComponent<PlayerVitals>();
            var pc = playerGo.AddComponent<PlayerController>();
            var melee = playerGo.AddComponent<MeleeController>();

            // ---- horse
            var horseGo = new GameObject("Horse");
            var hcc = horseGo.AddComponent<CharacterController>();
            hcc.height = 1.7f; hcc.radius = 0.6f; hcc.center = new Vector3(0f, 0.95f, 0f); hcc.slopeLimit = 55f;
            SceneKit.Primitive(PrimitiveType.Cube, "Barrel", horseGo.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.65f, 0.75f, 1.9f), bay);
            SceneKit.Primitive(PrimitiveType.Cube, "Neck", horseGo.transform, new Vector3(0f, 1.6f, 1.05f), new Vector3(0.3f, 0.7f, 0.5f), bay);
            SceneKit.Primitive(PrimitiveType.Cube, "Head", horseGo.transform, new Vector3(0f, 1.85f, 1.45f), new Vector3(0.26f, 0.3f, 0.6f), bay);
            foreach (var (x, z) in new[] { (-0.22f, 0.7f), (0.22f, 0.7f), (-0.22f, -0.7f), (0.22f, -0.7f) })
                SceneKit.Primitive(PrimitiveType.Cube, "Leg", horseGo.transform, new Vector3(x, 0.4f, z), new Vector3(0.14f, 0.8f, 0.14f), bay);
            SceneKit.Primitive(PrimitiveType.Cube, "Saddle", horseGo.transform, new Vector3(0f, 1.55f, -0.1f), new Vector3(0.5f, 0.12f, 0.6f), leather);
            var seat = new GameObject("Seat").transform;
            seat.SetParent(horseGo.transform, false); seat.localPosition = new Vector3(0f, balance.horse.seatHeight - 0.15f, -0.1f);
            var horse = horseGo.AddComponent<HorseController>();
            horse.seat = seat;

            // ---- command vantage
            var vantageGo = new GameObject("CommandVantage");
            float vx = field.vantageHill.x, vz = field.vantageHill.z;
            vantageGo.transform.position = new Vector3(vx, terrain.SampleHeight(new Vector3(vx, 0f, vz)), vz);
            vantageGo.transform.rotation = Quaternion.LookRotation(new Vector3(field.playerArmyCentre.x - vx, 0f, field.playerArmyCentre.z - vz));
            SceneKit.Primitive(PrimitiveType.Cylinder, "Stone", vantageGo.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 0.3f, 1.6f), stone);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Pole", vantageGo.transform, new Vector3(0f, 2.8f, 0f), new Vector3(0.08f, 2.6f, 0.08f), darkWood);
            SceneKit.Primitive(PrimitiveType.Cube, "Standard", vantageGo.transform, new Vector3(0.8f, 4.9f, 0f), new Vector3(1.6f, 1.0f, 0.04f), bannerPlayer);
            var ringLr = vantageGo.AddComponent<LineRenderer>();
            ringLr.loop = true; ringLr.positionCount = 64; ringLr.startWidth = ringLr.endWidth = 0.2f; ringLr.material = ring; ringLr.shadowCastingMode = ShadowCastingMode.Off;
            ringLr.useWorldSpace = false;   // the siege scene moves the vantage inside the walls at play time
            for (int i = 0; i < 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2f;
                float x = vx + Mathf.Cos(a) * balance.command.vantageRadius, z = vz + Mathf.Sin(a) * balance.command.vantageRadius;
                ringLr.SetPosition(i, vantageGo.transform.InverseTransformPoint(new Vector3(x, terrain.SampleHeight(new Vector3(x, 0f, z)) + 0.2f, z)));
            }

            // ---- battle
            var directorGo = new GameObject("BattleDirector");
            var director = directorGo.AddComponent<BattleDirector>();
            director.playerSoldierMaterial = ironPlayer; director.enemySoldierMaterial = ironEnemy;
            director.soldierMesh = SceneKit.ArtMesh("Characters/Soldier.fbx") ?? SceneKit.CapsuleMesh();
            director.soldierIronMaterial = SceneKit.Mat("SoldierIron", LitShader, new Color(0.44f, 0.46f, 0.49f), 0.6f, 0.5f, instancing: true);
            var bannersGo = new GameObject("UnitBanners");
            var banners = bannersGo.AddComponent<UnitBanners>();
            banners.director = director; banners.playerBanner = bannerPlayer; banners.enemyBanner = bannerEnemy;
            var commandGo = new GameObject("CommandController");
            var command = commandGo.AddComponent<CommandController>();
            command.director = director; command.commandCamera = cmdCam; command.vantage = vantageGo.transform; command.selectionRingMaterial = ring;
            command.enabled = false;

            // ---- HUD
            var hudGo = new GameObject("HUD");
            var doc = hudGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var hud = hudGo.AddComponent<HudController>();

            // ---- modes
            var modesGo = new GameObject("ModeManager");
            var modes = modesGo.AddComponent<ModeManager>();
            modes.player = pc; modes.horse = horse; modes.vitals = vitals; modes.melee = melee; modes.thirdPersonCamera = tpc;
            modes.gameplayCamera = cam; modes.command = command; modes.director = director; modes.vantage = vantageGo.transform; modes.hud = hud;

            // ---- save
            var saveGo = new GameObject("SaveManager");
            var save = saveGo.AddComponent<SaveManager>();
            save.player = pc; save.horse = horse; save.vitals = vitals; save.melee = melee; save.director = director; save.hud = hud; save.modes = modes;
            modes.saveManager = save;
            var appearance = playerGo.AddComponent<PlayerAppearance>(); appearance.body = playerGo.transform.Find("Body"); appearance.head = playerGo.transform.Find("Head");
            save.appearance = appearance;
            var growth = sceneGo.AddComponent<SkillGrowth>(); growth.horse = horse; growth.melee = melee;
            var info = sceneGo.AddComponent<InfoPanels>(); info.hud = hud; info.saveManager = save; info.modes = modes;

            // ---- wiring
            tpc.target = playerGo.transform;
            pc.cameraRig = tpc; pc.vitals = vitals;
            melee.director = director; melee.vitals = vitals; melee.weaponVisual = weaponPivot; melee.facingSource = playerGo.transform;
            driver.terrain = terrain; driver.director = director; driver.banners = banners; driver.command = command; driver.modes = modes;
            driver.player = pc; driver.horse = horse; driver.vitals = vitals; driver.melee = melee; driver.thirdPersonCamera = tpc;
            driver.hud = hud; driver.sun = sun; driver.perf = perf;
            var blood = sceneGo.AddComponent<BloodDecals>(); blood.director = director; blood.vitals = vitals; blood.melee = melee; blood.player = pc.transform;
            blood.material = SceneKit.Mat("Blood", LitShader, new Color(0.22f, 0.04f, 0.03f), 0f, 0.6f, instancing: true);
            var grunt = sceneGo.AddComponent<GruntMode>();
            grunt.director = director; grunt.modes = modes; grunt.player = pc; grunt.horse = horse; grunt.vitals = vitals; grunt.melee = melee; grunt.hud = hud; grunt.ringMaterial = ring; grunt.enabled = false;
            driver.grunt = grunt; save.grunt = grunt;
            var gaitP = playerGo.AddComponent<ProceduralGait>(); gaitP.fullSpeed = balance.player.sprintSpeed;
            var gaitH = horseGo.AddComponent<ProceduralGait>(); gaitH.horse = true; gaitH.fullSpeed = balance.horse.gallopSpeed; gaitH.bobMetres = 0.07f; gaitH.strideHz = 1.8f;
            if (siege) driver.siegeField = BuildWalls(terrain, balance.siege, stone, darkWood);

            // Initial placement so the scene view is not empty before play.
            playerGo.transform.position = new Vector3(field.playerSpawn.x, terrain.SampleHeight(new Vector3(field.playerSpawn.x, 0f, field.playerSpawn.z)) + 0.2f, field.playerSpawn.z);
            horseGo.transform.position = new Vector3(field.horseSpawn.x, terrain.SampleHeight(new Vector3(field.horseSpawn.x, 0f, field.horseSpawn.z)) + 0.2f, field.horseSpawn.z);
            camGo.transform.position = playerGo.transform.position + new Vector3(0f, 2.5f, -6f);
            camGo.transform.LookAt(playerGo.transform.position + Vector3.up * 1.5f);

            string scenePath = siege ? Paths.Phase9Scene : Paths.Phase0Scene;
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ashes and Crowns] " + (siege ? "siege" : "Phase 0") + " scene built at " + scenePath);
        }

        // ------------------------------------------------------------------ curtain wall
        private static SiegeField BuildWalls(Terrain terrain, SiegeBalance sb, Material stone, Material darkWood)
        {
            var root = new GameObject("CurtainWall");
            var sf = root.AddComponent<SiegeField>();
            sf.wallHeight = sb.wallHeight; sf.ladderMaterial = darkWood;
            float size = terrain.terrainData.size.x;
            float topY = terrain.SampleHeight(new Vector3(sb.gateX, 0f, sb.wallZ + sb.wallHalfThickness + 2f)) + terrain.transform.position.y;
            float footY = terrain.SampleHeight(new Vector3(sb.gateX, 0f, sb.wallZ - sb.wallHalfThickness - 1f)) + terrain.transform.position.y;
            Mesh wallMesh = SceneKit.ArtMesh("Siege/WallSegment.fbx"), towerMesh = SceneKit.ArtMesh("Siege/Tower.fbx");
            if (wallMesh != null)
            {
                // Tile 10 m segments along the wall line, leaving the gate bay open; towers flank the gate.
                float sizeX = terrain.terrainData.size.x;
                for (float x = 45f; x < sizeX - 45f; x += 10f)
                {
                    if (Mathf.Abs(x + 5f - sb.gateX) < sb.gateHalfWidth + 8f) continue;
                    var seg = SceneKit.MeshChild("Wall_" + (int)x, root.transform, wallMesh, stone, stone, collider: false);
                    seg.transform.position = new Vector3(x + 5f, footY, sb.wallZ - sb.wallHalfThickness * 0.5f);
                }
                if (towerMesh != null)
                {
                    SceneKit.MeshChild("TowerWest", root.transform, towerMesh, stone, darkWood).transform.position = new Vector3(sb.gateX - sb.gateHalfWidth - 6f, footY, sb.wallZ);
                    SceneKit.MeshChild("TowerEast", root.transform, towerMesh, stone, darkWood).transform.position = new Vector3(sb.gateX + sb.gateHalfWidth + 6f, footY, sb.wallZ);
                }
                var gateFbx = SceneKit.Primitive(PrimitiveType.Cube, "Gate", root.transform, new Vector3(sb.gateX, footY + sb.wallHeight * 0.45f, sb.wallZ - sb.wallHalfThickness), new Vector3(sb.gateHalfWidth * 2f + 1f, sb.wallHeight * 0.9f, 0.5f), darkWood);
                sf.gate = gateFbx;
                var laddersFbx = new GameObject("Ladders"); laddersFbx.transform.SetParent(root.transform, false); sf.ladderRoot = laddersFbx.transform;
                return sf;
            }
            // parapet along the plateau edge, in two halves so the gate stays open
            float left = 40f, right = size - 40f, gl = sb.gateX - sb.gateHalfWidth - 3f, gr = sb.gateX + sb.gateHalfWidth + 3f;
            SceneKit.Primitive(PrimitiveType.Cube, "ParapetWest", root.transform, new Vector3((left + gl) * 0.5f, topY + 0.9f, sb.wallZ + 0.6f), new Vector3(gl - left, 1.8f, 1.2f), stone);
            SceneKit.Primitive(PrimitiveType.Cube, "ParapetEast", root.transform, new Vector3((gr + right) * 0.5f, topY + 0.9f, sb.wallZ + 0.6f), new Vector3(right - gr, 1.8f, 1.2f), stone);
            // the cliff face of the terrain ramp is the wall; stone cladding so it reads as masonry
            SceneKit.Primitive(PrimitiveType.Cube, "FaceWest", root.transform, new Vector3((left + gl) * 0.5f, footY + sb.wallHeight * 0.5f, sb.wallZ - sb.wallHalfThickness * 0.5f), new Vector3(gl - left, sb.wallHeight, 0.6f), stone);
            SceneKit.Primitive(PrimitiveType.Cube, "FaceEast", root.transform, new Vector3((gr + right) * 0.5f, footY + sb.wallHeight * 0.5f, sb.wallZ - sb.wallHalfThickness * 0.5f), new Vector3(right - gr, sb.wallHeight, 0.6f), stone);
            SceneKit.Primitive(PrimitiveType.Cube, "TowerWest", root.transform, new Vector3(gl - 1f, footY + (sb.wallHeight + 4f) * 0.5f, sb.wallZ), new Vector3(6f, sb.wallHeight + 4f, 7f), stone);
            SceneKit.Primitive(PrimitiveType.Cube, "TowerEast", root.transform, new Vector3(gr + 1f, footY + (sb.wallHeight + 4f) * 0.5f, sb.wallZ), new Vector3(6f, sb.wallHeight + 4f, 7f), stone);
            var gate = SceneKit.Primitive(PrimitiveType.Cube, "Gate", root.transform, new Vector3(sb.gateX, footY + sb.wallHeight * 0.45f, sb.wallZ - sb.wallHalfThickness), new Vector3(sb.gateHalfWidth * 2f + 1f, sb.wallHeight * 0.9f, 0.5f), darkWood);
            sf.gate = gate;
            var ladders = new GameObject("Ladders"); ladders.transform.SetParent(root.transform, false); sf.ladderRoot = ladders.transform;
            return sf;
        }

        // ------------------------------------------------------------------ terrain

        private static Terrain BuildTerrain(Phase0Battlefield field, Material groundMaterial, SiegeBalance siege)
        {
            const int res = 257;
            float size = field.terrainSize, maxH = field.terrainHeight;
            var heights = new float[res, res];
            var rng = new DeterministicRng((ulong)field.noiseSeed);
            float ox = rng.Range(0f, 1000f), oz = rng.Range(0f, 1000f);
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = x / (float)(res - 1) * size, wz = z / (float)(res - 1) * size;
                float h = 3.0f;                                                                 // base
                h += (Mathf.PerlinNoise(ox + wx * 0.012f, oz + wz * 0.012f) - 0.5f) * 4.5f;      // rolling ground
                h += (Mathf.PerlinNoise(ox + wx * 0.07f, oz + wz * 0.07f) - 0.5f) * 0.6f;        // ruts and clods
                float dx = wx - field.vantageHill.x, dz = wz - field.vantageHill.z;
                h += field.vantageHill.height * Mathf.Exp(-(dx * dx + dz * dz) / (2f * field.vantageHill.radius * field.vantageHill.radius * 0.35f));
                if (siege != null)                                                             // the castle plateau: a steep ramp over the wall band, flat behind it
                {
                    float t = Mathf.InverseLerp(siege.wallZ - siege.wallHalfThickness, siege.wallZ, wz);
                    h += Mathf.SmoothStep(0f, 1f, t) * siege.wallHeight;
                }
                float edge = Mathf.Min(Mathf.Min(wx, size - wx), Mathf.Min(wz, size - wz));     // ridge fence so the edge is never seen
                if (edge < 60f) h += Mathf.SmoothStep(0f, 1f, (60f - edge) / 60f) * 22f;
                heights[z, x] = Mathf.Clamp01(h / maxH);
            }
            var data = new TerrainData { heightmapResolution = res, size = new Vector3(size, maxH, size) };
            data.SetHeights(0, 0, heights);

            // Phase 0 shipped a Texture2D saved as .asset, which rendered the ground white; replace it with the PNG layer.
            AssetDatabase.DeleteAsset(Paths.Generated + "/MudAlbedo.asset");
            var staleLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Paths.Generated + "/Mud.terrainlayer");
            if (staleLayer != null && (staleLayer.diffuseTexture == null || !AssetDatabase.GetAssetPath(staleLayer.diffuseTexture).EndsWith(".png"))) AssetDatabase.DeleteAsset(Paths.Generated + "/Mud.terrainlayer");
            Texture2D tex = SceneKit.NoiseTexture(Paths.Generated + "/MudAlbedo.png", new Color(0.21f, 0.19f, 0.15f), new Color(0.33f, 0.31f, 0.24f), 5f, 19f, 256, 4.2f);
            TerrainLayer layer = SceneKit.Layer(Paths.Generated + "/Mud.terrainlayer", tex, 6f);
            data.terrainLayers = new[] { layer };
            string terrainAsset = Paths.Generated + (siege != null ? "/Phase9Terrain.asset" : "/Phase0Terrain.asset");
            AssetDatabase.DeleteAsset(terrainAsset);
            AssetDatabase.CreateAsset(data, terrainAsset);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Terrain";
            var terrain = terrainGo.GetComponent<Terrain>();
            Shader terrainShader = Shader.Find(TerrainShader);
            if (terrainShader == null) Debug.LogError("[Ashes and Crowns] URP terrain shader not found; is the URP package installed?");
            else
            {
                var tm = AssetDatabase.LoadAssetAtPath<Material>(Paths.Palette + "/TerrainMud.mat");
                if (tm == null) { tm = new Material(terrainShader) { name = "TerrainMud" }; AssetDatabase.CreateAsset(tm, Paths.Palette + "/TerrainMud.mat"); }
                terrain.materialTemplate = tm;
            }
            terrain.heightmapPixelError = 6f; terrain.basemapDistance = 400f; terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            return terrain;
        }

    }
}

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
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";
        private const string TerrainShader = "Universal Render Pipeline/Terrain/Lit";

        public static void Build()
        {
            Balance balance = DataFiles.LoadBalance();
            WorldData world = DataFiles.LoadWorld();
            Phase0Battlefield field = world.phase0Battlefield;
            ProjectBootstrap.EnsureUrp();
            PanelSettings panel = ProjectBootstrap.EnsureUiToolkit();

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { Debug.LogWarning("[Ashes and Crowns] scene build cancelled by user"); return; }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- palette (muted: mud, iron, blood, cold light)
            Material mud = Mat("Mud", LitShader, new Color(0.30f, 0.27f, 0.22f), 0.05f, 0.95f);
            Material ironPlayer = Mat("SoldierPlayer", LitShader, new Color(0.36f, 0.39f, 0.45f), 0.35f, 0.75f, instancing: true);
            Material ironEnemy = Mat("SoldierEnemy", LitShader, new Color(0.46f, 0.30f, 0.27f), 0.30f, 0.80f, instancing: true);
            Material leather = Mat("Leather", LitShader, new Color(0.24f, 0.18f, 0.13f), 0.0f, 0.85f);
            Material steel = Mat("Steel", LitShader, new Color(0.55f, 0.57f, 0.60f), 0.9f, 0.35f);
            Material bay = Mat("HorseBay", LitShader, new Color(0.26f, 0.17f, 0.11f), 0.0f, 0.8f);
            Material darkWood = Mat("DarkWood", LitShader, new Color(0.18f, 0.14f, 0.10f), 0.0f, 0.9f);
            Material stone = Mat("Stone", LitShader, new Color(0.38f, 0.38f, 0.37f), 0.0f, 0.95f);
            Material bannerPlayer = Mat("BannerPlayer", LitShader, new Color(0.22f, 0.27f, 0.42f), 0.0f, 0.9f);
            Material bannerEnemy = Mat("BannerEnemy", LitShader, new Color(0.45f, 0.16f, 0.14f), 0.0f, 0.9f);
            Material ring = Mat("SelectionRing", UnlitShader, new Color(0.85f, 0.80f, 0.65f), 0f, 1f);

            // ---- light and sky
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;
            sun.color = Col(balance.look.sunColor); sun.intensity = balance.look.sunIntensity;
            sunGo.transform.rotation = Quaternion.Euler(balance.look.sunPitchDegrees, balance.look.sunYawDegrees, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Col(balance.look.ambientColor);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = balance.look.fogDensity; RenderSettings.fogColor = Col(balance.look.fogColor);

            // ---- terrain
            Terrain terrain = BuildTerrain(field, mud);

            // ---- post-processing volume
            BuildVolume(balance);

            // ---- cameras
            var camGo = new GameObject("GameplayCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Col(balance.look.fogColor);
            cam.farClipPlane = 700f; cam.nearClipPlane = 0.1f; cam.fieldOfView = 58f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            var tpc = camGo.AddComponent<ThirdPersonCamera>();

            var cmdCamGo = new GameObject("CommandCamera");
            var cmdCam = cmdCamGo.AddComponent<Camera>();
            cmdCam.clearFlags = CameraClearFlags.SolidColor; cmdCam.backgroundColor = Col(balance.look.fogColor);
            cmdCam.farClipPlane = 900f; cmdCam.fieldOfView = 50f; cmdCam.enabled = false;
            cmdCam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // ---- bootstrap + scene driver
            var bootGo = new GameObject("GameBootstrap");
            bootGo.AddComponent<GameBootstrap>();
            var sceneGo = new GameObject("Phase0Scene");
            var driver = sceneGo.AddComponent<Phase0Scene>();
            var perf = sceneGo.AddComponent<PerfProbe>();

            // ---- player
            var playerGo = new GameObject("Player");
            var pcc = playerGo.AddComponent<CharacterController>();
            pcc.height = balance.player.height; pcc.radius = 0.35f; pcc.center = new Vector3(0f, balance.player.height * 0.5f, 0f); pcc.slopeLimit = 55f;
            var body = Primitive(PrimitiveType.Capsule, "Body", playerGo.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.55f, 0.75f, 0.55f), leather);
            Primitive(PrimitiveType.Sphere, "Head", playerGo.transform, new Vector3(0f, 1.78f, 0f), new Vector3(0.3f, 0.32f, 0.3f), steel);
            var weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(playerGo.transform, false); weaponPivot.localPosition = new Vector3(0.38f, 1.2f, 0.1f);
            Primitive(PrimitiveType.Cube, "Blade", weaponPivot, new Vector3(0f, 0f, 0.55f), new Vector3(0.05f, 0.12f, 1.0f), steel);
            var vitals = playerGo.AddComponent<PlayerVitals>();
            var pc = playerGo.AddComponent<PlayerController>();
            var melee = playerGo.AddComponent<MeleeController>();

            // ---- horse
            var horseGo = new GameObject("Horse");
            var hcc = horseGo.AddComponent<CharacterController>();
            hcc.height = 1.7f; hcc.radius = 0.6f; hcc.center = new Vector3(0f, 0.95f, 0f); hcc.slopeLimit = 55f;
            Primitive(PrimitiveType.Cube, "Barrel", horseGo.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.65f, 0.75f, 1.9f), bay);
            Primitive(PrimitiveType.Cube, "Neck", horseGo.transform, new Vector3(0f, 1.6f, 1.05f), new Vector3(0.3f, 0.7f, 0.5f), bay);
            Primitive(PrimitiveType.Cube, "Head", horseGo.transform, new Vector3(0f, 1.85f, 1.45f), new Vector3(0.26f, 0.3f, 0.6f), bay);
            foreach (var (x, z) in new[] { (-0.22f, 0.7f), (0.22f, 0.7f), (-0.22f, -0.7f), (0.22f, -0.7f) })
                Primitive(PrimitiveType.Cube, "Leg", horseGo.transform, new Vector3(x, 0.4f, z), new Vector3(0.14f, 0.8f, 0.14f), bay);
            Primitive(PrimitiveType.Cube, "Saddle", horseGo.transform, new Vector3(0f, 1.55f, -0.1f), new Vector3(0.5f, 0.12f, 0.6f), leather);
            var seat = new GameObject("Seat").transform;
            seat.SetParent(horseGo.transform, false); seat.localPosition = new Vector3(0f, balance.horse.seatHeight - 0.15f, -0.1f);
            var horse = horseGo.AddComponent<HorseController>();
            horse.seat = seat;

            // ---- command vantage
            var vantageGo = new GameObject("CommandVantage");
            float vx = field.vantageHill.x, vz = field.vantageHill.z;
            vantageGo.transform.position = new Vector3(vx, terrain.SampleHeight(new Vector3(vx, 0f, vz)), vz);
            vantageGo.transform.rotation = Quaternion.LookRotation(new Vector3(field.playerArmyCentre.x - vx, 0f, field.playerArmyCentre.z - vz));
            Primitive(PrimitiveType.Cylinder, "Stone", vantageGo.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 0.3f, 1.6f), stone);
            Primitive(PrimitiveType.Cylinder, "Pole", vantageGo.transform, new Vector3(0f, 2.8f, 0f), new Vector3(0.08f, 2.6f, 0.08f), darkWood);
            Primitive(PrimitiveType.Cube, "Standard", vantageGo.transform, new Vector3(0.8f, 4.9f, 0f), new Vector3(1.6f, 1.0f, 0.04f), bannerPlayer);
            var ringLr = vantageGo.AddComponent<LineRenderer>();
            ringLr.loop = true; ringLr.positionCount = 64; ringLr.startWidth = ringLr.endWidth = 0.2f; ringLr.material = ring; ringLr.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2f;
                float x = vx + Mathf.Cos(a) * balance.command.vantageRadius, z = vz + Mathf.Sin(a) * balance.command.vantageRadius;
                ringLr.SetPosition(i, new Vector3(x, terrain.SampleHeight(new Vector3(x, 0f, z)) + 0.2f, z));
            }

            // ---- battle
            var directorGo = new GameObject("BattleDirector");
            var director = directorGo.AddComponent<BattleDirector>();
            director.playerSoldierMaterial = ironPlayer; director.enemySoldierMaterial = ironEnemy;
            director.soldierMesh = CapsuleMesh();
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

            // ---- wiring
            tpc.target = playerGo.transform;
            pc.cameraRig = tpc; pc.vitals = vitals;
            melee.director = director; melee.vitals = vitals; melee.weaponVisual = weaponPivot; melee.facingSource = playerGo.transform;
            driver.terrain = terrain; driver.director = director; driver.banners = banners; driver.command = command; driver.modes = modes;
            driver.player = pc; driver.horse = horse; driver.vitals = vitals; driver.melee = melee; driver.thirdPersonCamera = tpc;
            driver.hud = hud; driver.sun = sun; driver.perf = perf;

            // Initial placement so the scene view is not empty before play.
            playerGo.transform.position = new Vector3(field.playerSpawn.x, terrain.SampleHeight(new Vector3(field.playerSpawn.x, 0f, field.playerSpawn.z)) + 0.2f, field.playerSpawn.z);
            horseGo.transform.position = new Vector3(field.horseSpawn.x, terrain.SampleHeight(new Vector3(field.horseSpawn.x, 0f, field.horseSpawn.z)) + 0.2f, field.horseSpawn.z);
            camGo.transform.position = playerGo.transform.position + new Vector3(0f, 2.5f, -6f);
            camGo.transform.LookAt(playerGo.transform.position + Vector3.up * 1.5f);

            EditorSceneManager.SaveScene(scene, Paths.Phase0Scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ashes and Crowns] Phase 0 scene built at " + Paths.Phase0Scene);
        }

        // ------------------------------------------------------------------ terrain

        private static Terrain BuildTerrain(Phase0Battlefield field, Material groundMaterial)
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
                float edge = Mathf.Min(Mathf.Min(wx, size - wx), Mathf.Min(wz, size - wz));     // ridge fence so the edge is never seen
                if (edge < 60f) h += Mathf.SmoothStep(0f, 1f, (60f - edge) / 60f) * 22f;
                heights[z, x] = Mathf.Clamp01(h / maxH);
            }
            var data = new TerrainData { heightmapResolution = res, size = new Vector3(size, maxH, size) };
            data.SetHeights(0, 0, heights);

            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "MudAlbedo" };
            var px = new Color[128 * 128];
            for (int i = 0; i < px.Length; i++)
            {
                int x = i % 128, y = i / 128;
                float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.6f + Mathf.PerlinNoise(x * 0.31f, y * 0.31f) * 0.4f;
                px[i] = Color.Lerp(new Color(0.21f, 0.19f, 0.15f), new Color(0.33f, 0.33f, 0.24f), n);
            }
            tex.SetPixels(px); tex.Apply();
            AssetDatabase.CreateAsset(tex, Paths.Generated + "/MudAlbedo.asset");
            var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(6f, 6f), name = "Mud" };
            AssetDatabase.CreateAsset(layer, Paths.Generated + "/Mud.terrainlayer");
            data.terrainLayers = new[] { layer };
            AssetDatabase.CreateAsset(data, Paths.Generated + "/Phase0Terrain.asset");

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Terrain";
            var terrain = terrainGo.GetComponent<Terrain>();
            Shader terrainShader = Shader.Find(TerrainShader);
            if (terrainShader == null) Debug.LogError("[Ashes and Crowns] URP terrain shader not found; is the URP package installed?");
            else
            {
                var tm = new Material(terrainShader) { name = "TerrainMud" };
                AssetDatabase.CreateAsset(tm, Paths.Palette + "/TerrainMud.mat");
                terrain.materialTemplate = tm;
            }
            terrain.heightmapPixelError = 6f; terrain.basemapDistance = 400f; terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            return terrain;
        }

        // ------------------------------------------------------------------ volume

        private static void BuildVolume(Balance balance)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, Paths.VolumeProfile);
            T Add<T>() where T : VolumeComponent { var c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); return c; }

            var tone = Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var ca = Add<ColorAdjustments>();
            ca.saturation.Override(balance.look.saturation); ca.contrast.Override(balance.look.contrast); ca.postExposure.Override(balance.look.postExposure);
            ca.colorFilter.Override(new Color(0.86f, 0.90f, 1.0f));
            var vig = Add<Vignette>(); vig.intensity.Override(balance.look.vignette); vig.smoothness.Override(0.5f);
            var grain = Add<FilmGrain>(); grain.type.Override(FilmGrainLookup.Medium2); grain.intensity.Override(balance.look.filmGrain); grain.response.Override(0.8f);
            var bloom = Add<Bloom>(); bloom.intensity.Override(0.25f); bloom.threshold.Override(1.1f);
            EditorUtility.SetDirty(profile);

            var volGo = new GameObject("GlobalVolume_GrimLook");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true; vol.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ helpers

        private static Material Mat(string name, string shaderName, Color color, float metallic, float roughness, bool instancing = false)
        {
            string path = Paths.Palette + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError("[Ashes and Crowns] shader not found: " + shaderName); shader = Shader.Find("Standard"); }
            var m = new Material(shader) { name = name, enableInstancing = instancing };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); else m.color = color;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f - roughness);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Mesh CapsuleMesh()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Mesh mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return mesh;
        }

        private static Color Col(float[] c) => c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2]) : Color.grey;
    }
}

using AshesAndCrowns.Combat;
using AshesAndCrowns.Game;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>Builds the England overworld scene: real-coastline terrain, sea, day/night, weather, bells and the first village.</summary>
    public static class Phase1SceneBuilder
    {
        public static void Build()
        {
            Balance balance = DataFiles.LoadBalance();
            WorldData world = DataFiles.LoadWorld();
            CoastData coast = DataFiles.LoadCoast();
            ProjectBootstrap.EnsureUrp();
            PanelSettings panel = ProjectBootstrap.EnsureUiToolkit();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Location village = world.Find(world.phase1Start.villageId);
            if (village == null) { Debug.LogError("[Ashes and Crowns] world.json has no location " + world.phase1Start.villageId); return; }
            float size = world.overworld.terrainSizeMetres;
            var villageXZ = new Vector2(village.nx * size, village.nz * size);

            Material leather = SceneKit.Mat("Leather", SceneKit.LitShader, new Color(0.24f, 0.18f, 0.13f), 0f, 0.85f);
            Material steel = SceneKit.Mat("Steel", SceneKit.LitShader, new Color(0.55f, 0.57f, 0.60f), 0.9f, 0.35f);
            Material bay = SceneKit.Mat("HorseBay", SceneKit.LitShader, new Color(0.26f, 0.17f, 0.11f), 0f, 0.8f);
            Material sea = SceneKit.Mat("SeaWater", SceneKit.LitShader, new Color(0.11f, 0.14f, 0.17f), 0.1f, 0.35f);
            Material rainMat = SceneKit.ParticleMat("RainDrop", new Color(0.7f, 0.74f, 0.8f, 0.45f));
            Material snowMat = SceneKit.ParticleMat("SnowFlake", new Color(0.85f, 0.86f, 0.9f, 0.85f));

            Light sun = SceneKit.BuildSun(balance);
            SceneKit.BuildVolume(balance, Paths.VolumeProfile);
            EditorUtility.DisplayProgressBar("Ashes and Crowns", "Rasterising England...", 0.2f);
            EnglandTerrainBuilder.Result land;
            var flatten = new System.Collections.Generic.List<Vector2> { villageXZ };
            foreach (Location l in world.locations) if (l.type != "village") flatten.Add(new Vector2(l.nx * size, l.nz * size));
            try { land = EnglandTerrainBuilder.Build(world, coast, balance, sea, flatten.ToArray(), balance.village.flattenRadiusMetres); }
            finally { EditorUtility.ClearProgressBar(); }
            Terrain terrain = land.Terrain;

            Vector3 centre = new Vector3(villageXZ.x, 0f, villageXZ.y);
            centre.y = SceneKit.GroundY(terrain, centre);
            var rng = new DeterministicRng((ulong)world.phase0Battlefield.noiseSeed).Fork(31);
            VillageBuilder.Result vill = VillageBuilder.Build(terrain, centre, balance, rng, village.name);

            Camera cam = SceneKit.BuildGameplayCamera(SceneKit.Col(balance.look.fogColor), out ThirdPersonCamera tpc);
            var bootGo = new GameObject("GameBootstrap"); bootGo.AddComponent<GameBootstrap>();

            var clockGo = new GameObject("WorldClock");
            var clock = clockGo.AddComponent<WorldClock>();
            var dayNight = clockGo.AddComponent<DayNightController>(); dayNight.clock = clock; dayNight.sun = sun;
            var weatherGo = new GameObject("Weather");
            var weather = weatherGo.AddComponent<WeatherController>(); weather.clock = clock;
            var bells = vill.Church.gameObject.AddComponent<BellTower>(); bells.clock = clock;

            GameObject playerGo = SceneKit.BuildPlayer(balance, leather, steel, out PlayerVitals vitals, out PlayerController pc, out MeleeController melee);
            GameObject horseGo = SceneKit.BuildHorse(balance, bay, leather, out HorseController horse);
            Object.DestroyImmediate(melee); // no battle in this scene yet
            pc.cameraRig = tpc; tpc.target = playerGo.transform;

            var spawn = new GameObject("PlayerSpawn").transform; spawn.position = centre + new Vector3(-6f, 0f, -8f);
            var horseSpawn = new GameObject("HorseSpawn").transform; horseSpawn.position = centre + new Vector3(-2f, 0f, -10f);
            playerGo.transform.position = new Vector3(spawn.position.x, SceneKit.GroundY(terrain, spawn.position) + 0.2f, spawn.position.z);
            horseGo.transform.position = new Vector3(horseSpawn.position.x, SceneKit.GroundY(terrain, horseSpawn.position) + 0.2f, horseSpawn.position.z);
            cam.transform.position = playerGo.transform.position + new Vector3(0f, 2.5f, -6f);
            cam.transform.LookAt(playerGo.transform.position + Vector3.up * 1.5f);

            // ---- castle and city stubs with spawn points (Phase 5 replaces these with real settlements)
            Material stoneMat = SceneKit.Mat("KeepStone", SceneKit.LitShader, new Color(0.40f, 0.39f, 0.36f), 0f, 0.95f);
            var spawnRoot = new GameObject("SpawnPoints").transform;
            var villageSpawn = new GameObject(village.id).transform; villageSpawn.SetParent(spawnRoot, true);
            villageSpawn.position = centre + new Vector3(-6f, 0f, -8f); villageSpawn.rotation = Quaternion.LookRotation(new Vector3(6f, 0f, 8f));
            Mesh keepMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Paths.Generated + "/Keep_Procedural.asset");
            if (keepMesh == null) { keepMesh = ProceduralBuildings.Keep(12f, 18f, 22f, 6f); AssetDatabase.CreateAsset(keepMesh, Paths.Generated + "/Keep_Procedural.asset"); }
            var stubsRoot = new GameObject("Strongholds").transform;
            foreach (Location l in world.locations)
            {
                if (l.type == "village") continue;
                var pos = new Vector3(l.nx * size, 0f, l.nz * size); pos.y = SceneKit.GroundY(terrain, pos) - 0.2f;
                var go = new GameObject("Keep_" + l.id); go.transform.SetParent(stubsRoot, true); go.transform.position = pos; go.isStatic = true;
                var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = keepMesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = new[] { stoneMat, stoneMat };
                var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = keepMesh;
                var sp = new GameObject(l.id).transform; sp.SetParent(spawnRoot, true);
                sp.position = pos + new Vector3(0f, 0.4f, 30f); sp.rotation = Quaternion.LookRotation(Vector3.back);
            }

            HudController hud = SceneKit.BuildHud(panel);
            var creation = hud.gameObject.AddComponent<CharacterCreationUI>();
            var sceneGo = new GameObject("Phase1Scene");
            var driver = sceneGo.AddComponent<Phase1Scene>();
            var perf = sceneGo.AddComponent<PerfProbe>();
            var narration = sceneGo.AddComponent<OpeningNarration>(); narration.hud = hud; narration.gameplayCamera = cam;
            var appearance = playerGo.AddComponent<PlayerAppearance>(); appearance.body = playerGo.transform.Find("Body"); appearance.head = playerGo.transform.Find("Head");
            var growth = sceneGo.AddComponent<SkillGrowth>(); growth.horse = horse;
            driver.terrain = terrain; driver.clock = clock; driver.dayNight = dayNight; driver.weather = weather; driver.bells = bells;
            driver.rainMaterial = rainMat; driver.snowMaterial = snowMat; driver.player = pc; driver.horse = horse; driver.vitals = vitals;
            driver.thirdPersonCamera = tpc; driver.hud = hud; driver.perf = perf; driver.spawnPoint = spawn; driver.horseSpawnPoint = horseSpawn;

            var saveGo = new GameObject("SaveManager");
            var save = saveGo.AddComponent<SaveManager>();
            save.clock = clock; save.player = pc; save.horse = horse; save.vitals = vitals; save.hud = hud; save.home = vill.Root; save.englandScene = driver;
            save.creation = creation; save.narration = narration; save.appearance = appearance; save.spawnPoints = spawnRoot;
            driver.saveManager = save;
            var info = sceneGo.AddComponent<InfoPanels>(); info.hud = hud; info.saveManager = save;

            EditorSceneManager.SaveScene(scene, Paths.Phase1Scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Ashes and Crowns] England scene built at {Paths.Phase1Scene}; village '{village.name}' at {centre}");
        }
    }
}

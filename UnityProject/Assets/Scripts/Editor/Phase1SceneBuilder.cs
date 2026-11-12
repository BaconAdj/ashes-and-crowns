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
        public static void Build(bool town = false)
        {
            Balance balance = DataFiles.LoadBalance();
            WorldData world = DataFiles.LoadWorld();
            CoastData coast = DataFiles.LoadCoast();
            ProjectBootstrap.EnsureUrp();
            PanelSettings panel = ProjectBootstrap.EnsureUiToolkit();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string startId = town ? "wallingford" : world.phase1Start.villageId;
            Location village = world.Find(startId);
            if (village == null) { Debug.LogError("[Ashes and Crowns] world.json has no location " + startId); return; }
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
            Terrain terrain; Vector3 centre; VillageBuilder.Result vill; TownBuilder.TownResult townBuilt = null; TownPlanData plan = null;
            var rng = new DeterministicRng((ulong)world.phase0Battlefield.noiseSeed).Fork(31);
            if (town)
            {
                // One town at one metre to the metre, on its own ground. The plan is fixed by the same seed the tests use.
                plan = TownPlan.Wallingford(new DeterministicRng(1135).Fork(77));
                EditorUtility.DisplayProgressBar("Ashes and Crowns", "Raising " + plan.Name + "...", 0.2f);
                try
                {
                    terrain = TownTerrainBuilder.Build(plan, sea).Terrain;
                    townBuilt = TownBuilder.Build(terrain, plan, balance, rng);
                }
                finally { EditorUtility.ClearProgressBar(); }
                vill = townBuilt.Vill; centre = vill.Green;
            }
            else
            {
                EditorUtility.DisplayProgressBar("Ashes and Crowns", "Rasterising England...", 0.2f);
                EnglandTerrainBuilder.Result land;
                var flatten = new System.Collections.Generic.List<Vector2> { villageXZ };
                foreach (Location l in world.locations) if (l.type != "village") flatten.Add(new Vector2(l.nx * size, l.nz * size));
                try { land = EnglandTerrainBuilder.Build(world, coast, balance, sea, flatten.ToArray(), balance.village.flattenRadiusMetres); }
                finally { EditorUtility.ClearProgressBar(); }
                terrain = land.Terrain;
                centre = new Vector3(villageXZ.x, 0f, villageXZ.y);
                centre.y = SceneKit.GroundY(terrain, centre);
                vill = VillageBuilder.Build(terrain, centre, balance, rng, village.name);
            }

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
            pc.cameraRig = tpc; tpc.target = playerGo.transform; pc.melee = melee; tpc.melee = melee;

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
            var fixedSites = new GameObject("FixedSites").transform;
            vill.Root.SetParent(null, true);
            if (town) foreach (Transform gate in townBuilt.GateSpawns) gate.SetParent(spawnRoot, true);
            foreach (Location l in world.locations)
            {
                if (town || l.type == "village") continue;
                var pos = new Vector3(l.nx * size, 0f, l.nz * size); pos.y = SceneKit.GroundY(terrain, pos) - 0.2f;
                var go = new GameObject("Keep_" + l.id); go.transform.SetParent(stubsRoot, true); go.transform.position = pos; go.isStatic = true;
                var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = keepMesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = new[] { stoneMat, stoneMat };
                var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = keepMesh;
                var sp = new GameObject(l.id).transform; sp.SetParent(spawnRoot, true);
                sp.position = pos + new Vector3(0f, 0.4f, 30f); sp.rotation = Quaternion.LookRotation(Vector3.back);
                var siteGo = new GameObject("Site_" + l.id); siteGo.transform.SetParent(fixedSites, true); siteGo.transform.position = pos + new Vector3(0f, 0.4f, 28f);
                var site = siteGo.AddComponent<SettlementSite>(); site.settlementId = l.id; site.centre = siteGo.transform; site.alehouse = siteGo.transform; site.radius = 90f;
            }

            HudController hud = SceneKit.BuildHud(panel);
            var creation = hud.gameObject.AddComponent<CharacterCreationUI>();
            var sceneGo = new GameObject("Phase1Scene");
            var driver = sceneGo.AddComponent<Phase1Scene>();
            var perf = sceneGo.AddComponent<PerfProbe>();
            var narration = sceneGo.AddComponent<OpeningNarration>(); narration.hud = hud; narration.gameplayCamera = cam;
            var torch = playerGo.AddComponent<TorchLight>(); torch.clock = clock; torch.hud = hud;
            var activity = sceneGo.AddComponent<ActivityDirector>(); activity.clock = clock; activity.hud = hud; activity.player = pc; activity.horse = horse; activity.vitals = vitals; activity.body = playerGo.transform.Find("Body");
            var appearance = playerGo.AddComponent<PlayerAppearance>(); appearance.body = playerGo.transform.Find("Body"); appearance.head = playerGo.transform.Find("Head");
            var growth = sceneGo.AddComponent<SkillGrowth>(); growth.horse = horse; growth.melee = melee;
            driver.terrain = terrain; driver.melee = melee; driver.clock = clock; driver.dayNight = dayNight; driver.weather = weather; driver.bells = bells;
            driver.rainMaterial = rainMat; driver.snowMaterial = snowMat; driver.player = pc; driver.horse = horse; driver.vitals = vitals;
            driver.thirdPersonCamera = tpc; driver.hud = hud; driver.perf = perf; driver.spawnPoint = spawn; driver.horseSpawnPoint = horseSpawn;

            var saveGo = new GameObject("SaveManager");
            var save = saveGo.AddComponent<SaveManager>();
            save.clock = clock; save.player = pc; save.horse = horse; save.vitals = vitals; save.hud = hud; save.home = vill.Root; save.englandScene = driver;
            save.creation = creation; save.narration = narration; save.appearance = appearance; save.spawnPoints = spawnRoot;
            driver.saveManager = save;
            var info = sceneGo.AddComponent<InfoPanels>(); info.hud = hud; info.saveManager = save;
            var survival = sceneGo.AddComponent<SurvivalSystem>(); survival.clock = clock; survival.vitals = vitals; survival.home = vill.Root;
            var life = sceneGo.AddComponent<VillageLife>();
            life.clock = clock; life.vitals = vitals; life.player = pc; life.horse = horse; life.hud = hud; life.saveManager = save; life.survival = survival;
            life.church = vill.Church; life.alehouse = vill.Alehouse; life.home = vill.Home; life.hallHouse = vill.HallHouse; life.workplace = vill.Workplace; life.mastersRoot = vill.Masters;
            driver.villageLife = life;
            if (town) life.roadsOut = townBuilt.RoadsOut.ToArray();
            var ashbySite = vill.Root.GetComponent<SettlementSite>(); if (ashbySite != null) ashbySite.transform.SetParent(fixedSites, true);
            TownScene townScene = null;
            if (town) { townScene = sceneGo.AddComponent<TownScene>(); townScene.settlementId = plan.Id; townScene.mapPosition = new Vector2(village.nx, village.nz); }
            var assets = sceneGo.AddComponent<WorldAssets>();
            assets.wattle = vill.Wattle; assets.thatch = vill.Thatch; assets.stone = vill.Stone; assets.slate = vill.Slate; assets.darkWood = vill.DarkWood; assets.hut = vill.HutMesh; assets.alehouse = vill.AlehouseMesh; assets.church = vill.ChurchMesh; assets.terrain = terrain;
            assets.linen = vill.Linen; assets.fire = vill.Fire; assets.iron = vill.Iron; assets.villager = vill.Villager; assets.skin = vill.Skin; assets.man = vill.Man; assets.woman = vill.Woman; assets.deer = vill.Deer; assets.deerCoat = vill.DeerCoat; assets.antler = vill.Antler;
            assets.oak = SceneKit.ArtMesh("Nature/Oak.fbx"); assets.keep = SceneKit.ArtMesh("Buildings/Keep.fbx"); assets.bark = vill.Bark; assets.leaf = SceneKit.Mat("OakLeaf", SceneKit.LitShader, new Color(0.30f, 0.36f, 0.20f), 0f, 0.95f);
            var spawnerGo = new GameObject("Settlements");
            var spawnerComp = spawnerGo.AddComponent<SettlementSpawner>(); spawnerComp.assets = assets; spawnerComp.fixedSitesRoot = fixedSites; spawnerComp.strongholdsRoot = stubsRoot; spawnerComp.townMode = town;
            if (town)
            {
                var crowd = sceneGo.AddComponent<TownCrowd>();
                crowd.site = townBuilt.Vill.Site; crowd.assets = assets; crowd.homes = townBuilt.Homes; crowd.churches = townBuilt.Churches; crowd.churchKinds = townBuilt.ChurchKinds; crowd.alehouses = townBuilt.Alehouses; crowd.streets = townBuilt.Streets;
            }
            save.spawner = spawnerComp; life.spawner = spawnerComp; survival.village = life; save.villageLife = life; life.narration = narration;
            var map = sceneGo.AddComponent<MapScreen>(); map.hud = hud; map.saveManager = save; map.player = playerGo.transform; map.terrainSize = size; map.town = townScene;
            var enc = sceneGo.AddComponent<EncounterDirector>();
            enc.melee = melee; enc.vitals = vitals; enc.clock = clock; enc.hud = hud; enc.village = life; enc.saveManager = save; enc.player = playerGo.transform;
            enc.banditMaterial = SceneKit.Mat("Bandit", SceneKit.LitShader, new Color(0.22f, 0.2f, 0.17f), 0f, 0.9f); enc.steelMaterial = steel;
            life.encounter = enc; life.melee = melee; life.activity = activity;
            save.melee = melee;
            var siege = sceneGo.AddComponent<SiegeDirector>();
            siege.clock = clock; siege.hud = hud; siege.saveManager = save; siege.village = life; siege.vitals = vitals; siege.spawner = spawnerComp;
            life.siege = siege;
            var lordship = sceneGo.AddComponent<LordshipDirector>();
            lordship.clock = clock; lordship.hud = hud; lordship.saveManager = save; lordship.village = life; lordship.siege = siege; lordship.vitals = vitals;
            life.lordship = lordship;
            var intrigue = sceneGo.AddComponent<IntrigueDirector>();
            intrigue.clock = clock; intrigue.hud = hud; intrigue.saveManager = save; intrigue.village = life; intrigue.encounter = enc; intrigue.vitals = vitals;
            life.intrigue = intrigue;
            var cheats = sceneGo.AddComponent<DevCheats>();
            cheats.clock = clock; cheats.hud = hud; cheats.saveManager = save; cheats.village = life; cheats.siege = siege; cheats.lordship = lordship; cheats.spawner = spawnerComp; cheats.encounter = enc; cheats.player = pc; cheats.horse = horse; cheats.vitals = vitals;
            life.cheats = cheats;
            var household = sceneGo.AddComponent<HouseholdDirector>();
            household.clock = clock; household.hud = hud; household.saveManager = save; household.village = life; household.encounter = enc; household.vitals = vitals;
            life.household = household;
            var gaitP = playerGo.AddComponent<ProceduralGait>(); gaitP.fullSpeed = balance.player.sprintSpeed; activity.gait = gaitP;
            var gaitH = horseGo.AddComponent<ProceduralGait>(); gaitH.horse = true; gaitH.fullSpeed = balance.horse.gallopSpeed; gaitH.bobMetres = 0.07f; gaitH.strideHz = 1.8f;
            var blood = sceneGo.AddComponent<BloodDecals>(); blood.vitals = vitals; blood.melee = melee; blood.player = playerGo.transform;
            blood.material = SceneKit.Mat("Blood", SceneKit.LitShader, new Color(0.22f, 0.04f, 0.03f), 0f, 0.6f, instancing: true);

            string scenePath = town ? Paths.TownScene : Paths.Phase1Scene;
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Ashes and Crowns] {(town ? "Town" : "England")} scene built at {scenePath}; '{village.name}' at {centre}");
        }
    }
}

using System.Collections.Generic;
using AshesAndCrowns.Game;
using AshesAndCrowns.Sim;
using UnityEditor;
using UnityEngine;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>
    /// Raises a town from its plan (Sim.TownPlan) on the ground TownTerrainBuilder made: every house on its plot facing
    /// its street, the parish churches, the priory, the castle on its motte within a curtain wall, the palisade along the
    /// rampart, the gates, the bridge. Interiors and people are not baked: Game.TownCrowd furnishes and peoples the
    /// streets around the player at run time from the household records written here.
    /// </summary>
    public static class TownBuilder
    {
        public sealed class TownResult
        {
            public VillageBuilder.Result Vill;
            public TownCrowd.Home[] Homes; public Transform[] Churches; public BuildingKind[] ChurchKinds; public Transform[] Alehouses;
            public float[] Streets; public Transform CastleGate; public readonly List<Transform> GateSpawns = new List<Transform>(4); public readonly List<Transform> RoadsOut = new List<Transform>(4);
            public Mesh TownHouse, Chapel;
        }

        public static TownResult Build(Terrain terrain, TownPlanData plan, Balance balance, DeterministicRng rng)
        {
            VillageBuilder.Result r = VillageBuilder.Assets(balance);
            Mesh townHouse = SceneKit.ArtMesh("Buildings/TownHouse.fbx") ?? r.HutMesh;
            Mesh chapel = SceneKit.ArtMesh("Buildings/Chapel.fbx") ?? r.ChurchMesh;
            Mesh keepMesh = SceneKit.ArtMesh("Buildings/Keep.fbx");
            Mesh gateMesh = SceneKit.ArtMesh("Siege/Gatehouse.fbx");
            Mesh palisadeMesh = SceneKit.ArtMesh("Siege/Palisade.fbx");
            Mesh wallMesh = SceneKit.ArtMesh("Siege/WallSegment.fbx");
            Mesh towerMesh = SceneKit.ArtMesh("Siege/Tower.fbx");
            Mesh oakMesh = SceneKit.ArtMesh("Nature/Oak.fbx");
            Material oakTimber = SceneKit.Mat("PalisadeOak", SceneKit.LitShader, new Color(0.27f, 0.22f, 0.17f), 0f, 0.95f);
            Material leaf = SceneKit.Mat("OakLeaf", SceneKit.LitShader, new Color(0.30f, 0.36f, 0.20f), 0f, 0.95f);

            var root = new GameObject("Town_" + plan.Name).transform;
            Vector3 centre = new Vector3(plan.Market.Cx, 0f, plan.Market.Cz); centre.y = SceneKit.GroundY(terrain, centre);
            root.position = centre;
            Transform houses = Group(root, "Houses"), churchesRoot = Group(root, "Churches"), castle = Group(root, "Castle"), defences = Group(root, "Defences"), priory = Group(root, "Priory"), marks = Group(root, "Marks");

            var result = new TownResult { Vill = r, TownHouse = townHouse, Chapel = chapel };
            var byId = new Dictionary<int, Transform>(plan.Buildings.Count);
            var kindOf = new Dictionary<int, BuildingKind>(plan.Buildings.Count);
            var churchList = new List<Transform>(12); var churchKinds = new List<BuildingKind>(12); var aleList = new List<Transform>(16);
            var parishChurch = new Dictionary<int, Transform>(12);
            Material[] daub = { r.Wattle, r.Thatch }, stoneSlate = { r.Stone, r.Slate }, stoneThatch = { r.Stone, r.Thatch }, stoneWood = { r.Stone, r.DarkWood }, timber = { oakTimber, r.DarkWood };

            foreach (TownBuilding b in plan.Buildings)
            {
                Vector3 pos = new Vector3(b.X, 0f, b.Z); Quaternion rot = Quaternion.Euler(0f, b.FacingDeg, 0f);
                Transform t = null; BuildingKind kind = BuildingKind.Hut;
                switch (b.Kind)
                {
                    case TownBuildingKind.House: t = VillageBuilder.Place(houses, "House_" + b.Id, townHouse, daub, terrain, pos, rot); kind = townHouse == r.HutMesh ? BuildingKind.Hut : BuildingKind.TownHouse; break;
                    case TownBuildingKind.Cot: t = VillageBuilder.Place(houses, "Cot_" + b.Id, r.HutMesh, daub, terrain, pos, rot); kind = BuildingKind.Hut; break;
                    case TownBuildingKind.GreatHouse: t = VillageBuilder.Place(houses, "GreatHouse_" + b.Id, r.AlehouseMesh, daub, terrain, pos, rot); kind = BuildingKind.Hall; break;
                    case TownBuildingKind.Alehouse: t = VillageBuilder.Place(houses, "Alehouse_" + b.Id, r.AlehouseMesh, daub, terrain, pos, rot); kind = BuildingKind.Alehouse; aleList.Add(t); break;
                    case TownBuildingKind.ParishChurch:
                        t = VillageBuilder.Place(b.Name == "the castle chapel" ? castle : churchesRoot, b.Name, chapel, stoneThatch, terrain, pos, rot); kind = chapel == r.ChurchMesh ? BuildingKind.Church : BuildingKind.Chapel;
                        churchList.Add(t); churchKinds.Add(kind); if (b.Parish >= 0) parishChurch[b.Parish] = t; break;
                    case TownBuildingKind.GreatChurch:
                        t = VillageBuilder.Place(churchesRoot, b.Name, r.ChurchMesh, stoneSlate, terrain, pos, rot); kind = BuildingKind.Church;
                        churchList.Add(t); churchKinds.Add(kind); if (b.Parish >= 0) parishChurch[b.Parish] = t; break;
                    case TownBuildingKind.PrioryRange: t = VillageBuilder.Place(priory, b.Name, r.AlehouseMesh, stoneSlate, terrain, pos, rot); kind = BuildingKind.Hall; break;
                    case TownBuildingKind.CastleHall: t = VillageBuilder.Place(castle, b.Name, r.AlehouseMesh, stoneSlate, terrain, pos, rot); kind = BuildingKind.Hall; break;
                    case TownBuildingKind.Stable: t = VillageBuilder.Place(castle, b.Name, r.AlehouseMesh, daub, terrain, pos, rot); kind = BuildingKind.Alehouse; break;
                    case TownBuildingKind.Keep:
                        if (keepMesh != null) { t = VillageBuilder.Place(castle, b.Name, keepMesh, stoneWood, terrain, pos, rot); t.localScale = Vector3.one * 0.55f; }
                        break;
                    case TownBuildingKind.Gatehouse:
                        if (gateMesh != null)
                        {
                            bool stone = b.Name == "the castle gate" || b.Name == "Bridge Gate";
                            t = VillageBuilder.Place(defences, b.Name, gateMesh, stone ? stoneWood : timber, terrain, pos, rot);
                            if (b.Name == "the castle gate") result.CastleGate = t;
                        }
                        break;
                }
                if (t != null) { byId[b.Id] = t; kindOf[b.Id] = kind; }
            }
            result.Churches = churchList.ToArray(); result.ChurchKinds = churchKinds.ToArray(); result.Alehouses = aleList.ToArray();

            // ---- the palisade on the crest of the bank, west, north and south; the river guards the east
            if (palisadeMesh != null)
            {
                Transform pal = Group(defences, "Palisade");
                float crest = plan.BankWidth * 0.5f, hw = plan.HalfWidth, hd = plan.HalfDepth; int n = 0;
                for (float z = -hd - crest; z <= hd + crest; z += 4f) if (Mathf.Abs(z) > 6.5f) Stake(pal, palisadeMesh, oakTimber, r.DarkWood, terrain, new Vector3(-hw - crest, 0f, z), -90f, n++);
                for (float x = -hw - crest + 4f; x <= plan.RiverX0 - 8f; x += 4f)
                {
                    if (Mathf.Abs(x) <= 6.5f) continue;
                    Stake(pal, palisadeMesh, oakTimber, r.DarkWood, terrain, new Vector3(x, 0f, hd + crest), 0f, n++);
                    Stake(pal, palisadeMesh, oakTimber, r.DarkWood, terrain, new Vector3(x, 0f, -hd - crest), 180f, n++);
                }
            }

            // ---- the castle: a curtain wall round the bailey with a tower at each corner, the keep on the motte inside it
            if (wallMesh != null)
            {
                Transform curtain = Group(castle, "Curtain"); TownRect bl = plan.Bailey; float seg = 10.4f; int n = 0;
                for (float x = bl.X0 + seg * 0.5f; x < bl.X1; x += seg) { Wall(curtain, wallMesh, r.Stone, r.DarkWood, terrain, new Vector3(x, 0f, bl.Z0), 180f, n++); Wall(curtain, wallMesh, r.Stone, r.DarkWood, terrain, new Vector3(x, 0f, bl.Z1), 0f, n++); }
                for (float z = bl.Z0 + seg * 0.5f; z < bl.Z1; z += seg) Wall(curtain, wallMesh, r.Stone, r.DarkWood, terrain, new Vector3(bl.X1, 0f, z), 90f, n++);
                // the west side is laid outward from the gate so the wall meets the gatehouse on both hands
                for (float z = 200f + 4.6f + seg * 0.5f; z - seg * 0.5f < bl.Z1; z += seg) Wall(curtain, wallMesh, r.Stone, r.DarkWood, terrain, new Vector3(bl.X0, 0f, z), -90f, n++);
                for (float z = 200f - 4.6f - seg * 0.5f; z + seg * 0.5f > bl.Z0; z -= seg) Wall(curtain, wallMesh, r.Stone, r.DarkWood, terrain, new Vector3(bl.X0, 0f, z), -90f, n++);
                if (towerMesh != null)
                    foreach (Vector2 c in new[] { new Vector2(bl.X0, bl.Z0), new Vector2(bl.X1, bl.Z0), new Vector2(bl.X0, bl.Z1), new Vector2(bl.X1, bl.Z1) })
                        VillageBuilder.Place(curtain, "Tower", towerMesh, stoneSlate, terrain, new Vector3(c.x, 0f, c.y), Quaternion.identity);
            }

            // ---- the priory precinct wall, open to the High Street
            {
                TownRect pr = plan.Priory; float y = TownTerrainBuilder.BaseHeight;
                Box(priory, "PrecinctWallN", new Vector3(pr.Cx, y + 0.9f, pr.Z1), new Vector3(pr.X1 - pr.X0, 1.8f, 0.6f), r.Stone);
                Box(priory, "PrecinctWallW", new Vector3(pr.X0, y + 0.9f, pr.Cz), new Vector3(0.6f, 1.8f, pr.Z1 - pr.Z0), r.Stone);
                Box(priory, "PrecinctWallE", new Vector3(pr.X1, y + 0.9f, pr.Cz + 20f), new Vector3(0.6f, 1.8f, pr.Z1 - pr.Z0 - 40f), r.Stone);
            }

            // ---- the bridge to Crowmarsh: timber on piers
            {
                Transform bridge = Group(defences, "Bridge"); float y = TownTerrainBuilder.BaseHeight;
                float len = plan.BridgeX1 - plan.BridgeX0, mid = (plan.BridgeX0 + plan.BridgeX1) * 0.5f;
                Box(bridge, "Deck", new Vector3(mid, y - 0.05f, plan.BridgeZ), new Vector3(len, 0.6f, plan.BridgeWidth), r.DarkWood);
                for (int side = -1; side <= 1; side += 2) Box(bridge, "Rail", new Vector3(mid, y + 0.8f, plan.BridgeZ + side * (plan.BridgeWidth * 0.5f - 0.1f)), new Vector3(len, 0.12f, 0.12f), r.DarkWood);
                for (float x = plan.RiverX0 - 2f; x <= plan.RiverX1 + 2f; x += 12f)
                {
                    Box(bridge, "Pier", new Vector3(x, y - 2.3f, plan.BridgeZ), new Vector3(1.4f, 4.2f, plan.BridgeWidth + 1.2f), r.Stone);
                    for (int side = -1; side <= 1; side += 2) Box(bridge, "Post", new Vector3(x, y + 0.5f, plan.BridgeZ + side * (plan.BridgeWidth * 0.5f - 0.1f)), new Vector3(0.14f, 0.9f, 0.14f), r.DarkWood, collider: false);
                }
            }

            // ---- the market: the well and the cross
            var well = SceneKit.Primitive(PrimitiveType.Cylinder, "Well", root, Vector3.zero, new Vector3(1.6f, 0.5f, 1.6f), r.Stone, keepCollider: true);
            well.transform.position = centre + Vector3.up * 0.5f;
            Box(root, "MarketCross", centre + new Vector3(14f, 1.8f, 20f), new Vector3(0.25f, 3.6f, 0.25f), r.Stone);
            Box(root, "MarketCrossArm", centre + new Vector3(14f, 3.0f, 20f), new Vector3(1.1f, 0.22f, 0.22f), r.Stone, collider: false);
            Box(root, "MarketCrossStep", centre + new Vector3(14f, 0.2f, 20f), new Vector3(1.8f, 0.4f, 1.8f), r.Stone);

            // ---- trees: the crofts inside the walls, the river bank, the country beyond
            var oaks = new List<Transform>(160);
            if (oakMesh != null)
            {
                Transform trees = Group(root, "Oaks");
                Scatter(trees, oaks, oakMesh, r.Bark, leaf, terrain, rng, plan, plan.Bullcroft, 22, 0f);
                Scatter(trees, oaks, oakMesh, r.Bark, leaf, terrain, rng, plan, plan.Kinecroft, 14, 0f);
                var west = new TownRect { X0 = -plan.HalfWidth - 420f, Z0 = -plan.HalfDepth - 300f, X1 = -plan.HalfWidth - 60f, Z1 = plan.HalfDepth + 300f };
                var north = new TownRect { X0 = -plan.HalfWidth - 60f, Z0 = plan.HalfDepth + 70f, X1 = plan.RiverX0 - 20f, Z1 = plan.HalfDepth + 380f };
                var south = new TownRect { X0 = -plan.HalfWidth - 60f, Z0 = -plan.HalfDepth - 380f, X1 = plan.RiverX0 - 20f, Z1 = -plan.HalfDepth - 180f };
                var east = new TownRect { X0 = plan.RiverX1 + 30f, Z0 = -700f, X1 = 760f, Z1 = 700f };
                Scatter(trees, oaks, oakMesh, r.Bark, leaf, terrain, rng, plan, west, 90, 9f);
                Scatter(trees, oaks, oakMesh, r.Bark, leaf, terrain, rng, plan, north, 40, 9f);
                Scatter(trees, oaks, oakMesh, r.Bark, leaf, terrain, rng, plan, south, 30, 9f);
                Scatter(trees, oaks, oakMesh, r.Bark, leaf, terrain, rng, plan, east, 70, 9f);
            }

            // ---- the streets and roads the townsfolk walk
            var streets = new List<float>(96);
            foreach (TownStreet s in plan.Streets) { streets.Add(s.X0); streets.Add(s.Z0); streets.Add(s.X1); streets.Add(s.Z1); }
            streets.AddRange(new[] { 0f, plan.HalfDepth, 0f, plan.HalfDepth + 190f, 0f, -plan.HalfDepth, 0f, -plan.HalfDepth - 190f, -plan.HalfWidth, 0f, -plan.HalfWidth - 190f, 0f });
            streets.AddRange(new[] { plan.HalfWidth, 0f, plan.BridgeX1 + 20f, 0f });
            streets.AddRange(new[] { plan.Bailey.X0 - 16f, 200f, plan.Bailey.X0 + 34f, 200f });
            result.Streets = streets.ToArray();

            // ---- households
            Transform work = Group(marks, "WorkSpots"), corners = Group(marks, "Corners");
            var homes = new List<TownCrowd.Home>(plan.Households.Count);
            int playerHome = -1, hallHome = -1; float bestP = float.MaxValue, bestH = float.MaxValue;
            foreach (TownHousehold h in plan.Households)
            {
                TownBuilding b = plan.Buildings[h.Building];
                if (!byId.TryGetValue(b.Id, out Transform t)) continue;
                var home = new TownCrowd.Home { building = t, kind = kindOf[b.Id], calling = h.Calling, trade = h.GameTrade, adults = (byte)Mathf.Clamp(h.Adults + (h.Servants > 0 ? 1 : 0), 1, 3), works = h.Works, wealth = h.Wealth, french = h.French };
                parishChurch.TryGetValue(h.Parish, out home.parish);
                home.alehouse = NearestOf(aleList, t.position);
                var corner = new GameObject("Corner_" + h.Id).transform; corner.SetParent(corners, false);
                Vector3 cp = t.position + t.forward * (b.HalfDepth + 4.5f); cp.y = SceneKit.GroundY(terrain, cp); corner.position = cp; home.corner = corner;
                Vector3? spot = WorkSpot(plan, h, rng);
                if (spot.HasValue) { var ws = new GameObject("Work_" + h.Id).transform; ws.SetParent(work, false); Vector3 wp = spot.Value; wp.y = SceneKit.GroundY(terrain, wp); ws.position = wp; home.workSpot = ws; }
                // Your own roof: an ordinary house on St Mary's Street. A knight's: the great house nearest the market.
                if (b.Kind == TownBuildingKind.House && !h.French && h.Calling != "moneyer") { float d = (b.X - 0f) * (b.X - 0f) + (b.Z + 170f) * (b.Z + 170f); if (d < bestP) { bestP = d; playerHome = homes.Count; } }
                if (b.Kind == TownBuildingKind.GreatHouse) { float d = (b.X - plan.Market.Cx) * (b.X - plan.Market.Cx) + (b.Z - plan.Market.Cz) * (b.Z - plan.Market.Cz); if (d < bestH) { bestH = d; hallHome = homes.Count; } }
                homes.Add(home);
            }
            if (playerHome >= 0) { var h = homes[playerHome]; h.reserved = true; homes[playerHome] = h; r.Home = h.building; BuildingKit.Furnish(h.building, h.kind, r.DarkWood, r.Stone, r.Linen, r.Fire, r.Iron, withLight: true); }
            if (hallHome >= 0) { var h = homes[hallHome]; h.reserved = true; homes[hallHome] = h; r.HallHouse = h.building; BuildingKit.Furnish(h.building, BuildingKind.Hall, r.DarkWood, r.Stone, r.Linen, r.Fire, r.Iron, withLight: true); }
            result.Homes = homes.ToArray();

            // ---- the site the rest of the game knows the town by
            var site = root.gameObject.AddComponent<SettlementSite>();
            site.settlementId = plan.Id; site.centre = root; site.radius = 760f; site.oaks.AddRange(oaks);
            parishChurch.TryGetValue(0, out Transform marketChurch);
            site.church = marketChurch != null ? marketChurch : (churchList.Count > 0 ? churchList[0] : null);
            site.alehouse = NearestOf(aleList, centre);
            site.keep = result.CastleGate;
            if (site.church != null) BuildingKit.Furnish(site.church, BuildingKind.Church, r.DarkWood, r.Stone, r.Linen, r.Fire, r.Iron, withLight: true);
            if (site.alehouse != null) BuildingKit.Furnish(site.alehouse, BuildingKind.Alehouse, r.DarkWood, r.Stone, r.Linen, r.Fire, r.Iron, withLight: true);
            r.Root = root; r.Church = site.church; r.Alehouse = site.alehouse; r.Green = centre; r.Site = site;
            var workplace = new GameObject("Workplace").transform; workplace.SetParent(root, true);
            workplace.position = r.Home != null ? (BuildingKit.Find(r.Home, "Bench") != null ? BuildingKit.Find(r.Home, "Bench").position : r.Home.position) : centre;
            r.Workplace = workplace;

            // ---- where travellers arrive
            foreach (TownGate g in plan.Gates)
            {
                var sp = new GameObject("gate_" + g.Name.ToLowerInvariant().Replace(' ', '_')).transform; sp.SetParent(marks, false);
                Quaternion face = Quaternion.Euler(0f, g.FacingDeg + 180f, 0f);   // looking into the town
                Vector3 gp = new Vector3(g.X, 0f, g.Z) + face * Vector3.forward * 14f; gp.y = SceneKit.GroundY(terrain, gp) + 0.2f;
                sp.SetPositionAndRotation(gp, face); result.GateSpawns.Add(sp);
                // and where the road leaves: a milestone a bowshot outside the gate (over the bridge, on the east)
                float outBy = g.Name == "Bridge Gate" ? plan.BridgeX1 - plan.HalfWidth + 16f : plan.BankWidth + plan.DitchWidth + 26f;
                Vector3 rp = new Vector3(g.X, 0f, g.Z) + face * Vector3.back * outBy + face * Vector3.right * 3.5f; rp.y = SceneKit.GroundY(terrain, rp);
                var stone = SceneKit.Primitive(PrimitiveType.Cube, "road_" + g.Name.ToLowerInvariant().Replace(' ', '_'), marks, Vector3.zero, new Vector3(0.45f, 1.1f, 0.3f), r.Stone, keepCollider: true);
                stone.transform.position = rp + Vector3.up * 0.5f; result.RoadsOut.Add(stone.transform);
            }

            // ---- people's materials and meshes, as the village builder returns them
            r.Villager = SceneKit.Mat("Villager", SceneKit.LitShader, new Color(0.3f, 0.26f, 0.2f), 0f, 0.9f);
            r.Skin = SceneKit.Mat("Skin", SceneKit.LitShader, new Color(0.58f, 0.46f, 0.37f), 0f, 0.9f);
            r.Man = SceneKit.ArtMesh("Characters/Man.fbx"); r.Woman = SceneKit.ArtMesh("Characters/Woman.fbx"); r.Deer = SceneKit.ArtMesh("Characters/Deer.fbx");
            r.DeerCoat = SceneKit.Mat("DeerCoat", SceneKit.LitShader, new Color(0.42f, 0.30f, 0.19f), 0f, 0.95f);
            r.Antler = SceneKit.Mat("Antler", SceneKit.LitShader, new Color(0.24f, 0.19f, 0.14f), 0f, 0.9f);
            r.Earth = SceneKit.Mat("PloughedEarth", SceneKit.LitShader, new Color(0.26f, 0.20f, 0.15f), 0f, 1f);
            Debug.Log($"[Ashes and Crowns] {plan.Name}: {plan.Houses} houses, {plan.Population} souls, {plan.ParishChurches - 1} parish churches, {plan.AreaHectares:F0} ha inside the ramparts.");
            return result;
        }

        private static Transform Group(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); t.position = Vector3.zero; return t; }

        private static Transform NearestOf(List<Transform> list, Vector3 p)
        {
            Transform best = null; float bestD = float.MaxValue;
            foreach (Transform t in list) { float d = (t.position - p).sqrMagnitude; if (d < bestD) { bestD = d; best = t; } }
            return best;
        }

        /// <summary>Where a household's head spends the working day when it is not at the shop board.</summary>
        private static Vector3? WorkSpot(TownPlanData p, TownHousehold h, DeterministicRng rng)
        {
            switch (h.Works)
            {
                case Workplace.Market: return new Vector3(rng.Range(p.Market.X0 + 4f, p.Market.X1 - 4f), 0f, rng.Range(p.Market.Z0 + 4f, p.Market.Z1 - 4f));
                case Workplace.Wharf: return new Vector3(p.HalfWidth + rng.Range(-3f, 4f), 0f, rng.Range(-p.HalfDepth + 50f, -30f));
                case Workplace.Fields: return new Vector3((rng.Chance(0.5f) ? 1f : -1f) * rng.Range(10f, 70f), 0f, -p.HalfDepth - rng.Range(60f, 170f));
                case Workplace.Castle: return new Vector3(p.Bailey.X0 + rng.Range(20f, 60f), 0f, 200f + rng.Range(-30f, 30f));
                case Workplace.Yard: return null;      // the yard behind the house; the bench stands in for it
                default: return null;
            }
        }

        private static void Stake(Transform parent, Mesh mesh, Material a, Material b, Terrain terrain, Vector3 pos, float yaw, int n)
        {
            var go = SceneKit.MeshChild("Stakes_" + n, parent, mesh, a, b);
            pos.y = SceneKit.GroundY(terrain, pos);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f)); go.isStatic = true;
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0f, 1.3f, 0f); col.size = new Vector3(4f, 3.6f, 0.4f);
        }

        private static void Wall(Transform parent, Mesh mesh, Material a, Material b, Terrain terrain, Vector3 pos, float yaw, int n)
        {
            var go = SceneKit.MeshChild("Wall_" + n, parent, mesh, a, b);
            pos.y = SceneKit.GroundY(terrain, pos) - 0.3f;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f)); go.isStatic = true;
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0.2f, 4f, 0f); col.size = new Vector3(10.5f, 8f, 2.2f);
        }

        private static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider = true)
        {
            var go = SceneKit.Primitive(PrimitiveType.Cube, name, parent, Vector3.zero, size, mat, keepCollider: collider);
            go.transform.position = pos; go.isStatic = true;
        }

        private static void Scatter(Transform parent, List<Transform> list, Mesh oak, Material bark, Material leaf, Terrain terrain, DeterministicRng rng, TownPlanData plan, TownRect area, int count, float roadClear)
        {
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(rng.Range(area.X0, area.X1), 0f, rng.Range(area.Z0, area.Z1));
                if (roadClear > 0f && (Mathf.Abs(pos.x) < roadClear || Mathf.Abs(pos.z) < roadClear)) continue;          // keep the roads open
                if (pos.x > plan.RiverX0 - 12f && pos.x < plan.RiverX1 + 12f) continue;                                   // not in the river
                pos.y = SceneKit.GroundY(terrain, pos) - 0.2f;
                var t = SceneKit.MeshChild("Oak_" + list.Count, parent, oak, bark, leaf);
                t.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f));
                float s = rng.Range(0.8f, 1.3f); t.transform.localScale = new Vector3(s, s, s); t.isStatic = true;
                var col = t.AddComponent<CapsuleCollider>(); col.center = new Vector3(0f, 2.5f, 0f); col.radius = 0.45f; col.height = 5f;
                list.Add(t.transform);
            }
        }
    }
}

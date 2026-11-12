using System.Collections.Generic;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// People and their stations for a village. Shared by the editor builder (Ashby) and the runtime spawner (every
    /// other village) so a village is the same thing wherever it comes from.
    /// </summary>
    public static class VillageKit
    {
        public static readonly string[] Trades = { "farmer", "blacksmith", "miller", "hunter", "brewer", "carpenter", "shepherd", "fisherman" };
        private static readonly Color[] Dyes =
        {
            new Color(0.33f, 0.28f, 0.20f), new Color(0.40f, 0.36f, 0.28f), new Color(0.28f, 0.30f, 0.24f), new Color(0.36f, 0.26f, 0.22f),
            new Color(0.30f, 0.32f, 0.38f), new Color(0.42f, 0.34f, 0.22f), new Color(0.26f, 0.24f, 0.22f), new Color(0.38f, 0.30f, 0.30f),
        };
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static bool IsTrade(string t) { foreach (string x in Trades) if (x == t) return true; return false; }
        public static Color Dye(int id) => Dyes[((id % Dyes.Length) + Dyes.Length) % Dyes.Length];

        /// <summary>Furnish every building of a village and people it. Huts are assigned to villagers round robin; each trade
        /// gets a station (a prop beside the hut, a field beyond it, or the alehouse bar). Returns the number of villagers made.</summary>
        public static int Populate(SettlementSite site, Transform root, Mesh man, Mesh woman, Material cloth, Material skin, Material wood, Material stone, Material linen, Material fire, Material iron, NameGenerator names, DeterministicRng rng, Terrain terrain, bool lights)
        {
            if (site.church != null && BuildingKit.Find(site.church, "Door") == null) BuildingKit.Furnish(site.church, BuildingKind.Church, wood, stone, linen, fire, iron, lights);
            if (site.alehouse != null && BuildingKit.Find(site.alehouse, "Door") == null) BuildingKit.Furnish(site.alehouse, BuildingKind.Alehouse, wood, stone, linen, fire, iron, lights);
            foreach (Transform hut in site.huts) if (BuildingKit.Find(hut, "Door") == null) BuildingKit.Furnish(hut, BuildingKind.Hut, wood, stone, linen, fire, iron, lights);
            if (site.huts.Count == 0) return 0;
            var people = new GameObject("People").transform; people.SetParent(root, true);
            int made = 0;
            for (int k = 0; k < Trades.Length; k++)
            {
                string trade = Trades[k];
                Transform hut = site.huts[k % site.huts.Count];
                bool female = trade == "brewer" || k % 4 == 3;
                var v = Spawn(people, site, trade, hut, names.Given(female, false), female, 100 + k, rng, female && woman != null ? woman : man, cloth, skin, wood, stone, iron, terrain);
                made++;
                // A second soul in most huts: spouse or grown child, who works the same trade.
                if (k % 2 == 0)
                {
                    Spawn(people, site, trade, hut, names.Given(!female, false), !female, 140 + k, rng, !female && woman != null ? woman : man, cloth, skin, wood, stone, iron, terrain, kin: true, station: v.workStation, stationBuilding: v.workBuilding);
                    made++;
                }
            }
            // The priest lives by his altar.
            if (site.church != null) { Spawn(people, site, "priest", site.church, names.Given(false, true), false, 190, rng, man, cloth, skin, wood, stone, iron, terrain, station: BuildingKit.Find(site.church, "Altar"), stationBuilding: site.church); made++; }
            return made;
        }

        public static Villager Spawn(Transform parent, SettlementSite site, string trade, Transform hut, string name, bool female, int id, DeterministicRng rng, Mesh man, Material cloth, Material skin, Material wood, Material stone, Material iron, Terrain terrain, bool kin = false, Transform station = null, Transform stationBuilding = null)
        {
            var go = new GameObject((trade == "priest" ? "Father " : string.Empty) + name + (trade == "priest" ? string.Empty : " the " + trade));
            go.transform.SetParent(parent, true);
            Vector3 start = BuildingKit.Find(hut, "Door") != null ? BuildingKit.Find(hut, "Door").position : hut.position + hut.forward * 4f;
            go.transform.position = start;
            var cc = go.AddComponent<CharacterController>(); cc.height = 1.7f; cc.radius = 0.32f; cc.center = new Vector3(0f, 0.9f, 0f); cc.stepOffset = 0.45f; cc.slopeLimit = 50f;
            GameObject body;
            if (man != null)
            {
                body = new GameObject("Body"); body.transform.SetParent(go.transform, false);
                var mf = body.AddComponent<MeshFilter>(); mf.sharedMesh = man;
                var mr = body.AddComponent<MeshRenderer>(); mr.sharedMaterials = man.subMeshCount >= 2 ? new[] { cloth, skin } : new[] { cloth };
                var block = new MaterialPropertyBlock(); block.SetColor(BaseColor, Dyes[id % Dyes.Length]); mr.SetPropertyBlock(block, 0);
            }
            else
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Body"; Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(go.transform, false); body.transform.localPosition = new Vector3(0f, 0.95f, 0f); body.transform.localScale = new Vector3(0.5f, 0.72f, 0.5f);
                body.GetComponent<Renderer>().sharedMaterial = cloth;
            }
            var tag = go.AddComponent<VillagerTag>(); tag.npcId = id; tag.trade = trade; tag.displayName = name; tag.disposition = rng.Range(-0.2f, 0.5f);
            var v = go.AddComponent<Villager>(); v.site = site; v.home = hut; v.visual = body.transform; v.variant = id; v.female = female;
            if (station != null) { v.workStation = station; v.workBuilding = stationBuilding; }
            else Station(v, trade, hut, site, wood, stone, iron, terrain, rng);
            site.villagers.Add(v);
            return v;
        }

        /// <summary>Where the trade is worked: a prop in the yard for crafts, a field for the land, the bar for the brewer.</summary>
        /// <summary>A station for the player's own trade, beside their roof: the field strips, the anvil, the bench. Work happens there.</summary>
        public static Transform MakeStation(string trade, Transform home, SettlementSite site, Material wood, Material stone, Material iron, Terrain terrain, DeterministicRng rng)
        {
            var probe = new GameObject("StationProbe").AddComponent<Villager>();
            Station(probe, trade, home, site, wood, stone, iron, terrain, rng);
            Transform station = probe.workStation;
            Object.Destroy(probe.gameObject);
            return station;
        }

        private static void Station(Villager v, string trade, Transform hut, SettlementSite site, Material wood, Material stone, Material iron, Terrain terrain, DeterministicRng rng)
        {
            Transform yard = new GameObject("Station_" + trade).transform; yard.SetParent(hut.parent, true);
            Vector3 side = hut.right * (hut.GetSiblingIndex() % 2 == 0 ? 1f : -1f);
            Vector3 pos = hut.position + hut.forward * 1.5f + side * 4.2f;
            switch (trade)
            {
                case "blacksmith":
                    Prop(yard, PrimitiveType.Cube, pos + Vector3.up * 0.35f, new Vector3(0.5f, 0.7f, 0.5f), wood);           // block
                    Prop(yard, PrimitiveType.Cube, pos + Vector3.up * 0.82f, new Vector3(0.75f, 0.25f, 0.3f), iron);         // anvil
                    Prop(yard, PrimitiveType.Cylinder, pos + side * 1.4f + Vector3.up * 0.4f, new Vector3(1.2f, 0.4f, 1.2f), stone);  // forge
                    break;
                case "carpenter":
                    Prop(yard, PrimitiveType.Cube, pos + Vector3.up * 0.45f, new Vector3(1.8f, 0.12f, 0.35f), wood);
                    Prop(yard, PrimitiveType.Cube, pos + Vector3.up * 0.2f, new Vector3(0.2f, 0.4f, 0.3f), wood);
                    break;
                case "miller":
                    Prop(yard, PrimitiveType.Cylinder, pos + Vector3.up * 0.3f, new Vector3(1.0f, 0.3f, 1.0f), stone);
                    break;
                case "brewer":
                    v.workStation = BuildingKit.Find(site.alehouse, "Bar"); v.workBuilding = site.alehouse; return;
                case "fisherman":
                    Prop(yard, PrimitiveType.Cube, pos + Vector3.up * 0.3f, new Vector3(1.4f, 0.6f, 0.5f), wood);           // traps
                    break;
                default:
                    {
                        // The land: out past the huts, away from the green.
                        Vector3 away = (hut.position - site.centre.position); away.y = 0f; away = away.sqrMagnitude > 0.1f ? away.normalized : hut.forward;
                        pos = hut.position + away * (trade == "shepherd" || trade == "hunter" ? 42f : 26f) + Quaternion.Euler(0f, 90f, 0f) * away * rng.Range(-8f, 8f);
                        if (trade == "farmer")
                            for (int i = 0; i < 4; i++) Prop(yard, PrimitiveType.Cube, pos + Quaternion.Euler(0f, 90f, 0f) * away * (i * 2.2f - 3.3f) + Vector3.up * 0.1f, new Vector3(14f, 0.2f, 1.0f), stone); // ridge and furrow
                        break;
                    }
            }
            if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            yard.position = pos;
            v.workStation = yard; v.workBuilding = null;
        }

        private static readonly Color[] GoodColours = { new Color(0.55f, 0.48f, 0.30f), new Color(0.62f, 0.58f, 0.48f), new Color(0.30f, 0.31f, 0.33f), new Color(0.45f, 0.30f, 0.20f), new Color(0.50f, 0.42f, 0.26f) };

        /// <summary>Stalls on the green: trestles with the goods the market has to spare laid out on them, three to a stall.
        /// Buying and selling happen at the good itself. Towns also get a banner post: the muster.</summary>
        public static void BuildMarket(SettlementSite site, Transform root, Market market, ItemsData items, Material wood, Material linen, Terrain terrain, bool town)
        {
            if (site == null || site.centre == null || market == null) return;
            var stalls = new GameObject("Market").transform; stalls.SetParent(root, true);
            var list = new List<MarketGood>(16);
            foreach (var g in market.Goods) if (g.Stock >= 1f && items.Find(g.Id) != null) list.Add(g);
            list.Sort((a, b) => b.Stock.CompareTo(a.Stock));
            int count = Mathf.Min(list.Count, town ? 15 : 9);
            float green = 16f;
            for (int i = 0; i < count; i++)
            {
                int stall = i / 3, slot = i % 3;
                float a = (0.75f + stall * 0.5f) * Mathf.PI;   // stalls around the south and west of the green, off the church and alehouse sides
                Vector3 sc = site.centre.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (green - 4f);
                if (terrain != null) sc.y = terrain.SampleHeight(sc) + terrain.transform.position.y;
                Quaternion face = Quaternion.LookRotation((site.centre.position - sc).normalized, Vector3.up);
                if (slot == 0)
                {
                    var table = GameObject.CreatePrimitive(PrimitiveType.Cube); table.name = "Stall"; table.transform.SetParent(stalls, true);
                    table.transform.SetPositionAndRotation(sc + Vector3.up * 0.75f, face); table.transform.localScale = new Vector3(2.6f, 0.08f, 0.9f); table.GetComponent<Renderer>().sharedMaterial = wood;
                    var awning = GameObject.CreatePrimitive(PrimitiveType.Cube); awning.name = "Awning"; Object.DestroyImmediate(awning.GetComponent<Collider>()); awning.transform.SetParent(stalls, true);
                    awning.transform.SetPositionAndRotation(sc + Vector3.up * 2.2f + face * Vector3.back * 0.2f, face * Quaternion.Euler(12f, 0f, 0f)); awning.transform.localScale = new Vector3(2.8f, 0.04f, 1.6f); awning.GetComponent<Renderer>().sharedMaterial = linen;
                    for (int k = -1; k <= 1; k += 2) { var post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.name = "Post"; Object.DestroyImmediate(post.GetComponent<Collider>()); post.transform.SetParent(stalls, true); post.transform.SetPositionAndRotation(sc + face * new Vector3(k * 1.3f, 1.1f, -0.5f), face); post.transform.localScale = new Vector3(0.08f, 2.2f, 0.08f); post.GetComponent<Renderer>().sharedMaterial = wood; }
                }
                var good = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere); good.name = "Good_" + list[i].Id; Object.DestroyImmediate(good.GetComponent<Collider>());
                good.transform.SetParent(stalls, true);
                good.transform.SetPositionAndRotation(sc + face * new Vector3((slot - 1) * 0.8f, 0.95f, 0f), face); good.transform.localScale = new Vector3(0.32f, 0.3f, 0.32f);
                var mr = good.GetComponent<Renderer>(); mr.sharedMaterial = linen;
                var block = new MaterialPropertyBlock(); block.SetColor(BaseColor, GoodColours[i % GoodColours.Length]); mr.SetPropertyBlock(block);
                var tag = good.AddComponent<MarketGoodTag>(); tag.itemId = list[i].Id;
                site.goods.Add(tag);
            }
            if (town)
            {
                Vector3 mp = site.centre.position + new Vector3(0f, 0f, -green + 3f); if (terrain != null) mp.y = terrain.SampleHeight(mp) + terrain.transform.position.y;
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.name = "MusterPost"; post.transform.SetParent(stalls, true); post.transform.position = mp + Vector3.up * 2.0f; post.transform.localScale = new Vector3(0.14f, 4.0f, 0.14f); post.GetComponent<Renderer>().sharedMaterial = wood;
                var banner = GameObject.CreatePrimitive(PrimitiveType.Cube); banner.name = "Banner"; Object.DestroyImmediate(banner.GetComponent<Collider>()); banner.transform.SetParent(stalls, true); banner.transform.position = mp + new Vector3(0.5f, 3.4f, 0f); banner.transform.localScale = new Vector3(0.9f, 0.6f, 0.03f);
                var bmr = banner.GetComponent<Renderer>(); bmr.sharedMaterial = linen; var bb = new MaterialPropertyBlock(); bb.SetColor(BaseColor, new Color(0.45f, 0.10f, 0.08f)); bmr.SetPropertyBlock(bb);
                site.muster = post.transform;
            }
        }

        /// <summary>Deer herds in the woods beyond the fields. Each herd is a parent with its ground as the centre.</summary>
        public static void SpawnHerds(SettlementSite site, Transform root, Mesh deer, Material coat, Material antler, Terrain terrain, DeterministicRng rng, WildlifeBalance w)
        {
            if (deer == null || site == null || site.centre == null) return;
            for (int h = 0; h < w.herdsPerVillage; h++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f);
                Vector3 c = site.centre.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (w.herdDistanceMetres + rng.Range(0f, 60f));
                if (terrain != null) c.y = terrain.SampleHeight(c) + terrain.transform.position.y;
                var herd = new GameObject("Herd_" + h).transform; herd.SetParent(root, true); herd.position = c;
                for (int i = 0; i < w.deerPerHerd; i++)
                {
                    var go = new GameObject("Deer"); go.transform.SetParent(herd, true);
                    Vector3 p = c + new Vector3(rng.Range(-8f, 8f), 0f, rng.Range(-8f, 8f)); if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                    go.transform.position = p; go.transform.rotation = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
                    var body = new GameObject("Body"); body.transform.SetParent(go.transform, false);
                    var mf = body.AddComponent<MeshFilter>(); mf.sharedMesh = deer;
                    var mr = body.AddComponent<MeshRenderer>(); mr.sharedMaterials = deer.subMeshCount >= 2 ? new[] { coat, antler } : new[] { coat };
                    var col = go.AddComponent<CapsuleCollider>(); col.center = new Vector3(0f, 0.8f, 0f); col.radius = 0.35f; col.height = 1.6f; col.direction = 2;
                    float sc = rng.Range(0.85f, 1.1f); if (i % 3 != 0) { sc *= 0.92f; }   // hinds are smaller; the stag keeps his antlers in the mesh regardless, for now
                    go.transform.localScale = new Vector3(sc, sc, sc);
                    var d = go.AddComponent<Deer>(); d.herdCentre = c; d.index = i + h * 7; d.visual = body.transform;
                    site.deer.Add(d);
                }
            }
        }

        /// <summary>Woods: clumps of oaks beyond the fields, far denser than the few about the green.</summary>
        public static void PlantWoods(SettlementSite site, Transform root, Mesh oak, Material bark, Material leaf, Terrain terrain, DeterministicRng rng, int clumps, int perClump, float distance)
        {
            if (oak == null || site == null || site.centre == null) return;
            var woods = new GameObject("Woods").transform; woods.SetParent(root, true);
            for (int c = 0; c < clumps; c++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f); float r = distance + rng.Range(0f, 80f);
                Vector3 centre = site.centre.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                for (int i = 0; i < perClump; i++)
                {
                    float ta = rng.Range(0f, Mathf.PI * 2f), tr = Mathf.Sqrt(rng.Range(0f, 1f)) * 34f;
                    Vector3 p = centre + new Vector3(Mathf.Cos(ta), 0f, Mathf.Sin(ta)) * tr;
                    if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y - 0.2f;
                    var go = new GameObject("Oak"); go.transform.SetParent(woods, true);
                    go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f));
                    float sc = rng.Range(0.75f, 1.35f); go.transform.localScale = new Vector3(sc, sc, sc);
                    var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = oak;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = oak.subMeshCount >= 2 ? new[] { bark, leaf } : new[] { bark };
                    var col = go.AddComponent<CapsuleCollider>(); col.center = new Vector3(0f, 2.5f, 0f); col.radius = 0.45f; col.height = 5f;   // the trunk only; walk under the canopy
                    go.isStatic = root.gameObject.isStatic;
                    site.oaks.Add(go.transform);
                }
            }
        }

        /// <summary>The open fields: strips of ridge and furrow in a belt around the village, with the odd hurdle fence.</summary>
        public static void LayFields(SettlementSite site, Transform root, Material earth, Material wood, Terrain terrain, DeterministicRng rng, float innerRadius, int strips)
        {
            if (site == null || site.centre == null) return;
            var fields = new GameObject("Fields").transform; fields.SetParent(root, true);
            for (int i = 0; i < strips; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f); float r = innerRadius + rng.Range(0f, 45f);
                Vector3 c = site.centre.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (terrain != null) c.y = terrain.SampleHeight(c) + terrain.transform.position.y;
                Quaternion rot = Quaternion.Euler(0f, rng.Range(0f, 180f), 0f);
                int ridges = rng.Range(4, 8);
                for (int k = 0; k < ridges; k++)
                {
                    var ridge = GameObject.CreatePrimitive(PrimitiveType.Cube); ridge.name = "Ridge"; Object.DestroyImmediate(ridge.GetComponent<Collider>());
                    ridge.transform.SetParent(fields, true);
                    Vector3 p = c + rot * new Vector3((k - ridges / 2f) * 2.2f, 0.08f, 0f);
                    if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.08f;
                    ridge.transform.SetPositionAndRotation(p, rot); ridge.transform.localScale = new Vector3(1.1f, 0.22f, 18f + rng.Range(0f, 10f));
                    ridge.GetComponent<Renderer>().sharedMaterial = earth;
                }
                if (rng.Chance(0.5f))
                {
                    var hurdle = GameObject.CreatePrimitive(PrimitiveType.Cube); hurdle.name = "Hurdle"; hurdle.transform.SetParent(fields, true);
                    Vector3 p = c + rot * new Vector3(0f, 0.5f, 12f); if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.5f;
                    hurdle.transform.SetPositionAndRotation(p, rot); hurdle.transform.localScale = new Vector3(ridges * 2.2f, 1.0f, 0.08f);
                    hurdle.GetComponent<Renderer>().sharedMaterial = wood;
                }
            }
        }

        private static void Prop(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type); go.transform.SetParent(parent, true);
            go.transform.position = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}

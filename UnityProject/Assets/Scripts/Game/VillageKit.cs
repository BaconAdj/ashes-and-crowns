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
        public static Color Dye(int id) => Dyes[((id % Dyes.Length) + Dyes.Length) % Dyes.Length];

        /// <summary>Furnish every building of a village and people it. Huts are assigned to villagers round robin; each trade
        /// gets a station (a prop beside the hut, a field beyond it, or the alehouse bar). Returns the number of villagers made.</summary>
        public static int Populate(SettlementSite site, Transform root, Mesh man, Material cloth, Material skin, Material wood, Material stone, Material linen, Material fire, Material iron, NameGenerator names, DeterministicRng rng, Terrain terrain, bool lights)
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
                var v = Spawn(people, site, trade, hut, names.Given(female, false), female, 100 + k, rng, man, cloth, skin, wood, stone, iron, terrain);
                made++;
                // A second soul in most huts: spouse or grown child, who works the same trade.
                if (k % 2 == 0)
                {
                    Spawn(people, site, trade, hut, names.Given(!female, false), !female, 140 + k, rng, man, cloth, skin, wood, stone, iron, terrain, kin: true, station: v.workStation, stationBuilding: v.workBuilding);
                    made++;
                }
            }
            // The priest lives by his altar.
            if (site.church != null) { Spawn(people, site, "priest", site.church, names.Given(false, true), false, 190, rng, man, cloth, skin, wood, stone, iron, terrain, station: BuildingKit.Find(site.church, "Altar"), stationBuilding: site.church); made++; }
            return made;
        }

        private static Villager Spawn(Transform parent, SettlementSite site, string trade, Transform hut, string name, bool female, int id, DeterministicRng rng, Mesh man, Material cloth, Material skin, Material wood, Material stone, Material iron, Terrain terrain, bool kin = false, Transform station = null, Transform stationBuilding = null)
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
                if (female) body.transform.localScale = new Vector3(0.94f, 0.95f, 0.94f);
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

        private static void Prop(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type); go.transform.SetParent(parent, true);
            go.transform.position = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}

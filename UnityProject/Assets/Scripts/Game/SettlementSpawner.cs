using System.Collections.Generic;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// Builds the run's procedural villages at play time (they change with the seed, so they cannot be baked into the scene):
    /// a few huts around a green, an alehouse, a small church, flattened ground. Fixed places already have keeps from the scene builder.
    /// </summary>
    public sealed class SettlementSpawner : MonoBehaviour
    {
        public WorldAssets assets;
        public Transform fixedSitesRoot;    // SettlementSite children created by the scene builder (home village, keeps)
        public Transform strongholdsRoot;   // the scene builder's keeps, named Keep_<settlement id>
        private readonly List<SettlementSite> _sites = new List<SettlementSite>(24);
        private NamesData _names;
        private bool _built;

        public IReadOnlyList<SettlementSite> Sites => _sites;

        public void Build(IList<Settlement> settlements, float terrainSize, DeterministicRng rng)
        {
            if (_built) return;
            _built = true;
            // Work on a copy: SetHeights on the saved TerrainData would dent the asset permanently in the editor.
            var td = Instantiate(assets.terrain.terrainData); td.name = assets.terrain.terrainData.name + " (runtime)";
            assets.terrain.terrainData = td;
            var col = assets.terrain.GetComponent<TerrainCollider>(); if (col != null) col.terrainData = td;
            if (fixedSitesRoot != null) foreach (Transform t in fixedSitesRoot) { var site = t.GetComponent<SettlementSite>(); if (site != null) _sites.Add(site); }
            foreach (var s in settlements)
            {
                if (s.IsFixed)
                {
                    SettlementSite site = EnsureFixedSite(s, terrainSize);
                    if (strongholdsRoot != null && site.keep == null) site.keep = strongholdsRoot.Find("Keep_" + s.Id);
                    // A keep stub is not a town: give every fixed place a church, an alehouse, huts and people on its north side.
                    if (site.huts.Count == 0 && (site.alehouse == null || site.alehouse == site.centre)) BuildVillage(s, terrainSize, rng.Fork(MapGeometry.StableHash(s.Id)), site);
                    continue;
                }
                BuildVillage(s, terrainSize, rng.Fork(MapGeometry.StableHash(s.Id)));
            }
            // Stalls: every settlement lays out what its market has, Ashby included (its market exists only once the run does).
            var ctx = RunSession.Context;
            if (ctx != null)
                foreach (var site in _sites)
                {
                    var s = ctx.FindSettlement(site.settlementId);
                    if (s == null || s.Market == null || site.goods.Count > 0) continue;
                    VillageKit.BuildMarket(site, site.transform, s.Market, ctx.Items, assets.darkWood, assets.linen != null ? assets.linen : assets.wattle, assets.terrain, s.Type >= SettlementType.Town);
                }
        }

        /// <summary>The player's kin, spawned under the player's roof when a life begins: they share the hearth, work the same
        /// land and keep the same hours. A knight's household gets a steward and a groom besides.</summary>
        public Transform SpawnHousehold(Transform home, RunContext ctx, DeterministicRng rng)
        {
            if (home == null || ctx == null) return null;
            SettlementSite site = null; float best = float.MaxValue;
            foreach (var s in _sites) { if (s == null || s.centre == null) continue; float d = (s.centre.position - home.position).sqrMagnitude; if (d < best) { best = d; site = s; } }
            if (site == null) return null;
            foreach (Villager old in home.GetComponentsInChildren<Villager>()) Destroy(old.gameObject);   // a second life under the same roof
            var people = home.Find("Household"); if (people == null) { people = new GameObject("Household").transform; people.SetParent(home, false); }
            string trade = string.IsNullOrEmpty(ctx.Character.Trade) ? "farmer" : ctx.Character.Trade;
            // Your own station beside your roof: the strips, the anvil, the bench. Your kin work it with you.
            Transform work = VillageKit.MakeStation(trade, home, site, assets.darkWood, assets.stone, assets.iron != null ? assets.iron : assets.slate, assets.terrain, rng.Fork(3));
            int id = 300;
            foreach (Npc kin in ctx.Family)
            {
                if (kin == null || kin.Relation == Relation.Steward || kin.Relation == Relation.Captain || kin.Relation == Relation.Queen) continue;
                var v = VillageKit.Spawn(people, site, trade, home, kin.Name, kin.Female, id++, rng, kin.Female && assets.woman != null ? assets.woman : assets.man, assets.villager != null ? assets.villager : assets.wattle, assets.skin != null ? assets.skin : assets.wattle, assets.darkWood, assets.stone, assets.iron != null ? assets.iron : assets.slate, assets.terrain, kin: true, station: work, stationBuilding: null);
                v.Tag.displayName = kin.Name; v.Tag.trade = RelationWord(kin.Relation);
                v.variant = id * 3;   // kin keep different hours from the neighbours so the hut is not a dormitory
                if (kin.Age < 12) v.visual.localScale = new Vector3(0.7f, 0.65f, 0.7f);
            }
            if (ctx.Character.Rank >= Rank.Knight)
            {
                VillageKit.Spawn(people, site, "steward", home, "Osbert", false, id++, rng, assets.man, assets.villager, assets.skin, assets.darkWood, assets.stone, assets.iron, assets.terrain, station: BuildingKit.Find(home, "Dais"), stationBuilding: home).Tag.displayName = "Osbert";
            }
            return work;
        }

        private static string RelationWord(Relation r)
        {
            switch (r)
            {
                case Relation.Father: return "your father"; case Relation.Mother: return "your mother"; case Relation.Spouse: return "your spouse";
                case Relation.Child: case Relation.Heir: return "your child"; case Relation.Sibling: case Relation.Sister: case Relation.ElderBrother: case Relation.HalfBrother: case Relation.HalfSibling: return "your sibling";
                case Relation.BrotherInLaw: return "your brother-in-law"; default: return "your kin";
            }
        }

        /// <summary>A settlement that appears after the scene started (an adulterine castle).</summary>
        public void AddSite(Settlement s, float size) { if (_built) EnsureFixedSite(s, size); }

        private SettlementSite EnsureFixedSite(Settlement s, float size)
        {
            foreach (var site in _sites) if (site.settlementId == s.Id) return site;
            var go = new GameObject("Site_" + s.Id);
            go.transform.SetParent(transform, true);
            var pos = new Vector3(s.Nx * size, 0f, s.Nz * size); pos.y = Ground(pos);
            go.transform.position = pos;
            var site2 = go.AddComponent<SettlementSite>(); site2.settlementId = s.Id; site2.centre = go.transform; site2.radius = 80f;
            // The alehouse of a town is its gate for now: trading and jobs happen at the centre marker.
            site2.alehouse = go.transform;
            _sites.Add(site2);
            return site2;
        }

        private void BuildVillage(Settlement s, float size, DeterministicRng rng, SettlementSite existing = null)
        {
            Transform root = existing != null ? existing.transform : new GameObject("Village_" + s.Name).transform;
            if (existing == null) root.SetParent(transform, true);
            var centre = existing != null ? existing.centre.position : new Vector3(s.Nx * size, 0f, s.Nz * size);
            Flatten(centre, existing != null ? 70f : 55f);
            centre.y = Ground(centre);
            root.position = centre;
            float green = 16f;
            bool town = existing != null;   // the keep stands south of a fixed place's centre: build on the north side
            Transform site_keep_placeholder = null;
            Transform church = Place(root, "Church", assets.church, new[] { assets.stone, assets.slate }, centre + new Vector3(town ? -green - 20f : 0f, 0f, town ? 6f : green + 12f), Quaternion.Euler(0f, 90f, 0f));
            Transform ale = Place(root, "Alehouse", assets.alehouse, new[] { assets.wattle, assets.thatch }, centre + new Vector3(green + 8f, 0f, -3f), Quaternion.Euler(0f, 90f, 0f));
            int huts = town ? Mathf.Clamp(s.Population / 60, 8, 16) : Mathf.Clamp(s.Population / 25, 3, 8);
            var hutList = new List<Transform>(huts);
            for (int i = 0; i < huts; i++)
            {
                float a0 = town ? 10f : 190f, a1 = town ? 170f : 350f;
                int ring = town ? i / 8 : 0;   // towns take a second ring
                float a = Mathf.Lerp(a0, a1, (i % 8) / 7f) * Mathf.Deg2Rad + rng.Range(-0.15f, 0.15f);
                float r = green + 5f + ring * 11f + rng.Range(0f, 9f);
                Vector3 pos = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vector3 toGreen = centre - pos; toGreen.y = 0f;
                hutList.Add(Place(root, "Hut_" + i, assets.hut, new[] { assets.wattle, assets.thatch }, pos, Quaternion.LookRotation(toGreen.normalized) * Quaternion.Euler(0f, rng.Range(-10f, 10f), 0f)));
            }
            var oakList = new List<Transform>(14);
            if (assets.oak != null)
            {
                for (int i = 0; i < 14; i++)
                {
                    float a = rng.Range(0f, Mathf.PI * 2f), r = green + 34f + rng.Range(0f, 30f);
                    Vector3 pos = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    var t = Place(root, "Oak_" + i, assets.oak, new[] { assets.bark != null ? assets.bark : assets.darkWood, assets.leaf != null ? assets.leaf : assets.thatch }, pos, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f));
                    float sc = rng.Range(0.8f, 1.25f); t.localScale = new Vector3(sc, sc, sc);
                    oakList.Add(t);
                }
            }
            if (existing == null && s.Type == SettlementType.Castle && assets.keep != null) site_keep_placeholder = Place(root, "Keep", assets.keep, new[] { assets.stone, assets.darkWood }, centre + new Vector3(-green - 22f, 0f, green + 10f), Quaternion.identity);
            var well = GameObject.CreatePrimitive(PrimitiveType.Cylinder); well.name = "Well"; well.transform.SetParent(root, true);
            well.transform.localScale = new Vector3(1.6f, 0.5f, 1.6f); well.transform.position = centre + Vector3.up * 0.5f; well.GetComponent<Renderer>().sharedMaterial = assets.stone;
            var site = existing != null ? existing : root.gameObject.AddComponent<SettlementSite>();
            site.settlementId = s.Id; site.centre = root; site.church = church; site.alehouse = ale; site.radius = town ? 100f : 60f;
            site.huts.AddRange(hutList); site.oaks.AddRange(oakList);
            if (site_keep_placeholder != null) site.keep = site_keep_placeholder;
            var wb = GameBootstrap.Instance != null ? GameBootstrap.Instance.Balance.wildlife : new WildlifeBalance();
            VillageKit.PlantWoods(site, root, assets.oak, assets.bark != null ? assets.bark : assets.darkWood, assets.leaf != null ? assets.leaf : assets.thatch, assets.terrain, rng.Fork(9), town ? 5 : 3, town ? 30 : 26, town ? 170f : 110f);
            VillageKit.LayFields(site, root, assets.stone, assets.darkWood, assets.terrain, rng.Fork(10), green + 40f, town ? 14 : 8);
            VillageKit.SpawnHerds(site, root, assets.deer, assets.deerCoat != null ? assets.deerCoat : assets.darkWood, assets.antler != null ? assets.antler : assets.darkWood, assets.terrain, rng.Fork(11), wb);
            if (_names == null) _names = DataFiles.LoadNames();
            VillageKit.Populate(site, root, assets.man, assets.woman, assets.villager != null ? assets.villager : assets.wattle, assets.skin != null ? assets.skin : assets.wattle, assets.darkWood, assets.stone, assets.linen != null ? assets.linen : assets.wattle, assets.fire != null ? assets.fire : assets.thatch, assets.iron != null ? assets.iron : assets.slate, new NameGenerator(_names, rng.Fork(5)), rng.Fork(7), assets.terrain, lights: true);
            if (existing == null) _sites.Add(site);
        }

        private Transform Place(Transform root, string name, Mesh mesh, Material[] mats, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name); go.transform.SetParent(root, true);
            pos.y = Ground(pos) - 0.15f;
            go.transform.SetPositionAndRotation(pos, rot);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mesh.subMeshCount >= 2 ? mats : new[] { mats[0] };
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh;
            return go.transform;
        }

        private float Ground(Vector3 p) => assets.terrain.SampleHeight(p) + assets.terrain.transform.position.y;

        /// <summary>Flatten a disc of terrain to the height at its centre (runtime, once per village).</summary>
        private void Flatten(Vector3 centre, float radius)
        {
            TerrainData data = assets.terrain.terrainData;
            int res = data.heightmapResolution;
            float metresPerSample = data.size.x / (res - 1);
            int cx = Mathf.RoundToInt((centre.x - assets.terrain.transform.position.x) / metresPerSample);
            int cz = Mathf.RoundToInt((centre.z - assets.terrain.transform.position.z) / metresPerSample);
            int r = Mathf.CeilToInt(radius * 1.5f / metresPerSample);
            int x0 = Mathf.Clamp(cx - r, 0, res - 1), z0 = Mathf.Clamp(cz - r, 0, res - 1);
            int w = Mathf.Clamp(cx + r, 0, res - 1) - x0 + 1, h = Mathf.Clamp(cz + r, 0, res - 1) - z0 + 1;
            if (w <= 1 || h <= 1) return;
            float[,] heights = data.GetHeights(x0, z0, w, h);
            float target = heights[Mathf.Clamp(cz - z0, 0, h - 1), Mathf.Clamp(cx - x0, 0, w - 1)];
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float dm = Mathf.Sqrt((x + x0 - cx) * (x + x0 - cx) + (z + z0 - cz) * (z + z0 - cz)) * metresPerSample;
                float k = 1f - Mathf.SmoothStep(radius, radius * 1.5f, dm);
                heights[z, x] = Mathf.Lerp(heights[z, x], target, k);
            }
            data.SetHeights(x0, z0, heights);
        }
    }
}

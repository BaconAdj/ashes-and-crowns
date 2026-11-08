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
        private readonly List<SettlementSite> _sites = new List<SettlementSite>(24);
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
                if (s.IsFixed) { EnsureFixedSite(s, terrainSize); continue; }
                BuildVillage(s, terrainSize, rng.Fork(MapGeometry.StableHash(s.Id)));
            }
        }

        /// <summary>A settlement that appears after the scene started (an adulterine castle).</summary>
        public void AddSite(Settlement s, float size) { if (_built) EnsureFixedSite(s, size); }

        private void EnsureFixedSite(Settlement s, float size)
        {
            foreach (var site in _sites) if (site.settlementId == s.Id) return;
            var go = new GameObject("Site_" + s.Id);
            go.transform.SetParent(transform, true);
            var pos = new Vector3(s.Nx * size, 0f, s.Nz * size); pos.y = Ground(pos);
            go.transform.position = pos;
            var site2 = go.AddComponent<SettlementSite>(); site2.settlementId = s.Id; site2.centre = go.transform; site2.radius = 80f;
            // The alehouse of a town is its gate for now: trading and jobs happen at the centre marker.
            site2.alehouse = go.transform;
            _sites.Add(site2);
        }

        private void BuildVillage(Settlement s, float size, DeterministicRng rng)
        {
            var root = new GameObject("Village_" + s.Name).transform;
            root.SetParent(transform, true);
            var centre = new Vector3(s.Nx * size, 0f, s.Nz * size);
            Flatten(centre, 55f);
            centre.y = Ground(centre);
            root.position = centre;
            float green = 16f;
            Transform church = Place(root, "Church", assets.church, new[] { assets.stone, assets.slate }, centre + new Vector3(0f, 0f, green + 12f), Quaternion.Euler(0f, 90f, 0f));
            Transform ale = Place(root, "Alehouse", assets.alehouse, new[] { assets.wattle, assets.thatch }, centre + new Vector3(green + 8f, 0f, -3f), Quaternion.Euler(0f, 90f, 0f));
            int huts = Mathf.Clamp(s.Population / 25, 3, 8);
            for (int i = 0; i < huts; i++)
            {
                float a = Mathf.Lerp(190f, 350f, i / (float)Mathf.Max(1, huts - 1)) * Mathf.Deg2Rad + rng.Range(-0.15f, 0.15f);
                float r = green + 5f + rng.Range(0f, 9f);
                Vector3 pos = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vector3 toGreen = centre - pos; toGreen.y = 0f;
                Place(root, "Hut_" + i, assets.hut, new[] { assets.wattle, assets.thatch }, pos, Quaternion.LookRotation(toGreen.normalized) * Quaternion.Euler(0f, rng.Range(-10f, 10f), 0f));
            }
            var well = GameObject.CreatePrimitive(PrimitiveType.Cylinder); well.name = "Well"; well.transform.SetParent(root, true);
            well.transform.localScale = new Vector3(1.6f, 0.5f, 1.6f); well.transform.position = centre + Vector3.up * 0.5f; well.GetComponent<Renderer>().sharedMaterial = assets.stone;
            var site = root.gameObject.AddComponent<SettlementSite>();
            site.settlementId = s.Id; site.centre = root; site.church = church; site.alehouse = ale; site.radius = 60f;
            _sites.Add(site);
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

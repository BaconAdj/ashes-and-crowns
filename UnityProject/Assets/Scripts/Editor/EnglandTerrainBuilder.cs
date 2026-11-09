using AshesAndCrowns.Game;
using AshesAndCrowns.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>
    /// Rasterises the real coastline (england_coast.json) plus stylised uplands, lowlands and rivers into a
    /// Unity terrain heightmap at editor time. Deterministic; no binary heightmap is committed.
    /// </summary>
    public static class EnglandTerrainBuilder
    {
        private const int FieldRes = 257; // low-res distance/upland fields, bilinearly upsampled

        public sealed class Result { public Terrain Terrain; public GameObject Sea; public float SeaLevel; }

        public static Result Build(WorldData world, CoastData coast, Balance balance, Material seaMaterial, Vector2[] flattenSpots, float flattenRadius)
        {
            Overworld ow = world.overworld;
            int res = ow.heightmapResolution;
            float size = ow.terrainSizeMetres, maxH = ow.maxTerrainHeightMetres;
            float kmPerUnit = coast.mapSideKm; // normalized 0..1 spans mapSideKm

            // ---- low-res fields
            var dist = new float[FieldRes * FieldRes];     // signed distance to coast in km (+ inland)
            var upland = new float[FieldRes * FieldRes];   // world-space metres of relief
            var lowland = new float[FieldRes * FieldRes];  // 0..1
            var river = new float[FieldRes * FieldRes];    // 0..1 carve
            for (int j = 0; j < FieldRes; j++)
            for (int i = 0; i < FieldRes; i++)
            {
                float nx = i / (float)(FieldRes - 1), nz = j / (float)(FieldRes - 1);
                bool inside = PointInRing(coast.mainland, nx, nz);
                float d = DistanceToRing(coast.mainland, nx, nz);
                if (!inside && coast.islands != null)
                    foreach (var isl in coast.islands)
                        if (PointInRing(isl.points, nx, nz)) { inside = true; d = DistanceToRing(isl.points, nx, nz); break; }
                        else d = Mathf.Min(d, DistanceToRing(isl.points, nx, nz));
                dist[j * FieldRes + i] = (inside ? 1f : -1f) * d * kmPerUnit;

                float up = 0f;
                foreach (var u in coast.uplands)
                {
                    float dx = (nx - u.nx) * kmPerUnit, dz = (nz - u.nz) * kmPerUnit;
                    float sigma = u.radiusKm / 1.6f;
                    up += u.heightM * Mathf.Exp(-(dx * dx + dz * dz) / (2f * sigma * sigma));
                }
                upland[j * FieldRes + i] = up * ow.realHeightToWorldHeight;

                float low = 0f;
                foreach (var l in coast.lowlands)
                {
                    float dx = (nx - l.nx) * kmPerUnit, dz = (nz - l.nz) * kmPerUnit;
                    float sigma = l.radiusKm / 1.4f;
                    low = Mathf.Max(low, Mathf.Exp(-(dx * dx + dz * dz) / (2f * sigma * sigma)));
                }
                lowland[j * FieldRes + i] = low;

                float carve = 0f;
                foreach (var r in coast.rivers)
                {
                    float dkm = DistanceToPolyline(r.points, nx, nz) * kmPerUnit;
                    carve = Mathf.Max(carve, 1f - Mathf.SmoothStep(0f, 1f, dkm / 3f));
                }
                river[j * FieldRes + i] = carve;
            }

            // ---- high-res heightmap
            float seaLevel = ow.seaLevelMetres;
            var heights = new float[res, res];
            var rng = new DeterministicRng((ulong)world.phase0Battlefield.noiseSeed);
            float ox = rng.Range(0f, 500f), oz = rng.Range(0f, 500f);
            float metresPerSample = size / (res - 1);
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float nx = x / (float)(res - 1), nz = z / (float)(res - 1);
                float d = Sample(dist, nx, nz), up = Sample(upland, nx, nz), low = Sample(lowland, nx, nz), carve = Sample(river, nx, nz);
                float wx = x * metresPerSample, wz = z * metresPerSample;
                float n = (Mathf.PerlinNoise(ox + wx * 0.0012f, oz + wz * 0.0012f) - 0.5f) * 2f
                        + (Mathf.PerlinNoise(ox + wx * 0.004f, oz + wz * 0.004f) - 0.5f) * 0.9f
                        + (Mathf.PerlinNoise(ox + wx * 0.02f, oz + wz * 0.02f) - 0.5f) * 0.25f;
                float h;
                if (d <= 0f)
                {
                    float t = Mathf.Clamp01(-d / ow.coastFalloffKm);
                    h = Mathf.Lerp(seaLevel - 1.5f, 0f, t) + n * 0.5f * (1f - t);
                }
                else
                {
                    float rise = Mathf.SmoothStep(0f, 1f, d / ow.coastFalloffKm);
                    float basin = Mathf.Lerp(seaLevel + 2f, ow.landBaseHeightM, rise);
                    basin = Mathf.Lerp(basin, seaLevel + 3f, low * 0.8f);
                    float relief = up * (1f - low * 0.9f);
                    float noise = n * (4f + relief * 0.12f) * (1f - low * 0.8f);
                    h = basin + relief + noise;
                    h -= carve * Mathf.Min(10f, Mathf.Max(0f, h - seaLevel - 2f)) * 0.7f;
                    if (h < seaLevel + 0.6f) h = seaLevel + 0.6f;
                }
                heights[z, x] = Mathf.Clamp01(h / maxH);
            }

            // ---- flatten spots (village sites)
            if (flattenSpots != null)
                foreach (var spot in flattenSpots)
                {
                    int cx = Mathf.RoundToInt(spot.x / size * (res - 1)), cz = Mathf.RoundToInt(spot.y / size * (res - 1));
                    int r = Mathf.CeilToInt(flattenRadius * 1.6f / metresPerSample);
                    float target = heights[Mathf.Clamp(cz, 0, res - 1), Mathf.Clamp(cx, 0, res - 1)];
                    for (int z = cz - r; z <= cz + r; z++)
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (x < 0 || z < 0 || x >= res || z >= res) continue;
                        float dm = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) * metresPerSample;
                        float w = 1f - Mathf.SmoothStep(flattenRadius, flattenRadius * 1.6f, dm);
                        heights[z, x] = Mathf.Lerp(heights[z, x], target, w);
                    }
                }

            var data = new TerrainData { heightmapResolution = res, size = new Vector3(size, maxH, size) };
            data.SetHeights(0, 0, heights);
            AssetDatabase.DeleteAsset(Paths.Generated + "/EnglandTerrain.asset");
            AssetDatabase.CreateAsset(data, Paths.Generated + "/EnglandTerrain.asset");   // must exist as an asset before layers are assigned, or the alpha maps are dropped

            // ---- layers and splat
            Texture2D grassTex = SceneKit.NoiseTexture(Paths.Generated + "/GrassAlbedo.png", new Color(0.24f, 0.27f, 0.16f), new Color(0.36f, 0.37f, 0.22f), 6f, 23f, 256, 1.3f);
            Texture2D mudTex = SceneKit.NoiseTexture(Paths.Generated + "/MudAlbedo.png", new Color(0.21f, 0.19f, 0.15f), new Color(0.33f, 0.31f, 0.24f), 5f, 19f, 256, 4.2f);
            Texture2D rockTex = SceneKit.NoiseTexture(Paths.Generated + "/RockAlbedo.png", new Color(0.30f, 0.30f, 0.29f), new Color(0.46f, 0.45f, 0.42f), 4f, 17f, 256, 8.8f);
            var layers = new[]
            {
                SceneKit.Layer(Paths.Generated + "/Grass.terrainlayer", grassTex, 9f, SceneKit.NormalTexture(Paths.Generated + "/GrassNormal.png", 6f, 23f, 1.3f, 1.6f)),
                SceneKit.Layer(Paths.Generated + "/Mud.terrainlayer", mudTex, 6f, SceneKit.NormalTexture(Paths.Generated + "/MudNormal.png", 5f, 19f, 4.2f, 2.2f)),
                SceneKit.Layer(Paths.Generated + "/Rock.terrainlayer", rockTex, 12f, SceneKit.NormalTexture(Paths.Generated + "/RockNormal.png", 4f, 17f, 8.8f, 3.0f)),
            };
            data.terrainLayers = layers;
            int ares = 1025;
            data.alphamapResolution = ares;
            var alphas = new float[ares, ares, 3];
            for (int z = 0; z < ares; z++)
            for (int x = 0; x < ares; x++)
            {
                float nx = x / (float)(ares - 1), nz = z / (float)(ares - 1);
                float h = data.GetInterpolatedHeight(nx, nz);
                float slope = data.GetSteepness(nx, nz); // degrees
                float mud = Mathf.Clamp01(1f - (h - seaLevel) / 4f) + Sample(river, nx, nz) * 0.6f;
                float rock = Mathf.Clamp01((slope - 22f) / 18f) + Mathf.Clamp01((h - 90f) / 40f);
                mud = Mathf.Clamp01(mud); rock = Mathf.Clamp01(rock);
                float grass = Mathf.Clamp01(1f - mud - rock);
                float sum = grass + mud + rock;
                alphas[z, x, 0] = grass / sum; alphas[z, x, 1] = mud / sum; alphas[z, x, 2] = rock / sum;
            }
            data.SetAlphamaps(0, 0, alphas);
            EditorUtility.SetDirty(data);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "England";
            var terrain = terrainGo.GetComponent<Terrain>();
            Shader terrainShader = Shader.Find(SceneKit.TerrainShader);
            if (terrainShader != null)
            {
                string matPath = Paths.Palette + "/TerrainEngland.mat";
                var tm = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (tm == null) { tm = new Material(terrainShader) { name = "TerrainEngland" }; AssetDatabase.CreateAsset(tm, matPath); }
                terrain.materialTemplate = tm;
            }
            terrain.heightmapPixelError = 8f; terrain.basemapDistance = 1500f; terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            terrain.drawInstanced = true;

            // ---- sea
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea";
            Object.DestroyImmediate(sea.GetComponent<Collider>());
            sea.transform.position = new Vector3(size / 2f, seaLevel, size / 2f);
            sea.transform.localScale = new Vector3(size / 10f * 1.5f, 1f, size / 10f * 1.5f);
            sea.GetComponent<Renderer>().sharedMaterial = seaMaterial;

            return new Result { Terrain = terrain, Sea = sea, SeaLevel = seaLevel };
        }

        private static float Sample(float[] field, float nx, float nz)
        {
            float u = Mathf.Clamp01(nx) * (FieldRes - 1), v = Mathf.Clamp01(nz) * (FieldRes - 1);
            int x0 = (int)u, z0 = (int)v, x1 = Mathf.Min(x0 + 1, FieldRes - 1), z1 = Mathf.Min(z0 + 1, FieldRes - 1);
            float fx = u - x0, fz = v - z0;
            return Mathf.Lerp(Mathf.Lerp(field[z0 * FieldRes + x0], field[z0 * FieldRes + x1], fx), Mathf.Lerp(field[z1 * FieldRes + x0], field[z1 * FieldRes + x1], fx), fz);
        }

        private static bool PointInRing(MapPoint[] ring, float x, float z)
        {
            bool inside = false;
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            {
                MapPoint a = ring[i], b = ring[j];
                if ((a.z > z) != (b.z > z) && x < (b.x - a.x) * (z - a.z) / (b.z - a.z + 1e-12f) + a.x) inside = !inside;
            }
            return inside;
        }

        private static float DistanceToRing(MapPoint[] ring, float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++) best = Mathf.Min(best, SegmentDistance(ring[j], ring[i], x, z));
            return best;
        }

        private static float DistanceToPolyline(MapPoint[] pts, float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 1; i < pts.Length; i++) best = Mathf.Min(best, SegmentDistance(pts[i - 1], pts[i], x, z));
            return best;
        }

        private static float SegmentDistance(MapPoint a, MapPoint b, float x, float z)
        {
            float dx = b.x - a.x, dz = b.z - a.z;
            float len2 = dx * dx + dz * dz;
            float t = len2 > 1e-12f ? Mathf.Clamp01(((x - a.x) * dx + (z - a.z) * dz) / len2) : 0f;
            float px = a.x + t * dx - x, pz = a.z + t * dz - z;
            return Mathf.Sqrt(px * px + pz * pz);
        }
    }
}

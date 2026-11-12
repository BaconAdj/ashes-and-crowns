using AshesAndCrowns.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>
    /// The ground a town stands on, at one metre to the metre: the river terrace, the Thames, the rampart bank and its wet
    /// ditch broken by causeways at the gates, and the motte. Scene coordinates are the plan's (x east, z north, the
    /// crossroads at the origin); the terrain object is shifted so that holds.
    /// </summary>
    public static class TownTerrainBuilder
    {
        public const float Size = 1600f, BaseHeight = 20f, MaxHeight = 64f;
        public const int Resolution = 1025, AlphaResolution = 1025;

        public sealed class Result { public Terrain Terrain; public Transform Water; }

        private static float S(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        /// <summary>How far outside the rampart line a point is (negative inside), which side it is nearest, and how much a gate
        /// causeway cancels the earthwork there. The east side is the river and has no bank.</summary>
        private static float Outside(TownPlanData p, float x, float z, out float gate)
        {
            float dW = -p.HalfWidth - x, dN = z - p.HalfDepth, dS = -p.HalfDepth - z;
            float d = Mathf.Max(dW, Mathf.Max(dN, dS));
            gate = 0f;
            if (d == dW) gate = 1f - S(5f, 9f, Mathf.Abs(z));
            else gate = 1f - S(5f, 9f, Mathf.Abs(x));
            if (x > p.RiverX0 - 4f) d = -1000f;   // the bank stops at the water
            return d;
        }

        public static float HeightAt(TownPlanData p, float x, float z)
        {
            float d = Outside(p, x, z, out float gate);
            float rough = d > p.BankWidth + p.DitchWidth + 20f ? 1.1f : 0.22f;
            float h = BaseHeight + (Mathf.PerlinNoise(x * 0.013f + 31.7f, z * 0.013f + 7.3f) - 0.5f) * 2f * rough + (Mathf.PerlinNoise(x * 0.09f + 3.1f, z * 0.09f + 11.9f) - 0.5f) * 0.12f;
            float earth = 1f - gate;
            if (d > 0f && d < p.BankWidth) h += p.BankHeight * Mathf.Sin(Mathf.PI * d / p.BankWidth) * earth;
            else if (d >= p.BankWidth && d < p.BankWidth + p.DitchWidth) h -= p.DitchDepth * Mathf.Sin(Mathf.PI * (d - p.BankWidth) / p.DitchWidth) * earth;
            // the Thames
            h -= 3.4f * S(p.RiverX0 - 8f, p.RiverX0 + 5f, x) * (1f - S(p.RiverX1 - 5f, p.RiverX1 + 8f, x));
            // the motte
            float r = Mathf.Sqrt((x - p.MotteX) * (x - p.MotteX) + (z - p.MotteZ) * (z - p.MotteZ));
            if (r < p.MotteRadius) h += p.MotteHeight * S(p.MotteRadius, p.MotteTopRadius, r);
            return h;
        }

        public static Result Build(TownPlanData p, Material waterMaterial)
        {
            int res = Resolution; float step = Size / (res - 1), half = Size * 0.5f;
            var heights = new float[res, res];
            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
                heights[iz, ix] = Mathf.Clamp01(HeightAt(p, ix * step - half, iz * step - half) / MaxHeight);
            var data = new TerrainData { heightmapResolution = res, size = new Vector3(Size, MaxHeight, Size) };
            data.SetHeights(0, 0, heights);
            string assetPath = Paths.Generated + "/TownTerrain_" + p.Id + ".asset";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(data, assetPath);   // before the layers, or the alpha maps are dropped

            Texture2D grassTex = SceneKit.NoiseTexture(Paths.Generated + "/GrassAlbedo.png", new Color(0.24f, 0.27f, 0.16f), new Color(0.36f, 0.37f, 0.22f), 6f, 23f, 256, 1.3f);
            Texture2D mudTex = SceneKit.NoiseTexture(Paths.Generated + "/MudAlbedo.png", new Color(0.21f, 0.19f, 0.15f), new Color(0.33f, 0.31f, 0.24f), 5f, 19f, 256, 4.2f);
            Texture2D rockTex = SceneKit.NoiseTexture(Paths.Generated + "/RockAlbedo.png", new Color(0.30f, 0.30f, 0.29f), new Color(0.46f, 0.45f, 0.42f), 4f, 17f, 256, 8.8f);
            data.terrainLayers = new[]
            {
                SceneKit.Layer(Paths.Generated + "/Grass.terrainlayer", grassTex, 9f, SceneKit.NormalTexture(Paths.Generated + "/GrassNormal.png", 6f, 23f, 1.3f, 1.6f)),
                SceneKit.Layer(Paths.Generated + "/Mud.terrainlayer", mudTex, 6f, SceneKit.NormalTexture(Paths.Generated + "/MudNormal.png", 5f, 19f, 4.2f, 2.2f)),
                SceneKit.Layer(Paths.Generated + "/Rock.terrainlayer", rockTex, 12f, SceneKit.NormalTexture(Paths.Generated + "/RockNormal.png", 4f, 17f, 8.8f, 3.0f)),
            };
            int ares = AlphaResolution; data.alphamapResolution = ares;
            var alphas = new float[ares, ares, 3];
            float astep = Size / (ares - 1);
            for (int iz = 0; iz < ares; iz++)
            for (int ix = 0; ix < ares; ix++)
            {
                float x = ix * astep - half, z = iz * astep - half;
                float mud = 0f;
                // trodden earth: the streets, the market, the castle yard, the roads out of the gates, the river's edge and the ditch
                foreach (TownStreet s in p.Streets)
                {
                    float ds = TownPlan.DistToSegment(x, z, s.X0, s.Z0, s.X1, s.Z1);
                    if (ds < s.Width * 0.5f + 5f) mud = Mathf.Max(mud, 1f - S(s.Width * 0.5f + 0.5f, s.Width * 0.5f + 5f, ds) * 0.75f);
                }
                if (p.Market.Contains(x, z, 2f)) mud = 1f;
                if (p.Bailey.Contains(x, z)) mud = Mathf.Max(mud, 0.55f);
                if (Mathf.Abs(x) < 4f && Mathf.Abs(z) > p.HalfDepth && Mathf.Abs(z) < p.HalfDepth + 220f) mud = Mathf.Max(mud, 0.9f);
                if (Mathf.Abs(z) < 4f && x < -p.HalfWidth && x > -p.HalfWidth - 220f) mud = Mathf.Max(mud, 0.9f);
                float d = Outside(p, x, z, out _);
                if (d > p.BankWidth - 2f && d < p.BankWidth + p.DitchWidth + 1f) mud = Mathf.Max(mud, 0.8f);
                if (x > p.RiverX0 - 10f && x < p.RiverX1 + 10f) mud = 1f;
                mud = Mathf.Clamp01(mud + (Mathf.PerlinNoise(x * 0.21f, z * 0.21f) - 0.5f) * 0.25f * (mud > 0.05f ? 1f : 0f));
                float rm = Mathf.Sqrt((x - p.MotteX) * (x - p.MotteX) + (z - p.MotteZ) * (z - p.MotteZ));
                float rock = rm < p.MotteTopRadius + 1f ? 0.7f : 0f;
                float grass = Mathf.Clamp01(1f - mud - rock);
                float sum = grass + mud + rock;
                alphas[iz, ix, 0] = grass / sum; alphas[iz, ix, 1] = mud / sum; alphas[iz, ix, 2] = rock / sum;
            }
            data.SetAlphamaps(0, 0, alphas);
            EditorUtility.SetDirty(data);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "TownGround"; terrainGo.transform.position = new Vector3(-half, 0f, -half);
            var terrain = terrainGo.GetComponent<Terrain>();
            Shader terrainShader = Shader.Find(SceneKit.TerrainShader);
            if (terrainShader != null)
            {
                string matPath = Paths.Palette + "/TerrainEngland.mat";
                var tm = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (tm == null) { tm = new Material(terrainShader) { name = "TerrainEngland" }; AssetDatabase.CreateAsset(tm, matPath); }
                terrain.materialTemplate = tm;
            }
            terrain.heightmapPixelError = 4f; terrain.basemapDistance = 600f; terrain.shadowCastingMode = ShadowCastingMode.TwoSided; terrain.drawInstanced = true;

            // ---- water: the river, and the ditch fed from it
            var water = new GameObject("Water").transform;
            Plane(water, "Thames", new Vector3((p.RiverX0 + p.RiverX1) * 0.5f, BaseHeight - 0.9f, 0f), p.RiverX1 - p.RiverX0 + 10f, Size, waterMaterial);
            float off = p.BankWidth + p.DitchWidth * 0.5f, ditchY = BaseHeight - 1.5f;
            Plane(water, "DitchWest", new Vector3(-p.HalfWidth - off, ditchY, 0f), p.DitchWidth, 2f * (p.HalfDepth + off) + p.DitchWidth, waterMaterial);
            float nsLen = p.RiverX0 + p.HalfWidth + p.BankWidth + p.DitchWidth, nsX = (p.RiverX0 - p.HalfWidth - p.BankWidth - p.DitchWidth) * 0.5f;
            Plane(water, "DitchNorth", new Vector3(nsX, ditchY, p.HalfDepth + off), nsLen, p.DitchWidth, waterMaterial);
            Plane(water, "DitchSouth", new Vector3(nsX, ditchY, -p.HalfDepth - off), nsLen, p.DitchWidth, waterMaterial);
            return new Result { Terrain = terrain, Water = water };
        }

        private static void Plane(Transform parent, string name, Vector3 pos, float sizeX, float sizeZ, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane); go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.position = pos; go.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}

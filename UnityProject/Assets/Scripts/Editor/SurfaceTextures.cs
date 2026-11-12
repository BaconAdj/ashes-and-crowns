using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>
    /// Building surfaces drawn as what they are, not as coloured noise: limewashed daub with cracks and straw, thatch in
    /// courses with the strands running down the slope, coursed rubble with mortar, slates, oak boards. Each is a tileable
    /// height field; the colour and the normal map both come from it so relief and paint agree. The models carry texture
    /// coordinates in metres with V running up the surface, so courses lie level and strands hang true on every building.
    /// </summary>
    public static class SurfaceTextures
    {
        public enum Kind { Daub, Thatch, Rubble, Slate, Plank }
        private const int Size = 512;

        public static Material Make(string name, Kind kind, float metresPerTile, float roughness)
        {
            string matPath = Paths.Palette + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Paths.Generated + "/" + name + "_Albedo.png"), normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Paths.Generated + "/" + name + "_Normal.png");
            if (albedo == null || normal == null) Bake(name, kind, out albedo, out normal);
            if (m == null) { m = new Material(Shader.Find(SceneKit.LitShader)) { name = name }; AssetDatabase.CreateAsset(m, matPath); }
            m.SetColor("_BaseColor", Color.white); m.SetTexture("_BaseMap", albedo); m.SetFloat("_Smoothness", 1f - roughness); m.SetFloat("_Metallic", 0f);
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTextureScale("_BaseMap", Vector2.one / metresPerTile);
            m.enableInstancing = true;   // four hundred houses of one shape draw as a handful of batches
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Bake(string name, Kind kind, out Texture2D albedo, out Texture2D normal)
        {
            var h = new float[Size * Size]; var c = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                Sample(kind, u, v, out float height, out Color colour);
                h[y * Size + x] = height; c[y * Size + x] = colour;
            }
            float strength = kind == Kind.Daub ? 3f : kind == Kind.Thatch || kind == Kind.Rubble ? 5f : 4f;
            var n = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float l = h[y * Size + (x + Size - 1) % Size], r = h[y * Size + (x + 1) % Size];
                float d = h[((y + Size - 1) % Size) * Size + x], up = h[((y + 1) % Size) * Size + x];
                var nrm = new Vector3((l - r) * strength, (d - up) * strength, 1f).normalized;
                n[y * Size + x] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
            }
            albedo = Write(Paths.Generated + "/" + name + "_Albedo.png", c, false);
            normal = Write(Paths.Generated + "/" + name + "_Normal.png", n, true);
        }

        private static Texture2D Write(string path, Color[] px, bool isNormal)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, isNormal);
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default; importer.sRGBTexture = !isNormal;
            importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true; importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ tileable noise

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint hh = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 1442695041u;
                hh = (hh ^ (hh >> 13)) * 1274126177u;
                return ((hh ^ (hh >> 16)) & 0xFFFFu) / 65535f;
            }
        }

        private static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

        /// <summary>Value noise that repeats exactly every (px, py) cells, so the texture tiles without a seam.</summary>
        private static float Noise(float u, float v, int px, int py, int seed)
        {
            float x = u * px, y = v * py; int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(Mod(x0, px), Mod(y0, py), seed), b = Hash(Mod(x0 + 1, px), Mod(y0, py), seed), c = Hash(Mod(x0, px), Mod(y0 + 1, py), seed), d = Hash(Mod(x0 + 1, px), Mod(y0 + 1, py), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float u, float v, int px, int py, int seed, int octaves)
        {
            float s = 0f, a = 0.5f, t = 0f;
            for (int o = 0; o < octaves; o++) { s += a * Noise(u, v, px << o, py << o, seed + o * 17); t += a; a *= 0.5f; }
            return s / t;
        }

        /// <summary>Courses of blocks: which block a point is in, and where inside it. Alternate courses break joint.</summary>
        private static void Cells(float u, float v, int rows, int cols, int seed, float jitter, out float fx, out float fy, out float id)
        {
            int row = Mathf.FloorToInt(v * rows);
            float off = Hash(row, 7, seed) * jitter + (row % 2) * 0.5f;
            float x = u * cols + off; int cx = Mathf.FloorToInt(x);
            fx = x - cx; fy = v * rows - row; id = Hash(Mod(cx, cols), row, seed + 3);
        }

        private static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));

        private static void Sample(Kind kind, float u, float v, out float h, out Color c)
        {
            switch (kind)
            {
                case Kind.Daub:
                    {
                        float blot = Fbm(u, v, 3, 3, 1, 4), fine = Fbm(u, v, 24, 24, 5, 3);
                        float crack = Mathf.Clamp01(1f - Mathf.Abs(Fbm(u, v, 5, 5, 9, 3) - 0.5f) / 0.007f) * Mathf.Clamp01((Fbm(u, v, 2, 2, 13, 2) - 0.5f) * 8f);
                        float straw = Mathf.Clamp01((Noise(u, v, 90, 14, 21) - 0.80f) / 0.2f);
                        h = 0.55f + 0.25f * blot + 0.15f * fine - 0.5f * crack + 0.1f * straw;
                        c = Mix(new Color(0.50f, 0.44f, 0.34f), new Color(0.66f, 0.61f, 0.50f), blot * 0.7f + fine * 0.3f) * (1f - 0.55f * crack);
                        c = Mix(c, new Color(0.60f, 0.52f, 0.33f), straw * 0.5f);
                        break;
                    }
                case Kind.Thatch:
                    {
                        float strand = Noise(u, v, 110, 7, 3) * 0.6f + Noise(u, v, 220, 13, 4) * 0.4f;
                        float f = Mathf.Repeat(v * 4f + 0.15f * Noise(u, v, 18, 1, 8), 1f);
                        float step = 1f - f, shadow = Mathf.Clamp01(1f - f / 0.08f), age = Fbm(u, v, 4, 4, 11, 4);
                        h = 0.25f + 0.45f * step + 0.3f * strand;
                        c = Mix(new Color(0.30f, 0.26f, 0.17f), new Color(0.55f, 0.47f, 0.29f), strand * 0.6f + step * 0.25f + age * 0.15f) * (1f - 0.45f * shadow);
                        c = Mix(c, new Color(0.30f, 0.33f, 0.20f), Mathf.Clamp01(age - 0.62f) * 1.6f);   // moss where it holds the wet
                        break;
                    }
                case Kind.Rubble:
                    {
                        Cells(u, v, 7, 4, 31, 0.5f, out float fx, out float fy, out float id);
                        float edge = Mathf.Min(Mathf.Min(fx, 1f - fx) * 1.6f, Mathf.Min(fy, 1f - fy)) + (Noise(u, v, 40, 40, 77) - 0.5f) * 0.05f;
                        float mortar = Mathf.Clamp01(1f - edge / 0.09f), fine = Fbm(u, v, 32, 32, 2, 3);
                        h = Mathf.Lerp(0.45f + 0.35f * id + 0.18f * fine, 0.2f, mortar);
                        Color stone = Mix(new Color(0.40f, 0.39f, 0.36f), new Color(0.60f, 0.57f, 0.50f), id * 0.7f + fine * 0.3f);
                        stone = Mix(stone, new Color(0.52f, 0.47f, 0.36f), Mathf.Clamp01(id - 0.7f) * 2f);
                        c = Mix(stone, new Color(0.44f, 0.42f, 0.36f), mortar);
                        break;
                    }
                case Kind.Slate:
                    {
                        Cells(u, v, 5, 4, 41, 0.1f, out float fx, out float fy, out float id);
                        float gap = Mathf.Clamp01(1f - Mathf.Min(fx, 1f - fx) / 0.03f), lip = Mathf.Clamp01(1f - fy / 0.07f), fine = Fbm(u, v, 40, 40, 6, 2);
                        h = (0.3f + 0.5f * (1f - fy) + 0.1f * fine) * (1f - 0.6f * gap);
                        c = Mix(new Color(0.17f, 0.18f, 0.21f), new Color(0.33f, 0.34f, 0.37f), id * 0.6f + fine * 0.4f) * (1f - 0.5f * gap) * (1f - 0.4f * lip);
                        break;
                    }
                default:
                    {
                        float x = u * 6f; int cx = Mathf.FloorToInt(x); float fx = x - cx, id = Hash(cx, 3, 51);
                        float gap = Mathf.Clamp01(1f - Mathf.Min(fx, 1f - fx) / 0.035f);
                        float grain = Noise(u + id * 0.37f, v, 96, 3, 7) * 0.6f + Noise(u, v + id, 200, 5, 8) * 0.4f;
                        h = 0.6f + 0.25f * grain - 0.5f * gap;
                        c = Mix(new Color(0.16f, 0.12f, 0.09f), new Color(0.34f, 0.26f, 0.18f), grain * 0.6f + id * 0.4f) * (1f - 0.6f * gap);
                        break;
                    }
            }
            c.a = 1f;
        }
    }
}

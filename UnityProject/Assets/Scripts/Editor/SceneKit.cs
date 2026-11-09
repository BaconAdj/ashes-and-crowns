using System.IO;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Game;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>Helpers shared by the scene builders: palette materials, textures, cameras, volume, player, horse, HUD.</summary>
    public static class SceneKit
    {
        public const string LitShader = "Universal Render Pipeline/Lit";
        public const string UnlitShader = "Universal Render Pipeline/Unlit";
        public const string TerrainShader = "Universal Render Pipeline/Terrain/Lit";
        public const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";

        public static Color Col(float[] c) => c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2]) : Color.grey;

        public static Material Mat(string name, string shaderName, Color color, float metallic, float roughness, bool instancing = false)
        {
            string path = Paths.Palette + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError("[Ashes and Crowns] shader not found: " + shaderName); shader = Shader.Find("Standard"); }
            var m = new Material(shader) { name = name, enableInstancing = instancing };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); else m.color = color;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f - roughness);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>A material with a tiling texture (for buildings and ground). Cached by name.</summary>
        public static Material TexturedMat(string name, Texture2D tex, Color tint, float roughness)
        {
            string path = Paths.Palette + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var m = new Material(Shader.Find(LitShader)) { name = name };
            m.SetColor("_BaseColor", tint); m.SetTexture("_BaseMap", tex); m.SetFloat("_Smoothness", 1f - roughness); m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Transparent soft particle material for rain or snow.</summary>
        public static Material ParticleMat(string name, Color color)
        {
            string path = Paths.Palette + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(ParticleShader) ?? Shader.Find(UnlitShader);
            var m = new Material(shader) { name = name };
            Texture2D dot = SoftDotTexture(Paths.Generated + "/SoftDot.png");
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", dot);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Two-colour Perlin texture written as a PNG and imported (so the terrain and buildings get a real, compressed, repeating texture).</summary>
        public static Texture2D NoiseTexture(string assetPath, Color a, Color b, float scale1, float scale2, int size = 256, float seedOffset = 0f)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existing != null) return existing;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                int x = i % size, y = i / size;
                // Tileable by sampling on a torus of two perlin lookups blended at the seams.
                float u = x / (float)size, v = y / (float)size;
                float n1 = Mathf.PerlinNoise(seedOffset + u * scale1, seedOffset + v * scale1);
                float n2 = Mathf.PerlinNoise(seedOffset + 3.1f + u * scale2, seedOffset + 7.7f + v * scale2);
                float n = n1 * 0.65f + n2 * 0.35f;
                float ex = Mathf.Min(u, 1f - u) * 2f, ey = Mathf.Min(v, 1f - v) * 2f;
                float edge = Mathf.Min(ex, ey);
                n = Mathf.Lerp(0.5f, n, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, edge * 3f)));
                px[i] = Color.Lerp(a, b, n);
            }
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.wrapMode = TextureWrapMode.Repeat; importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        public static Texture2D SoftDotTexture(string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existing != null) return existing;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                float dx = (i % size + 0.5f) / size - 0.5f, dy = (i / size + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[i] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        public static TerrainLayer Layer(string assetPath, Texture2D tex, float tileMetres)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(assetPath);
            if (existing != null) return existing;
            var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tileMetres, tileMetres), smoothness = 0f, metallic = 0f, specular = Color.black };
            AssetDatabase.CreateAsset(layer, assetPath);
            return layer;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public const string ArtFolder = "Assets/Art";

        /// <summary>A Blender FBX under Assets/Art, or null when it has not been generated yet (callers fall back to primitives).</summary>
        public static Mesh ArtMesh(string relativePath)
        {
            string path = ArtFolder + "/" + relativePath;
            if (!File.Exists(path)) { path = Path.ChangeExtension(path, ".obj"); if (!File.Exists(path)) return null; }   // OBJ twins are committed; FBX comes from a local Blender run
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Mesh m) { mesh = m; break; }
            if (mesh != null) Debug.Log("[Ashes and Crowns] using Blender asset " + path);
            return mesh;
        }

        /// <summary>A static mesh child with two palette materials (slot 1 / slot 2) and an optional collider.</summary>
        public static GameObject MeshChild(string name, Transform parent, Mesh mesh, Material slot1, Material slot2, bool collider = false)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mesh.subMeshCount >= 2 ? new[] { slot1, slot2 } : new[] { slot1 };
            if (collider) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; }
            return go;
        }

        public static Mesh CapsuleMesh()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Mesh mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return mesh;
        }

        public static Light BuildSun(Balance balance)
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;
            sun.color = Col(balance.look.sunColor); sun.intensity = balance.look.sunIntensity;
            sunGo.transform.rotation = Quaternion.Euler(balance.look.sunPitchDegrees, balance.look.sunYawDegrees, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Col(balance.look.ambientColor);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = balance.look.fogDensity; RenderSettings.fogColor = Col(balance.look.fogColor);
            return sun;
        }

        public static void BuildVolume(Balance balance, string profilePath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                T Add<T>() where T : VolumeComponent { var c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); return c; }
                var tone = Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
                var ca = Add<ColorAdjustments>();
                ca.saturation.Override(balance.look.saturation); ca.contrast.Override(balance.look.contrast); ca.postExposure.Override(balance.look.postExposure);
                ca.colorFilter.Override(new Color(0.86f, 0.90f, 1.0f));
                var vig = Add<Vignette>(); vig.intensity.Override(balance.look.vignette); vig.smoothness.Override(0.5f);
                var grain = Add<FilmGrain>(); grain.type.Override(FilmGrainLookup.Medium2); grain.intensity.Override(balance.look.filmGrain); grain.response.Override(0.8f);
                var bloom = Add<Bloom>(); bloom.intensity.Override(0.15f); bloom.threshold.Override(1.2f);
                EditorUtility.SetDirty(profile);
            }
            var volGo = new GameObject("GlobalVolume_GrimLook");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true; vol.sharedProfile = profile;
        }

        public static Camera BuildGameplayCamera(Color background, out ThirdPersonCamera tpc)
        {
            var camGo = new GameObject("GameplayCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = background;
            cam.farClipPlane = 3000f; cam.nearClipPlane = 0.1f; cam.fieldOfView = 58f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            tpc = camGo.AddComponent<ThirdPersonCamera>();
            return cam;
        }

        public static GameObject BuildPlayer(Balance balance, Material leather, Material steel, out PlayerVitals vitals, out PlayerController pc, out MeleeController melee)
        {
            var playerGo = new GameObject("Player");
            var pcc = playerGo.AddComponent<CharacterController>();
            pcc.height = balance.player.height; pcc.radius = 0.35f; pcc.center = new Vector3(0f, balance.player.height * 0.5f, 0f); pcc.slopeLimit = 55f;
            Mesh man = ArtMesh("Characters/Man.fbx");
            if (man != null) MeshChild("Body", playerGo.transform, man, leather, Mat("Skin", LitShader, new Color(0.58f, 0.46f, 0.37f), 0f, 0.9f));
            else
            {
                Primitive(PrimitiveType.Capsule, "Body", playerGo.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.55f, 0.75f, 0.55f), leather);
                Primitive(PrimitiveType.Sphere, "Head", playerGo.transform, new Vector3(0f, 1.78f, 0f), new Vector3(0.3f, 0.32f, 0.3f), steel);
            }
            var weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(playerGo.transform, false); weaponPivot.localPosition = new Vector3(0.38f, 1.2f, 0.1f);
            Primitive(PrimitiveType.Cube, "Blade", weaponPivot, new Vector3(0f, 0f, 0.55f), new Vector3(0.05f, 0.12f, 1.0f), steel);
            vitals = playerGo.AddComponent<PlayerVitals>();
            pc = playerGo.AddComponent<PlayerController>();
            melee = playerGo.AddComponent<MeleeController>();
            melee.weaponVisual = weaponPivot; melee.facingSource = playerGo.transform; melee.vitals = vitals;
            pc.vitals = vitals;
            return playerGo;
        }

        public static GameObject BuildHorse(Balance balance, Material bay, Material leather, out HorseController horse)
        {
            var horseGo = new GameObject("Horse");
            var hcc = horseGo.AddComponent<CharacterController>();
            hcc.height = 1.7f; hcc.radius = 0.6f; hcc.center = new Vector3(0f, 0.95f, 0f); hcc.slopeLimit = 55f;
            Mesh horseMesh = ArtMesh("Characters/Horse.fbx");
            if (horseMesh != null) MeshChild("Body", horseGo.transform, horseMesh, bay, leather);
            else
            {
                Primitive(PrimitiveType.Cube, "Barrel", horseGo.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.65f, 0.75f, 1.9f), bay);
                Primitive(PrimitiveType.Cube, "Neck", horseGo.transform, new Vector3(0f, 1.6f, 1.05f), new Vector3(0.3f, 0.7f, 0.5f), bay);
                Primitive(PrimitiveType.Cube, "Head", horseGo.transform, new Vector3(0f, 1.85f, 1.45f), new Vector3(0.26f, 0.3f, 0.6f), bay);
                foreach (var (x, z) in new[] { (-0.22f, 0.7f), (0.22f, 0.7f), (-0.22f, -0.7f), (0.22f, -0.7f) })
                    Primitive(PrimitiveType.Cube, "Leg", horseGo.transform, new Vector3(x, 0.4f, z), new Vector3(0.14f, 0.8f, 0.14f), bay);
                Primitive(PrimitiveType.Cube, "Saddle", horseGo.transform, new Vector3(0f, 1.55f, -0.1f), new Vector3(0.5f, 0.12f, 0.6f), leather);
            }
            var seat = new GameObject("Seat").transform;
            seat.SetParent(horseGo.transform, false); seat.localPosition = new Vector3(0f, balance.horse.seatHeight - 0.15f, -0.1f);
            horse = horseGo.AddComponent<HorseController>();
            horse.seat = seat;
            return horseGo;
        }

        public static HudController BuildHud(PanelSettings panel)
        {
            var hudGo = new GameObject("HUD");
            var doc = hudGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            return hudGo.AddComponent<HudController>();
        }

        public static float GroundY(Terrain terrain, Vector3 at) => terrain.SampleHeight(at) + terrain.transform.position.y;
    }
}

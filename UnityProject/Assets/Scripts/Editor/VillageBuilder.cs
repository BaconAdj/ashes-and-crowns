using System.IO;
using AshesAndCrowns.Game;
using AshesAndCrowns.Sim;
using UnityEditor;
using UnityEngine;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>
    /// Lays out one village: church to the north of the green, alehouse to the east, huts around.
    /// Uses FBX from Assets/Art/Buildings when present (the Blender pipeline), otherwise the procedural meshes.
    /// </summary>
    public static class VillageBuilder
    {
        public const string BuildingsFolder = "Assets/Art/Buildings";

        public sealed class Result { public Transform Root; public Transform Church; public Transform Alehouse; public Transform Home; public Transform Workplace; public Transform Masters; public Vector3 Green; public Material Wattle, Thatch, Stone, Slate, DarkWood, Bark; public Mesh HutMesh, AlehouseMesh, ChurchMesh; }

        public static Result Build(Terrain terrain, Vector3 centre, Balance balance, DeterministicRng rng, string villageName)
        {
            VillageBalance v = balance.village;
            Texture2D wattleTex = SceneKit.NoiseTexture(Paths.Generated + "/WattleAlbedo.png", new Color(0.52f, 0.46f, 0.36f), new Color(0.64f, 0.58f, 0.47f), 8f, 31f, 256, 2.6f);
            Texture2D thatchTex = SceneKit.NoiseTexture(Paths.Generated + "/ThatchAlbedo.png", new Color(0.36f, 0.30f, 0.18f), new Color(0.50f, 0.42f, 0.26f), 3f, 40f, 256, 5.5f);
            Texture2D stoneTex = SceneKit.NoiseTexture(Paths.Generated + "/StoneAlbedo.png", new Color(0.42f, 0.41f, 0.38f), new Color(0.56f, 0.54f, 0.50f), 10f, 29f, 256, 9.1f);
            Texture2D slateTex = SceneKit.NoiseTexture(Paths.Generated + "/SlateAlbedo.png", new Color(0.20f, 0.21f, 0.24f), new Color(0.30f, 0.31f, 0.34f), 12f, 36f, 256, 12.4f);
            Material wattle = SceneKit.TexturedMat("Wattle", wattleTex, Color.white, 0.95f, SceneKit.NormalTexture(Paths.Generated + "/WattleNormal.png", 8f, 31f, 2.6f, 2.5f));
            Material thatch = SceneKit.TexturedMat("Thatch", thatchTex, Color.white, 0.98f, SceneKit.NormalTexture(Paths.Generated + "/ThatchNormal.png", 3f, 40f, 5.5f, 3.5f));
            Material stone = SceneKit.TexturedMat("ChurchStone", stoneTex, Color.white, 0.92f, SceneKit.NormalTexture(Paths.Generated + "/StoneNormal.png", 10f, 29f, 9.1f, 3.0f));
            Material slate = SceneKit.TexturedMat("Slate", slateTex, Color.white, 0.75f, SceneKit.NormalTexture(Paths.Generated + "/SlateNormal.png", 12f, 36f, 12.4f, 2.0f));
            Material darkWood = SceneKit.Mat("DarkWood", SceneKit.LitShader, new Color(0.18f, 0.14f, 0.10f), 0f, 0.9f);
            Material bark = SceneKit.Mat("Bark", SceneKit.LitShader, new Color(0.33f, 0.27f, 0.20f), 0f, 0.95f);

            Mesh hutMesh = LoadOrMake("Hut", () => ProceduralBuildings.Hut(v.hutFootprintMetres[0], v.hutFootprintMetres[1], v.hutWallHeight, v.hutRoofHeight));
            Mesh aleMesh = LoadOrMake("Alehouse", () => ProceduralBuildings.Alehouse(v.alehouseFootprint[0], v.alehouseFootprint[1], v.alehouseWallHeight));
            Mesh churchMesh = LoadOrMake("Church", () => ProceduralBuildings.Church(v.churchNave[0], v.churchNave[1], v.churchNave[2], v.churchTowerHeight));

            var root = new GameObject("Village_" + villageName).transform;
            root.position = centre;
            float green = v.greenRadiusMetres;

            // Church: north of the green, chancel (its +Z) pointing east, so the tower stands west.
            Vector3 churchPos = centre + new Vector3(0f, 0f, green + 14f);
            Transform church = Place(root, "Church", churchMesh, new[] { stone, slate }, terrain, churchPos, Quaternion.Euler(0f, 90f, 0f));
            Transform home = null;
            // Alehouse: east, door toward the green.
            Transform alehouse = Place(root, "Alehouse", aleMesh, new[] { wattle, thatch }, terrain, centre + new Vector3(green + 9f, 0f, -4f), Quaternion.Euler(0f, 90f, 0f));
            // Huts in a loose ring, skipping the church (north) and alehouse (east) sectors.
            int placed = 0, attempts = 0;
            while (placed < v.hutCount && attempts < v.hutCount * 6)
            {
                attempts++;
                float angle = rng.Range(0f, 360f);
                if ((angle > 60f && angle < 120f) || (angle > -20f && angle < 25f) || angle > 340f) continue; // keep north and east clear
                float radius = green + 6f + rng.Range(0f, 14f);
                Vector3 pos = centre + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, 0f, Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
                bool tooClose = false;
                foreach (Transform t in root) if (t.name.StartsWith("Hut") && Vector3.Distance(t.position, pos) < 8.5f) { tooClose = true; break; }
                if (tooClose) continue;
                Vector3 toGreen = centre - pos; toGreen.y = 0f;
                Quaternion face = Quaternion.LookRotation(toGreen.normalized, Vector3.up) * Quaternion.Euler(0f, rng.Range(-12f, 12f), 0f);
                Transform hut = Place(root, "Hut_" + placed, hutMesh, new[] { wattle, thatch }, terrain, pos, face);
                if (home == null) home = hut;
                placed++;
            }
            // Well on the green
            var well = SceneKit.Primitive(PrimitiveType.Cylinder, "Well", root, Vector3.zero, new Vector3(1.6f, 0.5f, 1.6f), stone, keepCollider: true);
            well.transform.position = new Vector3(centre.x, SceneKit.GroundY(terrain, centre) + 0.5f, centre.z);
            var post = SceneKit.Primitive(PrimitiveType.Cube, "WellBeam", root, Vector3.zero, new Vector3(0.12f, 2.2f, 0.12f), darkWood);
            post.transform.position = well.transform.position + new Vector3(0.7f, 1.3f, 0f);
            // Village masters: one placeholder villager per trade, standing by the green, named from the name lists.
            var masters = new GameObject("Masters").transform; masters.SetParent(root, true);
            var nameGen = new NameGenerator(DataFiles.LoadNames(), rng.Fork(5));
            string[] trades = { "farmer", "blacksmith", "miller", "hunter", "brewer", "carpenter", "shepherd", "fisherman" };
            Material villagerMat = SceneKit.Mat("Villager", SceneKit.LitShader, new Color(0.3f, 0.26f, 0.2f), 0f, 0.9f);
            Material skinMat = SceneKit.Mat("Skin", SceneKit.LitShader, new Color(0.58f, 0.46f, 0.37f), 0f, 0.9f);
            Mesh manMesh = SceneKit.ArtMesh("Characters/Man.fbx");
            PlantOaks(root, terrain, centre, green, rng, bark);
            int k = 0;
            foreach (string trade in trades)
            {
                float a = (k / (float)trades.Length) * Mathf.PI * 2f + 0.3f;
                Vector3 pos = centre + new Vector3(Mathf.Cos(a) * (green + 3f), 0f, Mathf.Sin(a) * (green + 3f));
                pos.y = SceneKit.GroundY(terrain, pos);
                var m = new GameObject("Master_" + trade); m.transform.SetParent(masters, true); m.transform.position = pos;
                m.transform.rotation = Quaternion.LookRotation(new Vector3(centre.x - pos.x, 0f, centre.z - pos.z));
                if (manMesh != null) SceneKit.MeshChild("Body", m.transform, manMesh, villagerMat, skinMat, collider: true);
                else
                {
                    SceneKit.Primitive(PrimitiveType.Capsule, "Body", m.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.72f, 0.5f), villagerMat, keepCollider: true);
                    SceneKit.Primitive(PrimitiveType.Sphere, "Head", m.transform, new Vector3(0f, 1.75f, 0f), new Vector3(0.28f, 0.3f, 0.28f), villagerMat);
                }
                var tag = m.AddComponent<VillagerTag>(); tag.npcId = 100 + k; tag.trade = trade; tag.displayName = nameGen.Given(k % 3 == 2, false); tag.disposition = rng.Range(-0.2f, 0.5f);
                k++;
            }
            // Workplace: a bench behind the player's hut; one spot stands in for field, forge, mill and weir in Phase 4.
            var work = new GameObject("Workplace").transform; work.SetParent(root, true);
            work.position = home != null ? home.position - home.forward * 6f : centre + new Vector3(0f, 0f, -green - 10f);
            var workMarker = SceneKit.Primitive(PrimitiveType.Cube, "WorkBench", work, Vector3.zero, new Vector3(1.6f, 0.8f, 0.8f), darkWood, keepCollider: true);
            workMarker.transform.position = new Vector3(work.position.x, SceneKit.GroundY(terrain, work.position) + 0.4f, work.position.z);
            var site = root.gameObject.AddComponent<SettlementSite>(); site.settlementId = "ashby_village"; site.centre = root; site.church = church; site.alehouse = alehouse; site.radius = 70f;
            return new Result { Root = root, Church = church, Alehouse = alehouse, Home = home, Workplace = work, Masters = masters, Green = centre, Wattle = wattle, Thatch = thatch, Stone = stone, Slate = slate, DarkWood = darkWood, Bark = bark, HutMesh = hutMesh, AlehouseMesh = aleMesh, ChurchMesh = churchMesh };
        }

        /// <summary>A loose ring of oaks beyond the huts when the Blender oak exists; nothing otherwise (the moor stays bare).</summary>
        private static void PlantOaks(Transform root, Terrain terrain, Vector3 centre, float green, DeterministicRng rng, Material bark)
        {
            Mesh oak = SceneKit.ArtMesh("Nature/Oak.fbx"); if (oak == null) return;
            Material leaf = SceneKit.Mat("OakLeaf", SceneKit.LitShader, new Color(0.30f, 0.36f, 0.20f), 0f, 0.95f);
            var trees = new GameObject("Oaks").transform; trees.SetParent(root, true);
            for (int i = 0; i < 26; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f), r = green + 36f + rng.Range(0f, 40f);
                var pos = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); pos.y = SceneKit.GroundY(terrain, pos) - 0.2f;
                var t = SceneKit.MeshChild("Oak_" + i, trees, oak, bark, leaf, collider: true);
                t.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rng.Range(0f, 360f), 0f));
                float s = rng.Range(0.8f, 1.25f); t.transform.localScale = new Vector3(s, s, s); t.isStatic = true;
            }
        }

        private static Transform Place(Transform root, string name, Mesh mesh, Material[] mats, Terrain terrain, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, true);
            pos.y = SceneKit.GroundY(terrain, pos) - 0.15f; // sink slightly into uneven ground
            go.transform.SetPositionAndRotation(pos, rot);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mesh.subMeshCount >= 2 ? mats : new[] { mats[0] };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh;
            go.isStatic = true;
            return go.transform;
        }

        /// <summary>Prefer a Blender FBX in Assets/Art/Buildings/&lt;Name&gt;.fbx; else generate and cache a procedural mesh asset.</summary>
        private static Mesh LoadOrMake(string name, System.Func<Mesh> make)
        {
            string fbx = BuildingsFolder + "/" + name + ".fbx";
            if (!File.Exists(fbx)) fbx = BuildingsFolder + "/" + name + ".obj";
            if (File.Exists(fbx))
            {
                var fromFbx = AssetDatabase.LoadAssetAtPath<Mesh>(fbx);
                if (fromFbx != null) { Debug.Log("[Ashes and Crowns] using Blender asset " + fbx); return fromFbx; }
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx)) if (o is Mesh m) { Debug.Log("[Ashes and Crowns] using Blender asset " + fbx); return m; }
            }
            string path = Paths.Generated + "/" + name + "_Procedural.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            Mesh mesh = make();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}

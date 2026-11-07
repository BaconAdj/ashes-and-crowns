using System.Collections.Generic;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// Low-poly building meshes generated in code: the fallback for when the Blender FBX is not present,
    /// and the reference shapes the Blender scripts in Tools/blender reproduce. 1 unit = 1 m, origin at
    /// the ground centre, +Z is the front. Flat shaded (no shared vertices).
    /// </summary>
    public static class ProceduralBuildings
    {
        private sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>(256);
            public readonly List<Vector2> UV = new List<Vector2>(256);
            public readonly List<int> Walls = new List<int>(512), Roof = new List<int>(256);

            public void Quad(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale = 0.5f)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                float w = Vector3.Distance(a, b) * uvScale, h = Vector3.Distance(a, d) * uvScale;
                UV.Add(new Vector2(0, 0)); UV.Add(new Vector2(w, 0)); UV.Add(new Vector2(w, h)); UV.Add(new Vector2(0, h));
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }

            public void Tri(List<int> tris, Vector3 a, Vector3 b, Vector3 c)
            {
                int i = V.Count; V.Add(a); V.Add(b); V.Add(c);
                UV.Add(new Vector2(0, 0)); UV.Add(new Vector2(1, 0)); UV.Add(new Vector2(0.5f, 1));
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            }

            /// <summary>Axis-aligned box with its base at y0, centred at (cx, cz).</summary>
            public void Box(List<int> tris, float cx, float cz, float y0, float sx, float sy, float sz)
            {
                float x0 = cx - sx / 2, x1 = cx + sx / 2, z0 = cz - sz / 2, z1 = cz + sz / 2, y1 = y0 + sy;
                Quad(tris, new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1)); // front +Z
                Quad(tris, new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0)); // back
                Quad(tris, new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1)); // right
                Quad(tris, new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0)); // left
                Quad(tris, new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0)); // top
            }

            /// <summary>Gabled roof over a footprint sx (width, gable axis) by sz (length), eaves at y0, ridge along Z.</summary>
            public void GableRoof(float cx, float cz, float y0, float sx, float sz, float height, float overhang)
            {
                float x0 = cx - sx / 2 - overhang, x1 = cx + sx / 2 + overhang, z0 = cz - sz / 2 - overhang, z1 = cz + sz / 2 + overhang, y1 = y0 + height;
                var r0 = new Vector3(cx, y1, z0); var r1 = new Vector3(cx, y1, z1);
                Quad(Roof, new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), r1, r0, 0.6f);   // left slope
                Quad(Roof, new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), r0, r1, 0.6f);   // right slope
                // gable ends (wall material)
                Tri(Walls, new Vector3(x1 - overhang, y0, z1 - overhang), new Vector3(x0 + overhang, y0, z1 - overhang), new Vector3(cx, y1, z1 - overhang));
                Tri(Walls, new Vector3(x0 + overhang, y0, z0 + overhang), new Vector3(x1 - overhang, y0, z0 + overhang), new Vector3(cx, y1, z0 + overhang));
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(V); m.SetUVs(0, UV);
                m.subMeshCount = 2;
                m.SetTriangles(Walls, 0); m.SetTriangles(Roof, 1);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Peasant hut: wattle box, thatched gable roof. Submesh 0 walls, 1 roof.</summary>
        public static Mesh Hut(float width, float depth, float wallHeight, float roofHeight)
        {
            var b = new Builder();
            b.Box(b.Walls, 0f, 0f, 0f, width, wallHeight, depth);
            b.GableRoof(0f, 0f, wallHeight, width, depth, roofHeight, 0.45f);
            // door lintel bump on the front so the facade is not blank
            b.Box(b.Walls, 0f, depth / 2 + 0.05f, 0f, 1.0f, 1.7f, 0.1f);
            return b.ToMesh("Hut");
        }

        /// <summary>Alehouse: longer hall with a steeper roof and a porch.</summary>
        public static Mesh Alehouse(float width, float depth, float wallHeight)
        {
            var b = new Builder();
            b.Box(b.Walls, 0f, 0f, 0f, width, wallHeight, depth);
            b.GableRoof(0f, 0f, wallHeight, width, depth, wallHeight * 1.1f, 0.5f);
            b.Box(b.Walls, 0f, depth / 2 + 0.8f, 0f, 2.4f, wallHeight * 0.8f, 1.6f);
            b.GableRoof(0f, depth / 2 + 0.8f, wallHeight * 0.8f, 2.4f, 1.6f, 1.0f, 0.3f);
            return b.ToMesh("Alehouse");
        }

        /// <summary>Norman parish church: nave with gable roof, lower chancel, square west tower. Front (+Z) is east.</summary>
        public static Mesh Church(float naveWidth, float naveWallHeight, float naveLength, float towerHeight)
        {
            var b = new Builder();
            float towerSide = naveWidth * 0.75f;
            float towerZ = -naveLength / 2 - towerSide / 2;
            b.Box(b.Walls, 0f, 0f, 0f, naveWidth, naveWallHeight, naveLength);
            b.GableRoof(0f, 0f, naveWallHeight, naveWidth, naveLength, naveWidth * 0.55f, 0.4f);
            float chancelW = naveWidth * 0.7f, chancelL = naveLength * 0.35f, chancelH = naveWallHeight * 0.8f;
            float chancelZ = naveLength / 2 + chancelL / 2;
            b.Box(b.Walls, 0f, chancelZ, 0f, chancelW, chancelH, chancelL);
            b.GableRoof(0f, chancelZ, chancelH, chancelW, chancelL, chancelW * 0.5f, 0.35f);
            b.Box(b.Walls, 0f, towerZ, 0f, towerSide, towerHeight, towerSide);
            // parapet and a low pyramid cap
            b.Box(b.Walls, 0f, towerZ, towerHeight, towerSide + 0.3f, 0.6f, towerSide + 0.3f);
            float capY = towerHeight + 0.6f; float h = towerSide * 0.45f; float s = towerSide / 2 + 0.15f;
            var apex = new Vector3(0f, capY + h, towerZ);
            b.Tri(b.Roof, new Vector3(-s, capY, towerZ + s), new Vector3(s, capY, towerZ + s), apex);
            b.Tri(b.Roof, new Vector3(s, capY, towerZ + s), new Vector3(s, capY, towerZ - s), apex);
            b.Tri(b.Roof, new Vector3(s, capY, towerZ - s), new Vector3(-s, capY, towerZ - s), apex);
            b.Tri(b.Roof, new Vector3(-s, capY, towerZ - s), new Vector3(-s, capY, towerZ + s), apex);
            // buttresses along the nave
            for (int i = -1; i <= 1; i++)
            {
                float z = i * naveLength / 3f;
                b.Box(b.Walls, -naveWidth / 2 - 0.3f, z, 0f, 0.6f, naveWallHeight * 0.85f, 0.8f);
                b.Box(b.Walls, naveWidth / 2 + 0.3f, z, 0f, 0.6f, naveWallHeight * 0.85f, 0.8f);
            }
            return b.ToMesh("Church");
        }
    }
}

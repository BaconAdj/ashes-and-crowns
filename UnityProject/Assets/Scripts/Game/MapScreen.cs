using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Game
{
    /// <summary>M: a parchment map of England with every settlement, you, and where your job is going. Drawn from the coast polygon at first open.</summary>
    public sealed class MapScreen : MonoBehaviour
    {
        public HudController hud;
        public SaveManager saveManager;
        public Transform player;
        public float terrainSize = 8192f;
        private Texture2D _map;
        private bool _open;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || RunSession.Context == null || RunSession.Context.Settlements == null) return;
            if (saveManager != null && saveManager.IsQuitScreenOpen) { if (_open) { _open = false; hud.HideMap(); } return; }
            if (!kb.mKey.wasPressedThisFrame) return;
            _open = !_open;
            if (!_open) { hud.HideMap(); return; }
            if (_map == null) _map = Draw(DataFiles.LoadCoast());
            var ctx = RunSession.Context;
            hud.ShowMap(_map, ctx.Settlements, new Vector2(player.position.x / terrainSize, player.position.z / terrainSize), ctx.ActiveJob != null ? ctx.ActiveJob.ToId : null);
        }

        private static Texture2D Draw(CoastData coast)
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "EnglandMap" };
            float[] xs = new float[coast.mainland.Length], zs = new float[coast.mainland.Length];
            for (int i = 0; i < xs.Length; i++) { xs[i] = coast.mainland[i].x; zs[i] = coast.mainland[i].z; }
            var sea = new Color(0.16f, 0.17f, 0.19f); var land = new Color(0.58f, 0.52f, 0.40f); var edge = new Color(0.25f, 0.22f, 0.17f);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size, nz = (y + 0.5f) / size;
                bool inside = MapGeometry.PointInRing(xs, zs, nx, nz);
                if (!inside && coast.islands != null)
                    foreach (var isl in coast.islands)
                    {
                        float[] ix = new float[isl.points.Length], iz = new float[isl.points.Length];
                        for (int k = 0; k < ix.Length; k++) { ix[k] = isl.points[k].x; iz[k] = isl.points[k].z; }
                        if (MapGeometry.PointInRing(ix, iz, nx, nz)) { inside = true; break; }
                    }
                Color c = inside ? land : sea;
                if (inside && MapGeometry.DistanceToRing(xs, zs, nx, nz) < 0.004f) c = edge;
                px[y * size + x] = c;
            }
            // Rivers as faint lines
            if (coast.rivers != null)
                foreach (var r in coast.rivers)
                    for (int i = 1; i < r.points.Length; i++)
                    {
                        Vector2 a = new Vector2(r.points[i - 1].x, r.points[i - 1].z) * size, b = new Vector2(r.points[i].x, r.points[i].z) * size;
                        int steps = Mathf.CeilToInt(Vector2.Distance(a, b));
                        for (int s = 0; s <= steps; s++) { Vector2 p = Vector2.Lerp(a, b, s / (float)Mathf.Max(1, steps)); int ix = Mathf.Clamp((int)p.x, 0, size - 1), iy = Mathf.Clamp((int)p.y, 0, size - 1); px[iy * size + ix] = new Color(0.3f, 0.33f, 0.38f); }
                    }
            tex.SetPixels(px); tex.Apply(false, false);
            return tex;
        }
    }
}

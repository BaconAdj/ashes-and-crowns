using System;

namespace AshesAndCrowns.Sim
{
    /// <summary>Point-in-polygon and distance over flat coordinate arrays, shared by the terrain builder and the settlement generator.</summary>
    public static class MapGeometry
    {
        /// <summary>FNV-1a over a string: stable across runtimes, unlike string.GetHashCode.</summary>
        public static uint StableHash(string s) { uint h = 2166136261; if (s != null) foreach (char c in s) { h ^= c; h *= 16777619; } return h; }

        public static bool PointInRing(float[] xs, float[] zs, float x, float z)
        {
            bool inside = false;
            int n = Math.Min(xs.Length, zs.Length);
            for (int i = 0, j = n - 1; i < n; j = i++)
                if ((zs[i] > z) != (zs[j] > z) && x < (xs[j] - xs[i]) * (z - zs[i]) / (zs[j] - zs[i] + 1e-12f) + xs[i]) inside = !inside;
            return inside;
        }

        public static float DistanceToRing(float[] xs, float[] zs, float x, float z)
        {
            float best = float.MaxValue;
            int n = Math.Min(xs.Length, zs.Length);
            for (int i = 0, j = n - 1; i < n; j = i++) best = Math.Min(best, SegmentDistance(xs[j], zs[j], xs[i], zs[i], x, z));
            return best;
        }

        public static float SegmentDistance(float ax, float az, float bx, float bz, float x, float z)
        {
            float dx = bx - ax, dz = bz - az;
            float len2 = dx * dx + dz * dz;
            float t = len2 > 1e-12f ? Math.Max(0f, Math.Min(1f, ((x - ax) * dx + (z - az) * dz) / len2)) : 0f;
            float px = ax + t * dx - x, pz = az + t * dz - z;
            return (float)Math.Sqrt(px * px + pz * pz);
        }
    }
}

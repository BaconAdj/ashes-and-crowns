using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AshesAndCrowns.Battle
{
    /// <summary>
    /// A Burst-readable copy of the terrain heightmap so jobs can keep soldiers on the ground
    /// without touching Terrain.SampleHeight (main-thread only).
    /// </summary>
    public struct HeightField
    {
        [ReadOnly] public NativeArray<float> Heights; // row-major, Resolution x Resolution, normalised 0..1
        public int Resolution;
        public float3 Origin;
        public float3 Size;

        public bool IsCreated => Heights.IsCreated;

        public float Sample(float worldX, float worldZ)
        {
            if (!Heights.IsCreated || Resolution < 2) return Origin.y;
            float u = math.saturate((worldX - Origin.x) / Size.x) * (Resolution - 1);
            float v = math.saturate((worldZ - Origin.z) / Size.z) * (Resolution - 1);
            int x0 = (int)math.floor(u), z0 = (int)math.floor(v);
            int x1 = math.min(x0 + 1, Resolution - 1), z1 = math.min(z0 + 1, Resolution - 1);
            float fx = u - x0, fz = v - z0;
            float h00 = Heights[z0 * Resolution + x0], h10 = Heights[z0 * Resolution + x1];
            float h01 = Heights[z1 * Resolution + x0], h11 = Heights[z1 * Resolution + x1];
            float h = math.lerp(math.lerp(h00, h10, fx), math.lerp(h01, h11, fx), fz);
            return Origin.y + h * Size.y;
        }

        public static HeightField FromTerrain(Terrain terrain, Allocator allocator)
        {
            TerrainData data = terrain.terrainData;
            int res = data.heightmapResolution;
            float[,] raw = data.GetHeights(0, 0, res, res); // [z, x]
            var heights = new NativeArray<float>(res * res, allocator, NativeArrayOptions.UninitializedMemory);
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    heights[z * res + x] = raw[z, x];
            return new HeightField
            {
                Heights = heights,
                Resolution = res,
                Origin = terrain.transform.position,
                Size = data.size
            };
        }

        public void Dispose()
        {
            if (Heights.IsCreated) Heights.Dispose();
        }
    }
}

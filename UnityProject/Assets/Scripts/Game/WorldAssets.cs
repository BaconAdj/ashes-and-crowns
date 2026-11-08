using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Materials and meshes the runtime spawners need, wired by the scene builder so nothing is loaded by path at play time.</summary>
    public sealed class WorldAssets : MonoBehaviour
    {
        public Material wattle, thatch, stone, slate, darkWood, villager;
        public Mesh hut, alehouse, church;
        public Mesh oak, keep;              // optional (Blender art)
        public Material bark, leaf;
        public Terrain terrain;
    }
}

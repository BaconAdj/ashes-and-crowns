using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Marks a village NPC placeholder with who they are.</summary>
    public sealed class VillagerTag : MonoBehaviour
    {
        public int npcId;
        public string displayName = "Someone";
        public string trade = string.Empty;
        public float disposition;
    }
}

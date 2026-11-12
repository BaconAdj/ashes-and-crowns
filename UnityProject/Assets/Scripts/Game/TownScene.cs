using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Marks a scene that is one town at full size (Wallingford) rather than the England overworld: lives started
    /// here begin in this settlement, and the map shows the body at the town's place in England, not at scene coordinates.</summary>
    public sealed class TownScene : MonoBehaviour
    {
        public string settlementId = "wallingford";
        public Vector2 mapPosition;       // the town's normalized place on the map of England
    }
}

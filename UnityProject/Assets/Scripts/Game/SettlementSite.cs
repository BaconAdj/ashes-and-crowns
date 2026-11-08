using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>A settlement's place in the world: where its church, alehouse and centre stand. The law and the market live in the Sim record with this id.</summary>
    public sealed class SettlementSite : MonoBehaviour
    {
        public string settlementId = string.Empty;
        public Transform centre, church, alehouse;
        public float radius = 60f;
    }
}

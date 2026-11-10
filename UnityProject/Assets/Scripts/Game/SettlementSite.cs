using System.Collections.Generic;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>A settlement's place in the world: where its church, alehouse, huts and the people's stations stand. The law and the market live in the Sim record with this id.</summary>
    public sealed class SettlementSite : MonoBehaviour
    {
        public string settlementId = string.Empty;
        public Transform centre, church, alehouse;
        public float radius = 60f;
        public List<Transform> huts = new List<Transform>(12);        // serialized: Ashby is baked into the scene
        public List<Villager> villagers = new List<Villager>(16);
        public List<Transform> oaks = new List<Transform>(30);          // wood to be cut, with an axe and an hour
        /// <summary>Open ground the village uses: the well on the green.</summary>
        public Transform Green => centre;
        public Transform Altar => BuildingKit.Find(church, "Altar");
        public Transform Nave => BuildingKit.Find(church, "Nave");
        public Transform Bar => BuildingKit.Find(alehouse, "Bar");
    }
}

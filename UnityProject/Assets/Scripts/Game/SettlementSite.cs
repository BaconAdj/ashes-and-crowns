using System.Collections.Generic;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>A settlement's place in the world: where its church, alehouse, huts and the people's stations stand. The law and the market live in the Sim record with this id.</summary>
    public sealed class SettlementSite : MonoBehaviour
    {
        public string settlementId = string.Empty;
        public Transform centre, church, alehouse;
        public Transform keep;                                           // the lord's keep, where audiences are sought (towns, castles, cities)
        public float radius = 60f;
        public List<Transform> huts = new List<Transform>(12);        // serialized: Ashby is baked into the scene
        public List<Villager> villagers = new List<Villager>(16);
        public List<Transform> oaks = new List<Transform>(30);          // wood to be cut, with an axe and an hour
        public List<Deer> deer = new List<Deer>(12);                     // the lord's deer, if you dare
        public List<MarketGoodTag> goods = new List<MarketGoodTag>(16);  // what is laid out on the stalls
        public Transform muster;                                         // the banner post on the green of a town: the king's penny and the muster
        /// <summary>Open ground the village uses: the well on the green.</summary>
        public Transform Green => centre;
        public Transform Altar => BuildingKit.Find(church, "Altar");
        public Transform Nave => BuildingKit.Find(church, "Nave");
        public Transform Bar => BuildingKit.Find(alehouse, "Bar");
    }
}

using System.Collections;
using AshesAndCrowns.Battle;
using AshesAndCrowns.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshesAndCrowns.Tests.PlayMode
{
    /// <summary>Phase 0 smoke test: two armies charge each other on a flat terrain and somebody dies.</summary>
    public class BattleSmokeTests
    {
        [UnityTest]
        public IEnumerator FiftyVersusFifty_Charging_ProducesCasualties_WithinThirtySeconds()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(200f, 10f, 200f) };
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            var go = new GameObject("Director");
            var director = go.AddComponent<BattleDirector>();
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            director.soldierMesh = capsule.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(capsule);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            director.playerSoldierMaterial = new Material(shader) { enableInstancing = true };
            director.enemySoldierMaterial = new Material(shader) { enableInstancing = true };

            var balance = new Balance(); balance.battle.soldiersPerSide = 50; balance.battle.unitSize = 50;
            balance.battle.enemyAdvanceDelaySeconds = 0f;
            director.Setup(balance, new DeterministicRng(1), terrainGo.GetComponent<Terrain>(), new Vector3(100f, 0f, 80f), new Vector3(100f, 0f, 120f));
            Assert.AreEqual(100, director.SoldierCount);
            for (int u = 0; u < director.UnitCount; u++)
                if (director.Units[u].Side == (byte)Side.Player)
                    director.IssueOrder(u, OrderType.Charge, new Unity.Mathematics.float3(100f, 0f, 120f));

            float t = 0f;
            while (t < 30f && director.PlayerLosses + director.EnemyLosses == 0) { t += Time.deltaTime; yield return null; }
            Assert.Greater(director.PlayerLosses + director.EnemyLosses, 0, "no casualties after 30 s of charging");
            Assert.LessOrEqual(director.PlayerAlive, 50);
            Object.Destroy(go); Object.Destroy(terrainGo);
        }
    }
}

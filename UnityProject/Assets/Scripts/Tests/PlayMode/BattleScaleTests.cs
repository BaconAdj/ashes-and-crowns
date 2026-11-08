using System.Collections;
using AshesAndCrowns.Battle;
using AshesAndCrowns.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshesAndCrowns.Tests.PlayMode
{
    /// <summary>Two thousand soldiers for 180 frames: logs the mean frame time and checks the battle ends in a rout, not annihilation.</summary>
    public class BattleScaleTests
    {
        [UnityTest]
        public IEnumerator TwoThousandSoldiers_RunAndSomebodyBreaks()
        {
            var data = new TerrainData { heightmapResolution = 65, size = new Vector3(600f, 10f, 600f) };
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            var go = new GameObject("Director");
            var director = go.AddComponent<BattleDirector>();
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            director.soldierMesh = capsule.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(capsule);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            director.playerSoldierMaterial = new Material(shader) { enableInstancing = true };
            director.enemySoldierMaterial = new Material(shader) { enableInstancing = true };
            var balance = new Balance(); balance.battle.soldiersPerSide = 1000; balance.battle.unitSize = 50; balance.battle.enemyAdvanceDelaySeconds = 0f;
            director.Setup(balance, new DeterministicRng(7), terrainGo.GetComponent<Terrain>(), new Vector3(300f, 0f, 200f), new Vector3(300f, 0f, 400f));
            Assert.AreEqual(2000, director.SoldierCount); Assert.AreEqual(40, director.UnitCount);
            for (int u = 0; u < director.UnitCount; u++) if (director.Units[u].Side == (byte)Side.Player) director.IssueOrder(u, OrderType.Charge, new Unity.Mathematics.float3(300f, 0f, 400f));
            float total = 0f; int frames = 0; float t = 0f;
            while (t < 240f && !director.Ended) { yield return null; total += Time.deltaTime; frames++; t += Time.deltaTime; }
            Debug.Log($"[perf] 2000 soldiers: {frames} frames, mean {total / Mathf.Max(1, frames) * 1000f:F1} ms, ended={director.Ended}, dead {director.PlayerLosses + director.EnemyLosses}");
            Assert.IsTrue(director.Ended, "a battle must end in a rout or surrender");
            Assert.Less(director.PlayerLosses + director.EnemyLosses, 2000, "annihilation is not how battles end");
            Object.Destroy(go); Object.Destroy(terrainGo);
        }
    }
}

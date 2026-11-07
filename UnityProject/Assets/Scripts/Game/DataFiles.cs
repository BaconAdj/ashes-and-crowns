using System.IO;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// Typed access to Assets/Data/*.json. In the editor the files are read straight from Assets/Data so
    /// balance.json can be edited and re-entered without a reimport; builds read the copy that
    /// Editor/DataStreamingCopy places into StreamingAssets/Data.
    /// </summary>
    public static class DataFiles
    {
        public const string Folder = "Data";

        public static string Root =>
            Application.isEditor ? Path.Combine(Application.dataPath, Folder) : Path.Combine(Application.streamingAssetsPath, Folder);

        public static string ReadText(string fileName)
        {
            string path = Path.Combine(Root, fileName);
            if (!File.Exists(path)) throw new FileNotFoundException("Data file missing: " + path);
            return File.ReadAllText(path);
        }

        public static Balance LoadBalance()
        {
            var balance = JsonUtility.FromJson<Balance>(ReadText("balance.json"));
            var errors = BalanceValidator.Validate(balance);
            foreach (string e in errors) Debug.LogError("[balance.json] " + e);
            return balance;
        }

        public static WorldData LoadWorld() => JsonUtility.FromJson<WorldData>(ReadText("world.json"));
    }

    [System.Serializable] public sealed class Point2 { public float x, z; }
    [System.Serializable] public sealed class Hill { public float x, z, height, radius; }
    [System.Serializable] public sealed class Phase0Battlefield
    {
        public float terrainSize = 400f, terrainHeight = 40f; public int noiseSeed = 1135;
        public Hill vantageHill; public Point2 playerArmyCentre, enemyArmyCentre, playerSpawn, horseSpawn;
    }
    [System.Serializable] public sealed class WorldData { public float scaleMetresPerUnit; public Phase0Battlefield phase0Battlefield; }
}

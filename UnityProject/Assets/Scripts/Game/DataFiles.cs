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
        public static CoastData LoadCoast() => JsonUtility.FromJson<CoastData>(ReadText("england_coast.json"));
        public static BackgroundsData LoadBackgrounds() => JsonUtility.FromJson<BackgroundsData>(ReadText("backgrounds.json"));
        public static NamesData LoadNames() => JsonUtility.FromJson<NamesData>(ReadText("names.json"));
        public static FiguresData LoadFigures() => JsonUtility.FromJson<FiguresData>(ReadText("figures.json"));
        public static ItemsData LoadItems() => JsonUtility.FromJson<ItemsData>(ReadText("items.json"));
        public static TradesData LoadTrades() => JsonUtility.FromJson<TradesData>(ReadText("trades.json"));
    }

    [System.Serializable] public sealed class Point2 { public float x, z; }
    [System.Serializable] public sealed class Hill { public float x, z, height, radius; }
    [System.Serializable] public sealed class Phase0Battlefield
    {
        public float terrainSize = 400f, terrainHeight = 40f; public int noiseSeed = 1135;
        public Hill vantageHill; public Point2 playerArmyCentre, enemyArmyCentre, playerSpawn, horseSpawn;
    }
    [System.Serializable] public sealed class Overworld
    {
        public int terrainSizeMetres = 8192, heightmapResolution = 2049;
        public float maxTerrainHeightMetres = 160f, seaLevelMetres = 12f, realHeightToWorldHeight = 0.12f, coastFalloffKm = 6f, landBaseHeightM = 30f;
    }
    [System.Serializable] public sealed class Location
    {
        public string id, name, type, allegiance, notes; public float nx, nz, lat, lon; public int population, garrison;
    }
    [System.Serializable] public sealed class Phase1Start { public string villageId; public float churchTowerHeightM; }
    [System.Serializable] public sealed class WorldData
    {
        public float scaleMetresPerUnit; public Overworld overworld; public Location[] locations; public Phase0Battlefield phase0Battlefield; public Phase1Start phase1Start;
        public Location Find(string id) { if (locations != null) foreach (var l in locations) if (l.id == id) return l; return null; }
    }

    [System.Serializable] public sealed class MapPoint { public float x, z; }
    [System.Serializable] public sealed class PointRing { public MapPoint[] points; }
    [System.Serializable] public sealed class NamedPolyline { public string name; public MapPoint[] points; }
    [System.Serializable] public sealed class Upland { public string name; public float nx, nz, radiusKm, heightM; }
    [System.Serializable] public sealed class Lowland { public string name; public float nx, nz, radiusKm; }
    [System.Serializable] public sealed class CoastData
    {
        public float mapSideKm; public float latMin, latMax, lonMin, lonMax;
        public MapPoint[] mainland; public PointRing[] islands; public Upland[] uplands; public Lowland[] lowlands; public NamedPolyline[] rivers;
    }
}

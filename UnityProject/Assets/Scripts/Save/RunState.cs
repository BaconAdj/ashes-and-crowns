using AshesAndCrowns.Sim;

namespace AshesAndCrowns.Save
{
    /// <summary>Everything a run needs to resume exactly. Plain fields; the codec writes them in a fixed order.</summary>
    public sealed class RunState
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public long Seed;
        public ulong RngState;
        public string SceneId = string.Empty;
        public long SavedAtUtcTicks;
        public AffairsChoice Affairs = AffairsChoice.None;
        public float Hostility;              // where the player was when the game closed
        public int EnemiesNearby;
        public bool InBattle;

        // World
        public double CalendarTotalHours;
        public WeatherSnapshot Weather;
        public ulong WeatherRngState;

        // Player
        public string PlayerName = "A nameless rider";
        public float PlayerX, PlayerY, PlayerZ, PlayerYaw;
        public bool Mounted;
        public float HorseX, HorseY, HorseZ, HorseYaw;
        public BodySnapshot Body = new BodySnapshot();
        public float Stamina;

        // The life lived (death screen)
        public double HoursSurvived;
        public int Kills, MenLost, Battles, PeakWealth;

        // Battle (optional)
        public BattleSnapshot Battle;
    }

    /// <summary>Flat arrays mirroring BattleDirector's structure-of-arrays.</summary>
    public sealed class BattleSnapshot
    {
        public int SoldierCount, UnitCount, PlayerSoldierEnd;
        public float Elapsed, EnemyThinkTimer;
        public bool Started, PlayerCharacterAlive;
        public float[] Positions = new float[0];   // xyz per soldier
        public float[] Yaws = new float[0];
        public float[] Health = new float[0];
        public float[] Cooldowns = new float[0];
        public byte[] Alive = new byte[0];
        public byte[] Side = new byte[0];
        public int[] UnitOf = new int[0];
        public int[] SlotOf = new int[0];
        public int[] Targets = new int[0];
        public byte[] UnitSide = new byte[0], UnitFormation = new byte[0], UnitOrder = new byte[0], UnitAlive = new byte[0];
        public int[] UnitStart = new int[0], UnitCount_ = new int[0], UnitAliveCount = new int[0];
        public float[] UnitAnchor = new float[0];  // xyz per unit
        public float[] UnitYaw = new float[0];
        public float[] UnitTarget = new float[0];  // xyz per unit
    }
}

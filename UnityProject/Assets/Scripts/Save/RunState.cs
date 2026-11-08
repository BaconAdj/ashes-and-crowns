using AshesAndCrowns.Sim;

namespace AshesAndCrowns.Save
{
    /// <summary>Everything a run needs to resume exactly. Plain fields; the codec writes them in a fixed order.</summary>
    public sealed class RunState
    {
        public const int CurrentVersion = 4;

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

        // Version 2: the character, family and baronage
        public bool HasCharacter;
        public CharacterSheet Character;
        public Npc[] Family = new Npc[0];
        public MinorLord[] Baronage = new MinorLord[0];
        public string SpawnLocationId = string.Empty;
        public bool OpeningSeen;

        // Version 4: survival and the village
        public float Hunger = 0.85f, Rest = 1f;
        public byte SicknessKind; public int SickDays;
        public string[] InventoryIds = new string[0]; public int[] InventoryCounts = new int[0];
        public float StandingValue = 0.5f; public int MassesAttended, MassesMissed;
        public string ApprenticeTrade = string.Empty; public int ApprenticeMaster; public float ApprenticeSeasons, ApprenticeCompetence;
        public string[] MarketIds = new string[0]; public float[] MarketStocks = new float[0]; public byte MarketShock; public float MarketShockStrength, MarketWealth = 1f;
        public float Outbreak; public long LastMassDay = -1;
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

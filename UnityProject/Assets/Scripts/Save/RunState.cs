using AshesAndCrowns.Sim;

namespace AshesAndCrowns.Save
{
    /// <summary>Everything a run needs to resume exactly. Plain fields; the codec writes them in a fixed order.</summary>
    public sealed class RunState
    {
        public const int CurrentVersion = 8;

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

        // Version 5: England
        public SettlementSnapshot[] Settlements = new SettlementSnapshot[0];
        public float[] Reputation = new float[4]; public float Notoriety;
        public CrimeSnapshot[] Crimes = new CrimeSnapshot[0]; public bool Outlawed; public float SanctuaryDaysLeft; public bool SanctuaryExpired;
        public JobSnapshot ActiveJob; public int NextJobId;
        public string HomeSettlementId = string.Empty;

        // Version 7: service
        public byte ServiceRole, ServiceSide; public string ServiceCompany = "", ServiceCaptain = "", ServicePatron = ""; public int ServiceWage, ServiceDays, ServiceOwed, ServiceBattles, ServiceRenown, ServiceKills, ServiceLines;
        public float ServiceInfractions, ServicePatronFavour; public bool ServiceDeserter, ServiceInCamp;
        public CompanySnapshot[] Companies = new CompanySnapshot[0];

        // Version 8: sieges and castles
        public bool HasSiege; public string SiegeTargetId = "", SiegeTargetName = ""; public byte SiegeBesieger, SiegeOutcome; public bool SiegePlayerAttacking, SiegePlayerCommands, SiegeRamBuilt, SiegeTowerBuilt, SiegeGateBroken, SiegeInAssault;
        public int SiegeDay, SiegeAttackers, SiegeDefenders, SiegeCoinSpent, SiegeLadders, SiegeRamDays, SiegeTowerDays, SiegeAssaults, SiegeLordId, SiegeAttDead, SiegeDefDead;
        public float SiegeFood, SiegeDefSick, SiegeAttSick, SiegeDefMorale, SiegeRelief;
        public bool HasCastle; public string CastleName = "", CastleSiteId = ""; public float CastleNx, CastleNz; public byte CastleStage; public int CastleDays, CastleCoin, CastleObjector;
        public int Garrison;
    }

    public sealed class CompanySnapshot { public string Name = "", Captain = "", BasedAtId = ""; public byte Employer; public int Size, ContractDaysLeft; public float Reputation; }

    public sealed class SettlementSnapshot
    {
        public string Id = "", Name = ""; public byte Type; public float Nx, Nz; public int Population; public float Wealth; public byte Allegiance; public int LordId;
        public float Outbreak, WantedHere; public bool Coastal, Upland, River, IsFixed;
        public string[] GoodIds = new string[0]; public float[] GoodStocks = new float[0], GoodBaselines = new float[0], GoodDemands = new float[0]; public byte Shock; public float ShockStrength;
        public JobSnapshot[] Jobs = new JobSnapshot[0];
    }

    public sealed class CrimeSnapshot { public byte Kind; public string Where = ""; public long Day; public int Witnesses; public bool Known, Tried; }

    public sealed class JobSnapshot
    {
        public int Id; public byte Kind; public string FromId = "", ToId = "", Employer = "", Blurb = ""; public int Pay, Advance, DaysRequired, DaysServed; public long DeadlineDay; public float Danger; public byte State;
    }

    /// <summary>Flat arrays mirroring BattleDirector's structure-of-arrays.</summary>
    public sealed class BattleSnapshot
    {
        public int SoldierCount, UnitCount, PlayerSoldierEnd;
        public float Elapsed, EnemyThinkTimer;
        public bool Started, PlayerCharacterAlive;
        public bool SiegeGateOpen; public float RamSeconds;   // v8
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
        public byte[] UnitStance = new byte[0];    // version 6
        public float[] UnitMorale = new float[0];
    }
}

using System.Collections.Generic;
using AshesAndCrowns.Save;
using AshesAndCrowns.Sim;

namespace AshesAndCrowns.Game
{
    /// <summary>Process-wide state that survives scene loads: a save waiting to be applied, the next run's seed.</summary>
    public static class RunSession
    {
        /// <summary>A loaded save that belongs to a scene we are switching to. Consumed by that scene's SaveManager.</summary>
        public static RunState PendingResume;
        /// <summary>Set when "Begin Again" starts a fresh run; GameBootstrap consumes it.</summary>
        public static long NextSeed;
        /// <summary>True once this process has already resolved the on-launch resume, so scene reloads don't redo it.</summary>
        public static bool LaunchHandled;
        /// <summary>Set when the England scene sends the player to the battlefield; read by the battlefield scene.</summary>
        public static BattleLaunch PendingBattle;
        /// <summary>The launch the current battlefield was started with (kept after PendingBattle is cleared).</summary>
        public static BattleLaunch LastBattleLaunch;
        /// <summary>Set by the battlefield when the player returns; the England scene shows it once.</summary>
        public static List<string> ReturnChronicle;
        /// <summary>Clock and body carried across a scene load inside one process (England to battlefield and back).</summary>
        public static double CarriedTotalHours = -1;
        public static WeatherSnapshot CarriedWeather = default; public static ulong CarriedWeatherRng;
        public static BodySnapshot CarriedBody;
        /// <summary>The living run: character, family, baronage. Null until a character exists.</summary>
        public static RunContext Context;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { PendingResume = null; NextSeed = 0; LaunchHandled = false; Context = null; PendingBattle = null; ReturnChronicle = null; CarriedTotalHours = -1; CarriedWeather = default; CarriedBody = null; }
    }

    public sealed class BattleLaunch
    {
        public bool Siege;            // storm the walls (the siege scene)
        public bool PlayerAttacking = true;
        public bool AsGrunt;
        public Allegiance Side;
        public string CompanyName = string.Empty;
        public int CompanySize = 60;
    }

    public sealed class RunContext
    {
        public CharacterSheet Character;
        public List<Npc> Family = new List<Npc>();
        public List<MinorLord> Baronage = new List<MinorLord>();
        public FiguresData Figures;
        public string SpawnLocationId = string.Empty;
        public bool OpeningSeen;

        // Phase 4: survival and the village
        public Needs Needs;
        public DiseaseState Disease;
        public Inventory Inventory = new Inventory();
        public VillageStanding Standing;
        public Apprenticeship Apprenticeship = new Apprenticeship();
        public Market Market;               // the home village's market
        public float Outbreak;              // local plague level 0..1
        public long LastMassDay = -1;       // Julian day number of the last mass attended
        public ItemsData Items;
        public TradesData Trades;

        // Phase 5: England
        public List<Settlement> Settlements = new List<Settlement>();
        public GlobalReputation Reputation;
        public CrimeRecord Crime;
        public Job ActiveJob;
        public int NextJobId;
        public string HomeSettlementId = string.Empty;
        public string CurrentSettlementId = string.Empty;   // where the player stands, or empty
        public NamesData Names;
        public float MapSideKm = 673f;

        // Phase 8: service
        public ServiceRecord Service = new ServiceRecord();
        public List<MercenaryCompany> Companies = new List<MercenaryCompany>();
        public Settlement PendingLevyAt;
        // Phase 9: sieges and castles
        public SiegeState Siege;
        public CastleBuild Castle;
        public int Garrison;
        public MinorLord PendingObjector;
        public Settlement PendingNewSettlement;
        public List<string> PendingNotices = new List<string>(4);
        // Phase 10: lordship and crown
        public Estate Estate;
        public LordAllegiance Lordship = new LordAllegiance();
        public Suit Suit;
        public RealmState Realm;
        public int DaysIntoSeason;
        public Allegiance PendingDemand;
        public int NextNpcId = 100;
        public int EstateDaysThisMonth;
        // Phase 11: betrayal and war in the lanes
        public Plot Plot;
        public string PendingWhisper;
        public int PendingRaiders;
        public float WarHeat;   // 0..1 from the last realm season
        public MinorLord LordById(int id) { foreach (var l in Baronage) if (l.Id == id) return l; return null; }
        public MercenaryCompany CompanyAt(string settlementId) { foreach (var c in Companies) if (c.BasedAtId == settlementId && c.ContractDaysLeft > 0) return c; return null; }

        public Settlement FindSettlement(string id) { foreach (var s in Settlements) if (s.Id == id) return s; return null; }
        public Settlement Current => string.IsNullOrEmpty(CurrentSettlementId) ? null : FindSettlement(CurrentSettlementId);
        public Settlement Home => FindSettlement(HomeSettlementId);

        public void EnsureEngland(Balance b)
        {
            if (Reputation == null) Reputation = new GlobalReputation(b.reputation);
            if (Crime == null) Crime = new CrimeRecord(b.crime);
        }

        public void EnsureSurvival(Balance b, ItemsData items, TradesData trades)
        {
            Items = items; Trades = trades;
            if (Needs == null) Needs = new Needs(b.survival);
            if (Disease == null) Disease = new DiseaseState(b.disease);
            if (Standing == null) Standing = new VillageStanding(b.standing);
            if (Market == null)
            {
                Market = new Market(b.economy, items);
                foreach (var def in items.items)
                {
                    float baseline = def.category == "food" ? 40f : def.category == "raw" ? 20f : def.category == "good" ? 6f : def.category == "tool" ? 2f : def.id == "mail" ? 0f : 1.5f;
                    if (baseline > 0f) Market.Stock(def.id, baseline);
                }
            }
        }
    }
}

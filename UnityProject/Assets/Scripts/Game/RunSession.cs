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
        /// <summary>The living run: character, family, baronage. Null until a character exists.</summary>
        public static RunContext Context;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { PendingResume = null; NextSeed = 0; LaunchHandled = false; Context = null; }
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

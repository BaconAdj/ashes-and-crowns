using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Loads data, seeds the run, owns input. Everything else asks this for Balance and Rng.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [Tooltip("0 = use balance.simulation.defaultSeed. Any other value forces a seed for reproducing bugs.")]
        public long seedOverride;

        public Balance Balance { get; private set; }
        public WorldData World { get; private set; }
        public DeterministicRng Rng { get; private set; }
        public GameInput Input { get; private set; }
        public long Seed { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Balance = DataFiles.LoadBalance();
            World = DataFiles.LoadWorld();
            long seed = RunSession.NextSeed != 0 ? RunSession.NextSeed : (seedOverride != 0 ? seedOverride : Balance.simulation.defaultSeed);
            if (RunSession.NextSeed == 0 && seedOverride == 0)
            {
                // A run in progress keeps its seed, so forked rng streams line up with the save.
                try { var peek = new AshesAndCrowns.Save.SaveStore(SaveManager.SaveDirectory).Load(); if (peek.Found) seed = peek.State.Seed; }
                catch (System.Exception e) { Debug.LogWarning("[save] could not peek the save for the seed: " + e.Message); }
            }
            RunSession.NextSeed = 0;
            Seed = seed;
            Rng = new DeterministicRng((ulong)seed);
            Input = new GameInput();
            Combat.MeleeController.ContextProvider = () =>
            {
                var c = RunSession.Context;
                if (c == null || c.Inventory == null || c.Items == null || c.Character == null) return null;
                return (c.Inventory, c.Items, c.Character.Get(Skill.OneHanded));
            };
            Debug.Log($"[Ashes and Crowns] run seed {seed}");
        }

        private void OnDestroy()
        {
            Input?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}

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
            RunSession.NextSeed = 0;
            Seed = seed;
            Rng = new DeterministicRng((ulong)seed);
            Input = new GameInput();
            Debug.Log($"[Ashes and Crowns] run seed {seed}");
        }

        private void OnDestroy()
        {
            Input?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}

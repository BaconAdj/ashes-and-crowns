using AshesAndCrowns.Save;

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

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { PendingResume = null; NextSeed = 0; LaunchHandled = false; }
    }
}

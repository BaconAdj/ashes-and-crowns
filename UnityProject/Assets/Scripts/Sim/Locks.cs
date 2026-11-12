using System;

namespace AshesAndCrowns.Sim
{
    /// <summary>What keeps a door or a chest shut. Bar: a peasant's door barred from within at night, lifted with a blade
    /// through the gap. Warded: an iron lock with wards, the kind a hall, a church or a merchant can afford, opened with a
    /// bent iron. Strong: the same made by a better smith, on a strongroom or a gaol, and never left open.</summary>
    public enum LockKind : byte { None = 0, Bar = 1, Warded = 2, Strong = 3 }
    public enum LockEvent : byte { None = 0, Binding = 1, PickBroke = 2, Opened = 3 }

    [Serializable] public sealed class LockBalance
    {
        public float nightLockHour = 21f, dawnUnlockHour = 5.5f;      // householders bar the door at Compline and lift it before Prime
        public float toleranceBar = 0.12f, toleranceWarded = 0.07f, toleranceStrong = 0.04f;   // half-width of the true position, of a 0..1 sweep
        public float skillTolerance = 0.05f;                           // added at full Stealth
        public float falloff = 0.30f;                                  // beyond tolerance + this, the bolt does not move at all
        public float turnSeconds = 1.0f, relaxSeconds = 0.4f;          // a full throw of the bolt; how fast it springs back
        public float strainSeconds = 0.8f, strainRecoverSeconds = 2.5f; // forcing a bound lock this long snaps the iron
        public float heardRadiusMetres = 10f, heardPerSecond = 0.02f, heardOnBreak = 0.35f;   // someone awake nearby hears it
        public float sleeperWakesChance = 0.25f;                       // rifling a chest beside a sleeper, before Stealth
        public int chestPenniesMin = 2, chestPenniesMax = 14, strongChestPenniesMax = 90;
        public float doorOpenDegrees = 105f, doorSwingSeconds = 0.4f, doorReachMetres = 1.6f, doorCloseAfterSeconds = 2.5f, doorAwakeMetres = 30f;
    }

    public sealed class LockState
    {
        public LockKind Kind;
        public float Sweet, Tolerance;      // where the wards let the iron pass, and how forgiving it is
        public float Turn, Strain;          // 0..1 each
        public int PicksBroken;
        public bool Open;
    }

    /// <summary>
    /// Doors and the opening of them without a key. The picking model is the familiar one: find the position by feel, turn,
    /// and the further you are from true the sooner the bolt binds; force a bound bolt and the iron snaps. Nothing here knows
    /// about Unity: the game feeds it a pick position, whether the hand is turning, and time.
    /// </summary>
    public static class LockRules
    {
        public static bool IsNight(float hourOfDay, LockBalance b) => hourOfDay >= b.nightLockHour || hourOfDay < b.dawnUnlockHour;

        /// <summary>Is the way shut to this person at this hour? Your own door is never shut to you.</summary>
        public static bool IsLocked(LockKind kind, float hourOfDay, bool ownersKey, LockBalance b)
        {
            if (kind == LockKind.None || ownersKey) return false;
            if (kind == LockKind.Strong) return true;
            return IsNight(hourOfDay, b);
        }

        /// <summary>The item the job needs: a blade to lift a bar, a bent iron for wards.</summary>
        public static string Tool(LockKind kind) => kind == LockKind.Bar ? "knife" : "picklock";

        /// <summary>A blade lifting a bar does not snap the way a thin iron does; only picklocks are spent.</summary>
        public static bool ToolBreaks(LockKind kind) => kind != LockKind.Bar;

        public static float Tolerance(LockKind kind, float stealth01, LockBalance b)
        {
            float t = kind == LockKind.Bar ? b.toleranceBar : kind == LockKind.Warded ? b.toleranceWarded : b.toleranceStrong;
            return t + b.skillTolerance * Clamp01(stealth01);
        }

        public static LockState Begin(LockKind kind, float stealth01, DeterministicRng rng, LockBalance b)
        {
            return new LockState { Kind = kind, Sweet = rng.Range(0.06f, 0.94f), Tolerance = Tolerance(kind, stealth01, b) };
        }

        /// <summary>How far the bolt will go with the iron held here: all the way inside the tolerance, less and less outside it.</summary>
        public static float MaxTurn(LockState s, float pick01, LockBalance b)
        {
            float err = Math.Abs(pick01 - s.Sweet) - s.Tolerance;
            if (err <= 0f) return 1f;
            return Clamp01(1f - err / b.falloff) * 0.92f;   // never quite opens when off true
        }

        public static LockEvent Tick(LockState s, float pick01, bool turning, float dt, LockBalance b)
        {
            if (s.Open) return LockEvent.Opened;
            if (!turning)
            {
                s.Turn = Math.Max(0f, s.Turn - dt / b.relaxSeconds);
                s.Strain = Math.Max(0f, s.Strain - dt / b.strainRecoverSeconds);
                return LockEvent.None;
            }
            float max = MaxTurn(s, pick01, b);
            if (s.Turn < max)
            {
                s.Turn = Math.Min(max, s.Turn + dt / b.turnSeconds);
                if (s.Turn >= 1f) { s.Open = true; return LockEvent.Opened; }
                if (s.Turn < max) return LockEvent.None;
            }
            else if (s.Turn > max) s.Turn = Math.Max(max, s.Turn - dt / b.relaxSeconds);
            // Bound: the hand is still forcing it.
            s.Strain += dt / b.strainSeconds;
            if (s.Strain >= 1f)
            {
                s.Strain = 0f; s.Turn = 0f;
                if (ToolBreaks(s.Kind)) { s.PicksBroken++; return LockEvent.PickBroke; }
            }
            return LockEvent.Binding;
        }

        /// <summary>Chance that this moment's noise carried to somebody awake within earshot.</summary>
        public static float HeardChance(LockEvent e, float dt, float stealth01, LockBalance b)
        {
            float quiet = 1f - 0.6f * Clamp01(stealth01);
            if (e == LockEvent.PickBroke) return b.heardOnBreak * quiet;
            return b.heardPerSecond * dt * quiet;
        }

        /// <summary>What a chest behind such a door holds, in pennies.</summary>
        public static int ChestPennies(LockKind kind, DeterministicRng rng, LockBalance b)
            => rng.Range(b.chestPenniesMin, (kind == LockKind.Bar || kind == LockKind.None ? b.chestPenniesMax : b.strongChestPenniesMax) + 1);

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}

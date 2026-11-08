using System;

namespace AshesAndCrowns.Sim
{
    public enum UnitStance : byte { Steady = 0, Shaken = 1, Routing = 2, Surrendered = 3, Destroyed = 4 }

    [Serializable] public sealed class MoraleBalance
    {
        public float start = 0.8f;
        public float casualtyWeight = 1.2f;         // morale lost per fraction of the unit killed
        public float recentLossSpike = 0.35f;       // extra hit when losses come fast (fraction lost per 10 s)
        public float flankedPenaltyPerSecond = 0.012f;
        public float surroundedPenaltyPerSecond = 0.025f;
        public float fatiguePenaltyPerSecond = 0.002f;   // charging and fighting tire a unit
        public float rainPenalty = 0.03f, snowPenalty = 0.06f;
        public float commanderNearBonusPerSecond = 0.01f, commanderFightingBonusPerSecond = 0.006f, commanderRange = 60f;
        public float allyRoutingPenaltyPerSecond = 0.008f;  // per routing friendly unit within sight
        public float enemyRoutingBonusPerSecond = 0.005f;
        public float recoveryPerSecond = 0.01f;     // out of contact, morale creeps back
        public float shakenBelow = 0.45f, routBelow = 0.2f, rallyAbove = 0.5f;
        public float surrenderWhenSurroundedBelow = 0.3f;
        public float veteranBonus = 0.1f;
        public float runDownKillChancePerSecond = 0.25f; // a pursuer within reach of a router
    }

    /// <summary>Per-unit morale state. Plain struct so it can sit in a NativeArray beside UnitState.</summary>
    public struct UnitMorale
    {
        public float Value;          // 0..1
        public byte Stance;          // UnitStance
        public float Fatigue;        // 0..1
        public float RecentLoss;     // fraction lost in the last window, decays
        public int StartCount;
        public float RoutedForSeconds;

        public UnitStance StanceEnum => (UnitStance)Stance;
    }

    /// <summary>Inputs for one morale step. Everything the unit can perceive, nothing it cannot.</summary>
    public struct MoraleSituation
    {
        public int Alive;
        public int LostThisStep;
        public bool Flanked, Surrounded, InContact, Moving, Charging;
        public bool Raining, Snowing;
        public bool CommanderNear, CommanderFighting;
        public int AlliesRoutingNearby, EnemiesRoutingNearby;
        public bool Veteran;
    }

    /// <summary>Pure morale arithmetic; Burst-compatible (no managed references). The battle calls it per unit per step.</summary>
    public static class MoraleModel
    {
        public static UnitMorale Fresh(MoraleBalance b, int count, bool veteran) => new UnitMorale { Value = Clamp01(b.start + (veteran ? b.veteranBonus : 0f)), Stance = (byte)UnitStance.Steady, StartCount = count };

        /// <summary>Advance the unit by dt seconds. Returns the new stance (the caller reacts to changes).</summary>
        public static UnitStance Step(ref UnitMorale m, in MoraleSituation s, float dt, MoraleBalance b)
        {
            if (m.Stance == (byte)UnitStance.Destroyed || m.Stance == (byte)UnitStance.Surrendered) return m.StanceEnum;
            if (s.Alive <= 0) { m.Stance = (byte)UnitStance.Destroyed; return UnitStance.Destroyed; }
            int start = Math.Max(1, m.StartCount);
            // Losses: the fraction just lost hits now; a fast trickle hurts more than a slow one.
            float lostFrac = s.LostThisStep / (float)start;
            m.RecentLoss = m.RecentLoss * (float)Math.Exp(-dt / 10.0) + lostFrac;
            float delta = -lostFrac * b.casualtyWeight - m.RecentLoss * b.recentLossSpike * dt;
            if (s.Flanked) delta -= b.flankedPenaltyPerSecond * dt;
            if (s.Surrounded) delta -= b.surroundedPenaltyPerSecond * dt;
            if (s.Raining) delta -= b.rainPenalty * dt * 0.1f;
            if (s.Snowing) delta -= b.snowPenalty * dt * 0.1f;
            if (s.Charging) m.Fatigue = Clamp01(m.Fatigue + 0.02f * dt); else if (s.InContact) m.Fatigue = Clamp01(m.Fatigue + 0.01f * dt); else if (!s.Moving) m.Fatigue = Clamp01(m.Fatigue - 0.01f * dt);
            delta -= m.Fatigue * b.fatiguePenaltyPerSecond * dt;
            if (s.CommanderNear) delta += b.commanderNearBonusPerSecond * dt;
            if (s.CommanderFighting) delta += b.commanderFightingBonusPerSecond * dt;
            delta -= s.AlliesRoutingNearby * b.allyRoutingPenaltyPerSecond * dt;
            delta += s.EnemiesRoutingNearby * b.enemyRoutingBonusPerSecond * dt;
            if (!s.InContact && s.LostThisStep == 0) delta += b.recoveryPerSecond * dt;
            m.Value = Clamp01(m.Value + delta);

            var stance = (UnitStance)m.Stance;
            switch (stance)
            {
                case UnitStance.Steady:
                    if (m.Value < b.routBelow) stance = UnitStance.Routing; else if (m.Value < b.shakenBelow) stance = UnitStance.Shaken;
                    break;
                case UnitStance.Shaken:
                    if (m.Value < b.routBelow) stance = UnitStance.Routing; else if (m.Value >= b.rallyAbove) stance = UnitStance.Steady;
                    if (s.Surrounded && m.Value < b.surrenderWhenSurroundedBelow) stance = UnitStance.Surrendered;
                    break;
                case UnitStance.Routing:
                    m.RoutedForSeconds += dt;
                    if (s.Surrounded && s.InContact) stance = UnitStance.Surrendered;
                    else if (!s.InContact && m.Value >= b.rallyAbove) { stance = UnitStance.Shaken; m.RoutedForSeconds = 0f; }
                    break;
            }
            m.Stance = (byte)stance;
            return stance;
        }

        /// <summary>The battle ends when one side has nothing steady left: morale collapse, not annihilation.</summary>
        public static bool SideBroken(UnitMorale[] units, byte[] sides, byte side)
        {
            for (int i = 0; i < units.Length; i++)
                if (sides[i] == side && (units[i].Stance == (byte)UnitStance.Steady || units[i].Stance == (byte)UnitStance.Shaken)) return false;
            return true;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>Named men so the casualty list has names. A unit gets a sergeant and a few named veterans; the rest are counted.</summary>
    public sealed class UnitRoster
    {
        public string UnitName = string.Empty;
        public bool PlayerSide;
        public string Sergeant = string.Empty;
        public string[] Veterans = new string[0];
        public int NamedAlive;      // sergeant + veterans alive
        public bool SergeantAlive = true;

        public static UnitRoster Create(string unitName, NameGenerator names, DeterministicRng rng, int veterans, bool norman)
        {
            var r = new UnitRoster { UnitName = unitName, Sergeant = names.Given(false, norman) + (rng.Chance(0.5f) ? " " + names.Epithet() : string.Empty), Veterans = new string[veterans] };
            for (int i = 0; i < veterans; i++) r.Veterans[i] = names.Given(false, false) + (rng.Chance(0.3f) ? " " + names.Epithet() : string.Empty);
            r.NamedAlive = veterans + 1;
            return r;
        }

        /// <summary>Losses fall on the named men in proportion. Returns the names of the named dead this step.</summary>
        public int ApplyLosses(int lost, int aliveBefore, DeterministicRng rng, System.Collections.Generic.List<string> deadNames)
        {
            if (lost <= 0 || aliveBefore <= 0 || NamedAlive <= 0) return 0;
            float p = Math.Min(1f, lost / (float)aliveBefore);
            int named = 0;
            if (SergeantAlive && rng.Chance(p * 0.7f)) { SergeantAlive = false; NamedAlive--; deadNames?.Add(Sergeant + ", sergeant of the " + UnitName); named++; }
            for (int i = 0; i < Veterans.Length; i++)
            {
                if (Veterans[i] == null) continue;
                if (rng.Chance(p)) { deadNames?.Add(Veterans[i] + " of the " + UnitName); Veterans[i] = null; NamedAlive--; named++; }
            }
            return named;
        }
    }

    public sealed class BattleReport
    {
        public bool PlayerWon, Draw;
        public int PlayerStart, PlayerDead, PlayerRouted, PlayerCaptured;
        public int EnemyStart, EnemyDead, EnemyRouted, EnemyCaptured;
        public float Seconds;
        public readonly System.Collections.Generic.List<string> NamedDead = new System.Collections.Generic.List<string>();
        public string Weather = string.Empty;

        public string Summary()
        {
            string verdict = Draw ? "Neither side would break." : PlayerWon ? "The enemy broke and ran." : "Your men broke and ran.";
            return $"{verdict}\nYours: {PlayerStart} stood, {PlayerDead} dead, {PlayerRouted} fled, {PlayerCaptured} taken.\nTheirs: {EnemyStart} stood, {EnemyDead} dead, {EnemyRouted} fled, {EnemyCaptured} taken.";
        }
    }
}

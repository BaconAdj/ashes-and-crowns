using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    /// <summary>How the player left their affairs when they quit. Rank widens the options later.</summary>
    public enum AffairsChoice : byte
    {
        None = 0,          // closed the game without choosing
        SleepAtHome = 1,
        PostGuards = 2,
        LeaveSteward = 3,
        Fortify = 4,
        KeepTravelling = 5,
        Hide = 6,
        StandInBattle = 7, // quit mid-fight: resolved as if you stood still
        FleeBattle = 8,    // quit mid-fight while moving away: resolved as a flight
        AbandonLife = 9    // not an absence at all: the player ends this run on purpose (never saved)
    }

    public struct AbsenceInput
    {
        public double ElapsedRealSeconds;
        public AffairsChoice Choice;
        public float Hostility;      // 0 safe home ground .. 1 enemy country
        public int EnemiesNearby;    // soldiers within reach at the moment of quitting
        public bool InBattle;
    }

    public sealed class AbsenceReport
    {
        public float GameHoursElapsed;
        public bool Capped;
        public bool Died;
        public string CauseOfDeath;
        public float BloodBefore, BloodAfter;
        public int WoundsTaken;
        public int CoinsLost;
        public readonly List<string> Chronicle = new List<string>(8);
    }

    /// <summary>
    /// Resolves the time the game was closed, in the abstract. Deterministic for a given rng state.
    /// The body passed in is mutated (wounds, bleeding, rest), and the calendar is advanced.
    /// </summary>
    public static class AbsenceResolver
    {
        public static AbsenceReport Resolve(AbsenceInput input, Balance balance, BodyState body, Calendar calendar, DeterministicRng rng)
        {
            var report = new AbsenceReport { BloodBefore = body.Blood, BloodAfter = body.Blood };
            if (input.ElapsedRealSeconds <= 0 || double.IsNaN(input.ElapsedRealSeconds))
            {
                report.Chronicle.Add("You return at once; the world had no time to move.");
                return report;
            }
            float hours = (float)(input.ElapsedRealSeconds / 3600.0 * balance.simulation.realHoursToGameHours);
            float cap = balance.simulation.maxCatchUpGameDays * 24f;
            if (hours > cap) { hours = cap; report.Capped = true; }
            report.GameHoursElapsed = hours;

            AbsenceBalance ab = balance.absence;
            if (input.InBattle && !body.IsDead) ResolveBattle(input, ab, balance.wounds, body, rng, report);

            float remaining = hours;
            int dayIndex = 0;
            while (remaining > 0f && !body.IsDead)
            {
                float step = Math.Min(24f, remaining);
                remaining -= step;
                dayIndex++;
                float fraction = step / 24f;
                float danger = DangerPerDay(input.Choice, ab) * (1f + input.Hostility * ab.hostileCountryMultiplier) * fraction;
                calendar.AdvanceHours(step);
                if (IsResting(input.Choice)) body.Rest(step * ab.restHealMultiplier);
                else body.Tick(step * 3600f * 0.02f); // travelling: wounds keep bleeding a little, no rest

                if (rng.Chance(danger))
                {
                    float kind = rng.NextFloat();
                    if (kind < ab.attackShare) Attack(input, ab, balance.wounds, body, rng, report, calendar);
                    else if (kind < ab.attackShare + ab.feverShare) Fever(ab, body, report, calendar);
                    else Robbery(rng, report, calendar);
                }
                if (body.IsDead) { report.Died = true; report.CauseOfDeath = body.CauseOfDeath; break; }
            }
            if (report.Chronicle.Count == 0)
                report.Chronicle.Add(hours < 24f ? "Nothing troubled your rest." : $"{Math.Max(1, (int)(hours / 24f))} days passed without incident. {calendar.LongDate()}.");
            if (report.Capped) report.Chronicle.Add("The chronicle falls silent after that; the rest of your absence left no mark.");
            report.BloodAfter = body.Blood;
            if (report.Died) report.Chronicle.Add("You did not wake.");
            return report;
        }

        public static float DangerPerDay(AffairsChoice choice, AbsenceBalance ab)
        {
            switch (choice)
            {
                case AffairsChoice.SleepAtHome: return ab.dangerSleepAtHome;
                case AffairsChoice.PostGuards: return ab.dangerPostGuards;
                case AffairsChoice.LeaveSteward: return ab.dangerLeaveSteward;
                case AffairsChoice.Fortify: return ab.dangerFortify;
                case AffairsChoice.KeepTravelling: return ab.dangerKeepTravelling;
                case AffairsChoice.Hide: return ab.dangerHide;
                case AffairsChoice.StandInBattle:
                case AffairsChoice.FleeBattle: return ab.dangerKeepTravelling;
                default: return ab.dangerNone;
            }
        }

        public static bool IsResting(AffairsChoice c) => c == AffairsChoice.SleepAtHome || c == AffairsChoice.PostGuards || c == AffairsChoice.LeaveSteward || c == AffairsChoice.Fortify;

        private static void ResolveBattle(AbsenceInput input, AbsenceBalance ab, WoundBalance wb, BodyState body, DeterministicRng rng, AbsenceReport report)
        {
            int enemies = Math.Max(0, input.EnemiesNearby);
            if (enemies == 0) { report.Chronicle.Add("You left the field while the fighting was elsewhere."); return; }
            int hits = 0;
            if (input.Choice == AffairsChoice.FleeBattle)
            {
                for (int i = 0; i < enemies; i++) if (rng.Chance(ab.battleFleeHitChance)) hits++;
                report.Chronicle.Add(hits == 0 ? "You ran, and the blows fell behind you." : $"You ran; {hits} blow{(hits == 1 ? "" : "s")} found your back.");
            }
            else
            {
                hits = (int)Math.Round(enemies * ab.battleStandHitsPerEnemy);
                report.Chronicle.Add($"You stood still among {enemies} enemies while the fight went on without you.");
            }
            for (int i = 0; i < hits && !body.IsDead; i++)
            {
                BodyPart part = BodyState.RollHitLocation(rng, wb);
                body.ApplyHit(part, rng.Range(ab.woundSeverityMin, ab.woundSeverityMax), "a blow taken in the battle you abandoned");
                report.WoundsTaken++;
            }
            if (body.IsDead) { report.Died = true; report.CauseOfDeath = body.CauseOfDeath; report.Chronicle.Add("They found you among the dead."); }
        }

        private static void Attack(AbsenceInput input, AbsenceBalance ab, WoundBalance wb, BodyState body, DeterministicRng rng, AbsenceReport report, Calendar cal)
        {
            int blows = input.Hostility > 0.5f ? rng.Range(1, 4) : 1;
            for (int i = 0; i < blows && !body.IsDead; i++)
            {
                BodyPart part = BodyState.RollHitLocation(rng, wb);
                body.ApplyHit(part, rng.Range(ab.woundSeverityMin, ab.woundSeverityMax), input.Choice == AffairsChoice.Hide ? "found in hiding" : "set upon in the night");
                report.WoundsTaken++;
            }
            string where = input.Choice == AffairsChoice.SleepAtHome ? "Men broke in while you slept" : input.Choice == AffairsChoice.Hide ? "They found where you hid" : "You were set upon on the road";
            report.Chronicle.Add($"{cal.ShortDate()}: {where}. {blows} blow{(blows == 1 ? "" : "s")} landed before it ended.");
        }

        private static void Fever(AbsenceBalance ab, BodyState body, AbsenceReport report, Calendar cal)
        {
            body.ApplyHit(BodyPart.Torso, ab.feverBloodLoss, "a fever");
            report.Chronicle.Add($"{cal.ShortDate()}: A fever took you for a day and left you weaker.");
        }

        private static void Robbery(DeterministicRng rng, AbsenceReport report, Calendar cal)
        {
            int coins = rng.Range(1, 6);
            report.CoinsLost += coins;
            report.Chronicle.Add($"{cal.ShortDate()}: Someone went through your things. {coins} pennies gone.");
        }
    }
}

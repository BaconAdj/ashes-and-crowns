using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum JobKind : byte { CaravanGuard = 0, Message = 1, Escort = 2, GarrisonDuty = 3, Bounty = 4, Labour = 5 }
    public enum JobState : byte { Offered = 0, Taken = 1, Done = 2, Failed = 3 }

    public sealed class Job
    {
        public int Id;
        public JobKind Kind;
        public string FromId = string.Empty, ToId = string.Empty;   // settlement ids
        public string Employer = string.Empty;
        public int Pay;                 // pennies on completion
        public int Advance;             // pennies paid up front
        public long DeadlineDay;        // Julian day number
        public float Danger;            // 0..1
        public int DaysRequired;        // garrison / labour
        public int DaysServed;
        public JobState State;
        public string Blurb = string.Empty;
    }

    [Serializable] public sealed class JobBalance
    {
        public int maxOffersPerSettlement = 3;
        public float refreshChancePerDay = 0.35f;
        public int messagePayPerDay = 2, guardPayPerDay = 3, escortPayPerDay = 4, garrisonPayPerDay = 2, bountyPay = 40, labourPayPerDay = 1;
        public float deadlineSlack = 1.8f;      // deadline = travel days * slack + 2
        public float guardDangerBase = 0.25f, escortDangerBase = 0.35f, bountyDanger = 0.7f, messageDanger = 0.15f;
        public int minRankForEscort = 1;        // warriors and above
    }

    /// <summary>Notice boards and tavern work. Pure generation and bookkeeping; travel and fights happen in the world.</summary>
    public static class JobBoard
    {
        public static void Refresh(Settlement here, IList<Settlement> all, float mapSideKm, SettlementBalance sb, JobBalance jb, Calendar cal, NameGenerator names, DeterministicRng rng, ref int nextId)
        {
            here.Jobs.RemoveAll(j => j.State == JobState.Done || j.State == JobState.Failed || (j.State == JobState.Offered && j.DeadlineDay < Today(cal)));
            while (here.Jobs.Count < jb.maxOffersPerSettlement)
            {
                Job j = Generate(here, all, mapSideKm, sb, jb, cal, names, rng, ref nextId);
                if (j == null) break;
                here.Jobs.Add(j);
            }
        }

        public static long Today(Calendar cal) => Calendar.JulianDayNumber(cal.Year, cal.Month, cal.Day);

        public static Job Generate(Settlement here, IList<Settlement> all, float mapSideKm, SettlementBalance sb, JobBalance jb, Calendar cal, NameGenerator names, DeterministicRng rng, ref int nextId)
        {
            if (all == null || all.Count < 2) return null;
            Settlement to = null; int guard = 0;
            while ((to == null || to == here) && guard++ < 10) to = all[rng.Range(0, all.Count)];
            if (to == null || to == here) return null;
            float days = SettlementGenerator.TravelDays(here, to, mapSideKm, false, sb);
            int travel = Math.Max(1, (int)Math.Ceiling(days));
            long deadline = Today(cal) + (long)Math.Ceiling(travel * jb.deadlineSlack) + 2;
            string employer = names.Given(rng.Chance(0.3f), here.Type >= SettlementType.Town) + (rng.Chance(0.5f) ? " the " + new[] { "carter", "chandler", "wool-man", "reeve", "steward", "priest" }[rng.Range(0, 6)] : string.Empty);
            float roll = rng.NextFloat();
            var j = new Job { Id = ++nextId, FromId = here.Id, ToId = to.Id, Employer = employer, DeadlineDay = deadline, State = JobState.Offered };
            if (roll < 0.3f)
            {
                j.Kind = JobKind.Message; j.Pay = Math.Max(2, travel * jb.messagePayPerDay); j.Danger = jb.messageDanger + 0.1f * (to.Allegiance != here.Allegiance && to.Allegiance != Allegiance.Neutral ? 1f : 0f);
                j.Blurb = $"Carry a sealed letter to {to.Name}. Do not read it. {j.Pay} pennies on delivery.";
            }
            else if (roll < 0.6f)
            {
                j.Kind = JobKind.CaravanGuard; j.Pay = Math.Max(3, travel * jb.guardPayPerDay); j.Advance = 1; j.Danger = jb.guardDangerBase + 0.1f * Math.Min(3, travel) / 3f;
                j.Blurb = $"Walk beside {employer}'s cart to {to.Name} with a spear in your hand. {j.Pay} pennies at the gate, one now.";
            }
            else if (roll < 0.75f)
            {
                j.Kind = JobKind.Escort; j.Pay = Math.Max(4, travel * jb.escortPayPerDay); j.Danger = jb.escortDangerBase; j.Advance = 2;
                j.Blurb = $"A lady's household rides to {to.Name} and wants armed men. {j.Pay} pennies. Armed men only.";
            }
            else if (roll < 0.88f)
            {
                j.Kind = JobKind.GarrisonDuty; j.ToId = here.Id; j.DaysRequired = rng.Range(5, 15); j.Pay = j.DaysRequired * jb.garrisonPayPerDay; j.Danger = 0.1f; j.DeadlineDay = Today(cal) + j.DaysRequired + 3;
                j.Blurb = $"Stand a watch at {here.Name} for {j.DaysRequired} days. {j.Pay} pennies, bread and a place by the fire.";
            }
            else if (roll < 0.95f)
            {
                j.Kind = JobKind.Bounty; j.Pay = jb.bountyPay; j.Danger = jb.bountyDanger;
                j.Blurb = $"An outlaw of {to.Name} robs the road. {j.Pay} pennies for his head, {j.Pay / 2} for his hand.";
            }
            else
            {
                j.Kind = JobKind.Labour; j.ToId = here.Id; j.DaysRequired = rng.Range(2, 6); j.Pay = j.DaysRequired * jb.labourPayPerDay; j.Danger = 0.02f; j.DeadlineDay = Today(cal) + j.DaysRequired + 2;
                j.Blurb = $"Hands wanted for {j.DaysRequired} days: ditching, carting, carrying stone. {j.Pay} pennies and pottage.";
            }
            return j;
        }

        public static bool CanTake(Job j, CharacterSheet c, Inventory inv)
        {
            if (j.State != JobState.Offered) return false;
            if (j.Kind == JobKind.Escort && c.Rank < Rank.Warrior) return false;
            if ((j.Kind == JobKind.CaravanGuard || j.Kind == JobKind.Bounty) && !HasWeapon(inv)) return false;
            return true;
        }

        public static bool HasWeapon(Inventory inv) => inv.Count("spear") > 0 || inv.Count("sword") > 0 || inv.Count("axe") > 0 || inv.Count("bow") > 0;

        public static void Take(Job j, CharacterSheet c) { j.State = JobState.Taken; c.Coins += j.Advance; }

        /// <summary>Arrived at the destination (or served the days). Returns pay; marks done or failed by the deadline.</summary>
        public static int Complete(Job j, CharacterSheet c, long today, VillageStanding standing)
        {
            if (j.State != JobState.Taken) return 0;
            if (today > j.DeadlineDay) { j.State = JobState.Failed; standing?.DebtDefaulted(); return 0; }
            j.State = JobState.Done; c.Coins += j.Pay; standing?.DebtHonoured();
            return j.Pay;
        }

        /// <summary>Travelling a day on a dangerous job: chance of an ambush. Returns true when the road turns ugly.</summary>
        public static bool RoadDanger(Job j, float hostility, DeterministicRng rng) => rng.Chance(j.Danger * 0.3f * (1f + hostility));

        public static string KindName(JobKind k)
        {
            switch (k)
            {
                case JobKind.CaravanGuard: return "caravan guard";
                case JobKind.Message: return "message";
                case JobKind.Escort: return "escort";
                case JobKind.GarrisonDuty: return "garrison duty";
                case JobKind.Bounty: return "bounty";
                default: return "labour";
            }
        }
    }
}

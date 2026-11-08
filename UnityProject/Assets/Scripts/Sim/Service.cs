using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum ServiceRole : byte { None = 0, Levy = 1, Mercenary = 2, Household = 3, Commander = 4 }
    public enum OrderBanner : byte { Hold = 0, Advance = 1, Charge = 2, Retreat = 3 }
    public enum LevyChoice : byte { Comply = 0, PayScutage = 1, Flee = 2 }
    public enum CaptureOutcome : byte { Mercy = 0, Ransomed = 1, Sword = 2, Escaped = 3 }
    public enum ProvostVerdict : byte { None = 0, Flogging = 1, Hanging = 2, Praise = 3 }

    [Serializable] public sealed class ServiceBalance
    {
        public float levyDemandChancePerDay = 0.02f;   // in a village when the war is near
        public int scutagePennies = 24;
        public int levyWagePerDay = 1, mercenaryWagePerDay = 3, householdWagePerDay = 2;
        public float latePayChance = 0.3f, shortRationsChance = 0.25f;
        public float lootPerEnemyDead = 0.4f;          // pennies, divided by the company
        public float formationSlack = 4f;              // metres from your slot before it counts as breaking ranks
        public float infractionPerSecondOutOfRanks = 1f;
        public float floggingAbove = 20f, hangingAbove = 90f;   // infraction points
        public float deedKillWitnessRange = 25f;
        public int renownPerWitnessedKill = 3, renownHeldTheLine = 6, renownFirstContact = 4, renownForStandingWhileOthersRan = 8;
        public int knighthoodRenown = 40;
        public float patronDispositionNeeded = 0.5f;
        public float captureMercyChance = 0.4f;
        public int ransomCommoner = 20;
        public float escapeChance = 0.1f;
        public int companySizeMin = 40, companySizeMax = 120;
    }

    public sealed class MercenaryCompany
    {
        public string Name = string.Empty, Captain = string.Empty;
        public Allegiance Employer;
        public int Size;
        public float Reputation;   // 0..1 how well they pay and fight
        public string BasedAtId = string.Empty;
        public int ContractDaysLeft;
    }

    /// <summary>The player's enlistment and everything that follows from standing in a line.</summary>
    public sealed class ServiceRecord
    {
        public ServiceRole Role;
        public string CompanyName = string.Empty, CaptainName = string.Empty;
        public Allegiance Side;
        public int WagePerDay;
        public int DaysServed;
        public int WagesOwed;
        public int BattlesFought;
        public int Renown;
        public float Infractions;
        public bool Deserter;
        public bool InCamp;
        public int WitnessedKills, LinesHeld;
        public string PatronId = string.Empty;    // lord who might dub you
        public float PatronFavour;                // 0..1

        public bool Active => Role != ServiceRole.None && !Deserter;

        public void Enlist(ServiceRole role, string company, string captain, Allegiance side, int wage)
        {
            Role = role; CompanyName = company; CaptainName = captain; Side = side; WagePerDay = wage; Deserter = false; Infractions = 0f; InCamp = true;
        }

        public void Discharge() { Role = ServiceRole.None; InCamp = false; }

        public void Desert(CrimeRecord crime, string where, long day, GlobalReputation rep, DeterministicRng rng)
        {
            Deserter = true; InCamp = false;
            crime?.Commit(CrimeKind.Desertion, where, day, 1, rng, rep);
            Role = ServiceRole.None;
        }
    }

    public static class ServiceRules
    {
        public static MercenaryCompany GenerateCompany(string basedAtId, Allegiance employer, NameGenerator names, DeterministicRng rng, ServiceBalance b)
        {
            string[] styles = { "the Free Company of {0}", "{0}'s Flemings", "the {1} Spears", "the Men of {1}", "{0}'s Rout" };
            string captain = names.Given(false, true) + " " + (rng.Chance(0.5f) ? names.House() : names.Epithet());
            string place = names.PlaceName();
            return new MercenaryCompany
            {
                Captain = captain, Name = string.Format(styles[rng.Range(0, styles.Length)], captain.Split(' ')[0], place), Employer = employer,
                Size = rng.Range(b.companySizeMin, b.companySizeMax + 1), Reputation = rng.Range(0.25f, 0.9f), BasedAtId = basedAtId, ContractDaysLeft = rng.Range(20, 90)
            };
        }

        /// <summary>Armed men at the door with a quota. Comply, pay, or run.</summary>
        public static string ResolveLevy(LevyChoice choice, CharacterSheet c, ServiceRecord service, Allegiance side, string lordName, CrimeRecord crime, GlobalReputation rep, string where, long day, ServiceBalance b, DeterministicRng rng, out bool mustLeaveNow)
        {
            mustLeaveNow = false;
            switch (choice)
            {
                case LevyChoice.PayScutage:
                    if (c.Coins < b.scutagePennies) { Enlist(service, side, lordName, b); mustLeaveNow = true; return $"You cannot pay {b.scutagePennies} pennies. They laugh and take you anyway."; }
                    c.Coins -= b.scutagePennies; rep?.Record(Deed.PaidTithe);
                    return $"You pay {b.scutagePennies} pennies and they take the miller's boy instead. Nobody looks at you.";
                case LevyChoice.Flee:
                    service.Desert(crime, where, day, rep, rng);
                    mustLeaveNow = true;
                    return "You go out the back and over the fence. By evening your name is on the sergeant's roll with a mark against it.";
                default:
                    Enlist(service, side, lordName, b);
                    mustLeaveNow = true;
                    return $"You take the spear they give you. {lordName}'s levy marches at Prime.";
            }
        }

        private static bool Enlist(ServiceRecord service, Allegiance side, string lordName, ServiceBalance b) { service.Enlist(ServiceRole.Levy, lordName + "'s levy", "a sergeant with a split lip", side, b.levyWagePerDay); return true; }

        /// <summary>A camp day: wages (maybe), rations (maybe), fever risk handled by the disease model with InCamp.</summary>
        public static string CampDay(ServiceRecord s, CharacterSheet c, Needs needs, DeterministicRng rng, ServiceBalance b)
        {
            if (!s.Active) return string.Empty;
            s.DaysServed++;
            bool late = rng.Chance(b.latePayChance);
            if (late) s.WagesOwed += s.WagePerDay; else { c.Coins += s.WagePerDay + s.WagesOwed; s.WagesOwed = 0; }
            bool shortRations = rng.Chance(b.shortRationsChance);
            needs?.Eat(shortRations ? 0.15f : 0.4f);
            needs?.Sleep(6f);
            return (late ? "The paymaster is 'expecting silver from Winchester'. " : "Wages paid. ") + (shortRations ? "Bread for two shared by four." : "Pottage and bread.");
        }

        /// <summary>Which banner the sergeant is screaming, from the unit's order and stance.</summary>
        public static OrderBanner BannerFor(OrderType order, UnitStance stance)
        {
            if (stance == UnitStance.Routing) return OrderBanner.Retreat;
            switch (order) { case OrderType.Charge: return OrderBanner.Charge; case OrderType.Move: return OrderBanner.Advance; default: return OrderBanner.Hold; }
        }

        public static string Shout(OrderBanner banner, DeterministicRng rng)
        {
            switch (banner)
            {
                case OrderBanner.Charge: return rng.Chance(0.5f) ? "CHARGE! On them! ON THEM!" : "Now! Now, you dogs, NOW!";
                case OrderBanner.Advance: return rng.Chance(0.5f) ? "Forward! Keep the line, keep the LINE!" : "Advance! Shoulder to shoulder!";
                case OrderBanner.Retreat: return rng.Chance(0.5f) ? "Back! Back to the hill!" : "It's done, run, RUN!";
                default: return rng.Chance(0.5f) ? "Hold! HOLD, damn you!" : "Stand fast! Nobody moves!";
            }
        }

        /// <summary>Discipline accrues while you are out of your slot and your unit still stands. Returns infraction points added.</summary>
        public static float TickDiscipline(ServiceRecord s, float distanceFromSlot, UnitStance unitStance, bool inContact, float dt, ServiceBalance b)
        {
            if (!s.Active || s.Role == ServiceRole.Commander) return 0f;
            if (unitStance == UnitStance.Routing || unitStance == UnitStance.Surrendered || unitStance == UnitStance.Destroyed) return 0f;
            if (distanceFromSlot <= b.formationSlack) return 0f;
            float add = b.infractionPerSecondOutOfRanks * dt * (inContact ? 2f : 1f);
            s.Infractions += add;
            return add;
        }

        public static void WitnessedKill(ServiceRecord s, ServiceBalance b) { s.WitnessedKills++; s.Renown += b.renownPerWitnessedKill; }
        public static void HeldTheLine(ServiceRecord s, ServiceBalance b) { s.LinesHeld++; s.Renown += b.renownHeldTheLine; }
        public static void StoodWhileOthersRan(ServiceRecord s, ServiceBalance b) { s.Renown += b.renownForStandingWhileOthersRan; }

        /// <summary>After the battle the provost reads the sergeants' reports.</summary>
        public static ProvostVerdict Provost(ServiceRecord s, bool sideWon, DeterministicRng rng, ServiceBalance b, out string text)
        {
            if (!s.Active) { text = string.Empty; return ProvostVerdict.None; }
            float inf = s.Infractions; s.Infractions = 0f; s.BattlesFought++;
            if (inf >= b.hangingAbove && rng.Chance(0.6f)) { text = "You left the line under arms and the sergeant says so. The provost does not argue with sergeants. The rope."; return ProvostVerdict.Hanging; }
            if (inf >= b.floggingAbove) { text = "Breaking ranks. Twenty strokes at the cart's tail, and the company watches."; return ProvostVerdict.Flogging; }
            if (s.WitnessedKills > 0 || s.LinesHeld > 0) { text = "The sergeant names you to the captain. The captain remembers names."; return ProvostVerdict.Praise; }
            text = sideWon ? "You stood where you were told. Nobody says anything, which is praise." : "Nobody is looking for heroes tonight.";
            return ProvostVerdict.None;
        }

        /// <summary>Loot after a won battle, divided by the company.</summary>
        public static int LootShare(int enemyDead, int companySize, ServiceBalance b, DeterministicRng rng)
        {
            float pool = enemyDead * b.lootPerEnemyDead;
            float share = pool / Math.Max(10, companySize);
            int coins = (int)Math.Floor(share);
            if (rng.Chance(share - coins)) coins++;
            return coins;
        }

        /// <summary>Taken by the enemy. Nobles are worth ransom; you may not be.</summary>
        public static CaptureOutcome Capture(CharacterSheet c, GlobalReputation rep, Allegiance captor, DeterministicRng rng, ServiceBalance b, out string text)
        {
            if (rng.Chance(b.escapeChance)) { text = "In the confusion after, a rope badly tied. You walk away in the dark."; return CaptureOutcome.Escaped; }
            if (c.Rank >= Rank.Knight) { int ransom = c.Rank >= Rank.Lord ? 400 : 60; c.Coins = Math.Max(0, c.Coins - ransom); text = $"A knight is worth money alive. Your ransom is set at {ransom} pennies and you are released on your word."; return CaptureOutcome.Ransomed; }
            if (c.Coins >= b.ransomCommoner) { c.Coins -= b.ransomCommoner; text = $"They find {b.ransomCommoner} pennies on you and decide you are worth that much alive."; return CaptureOutcome.Ransomed; }
            float mercy = b.captureMercyChance + (rep != null ? Math.Max(0f, rep.Get(captor == Allegiance.Stephen ? Faction.Crown : Faction.Angevin)) * 0.3f : 0f);
            if (rng.Chance(mercy)) { text = "They strip you of everything worth having and let you go. It is cheaper than feeding you."; return CaptureOutcome.Mercy; }
            text = "A commoner with no ransom. They do it quickly, at least.";
            return CaptureOutcome.Sword;
        }

        /// <summary>Deeds, equipment and a patron: all three, or you stay a well-armed commoner.</summary>
        public static bool KnighthoodCheck(ServiceRecord s, Inventory inv, ServiceBalance b, out string missing)
        {
            var parts = new List<string>(3);
            if (s.Renown < b.knighthoodRenown) parts.Add($"deeds ({s.Renown}/{b.knighthoodRenown} renown)");
            if (inv.Count("warhorse") == 0 || inv.Count("mail") == 0) parts.Add("a warhorse and mail");
            if (s.PatronFavour < b.patronDispositionNeeded || string.IsNullOrEmpty(s.PatronId)) parts.Add("a lord willing to dub you");
            missing = string.Join(", ", parts);
            return parts.Count == 0;
        }
    }
}

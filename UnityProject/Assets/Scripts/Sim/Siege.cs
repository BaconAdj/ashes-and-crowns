using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum SiegeOutcome : byte { None = 0, Surrendered = 1, Stormed = 2, Relieved = 3, Abandoned = 4 }
    public enum SiegeTerms : byte { MarchOut = 0, Prisoners = 1, NoQuarter = 2 }
    public enum CastleFate : byte { Hold = 0, Garrison = 1, Sack = 2 }
    public enum CastleStage : byte { None = 0, Motte = 1, Palisade = 2, Tower = 3, Done = 4 }

    [Serializable] public sealed class SiegeBalance
    {
        // blockade
        public int garrisonMin = 25, garrisonMax = 300; public float garrisonPerHead = 0.3f;
        public int defenderFoodDaysMin = 20, defenderFoodDaysMax = 70;
        public float defenderSicknessPerDay = 0.012f, attackerSicknessPerDay = 0.008f, hungerSicknessMult = 3f, rainSicknessMult = 1.5f;
        public float sicknessDeathFraction = 0.03f;      // of the force, per day, times sickness
        public float moralePerHungryDay = 0.05f, moralePerSicknessPoint = 0.04f, moraleRecoverPerDay = 0.01f, moraleWhenEnginesRise = 0.01f;
        public float surrenderMoraleBelow = 0.2f; public int starveDaysToSurrender = 4;
        public float costPerManPerDay = 0.1f;             // pennies
        public float desertionPerUnpaidDay = 0.01f;
        public float sortieChancePerDay = 0.04f, sortieLossFraction = 0.03f;
        public float reliefChancePerDay = 0.006f;         // times (1 + relief strength)
        public int laddersPerDay = 1, maxLadders = 8, ramDays = 5, towerDays = 12;
        public float captainStormsAbove = 0.55f, captainOffersTermsBelow = 0.3f;
        public int ramCrew = 8; public float ramSecondsToBreak = 45f;
        // assault layout (battlefield metres)
        public float wallZ = 215f, wallHalfThickness = 2.5f, wallHeight = 5f, gateX = 200f, gateHalfWidth = 2.5f, ladderHalfWidth = 1.5f, ladderSpacing = 7f;
        public float ladderVulnerability = 2f;            // damage taken while on a ladder, and dealt divided by it
        public float attackerCentreZ = 130f, defenderCentreZ = 221f;
        // aftermath
        public float sackLootPerHead = 0.8f, sackKillFraction = 0.15f, sackWealthMult = 0.5f;
        public float commanderLootShare = 0.1f;
        public int renownForStorm = 10, renownForSurrender = 6;
        public int commandBaseMen = 150, commandMenPerKnight = 10;
        // adulterine castles
        public int motteCost = 120, palisadeCost = 200, towerCost = 400; public int motteDays = 15, palisadeDays = 20, towerDays2 = 30;
        public float objectionChancePerDay = 0.015f, objectionRangeNorm = 0.12f; public int objectorMenPerKnight = 8, objectorBaseMen = 40;
        public int garrisonHirePennies = 2;
    }

    public sealed class SiegeState
    {
        public string TargetId = string.Empty, TargetName = string.Empty;
        public Allegiance Besieger;
        public bool PlayerAttacking = true;   // the player's side is outside the walls
        public bool PlayerCommands;           // the player gives the orders (else a captain does)
        public int Day;
        public int Attackers, Defenders;
        public float DefenderFoodDays;
        public float DefenderSickness, AttackerSickness;
        public float DefenderMorale = 1f;
        public int CoinSpent;
        public int Ladders; public int RamDays, TowerDays; public bool RamBuilt, TowerBuilt, GateBroken;
        public SiegeOutcome Outcome;
        public int Assaults;
        public bool InAssault;                // the battlefield scene is resolving a storm
        public int LordId = -1;               // the defending lord (relief strength)
        public float ReliefStrength;          // 0.. how likely a relieving force is
        public int AttackerDeadInAssaults, DefenderDeadInAssaults;

        public bool Active => Outcome == SiegeOutcome.None;
        public bool Starving => DefenderFoodDays <= 0f;
    }

    public static class SiegeRules
    {
        public static int Garrison(Settlement s, SiegeBalance b) => Math.Max(b.garrisonMin, Math.Min(b.garrisonMax, (int)Math.Round(s.Population * b.garrisonPerHead)));

        public static SiegeState Begin(Settlement target, Allegiance besieger, int attackers, bool playerAttacking, bool playerCommands, float reliefStrength, DeterministicRng rng, SiegeBalance b)
        {
            return new SiegeState
            {
                TargetId = target.Id, TargetName = target.Name, Besieger = besieger, PlayerAttacking = playerAttacking, PlayerCommands = playerCommands,
                Attackers = Math.Max(1, attackers), Defenders = Garrison(target, b),
                DefenderFoodDays = rng.Range(b.defenderFoodDaysMin, b.defenderFoodDaysMax + 1), DefenderMorale = 0.75f + 0.25f * target.Wealth / 2f,
                LordId = target.LordId, ReliefStrength = reliefStrength
            };
        }

        /// <summary>One day of blockade. Writes what happened into log (one line per event).</summary>
        public static void BlockadeDay(SiegeState s, bool raining, bool attackersPaid, DeterministicRng rng, SiegeBalance b, List<string> log)
        {
            if (!s.Active) return;
            s.Day++;
            s.DefenderFoodDays -= 1f;
            bool hungry = s.Starving;
            float rain = raining ? b.rainSicknessMult : 1f;
            s.DefenderSickness = Clamp01(s.DefenderSickness + b.defenderSicknessPerDay * (hungry ? b.hungerSicknessMult : 1f) * rain);
            int defDead = (int)Math.Round(s.Defenders * s.DefenderSickness * b.sicknessDeathFraction);
            if (defDead > 0) { s.Defenders = Math.Max(0, s.Defenders - defDead); log?.Add($"Smoke from the walls at dawn: they are burning their dead ({defDead})."); }
            float morale = s.DefenderMorale;
            if (hungry) morale -= b.moralePerHungryDay;
            morale -= s.DefenderSickness * b.moralePerSicknessPoint;
            if (s.Ladders > 0 || s.RamBuilt || s.TowerBuilt) morale -= b.moraleWhenEnginesRise;
            if (!hungry && s.DefenderSickness < 0.1f) morale += b.moraleRecoverPerDay;
            s.DefenderMorale = Clamp01(morale);

            s.AttackerSickness = Clamp01(s.AttackerSickness + b.attackerSicknessPerDay * rain);
            int attDead = (int)Math.Round(s.Attackers * s.AttackerSickness * b.sicknessDeathFraction * 0.5f);
            if (attDead > 0) { s.Attackers = Math.Max(0, s.Attackers - attDead); log?.Add($"Camp fever took {attDead} of ours in the night."); }
            s.CoinSpent += (int)Math.Ceiling(s.Attackers * b.costPerManPerDay);
            if (!attackersPaid)
            {
                int gone = (int)Math.Round(s.Attackers * b.desertionPerUnpaidDay);
                if (gone > 0) { s.Attackers = Math.Max(0, s.Attackers - gone); log?.Add($"{gone} men walked off unpaid."); }
            }

            if (s.Defenders > 0 && rng.Chance(b.sortieChancePerDay * s.DefenderMorale))
            {
                int ours = Math.Max(1, (int)Math.Round(s.Attackers * b.sortieLossFraction)), theirs = (int)Math.Round(s.Defenders * b.sortieLossFraction * 0.5f);
                s.Attackers = Math.Max(0, s.Attackers - ours); s.Defenders = Math.Max(0, s.Defenders - theirs);
                string burned = s.Ladders > 0 ? " and burned a ladder" : string.Empty; if (s.Ladders > 0) s.Ladders--;
                log?.Add($"A sortie at the gate before light: {ours} of ours dead, {theirs} of theirs{burned}.");
            }

            if (s.Ladders < b.maxLadders) { s.Ladders = Math.Min(b.maxLadders, s.Ladders + b.laddersPerDay); }
            if (!s.RamBuilt) { s.RamDays++; if (s.RamDays >= b.ramDays) { s.RamBuilt = true; log?.Add("The ram is finished: an oak trunk under a wet-hide roof."); } }
            if (!s.TowerBuilt) { s.TowerDays++; if (s.TowerDays >= b.towerDays) { s.TowerBuilt = true; log?.Add("The siege tower stands, swaying, taller than the wall."); } }

            if (rng.Chance(b.reliefChancePerDay * (1f + s.ReliefStrength))) { s.Outcome = SiegeOutcome.Relieved; log?.Add("Horsemen on the ridge at noon, then banners behind them: a relieving force. The siege is broken."); return; }
            if (s.Defenders <= 0) { s.Outcome = SiegeOutcome.Surrendered; log?.Add("No one answers from the walls. The gate stands open."); return; }
            if (s.DefenderMorale <= b.surrenderMoraleBelow || s.DefenderFoodDays <= -b.starveDaysToSurrender)
            {
                s.Outcome = SiegeOutcome.Surrendered; log?.Add("A white cloth on a spear above the gate. They ask for terms."); return;
            }
            if (s.Attackers <= 0) { s.Outcome = SiegeOutcome.Abandoned; log?.Add("There is no army left outside the walls."); }
        }

        public static float TermsChance(SiegeState s, SiegeTerms terms)
        {
            switch (terms)
            {
                case SiegeTerms.MarchOut: return Clamp01((0.75f - s.DefenderMorale) * 2f + (s.Starving ? 0.3f : 0f));
                case SiegeTerms.Prisoners: return Clamp01((0.45f - s.DefenderMorale) * 2.5f + (s.Starving ? 0.2f : 0f));
                default: return 0f;
            }
        }

        public static bool OfferTerms(SiegeState s, SiegeTerms terms, DeterministicRng rng, SiegeBalance b)
        {
            if (!s.Active) return false;
            if (rng.Chance(TermsChance(s, terms))) { s.Outcome = SiegeOutcome.Surrendered; return true; }
            s.DefenderMorale = Clamp01(s.DefenderMorale + 0.05f);   // defiance
            return false;
        }

        public static int EffectiveLadders(SiegeState s) => s.Ladders + (s.TowerBuilt ? 2 : 0) + (s.GateBroken ? 3 : 0);
        public static bool CanStorm(SiegeState s) => s.Active && (EffectiveLadders(s) > 0 || s.RamBuilt);

        public static float StormOdds(SiegeState s, SiegeBalance b)
        {
            float ratio = s.Attackers / (float)Math.Max(1, s.Defenders);
            float odds = ratio * 0.12f + EffectiveLadders(s) * 0.05f + (s.RamBuilt ? 0.08f : 0f) + (1f - s.DefenderMorale) * 0.35f + s.DefenderSickness * 0.2f;
            return Clamp01(odds);
        }

        public static bool CaptainStorms(SiegeState s, SiegeBalance b) => CanStorm(s) && StormOdds(s, b) >= b.captainStormsAbove;
        public static bool CaptainOffersTerms(SiegeState s, SiegeBalance b) => s.Active && StormOdds(s, b) < b.captainOffersTermsBelow && s.DefenderMorale < 0.6f;
        public static bool CaptainAbandons(SiegeState s) => s.Active && s.Attackers < s.Defenders;

        /// <summary>After a real-time storm: attackersWon means the besiegers hold the walls.</summary>
        public static void ResolveStorm(SiegeState s, bool attackersWon, int attackerDead, int defenderDead)
        {
            s.InAssault = false; s.Assaults++;
            s.Attackers = Math.Max(0, s.Attackers - attackerDead); s.Defenders = Math.Max(0, s.Defenders - defenderDead);
            s.AttackerDeadInAssaults += attackerDead; s.DefenderDeadInAssaults += defenderDead;
            if (attackersWon || s.Defenders <= 0) { s.Outcome = SiegeOutcome.Stormed; return; }
            s.DefenderMorale = Clamp01(s.DefenderMorale + 0.15f);
            s.Ladders = Math.Max(0, s.Ladders - 2);
            if (s.Attackers <= 0) s.Outcome = SiegeOutcome.Abandoned;
        }

        public static void Abandon(SiegeState s) { if (s.Active) s.Outcome = SiegeOutcome.Abandoned; }

        /// <summary>What happens to a taken castle. Returns the loot in pennies (0 unless sacked).</summary>
        public static int ApplyFate(Settlement target, CastleFate fate, Allegiance newOwner, int newLordId, GlobalReputation rep, SiegeBalance b, DeterministicRng rng)
        {
            target.Allegiance = newOwner; target.LordId = newLordId;
            if (fate != CastleFate.Sack) { if (fate == CastleFate.Garrison) target.WantedHere = 0f; return 0; }
            int loot = (int)Math.Round(target.Population * target.Wealth * b.sackLootPerHead * rng.Range(0.7f, 1.3f));
            target.Population = Math.Max(1, (int)Math.Round(target.Population * (1f - b.sackKillFraction)));
            target.Wealth *= b.sackWealthMult;
            rep?.Record(Deed.Sacked);
            return loot;
        }

        public static string Describe(SiegeState s, SiegeBalance b)
        {
            string food = s.Starving ? "They are eating the horses." : s.DefenderFoodDays < 10 ? "Thin faces on the wall." : "They still have bread.";
            string engines = (s.Ladders > 0 ? $"{s.Ladders} ladders" : "no ladders") + (s.RamBuilt ? ", a ram" : "") + (s.TowerBuilt ? ", a tower" : "") + (s.GateBroken ? ", the gate is broken" : "");
            return $"Day {s.Day}. {s.Attackers} outside, perhaps {s.Defenders} within. {food} Ours sick: {(int)(s.AttackerSickness * 100)} in a hundred; theirs: {(int)(s.DefenderSickness * 100)}. Spent so far: {s.CoinSpent} pennies. Engines: {engines}. Odds of a storm: {(int)(StormOdds(s, b) * 100)} in a hundred.";
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>An unlicensed castle rising on seized land: motte, palisade, tower. Someone may object.</summary>
    public sealed class CastleBuild
    {
        public string Name = string.Empty, SiteSettlementId = string.Empty;
        public float Nx, Nz;
        public CastleStage Stage;
        public int DaysIntoStage, CoinSpent;
        public int ObjectorLordId = -1;
        public bool Active => Stage != CastleStage.None && Stage != CastleStage.Done;
    }

    public static class CastleRules
    {
        public const int PlayerLordId = -2;

        public static int StageCost(CastleStage st, SiegeBalance b) => st == CastleStage.Motte ? b.motteCost : st == CastleStage.Palisade ? b.palisadeCost : st == CastleStage.Tower ? b.towerCost : 0;
        public static int StageDays(CastleStage st, SiegeBalance b) => st == CastleStage.Motte ? b.motteDays : st == CastleStage.Palisade ? b.palisadeDays : st == CastleStage.Tower ? b.towerDays2 : 0;

        public static bool CanStart(CharacterSheet c, Settlement at, CastleBuild existing, SiegeBalance b, out string why)
        {
            why = string.Empty;
            if (existing != null && existing.Stage != CastleStage.None) { why = "You already have a castle rising."; return false; }
            if (at == null || at.Type != SettlementType.Village) { why = "Castles rise on village land, not in towns."; return false; }
            if (c.Rank < Rank.Warrior) { why = "A peasant with a spade is a peasant with a spade. You need a name men will follow."; return false; }
            if (c.Coins < b.motteCost) { why = $"A motte needs {b.motteCost} pennies of labour before the first ditch is dug."; return false; }
            return true;
        }

        public static CastleBuild Start(string name, Settlement at) => new CastleBuild { Name = name, SiteSettlementId = at.Id, Nx = at.Nx, Nz = at.Nz, Stage = CastleStage.Motte };

        /// <summary>One day of digging. Pays the day's share of the stage cost; stalls unpaid. May raise an objector.</summary>
        public static string BuildDay(CastleBuild cb, CharacterSheet c, Allegiance mySide, IList<MinorLord> lords, DeterministicRng rng, SiegeBalance b, out MinorLord objector)
        {
            objector = null;
            if (!cb.Active) return string.Empty;
            int days = Math.Max(1, StageDays(cb.Stage, b));
            int perDay = Math.Max(1, (int)Math.Ceiling(StageCost(cb.Stage, b) / (float)days));
            if (c.Coins < perDay) return "The labourers stand idle: no silver, no digging.";
            c.Coins -= perDay; cb.CoinSpent += perDay; cb.DaysIntoStage++;
            string text = string.Empty;
            if (cb.DaysIntoStage >= days)
            {
                cb.DaysIntoStage = 0;
                cb.Stage = cb.Stage == CastleStage.Motte ? CastleStage.Palisade : cb.Stage == CastleStage.Palisade ? CastleStage.Tower : CastleStage.Done;
                text = cb.Stage == CastleStage.Palisade ? "The motte is raised: a mound of wet earth with a ditch around it." : cb.Stage == CastleStage.Tower ? "The palisade is up. From the ridge it already looks like a castle." : $"The tower is roofed. {cb.Name} stands, and nobody licensed it.";
            }
            if (cb.Active || cb.Stage == CastleStage.Done)
            {
                MinorLord near = null; float best = float.MaxValue;
                if (lords != null) foreach (var l in lords)
                {
                    if (!l.Alive) continue;
                    float dx = l.Nx - cb.Nx, dz = l.Nz - cb.Nz, d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d < b.objectionRangeNorm && d < best && (l.Declared != mySide || l.Ambition > 0.6f)) { best = d; near = l; }
                }
                if (near != null && cb.Stage != CastleStage.Done && rng.Chance(b.objectionChancePerDay * (1f + near.Knights / 10f)))
                {
                    objector = near; cb.ObjectorLordId = near.Id;
                }
            }
            return text;
        }

        public static int ObjectorMen(MinorLord l, SiegeBalance b) => b.objectorBaseMen + l.Knights * b.objectorMenPerKnight;

        public static Settlement Complete(CastleBuild cb, Allegiance side, string region)
        {
            return new Settlement
            {
                Id = "castle_" + cb.SiteSettlementId, Name = cb.Name, Type = SettlementType.Castle, Nx = cb.Nx + 0.004f, Nz = cb.Nz + 0.004f,
                Population = 40, Wealth = 0.8f, Allegiance = side, LordId = PlayerLordId, Region = region ?? string.Empty, IsFixed = true
            };
        }
    }
}

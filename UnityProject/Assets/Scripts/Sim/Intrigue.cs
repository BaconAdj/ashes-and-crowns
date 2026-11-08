using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum PlotKind : byte { Poison = 0, Knife = 1, Ambush = 2, Usurp = 3, OpenTheGate = 4 }
    public enum PlotterKind : byte { Relative = 0, Rival = 1, Vassal = 2, Stranger = 3 }
    public enum PlotOutcome : byte { Nothing = 0, Dead = 1, Wounded = 2, Foiled = 3, Fled = 4, Lost = 5 }
    public enum Counter : byte { Nothing = 0, DoubleGuard = 1, Taster = 2, Confront = 3, SendAway = 4 }

    [Serializable] public sealed class IntrigueBalance
    {
        public float plotChancePerDayBase = 0.004f;          // scales with rank, notoriety, and hostile relatives
        public float plotChancePerRank = 0.004f, plotChancePerNotoriety = 0.01f, plotChancePerHostileRelative = 0.006f;
        public int plotDaysMin = 6, plotDaysMax = 20;
        public float warningChancePerDay = 0.35f, warningCunningWeight = 0.03f;
        public float poisonDeathChance = 0.45f, knifeDeathChance = 0.35f, ambushDeathChance = 0.3f;
        public float guardFoilBonus = 0.35f, tasterFoilsPoison = 0.9f, confrontCunningWeight = 0.05f;
        public int guardCostPennies = 20, tasterCostPennies = 15;
        public float relativeHostileBelow = -0.2f, vassalLoyaltyBelow = 0.3f;
        public float dispositionDropOnFalseAccusation = 0.4f;
    }

    public sealed class Plot
    {
        public PlotKind Kind; public PlotterKind Plotter; public int PlotterId = -1; public string PlotterName = string.Empty;
        public int DaysUntil; public int WarningsGiven; public bool Guarded, Tasted, Exposed;
        public readonly List<string> Warnings = new List<string>(4);
    }

    public static class IntrigueRules
    {
        /// <summary>Daily. A plot grows where the player has power, notoriety or an unhappy household.</summary>
        public static Plot Roll(CharacterSheet c, IList<Npc> family, IList<MinorLord> lords, Estate estate, float notoriety, DeterministicRng rng, IntrigueBalance b)
        {
            int hostile = 0; Npc worst = null;
            if (family != null) foreach (var n in family) if (n.Alive && n.Disposition < b.relativeHostileBelow) { hostile++; if (worst == null || n.Disposition < worst.Disposition) worst = n; }
            MinorLord vassal = null;
            if (estate != null && lords != null) foreach (var l in lords) if (l.Alive && l.Loyalty < b.vassalLoyaltyBelow && (vassal == null || l.Loyalty < vassal.Loyalty)) vassal = l;
            float p = b.plotChancePerDayBase + (int)c.Rank * b.plotChancePerRank + notoriety * b.plotChancePerNotoriety + hostile * b.plotChancePerHostileRelative;
            if (c.Rank < Rank.Warrior && hostile == 0) p *= 0.3f;
            if (!rng.Chance(p)) return null;
            var plot = new Plot { DaysUntil = rng.Range(b.plotDaysMin, b.plotDaysMax + 1) };
            float pick = rng.NextFloat();
            if (worst != null && pick < 0.45f)
            {
                plot.Plotter = PlotterKind.Relative; plot.PlotterId = worst.Id; plot.PlotterName = worst.Name;
                plot.Kind = (worst.Relation == Relation.Heir || worst.Relation == Relation.Child) && c.Rank >= Rank.Lord ? PlotKind.Usurp : rng.Chance(0.6f) ? PlotKind.Poison : PlotKind.Knife;
            }
            else if (vassal != null && estate != null && pick < 0.75f)
            {
                plot.Plotter = PlotterKind.Vassal; plot.PlotterId = vassal.Id; plot.PlotterName = vassal.FullName; plot.Kind = rng.Chance(0.5f) ? PlotKind.OpenTheGate : PlotKind.Ambush;
            }
            else if (lords != null && lords.Count > 0 && pick < 0.9f)
            {
                MinorLord rival = null; foreach (var l in lords) if (l.Alive && l.FeudWith >= 0 && (rival == null || l.Ambition > rival.Ambition)) rival = l;
                if (rival == null) rival = lords[rng.Range(0, lords.Count)];
                plot.Plotter = PlotterKind.Rival; plot.PlotterId = rival.Id; plot.PlotterName = rival.FullName; plot.Kind = rng.Chance(0.5f) ? PlotKind.Ambush : PlotKind.Knife;
            }
            else { plot.Plotter = PlotterKind.Stranger; plot.PlotterName = "someone"; plot.Kind = rng.Chance(0.5f) ? PlotKind.Knife : PlotKind.Poison; }
            return plot;
        }

        /// <summary>The subtle warnings an attentive player can read. Null when nothing shows today.</summary>
        public static string Warning(Plot p, CharacterSheet c, DeterministicRng rng, IntrigueBalance b)
        {
            float chance = b.warningChancePerDay + (c.Get(Attribute.Cunning) - 8) * b.warningCunningWeight;
            if (p.WarningsGiven >= 3 || !rng.Chance(Math.Max(0.05f, chance))) return null;
            string w;
            switch (p.Kind)
            {
                case PlotKind.Poison: w = p.WarningsGiven == 0 ? "The dog would not eat what fell from your table." : p.WarningsGiven == 1 ? (p.Plotter == PlotterKind.Relative ? $"{p.PlotterName} has stopped eating what you eat." : "The cook has a new boy nobody can name.") : "Your wine tastes of almonds, a little."; break;
                case PlotKind.Knife: w = p.WarningsGiven == 0 ? "A stranger asked the ostler which horse is yours." : p.WarningsGiven == 1 ? "There was a man under the stair who was not there when you looked again." : "A knife went missing from the kitchen. Nobody took it."; break;
                case PlotKind.Ambush: w = p.WarningsGiven == 0 ? "The carter says there are riders in the wood by the ford, waiting for nobody." : p.WarningsGiven == 1 ? $"{p.PlotterName}'s men were in the alehouse asking which road you take." : "The road is very quiet today."; break;
                case PlotKind.Usurp: w = p.WarningsGiven == 0 ? $"{p.PlotterName} has been talking to the garrison sergeant, often, about nothing." : p.WarningsGiven == 1 ? "The priest asks, carefully, whether your will is written." : $"{p.PlotterName} wore your colours to the market."; break;
                default: w = p.WarningsGiven == 0 ? $"{p.PlotterName} sent a gift to the other side's captain. Everybody knows; nobody says." : p.WarningsGiven == 1 ? "The gate-ward has new boots he could not afford." : "Someone has been counting the sentries."; break;
            }
            p.WarningsGiven++; p.Warnings.Add(w);
            return w;
        }

        /// <summary>The player acts on a suspicion. Returns the result text; may expose or end the plot.</summary>
        public static string Act(Plot p, Counter action, CharacterSheet c, IList<Npc> family, IList<MinorLord> lords, Npc accused, DeterministicRng rng, IntrigueBalance b, out bool ended)
        {
            ended = false;
            switch (action)
            {
                case Counter.DoubleGuard:
                    if (c.Coins < b.guardCostPennies) return "You cannot pay more men to stand at your door.";
                    c.Coins -= b.guardCostPennies; p.Guarded = true; return "Two more men at the door and one under the window. They know something is wrong; so does everyone else.";
                case Counter.Taster:
                    if (c.Coins < b.tasterCostPennies) return "Nobody will taste for you for nothing.";
                    c.Coins -= b.tasterCostPennies; p.Tasted = true; return "A boy eats first from every dish. He is pleased with the arrangement.";
                case Counter.Confront:
                    if (accused == null) return "Confront whom?";
                    if (p.Plotter == PlotterKind.Relative && accused.Id == p.PlotterId)
                    {
                        bool breaks = rng.Chance(0.5f + (c.Get(Attribute.Cunning) - 8) * b.confrontCunningWeight);
                        if (breaks) { p.Exposed = true; ended = true; accused.Disposition = -1f; accused.Note = "exposed as a plotter"; return $"{accused.Name} goes white, then red, then says things that cannot be unsaid. It was true. Everyone heard."; }
                        return $"{accused.Name} denies everything with perfect calm. You are not sure. Nor is anyone else.";
                    }
                    accused.Disposition = Math.Max(-1f, accused.Disposition - b.dispositionDropOnFalseAccusation);
                    return $"{accused.Name} stares at you. \"After everything.\" It was not {accused.Name}. The household saw you accuse your own.";
                case Counter.SendAway:
                    if (accused == null) return "Send whom away?";
                    accused.Alive = true; accused.Note = "sent away"; accused.Disposition = Math.Max(-1f, accused.Disposition - 0.3f);
                    if (p.Plotter == PlotterKind.Relative && accused.Id == p.PlotterId) { ended = true; return $"{accused.Name} goes to the nunnery, or the abbey, or a cousin in the north, with one cart. The house is quieter."; }
                    return $"{accused.Name} goes, unjustly, and says so at every hall between here and the coast.";
            }
            return string.Empty;
        }

        /// <summary>The day comes.</summary>
        public static PlotOutcome Resolve(Plot p, CharacterSheet c, int garrison, DeterministicRng rng, IntrigueBalance b, out string text)
        {
            float death = p.Kind == PlotKind.Poison ? b.poisonDeathChance : p.Kind == PlotKind.Knife ? b.knifeDeathChance : b.ambushDeathChance;
            float foil = (p.Guarded ? b.guardFoilBonus : 0f) + (p.Kind == PlotKind.Poison && p.Tasted ? b.tasterFoilsPoison : 0f) + Math.Min(0.3f, garrison / 100f) + (c.Get(Attribute.Cunning) - 8) * 0.02f;
            switch (p.Kind)
            {
                case PlotKind.Poison:
                    if (rng.Chance(foil)) { text = p.Tasted ? "The taster dies at your table, screaming. You do not finish your supper." : "The dog dies under the table. You do not finish your supper."; return PlotOutcome.Foiled; }
                    if (rng.Chance(death)) { text = "The wine tastes of almonds. By midnight you cannot feel your hands."; return PlotOutcome.Dead; }
                    text = "Three days of cramps and a priest at the door. You live, thinner, and trust no cup."; return PlotOutcome.Wounded;
                case PlotKind.Knife:
                    if (rng.Chance(foil)) { text = "A man comes over the sill with a knife and the guard breaks his arm on the floor. He will not say who paid him. He will, later."; return PlotOutcome.Foiled; }
                    if (rng.Chance(death)) { text = "You wake with a hand on your mouth and do not wake again."; return PlotOutcome.Dead; }
                    text = "The knife goes in under the ribs and you get a hand on his wrist. He runs. You bleed."; return PlotOutcome.Wounded;
                case PlotKind.Ambush:
                    if (rng.Chance(foil * 0.6f)) { text = "You take the other road. Riders wait at the ford until dark."; return PlotOutcome.Foiled; }
                    text = p.PlotterName + "'s men at the ford."; return PlotOutcome.Nothing;   // the game layer fights it out
                case PlotKind.Usurp:
                    if (rng.Chance(foil + 0.3f)) { text = $"{p.PlotterName} speaks to the garrison at dawn and the sergeant arrests him. It was closer than you will ever know."; return PlotOutcome.Foiled; }
                    text = $"You come down to the hall and find {p.PlotterName} in your chair, the garrison behind him, and the gate shut."; return PlotOutcome.Lost;
                default:
                    if (rng.Chance(foil + 0.2f)) { text = $"The gate-ward is found with a rope ladder and {p.PlotterName}'s silver. He hangs. {p.PlotterName} sends his regrets."; return PlotOutcome.Foiled; }
                    text = $"The gate was open at dawn and {p.PlotterName}'s men were inside it."; return PlotOutcome.Lost;
            }
        }
    }

    // ------------------------------------------------------------------ war comes to the village
    public enum VillageEventKind : byte { None = 0, Refugees = 1, Foragers = 2, Raid = 3, Billet = 4 }

    [Serializable] public sealed class VillageEventBalance
    {
        public float chancePerDayBase = 0.01f, chancePerDayWarWeight = 0.03f;
        public int raiderCountMin = 2, raiderCountMax = 5;
        public float raidPopulationLoss = 0.06f, raidWealthLoss = 0.2f, forageWealthLoss = 0.1f, refugeeShock = 0.7f;
    }

    public static class VillageEvents
    {
        /// <summary>warHeat 0..1: how much fighting the realm saw last season near here.</summary>
        public static VillageEventKind Roll(Settlement s, float warHeat, DeterministicRng rng, VillageEventBalance b)
        {
            if (s == null || !rng.Chance(b.chancePerDayBase + warHeat * b.chancePerDayWarWeight)) return VillageEventKind.None;
            float p = rng.NextFloat();
            return p < 0.35f ? VillageEventKind.Refugees : p < 0.6f ? VillageEventKind.Foragers : p < 0.85f ? VillageEventKind.Raid : VillageEventKind.Billet;
        }

        public static string Apply(VillageEventKind k, Settlement s, DeterministicRng rng, VillageEventBalance b, out int raiders)
        {
            raiders = 0;
            switch (k)
            {
                case VillageEventKind.Refugees:
                    s.Market?.ApplyShock(WarShock.Refugees, b.refugeeShock); s.Population += rng.Range(10, 40);
                    return "A column of carts from the west: a burned town's worth of people, with nothing to sell but their hands. Bread is dear by evening and labour is cheap.";
                case VillageEventKind.Foragers:
                    s.Market?.ApplyShock(WarShock.Foraged, 1f); s.Wealth *= 1f - b.forageWealthLoss;
                    return "An army's foragers with a writ and forty carts. They take the granary, the seed-corn and the priest's mule, and pay in promises.";
                case VillageEventKind.Raid:
                    raiders = rng.Range(b.raiderCountMin, b.raiderCountMax + 1);
                    s.Market?.ApplyShock(WarShock.Raided, 1f); s.Population = Math.Max(10, (int)(s.Population * (1f - b.raidPopulationLoss))); s.Wealth *= 1f - b.raidWealthLoss;
                    return "Smoke from the far end of the village and a woman running. Raiders: they burn what they cannot carry.";
                case VillageEventKind.Billet:
                    s.Market?.ApplyShock(WarShock.GarrisonBilleted, 0.6f);
                    return "Sixty soldiers billeted on the village for the week. They eat, they drink, they do not pay, and the girls stay indoors.";
            }
            return string.Empty;
        }
    }

    // ------------------------------------------------------------------ strings
    /// <summary>A flat English→other-language table. Code keeps English as the key; the French file supplies the value.</summary>
    [Serializable] public sealed class StringsData { public string language = "en"; public string[] keys = new string[0], values = new string[0]; }

    public static class L
    {
        private static readonly Dictionary<string, string> _table = new Dictionary<string, string>(256);
        public static string Language { get; private set; } = "en";

        public static void Load(StringsData data)
        {
            _table.Clear(); Language = data != null && !string.IsNullOrEmpty(data.language) ? data.language : "en";
            if (data == null) return;
            for (int i = 0; i < data.keys.Length && i < data.values.Length; i++) if (!string.IsNullOrEmpty(data.keys[i])) _table[data.keys[i]] = data.values[i];
        }

        public static void Clear() { _table.Clear(); Language = "en"; }
        public static int Count => _table.Count;
        public static string T(string english) => english != null && _table.TryGetValue(english, out string v) && !string.IsNullOrEmpty(v) ? v : english;
    }
}

using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum Trade : byte { Farmer = 0, Blacksmith, Miller, Hunter, Brewer, Carpenter, Shepherd, Fisherman }

    [Serializable] public sealed class TradeDef
    {
        public string id;              // "farmer"
        public string skill;           // Skill name
        public string tool;            // item id required for full output
        public string product;         // item id produced
        public string input;           // item id consumed per unit (may be empty)
        public float baseUnitsPerDay;  // at skill 50 with tool
        public float standing;         // social standing of the trade, 0..1
        public float dangerPerDay;     // injury chance per working day
        public float[] seasonByMonth = new float[12];
        public string momentKind;      // which hands-on moment: "rhythm", "stalk", "timing"
    }

    [Serializable] public sealed class TradesData
    {
        public TradeDef[] trades = new TradeDef[0];
        public TradeDef Find(string id) { foreach (var t in trades) if (t.id == id) return t; return null; }
    }

    public sealed class WorkDayResult
    {
        public int UnitsProduced;
        public string Product = string.Empty;
        public int InputsConsumed;
        public float SkillGained;
        public bool Injured;
        public string Note = string.Empty;
    }

    /// <summary>The abstract daily routine of a trade. The hands-on moment feeds in as a 0..1 quality.</summary>
    public static class TradeWork
    {
        public static bool TryParse(string id, out Trade trade)
        {
            foreach (Trade t in Enum.GetValues(typeof(Trade))) if (string.Equals(t.ToString(), id, StringComparison.OrdinalIgnoreCase)) { trade = t; return true; }
            trade = default; return false;
        }

        /// <param name="momentQuality">0..1 result of the hands-on moment; 0.5 if skipped.</param>
        public static WorkDayResult WorkDay(TradeDef def, CharacterSheet worker, Inventory inv, Needs needs, int month, float weatherPenalty, float momentQuality,
                                            CharacterBalance cb, BodyState body, DeterministicRng rng, float hours = 10f, bool apprentice = false)
        {
            var r = new WorkDayResult { Product = def.product ?? string.Empty };
            if (!Skills.TryParse(def.skill, out Skill skill)) skill = Skill.Farming;
            float level = worker.Get(skill);
            float skillMul = 0.4f + level / 50f * 0.6f;                   // skill 0: 0.4x, 50: 1x, 100: 1.6x
            bool hasTool = string.IsNullOrEmpty(def.tool) || inv.Count(def.tool) > 0;
            float toolMul = hasTool ? 1f : 0.35f;
            float season = def.seasonByMonth != null && def.seasonByMonth.Length == 12 ? def.seasonByMonth[Math.Max(0, Math.Min(11, month - 1))] : 1f;
            float moment = 0.7f + Math.Max(0f, Math.Min(1f, momentQuality)) * 0.6f;
            float rest = needs != null ? 0.6f + needs.Rest * 0.4f : 1f;
            float hunger = needs != null && needs.Hungry ? 0.7f : 1f;
            float units = def.baseUnitsPerDay * skillMul * toolMul * season * moment * rest * hunger * (1f - weatherPenalty) * (hours / 10f) * (apprentice ? 0.5f : 1f);
            int whole = (int)Math.Floor(units);
            if (rng.Chance(units - whole)) whole++;
            if (!string.IsNullOrEmpty(def.input))
            {
                int available = inv.Count(def.input);
                if (available < whole) { r.Note = "Short of " + def.input + "."; whole = available; }
                if (whole > 0) { inv.Remove(def.input, whole); r.InputsConsumed = whole; }
            }
            if (whole > 0 && !string.IsNullOrEmpty(def.product)) inv.Add(def.product, whole);
            r.UnitsProduced = whole;
            r.SkillGained = worker.Use(skill, cb, hours / 10f);
            needs?.Work(hours);
            if (body != null && rng.Chance(def.dangerPerDay * (hasTool ? 1f : 1.5f)))
            {
                r.Injured = true;
                BodyPart part = rng.Chance(0.6f) ? (rng.Chance(0.5f) ? BodyPart.LeftArm : BodyPart.RightArm) : (rng.Chance(0.5f) ? BodyPart.LeftLeg : BodyPart.RightLeg);
                body.ApplyHit(part, rng.Range(0.05f, 0.2f), "an accident at work");
            }
            if (!hasTool && r.Note.Length == 0) r.Note = "Without a " + def.tool + " the work goes slowly.";
            return r;
        }
    }

    /// <summary>Changing trades: convince a master, then seasons of reduced income and obedience.</summary>
    public sealed class Apprenticeship
    {
        public string Trade = string.Empty;
        public int MasterNpcId;
        public float SeasonsRemaining;   // 1 season = 91 days
        public float Competence;         // 0..1
        public bool Active => !string.IsNullOrEmpty(Trade) && SeasonsRemaining > 0f;

        public static bool TryConvince(CharacterSheet player, float masterDisposition, float standing, DeterministicRng rng, out string reply)
        {
            float chance = 0.25f + (player.Get(Attribute.Charisma) - 4) * 0.06f + masterDisposition * 0.25f + (standing - 0.5f) * 0.3f;
            if (player.Rank > Rank.Peasant) chance += 0.15f;
            chance = Math.Max(0.05f, Math.Min(0.9f, chance));
            if (rng.Chance(chance)) { reply = "Very well. You will do as I say, and you will be paid half until you are worth more."; return true; }
            reply = rng.Chance(0.5f) ? "I have hands enough. Come back when you have something to offer." : "You? No.";
            return false;
        }

        public void Begin(string trade, int masterId, float seasons) { Trade = trade; MasterNpcId = masterId; SeasonsRemaining = seasons; Competence = 0f; }

        /// <summary>Progress by working days. Returns true when the apprenticeship completes.</summary>
        public bool WorkedDay(float quality)
        {
            if (!Active) return false;
            SeasonsRemaining -= 1f / 91f;
            Competence = Math.Min(1f, Competence + 0.011f * (0.5f + quality));
            if (SeasonsRemaining <= 0f) { SeasonsRemaining = 0f; return true; }
            return false;
        }
    }
}

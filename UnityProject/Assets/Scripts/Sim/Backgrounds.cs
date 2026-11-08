using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    // Mirrors of backgrounds.json. Attribute and skill bonuses are flattened into parallel arrays
    // because JsonUtility (Unity) cannot read dictionaries; the Game layer converts.

    [Serializable] public sealed class BackgroundWound { public string part; public float severity; }

    [Serializable] public sealed class BackgroundDef
    {
        public string id, name, blurb, trade, fixedFigure;
        public int coins;
        public bool wanted;
        public string[] attributeKeys = new string[0]; public int[] attributeValues = new int[0];
        public string[] skillKeys = new string[0]; public float[] skillValues = new float[0];
        public string[] family = new string[0];
        public BackgroundWound[] wounds = new BackgroundWound[0];
    }

    [Serializable] public sealed class RankDef
    {
        public string id, name, blurb, spawn;
        public BackgroundDef[] backgrounds = new BackgroundDef[0];
    }

    [Serializable] public sealed class BackgroundsData
    {
        public int attributePoints = 10, attributeMin = 3, attributeMax = 9, attributeBase = 4;
        public RankDef[] ranks = new RankDef[0];

        public RankDef Rank(Rank r) => ranks != null && (int)r < ranks.Length ? ranks[(int)r] : null;

        public BackgroundDef Find(string id)
        {
            foreach (var r in ranks) foreach (var b in r.backgrounds) if (b.id == id) return b;
            return null;
        }
    }

    /// <summary>Turns a rank, a background and an attribute allocation into a CharacterSheet.</summary>
    public static class CharacterFactory
    {
        public sealed class Choice
        {
            public string Name = "Nobody";
            public bool Female;
            public Rank Rank;
            public string BackgroundId;
            public int[] Allocation = { 4, 4, 4, 4 }; // base points before bonuses
            public int AppearancePreset;
            public float Height = 1.72f, Build = 0.5f, SkinTone = 0.5f, HairTone = 0.4f;
        }

        /// <summary>Total attribute points a valid allocation must sum to: the base in each of four attributes plus the free points.</summary>
        public static int Budget(BackgroundsData data) => data.attributeBase * 4 + data.attributePoints;

        /// <summary>Returns null when valid; otherwise the reason.</summary>
        public static string Validate(Choice c, BackgroundsData data)
        {
            if (c == null) return "no choice";
            if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 32) return "name must be 1 to 32 characters";
            if (data.Find(c.BackgroundId) == null) return "unknown background " + c.BackgroundId;
            if (c.Allocation == null || c.Allocation.Length != 4) return "four attributes required";
            int total = 0;
            foreach (int v in c.Allocation)
            {
                if (v < data.attributeMin || v > data.attributeMax) return $"attributes must be {data.attributeMin} to {data.attributeMax}";
                total += v;
            }
            int expected = Budget(data);
            if (total != expected) return $"spend exactly {data.attributePoints} points over the base of {data.attributeBase}: total must be {expected}";
            return null;
        }

        public static CharacterSheet Create(Choice c, BackgroundsData data, BodyState body)
        {
            string problem = Validate(c, data);
            if (problem != null) throw new ArgumentException(problem);
            BackgroundDef bg = data.Find(c.BackgroundId);
            var sheet = new CharacterSheet
            {
                Name = c.Name.Trim(), Female = c.Female, Rank = c.Rank, BackgroundId = bg.id, Trade = bg.trade ?? string.Empty,
                FixedFigureId = bg.fixedFigure ?? string.Empty, Coins = bg.coins, Wanted = bg.wanted,
                AppearancePreset = c.AppearancePreset, Height = c.Height, Build = c.Build, SkinTone = c.SkinTone, HairTone = c.HairTone
            };
            for (int i = 0; i < 4; i++) sheet.Attributes[i] = c.Allocation[i];
            for (int i = 0; i < bg.attributeKeys.Length && i < bg.attributeValues.Length; i++)
                if (Enum.TryParse(bg.attributeKeys[i], true, out Attribute a)) sheet.Attributes[(int)a] += bg.attributeValues[i];
            for (int i = 0; i < bg.skillKeys.Length && i < bg.skillValues.Length; i++)
                if (Skills.TryParse(bg.skillKeys[i], out Skill s)) sheet.Set(s, bg.skillValues[i]);
            if (body != null && bg.wounds != null)
                foreach (var w in bg.wounds)
                    if (Enum.TryParse(w.part, true, out BodyPart part)) body.ApplyHit(part, w.severity, "an old wound");
            return sheet;
        }
    }
}

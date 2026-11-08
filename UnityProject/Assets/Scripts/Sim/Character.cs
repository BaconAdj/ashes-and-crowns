using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum Attribute : byte { Strength = 0, Endurance = 1, Charisma = 2, Cunning = 3 }
    public enum Rank : byte { Peasant = 0, Warrior = 1, Knight = 2, Lord = 3, Monarch = 4 }

    /// <summary>Skills grow with use; attributes gate how fast. Names match the keys in backgrounds.json.</summary>
    public enum Skill : byte
    {
        OneHanded, TwoHanded, Polearm, Bow, Riding, Medicine, Leadership, Haggling, Stealth, CourtIntrigue,
        Farming, Smithing, Milling, Hunting, Brewing, Carpentry, Herding, Fishing
    }

    public static class Skills
    {
        public static readonly Skill[] All = (Skill[])Enum.GetValues(typeof(Skill));
        public const int Count = 18;

        public static Attribute Governing(Skill s)
        {
            switch (s)
            {
                case Skill.OneHanded: case Skill.TwoHanded: case Skill.Polearm: case Skill.Smithing: return Attribute.Strength;
                case Skill.Riding: case Skill.Farming: case Skill.Herding: case Skill.Fishing: case Skill.Carpentry: case Skill.Milling: return Attribute.Endurance;
                case Skill.Leadership: case Skill.Haggling: case Skill.Brewing: return Attribute.Charisma;
                default: return Attribute.Cunning;
            }
        }

        public static bool TryParse(string key, out Skill skill)
        {
            foreach (Skill s in All) if (string.Equals(s.ToString(), key, StringComparison.OrdinalIgnoreCase)) { skill = s; return true; }
            skill = default; return false;
        }

        public static string DisplayName(Skill s)
        {
            switch (s)
            {
                case Skill.OneHanded: return "one-handed"; case Skill.TwoHanded: return "two-handed"; case Skill.CourtIntrigue: return "court intrigue";
                default: return s.ToString().ToLowerInvariant();
            }
        }
    }

    [Serializable] public sealed class CharacterBalance
    {
        public float skillGainBase = 0.6f;          // skill points per "use" at skill 0 with an average attribute
        public float skillGainAttributeWeight = 0.15f; // each attribute point above base scales the gain by this
        public float skillGainDiminish = 0.012f;    // gain falls off as skill rises
        public float skillCap = 100f;
        public int adultAge = 15, oldAge = 50, ancientAge = 70;
        public float agingAttributeLossPerYearAfterOld = 0.15f;
    }

    /// <summary>The player (and, later, important NPCs). Plain data plus the growth rule.</summary>
    public sealed class CharacterSheet
    {
        public string Name = "Nobody";
        public bool Female;
        public int AgeYears = 24;
        public Rank Rank = Rank.Peasant;
        public string BackgroundId = string.Empty;
        public string Trade = string.Empty;      // peasants
        public string FixedFigureId = string.Empty; // monarchs playing a historical figure
        public int Coins;
        public bool Wanted;
        public int AppearancePreset;             // 0..n, see UI
        public float Height = 1.72f, Build = 0.5f, SkinTone = 0.5f, HairTone = 0.4f;
        public readonly int[] Attributes = new int[4];
        public readonly float[] SkillLevels = new float[Skills.Count];
        public float AttributeErosion;           // accumulated loss from age and maiming

        public int Get(Attribute a) => Attributes[(int)a];
        public float Get(Skill s) => SkillLevels[(int)s];
        public void Set(Skill s, float v) => SkillLevels[(int)s] = Math.Max(0f, v);

        /// <summary>Use a skill once. Returns the gain. Attributes gate growth; high skills grow slowly.</summary>
        public float Use(Skill s, CharacterBalance b, float effort = 1f)
        {
            int i = (int)s;
            float level = SkillLevels[i];
            if (level >= b.skillCap) return 0f;
            int attr = Get(Skills.Governing(s));
            float attrFactor = 1f + (attr - 4) * b.skillGainAttributeWeight;
            if (attrFactor < 0.25f) attrFactor = 0.25f;
            float gain = b.skillGainBase * attrFactor * effort / (1f + level * b.skillGainDiminish);
            SkillLevels[i] = Math.Min(b.skillCap, level + gain);
            return gain;
        }

        /// <summary>Age one year: past old age, attributes erode a little each year.</summary>
        public void AgeOneYear(CharacterBalance b, DeterministicRng rng)
        {
            AgeYears++;
            if (AgeYears <= b.oldAge) return;
            AttributeErosion += b.agingAttributeLossPerYearAfterOld * (AgeYears > b.ancientAge ? 2f : 1f);
            while (AttributeErosion >= 1f)
            {
                AttributeErosion -= 1f;
                int a = rng.Range(0, 4);
                if (Attributes[a] > 1) Attributes[a]--;
            }
        }

        /// <summary>A maiming permanently costs an attribute point (arms: strength; legs: endurance; head: charisma or cunning).</summary>
        public void ApplyMaim(BodyPart part, DeterministicRng rng)
        {
            Attribute a;
            switch (part)
            {
                case BodyPart.LeftArm: case BodyPart.RightArm: a = Attribute.Strength; break;
                case BodyPart.LeftLeg: case BodyPart.RightLeg: a = Attribute.Endurance; break;
                case BodyPart.Head: a = rng.Chance(0.5f) ? Attribute.Charisma : Attribute.Cunning; break;
                default: a = Attribute.Endurance; break;
            }
            if (Attributes[(int)a] > 1) Attributes[(int)a]--;
        }
    }
}

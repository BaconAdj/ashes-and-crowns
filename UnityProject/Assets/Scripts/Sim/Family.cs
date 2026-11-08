using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum Relation : byte
    {
        Father, Mother, Spouse, Child, Heir, Sibling, ElderBrother, Sister, BrotherInLaw, HalfBrother, HalfSibling, Steward, Captain, Queen, Other
    }

    /// <summary>A person in the player's life. In-run only; no dynasty carries over.</summary>
    public sealed class Npc
    {
        public int Id;
        public string Name = string.Empty;
        public bool Female;
        public int Age;
        public Relation Relation;
        public bool Alive = true;
        public string TraitA = string.Empty, TraitB = string.Empty;
        public float Disposition;   // -1 hates you .. +1 devoted
        public string Note = string.Empty;
    }

    /// <summary>Builds the starting family from the background's template words ("father", "wife", "heir"...).</summary>
    public static class FamilyGenerator
    {
        public static List<Npc> Generate(string[] template, CharacterSheet player, NameGenerator names, DeterministicRng rng)
        {
            var list = new List<Npc>(8);
            if (template == null) return list;
            bool norman = player.Rank >= Rank.Knight;
            int id = 1;
            foreach (string word in template)
            {
                if (!TryRelation(word, out Relation rel)) continue;
                var npc = new Npc { Id = id++, Relation = rel };
                switch (rel)
                {
                    case Relation.Father: npc.Female = false; npc.Age = player.AgeYears + rng.Range(19, 32); break;
                    case Relation.Mother: npc.Female = true; npc.Age = player.AgeYears + rng.Range(16, 28); break;
                    case Relation.Spouse: npc.Female = !player.Female; npc.Age = Math.Max(15, player.AgeYears + rng.Range(-6, 5)); break;
                    case Relation.Queen: npc.Female = true; npc.Age = player.AgeYears - rng.Range(0, 8); break;
                    case Relation.Child: npc.Female = rng.Chance(0.5f); npc.Age = rng.Range(1, Math.Max(2, player.AgeYears - 16)); break;
                    case Relation.Heir: npc.Female = false; npc.Age = rng.Range(6, Math.Max(7, player.AgeYears - 16)); break;
                    case Relation.ElderBrother: npc.Female = false; npc.Age = player.AgeYears + rng.Range(1, 8); break;
                    case Relation.Sister: npc.Female = true; npc.Age = Math.Max(12, player.AgeYears + rng.Range(-8, 8)); break;
                    case Relation.Sibling: case Relation.HalfSibling: npc.Female = rng.Chance(0.5f); npc.Age = Math.Max(10, player.AgeYears + rng.Range(-9, 9)); break;
                    case Relation.HalfBrother: case Relation.BrotherInLaw: npc.Female = false; npc.Age = Math.Max(16, player.AgeYears + rng.Range(-8, 10)); break;
                    case Relation.Steward: npc.Female = false; npc.Age = rng.Range(35, 60); break;
                    case Relation.Captain: npc.Female = false; npc.Age = rng.Range(28, 50); break;
                    default: npc.Age = rng.Range(16, 60); break;
                }
                npc.Name = names.Given(npc.Female, norman || rel == Relation.Steward || rel == Relation.Queen);
                names.TwoTraits(out npc.TraitA, out npc.TraitB);
                npc.Disposition = rel == Relation.Steward || rel == Relation.Captain ? rng.Range(-0.1f, 0.5f) : rng.Range(0.1f, 0.9f);
                if (npc.TraitA == "treacherous" || npc.TraitB == "treacherous") npc.Disposition -= 0.5f;
                list.Add(npc);
            }
            return list;
        }

        public static bool TryRelation(string word, out Relation rel)
        {
            switch ((word ?? string.Empty).ToLowerInvariant())
            {
                case "father": rel = Relation.Father; return true;
                case "mother": rel = Relation.Mother; return true;
                case "wife": case "husband": case "spouse": rel = Relation.Spouse; return true;
                case "queen": rel = Relation.Queen; return true;
                case "child": rel = Relation.Child; return true;
                case "heir": rel = Relation.Heir; return true;
                case "sibling": rel = Relation.Sibling; return true;
                case "elderbrother": rel = Relation.ElderBrother; return true;
                case "sister": rel = Relation.Sister; return true;
                case "brotherinlaw": rel = Relation.BrotherInLaw; return true;
                case "halfbrother": rel = Relation.HalfBrother; return true;
                case "halfsibling": rel = Relation.HalfSibling; return true;
                case "steward": rel = Relation.Steward; return true;
                case "captain": rel = Relation.Captain; return true;
                default: rel = Relation.Other; return false;
            }
        }

        public static string Describe(Relation r, bool female)
        {
            switch (r)
            {
                case Relation.Spouse: return female ? "wife" : "husband";
                case Relation.Child: return female ? "daughter" : "son";
                case Relation.Sibling: case Relation.HalfSibling: return (r == Relation.HalfSibling ? "half-" : string.Empty) + (female ? "sister" : "brother");
                case Relation.ElderBrother: return "elder brother";
                case Relation.BrotherInLaw: return "brother-in-law";
                case Relation.HalfBrother: return "half-brother";
                default: return r.ToString().ToLowerInvariant();
            }
        }
    }
}

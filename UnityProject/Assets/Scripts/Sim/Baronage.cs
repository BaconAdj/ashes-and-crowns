using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum Allegiance : byte { Neutral = 0, Stephen = 1, Matilda = 2 }

    /// <summary>A procedural minor lord: regenerated every run beneath the fixed major figures.</summary>
    public sealed class MinorLord
    {
        public int Id;
        public string GivenName = string.Empty, House = string.Empty, Epithet = string.Empty;
        public int Age;
        public string TraitA = string.Empty, TraitB = string.Empty;
        public Allegiance Declared, Secret;   // Secret may differ: the double-dealers
        public float Loyalty;                 // to their declared side, 0..1
        public float Ambition;                // 0..1
        public int Knights;                   // household strength
        public string SeatName = string.Empty;
        public string NearLocationId = string.Empty;
        public float Nx, Nz;                  // seat position on the map
        public int FeudWith = -1;             // another lord's Id
        public bool Alive = true;

        public string FullName => (GivenName + " " + House + (string.IsNullOrEmpty(Epithet) ? string.Empty : " " + Epithet)).Trim();
        public bool DoubleDealer => Secret != Declared;
    }

    [Serializable] public sealed class BaronageBalance
    {
        public int minorLordCount = 14;
        public float westMatildaBias = 0.65f;   // west of the divide most declare for Matilda
        public float eastStephenBias = 0.70f;
        public float divideNx = 0.47f;
        public float neutralShare = 0.2f;
        public float doubleDealerChance = 0.22f;
        public float feudChance = 0.35f;
        public int knightsMin = 5, knightsMax = 40;
        public float seatScatterKm = 18f;
    }

    public static class BaronageGenerator
    {
        public struct Anchor { public string LocationId; public float Nx, Nz; }

        /// <summary>Deterministic from the rng. Anchors are the fixed settlements; seats scatter around them.</summary>
        public static List<MinorLord> Generate(BaronageBalance b, IList<Anchor> anchors, float mapSideKm, NameGenerator names, DeterministicRng rng)
        {
            var lords = new List<MinorLord>(b.minorLordCount);
            if (anchors == null || anchors.Count == 0) return lords;
            var usedNames = new HashSet<string>();
            float scatter = b.seatScatterKm / Math.Max(1f, mapSideKm);
            for (int i = 0; i < b.minorLordCount; i++)
            {
                Anchor a = anchors[rng.Range(0, anchors.Count)];
                var lord = new MinorLord { Id = i + 1, Age = rng.Range(22, 62), NearLocationId = a.LocationId };
                lord.Nx = Clamp01(a.Nx + rng.Range(-scatter, scatter)); lord.Nz = Clamp01(a.Nz + rng.Range(-scatter, scatter));
                int guard = 0;
                do { lord.GivenName = names.Given(false, true); lord.House = names.House(); }
                while (!usedNames.Add(lord.GivenName + " " + lord.House) && guard++ < 20);
                if (rng.Chance(0.35f)) lord.Epithet = names.Epithet();
                names.TwoTraits(out lord.TraitA, out lord.TraitB);
                lord.Ambition = rng.NextFloat();
                lord.Knights = rng.Range(b.knightsMin, b.knightsMax + 1);
                lord.SeatName = names.PlaceName();

                bool west = lord.Nx < b.divideNx;
                float roll = rng.NextFloat();
                if (roll < b.neutralShare) lord.Declared = Allegiance.Neutral;
                else lord.Declared = rng.Chance(west ? b.westMatildaBias : 1f - b.eastStephenBias) ? Allegiance.Matilda : Allegiance.Stephen;
                lord.Loyalty = rng.Range(0.3f, 0.95f);
                if (lord.TraitA == "loyal" || lord.TraitB == "loyal") lord.Loyalty = Math.Min(1f, lord.Loyalty + 0.2f);
                if (lord.TraitA == "treacherous" || lord.TraitB == "treacherous") lord.Loyalty *= 0.5f;
                lord.Secret = lord.Declared;
                if (lord.Declared != Allegiance.Neutral && rng.Chance(b.doubleDealerChance * (1.5f - lord.Loyalty)))
                    lord.Secret = lord.Declared == Allegiance.Stephen ? Allegiance.Matilda : Allegiance.Stephen;
                lords.Add(lord);
            }
            // Feuds: pair neighbours with some probability, symmetric.
            for (int i = 0; i < lords.Count; i++)
            {
                if (lords[i].FeudWith >= 0 || !rng.Chance(b.feudChance)) continue;
                int best = -1; float bestD = float.MaxValue;
                for (int j = 0; j < lords.Count; j++)
                {
                    if (j == i || lords[j].FeudWith >= 0) continue;
                    float dx = lords[i].Nx - lords[j].Nx, dz = lords[i].Nz - lords[j].Nz;
                    float d = dx * dx + dz * dz;
                    if (d < bestD) { bestD = d; best = j; }
                }
                if (best >= 0) { lords[i].FeudWith = lords[best].Id; lords[best].FeudWith = lords[i].Id; }
            }
            return lords;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}

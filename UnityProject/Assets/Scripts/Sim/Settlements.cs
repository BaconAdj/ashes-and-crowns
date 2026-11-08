using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum SettlementType : byte { Village = 0, Town = 1, City = 2, Castle = 3 }

    public sealed class Settlement
    {
        public string Id = string.Empty, Name = string.Empty;
        public SettlementType Type;
        public float Nx, Nz;
        public int Population;
        public float Wealth = 1f;
        public Allegiance Allegiance;
        public int LordId = -1;            // minor lord holding it, -1 for crown/major figure
        public float Outbreak;             // plague level
        public bool Coastal, Upland, River;
        public Market Market;              // created by the game layer (needs items)
        public List<Job> Jobs = new List<Job>(4);
        public float WantedHere;           // the player's notoriety with this settlement's law, 0..1
        public string Region = string.Empty;
        public bool IsFixed;               // from world.json (real place)
    }

    [Serializable] public sealed class SettlementBalance
    {
        public int proceduralVillages = 8;
        public float minSpacingKm = 25f;
        public float coastalKm = 12f;
        public int villagePopMin = 60, villagePopMax = 220;
        public float walkKmPerDay = 28f, rideKmPerDay = 48f;
        public float roadSpeedBonus = 1.25f;
        public float villageWealthMin = 0.6f, villageWealthMax = 1.3f;
    }

    /// <summary>Places the procedural villages on land and classifies every settlement for its market character.</summary>
    public static class SettlementGenerator
    {
        public delegate bool LandTest(float nx, float nz);
        public delegate float CoastDistanceKm(float nx, float nz);

        public static List<Settlement> Generate(SettlementBalance b, IList<Settlement> fixedOnes, LandTest isLand, CoastDistanceKm coastKm, float mapSideKm, IList<MinorLord> lords, NameGenerator names, DeterministicRng rng)
        {
            var all = new List<Settlement>(fixedOnes);
            var used = new HashSet<string>();
            foreach (var s in all) used.Add(s.Name);
            int attempts = 0;
            while (all.Count < fixedOnes.Count + b.proceduralVillages && attempts++ < 4000)
            {
                float nx = rng.Range(0.12f, 0.9f), nz = rng.Range(0.05f, 0.9f);
                if (!isLand(nx, nz)) continue;
                float coast = coastKm(nx, nz);
                if (coast < 4f) continue;
                bool tooClose = false;
                foreach (var s in all)
                {
                    float d = Dist(nx, nz, s.Nx, s.Nz) * mapSideKm;
                    if (d < b.minSpacingKm) { tooClose = true; break; }
                }
                if (tooClose) continue;
                string name; int guard = 0;
                do { name = names.PlaceName(); } while (!used.Add(name) && guard++ < 30);
                var v = new Settlement
                {
                    Id = "village_" + name.ToLowerInvariant(), Name = name, Type = SettlementType.Village, Nx = nx, Nz = nz,
                    Population = rng.Range(b.villagePopMin, b.villagePopMax + 1), Wealth = rng.Range(b.villageWealthMin, b.villageWealthMax),
                    Coastal = coast < b.coastalKm
                };
                all.Add(v);
            }
            foreach (var s in all)
            {
                s.Coastal = s.Coastal || coastKm(s.Nx, s.Nz) < b.coastalKm;
                // Nearest minor lord holds the villages; fixed places keep their world.json allegiance.
                if (!s.IsFixed && lords != null && lords.Count > 0)
                {
                    MinorLord best = null; float bestD = float.MaxValue;
                    foreach (var l in lords) { float d = Dist(s.Nx, s.Nz, l.Nx, l.Nz); if (d < bestD) { bestD = d; best = l; } }
                    if (best != null) { s.LordId = best.Id; s.Allegiance = best.Declared; }
                }
            }
            return all;
        }

        public static float Dist(float ax, float az, float bx, float bz) { float dx = ax - bx, dz = az - bz; return (float)Math.Sqrt(dx * dx + dz * dz); }

        public static float TravelDays(Settlement a, Settlement b, float mapSideKm, bool mounted, SettlementBalance bal, bool road = false)
        {
            float km = Dist(a.Nx, a.Nz, b.Nx, b.Nz) * mapSideKm;
            float perDay = (mounted ? bal.rideKmPerDay : bal.walkKmPerDay) * (road ? bal.roadSpeedBonus : 1f);
            return km / perDay;
        }

        /// <summary>Regional flavour: what a settlement has too much of and too little of.</summary>
        public static void SeedMarket(Settlement s, Market m, ItemsData items)
        {
            // standard = what any market carries; regional = local plenty (>1) or scarcity (<1).
            // Supply scales the baseline stock, demand runs inversely, so plentiful things are cheap here and dear elsewhere.
            foreach (var def in items.items)
            {
                float standard;
                switch (def.category)
                {
                    case "food": standard = 40f; break;
                    case "raw": standard = 20f; break;
                    case "good": standard = 6f; break;
                    case "tool": standard = 2f; break;
                    case "weapon": standard = 1.5f; break;
                    default: standard = 0.8f; break;
                }
                float regional = 1f;
                if (def.category == "good" || def.category == "tool" || def.category == "weapon") regional *= s.Type >= SettlementType.Town ? 1.8f : 0.6f;
                if (def.category == "armour") regional *= s.Type == SettlementType.City ? 2.5f : s.Type >= SettlementType.Town ? 1f : 0f;
                if (def.id == "fish") regional *= s.Coastal || s.River ? 2.5f : 0.3f;
                if (def.id == "wool" || def.id == "cheese") regional *= s.Upland ? 2.5f : 0.8f;
                if (def.id == "grain" || def.id == "flour") regional *= s.Type == SettlementType.Village && !s.Upland ? 1.8f : s.Type == SettlementType.City ? 0.6f : 1f;
                if (def.id == "timber") regional *= s.Type == SettlementType.Village ? 1.6f : 0.7f;
                if (def.id == "iron" || def.id == "tools") regional *= s.Type >= SettlementType.Town ? 1.5f : 0.6f;
                if (regional <= 0f) continue;
                m.Stock(def.id, standard * regional * s.Wealth, 1f / regional);
            }
            m.Wealth = s.Wealth;
        }

        /// <summary>Best thing to carry from a to b, by profit per unit, or null.</summary>
        public static string BestCargo(Settlement a, Settlement b, int month, out int profitPerUnit)
        {
            string best = null; profitPerUnit = 0;
            if (a.Market == null || b.Market == null) return null;
            foreach (var g in a.Market.Goods)
            {
                int buy = a.Market.BuyPrice(g.Id, month), sell = b.Market.SellPrice(g.Id, month);
                int profit = sell - buy;
                if (profit > profitPerUnit && g.Stock >= 1f) { profitPerUnit = profit; best = g.Id; }
            }
            return best;
        }
    }
}

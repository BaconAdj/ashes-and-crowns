using System.Collections.Generic;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Builds the run's England: fixed places from world.json plus procedural villages, each with a market and a notice board.</summary>
    public static class EnglandSetup
    {
        public static List<Settlement> Generate(Balance balance, WorldData world, CoastData coast, ItemsData items, IList<MinorLord> lords, NameGenerator names, DeterministicRng rng)
        {
            var fixedOnes = new List<Settlement>();
            foreach (Location l in world.locations)
            {
                var s = new Settlement
                {
                    Id = l.id, Name = l.name, Nx = l.nx, Nz = l.nz, Population = l.population, IsFixed = true,
                    Type = l.type == "city" ? SettlementType.City : l.type == "town" ? SettlementType.Town : l.type == "castle" ? SettlementType.Castle : SettlementType.Village,
                    Allegiance = l.allegiance == "stephen" ? Allegiance.Stephen : l.allegiance == "matilda" ? Allegiance.Matilda : Allegiance.Neutral,
                    Wealth = l.type == "city" ? 1.8f : l.type == "town" ? 1.3f : 1f, River = l.id == "london" || l.id == "oxford" || l.id == "wallingford" || l.id == "gloucester" || l.id == "ashby_village"
                };
                fixedOnes.Add(s);
            }
            float[] xs = new float[coast.mainland.Length], zs = new float[coast.mainland.Length];
            for (int i = 0; i < coast.mainland.Length; i++) { xs[i] = coast.mainland[i].x; zs[i] = coast.mainland[i].z; }
            bool Land(float x, float z) => MapGeometry.PointInRing(xs, zs, x, z);
            float CoastKm(float x, float z) => MapGeometry.DistanceToRing(xs, zs, x, z) * coast.mapSideKm;
            var all = SettlementGenerator.Generate(balance.settlements, fixedOnes, Land, CoastKm, coast.mapSideKm, lords, names, rng);
            foreach (var s in all)
            {
                foreach (var u in coast.uplands) if (SettlementGenerator.Dist(s.Nx, s.Nz, u.nx, u.nz) * coast.mapSideKm < u.radiusKm * 0.8f && u.heightM > 300f) s.Upland = true;
                s.Market = new Market(balance.economy, items);
                SettlementGenerator.SeedMarket(s, s.Market, items);
            }
            return all;
        }

        public static Settlement Nearest(IList<Settlement> all, float nx, float nz, float maxNormalized)
        {
            Settlement best = null; float bestD = maxNormalized;
            foreach (var s in all) { float d = SettlementGenerator.Dist(s.Nx, s.Nz, nx, nz); if (d < bestD) { bestD = d; best = s; } }
            return best;
        }
    }
}

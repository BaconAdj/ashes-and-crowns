using System;
using System.Collections.Generic;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class TownPlanTests
    {
        private static TownPlanData Make(ulong seed = 1135) => TownPlan.Wallingford(new DeterministicRng(seed).Fork(77));

        [Test]
        public void Wallingford_MatchesTheRecord()
        {
            var p = Make();
            Console.WriteLine($"houses {p.Houses}, townsfolk {p.Townsfolk}, population {p.Population}, households {p.Households.Count}, buildings {p.Buildings.Count}");
            Assert.AreEqual(43f, p.AreaHectares, 0.5f, "the burh encloses about 43 hectares");
            Assert.That(p.Houses, Is.InRange(TownPlan.TargetHousesMin, TownPlan.TargetHousesMax), "four to five hundred house plots");
            Assert.That(p.Population, Is.InRange(1800, 2500), "perhaps two thousand people");
            Assert.AreEqual(TownPlan.ParishChurchCount, p.ParishChurches - 1, "eleven parish churches and the castle chapel");
            Assert.AreEqual(4, p.Gates.Count);
            Assert.AreEqual(60f, p.MotteRadius * 2f); Assert.AreEqual(13f, p.MotteHeight);
            int french = 0, moneyers = 0; foreach (var h in p.Households) { if (h.French) french++; if (h.Calling == "moneyer") moneyers++; }
            Assert.AreEqual(TownPlan.FrenchmenHouses, french); Assert.AreEqual(TownPlan.MoneyerHouseholds, moneyers);
        }

        [Test]
        public void TheSameSeed_BuildsTheSameTown_AndOtherSeedsStayWithinTheRecord()
        {
            var a = Make(); var b = Make();
            Assert.AreEqual(a.Buildings.Count, b.Buildings.Count);
            for (int i = 0; i < a.Buildings.Count; i++) { Assert.AreEqual(a.Buildings[i].X, b.Buildings[i].X); Assert.AreEqual(a.Buildings[i].Kind, b.Buildings[i].Kind); }
            for (int i = 0; i < a.Households.Count; i++) Assert.AreEqual(a.Households[i].Calling, b.Households[i].Calling);
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var p = Make(seed);
                Assert.That(p.Houses, Is.InRange(TownPlan.TargetHousesMin, TownPlan.TargetHousesMax), "seed " + seed);
                Assert.That(p.Population, Is.InRange(1800, 2500), "seed " + seed);
            }
        }

        [Test]
        public void NoHouseStandsOnAStreet_InAChurchyard_TheMarket_TheCastle_OrAnotherHouse()
        {
            var p = Make();
            var dwellings = new List<TownBuilding>(); foreach (var b in p.Buildings) if (TownPlanData.IsDwelling(b.Kind)) dwellings.Add(b);
            foreach (var b in dwellings)
            {
                bool turned = Math.Abs(Math.Abs(b.FacingDeg % 180f) - 90f) < 45f;
                float hx = turned ? b.HalfDepth : b.HalfWidth, hz = turned ? b.HalfWidth : b.HalfDepth;
                if (!b.Suburb)
                {
                    Assert.Less(Math.Abs(b.X) + hx, p.HalfWidth, b.Street); Assert.Less(Math.Abs(b.Z) + hz, p.HalfDepth, b.Street);
                    foreach (var r in p.Reserved) Assert.IsFalse(r.Contains(b.X, b.Z), $"house {b.Id} on {b.Street} stands in {r.Name}");
                    foreach (var s in p.Streets) Assert.Greater(TownPlan.DistToSegment(b.X, b.Z, s.X0, s.Z0, s.X1, s.Z1), s.Width * 0.5f + Math.Min(hx, hz) - 0.01f, $"house {b.Id} blocks {s.Name}");
                }
                foreach (var o in p.Buildings)
                {
                    if (o == b) continue;
                    bool ot = Math.Abs(Math.Abs(o.FacingDeg % 180f) - 90f) < 45f;
                    float ox = ot ? o.HalfDepth : o.HalfWidth, oz = ot ? o.HalfWidth : o.HalfDepth;
                    bool overlap = Math.Abs(o.X - b.X) < hx + ox - 0.05f && Math.Abs(o.Z - b.Z) < hz + oz - 0.05f;
                    Assert.IsFalse(overlap, $"house {b.Id} on {b.Street} overlaps {o.Kind} {o.Id} {o.Name} {o.Street}");
                }
            }
        }

        [Test]
        public void EveryDoorFacesItsStreet()
        {
            var p = Make();
            foreach (var b in p.Buildings)
            {
                if (!TownPlanData.IsDwelling(b.Kind) || b.Suburb) continue;
                TownStreet s = p.Streets.Find(x => x.Name == b.Street); Assert.IsNotNull(s, b.Street);
                double rad = b.FacingDeg * Math.PI / 180.0; float fx = (float)Math.Sin(rad), fz = (float)Math.Cos(rad);
                float before = TownPlan.DistToSegment(b.X, b.Z, s.X0, s.Z0, s.X1, s.Z1), after = TownPlan.DistToSegment(b.X + fx * 2f, b.Z + fz * 2f, s.X0, s.Z0, s.X1, s.Z1);
                Assert.Less(after, before, $"house {b.Id} on {b.Street} turns its back on the street");
            }
        }

        [Test]
        public void Households_HaveACalling_AParish_AndSomebodyHome()
        {
            var p = Make();
            var callings = new Dictionary<string, int>();
            foreach (var h in p.Households)
            {
                Assert.IsNotEmpty(h.Calling); Assert.GreaterOrEqual(h.Adults, 1); Assert.That(h.Parish, Is.InRange(0, TownPlan.ParishChurchCount - 1));
                callings[h.Calling] = callings.TryGetValue(h.Calling, out int n) ? n + 1 : 1;
            }
            Assert.Greater(callings.Count, 25, "a town lives by many trades");
            Assert.Greater(callings["labourer"], callings["goldsmith"]);
            float mean = p.Townsfolk / (float)p.Households.Count;
            Assert.That(mean, Is.InRange(3.5f, 5.2f), "mean household");
        }
    }
}

using System.Collections.Generic;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Sim
{
    public class TownRoutesTests
    {
        private static TownRoutes Wallingford(out TownPlanData p)
        {
            p = TownPlan.Wallingford(new DeterministicRng(1135).Fork(77));
            var seg = new List<float>(); foreach (var s in p.Streets) { seg.Add(s.X0); seg.Add(s.Z0); seg.Add(s.X1); seg.Add(s.Z1); }
            return new TownRoutes(seg);
        }

        [Test]
        public void ACross_RoutesRoundTheCorner_NotAcrossTheBlock()
        {
            var r = new TownRoutes(new float[] { -100, 0, 100, 0, 0, -100, 0, 100 });
            var buf = new float[32];
            int n = r.Route(-80, 6, 5, 80, buf);
            Assert.AreEqual(3, n);
            Assert.AreEqual(-80f, buf[0], 0.01f); Assert.AreEqual(0f, buf[1], 0.01f);
            Assert.AreEqual(0f, buf[2], 0.01f); Assert.AreEqual(0f, buf[3], 0.01f);
            Assert.AreEqual(0f, buf[4], 0.01f); Assert.AreEqual(80f, buf[5], 0.01f);
        }

        [Test]
        public void SameStreet_IsTwoPoints()
        {
            var r = new TownRoutes(new float[] { -100, 0, 100, 0, 0, -100, 0, 100 });
            var buf = new float[32];
            Assert.AreEqual(2, r.Route(20, 3, 70, -4, buf));
            Assert.AreEqual(20f, buf[0], 0.01f); Assert.AreEqual(70f, buf[2], 0.01f);
        }

        [Test]
        public void EveryHouseInWallingford_CanWalkToItsParishChurch_AlongStreets()
        {
            var r = Wallingford(out TownPlanData p);
            var buf = new float[32];
            foreach (var h in p.Households)
            {
                TownBuilding home = p.Buildings[h.Building]; if (home.Suburb) continue;
                TownBuilding church = p.Buildings.Find(b => b.Parish == h.Parish && (b.Kind == TownBuildingKind.ParishChurch || b.Kind == TownBuildingKind.GreatChurch));
                int n = r.Route(home.X, home.Z, church.X, church.Z, buf);
                Assert.GreaterOrEqual(n, 2); Assert.LessOrEqual(n, 16);
                for (int i = 0; i < n; i++)
                {
                    float best = float.MaxValue;
                    foreach (var s in p.Streets) best = System.Math.Min(best, TownPlan.DistToSegment(buf[i * 2], buf[i * 2 + 1], s.X0, s.Z0, s.X1, s.Z1));
                    Assert.Less(best, 5f, "a waypoint off the streets");
                }
                // consecutive points share a street: the leg does not cut a corner through houses
                for (int i = 0; i + 1 < n; i++)
                {
                    float mx = (buf[i * 2] + buf[i * 2 + 2]) * 0.5f, mz = (buf[i * 2 + 1] + buf[i * 2 + 3]) * 0.5f, best = float.MaxValue;
                    foreach (var s in p.Streets) best = System.Math.Min(best, TownPlan.DistToSegment(mx, mz, s.X0, s.Z0, s.X1, s.Z1));
                    Assert.Less(best, 5f, "a leg that leaves the street");
                }
            }
        }

        [Test]
        public void FromTheSouthEastToTheNorthGate_GoesByTheCrossroads()
        {
            var r = Wallingford(out TownPlanData p);
            var buf = new float[32];
            int n = r.Route(170f, -300f, 0f, p.HalfDepth - 5f, buf);
            Assert.Greater(n, 2);
            bool viaHighStreet = false; for (int i = 0; i < n; i++) if (System.Math.Abs(buf[i * 2 + 1]) < 6f) viaHighStreet = true;
            Assert.IsTrue(viaHighStreet);
        }
    }
}

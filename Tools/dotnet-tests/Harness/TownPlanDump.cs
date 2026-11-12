using System;
using System.Globalization;
using System.IO;
using System.Text;
using AshesAndCrowns.Sim;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Harness
{
    /// <summary>Not a test of anything: with TOWN_DUMP set to a file path, writes the Wallingford plan as CSV so
    /// Tools/town_map.py can draw docs/wallingford-plan.png. Does nothing otherwise.</summary>
    public class TownPlanDump
    {
        [Test]
        public void DumpPlan()
        {
            string path = Environment.GetEnvironmentVariable("TOWN_DUMP");
            if (string.IsNullOrEmpty(path)) { Assert.Pass("TOWN_DUMP not set"); return; }
            var p = TownPlan.Wallingford(new DeterministicRng(1135).Fork(77));
            var sb = new StringBuilder(); var ic = CultureInfo.InvariantCulture;
            sb.AppendLine(string.Format(ic, "plan,{0},{1},{2},{3},{4},{5},{6},{7},{8}", p.HalfWidth, p.HalfDepth, p.RiverX0, p.RiverX1, p.MotteX, p.MotteZ, p.MotteRadius, p.BridgeX0, p.BridgeX1));
            foreach (var r in new[] { p.Bailey, p.Market, p.Priory, p.Kinecroft, p.Bullcroft }) sb.AppendLine(string.Format(ic, "rect,{0},{1},{2},{3},{4}", r.Name, r.X0, r.Z0, r.X1, r.Z1));
            foreach (var s in p.Streets) sb.AppendLine(string.Format(ic, "street,{0},{1},{2},{3},{4},{5}", s.Name, s.X0, s.Z0, s.X1, s.Z1, s.Width));
            foreach (var b in p.Buildings)
            {
                string calling = b.Household >= 0 ? p.Households[b.Household].Calling : string.Empty;
                sb.AppendLine(string.Format(ic, "building,{0},{1},{2},{3},{4},{5},{6},{7}", b.Kind, b.X, b.Z, b.FacingDeg, b.HalfWidth, b.HalfDepth, b.Name, calling));
            }
            sb.AppendLine(string.Format(ic, "totals,{0},{1},{2},{3}", p.Houses, p.Townsfolk, p.Population, p.ParishChurches));
            File.WriteAllText(path, sb.ToString());
        }
    }
}

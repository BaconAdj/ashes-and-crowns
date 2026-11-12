using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum TownBuildingKind : byte { House = 0, Cot = 1, GreatHouse = 2, Alehouse = 3, ParishChurch = 4, GreatChurch = 5, PrioryRange = 6, CastleHall = 7, Keep = 8, Gatehouse = 9, Stable = 10 }
    public enum Workplace : byte { Shop = 0, Yard = 1, Market = 2, Wharf = 3, Fields = 4, Castle = 5, Church = 6, Home = 7 }
    public enum Wealth : byte { Poor = 0, Middling = 1, Rich = 2 }

    public sealed class TownStreet { public string Name = string.Empty; public float X0, Z0, X1, Z1, Width; public float Fill = 1f; }
    public sealed class TownRect { public string Name = string.Empty; public float X0, Z0, X1, Z1; public bool Contains(float x, float z, float margin = 0f) => x >= X0 - margin && x <= X1 + margin && z >= Z0 - margin && z <= Z1 + margin; public float Cx => (X0 + X1) * 0.5f; public float Cz => (Z0 + Z1) * 0.5f; }
    public sealed class TownGate { public string Name = string.Empty; public float X, Z, FacingDeg; }

    public sealed class TownBuilding
    {
        public int Id; public TownBuildingKind Kind; public float X, Z, FacingDeg; public float HalfWidth, HalfDepth;
        public string Name = string.Empty, Street = string.Empty; public int Household = -1, Parish = -1; public bool Suburb;
    }

    public sealed class TownHousehold
    {
        public int Id, Building; public string Calling = string.Empty, GameTrade = string.Empty; public Workplace Works; public Wealth Wealth;
        public int Adults, Children, Servants; public bool French; public int Parish;
        public int Souls => Adults + Children + Servants;
    }

    public sealed class TownPlanData
    {
        public string Id = string.Empty, Name = string.Empty;
        public float HalfWidth, HalfDepth;                 // inside the ramparts; the east side is the river bank
        public float RiverX0, RiverX1;                     // the Thames, running north to south past the east side
        public float BankWidth, BankHeight, DitchWidth, DitchDepth;
        public float MotteX, MotteZ, MotteRadius, MotteHeight, MotteTopRadius;
        public TownRect Bailey, Market, Priory, Kinecroft, Bullcroft;
        public float BridgeX0, BridgeX1, BridgeZ, BridgeWidth;
        public readonly List<TownStreet> Streets = new List<TownStreet>(24);
        public readonly List<TownGate> Gates = new List<TownGate>(6);
        public readonly List<TownBuilding> Buildings = new List<TownBuilding>(640);
        public readonly List<TownHousehold> Households = new List<TownHousehold>(560);
        public readonly List<TownRect> Reserved = new List<TownRect>(24);
        public int Garrison, Monks;

        public float AreaHectares => (2f * HalfWidth) * (2f * HalfDepth) / 10000f;
        public int Houses { get { int n = 0; foreach (var b in Buildings) if (IsDwelling(b.Kind)) n++; return n; } }
        public int ParishChurches { get { int n = 0; foreach (var b in Buildings) if (b.Kind == TownBuildingKind.ParishChurch || b.Kind == TownBuildingKind.GreatChurch) n++; return n; } }
        public int Townsfolk { get { int n = 0; foreach (var h in Households) n += h.Souls; return n; } }
        public int Population => Townsfolk + Garrison + Monks;
        public static bool IsDwelling(TownBuildingKind k) => k == TownBuildingKind.House || k == TownBuildingKind.Cot || k == TownBuildingKind.GreatHouse || k == TownBuildingKind.Alehouse;
    }

    /// <summary>
    /// Wallingford as it stood when Stephen took the crown, laid out from what is known and filled in where it is not.
    /// Known (see docs/sources.md): a late Saxon burh of about 43 hectares on the west bank of the Thames, earth ramparts
    /// with a timber palisade and a wet ditch on three sides and the river on the fourth; two main streets crossing and
    /// quartering it; the castle, begun about 1070 and in stone by the 1130s, filling the north-east quarter with a motte
    /// 60 m across and 13 m high; Holy Trinity priory in the south of the north-west quarter (the Bullcroft), the rest of
    /// that quarter and most of the south-west (the Kinecroft) left open; the market and the houses crowded into the south
    /// and east; eleven parish churches; a bridge to Crowmarsh; a mint; in 1086 some four to five hundred house plots, 22
    /// of them held by Frenchmen, and perhaps two thousand people. Not known: the place of every lane and lost church and
    /// who lived where. Those are generated here from a seed, to those totals. Coordinates are metres from the crossroads,
    /// x east, z north.
    /// </summary>
    public static class TownPlan
    {
        // ---- the recorded figures the plan is held to
        public const float RampartWidthMetres = 550f, RampartDepthMetres = 780f;    // 42.9 ha: "a defensive zone of 43 hectares"
        public const int FrenchmenHouses = 22;                                      // Domesday: 22 dwellings of Frenchmen
        public const int ParishChurchCount = 11;                                    // "in the twelfth century, eleven parochial churches"
        public const int TargetHousesMin = 420, TargetHousesMax = 520;              // "probably 400 or more properties"; "c 500 houses"
        public const int MoneyerHouseholds = 3;                                     // a shire mint with a handful of moneyers
        public const float MotteDiameterMetres = 60f, MotteHeightMetres = 13f;

        private struct CallingDef { public string Name, Trade; public float Weight; public Workplace Works; public Wealth Wealth; public CallingDef(string n, float w, Workplace wp, Wealth we, string trade = "") { Name = n; Weight = w; Works = wp; Wealth = we; Trade = trade; } }

        // What the households live by. No list survives for Wallingford; the shares follow the pattern of the Winchester
        // surveys of about 1110 and 1148: victuals and drink first, then leather, cloth, metal and wood, with a broad base of
        // labourers, carriers and men who still work the town fields.
        private static readonly CallingDef[] Callings =
        {
            new CallingDef("baker", 5f, Workplace.Shop, Wealth.Middling), new CallingDef("alewife", 7f, Workplace.Shop, Wealth.Poor, "brewer"), new CallingDef("butcher", 4f, Workplace.Market, Wealth.Middling),
            new CallingDef("fishmonger", 2f, Workplace.Market, Wealth.Middling), new CallingDef("cook", 1.5f, Workplace.Shop, Wealth.Poor), new CallingDef("miller", 1f, Workplace.Wharf, Wealth.Middling, "miller"), new CallingDef("vintner", 0.8f, Workplace.Shop, Wealth.Rich),
            new CallingDef("weaver", 6f, Workplace.Shop, Wealth.Poor), new CallingDef("fuller", 2f, Workplace.Wharf, Wealth.Poor), new CallingDef("dyer", 1.5f, Workplace.Wharf, Wealth.Middling), new CallingDef("tailor", 3f, Workplace.Shop, Wealth.Middling),
            new CallingDef("tanner", 3f, Workplace.Wharf, Wealth.Middling), new CallingDef("shoemaker", 4f, Workplace.Shop, Wealth.Poor), new CallingDef("saddler", 1f, Workplace.Shop, Wealth.Middling), new CallingDef("skinner", 1f, Workplace.Shop, Wealth.Middling), new CallingDef("glover", 1f, Workplace.Shop, Wealth.Poor),
            new CallingDef("smith", 3f, Workplace.Yard, Wealth.Middling, "blacksmith"), new CallingDef("goldsmith", 0.7f, Workplace.Shop, Wealth.Rich), new CallingDef("lorimer", 1f, Workplace.Shop, Wealth.Middling),
            new CallingDef("carpenter", 4f, Workplace.Yard, Wealth.Middling, "carpenter"), new CallingDef("mason", 2f, Workplace.Castle, Wealth.Middling), new CallingDef("thatcher", 1f, Workplace.Fields, Wealth.Poor), new CallingDef("cooper", 2f, Workplace.Shop, Wealth.Middling), new CallingDef("turner", 1f, Workplace.Shop, Wealth.Poor), new CallingDef("potter", 1.5f, Workplace.Yard, Wealth.Poor),
            new CallingDef("merchant", 3.5f, Workplace.Market, Wealth.Rich), new CallingDef("chapman", 2f, Workplace.Market, Wealth.Poor),
            new CallingDef("carter", 3f, Workplace.Market, Wealth.Poor), new CallingDef("boatman", 3f, Workplace.Wharf, Wealth.Poor), new CallingDef("fisherman", 2f, Workplace.Wharf, Wealth.Poor, "fisherman"), new CallingDef("porter", 2f, Workplace.Market, Wealth.Poor),
            new CallingDef("labourer", 9f, Workplace.Fields, Wealth.Poor), new CallingDef("husbandman", 6f, Workplace.Fields, Wealth.Middling, "farmer"), new CallingDef("shepherd", 1f, Workplace.Fields, Wealth.Poor, "shepherd"),
            new CallingDef("widow", 4f, Workplace.Home, Wealth.Poor), new CallingDef("clerk", 1f, Workplace.Church, Wealth.Middling), new CallingDef("leech", 0.6f, Workplace.Shop, Wealth.Middling), new CallingDef("pauper", 2f, Workplace.Market, Wealth.Poor),
        };

        private static readonly string[] ChurchNames = { "St Mary-le-More", "Holy Trinity", "St Martin", "St Peter", "St Leonard", "St Mary the Less", "St John", "St Michael", "St Rumbold", "St Lucian", "All Hallows" };

        public static TownPlanData Wallingford(DeterministicRng rng)
        {
            var p = new TownPlanData { Id = "wallingford", Name = "Wallingford", HalfWidth = RampartWidthMetres * 0.5f, HalfDepth = RampartDepthMetres * 0.5f };
            float hw = p.HalfWidth, hd = p.HalfDepth;
            p.RiverX0 = hw + 14f; p.RiverX1 = hw + 84f;                       // the Thames here is some 70 m across
            p.BankWidth = 14f; p.BankHeight = 3.6f; p.DitchWidth = 15f; p.DitchDepth = 3.4f;   // "in places 7 m from bank top to ditch bottom"
            p.MotteRadius = MotteDiameterMetres * 0.5f; p.MotteHeight = MotteHeightMetres; p.MotteTopRadius = 11f;
            p.Bailey = new TownRect { Name = "the castle", X0 = 95f, Z0 = 85f, X1 = hw - 12f, Z1 = hd - 60f };
            p.MotteX = p.Bailey.X1 - 48f; p.MotteZ = p.Bailey.Z0 + 55f;       // the motte stands in the bailey's south-east, over the river
            p.Market = new TownRect { Name = "the market place", X0 = 8f, Z0 = -112f, X1 = 68f, Z1 = -14f };
            p.Priory = new TownRect { Name = "Holy Trinity priory", X0 = -205f, Z0 = 16f, X1 = -45f, Z1 = 150f };
            p.Kinecroft = new TownRect { Name = "the Kinecroft", X0 = -hw + 30f, Z0 = -295f, X1 = -45f, Z1 = -45f };
            p.Bullcroft = new TownRect { Name = "the Bullcroft", X0 = -hw + 30f, Z0 = 165f, X1 = -45f, Z1 = hd - 30f };
            p.BridgeX0 = hw + 3.2f;   // from the outer face of the Bridge Gate p.BridgeX1 = p.RiverX1 + 26f; p.BridgeZ = 0f; p.BridgeWidth = 5f;
            p.Garrison = 150; p.Monks = 9;

            // ---- gates and streets
            p.Gates.Add(new TownGate { Name = "North Gate", X = 0f, Z = hd, FacingDeg = 0f });
            p.Gates.Add(new TownGate { Name = "South Gate", X = 0f, Z = -hd, FacingDeg = 180f });
            p.Gates.Add(new TownGate { Name = "West Gate", X = -hw, Z = 0f, FacingDeg = 270f });
            p.Gates.Add(new TownGate { Name = "Bridge Gate", X = hw, Z = 0f, FacingDeg = 90f });
            Street(p, "High Street", -hw, 0f, hw, 0f, 8f, 0.9f);
            Street(p, "Castle Street", 0f, 0f, 0f, hd, 7f, 0.75f);
            Street(p, "St Mary's Street", 0f, 0f, 0f, -hd, 7f, 0.9f);
            Street(p, "Fish Street", 82f, -4f, 82f, -hd + 34f, 5f, 0.85f);
            Street(p, "Wood Street", 170f, -4f, 170f, -hd + 34f, 5f, 0.8f);
            Street(p, "Thames Street", hw - 22f, -4f, hw - 22f, -hd + 34f, 5f, 0.6f);
            Street(p, "St Peter's Lane", 4f, -130f, hw - 24f, -130f, 5f, 0.85f);
            Street(p, "Hart Lane", 4f, -222f, hw - 24f, -222f, 5f, 0.8f);
            Street(p, "St Leonard's Lane", 4f, -312f, hw - 24f, -312f, 5f, 0.65f);
            Street(p, "Goldsmiths' Lane", -4f, -312f, -hw + 34f, -312f, 5f, 0.45f);
            Street(p, "Castle Lane", 4f, 200f, p.Bailey.X0 - 16f, 200f, 5f, 0.4f);
            Street(p, "Bullcroft Lane", -4f, 158f, -120f, 158f, 4f, 0.35f);

            // ---- ground nobody builds houses on
            p.Reserved.Add(p.Market); p.Reserved.Add(p.Priory);
            p.Reserved.Add(new TownRect { Name = "castle ditch", X0 = p.Bailey.X0 - 20f, Z0 = p.Bailey.Z0 - 20f, X1 = hw, Z1 = hd });

            // ---- churches first: they were there before most of the houses
            int id = 0;
            Church(p, ref id, 0, TownBuildingKind.GreatChurch, 38f, -150f, 90f);                 // St Mary-le-More, the market church
            Church(p, ref id, 1, TownBuildingKind.GreatChurch, -125f, 60f, 90f);                 // Holy Trinity, parish nave and priory choir
            Church(p, ref id, 2, TownBuildingKind.ParishChurch, -22f, 30f, 90f);                 // St Martin at the crossroads
            Church(p, ref id, 3, TownBuildingKind.ParishChurch, hw - 48f, -26f, 90f);            // St Peter by the bridge
            Church(p, ref id, 4, TownBuildingKind.ParishChurch, hw - 55f, -338f, 90f);           // St Leonard by the river in the south-east
            Church(p, ref id, 5, TownBuildingKind.ParishChurch, -24f, -205f, 90f);               // St Mary the Less
            Church(p, ref id, 6, TownBuildingKind.ParishChurch, 30f, -350f, 90f);                // St John near the South Gate
            Church(p, ref id, 7, TownBuildingKind.ParishChurch, hw - 40f, 40f, 90f);             // St Michael on the eastern defences
            Church(p, ref id, 8, TownBuildingKind.ParishChurch, 126f, -176f, 90f);               // St Rumbold
            Church(p, ref id, 9, TownBuildingKind.ParishChurch, -34f, -hd - 62f, 90f, suburb: true);   // St Lucian outside the South Gate
            Church(p, ref id, 10, TownBuildingKind.ParishChurch, 46f, 262f, 90f);                // All Hallows by the castle

            // ---- the priory's ranges and the castle's buildings
            Fixed(p, ref id, TownBuildingKind.PrioryRange, "the prior's hall", -150f, 108f, 180f, 5f, 3.5f);
            Fixed(p, ref id, TownBuildingKind.PrioryRange, "the monks' dorter", -100f, 108f, 180f, 5f, 3.5f);
            Fixed(p, ref id, TownBuildingKind.Keep, "the keep", p.MotteX, p.MotteZ, 0f, 6f, 6f);
            Fixed(p, ref id, TownBuildingKind.CastleHall, "the great hall", p.Bailey.X0 + 50f, p.Bailey.Cz + 40f, 90f, 5f, 3.5f);
            Fixed(p, ref id, TownBuildingKind.Stable, "the stables", p.Bailey.X0 + 30f, p.Bailey.Z1 - 30f, 180f, 5f, 3.5f);
            Fixed(p, ref id, TownBuildingKind.ParishChurch, "the castle chapel", p.Bailey.X0 + 62f, p.Bailey.Z0 + 34f, 90f, 2.75f, 7.3f);
            Fixed(p, ref id, TownBuildingKind.Gatehouse, "the castle gate", p.Bailey.X0, 200f, 270f, 4.5f, 3f);
            foreach (var g in p.Gates) Fixed(p, ref id, TownBuildingKind.Gatehouse, g.Name, g.X, g.Z, g.FacingDeg, 4.5f, 3f);

            // ---- plots along every street
            foreach (TownStreet s in p.Streets) Plots(p, s, ref id, rng);
            // a few houses outside the gates, along the roads
            Suburb(p, ref id, rng, 0f, -hd - 34f, 0f, -hd - 150f, 7);
            Suburb(p, ref id, rng, -hw - 34f, 0f, -hw - 120f, 0f, 5);
            Suburb(p, ref id, rng, 0f, hd + 34f, 0f, hd + 110f, 4);

            Households(p, rng);
            return p;
        }

        private static void Street(TownPlanData p, string name, float x0, float z0, float x1, float z1, float width, float fill)
            => p.Streets.Add(new TownStreet { Name = name, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Width = width, Fill = fill });

        private static void Church(TownPlanData p, ref int id, int parish, TownBuildingKind kind, float x, float z, float facing, bool suburb = false)
        {
            bool great = kind == TownBuildingKind.GreatChurch;
            // Churches lie east and west: the model's chancel is its +Z, so a yaw of 90 points it east.
            var b = new TownBuilding { Id = id++, Kind = kind, X = x, Z = z, FacingDeg = facing, HalfWidth = great ? 5.5f : 3.4f, HalfDepth = great ? 16f : 8f, Name = ChurchNames[parish], Parish = parish, Suburb = suburb };
            p.Buildings.Add(b);
            float m = great ? 9f : 6f;   // the churchyard
            p.Reserved.Add(new TownRect { Name = b.Name + " churchyard", X0 = x - b.HalfDepth - m, Z0 = z - b.HalfWidth - m, X1 = x + b.HalfDepth + m, Z1 = z + b.HalfWidth + m });
        }

        private static void Fixed(TownPlanData p, ref int id, TownBuildingKind kind, string name, float x, float z, float facing, float halfW, float halfD)
            => p.Buildings.Add(new TownBuilding { Id = id++, Kind = kind, X = x, Z = z, FacingDeg = facing, HalfWidth = halfW, HalfDepth = halfD, Name = name });

        private static void Footprint(TownBuildingKind k, out float halfW, out float halfD)
        {
            switch (k)
            {
                case TownBuildingKind.Cot: halfW = 2.9f; halfD = 2.2f; break;          // the hut, long side to the street
                case TownBuildingKind.GreatHouse: case TownBuildingKind.Alehouse: halfW = 5.2f; halfD = 3.7f; break;
                default: halfW = 2.65f; halfD = 4.65f; break;                         // the burgess house, gable to the street
            }
        }

        private static void Plots(TownPlanData p, TownStreet s, ref int id, DeterministicRng rng)
        {
            float dx = s.X1 - s.X0, dz = s.Z1 - s.Z0; float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1f) return;
            float ux = dx / len, uz = dz / len;          // along the street
            float nx = -uz, nz = ux;                     // its left side
            for (int side = -1; side <= 1; side += 2)
            {
                float t = 12f + rng.Range(0f, 4f);
                while (t < len - 12f)
                {
                    float roll = rng.NextFloat();
                    TownBuildingKind kind = roll < 0.70f ? TownBuildingKind.House : roll < 0.92f ? TownBuildingKind.Cot : roll < 0.965f ? TownBuildingKind.GreatHouse : TownBuildingKind.Alehouse;
                    Footprint(kind, out float halfW, out float halfD);
                    float porch = kind == TownBuildingKind.GreatHouse || kind == TownBuildingKind.Alehouse ? 2.2f : 0f;   // their porch stands out from the wall
                    float c = t + halfW;
                    float off = s.Width * 0.5f + 1.4f + porch + halfD;
                    float x = s.X0 + ux * c + nx * off * side, z = s.Z0 + uz * c + nz * off * side;
                    float step = 2f * halfW + rng.Range(0.9f, 2.6f);
                    if (rng.NextFloat() > s.Fill) { t += step; continue; }                       // a garden, a void plot, a yard gate
                    // the door looks back at the street
                    float facing = (float)(Math.Atan2(-nx * side, -nz * side) * 180.0 / Math.PI); if (facing < 0f) facing += 360f;
                    if (Free(p, x, z, halfW, halfD, facing, s))
                    {
                        p.Buildings.Add(new TownBuilding { Id = id++, Kind = kind, X = x, Z = z, FacingDeg = facing, HalfWidth = halfW, HalfDepth = halfD, Street = s.Name });
                        t += step;
                    }
                    else t += 3f;
                }
            }
        }

        private static void Suburb(TownPlanData p, ref int id, DeterministicRng rng, float x0, float z0, float x1, float z1, int count)
        {
            float dx = x1 - x0, dz = z1 - z0; float len = (float)Math.Sqrt(dx * dx + dz * dz); float ux = dx / len, uz = dz / len, nx = -uz, nz = ux;
            for (int i = 0; i < count; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                float c = (i / 2) * (len / Math.Max(1, (count + 1) / 2)) + rng.Range(2f, 8f);
                Footprint(TownBuildingKind.Cot, out float halfW, out float halfD);
                float off = 4f + 1.6f + halfD;
                float x = x0 + ux * c + nx * off * side, z = z0 + uz * c + nz * off * side;
                float facing = (float)(Math.Atan2(-nx * side, -nz * side) * 180.0 / Math.PI); if (facing < 0f) facing += 360f;
                if (!Clear(p, x, z, halfW, halfD)) continue;
                p.Buildings.Add(new TownBuilding { Id = id++, Kind = TownBuildingKind.Cot, X = x, Z = z, FacingDeg = facing, HalfWidth = halfW, HalfDepth = halfD, Street = "the road", Suburb = true });
            }
        }

        /// <summary>Is this footprint on buildable ground: inside the ramparts, off the reserved ground, clear of every other
        /// street and of every building already standing?</summary>
        private static bool Free(TownPlanData p, float x, float z, float halfW, float halfD, float facing, TownStreet own)
        {
            float r = Math.Max(halfW, halfD);
            float inner = p.BankWidth + 6f;
            if (x - r < -p.HalfWidth + inner || x + r > p.HalfWidth - 8f || z - r < -p.HalfDepth + inner || z + r > p.HalfDepth - inner) return false;
            foreach (TownRect rz in p.Reserved) if (rz.Contains(x, z, r + 1f)) return false;
            foreach (TownStreet s in p.Streets)
            {
                if (s == own) continue;
                if (DistToSegment(x, z, s.X0, s.Z0, s.X1, s.Z1) < s.Width * 0.5f + r + 0.8f) return false;
            }
            return Clear(p, x, z, halfW, halfD);
        }

        private static bool Clear(TownPlanData p, float x, float z, float halfW, float halfD)
        {
            float r = Math.Max(halfW, halfD);
            foreach (TownBuilding b in p.Buildings)
            {
                float rb = Math.Max(b.HalfWidth, b.HalfDepth);
                // Neighbours in a row stand side by side: allow them to be as close as their narrow sides, never overlapping.
                float need = Math.Min(r + rb, Math.Min(halfW, halfD) + Math.Min(b.HalfWidth, b.HalfDepth) + 0.6f);
                float ddx = b.X - x, ddz = b.Z - z;
                if (ddx * ddx + ddz * ddz < need * need) return false;
                if (Math.Abs(ddx) < r + rb - 0.1f && Math.Abs(ddz) < r + rb - 0.1f && Overlaps(x, z, halfW, halfD, b)) return false;
            }
            return true;
        }

        /// <summary>Axis-aligned overlap test; every plot faces a compass point, so widths and depths swap by facing.</summary>
        private static bool Overlaps(float x, float z, float halfW, float halfD, TownBuilding b)
        {
            // The candidate's extents are conservative (its larger half-size both ways); the standing building's are exact.
            float r = Math.Max(halfW, halfD);
            bool turned = Math.Abs(Math.Abs(b.FacingDeg % 180f) - 90f) < 45f;     // door facing east or west: width runs along z
            float bx = turned ? b.HalfDepth : b.HalfWidth, bz = turned ? b.HalfWidth : b.HalfDepth;
            return Math.Abs(b.X - x) < bx + Math.Min(r, halfW + 2.2f) && Math.Abs(b.Z - z) < bz + Math.Min(r, halfD + 2.2f);
        }

        public static float DistToSegment(float px, float pz, float x0, float z0, float x1, float z1)
        {
            float dx = x1 - x0, dz = z1 - z0; float l2 = dx * dx + dz * dz;
            float t = l2 > 1e-6f ? ((px - x0) * dx + (pz - z0) * dz) / l2 : 0f; t = t < 0f ? 0f : t > 1f ? 1f : t;
            float cx = x0 + dx * t - px, cz = z0 + dz * t - pz;
            return (float)Math.Sqrt(cx * cx + cz * cz);
        }

        // ------------------------------------------------------------------ people

        private static void Households(TownPlanData p, DeterministicRng rng)
        {
            var weights = new float[Callings.Length];
            for (int i = 0; i < Callings.Length; i++) weights[i] = Callings[i].Weight;
            // The Frenchmen: the dwellings nearest the castle gate. The moneyers: the best houses nearest the market.
            var dwellings = new List<TownBuilding>(520);
            foreach (TownBuilding b in p.Buildings) if (TownPlanData.IsDwelling(b.Kind)) dwellings.Add(b);
            var french = Nearest(dwellings, p.Bailey.X0, 200f, FrenchmenHouses, null);
            var moneyers = Nearest(dwellings, p.Market.Cx, p.Market.Cz, MoneyerHouseholds, french);
            foreach (TownBuilding b in dwellings)
            {
                var h = new TownHousehold { Id = p.Households.Count, Building = b.Id, Parish = NearestParish(p, b.X, b.Z) };
                b.Household = h.Id; b.Parish = h.Parish;
                if (french.Contains(b)) { h.French = true; h.Calling = rng.Chance(0.35f) ? "knight" : "serjeant"; h.Works = Workplace.Castle; h.Wealth = h.Calling == "knight" ? Wealth.Rich : Wealth.Middling; }
                else if (moneyers.Contains(b)) { h.Calling = "moneyer"; h.Works = Workplace.Shop; h.Wealth = Wealth.Rich; }
                else if (b.Kind == TownBuildingKind.Alehouse) { h.Calling = "alewife"; h.GameTrade = "brewer"; h.Works = Workplace.Shop; h.Wealth = Wealth.Middling; }
                else
                {
                    CallingDef c = Callings[rng.WeightedIndex(weights)];
                    // The great houses belong to those who can afford them; the cots to those who cannot afford better.
                    if (b.Kind == TownBuildingKind.GreatHouse && c.Wealth != Wealth.Rich) c = Callings[IndexOf(rng.Chance(0.6f) ? "merchant" : "vintner")];
                    if (b.Kind == TownBuildingKind.Cot && c.Wealth == Wealth.Rich) c = Callings[IndexOf("labourer")];
                    h.Calling = c.Name; h.GameTrade = c.Trade; h.Works = c.Works; h.Wealth = c.Wealth;
                }
                h.Adults = h.Calling == "widow" || h.Calling == "pauper" ? 1 : rng.Chance(0.12f) ? 3 : 2;
                h.Children = h.Calling == "pauper" ? 0 : rng.WeightedIndex(ChildWeights);
                h.Servants = h.Wealth == Wealth.Rich ? rng.Range(2, 5) : h.Wealth == Wealth.Middling ? rng.Range(0, 2) : 0;
                p.Households.Add(h);
            }
        }

        // Children under the roof: many die young, so households are smaller than the births would suggest.
        private static readonly float[] ChildWeights = { 0.22f, 0.26f, 0.26f, 0.16f, 0.07f, 0.03f };

        private static int IndexOf(string calling) { for (int i = 0; i < Callings.Length; i++) if (Callings[i].Name == calling) return i; return 0; }

        private static HashSet<TownBuilding> Nearest(List<TownBuilding> from, float x, float z, int count, HashSet<TownBuilding> not)
        {
            var sorted = new List<TownBuilding>(from);
            sorted.Sort((a, b) => { float da = (a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z), db = (b.X - x) * (b.X - x) + (b.Z - z) * (b.Z - z); int c = da.CompareTo(db); return c != 0 ? c : a.Id.CompareTo(b.Id); });
            var set = new HashSet<TownBuilding>();
            foreach (TownBuilding b in sorted) { if (set.Count >= count) break; if (b.Suburb || (not != null && not.Contains(b))) continue; set.Add(b); }
            return set;
        }

        public static int NearestParish(TownPlanData p, float x, float z)
        {
            int best = 0; float bestD = float.MaxValue;
            foreach (TownBuilding b in p.Buildings)
            {
                if (b.Parish < 0 || (b.Kind != TownBuildingKind.ParishChurch && b.Kind != TownBuildingKind.GreatChurch)) continue;
                float d = (b.X - x) * (b.X - x) + (b.Z - z) * (b.Z - z);
                if (d < bestD) { bestD = d; best = b.Parish; }
            }
            return best;
        }

        public static string ParishName(int parish) => parish >= 0 && parish < ChurchNames.Length ? ChurchNames[parish] : string.Empty;
    }
}

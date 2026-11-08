using System;

namespace AshesAndCrowns.Sim
{
    public enum FormationType : byte { Line = 0, Wedge = 1, Square = 2, Shieldwall = 3 }

    /// <summary>What a unit has been told to do. Lives in Sim so the service rules can read the banner.</summary>
    public enum OrderType : byte { Hold = 0, Move = 1, Charge = 2 }

    /// <summary>
    /// Pure functions giving each soldier an offset from the unit anchor, in unit-local space
    /// (x = right, z = forward). Deterministic, allocation-free, Burst-compatible.
    /// </summary>
    public static class FormationMath
    {
        public const int LineRankWidth = 10;

        public static void SlotOffset(FormationType formation, int index, int count, float spacing, float shieldwallSpacing, out float x, out float z)
        {
            if (count <= 0) { x = 0f; z = 0f; return; }
            index = Math.Max(0, Math.Min(index, count - 1));
            switch (formation)
            {
                case FormationType.Wedge: Wedge(index, spacing, out x, out z); return;
                case FormationType.Square: Grid(index, count, (int)Math.Ceiling(Math.Sqrt(count)), spacing, out x, out z); return;
                case FormationType.Shieldwall: Grid(index, count, (int)Math.Ceiling(count / 2.0), shieldwallSpacing, out x, out z); return;
                default: Grid(index, count, Math.Min(LineRankWidth, count), spacing, out x, out z); return;
            }
        }

        private static void Grid(int index, int count, int width, float spacing, out float x, out float z)
        {
            if (width < 1) width = 1;
            int rank = index / width;
            int file = index % width;
            int ranks = (count + width - 1) / width;
            int inThisRank = Math.Min(width, count - rank * width);
            x = (file - (inThisRank - 1) * 0.5f) * spacing;
            z = -(rank - (ranks - 1) * 0.5f) * spacing; // front rank has the highest z
        }

        private static void Wedge(int index, float spacing, out float x, out float z)
        {
            // Row r has r+1 soldiers: 1, 2, 3 ... The point man is index 0 at the front.
            int row = 0, first = 0;
            while (first + row + 1 <= index) { first += row + 1; row++; }
            int inRow = index - first;
            x = (inRow - row * 0.5f) * spacing;
            z = -row * spacing * 0.85f;
        }

        /// <summary>Approximate frontage (metres) of a formation, for arrival checks and selection.</summary>
        public static float Frontage(FormationType formation, int count, float spacing, float shieldwallSpacing)
        {
            switch (formation)
            {
                case FormationType.Wedge: return RowsForWedge(count) * spacing;
                case FormationType.Square: return (float)Math.Ceiling(Math.Sqrt(count)) * spacing;
                case FormationType.Shieldwall: return (float)Math.Ceiling(count / 2.0) * shieldwallSpacing;
                default: return Math.Min(LineRankWidth, count) * spacing;
            }
        }

        private static int RowsForWedge(int count)
        {
            int rows = 0, placed = 0;
            while (placed < count) { rows++; placed += rows; }
            return rows;
        }
    }
}

using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    [Serializable] public sealed class ItemDef
    {
        public string id, name, category;    // category: food, raw, good, tool, weapon, armour
        public int basePrice;                // pennies
        public float nutrition;              // food only, fraction of a full belly
        public float weight;                 // kg
        public int armour;                   // armour only, 0..100 -> fraction
        public int damage;                   // weapon only
        public string tradeTool;             // which trade needs this tool
    }

    [Serializable] public sealed class ItemsData
    {
        public ItemDef[] items = new ItemDef[0];
        public ItemDef Find(string id) { foreach (var i in items) if (i.id == id) return i; return null; }
    }

    /// <summary>Counts by item id. Allocation-free lookups after warm-up; small lists.</summary>
    public sealed class Inventory
    {
        private readonly List<string> _ids = new List<string>(16);
        private readonly List<int> _counts = new List<int>(16);

        public int Count(string id) { int i = _ids.IndexOf(id); return i < 0 ? 0 : _counts[i]; }
        public int DistinctCount => _ids.Count;
        public string IdAt(int index) => _ids[index];
        public int CountAt(int index) => _counts[index];

        public void Add(string id, int n = 1)
        {
            if (n <= 0 || string.IsNullOrEmpty(id)) return;
            int i = _ids.IndexOf(id);
            if (i < 0) { _ids.Add(id); _counts.Add(n); } else _counts[i] += n;
        }

        public bool Remove(string id, int n = 1)
        {
            int i = _ids.IndexOf(id);
            if (i < 0 || _counts[i] < n) return false;
            _counts[i] -= n;
            if (_counts[i] == 0) { _ids.RemoveAt(i); _counts.RemoveAt(i); }
            return true;
        }

        public float TotalWeight(ItemsData items)
        {
            float w = 0f;
            for (int i = 0; i < _ids.Count; i++) { var d = items.Find(_ids[i]); if (d != null) w += d.weight * _counts[i]; }
            return w;
        }

        public void Clear() { _ids.Clear(); _counts.Clear(); }
    }
}

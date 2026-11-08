using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum WarShock : byte { None = 0, Foraged = 1, Refugees = 2, Raided = 3, GarrisonBilleted = 4 }

    [Serializable] public sealed class EconomyBalance
    {
        public float elasticity = 0.6f;            // price = base * (demand/stock)^elasticity
        public float restockPerDay = 0.15f;        // stock moves this fraction toward baseline per day
        public float minPriceMultiplier = 0.3f, maxPriceMultiplier = 6f;
        public float sellSpread = 0.7f;            // merchants buy at this fraction of the price they sell at
        public float[] foodSeasonByMonth = { 1.35f, 1.45f, 1.5f, 1.4f, 1.2f, 1.1f, 1.0f, 0.75f, 0.7f, 0.8f, 1.0f, 1.2f };
        public float shockDecayPerDay = 0.08f;
        public float haggleBaseAccept = 0.5f;      // chance a fair offer is accepted
        public float haggleSkillWeight = 0.006f;   // per skill point
        public float haggleGreedPenalty = 2.2f;    // how fast acceptance drops as the offer gets worse for the merchant
        public float haggleStandingWeight = 0.3f;
    }

    public sealed class MarketGood
    {
        public string Id; public float Stock, Baseline, Demand;
    }

    /// <summary>One settlement's market. Prices come from stock vs demand, the season, standing and war shocks.</summary>
    public sealed class Market
    {
        private readonly EconomyBalance _b;
        private readonly ItemsData _items;
        private readonly List<MarketGood> _goods = new List<MarketGood>(32);
        public WarShock Shock { get; private set; }
        public float ShockStrength { get; private set; }
        public float Wealth = 1f;   // settlement prosperity, scales baseline stock (0.3 ruined .. 2 rich)

        public IReadOnlyList<MarketGood> Goods => _goods;

        public Market(EconomyBalance balance, ItemsData items) { _b = balance ?? throw new ArgumentNullException(nameof(balance)); _items = items ?? throw new ArgumentNullException(nameof(items)); }

        public void Stock(string id, float baseline, float demand = 1f)
        {
            MarketGood g = Find(id);
            if (g == null) { g = new MarketGood { Id = id }; _goods.Add(g); }
            g.Baseline = baseline; g.Stock = baseline; g.Demand = demand;
        }

        public MarketGood Find(string id) { foreach (var g in _goods) if (g.Id == id) return g; return null; }

        private float Multiplier(MarketGood g, ItemDef def, int month)
        {
            float ratio = g.Demand * Math.Max(0.05f, g.Baseline) / Math.Max(0.5f, g.Stock);
            float m = (float)Math.Pow(ratio, _b.elasticity);
            if (def != null && def.category == "food") m *= _b.foodSeasonByMonth[Clamp(month - 1, 0, 11)];
            m *= ShockMultiplier(def);
            return Math.Max(_b.minPriceMultiplier, Math.Min(_b.maxPriceMultiplier, m));
        }

        private float ShockMultiplier(ItemDef def)
        {
            if (Shock == WarShock.None || ShockStrength <= 0f || def == null) return 1f;
            float s = ShockStrength;
            switch (Shock)
            {
                case WarShock.Foraged: return def.category == "food" || def.id == "grain" || def.id == "flour" ? 1f + 2f * s : 1f;      // grain triples
                case WarShock.Refugees: return def.category == "food" ? 1f + 0.8f * s : def.category == "good" ? 1f - 0.3f * s : 1f;  // food dear, labour and goods cheap
                case WarShock.Raided: return 1f + 0.6f * s;
                case WarShock.GarrisonBilleted: return def.category == "food" || def.id == "ale" ? 1f + 0.5f * s : def.category == "weapon" || def.category == "armour" ? 1f + 0.4f * s : 1f;
                default: return 1f;
            }
        }

        /// <summary>Price the player pays for one unit.</summary>
        public int BuyPrice(string id, int month, float standing = 0.5f)
        {
            MarketGood g = Find(id); ItemDef def = _items.Find(id);
            if (g == null || def == null) return 0;
            float standingMul = 1.1f - standing * 0.2f; // respected: 0.9x; despised: 1.1x
            return Math.Max(1, (int)Math.Round(def.basePrice * Multiplier(g, def, month) * standingMul));
        }

        /// <summary>Price the merchant pays the player for one unit.</summary>
        public int SellPrice(string id, int month, float standing = 0.5f)
        {
            MarketGood g = Find(id); ItemDef def = _items.Find(id);
            if (def == null) return Math.Max(0, 0);
            float mul = g != null ? Multiplier(g, def, month) : 1f;
            return Math.Max(0, (int)Math.Round(def.basePrice * mul * _b.sellSpread * (0.9f + standing * 0.2f)));
        }

        public bool Buy(string id, int n, int month, float standing, CharacterSheet buyer, Inventory into, out int paid)
        {
            paid = 0;
            MarketGood g = Find(id);
            if (g == null || n <= 0 || g.Stock < n) return false;
            int price = BuyPrice(id, month, standing) * n;
            if (buyer.Coins < price) return false;
            buyer.Coins -= price; g.Stock -= n; into.Add(id, n); paid = price;
            return true;
        }

        public bool Sell(string id, int n, int month, float standing, CharacterSheet seller, Inventory from, out int earned)
        {
            earned = 0;
            if (n <= 0 || from.Count(id) < n) return false;
            MarketGood g = Find(id);
            if (g == null) { g = new MarketGood { Id = id, Baseline = 0.5f, Demand = 0.5f }; _goods.Add(g); }
            int price = SellPrice(id, month, standing) * n;
            from.Remove(id, n); g.Stock += n; seller.Coins += price; earned = price;
            return true;
        }

        /// <summary>Haggle for a better price. offerRatio is offer/asking (buying: below 1 is bold; selling: above 1 is bold).</summary>
        public bool Haggle(float offerRatio, bool buying, float skill, float standing, DeterministicRng rng)
        {
            float greed = buying ? 1f - offerRatio : offerRatio - 1f; // how much worse for the merchant than the asking price
            if (greed <= 0f) return true;
            float accept = _b.haggleBaseAccept + skill * _b.haggleSkillWeight + (standing - 0.5f) * _b.haggleStandingWeight - greed * _b.haggleGreedPenalty;
            return rng.Chance(Math.Max(0.02f, Math.Min(0.95f, accept)));
        }

        /// <summary>Restore a saved shock without re-applying its one-time stock damage.</summary>
        public void RestoreShock(WarShock shock, float strength) { Shock = shock; ShockStrength = Math.Max(0f, Math.Min(1f, strength)); if (ShockStrength <= 0f) Shock = WarShock.None; }

        public void ApplyShock(WarShock shock, float strength)
        {
            Shock = shock; ShockStrength = Math.Max(0f, Math.Min(1f, strength));
            if (shock == WarShock.Foraged) foreach (var g in _goods) if (IsFood(g.Id)) g.Stock *= 0.3f;
            if (shock == WarShock.Raided) foreach (var g in _goods) g.Stock *= 0.5f;
        }

        private bool IsFood(string id) { var d = _items.Find(id); return d != null && (d.category == "food" || id == "grain" || id == "flour"); }

        /// <summary>Once a game day: restock toward baseline (scaled by wealth), decay shocks.</summary>
        public void TickDay()
        {
            foreach (var g in _goods)
            {
                float target = g.Baseline * Wealth;
                g.Stock += (target - g.Stock) * _b.restockPerDay;
            }
            if (ShockStrength > 0f) { ShockStrength = Math.Max(0f, ShockStrength - _b.shockDecayPerDay); if (ShockStrength <= 0f) Shock = WarShock.None; }
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}

using System;

namespace AshesAndCrowns.Sim
{
    public enum Faction : byte { Crown = 0, Angevin = 1, Church = 2, Commons = 3 }

    public enum Deed : byte
    {
        FoughtForStephen, FoughtForMatilda, KilledSurrendered, RanDownRouters, AttendedMass, DesecratedChurch, RobbedTraveller,
        PaidTithe, HelpedRefugees, Deserted, HonouredContract, BrokeContract, GaveAlms, Murder, Theft
    }

    [Serializable] public sealed class ReputationBalance
    {
        public float decayPerDay = 0.002f;
        public float knownBelow = -0.3f, favouredAbove = 0.3f;
    }

    /// <summary>What the great powers think of you: Crown (Stephen), Angevin (Matilda), Church, Commons. -1..1 each. Outlaw notoriety separate.</summary>
    public sealed class GlobalReputation
    {
        private readonly ReputationBalance _b;
        private readonly float[] _v = new float[4];
        public float Notoriety { get; private set; }   // 0..1 how famous your crimes are

        public GlobalReputation(ReputationBalance balance) { _b = balance ?? throw new ArgumentNullException(nameof(balance)); }

        public float Get(Faction f) => _v[(int)f];
        public void Set(Faction f, float v) => _v[(int)f] = Clamp(v);
        public void SetNotoriety(float v) => Notoriety = Math.Max(0f, Math.Min(1f, v));

        public void Record(Deed d)
        {
            switch (d)
            {
                case Deed.FoughtForStephen: Add(Faction.Crown, 0.06f); Add(Faction.Angevin, -0.05f); break;
                case Deed.FoughtForMatilda: Add(Faction.Angevin, 0.06f); Add(Faction.Crown, -0.05f); break;
                case Deed.KilledSurrendered: Add(Faction.Church, -0.08f); Add(Faction.Commons, -0.04f); Notoriety = Math.Min(1f, Notoriety + 0.05f); break;
                case Deed.RanDownRouters: Add(Faction.Commons, -0.05f); Add(Faction.Church, -0.03f); break;
                case Deed.AttendedMass: Add(Faction.Church, 0.01f); break;
                case Deed.DesecratedChurch: Add(Faction.Church, -0.3f); Notoriety = Math.Min(1f, Notoriety + 0.2f); break;
                case Deed.RobbedTraveller: Add(Faction.Commons, -0.06f); Notoriety = Math.Min(1f, Notoriety + 0.08f); break;
                case Deed.PaidTithe: Add(Faction.Church, 0.03f); break;
                case Deed.HelpedRefugees: Add(Faction.Commons, 0.05f); Add(Faction.Church, 0.02f); break;
                case Deed.Deserted: Add(Faction.Crown, -0.08f); Add(Faction.Angevin, -0.08f); Notoriety = Math.Min(1f, Notoriety + 0.1f); break;
                case Deed.HonouredContract: Add(Faction.Commons, 0.02f); break;
                case Deed.BrokeContract: Add(Faction.Commons, -0.04f); break;
                case Deed.GaveAlms: Add(Faction.Church, 0.02f); Add(Faction.Commons, 0.02f); break;
                case Deed.Murder: Add(Faction.Commons, -0.1f); Add(Faction.Church, -0.06f); Notoriety = Math.Min(1f, Notoriety + 0.15f); break;
                case Deed.Theft: Add(Faction.Commons, -0.03f); Notoriety = Math.Min(1f, Notoriety + 0.04f); break;
            }
        }

        public void TickDay()
        {
            for (int i = 0; i < 4; i++) _v[i] -= Math.Sign(_v[i]) * Math.Min(Math.Abs(_v[i]), _b.decayPerDay);
            Notoriety = Math.Max(0f, Notoriety - _b.decayPerDay * 2f);
        }

        public string Describe(Faction f)
        {
            float v = Get(f);
            if (v >= 0.6f) return "trusted"; if (v >= _b.favouredAbove) return "favoured"; if (v > _b.knownBelow) return "unremarked";
            return v > -0.6f ? "suspect" : "hated";
        }

        private void Add(Faction f, float d) => _v[(int)f] = Clamp(_v[(int)f] + d);
        private static float Clamp(float v) => v < -1f ? -1f : (v > 1f ? 1f : v);
    }
}

using System;

namespace AshesAndCrowns.Sim
{
    [Serializable] public sealed class StandingBalance
    {
        public float massAttended = 0.03f, massMissed = -0.04f, debtHonoured = 0.05f, debtDefaulted = -0.12f;
        public float fightWon = 0.01f, fightLost = -0.02f, brawlStarted = -0.06f, generosity = 0.04f, theftSeen = -0.25f;
        public float decayTowardNeutralPerDay = 0.004f;
        public float respectedAbove = 0.7f, despisedBelow = 0.3f;
    }

    /// <summary>Informal village standing, 0 despised .. 1 respected. No offices; reputation is the office.</summary>
    public sealed class VillageStanding
    {
        private readonly StandingBalance _b;
        public float Value { get; private set; } = 0.5f;
        public int MassesAttended { get; private set; }
        public int MassesMissed { get; private set; }

        public VillageStanding(StandingBalance balance) { _b = balance ?? throw new ArgumentNullException(nameof(balance)); }

        public bool Respected => Value >= _b.respectedAbove;
        public bool Despised => Value <= _b.despisedBelow;

        public void Set(float value, int attended, int missed) { Value = Clamp(value); MassesAttended = attended; MassesMissed = missed; }

        public void AttendedMass() { MassesAttended++; Adjust(_b.massAttended); }
        /// <summary>Kneeling at the altar, seen by whoever is in the nave. Amount comes from ActivityRules.PrayerGain.</summary>
        public void Prayed(float favour) => Adjust(favour);
        public void MissedMass() { MassesMissed++; Adjust(_b.massMissed); }
        public void DebtHonoured() => Adjust(_b.debtHonoured);
        public void DebtDefaulted() => Adjust(_b.debtDefaulted);
        public void FightWon() => Adjust(_b.fightWon);
        public void FightLost() => Adjust(_b.fightLost);
        public void BrawlStarted() => Adjust(_b.brawlStarted);
        public void Generosity() => Adjust(_b.generosity);
        public void TheftSeen() => Adjust(_b.theftSeen);

        public void TickDay() { Value += (0.5f - Value) * _b.decayTowardNeutralPerDay; }

        public string Describe()
        {
            if (Value >= 0.85f) return "honoured";
            if (Respected) return "respected";
            if (Value >= 0.45f) return "accepted";
            if (Value > _b.despisedBelow) return "doubted";
            return "despised";
        }

        private void Adjust(float d) => Value = Clamp(Value + d);
        private static float Clamp(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}

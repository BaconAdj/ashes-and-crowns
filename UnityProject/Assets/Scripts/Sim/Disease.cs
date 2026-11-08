using System;

namespace AshesAndCrowns.Sim
{
    public enum Sickness : byte { None = 0, Flux = 1, Fever = 2, Plague = 3 }

    [Serializable] public sealed class DiseaseBalance
    {
        public float fluxDailyChance = 0.004f, feverDailyChance = 0.003f;
        public float plagueDailyChanceAtFullOutbreak = 0.08f;
        public float campMultiplier = 3f;             // armies breed fever
        public float fluxBloodLossPerDay = 0.03f, feverBloodLossPerDay = 0.05f, plagueBloodLossPerDay = 0.12f;
        public float fluxRecoverChance = 0.25f, feverRecoverChance = 0.18f, plagueRecoverChance = 0.08f;
        public float restRecoveryBonus = 1.6f;        // recovering while resting at home
        public float settlementOutbreakDecayPerDay = 0.04f;
        public float settlementOutbreakSpreadPerTraveller = 0.15f;
    }

    /// <summary>Who is sick with what, and the daily rolls. Settlement outbreak level (0..1) drives plague exposure.</summary>
    public sealed class DiseaseState
    {
        private readonly DiseaseBalance _b;
        public Sickness Current { get; private set; }
        public int DaysSick { get; private set; }

        public DiseaseState(DiseaseBalance balance) { _b = balance ?? throw new ArgumentNullException(nameof(balance)); }

        public void Set(Sickness s, int days) { Current = s; DaysSick = days; }

        /// <summary>Once a game day. outbreak is the local plague level; inCamp doubles exposure; resting helps recovery.</summary>
        public void TickDay(BodyState body, float outbreak, bool inCamp, bool resting, DeterministicRng rng)
        {
            if (body == null || body.IsDead) return;
            float mult = inCamp ? _b.campMultiplier : 1f;
            if (Current == Sickness.None)
            {
                if (rng.Chance(_b.plagueDailyChanceAtFullOutbreak * Math.Max(0f, Math.Min(1f, outbreak)))) Current = Sickness.Plague;
                else if (rng.Chance(_b.feverDailyChance * mult)) Current = Sickness.Fever;
                else if (rng.Chance(_b.fluxDailyChance * mult)) Current = Sickness.Flux;
                DaysSick = 0;
                return;
            }
            DaysSick++;
            float loss, recover;
            switch (Current)
            {
                case Sickness.Flux: loss = _b.fluxBloodLossPerDay; recover = _b.fluxRecoverChance; break;
                case Sickness.Fever: loss = _b.feverBloodLossPerDay; recover = _b.feverRecoverChance; break;
                default: loss = _b.plagueBloodLossPerDay; recover = _b.plagueRecoverChance; break;
            }
            body.Drain(loss, Current == Sickness.Plague ? "the plague" : Current == Sickness.Fever ? "a fever" : "the bloody flux");
            if (!body.IsDead && rng.Chance(recover * (resting ? _b.restRecoveryBonus : 1f))) { Current = Sickness.None; DaysSick = 0; }
        }

        public string Describe()
        {
            switch (Current)
            {
                case Sickness.Flux: return "the flux";
                case Sickness.Fever: return "a fever";
                case Sickness.Plague: return "the plague";
                default: return string.Empty;
            }
        }
    }
}

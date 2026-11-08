using System;

namespace AshesAndCrowns.Sim
{
    [Serializable] public sealed class SurvivalBalance
    {
        public float hungerPerGameHour = 0.03f;        // full to empty in about 33 hours
        public float starvationBloodLossPerHour = 0.012f;
        public float hungerStaminaPenaltyBelow = 0.25f; // stamina regen suffers when this hungry
        public float sleepHoursPerNight = 7f;
        public float fatiguePerWorkHour = 0.08f;
        public float fatigueRecoveryPerSleepHour = 0.16f;
    }

    /// <summary>Hunger and fatigue. Both 0..1 where 1 is full / rested. No regeneration without food or sleep.</summary>
    public sealed class Needs
    {
        private readonly SurvivalBalance _b;
        public float Hunger { get; private set; } = 0.85f;
        public float Rest { get; private set; } = 1f;

        public Needs(SurvivalBalance balance) { _b = balance ?? throw new ArgumentNullException(nameof(balance)); }

        public bool Starving => Hunger <= 0f;
        public bool Hungry => Hunger < _b.hungerStaminaPenaltyBelow;

        /// <summary>Advance by game hours; starvation drains the body.</summary>
        public void Tick(float gameHours, BodyState body)
        {
            if (gameHours <= 0f) return;
            float before = Hunger;
            Hunger = Math.Max(0f, Hunger - _b.hungerPerGameHour * gameHours);
            if (Hunger <= 0f && body != null)
            {
                float starvingHours = before <= 0f ? gameHours : Math.Max(0f, gameHours - before / _b.hungerPerGameHour);
                body.Drain(_b.starvationBloodLossPerHour * starvingHours, "hunger");
            }
        }

        public void Eat(float nutrition) { if (nutrition > 0f) Hunger = Math.Min(1f, Hunger + nutrition); }

        public void Work(float hours) { Rest = Math.Max(0f, Rest - _b.fatiguePerWorkHour * hours); }
        public void Sleep(float hours) { Rest = Math.Min(1f, Rest + _b.fatigueRecoveryPerSleepHour * hours); }

        public void Set(float hunger, float rest) { Hunger = Clamp01(hunger); Rest = Clamp01(rest); }
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}

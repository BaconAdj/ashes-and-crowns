using System;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Combat
{
    /// <summary>The player's body: wounds per part, blood as the life total, stamina. No regen in combat, ever.</summary>
    public sealed class PlayerVitals : MonoBehaviour
    {
        private Balance _balance;
        private DeterministicRng _rng;
        public BodyState Body { get; private set; }
        public float Stamina { get; private set; }
        public float StaminaMax => _balance != null ? _balance.player.staminaMax : 100f;
        public bool IsDead => Body != null && Body.IsDead;
        public string CauseOfDeath => Body?.CauseOfDeath;
        /// <summary>Hunger and sickness slow stamina recovery (set by the village systems).</summary>
        public float StaminaRegenScale = 1f;

        public event Action Died;
        public event Action<BodyPart, float> Wounded;

        public void Init(Balance balance, DeterministicRng rng)
        {
            _balance = balance; _rng = rng.Fork(11);
            Body = new BodyState(balance.wounds);
            Stamina = balance.player.staminaMax;
        }

        public bool TrySpendStamina(float amount)
        {
            if (Stamina < amount) return false;
            Stamina -= amount; return true;
        }

        /// <summary>The body died through a path other than TakeHit (hunger, sickness, sepsis).</summary>
        public void NotifyDeadFromBody() { if (Body != null && Body.IsDead && !_diedRaised) { _diedRaised = true; Died?.Invoke(); } }
        private bool _diedRaised;

        public void SetStamina(float value) => Stamina = Mathf.Clamp(value, 0f, StaminaMax);

        public void DrainStamina(float perSecond) => Stamina = Mathf.Max(0f, Stamina - perSecond * Time.deltaTime);

        /// <summary>A blow has landed on a known part.</summary>
        public void TakeHitAt(BodyPart part, float severity, string source)
        {
            if (Body == null || Body.IsDead) return;
            Body.ApplyHit(part, severity, source);
            Wounded?.Invoke(part, severity);
            if (Body.IsDead) NotifyDeadFromBody();
        }

        /// <summary>A blow has landed. Severity 0..1; location rolled from balance weights.</summary>
        public void TakeHit(float severity, string source)
        {
            if (Body == null || Body.IsDead) return;
            BodyPart part = BodyState.RollHitLocation(_rng, _balance.wounds);
            Body.ApplyHit(part, severity, source);
            Wounded?.Invoke(part, severity);
            if (Body.IsDead) NotifyDeadFromBody();
        }

        private void Update()
        {
            if (Body == null) return;
            bool wasDead = Body.IsDead;
            Body.Tick(Time.deltaTime);
            if (!wasDead && Body.IsDead) NotifyDeadFromBody();
            if (!Body.IsDead) Stamina = Mathf.Min(StaminaMax, Stamina + _balance.player.staminaRegenPerSecond * StaminaRegenScale * Time.deltaTime);
        }
    }
}

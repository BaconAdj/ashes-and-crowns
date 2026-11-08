using System;
using System.Collections.Generic;

namespace AshesAndCrowns.Sim
{
    public enum BodyPart : byte { Head = 0, Torso = 1, LeftArm = 2, RightArm = 3, LeftLeg = 4, RightLeg = 5 }

    public static class BodyParts
    {
        public const int Count = 6;
        public static string Name(BodyPart p)
        {
            switch (p)
            {
                case BodyPart.Head: return "head";
                case BodyPart.Torso: return "torso";
                case BodyPart.LeftArm: return "left arm";
                case BodyPart.RightArm: return "right arm";
                case BodyPart.LeftLeg: return "left leg";
                default: return "right leg";
            }
        }
    }

    public struct Wound
    {
        public BodyPart Part;
        public float Severity;   // 0..1 at infliction; drives bleeding and maiming
        public float Bleeding;   // current bleed rate contribution (severity that has not clotted)
        public float AgeSeconds;
        public bool Infected;
        public bool Treated;
    }

    public sealed class BodySnapshot
    {
        public float Blood = 1f;
        public float[] PartIntegrity = new float[BodyParts.Count];
        public Wound[] Wounds = new Wound[0];
        public string CauseOfDeath;
    }

    /// <summary>
    /// Per-body-part wound tracking. Blood (0..1) is the life total; it never regenerates in combat.
    /// Wounds persist, bleed, clot slowly, and can maim. Deterministic and allocation-free after construction.
    /// </summary>
    public sealed class BodyState
    {
        public const int MaxWounds = 32;

        private readonly float[] _partIntegrity = new float[BodyParts.Count];
        private readonly List<Wound> _wounds = new List<Wound>(MaxWounds);
        private readonly WoundBalance _balance;

        public float Blood { get; private set; } = 1f;
        public string CauseOfDeath { get; private set; }
        public bool IsDead => Blood <= 0f || _partIntegrity[(int)BodyPart.Head] <= 0f || _partIntegrity[(int)BodyPart.Torso] <= 0f;
        public IReadOnlyList<Wound> Wounds => _wounds;

        public BodyState(WoundBalance balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            for (int i = 0; i < BodyParts.Count; i++) _partIntegrity[i] = 1f;
        }

        public float PartIntegrity(BodyPart part) => _partIntegrity[(int)part];

        public BodySnapshot Export()
        {
            var s = new BodySnapshot { Blood = Blood, CauseOfDeath = CauseOfDeath, Wounds = _wounds.ToArray() };
            Array.Copy(_partIntegrity, s.PartIntegrity, BodyParts.Count);
            return s;
        }

        public void Import(BodySnapshot s)
        {
            Blood = s.Blood; CauseOfDeath = s.CauseOfDeath;
            for (int i = 0; i < BodyParts.Count; i++) _partIntegrity[i] = s.PartIntegrity != null && i < s.PartIntegrity.Length ? s.PartIntegrity[i] : 1f;
            _wounds.Clear();
            if (s.Wounds != null) for (int i = 0; i < s.Wounds.Length && i < MaxWounds; i++) _wounds.Add(s.Wounds[i]);
        }
        public bool IsMaimed(BodyPart part) => _partIntegrity[(int)part] <= 1f - _balance.maimThreshold;

        /// <summary>Pick a body part to hit using the configured weights.</summary>
        public static BodyPart RollHitLocation(DeterministicRng rng, WoundBalance balance) => (BodyPart)rng.WeightedIndex(balance.bodyPartHitWeights);

        /// <summary>
        /// Apply a hit. damage01 is the wound severity, 0..1, where 1 is a full-strength blow with no armour.
        /// </summary>
        public void ApplyHit(BodyPart part, float damage01, string source)
        {
            if (IsDead) return;
            if (damage01 <= 0f) return;
            if (damage01 > 1f) damage01 = 1f;
            int i = (int)part;
            float lethality = _balance.bodyPartLethality[i];
            _partIntegrity[i] = Math.Max(0f, _partIntegrity[i] - damage01);
            Blood = Math.Max(0f, Blood - damage01 * lethality * 0.5f);
            if (_wounds.Count < MaxWounds)
                _wounds.Add(new Wound { Part = part, Severity = damage01, Bleeding = damage01, AgeSeconds = 0f });
            if (IsDead && CauseOfDeath == null) CauseOfDeath = source + " (" + BodyParts.Name(part) + ")";
        }

        /// <summary>Advance bleeding and clotting by dt seconds. Never heals.</summary>
        public void Tick(float dt)
        {
            if (IsDead || dt <= 0f) return;
            float bleed = 0f;
            for (int w = 0; w < _wounds.Count; w++)
            {
                Wound wound = _wounds[w];
                wound.AgeSeconds += dt;
                if (wound.Bleeding > 0f && wound.Severity > 0f)
                {
                    float clotRate = 1f / (_balance.clotSecondsPerSeverity * Math.Max(wound.Severity, 0.05f));
                    wound.Bleeding = Math.Max(0f, wound.Bleeding - wound.Severity * clotRate * dt);
                }
                bleed += wound.Bleeding * _balance.bleedPerSecondPerSeverity;
                _wounds[w] = wound;
            }
            if (bleed > 0f)
            {
                Blood = Math.Max(0f, Blood - bleed * dt);
                if (IsDead && CauseOfDeath == null) CauseOfDeath = "bled out";
            }
        }

        /// <summary>Lose blood to something that is not a wound: starvation, sickness, infection.</summary>
        public void Drain(float amount, string cause)
        {
            if (IsDead || amount <= 0f) return;
            Blood = Math.Max(0f, Blood - amount);
            if (IsDead && CauseOfDeath == null) CauseOfDeath = cause;
        }

        /// <summary>Once a game day: open wounds may turn septic; septic wounds drain blood and may clear. Deterministic.</summary>
        public void TickDay(DeterministicRng rng)
        {
            if (IsDead) return;
            for (int w = 0; w < _wounds.Count; w++)
            {
                Wound wound = _wounds[w];
                float ageDays = wound.AgeSeconds / 86400f;
                if (wound.Infected)
                {
                    Drain(_balance.infectionBloodLossPerDay * Math.Max(0.3f, wound.Severity), "wound fever");
                    if (rng.Chance(_balance.infectionClearChancePerDay * (wound.Treated ? 2f : 1f))) { wound.Infected = false; wound.Severity *= 0.8f; }
                }
                else if (ageDays <= _balance.infectionWindowDays && wound.Severity >= 0.1f)
                {
                    float chance = _balance.infectionChancePerDayPerSeverity * wound.Severity * (wound.Treated ? _balance.treatedInfectionMultiplier : 1f);
                    if (rng.Chance(chance)) wound.Infected = true;
                }
                wound.AgeSeconds += 86400f;
                _wounds[w] = wound;
                if (IsDead) return;
            }
        }

        /// <summary>Clean and bind every wound. Treated wounds rarely infect.</summary>
        public int TreatAll()
        {
            int n = 0;
            for (int w = 0; w < _wounds.Count; w++) { Wound wound = _wounds[w]; if (!wound.Treated) { wound.Treated = true; wound.Bleeding *= 0.5f; n++; } _wounds[w] = wound; }
            return n;
        }

        public bool HasInfection { get { foreach (Wound w in _wounds) if (w.Infected) return true; return false; } }

        /// <summary>Slow recovery while resting, in game hours. Maimed parts never recover past the maim line.</summary>
        public void Rest(float gameHours)
        {
            if (IsDead || gameHours <= 0f) return;
            float heal = _balance.restHealPerHour * gameHours;
            Blood = Math.Min(1f, Blood + heal);
            for (int i = 0; i < BodyParts.Count; i++)
            {
                float cap = IsMaimed((BodyPart)i) ? 1f - _balance.maimThreshold : 1f;
                _partIntegrity[i] = Math.Min(cap, _partIntegrity[i] + heal);
            }
        }
    }
}

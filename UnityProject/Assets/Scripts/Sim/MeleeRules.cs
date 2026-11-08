using System;

namespace AshesAndCrowns.Sim
{
    public enum AttackDir : byte { High = 0, Left = 1, Right = 2, Low = 3 }
    public enum FightPhase : byte { Idle = 0, Windup = 1, Strike = 2, Recover = 3, Block = 4, Dodge = 5, Stagger = 6, Yielded = 7, Down = 8 }
    public enum HitOutcome : byte { Miss = 0, Dodged = 1, Blocked = 2, Parried = 3, Hit = 4, OutOfReach = 5 }

    [Serializable] public sealed class MeleeRulesBalance
    {
        public float windupSeconds = 0.32f, strikeSeconds = 0.12f, recoverSeconds = 0.45f;
        public float parryWindowSeconds = 0.14f;       // first part of a block is a parry
        public float blockStaminaCost = 10f, swingStaminaCost = 12f, dodgeStaminaCost = 16f, parryStaggerSeconds = 0.9f, hitStaggerSeconds = 0.35f;
        public float dodgeSeconds = 0.4f, dodgeInvulnerableSeconds = 0.25f;
        public float staminaRegenPerSecond = 9f, staminaRegenBlocking = 3f;
        public float exhaustedThreshold = 8f;         // below this you cannot swing
        public float yieldBloodBelow = 0.3f;          // NPCs may yield below this
        public float yieldChancePerHit = 0.5f;
        public float unarmedDamage = 6f, unarmedReach = 1.1f;
        public float spearReachBonus = 0.9f;
        public float headMultiplier = 1.6f, limbMultiplier = 0.7f;
        public float armourPerPoint = 0.01f;          // ItemDef.armour 55 -> 55% absorbed
        public float skillDamageWeight = 0.006f;      // +60% at skill 100
        public float skillParryWindowWeight = 0.0012f; // +0.12 s at skill 100
    }

    /// <summary>What a fighter carries: resolved from items once per fight.</summary>
    public struct Loadout
    {
        public float Damage, Reach, Variance;
        public float ArmourHead, ArmourTorso, ArmourLimbs; // 0..1 absorbed
        public bool HasShield;
        public string WeaponName;

        public static Loadout From(Inventory inv, ItemsData items, MeleeRulesBalance b)
        {
            var l = new Loadout { Damage = b.unarmedDamage, Reach = b.unarmedReach, Variance = 0.25f, WeaponName = "fists" };
            string[] weapons = { "sword", "axe", "spear", "hammer", "adze", "knife", "crook" };
            int bestDmg = 0; string best = null;
            foreach (string w in weapons) { var d = items.Find(w); if (d != null && inv.Count(w) > 0 && d.damage > bestDmg) { bestDmg = d.damage; best = w; } }
            if (best != null) { l.Damage = bestDmg; l.WeaponName = items.Find(best).name; l.Reach = best == "spear" ? b.unarmedReach + b.spearReachBonus + 0.4f : best == "knife" ? b.unarmedReach : b.unarmedReach + 0.7f; }
            float torso = 0f, head = 0f, limbs = 0f;
            if (inv.Count("gambeson") > 0) { torso += 0.2f; limbs += 0.15f; }
            if (inv.Count("mail") > 0) { torso += 0.55f; limbs += 0.4f; }
            if (inv.Count("helm") > 0) head += 0.5f;
            if (inv.Count("shield") > 0) l.HasShield = true;
            l.ArmourHead = Math.Min(0.9f, head); l.ArmourTorso = Math.Min(0.9f, torso); l.ArmourLimbs = Math.Min(0.9f, limbs);
            return l;
        }
    }

    /// <summary>
    /// One fighter's timing state. Pure and deterministic; the Game layer feeds inputs and distances and
    /// applies results to bodies. Both the player and NPC duellists run this.
    /// </summary>
    public sealed class FighterState
    {
        private readonly MeleeRulesBalance _b;
        public FightPhase Phase { get; private set; }
        public AttackDir Dir { get; private set; }         // direction of the current attack or guard
        public float PhaseTime { get; private set; }       // seconds into the phase
        public float Stamina { get; private set; }
        public float StaminaMax { get; }
        public float Skill;                                 // weapon skill 0..100 (set by the owner)
        public bool StrikeConsumed { get; private set; }   // the strike phase hits once

        public FighterState(MeleeRulesBalance balance, float staminaMax)
        {
            _b = balance ?? throw new ArgumentNullException(nameof(balance));
            StaminaMax = staminaMax; Stamina = staminaMax;
        }

        public bool CanAct => Phase == FightPhase.Idle || Phase == FightPhase.Block;
        public bool IsInvulnerable => Phase == FightPhase.Dodge && PhaseTime <= _b.dodgeInvulnerableSeconds;
        public bool InParryWindow => Phase == FightPhase.Block && PhaseTime <= _b.parryWindowSeconds + Skill * _b.skillParryWindowWeight;
        public bool Exhausted => Stamina < _b.exhaustedThreshold;

        public void SetStamina(float v) => Stamina = Math.Max(0f, Math.Min(StaminaMax, v));

        public bool TryAttack(AttackDir dir)
        {
            if (!CanAct || Stamina < _b.swingStaminaCost || Exhausted) return false;
            Stamina -= _b.swingStaminaCost;
            Phase = FightPhase.Windup; Dir = dir; PhaseTime = 0f; StrikeConsumed = false;
            return true;
        }

        public bool TryBlock(AttackDir dir)
        {
            if (Phase == FightPhase.Block) { Dir = dir; return true; }
            if (Phase != FightPhase.Idle) return false;
            Phase = FightPhase.Block; Dir = dir; PhaseTime = 0f;
            return true;
        }

        public void ReleaseBlock() { if (Phase == FightPhase.Block) { Phase = FightPhase.Idle; PhaseTime = 0f; } }

        public bool TryDodge()
        {
            if (!CanAct || Stamina < _b.dodgeStaminaCost) return false;
            Stamina -= _b.dodgeStaminaCost;
            Phase = FightPhase.Dodge; PhaseTime = 0f;
            return true;
        }

        public void Stagger(float seconds) { if (Phase == FightPhase.Down || Phase == FightPhase.Yielded) return; Phase = FightPhase.Stagger; PhaseTime = -seconds + _b.hitStaggerSeconds; }
        public void Yield() { Phase = FightPhase.Yielded; PhaseTime = 0f; }
        public void GoDown() { Phase = FightPhase.Down; PhaseTime = 0f; }
        public void Reset() { Phase = FightPhase.Idle; PhaseTime = 0f; Stamina = StaminaMax; }

        /// <summary>Advance time. Returns true on the frame the strike lands (the owner then resolves the hit).</summary>
        public bool Tick(float dt)
        {
            if (dt <= 0f) return false;
            PhaseTime += dt;
            bool strikes = false;
            switch (Phase)
            {
                case FightPhase.Windup: if (PhaseTime >= _b.windupSeconds) { Phase = FightPhase.Strike; PhaseTime = 0f; strikes = true; } break;
                case FightPhase.Strike: if (PhaseTime >= _b.strikeSeconds) { Phase = FightPhase.Recover; PhaseTime = 0f; } break;
                case FightPhase.Recover: if (PhaseTime >= _b.recoverSeconds) { Phase = FightPhase.Idle; PhaseTime = 0f; } break;
                case FightPhase.Dodge: if (PhaseTime >= _b.dodgeSeconds) { Phase = FightPhase.Idle; PhaseTime = 0f; } break;
                case FightPhase.Stagger: if (PhaseTime >= _b.hitStaggerSeconds) { Phase = FightPhase.Idle; PhaseTime = 0f; } break;
            }
            float regen = Phase == FightPhase.Block ? _b.staminaRegenBlocking : (Phase == FightPhase.Idle || Phase == FightPhase.Yielded ? _b.staminaRegenPerSecond : 0f);
            Stamina = Math.Min(StaminaMax, Stamina + regen * dt);
            return strikes;
        }

        public void ConsumeStrike() => StrikeConsumed = true;
    }

    public struct HitResult
    {
        public HitOutcome Outcome; public BodyPart Part; public float Severity; public float RawDamage;
    }

    public static class MeleeRules
    {
        /// <summary>Which body part a direction tends to find.</summary>
        public static BodyPart PartFor(AttackDir dir, DeterministicRng rng)
        {
            switch (dir)
            {
                case AttackDir.High: return rng.Chance(0.55f) ? BodyPart.Head : BodyPart.Torso;
                case AttackDir.Left: return rng.Chance(0.6f) ? BodyPart.RightArm : BodyPart.Torso;   // attacker's left lands on defender's right
                case AttackDir.Right: return rng.Chance(0.6f) ? BodyPart.LeftArm : BodyPart.Torso;
                default: return rng.Chance(0.5f) ? BodyPart.LeftLeg : BodyPart.RightLeg;
            }
        }

        /// <summary>Does a guard in blockDir stop an attack from attackDir? A shield also covers the side opposite the weapon.</summary>
        public static bool GuardCovers(AttackDir blockDir, AttackDir attackDir, bool shield)
        {
            if (blockDir == attackDir) return true;
            if (shield && (attackDir == AttackDir.Left || attackDir == AttackDir.Right) && (blockDir == AttackDir.Left || blockDir == AttackDir.Right)) return true;
            return false;
        }

        /// <summary>Resolve the attacker's landing strike against the defender. Deterministic for the rng.</summary>
        public static HitResult Resolve(FighterState attacker, Loadout aLoad, FighterState defender, Loadout dLoad, float distance, DeterministicRng rng, MeleeRulesBalance b)
        {
            var r = new HitResult { Outcome = HitOutcome.Miss };
            if (attacker.StrikeConsumed) return r;
            attacker.ConsumeStrike();
            if (distance > aLoad.Reach) { r.Outcome = HitOutcome.OutOfReach; return r; }
            if (defender.IsInvulnerable) { r.Outcome = HitOutcome.Dodged; return r; }
            if (defender.Phase == FightPhase.Block && GuardCovers(defender.Dir, attacker.Dir, dLoad.HasShield))
            {
                if (defender.InParryWindow) { r.Outcome = HitOutcome.Parried; attacker.Stagger(b.parryStaggerSeconds); return r; }
                float cost = b.blockStaminaCost * (dLoad.HasShield ? 0.6f : 1f);
                if (defender.Stamina >= cost) { defender.SetStamina(defender.Stamina - cost); r.Outcome = HitOutcome.Blocked; return r; }
                // Guard broken: the blow comes through weakened.
                defender.SetStamina(0f);
            }
            r.Part = PartFor(attacker.Dir, rng);
            float armour = r.Part == BodyPart.Head ? dLoad.ArmourHead : r.Part == BodyPart.Torso ? dLoad.ArmourTorso : dLoad.ArmourLimbs;
            float skillMul = 1f + attacker.Skill * b.skillDamageWeight;
            float partMul = r.Part == BodyPart.Head ? b.headMultiplier : (r.Part == BodyPart.Torso ? 1f : b.limbMultiplier);
            r.RawDamage = CombatMath.Damage(aLoad.Damage * skillMul * partMul, aLoad.Variance, rng.NextFloat(), armour);
            r.Severity = Math.Min(1f, r.RawDamage / 40f);   // 40 raw = a killing blow to the head
            r.Outcome = HitOutcome.Hit;
            defender.Stagger(b.hitStaggerSeconds);
            return r;
        }
    }

    /// <summary>A plain opponent brain: reads the player's windup with skill, guards, feints, tires, yields when hurt.</summary>
    public sealed class FighterBrain
    {
        private readonly DeterministicRng _rng;
        private float _think;
        public float Aggression = 0.5f, Read = 0.4f;   // Read: chance to guard the right line
        public bool WillYield = true;

        public FighterBrain(DeterministicRng rng, float aggression, float read) { _rng = rng; Aggression = aggression; Read = read; }

        /// <summary>Decide for this tick. opponentWindupDir is null unless the opponent is winding up.</summary>
        public void Think(FighterState me, AttackDir? opponentWindupDir, float distance, float myReach, float dt, MeleeRulesBalance b)
        {
            _think -= dt;
            if (me.Phase == FightPhase.Yielded || me.Phase == FightPhase.Down) return;
            if (opponentWindupDir.HasValue && me.CanAct)
            {
                if (_rng.Chance(Read)) { me.TryBlock(opponentWindupDir.Value); _think = 0.3f; return; }
                if (_rng.Chance(0.15f)) { me.TryDodge(); _think = 0.4f; return; }
            }
            if (_think > 0f) return;
            _think = _rng.Range(0.25f, 0.7f);
            if (me.Phase == FightPhase.Block && !opponentWindupDir.HasValue) me.ReleaseBlock();
            if (distance <= myReach && me.CanAct && !me.Exhausted && _rng.Chance(Aggression)) me.TryAttack((AttackDir)_rng.Range(0, 4));
        }

        public bool ConsiderYield(FighterState me, float blood, MeleeRulesBalance b) => WillYield && blood < b.yieldBloodBelow && _rng.Chance(b.yieldChancePerHit);
    }
}

using System;

namespace AshesAndCrowns.Sim
{
    /// <summary>Guard and attack lines. Three stances are used (high, left, right); Low is kept for old saves and the mass model.</summary>
    public enum AttackDir : byte { High = 0, Left = 1, Right = 2, Low = 3 }
    public enum AttackKind : byte { Light = 0, Heavy = 1 }
    public enum FightPhase : byte { Idle = 0, Windup = 1, Strike = 2, Recover = 3, Block = 4, Dodge = 5, Stagger = 6, Yielded = 7, Down = 8, GuardBreak = 9 }
    public enum HitOutcome : byte { Miss = 0, Dodged = 1, Blocked = 2, Parried = 3, Hit = 4, OutOfReach = 5 }
    public enum GuardBreakOutcome : byte { OutOfReach = 0, Broken = 1, Countered = 2, BeatenByAttack = 3, Dodged = 4 }
    /// <summary>What a tick of the state machine produced for the owner to act on.</summary>
    public enum FightEvent : byte { None = 0, Strike = 1, GuardBreakLands = 2 }

    [Serializable] public sealed class MeleeRulesBalance
    {
        // Attacks: a light is quick and cheap, a heavy is slow, hits hard, can be feinted and is the parry input.
        public float lightWindupSeconds = 0.30f, heavyWindupSeconds = 0.72f, strikeSeconds = 0.12f, lightRecoverSeconds = 0.40f, heavyRecoverSeconds = 0.60f;
        public float lightStaminaCost = 10f, heavyStaminaCost = 18f, lightDamageMul = 0.65f, heavyDamageMul = 1.25f;
        public float feintStaminaCost = 6f, feintRecoverSeconds = 0.22f, feintLatestSeconds = 0.14f;   // a heavy can be cancelled until this long before it lands
        // Guard: a persistent stance. Blocking costs stamina; a heavy started in the attacker's line just before the blow lands is a parry.
        public float parryWindowSeconds = 0.20f;
        public float blockStaminaCost = 10f, heavyBlockStaminaMul = 1.6f, parryStaggerSeconds = 1.0f, lightHitStaggerSeconds = 0.30f, heavyHitStaggerSeconds = 0.50f;
        // Guard break: beats a standing guard, loses to an attack, is countered by a guard break pressed as it lands.
        public float guardBreakSeconds = 0.35f, guardBreakStaminaCost = 8f, guardBreakStaggerSeconds = 1.1f, counterGuardBreakWindowSeconds = 0.30f, guardBreakReach = 1.4f;
        public float dodgeStaminaCost = 16f, dodgeSeconds = 0.4f, dodgeInvulnerableSeconds = 0.25f;
        public float staminaRegenPerSecond = 9f, staminaRegenGuarding = 5f;
        public float exhaustedThreshold = 8f;         // below this you cannot swing
        // Revenge: outnumbered and battered, you get a few seconds of fury.
        public float revengePerHitTaken = 0.22f, revengePerBlock = 0.12f, revengeSeconds = 8f, revengeDamageMul = 1.3f, revengeStaggerSeconds = 0.8f;
        public float yieldBloodBelow = 0.3f;          // NPCs may yield below this
        public float yieldChancePerHit = 0.5f;
        public float unarmedDamage = 6f, unarmedReach = 1.1f;
        public float spearReachBonus = 0.9f;
        public float headMultiplier = 1.6f, limbMultiplier = 0.7f;
        public float armourPerPoint = 0.01f;          // ItemDef.armour 55 -> 55% absorbed
        public float skillDamageWeight = 0.006f;      // +60% at skill 100
        public float skillParryWindowWeight = 0.0012f; // +0.12 s at skill 100

        public float WindupSeconds(AttackKind k) => k == AttackKind.Heavy ? heavyWindupSeconds : lightWindupSeconds;
        public float RecoverSeconds(AttackKind k) => k == AttackKind.Heavy ? heavyRecoverSeconds : lightRecoverSeconds;
        public float StaminaCost(AttackKind k) => k == AttackKind.Heavy ? heavyStaminaCost : lightStaminaCost;
        public float DamageMul(AttackKind k) => k == AttackKind.Heavy ? heavyDamageMul : lightDamageMul;
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
    /// One fighter's timing state, in the For Honor mould: a held guard stance, light and heavy attacks, feints,
    /// guard breaks, dodges and revenge. Pure and deterministic; the Game layer feeds inputs and distances and
    /// applies results to bodies. Both the player and NPC duellists run this.
    /// </summary>
    public sealed class FighterState
    {
        private readonly MeleeRulesBalance _b;
        public FightPhase Phase { get; private set; }
        public AttackDir Dir { get; private set; }         // line of the current attack
        public AttackKind Kind { get; private set; }       // light or heavy
        public AttackDir? GuardDir { get; private set; }   // the held stance; null when not in guard mode
        public float PhaseTime { get; private set; }       // seconds into the phase
        public float Stamina { get; private set; }
        public float StaminaMax { get; }
        public float Skill;                                 // weapon skill 0..100 (set by the owner)
        public bool StrikeConsumed { get; private set; }   // the strike phase hits once
        public bool Open { get; private set; }             // guard broken: the next blow cannot be blocked or parried
        public float Revenge { get; private set; }         // 0..1 meter
        public float RevengeTimeLeft { get; private set; }
        private float _stagger;                            // length of the current stagger
        private float _gbPressAge = 99f;                   // seconds since guard break was last pressed (counter window)

        public FighterState(MeleeRulesBalance balance, float staminaMax)
        {
            _b = balance ?? throw new ArgumentNullException(nameof(balance));
            StaminaMax = staminaMax; Stamina = staminaMax;
        }

        public bool CanAct => Phase == FightPhase.Idle;
        public bool IsGuarding => Phase == FightPhase.Idle && GuardDir.HasValue;
        public bool IsInvulnerable => Phase == FightPhase.Dodge && PhaseTime <= _b.dodgeInvulnerableSeconds;
        public bool IsAttacking => Phase == FightPhase.Windup || Phase == FightPhase.Strike;
        public bool Exhausted => Stamina < _b.exhaustedThreshold;
        public bool InRevenge => RevengeTimeLeft > 0f;
        public bool RevengeReady => Revenge >= 1f && !InRevenge;
        public float ParryWindow => _b.parryWindowSeconds + Skill * _b.skillParryWindowWeight;
        /// <summary>A heavy begun in this line within the parry window counts as a parry when the blow lands.</summary>
        public bool InParryWindow(AttackDir incoming) => Phase == FightPhase.Windup && Kind == AttackKind.Heavy && Dir == incoming && PhaseTime <= ParryWindow;
        public bool CounterGuardBreakArmed => _gbPressAge <= _b.counterGuardBreakWindowSeconds;

        public void SetStamina(float v) => Stamina = Math.Max(0f, Math.Min(StaminaMax, v));

        /// <summary>Enter or change the guard stance; null leaves guard mode. Stance survives attacks and recovers.</summary>
        public void SetGuard(AttackDir? dir) { GuardDir = dir == AttackDir.Low ? AttackDir.High : dir; }

        public bool TryAttack(AttackDir dir, AttackKind kind = AttackKind.Light)
        {
            float cost = _b.StaminaCost(kind);
            if (!CanAct || Stamina < cost || Exhausted) return false;
            Stamina -= cost;
            Phase = FightPhase.Windup; Dir = dir; Kind = kind; PhaseTime = 0f; StrikeConsumed = false;
            return true;
        }

        /// <summary>Cancel a heavy before it lands. Costs a little stamina; the recovery is short.</summary>
        public bool TryFeint()
        {
            if (Phase != FightPhase.Windup || Kind != AttackKind.Heavy || PhaseTime > _b.heavyWindupSeconds - _b.feintLatestSeconds) return false;
            Stamina = Math.Max(0f, Stamina - _b.feintStaminaCost);
            Phase = FightPhase.Recover; PhaseTime = _b.RecoverSeconds(Kind) - _b.feintRecoverSeconds; StrikeConsumed = true;
            return true;
        }

        /// <summary>Press guard break: starts one when free, and arms the counter window either way.</summary>
        public bool TryGuardBreak()
        {
            _gbPressAge = 0f;
            if (!CanAct || Stamina < _b.guardBreakStaminaCost) return false;
            Stamina -= _b.guardBreakStaminaCost;
            Phase = FightPhase.GuardBreak; PhaseTime = 0f;
            return true;
        }

        public bool TryDodge()
        {
            if (!CanAct || Stamina < _b.dodgeStaminaCost) return false;
            Stamina -= _b.dodgeStaminaCost;
            Phase = FightPhase.Dodge; PhaseTime = 0f;
            return true;
        }

        public bool TryRevenge()
        {
            if (!RevengeReady || Phase == FightPhase.Down || Phase == FightPhase.Yielded) return false;
            Revenge = 0f; RevengeTimeLeft = _b.revengeSeconds; Stamina = StaminaMax;
            if (Phase == FightPhase.Stagger) { Phase = FightPhase.Idle; PhaseTime = 0f; }
            Open = false;
            return true;
        }

        public void AddRevenge(float amount) { if (!InRevenge) Revenge = Math.Min(1f, Revenge + amount); }

        public void Stagger(float seconds)
        {
            if (Phase == FightPhase.Down || Phase == FightPhase.Yielded) return;
            Phase = FightPhase.Stagger; PhaseTime = 0f; _stagger = seconds; StrikeConsumed = true;
        }
        /// <summary>Guard broken: staggered and open to the next blow.</summary>
        public void BreakGuard() { Stagger(_b.guardBreakStaggerSeconds); Open = true; }
        public void EndAction() { if (Phase != FightPhase.Down && Phase != FightPhase.Yielded) { Phase = FightPhase.Idle; PhaseTime = 0f; } }
        public void Yield() { Phase = FightPhase.Yielded; PhaseTime = 0f; }
        public void GoDown() { Phase = FightPhase.Down; PhaseTime = 0f; }
        public void Reset() { Phase = FightPhase.Idle; PhaseTime = 0f; Stamina = StaminaMax; Open = false; Revenge = 0f; RevengeTimeLeft = 0f; GuardDir = null; _gbPressAge = 99f; }

        /// <summary>Advance time. Returns the event the owner must resolve this frame, if any.</summary>
        public FightEvent Tick(float dt)
        {
            if (dt <= 0f) return FightEvent.None;
            PhaseTime += dt; _gbPressAge += dt;
            if (RevengeTimeLeft > 0f) RevengeTimeLeft = Math.Max(0f, RevengeTimeLeft - dt);
            FightEvent ev = FightEvent.None;
            switch (Phase)
            {
                case FightPhase.Windup: if (PhaseTime >= _b.WindupSeconds(Kind)) { Phase = FightPhase.Strike; PhaseTime = 0f; ev = FightEvent.Strike; } break;
                case FightPhase.Strike: if (PhaseTime >= _b.strikeSeconds) { Phase = FightPhase.Recover; PhaseTime = 0f; } break;
                case FightPhase.Recover: if (PhaseTime >= _b.RecoverSeconds(Kind)) { Phase = FightPhase.Idle; PhaseTime = 0f; } break;
                case FightPhase.GuardBreak: if (PhaseTime >= _b.guardBreakSeconds) { Phase = FightPhase.Recover; Kind = AttackKind.Light; PhaseTime = 0f; ev = FightEvent.GuardBreakLands; } break;
                case FightPhase.Dodge: if (PhaseTime >= _b.dodgeSeconds) { Phase = FightPhase.Idle; PhaseTime = 0f; } break;
                case FightPhase.Stagger: if (PhaseTime >= _stagger) { Phase = FightPhase.Idle; PhaseTime = 0f; Open = false; } break;
            }
            float regen = Phase == FightPhase.Idle ? (GuardDir.HasValue ? _b.staminaRegenGuarding : _b.staminaRegenPerSecond) : (Phase == FightPhase.Yielded ? _b.staminaRegenPerSecond : 0f);
            Stamina = Math.Min(StaminaMax, Stamina + regen * dt);
            return ev;
        }

        public void ConsumeStrike() => StrikeConsumed = true;
        /// <summary>The next blow that lands finds an open guard (set after a hit resolves).</summary>
        public void CloseGuard() => Open = false;
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
            if (attackDir == AttackDir.Low && blockDir != AttackDir.High) return true;   // the mass model still swings low; a side guard takes it
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
            if (!defender.Open)
            {
                if (defender.InParryWindow(attacker.Dir))
                {
                    r.Outcome = HitOutcome.Parried;
                    attacker.Stagger(b.parryStaggerSeconds);
                    defender.EndAction();                                   // the parry replaces the heavy; the defender is free to punish
                    defender.AddRevenge(b.revengePerBlock);
                    return r;
                }
                if (defender.IsGuarding && GuardCovers(defender.GuardDir.Value, attacker.Dir, dLoad.HasShield))
                {
                    float cost = b.blockStaminaCost * (dLoad.HasShield ? 0.6f : 1f) * (attacker.Kind == AttackKind.Heavy ? b.heavyBlockStaminaMul : 1f);
                    if (defender.Stamina >= cost) { defender.SetStamina(defender.Stamina - cost); defender.AddRevenge(b.revengePerBlock); r.Outcome = HitOutcome.Blocked; return r; }
                    defender.SetStamina(0f);                                // guard crushed: the blow comes through
                }
            }
            r.Part = PartFor(attacker.Dir, rng);
            float armour = r.Part == BodyPart.Head ? dLoad.ArmourHead : r.Part == BodyPart.Torso ? dLoad.ArmourTorso : dLoad.ArmourLimbs;
            float skillMul = 1f + attacker.Skill * b.skillDamageWeight;
            float partMul = r.Part == BodyPart.Head ? b.headMultiplier : (r.Part == BodyPart.Torso ? 1f : b.limbMultiplier);
            float kindMul = b.DamageMul(attacker.Kind) * (attacker.InRevenge ? b.revengeDamageMul : 1f);
            r.RawDamage = CombatMath.Damage(aLoad.Damage * skillMul * partMul * kindMul, aLoad.Variance, rng.NextFloat(), armour);
            r.Severity = Math.Min(1f, r.RawDamage / 40f);   // 40 raw = a killing blow to the head
            r.Outcome = HitOutcome.Hit;
            bool hyperArmour = defender.InRevenge && attacker.Kind == AttackKind.Light;
            if (!hyperArmour) defender.Stagger(attacker.Kind == AttackKind.Heavy ? b.heavyHitStaggerSeconds : b.lightHitStaggerSeconds);
            defender.CloseGuard();
            defender.AddRevenge(b.revengePerHitTaken);
            return r;
        }

        /// <summary>Resolve a landing guard break. Attacks beat it, a timely press counters it, a standing guard loses to it.</summary>
        public static GuardBreakOutcome ResolveGuardBreak(FighterState attacker, FighterState defender, float distance, MeleeRulesBalance b)
        {
            if (distance > b.guardBreakReach) return GuardBreakOutcome.OutOfReach;
            if (defender.IsInvulnerable || defender.Phase == FightPhase.Dodge) return GuardBreakOutcome.Dodged;
            if (defender.IsAttacking) { attacker.Stagger(b.lightHitStaggerSeconds); return GuardBreakOutcome.BeatenByAttack; }
            if (defender.CounterGuardBreakArmed) { attacker.Stagger(b.lightHitStaggerSeconds); defender.EndAction(); return GuardBreakOutcome.Countered; }
            defender.BreakGuard();
            return GuardBreakOutcome.Broken;
        }
    }

    /// <summary>A plain opponent brain: holds a stance, reads the player's windups with skill, mixes lights and heavies, feints, breaks a patient guard, tires, yields when hurt.</summary>
    public sealed class FighterBrain
    {
        private readonly DeterministicRng _rng;
        private float _think, _opponentGuardTime;
        public float Aggression = 0.5f, Read = 0.4f;   // Read: chance to guard the right line
        public bool WillYield = true;

        public FighterBrain(DeterministicRng rng, float aggression, float read) { _rng = rng; Aggression = aggression; Read = read; }

        /// <summary>Decide for this tick. opponentWindupDir is null unless the opponent is winding up; opponentGuarding says they hold a stance.</summary>
        public void Think(FighterState me, AttackDir? opponentWindupDir, AttackKind opponentKind, bool opponentGuarding, float distance, float myReach, float dt, MeleeRulesBalance b)
        {
            _think -= dt;
            if (me.Phase == FightPhase.Yielded || me.Phase == FightPhase.Down) return;
            _opponentGuardTime = opponentGuarding && !opponentWindupDir.HasValue ? _opponentGuardTime + dt : 0f;
            if (me.RevengeReady) { me.TryRevenge(); return; }
            if (opponentWindupDir.HasValue && me.CanAct)
            {
                if (_rng.Chance(Read))
                {
                    // Read it: a well-read heavy is parried with a heavy of our own, anything else is guarded.
                    if (opponentKind == AttackKind.Heavy && _rng.Chance(Read * 0.5f)) me.TryAttack(opponentWindupDir.Value, AttackKind.Heavy);
                    else me.SetGuard(opponentWindupDir.Value);
                    _think = 0.3f; return;
                }
                if (_rng.Chance(0.12f)) { me.TryDodge(); _think = 0.4f; return; }
            }
            if (_think > 0f) return;
            _think = _rng.Range(0.25f, 0.7f);
            if (!me.GuardDir.HasValue) me.SetGuard((AttackDir)_rng.Range(0, 3));
            if (distance > myReach || !me.CanAct || me.Exhausted) return;
            if (opponentGuarding && _opponentGuardTime > 1.2f && _rng.Chance(0.5f)) { me.TryGuardBreak(); _opponentGuardTime = 0f; return; }
            if (_rng.Chance(Aggression))
            {
                AttackKind kind = _rng.Chance(0.35f) ? AttackKind.Heavy : AttackKind.Light;
                me.TryAttack((AttackDir)_rng.Range(0, 3), kind);
            }
        }

        /// <summary>Mid-heavy: a cunning fighter sometimes pulls the blow when the opponent commits to a guard.</summary>
        public bool ConsiderFeint(FighterState me, bool opponentGuardMatches) => me.Phase == FightPhase.Windup && me.Kind == AttackKind.Heavy && opponentGuardMatches && _rng.Chance(Read * 0.02f);

        public bool ConsiderYield(FighterState me, float blood, MeleeRulesBalance b) => WillYield && blood < b.yieldBloodBelow && _rng.Chance(b.yieldChancePerHit);
    }
}

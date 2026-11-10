using System.Collections.Generic;
using AshesAndCrowns.Battle;
using AshesAndCrowns.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Combat
{
    /// <summary>
    /// The player's melee, on the shared FighterState, in the For Honor mould.
    /// Hold Ctrl for guard mode: you lock on to the nearest opponent, your stance follows the mouse (push up, left
    /// or right), LMB is a light attack, RMB a heavy, E a guard break (or a feint while a heavy winds up),
    /// Space a dodge, R revenge when the meter is full. Outside guard mode the keys do their village jobs and the
    /// weapon still swings (LMB) for the mass battle. Blows resolve against duellists (one on one) and against the
    /// battle's soldiers (the mass model). Every hit taken is a persistent wound.
    /// </summary>
    public sealed class MeleeController : MonoBehaviour
    {
        public BattleDirector director;   // optional
        public PlayerVitals vitals;
        public Transform weaponVisual;
        public Transform facingSource;
        public bool mounted;              // set by the mode manager / scene: no dodge in the saddle
        public float lockRange = 9f;      // metres: furthest opponent guard mode will lock to
        public float stanceMouseThreshold = 18f; // mouse travel (pixels) that changes the stance

        private Balance _balance;
        private MeleeRulesBalance _b;
        private DeterministicRng _rng;
        private FighterState _fighter;
        private Loadout _loadout;
        private readonly List<Duelist> _duelists = new List<Duelist>(8);
        private Vector2 _mouseAccum;
        private AttackDir _stance = AttackDir.High;
        private int _kills, _blocked, _parries;
        private float _lastHitFlash = -100f;
        private string _lastEvent = string.Empty; private float _lastEventUntil;
        private Duelist _lock;
        private AttackDir? _incomingDir; private AttackKind _incomingKind; private float _incomingUntil;

        public FighterState Fighter => _fighter;
        public Loadout Loadout => _loadout;
        public int Kills => _kills;
        public int Blocked => _blocked;
        public int Parries => _parries;
        public float TimeSinceHitTaken => Time.time - _lastHitFlash;
        /// <summary>Ctrl is held: stance, lock-on and the fighting keys are live.</summary>
        public bool GuardMode { get; private set; }
        public AttackDir Stance => _stance;
        public bool IsBlocking => _fighter != null && _fighter.IsGuarding;
        /// <summary>The opponent guard mode is locked to, or null. The camera and the body face it.</summary>
        public Transform LockTarget => _lock != null && !_lock.Out ? _lock.transform : null;
        /// <summary>An opponent's attack is winding up in this line (shown on the HUD for a moment).</summary>
        public AttackDir? IncomingDir => Time.time < _incomingUntil ? _incomingDir : null;
        public AttackKind IncomingKind => _incomingKind;
        public string LastEvent => Time.time < _lastEventUntil ? _lastEvent : string.Empty;
        public IReadOnlyList<Duelist> Duelists => _duelists;
        public AttackDir? WindupDir => _fighter != null && _fighter.Phase == FightPhase.Windup ? _fighter.Dir : (AttackDir?)null;
        public event System.Action Swung;
        public event System.Action<Duelist> DuelistDowned;   // killed or yielded
        public event System.Action RevengeStarted;

        public void Init(Balance balance, DeterministicRng rng)
        {
            _balance = balance; _b = balance.duel; _rng = rng.Fork(13);
            _fighter = new FighterState(_b, balance.player.staminaMax);
            vitals.ExternalStamina = true;
            RefreshLoadout();
            if (director != null) { director.EnemyStruckPlayer += OnSoldierStrikes; director.PlayerKilledSoldier += _ => _kills++; }
        }

        /// <summary>Re-read weapon and armour from the inventory (after buying, looting, dressing).</summary>
        public void RefreshLoadout()
        {
            var ctx = Game_Context();
            _loadout = ctx.HasValue ? Loadout.From(ctx.Value.inventory, ctx.Value.items, _b) : new Loadout { Damage = _balance.melee.baseDamage, Reach = _balance.melee.reach, Variance = _balance.melee.damageVariance, WeaponName = "sword" };
            if (_fighter != null) _fighter.Skill = ctx.HasValue ? ctx.Value.skill : 20f;
        }

        // The Combat assembly cannot see Game.RunSession, so the Game layer pushes the inventory in.
        public static System.Func<(Inventory inventory, ItemsData items, float skill)?> ContextProvider;
        private static (Inventory inventory, ItemsData items, float skill)? Game_Context() => ContextProvider?.Invoke();

        public void RegisterDuelist(Duelist d) { if (!_duelists.Contains(d)) { _duelists.Add(d); d.target = facingSource; } }
        public void UnregisterDuelist(Duelist d) { _duelists.Remove(d); if (_lock == d) _lock = null; }

        private void OnDestroy()
        {
            if (director != null) director.EnemyStruckPlayer -= OnSoldierStrikes;
        }

        private void OnDisable() { GuardMode = false; _lock = null; if (_fighter != null) _fighter.SetGuard(null); }

        private void Update()
        {
            if (_balance == null || vitals == null || vitals.IsDead) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            float dt = Time.deltaTime;

            // ---- guard mode: Ctrl held, cursor locked (not in a menu)
            bool wantGuard = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && Cursor.lockState == CursorLockMode.Locked;
            if (wantGuard != GuardMode)
            {
                GuardMode = wantGuard;
                if (GuardMode) { _mouseAccum = Vector2.zero; _fighter.SetGuard(_stance); }
                else { _fighter.SetGuard(null); _lock = null; }
            }
            if (GuardMode)
            {
                if (mouse != null) ReadStance(mouse.delta.ReadValue());
                PickLock();
                bool heavyWinding = _fighter.Phase == FightPhase.Windup && _fighter.Kind == AttackKind.Heavy;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && _fighter.TryAttack(_stance, AttackKind.Light)) Swung?.Invoke();
                if (mouse != null && mouse.rightButton.wasPressedThisFrame && _fighter.TryAttack(_stance, AttackKind.Heavy)) Swung?.Invoke();
                bool gbKey = (kb != null && kb.eKey.wasPressedThisFrame) || (mouse != null && mouse.middleButton.wasPressedThisFrame);
                if (gbKey)
                {
                    if (heavyWinding) { if (_fighter.TryFeint()) Note("feint"); }
                    else if (_fighter.TryGuardBreak()) Note("guard break");
                }
                if (kb != null && kb.spaceKey.wasPressedThisFrame && !mounted && _fighter.TryDodge()) Note("dodge");
                if (kb != null && kb.rKey.wasPressedThisFrame && _fighter.TryRevenge()) { Note("REVENGE"); RevengeStarted?.Invoke(); StaggerAllNear(); }
            }
            else
            {
                // Out of guard mode: a plain swing for the press of battle; the stance stays where the mouse last put it.
                bool locked = Cursor.lockState == CursorLockMode.Locked;   // never swing at a menu button
                if (locked && mouse != null && mouse.leftButton.wasPressedThisFrame && _fighter.TryAttack(_stance, AttackKind.Light)) Swung?.Invoke();
                if (locked && kb != null && kb.spaceKey.wasPressedThisFrame && !mounted && _duelists.Count > 0 && _fighter.TryDodge()) Note("dodge");
            }

            _fighter.SetStamina(vitals.Stamina);            // honour sprint drain and anything else that touched it
            float pre = _fighter.Stamina;
            FightEvent ev = _fighter.Tick(dt);
            if (_fighter.Stamina > pre) _fighter.SetStamina(pre + (_fighter.Stamina - pre) * vitals.StaminaRegenScale); // hunger and sickness slow recovery
            if (ev == FightEvent.Strike) Strike();
            else if (ev == FightEvent.GuardBreakLands) LandGuardBreak();

            // Duellists act against us.
            bool guarding = _fighter.IsGuarding;
            for (int i = _duelists.Count - 1; i >= 0; i--)
            {
                Duelist d = _duelists[i];
                if (d == null) { _duelists.RemoveAt(i); continue; }
                bool wasOut = d.Out;
                if (d.Fighter.Phase == FightPhase.Windup && !d.Out) { _incomingDir = d.Fighter.Dir; _incomingKind = d.Fighter.Kind; _incomingUntil = Time.time + 0.15f; }
                FightEvent dev = d.Step(dt, WindupDir, _fighter.Kind, guarding, _fighter.GuardDir);
                if (dev == FightEvent.Strike)
                {
                    HitResult r = MeleeRules.Resolve(d.Fighter, d.Loadout, _fighter, _loadout, d.DistanceToTarget, _rng, _b);
                    ApplyIncoming(r, d.displayName + "'s " + d.Loadout.WeaponName.ToLowerInvariant());
                }
                else if (dev == FightEvent.GuardBreakLands)
                {
                    GuardBreakOutcome o = MeleeRules.ResolveGuardBreak(d.Fighter, _fighter, d.DistanceToTarget, _b);
                    Note(o == GuardBreakOutcome.Broken ? "your guard is broken!" : o == GuardBreakOutcome.Countered ? "countered" : o == GuardBreakOutcome.BeatenByAttack ? "you beat his grab" : "he grabs at air");
                }
                if (!wasOut && d.Out) DuelistDowned?.Invoke(d);
            }
            vitals.SetStamina(_fighter.Stamina);
            AnimateWeapon();
        }

        /// <summary>Mouse travel picks the stance: up is high, sideways is left or right. Down is ignored (no low guard).</summary>
        private void ReadStance(Vector2 delta)
        {
            _mouseAccum += delta;
            if (_mouseAccum.magnitude < stanceMouseThreshold) return;
            AttackDir wanted = _stance;
            if (Mathf.Abs(_mouseAccum.x) > Mathf.Abs(_mouseAccum.y)) wanted = _mouseAccum.x < 0f ? AttackDir.Left : AttackDir.Right;
            else if (_mouseAccum.y > 0f) wanted = AttackDir.High;
            _mouseAccum = Vector2.zero;
            if (wanted != _stance) { _stance = wanted; _fighter.SetGuard(_stance); }
        }

        private void PickLock()
        {
            if (_lock != null && !_lock.Out && Flat(_lock.transform.position - facingSource.position).magnitude <= lockRange * 1.3f) return;
            _lock = null; float best = lockRange;
            foreach (Duelist d in _duelists)
            {
                if (d == null || d.Out) continue;
                float dist = Flat(d.transform.position - facingSource.position).magnitude;
                if (dist < best) { best = dist; _lock = d; }
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        private void Strike()
        {
            bool any = false;
            // The locked opponent first, then anyone in the arc.
            if (_lock != null && TryStrike(_lock)) any = true;
            if (!any)
                foreach (Duelist d in _duelists) { if (d != null && d != _lock && TryStrike(d)) { any = true; break; } }
            if (!any && director != null && director.IsReady)
            {
                float dmg = CombatMath.Damage(_loadout.Damage * (1f + _fighter.Skill * _b.skillDamageWeight) * _b.DamageMul(_fighter.Kind) * (_fighter.InRevenge ? _b.revengeDamageMul : 1f), _loadout.Variance, _rng.NextFloat(), 0f);
                int hits = director.StrikeArc(facingSource.position, facingSource.forward, _loadout.Reach, _balance.melee.arcDegrees, dmg, Side.Player);
                if (hits > 0) Note("hit");
            }
        }

        private bool TryStrike(Duelist d)
        {
            if (d.Dead) return false;
            Vector3 to = d.transform.position - facingSource.position;
            if (!CombatMath.InArc(to.x, to.z, facingSource.forward.x, facingSource.forward.z, _balance.melee.arcDegrees)) return false;
            float dist = new Vector2(to.x, to.z).magnitude;
            if (dist > _loadout.Reach) return false;
            if (d.Yielded) { KillTheYielded(d); return true; }
            HitResult r = MeleeRules.Resolve(_fighter, _loadout, d.Fighter, d.Loadout, dist, _rng, _b);
            _fighter.ConsumeStrike(); // one target per swing; StrikeConsumed stays set
            if (r.Outcome == HitOutcome.Hit) { d.TakeHit(r, "your " + _loadout.WeaponName.ToLowerInvariant()); Note(r.Part == BodyPart.Head ? "a blow to the head" : "hit"); if (d.Dead) _kills++; }
            else Note(r.Outcome == HitOutcome.Parried ? "parried!" : r.Outcome == HitOutcome.Blocked ? "blocked" : r.Outcome == HitOutcome.Dodged ? "he slips it" : "short");
            return true;
        }

        private void LandGuardBreak()
        {
            Duelist d = _lock;
            if (d == null || d.Out) { foreach (Duelist x in _duelists) if (x != null && !x.Out) { d = x; break; } }
            if (d == null) { Note("you grab at air"); return; }
            GuardBreakOutcome o = MeleeRules.ResolveGuardBreak(_fighter, d.Fighter, d.DistanceToTarget, _b);
            Note(o == GuardBreakOutcome.Broken ? "his guard is broken: strike!" : o == GuardBreakOutcome.Countered ? "he counters your grab" : o == GuardBreakOutcome.BeatenByAttack ? "his blow beats your grab" : "short");
        }

        private void StaggerAllNear()
        {
            foreach (Duelist d in _duelists) if (d != null && !d.Out && d.DistanceToTarget <= _b.guardBreakReach + 0.6f) d.Fighter.Stagger(_b.revengeStaggerSeconds);
        }

        private void KillTheYielded(Duelist d)
        {
            d.Body.ApplyHit(BodyPart.Head, 1f, "your " + _loadout.WeaponName.ToLowerInvariant());
            d.Die(); _kills++;
            Note("you kill a man who had yielded");
            OnKilledSurrendered?.Invoke();
        }

        public event System.Action OnKilledSurrendered;

        /// <summary>A battle soldier's blow: same rules, a guessed line, always a light.</summary>
        private void OnSoldierStrikes(int soldierIndex)
        {
            if (vitals.IsDead) return;
            Unity.Mathematics.float3 sp = director.SoldierPosition(soldierIndex);
            float dist = Vector3.Distance(new Vector3(sp.x, 0f, sp.z), new Vector3(facingSource.position.x, 0f, facingSource.position.z));
            var attacker = new FighterState(_b, 50f) { Skill = 25f };
            attacker.TryAttack((AttackDir)_rng.Range(0, 3), AttackKind.Light);
            while (attacker.Tick(0.05f) != FightEvent.Strike) { }
            var soldierLoad = new Loadout { Damage = _balance.battle.soldierBaseDamage, Reach = _balance.melee.enemyReach, Variance = _balance.battle.soldierDamageVariance, WeaponName = "spear" };
            HitResult r = MeleeRules.Resolve(attacker, soldierLoad, _fighter, _loadout, dist, _rng, _b);
            ApplyIncoming(r, "a soldier's spear");
        }

        private void ApplyIncoming(HitResult r, string source)
        {
            switch (r.Outcome)
            {
                case HitOutcome.Blocked: _blocked++; Note("blocked"); break;
                case HitOutcome.Parried: _parries++; Note("parried!"); break;
                case HitOutcome.Dodged: Note("missed you"); break;
                case HitOutcome.Hit: vitals.TakeHitAt(r.Part, r.Severity, source); _lastHitFlash = Time.time; Note(r.Part == BodyPart.Head ? "your head rings" : "you are hit"); break;
            }
        }

        private void Note(string s) { _lastEvent = s; _lastEventUntil = Time.time + 1.2f; }

        private void AnimateWeapon()
        {
            if (weaponVisual == null) return;
            float x = 0f, y = 20f, z = 0f;
            AttackDir pose = _fighter.IsAttacking ? _fighter.Dir : _stance;
            switch (_fighter.Phase)
            {
                case FightPhase.Windup:
                    bool heavy = _fighter.Kind == AttackKind.Heavy;
                    y = heavy ? -95f : -70f; x = pose == AttackDir.High ? (heavy ? -70f : -50f) : 0f; z = pose == AttackDir.Left ? 40f : pose == AttackDir.Right ? -40f : 0f; break;
                case FightPhase.Strike: y = 90f; x = pose == AttackDir.High ? 40f : 0f; z = pose == AttackDir.Left ? -20f : pose == AttackDir.Right ? 20f : 0f; break;
                case FightPhase.Recover: y = 60f; break;
                case FightPhase.GuardBreak: x = 20f; y = 10f; z = 0f; break;
                case FightPhase.Stagger: x = 30f; y = -20f; break;
                default:
                    if (GuardMode) { x = pose == AttackDir.High ? -80f : -50f; z = pose == AttackDir.Left ? 90f : pose == AttackDir.Right ? 50f : 70f; y = -20f; }
                    break;
            }
            weaponVisual.localRotation = Quaternion.Euler(x, y, z);
        }
    }
}

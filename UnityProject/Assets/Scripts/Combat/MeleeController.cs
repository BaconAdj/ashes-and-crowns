using System.Collections.Generic;
using AshesAndCrowns.Battle;
using AshesAndCrowns.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Combat
{
    /// <summary>
    /// The player's melee, on the shared FighterState. Attack direction comes from the mouse's motion as you press
    /// (up: high, down: low, sideways: left or right); hold RMB to guard in the mouse's direction, the first instant
    /// of a guard is a parry; Space dodges. Blows resolve against duellists (one on one) and against the battle's
    /// soldiers (the mass model). Every hit taken is a persistent wound.
    /// </summary>
    public sealed class MeleeController : MonoBehaviour
    {
        public BattleDirector director;   // optional
        public PlayerVitals vitals;
        public Transform weaponVisual;
        public Transform facingSource;
        public bool mounted;              // set by the mode manager / scene: no dodge in the saddle

        private Balance _balance;
        private MeleeRulesBalance _b;
        private DeterministicRng _rng;
        private FighterState _fighter;
        private Loadout _loadout;
        private readonly List<Duelist> _duelists = new List<Duelist>(8);
        private Vector2 _mouseAccum;
        private int _kills, _blocked, _parries;
        private float _lastHitFlash = -100f;
        private string _lastEvent = string.Empty; private float _lastEventUntil;
        private float _soldierBlockLine; // soldiers attack from a pseudo-random line

        public FighterState Fighter => _fighter;
        public Loadout Loadout => _loadout;
        public int Kills => _kills;
        public int Blocked => _blocked;
        public int Parries => _parries;
        public float TimeSinceHitTaken => Time.time - _lastHitFlash;
        public bool IsBlocking => _fighter != null && _fighter.Phase == FightPhase.Block;
        public string LastEvent => Time.time < _lastEventUntil ? _lastEvent : string.Empty;
        public IReadOnlyList<Duelist> Duelists => _duelists;
        public AttackDir? WindupDir => _fighter != null && _fighter.Phase == FightPhase.Windup ? _fighter.Dir : (AttackDir?)null;
        public event System.Action Swung;
        public event System.Action<Duelist> DuelistDowned;   // killed or yielded

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
        public void UnregisterDuelist(Duelist d) => _duelists.Remove(d);

        private void OnDestroy()
        {
            if (director != null) director.EnemyStruckPlayer -= OnSoldierStrikes;
        }

        private void Update()
        {
            if (_balance == null || vitals == null || vitals.IsDead) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            float dt = Time.deltaTime;
            if (mouse != null) { Vector2 d = mouse.delta.ReadValue(); _mouseAccum = _mouseAccum * 0.8f + d; }
            AttackDir dir = DirFromMouse();

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && _fighter.TryAttack(dir)) { Swung?.Invoke(); }
            if (mouse != null && mouse.rightButton.isPressed) _fighter.TryBlock(dir); else _fighter.ReleaseBlock();
            if (kb != null && kb.spaceKey.wasPressedThisFrame && !mounted && _fighter.TryDodge()) Note("dodge");

            _fighter.SetStamina(vitals.Stamina);            // honour sprint drain and anything else that touched it
            float pre = _fighter.Stamina;
            bool strikes = _fighter.Tick(dt);
            if (_fighter.Stamina > pre) _fighter.SetStamina(pre + (_fighter.Stamina - pre) * vitals.StaminaRegenScale); // hunger and sickness slow recovery
            if (strikes) Strike();

            // Duellists act against us.
            for (int i = _duelists.Count - 1; i >= 0; i--)
            {
                Duelist d = _duelists[i];
                if (d == null) { _duelists.RemoveAt(i); continue; }
                bool wasOut = d.Out;
                if (d.Step(dt, WindupDir))
                {
                    HitResult r = MeleeRules.Resolve(d.Fighter, d.Loadout, _fighter, _loadout, d.DistanceToTarget, _rng, _b);
                    ApplyIncoming(r, d.displayName + "'s " + d.Loadout.WeaponName.ToLowerInvariant());
                }
                if (!wasOut && d.Out) DuelistDowned?.Invoke(d);
            }
            vitals.SetStamina(_fighter.Stamina);
            AnimateWeapon();
        }

        private AttackDir DirFromMouse()
        {
            if (_mouseAccum.sqrMagnitude < 4f) return AttackDir.High;
            if (Mathf.Abs(_mouseAccum.x) > Mathf.Abs(_mouseAccum.y)) return _mouseAccum.x < 0f ? AttackDir.Left : AttackDir.Right;
            return _mouseAccum.y > 0f ? AttackDir.High : AttackDir.Low;
        }

        private void Strike()
        {
            bool any = false;
            foreach (Duelist d in _duelists)
            {
                if (d == null || d.Dead) continue;
                Vector3 to = d.transform.position - facingSource.position;
                if (!CombatMath.InArc(to.x, to.z, facingSource.forward.x, facingSource.forward.z, _balance.melee.arcDegrees)) continue;
                float dist = new Vector2(to.x, to.z).magnitude;
                if (dist > _loadout.Reach) continue;
                if (d.Yielded) { KillTheYielded(d); any = true; continue; }
                HitResult r = MeleeRules.Resolve(_fighter, _loadout, d.Fighter, d.Loadout, dist, _rng, _b);
                _fighter.ConsumeStrike(); // one target per swing; StrikeConsumed stays set
                if (r.Outcome == HitOutcome.Hit) { d.TakeHit(r, "your " + _loadout.WeaponName.ToLowerInvariant()); Note(r.Part == BodyPart.Head ? "a blow to the head" : "hit"); if (d.Dead) _kills++; }
                else Note(r.Outcome == HitOutcome.Parried ? "parried!" : r.Outcome == HitOutcome.Blocked ? "blocked" : r.Outcome == HitOutcome.Dodged ? "he slips it" : "short");
                any = true;
                break;
            }
            if (!any && director != null && director.IsReady)
            {
                float dmg = CombatMath.Damage(_loadout.Damage * (1f + _fighter.Skill * _b.skillDamageWeight), _loadout.Variance, _rng.NextFloat(), 0f);
                int hits = director.StrikeArc(facingSource.position, facingSource.forward, _loadout.Reach, _balance.melee.arcDegrees, dmg, Side.Player);
                if (hits > 0) Note("hit");
            }
        }

        private void KillTheYielded(Duelist d)
        {
            d.Body.ApplyHit(BodyPart.Head, 1f, "your " + _loadout.WeaponName.ToLowerInvariant());
            d.Die(); _kills++;
            Note("you kill a man who had yielded");
            OnKilledSurrendered?.Invoke();
        }

        public event System.Action OnKilledSurrendered;

        /// <summary>A battle soldier's blow: same rules, a guessed line.</summary>
        private void OnSoldierStrikes(int soldierIndex)
        {
            if (vitals.IsDead) return;
            Unity.Mathematics.float3 sp = director.SoldierPosition(soldierIndex);
            float dist = Vector3.Distance(new Vector3(sp.x, 0f, sp.z), new Vector3(facingSource.position.x, 0f, facingSource.position.z));
            var attacker = new FighterState(_b, 50f) { Skill = 25f };
            attacker.TryAttack((AttackDir)_rng.Range(0, 4));
            while (!attacker.Tick(0.05f)) { }
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
            switch (_fighter.Phase)
            {
                case FightPhase.Windup: y = -70f; x = _fighter.Dir == AttackDir.High ? -50f : _fighter.Dir == AttackDir.Low ? 30f : 0f; z = _fighter.Dir == AttackDir.Left ? 40f : _fighter.Dir == AttackDir.Right ? -40f : 0f; break;
                case FightPhase.Strike: y = 90f; x = _fighter.Dir == AttackDir.High ? 40f : _fighter.Dir == AttackDir.Low ? -30f : 0f; break;
                case FightPhase.Recover: y = 60f; break;
                case FightPhase.Block: x = _fighter.Dir == AttackDir.High ? -80f : _fighter.Dir == AttackDir.Low ? -20f : -50f; z = _fighter.Dir == AttackDir.Left ? 90f : _fighter.Dir == AttackDir.Right ? 50f : 70f; y = -20f; break;
                case FightPhase.Stagger: x = 30f; y = -20f; break;
            }
            weaponVisual.localRotation = Quaternion.Euler(x, y, z);
        }
    }
}

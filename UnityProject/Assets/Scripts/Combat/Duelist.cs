using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Combat
{
    /// <summary>An NPC who fights the player one on one: bandit, outlaw, melee opponent. Runs the same FighterState as the player.</summary>
    public sealed class Duelist : MonoBehaviour
    {
        public string displayName = "A man";
        public Transform weaponPivot;
        public Transform target;
        public float moveSpeed = 2.6f;
        public bool CanYield = true;
        public bool IsBandit;            // robs you if you fall

        public FighterState Fighter { get; private set; }
        public FighterBrain Brain { get; private set; }
        public BodyState Body { get; private set; }
        public Loadout Loadout { get; private set; }
        public bool Dead => Body != null && Body.IsDead;
        public bool Yielded => Fighter != null && Fighter.Phase == FightPhase.Yielded;
        public bool Out => Dead || Yielded;
        public float DistanceToTarget => target != null ? Vector3.Distance(Flat(transform.position), Flat(target.position)) : 999f;

        private MeleeRulesBalance _b;
        private CharacterController _cc;
        private float _vy;

        public void Init(MeleeRulesBalance balance, WoundBalance wounds, Loadout loadout, float skill, float aggression, float read, DeterministicRng rng, float staminaMax = 90f)
        {
            _b = balance; Loadout = loadout;
            Fighter = new FighterState(balance, staminaMax) { Skill = skill };
            Brain = new FighterBrain(rng, aggression, read) { WillYield = CanYield };
            Body = new BodyState(wounds);
            _cc = GetComponent<CharacterController>();
        }

        /// <summary>Tick the brain and the state machine. Returns the event (strike or guard break landing) the owner must resolve this frame.</summary>
        public FightEvent Step(float dt, AttackDir? playerWindup, AttackKind playerKind, bool playerGuarding, AttackDir? playerGuardDir)
        {
            if (Fighter == null || Dead) return FightEvent.None;
            Brain.Think(Fighter, playerWindup, playerKind, playerGuarding, DistanceToTarget, Loadout.Reach, dt, _b);
            if (Brain.ConsiderFeint(Fighter, playerGuarding && playerGuardDir == Fighter.Dir)) Fighter.TryFeint();
            FightEvent ev = Fighter.Tick(dt);
            Move(dt);
            Animate();
            return ev;
        }

        private void Move(float dt)
        {
            if (target == null || _cc == null) return;
            Vector3 to = Flat(target.position) - Flat(transform.position);
            float d = to.magnitude;
            Vector3 vel = Vector3.zero;
            if (!Out && Fighter.CanAct && d > Loadout.Reach * 0.8f) vel = to.normalized * moveSpeed;
            if (Fighter.Phase == FightPhase.Dodge) vel = -to.normalized * moveSpeed * 1.6f;
            if (d > 0.01f && !Out) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 1f - Mathf.Exp(-10f * dt));
            if (_cc.isGrounded && _vy < 0f) _vy = -2f; _vy -= 20f * dt;
            _cc.Move((vel + Vector3.up * _vy) * dt);
        }

        private void Animate()
        {
            if (weaponPivot == null) return;
            float x = 0f, y = 20f, z = 0f;
            switch (Fighter.Phase)
            {
                case FightPhase.Windup: y = Fighter.Kind == AttackKind.Heavy ? -95f : -60f; x = Fighter.Dir == AttackDir.High ? -50f : 0f; z = Fighter.Dir == AttackDir.Left ? 40f : Fighter.Dir == AttackDir.Right ? -40f : 0f; break;
                case FightPhase.Strike: y = 80f; x = Fighter.Dir == AttackDir.High ? 40f : 0f; break;
                case FightPhase.GuardBreak: x = 20f; y = 10f; break;
                case FightPhase.Idle when Fighter.GuardDir.HasValue: x = Fighter.GuardDir == AttackDir.High ? -80f : -50f; z = Fighter.GuardDir == AttackDir.Left ? 90f : Fighter.GuardDir == AttackDir.Right ? 50f : 70f; y = -20f; break;
                case FightPhase.Stagger: x = 30f; y = -20f; break;
                case FightPhase.Yielded: x = 70f; y = 0f; break;
                case FightPhase.Down: x = 90f; break;
            }
            weaponPivot.localRotation = Quaternion.Euler(x, y, z);
            if (Dead) transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
        }

        public void Die()
        {
            Fighter.GoDown();
            if (_cc != null) _cc.enabled = false;
            transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
            if (weaponPivot != null) weaponPivot.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        public void TakeHit(HitResult r, string source)
        {
            Body.ApplyHit(r.Part, r.Severity, source);
            if (Body.IsDead) { Die(); return; }
            if (Brain.ConsiderYield(Fighter, Body.Blood, _b)) Fighter.Yield();
        }

        public void Spare() { if (Yielded) { target = null; moveSpeed = 0f; } }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}

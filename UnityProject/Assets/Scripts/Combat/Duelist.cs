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

        /// <summary>Tick the brain and the state machine. Returns true on the frame this duelist's strike lands.</summary>
        public bool Step(float dt, AttackDir? playerWindup)
        {
            if (Fighter == null || Dead) return false;
            Brain.Think(Fighter, playerWindup, DistanceToTarget, Loadout.Reach, dt, _b);
            bool strikes = Fighter.Tick(dt);
            Move(dt);
            Animate();
            return strikes;
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
                case FightPhase.Windup: y = -60f; x = Fighter.Dir == AttackDir.High ? -50f : Fighter.Dir == AttackDir.Low ? 30f : 0f; z = Fighter.Dir == AttackDir.Left ? 40f : Fighter.Dir == AttackDir.Right ? -40f : 0f; break;
                case FightPhase.Strike: y = 80f; x = Fighter.Dir == AttackDir.High ? 40f : Fighter.Dir == AttackDir.Low ? -30f : 0f; break;
                case FightPhase.Block: x = -60f; z = 70f; break;
                case FightPhase.Stagger: x = 30f; y = -20f; break;
                case FightPhase.Yielded: x = 70f; y = 0f; break;
                case FightPhase.Down: x = 90f; break;
            }
            weaponPivot.localRotation = Quaternion.Euler(x, y, z);
            if (Dead) transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
        }

        public void TakeHit(HitResult r, string source)
        {
            Body.ApplyHit(r.Part, r.Severity, source);
            if (Body.IsDead) { Fighter.GoDown(); if (_cc != null) _cc.enabled = false; return; }
            if (Brain.ConsiderYield(Fighter, Body.Blood, _b)) Fighter.Yield();
        }

        public void Spare() { if (Yielded) { target = null; moveSpeed = 0f; } }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}

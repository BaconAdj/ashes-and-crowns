using AshesAndCrowns.Battle;
using AshesAndCrowns.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Combat
{
    public enum SwingPhase : byte { Idle, Windup, Recovery }

    /// <summary>
    /// Phase 0 melee: a timed swing that strikes an arc in front of the player, a held block that
    /// stops blows from the front at a stamina cost, and enemy blows that become persistent wounds.
    /// Phase 6 replaces the arc check with animated weapon colliders and directional attacks.
    /// </summary>
    public sealed class MeleeController : MonoBehaviour
    {
        public BattleDirector director;
        public PlayerVitals vitals;
        public Transform weaponVisual;   // simple blade primitive rotated during the swing
        public Transform facingSource;   // the transform whose forward is the attack direction (player body)

        private Balance _balance;
        private DeterministicRng _rng;
        private SwingPhase _phase;
        private float _phaseTimer;
        private bool _blocking;
        private float _lastHitFlash;
        private int _kills;
        private int _blocked;

        public SwingPhase Phase => _phase;
        public bool IsBlocking => _blocking;
        public int Kills => _kills;
        public int Blocked => _blocked;
        public float TimeSinceHitTaken => Time.time - _lastHitFlash;
        public event System.Action Swung;

        public void Init(Balance balance, DeterministicRng rng)
        {
            _balance = balance; _rng = rng.Fork(13);
            director.EnemyStruckPlayer += OnEnemyStrikes;
            director.PlayerKilledSoldier += _ => _kills++;
            _lastHitFlash = -100f;
        }

        private void OnDestroy()
        {
            if (director != null) director.EnemyStruckPlayer -= OnEnemyStrikes;
        }

        private void Update()
        {
            if (_balance == null || vitals == null || vitals.IsDead) { _blocking = false; return; }
            var mouse = Mouse.current;
            _blocking = mouse != null && mouse.rightButton.isPressed && _phase == SwingPhase.Idle;

            switch (_phase)
            {
                case SwingPhase.Idle:
                    if (mouse != null && mouse.leftButton.wasPressedThisFrame && vitals.TrySpendStamina(_balance.melee.swingStaminaCost))
                    { _phase = SwingPhase.Windup; _phaseTimer = _balance.melee.swingWindupSeconds; }
                    break;
                case SwingPhase.Windup:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f) { Strike(); _phase = SwingPhase.Recovery; _phaseTimer = _balance.melee.swingRecoverySeconds; }
                    break;
                case SwingPhase.Recovery:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f) _phase = SwingPhase.Idle;
                    break;
            }
            AnimateWeapon();
        }

        private void Strike()
        {
            float dmg = CombatMath.Damage(_balance.melee.baseDamage, _balance.melee.damageVariance, _rng.NextFloat(), 0f);
            director.StrikeArc(facingSource.position, facingSource.forward, _balance.melee.reach, _balance.melee.arcDegrees, dmg, Side.Player);
            Swung?.Invoke();
        }

        private void OnEnemyStrikes(int soldierIndex)
        {
            if (vitals.IsDead) return;
            Vector3 from = (Vector3)director.SoldierPosition(soldierIndex) - facingSource.position;
            bool frontal = CombatMath.InArc(from.x, from.z, facingSource.forward.x, facingSource.forward.z, _balance.melee.blockArcDegrees);
            if (_blocking && frontal && vitals.TrySpendStamina(_balance.melee.blockStaminaCost))
            {
                _blocked++;
                return;
            }
            float severity = CombatMath.Damage(_balance.melee.enemyDamage, _balance.battle.soldierDamageVariance, _rng.NextFloat(), 0f);
            vitals.TakeHit(severity, "a soldier's blade");
            _lastHitFlash = Time.time;
        }

        private void AnimateWeapon()
        {
            if (weaponVisual == null) return;
            float angle;
            if (_phase == SwingPhase.Windup) angle = Mathf.Lerp(-70f, 20f, 1f - _phaseTimer / _balance.melee.swingWindupSeconds);
            else if (_phase == SwingPhase.Recovery) angle = Mathf.Lerp(90f, 20f, 1f - _phaseTimer / _balance.melee.swingRecoverySeconds);
            else angle = _blocking ? -20f : 20f;
            weaponVisual.localRotation = Quaternion.Euler(_blocking ? -60f : 0f, angle, _blocking ? 70f : 0f);
        }
    }
}

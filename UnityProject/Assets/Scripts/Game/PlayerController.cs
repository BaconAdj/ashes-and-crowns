using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>On-foot WASD movement relative to the camera, with sprint stamina and gravity.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public ThirdPersonCamera cameraRig;
        public PlayerVitals vitals;
        public MeleeController melee;     // optional: in guard mode the body squares up to the opponent
        private Balance _balance;
        private GameInput _input;
        private CharacterController _cc;
        private float _verticalVelocity;

        public void Init(Balance balance, GameInput input)
        {
            _balance = balance; _input = input; _cc = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (_balance == null) return;
            Vector2 move = _input.MoveValue;
            Quaternion yaw = Quaternion.Euler(0f, cameraRig.Yaw, 0f);
            Vector3 dir = yaw * new Vector3(move.x, 0f, move.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            // Wounded legs slow you down; permanently if maimed.
            float legs = 0.5f * (vitals.Body.PartIntegrity(BodyPart.LeftLeg) + vitals.Body.PartIntegrity(BodyPart.RightLeg));
            float legFactor = Mathf.Lerp(0.45f, 1f, legs);
            bool sprinting = _input.Sprint.IsPressed() && vitals.Stamina > 1f && dir.sqrMagnitude > 0.01f;
            float speed = (sprinting ? _balance.player.sprintSpeed : _balance.player.walkSpeed) * legFactor;
            if (sprinting) vitals.DrainStamina(_balance.player.sprintStaminaPerSecond);

            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity -= _balance.player.gravity * Time.deltaTime;
            Vector3 velocity = dir * speed + Vector3.up * _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);

            Transform lockOn = melee != null && melee.GuardMode ? melee.LockTarget : null;
            if (lockOn != null)
            {
                Vector3 face = lockOn.position - transform.position; face.y = 0f;
                if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face.normalized, Vector3.up), 1f - Mathf.Exp(-12f * Time.deltaTime));
            }
            else if (melee != null && melee.GuardMode)
            {
                // Guard mode with nobody to lock to: face the camera's way so the stance means something.
                transform.rotation = Quaternion.Slerp(transform.rotation, yaw, 1f - Mathf.Exp(-12f * Time.deltaTime));
            }
            else if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
    }
}

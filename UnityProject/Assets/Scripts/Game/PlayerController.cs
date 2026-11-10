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
        private bool _mantling; private Vector3 _mantleFrom, _mantleTo; private float _mantleT;
        private static readonly RaycastHit[] s_hits = new RaycastHit[4];

        public bool Mantling => _mantling;

        public void Init(Balance balance, GameInput input)
        {
            _balance = balance; _input = input; _cc = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (_balance == null) return;
            if (_mantling) { Mantle(); return; }
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

            bool guard = melee != null && melee.GuardMode;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool jumpPressed = kb != null && kb.spaceKey.wasPressedThisFrame && !guard && Cursor.lockState == CursorLockMode.Locked;
            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            if (jumpPressed && _cc.isGrounded && vitals.Stamina > 4f) { _verticalVelocity = _balance.player.jumpSpeed * legFactor; vitals.DrainStamina(4f / Mathf.Max(Time.deltaTime, 1e-3f)); }
            else if (!_cc.isGrounded && (jumpPressed || (_verticalVelocity < 1f && dir.sqrMagnitude > 0.1f)) && TryFindLedge(dir.sqrMagnitude > 0.1f ? dir : transform.forward, out Vector3 top)) { BeginMantle(top); return; }
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

        /// <summary>A ledge in front at chest to head height with standing room on top: wall, cart, low roof, rampart step.</summary>
        private bool TryFindLedge(Vector3 forward, out Vector3 top)
        {
            top = default;
            var pb = _balance.player;
            Vector3 feet = transform.position;
            Vector3 chest = feet + Vector3.up * (pb.mantleMinHeight + 0.05f);
            Vector3 fwd = forward.normalized;
            if (!Physics.Raycast(chest, fwd, out RaycastHit wall, 0.9f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (wall.collider.GetComponent<CharacterController>() != null || wall.collider.GetComponent<TerrainCollider>() != null) return false;
            Vector3 probe = wall.point + fwd * 0.35f + Vector3.up * (pb.mantleMaxHeight - pb.mantleMinHeight + 0.3f);
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit ground, pb.mantleMaxHeight + 0.3f, ~0, QueryTriggerInteraction.Ignore)) return false;
            float height = ground.point.y - feet.y;
            if (height < pb.mantleMinHeight || height > pb.mantleMaxHeight) return false;
            // Headroom on top for the body.
            if (Physics.CheckCapsule(ground.point + Vector3.up * (_cc.radius + 0.05f), ground.point + Vector3.up * (_cc.height - _cc.radius), _cc.radius * 0.9f, ~0, QueryTriggerInteraction.Ignore)) return false;
            top = ground.point;
            return true;
        }

        private void BeginMantle(Vector3 top)
        {
            _mantling = true; _mantleT = 0f; _mantleFrom = transform.position; _mantleTo = top + Vector3.up * 0.05f;
            _verticalVelocity = 0f; _cc.enabled = false;
        }

        private void Mantle()
        {
            _mantleT += Time.deltaTime / Mathf.Max(0.1f, _balance.player.mantleSeconds);
            float k = Mathf.Clamp01(_mantleT);
            // Up first, then over: the body rises along the wall, then the feet swing on to the ledge.
            float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f));
            float over = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.45f) / 0.55f));
            Vector3 p = _mantleFrom; p.y = Mathf.Lerp(_mantleFrom.y, _mantleTo.y, up);
            p.x = Mathf.Lerp(_mantleFrom.x, _mantleTo.x, over); p.z = Mathf.Lerp(_mantleFrom.z, _mantleTo.z, over);
            transform.position = p;
            if (k >= 1f) { _mantling = false; _cc.enabled = true; _verticalVelocity = -2f; }
        }
    }
}

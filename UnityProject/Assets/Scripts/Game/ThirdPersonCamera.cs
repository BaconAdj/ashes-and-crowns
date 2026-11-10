using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Mouse-orbit third-person camera. The target and distance change when mounted.</summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public MeleeController melee;     // optional: guard mode locks the camera on the opponent
        private Balance _balance;
        private GameInput _input;
        private float _yaw, _pitch = 15f;
        private float _distance, _height;

        public float Yaw => _yaw;

        public void Init(Balance balance, GameInput input)
        {
            _balance = balance; _input = input;
            SetRiding(false);
        }

        public void SetRiding(bool riding)
        {
            _distance = riding ? _balance.camera.ridingDistance : _balance.camera.thirdPersonDistance;
            _height = riding ? _balance.camera.ridingHeight : _balance.camera.thirdPersonHeight;
        }

        private void LateUpdate()
        {
            if (_balance == null || target == null) return;
            Transform lockOn = melee != null && melee.GuardMode ? melee.LockTarget : null;
            if (lockOn != null)
            {
                // Guard mode: the mouse picks the stance, the camera holds the opponent in frame.
                Vector3 to = lockOn.position - target.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                {
                    float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    _yaw = Mathf.LerpAngle(_yaw, wantYaw, 1f - Mathf.Exp(-8f * Time.deltaTime));
                }
                _pitch = Mathf.Lerp(_pitch, 12f, 1f - Mathf.Exp(-4f * Time.deltaTime));
            }
            else if (Cursor.lockState == CursorLockMode.Locked && !(melee != null && melee.GuardMode))
            {
                Vector2 look = _input.LookDelta * _balance.camera.lookSensitivity;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, _balance.camera.pitchMinDegrees, _balance.camera.pitchMaxDegrees);
            }
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * _height;
            Vector3 desired = pivot - rot * Vector3.forward * _distance;
            // Keep the camera out of walls: pull it in along the line from the pivot when something is in the way (interiors, trees).
            Vector3 toCam = desired - pivot; float dist = toCam.magnitude;
            if (dist > 0.05f && Physics.SphereCast(pivot, 0.25f, toCam / dist, out RaycastHit block, dist, ~0, QueryTriggerInteraction.Ignore)
                && block.collider.GetComponent<CharacterController>() == null)
                desired = pivot + toCam / dist * Mathf.Max(0.6f, block.distance - 0.1f);
            // Keep the camera out of the ground.
            if (Terrain.activeTerrain != null)
            {
                float ground = Terrain.activeTerrain.SampleHeight(desired) + Terrain.activeTerrain.transform.position.y + 0.6f;
                if (desired.y < ground) desired.y = ground;
            }
            float t = 1f - Mathf.Exp(-_balance.camera.followSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
            transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
        }
    }
}

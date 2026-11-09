using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// A horse you can mount (E). W/S accelerate and brake, A/D steer, Shift gallops. The horse turns
    /// slower at a gallop. The rider is parented to the seat while mounted.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class HorseController : MonoBehaviour
    {
        public Transform seat;
        private Balance _balance;
        private GameInput _input;
        private CharacterController _cc;
        private float _speed;
        private float _verticalVelocity;

        public bool Mounted { get; private set; }
        public float Speed => _speed;
        public bool Galloping => _speed > _balance.horse.walkSpeed * 1.15f;

        public void Init(Balance balance, GameInput input)
        {
            _balance = balance; _input = input; _cc = GetComponent<CharacterController>();
        }

        public void Mount(Transform rider)
        {
            Mounted = true;
            var cc = rider.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
            var riderGait = rider.GetComponent<ProceduralGait>(); if (riderGait != null) riderGait.enabled = false;
            rider.SetParent(seat, false);
            rider.localPosition = Vector3.zero;
            rider.localRotation = Quaternion.identity;
        }

        public void Dismount(Transform rider)
        {
            { var riderGait = rider.GetComponent<ProceduralGait>(); if (riderGait != null) riderGait.enabled = true; }
            Mounted = false;
            rider.SetParent(null, true);
            Vector3 side = transform.right * 1.6f;
            Vector3 pos = transform.position + side;
            if (Terrain.activeTerrain != null) pos.y = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y + 0.1f;
            rider.position = pos;
            rider.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            var cc = rider.GetComponent<CharacterController>(); if (cc != null) cc.enabled = true;
            _speed = 0f;
        }

        private void Update()
        {
            if (_balance == null) return;
            float dt = Time.deltaTime;
            if (Mounted)
            {
                Vector2 move = _input.MoveValue;
                float top = _input.Sprint.IsPressed() ? _balance.horse.gallopSpeed : _balance.horse.walkSpeed;
                float wanted = Mathf.Max(0f, move.y) * top;
                if (move.y < -0.1f) _speed = Mathf.MoveTowards(_speed, 0f, _balance.horse.braking * dt);
                else if (wanted > _speed) _speed = Mathf.MoveTowards(_speed, wanted, _balance.horse.acceleration * dt);
                else _speed = Mathf.MoveTowards(_speed, wanted, _balance.horse.braking * 0.5f * dt);
                float turnRate = Mathf.Lerp(_balance.horse.turnDegreesPerSecond, _balance.horse.gallopTurnDegreesPerSecond, _speed / _balance.horse.gallopSpeed);
                transform.Rotate(0f, move.x * turnRate * dt, 0f);
            }
            else _speed = Mathf.MoveTowards(_speed, 0f, _balance.horse.braking * dt);

            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity -= _balance.player.gravity * dt;
            _cc.Move((transform.forward * _speed + Vector3.up * _verticalVelocity) * dt);
        }
    }
}

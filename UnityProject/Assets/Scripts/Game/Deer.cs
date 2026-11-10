using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// A deer in a herd: grazes around the herd's ground, bolts when a walker comes close, drifts back. Killed, it lies where
    /// it fell until dressed. No CharacterController: it rides the terrain height, which is all a deer needs. Only herds
    /// near the player are stepped.
    /// </summary>
    public sealed class Deer : MonoBehaviour
    {
        public Vector3 herdCentre;
        public int index;
        public Transform visual;

        private static Transform s_player; private static float s_nextLookup; private static Balance s_balance;
        private Vector3 _goal; private float _nextGoal, _fleeUntil, _bob;
        private Terrain _terrain;
        public bool Dead { get; private set; }
        public bool Dressed { get; private set; }

        private void Start()
        {
            _terrain = Terrain.activeTerrain;
            _goal = transform.position; _nextGoal = Time.time + index * 0.7f;
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        }

        private void Update()
        {
            if (Dead) return;
            if (Time.unscaledTime >= s_nextLookup)
            {
                s_nextLookup = Time.unscaledTime + 2f;
                if (s_balance == null && GameBootstrap.Instance != null) s_balance = GameBootstrap.Instance.Balance;
                if (s_player == null) { var pc = FindAnyObjectByType<PlayerController>(); if (pc != null) s_player = pc.transform; }
            }
            if (s_balance == null || s_player == null) return;
            var w = s_balance.wildlife;
            Vector3 toPlayer = transform.position - s_player.position; toPlayer.y = 0f;
            float d2 = toPlayer.sqrMagnitude;
            if (d2 > 300f * 300f) return;   // far herds stand still; nobody is looking
            float dt = Time.deltaTime;
            if (d2 < w.fleeRadiusMetres * w.fleeRadiusMetres) { _fleeUntil = Time.time + w.fleeSeconds; _goal = transform.position + toPlayer.normalized * 60f + (herdCentre - s_player.position).normalized * 20f; }
            bool fleeing = Time.time < _fleeUntil;
            if (!fleeing && Time.time >= _nextGoal)
            {
                _nextGoal = Time.time + 4f + (index * 1.3f % 5f);
                float a = (Time.time * 0.37f + index * 2.1f) % (Mathf.PI * 2f);
                _goal = herdCentre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (w.grazeRadiusMetres * (0.3f + 0.7f * ((index * 7 + (int)(Time.time * 0.1f)) % 10) / 10f));
            }
            Vector3 to = _goal - transform.position; to.y = 0f;
            float dist = to.magnitude;
            float speed = fleeing ? w.fleeSpeed : (dist > 1.5f ? w.grazeSpeed : 0f);
            if (dist > 0.3f && speed > 0f)
            {
                Vector3 dir = to / dist;
                transform.position += dir * speed * dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-(fleeing ? 10f : 3f) * dt));
            }
            if (_terrain != null) { Vector3 p = transform.position; p.y = _terrain.SampleHeight(p) + _terrain.transform.position.y; transform.position = p; }
            if (visual != null)
            {
                _bob += dt * (fleeing ? 14f : 2f);
                visual.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(_bob)) * (fleeing ? 0.18f : 0.01f);
                visual.localRotation = speed <= 0f ? Quaternion.Euler(Mathf.Sin(_bob * 0.5f) * 3f + 14f, 0f, 0f) : Quaternion.identity;   // head down to graze
            }
        }

        /// <summary>A shaft in the heart: it drops where it stands. The herd bolts.</summary>
        public void Kill()
        {
            if (Dead) return;
            Dead = true;
            if (visual != null) { visual.localRotation = Quaternion.Euler(0f, 0f, 80f); visual.localPosition = new Vector3(0f, 0.25f, 0f); }
            foreach (Deer d in transform.parent != null ? transform.parent.GetComponentsInChildren<Deer>() : new[] { this }) if (d != this && !d.Dead) d._fleeUntil = Time.time + 8f;
        }

        public void Dress() { Dressed = true; gameObject.SetActive(false); }
    }
}

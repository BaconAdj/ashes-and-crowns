using System.Collections.Generic;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// A hung door. It swings away from whoever walks into it and falls shut behind them. When it is barred or locked
    /// (Sim.LockRules: at night, never to its owner) it is a solid plank and has to be knocked at, or opened with a blade
    /// or a bent iron. This component sits on the hinge, which never moves; the leaf is its child and does the swinging.
    /// </summary>
    public sealed class Door : MonoBehaviour
    {
        public Transform building;         // whose door this is
        public Transform leaf;             // the pivot that swings (its child is the plank)
        public Collider plank;             // solid only while the door is shut fast
        public LockKind lockKind = LockKind.Bar;
        public float width = 1.2f;
        public bool playersOwn;            // set when a life begins under this roof
        public string householder = string.Empty;   // who answers a knock

        private static readonly List<Door> s_all = new List<Door>(128);
        private static Transform s_player; private static WorldClock s_clock; private static Balance s_balance; private static float s_nextLookup;

        private float _angle, _sign = 1f, _lastWanted = -100f, _nudgedUntil = -100f;
        private bool _picked, _pickedAtNight;

        public static IReadOnlyList<Door> All => s_all;
        /// <summary>The middle of the doorway on the floor line.</summary>
        public Vector3 Centre => transform.position + transform.right * (width * 0.5f);
        public bool IsOpen => Mathf.Abs(_angle) > 1f;

        public bool Locked
        {
            get
            {
                if (s_balance == null || s_clock == null || !s_clock.IsReady) return false;
                if (_picked) return false;
                return LockRules.IsLocked(lockKind, s_clock.Calendar.HourOfDay, playersOwn, s_balance.locks);
            }
        }

        /// <summary>The door of a building, if one has been hung.</summary>
        public static Door For(Transform ofBuilding)
        {
            if (ofBuilding == null) return null;
            for (int i = 0; i < s_all.Count; i++) if (s_all[i] != null && s_all[i].building == ofBuilding) return s_all[i];
            return null;
        }

        /// <summary>The bar is lifted or the wards are beaten: it stays open to all until the household next secures it.</summary>
        public void ForceOpen()
        {
            _picked = true;
            _pickedAtNight = s_balance != null && s_clock != null && s_clock.IsReady && LockRules.IsNight(s_clock.Calendar.HourOfDay, s_balance.locks);
        }

        /// <summary>Somebody who is not the player is going through (a villager on their rounds).</summary>
        public void Nudge(Vector3 from)
        {
            if (Locked && !IsOpen) { _picked = true; _pickedAtNight = true; }   // the household lets itself through its own door
            if (!IsOpen) _sign = Vector3.Dot(from - Centre, transform.forward) > 0f ? 1f : -1f;
            _nudgedUntil = Time.time + 1.2f;
        }

        private void OnEnable() { s_all.Add(this); }
        private void OnDisable() { s_all.Remove(this); }

        private static void Lookup()
        {
            if (Time.unscaledTime < s_nextLookup) return;
            s_nextLookup = Time.unscaledTime + 2f;
            if (s_balance == null && GameBootstrap.Instance != null) s_balance = GameBootstrap.Instance.Balance;
            if (s_clock == null) s_clock = FindAnyObjectByType<WorldClock>();
            if (s_player == null) { var pc = FindAnyObjectByType<PlayerController>(); if (pc != null) s_player = pc.transform; }
        }

        private void Update()
        {
            Lookup();
            if (s_balance == null || s_player == null || leaf == null) return;
            LockBalance lb = s_balance.locks;
            Vector3 to = s_player.position - Centre;
            float sq = to.x * to.x + to.z * to.z;
            bool nudged = Time.time < _nudgedUntil;
            if (sq > lb.doorAwakeMetres * lb.doorAwakeMetres && !nudged && _angle == 0f) return;

            // A forced door is made fast again when the night turns to day or the day to night.
            if (_picked && s_clock != null && s_clock.IsReady && LockRules.IsNight(s_clock.Calendar.HourOfDay, lb) != _pickedAtNight && !IsOpen) _picked = false;

            bool locked = Locked;
            bool wanted = nudged;
            if (!locked && sq < lb.doorReachMetres * lb.doorReachMetres && Mathf.Abs(to.y) < 2.5f)
            {
                wanted = true;
                if (!IsOpen) _sign = Vector3.Dot(to, transform.forward) > 0f ? 1f : -1f;   // from outside it swings in, from inside out
            }
            if (wanted) _lastWanted = Time.time;
            bool hold = Time.time - _lastWanted < lb.doorCloseAfterSeconds;
            float target = (wanted || hold) && (!locked || nudged) ? _sign * lb.doorOpenDegrees : 0f;
            if (_angle != target)
            {
                _angle = Mathf.MoveTowards(_angle, target, lb.doorOpenDegrees / lb.doorSwingSeconds * Time.deltaTime);
                leaf.localRotation = Quaternion.Euler(0f, _angle, 0f);
            }
            if (plank != null) { bool solid = locked && !IsOpen; if (plank.enabled != solid) plank.enabled = solid; }
        }
    }
}

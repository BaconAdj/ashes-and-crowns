using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// No rigs yet, so bodies move the cheap way: the visual child bobs and leans with speed, a horse rocks with its gait,
    /// a standing man breathes. Reads the parent's displacement each frame, so it works for the player, the horse and duelists.
    /// </summary>
    public sealed class ProceduralGait : MonoBehaviour
    {
        public Transform visual;              // the child to move (Body); defaults to the first child
        public float strideHz = 2.2f;         // steps per second at full speed
        public float bobMetres = 0.045f;
        public float leanDegrees = 6f;
        public float swayDegrees = 2.5f;
        public float breatheMetres = 0.008f;
        public float fullSpeed = 4f;          // m/s that counts as "running"
        public bool horse;

        private Vector3 _lastPos, _restLocalPos;
        private Quaternion _restLocalRot;
        private float _phase, _speedSmoothed;

        private void Start()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null) { _restLocalPos = visual.localPosition; _restLocalRot = visual.localRotation; }
            _lastPos = transform.position;
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            float dt = Time.deltaTime; if (dt <= 0f) return;
            Vector3 delta = transform.position - _lastPos; delta.y = 0f; _lastPos = transform.position;
            float speed = delta.magnitude / dt;
            if (speed > fullSpeed * 3f) speed = 0f;   // teleport or scene load
            _speedSmoothed = Mathf.Lerp(_speedSmoothed, speed, 1f - Mathf.Exp(-8f * dt));
            float k = Mathf.Clamp01(_speedSmoothed / fullSpeed);
            _phase += dt * strideHz * Mathf.PI * 2f * Mathf.Max(0.35f, k);
            float s = Mathf.Sin(_phase), s2 = Mathf.Sin(_phase * 2f);
            float bob = horse ? Mathf.Abs(s) * bobMetres * k : s2 * bobMetres * k;
            float breathe = Mathf.Sin(Time.time * 1.1f) * breatheMetres * (1f - k);
            visual.localPosition = _restLocalPos + new Vector3(0f, bob + breathe, 0f);
            float lean = leanDegrees * k * (horse ? 0.5f : 1f);
            float sway = s * swayDegrees * k;
            visual.localRotation = _restLocalRot * Quaternion.Euler(lean, 0f, sway);
        }
    }
}

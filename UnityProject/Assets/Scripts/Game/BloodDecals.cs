using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
using Unity.Mathematics;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Dark stains where men fall: a ring of instanced quads, no allocation per frame. Bodies stay; so does the blood.</summary>
    public sealed class BloodDecals : MonoBehaviour
    {
        public Material material;
        public BattleDirector director;      // optional
        public PlayerVitals vitals;          // optional
        public MeleeController melee;        // optional
        public Transform player;
        public int capacity = 1023;
        public float size = 1.1f;

        private Matrix4x4[] _matrices;
        private Mesh _quad;
        private int _count, _next;
        private RenderParams _rp;
        private readonly Vector3 _lift = new Vector3(0f, 0.04f, 0f);

        private void Awake()
        {
            capacity = Mathf.Clamp(capacity, 1, 1023);
            _matrices = new Matrix4x4[capacity];
            _quad = new Mesh { name = "BloodQuad" };
            _quad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateNormals(); _quad.RecalculateBounds();
            if (material != null) _rp = new RenderParams(material) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, worldBounds = new Bounds(Vector3.zero, Vector3.one * 20000f) };
        }

        private void OnEnable()
        {
            if (vitals != null) vitals.Wounded += OnPlayerWounded;
            if (melee != null) melee.DuelistDowned += OnDuelistDowned;
        }

        private void OnDisable()
        {
            if (vitals != null) vitals.Wounded -= OnPlayerWounded;
            if (melee != null) melee.DuelistDowned -= OnDuelistDowned;
        }

        private void OnPlayerWounded(Sim.BodyPart part, float severity) { if (player != null && severity > 0.05f) Add(player.position, 0.5f + severity); }
        private void OnDuelistDowned(Duelist d) { if (d != null) Add(d.transform.position, 1f); }

        public void Add(Vector3 at, float scale)
        {
            if (_matrices == null) return;
            float yaw = (at.x * 12.9898f + at.z * 78.233f) % 6.2831f;
            _matrices[_next] = Matrix4x4.TRS(at + _lift, Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f), new Vector3(size * scale, 1f, size * scale * 0.8f));
            _next = (_next + 1) % capacity; if (_count < capacity) _count++;
        }

        private void LateUpdate()
        {
            if (director != null && director.IsReady)
            {
                var deaths = director.DeathsThisFrame;
                for (int i = 0; i < deaths.Length; i++) { float3 p = deaths[i]; Add(new Vector3(p.x, p.y, p.z), 1f); }
            }
            if (_count == 0 || material == null) return;
            Graphics.RenderMeshInstanced(_rp, _quad, 0, _matrices, _count);
        }
    }
}

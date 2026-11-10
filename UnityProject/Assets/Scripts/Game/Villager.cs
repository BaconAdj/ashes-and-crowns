using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// A villager with a life of their own: sleeps at home, works at a station, eats at the hearth, idles on the green,
    /// drinks in the alehouse, goes to mass. The plan comes from Sim.VillageDay by the bell; this component walks the body
    /// there through doors, poses it (asleep, kneeling, working) and answers "what are they doing" for the talk screen.
    /// Far from the player it only places itself; the walking is for the village you are standing in.
    /// </summary>
    [RequireComponent(typeof(VillagerTag))]
    public sealed class Villager : MonoBehaviour
    {
        public SettlementSite site;
        public Transform home;             // the hut (furnished: Bed, Hearth, Door, Inside markers)
        public Transform workStation;      // where the day's work happens (outside or inside a building)
        public Transform workBuilding;     // if the station is indoors (the brewer's alehouse), the building it is in
        public Transform visual;           // the body child
        public int variant;                // spreads habits across the village
        public bool female;

        private static Transform s_player;
        private static WorldClock s_clock;
        private static Balance s_balance;
        private static float s_nextPlayerLookup;

        private VillagerTag _tag;
        private CharacterController _cc;
        private VillagerActivity _activity = VillagerActivity.Home;
        private bool _planned;
        private Transform _building;          // the building we are inside, or null
        private readonly Vector3[] _route = new Vector3[5]; private int _routeCount, _routeIndex, _exitDoorIndex = -1;
        private Vector3 _goal; private Transform _goalBuilding;
        private float _vy, _dodgeUntil, _dodgeSign = 1f, _bob, _nextThink;
        private Vector3 _restPos; private Quaternion _restRot;

        public VillagerActivity Activity => _activity;
        public VillagerTag Tag => _tag;
        public string Doing => VillageDay.Describe(_activity, _tag != null ? _tag.trade : string.Empty);
        public bool WillTalk => _activity != VillagerActivity.Asleep;

        private void Awake() { _tag = GetComponent<VillagerTag>(); _cc = GetComponent<CharacterController>(); }

        private void Start()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null) { _restPos = visual.localPosition; _restRot = visual.localRotation; }
            _nextThink = Random_(0.5f);
            // Property blocks are not saved with a scene: re-dye the tunic from the id so Ashby's people keep their colours.
            var r = visual != null ? visual.GetComponent<Renderer>() : null;
            if (r != null && _tag != null) { var block = new MaterialPropertyBlock(); block.SetColor(Shader.PropertyToID("_BaseColor"), VillageKit.Dye(_tag.npcId)); r.SetPropertyBlock(block, 0); }
        }

        private float Random_(float max) => ((_tag != null ? _tag.npcId : GetInstanceID()) * 0.618f % 1f) * max;

        private static void Lookup()
        {
            if (Time.unscaledTime < s_nextPlayerLookup) return;
            s_nextPlayerLookup = Time.unscaledTime + 2f;
            if (s_balance == null && GameBootstrap.Instance != null) s_balance = GameBootstrap.Instance.Balance;
            if (s_clock == null) s_clock = FindFirstObjectByType<WorldClock>();
            if (s_player == null) { var pc = FindFirstObjectByType<PlayerController>(); if (pc != null) s_player = pc.transform; }
        }

        private void Update()
        {
            Lookup();
            if (s_balance == null || s_clock == null || !s_clock.IsReady) return;
            var b = s_balance.villageDay;
            float dt = Time.deltaTime;
            // Plan by the bell (cheap; the clock is shared).
            if (Time.unscaledTime >= _nextThink)
            {
                _nextThink = Time.unscaledTime + 1.5f;
                var cal = s_clock.Calendar;
                long jdn = Calendar.JulianDayNumber(cal.Year, cal.Month, cal.Day);
                bool sunday = (jdn + 1) % 7 == 0;
                bool mass = Festivals.IsMassWindow(cal, s_balance.time) && (sunday || Festivals.FeastOn(cal) != Feast.None);
                VillagerActivity want = VillageDay.At(cal.HourOfDay, sunday, mass, variant, b);
                if (want != _activity || !_planned) { _activity = want; _planned = true; Plan(); }
            }
            bool near = s_player != null && (transform.position - s_player.position).sqrMagnitude < b.awakeRadiusMetres * b.awakeRadiusMetres;
            if (!near) { if (_routeCount > 0) Teleport(); return; }
            Walk(dt, b);
            Pose(dt);
        }

        // ------------------------------------------------------------------ planning

        private void Plan()
        {
            Transform targetBuilding = null; Vector3 goal;
            switch (_activity)
            {
                case VillagerActivity.Asleep: case VillagerActivity.Rising: targetBuilding = home; goal = MarkerOr(home, "Bed", 0); break;
                case VillagerActivity.Eating: case VillagerActivity.Home: targetBuilding = home; goal = MarkerOr(home, "Hearth", 0); break;
                case VillagerActivity.Working: targetBuilding = workBuilding; goal = workStation != null ? workStation.position : (home != null ? home.position + home.forward * 5f : transform.position); break;
                case VillagerActivity.Drinking: targetBuilding = site != null ? site.alehouse : null; goal = MarkerOr(targetBuilding, "Seat", variant % 4); break;
                case VillagerActivity.Praying: targetBuilding = site != null ? site.church : null; goal = MarkerOr(targetBuilding, "Seat", variant % 4); break;
                default:
                    {
                        Transform g = site != null ? site.Green : null;
                        float a = (variant * 1.7f + 0.4f) % (Mathf.PI * 2f); float r = 3.5f + (variant % 3) * 1.4f;
                        goal = g != null ? g.position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r) : transform.position;
                        break;
                    }
            }
            _goal = goal; _goalBuilding = targetBuilding;
            _routeCount = 0; _routeIndex = 0; _exitDoorIndex = -1;
            if (_building != null && _building != targetBuilding) { Add(Marker(_building, "Inside")); Add(Marker(_building, "Door")); _exitDoorIndex = 1; }
            if (targetBuilding != null && targetBuilding != _building) { Add(Marker(targetBuilding, "Door")); Add(Marker(targetBuilding, "Inside")); }
            Add(goal);
        }

        private void Add(Vector3 p) { if (_routeCount < _route.Length) _route[_routeCount++] = p; }
        private static Vector3 Marker(Transform building, string name) { var m = BuildingKit.Find(building, name); return m != null ? m.position : building.position; }
        private static Vector3 MarkerOr(Transform building, string name, int index)
        {
            if (building == null) return Vector3.zero;
            var m = BuildingKit.Find(building, name, index) ?? BuildingKit.Find(building, name, 0);
            return m != null ? m.position : building.position;
        }

        private void Teleport()
        {
            if (_cc != null) _cc.enabled = false;
            transform.position = _goal + Vector3.up * 0.05f;
            if (_cc != null) _cc.enabled = true;
            _building = _goalBuilding; _routeCount = 0;
        }

        // ------------------------------------------------------------------ walking

        private void Walk(float dt, VillageDayBalance b)
        {
            Vector3 vel = Vector3.zero;
            if (_routeIndex < _routeCount)
            {
                Vector3 target = _route[_routeIndex];
                Vector3 to = target - transform.position; to.y = 0f;
                float d = to.magnitude;
                if (d < 0.35f)
                {
                    _routeIndex++;
                    // Crossing a door marks us outside; arriving marks us inside the goal's building (or outdoors).
                    if (_exitDoorIndex >= 0 && _routeIndex == _exitDoorIndex + 1) _building = null;
                    if (_routeIndex == _routeCount) _building = _goalBuilding;
                }
                else
                {
                    Vector3 dir = to / d;
                    if (Time.time < _dodgeUntil) dir = (Quaternion.Euler(0f, 55f * _dodgeSign, 0f) * dir);
                    else if (Physics.Raycast(transform.position + Vector3.up * 0.9f, dir, out RaycastHit hit, 1.1f) && !hit.collider.isTrigger && hit.collider.GetComponent<TerrainCollider>() == null && hit.collider.GetComponent<CharacterController>() == null)
                    {
                        _dodgeUntil = Time.time + 0.6f; _dodgeSign = Vector3.Dot(hit.normal, transform.right) > 0f ? 1f : -1f;
                    }
                    bool late = _activity == VillagerActivity.Praying || _activity == VillagerActivity.Asleep;
                    vel = dir * (late ? b.hurrySpeed : b.walkSpeed);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-8f * dt));
                }
            }
            else if (_activity == VillagerActivity.Idling && site != null && site.Green != null)
            {
                Vector3 face = site.Green.position - transform.position; face.y = 0f;
                if (face.sqrMagnitude > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face.normalized, Vector3.up), 1f - Mathf.Exp(-3f * dt));
            }
            if (_cc != null && _cc.enabled)
            {
                if (_cc.isGrounded && _vy < 0f) _vy = -2f; _vy -= 20f * dt;
                _cc.Move((vel + Vector3.up * _vy) * dt);
            }
            else transform.position += vel * dt;
        }

        // ------------------------------------------------------------------ pose

        private void Pose(float dt)
        {
            if (visual == null) return;
            bool arrived = _routeIndex >= _routeCount;
            _bob += dt;
            if (!arrived) { visual.localPosition = _restPos + Vector3.up * Mathf.Abs(Mathf.Sin(_bob * 7f)) * 0.03f; visual.localRotation = _restRot; return; }
            switch (_activity)
            {
                case VillagerActivity.Asleep: visual.localRotation = _restRot * Quaternion.Euler(-88f, 0f, 0f); visual.localPosition = _restPos + new Vector3(0f, 0.25f, -0.6f); break;
                case VillagerActivity.Praying: visual.localRotation = _restRot * Quaternion.Euler(18f, 0f, 0f); visual.localPosition = _restPos + new Vector3(0f, -0.42f, 0.05f); break;
                case VillagerActivity.Working: visual.localRotation = _restRot * Quaternion.Euler(14f + Mathf.Sin(_bob * 5f) * 7f, 0f, 0f); visual.localPosition = _restPos; break;
                case VillagerActivity.Eating: case VillagerActivity.Drinking: visual.localRotation = _restRot * Quaternion.Euler(10f, 0f, 0f); visual.localPosition = _restPos + Vector3.down * 0.35f; break;
                default: visual.localRotation = _restRot * Quaternion.Euler(Mathf.Sin(_bob * 0.8f) * 2f, 0f, 0f); visual.localPosition = _restPos; break;
            }
        }
    }
}

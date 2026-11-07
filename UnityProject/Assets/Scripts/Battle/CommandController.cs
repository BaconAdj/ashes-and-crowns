using AshesAndCrowns.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Battle
{
    /// <summary>
    /// The RTS layer. Only enabled while the player stands at a command vantage.
    /// Left click selects a friendly unit, right click orders a move, C charges, H holds, 1-4 set formation.
    /// </summary>
    public sealed class CommandController : MonoBehaviour
    {
        public BattleDirector director;
        public Camera commandCamera;
        public Transform vantage;
        public Material selectionRingMaterial;

        private Balance _balance;
        private readonly bool[] _selected = new bool[64];
        private LineRenderer[] _rings = new LineRenderer[0];
        private float _zoom;
        private Vector3 _focus;
        private float _lastSummaryUpdate;
        private string _summary = string.Empty;

        public string Summary => _summary;
        public int SelectedCount { get; private set; }

        public void Init(Balance balance)
        {
            _balance = balance;
            _zoom = balance.camera.commandHeight;
            _focus = vantage != null ? vantage.position : Vector3.zero;
            BuildRings();
        }

        private void BuildRings()
        {
            foreach (var r in _rings) if (r != null) Destroy(r.gameObject);
            _rings = new LineRenderer[director.UnitCount];
            for (int u = 0; u < director.UnitCount; u++)
            {
                var go = new GameObject("SelectionRing_" + u);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.loop = true; lr.useWorldSpace = true; lr.positionCount = 32;
                lr.startWidth = lr.endWidth = 0.25f;
                lr.material = selectionRingMaterial;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.enabled = false;
                _rings[u] = lr;
            }
        }

        private void OnEnable()
        {
            if (commandCamera != null) commandCamera.enabled = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (vantage != null) _focus = vantage.position + vantage.forward * 40f;
        }

        private void OnDisable()
        {
            if (commandCamera != null) commandCamera.enabled = false;
            for (int i = 0; i < _rings.Length; i++) if (_rings[i] != null) _rings[i].enabled = false;
        }

        private void Update()
        {
            if (director == null || !director.IsReady || _balance == null) return;
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            float dt = Time.deltaTime;

            // Pan / zoom
            Vector2 pan = Vector2.zero;
            if (kb.upArrowKey.isPressed) pan.y += 1f; if (kb.downArrowKey.isPressed) pan.y -= 1f;
            if (kb.leftArrowKey.isPressed) pan.x -= 1f; if (kb.rightArrowKey.isPressed) pan.x += 1f;
            Vector3 camFwd = Vector3.ProjectOnPlane(commandCamera.transform.forward, Vector3.up).normalized;
            Vector3 camRight = commandCamera.transform.right;
            _focus += (camFwd * pan.y + camRight * pan.x) * _balance.camera.commandPanSpeed * dt * (_zoom / _balance.camera.commandHeight);
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) _zoom = Mathf.Clamp(_zoom - Mathf.Sign(scroll) * _balance.camera.commandZoomSpeed, _balance.camera.commandZoomMin, _balance.camera.commandZoomMax);
            _focus.y = director.GroundHeight(_focus.x, _focus.z);
            float pitch = _balance.camera.commandPitchDegrees * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch)) * _zoom;
            commandCamera.transform.position = _focus + offset;
            commandCamera.transform.LookAt(_focus);

            // Selection
            if (mouse.leftButton.wasPressedThisFrame && TryGroundPoint(mouse.position.ReadValue(), out Vector3 pick))
            {
                int unit = NearestPlayerUnit(pick, _balance.command.unitSelectRadius);
                if (!kb.leftShiftKey.isPressed) ClearSelection();
                if (unit >= 0) _selected[unit] = !kb.leftShiftKey.isPressed || !_selected[unit];
            }
            // Orders
            if (mouse.rightButton.wasPressedThisFrame && TryGroundPoint(mouse.position.ReadValue(), out Vector3 dest))
                ForEachSelected(u => director.IssueOrder(u, OrderType.Move, dest));
            if (kb.cKey.wasPressedThisFrame)
            {
                ForEachSelected(u =>
                {
                    int enemy = director.NearestLivingUnit(Side.Enemy, director.UnitCentroids[u]);
                    if (enemy >= 0) director.IssueOrder(u, OrderType.Charge, director.UnitCentroids[enemy]);
                });
            }
            if (kb.hKey.wasPressedThisFrame) ForEachSelected(u => director.IssueOrder(u, OrderType.Hold, float3.zero));
            if (kb.digit1Key.wasPressedThisFrame) ForEachSelected(u => director.SetFormation(u, FormationType.Line));
            if (kb.digit2Key.wasPressedThisFrame) ForEachSelected(u => director.SetFormation(u, FormationType.Wedge));
            if (kb.digit3Key.wasPressedThisFrame) ForEachSelected(u => director.SetFormation(u, FormationType.Square));
            if (kb.digit4Key.wasPressedThisFrame) ForEachSelected(u => director.SetFormation(u, FormationType.Shieldwall));

            UpdateRings();
            if (Time.unscaledTime - _lastSummaryUpdate > 0.25f) { _lastSummaryUpdate = Time.unscaledTime; _summary = BuildSummary(); }
        }

        private delegate void UnitAction(int unit);
        private void ForEachSelected(UnitAction action)
        {
            for (int u = 0; u < director.UnitCount && u < _selected.Length; u++) if (_selected[u]) action(u);
        }

        private void ClearSelection() { for (int i = 0; i < _selected.Length; i++) _selected[i] = false; }

        private bool TryGroundPoint(Vector2 screen, out Vector3 point)
        {
            Ray ray = commandCamera.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out RaycastHit hit, 2000f)) { point = hit.point; return true; }
            point = default; return false;
        }

        private int NearestPlayerUnit(Vector3 p, float radius)
        {
            int best = -1; float bestD = radius * radius;
            for (int u = 0; u < director.UnitCount; u++)
            {
                UnitState unit = director.Units[u];
                if (unit.Side != (byte)Side.Player || unit.Alive == 0) continue;
                float d = math.distancesq((float3)p, director.UnitCentroids[u]);
                if (d < bestD) { bestD = d; best = u; }
            }
            return best;
        }

        private void UpdateRings()
        {
            SelectedCount = 0;
            for (int u = 0; u < _rings.Length; u++)
            {
                bool on = _selected[u] && director.Units[u].Alive != 0;
                _rings[u].enabled = on;
                if (!on) continue;
                SelectedCount++;
                UnitState unit = director.Units[u];
                float radius = FormationMath.Frontage(unit.FormationType, unit.SoldierCount, _balance.battle.formationSpacing, _balance.battle.shieldwallSpacing) * 0.6f + 1f;
                Vector3 c = director.UnitCentroids[u];
                for (int k = 0; k < 32; k++)
                {
                    float a = k / 32f * Mathf.PI * 2f;
                    float x = c.x + Mathf.Cos(a) * radius, z = c.z + Mathf.Sin(a) * radius;
                    _rings[u].SetPosition(k, new Vector3(x, director.GroundHeight(x, z) + 0.15f, z));
                }
            }
        }

        private string BuildSummary()
        {
            int n = 0;
            for (int u = 0; u < director.UnitCount; u++) if (director.Units[u].Side == (byte)Side.Player && director.Units[u].Alive != 0) n++;
            return SelectedCount == 0
                ? $"{n} units under your command. Left-click a banner to select. Arrows pan, wheel zooms. WASD walks you off the hill."
                : $"{SelectedCount} selected  |  RMB move  C charge  H hold  1 line  2 wedge  3 square  4 shieldwall";
        }
    }
}

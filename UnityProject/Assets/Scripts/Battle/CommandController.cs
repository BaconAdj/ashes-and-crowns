using AshesAndCrowns.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Battle
{
    /// <summary>
    /// The RTS layer, keyed like a Total War battle. Only enabled while the player stands at a command vantage.
    /// Selection: left click a banner (Shift adds), Ctrl+A all, Return none, Ctrl+I all infantry (every unit, for now),
    /// Ctrl+[0-9] saves the selection as a group, [0-9] recalls it. Orders: right click move, Alt+drag move facing the
    /// drag, C or F charge (melee), H hold, Backspace halt, J about face, U withdraw to the rally point,
    /// Ctrl+Left/Right wheel the formation, Shift+1..4 line / wedge / square / shieldwall (Alt+1..4 too).
    /// Camera: WASD or arrows pan, Q/E rotate, wheel or Z/X zoom, Home pans to your own body. K hides the HUD, P pauses.
    /// </summary>
    public sealed class CommandController : MonoBehaviour
    {
        public BattleDirector director;
        public Camera commandCamera;
        public Transform vantage;
        public Transform lord;                 // the player's body: Home pans here
        public Material selectionRingMaterial;
        public float rotateDegreesPerSecond = 70f, wheelDegreesPerSecond = 60f;

        private Balance _balance;
        private bool[] _selected = new bool[0];
        private bool[][] _groups = new bool[10][];
        private LineRenderer[] _rings = new LineRenderer[0];
        private float _zoom, _yaw;
        private Vector3 _focus;
        private float _lastSummaryUpdate;
        private string _summary = string.Empty;
        private bool _dragging; private Vector3 _dragStart;
        private bool _uiHidden, _paused;

        public string Summary => _summary;
        public int SelectedCount { get; private set; }
        public bool UiHidden => _uiHidden;
        public bool Paused => _paused;

        public void Init(Balance balance)
        {
            _balance = balance;
            _zoom = balance.camera.commandHeight;
            _focus = vantage != null ? vantage.position : Vector3.zero;
            _yaw = vantage != null ? vantage.eulerAngles.y : 0f;
            _selected = new bool[director.UnitCount];
            for (int g = 0; g < _groups.Length; g++) _groups[g] = new bool[director.UnitCount];
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
            if (vantage != null) { _focus = vantage.position + vantage.forward * 40f; _yaw = vantage.eulerAngles.y; }
        }

        private void OnDisable()
        {
            if (commandCamera != null) commandCamera.enabled = false;
            for (int i = 0; i < _rings.Length; i++) if (_rings[i] != null) _rings[i].enabled = false;
            if (_paused) { _paused = false; Time.timeScale = 1f; }
            _dragging = false;
        }

        private void Update()
        {
            if (director == null || !director.IsReady || _balance == null) return;
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            float dt = Time.unscaledDeltaTime;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;

            // ---- camera: pan (WASD / arrows), rotate (Q/E or middle drag), zoom (wheel, Z/X), Home to the lord
            Vector2 pan = Vector2.zero;
            if (kb.upArrowKey.isPressed || (kb.wKey.isPressed && !ctrl)) pan.y += 1f; if (kb.downArrowKey.isPressed || (kb.sKey.isPressed && !ctrl)) pan.y -= 1f;
            if ((kb.leftArrowKey.isPressed && !ctrl) || (kb.aKey.isPressed && !ctrl)) pan.x -= 1f; if ((kb.rightArrowKey.isPressed && !ctrl) || (kb.dKey.isPressed && !ctrl)) pan.x += 1f;
            if (kb.qKey.isPressed) _yaw -= rotateDegreesPerSecond * dt; if (kb.eKey.isPressed) _yaw += rotateDegreesPerSecond * dt;
            if (mouse.middleButton.isPressed) _yaw += mouse.delta.ReadValue().x * 0.25f;
            Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 camFwd = yawRot * Vector3.forward, camRight = yawRot * Vector3.right;
            _focus += (camFwd * pan.y + camRight * pan.x) * _balance.camera.commandPanSpeed * dt * (_zoom / _balance.camera.commandHeight);
            float scroll = mouse.scroll.ReadValue().y;
            if (kb.zKey.isPressed) scroll += 0.3f; if (kb.xKey.isPressed) scroll -= 0.3f;
            if (Mathf.Abs(scroll) > 0.01f) _zoom = Mathf.Clamp(_zoom - Mathf.Sign(scroll) * _balance.camera.commandZoomSpeed * (Mathf.Abs(scroll) < 1f ? Mathf.Abs(scroll) : 1f), _balance.camera.commandZoomMin, _balance.camera.commandZoomMax);
            if (kb.homeKey.wasPressedThisFrame && lord != null) _focus = lord.position;
            _focus.y = director.GroundHeight(_focus.x, _focus.z);
            float pitch = _balance.camera.commandPitchDegrees * Mathf.Deg2Rad;
            Vector3 offset = yawRot * new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch)) * _zoom;
            commandCamera.transform.position = _focus + offset;
            commandCamera.transform.LookAt(_focus);

            // ---- UI and pause
            if (kb.kKey.wasPressedThisFrame) _uiHidden = !_uiHidden;
            if (kb.pKey.wasPressedThisFrame) { _paused = !_paused; Time.timeScale = _paused ? 0f : 1f; }

            // ---- selection
            if (ctrl && kb.aKey.wasPressedThisFrame) SelectAll();
            if (ctrl && kb.iKey.wasPressedThisFrame) SelectAll();           // melee infantry: every unit is foot, for now
            if (ctrl && (kb.mKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame || kb.bKey.wasPressedThisFrame)) ClearSelection(); // no missile, horse or engines in the ranks yet
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) ClearSelection();
            for (int d = 0; d <= 9; d++)
            {
                if (!DigitPressed(kb, d)) continue;
                if (ctrl) System.Array.Copy(_selected, _groups[d], _selected.Length);
                else if (shift || alt) { if (d >= 1 && d <= 4) ForEachSelected(u => director.SetFormation(u, (FormationType)(d - 1))); }
                else System.Array.Copy(_groups[d], _selected, _selected.Length);
            }
            if (alt && mouse.leftButton.wasPressedThisFrame && TryGroundPoint(mouse.position.ReadValue(), out Vector3 start)) { _dragging = true; _dragStart = start; }
            else if (!alt && mouse.leftButton.wasPressedThisFrame && TryGroundPoint(mouse.position.ReadValue(), out Vector3 pick))
            {
                int unit = NearestPlayerUnit(pick, _balance.command.unitSelectRadius);
                if (!shift) ClearSelection();
                if (unit >= 0) _selected[unit] = !shift || !_selected[unit];
            }
            if (_dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                _dragging = false;
                if (TryGroundPoint(mouse.position.ReadValue(), out Vector3 end))
                {
                    Vector3 face = end - _dragStart; face.y = 0f;
                    float yaw = face.sqrMagnitude > 1f ? Mathf.Atan2(face.x, face.z) : float.NaN;
                    Vector3 dest = _dragStart;
                    ForEachSelected(u => { director.IssueOrder(u, OrderType.Move, dest); if (!float.IsNaN(yaw)) director.SetFacing(u, yaw); });
                }
            }

            // ---- orders
            if (!alt && mouse.rightButton.wasPressedThisFrame && TryGroundPoint(mouse.position.ReadValue(), out Vector3 dest2))
                ForEachSelected(u => director.IssueOrder(u, OrderType.Move, dest2));
            if (!ctrl && (kb.cKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame))
            {
                ForEachSelected(u =>
                {
                    int enemy = director.NearestLivingUnit(Side.Enemy, director.UnitCentroids[u]);
                    if (enemy >= 0) director.IssueOrder(u, OrderType.Charge, director.UnitCentroids[enemy]);
                });
            }
            if (kb.hKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) ForEachSelected(u => director.IssueOrder(u, OrderType.Hold, float3.zero));
            if (kb.jKey.wasPressedThisFrame) ForEachSelected(u => director.SetFacing(u, director.Units[u].Yaw + math.PI));
            if (kb.uKey.wasPressedThisFrame) { float3 rally = director.RallyPoint(Side.Player); ForEachSelected(u => director.IssueOrder(u, OrderType.Move, rally)); }
            if (ctrl && (kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed))
            {
                float turn = (kb.rightArrowKey.isPressed ? 1f : -1f) * wheelDegreesPerSecond * Mathf.Deg2Rad * dt;
                ForEachSelected(u => director.SetFacing(u, director.Units[u].Yaw + turn));
            }

            UpdateRings();
            if (Time.unscaledTime - _lastSummaryUpdate > 0.25f) { _lastSummaryUpdate = Time.unscaledTime; _summary = BuildSummary(); }
        }

        private static bool DigitPressed(Keyboard kb, int d)
        {
            switch (d)
            {
                case 0: return kb.digit0Key.wasPressedThisFrame; case 1: return kb.digit1Key.wasPressedThisFrame; case 2: return kb.digit2Key.wasPressedThisFrame;
                case 3: return kb.digit3Key.wasPressedThisFrame; case 4: return kb.digit4Key.wasPressedThisFrame; case 5: return kb.digit5Key.wasPressedThisFrame;
                case 6: return kb.digit6Key.wasPressedThisFrame; case 7: return kb.digit7Key.wasPressedThisFrame; case 8: return kb.digit8Key.wasPressedThisFrame;
                default: return kb.digit9Key.wasPressedThisFrame;
            }
        }

        private delegate void UnitAction(int unit);
        private void ForEachSelected(UnitAction action)
        {
            for (int u = 0; u < director.UnitCount && u < _selected.Length; u++) if (_selected[u]) action(u);
        }

        private void ClearSelection() { for (int i = 0; i < _selected.Length; i++) _selected[i] = false; }
        private void SelectAll() { for (int u = 0; u < director.UnitCount && u < _selected.Length; u++) _selected[u] = director.Units[u].Side == (byte)Side.Player && director.Units[u].Alive != 0; }

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
            if (_paused) return "PAUSED  (P resumes)";
            if (SelectedCount == 0) return $"{n} units under your command. Left-click a banner, Ctrl+A for all. WASD/arrows pan, Q/E rotate, wheel zooms, Home finds you.";
            string morale = string.Empty;
            for (int u = 0; u < director.UnitCount && u < _selected.Length; u++) if (_selected[u]) { morale = $"  |  {director.StanceOf(u)}, morale {Mathf.RoundToInt(director.MoraleOf(u) * 100)}%"; break; }
            return $"{SelectedCount} selected{morale}  |  RMB move  Alt+drag move facing  C/F charge  H hold  J about face  U withdraw  Ctrl+arrows wheel  Shift+1..4 formation  Ctrl+0..9 save group";
        }
    }
}

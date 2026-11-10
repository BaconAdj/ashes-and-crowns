using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    public enum ActivityKind : byte { None = 0, Pray = 1, Work = 2 }

    /// <summary>
    /// Hands-on activities that take real time at the place they happen. The body is held in a pose, the clock runs
    /// faster, needs and weather tick as they would, and the activity either runs until you rise (prayer) or across a
    /// fixed span punctuated by timing beats (work: press X as the marker crosses the band). Nothing here is a menu.
    /// </summary>
    public sealed class ActivityDirector : MonoBehaviour
    {
        public WorldClock clock;
        public HudController hud;
        public PlayerController player;
        public HorseController horse;
        public Transform body;                 // the visual child posed while kneeling or working
        public ProceduralGait gait;            // paused while posed

        private Balance _balance;
        private ActivityKind _kind;
        private float _savedTimeScale = 1f;
        private Vector3 _restPos; private Quaternion _restRot;
        private float _elapsedReal, _gameHoursDone;
        // prayer
        private long _prayDay = long.MinValue; private float _prayedHoursToday, _favourThisKneel;
        private System.Action<float, float> _onPrayerDone;   // hours, favour
        // work
        private float _workHours; private int _beatsPlanned, _beatsFired, _beatsPressed; private float _beatSum; private float _nextBeatAtHours; private bool _beatOpen; private float _beatT; private string _beatVerb;
        private System.Action<float> _onWorkDone;             // quality

        public ActivityKind Current => _kind;
        public bool Active => _kind != ActivityKind.None;

        private void Start() { _balance = GameBootstrap.Instance.Balance; }

        // ------------------------------------------------------------------ prayer

        /// <summary>Kneel until E or Space. onDone(hoursPrayed, favourEarned).</summary>
        public void BeginPrayer(System.Action<float, float> onDone)
        {
            if (Active || _balance == null) return;
            var cal = clock.Calendar; long today = Calendar.JulianDayNumber(cal.Year, cal.Month, cal.Day);
            if (today != _prayDay) { _prayDay = today; _prayedHoursToday = 0f; }
            _onPrayerDone = onDone; _favourThisKneel = 0f;
            Enter(ActivityKind.Pray, _balance.activity.prayTimeScale);
            Pose(kneel: true);
            hud.ShowActivity(L.T("You kneel"), L.T("Cold stone under your knees. Hold still. E or Space to rise."));
            SetPrayerText();
        }

        private void SetPrayerText()
        {
            int mins = Mathf.RoundToInt(_gameHoursDone * 60f);
            string seen = _favourThisKneel > 0.02f ? L.T("The village will hear of your piety.") : _favourThisKneel > 0.005f ? L.T("Someone in the nave notices.") : L.T("Nobody has noticed yet.");
            string cap = _prayedHoursToday > 2.5f ? L.T(" God has heard enough for one day.") : string.Empty;
            hud.SetActivity($"{mins / 60}h {mins % 60:00}m on your knees.  {seen}{cap}", L.T("E or Space to rise"));
        }

        // ------------------------------------------------------------------ work

        /// <summary>A day of labour: hours at the trade's pace with beats to time. onDone(quality 0..1).</summary>
        public void BeginWork(TradeDef def, float hours, System.Action<float> onDone)
        {
            if (Active || _balance == null) return;
            _onWorkDone = onDone; _workHours = hours;
            _beatsPlanned = _balance.activity.workBeats; _beatsFired = 0; _beatsPressed = 0; _beatSum = 0f; _beatOpen = false;
            _nextBeatAtHours = hours / (_beatsPlanned + 1);
            _beatVerb = def.momentKind == "stalk" ? L.T("Loose when the deer stills") : def.momentKind == "timing" ? L.T("Now, judge the moment") : L.T("Strike on the beat");
            Enter(ActivityKind.Work, _balance.activity.workTimeScale);
            Pose(kneel: false);
            hud.ShowActivity(L.T("At work: ") + def.id, L.T("Watch for the band. X when the marker crosses it. Esc abandons the day."));
            SetWorkText();
        }

        private void SetWorkText()
        {
            hud.SetActivity($"{Mathf.FloorToInt(_gameHoursDone)}h of {Mathf.RoundToInt(_workHours)}h.  Beats: {_beatsPressed} of {_beatsFired} taken, {Mathf.RoundToInt(_beatsFired > 0 ? _beatSum / _beatsFired * 100f : 0f)}% good.", _beatOpen ? _beatVerb + "  [ X ]" : L.T("Work on..."));
        }

        // ------------------------------------------------------------------ shared

        private void Enter(ActivityKind kind, float timeScale)
        {
            _kind = kind; _elapsedReal = 0f; _gameHoursDone = 0f;
            _savedTimeScale = clock.timeScale; clock.timeScale = timeScale;
            if (player != null) player.enabled = false;
            if (horse != null) horse.enabled = false;
            clock.HoursPassed += OnHours;
        }

        private void Leave()
        {
            clock.HoursPassed -= OnHours;
            clock.timeScale = _savedTimeScale;
            Unpose();
            hud.HideActivity(); hud.HideBeat();
            _kind = ActivityKind.None;
            if (player != null) player.enabled = horse == null || !horse.Mounted;
            if (horse != null) horse.enabled = true;
        }

        private void Pose(bool kneel)
        {
            if (body == null) return;
            _restPos = body.localPosition; _restRot = body.localRotation;
            if (gait != null) gait.enabled = false;
            if (kneel) { body.localPosition = _restPos + new Vector3(0f, -0.42f, 0.05f); body.localRotation = _restRot * Quaternion.Euler(18f, 0f, 0f); }
            else { body.localPosition = _restPos + new Vector3(0f, -0.08f, 0.1f); body.localRotation = _restRot * Quaternion.Euler(22f, 0f, 0f); }
        }

        private void Unpose()
        {
            if (body == null) return;
            body.localPosition = _restPos; body.localRotation = _restRot;
            if (gait != null) { gait.enabled = true; gait.CaptureRest(); }
        }

        private void OnHours(float h)
        {
            if (!Active) return;
            _gameHoursDone += h;
            if (_kind == ActivityKind.Pray)
            {
                float gain = ActivityRules.PrayerGain(_prayedHoursToday, h, _balance.activity);
                _prayedHoursToday += h; _favourThisKneel += gain;
            }
        }

        private void Update()
        {
            if (!Active) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            _elapsedReal += Time.unscaledDeltaTime;
            if (_kind == ActivityKind.Pray)
            {
                // a working bob of the shoulders while kneeling
                if (body != null) body.localRotation = _restRot * Quaternion.Euler(18f + Mathf.Sin(_elapsedReal * 1.1f) * 1.5f, 0f, 0f);
                if (Time.frameCount % 15 == 0) SetPrayerText();
                if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) && _elapsedReal > 0.4f) EndPrayer();
                return;
            }
            // ---- work
            if (body != null && !_beatOpen) body.localRotation = _restRot * Quaternion.Euler(22f + Mathf.Sin(_elapsedReal * 6f) * 6f, 0f, 0f);
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { EndWork(abandoned: true); return; }
            if (!_beatOpen && _beatsFired < _beatsPlanned && _gameHoursDone >= _nextBeatAtHours)
            {
                _beatOpen = true; _beatT = 0f; _beatsFired++;
                _nextBeatAtHours += _workHours / (_beatsPlanned + 1);
                hud.ShowBeat(_beatVerb, _balance.activity.beatZoneWidth);
                SetWorkText();
            }
            if (_beatOpen)
            {
                _beatT += Time.unscaledDeltaTime;
                float pos = ActivityRules.MarkerPosition(_beatT, _balance.activity.beatSweepSeconds);
                hud.SetBeatMarker(pos);
                bool pressed = kb != null && (kb.xKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
                if (pressed)
                {
                    float q = ActivityRules.BeatQuality(pos, _balance.activity.beatZoneWidth);
                    _beatSum += q; _beatsPressed++; _beatOpen = false;
                    hud.HideBeat(); hud.ShowToast(q > 0.8f ? L.T("Clean.") : q > 0.45f ? L.T("Good enough.") : q > 0.1f ? L.T("Late.") : L.T("Clumsy."));
                    SetWorkText();
                }
                else if (_beatT > _balance.activity.beatSweepSeconds * 2.2f) { _beatOpen = false; hud.HideBeat(); hud.ShowToast(L.T("The moment passes.")); SetWorkText(); }
            }
            else if (Time.frameCount % 20 == 0) SetWorkText();
            if (_gameHoursDone >= _workHours) EndWork(abandoned: false);
        }

        private void EndPrayer()
        {
            float hours = _gameHoursDone, favour = _favourThisKneel;
            if (hours * 60f < _balance.activity.prayMinMinutesToCount) favour = 0f;
            var done = _onPrayerDone; _onPrayerDone = null;
            Leave();
            done?.Invoke(hours, favour);
        }

        private void EndWork(bool abandoned)
        {
            float quality = abandoned ? 0f : ActivityRules.DayQuality(_beatSum, _beatsPressed, _beatsPlanned, _balance.activity);
            float hours = _gameHoursDone;
            var done = _onWorkDone; _onWorkDone = null;
            Leave();
            if (abandoned) { hud.ShowToast(L.T("You down tools after ") + Mathf.RoundToInt(hours) + L.T(" hours. Nothing to show for it.")); return; }
            done?.Invoke(quality);
        }
    }
}

using AshesAndCrowns.Battle;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Smoothed fps for the HUD, and a periodic log line so a run leaves numbers behind.</summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        public BattleDirector director;
        public float logEverySeconds = 10f;
        private float _accum, _worst = float.MaxValue, _timer;
        private int _frames;
        public float Fps { get; private set; }
        public float WorstFrameMs { get; private set; }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _accum += dt; _frames++; _timer += dt;
            if (dt > 0f) _worst = Mathf.Min(_worst, 1f / dt);
            if (_timer >= 0.5f) { Fps = _frames / _accum; WorstFrameMs = 1000f / _worst; _accum = 0f; _frames = 0; _timer = 0f; _worst = float.MaxValue; }
            if (logEverySeconds > 0f && Time.time > 2f && Mathf.FloorToInt(Time.time / logEverySeconds) != Mathf.FloorToInt((Time.time - dt) / logEverySeconds))
                Debug.Log($"[perf] {Fps:F0} fps avg, worst frame {WorstFrameMs:F1} ms, soldiers alive {(director != null ? director.PlayerAlive + director.EnemyAlive : 0)}");
        }
    }
}

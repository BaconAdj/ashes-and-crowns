using System.Collections.Generic;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Named audio cues that do not exist yet (see docs/audio-list.md). Logs once per name per few seconds; no allocation after warm-up.</summary>
    public static class AudioStubs
    {
        private static readonly Dictionary<string, float> _last = new Dictionary<string, float>(32);
        public const float MinGapSeconds = 4f;

        public static void Play(string name)
        {
            float now = Time.unscaledTime;
            if (_last.TryGetValue(name, out float t) && now - t < MinGapSeconds) return;
            _last[name] = now;
            Debug.Log("[audio stub] " + name + ".ogg");
        }
    }
}

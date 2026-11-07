using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// The church tower rings the canonical hours. Audio is a named stub until the clips exist
    /// (see docs/audio-list.md); the HUD toast and the log make the bell observable meanwhile.
    /// </summary>
    public sealed class BellTower : MonoBehaviour
    {
        public WorldClock clock;
        public AudioClip[] bellClips = new AudioClip[0]; // one per bell index, may be empty
        public UI.HudController hud;
        private AudioSource _source;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0.6f; _source.minDistance = 40f; _source.maxDistance = 1200f;
            _source.rolloffMode = AudioRolloffMode.Linear; _source.playOnAwake = false;
        }

        private void OnEnable() { if (clock != null) clock.BellRang += OnBell; }
        private void OnDisable() { if (clock != null) clock.BellRang -= OnBell; }

        private void OnBell(int index, string name)
        {
            AudioClip clip = index >= 0 && index < bellClips.Length ? bellClips[index] : null;
            if (clip != null) _source.PlayOneShot(clip);
            else Debug.Log($"[audio stub] bells_{name.ToLowerInvariant()}.ogg would play now ({clock.Calendar.ShortDate()} {clock.Calendar.HourOfDay:F1}h)");
            if (hud != null) hud.ShowBell(name);
        }
    }
}

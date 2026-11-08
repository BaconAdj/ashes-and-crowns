using System.Collections;
using AshesAndCrowns.UI;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Three to five somber lines over a slow establishing shot, then cold reality. Any key skips.</summary>
    public sealed class OpeningNarration : MonoBehaviour
    {
        public HudController hud;
        public Camera gameplayCamera;
        public float secondsPerLine = 4.5f;
        public bool Running { get; private set; }

        public void Play(string[] lines, Transform focus, System.Action onDone)
        {
            if (Running) return;
            StartCoroutine(Run(lines, focus, onDone));
        }

        private IEnumerator Run(string[] lines, Transform focus, System.Action onDone)
        {
            Running = true;
            hud.ShowNarration(string.Empty, 1f);
            // Establishing shot: high and slow, drifting toward the player.
            Vector3 start = focus.position + new Vector3(-18f, 14f, -26f);
            Vector3 end = focus.position + new Vector3(-4f, 3f, -7f);
            float total = lines.Length * secondsPerLine;
            float t = 0f;
            var tpc = gameplayCamera.GetComponent<ThirdPersonCamera>();
            if (tpc != null) tpc.enabled = false;
            int line = -1;
            while (t < total)
            {
                int wanted = Mathf.Min(lines.Length - 1, (int)(t / secondsPerLine));
                if (wanted != line)
                {
                    line = wanted;
                    hud.ShowNarration(lines[line], 0f);
                    Debug.Log("[audio stub] narr_opening_" + (line + 1).ToString("00") + ".ogg: " + lines[line]);
                }
                float k = t / total;
                gameplayCamera.transform.position = Vector3.Lerp(start, end, k * k * (3f - 2f * k));
                gameplayCamera.transform.LookAt(focus.position + Vector3.up * 1.4f);
                var kb = UnityEngine.InputSystem.Keyboard.current; var mouse = UnityEngine.InputSystem.Mouse.current;
                if ((kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame)) break;
                t += Time.deltaTime;
                yield return null;
            }
            hud.HideNarration();
            if (tpc != null) tpc.enabled = true;
            Running = false;
            onDone?.Invoke();
        }
    }
}

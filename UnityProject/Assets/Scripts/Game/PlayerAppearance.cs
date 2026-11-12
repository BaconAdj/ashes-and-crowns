using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Applies the character's presets to the placeholder body: height, build, skin and hair tints. Phase 6 brings a real rig.</summary>
    public sealed class PlayerAppearance : MonoBehaviour
    {
        public Transform body;     // the modelled figure (Art/Characters/Man) or the placeholder capsule
        public Transform head;     // placeholder sphere; null when a modelled figure is used
        private MaterialPropertyBlock _block;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void Apply(CharacterSheet c)
        {
            if (c == null) return;
            float h = Mathf.Clamp(c.Height, 1.5f, 1.95f) / 1.72f;
            float w = Mathf.Lerp(0.85f, 1.2f, Mathf.Clamp01(c.Build));
            bool meshBody = body != null && body.GetComponent<MeshFilter>() != null && head == null;
            if (meshBody)
            {
                // A modelled figure stands on its own feet at the origin: scale it, never lift it.
                body.localScale = new Vector3(Mathf.Lerp(0.95f, 1.08f, Mathf.Clamp01(c.Build)), h, Mathf.Lerp(0.95f, 1.08f, Mathf.Clamp01(c.Build)));
                body.localPosition = Vector3.zero;
            }
            else if (body != null) { body.localScale = new Vector3(0.55f * w, 0.75f * h, 0.55f * w); body.localPosition = new Vector3(0f, 0.95f * h, 0f); }
            if (head != null) head.localPosition = new Vector3(0f, 1.78f * h, 0f);
            var gait = GetComponent<ProceduralGait>(); if (gait != null) gait.CaptureRest();
            var cc = GetComponent<CharacterController>();
            float baseHeight = GameBootstrap.Instance != null ? GameBootstrap.Instance.Balance.player.height : 1.8f;
            // The body that collides is the same for everyone: a tall character must still pass every door a short one does.
            if (cc != null) { cc.height = baseHeight; cc.center = new Vector3(0f, baseHeight * 0.5f, 0f); }
            _block = _block ?? new MaterialPropertyBlock();
            Color skin = Color.Lerp(new Color(0.78f, 0.66f, 0.55f), new Color(0.35f, 0.25f, 0.18f), Mathf.Clamp01(c.SkinTone)) * 0.9f;
            Color hair = Color.Lerp(new Color(0.75f, 0.62f, 0.4f), new Color(0.12f, 0.1f, 0.08f), Mathf.Clamp01(c.HairTone));
            if (head != null) { var r = head.GetComponent<Renderer>(); if (r != null) { _block.SetColor(BaseColor, Color.Lerp(skin, hair, 0.35f)); r.SetPropertyBlock(_block); } }
            Color coat = c.Rank >= Rank.Knight ? new Color(0.30f, 0.31f, 0.36f) : c.Rank == Rank.Warrior ? new Color(0.28f, 0.24f, 0.2f) : new Color(0.33f, 0.28f, 0.2f);
            coat = Color.Lerp(coat, coat * (0.8f + c.AppearancePreset * 0.1f), 0.5f);
            if (body != null) { var r = body.GetComponent<Renderer>(); if (r != null) { _block.SetColor(BaseColor, coat); r.SetPropertyBlock(_block); } }
        }
    }
}

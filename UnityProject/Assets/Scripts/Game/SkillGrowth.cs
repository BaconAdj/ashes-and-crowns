using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Skills grow with use: riding while moving mounted, one-handed on every swing. Phase 4 adds the trades.</summary>
    public sealed class SkillGrowth : MonoBehaviour
    {
        public HorseController horse;
        public MeleeController melee;
        private float _rideAccum;

        private void OnEnable() { if (melee != null) melee.Swung += OnSwung; }
        private void OnDisable() { if (melee != null) melee.Swung -= OnSwung; }

        private void Update()
        {
            var ctx = RunSession.Context;
            if (ctx == null || horse == null || !horse.Mounted || horse.Speed < 1f) return;
            _rideAccum += Time.deltaTime * (horse.Galloping ? 2f : 1f);
            if (_rideAccum >= 8f) { _rideAccum = 0f; ctx.Character.Use(Skill.Riding, GameBootstrap.Instance.Balance.character); }
        }

        private void OnSwung() => RunSession.Context?.Character.Use(Skill.OneHanded, GameBootstrap.Instance.Balance.character, 0.5f);
    }
}

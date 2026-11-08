using System;

namespace AshesAndCrowns.Sim
{
    public enum WeatherKind : byte { Clear = 0, Overcast = 1, Fog = 2, Rain = 3, Snow = 4 }

    [Serializable] public sealed class WeatherBalance
    {
        public float changeIntervalHours = 4f;
        /// <summary>Per month (January first): probability that a new spell is of that kind. The remainder is Clear.</summary>
        public float[] overcastByMonth = { 0.40f, 0.38f, 0.35f, 0.30f, 0.28f, 0.25f, 0.24f, 0.25f, 0.30f, 0.36f, 0.40f, 0.42f };
        public float[] rainByMonth     = { 0.28f, 0.25f, 0.24f, 0.22f, 0.20f, 0.18f, 0.18f, 0.20f, 0.22f, 0.28f, 0.30f, 0.28f };
        public float[] fogByMonth      = { 0.10f, 0.09f, 0.08f, 0.06f, 0.05f, 0.04f, 0.04f, 0.05f, 0.08f, 0.12f, 0.12f, 0.11f };
        public float[] snowByMonth     = { 0.10f, 0.09f, 0.04f, 0.01f, 0f, 0f, 0f, 0f, 0f, 0f, 0.02f, 0.07f };
        public float blendHours = 0.5f;          // how long a change takes to fully set in
        public float fogDensityClear = 0.004f, fogDensityOvercast = 0.008f, fogDensityRain = 0.014f, fogDensityFog = 0.045f, fogDensitySnow = 0.018f;
        public float windClear = 0.2f, windOvercast = 0.4f, windRain = 0.7f, windFog = 0.05f, windSnow = 0.5f;
    }

    public struct WeatherSnapshot
    {
        public byte Kind; public float UntilChange, Precipitation, Fog, Wind, Cloud;
    }

    /// <summary>
    /// Spell-based weather: every changeIntervalHours a new spell is drawn from the month's table.
    /// Precipitation and fog blend smoothly toward the spell's targets so the renderer never pops.
    /// </summary>
    public sealed class WeatherModel
    {
        private readonly WeatherBalance _b;
        private readonly DeterministicRng _rng;
        private float _untilChange;

        public WeatherKind Current { get; private set; } = WeatherKind.Overcast;
        public float Precipitation { get; private set; }     // 0..1, rain or snow strength (see Current)
        public float FogDensityFactor { get; private set; }  // absolute exponential-squared density to apply
        public float Wind { get; private set; }
        public float CloudCover { get; private set; }        // 0..1 for sky/sun dimming

        public WeatherModel(WeatherBalance balance, DeterministicRng rng)
        {
            _b = balance ?? throw new ArgumentNullException(nameof(balance));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _untilChange = balance.changeIntervalHours;
            FogDensityFactor = balance.fogDensityOvercast; Wind = balance.windOvercast; CloudCover = 0.8f;
        }

        public ulong RngState { get => _rng.State; set => _rng.State = value; }

        public WeatherSnapshot Export() => new WeatherSnapshot { Kind = (byte)Current, UntilChange = _untilChange, Precipitation = Precipitation, Fog = FogDensityFactor, Wind = Wind, Cloud = CloudCover };

        public void Import(WeatherSnapshot s)
        {
            Current = (WeatherKind)s.Kind; _untilChange = s.UntilChange; Precipitation = s.Precipitation; FogDensityFactor = s.Fog; Wind = s.Wind; CloudCover = s.Cloud;
        }

        public void AdvanceHours(float hours, int month)
        {
            if (hours <= 0f) return;
            month = Math.Max(1, Math.Min(12, month));
            _untilChange -= hours;
            while (_untilChange <= 0f) { Roll(month - 1); _untilChange += _b.changeIntervalHours; }
            float t = _b.blendHours <= 0f ? 1f : Math.Min(1f, hours / _b.blendHours);
            Precipitation += (TargetPrecipitation() - Precipitation) * t;
            FogDensityFactor += (TargetFog() - FogDensityFactor) * t;
            Wind += (TargetWind() - Wind) * t;
            CloudCover += (TargetCloud() - CloudCover) * t;
            Precipitation = Clamp01(Precipitation); CloudCover = Clamp01(CloudCover);
        }

        private void Roll(int m)
        {
            float roll = _rng.NextFloat();
            float snow = _b.snowByMonth[m], rain = _b.rainByMonth[m], fog = _b.fogByMonth[m], over = _b.overcastByMonth[m];
            if (roll < snow) Current = WeatherKind.Snow;
            else if (roll < snow + rain) Current = WeatherKind.Rain;
            else if (roll < snow + rain + fog) Current = WeatherKind.Fog;
            else if (roll < snow + rain + fog + over) Current = WeatherKind.Overcast;
            else Current = WeatherKind.Clear;
        }

        private float TargetPrecipitation() => Current == WeatherKind.Rain || Current == WeatherKind.Snow ? 1f : 0f;
        private float TargetFog()
        {
            switch (Current)
            {
                case WeatherKind.Clear: return _b.fogDensityClear;
                case WeatherKind.Fog: return _b.fogDensityFog;
                case WeatherKind.Rain: return _b.fogDensityRain;
                case WeatherKind.Snow: return _b.fogDensitySnow;
                default: return _b.fogDensityOvercast;
            }
        }
        private float TargetWind()
        {
            switch (Current)
            {
                case WeatherKind.Clear: return _b.windClear;
                case WeatherKind.Fog: return _b.windFog;
                case WeatherKind.Rain: return _b.windRain;
                case WeatherKind.Snow: return _b.windSnow;
                default: return _b.windOvercast;
            }
        }
        private float TargetCloud()
        {
            switch (Current)
            {
                case WeatherKind.Clear: return 0.15f;
                case WeatherKind.Fog: return 0.9f;
                case WeatherKind.Rain: return 1f;
                case WeatherKind.Snow: return 1f;
                default: return 0.8f;
            }
        }
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}

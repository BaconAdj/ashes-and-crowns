using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Drives the sun, ambient light and fog colour from the clock and the weather. Cheap: a few lerps per frame.</summary>
    public sealed class DayNightController : MonoBehaviour
    {
        public WorldClock clock;
        public Light sun;
        private Balance _balance;
        private Color _daySun, _dawnSun, _dayAmbient, _nightAmbient, _dayFog, _nightFog;

        public float CurrentDaylight { get; private set; }

        public void Init(Balance balance)
        {
            _balance = balance;
            var l = balance.look;
            _daySun = Col(l.sunColor); _dawnSun = Col(l.dawnSunColor);
            _dayAmbient = Col(l.ambientColor); _nightAmbient = Col(l.nightAmbientColor);
            _dayFog = Col(l.fogColor); _nightFog = Col(l.nightFogColor);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        private void LateUpdate()
        {
            if (_balance == null || clock == null || !clock.IsReady || sun == null) return;
            var cal = clock.Calendar;
            float hour = cal.HourOfDay; int doy = cal.DayOfYear;
            float elevation = Daylight.SunElevationDegrees(hour, doy);
            float azimuth = Daylight.SunAzimuthDegrees(hour, doy);
            float daylight = Daylight.DaylightFactor(hour, doy);
            CurrentDaylight = daylight;
            float cloud = clock.Weather.CloudCover;
            float cloudDim = Mathf.Lerp(1f, _balance.look.cloudSunDimming, cloud);

            // Sun: real elevation by day, a dim "moon" from the opposite side by night.
            bool night = elevation < -2f;
            float lightElevation = night ? Mathf.Clamp(-elevation, 10f, 60f) : Mathf.Max(elevation, 3f);
            float lightAzimuth = night ? azimuth + 180f : azimuth;
            sun.transform.rotation = Quaternion.Euler(lightElevation, lightAzimuth + 180f, 0f); // Unity light points along +Z; azimuth is where the sun is
            float lowSun = Mathf.InverseLerp(12f, 0f, elevation); // 1 near the horizon
            Color sunColor = Color.Lerp(_daySun, _dawnSun, lowSun * 0.85f);
            float sunIntensity = night
                ? _balance.look.moonIntensity * (1f - cloud * 0.7f)
                : Mathf.Lerp(_balance.look.dawnSunIntensity, _balance.look.noonSunIntensity, Mathf.InverseLerp(0f, 35f, elevation)) * cloudDim * Mathf.Max(daylight, 0.15f);
            sun.color = night ? new Color(0.55f, 0.6f, 0.75f) : sunColor;
            sun.intensity = sunIntensity;
            sun.shadowStrength = Mathf.Lerp(0.5f, 0.9f, 1f - cloud);

            Color ambient = Color.Lerp(_nightAmbient, _dayAmbient, daylight) * Mathf.Lerp(1f, 0.8f, cloud);
            RenderSettings.ambientLight = ambient;
            Color fog = Color.Lerp(_nightFog, _dayFog, daylight);
            fog = Color.Lerp(fog, fog * 0.85f, cloud * 0.5f);
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = clock.Weather.FogDensityFactor;
            Camera main = Camera.main;
            if (main != null) main.backgroundColor = fog;
        }

        private static Color Col(float[] c) => c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2]) : Color.grey;
    }
}

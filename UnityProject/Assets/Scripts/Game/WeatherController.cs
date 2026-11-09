using AshesAndCrowns.Sim;
using UnityEngine;

namespace AshesAndCrowns.Game
{
    /// <summary>Rain and snow particle systems that follow the camera, driven by the weather model.</summary>
    public sealed class WeatherController : MonoBehaviour
    {
        public WorldClock clock;
        public Transform follow;
        private Balance _balance;
        private ParticleSystem _rain, _snow;
        private ParticleSystem.EmissionModule _rainEmit, _snowEmit;
        private string _lastKind = string.Empty;
        private float _lastRain = -1f, _lastSnow = -1f;

        public string Description { get; private set; } = string.Empty;

        public void Init(Balance balance, Material rainMaterial, Material snowMaterial)
        {
            _balance = balance;
            _rain = MakeSystem("Rain", rainMaterial, new Vector3(0f, -22f, 0f), 0.9f, new Vector2(0.03f, 0.35f), 1.2f, 4000);
            _snow = MakeSystem("Snow", snowMaterial, new Vector3(0f, -2.2f, 0f), 9f, new Vector2(0.07f, 0.07f), 0.6f, 12000);
            _rainEmit = _rain.emission; _snowEmit = _snow.emission;
        }

        private ParticleSystem MakeSystem(string name, Material mat, Vector3 velocity, float lifetime, Vector2 size, float noise, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.startLifetime = lifetime; main.startSpeed = 0f; main.maxParticles = maxParticles;
            main.startSize3D = true; main.startSizeX = size.x; main.startSizeY = size.y; main.startSizeZ = size.x;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0f;
            main.startColor = new Color(0.75f, 0.78f, 0.82f, 0.55f);
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(40f, 1f, 40f); shape.position = new Vector3(0f, 18f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(velocity.x - noise, velocity.x + noise);
            vel.y = new ParticleSystem.MinMaxCurve(velocity.y, velocity.y);   // same mode as x and z or Unity warns every frame
            vel.z = new ParticleSystem.MinMaxCurve(velocity.z - noise, velocity.z + noise);
            var emission = ps.emission; emission.rateOverTime = 0f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = mat; r.renderMode = name == "Rain" ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.lengthScale = 0f; r.velocityScale = name == "Rain" ? 0.08f : 0f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private void LateUpdate()
        {
            if (_balance == null || clock == null || !clock.IsReady) return;
            if (follow != null) transform.position = follow.position;
            var w = clock.Weather;
            float p = w.Precipitation;
            bool snow = w.Current == WeatherKind.Snow;
            float rain = snow ? 0f : p * _balance.look.rainParticlesPerSecond, snowRate = snow ? p * _balance.look.snowParticlesPerSecond : 0f;
            if (rain != _lastRain) { _rainEmit.rateOverTime = rain; _lastRain = rain; }
            if (snowRate != _lastSnow) { _snowEmit.rateOverTime = snowRate; _lastSnow = snowRate; }
            string kind = w.Current.ToString();
            if (kind != _lastKind) { _lastKind = kind; Description = Describe(w.Current); }
        }

        private static string Describe(WeatherKind k)
        {
            switch (k)
            {
                case WeatherKind.Clear: return "A clear, cold sky.";
                case WeatherKind.Fog: return "Fog lies on the land.";
                case WeatherKind.Rain: return "Rain.";
                case WeatherKind.Snow: return "Snow is falling.";
                default: return "Low grey cloud.";
            }
        }
    }
}

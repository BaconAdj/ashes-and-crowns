using UnityEngine;
namespace UnityEngine.Rendering.Universal
{
    public class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing, renderShadows; public int antialiasing; public LayerMask volumeLayerMask; }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera c) => null; }
    public class ScriptableRendererData : ScriptableObject { } public class UniversalRendererData : ScriptableRendererData { }
    public class UniversalRenderPipelineAsset : UnityEngine.Rendering.RenderPipelineAsset { public static string packagePath; public static UniversalRenderPipelineAsset Create(ScriptableRendererData d) => null; public float shadowDistance; public int msaaSampleCount; public bool supportsHDR; public int shadowCascadeCount; public float renderScale; public bool supportsCameraDepthTexture; public override UnityEngine.Rendering.RenderPipeline CreatePipeline() => null; }
    public class Bloom : VolumeComponent { public MinFloatParameter intensity, threshold, scatter; } public class Vignette : VolumeComponent { public ClampedFloatParameter intensity, smoothness; }
    public class ColorAdjustments : VolumeComponent { public FloatParameter postExposure; public ClampedFloatParameter contrast, saturation; public ColorParameter colorFilter; }
    public class Tonemapping : VolumeComponent { public TonemappingModeParameter mode; } public enum TonemappingMode { None, Neutral, ACES } public class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }
    public class WhiteBalance : VolumeComponent { public ClampedFloatParameter temperature, tint; } public class FilmGrain : VolumeComponent { public ClampedFloatParameter intensity, response; public VolumeParameter<FilmGrainLookup> type; } public enum FilmGrainLookup { Thin1, Thin2, Medium1, Medium2, Medium3 }
    public class LiftGammaGain : VolumeComponent { public Vector4Parameter lift, gamma, gain; } public class DepthOfField : VolumeComponent { }
}
namespace UnityEngine.Rendering
{
    public class VolumeParameter { public bool overrideState; } public class VolumeParameter<T> : VolumeParameter { public T value; public void Override(T v) { } }
    public class FloatParameter : VolumeParameter<float> { } public class MinFloatParameter : FloatParameter { } public class ClampedFloatParameter : FloatParameter { } public class ColorParameter : VolumeParameter<Color> { } public class Vector4Parameter : VolumeParameter<Vector4> { } public class BoolParameter : VolumeParameter<bool> { }
    public class VolumeComponent : ScriptableObject { public bool active; }
    public class VolumeProfile : ScriptableObject { public T Add<T>(bool overrides = false) where T : VolumeComponent => null; public bool TryGet<T>(out T c) where T : VolumeComponent { c = null; return false; } public bool Has<T>() where T : VolumeComponent => false; public System.Collections.Generic.List<VolumeComponent> components; }
    public class Volume : MonoBehaviour { public bool isGlobal; public float priority, weight; public VolumeProfile profile, sharedProfile; }
    public static class ResourceReloader { public static bool TryReloadAllNullIn(object o, string p) => false; public static void ReloadAllNullIn(object o, string p) { } }
}

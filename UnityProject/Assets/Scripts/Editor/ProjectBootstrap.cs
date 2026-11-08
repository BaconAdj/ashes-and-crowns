using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>
    /// Code-driven project setup, because nobody can click in the editor for us. Runs once per editor session
    /// on load and is idempotent: it only creates what is missing. Everything is also available from the
    /// "Ashes and Crowns" menu.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        private const string SessionKey = "AshesAndCrowns.Bootstrapped";

        static ProjectBootstrap()
        {
            if (Application.isBatchMode) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) EnsureEverything(openScene: true); };
        }

        [MenuItem("Ashes and Crowns/Setup Project (URP, Input, Settings)", priority = 0)]
        public static void SetupProjectMenu() => EnsureEverything(openScene: false);

        [MenuItem("Ashes and Crowns/Open England Scene (Phase 1)", priority = 1)]
        public static void OpenEnglandMenu()
        {
            if (!File.Exists(Paths.Phase1Scene)) RebuildEnglandMenu();
            EditorSceneManager.OpenScene(Paths.Phase1Scene);
        }

        [MenuItem("Ashes and Crowns/Open Battlefield Scene (Phase 0)", priority = 2)]
        public static void OpenSceneMenu()
        {
            if (!File.Exists(Paths.Phase0Scene)) RebuildSceneMenu();
            EditorSceneManager.OpenScene(Paths.Phase0Scene);
        }

        [MenuItem("Ashes and Crowns/Play England", priority = 3)]
        public static void PlayEnglandMenu() { OpenEnglandMenu(); EditorApplication.isPlaying = true; }

        [MenuItem("Ashes and Crowns/Play Battlefield", priority = 4)]
        public static void PlayMenu() { OpenSceneMenu(); EditorApplication.isPlaying = true; }

        [MenuItem("Ashes and Crowns/Rebuild/England Scene", priority = 20)]
        public static void RebuildEnglandMenu()
        {
            EnsureProjectSettings();
            Phase1SceneBuilder.Build();
            RegisterScenes();
        }

        [MenuItem("Ashes and Crowns/Rebuild/Battlefield Scene", priority = 21)]
        public static void RebuildSceneMenu()
        {
            EnsureProjectSettings();
            Phase0SceneBuilder.Build();
            RegisterScenes();
        }

        [MenuItem("Ashes and Crowns/Rebuild/Siege Scene", priority = 22)]
        public static void RebuildSiegeSceneMenu()
        {
            EnsureProjectSettings();
            Phase0SceneBuilder.Build(siege: true);
            RegisterScenes();
        }

        /// <summary>Batch-mode entry point: unity run ... -executeMethod AshesAndCrowns.EditorTools.ProjectBootstrap.SetupHeadless</summary>
        public static void SetupHeadless()
        {
            EnsureEverything(openScene: false);
            AssetDatabase.SaveAssets();
        }

        public static void EnsureEverything(bool openScene)
        {
            EnsureProjectSettings();
            bool built = false;
            if (!File.Exists(Paths.Phase0Scene)) { Phase0SceneBuilder.Build(); built = true; }
            if (!File.Exists(Paths.Phase1Scene)) { Phase1SceneBuilder.Build(); built = true; }
            if (!File.Exists(Paths.Phase9Scene)) { Phase0SceneBuilder.Build(siege: true); built = true; }
            RegisterScenes();
            if (openScene && !Application.isBatchMode && File.Exists(Paths.Phase1Scene) && EditorSceneManager.GetActiveScene().path != Paths.Phase1Scene)
                EditorSceneManager.OpenScene(Paths.Phase1Scene);
            Debug.Log("[Ashes and Crowns] project setup verified" + (built ? "; scenes generated." : "."));
        }

        public static void EnsureProjectSettings()
        {
            EnsureFolders();
            EnsureUrp();
            EnsurePlayerSettings();
            EnsureUiToolkit();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolders()
        {
            foreach (string f in new[] { Paths.Settings, Paths.Generated, Paths.Palette, Paths.UiFolder, Paths.Scenes })
                if (!AssetDatabase.IsValidFolder(f)) Directory.CreateDirectory(f);
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- URP

        public static UniversalRenderPipelineAsset EnsureUrp()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Paths.UrpAsset);
            if (asset == null)
            {
                try
                {
                    var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    ResourceReloader.TryReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                    AssetDatabase.CreateAsset(renderer, Paths.UrpRenderer);
                    asset = UniversalRenderPipelineAsset.Create(renderer);
                    asset.shadowDistance = 160f;
                    asset.supportsCameraDepthTexture = true;
                    asset.msaaSampleCount = 2;
                    AssetDatabase.CreateAsset(asset, Paths.UrpAsset);
                    Debug.Log("[Ashes and Crowns] created URP asset at " + Paths.UrpAsset);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[Ashes and Crowns] could not create the URP asset in code: " + e.Message +
                                   "\nFallback: Assets > Create > Rendering > URP Asset (with Universal Renderer), save it as " + Paths.UrpAsset + ", then run Ashes and Crowns > Setup Project again.");
                    return null;
                }
            }
            if (GraphicsSettings.defaultRenderPipeline != asset) GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(Mathf.Max(current, QualitySettings.names.Length - 1), false);
            return asset;
        }

        // ---------------------------------------------------------------- Player settings

        private static void EnsurePlayerSettings()
        {
            if (PlayerSettings.productName != "Ashes and Crowns") PlayerSettings.productName = "Ashes and Crowns";
            if (PlayerSettings.companyName != "BaconAdj") PlayerSettings.companyName = "BaconAdj";
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;

            // Input System: 0 = old input manager, 1 = new Input System, 2 = both. Needs an editor restart to apply.
            var so = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var handler = so.FindProperty("activeInputHandler");
            if (handler != null && handler.intValue != 1 && handler.intValue != 2)
            {
                handler.intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.LogWarning("[Ashes and Crowns] enabled the new Input System in Player Settings. Unity needs a restart for this to take effect: close and reopen the project once.");
            }
        }

        // ---------------------------------------------------------------- UI Toolkit

        public static PanelSettings EnsureUiToolkit()
        {
            if (!File.Exists(Paths.Theme))
            {
                File.WriteAllText(Paths.Theme, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(Paths.Theme);
            }
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(Paths.PanelSettings);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ps.referenceResolution = new Vector2Int(1920, 1080);
                ps.match = 0.5f;
                AssetDatabase.CreateAsset(ps, Paths.PanelSettings);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Paths.Theme);
            if (theme != null && ps.themeStyleSheet != theme) { ps.themeStyleSheet = theme; EditorUtility.SetDirty(ps); }
            return ps;
        }

        private static void RegisterScenes()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            if (File.Exists(Paths.Phase1Scene)) list.Add(new EditorBuildSettingsScene(Paths.Phase1Scene, true));
            if (File.Exists(Paths.Phase0Scene)) list.Add(new EditorBuildSettingsScene(Paths.Phase0Scene, true));
            if (File.Exists(Paths.Phase9Scene)) list.Add(new EditorBuildSettingsScene(Paths.Phase9Scene, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}

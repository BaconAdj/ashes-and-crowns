namespace AshesAndCrowns.EditorTools
{
    public static class Paths
    {
        public const string Settings = "Assets/Settings";
        public const string Generated = "Assets/Generated";
        public const string Palette = "Assets/Art/Palette";
        public const string UiFolder = "Assets/UI";
        public const string Scenes = "Assets/Scenes";
        public const string UrpRenderer = Settings + "/AshesURP_Renderer.asset";
        public const string UrpAsset = Settings + "/AshesURP.asset";
        public const string VolumeProfile = Settings + "/GrimLook.asset";
        public const string UiResources = UiFolder + "/Resources";
        public const string PanelSettings = UiResources + "/AshesPanelSettings.asset";   // under Resources: the HUD can Resources.Load it if a scene lost the reference
        public const string Theme = UiFolder + "/AshesTheme.tss";
        public const string Phase0Scene = Scenes + "/Phase0_Battlefield.unity";
        public const string Phase1Scene = Scenes + "/Phase1_England.unity";
        public const string Phase9Scene = Scenes + "/Phase9_Siege.unity";
        public const string TownScene = Scenes + "/Town_Wallingford.unity";
    }
}

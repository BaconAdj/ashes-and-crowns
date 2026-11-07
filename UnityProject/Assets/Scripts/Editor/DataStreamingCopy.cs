using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>Copies Assets/Data/*.json into StreamingAssets for player builds, then removes the copy.</summary>
    public sealed class DataStreamingCopy : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string Source = "Assets/Data";
        private const string Target = "Assets/StreamingAssets/Data";
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Directory.CreateDirectory(Target);
            foreach (string file in Directory.GetFiles(Source, "*.json"))
                File.Copy(file, Path.Combine(Target, Path.GetFileName(file)), true);
            AssetDatabase.Refresh();
            Debug.Log("[Ashes and Crowns] copied data files to StreamingAssets for the build");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (Directory.Exists("Assets/StreamingAssets"))
            {
                FileUtil.DeleteFileOrDirectory("Assets/StreamingAssets");
                FileUtil.DeleteFileOrDirectory("Assets/StreamingAssets.meta");
                AssetDatabase.Refresh();
            }
        }
    }
}

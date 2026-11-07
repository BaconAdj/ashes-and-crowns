using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AshesAndCrowns.EditorTools
{
    /// <summary>
    /// Headless builds:  unity build UnityProject --target StandaloneWindows64 --execute-method AshesAndCrowns.EditorTools.BatchBuild.BuildWindows
    /// </summary>
    public static class BatchBuild
    {
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Build/Windows/AshesAndCrowns.exe");
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Build/macOS/AshesAndCrowns.app");
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Build/Linux/AshesAndCrowns.x86_64");

        private static void Build(BuildTarget target, string output)
        {
            ProjectBootstrap.EnsureEverything(openScene: false);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Paths.Phase0Scene },
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Ashes and Crowns] build {report.summary.result}: {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors");
            if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}

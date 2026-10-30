// UpgradeAndBuild.cs (added October 2026)
//
// Used by the "Unity upgrade & build" GitHub workflow (.github/workflows/unity-upgrade.yml).
// When a newer Unity opens this 2017 project, it upgrades every scene, prefab and
// setting in memory. This script then:
//   1. switches the project to TEXT asset files (instead of binary), so future changes
//      show up as readable diffs on GitHub,
//   2. re-saves every asset in the new Unity format, and
//   3. builds the game for Linux to prove it compiles and builds cleanly.
// The workflow then commits the upgraded files back to the branch.
//
// You can also run it by hand: Tools > Frogs-N-Logs > Upgrade And Build.
// It's not a leap of faith - it's a leap of frog.

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FrogsNLogs.EditorTools
{
    public static class UpgradeAndBuild
    {
        private const string FallbackScene = "Assets/_Scenes.unity";

        [MenuItem("Tools/Frogs-N-Logs/Upgrade And Build")]
        public static void RunFromMenu()
        {
            Execute(exitWhenDone: false);
        }

        // Entry point for batch mode (-executeMethod FrogsNLogs.EditorTools.UpgradeAndBuild.Run).
        public static void Run()
        {
            Execute(exitWhenDone: true);
        }

        private static void Execute(bool exitWhenDone)
        {
            int exitCode;
            try
            {
                Debug.Log("[FrogsUpgrade] Unity " + Application.unityVersion + ": switching assets to text format...");
                EditorSettings.serializationMode = SerializationMode.ForceText;

                Debug.Log("[FrogsUpgrade] Re-saving every asset in the new format...");
                AssetDatabase.ForceReserializeAssets();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string[] scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                    .Select(s => s.path)
                    .ToArray();
                if (scenes.Length == 0)
                {
                    scenes = new[] { FallbackScene };
                }

                string outputPath = Environment.GetEnvironmentVariable("FROGS_BUILD_PATH");
                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = "Build/Linux/FrogsNLogs.x86_64";
                }

                Debug.Log("[FrogsUpgrade] Building " + string.Join(", ", scenes) + " -> " + outputPath);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneLinux64,
                    options = BuildOptions.None
                });

                BuildSummary summary = report.summary;
                Debug.Log("[FrogsUpgrade] Build result: " + summary.result
                          + " | errors: " + summary.totalErrors
                          + " | warnings: " + summary.totalWarnings
                          + " | size: " + summary.totalSize + " bytes");
                exitCode = summary.result == BuildResult.Succeeded ? 0 : 2;
            }
            catch (Exception e)
            {
                Debug.LogError("[FrogsUpgrade] Upgrade failed: " + e);
                exitCode = 3;
            }

            if (exitWhenDone)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}

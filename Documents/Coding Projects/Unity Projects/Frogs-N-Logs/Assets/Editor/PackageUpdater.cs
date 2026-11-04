// PackageUpdater.cs (added Nov 2026): keeps the Unity packages the game uses on their newest
// versions for this editor. Runs on GitHub's servers before each build (UpgradeAndBuild.Run).
// Asking the Package Manager to "add" a package without a version installs the newest
// version that's verified for this Unity release.
using System.Threading;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace FrogsNLogs.EditorTools
{
    public static class PackageUpdater
    {
        private static readonly string[] Packages =
        {
            "com.unity.render-pipelines.universal",
            "com.unity.ai.navigation",
            "com.unity.ugui",
            "com.unity.test-framework",
            "com.unity.ide.rider",
            "com.unity.ide.visualstudio",
        };

        public static void UpdateAll()
        {
            foreach (string name in Packages)
            {
                AddRequest request = Client.Add(name);
                float waited = 0f;
                while (!request.IsCompleted && waited < 180f)
                {
                    Thread.Sleep(200);
                    waited += 0.2f;
                }
                if (request.Status == StatusCode.Success)
                {
                    Debug.Log("[FrogsPackages] " + name + " -> " + request.Result.version);
                }
                else
                {
                    Debug.LogWarning("[FrogsPackages] Couldn't update " + name + ": "
                                     + (request.Error != null ? request.Error.message : "timed out"));
                }
            }
        }
    }
}

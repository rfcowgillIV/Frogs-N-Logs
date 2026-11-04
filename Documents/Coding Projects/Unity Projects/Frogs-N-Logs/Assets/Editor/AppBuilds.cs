// AppBuilds.cs (added Nov 2026): builds the game as a real app for each platform.
//
// Used by .github/workflows/apps.yml, one Unity session per platform:
//   -executeMethod FrogsNLogs.EditorTools.AppBuilds.Build
//   FROGS_APP_TARGET = android | windows | mac | linux | ios
//   FROGS_APP_PATH   = output folder
//   FROGS_VERSION    = version shown to players, e.g. 1.0.0 (optional)
//   FROGS_BUILD_NUMBER = whole number that goes up every build (optional, Android needs it)
//
// Android: a 64-bit APK you can install straight onto a phone or tablet (no Play Store).
// Windows / Linux: a folder with the game in it. Mac: one app that runs on Apple silicon
// (M-series) and Intel Macs. iOS: an Xcode project (signing happens on a Mac later).
// Apps ask for the screen's own frame rate (120 Hz on ProMotion, etc.); see GraphicsManager.
//
// Hop on any platform.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FrogsNLogs.EditorTools
{
    public static class AppBuilds
    {
        public const string AppId = "com.freddietindlecowgill.frogsnlogs";
        public const string Company = "Freddie Tindle-Cowgill";
        public const string Product = "Frogs-N-Logs";
        private const string IconPath = "Assets/Art/Icon/AppIcon.png";

        [MenuItem("Tools/Frogs-N-Logs/Build App/Android")] public static void MenuAndroid() { Run("android", false); }
        [MenuItem("Tools/Frogs-N-Logs/Build App/Windows")] public static void MenuWindows() { Run("windows", false); }
        [MenuItem("Tools/Frogs-N-Logs/Build App/Mac")] public static void MenuMac() { Run("mac", false); }
        [MenuItem("Tools/Frogs-N-Logs/Build App/Linux")] public static void MenuLinux() { Run("linux", false); }
        [MenuItem("Tools/Frogs-N-Logs/Build App/iOS (Xcode project)")] public static void MenuIOS() { Run("ios", false); }

        // Batch mode entry point.
        public static void Build()
        {
            Run(Environment.GetEnvironmentVariable("FROGS_APP_TARGET") ?? "linux", true);
        }

        private static void Run(string target, bool exitWhenDone)
        {
            int exitCode;
            try
            {
                exitCode = BuildFor(target.Trim().ToLowerInvariant()) ? 0 : 2;
            }
            catch (Exception e)
            {
                Debug.LogError("[FrogsApp] Build failed: " + e);
                exitCode = 3;
            }
            if (exitWhenDone) EditorApplication.Exit(exitCode);
        }

        private static bool BuildFor(string target)
        {
            string root = Environment.GetEnvironmentVariable("FROGS_APP_PATH");
            if (string.IsNullOrEmpty(root)) root = "Build/Apps/" + target;
            Directory.CreateDirectory(root);

            ApplyCommonSettings();

            BuildTarget buildTarget;
            BuildTargetGroup group;
            string location;
            switch (target)
            {
                case "android":
                    buildTarget = BuildTarget.Android; group = BuildTargetGroup.Android;
                    SetupAndroid();
                    location = Path.Combine(root, "Frogs-N-Logs.apk");
                    break;
                case "windows":
                    buildTarget = BuildTarget.StandaloneWindows64; group = BuildTargetGroup.Standalone;
                    location = Path.Combine(root, "Frogs-N-Logs.exe");
                    break;
                case "mac":
                    buildTarget = BuildTarget.StandaloneOSX; group = BuildTargetGroup.Standalone;
                    SetupMac();
                    location = Path.Combine(root, "Frogs-N-Logs.app");
                    break;
                case "ios":
                    buildTarget = BuildTarget.iOS; group = BuildTargetGroup.iOS;
                    SetupIOS();
                    location = root;
                    break;
                default:
                    buildTarget = BuildTarget.StandaloneLinux64; group = BuildTargetGroup.Standalone;
                    location = Path.Combine(root, "Frogs-N-Logs.x86_64");
                    break;
            }

            if (EditorUserBuildSettings.activeBuildTarget != buildTarget)
            {
                Debug.Log("[FrogsApp] Switching to " + buildTarget);
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, buildTarget);
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/_Scenes.unity" };

            Debug.Log("[FrogsApp] Building " + target + " " + PlayerSettings.bundleVersion + " -> " + location);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = buildTarget,
                options = BuildOptions.None
            });
            BuildSummary s2 = report.summary;
            Debug.Log("[FrogsApp] Build result: " + s2.result + " | errors: " + s2.totalErrors
                      + " | size: " + (s2.totalSize / (1024 * 1024)) + " MB");
            return s2.result == BuildResult.Succeeded;
        }

        private static void ApplyCommonSettings()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = Product;
            string version = Environment.GetEnvironmentVariable("FROGS_VERSION");
            PlayerSettings.bundleVersion = string.IsNullOrEmpty(version) ? "1.0.0" : version.TrimStart('v');

            foreach (NamedBuildTarget t in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetApplicationIdentifier(t, AppId);
            }

            // Landscape only (both ways), like the web version.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Desktop: full screen window, resizable if the player leaves full screen.
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                Debug.Log("[FrogsApp] App icon set");
            }
            else
            {
                Debug.LogWarning("[FrogsApp] App icon not found at " + IconPath);
            }
        }

        private static void SetupAndroid()
        {
            int number;
            if (!int.TryParse(Environment.GetEnvironmentVariable("FROGS_BUILD_NUMBER"), out number) || number < 1) number = 1;
            PlayerSettings.Android.bundleVersionCode = number;
            // 64-bit (required by Android phones and Google Play), compiled to native code.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false; // APK: installs straight onto a device
            // Smooth frame pacing on 90/120 Hz Android screens.
            PlayerSettings.Android.optimizedFramePacing = true;
            // Signed with Unity's debug key for now (fine for installing yourself; the Play Store
            // needs a real upload key later).
        }

        private static void SetupMac()
        {
            // One app for both Apple silicon (M-series) and Intel Macs. The setting lives in the Mac
            // build module, so set it by reflection (this file also compiles without that module).
            try
            {
                Type ubs = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("UnityEditor.OSXStandalone.UserBuildSettings", false))
                    .FirstOrDefault(t => t != null);
                PropertyInfo arch = ubs != null ? ubs.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static) : null;
                if (arch != null)
                {
                    object universal = Enum.Parse(arch.PropertyType, "x64ARM64");
                    arch.SetValue(null, universal);
                    Debug.Log("[FrogsApp] Mac build: Apple silicon + Intel (universal)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FrogsApp] Couldn't set the Mac architecture: " + e.Message);
            }
            PlayerSettings.macRetinaSupport = true;
        }

        private static void SetupIOS()
        {
            PlayerSettings.iOS.buildNumber = Environment.GetEnvironmentVariable("FROGS_BUILD_NUMBER") ?? "1";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            // TODO (with the Apple Developer account): add CADisableMinimumFrameDurationOnPhone to
            // Info.plist so iPhone Pro models can run at 120 Hz too (iPads allow it already).
        }
    }
}

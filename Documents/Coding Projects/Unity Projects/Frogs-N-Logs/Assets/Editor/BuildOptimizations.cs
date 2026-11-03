// BuildOptimizations.cs (added Nov 2026)
//
// Makes the web version smaller (faster to download) and lighter to draw. Runs on GitHub's
// servers right before the web build (see UpgradeAndBuild.BuildWebGL), so it never changes
// how the project looks or works when you open it in Unity yourself.
//
//  1. Textures: web copies are capped at 1024 px (normal/detail maps at 512). The 2017 models
//     came with 2K-4K textures, which is far more detail than a small web canvas can show.
//  2. Sounds: short sound effects become mono and slightly more compressed for the web.
//     (Music keeps stereo.)
//  3. Code: Unity strips out code and engine features the game never uses.
//  4. Scenery: rocks, logs and other things that never move are marked "static" while the
//     level is built, so Unity can draw them in a few big batches instead of one by one.
//
// Less to download, less to draw, more time eating flies.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrogsNLogs.EditorTools
{
    public static class BuildOptimizations
    {
        public const int MaxTextureSize = 1024;
        public const int MaxMaskTextureSize = 512;

        public static void ApplyForWeb()
        {
            int textures = ShrinkTexturesForWeb();
            int sounds = CompressSoundsForWeb();

            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.WebGL.showDiagnostics = false;

            Debug.Log("[FrogsWeb] Optimized " + textures + " textures and " + sounds + " sounds for the web build");
        }

        private static bool IsEditorOnly(string path)
        {
            string p = path.Replace('\\', '/');
            return p.Contains("/Editor/") || p.Contains("/Editor Resources/") || p.Contains("/Tests/");
        }

        private static int ShrinkTexturesForWeb()
        {
            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (IsEditorOnly(path)) continue;
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    // Normal maps, metal/smoothness masks and AO maps can be smaller than colour.
                    string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    bool mask = importer.textureType == TextureImporterType.NormalMap
                                || name.Contains("normal") || name.Contains("metal") || name.Contains("rough")
                                || name.Contains("occlusion") || name.EndsWith("_ao") || name.Contains("mask");
                    int cap = mask ? MaxMaskTextureSize : MaxTextureSize;

                    TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings("WebGL");
                    int current = web.overridden ? web.maxTextureSize : importer.maxTextureSize;
                    if (current <= cap && web.overridden) continue;

                    web.overridden = true;
                    web.maxTextureSize = Mathf.Min(current, cap);
                    web.format = TextureImporterFormat.Automatic;
                    web.textureCompression = TextureImporterCompression.Compressed;
                    web.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(web);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return changed;
        }

        private static int CompressSoundsForWeb()
        {
            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (IsEditorOnly(path)) continue;
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null) continue;

                    string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    bool music = name.Contains("music") || name.Contains("bg");

                    AudioImporterSampleSettings web = importer.GetOverrideSampleSettings("WebGL");
                    web.quality = music ? 0.6f : 0.45f;
                    web.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                    importer.SetOverrideSampleSettings("WebGL", web);
                    if (!music) importer.forceToMono = true;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return changed;
        }
    }

    // While a level is being built (not when you press Play), mark non-moving scenery as
    // static so Unity batches it. Anything with physics, animation, AI or particles is skipped.
    public class MarkStaticSceneryForBuild : IProcessSceneWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return; // only during real builds
            int marked = 0;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (!rootObject.name.StartsWith("Environment")) continue;
                foreach (MeshRenderer renderer in rootObject.GetComponentsInChildren<MeshRenderer>(true))
                {
                    GameObject go = renderer.gameObject;
                    if (go.GetComponentInParent<Rigidbody>() != null) continue;
                    if (go.GetComponentInParent<Animator>() != null) continue;
                    if (go.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
                    if (go.GetComponent<ParticleSystem>() != null) continue;
                    if (go.GetComponent("Water") != null || go.name.ToLowerInvariant().Contains("water")) continue;
                    // Logs stay un-batched: the drifting-log hazard borrows their bark material.
                    if (go.name.ToLowerInvariant().Contains("log")) continue;
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
                    GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.BatchingStatic);
                    marked++;
                }
            }
            if (marked > 0)
            {
                Debug.Log("[FrogsWeb] Marked " + marked + " pieces of scenery as static for batching");
            }
        }
    }
}

// FrogsAssetImport.cs (added Nov 2026)
//
// Import settings for the new free art, applied automatically whenever Unity imports it:
//   Resources/Characters  - the frog and eagle: simple ("legacy") looping animations, so
//                           CharacterVisual.cs can play "idle", "walk", "Flying" by name.
//   Resources/Nature      - trees, plants and rocks: no animation, no colliders.
//   */Textures, Biomes    - up to 2048 px in the desktop apps, 1024 px on the web and phones.
//   Resources/Audio       - short sound effects: mono, loaded when the game starts.
// See CREDITS.md for who made each asset (all free: CC0 or CC BY).

using UnityEditor;
using UnityEngine;

namespace FrogsNLogs.EditorTools
{
    public class FrogsAssetImport : AssetPostprocessor
    {
        private bool In(string folder)
        {
            return assetPath.Replace('\\', '/').Contains("/Resources/" + folder + "/");
        }

        void OnPreprocessModel()
        {
            var model = (ModelImporter)assetImporter;
            if (In("Characters"))
            {
                model.animationType = ModelImporterAnimationType.Legacy;
                model.importAnimation = true;
                model.animationWrapMode = WrapMode.Loop;
                model.generateAnimations = ModelImporterGenerateAnimations.GenerateAnimations;
                model.importCameras = false;
                model.importLights = false;
                model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                model.addCollider = false;
            }
            else if (In("Nature"))
            {
                model.animationType = ModelImporterAnimationType.None;
                model.importAnimation = false;
                model.importCameras = false;
                model.importLights = false;
                model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                model.addCollider = false;
                model.meshCompression = ModelImporterMeshCompression.Medium;
            }
        }

        // Nov 2026: the sharpest art each device can hold.
        //   Windows / Mac / Linux apps:  full detail (the ground is 4K)
        //   iPhone / iPad / Android apps: up to 2K
        //   Web:                          2K ground, 1K for everything else (a browser tab on a
        //                                 phone has little memory, and too much crashes it)
        // Mipmaps let far-away ground use smaller copies automatically.
        void OnPreprocessTexture()
        {
            bool biome = In("Biomes");
            if (!In("Characters/Textures") && !In("Nature/Textures") && !In("Textures") && !biome) return;
            var tex = (TextureImporter)assetImporter;
            tex.maxTextureSize = 4096;
            tex.mipmapEnabled = true;
            tex.alphaIsTransparency = tex.DoesSourceTextureHaveAlpha();
            if (biome)
            {
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.anisoLevel = 8; // ground seen at an angle stays sharp
            }
            foreach (string platform in new[] { "WebGL", "Android", "iPhone" })
            {
                TextureImporterPlatformSettings p = tex.GetPlatformTextureSettings(platform);
                p.overridden = true;
                p.maxTextureSize = platform == "WebGL" && !biome ? 1024 : 2048;
                p.format = TextureImporterFormat.Automatic;
                p.compressionQuality = 80;
                tex.SetPlatformTextureSettings(p);
            }
        }

        void OnPreprocessAudio()
        {
            if (!In("Audio")) return;
            var audio = (AudioImporter)assetImporter;
            audio.forceToMono = true;
            AudioImporterSampleSettings s = audio.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
            audio.defaultSampleSettings = s;
        }
    }
}

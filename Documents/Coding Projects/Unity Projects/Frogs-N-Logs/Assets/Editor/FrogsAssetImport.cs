// FrogsAssetImport.cs (added Nov 2026)
//
// Import settings for the new free art, applied automatically whenever Unity imports it:
//   Resources/Characters  - the frog and eagle: simple ("legacy") looping animations, so
//                           CharacterVisual.cs can play "idle", "walk", "Flying" by name.
//   Resources/Nature      - trees, plants and rocks: no animation, no colliders.
//   */Textures            - capped at 512 px (plenty for a cartoon look), leaves keep alpha.
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

        void OnPreprocessTexture()
        {
            if (!In("Characters/Textures") && !In("Nature/Textures") && !In("Textures")) return;
            var tex = (TextureImporter)assetImporter;
            tex.maxTextureSize = 512;
            tex.mipmapEnabled = true;
            tex.alphaIsTransparency = tex.DoesSourceTextureHaveAlpha();
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

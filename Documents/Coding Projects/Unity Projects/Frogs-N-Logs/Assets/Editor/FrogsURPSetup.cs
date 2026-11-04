// FrogsURPSetup.cs (added Nov 2026)
//
// Moves Frogs-N-Logs from Unity's 2017 "Built-in" renderer to the Universal Render Pipeline
// (URP), Unity's current renderer: better looking, and faster on phones, iPads and the web.
//
// It runs automatically on GitHub's servers before every build (UpgradeAndBuild.Run), and
// the first time you open the project in Unity if it hasn't been set up yet. It's safe to
// run again - it only fills in what's missing. You can also run it from
// Tools > Frogs-N-Logs > Set Up URP.
//
//  1. Creates the URP settings (Assets/Settings/FrogsURP.asset + its renderer), including the
//     screen-filter effect (FrogsFilterFeature), and turns URP on for every quality level.
//  2. Converts the old materials: Standard -> URP Lit, old particles -> URP Particles, the
//     2017 water -> the new cartoon water (Frogs/ToonWater).
//  3. Makes URP versions of the terrain and default particle materials.
//  4. Cleans up leftovers from the removed 2017 packages (missing scripts) in the level.
// From pond to pipeline in one hop.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FrogsNLogs.EditorTools
{
    [InitializeOnLoad]
    public static class FrogsURPSetup
    {
        public const string SettingsDir = "Assets/Settings";
        public const string RendererPath = SettingsDir + "/FrogsURP_Renderer.asset";
        public const string PipelinePath = SettingsDir + "/FrogsURP.asset";
        public const string GeneratedDir = "Assets/Resources/Generated";
        public const string TerrainMatPath = GeneratedDir + "/TerrainURP.mat";
        public const string ParticleMatPath = GeneratedDir + "/ParticleURP.mat";
        private const string ScenePath = "Assets/_Scenes.unity";

        static FrogsURPSetup()
        {
            // When the project is opened in the Unity editor (not on the build server), set up
            // URP automatically if it hasn't been done yet.
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode && GraphicsSettings.defaultRenderPipeline == null)
                {
                    Debug.Log("[FrogsURP] Setting up the Universal Render Pipeline for this project...");
                    Run();
                }
            };
        }

        [MenuItem("Tools/Frogs-N-Logs/Set Up URP")]
        public static void Run()
        {
            UniversalRenderPipelineAsset pipeline = EnsurePipelineAsset();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            int converted = ConvertMaterials();
            EnsureRuntimeMaterials();
            int cleaned = FixPrefabsAndScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[FrogsURP] URP ready: " + converted + " materials converted, " + cleaned + " objects cleaned up");
        }

        // ------------------------------------------------------------ pipeline asset

        private static UniversalRenderPipelineAsset EnsurePipelineAsset()
        {
            Directory.CreateDirectory(SettingsDir);

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            var so = new SerializedObject(rendererData);
            SerializedProperty post = so.FindProperty("postProcessData");
            if (post != null && post.objectReferenceValue == null)
            {
                post.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AddFilterFeature(rendererData);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.renderScale = 1f;
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 1;
            pipeline.shadowDistance = 45f;
            pipeline.shadowCascadeCount = 1;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;

            var pso = new SerializedObject(pipeline);
            SetInt(pso, "m_MainLightShadowmapResolution", 1024);
            SetBool(pso, "m_SoftShadowsSupported", true);
            SetInt(pso, "m_AdditionalLightsRenderingMode", 0); // the pond only needs the sun
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(rendererData);
            return pipeline;
        }

        private static void AddFilterFeature(UniversalRendererData rendererData)
        {
            foreach (ScriptableRendererFeature f in rendererData.rendererFeatures)
            {
                if (f is FrogsFilterFeature) return;
            }
            var feature = ScriptableObject.CreateInstance<FrogsFilterFeature>();
            feature.name = "FrogsFilterFeature";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.SaveAssets();

            var so = new SerializedObject(rendererData);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            int n = features.arraySize;
            features.InsertArrayElementAtIndex(n);
            features.GetArrayElementAtIndex(n).objectReferenceValue = feature;
            if (map != null)
            {
                string guid;
                long localId;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out guid, out localId);
                map.InsertArrayElementAtIndex(map.arraySize);
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
        }

        // ------------------------------------------------------------ materials

        private static readonly HashSet<string> StandardShaders = new HashSet<string>
        {
            "Standard", "Standard (Specular setup)", "Legacy Shaders/Diffuse", "Legacy Shaders/Bumped Diffuse",
            "Legacy Shaders/Specular", "Mobile/Diffuse", "Mobile/Bumped Diffuse", "Hidden/InternalErrorShader",
        };

        private static int ConvertMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Shader water = Shader.Find("Frogs/ToonWater");
            int converted = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(GeneratedDir) || path.Contains("/Editor/")) continue;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                string shaderName = mat.shader != null ? mat.shader.name : "Hidden/InternalErrorShader";
                if (shaderName.StartsWith("Universal Render Pipeline/") || shaderName.StartsWith("Frogs/")) continue;

                if (mat.name.ToLowerInvariant().Contains("water") && water != null)
                {
                    mat.shader = water;
                    converted++;
                }
                else if ((shaderName.StartsWith("Particles/") || shaderName.Contains("/Particles/")) && particles != null)
                {
                    ToParticles(mat, particles, shaderName.Contains("Additive"));
                    converted++;
                }
                else if (StandardShaders.Contains(shaderName) && lit != null)
                {
                    ToLit(mat, lit);
                    converted++;
                }
                else
                {
                    continue;
                }
                EditorUtility.SetDirty(mat);
                Debug.Log("[FrogsURP] Converted material " + path + " (" + shaderName + " -> " + mat.shader.name + ")");
            }
            return converted;
        }

        private static Texture Tex(Material m, string name)
        {
            try { return m.GetTexture(name); } catch { return null; }
        }

        private static Color Col(Material m, string name, Color fallback)
        {
            try { return m.HasProperty(name) || m.shader == null ? m.GetColor(name) : fallback; } catch { return fallback; }
        }

        private static void ToLit(Material mat, Shader lit)
        {
            Texture main = Tex(mat, "_MainTex");
            Texture bump = Tex(mat, "_BumpMap");
            Texture metal = Tex(mat, "_MetallicGlossMap");
            Texture ao = Tex(mat, "_OcclusionMap");
            Color color = Col(mat, "_Color", Color.white);
            float gloss = 0.3f;
            try { gloss = mat.GetFloat("_Glossiness"); } catch { }

            mat.shader = lit;
            mat.SetTexture("_BaseMap", main);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", Mathf.Clamp01(gloss));
            if (bump != null) { mat.SetTexture("_BumpMap", bump); mat.EnableKeyword("_NORMALMAP"); }
            if (metal != null) { mat.SetTexture("_MetallicGlossMap", metal); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (ao != null) { mat.SetTexture("_OcclusionMap", ao); mat.EnableKeyword("_OCCLUSIONMAP"); }
        }

        private static void ToParticles(Material mat, Shader particles, bool additive)
        {
            Texture main = Tex(mat, "_MainTex");
            Color tint = Col(mat, "_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)) * 2f;
            mat.shader = particles;
            mat.SetTexture("_BaseMap", main);
            mat.SetColor("_BaseColor", tint);
            MakeTransparent(mat, additive);
        }

        private static void MakeTransparent(Material mat, bool additive)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
        }

        private static void EnsureRuntimeMaterials()
        {
            Directory.CreateDirectory(GeneratedDir);
            if (AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath) == null)
            {
                Shader terrain = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                if (terrain != null) AssetDatabase.CreateAsset(new Material(terrain), TerrainMatPath);
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(ParticleMatPath) == null)
            {
                Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (particles != null)
                {
                    var m = new Material(particles);
                    Texture2D dot = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
                    if (dot != null) m.SetTexture("_BaseMap", dot);
                    m.SetColor("_BaseColor", Color.white);
                    MakeTransparent(m, false);
                    AssetDatabase.CreateAsset(m, ParticleMatPath);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------ prefabs + level

        private static bool NeedsParticleMaterial(Material m)
        {
            if (m == null || m.shader == null) return true;
            string n = m.shader.name;
            return !(n.StartsWith("Universal Render Pipeline/") || n.StartsWith("Frogs/"));
        }

        private static int FixObjects(GameObject root, Material terrainMat, Material particleMat)
        {
            int changes = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                try { changes += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject); }
                catch (System.Exception e) { Debug.LogWarning("[FrogsURP] Couldn't clean " + t.name + ": " + e.Message); }
            }
            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                if (terrainMat != null && terrain.materialTemplate != terrainMat)
                {
                    terrain.materialTemplate = terrainMat;
                    changes++;
                }
            }
            foreach (ParticleSystemRenderer r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (particleMat != null && NeedsParticleMaterial(r.sharedMaterial))
                {
                    r.sharedMaterial = particleMat;
                    changes++;
                }
            }
            return changes;
        }

        private static int FixPrefabsAndScene()
        {
            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            var particleMat = AssetDatabase.LoadAssetAtPath<Material>(ParticleMatPath);
            int changes = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int c = FixObjects(contents, terrainMat, particleMat);
                    if (c > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        changes += c;
                        Debug.Log("[FrogsURP] Updated prefab " + path + " (" + c + " changes)");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int sceneChanges = 0;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                sceneChanges += FixObjects(rootObject, terrainMat, particleMat);
            }
            if (sceneChanges > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[FrogsURP] Updated the level (" + sceneChanges + " changes)");
            }
            return changes + sceneChanges;
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace LumiWorld.EditorTools
{
    /// <summary>
    /// Dedicated standalone IPreprocessShaders stripper.
    /// Uses LocalKeyword (shader.keywordSpace.FindKeyword) and global ShaderKeyword
    /// to reliably strip heavy Forward+ / ReflectionProbeAtlas / LightLayers / SSAO /
    /// Lightmap / Decal / Procedural / Deferred keywords and unused passes BEFORE compilation.
    /// </summary>
    public class LumiWorldShaderStripper : IPreprocessShaders
    {
        public int callbackOrder => -2000;

        private static readonly string[] StrippedKeywordNames = new[]
        {
            "_CLUSTER_LIGHT_LOOP",
            "_LIGHT_LAYERS",
            "_LIGHT_COOKIES",
            "_REFLECTION_PROBE_ATLAS",
            "_REFLECTION_PROBE_BLENDING",
            "_REFLECTION_PROBE_BOX_PROJECTION",
            "LOD_FADE_CROSSFADE",
            "_DETAIL_MULX2",
            "_DETAIL_SCALED",
            "_PARALLAXMAP",
            "DIRLIGHTMAP_COMBINED",
            "DYNAMICLIGHTMAP_ON",
            "LIGHTMAP_ON",
            "SHADOWS_SHADOWMASK",
            "LIGHTMAP_SHADOW_MIXING",
            "LIGHTMAP_BICUBIC_SAMPLING",
            "USE_LEGACY_LIGHTMAPS",
            "DOTS_INSTANCING_ON",
            "PROCEDURAL_INSTANCING_ON",
            "FOG_EXP",
            "FOG_EXP2",
            "_DBUFFER_MRT1",
            "_DBUFFER_MRT2",
            "_DBUFFER_MRT3",
            "DEBUG_DISPLAY",
            "_SCREEN_SPACE_OCCLUSION",
            "_WRITE_RENDERING_LAYERS",
            "_ADDITIONAL_LIGHT_SHADOWS",
            "_CASTING_PUNCTUAL_LIGHT_SHADOW",
            "_DEFERRED_FIRST_LIGHT",
            "_DEFERRED_MAIN_LIGHT",
            "_DEFERRED_MIXED_LIGHTING",
            "_GBUFFER_NORMALS_OCT",
            "PROBE_VOLUMES_L1",
            "PROBE_VOLUMES_L2",
            "EDITOR_VISUALIZATION",
            "_ENABLEPLANERREFLECTION"
        };

        private static readonly HashSet<string> StrippedKeywordSet = new HashSet<string>(StrippedKeywordNames, StringComparer.Ordinal);

        private static bool IsTargetDiagnosticShader(Shader shader)
        {
            if (shader == null || string.IsNullOrEmpty(shader.name)) return false;
            return shader.name.Contains("SG_HexGround_TopSide") ||
                   shader.name.Contains("SG_LowpolyNature") ||
                   shader.name.Equals("Universal Render Pipeline/Lit", StringComparison.Ordinal) ||
                   shader.name.Contains("UberStylizedWater");
        }

        private static string FormatVariantKeywords(ShaderKeywordSet ks)
        {
            var kws = ks.GetShaderKeywords();
            if (kws == null || kws.Length == 0) return "<no keywords>";
            var names = new List<string>(kws.Length);
            for (int i = 0; i < kws.Length; i++)
            {
                string n = kws[i].name;
                if (!string.IsNullOrEmpty(n)) names.Add(n);
            }
            names.Sort(StringComparer.Ordinal);
            return names.Count > 0 ? string.Join(" ", names) : "<no keywords>";
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || data == null || data.Count == 0) return;

            int beforeCount = data.Count;
            string passName = snippet.passName ?? "";
            bool isDiag = IsTargetDiagnosticShader(shader);

            if (passName.IndexOf("MotionVectors", StringComparison.OrdinalIgnoreCase) >= 0 ||
                passName.IndexOf("Universal 2D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                passName.IndexOf("Universal2D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                passName.Equals("GBuffer", StringComparison.OrdinalIgnoreCase) ||
                passName.Equals("Meta", StringComparison.OrdinalIgnoreCase) ||
                passName.Equals("DepthNormals", StringComparison.OrdinalIgnoreCase) ||
                passName.Equals("DepthNormalsOnly", StringComparison.OrdinalIgnoreCase) ||
                snippet.passType == PassType.Meta ||
                snippet.passType == PassType.MotionVectors)
            {
                data.Clear();
                if (isDiag)
                {
                    Debug.Log(string.Format("[LumiWorldShaderStripper] CLEARED {0} ({1} | {2} | {3}): {4} -> 0 variants",
                        shader.name, passName, snippet.shaderType, snippet.passType, beforeCount));
                }
                return;
            }

            for (int i = data.Count - 1; i >= 0; i--)
            {
                var kws = data[i].shaderKeywordSet.GetShaderKeywords();
                bool strip = false;
                if (kws != null)
                {
                    for (int k = 0; k < kws.Length; k++)
                    {
                        string kwName = kws[k].name;
                        if (!string.IsNullOrEmpty(kwName) && StrippedKeywordSet.Contains(kwName))
                        {
                            strip = true;
                            break;
                        }
                    }
                }
                if (strip)
                {
                    data.RemoveAt(i);
                }
            }

            if (isDiag)
            {
                string sampleKws = data.Count > 0 ? FormatVariantKeywords(data[0].shaderKeywordSet) : "<none>";
                Debug.Log(string.Format("[LumiWorldShaderStripper] {0} ({1} | {2} | {3}): {4} -> {5} variants | sample[0]: {6}",
                    shader.name, passName, snippet.shaderType, snippet.passType, beforeCount, data.Count, sampleKws));
            }
        }
    }

    /// <summary>
    /// Frees unused Editor RAM before building without changing rendering settings.
    /// Lightweight URP settings and water shader changes are explicit menu actions only.
    /// </summary>
    public class LumiWorldShaderBuildOptimizer : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            FreeUnusedEditorMemory();
        }

        // Explicit manual preset: despite the legacy name, this saves changes to disk.
        // Do not call from Editor initialization or build callbacks.
        public static void ApplyInMemoryURPOptimizations(bool freeRamNow)
        {
            UnityEngine.Object rpAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PC_RPAsset.asset");
            if (rpAsset != null)
            {
                SerializedObject soRp = new SerializedObject(rpAsset);
                SetBoolProp(soRp, "m_EnableLODCrossFade", false);
                SetBoolProp(soRp, "m_AdditionalLightShadowsSupported", false);
                SetBoolProp(soRp, "m_ReflectionProbeBlending", false);
                SetBoolProp(soRp, "m_ReflectionProbeBoxProjection", false);
                SetBoolProp(soRp, "m_ReflectionProbeAtlas", false);
                SetBoolProp(soRp, "m_MixedLightingSupported", false);
                SetBoolProp(soRp, "m_SupportsLightCookies", false);
                SetBoolProp(soRp, "m_SupportsLightLayers", false);
                SetIntProp(soRp, "m_SoftShadowQuality", 1);
                soRp.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rpAsset);
            }

            UnityEngine.Object[] rendererSubAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/PC_Renderer.asset");
            if (rendererSubAssets != null)
            {
                foreach (var sub in rendererSubAssets)
                {
                    if (sub == null) continue;
                    SerializedObject soSub = new SerializedObject(sub);
                    if (sub.name == "PC_Renderer")
                    {
                        SetIntProp(soSub, "m_RenderingMode", 0); // Forward
                    }
                    else if (sub.name.Contains("ScreenSpaceAmbientOcclusion"))
                    {
                        SetBoolProp(soSub, "m_Active", false);
                    }
                    soSub.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(sub);
                }
            }

            AssetDatabase.SaveAssets();

            if (freeRamNow)
            {
                FreeUnusedEditorMemory();
            }
        }

        private static void FreeUnusedEditorMemory()
        {
            EditorUtility.UnloadUnusedAssetsImmediate(true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Debug.Log("[LumiWorldShaderBuildOptimizer] Freed unused Editor RAM; rendering settings unchanged.");
        }

        private static void SetBoolProp(SerializedObject so, string name, bool val)
        {
            var p = so.FindProperty(name);
            if (p != null && p.boolValue != val) p.boolValue = val;
        }

        private static void SetIntProp(SerializedObject so, string name, int val)
        {
            var p = so.FindProperty(name);
            if (p != null && p.intValue != val) p.intValue = val;
        }

        [MenuItem("LumiWorld/WetIsland/5. Switch Water to Lightweight Build Mode (Fix Build OOM)")]
        public static void SwitchToLightweightBuildMode()
        {
            ApplyInMemoryURPOptimizations(true);

            Shader lightWater = Shader.Find("LumiWorld/WetIslandWater");
            if (lightWater != null)
            {
                string[] matPaths = new[]
                {
                    "Assets/Acs/Art/Ground/Materials/WetIsland/M_WetWater_Lilywater.mat",
                    "Assets/Acs/Art/Ground/Materials/WetIsland/M_WetWater_Streamstone.mat",
                    "Assets/Acs/Art/Ground/Materials/WetIsland/M_WetWater_Reedmarsh.mat",
                    "Assets/Acs/Art/Ground/Materials/WetIsland/M_WetWater_Reedmarsh_3Hex.mat"
                };
                foreach (var mp in matPaths)
                {
                    Material m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                    if (m != null)
                    {
                        m.shader = lightWater;
                        m.shaderKeywords = new string[0];
                        EditorUtility.SetDirty(m);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LumiWorldShaderBuildOptimizer] Switched to Lightweight Build Mode (Forward URP + Stripped Variants + Lightweight Water).");
        }

        [MenuItem("LumiWorld/WetIsland/6. Switch Water Back to Full UberStylizedWater (Showcase Mode)")]
        public static void SwitchToFullShowcaseWater()
        {
            WetIslandBiomeSurfaceSetup.ApplyPresetClear();
            Debug.Log("[LumiWorldShaderBuildOptimizer] Switched Wet Island water back to Full UberStylizedWater (Showcase Mode)!");
        }
    }
}
#endif

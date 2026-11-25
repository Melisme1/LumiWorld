using System.Collections.Generic;
using System.IO;
using LumiWorld.Acs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LumiWorld.EditorTools
{
    public struct HexGroundMaterialSpec
    {
        public string materialPath;
        public Texture2D topTex;
        public Texture2D waterMask;
        public Texture2D causticsTex;
        public float shoreStart;
        public float waterThreshold;
        public Color shoreColor;
        public Color bedColor;
        public Color bedMidColor;
        public Color bedDeepColor;
        public Color sideColor;
        public Color sideBotColor;
        public float wetDarken;
        public Color causticColor;
        public float causticStrength;
    }

    public struct HexUberWaterCalibration
    {
        public float waterDepth;
        public float shoreFadeSmoothness;
        public float intersectionWidth;
        public float shorelineWaterDepth;
        public float underwaterDepth;
        public Color underwaterColor;
        public float normalScale;
        public float normalStrength;
        public float normalPan;
        public float causticStrength;
        public Color surfFoamColor;
        public Color shorelineColor;
        public bool preserveCustomPaletteOnPresetSwitch;
        public Color customShallowColor;
        public Color customDeepColor;
        public Color customIntersectionColor;

        public static HexUberWaterCalibration CreateClearPondDefault()
        {
            return new HexUberWaterCalibration
            {
                waterDepth = 0.68f,
                shoreFadeSmoothness = 0.22f,
                intersectionWidth = 0.22f,
                shorelineWaterDepth = 0.22f,
                underwaterDepth = -0.75f,
                underwaterColor = new Color(0.03f, 0.24f, 0.36f, 0.25f),
                normalScale = 4.2f,
                normalStrength = 0.42f,
                normalPan = 0.55f,
                causticStrength = 2.8f,
                surfFoamColor = Color.white,
                shorelineColor = Color.white,
                preserveCustomPaletteOnPresetSwitch = false,
                customShallowColor = new Color(0.28f, 0.88f, 0.78f, 0.04f),
                customDeepColor = new Color(0.01f, 0.28f, 0.46f, 0.70f),
                customIntersectionColor = new Color(0.98f, 0.99f, 1.0f, 0.84f)
            };
        }
    }

    public struct HexWaterMaterialSpec
    {
        public string materialPath;
        public Texture2D waterMask;
        public Texture2D normalTex;
        public Texture2D causticsTex;
        public Texture2D noiseTex;
        public float waterThreshold;
        public float edgeFeather;
        public float normalScale;
        public float normalStrength;
        public float refractionStrength;
        public Color shallowColor;
        public Color midColor;
        public Color deepColor;
        public Color fresnelColor;
        public Color cloudColor;
        public float fresnelPower;
        public float fresnelStrength;
        public float cloudStrength;
        public Color foamColor;
        public float foamBandWidth;
        public float objectFoamDist;
        public float causticStrength;
        public Vector4 flowParams;
        public float sunSheen;
        public float sunGloss;
        public float sparkleThreshold;
        public HexUberWaterCalibration uberCalibration;
    }

    public struct HexWaterPresetTarget
    {
        public string materialPath;
        public HexUberWaterCalibration calibration;
    }

    public struct HexBiomeClusterBuildSpec
    {
        public string biomeName;
        public int clusterSize;
        public IReadOnlyList<HexCoordinates> hexes;
        public Texture2D waterMask;
        public HexBiomeSurfaceSettings surfaceSettings;
        public Material groundMaterial;
        public Material waterMaterial;
        public string meshesOutDir;
        public string prefabsOutDir;
    }

    /// <summary>
    /// Reusable, island-agnostic editor builder for multi-hex biome ground/water materials,
    /// URP texture setup, UberStylizedWater preset calibration, and cluster surface prefabs.
    /// </summary>
    public static class HexBiomeSurfaceBuilder
    {
        public const string GroundShaderName = "LumiWorld/HexClusterGround";
        public const string LightweightWaterShaderName = "LumiWorld/HexClusterWater";
        public const string UberWaterShaderPath = "Assets/Acs/UberStylizedWater/Shader/UberStylizedWater.shader";
        public const string UberWaterTemplateDir = "Assets/Acs/UberStylizedWater/Template Materials";

        public static Shader FindGroundShader()
        {
            return Shader.Find(GroundShaderName);
        }

        public static Shader FindLightweightWaterShader()
        {
            return Shader.Find(LightweightWaterShaderName);
        }

        public static Shader FindPreferredWaterShader()
        {
            return AssetDatabase.LoadAssetAtPath<Shader>(UberWaterShaderPath)
                ?? Shader.Find("UberStylizedWater")
                ?? FindLightweightWaterShader();
        }

        public static void EnsureURPDepthAndOpaqueTexturesEnabled()
        {
            string[] rpPaths = new[]
            {
                "Assets/Settings/PC_RPAsset.asset",
                "Assets/Settings/Mobile_RPAsset.asset"
            };

            foreach (string path in rpPaths)
            {
                Object rpObj = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (rpObj == null) continue;

                SerializedObject so = new SerializedObject(rpObj);
                SerializedProperty depthProp = so.FindProperty("m_RequireDepthTexture");
                SerializedProperty opaqueProp = so.FindProperty("m_RequireOpaqueTexture");
                bool changed = false;

                if (depthProp != null && depthProp.intValue != 1)
                {
                    depthProp.intValue = 1;
                    changed = true;
                }
                if (opaqueProp != null && opaqueProp.intValue != 1)
                {
                    opaqueProp.intValue = 1;
                    changed = true;
                }
                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(rpObj);
                }
            }
        }

        public static void ConfigureGroundTexture(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (!importer.sRGBTexture) { importer.sRGBTexture = true; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; dirty = true; }
            if (importer.maxTextureSize != 1024) { importer.maxTextureSize = 1024; dirty = true; }

            if (dirty) importer.SaveAndReimport();
        }

        public static void ConfigureWaterMaskTexture(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.sRGBTexture) { importer.sRGBTexture = false; dirty = true; }
            if (!importer.isReadable) { importer.isReadable = true; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        public static Material CreateOrUpdateGroundMaterial(Shader shader, in HexGroundMaterialSpec spec)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(spec.materialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, spec.materialPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetTexture("_TopTex", spec.topTex);
            mat.SetFloat("_TopTiling", 0.55f);
            mat.SetColor("_TopTint", Color.white);
            mat.SetColor("_SideColor", spec.sideColor);
            mat.SetColor("_SideBottomColor", spec.sideBotColor);
            mat.SetFloat("_BevelEdge1", 0.45f);
            mat.SetFloat("_BevelEdge2", 0.86f);
            mat.SetFloat("_SeamCreaseTint", 0.22f);
            mat.SetFloat("_BevelRimHighlight", 0.08f);

            mat.SetTexture("_WaterMask", spec.waterMask);
            mat.SetFloat("_ShoreStart", spec.shoreStart);
            mat.SetFloat("_WaterThreshold", spec.waterThreshold);
            mat.SetColor("_ShoreColor", spec.shoreColor);
            mat.SetColor("_BedColor", spec.bedColor);
            mat.SetColor("_BedMidColor", spec.bedMidColor);
            mat.SetColor("_BedDeepColor", spec.bedDeepColor);
            mat.SetFloat("_ShoreGrainStrength", 0.065f);
            mat.SetFloat("_WetnessDarken", spec.wetDarken);

            mat.SetTexture("_CausticsTex", spec.causticsTex);
            mat.SetColor("_BedCausticColor", spec.causticColor);
            mat.SetFloat("_BedCausticStrength", spec.causticStrength);
            mat.SetFloat("_BedCausticScale", 0.65f);
            mat.SetFloat("_BedCausticSpeed", 0.08f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material CreateOrUpdateWaterMaterial(Shader shader, in HexWaterMaterialSpec spec)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(spec.materialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, spec.materialPath);
            }
            else
            {
                mat.shader = shader;
            }

            Material uberClearTemplate = AssetDatabase.LoadAssetAtPath<Material>($"{UberWaterTemplateDir}/UWa-Template-Clear.mat");
            if (uberClearTemplate != null && shader.name.Contains("UberStylizedWater"))
            {
                HexUberWaterCalibration cal = spec.uberCalibration;
                mat.CopyPropertiesFromMaterial(uberClearTemplate);
                mat.shaderKeywords = uberClearTemplate.shaderKeywords;

                mat.SetFloat("_Water_Depth", cal.waterDepth);
                mat.SetColor("_Color_Shallow", cal.customShallowColor);
                mat.SetColor("_Color_Deep", cal.customDeepColor);

                mat.SetFloat("_ShoreFade", 1f);
                mat.SetFloat("_ShoreFade_Smoothness", cal.shoreFadeSmoothness);

                if (mat.HasProperty("_SurfFoam_Color")) mat.SetColor("_SurfFoam_Color", cal.surfFoamColor);
                if (mat.HasProperty("_SL_Color")) mat.SetColor("_SL_Color", cal.shorelineColor);
                if (mat.HasProperty("_ENABLE_UNDERWATERLAYER"))
                {
                    mat.SetFloat("_ENABLE_UNDERWATERLAYER", 0f);
                    mat.DisableKeyword("_ENABLE_UNDERWATERLAYER");
                    mat.SetFloat("_UnderWater_Depth", cal.underwaterDepth);
                    mat.SetColor("_Underwater_Color", cal.underwaterColor);
                }

                mat.SetFloat("_ENABLENORMAL", 1f);
                mat.EnableKeyword("_ENABLENORMAL");
                mat.SetFloat("_Normal_Scale", cal.normalScale);
                mat.SetFloat("_Normal_Strength", cal.normalStrength);
                mat.SetFloat("_Normal_Pan", cal.normalPan);

                mat.SetFloat("_ENABLEREFRACTION", 1f);
                mat.EnableKeyword("_ENABLEREFRACTION");
                mat.SetFloat("_Refraction_Strength", 0.20f);

                mat.SetFloat("_ENABLECAUSTICS", 1f);
                mat.EnableKeyword("_ENABLECAUSTICS");
                mat.SetFloat("_Caustics_Depth", -0.75f);
                mat.SetFloat("_Caustics_Scale", 2.4f);
                mat.SetFloat("_Caustics_Strength", cal.causticStrength);
                mat.SetFloat("_Caustics_Start", 20f);
                mat.SetFloat("_Caustics_Fade", 15f);

                Texture2D foamMask = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Acs/UberStylizedWater/Textures/Noise 3.png");
                if (foamMask != null && mat.HasProperty("_InterSec_Foam_Mask"))
                {
                    mat.SetTexture("_InterSec_Foam_Mask", foamMask);
                }
                mat.SetFloat("_Enable_Intersection", 0f);
                mat.SetColor("_InterSec_Color", spec.foamColor);
                mat.SetFloat("_InterSec_Width", cal.intersectionWidth);
                mat.SetFloat("_InterSec_Foam_Scale", 4.5f);

                if (mat.HasProperty("_Specular_Color"))
                {
                    mat.SetColor("_Specular_Color", new Color(5.5f, 5.5f, 5.5f, 1f));
                }

                mat.SetFloat("_SL_WaterDepth", cal.shorelineWaterDepth);

                mat.SetFloat("_ENABLEWAVE", 0f);
                mat.DisableKeyword("_ENABLEWAVE");

                EditorUtility.SetDirty(mat);
                return mat;
            }

            // Lightweight HexClusterWater fallback parameters
            mat.SetTexture("_WaterMask", spec.waterMask);
            mat.SetFloat("_WaterThreshold", spec.waterThreshold);
            mat.SetFloat("_EdgeFeather", spec.edgeFeather);

            mat.SetTexture("_NormalMap", spec.normalTex);
            mat.SetFloat("_NormalScale", spec.normalScale);
            mat.SetFloat("_NormalStrength", spec.normalStrength);
            mat.SetFloat("_RefractionStrength", spec.refractionStrength);

            mat.SetColor("_ShallowColor", spec.shallowColor);
            mat.SetColor("_MidColor", spec.midColor);
            mat.SetColor("_DeepColor", spec.deepColor);
            mat.SetFloat("_DepthCurve", 1.35f);

            mat.SetColor("_FresnelColor", spec.fresnelColor);
            mat.SetColor("_CloudColor", spec.cloudColor);
            mat.SetFloat("_FresnelPower", spec.fresnelPower);
            mat.SetFloat("_FresnelStrength", spec.fresnelStrength);
            mat.SetFloat("_CloudReflectionStrength", spec.cloudStrength);

            mat.SetTexture("_CausticsTex", spec.causticsTex);
            mat.SetFloat("_CausticStrength", spec.causticStrength);
            mat.SetFloat("_CausticScale", 0.62f);

            mat.SetTexture("_NoiseTex", spec.noiseTex);
            mat.SetColor("_FoamColor", spec.foamColor);
            mat.SetFloat("_FoamBandWidth", spec.foamBandWidth);
            mat.SetFloat("_ObjectFoamDistance", spec.objectFoamDist);
            mat.SetVector("_FlowParams", spec.flowParams);
            mat.SetFloat("_StreamRibbonStrength", 0.0f);

            mat.SetFloat("_SunSheen", spec.sunSheen);
            mat.SetFloat("_SunGloss", spec.sunGloss);
            mat.SetFloat("_SparkleThreshold", spec.sparkleThreshold);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void ApplyUberWaterPreset(
            IReadOnlyList<HexWaterPresetTarget> targets,
            string templateFileName,
            bool scaleForHexBasin)
        {
            string templatePath = $"{UberWaterTemplateDir}/{templateFileName}";
            Material templateMat = AssetDatabase.LoadAssetAtPath<Material>(templatePath);
            if (templateMat == null)
            {
                Debug.LogError($"[HexBiomeSurfaceBuilder] Could not find template material at '{templatePath}'.");
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                HexWaterPresetTarget target = targets[i];
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(target.materialPath);
                if (mat == null) continue;

                Undo.RecordObject(mat, $"Apply {templateFileName}");
                mat.shader = templateMat.shader;
                mat.CopyPropertiesFromMaterial(templateMat);
                mat.shaderKeywords = templateMat.shaderKeywords;

                if (scaleForHexBasin)
                {
                    HexUberWaterCalibration cal = target.calibration;
                    if (mat.HasProperty("_Water_Depth")) mat.SetFloat("_Water_Depth", cal.waterDepth);
                    if (mat.HasProperty("_ShoreFade")) mat.SetFloat("_ShoreFade", 1f);
                    if (mat.HasProperty("_ShoreFade_Smoothness")) mat.SetFloat("_ShoreFade_Smoothness", cal.shoreFadeSmoothness);
                    if (mat.HasProperty("_InterSec_Width"))
                    {
                        mat.SetFloat("_InterSec_Width", Mathf.Min(mat.GetFloat("_InterSec_Width"), cal.intersectionWidth));
                    }
                    if (mat.HasProperty("_SL_WaterDepth"))
                    {
                        mat.SetFloat("_SL_WaterDepth", Mathf.Min(mat.GetFloat("_SL_WaterDepth"), cal.shorelineWaterDepth));
                    }
                    if (mat.HasProperty("_Caustics_Depth")) mat.SetFloat("_Caustics_Depth", -0.75f);
                    if (mat.HasProperty("_Caustics_Start")) mat.SetFloat("_Caustics_Start", 20f);
                    if (mat.HasProperty("_Caustics_Fade")) mat.SetFloat("_Caustics_Fade", 15f);

                    Texture2D foamMask = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Acs/UberStylizedWater/Textures/Noise 3.png");
                    if (foamMask != null && mat.HasProperty("_InterSec_Foam_Mask") && mat.GetTexture("_InterSec_Foam_Mask") == null)
                    {
                        mat.SetTexture("_InterSec_Foam_Mask", foamMask);
                    }

                    if (mat.HasProperty("_ENABLE_UNDERWATERLAYER"))
                    {
                        mat.SetFloat("_ENABLE_UNDERWATERLAYER", 0f);
                        mat.DisableKeyword("_ENABLE_UNDERWATERLAYER");
                    }

                    if (mat.HasProperty("_Specular_Color"))
                    {
                        Color specCol = mat.GetColor("_Specular_Color");
                        if (specCol.r > 12f)
                        {
                            mat.SetColor("_Specular_Color", new Color(5.5f, 5.5f, 5.5f, 1f));
                        }
                    }

                    if (cal.preserveCustomPaletteOnPresetSwitch)
                    {
                        if (mat.HasProperty("_Color_Shallow")) mat.SetColor("_Color_Shallow", cal.customShallowColor);
                        if (mat.HasProperty("_Color_Deep")) mat.SetColor("_Color_Deep", cal.customDeepColor);
                        if (mat.HasProperty("_SurfFoam_Color")) mat.SetColor("_SurfFoam_Color", cal.surfFoamColor);
                        if (mat.HasProperty("_InterSec_Color")) mat.SetColor("_InterSec_Color", cal.customIntersectionColor);
                        if (mat.HasProperty("_SL_Color")) mat.SetColor("_SL_Color", cal.shorelineColor);
                        if (mat.HasProperty("_Underwater_Color")) mat.SetColor("_Underwater_Color", cal.underwaterColor);
                    }

                    // Disable big ocean vertex waves so hex basin water stays inside its bank
                    if (mat.HasProperty("_ENABLEWAVE")) mat.SetFloat("_ENABLEWAVE", 0f);
                    mat.DisableKeyword("_ENABLEWAVE");
                }

                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        public static void SwitchWaterMaterialsToShader(IReadOnlyList<string> materialPaths, Shader targetShader)
        {
            if (targetShader == null || materialPaths == null) return;

            for (int i = 0; i < materialPaths.Count; i++)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPaths[i]);
                if (mat == null) continue;

                mat.shader = targetShader;
                mat.shaderKeywords = System.Array.Empty<string>();
                EditorUtility.SetDirty(mat);
            }
        }

        public static GameObject BuildBiomeClusterPrefab(in HexBiomeClusterBuildSpec spec)
        {
            EnsureFolder(spec.meshesOutDir);
            EnsureFolder(spec.prefabsOutDir);

            string tag = $"{spec.biomeName}_{spec.clusterSize}Hex";
            string groundMeshPath = $"{spec.meshesOutDir}/Mesh_Ground_{tag}.asset";
            Mesh groundMesh = HexBiomeSurfaceMeshBuilder.BuildGroundClusterMesh($"Mesh_Ground_{tag}", spec.hexes, spec.waterMask, spec.surfaceSettings);
            groundMesh = SaveOrReplaceMeshAsset(groundMesh, groundMeshPath);

            Mesh waterMesh = null;
            if (spec.waterMask != null && spec.waterMaterial != null)
            {
                string waterMeshPath = $"{spec.meshesOutDir}/Mesh_Water_{tag}.asset";
                waterMesh = HexBiomeSurfaceMeshBuilder.BuildWaterSurfaceMesh($"Mesh_Water_{tag}", spec.hexes, spec.waterMask, spec.surfaceSettings);
                waterMesh = SaveOrReplaceMeshAsset(waterMesh, waterMeshPath);
            }

            GameObject root = new GameObject($"Biome_{tag}");
            try
            {
                GameObject groundGo = new GameObject("Ground_Basin");
                groundGo.transform.SetParent(root.transform, false);
                MeshFilter gMf = groundGo.AddComponent<MeshFilter>();
                gMf.sharedMesh = groundMesh;
                MeshRenderer gMr = groundGo.AddComponent<MeshRenderer>();
                gMr.sharedMaterial = spec.groundMaterial;
                MeshCollider gCol = groundGo.AddComponent<MeshCollider>();
                gCol.sharedMesh = groundMesh;

                if (waterMesh != null && spec.waterMaterial != null)
                {
                    GameObject waterGo = new GameObject("Water_Surface");
                    waterGo.transform.SetParent(root.transform, false);
                    MeshFilter wMf = waterGo.AddComponent<MeshFilter>();
                    wMf.sharedMesh = waterMesh;
                    MeshRenderer wMr = waterGo.AddComponent<MeshRenderer>();
                    wMr.sharedMaterial = spec.waterMaterial;
                    wMr.shadowCastingMode = ShadowCastingMode.Off;
                    wMr.receiveShadows = true;
                }

                string prefabPath = $"{spec.prefabsOutDir}/Biome_{tag}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static Mesh SaveOrReplaceMeshAsset(Mesh newMesh, string assetPath)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(newMesh, assetPath);
                return newMesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(newMesh, existing);
            existing.name = newMesh.name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(newMesh);
            return existing;
        }

        public static void EnsureFolder(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath) || AssetDatabase.IsValidFolder(assetFolderPath)) return;
            string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetFolderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

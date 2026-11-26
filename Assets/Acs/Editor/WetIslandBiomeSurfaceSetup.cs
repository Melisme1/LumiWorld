using System.Collections.Generic;
using LumiWorld.Acs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LumiWorld.EditorTools
{
    /// <summary>
    /// Wet Island biome presets, material palettes, 3/6/10-hex cluster generation menu,
    /// and showcase launcher. All generic shader/material/mesh/prefab logic lives in HexBiomeSurfaceBuilder.
    /// </summary>
    public static class WetIslandBiomeSurfaceSetup
    {
        private const string GroundTexDir = "Assets/Acs/Art/Ground/Textures/WetIsland";
        private const string WaterMaskDir = "Assets/Acs/Hex/WetIsland/WaterMasks";
        private const string MaterialsOutDir = "Assets/Acs/Art/Ground/Materials/WetIsland";
        private const string BiomesRootDir = "Assets/Acs/Hex/WetIsland/Biomes";
        private const string MeshesOutDir = "Assets/Acs/Hex/WetIsland/Biomes/Meshes";
        private const string PrefabsOutDir = "Assets/Acs/Hex/WetIsland/Biomes/Prefabs";

        private const string WaterNormalTexPath = "Assets/Acs/HexTilesNature/Assets/VFX/Water/Textures/Water_Normal.png";
        private const string WaterNormalFallbackPath = "Assets/VinhWorkSpace/Textures/Water_Normal.png";
        private const string WaterCausticsTexPath = "Assets/Acs/HexTilesNature/Assets/VFX/Water/Textures/Water_WaterCaustics.png";
        private const string WaterNoiseTexPath = "Assets/VinhWorkSpace/Textures/Water_Noise.png";

        private static readonly string[] WaterMaterialPaths = new[]
        {
            $"{MaterialsOutDir}/M_WetWater_Lilywater.mat",
            $"{MaterialsOutDir}/M_WetWater_Lilywater_3Hex.mat",
            $"{MaterialsOutDir}/M_WetWater_Lilywater_6Hex.mat",
            $"{MaterialsOutDir}/M_WetWater_Lilywater_10Hex.mat",
            $"{MaterialsOutDir}/M_WetWater_Streamstone.mat",
            $"{MaterialsOutDir}/M_WetWater_Reedmarsh.mat",
            $"{MaterialsOutDir}/M_WetWater_Reedmarsh_3Hex.mat",
            $"{MaterialsOutDir}/M_WetWater_Reedmarsh_6Hex.mat",
            $"{MaterialsOutDir}/M_WetWater_Reedmarsh_10Hex.mat"
        };

        public static IReadOnlyList<string> GetWaterMaterialPaths() => WaterMaterialPaths;

        [MenuItem("LumiWorld/WetIsland/1. Build All 4 Biomes (Materials + 3/6/10 Hex Prefabs) & Spawn Showcase")]
        public static void BuildAllAndSpawnShowcase()
        {
            BuildAllAssets();
            SpawnShowcaseInActiveScene(includeAllSizes: false);
        }

        [MenuItem("LumiWorld/WetIsland/2. Spawn 4 Biomes Showcase (3, 6, and 10 Hex Matrix)")]
        public static void SpawnFullMatrixShowcase()
        {
            BuildAllAssets();
            SpawnShowcaseInActiveScene(includeAllSizes: true);
        }

        [MenuItem("LumiWorld/WetIsland/3. Toggle Temp Raycast Rocks & Shadow Pillars in Showcase")]
        public static void ToggleTempRaycastProps()
        {
            GameObject showcaseRoot = GameObject.Find("WetIsland_4Biomes_Showcase");
            if (showcaseRoot == null)
            {
                Debug.LogWarning("[WetIslandSetup] 'WetIsland_4Biomes_Showcase' not found in active scene.");
                return;
            }

            bool anyToggled = false;
            foreach (Transform biomeTr in showcaseRoot.transform)
            {
                Transform tempProps = biomeTr.Find("Temp_RaycastLighting_Props");
                if (tempProps != null)
                {
                    Undo.RecordObject(tempProps.gameObject, "Toggle Temp Raycast Props");
                    tempProps.gameObject.SetActive(!tempProps.gameObject.activeSelf);
                    anyToggled = true;
                }
            }

            if (anyToggled && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        [MenuItem("LumiWorld/WetIsland/4. Apply UberStylizedWater Preset: Clear (Default)")]
        public static void ApplyPresetClear() => ApplyUberWaterPresetToBiomes("UWa-Template-Clear.mat", scaleForHexBasin: true);

        [MenuItem("LumiWorld/WetIsland/5. Apply UberStylizedWater Preset: Genshin")]
        public static void ApplyPresetGenshin() => ApplyUberWaterPresetToBiomes("UWa-Template-Genshin.mat", scaleForHexBasin: true);

        [MenuItem("LumiWorld/WetIsland/6. Apply UberStylizedWater Preset: Tropical")]
        public static void ApplyPresetTropical() => ApplyUberWaterPresetToBiomes("UWa-Template-Tropical.mat", scaleForHexBasin: true);

        [MenuItem("LumiWorld/WetIsland/7. Apply UberStylizedWater Preset: Anime")]
        public static void ApplyPresetAnime() => ApplyUberWaterPresetToBiomes("UWa-Template-Anime.mat", scaleForHexBasin: true);

        public static void BuildAllAssets()
        {
            HexBiomeSurfaceBuilder.EnsureFolder(MaterialsOutDir);
            HexBiomeSurfaceBuilder.EnsureFolder(BiomesRootDir);
            HexBiomeSurfaceBuilder.EnsureFolder(MeshesOutDir);
            HexBiomeSurfaceBuilder.EnsureFolder(PrefabsOutDir);
            HexBiomeSurfaceBuilder.EnsureURPDepthAndOpaqueTexturesEnabled();

            string texLily = $"{GroundTexDir}/T_Ground_Lilywater_Clean_v2.png";
            string texStream = $"{GroundTexDir}/T_Ground_Streamstone_Gray_v3.png";
            string texReed = $"{GroundTexDir}/T_Ground_Reedmarsh_v1.png";
            string texRain = $"{GroundTexDir}/T_Ground_Rainleaf_Seamless.png";

            HexBiomeSurfaceBuilder.ConfigureGroundTexture(texLily);
            HexBiomeSurfaceBuilder.ConfigureGroundTexture(texStream);
            HexBiomeSurfaceBuilder.ConfigureGroundTexture(texReed);
            HexBiomeSurfaceBuilder.ConfigureGroundTexture(texRain);

            string maskLily = $"{WaterMaskDir}/T_WaterMask_Lilywater_v4.png";
            string maskStream = $"{WaterMaskDir}/T_WaterMask_Streamstone_v3.png";
            string maskReed6 = $"{WaterMaskDir}/T_WaterMask_Reedmarsh_v3.png";
            string maskReed3 = $"{WaterMaskDir}/T_WaterMask_Reedmarsh_3Hex_v3.png";

            HexBiomeSurfaceBuilder.ConfigureWaterMaskTexture(maskLily);
            HexBiomeSurfaceBuilder.ConfigureWaterMaskTexture(maskStream);
            HexBiomeSurfaceBuilder.ConfigureWaterMaskTexture(maskReed6);
            HexBiomeSurfaceBuilder.ConfigureWaterMaskTexture(maskReed3);

            AssetDatabase.Refresh();

            Shader groundShader = HexBiomeSurfaceBuilder.FindGroundShader();
            Shader waterShader = HexBiomeSurfaceBuilder.FindPreferredWaterShader();
            if (groundShader == null || waterShader == null)
            {
                Debug.LogError($"[WetIslandSetup] Missing shaders: '{HexBiomeSurfaceBuilder.GroundShaderName}' or '{HexBiomeSurfaceBuilder.UberWaterShaderPath}'.");
                return;
            }

            Texture2D tGroundLily = AssetDatabase.LoadAssetAtPath<Texture2D>(texLily);
            Texture2D tGroundStream = AssetDatabase.LoadAssetAtPath<Texture2D>(texStream);
            Texture2D tGroundReed = AssetDatabase.LoadAssetAtPath<Texture2D>(texReed);
            Texture2D tGroundRain = AssetDatabase.LoadAssetAtPath<Texture2D>(texRain);

            Texture2D tMaskLily = AssetDatabase.LoadAssetAtPath<Texture2D>(maskLily);
            Texture2D tMaskStream = AssetDatabase.LoadAssetAtPath<Texture2D>(maskStream);
            Texture2D tMaskReed6 = AssetDatabase.LoadAssetAtPath<Texture2D>(maskReed6);
            Texture2D tMaskReed3 = AssetDatabase.LoadAssetAtPath<Texture2D>(maskReed3);

            Texture2D tWaterNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalTexPath)
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalFallbackPath);
            Texture2D tWaterCaustics = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterCausticsTexPath);
            Texture2D tWaterNoise = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNoiseTexPath);

            // A. Lilywater
            Material matGroundLily = HexBiomeSurfaceBuilder.CreateOrUpdateGroundMaterial(groundShader, new HexGroundMaterialSpec
            {
                materialPath = $"{MaterialsOutDir}/M_WetGround_Lilywater.mat",
                topTex = tGroundLily,
                waterMask = tMaskLily,
                causticsTex = tWaterCaustics,
                shoreStart = 0.03f,
                waterThreshold = 0.21f,
                shoreColor = new Color(0.93f, 0.78f, 0.44f, 1f),
                bedColor = new Color(0.88f, 0.73f, 0.41f, 1f),
                bedMidColor = new Color(0.58f, 0.56f, 0.36f, 1f),
                bedDeepColor = new Color(0.24f, 0.38f, 0.35f, 1f),
                sideColor = new Color(0.62f, 0.44f, 0.27f, 1f),
                sideBotColor = new Color(0.38f, 0.26f, 0.15f, 1f),
                wetDarken = 0.12f,
                causticColor = new Color(1.0f, 0.96f, 0.84f, 1f),
                causticStrength = 0.32f
            });

            Material matWaterLily = HexBiomeSurfaceBuilder.CreateOrUpdateWaterMaterial(waterShader, new HexWaterMaterialSpec
            {
                materialPath = $"{MaterialsOutDir}/M_WetWater_Lilywater.mat",
                waterMask = tMaskLily,
                normalTex = tWaterNormal,
                causticsTex = tWaterCaustics,
                noiseTex = tWaterNoise,
                waterThreshold = 0.21f,
                edgeFeather = 0.024f,
                normalScale = 0.46f,
                normalStrength = 0.42f,
                refractionStrength = 0.024f,
                shallowColor = new Color(0.32f, 0.86f, 0.82f, 0.10f),
                midColor = new Color(0.14f, 0.58f, 0.74f, 0.56f),
                deepColor = new Color(0.02f, 0.32f, 0.48f, 0.95f),
                fresnelColor = new Color(0.46f, 0.76f, 0.98f, 1.0f),
                cloudColor = new Color(0.95f, 0.97f, 1.0f, 1.0f),
                fresnelPower = 2.5f,
                fresnelStrength = 0.54f,
                cloudStrength = 0.24f,
                foamColor = new Color(0.98f, 0.99f, 1.0f, 0.84f),
                foamBandWidth = 0.026f,
                objectFoamDist = 0.16f,
                causticStrength = 0.38f,
                flowParams = new Vector4(0.70f, 0.40f, 0.46f, 0.07f),
                sunSheen = 2.4f,
                sunGloss = 220f,
                sparkleThreshold = 0.955f,
                uberCalibration = CreateLilywaterUberCalibration()
            });

            // B. Streamstone
            Material matGroundStream = HexBiomeSurfaceBuilder.CreateOrUpdateGroundMaterial(groundShader, new HexGroundMaterialSpec
            {
                materialPath = $"{MaterialsOutDir}/M_WetGround_Streamstone.mat",
                topTex = tGroundStream,
                waterMask = tMaskStream,
                causticsTex = tWaterCaustics,
                shoreStart = 0.06f,
                waterThreshold = 0.42f,
                shoreColor = new Color(0.58f, 0.57f, 0.52f, 1f),
                bedColor = new Color(0.55f, 0.54f, 0.49f, 1f),
                bedMidColor = new Color(0.48f, 0.49f, 0.45f, 1f),
                bedDeepColor = new Color(0.36f, 0.40f, 0.39f, 1f),
                sideColor = new Color(0.48f, 0.45f, 0.40f, 1f),
                sideBotColor = new Color(0.30f, 0.28f, 0.25f, 1f),
                wetDarken = 0.12f,
                causticColor = new Color(0.95f, 0.99f, 0.95f, 1f),
                causticStrength = 0.36f
            });

            Material matWaterStream = HexBiomeSurfaceBuilder.CreateOrUpdateWaterMaterial(waterShader, new HexWaterMaterialSpec
            {
                materialPath = $"{MaterialsOutDir}/M_WetWater_Streamstone.mat",
                waterMask = tMaskStream,
                normalTex = tWaterNormal,
                causticsTex = tWaterCaustics,
                noiseTex = tWaterNoise,
                waterThreshold = 0.42f,
                edgeFeather = 0.026f,
                normalScale = 0.54f,
                normalStrength = 0.48f,
                refractionStrength = 0.026f,
                shallowColor = new Color(0.48f, 0.88f, 0.92f, 0.10f),
                midColor = new Color(0.24f, 0.70f, 0.82f, 0.40f),
                deepColor = new Color(0.14f, 0.48f, 0.68f, 0.66f),
                fresnelColor = new Color(0.52f, 0.82f, 0.98f, 1.0f),
                cloudColor = new Color(0.96f, 0.98f, 1.0f, 1.0f),
                fresnelPower = 2.6f,
                fresnelStrength = 0.48f,
                cloudStrength = 0.20f,
                foamColor = new Color(0.98f, 0.99f, 1.0f, 0.86f),
                foamBandWidth = 0.032f,
                objectFoamDist = 0.18f,
                causticStrength = 0.42f,
                flowParams = new Vector4(0.82f, -0.57f, 0.54f, 0.22f),
                sunSheen = 2.5f,
                sunGloss = 200f,
                sparkleThreshold = 0.952f,
                uberCalibration = CreateStreamstoneUberCalibration()
            });

            // C. Reedmarsh (6/10 Hex & 3 Hex masks)
            Material matGroundReed6 = CreateReedmarshGroundMaterial(groundShader, $"{MaterialsOutDir}/M_WetGround_Reedmarsh.mat", tGroundReed, tMaskReed6, tWaterCaustics);
            Material matWaterReed6 = CreateReedmarshWaterMaterial(waterShader, $"{MaterialsOutDir}/M_WetWater_Reedmarsh.mat", tMaskReed6, tWaterNormal, tWaterCaustics, tWaterNoise);
            Material matGroundReed3 = CreateReedmarshGroundMaterial(groundShader, $"{MaterialsOutDir}/M_WetGround_Reedmarsh_3Hex.mat", tGroundReed, tMaskReed3, tWaterCaustics);
            Material matWaterReed3 = CreateReedmarshWaterMaterial(waterShader, $"{MaterialsOutDir}/M_WetWater_Reedmarsh_3Hex.mat", tMaskReed3, tWaterNormal, tWaterCaustics, tWaterNoise);

            // D. Rainleaf (dry rainforest floor)
            Material matGroundRain = HexBiomeSurfaceBuilder.CreateOrUpdateGroundMaterial(groundShader, new HexGroundMaterialSpec
            {
                materialPath = $"{MaterialsOutDir}/M_WetGround_Rainleaf.mat",
                topTex = tGroundRain,
                waterMask = null,
                causticsTex = tWaterCaustics,
                shoreStart = 0.90f,
                waterThreshold = 0.95f,
                shoreColor = new Color(0.28f, 0.22f, 0.15f, 1f),
                bedColor = new Color(0.28f, 0.22f, 0.15f, 1f),
                bedMidColor = new Color(0.28f, 0.22f, 0.15f, 1f),
                bedDeepColor = new Color(0.28f, 0.22f, 0.15f, 1f),
                sideColor = new Color(0.38f, 0.27f, 0.17f, 1f),
                sideBotColor = new Color(0.24f, 0.16f, 0.10f, 1f),
                wetDarken = 0.0f,
                causticColor = Color.black,
                causticStrength = 0.0f
            });

            int[] sizes = new[] { 3, 6, 10 };
            foreach (int size in sizes)
            {
                List<HexCoordinates> hexes = HexBiomeSurfaceMeshBuilder.GetUprightTriangleHexes(size);

                var lilySettings = HexBiomeSurfaceSettings.CreateDefault();
                lilySettings.shoreStartMask = 0.03f;
                lilySettings.waterThresholdMask = 0.21f;
                lilySettings.maxBasinDepth = 0.72f;
                lilySettings.waterSurfaceDrop = 0.055f;

                HexBiomeSurfaceBuilder.BuildBiomeClusterPrefab(new HexBiomeClusterBuildSpec
                {
                    biomeName = "Lilywater",
                    clusterSize = size,
                    hexes = hexes,
                    waterMask = tMaskLily,
                    surfaceSettings = lilySettings,
                    groundMaterial = matGroundLily,
                    waterMaterial = matWaterLily,
                    meshesOutDir = MeshesOutDir,
                    prefabsOutDir = PrefabsOutDir
                });

                var streamSettings = HexBiomeSurfaceSettings.CreateDefault();
                streamSettings.shoreStartMask = 0.06f;
                streamSettings.waterThresholdMask = 0.42f;
                streamSettings.maxBasinDepth = 0.34f;
                streamSettings.waterSurfaceDrop = 0.055f;
                streamSettings.streamSlopeDrop = 0.08f;
                streamSettings.streamSlopeDir = new Vector2(0.82f, -0.57f);

                HexBiomeSurfaceBuilder.BuildBiomeClusterPrefab(new HexBiomeClusterBuildSpec
                {
                    biomeName = "Streamstone",
                    clusterSize = size,
                    hexes = hexes,
                    waterMask = tMaskStream,
                    surfaceSettings = streamSettings,
                    groundMaterial = matGroundStream,
                    waterMaterial = matWaterStream,
                    meshesOutDir = MeshesOutDir,
                    prefabsOutDir = PrefabsOutDir
                });

                var reedSettings = HexBiomeSurfaceSettings.CreateDefault();
                reedSettings.shoreStartMask = 0.05f;
                reedSettings.waterThresholdMask = 0.38f;
                reedSettings.maxBasinDepth = 0.24f;
                reedSettings.waterSurfaceDrop = 0.045f;

                HexBiomeSurfaceBuilder.BuildBiomeClusterPrefab(new HexBiomeClusterBuildSpec
                {
                    biomeName = "Reedmarsh",
                    clusterSize = size,
                    hexes = hexes,
                    waterMask = size == 3 ? tMaskReed3 : tMaskReed6,
                    surfaceSettings = reedSettings,
                    groundMaterial = size == 3 ? matGroundReed3 : matGroundReed6,
                    waterMaterial = size == 3 ? matWaterReed3 : matWaterReed6,
                    meshesOutDir = MeshesOutDir,
                    prefabsOutDir = PrefabsOutDir
                });

                var rainSettings = HexBiomeSurfaceSettings.CreateDefault();
                rainSettings.maxBasinDepth = 0f;

                HexBiomeSurfaceBuilder.BuildBiomeClusterPrefab(new HexBiomeClusterBuildSpec
                {
                    biomeName = "Rainleaf",
                    clusterSize = size,
                    hexes = hexes,
                    waterMask = null,
                    surfaceSettings = rainSettings,
                    groundMaterial = matGroundRain,
                    waterMaterial = null,
                    meshesOutDir = MeshesOutDir,
                    prefabsOutDir = PrefabsOutDir
                });
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WetIslandSetup] Built all 4 Wet Island biome materials & 3/6/10-hex cluster prefabs successfully!");
        }

        public static void SpawnShowcaseInActiveScene(bool includeAllSizes)
        {
            const string rootName = "WetIsland_4Biomes_Showcase";
            GameObject existing = GameObject.Find(rootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject showcaseRoot = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(showcaseRoot, "Spawn WetIsland 4 Biomes Showcase");
            showcaseRoot.transform.position = new Vector3(30f, 0f, 10f);

            EnsureCounterLightSunAndAtmosphere();

            string[] biomes = new[] { "Lilywater", "Streamstone", "Reedmarsh", "Rainleaf" };
            int[] sizes = includeAllSizes ? new[] { 3, 6, 10 } : new[] { 6 };

            GameObject lilyInstance = null;

            for (int r = 0; r < sizes.Length; r++)
            {
                int size = sizes[r];
                float zOffset = r * 9.5f;

                for (int c = 0; c < biomes.Length; c++)
                {
                    string prefabPath = $"{PrefabsOutDir}/Biome_{biomes[c]}_{size}Hex.prefab";
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null) continue;

                    GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, showcaseRoot.transform);
                    inst.name = $"Biome_{biomes[c]}_{size}Hex";
                    inst.transform.localPosition = new Vector3(c * 8.5f, 0f, zOffset);

                    if (r == 0 && c == 0) lilyInstance = inst;

                    if (c < 3)
                    {
                        SpawnTempRaycastLightingPropsForBiome(inst, biomes[c]);
                    }
                }
            }

            Physics.SyncTransforms();

            Selection.activeGameObject = lilyInstance != null ? lilyInstance : showcaseRoot;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView sv = SceneView.lastActiveSceneView;
                sv.sceneLighting = true;
                Vector3 focusTarget = showcaseRoot.transform.position + new Vector3(3.2f, 0.85f, -0.3f);
                Quaternion counterLightCamRot = Quaternion.Euler(29f, 22f, 0f);
                sv.LookAt(focusTarget, counterLightCamRot, 6.2f);
                sv.Repaint();
            }

            Debug.Log($"[WetIslandSetup] Spawned '{rootName}' showcase.");
        }

        private static void ApplyUberWaterPresetToBiomes(string templateFileName, bool scaleForHexBasin)
        {
            HexUberWaterCalibration lilyCal = CreateLilywaterUberCalibration();
            HexUberWaterCalibration streamCal = CreateStreamstoneUberCalibration();
            HexUberWaterCalibration reedCal = CreateReedmarshUberCalibration();

            var targets = new[]
            {
                new HexWaterPresetTarget { materialPath = $"{MaterialsOutDir}/M_WetWater_Lilywater.mat", calibration = lilyCal },
                new HexWaterPresetTarget { materialPath = $"{MaterialsOutDir}/M_WetWater_Lilywater_3Hex.mat", calibration = lilyCal },
                new HexWaterPresetTarget { materialPath = $"{MaterialsOutDir}/M_WetWater_Lilywater_6Hex.mat", calibration = lilyCal },
                new HexWaterPresetTarget { materialPath = $"{MaterialsOutDir}/M_WetWater_Lilywater_10Hex.mat", calibration = lilyCal },
                new HexWaterPresetTarget { materialPath = $"{MaterialsOutDir}/M_WetWater_Streamstone.mat", calibration = streamCal },
                new HexWaterPresetTarget { materialPath = $"{MaterialsOutDir}/M_WetWater_Reedmarsh.mat", calibration = reedCal },
                new HexWaterPresetTarget { materialPath = $"{MaterialsOutDir}/M_WetWater_Reedmarsh_3Hex.mat", calibration = reedCal },
                new HexWaterPresetTarget { materialPath = $"{MaterialsOutDir}/M_WetWater_Reedmarsh_6Hex.mat", calibration = reedCal },
                new HexWaterPresetTarget { materialPath = $"{MaterialsOutDir}/M_WetWater_Reedmarsh_10Hex.mat", calibration = reedCal }
            };

            HexBiomeSurfaceBuilder.ApplyUberWaterPreset(targets, templateFileName, scaleForHexBasin);
            Debug.Log($"[WetIslandSetup] Applied UberStylizedWater preset '{templateFileName}' to all 9 Wet Island water materials!");
        }

        private static HexUberWaterCalibration CreateLilywaterUberCalibration()
        {
            var cal = HexUberWaterCalibration.CreateClearPondDefault();
            cal.waterDepth = 0.68f;
            cal.shoreFadeSmoothness = 0.22f;
            cal.customShallowColor = new Color(0.28f, 0.88f, 0.78f, 0.04f);
            cal.customDeepColor = new Color(0.01f, 0.28f, 0.46f, 0.70f);
            return cal;
        }

        private static HexUberWaterCalibration CreateStreamstoneUberCalibration()
        {
            var cal = HexUberWaterCalibration.CreateClearPondDefault();
            cal.waterDepth = 0.42f;
            cal.normalScale = 3.2f;
            cal.normalStrength = 0.38f;
            cal.normalPan = 0.65f;
            cal.customShallowColor = new Color(0.42f, 0.88f, 0.92f, 0.04f);
            cal.customDeepColor = new Color(0.08f, 0.42f, 0.64f, 0.72f);
            return cal;
        }

        private static HexUberWaterCalibration CreateReedmarshUberCalibration()
        {
            return new HexUberWaterCalibration
            {
                waterDepth = 0.32f,
                shoreFadeSmoothness = 0.16f,
                intersectionWidth = 0.14f,
                shorelineWaterDepth = 0.14f,
                underwaterDepth = -0.35f,
                underwaterColor = new Color(0.36f, 0.30f, 0.10f, 0.28f),
                normalScale = 2.8f,
                normalStrength = 0.32f,
                normalPan = 0.35f,
                causticStrength = 2.2f,
                surfFoamColor = new Color(0.88f, 0.82f, 0.52f, 0.85f),
                shorelineColor = new Color(0.86f, 0.80f, 0.52f, 0.78f),
                preserveCustomPaletteOnPresetSwitch = true,
                customShallowColor = new Color(0.68f, 0.62f, 0.22f, 0.18f),
                customDeepColor = new Color(0.24f, 0.34f, 0.10f, 0.76f),
                customIntersectionColor = new Color(0.88f, 0.82f, 0.52f, 0.78f)
            };
        }

        private static Material CreateReedmarshGroundMaterial(
            Shader groundShader,
            string matPath,
            Texture2D groundTex,
            Texture2D maskTex,
            Texture2D causticsTex)
        {
            return HexBiomeSurfaceBuilder.CreateOrUpdateGroundMaterial(groundShader, new HexGroundMaterialSpec
            {
                materialPath = matPath,
                topTex = groundTex,
                waterMask = maskTex,
                causticsTex = causticsTex,
                shoreStart = 0.05f,
                waterThreshold = 0.42f,
                shoreColor = new Color(0.45f, 0.38f, 0.22f, 1f),
                bedColor = new Color(0.42f, 0.36f, 0.20f, 1f),
                bedMidColor = new Color(0.35f, 0.31f, 0.17f, 1f),
                bedDeepColor = new Color(0.25f, 0.24f, 0.13f, 1f),
                sideColor = new Color(0.34f, 0.27f, 0.18f, 1f),
                sideBotColor = new Color(0.21f, 0.16f, 0.11f, 1f),
                wetDarken = 0.12f,
                causticColor = new Color(0.96f, 0.90f, 0.56f, 1f),
                causticStrength = 0.30f
            });
        }

        private static Material CreateReedmarshWaterMaterial(
            Shader waterShader,
            string matPath,
            Texture2D maskTex,
            Texture2D normalTex,
            Texture2D causticsTex,
            Texture2D noiseTex)
        {
            return HexBiomeSurfaceBuilder.CreateOrUpdateWaterMaterial(waterShader, new HexWaterMaterialSpec
            {
                materialPath = matPath,
                waterMask = maskTex,
                normalTex = normalTex,
                causticsTex = causticsTex,
                noiseTex = noiseTex,
                waterThreshold = 0.42f,
                edgeFeather = 0.028f,
                normalScale = 0.50f,
                normalStrength = 0.36f,
                refractionStrength = 0.020f,
                shallowColor = new Color(0.68f, 0.62f, 0.22f, 0.42f),
                midColor = new Color(0.46f, 0.48f, 0.16f, 0.68f),
                deepColor = new Color(0.28f, 0.34f, 0.10f, 0.92f),
                fresnelColor = new Color(0.72f, 0.74f, 0.48f, 1.0f),
                cloudColor = new Color(0.94f, 0.92f, 0.78f, 1.0f),
                fresnelPower = 2.4f,
                fresnelStrength = 0.46f,
                cloudStrength = 0.18f,
                foamColor = new Color(0.88f, 0.82f, 0.52f, 0.78f),
                foamBandWidth = 0.024f,
                objectFoamDist = 0.14f,
                causticStrength = 0.30f,
                flowParams = new Vector4(0.60f, 0.80f, 0.50f, 0.06f),
                sunSheen = 2.1f,
                sunGloss = 210f,
                sparkleThreshold = 0.958f,
                uberCalibration = CreateReedmarshUberCalibration()
            });
        }

        private static void SpawnTempRaycastLightingPropsForBiome(GameObject biomeInstance, string biomeName)
        {
            Transform groundTr = biomeInstance.transform.Find("Ground_Basin");
            if (groundTr == null) return;
            Collider basinCol = groundTr.GetComponent<Collider>();
            if (basinCol == null) return;

            Physics.SyncTransforms();

            GameObject propGroup = new GameObject("Temp_RaycastLighting_Props");
            propGroup.transform.SetParent(biomeInstance.transform, false);

            GameObject rockPrefab1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Acs/Art/Stone/Prefabs/Rocks/SM_Rocks_01.prefab");
            GameObject rockPrefab2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Acs/Art/Stone/Prefabs/Rocks/SM_Rocks_03.prefab");
            GameObject stoneBig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Acs/Art/Stone/Prefabs/Stones/Big_2.prefab");
            GameObject stoneSmall = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Acs/Art/Stone/Prefabs/Stones/Small_2.prefab");

            if (biomeName == "Lilywater")
            {
                PlaceRaycastRock(propGroup.transform, basinCol, rockPrefab1, new Vector3(-0.95f, 0f, -0.15f), new Vector3(0.42f, 0.38f, 0.42f), 25f, sinkOffset: -0.04f);
                PlaceRaycastRock(propGroup.transform, basinCol, stoneBig, new Vector3(-0.35f, 0f, -0.55f), new Vector3(0.32f, 0.24f, 0.32f), 110f, sinkOffset: -0.05f);
                PlaceRaycastRock(propGroup.transform, basinCol, stoneSmall, new Vector3(0.75f, 0f, -0.45f), new Vector3(0.28f, 0.22f, 0.28f), -40f, sinkOffset: -0.02f);

                CreateShadowCasterPillar(propGroup.transform, basinCol, new Vector3(0.15f, 0f, 1.35f), new Vector3(0.28f, 1.45f, 0.28f), Quaternion.Euler(16f, -20f, 14f));
                CreateShadowCasterPillar(propGroup.transform, basinCol, new Vector3(1.10f, 0f, 0.85f), new Vector3(0.24f, 1.15f, 0.24f), Quaternion.Euler(12f, -35f, -18f));
            }
            else if (biomeName == "Streamstone")
            {
                PlaceRaycastRock(propGroup.transform, basinCol, rockPrefab2, new Vector3(-1.05f, 0f, 0.05f), new Vector3(0.68f, 0.75f, 0.65f), 35f, sinkOffset: -0.05f);
                PlaceRaycastRock(propGroup.transform, basinCol, rockPrefab1, new Vector3(-0.65f, 0f, 0.45f), new Vector3(0.48f, 0.52f, 0.48f), -20f, sinkOffset: -0.04f);
                PlaceRaycastRock(propGroup.transform, basinCol, stoneBig, new Vector3(-0.05f, 0f, -0.22f), new Vector3(0.34f, 0.20f, 0.30f), 18f, sinkOffset: -0.04f);
                PlaceRaycastRock(propGroup.transform, basinCol, stoneSmall, new Vector3(0.48f, 0f, -0.62f), new Vector3(0.28f, 0.18f, 0.26f), -32f, sinkOffset: -0.03f);
            }
            else if (biomeName == "Reedmarsh")
            {
                PlaceRaycastRock(propGroup.transform, basinCol, stoneSmall, new Vector3(-0.35f, 0f, 0.10f), new Vector3(0.26f, 0.20f, 0.26f), 45f, sinkOffset: -0.03f);
                PlaceRaycastRock(propGroup.transform, basinCol, stoneSmall, new Vector3(0.65f, 0f, -0.55f), new Vector3(0.22f, 0.18f, 0.22f), -15f, sinkOffset: -0.02f);
            }
        }

        private static void PlaceRaycastRock(
            Transform parent,
            Collider basinCol,
            GameObject prefab,
            Vector3 localXZ,
            Vector3 scale,
            float yaw,
            float sinkOffset)
        {
            Vector3 worldOrigin = parent.TransformPoint(new Vector3(localXZ.x, 5.0f, localXZ.z));
            Ray ray = new Ray(worldOrigin, Vector3.down);
            Vector3 hitPoint = parent.TransformPoint(new Vector3(localXZ.x, 0.90f, localXZ.z));

            if (basinCol.Raycast(ray, out RaycastHit hit, 10.0f))
            {
                hitPoint = hit.point;
            }

            GameObject obj;
            if (prefab != null)
            {
                obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            }
            else
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.SetParent(parent, false);
            }

            obj.transform.position = hitPoint + new Vector3(0f, sinkOffset, 0f);
            obj.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            obj.transform.localScale = scale;
        }

        private static void CreateShadowCasterPillar(
            Transform parent,
            Collider basinCol,
            Vector3 localXZ,
            Vector3 scale,
            Quaternion localRot)
        {
            Vector3 worldOrigin = parent.TransformPoint(new Vector3(localXZ.x, 5.0f, localXZ.z));
            Ray ray = new Ray(worldOrigin, Vector3.down);
            Vector3 hitPoint = parent.TransformPoint(new Vector3(localXZ.x, 1.0f, localXZ.z));

            if (basinCol.Raycast(ray, out RaycastHit hit, 10.0f))
            {
                hitPoint = hit.point;
            }

            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Temp_ShadowCaster_Monolith";
            pillar.transform.SetParent(parent, false);
            pillar.transform.position = hitPoint + new Vector3(0f, scale.y * 0.35f, 0f);
            pillar.transform.localRotation = localRot;
            pillar.transform.localScale = scale;

            Material stoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Acs/Art/Stone/Materials/M_Rocks_Stylized.mat");
            if (stoneMat != null)
            {
                MeshRenderer mr = pillar.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = stoneMat;
            }
        }

        private static void EnsureCounterLightSunAndAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.76f, 0.96f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.68f, 0.64f, 0.56f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.38f, 0.28f, 1f);

            Light mainSun = RenderSettings.sun;
            if (mainSun == null)
            {
                Light[] lights = Object.FindObjectsByType<Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional)
                    {
                        mainSun = lights[i];
                        break;
                    }
                }
            }

            if (mainSun != null)
            {
                Undo.RecordObject(mainSun, "Tune Counter-Light Sun");
                Undo.RecordObject(mainSun.transform, "Tune Counter-Light Sun Angle");
                mainSun.color = new Color(1.0f, 0.96f, 0.88f, 1f);
                mainSun.intensity = 1.45f;
                mainSun.shadows = LightShadows.Soft;
                mainSun.transform.rotation = Quaternion.Euler(36f, -158f, 0f);
            }
        }
    }
}

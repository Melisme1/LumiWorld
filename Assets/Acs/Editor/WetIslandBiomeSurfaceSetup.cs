using System.Collections.Generic;
using System.IO;
using LumiWorld.Acs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LumiWorld.EditorTools
{
    public static class WetIslandBiomeSurfaceSetup
    {
        private const string DraftGroundTexDir = "Assets/Acs/Art/Ground/Textures/WetIsland_Drafts";
        private const string WaterMaskDir = "Assets/Acs/Hex/WetIsland/WaterMasks";
        private const string MaterialsOutDir = "Assets/Acs/Art/Ground/Materials/WetIsland";
        private const string BiomesRootDir = "Assets/Acs/Hex/WetIsland/Biomes";
        private const string MeshesOutDir = "Assets/Acs/Hex/WetIsland/Biomes/Meshes";
        private const string PrefabsOutDir = "Assets/Acs/Hex/WetIsland/Biomes/Prefabs";

        private const string GroundShaderName = "LumiWorld/WetIslandGround";
        private const string UberWaterShaderPath = "Assets/Acs/UberStylizedWater/Shader/UberStylizedWater.shader";
        private const string UberWaterTemplateDir = "Assets/Acs/UberStylizedWater/Template Materials";
        private const string FallbackWaterShaderName = "LumiWorld/WetIslandWater";

        private const string WaterNormalTexPath = "Assets/Acs/HexTilesNature/Assets/VFX/Water/Textures/Water_Normal.png";
        private const string WaterNormalFallbackPath = "Assets/VinhWorkSpace/Textures/Water_Normal.png";
        private const string WaterCausticsTexPath = "Assets/Acs/HexTilesNature/Assets/VFX/Water/Textures/Water_WaterCaustics.png";
        private const string WaterNoiseTexPath = "Assets/VinhWorkSpace/Textures/Water_Noise.png";

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
            EnsureFolder(MaterialsOutDir);
            EnsureFolder(BiomesRootDir);
            EnsureFolder(MeshesOutDir);
            EnsureFolder(PrefabsOutDir);

            // Ensure URP Depth & Opaque Textures are enabled so real refraction & depth absorption work!
            EnsureURPDepthAndOpaqueTexturesEnabled();

            // 1. Configure texture import settings
            string texLily = $"{DraftGroundTexDir}/T_Ground_Lilywater_Clean_v2.png";
            string texStream = $"{DraftGroundTexDir}/T_Ground_Streamstone_Gray_v3.png";
            string texReed = $"{DraftGroundTexDir}/T_Ground_Reedmarsh_v1.png";
            string texRain = $"{DraftGroundTexDir}/T_Ground_Rainleaf_Seamless.png";

            ConfigureGroundTexture(texLily);
            ConfigureGroundTexture(texStream);
            ConfigureGroundTexture(texReed);
            ConfigureGroundTexture(texRain);

            string maskLily = $"{WaterMaskDir}/T_WaterMask_Lilywater_v4.png";
            string maskStream = $"{WaterMaskDir}/T_WaterMask_Streamstone_v3.png";
            string maskReed6 = $"{WaterMaskDir}/T_WaterMask_Reedmarsh_v3.png";
            string maskReed3 = $"{WaterMaskDir}/T_WaterMask_Reedmarsh_3Hex_v3.png";

            ConfigureWaterMaskTexture(maskLily);
            ConfigureWaterMaskTexture(maskStream);
            ConfigureWaterMaskTexture(maskReed6);
            ConfigureWaterMaskTexture(maskReed3);

            AssetDatabase.Refresh();

            Shader groundShader = Shader.Find(GroundShaderName);
            Shader waterShader = AssetDatabase.LoadAssetAtPath<Shader>(UberWaterShaderPath) ?? Shader.Find("UberStylizedWater");
            if (waterShader == null)
            {
                waterShader = Shader.Find(FallbackWaterShaderName);
            }
            if (groundShader == null || waterShader == null)
            {
                Debug.LogError($"[WetIslandSetup] Missing shaders: '{GroundShaderName}' or '{UberWaterShaderPath}'.");
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

            Texture2D tWaterNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalTexPath);
            if (tWaterNormal == null)
            {
                tWaterNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalFallbackPath);
            }
            Texture2D tWaterCaustics = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterCausticsTexPath);
            Texture2D tWaterNoise = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNoiseTexPath);

            // 2. Create Ground & Uber-Stylized Crystal-Clear Water Materials
            // A. LILYWATER: Fuller Pond (waterThreshold 0.21) + Deep Carved Basin + Warm Golden Sand Rim -> Deep Emerald-Azure Heart
            Material matGroundLily = CreateOrUpdateGroundMaterial(
                $"{MaterialsOutDir}/M_WetGround_Lilywater.mat", groundShader,
                tGroundLily, tMaskLily, tWaterCaustics,
                shoreStart: 0.03f, waterThreshold: 0.21f,
                shoreColor: new Color(0.93f, 0.78f, 0.44f, 1f),
                bedColor: new Color(0.88f, 0.73f, 0.41f, 1f),
                bedMidColor: new Color(0.58f, 0.56f, 0.36f, 1f),
                bedDeepColor: new Color(0.24f, 0.38f, 0.35f, 1f),
                sideColor: new Color(0.62f, 0.44f, 0.27f, 1f),
                sideBotColor: new Color(0.38f, 0.26f, 0.15f, 1f),
                wetDarken: 0.12f,
                causticColor: new Color(1.0f, 0.96f, 0.84f, 1f),
                causticStrength: 0.32f);

            Material matWaterLily = CreateOrUpdateWaterMaterial(
                $"{MaterialsOutDir}/M_WetWater_Lilywater.mat", waterShader,
                tMaskLily, tWaterNormal, tWaterCaustics, tWaterNoise,
                waterThreshold: 0.21f, edgeFeather: 0.024f,
                normalScale: 0.46f, normalStrength: 0.42f, refractionStrength: 0.024f,
                shallowColor: new Color(0.32f, 0.86f, 0.82f, 0.10f),
                midColor: new Color(0.14f, 0.58f, 0.74f, 0.56f),
                deepColor: new Color(0.02f, 0.32f, 0.48f, 0.95f),
                fresnelColor: new Color(0.46f, 0.76f, 0.98f, 1.0f),
                cloudColor: new Color(0.95f, 0.97f, 1.0f, 1.0f),
                fresnelPower: 2.5f, fresnelStrength: 0.54f, cloudStrength: 0.24f,
                foamColor: new Color(0.98f, 0.99f, 1.0f, 0.84f),
                foamBandWidth: 0.026f, objectFoamDist: 0.16f, causticStrength: 0.38f,
                flowParams: new Vector4(0.70f, 0.40f, 0.46f, 0.07f),
                sunSheen: 2.4f, sunGloss: 220f, sparkleThreshold: 0.955f);

            // B. STREAMSTONE: Clean River Stone Bed + Crystal-Clear Flowing Aqua Stream
            Material matGroundStream = CreateOrUpdateGroundMaterial(
                $"{MaterialsOutDir}/M_WetGround_Streamstone.mat", groundShader,
                tGroundStream, tMaskStream, tWaterCaustics,
                shoreStart: 0.06f, waterThreshold: 0.42f,
                shoreColor: new Color(0.58f, 0.57f, 0.52f, 1f),
                bedColor: new Color(0.55f, 0.54f, 0.49f, 1f),
                bedMidColor: new Color(0.48f, 0.49f, 0.45f, 1f),
                bedDeepColor: new Color(0.36f, 0.40f, 0.39f, 1f),
                sideColor: new Color(0.48f, 0.45f, 0.40f, 1f),
                sideBotColor: new Color(0.30f, 0.28f, 0.25f, 1f),
                wetDarken: 0.12f,
                causticColor: new Color(0.95f, 0.99f, 0.95f, 1f),
                causticStrength: 0.36f);

            Material matWaterStream = CreateOrUpdateWaterMaterial(
                $"{MaterialsOutDir}/M_WetWater_Streamstone.mat", waterShader,
                tMaskStream, tWaterNormal, tWaterCaustics, tWaterNoise,
                waterThreshold: 0.42f, edgeFeather: 0.026f,
                normalScale: 0.54f, normalStrength: 0.48f, refractionStrength: 0.026f,
                shallowColor: new Color(0.48f, 0.88f, 0.92f, 0.10f),
                midColor: new Color(0.24f, 0.70f, 0.82f, 0.40f),
                deepColor: new Color(0.14f, 0.48f, 0.68f, 0.66f),
                fresnelColor: new Color(0.52f, 0.82f, 0.98f, 1.0f),
                cloudColor: new Color(0.96f, 0.98f, 1.0f, 1.0f),
                fresnelPower: 2.6f, fresnelStrength: 0.48f, cloudStrength: 0.20f,
                foamColor: new Color(0.98f, 0.99f, 1.0f, 0.86f),
                foamBandWidth: 0.032f, objectFoamDist: 0.18f, causticStrength: 0.42f,
                flowParams: new Vector4(0.82f, -0.57f, 0.54f, 0.22f),
                sunSheen: 2.5f, sunGloss: 200f, sparkleThreshold: 0.952f);

            // C. REEDMARSH: Warm Golden-Peat Bed + Translucent Yellowish-Olive Marsh Puddles
            Material matGroundReed6 = CreateOrUpdateGroundMaterial(
                $"{MaterialsOutDir}/M_WetGround_Reedmarsh.mat", groundShader,
                tGroundReed, tMaskReed6, tWaterCaustics,
                shoreStart: 0.05f, waterThreshold: 0.42f,
                shoreColor: new Color(0.45f, 0.38f, 0.22f, 1f),
                bedColor: new Color(0.42f, 0.36f, 0.20f, 1f),
                bedMidColor: new Color(0.35f, 0.31f, 0.17f, 1f),
                bedDeepColor: new Color(0.25f, 0.24f, 0.13f, 1f),
                sideColor: new Color(0.34f, 0.27f, 0.18f, 1f),
                sideBotColor: new Color(0.21f, 0.16f, 0.11f, 1f),
                wetDarken: 0.12f,
                causticColor: new Color(0.96f, 0.90f, 0.56f, 1f),
                causticStrength: 0.30f);

            Material matWaterReed6 = CreateOrUpdateWaterMaterial(
                $"{MaterialsOutDir}/M_WetWater_Reedmarsh.mat", waterShader,
                tMaskReed6, tWaterNormal, tWaterCaustics, tWaterNoise,
                waterThreshold: 0.42f, edgeFeather: 0.028f,
                normalScale: 0.50f, normalStrength: 0.36f, refractionStrength: 0.020f,
                shallowColor: new Color(0.68f, 0.62f, 0.22f, 0.42f),
                midColor: new Color(0.46f, 0.48f, 0.16f, 0.68f),
                deepColor: new Color(0.28f, 0.34f, 0.10f, 0.92f),
                fresnelColor: new Color(0.72f, 0.74f, 0.48f, 1.0f),
                cloudColor: new Color(0.94f, 0.92f, 0.78f, 1.0f),
                fresnelPower: 2.4f, fresnelStrength: 0.46f, cloudStrength: 0.18f,
                foamColor: new Color(0.88f, 0.82f, 0.52f, 0.78f),
                foamBandWidth: 0.024f, objectFoamDist: 0.14f, causticStrength: 0.30f,
                flowParams: new Vector4(0.60f, 0.80f, 0.50f, 0.06f),
                sunSheen: 2.1f, sunGloss: 210f, sparkleThreshold: 0.958f);

            Material matGroundReed3 = CreateOrUpdateGroundMaterial(
                $"{MaterialsOutDir}/M_WetGround_Reedmarsh_3Hex.mat", groundShader,
                tGroundReed, tMaskReed3, tWaterCaustics,
                shoreStart: 0.05f, waterThreshold: 0.42f,
                shoreColor: new Color(0.45f, 0.38f, 0.22f, 1f),
                bedColor: new Color(0.42f, 0.36f, 0.20f, 1f),
                bedMidColor: new Color(0.35f, 0.31f, 0.17f, 1f),
                bedDeepColor: new Color(0.25f, 0.24f, 0.13f, 1f),
                sideColor: new Color(0.34f, 0.27f, 0.18f, 1f),
                sideBotColor: new Color(0.21f, 0.16f, 0.11f, 1f),
                wetDarken: 0.12f,
                causticColor: new Color(0.96f, 0.90f, 0.56f, 1f),
                causticStrength: 0.30f);

            Material matWaterReed3 = CreateOrUpdateWaterMaterial(
                $"{MaterialsOutDir}/M_WetWater_Reedmarsh_3Hex.mat", waterShader,
                tMaskReed3, tWaterNormal, tWaterCaustics, tWaterNoise,
                waterThreshold: 0.42f, edgeFeather: 0.028f,
                normalScale: 0.50f, normalStrength: 0.36f, refractionStrength: 0.020f,
                shallowColor: new Color(0.68f, 0.62f, 0.22f, 0.42f),
                midColor: new Color(0.46f, 0.48f, 0.16f, 0.68f),
                deepColor: new Color(0.28f, 0.34f, 0.10f, 0.92f),
                fresnelColor: new Color(0.72f, 0.74f, 0.48f, 1.0f),
                cloudColor: new Color(0.94f, 0.92f, 0.78f, 1.0f),
                fresnelPower: 2.4f, fresnelStrength: 0.46f, cloudStrength: 0.18f,
                foamColor: new Color(0.88f, 0.82f, 0.52f, 0.78f),
                foamBandWidth: 0.024f, objectFoamDist: 0.14f, causticStrength: 0.30f,
                flowParams: new Vector4(0.60f, 0.80f, 0.50f, 0.06f),
                sunSheen: 2.1f, sunGloss: 210f, sparkleThreshold: 0.958f);

            // D. RAINLEAF
            Material matGroundRain = CreateOrUpdateGroundMaterial(
                $"{MaterialsOutDir}/M_WetGround_Rainleaf.mat", groundShader,
                tGroundRain, null, tWaterCaustics,
                shoreStart: 0.90f, waterThreshold: 0.95f,
                shoreColor: new Color(0.28f, 0.22f, 0.15f, 1f),
                bedColor: new Color(0.28f, 0.22f, 0.15f, 1f),
                bedMidColor: new Color(0.28f, 0.22f, 0.15f, 1f),
                bedDeepColor: new Color(0.28f, 0.22f, 0.15f, 1f),
                sideColor: new Color(0.38f, 0.27f, 0.17f, 1f),
                sideBotColor: new Color(0.24f, 0.16f, 0.10f, 1f),
                wetDarken: 0.0f,
                causticColor: Color.black,
                causticStrength: 0.0f);

            // 3. Build 3, 6, and 10 Hex Cluster Meshes & Prefabs for all 4 Biomes
            int[] sizes = new[] { 3, 6, 10 };
            foreach (int size in sizes)
            {
                List<HexCoordinates> hexes = WetIslandClusterMeshBuilder.GetUprightTriangleHexes(size);

                // A. Lilywater (fuller pond area + genuinely deep 3D basin)
                var lilySettings = WetIslandClusterSurfaceSettings.CreateDefault();
                lilySettings.shoreStartMask = 0.03f;
                lilySettings.waterThresholdMask = 0.21f;
                lilySettings.maxBasinDepth = 0.72f;
                lilySettings.waterSurfaceDrop = 0.055f;

                BuildBiomeClusterPrefab(
                    biomeName: "Lilywater",
                    clusterSize: size,
                    hexes: hexes,
                    waterMask: tMaskLily,
                    settings: lilySettings,
                    groundMat: matGroundLily,
                    waterMat: matWaterLily,
                    rockMat: null,
                    includeStreamstoneFeatures: false);

                // B. Streamstone
                var streamSettings = WetIslandClusterSurfaceSettings.CreateDefault();
                streamSettings.shoreStartMask = 0.06f;
                streamSettings.waterThresholdMask = 0.42f;
                streamSettings.maxBasinDepth = 0.34f;
                streamSettings.waterSurfaceDrop = 0.055f;
                streamSettings.streamSlopeDrop = 0.08f;
                streamSettings.streamSlopeDir = new Vector2(0.82f, -0.57f);

                BuildBiomeClusterPrefab(
                    biomeName: "Streamstone",
                    clusterSize: size,
                    hexes: hexes,
                    waterMask: tMaskStream,
                    settings: streamSettings,
                    groundMat: matGroundStream,
                    waterMat: matWaterStream,
                    rockMat: null,
                    includeStreamstoneFeatures: false);

                // C. Reedmarsh
                var reedSettings = WetIslandClusterSurfaceSettings.CreateDefault();
                reedSettings.shoreStartMask = 0.05f;
                reedSettings.waterThresholdMask = 0.38f;
                reedSettings.maxBasinDepth = 0.24f;
                reedSettings.waterSurfaceDrop = 0.045f;

                BuildBiomeClusterPrefab(
                    biomeName: "Reedmarsh",
                    clusterSize: size,
                    hexes: hexes,
                    waterMask: size == 3 ? tMaskReed3 : tMaskReed6,
                    settings: reedSettings,
                    groundMat: size == 3 ? matGroundReed3 : matGroundReed6,
                    waterMat: size == 3 ? matWaterReed3 : matWaterReed6,
                    rockMat: null,
                    includeStreamstoneFeatures: false);

                // D. Rainleaf
                var rainSettings = WetIslandClusterSurfaceSettings.CreateDefault();
                rainSettings.maxBasinDepth = 0f;

                BuildBiomeClusterPrefab(
                    biomeName: "Rainleaf",
                    clusterSize: size,
                    hexes: hexes,
                    waterMask: null,
                    settings: rainSettings,
                    groundMat: matGroundRain,
                    waterMat: null,
                    rockMat: null,
                    includeStreamstoneFeatures: false);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WetIslandSetup] Built Option A (Uber-Stylized Crystal-Clear Water + Refraction + Caustics + Sun Sparkles) successfully!");
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

                    // Spawn temporary raycasted submerged rocks & shadow pillars (Uber-Stylized-Water demo style!)
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

                // Align SceneView camera at a 28-degree grazing counter-light angle facing Lilywater & Streamstone
                // so the sun sparkles, sky reflection, and crystal-clear sandy bottom are immediately visible!
                Vector3 focusTarget = showcaseRoot.transform.position + new Vector3(3.2f, 0.85f, -0.3f);
                Quaternion counterLightCamRot = Quaternion.Euler(29f, 22f, 0f);
                sv.LookAt(focusTarget, counterLightCamRot, 6.2f);
                sv.Repaint();
            }

            Debug.Log($"[WetIslandSetup] Spawned '{rootName}' with Counter-Light Sun, Raycasted Submerged Rocks & Shadow Pillars! (Use 'LumiWorld -> WetIsland -> 3. Toggle Temp Raycast Rocks & Shadow Pillars' to hide/show test props).");
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

            // 1. Half-submerged shoreline boulder + mid-water submerged rock (demonstrates refraction, depth tint & intersection foam!)
            if (biomeName == "Lilywater")
            {
                PlaceRaycastRock(propGroup.transform, basinCol, rockPrefab1, new Vector3(-0.95f, 0f, -0.15f), new Vector3(0.42f, 0.38f, 0.42f), 25f, sinkOffset: -0.04f);
                PlaceRaycastRock(propGroup.transform, basinCol, stoneBig, new Vector3(-0.35f, 0f, -0.55f), new Vector3(0.32f, 0.24f, 0.32f), 110f, sinkOffset: -0.05f);
                PlaceRaycastRock(propGroup.transform, basinCol, stoneSmall, new Vector3(0.75f, 0f, -0.45f), new Vector3(0.28f, 0.22f, 0.28f), -40f, sinkOffset: -0.02f);

                // Slanted shadow-casting monoliths on the back bank (just like the Uber-Stylized-Water screenshot!)
                CreateShadowCasterPillar(propGroup.transform, basinCol, new Vector3(0.15f, 0f, 1.35f), new Vector3(0.28f, 1.45f, 0.28f), Quaternion.Euler(16f, -20f, 14f));
                CreateShadowCasterPillar(propGroup.transform, basinCol, new Vector3(1.10f, 0f, 0.85f), new Vector3(0.24f, 1.15f, 0.24f), Quaternion.Euler(12f, -35f, -18f));
            }
            else if (biomeName == "Streamstone")
            {
                // Left-flank headwater boulders + stepping stones in the crystal stream
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
            // Sunny blue sky + warm golden sand bounce ambient (Uber-Stylized-Water style)
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
                // Counter-light direction: shines from back-right toward the front-left camera so sun sparkles & pillar shadows fall across the pond!
                mainSun.transform.rotation = Quaternion.Euler(36f, -158f, 0f);
            }
        }

        private static void EnsureURPDepthAndOpaqueTexturesEnabled()
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

        private static void BuildBiomeClusterPrefab(
            string biomeName,
            int clusterSize,
            IReadOnlyList<HexCoordinates> hexes,
            Texture2D waterMask,
            WetIslandClusterSurfaceSettings settings,
            Material groundMat,
            Material waterMat,
            Material rockMat,
            bool includeStreamstoneFeatures)
        {
            string tag = $"{biomeName}_{clusterSize}Hex";
            string groundMeshPath = $"{MeshesOutDir}/Mesh_Ground_{tag}.asset";
            Mesh groundMesh = WetIslandClusterMeshBuilder.BuildGroundClusterMesh($"Mesh_Ground_{tag}", hexes, waterMask, settings);
            groundMesh = SaveOrReplaceMeshAsset(groundMesh, groundMeshPath);

            Mesh waterMesh = null;
            if (waterMask != null && waterMat != null)
            {
                string waterMeshPath = $"{MeshesOutDir}/Mesh_Water_{tag}.asset";
                waterMesh = WetIslandClusterMeshBuilder.BuildWaterSurfaceMesh($"Mesh_Water_{tag}", hexes, waterMask, settings);
                waterMesh = SaveOrReplaceMeshAsset(waterMesh, waterMeshPath);
            }

            GameObject root = new GameObject($"Biome_{tag}");
            try
            {
                // 1. Ground Cluster Mesh + Collider
                GameObject groundGo = new GameObject("Ground_Basin");
                groundGo.transform.SetParent(root.transform, false);
                MeshFilter gMf = groundGo.AddComponent<MeshFilter>();
                gMf.sharedMesh = groundMesh;
                MeshRenderer gMr = groundGo.AddComponent<MeshRenderer>();
                gMr.sharedMaterial = groundMat;
                MeshCollider gCol = groundGo.AddComponent<MeshCollider>();
                gCol.sharedMesh = groundMesh;

                // 2. Water Surface Mesh
                if (waterMesh != null && waterMat != null)
                {
                    GameObject waterGo = new GameObject("Water_Surface");
                    waterGo.transform.SetParent(root.transform, false);
                    MeshFilter wMf = waterGo.AddComponent<MeshFilter>();
                    wMf.sharedMesh = waterMesh;
                    MeshRenderer wMr = waterGo.AddComponent<MeshRenderer>();
                    wMr.sharedMaterial = waterMat;
                    wMr.shadowCastingMode = ShadowCastingMode.Off;
                    wMr.receiveShadows = true;
                }

                string prefabPath = $"{PrefabsOutDir}/Biome_{tag}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh SaveOrReplaceMeshAsset(Mesh newMesh, string assetPath)
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

        private static Material CreateOrUpdateGroundMaterial(
            string matPath,
            Shader shader,
            Texture2D topTex,
            Texture2D waterMask,
            Texture2D causticsTex,
            float shoreStart,
            float waterThreshold,
            Color shoreColor,
            Color bedColor,
            Color bedMidColor,
            Color bedDeepColor,
            Color sideColor,
            Color sideBotColor,
            float wetDarken,
            Color causticColor,
            float causticStrength)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetTexture("_TopTex", topTex);
            mat.SetFloat("_TopTiling", 0.55f);
            mat.SetColor("_TopTint", Color.white);
            mat.SetColor("_SideColor", sideColor);
            mat.SetColor("_SideBottomColor", sideBotColor);
            mat.SetFloat("_BevelEdge1", 0.45f);
            mat.SetFloat("_BevelEdge2", 0.86f);
            mat.SetFloat("_SeamCreaseTint", 0.22f);
            mat.SetFloat("_BevelRimHighlight", 0.08f);

            mat.SetTexture("_WaterMask", waterMask);
            mat.SetFloat("_ShoreStart", shoreStart);
            mat.SetFloat("_WaterThreshold", waterThreshold);
            mat.SetColor("_ShoreColor", shoreColor);
            mat.SetColor("_BedColor", bedColor);
            mat.SetColor("_BedMidColor", bedMidColor);
            mat.SetColor("_BedDeepColor", bedDeepColor);
            mat.SetFloat("_ShoreGrainStrength", 0.065f);
            mat.SetFloat("_WetnessDarken", wetDarken);

            mat.SetTexture("_CausticsTex", causticsTex);
            mat.SetColor("_BedCausticColor", causticColor);
            mat.SetFloat("_BedCausticStrength", causticStrength);
            mat.SetFloat("_BedCausticScale", 0.65f);
            mat.SetFloat("_BedCausticSpeed", 0.08f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateWaterMaterial(
            string matPath,
            Shader shader,
            Texture2D waterMask,
            Texture2D normalTex,
            Texture2D causticsTex,
            Texture2D noiseTex,
            float waterThreshold,
            float edgeFeather,
            float normalScale,
            float normalStrength,
            float refractionStrength,
            Color shallowColor,
            Color midColor,
            Color deepColor,
            Color fresnelColor,
            Color cloudColor,
            float fresnelPower,
            float fresnelStrength,
            float cloudStrength,
            Color foamColor,
            float foamBandWidth,
            float objectFoamDist,
            float causticStrength,
            Vector4 flowParams,
            float sunSheen,
            float sunGloss,
            float sparkleThreshold)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            // Direct integration with MatrixRex/Uber-Stylized-Water shader
            Material uberClearTemplate = AssetDatabase.LoadAssetAtPath<Material>($"{UberWaterTemplateDir}/UWa-Template-Clear.mat");
            if (uberClearTemplate != null && shader.name.Contains("UberStylizedWater"))
            {
                mat.CopyPropertiesFromMaterial(uberClearTemplate);
                mat.shaderKeywords = uberClearTemplate.shaderKeywords;

                bool isStream = matPath.Contains("Streamstone");
                bool isReed = matPath.Contains("Reedmarsh");

                // Calibrate depth & scale for 3/6/10-hex cluster basins
                float targetWaterDepth = isReed ? 0.09f : (isStream ? 0.24f : 0.20f);
                mat.SetFloat("_Water_Depth", targetWaterDepth);
                mat.SetColor("_Color_Shallow", new Color(shallowColor.r, shallowColor.g, shallowColor.b, isReed ? 0.42f : 0.06f));
                mat.SetColor("_Color_Deep", new Color(deepColor.r, deepColor.g, deepColor.b, isReed ? 0.92f : 0.94f));

                mat.SetFloat("_ShoreFade", 1f);
                mat.SetFloat("_ShoreFade_Smoothness", isReed ? 0.12f : 0.18f);

                if (mat.HasProperty("_SurfFoam_Color"))
                {
                    mat.SetColor("_SurfFoam_Color", isReed ? new Color(0.88f, 0.82f, 0.52f, 0.85f) : Color.white);
                }
                if (mat.HasProperty("_SL_Color"))
                {
                    mat.SetColor("_SL_Color", isReed ? new Color(0.86f, 0.80f, 0.52f, 0.78f) : Color.white);
                }
                if (mat.HasProperty("_ENABLE_UNDERWATERLAYER"))
                {
                    mat.SetFloat("_ENABLE_UNDERWATERLAYER", 1f);
                    mat.EnableKeyword("_ENABLE_UNDERWATERLAYER");
                    mat.SetFloat("_UnderWater_Depth", isReed ? -0.22f : -0.45f);
                    mat.SetColor("_Underwater_Color", isReed
                        ? new Color(0.36f, 0.30f, 0.10f, 0.48f)
                        : new Color(0.03f, 0.24f, 0.36f, 0.52f));
                }

                mat.SetFloat("_ENABLENORMAL", 1f);
                mat.EnableKeyword("_ENABLENORMAL");
                mat.SetFloat("_Normal_Scale", isStream ? 3.2f : 2.8f);
                mat.SetFloat("_Normal_Strength", isStream ? 0.38f : 0.32f);
                mat.SetFloat("_Normal_Pan", isStream ? 0.65f : 0.35f);

                mat.SetFloat("_ENABLEREFRACTION", 1f);
                mat.EnableKeyword("_ENABLEREFRACTION");
                mat.SetFloat("_Refraction_Strength", 0.18f);

                mat.SetFloat("_ENABLECAUSTICS", 1f);
                mat.EnableKeyword("_ENABLECAUSTICS");
                mat.SetFloat("_Caustics_Depth", -0.75f);
                mat.SetFloat("_Caustics_Scale", 1.8f);
                mat.SetFloat("_Caustics_Strength", isReed ? 1.8f : 2.5f);
                mat.SetFloat("_Caustics_Start", 20f);
                mat.SetFloat("_Caustics_Fade", 15f);

                mat.SetFloat("_Enable_Intersection", 1f);
                mat.SetColor("_InterSec_Color", foamColor);
                mat.SetFloat("_InterSec_Width", isReed ? 0.14f : 0.22f);
                mat.SetFloat("_InterSec_Foam_Scale", 4.5f);

                mat.SetFloat("_SL_WaterDepth", isReed ? 0.14f : 0.22f);

                mat.SetFloat("_ENABLEWAVE", 0f);
                mat.DisableKeyword("_ENABLEWAVE");

                EditorUtility.SetDirty(mat);
                return mat;
            }

            mat.SetTexture("_WaterMask", waterMask);
            mat.SetFloat("_WaterThreshold", waterThreshold);
            mat.SetFloat("_EdgeFeather", edgeFeather);

            mat.SetTexture("_NormalMap", normalTex);
            mat.SetFloat("_NormalScale", normalScale);
            mat.SetFloat("_NormalStrength", normalStrength);
            mat.SetFloat("_RefractionStrength", refractionStrength);

            mat.SetColor("_ShallowColor", shallowColor);
            mat.SetColor("_MidColor", midColor);
            mat.SetColor("_DeepColor", deepColor);
            mat.SetFloat("_DepthCurve", 1.35f);

            mat.SetColor("_FresnelColor", fresnelColor);
            mat.SetColor("_CloudColor", cloudColor);
            mat.SetFloat("_FresnelPower", fresnelPower);
            mat.SetFloat("_FresnelStrength", fresnelStrength);
            mat.SetFloat("_CloudReflectionStrength", cloudStrength);

            mat.SetTexture("_CausticsTex", causticsTex);
            mat.SetFloat("_CausticStrength", causticStrength);
            mat.SetFloat("_CausticScale", 0.62f);

            mat.SetTexture("_NoiseTex", noiseTex);
            mat.SetColor("_FoamColor", foamColor);
            mat.SetFloat("_FoamBandWidth", foamBandWidth);
            mat.SetFloat("_ObjectFoamDistance", objectFoamDist);
            mat.SetVector("_FlowParams", flowParams);
            mat.SetFloat("_StreamRibbonStrength", 0.0f);

            mat.SetFloat("_SunSheen", sunSheen);
            mat.SetFloat("_SunGloss", sunGloss);
            mat.SetFloat("_SparkleThreshold", sparkleThreshold);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ApplyUberWaterPresetToBiomes(string templateFileName, bool scaleForHexBasin)
        {
            string templatePath = $"{UberWaterTemplateDir}/{templateFileName}";
            Material templateMat = AssetDatabase.LoadAssetAtPath<Material>(templatePath);
            if (templateMat == null)
            {
                Debug.LogError($"[WetIslandSetup] Could not find template material at '{templatePath}'.");
                return;
            }

            string[] waterMatPaths = new[]
            {
                $"{MaterialsOutDir}/M_WetWater_Lilywater.mat",
                $"{MaterialsOutDir}/M_WetWater_Streamstone.mat",
                $"{MaterialsOutDir}/M_WetWater_Reedmarsh.mat",
                $"{MaterialsOutDir}/M_WetWater_Reedmarsh_3Hex.mat"
            };

            foreach (string matPath in waterMatPaths)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) continue;

                Undo.RecordObject(mat, $"Apply {templateFileName}");
                mat.shader = templateMat.shader;
                mat.CopyPropertiesFromMaterial(templateMat);
                mat.shaderKeywords = templateMat.shaderKeywords;

                if (scaleForHexBasin)
                {
                    bool isReed = matPath.Contains("Reedmarsh");
                    bool isStream = matPath.Contains("Streamstone");
                    float depthScale = isReed ? 0.09f : (isStream ? 0.24f : 0.20f);
                    if (mat.HasProperty("_Water_Depth")) mat.SetFloat("_Water_Depth", depthScale);
                    if (mat.HasProperty("_ShoreFade")) mat.SetFloat("_ShoreFade", 1f);
                    if (mat.HasProperty("_ShoreFade_Smoothness")) mat.SetFloat("_ShoreFade_Smoothness", isReed ? 0.12f : 0.18f);
                    if (mat.HasProperty("_InterSec_Width"))
                    {
                        mat.SetFloat("_InterSec_Width", Mathf.Min(mat.GetFloat("_InterSec_Width"), isReed ? 0.14f : 0.22f));
                    }
                    if (mat.HasProperty("_SL_WaterDepth"))
                    {
                        mat.SetFloat("_SL_WaterDepth", Mathf.Min(mat.GetFloat("_SL_WaterDepth"), isReed ? 0.14f : 0.22f));
                    }
                    if (mat.HasProperty("_Caustics_Depth")) mat.SetFloat("_Caustics_Depth", -0.75f);
                    if (mat.HasProperty("_Caustics_Start")) mat.SetFloat("_Caustics_Start", 20f);
                    if (mat.HasProperty("_Caustics_Fade")) mat.SetFloat("_Caustics_Fade", 15f);

                    // Preserve Reedmarsh's signature warm yellowish-olive marsh water identity when switching presets!
                    if (isReed)
                    {
                        if (mat.HasProperty("_Color_Shallow")) mat.SetColor("_Color_Shallow", new Color(0.68f, 0.62f, 0.22f, 0.42f));
                        if (mat.HasProperty("_Color_Deep")) mat.SetColor("_Color_Deep", new Color(0.28f, 0.34f, 0.10f, 0.92f));
                        if (mat.HasProperty("_SurfFoam_Color")) mat.SetColor("_SurfFoam_Color", new Color(0.88f, 0.82f, 0.52f, 0.85f));
                        if (mat.HasProperty("_InterSec_Color")) mat.SetColor("_InterSec_Color", new Color(0.88f, 0.82f, 0.52f, 0.78f));
                        if (mat.HasProperty("_SL_Color")) mat.SetColor("_SL_Color", new Color(0.86f, 0.80f, 0.52f, 0.78f));
                        if (mat.HasProperty("_Underwater_Color")) mat.SetColor("_Underwater_Color", new Color(0.36f, 0.30f, 0.10f, 0.48f));
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
            Debug.Log($"[WetIslandSetup] Applied UberStylizedWater preset '{templateFileName}' to all Wet Island water materials!");
        }

        private static void ConfigureGroundTexture(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (!importer.sRGBTexture) { importer.sRGBTexture = true; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; dirty = true; }
            if (importer.maxTextureSize != 1024) { importer.maxTextureSize = 1024; dirty = true; }

            if (dirty) importer.SaveAndReimport();
        }

        private static void ConfigureWaterMaskTexture(string assetPath)
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

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
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

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using LumiWorld.Acs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LumiWorld.EditorTools
{
    /// <summary>
    /// Builds and showcases the 4 Wet Island 1-Hex single Nature tile prefabs:
    ///   - Lilywater (1A): pocket pond + floating lotus cluster + pebble bank + iris
    ///   - Reedmarsh (2A): shallow off-center marsh puddle + golden reed + cattail + marsh collar
    ///   - Streamstone (3B): flat mossy gravel ground + stacked mossy boulders & river cobbles + ferns (dry until formed)
    ///   - Rainleaf (4A): dark rainforest leaf-litter floor + young banana/palm + broadleaf understory
    /// Reuses HexBiomeSurfaceMeshBuilder, HexBiomeSurfaceBuilder, and BiomeLayoutGenerator cleanly.
    /// </summary>
    public static class WetIslandSingleHexSetup
    {
        private const string GroundTexDir = "Assets/Acs/Art/Ground/Textures/WetIsland";
        private const string WaterMaskDir = "Assets/Acs/Hex/WetIsland/WaterMasks";
        private const string WaterMaterialsDir = "Assets/Acs/Art/Ground/Materials/WetIsland";
        private const string BiomesMaterialsDir = "Assets/Acs/Hex/WetIsland/Biomes/Materials";
        private const string MeshesOutDir = "Assets/Acs/Hex/WetIsland/Biomes/Meshes";
        private const string ProfilesOutDir = "Assets/Acs/Hex/WetIsland/Biomes/Profiles";
        private const string PrefabsOutDir = "Assets/Acs/Hex/WetIsland/Biomes/Prefabs";
        private const string WaterCausticsTexPath = "Assets/Acs/HexTilesNature/Assets/VFX/Water/Textures/Water_WaterCaustics.png";

        private static readonly string[] BiomeOrder = { "Lilywater", "Reedmarsh", "Streamstone", "Rainleaf" };

        [MenuItem("LumiWorld/WetIsland/8. Build 4 Single-Hex Prefabs (1A Lilywater, 2A Reedmarsh, 3B Streamstone, 4A Rainleaf)")]
        public static void BuildAllSingleHexPrefabs()
        {
            HexBiomeSurfaceBuilder.EnsureFolder(BiomesMaterialsDir);
            HexBiomeSurfaceBuilder.EnsureFolder(WaterMaterialsDir);
            HexBiomeSurfaceBuilder.EnsureFolder(MeshesOutDir);
            HexBiomeSurfaceBuilder.EnsureFolder(ProfilesOutDir);
            HexBiomeSurfaceBuilder.EnsureFolder(PrefabsOutDir);
            HexBiomeSurfaceBuilder.EnsureURPDepthAndOpaqueTexturesEnabled();

            string lilyMaskPath = $"{WaterMaskDir}/T_Diorama_Lilywater1.png";
            string reedMaskPath = $"{WaterMaskDir}/T_Diorama_Reedmarsh1.png";
            HexBiomeSurfaceBuilder.ConfigureWaterMaskTexture(lilyMaskPath);
            HexBiomeSurfaceBuilder.ConfigureWaterMaskTexture(reedMaskPath);
            AssetDatabase.Refresh();

            Shader groundShader = HexBiomeSurfaceBuilder.FindGroundShader();
            if (groundShader == null)
            {
                Debug.LogError($"[WetIslandSingleHex] Missing shader '{HexBiomeSurfaceBuilder.GroundShaderName}'.");
                return;
            }

            Texture2D tMaskLily = AssetDatabase.LoadAssetAtPath<Texture2D>(lilyMaskPath);
            Texture2D tMaskReed = AssetDatabase.LoadAssetAtPath<Texture2D>(reedMaskPath);
            if (tMaskLily == null || tMaskReed == null)
            {
                Debug.LogError("[WetIslandSingleHex] Missing 1-Hex water masks T_Diorama_Lilywater1.png or T_Diorama_Reedmarsh1.png.");
                return;
            }

            Texture2D tGroundLily = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundTexDir}/T_Ground_Lilywater_Clean_v2.png");
            Texture2D tGroundReed = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundTexDir}/T_Ground_Reedmarsh_v1.png");
            Texture2D tGroundStream = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundTexDir}/T_Ground_Streamstone_Gray_v3.png");
            Texture2D tCaustics = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterCausticsTexPath);

            // 1. Ground materials (exact palette & texture parity with 3/6/10 diorama presets)
            Material matGroundLily = HexBiomeSurfaceBuilder.CreateOrUpdateGroundMaterial(groundShader, new HexGroundMaterialSpec
            {
                materialPath = $"{BiomesMaterialsDir}/M_DioramaGround_Lilywater1.mat",
                topTex = tGroundLily,
                waterMask = tMaskLily,
                causticsTex = tCaustics,
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

            Material matGroundReed = HexBiomeSurfaceBuilder.CreateOrUpdateGroundMaterial(groundShader, new HexGroundMaterialSpec
            {
                materialPath = $"{BiomesMaterialsDir}/M_DioramaGround_Reedmarsh1.mat",
                topTex = tGroundReed,
                waterMask = tMaskReed,
                causticsTex = tCaustics,
                shoreStart = 0.05f,
                waterThreshold = 0.38f,
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

            Material matGroundStream = HexBiomeSurfaceBuilder.CreateOrUpdateGroundMaterial(groundShader, new HexGroundMaterialSpec
            {
                materialPath = $"{BiomesMaterialsDir}/M_DioramaGround_Streamstone1.mat",
                topTex = tGroundStream,
                waterMask = null,
                causticsTex = tCaustics,
                shoreStart = 0.90f,
                waterThreshold = 0.95f,
                shoreColor = new Color(0.58f, 0.57f, 0.52f, 1f),
                bedColor = new Color(0.55f, 0.54f, 0.49f, 1f),
                bedMidColor = new Color(0.48f, 0.49f, 0.45f, 1f),
                bedDeepColor = new Color(0.36f, 0.40f, 0.39f, 1f),
                sideColor = new Color(0.48f, 0.45f, 0.40f, 1f),
                sideBotColor = new Color(0.30f, 0.28f, 0.25f, 1f),
                wetDarken = 0.0f,
                causticColor = Color.black,
                causticStrength = 0.0f
            });

            Material matGroundRain = AssetDatabase.LoadAssetAtPath<Material>($"{WaterMaterialsDir}/M_WetGround_Rainleaf.mat");
            if (matGroundRain == null)
            {
                Texture2D tGroundRain = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundTexDir}/T_Ground_Rainleaf_Seamless.png");
                matGroundRain = HexBiomeSurfaceBuilder.CreateOrUpdateGroundMaterial(groundShader, new HexGroundMaterialSpec
                {
                    materialPath = $"{WaterMaterialsDir}/M_WetGround_Rainleaf.mat",
                    topTex = tGroundRain,
                    waterMask = null,
                    causticsTex = tCaustics,
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
            }

            // 2. Water materials for 1A & 2A cloned from the exact 3Hex UberStylizedWater materials with shallower pocket depth
            Material matWaterLily = CloneAndCalibrateWaterMaterial(
                $"{WaterMaterialsDir}/M_WetWater_Lilywater_3Hex.mat",
                $"{WaterMaterialsDir}/M_WetWater_Lilywater_1Hex.mat",
                waterDepth: 0.40f, shoreSmoothness: 0.14f, shorelineDepth: 0.15f);

            Material matWaterReed = CloneAndCalibrateWaterMaterial(
                $"{WaterMaterialsDir}/M_WetWater_Reedmarsh_3Hex.mat",
                $"{WaterMaterialsDir}/M_WetWater_Reedmarsh_1Hex.mat",
                waterDepth: 0.24f, shoreSmoothness: 0.12f, shorelineDepth: 0.11f);

            Dictionary<string, GameObject> catalog = BuildArtPrefabCatalog();

            // 3. Build all 4 profiles + meshes + populated 1-Hex prefabs
            BuildSingleBiome("Lilywater", seed: 101,
                waterMask: tMaskLily,
                surfaceSettings: CreateLilywater1SurfaceSettings(),
                groundMaterial: matGroundLily,
                waterMaterial: matWaterLily,
                designNotes: "1A Single Hex: Sunken pocket pond, focal pink lotus cluster, pebble bank, water iris & grass collar.",
                layers: CreateLilywater1Layers(catalog));

            BuildSingleBiome("Reedmarsh", seed: 202,
                waterMask: tMaskReed,
                surfaceSettings: CreateReedmarsh1SurfaceSettings(),
                groundMaterial: matGroundReed,
                waterMaterial: matWaterReed,
                designNotes: "2A Single Hex: Shallow off-center marsh puddle, golden reed & cattail clump, half-submerged stone & marsh grass.",
                layers: CreateReedmarsh1Layers(catalog));

            BuildSingleBiome("Streamstone", seed: 303,
                waterMask: null,
                surfaceSettings: CreateDry1SurfaceSettings(),
                groundMaterial: matGroundStream,
                waterMaterial: null,
                designNotes: "3B Single Hex: Flat mossy-gravel ground (no water until formed), focal mossy boulder, stepped river cobbles, ferns & wildflowers.",
                layers: CreateStreamstone1Layers(catalog));

            BuildSingleBiome("Rainleaf", seed: 404,
                waterMask: null,
                surfaceSettings: CreateDry1SurfaceSettings(),
                groundMaterial: matGroundRain,
                waterMaterial: null,
                designNotes: "4A Single Hex: Dark rainforest floor, young tilted banana/palm sapling, broadleaf understory, moss stone & low grass.",
                layers: CreateRainleaf1Layers(catalog));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WetIslandSingleHex] Built all 4 Wet Island 1-Hex single Nature prefabs (1A, 2A, 3B, 4A) successfully!");
        }

        [MenuItem("LumiWorld/WetIsland/9. Spawn 1-Hex vs 3-Hex Showcase (4 Biomes + Seed Variants)")]
        public static void SpawnSingleHexVs3HexShowcase()
        {
            bool missingAny = false;
            foreach (string biome in BiomeOrder)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOutDir}/Biome_{biome}_1Hex.prefab") == null)
                {
                    missingAny = true;
                    break;
                }
            }
            if (missingAny)
            {
                BuildAllSingleHexPrefabs();
            }

            const string rootName = "WetIsland_1Hex_vs_3Hex_Showcase";
            GameObject existing = GameObject.Find(rootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "Spawn 1-Hex vs 3-Hex Showcase");
            root.transform.position = new Vector3(0f, 0f, -14f);

            const float colStep = 5.2f;
            float startX = -((BiomeOrder.Length - 1) * colStep) * 0.5f;

            for (int i = 0; i < BiomeOrder.Length; i++)
            {
                string biome = BiomeOrder[i];
                float x = startX + i * colStep;

                // Row 1 (Front): Default baked 1-Hex single Nature tile
                GameObject p1 = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOutDir}/Biome_{biome}_1Hex.prefab");
                if (p1 != null)
                {
                    GameObject inst1 = (GameObject)PrefabUtility.InstantiatePrefab(p1, root.transform);
                    inst1.name = $"Biome_{biome}_1Hex_SeedA";
                    inst1.transform.localPosition = new Vector3(x, 0f, 0f);

                    // Row 2 (Middle): Alternate seed (+1) 1-Hex variant to inspect variety
                    GameObject instVar = (GameObject)PrefabUtility.InstantiatePrefab(p1, root.transform);
                    instVar.name = $"Biome_{biome}_1Hex_SeedB";
                    instVar.transform.localPosition = new Vector3(x, 0f, 3.0f);
                    BiomeLayoutController ctrl = instVar.GetComponent<BiomeLayoutController>();
                    if (ctrl != null)
                    {
                        ctrl.LayoutSeed += 1;
                        ctrl.SourcePrefabAssetPath = "";
                        PrefabUtility.UnpackPrefabInstance(instVar, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                        ctrl.SourcePrefabAssetPath = "";
                        ctrl.Regenerate(preserveLocked: false, instantiator: BiomeLayoutControllerEditor.InstantiateLinkedPrefab);
                        if (ctrl.ActivityZones != null) ctrl.ActivityZones.HideGizmos = true;
                    }
                }

                // Row 3 (Back): Formed 3-Hex biome right behind its 1-Hex tiles for instant contrast!
                GameObject p3 = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOutDir}/Biome_{biome}_3Hex.prefab");
                if (p3 != null)
                {
                    GameObject inst3 = (GameObject)PrefabUtility.InstantiatePrefab(p3, root.transform);
                    inst3.name = $"Biome_{biome}_3Hex_Formed";
                    inst3.transform.localPosition = new Vector3(x, 0f, 8.2f);
                }
            }

            Physics.SyncTransforms();
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView sv = SceneView.lastActiveSceneView;
                sv.sceneLighting = true;
                Vector3 focus = root.transform.position + new Vector3(0f, 0.9f, 3.6f);
                sv.LookAt(focus, Quaternion.Euler(36f, 0f, 0f), 14.5f);
                sv.Repaint();
            }

            Debug.Log($"[WetIslandSingleHex] Spawned '{rootName}' (Front: 1-Hex Seed A, Middle: 1-Hex Seed B, Back: 3-Hex Formed).");
        }

        private static void BuildSingleBiome(
            string biomeName,
            int seed,
            Texture2D waterMask,
            HexBiomeSurfaceSettings surfaceSettings,
            Material groundMaterial,
            Material waterMaterial,
            string designNotes,
            BiomePropLayer[] layers)
        {
            // 1. Save / update dedicated 1-Hex BiomeLayoutProfile
            string profilePath = $"{ProfilesOutDir}/Profile_{biomeName}_1Hex.asset";
            BiomeLayoutProfile profile = AssetDatabase.LoadAssetAtPath<BiomeLayoutProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BiomeLayoutProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.biomeId = biomeName;
            profile.designNotes = designNotes;
            profile.waterMask1Hex = waterMask;
            profile.surfaceSettings = surfaceSettings;
            profile.minSwimWaterDepthMeters = 0.06f;
            profile.walkableSampleSpacing = 0.55f;
            profile.swimmableSampleSpacing = 0.45f;
            profile.creatureAgentRadius = 0.28f;
            profile.dryCorridorWidth = 0f;
            profile.layers = layers;
            profile.sizingVersion = WetIslandTierPresetSetup.Version;
            EditorUtility.SetDirty(profile);

            // 2. Build watertight 1-Hex meshes
            List<HexCoordinates> hexes = HexBiomeSurfaceMeshBuilder.GetUprightTriangleHexes(1);
            string tag = $"{biomeName}_1Hex";
            Mesh groundMesh = HexBiomeSurfaceMeshBuilder.BuildGroundClusterMesh($"Mesh_Ground_{tag}", hexes, waterMask, surfaceSettings);
            groundMesh = HexBiomeSurfaceBuilder.SaveOrReplaceMeshAsset(groundMesh, $"{MeshesOutDir}/Mesh_Ground_{tag}.asset");

            Mesh waterMesh = null;
            if (waterMask != null && waterMaterial != null && surfaceSettings.maxBasinDepth > 0.0001f)
            {
                waterMesh = HexBiomeSurfaceMeshBuilder.BuildWaterSurfaceMesh($"Mesh_Water_{tag}", hexes, waterMask, surfaceSettings);
                waterMesh = HexBiomeSurfaceBuilder.SaveOrReplaceMeshAsset(waterMesh, $"{MeshesOutDir}/Mesh_Water_{tag}.asset");
            }

            // 3. Assemble root, generate measured linked props, and save prefab
            string prefabPath = $"{PrefabsOutDir}/Biome_{tag}.prefab";
            GameObject root = new GameObject($"Biome_{tag}");
            try
            {
                GameObject groundGo = new GameObject("Ground_Basin");
                groundGo.transform.SetParent(root.transform, false);
                groundGo.AddComponent<MeshFilter>().sharedMesh = groundMesh;
                groundGo.AddComponent<MeshRenderer>().sharedMaterial = groundMaterial;
                groundGo.AddComponent<MeshCollider>().sharedMesh = groundMesh;

                if (waterMesh != null && waterMaterial != null)
                {
                    GameObject waterGo = new GameObject("Water_Surface");
                    waterGo.transform.SetParent(root.transform, false);
                    waterGo.AddComponent<MeshFilter>().sharedMesh = waterMesh;
                    MeshRenderer wMr = waterGo.AddComponent<MeshRenderer>();
                    wMr.sharedMaterial = waterMaterial;
                    wMr.shadowCastingMode = ShadowCastingMode.Off;
                    wMr.receiveShadows = true;
                }

                BiomeLayoutController controller = root.AddComponent<BiomeLayoutController>();
                controller.Configure(profile, size: 1, seed: seed);
                controller.SourcePrefabAssetPath = prefabPath;
                if (controller.ActivityZones != null)
                {
                    controller.ActivityZones.HideGizmos = true;
                }

                BiomeLayoutBuildResult buildResult = controller.Regenerate(
                    preserveLocked: false,
                    instantiator: BiomeLayoutControllerEditor.InstantiateLinkedPrefab);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                if (!saved)
                {
                    throw new InvalidOperationException($"Failed to save prefab: {prefabPath}");
                }
                Debug.Log($"[WetIslandSingleHex] Saved {prefabPath} ({buildResult.placedPropCount} props, seed {seed}).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Material CloneAndCalibrateWaterMaterial(
            string sourcePath,
            string targetPath,
            float waterDepth,
            float shoreSmoothness,
            float shorelineDepth)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"[WetIslandSingleHex] Source water material missing: {sourcePath}");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (mat == null)
            {
                mat = new Material(source);
                AssetDatabase.CreateAsset(mat, targetPath);
            }
            else
            {
                mat.shader = source.shader;
                mat.CopyPropertiesFromMaterial(source);
                mat.shaderKeywords = source.shaderKeywords;
            }

            if (mat.HasProperty("_Water_Depth")) mat.SetFloat("_Water_Depth", waterDepth);
            if (mat.HasProperty("_ShoreFade_Smoothness")) mat.SetFloat("_ShoreFade_Smoothness", shoreSmoothness);
            if (mat.HasProperty("_SL_WaterDepth")) mat.SetFloat("_SL_WaterDepth", shorelineDepth);
            if (mat.HasProperty("_ENABLEWAVE"))
            {
                mat.SetFloat("_ENABLEWAVE", 0f);
                mat.DisableKeyword("_ENABLEWAVE");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Dictionary<string, GameObject> BuildArtPrefabCatalog()
        {
            var catalog = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Acs/Art" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && !catalog.ContainsKey(prefab.name))
                {
                    catalog.Add(prefab.name, prefab);
                }
            }
            return catalog;
        }

        private static GameObject[] Resolve(Dictionary<string, GameObject> catalog, params string[] names)
        {
            var list = new GameObject[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                if (!catalog.TryGetValue(names[i], out GameObject asset) || asset == null)
                    throw new InvalidOperationException($"[WetIslandSingleHex] Missing art prefab: {names[i]}");
                list[i] = asset;
            }
            return list;
        }

        private static CompositionPoint[] Pts(params (float u, float v)[] pts)
        {
            var arr = new CompositionPoint[pts.Length];
            for (int i = 0; i < pts.Length; i++) arr[i] = new CompositionPoint { u = pts[i].u, v = pts[i].v };
            return arr;
        }

        private static BiomePropLayer MakeLayer(
            string roleId,
            int count,
            HabitatGround ground,
            GameObject[] assets,
            DioramaSizeAxis axis,
            float targetMin,
            float targetMax,
            float minH,
            float maxH,
            float minW,
            float maxW,
            CompositionPoint[] guides = null,
            string[] parents = null,
            float radiusMin = 0.22f,
            float radiusMax = 0.56f,
            float spread = 0.06f,
            float spacing = 0.22f,
            float yOffset = 0f,
            float obstacleRadius = 0f,
            float solidFraction = 0f,
            bool waterHeight = false,
            bool preservePivot = false,
            DioramaFacing facing = DioramaFacing.Random,
            float tiltMin = 0f,
            float tiltMax = 0f)
        {
            var limits = new DioramaSizeLimits
            {
                primaryAxis = axis,
                targetMin = targetMin,
                targetMax = targetMax,
                minHeight = minH,
                maxHeight = maxH,
                minFootprint = minW,
                maxFootprint = maxW
            };
            var tier1 = new BiomePropTier
            {
                enabled = true,
                count = count,
                assets = (GameObject[])assets.Clone(),
                size = limits
            };
            return new BiomePropLayer
            {
                roleId = roleId,
                count1 = count,
                count3 = count,
                count6 = count,
                count10 = count,
                ground = ground,
                guides = guides ?? Array.Empty<CompositionPoint>(),
                clusterAroundParentRole = parents ?? Array.Empty<string>(),
                radiusMin = radiusMin,
                radiusMax = radiusMax,
                spread = spread,
                spacing = spacing,
                obstacleRadius = obstacleRadius,
                yOffset = yOffset,
                scaleMin = 0.92f,
                scaleMax = 1.05f,
                tiltMin = tiltMin,
                tiltMax = tiltMax,
                waterHeight = waterHeight,
                facing = facing,
                allowedVisualOverlap = 0.35f,
                solidFootprintFraction = solidFraction,
                preserveAuthoredPivot = preservePivot,
                pool = (GameObject[])assets.Clone(),
                useTierPresets = true,
                hex1 = tier1
            };
        }

        private static HexBiomeSurfaceSettings CreateLilywater1SurfaceSettings()
        {
            var s = HexBiomeSurfaceSettings.CreateDefault();
            s.shoreStartMask = 0.03f;
            s.waterThresholdMask = 0.21f;
            s.maxBasinDepth = 0.42f;
            s.waterSurfaceDrop = 0.055f;
            return s;
        }

        private static HexBiomeSurfaceSettings CreateReedmarsh1SurfaceSettings()
        {
            var s = HexBiomeSurfaceSettings.CreateDefault();
            s.shoreStartMask = 0.05f;
            s.waterThresholdMask = 0.38f;
            s.maxBasinDepth = 0.20f;
            s.waterSurfaceDrop = 0.045f;
            return s;
        }

        private static HexBiomeSurfaceSettings CreateDry1SurfaceSettings()
        {
            var s = HexBiomeSurfaceSettings.CreateDefault();
            s.shoreStartMask = 0.90f;
            s.waterThresholdMask = 0.95f;
            s.maxBasinDepth = 0f;
            s.waterSurfaceDrop = 0f;
            return s;
        }

        private static BiomePropLayer[] CreateLilywater1Layers(Dictionary<string, GameObject> cat)
        {
            const DioramaSizeAxis H = DioramaSizeAxis.Height;
            const DioramaSizeAxis W = DioramaSizeAxis.Footprint;
            return new[]
            {
                MakeLayer("LotusFocal", 1, HabitatGround.Water,
                    Resolve(cat, "Lotus_Cluster_01", "Lotus_Cluster_03"),
                    W, 0.44f, 0.54f, 0.09f, 0.24f, 0.40f, 0.58f,
                    guides: Pts((0.49f, 0.49f)), spread: 0.05f, waterHeight: true),

                MakeLayer("FloatingLeaves", 1, HabitatGround.Water,
                    Resolve(cat, "WaterPlant_1", "WaterPlant_4", "WaterPlant_5"),
                    W, 0.22f, 0.30f, 0.03f, 0.16f, 0.18f, 0.34f,
                    parents: new[] { "LotusFocal" }, radiusMin: 0.22f, radiusMax: 0.40f, spread: 0.05f, spacing: 0.14f, waterHeight: true),

                MakeLayer("PebbleBank", 3, HabitatGround.Bank,
                    Resolve(cat, "RiverRock_P3_045", "RiverRock_P3_063", "RiverRock_P4_070", "RiverRock_P4_072"),
                    W, 0.24f, 0.34f, 0.07f, 0.22f, 0.20f, 0.38f,
                    guides: Pts((0.76f, 0.44f), (0.64f, 0.76f), (0.26f, 0.34f)), spread: 0.06f, spacing: 0.22f, yOffset: -0.04f, solidFraction: 0.75f),

                MakeLayer("Iris", 1, HabitatGround.Bank,
                    Resolve(cat, "Wetland_WaterIris_01"),
                    H, 0.36f, 0.44f, 0.32f, 0.48f, 0.26f, 0.45f,
                    guides: Pts((0.24f, 0.64f)), spread: 0.07f, spacing: 0.20f),

                MakeLayer("GrassCollar", 2, HabitatGround.Land,
                    Resolve(cat, "Field_Grass_10", "Field_Grass_12", "Field_Grass_14", "Grass7", "Floralis_Clover_01", "Floralis_Clover_03"),
                    W, 0.32f, 0.46f, 0.06f, 0.24f, 0.28f, 0.50f,
                    guides: Pts((0.36f, 0.80f), (0.70f, 0.24f)), spread: 0.08f, spacing: 0.28f)
            };
        }

        private static BiomePropLayer[] CreateReedmarsh1Layers(Dictionary<string, GameObject> cat)
        {
            const DioramaSizeAxis H = DioramaSizeAxis.Height;
            const DioramaSizeAxis W = DioramaSizeAxis.Footprint;
            return new[]
            {
                MakeLayer("ReedClump", 1, HabitatGround.Bank,
                    Resolve(cat, "Wetland_GoldenReedSparse_01", "Wetland_GoldenReedBush_01"),
                    H, 0.56f, 0.68f, 0.52f, 0.72f, 0.34f, 0.74f,
                    guides: Pts((0.36f, 0.62f)), spread: 0.07f, spacing: 0.26f),

                MakeLayer("CattailClump", 1, HabitatGround.Bank,
                    Resolve(cat, "Wetland_CattailStylized_01"),
                    H, 0.54f, 0.66f, 0.50f, 0.70f, 0.34f, 0.54f,
                    parents: new[] { "ReedClump" }, radiusMin: 0.24f, radiusMax: 0.48f, spread: 0.06f, spacing: 0.22f),

                MakeLayer("HalfSubmergedStone", 1, HabitatGround.Bank,
                    Resolve(cat, "PropStone_MossyBoulder_01", "PropStone_MossyBoulder_02", "PropStone_MossyBoulder_05", "PropStone_MossyBoulder_06"),
                    W, 0.34f, 0.46f, 0.13f, 0.34f, 0.30f, 0.50f,
                    guides: Pts((0.68f, 0.64f)), spread: 0.08f, spacing: 0.22f, yOffset: -0.10f, obstacleRadius: 0.14f, solidFraction: 0.75f),

                MakeLayer("MarshCollar", 3, HabitatGround.Land,
                    Resolve(cat, "Grass3", "Grass4", "Grass5", "Wetland_SedgeArch_01", "WaterPlant_2", "WaterPlant_3"),
                    H, 0.24f, 0.38f, 0.20f, 0.42f, 0.18f, 0.65f,
                    parents: new[] { "ReedClump", "CattailClump" }, radiusMin: 0.20f, radiusMax: 0.56f, spread: 0.07f, spacing: 0.20f),

                MakeLayer("MarshAccent", 1, HabitatGround.Land,
                    Resolve(cat, "Field_Grass_15", "NatureEnv_ReedCattail_02"),
                    H, 0.24f, 0.34f, 0.20f, 0.38f, 0.16f, 0.46f,
                    guides: Pts((0.26f, 0.32f)), spread: 0.08f, spacing: 0.22f)
            };
        }

        private static BiomePropLayer[] CreateStreamstone1Layers(Dictionary<string, GameObject> cat)
        {
            const DioramaSizeAxis W = DioramaSizeAxis.Footprint;
            return new[]
            {
                MakeLayer("FocalBoulder", 1, HabitatGround.Land,
                    Resolve(cat, "PropStone_MossyBoulder_01", "PropStone_MossyBoulder_02", "PropStone_MossyBoulder_05", "PropStone_MossyBoulder_06"),
                    W, 0.62f, 0.76f, 0.26f, 0.54f, 0.58f, 0.80f,
                    guides: Pts((0.46f, 0.56f)), spread: 0.05f, spacing: 0.24f, yOffset: -0.07f, obstacleRadius: 0.18f, solidFraction: 0.75f),

                MakeLayer("RiverCobbles", 2, HabitatGround.Land,
                    Resolve(cat, "RiverRock_P3_063", "RiverRock_P4_070", "RiverRock_P4_072"),
                    W, 0.30f, 0.40f, 0.08f, 0.24f, 0.26f, 0.44f,
                    guides: Pts((0.30f, 0.36f), (0.64f, 0.44f)), spread: 0.06f, spacing: 0.28f, yOffset: -0.05f, obstacleRadius: 0.10f, solidFraction: 0.65f),

                MakeLayer("RockFerns", 2, HabitatGround.Land,
                    Resolve(cat, "Fern_AustralianSword_01", "Fern_AustralianSword_02", "Fern_Bead_01", "Fern_Bead_02"),
                    W, 0.52f, 0.70f, 0.20f, 0.50f, 0.48f, 0.75f,
                    guides: Pts((0.26f, 0.68f), (0.70f, 0.64f)), spread: 0.07f, spacing: 0.32f),

                MakeLayer("Wildflowers", 1, HabitatGround.Land,
                    Resolve(cat, "Flower_Bush", "Flower_7", "Flower_8"),
                    W, 0.28f, 0.40f, 0.09f, 0.30f, 0.24f, 0.44f,
                    guides: Pts((0.64f, 0.26f)), spread: 0.07f, spacing: 0.22f),

                MakeLayer("SoftMeadow", 2, HabitatGround.Land,
                    Resolve(cat, "Field_Grass_10", "Field_Grass_12", "Field_Grass_14", "Grass7", "Floralis_Clover_01", "Floralis_Clover_03"),
                    W, 0.34f, 0.48f, 0.06f, 0.25f, 0.30f, 0.52f,
                    guides: Pts((0.46f, 0.22f), (0.50f, 0.80f)), spread: 0.07f, spacing: 0.28f)
            };
        }

        private static BiomePropLayer[] CreateRainleaf1Layers(Dictionary<string, GameObject> cat)
        {
            const DioramaSizeAxis H = DioramaSizeAxis.Height;
            const DioramaSizeAxis W = DioramaSizeAxis.Footprint;
            return new[]
            {
                MakeLayer("YoungTropicalTree", 1, HabitatGround.Land,
                    Resolve(cat, "Fab_BananaTree_01", "Fab_BananaTree_02", "StylizedTropical_BananaTree_01", "Amazon_RainforestPalm_01", "Amazon_RainforestPalm_03"),
                    H, 1.02f, 1.28f, 0.96f, 1.35f, 0.50f, 1.38f,
                    guides: Pts((0.46f, 0.58f)), spread: 0.05f, spacing: 0.24f, obstacleRadius: 0.14f, solidFraction: 0.14f,
                    preservePivot: true, facing: DioramaFacing.Outward, tiltMin: 4f, tiltMax: 10f),

                MakeLayer("BroadleafUnderstory", 3, HabitatGround.Land,
                    Resolve(cat, "StylizedTropical_MonsteraBush_01", "StylizedTropical_MonsteraBush_02", "TropVeg01_AnthuriumBush_02", "TropVeg01_RedColeusBush_01", "Wetland_RoundLeafBush_01", "SetTropic_TaroBush_01"),
                    W, 0.46f, 0.66f, 0.13f, 0.54f, 0.42f, 0.72f,
                    guides: Pts((0.24f, 0.46f), (0.72f, 0.62f), (0.56f, 0.26f)), spread: 0.06f, spacing: 0.30f),

                MakeLayer("MossStone", 1, HabitatGround.Land,
                    Resolve(cat, "PropStone_MossyBoulder_01", "PropStone_MossyBoulder_02", "PropStone_MossyBoulder_05", "PropStone_MossyBoulder_06"),
                    W, 0.34f, 0.46f, 0.13f, 0.34f, 0.30f, 0.50f,
                    guides: Pts((0.36f, 0.74f)), spread: 0.06f, spacing: 0.22f, yOffset: -0.07f, obstacleRadius: 0.12f, solidFraction: 0.75f),

                MakeLayer("CentralGrassCarpet", 3, HabitatGround.Land,
                    Resolve(cat, "Field_Grass_10", "Field_Grass_12", "Field_Grass_14", "Grass7", "Floralis_Clover_01", "Floralis_Clover_03"),
                    W, 0.34f, 0.48f, 0.06f, 0.25f, 0.30f, 0.52f,
                    guides: Pts((0.32f, 0.26f), (0.74f, 0.38f), (0.54f, 0.80f)), spread: 0.07f, spacing: 0.24f)
            };
        }
    }
}
#endif

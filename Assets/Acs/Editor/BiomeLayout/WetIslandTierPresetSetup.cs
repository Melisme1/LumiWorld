using System;
using System.Collections.Generic;
using System.Linq;
using LumiWorld.Acs;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.EditorTools
{
    /// <summary>One-time, explicit migration. Defaults are editable proposals, never a load-time reset.</summary>
    public static class WetIslandTierPresetSetup
    {
        public const int Version = 1;

        [MenuItem("LumiWorld/WetIsland/Prepare Measured Tier Presets")]
        public static void PrepareAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:BiomeLayoutProfile", new[] { "Assets/Acs/Hex/WetIsland/Biomes/Profiles" }))
                Prepare(AssetDatabase.LoadAssetAtPath<BiomeLayoutProfile>(AssetDatabase.GUIDToAssetPath(guid)));
            AssetDatabase.SaveAssets();
            Debug.Log("[BiomeLayout] Tier presets prepared. Select a biome instance and Regenerate Layout. Art prefabs were not changed.");
        }

        public static void Prepare(BiomeLayoutProfile profile)
        {
            if (profile == null || profile.sizingVersion >= Version) return;
            var catalog = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Acs/Art" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && !catalog.ContainsKey(prefab.name)) catalog.Add(prefab.name, prefab);
            }
            // Work on copies: an absent asset or unknown role cannot leave a half-migrated profile.
            BiomeLayoutProfile draft = UnityEngine.Object.Instantiate(profile);
            try
            {
                foreach (BiomePropLayer layer in draft.layers)
                {
                    RoleDefaults defaults = Defaults(layer.roleId);
                    GameObject[] palette = defaults.names == null ? layer.pool : defaults.names.Select(name =>
                        catalog.TryGetValue(name, out GameObject asset) ? asset : throw new InvalidOperationException("Missing curated asset: " + name)).ToArray();
                    layer.hex3 = MakeTier(layer.count3, palette, defaults, defaults.factor3);
                    layer.hex6 = MakeTier(layer.count6, palette, defaults, 1f);
                    layer.hex10 = MakeTier(layer.count10, palette, defaults, defaults.factor10);
                    layer.useTierPresets = true;
                    layer.allowedVisualOverlap = defaults.overlap;
                    layer.solidFootprintFraction = defaults.solidFraction;
                    layer.preserveAuthoredPivot = layer.roleId == "WeepingBank" || layer.roleId == "ShoreFronds" ||
                        layer.roleId == "CurvedPalms" || layer.roleId == "BananaBackdrop" || layer.roleId == "CanopyTree";
                    if (layer.roleId == "HeadwaterCliff") { layer.facing = DioramaFacing.Authored; layer.yaw = 176; }
                    if (layer.roleId == "LogBridge") layer.obstacleRadius = 0;
                    if (layer.roleId == "HangingVines" || layer.roleId == "CanopyVines")
                    {
                        layer.parentHeightFraction = .80f;
                        layer.hangFromParent = true;
                        layer.yOffset = 0;
                    }
                    if (layer.roleId == "TrunkOrchids") { layer.parentHeightFraction = .25f; layer.yOffset = 0; }
                    // Low vegetation remains traversable regardless of its new visual size.
                    if (defaults.solidFraction == 0) layer.obstacleRadius = 0;
                }
                Undo.RecordObject(profile, "Prepare Measured Biome Tiers");
                profile.layers = draft.layers;
                profile.sizingVersion = Version;
                EditorUtility.SetDirty(profile);
            }
            finally { UnityEngine.Object.DestroyImmediate(draft); }
        }

        private static BiomePropTier MakeTier(int count, GameObject[] palette, RoleDefaults role, float factor)
        {
            return new BiomePropTier
            {
                count = count, assets = (GameObject[])palette.Clone(),
                size = new DioramaSizeLimits
                {
                    primaryAxis = role.axis, targetMin = role.min * factor, targetMax = role.max * factor,
                    minHeight = role.minH * factor, maxHeight = role.maxH * factor,
                    minFootprint = role.minW * factor, maxFootprint = role.maxW * factor
                }
            };
        }

        private sealed class RoleDefaults
        {
            public DioramaSizeAxis axis;
            public float min, max, minH, maxH, minW, maxW;
            public float factor3 = .86f, factor10 = 1.14f, overlap = .35f, solidFraction;
            public string[] names;
        }

        private static RoleDefaults Size(DioramaSizeAxis axis, float min, float max, float minH, float maxH,
            float minW, float maxW, float solid = 0, params string[] names)
        {
            return new RoleDefaults { axis = axis, min = min, max = max, minH = minH, maxH = maxH,
                minW = minW, maxW = maxW, solidFraction = solid, names = names.Length == 0 ? null : names };
        }

        private static RoleDefaults Defaults(string role)
        {
            const DioramaSizeAxis height = DioramaSizeAxis.Height;
            const DioramaSizeAxis width = DioramaSizeAxis.Footprint;
            switch (role)
            {
                case "LotusIslands": return Size(width, .52f, .72f, .10f, .40f, .45f, .76f, 0,
                    "Lotus_Cluster_01", "Lotus_Cluster_03", "NatureEnv_CloverPatch_01", "WaterPlant_6");
                case "FloatingLeaves": case "CornerPads": return Size(width, .28f, .42f, .04f, .20f, .25f, .45f, 0,
                    "WaterPlant_1", "WaterPlant_4", "WaterPlant_5");
                case "PebbleBank": return Size(width, .32f, .48f, .09f, .32f, .28f, .50f, .75f,
                    "RiverRock_P3_045", "RiverRock_P3_063", "RiverRock_P4_070", "RiverRock_P4_072");
                case "BankBoulders": case "CliffFoot": case "HalfSubmergedStones": case "RootStones":
                    return Size(width, .48f, .68f, .18f, .48f, .44f, .72f, .75f,
                        "PropStone_MossyBoulder_01", "PropStone_MossyBoulder_02", "PropStone_MossyBoulder_05", "PropStone_MossyBoulder_06");
                case "Roundleaf": return Size(height, .43f, .55f, .40f, .58f, .48f, .80f, 0, "Wetland_RoundLeafBush_01");
                case "Iris": return Size(height, .43f, .56f, .40f, .59f, .32f, .58f, 0, "Wetland_WaterIris_01");
                case "WeepingBank": return Size(width, .55f, .72f, .25f, .43f, .50f, .75f, 0, "Wetland_ShoreGrassWeep_01");
                case "RearCattail": case "CattailCore": return Size(height, .78f, .96f, .72f, 1f, .42f, .77f, 0, "Wetland_CattailStylized_01");
                case "GoldenReedCore": return Size(height, .90f, 1.08f, .85f, 1.12f, .75f, 1.10f, 0, "Wetland_GoldenReedBush_01");
                case "SparseReedEdge": return Size(height, .70f, .87f, .65f, .91f, .42f, .68f, 0, "Wetland_GoldenReedSparse_01");
                case "MarshCollar": return Size(height, .33f, .53f, .30f, .56f, .30f, .90f, 0,
                    "Grass3", "Grass4", "Grass5", "Wetland_SedgeArch_01", "WaterPlant_2", "WaterPlant_3");
                case "MeadowFlowers": case "Wildflowers": return Size(width, .35f, .55f, .12f, .38f, .30f, .60f, 0,
                    "Flower_Bush", "Flower_7", "Flower_8");
                case "MarshFlowers": return Size(height, .30f, .44f, .27f, .47f, .23f, .64f, 0,
                    "NatureEnv_ReedCattail_01", "NatureEnv_ReedCattail_02", "Field_Grass_15");
                case "GrassCollar": case "SoftMeadow": case "CentralGrassCarpet":
                    return Size(width, .45f, .68f, .09f, .34f, .40f, .74f, 0,
                        "Field_Grass_10", "Field_Grass_12", "Field_Grass_14", "Grass7", "Floralis_Clover_01", "Floralis_Clover_03");
                case "HeadwaterCliff":
                    var cliff = Size(height, 1.24f, 1.42f, 1.15f, 1.50f, 1.75f, 2.55f, .42f,
                        "Wetland_HeadwaterRock_01", "Wetland_HeadwaterRock_02");
                    cliff.factor3 = .80f; cliff.factor10 = 1.16f; return cliff;
                case "HangingVines": case "CanopyVines": return Size(height, .50f, .85f, .45f, .90f, .12f, .85f, 0,
                    "Vine_Plant_01", "Vine_Plant_03", "Vine_Plant_07", "Vine_Plant_19");
                case "LogBridge": return Size(width, .85f, 2.4f, .25f, 1.05f, .80f, 2.45f, 0, "fallen_log");
                case "SteppingStones": return Size(width, .34f, .46f, .09f, .25f, .30f, .49f, .65f,
                    "RiverRock_P3_063", "RiverRock_P4_070", "RiverRock_P4_072");
                case "OuterFerns": return Size(width, .70f, 1.0f, .28f, .70f, .65f, 1.05f, 0,
                    "Fern_AustralianSword_01", "Fern_AustralianSword_02", "Fern_Bead_01", "Fern_Bead_02");
                case "ShoreFronds": return Size(width, .55f, .75f, .22f, .44f, .50f, .80f, 0,
                    "Wetland_ShoreGrassWeep_01", "Wetland_SedgeArch_01");
                case "CanopyTree":
                    var tree = Size(height, 2.4f, 2.8f, 2.3f, 2.9f, 1.8f, 2.95f, .14f,
                        "Amazon_MangroveStiltTree_01", "Amazon_MangroveStiltTree_03", "Amazon_MangroveStiltTree_04");
                    tree.factor3 = .84f; tree.factor10 = 1.12f; return tree;
                case "CurvedPalms": return Size(height, 1.65f, 2.15f, 1.55f, 2.25f, .70f, 1.75f, .12f,
                    "Fab_PalmTree_01", "Fab_PalmTree_02", "Amazon_RainforestPalm_01", "Amazon_RainforestPalm_03");
                case "BananaBackdrop": return Size(height, 1.35f, 1.80f, 1.25f, 1.9f, .85f, 1.80f, .15f,
                    "Fab_BananaTree_01", "Fab_BananaTree_02", "StylizedTropical_BananaTree_01");
                case "BroadleafUnderstory": return Size(width, .65f, .95f, .25f, .70f, .60f, 1f, 0,
                    "StylizedTropical_MonsteraBush_01", "StylizedTropical_MonsteraBush_02", "TropVeg01_AnthuriumBush_02",
                    "TropVeg01_RedColeusBush_01", "Wetland_RoundLeafBush_01", "SetTropic_TaroBush_01");
                case "TrunkOrchids": return Size(width, .32f, .48f, .14f, .33f, .28f, .52f, 0, "Fab_Orchid_01");
                default: throw new InvalidOperationException("No migration defaults for role " + role + "; keep the existing profile and configure this role explicitly.");
            }
        }
    }
}

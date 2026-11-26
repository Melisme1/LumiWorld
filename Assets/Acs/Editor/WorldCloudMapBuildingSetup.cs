using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LumiWorld.Acs.Islands;

namespace LumiWorld.Acs.Editor
{
    /// <summary>
    /// Installs or updates the World Cloud Sea (Painterly 2.5D) system inside the main scene
    /// Assets/Scenes/MapBuilding.unity using exact measured IslandClusterBoundary radii from IslandCatalog.asset.
    /// Keeps existing CloudManager (high sky clouds), Ocean, Camera, and IslandSystem untouched.
    /// Menu: LumiWorld > Tools > Install World Cloud Sea in MapBuilding
    /// </summary>
    public static class WorldCloudMapBuildingSetup
    {
        public const string MapBuildingScenePath = "Assets/Scenes/MapBuilding.unity";
        public const string IslandCatalogPath = "Assets/Acs/Data/Islands/IslandCatalog.asset";
        public const string MaterialsFolder = "Assets/Acs/Art/Environment/Clouds";
        public const string CloudSystemObjectName = "WorldCloudSeaSystem";

        private const float ShorelinePadding = 7.5f;
        private const float EdgeSoftness = 0.22f;
        private const float BaseHeight = 3.0f;
        private const float OuterSeaMargin = 140f;

        [MenuItem("LumiWorld/Tools/Install World Cloud Sea in MapBuilding", priority = 31)]
        public static void InstallInMapBuildingMenu()
        {
            InstallInMapBuilding();
        }

        public static string InstallInMapBuilding()
        {
            IslandCatalog catalog = AssetDatabase.LoadAssetAtPath<IslandCatalog>(IslandCatalogPath);
            if (catalog == null)
                throw new InvalidOperationException($"Missing IslandCatalog at {IslandCatalogPath}");

            Material matSea = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_CozyWorldCloudSea.mat");
            Material matSprite = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_CozyCloudSpriteBank.mat");
            Material matSdf = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_CozyCloudPuff_SmoothSDF.mat");

            if (matSea == null || matSprite == null || matSdf == null)
                throw new InvalidOperationException($"Missing cloud materials inside {MaterialsFolder}. Run CloudCoverSandboxSetup first.");
            if (matSea.shader == null || !matSea.shader.isSupported)
                throw new InvalidOperationException("M_CozyWorldCloudSea shader missing or unsupported.");
            if (matSprite.shader == null || !matSprite.shader.isSupported)
                throw new InvalidOperationException("M_CozyCloudSpriteBank shader missing or unsupported.");

            if (!matSprite.enableInstancing)
            {
                matSprite.enableInstancing = true;
                EditorUtility.SetDirty(matSprite);
            }
            if (!matSdf.enableInstancing)
            {
                matSdf.enableInstancing = true;
                EditorUtility.SetDirty(matSdf);
            }

            Scene active = SceneManager.GetActiveScene();
            if (!string.Equals(active.path, MapBuildingScenePath, StringComparison.OrdinalIgnoreCase))
            {
                active = EditorSceneManager.OpenScene(MapBuildingScenePath, OpenSceneMode.Single);
            }

            // Idempotent lookup: remove accidental duplicates if any, keep single root
            GameObject cloudGo = null;
            GameObject[] roots = active.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != CloudSystemObjectName) continue;
                if (cloudGo == null) cloudGo = roots[i];
                else UnityEngine.Object.DestroyImmediate(roots[i]);
            }

            if (cloudGo == null)
            {
                cloudGo = new GameObject(CloudSystemObjectName);
                Undo.RegisterCreatedObjectUndo(cloudGo, "Create WorldCloudSeaSystem");
            }

            cloudGo.transform.position = Vector3.zero;
            cloudGo.transform.rotation = Quaternion.identity;
            cloudGo.transform.localScale = Vector3.one;

            CozyWorldCloudSystem cloudSystem = cloudGo.GetComponent<CozyWorldCloudSystem>();
            if (cloudSystem == null) cloudSystem = cloudGo.AddComponent<CozyWorldCloudSystem>();

            IslandWorldCloudBridge bridge = cloudGo.GetComponent<IslandWorldCloudBridge>();
            if (bridge == null) bridge = cloudGo.AddComponent<IslandWorldCloudBridge>();

            IslandWorldCloudBridge.BuildCatalogLayout(
                catalog,
                ShorelinePadding,
                OuterSeaMargin,
                out Vector3 seaCenter,
                out float seaExtent,
                out List<CloudClearZone> zones);

            cloudSystem.ConfigureSystem(matSea, matSprite, matSdf, seaCenter, seaExtent, zones);
            cloudSystem.SetLiveTuning(height: BaseHeight, softness: EdgeSoftness, scale: 1.0f, speed: 1.0f);
            cloudSystem.SetStyle(WorldCloudStyle.Painterly25D);

            bridge.Configure(catalog, cloudSystem);

            var bridgeSo = new SerializedObject(bridge);
            bridgeSo.FindProperty("shorelinePadding").floatValue = ShorelinePadding;
            bridgeSo.FindProperty("worldEdgeSoftness").floatValue = EdgeSoftness;
            bridgeSo.FindProperty("cloudBaseHeight").floatValue = BaseHeight;
            bridgeSo.FindProperty("outerSeaMargin").floatValue = OuterSeaMargin;
            bridgeSo.FindProperty("defaultStyle").enumValueIndex = (int)WorldCloudStyle.Painterly25D;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(cloudSystem);
            EditorUtility.SetDirty(bridge);
            EditorUtility.SetDirty(cloudGo);
            EditorSceneManager.MarkSceneDirty(active);

            if (!EditorSceneManager.SaveScene(active))
                throw new InvalidOperationException($"Failed to save scene {MapBuildingScenePath}");

            AssetDatabase.SaveAssets();

            string summary = $"Installed {CloudSystemObjectName} in {active.path} | seaCenter={seaCenter}, seaExtent={seaExtent:F1}m";
            foreach (var z in zones)
            {
                summary += $" | zone[{z.id}]: radius={z.clearRadius:F2}m, unlocked={z.isUnlocked}";
            }
            return summary;
        }
    }
}

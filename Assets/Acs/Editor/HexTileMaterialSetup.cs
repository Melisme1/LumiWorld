using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.EditorTools
{
    /// <summary>
    /// Configuration for batch-setting up custom hex tile FBX materials and HexMetrics-normalized prefabs.
    /// </summary>
    public struct HexTileSetupConfig
    {
        public string modelsRootFolder;
        public string prefabsOutputFolder;
        public string materialPrefix;
        public float smoothness;
        public float targetHexWidthX;
        public float targetHexDepthZ;
        public Func<string, float> targetHeightResolver;

        public static HexTileSetupConfig CreateDefault(
            string modelsRootFolder,
            string prefabsOutputFolder,
            string materialPrefix)
        {
            return new HexTileSetupConfig
            {
                modelsRootFolder = modelsRootFolder,
                prefabsOutputFolder = prefabsOutputFolder,
                materialPrefix = materialPrefix,
                smoothness = 0.15f,
                targetHexWidthX = 2.0f * HexMetrics.InnerRadius,
                targetHexDepthZ = 2.0f * HexMetrics.OuterRadius,
                targetHeightResolver = DefaultHeightFromFolderName
            };
        }

        public static float DefaultHeightFromFolderName(string folderName)
        {
            string lower = folderName.ToLowerInvariant();
            if (lower.Contains("high")) return 2.6f;
            if (lower.Contains("med")) return 1.8f;
            return 1.0f;
        }
    }

    /// <summary>
    /// Reusable editor pipeline for configuring URP Lit materials, remapping FBX materials,
    /// and generating Pointed-Top HexMetrics-normalized tile prefabs for any island's hex model set.
    /// </summary>
    public static class HexTileMaterialSetup
    {
        private static readonly HexTileSetupConfig WetIslandConfig = HexTileSetupConfig.CreateDefault(
            modelsRootFolder: "Assets/Acs/Hex/WetIsland/Models",
            prefabsOutputFolder: "Assets/Acs/Hex/WetIsland/Prefabs",
            materialPrefix: "M_WetIsland_");

        private static readonly (int q, int r, string prefabName)[] WetIslandTestLayout = new[]
        {
            (0, 0, "Lush-high"),
            (1, 0, "Normal-high"),
            (0, 1, "Lush-med"),
            (-1, 1, "Normal-med"),
            (-1, 0, "Lush-low"),
            (0, -1, "Normal-low"),
            (1, -1, "Lush-med"),
            (-1, -1, "Normal-low"),
            (-2, 1, "Lush-low"),
        };

        [MenuItem("LumiWorld/Tools/Hex Tiles/Setup Selected Models Folder (Materials & Prefabs)")]
        public static void SetupSelectedModelsFolder()
        {
            string selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath) || !AssetDatabase.IsValidFolder(selectedPath))
            {
                Debug.LogWarning("[HexTileSetup] Please select a hex 'Models' folder in the Project window first.");
                return;
            }

            string parentFolder = Path.GetDirectoryName(selectedPath)?.Replace('\\', '/');
            string islandTag = !string.IsNullOrEmpty(parentFolder) ? Path.GetFileName(parentFolder) : Path.GetFileName(selectedPath);
            string prefabsOut = !string.IsNullOrEmpty(parentFolder) ? $"{parentFolder}/Prefabs" : $"{selectedPath}/Prefabs";

            var config = HexTileSetupConfig.CreateDefault(
                modelsRootFolder: selectedPath,
                prefabsOutputFolder: prefabsOut,
                materialPrefix: $"M_{islandTag}_");

            SetupTiles(in config, createPrefabs: true);
        }

        [MenuItem("LumiWorld/Tools/Hex Tiles/WetIsland - Setup Materials")]
        public static void SetupWetIslandMaterials()
        {
            SetupTiles(in WetIslandConfig, createPrefabs: false);
        }

        [MenuItem("LumiWorld/Tools/Hex Tiles/WetIsland - Setup Materials & Prefabs")]
        public static void SetupWetIslandMaterialsAndPrefabs()
        {
            SetupTiles(in WetIslandConfig, createPrefabs: true);
        }

        [MenuItem("LumiWorld/Tools/Hex Tiles/WetIsland - Spawn Test Cluster (Edit Mode)")]
        public static void SpawnWetIslandTestCluster()
        {
            SpawnTestCluster(
                clusterName: "WetIsland_Test_Cluster",
                prefabsFolder: WetIslandConfig.prefabsOutputFolder,
                layout: WetIslandTestLayout,
                worldOrigin: new Vector3(30f, 0f, 0f));
        }

        public static GameObject SpawnTestCluster(
            string clusterName,
            string prefabsFolder,
            IReadOnlyList<(int q, int r, string prefabName)> layout,
            Vector3 worldOrigin)
        {
            GameObject existing = GameObject.Find(clusterName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject clusterRoot = new GameObject(clusterName);
            Undo.RegisterCreatedObjectUndo(clusterRoot, $"Spawn {clusterName}");
            clusterRoot.transform.position = worldOrigin;

            for (int i = 0; i < layout.Count; i++)
            {
                var item = layout[i];
                string prefabPath = $"{prefabsFolder}/{item.prefabName}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[HexTileSetup] Missing prefab: {prefabPath}");
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, clusterRoot.transform);
                instance.name = $"{item.prefabName}_({item.q},{item.r})";
                instance.transform.localPosition = HexMetrics.HexToWorldPosition(new HexCoordinates(item.q, item.r), 0f);
            }

            Selection.activeGameObject = clusterRoot;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log($"[HexTileSetup] Spawned '{clusterName}' using exact HexMetrics coordinates.");
            return clusterRoot;
        }

        public static int SetupTiles(in HexTileSetupConfig config, bool createPrefabs)
        {
            if (string.IsNullOrEmpty(config.modelsRootFolder) || !AssetDatabase.IsValidFolder(config.modelsRootFolder))
            {
                Debug.LogError($"[HexTileSetup] Folder not found: {config.modelsRootFolder}");
                return 0;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[HexTileSetup] Could not find shader 'Universal Render Pipeline/Lit'.");
                return 0;
            }

            if (createPrefabs && !AssetDatabase.IsValidFolder(config.prefabsOutputFolder))
            {
                Directory.CreateDirectory(config.prefabsOutputFolder);
                AssetDatabase.Refresh();
            }

            string[] subFolders = AssetDatabase.GetSubFolders(config.modelsRootFolder);
            var fbxToFolderName = new List<(string fbxPath, string folderName)>();
            int processedCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (string folder in subFolders)
                {
                    string folderName = Path.GetFileName(folder);
                    string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { folder });
                    if (fbxGuids.Length == 0)
                    {
                        Debug.LogWarning($"[HexTileSetup] No model found in {folder}");
                        continue;
                    }

                    string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[0]);
                    Texture2D baseTex = null;
                    Texture2D normalTex = null;

                    string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                    foreach (string guid in texGuids)
                    {
                        string texPath = AssetDatabase.GUIDToAssetPath(guid);
                        string fileName = Path.GetFileNameWithoutExtension(texPath).ToLowerInvariant();

                        if (fileName.EndsWith("_normal"))
                        {
                            ConfigureAsNormalMap(texPath);
                            normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                        }
                        else if (fileName.EndsWith("_texture"))
                        {
                            baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                        }
                    }

                    if (baseTex == null)
                    {
                        Debug.LogWarning($"[HexTileSetup] No base texture (*_texture) found in {folder}");
                        continue;
                    }

                    string matPath = $"{folder}/{config.materialPrefix}{folderName}.mat";
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null)
                    {
                        mat = new Material(urpLit);
                        AssetDatabase.CreateAsset(mat, matPath);
                    }
                    else
                    {
                        mat.shader = urpLit;
                    }

                    mat.SetTexture("_BaseMap", baseTex);
                    mat.SetColor("_BaseColor", Color.white);
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", config.smoothness);

                    if (normalTex != null)
                    {
                        mat.SetTexture("_BumpMap", normalTex);
                        mat.SetFloat("_BumpScale", 1f);
                        mat.EnableKeyword("_NORMALMAP");
                    }

                    EditorUtility.SetDirty(mat);
                    RemapFbxMaterial(fbxPath, mat);
                    fbxToFolderName.Add((fbxPath, folderName));
                    processedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (createPrefabs)
            {
                foreach (var entry in fbxToFolderName)
                {
                    CreateNormalizedPrefab(entry.fbxPath, entry.folderName, in config);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[HexTileSetup] Done! Processed {processedCount} hex folders in '{config.modelsRootFolder}'" +
                      (createPrefabs ? $" and saved prefabs to '{config.prefabsOutputFolder}'." : "."));
            return processedCount;
        }

        private static string GetSelectedFolderPath()
        {
            UnityEngine.Object active = Selection.activeObject;
            if (active == null) return null;
            string path = AssetDatabase.GetAssetPath(active);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/');
        }

        private static void ConfigureAsNormalMap(string texPath)
        {
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer == null) return;

            if (importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        private static void RemapFbxMaterial(string fbxPath, Material targetMat)
        {
            var modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (modelImporter == null) return;

            modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            modelImporter.materialLocation = ModelImporterMaterialLocation.InPrefab;

            bool remappedAny = false;
            foreach (UnityEngine.Object subAsset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (subAsset is Material embeddedMat)
                {
                    modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(embeddedMat), targetMat);
                    remappedAny = true;
                }
            }

            if (!remappedAny)
            {
                modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material.002"), targetMat);
                modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material"), targetMat);
            }

            modelImporter.SaveAndReimport();
        }

        private static void CreateNormalizedPrefab(string fbxPath, string folderName, in HexTileSetupConfig config)
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null) return;

            Func<string, float> resolver = config.targetHeightResolver ?? HexTileSetupConfig.DefaultHeightFromFolderName;
            float targetHeight = resolver(folderName);

            GameObject root = new GameObject(folderName);
            GameObject offsetHolder = new GameObject("MeshPivot");
            offsetHolder.transform.SetParent(root.transform, false);

            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, offsetHolder.transform);
            modelInstance.name = folderName + "_Mesh";

            try
            {
                if (!TryComputeWorldVertexBounds(root, out Bounds bounds))
                {
                    Debug.LogWarning($"[HexTileSetup] Could not compute bounds for {folderName}");
                    return;
                }

                // Ensure Pointed-Top orientation (Z depth > X width)
                if (bounds.size.x > bounds.size.z * 1.05f)
                {
                    offsetHolder.transform.localEulerAngles = new Vector3(0f, 90f, 0f);
                    TryComputeWorldVertexBounds(root, out bounds);
                }

                float scaleX = config.targetHexWidthX / Mathf.Max(0.0001f, bounds.size.x);
                float scaleY = targetHeight / Mathf.Max(0.0001f, bounds.size.y);
                float scaleZ = config.targetHexDepthZ / Mathf.Max(0.0001f, bounds.size.z);
                offsetHolder.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

                if (TryComputeWorldVertexBounds(root, out bounds))
                {
                    offsetHolder.transform.localPosition = new Vector3(
                        -bounds.center.x,
                        -bounds.min.y,
                        -bounds.center.z);
                }

                string prefabPath = $"{config.prefabsOutputFolder}/{folderName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static bool TryComputeWorldVertexBounds(GameObject root, out Bounds bounds)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            bool hasAny = false;
            bounds = new Bounds();

            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                Vector3[] verts = mf.sharedMesh.vertices;
                if (verts == null || verts.Length == 0) continue;

                Matrix4x4 l2w = mf.transform.localToWorldMatrix;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 wp = l2w.MultiplyPoint3x4(verts[i]);
                    if (!hasAny)
                    {
                        bounds = new Bounds(wp, Vector3.zero);
                        hasAny = true;
                    }
                    else
                    {
                        bounds.Encapsulate(wp);
                    }
                }
            }

            return hasAny;
        }
    }
}

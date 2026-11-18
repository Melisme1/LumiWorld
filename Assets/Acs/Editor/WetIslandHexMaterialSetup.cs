using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.EditorTools
{
    public static class WetIslandHexMaterialSetup
    {
        private const string ModelsRootFolder = "Assets/Acs/Hex/WetIsland/Models";
        private const string PrefabsOutputFolder = "Assets/Acs/Hex/WetIsland/Prefabs";

        private const float TargetHexWidthX = 2.0f;      // 2 * HexMetrics.InnerRadius (1.0f)
        private const float TargetHexDepthZ = 2.3094f;   // 2 * HexMetrics.OuterRadius (1.1547f)

        [MenuItem("LumiWorld/Tools/Setup WetIsland Hex Materials")]
        public static void SetupMaterials()
        {
            SetupAll(createPrefabs: false);
        }

        [MenuItem("LumiWorld/Tools/Setup WetIsland Hex Materials & Prefabs")]
        public static void SetupMaterialsAndPrefabs()
        {
            SetupAll(createPrefabs: true);
        }

        [MenuItem("LumiWorld/Tools/Spawn WetIsland Test Cluster (Edit Mode)")]
        public static void SpawnTestCluster()
        {
            const string clusterName = "WetIsland_Test_Cluster";
            GameObject existing = GameObject.Find(clusterName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject clusterRoot = new GameObject(clusterName);
            Undo.RegisterCreatedObjectUndo(clusterRoot, "Spawn WetIsland Test Cluster");
            clusterRoot.transform.position = new Vector3(30f, 0f, 0f);

            var layout = new (int q, int r, string prefabName)[]
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

            foreach (var item in layout)
            {
                string prefabPath = $"{PrefabsOutputFolder}/{item.prefabName}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[WetIslandHexSetup] Missing prefab: {prefabPath}");
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, clusterRoot.transform);
                instance.name = $"{item.prefabName}_({item.q},{item.r})";
                Vector3 localPos = HexMetrics.HexToWorldPosition(new HexCoordinates(item.q, item.r), 0f);
                instance.transform.localPosition = localPos;
            }

            Selection.activeGameObject = clusterRoot;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("[WetIslandHexSetup] Spawned WetIsland_Test_Cluster using exact HexMetrics coordinates.");
        }

        private static void SetupAll(bool createPrefabs)
        {
            if (!AssetDatabase.IsValidFolder(ModelsRootFolder))
            {
                Debug.LogError($"[WetIslandHexSetup] Folder not found: {ModelsRootFolder}");
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[WetIslandHexSetup] Could not find shader 'Universal Render Pipeline/Lit'.");
                return;
            }

            if (createPrefabs && !AssetDatabase.IsValidFolder(PrefabsOutputFolder))
            {
                Directory.CreateDirectory(PrefabsOutputFolder);
                AssetDatabase.Refresh();
            }

            string[] subFolders = AssetDatabase.GetSubFolders(ModelsRootFolder);
            var fbxToFolderName = new System.Collections.Generic.List<(string fbxPath, string folderName)>();
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
                        Debug.LogWarning($"[WetIslandHexSetup] No FBX found in {folder}");
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
                        Debug.LogWarning($"[WetIslandHexSetup] No base texture (*_texture.png) found in {folder}");
                        continue;
                    }

                    string matPath = $"{folder}/M_WetIsland_{folderName}.mat";
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
                    mat.SetFloat("_Smoothness", 0.15f);

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
                    CreateNormalizedPrefab(entry.fbxPath, entry.folderName);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[WetIslandHexSetup] Done! Processed {processedCount} hex folders" +
                      (createPrefabs ? $" and saved prefabs to {PrefabsOutputFolder}." : "."));
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

        private static void CreateNormalizedPrefab(string fbxPath, string folderName)
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null) return;

            float targetHeight = GetTargetHeightFromFolderName(folderName);

            GameObject root = new GameObject(folderName);
            GameObject offsetHolder = new GameObject("MeshPivot");
            offsetHolder.transform.SetParent(root.transform, false);

            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, offsetHolder.transform);
            modelInstance.name = folderName + "_Mesh";

            try
            {
                if (!TryComputeWorldVertexBounds(root, out Bounds bounds))
                {
                    Debug.LogWarning($"[WetIslandHexSetup] Could not compute bounds for {folderName}");
                    return;
                }

                // Ensure Pointed-Top orientation (Z depth > X width)
                if (bounds.size.x > bounds.size.z * 1.05f)
                {
                    offsetHolder.transform.localEulerAngles = new Vector3(0f, 90f, 0f);
                    TryComputeWorldVertexBounds(root, out bounds);
                }

                float scaleX = TargetHexWidthX / Mathf.Max(0.0001f, bounds.size.x);
                float scaleY = targetHeight / Mathf.Max(0.0001f, bounds.size.y);
                float scaleZ = TargetHexDepthZ / Mathf.Max(0.0001f, bounds.size.z);
                offsetHolder.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

                if (TryComputeWorldVertexBounds(root, out bounds))
                {
                    offsetHolder.transform.localPosition = new Vector3(
                        -bounds.center.x,
                        -bounds.min.y,
                        -bounds.center.z
                    );
                }

                string prefabPath = $"{PrefabsOutputFolder}/{folderName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static float GetTargetHeightFromFolderName(string folderName)
        {
            string lower = folderName.ToLowerInvariant();
            if (lower.Contains("high")) return 2.6f;
            if (lower.Contains("med")) return 1.8f;
            return 1.0f;
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

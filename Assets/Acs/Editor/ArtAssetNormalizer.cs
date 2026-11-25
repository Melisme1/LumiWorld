#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LumiWorld.EditorTools
{
    /// <summary>
    /// General-purpose Unity Editor normalizer for mesh, texture, material, and prefab assets
    /// under Assets/Acs/Art. Supports both manifest-driven batch builds (art_asset_manifest.json)
    /// and direct category-folder normalization for newly imported asset packs.
    /// </summary>
    public static class ArtAssetNormalizer
    {
        [Serializable]
        private class ManifestEntry
        {
            public string category = "";
            public string modelName = "";
            public string modelRelPath = "";
            public string materialName = "";
            public string albedoTex = "";
            public string normalTex = "";
            public bool alphaClip = false;
            public string sourcePack = "";
        }

        [Serializable]
        private class ManifestWrapper
        {
            public List<ManifestEntry> items = null;
        }

        public const string DefaultArtRoot = "Assets/Acs/Art";
        public const string DefaultManifestPath = "Assets/Acs/Art/art_asset_manifest.json";

        private static readonly HashSet<string> NonPropCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ground",
            "UI",
            "Test",
            "Forest",
            "_StyleArchive"
        };

        private static readonly HashSet<string> OpaqueCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Stone",
            "Rock",
            "Rocks",
            "Wood",
            "Log",
            "Props",
            "Shell",
            "Structure"
        };

        [MenuItem("LumiWorld/Assets/Art Normalizer/1. Build All From Manifest (Assets/Acs/Art)")]
        public static void BuildAllFromDefaultManifest()
        {
            BuildFromManifest(DefaultManifestPath, DefaultArtRoot);
        }

        [MenuItem("LumiWorld/Assets/Art Normalizer/2. Normalize Selected Category Folder (Models -> Materials & Prefabs)")]
        public static void NormalizeSelectedCategoryFolder()
        {
            string selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath) || !AssetDatabase.IsValidFolder(selectedPath))
            {
                Debug.LogWarning("[ArtAssetNormalizer] Please select a category folder (or its Models subfolder) under Assets/Acs/Art in the Project window.");
                return;
            }

            string categoryFolder = ResolveCategoryFolder(selectedPath);
            if (string.IsNullOrEmpty(categoryFolder) || !categoryFolder.StartsWith(DefaultArtRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[ArtAssetNormalizer] Selected folder '{selectedPath}' is not a category folder under '{DefaultArtRoot}'.");
                return;
            }

            NormalizeCategoryFolder(categoryFolder);
        }

        [MenuItem("LumiWorld/Assets/Art Normalizer/3. Audit All Art Assets (Manifest + Materials + Bounds)")]
        public static void AuditDefaultArtLibrary()
        {
            AuditAllArtAssets(DefaultArtRoot, DefaultManifestPath);
        }

        public static void BuildFromManifest(string manifestPath, string artRoot = DefaultArtRoot)
        {
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[ArtAssetNormalizer] Manifest not found at " + manifestPath);
                return;
            }

            AssetDatabase.Refresh();

            string rawJson = File.ReadAllText(manifestPath);
            string wrappedJson = "{\"items\":" + rawJson + "}";
            ManifestWrapper wrapper = JsonUtility.FromJson<ManifestWrapper>(wrappedJson);
            if (wrapper == null || wrapper.items == null || wrapper.items.Count == 0)
            {
                Debug.LogWarning("[ArtAssetNormalizer] Manifest is empty or invalid.");
                return;
            }

            Shader urpLit = FindDefaultLitShader();

            var albedoNeedsAlpha = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var normalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in wrapper.items)
            {
                string[] matTokens = SplitPipe(item.materialName);
                string[] albedos = SplitPipe(item.albedoTex);
                string[] normals = SplitPipe(item.normalTex);

                for (int i = 0; i < albedos.Length; i++)
                {
                    string albPath = ResolveTextureAssetPath(artRoot, item.category, albedos[i]);
                    string mShort = i < matTokens.Length
                        ? Path.GetFileNameWithoutExtension(matTokens[i])
                        : (matTokens.Length > 0 ? Path.GetFileNameWithoutExtension(matTokens[0]) : "");
                    bool useAlpha = item.alphaClip && !IsOpaqueSubMaterial(mShort);
                    if (albedoNeedsAlpha.TryGetValue(albPath, out bool prev))
                        albedoNeedsAlpha[albPath] = prev || useAlpha;
                    else
                        albedoNeedsAlpha[albPath] = useAlpha;
                }
                for (int i = 0; i < normals.Length; i++)
                {
                    normalPaths.Add(ResolveTextureAssetPath(artRoot, item.category, normals[i]));
                }
            }

            var materialCache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            int prefabCount = 0;
            int missingModels = 0;
            int missingTextures = 0;

            try
            {
                // Pass 1: Configure TextureImporters (OR-aggregated alpha so shared bark+leaf atlases keep alphaIsTransparency=true)
                AssetDatabase.StartAssetEditing();
                try
                {
                    int totalTex = albedoNeedsAlpha.Count + normalPaths.Count;
                    int tIdx = 0;
                    foreach (var kv in albedoNeedsAlpha)
                    {
                        if (tIdx % 10 == 0)
                        {
                            EditorUtility.DisplayProgressBar(
                                "Art Asset Normalizer (1/2)",
                                $"Configuring textures ({tIdx + 1}/{totalTex}): {Path.GetFileName(kv.Key)}",
                                (float)tIdx / Math.Max(1, totalTex));
                        }
                        if (!ConfigureTexture(kv.Key, isNormal: false, alphaIsTransparency: kv.Value))
                            missingTextures++;
                        tIdx++;
                    }
                    foreach (var nrmPath in normalPaths)
                    {
                        if (tIdx % 10 == 0)
                        {
                            EditorUtility.DisplayProgressBar(
                                "Art Asset Normalizer (1/2)",
                                $"Configuring normals ({tIdx + 1}/{totalTex}): {Path.GetFileName(nrmPath)}",
                                (float)tIdx / Math.Max(1, totalTex));
                        }
                        if (!ConfigureTexture(nrmPath, isNormal: true, alphaIsTransparency: false))
                            missingTextures++;
                        tIdx++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                AssetDatabase.Refresh();

                // Pass 2: Create/Update Materials and Prefabs inside an isolated PreviewScene (never dirties active scene)
                Scene previewScene = EditorSceneManager.NewPreviewScene();
                try
                {
                    int total = wrapper.items.Count;
                    for (int idx = 0; idx < total; idx++)
                    {
                        var item = wrapper.items[idx];
                        if (idx % 5 == 0)
                        {
                            EditorUtility.DisplayProgressBar(
                                "Art Asset Normalizer (2/2)",
                                $"Building Prefabs ({idx + 1}/{total}): {item.category}/{item.modelName}",
                                (float)idx / total);
                        }

                        string catRoot = $"{artRoot}/{item.category}";
                        string prefabFolder = catRoot + "/Prefabs";

                        EnsureFolder(artRoot, item.category);
                        EnsureFolder(catRoot, "Materials");
                        EnsureFolder(catRoot, "Prefabs");

                        string[] matTokens = SplitPipe(item.materialName);
                        string[] albedos = SplitPipe(item.albedoTex);
                        string[] normals = SplitPipe(item.normalTex);

                        string[] matShortNames = new string[matTokens.Length];
                        Material[] mats = new Material[matTokens.Length];
                        for (int i = 0; i < matTokens.Length; i++)
                        {
                            string matPath = ResolveMaterialAssetPath(artRoot, item.category, matTokens[i]);
                            string mShort = Path.GetFileNameWithoutExtension(matPath);
                            matShortNames[i] = mShort;
                            if (!materialCache.TryGetValue(matPath, out Material mat))
                            {
                                string matDir = Path.GetDirectoryName(matPath)?.Replace('\\', '/');
                                EnsureFolderRecursive(matDir);
                                string albTok = i < albedos.Length ? albedos[i] : (albedos.Length > 0 ? albedos[0] : "");
                                string nrmTok = i < normals.Length ? normals[i] : "";
                                bool useAlpha = item.alphaClip && !IsOpaqueSubMaterial(mShort);
                                string albPath = string.IsNullOrEmpty(albTok) ? "" : ResolveTextureAssetPath(artRoot, item.category, albTok);
                                string nrmPath = string.IsNullOrEmpty(nrmTok) ? "" : ResolveTextureAssetPath(artRoot, item.category, nrmTok);
                                mat = CreateOrUpdateMaterial(matPath, urpLit, albPath, nrmPath, useAlpha);
                                materialCache[matPath] = mat;
                            }
                            mats[i] = mat;
                        }

                        if (BuildPrefabFromModel(item.modelRelPath, item.modelName, prefabFolder, previewScene, matShortNames, mats))
                        {
                            prefabCount++;
                        }
                        else
                        {
                            missingModels++;
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(previewScene);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(string.Format(
                    "[ArtAssetNormalizer] Complete! Built/updated {0} URP Materials and {1} Unity-Ready Prefabs under {2} (missingModels={3}, missingTextures={4}).",
                    materialCache.Count, prefabCount, artRoot, missingModels, missingTextures));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Directly normalizes a single category folder (e.g. Assets/Acs/Art/Wood or Assets/Acs/Art/Coral)
        /// by scanning Models/, matching or reusing Materials/ + Textures/, and saving Unity-ready Prefabs/.
        /// Ideal for newly imported asset packs not yet registered in art_asset_manifest.json.
        /// </summary>
        public static void NormalizeCategoryFolder(string categoryFolderPath)
        {
            string categoryName = Path.GetFileName(categoryFolderPath);
            string modelsFolder = categoryFolderPath + "/Models";
            if (!AssetDatabase.IsValidFolder(modelsFolder))
            {
                Debug.LogWarning($"[ArtAssetNormalizer] No 'Models' subfolder found in '{categoryFolderPath}'.");
                return;
            }

            string texturesFolder = categoryFolderPath + "/Textures";
            string materialsFolder = categoryFolderPath + "/Materials";
            string prefabsFolder = categoryFolderPath + "/Prefabs";

            EnsureFolder(categoryFolderPath, "Materials");
            EnsureFolder(categoryFolderPath, "Prefabs");

            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { modelsFolder });
            if (modelGuids.Length == 0)
            {
                Debug.LogWarning($"[ArtAssetNormalizer] No models found in '{modelsFolder}'.");
                return;
            }

            bool defaultAlphaClip = !OpaqueCategories.Contains(categoryName);
            Shader urpLit = FindDefaultLitShader();

            var albedoTextures = new List<string>();
            var normalTextures = new List<string>();
            if (AssetDatabase.IsValidFolder(texturesFolder))
            {
                string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesFolder });
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (string guid in texGuids)
                    {
                        string texPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (IsNormalMapFileName(texPath))
                        {
                            ConfigureTexture(texPath, isNormal: true, alphaIsTransparency: false);
                            normalTextures.Add(texPath);
                        }
                        else
                        {
                            ConfigureTexture(texPath, isNormal: false, alphaIsTransparency: defaultAlphaClip);
                            albedoTextures.Add(texPath);
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
                AssetDatabase.Refresh();
            }

            // Collect any existing materials in Materials/ so packs with a pre-configured shared material keep using it
            var existingMaterials = new List<Material>();
            string[] existingMatGuids = AssetDatabase.FindAssets("t:Material", new[] { materialsFolder });
            foreach (string guid in existingMatGuids)
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null) existingMaterials.Add(m);
            }

            int builtPrefabs = 0;
            int builtMaterials = 0;
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                for (int i = 0; i < modelGuids.Length; i++)
                {
                    string modelPath = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                    string modelName = Path.GetFileNameWithoutExtension(modelPath);

                    EditorUtility.DisplayProgressBar(
                        $"Art Asset Normalizer ({categoryName})",
                        $"Normalizing ({i + 1}/{modelGuids.Length}): {modelName}",
                        (float)i / modelGuids.Length);

                    string matchedAlbedo = FindBestMatchingTexture(modelName, albedoTextures);
                    string matchedNormal = FindBestMatchingTexture(modelName, normalTextures);

                    Material resolvedMat = null;
                    if (!string.IsNullOrEmpty(matchedAlbedo))
                    {
                        string matBase = albedoTextures.Count == 1
                            ? $"M_{categoryName}_Shared"
                            : $"M_{modelName}";
                        string matPath = $"{materialsFolder}/{matBase}.mat";
                        bool existed = AssetDatabase.LoadAssetAtPath<Material>(matPath) != null;
                        resolvedMat = CreateOrUpdateMaterial(matPath, urpLit, matchedAlbedo, matchedNormal, defaultAlphaClip);
                        if (!existed) builtMaterials++;
                    }
                    else if (existingMaterials.Count == 1)
                    {
                        resolvedMat = existingMaterials[0];
                    }

                    string[] matNames = resolvedMat != null ? new[] { resolvedMat.name } : Array.Empty<string>();
                    Material[] mats = resolvedMat != null ? new[] { resolvedMat } : Array.Empty<Material>();

                    if (BuildPrefabFromModel(modelPath, modelName, prefabsFolder, previewScene, matNames, mats))
                    {
                        builtPrefabs++;
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArtAssetNormalizer] Normalized '{categoryName}': {builtPrefabs} prefabs saved to '{prefabsFolder}' (new materials created: {builtMaterials}).");
        }

        /// <summary>
        /// Audits manifest completeness AND scans every prefab across all prop categories in Assets/Acs/Art
        /// for missing meshes, missing/error materials, or abnormal import scales (< 0.08m or > 25m).
        /// </summary>
        public static void AuditAllArtAssets(string artRoot = DefaultArtRoot, string manifestPath = DefaultManifestPath)
        {
            int manifestCount = 0;
            int missingManifestPrefabs = 0;
            int missingManifestMats = 0;
            var missingByCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(manifestPath))
            {
                string rawJson = File.ReadAllText(manifestPath);
                ManifestWrapper wrapper = JsonUtility.FromJson<ManifestWrapper>("{\"items\":" + rawJson + "}");
                if (wrapper != null && wrapper.items != null)
                {
                    manifestCount = wrapper.items.Count;
                    foreach (var item in wrapper.items)
                    {
                        string prefabPath = $"{artRoot}/{item.category}/Prefabs/{item.modelName}.prefab";
                        if (!File.Exists(prefabPath))
                        {
                            missingManifestPrefabs++;
                            missingByCategory[item.category] = missingByCategory.TryGetValue(item.category, out int c) ? c + 1 : 1;
                        }

                        foreach (var mTok in SplitPipe(item.materialName))
                        {
                            string matPath = ResolveMaterialAssetPath(artRoot, item.category, mTok);
                            if (!File.Exists(matPath)) missingManifestMats++;
                        }
                    }
                }
            }

            int totalPrefabsChecked = 0;
            int missingMeshPrefabs = 0;
            int brokenMaterialPrefabs = 0;
            int abnormalScalePrefabs = 0;

            string[] categoryFolders = AssetDatabase.GetSubFolders(artRoot);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (string catFolder in categoryFolders)
                {
                    string catName = Path.GetFileName(catFolder);
                    if (NonPropCategories.Contains(catName)) continue;

                    string prefabRoot = catFolder + "/Prefabs";
                    if (!AssetDatabase.IsValidFolder(prefabRoot)) continue;

                    string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabRoot });
                    foreach (string guid in prefabGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (pf == null) continue;

                        totalPrefabsChecked++;
                        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, preview);
                        try
                        {
                            MeshFilter[] filters = inst.GetComponentsInChildren<MeshFilter>(true);
                            MeshRenderer[] renderers = inst.GetComponentsInChildren<MeshRenderer>(true);

                            bool hasValidMesh = false;
                            for (int i = 0; i < filters.Length; i++)
                            {
                                if (filters[i].sharedMesh != null && filters[i].sharedMesh.vertexCount > 0)
                                {
                                    hasValidMesh = true;
                                    break;
                                }
                            }

                            if (!hasValidMesh)
                            {
                                missingMeshPrefabs++;
                                Debug.LogWarning($"[ArtAssetNormalizer] Missing/empty mesh: {path}");
                                continue;
                            }

                            bool hasBrokenMat = false;
                            Bounds combined = new Bounds();
                            bool hasBounds = false;

                            for (int i = 0; i < renderers.Length; i++)
                            {
                                MeshRenderer r = renderers[i];
                                if (!hasBounds)
                                {
                                    combined = r.bounds;
                                    hasBounds = true;
                                }
                                else
                                {
                                    combined.Encapsulate(r.bounds);
                                }

                                Material[] mats = r.sharedMaterials;
                                if (mats == null || mats.Length == 0)
                                {
                                    hasBrokenMat = true;
                                }
                                else
                                {
                                    for (int m = 0; m < mats.Length; m++)
                                    {
                                        Material curMat = mats[m];
                                        if (curMat == null || curMat.shader == null ||
                                            curMat.shader.name.Contains("Error"))
                                        {
                                            hasBrokenMat = true;
                                            break;
                                        }
                                        bool hasAlbedo =
                                            (curMat.HasProperty("_BaseMap") && curMat.GetTexture("_BaseMap") != null) ||
                                            (curMat.HasProperty("_MainTex") && curMat.GetTexture("_MainTex") != null) ||
                                            (curMat.HasProperty("_ColorTex") && curMat.GetTexture("_ColorTex") != null);
                                        if (!hasAlbedo)
                                        {
                                            hasBrokenMat = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (hasBrokenMat)
                            {
                                brokenMaterialPrefabs++;
                                Debug.LogWarning($"[ArtAssetNormalizer] Broken/missing material slot: {path}");
                            }

                            if (hasBounds)
                            {
                                if (!ArtDioramaScaleNormalizer.CheckAgainstDioramaTolerance(catName, pf.name, combined.size, out string scaleIssue))
                                {
                                    abnormalScalePrefabs++;
                                    Debug.LogWarning($"[ArtAssetNormalizer] Diorama scale mismatch: {path} -> {scaleIssue}");
                                }
                            }
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(inst);
                        }
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            if (missingManifestPrefabs == 0 && missingManifestMats == 0 &&
                missingMeshPrefabs == 0 && brokenMaterialPrefabs == 0 && abnormalScalePrefabs == 0)
            {
                Debug.Log($"[ArtAssetNormalizer] ALL READY! Checked {totalPrefabsChecked} prefabs across {artRoot} (manifest={manifestCount}): 0 missing prefabs, 0 broken materials, 0 missing meshes, 0 abnormal bounds.");
            }
            else
            {
                var summary = new List<string>();
                foreach (var kv in missingByCategory) summary.Add($"{kv.Key}:{kv.Value}");
                Debug.LogWarning(
                    $"[ArtAssetNormalizer] Audit found issues across {totalPrefabsChecked} prefabs: " +
                    $"missingManifestPrefabs={missingManifestPrefabs} ({string.Join(", ", summary)}), " +
                    $"missingManifestMats={missingManifestMats}, missingMeshes={missingMeshPrefabs}, " +
                    $"brokenMaterials={brokenMaterialPrefabs}, abnormalScale={abnormalScalePrefabs}.");
            }
        }

        private static bool BuildPrefabFromModel(
            string modelRelPath,
            string modelName,
            string prefabFolder,
            Scene previewScene,
            string[] matNames,
            Material[] mats)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelRelPath);
            if (modelAsset == null)
            {
                Debug.LogWarning("[ArtAssetNormalizer] Missing model asset: " + modelRelPath);
                return false;
            }

            GameObject tempInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, previewScene);
            if (tempInstance == null)
            {
                Debug.LogWarning("[ArtAssetNormalizer] Could not instantiate: " + modelRelPath);
                return false;
            }

            try
            {
                tempInstance.name = modelName;
                if (mats != null && mats.Length > 0)
                {
                    AssignMaterialsToHierarchy(tempInstance, matNames, mats);
                }

                string prefabPath = prefabFolder + "/" + modelName + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempInstance);
            }
        }

        private static Shader FindDefaultLitShader()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            return urpLit != null ? urpLit : Shader.Find("Standard");
        }

        private static string GetSelectedFolderPath()
        {
            UnityEngine.Object active = Selection.activeObject;
            if (active == null) return null;
            string path = AssetDatabase.GetAssetPath(active);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/');
        }

        private static string ResolveCategoryFolder(string selectedFolder)
        {
            string leaf = Path.GetFileName(selectedFolder);
            if (string.Equals(leaf, "Models", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(leaf, "Textures", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(leaf, "Materials", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(leaf, "Prefabs", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(selectedFolder)?.Replace('\\', '/');
            }
            return selectedFolder;
        }

        private static bool IsNormalMapFileName(string texPath)
        {
            string lower = Path.GetFileNameWithoutExtension(texPath).ToLowerInvariant();
            return lower.EndsWith("_normal") || lower.EndsWith("_nrm") ||
                   lower.EndsWith("_bump") || lower.EndsWith("_n") ||
                   lower.Contains("normalmap");
        }

        private static string FindBestMatchingTexture(string modelName, List<string> candidates)
        {
            if (candidates == null || candidates.Count == 0) return "";
            if (candidates.Count == 1) return candidates[0];

            string normModel = NormalizeToken(modelName);
            for (int i = 0; i < candidates.Count; i++)
            {
                string normTex = NormalizeToken(Path.GetFileNameWithoutExtension(candidates[i]));
                if (normTex.IndexOf(normModel, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normModel.IndexOf(normTex, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidates[i];
                }
            }
            return "";
        }

        private static void AssignMaterialsToHierarchy(GameObject root, string[] matNames, Material[] mats)
        {
            if (mats == null || mats.Length == 0) return;

            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int rIdx = 0; rIdx < renderers.Length; rIdx++)
            {
                var r = renderers[rIdx];
                int subCount = 1;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    subCount = Mathf.Max(1, mf.sharedMesh.subMeshCount);
                }
                else if (r.sharedMaterials != null && r.sharedMaterials.Length > 0)
                {
                    subCount = r.sharedMaterials.Length;
                }

                if (mats.Length == 1)
                {
                    if (subCount == 1)
                    {
                        r.sharedMaterial = mats[0];
                    }
                    else
                    {
                        Material[] arr = new Material[subCount];
                        for (int s = 0; s < subCount; s++) arr[s] = mats[0];
                        r.sharedMaterials = arr;
                    }
                    continue;
                }

                // Multi-material asset: supports both single-renderer multi-submesh and multi-renderer groups
                Material[] existing = r.sharedMaterials ?? Array.Empty<Material>();
                Material[] assigned = new Material[subCount];
                for (int s = 0; s < subCount; s++)
                {
                    string existingMatName = (s < existing.Length && existing[s] != null) ? existing[s].name : "";
                    int fallbackIdx = subCount > 1 ? s : rIdx;
                    assigned[s] = ResolveMaterialForSlot(existingMatName, r.gameObject.name, matNames, mats, fallbackIdx);
                }
                r.sharedMaterials = assigned;
            }
        }

        private static Material ResolveMaterialForSlot(string existingMatName, string rendererName, string[] matNames, Material[] mats, int fallbackIdx)
        {
            if (!string.IsNullOrEmpty(existingMatName))
            {
                for (int i = 0; i < matNames.Length; i++)
                {
                    if (string.Equals(existingMatName, matNames[i], StringComparison.OrdinalIgnoreCase))
                        return mats[i];
                }
            }

            if (!string.IsNullOrEmpty(rendererName))
            {
                for (int i = 0; i < matNames.Length; i++)
                {
                    if (string.Equals(rendererName, matNames[i], StringComparison.OrdinalIgnoreCase))
                        return mats[i];
                }
            }

            string normMat = NormalizeToken(existingMatName);
            string normRend = NormalizeToken(rendererName);
            for (int i = 0; i < matNames.Length; i++)
            {
                string normTarget = NormalizeToken(matNames[i]);
                if (!string.IsNullOrEmpty(normMat) &&
                    (normTarget.EndsWith(normMat, StringComparison.OrdinalIgnoreCase) ||
                     normMat.EndsWith(normTarget, StringComparison.OrdinalIgnoreCase)))
                {
                    return mats[i];
                }
                if (!string.IsNullOrEmpty(normRend) &&
                    normRend.Length >= 3 &&
                    normTarget.EndsWith(normRend, StringComparison.OrdinalIgnoreCase))
                {
                    return mats[i];
                }
            }

            bool slotIsOpaque = IsOpaqueSubMaterial(existingMatName) || IsOpaqueSubMaterial(rendererName);
            if (slotIsOpaque)
            {
                for (int i = 0; i < matNames.Length; i++)
                {
                    if (IsOpaqueSubMaterial(matNames[i])) return mats[i];
                }
            }
            else if (ContainsFoliageHint(existingMatName) || ContainsFoliageHint(rendererName))
            {
                for (int i = 0; i < matNames.Length; i++)
                {
                    if (!IsOpaqueSubMaterial(matNames[i])) return mats[i];
                }
            }

            return mats[Mathf.Clamp(fallbackIdx, 0, mats.Length - 1)];
        }

        private static string NormalizeToken(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = raw.Trim();
            if (s.EndsWith(" (Instance)", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - " (Instance)".Length);
            if (s.EndsWith("_Mat", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - "_Mat".Length);
            if (s.StartsWith("M_", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);
            return s.Replace("_", "");
        }

        private static bool IsOpaqueSubMaterial(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Trunk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Bark", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsFoliageHint(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Leaf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Leaves", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Frond", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Plant", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string[] SplitPipe(string value)
        {
            return (value ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool ConfigureTexture(string assetPath, bool isNormal, bool alphaIsTransparency)
        {
            if (!File.Exists(assetPath))
            {
                Debug.LogWarning("[ArtAssetNormalizer] Missing texture file: " + assetPath);
                return false;
            }
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return false;

            bool dirty = false;
            if (isNormal)
            {
                if (ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }
            }
            else
            {
                if (ti.textureType != TextureImporterType.Default)
                {
                    ti.textureType = TextureImporterType.Default;
                    dirty = true;
                }
                if (!ti.sRGBTexture)
                {
                    ti.sRGBTexture = true;
                    dirty = true;
                }
                if (ti.alphaIsTransparency != alphaIsTransparency)
                {
                    ti.alphaIsTransparency = alphaIsTransparency;
                    dirty = true;
                }
            }

            if (dirty)
            {
                ti.SaveAndReimport();
            }
            return true;
        }

        private static Material CreateOrUpdateMaterial(string matPath, Shader shader, string albedoPath, string normalPath, bool alphaClip)
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

            Texture2D albedo = !string.IsNullOrEmpty(albedoPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath) : null;
            Texture2D normal = !string.IsNullOrEmpty(normalPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null;

            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTexture("_MainTex", albedo);
            }
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.SetTexture("_BumpMap", null);
                mat.DisableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0.0f);

            if (alphaClip)
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.35f);
                mat.SetFloat("_Cull", 0f); // Render both sides for foliage cards
                mat.SetFloat("_AlphaToMask", 1f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
                mat.doubleSidedGI = true;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_Cull", 2f); // Back-face culling for solid rocks/wood/trunks
                mat.SetFloat("_AlphaToMask", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = -1;
                mat.doubleSidedGI = false;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static string ResolveMaterialAssetPath(string artRoot, string category, string matToken)
        {
            if (string.IsNullOrEmpty(matToken)) return "";
            string norm = matToken.Replace('\\', '/').TrimStart('/');
            if (norm.Contains("/"))
            {
                string withExt = norm.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ? norm : norm + ".mat";
                return norm.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ? withExt : $"{artRoot}/{withExt}";
            }
            string file = norm.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ? norm : norm + ".mat";
            return $"{artRoot}/{category}/Materials/{file}";
        }

        private static string ResolveTextureAssetPath(string artRoot, string category, string texToken)
        {
            if (string.IsNullOrEmpty(texToken)) return "";
            string norm = texToken.Replace('\\', '/').TrimStart('/');
            if (norm.Contains("/"))
            {
                return norm.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ? norm : $"{artRoot}/{norm}";
            }
            return $"{artRoot}/{category}/Textures/{norm}";
        }

        private static void EnsureFolderRecursive(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderRecursive(parent);
            }
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
#endif

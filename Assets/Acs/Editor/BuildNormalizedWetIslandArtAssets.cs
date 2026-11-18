#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.EditorTools
{
    public static class BuildNormalizedWetIslandArtAssets
    {
        [Serializable]
        private class ManifestEntry
        {
            public string category;
            public string modelName;
            public string modelRelPath;
            public string materialName;
            public string albedoTex;
            public string normalTex;
            public bool alphaClip;
            public string sourcePack;
        }

        [Serializable]
        private class ManifestWrapper
        {
            public List<ManifestEntry> items;
        }

        private const string ManifestAssetPath = "Assets/Acs/Art/wetisland_art_manifest.json";

        [MenuItem("LumiWorld/WetIsland/4. Build All Normalized Art Prefabs & Materials")]
        public static void BuildAllFromManifest()
        {
            if (!File.Exists(ManifestAssetPath))
            {
                Debug.LogWarning("[WetIslandArtBuilder] Manifest not found at " + ManifestAssetPath);
                return;
            }

            AssetDatabase.Refresh();

            string rawJson = File.ReadAllText(ManifestAssetPath);
            string wrappedJson = "{\"items\":" + rawJson + "}";
            ManifestWrapper wrapper = JsonUtility.FromJson<ManifestWrapper>(wrappedJson);
            if (wrapper == null || wrapper.items == null || wrapper.items.Count == 0)
            {
                Debug.LogWarning("[WetIslandArtBuilder] Manifest is empty or invalid.");
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            var configuredTextures = new HashSet<string>();
            var materialCache = new Dictionary<string, Material>();
            int prefabCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                // Pass 1: Configure TextureImporters
                foreach (var item in wrapper.items)
                {
                    string texFolder = "Assets/Acs/Art/" + item.category + "/Textures";
                    string[] albedos = (item.albedoTex ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    string[] normals = (item.normalTex ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < albedos.Length; i++)
                    {
                        string albPath = texFolder + "/" + albedos[i];
                        if (configuredTextures.Add(albPath))
                        {
                            ConfigureTexture(albPath, false, item.alphaClip);
                        }
                    }
                    for (int i = 0; i < normals.Length; i++)
                    {
                        string nrmPath = texFolder + "/" + normals[i];
                        if (configuredTextures.Add(nrmPath))
                        {
                            ConfigureTexture(nrmPath, true, false);
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();

            // Pass 2: Create/Update Materials and Prefabs
            foreach (var item in wrapper.items)
            {
                string catRoot = "Assets/Acs/Art/" + item.category;
                string texFolder = catRoot + "/Textures";
                string matFolder = catRoot + "/Materials";
                string prefabFolder = catRoot + "/Prefabs";

                EnsureFolder("Assets/Acs/Art", item.category);
                EnsureFolder(catRoot, "Materials");
                EnsureFolder(catRoot, "Prefabs");

                string[] matNames = (item.materialName ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                string[] albedos = (item.albedoTex ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                string[] normals = (item.normalTex ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                Material[] mats = new Material[matNames.Length];
                for (int i = 0; i < matNames.Length; i++)
                {
                    string mName = matNames[i];
                    string matPath = matFolder + "/" + mName + ".mat";
                    Material mat;
                    if (!materialCache.TryGetValue(matPath, out mat))
                    {
                        string albFile = i < albedos.Length ? albedos[i] : (albedos.Length > 0 ? albedos[0] : "");
                        string nrmFile = i < normals.Length ? normals[i] : "";
                        bool useAlpha = item.alphaClip && !mName.Contains("Trunk") && !mName.Contains("Bark");
                        mat = CreateOrUpdateMaterial(matPath, urpLit, texFolder + "/" + albFile, string.IsNullOrEmpty(nrmFile) ? "" : (texFolder + "/" + nrmFile), useAlpha);
                        materialCache[matPath] = mat;
                    }
                    mats[i] = mat;
                }

                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(item.modelRelPath);
                if (modelAsset == null)
                {
                    Debug.LogWarning("[WetIslandArtBuilder] Missing model asset: " + item.modelRelPath);
                    continue;
                }

                GameObject tempInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                tempInstance.name = item.modelName;

                var renderers = tempInstance.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers)
                {
                    if (mats.Length == 1)
                    {
                        int subCount = 1;
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null) subCount = Mathf.Max(1, mf.sharedMesh.subMeshCount);
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
                    }
                    else if (mats.Length > 1)
                    {
                        r.sharedMaterials = mats;
                    }
                }

                string prefabPath = prefabFolder + "/" + item.modelName + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
                UnityEngine.Object.DestroyImmediate(tempInstance);
                prefabCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(string.Format("[WetIslandArtBuilder] Built {0} Materials and {1} Unity-Ready Prefabs across Assets/Acs/Art categories!", materialCache.Count, prefabCount));
        }

        private static void ConfigureTexture(string assetPath, bool isNormal, bool alphaIsTransparency)
        {
            if (!File.Exists(assetPath)) return;
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return;

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
                mat.DisableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0.0f);

            if (alphaClip)
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.33f);
                mat.SetFloat("_Cull", 0f); // Render Both Sides for Foliage Cards
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_Cull", 2f); // Back-face culling for solid rocks/trunks
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = -1;
            }

            EditorUtility.SetDirty(mat);
            return mat;
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

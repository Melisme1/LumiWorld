#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal static class BuildLotusAssets
{
    private const string Root = "Assets/Acs/Art/Lotus";

    [MenuItem("LumiWorld/Assets/Rebuild Lotus Prefabs")]
    public static void Build()
    {
        var texturePath = Root + "/Textures/Lotus_Color_Alpha.png";
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (textureImporter == null)
            throw new InvalidOperationException("Lotus atlas was not imported as a texture.");
        textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
        textureImporter.alphaIsTransparency = true;
        textureImporter.sRGBTexture = true;
        textureImporter.mipmapEnabled = true;
        textureImporter.maxTextureSize = 2048;
        textureImporter.wrapMode = TextureWrapMode.Clamp;
        textureImporter.SaveAndReimport();

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (texture == null || shader == null)
            throw new InvalidOperationException("Lotus texture or URP Lit shader is unavailable.");

        var materialPath = Root + "/Materials/M_Lotus_Atlas.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.45f);
        material.SetFloat("_Cull", 0f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.2f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);

        var modelFiles = Directory.GetFiles(Root + "/Models", "*.fbx");
        Array.Sort(modelFiles, StringComparer.Ordinal);
        if (modelFiles.Length != 24)
            throw new InvalidOperationException("Expected 24 Lotus FBX files, found " + modelFiles.Length);

        foreach (var modelFile in modelFiles)
        {
            var modelPath = modelFile.Replace('\\', '/');
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                throw new InvalidOperationException("Could not import " + modelPath);
            var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate " + modelPath);
            try
            {
                instance.name = Path.GetFileNameWithoutExtension(modelPath);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("No mesh renderer in " + modelPath);
                foreach (var renderer in renderers)
                {
                    var slots = renderer.sharedMaterials;
                    if (slots.Length == 0)
                        slots = new Material[1];
                    for (var i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                }
                var prefabPath = Root + "/Prefabs/" + instance.name + ".prefab";
                if (PrefabUtility.SaveAsPrefabAsset(instance, prefabPath) == null)
                    throw new InvalidOperationException("Could not create " + prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("LOTUS_BUILD_COMPLETE: 24 model prefabs and one URP alpha-clipped material.");
    }
}
#endif

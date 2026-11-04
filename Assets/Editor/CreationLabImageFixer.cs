#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Editor tool to remove dark backgrounds from biome icon PNGs and fix tube sprites.
/// Menu: LumiWorld > Fix Creation Lab Images
/// </summary>
public class CreationLabImageFixer : EditorWindow
{
    [MenuItem("LumiWorld/Fix Creation Lab Images")]
    public static void RunFix()
    {
        // 1. Fix biome icons - remove dark backgrounds
        string iconDir = "Assets/Project/Resources/UI_Icon_card";
        string[] biomeIcons = {
            "Bloomfield.png", "Leafwood.png", "Lilywater.png", "Rainleaf.png",
            "Reedmarsh.png", "Streamstone.png", "Windheath.png"
        };

        int fixedIcons = 0;
        foreach (var iconName in biomeIcons)
        {
            string assetPath = $"{iconDir}/{iconName}";
            string fullPath = Path.GetFullPath(assetPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[ImageFixer] Missing: {assetPath}");
                continue;
            }

            // Make texture readable first
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            bool wasReadable = ti.isReadable;
            TextureImporterType origType = ti.textureType;
            if (!wasReadable || ti.textureType != TextureImporterType.Default)
            {
                ti.isReadable = true;
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                Debug.LogWarning($"[ImageFixer] Could not load: {assetPath}");
                continue;
            }

            // Flood-fill BFS from borders to remove dark background
            Color[] pixels = tex.GetPixels();
            int w = tex.width, h = tex.height;
            bool[] bgMask = RemoveDarkBgBFS(pixels, w, h, darkThresh: 0.16f, tolerance: 0.28f);

            // Apply mask
            for (int i = 0; i < pixels.Length; i++)
            {
                if (bgMask[i])
                {
                    pixels[i] = Color.clear;
                }
                else
                {
                    // Soft feather at border
                    int x = i % w, y = i / w;
                    int adjBg = 0;
                    int[] dx = { -1, 1, 0, 0 };
                    int[] dy = { 0, 0, -1, 1 };
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + dx[d], ny = y + dy[d];
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && bgMask[ny * w + nx])
                            adjBg++;
                    }
                    if (adjBg > 0)
                    {
                        Color c = pixels[i];
                        c.a = Mathf.Max(0f, c.a - adjBg * 0.1f);
                        pixels[i] = c;
                    }
                }
            }

            Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels(pixels);
            outTex.Apply();

            byte[] pngBytes = outTex.EncodeToPNG();
            File.WriteAllBytes(fullPath, pngBytes);
            Object.DestroyImmediate(outTex);

            // Restore as Sprite with transparency
            ti.isReadable = false;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            fixedIcons++;
            Debug.Log($"[ImageFixer] Fixed: {iconName}");
        }

        // 2. Re-import tube images as Sprite with transparency
        string[] tubeAssets = {
            "Assets/Project/Resources/UI_Breeding/tube_active.png",
            "Assets/Project/Resources/UI_Breeding/tube_locked.png"
        };
        foreach (var tubePath in tubeAssets)
        {
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(tubePath);
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.isReadable = false;
                AssetDatabase.ImportAsset(tubePath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[ImageFixer] Re-imported tube: {tubePath}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("LumiWorld Image Fixer",
            $"Done!\n- Fixed {fixedIcons} biome icon backgrounds\n- Re-imported 2 tube sprites",
            "OK");
    }

    private static bool[] RemoveDarkBgBFS(Color[] pixels, int w, int h, float darkThresh, float tolerance)
    {
        bool[] bgMask = new bool[w * h];
        bool[] visited = new bool[w * h];
        Queue<int> queue = new Queue<int>();

        float Brightness(int idx) {
            Color c = pixels[idx];
            return c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        }

        // Seed border pixels
        for (int x = 0; x < w; x++)
        {
            int bot = 0 * w + x;
            int top = (h - 1) * w + x;
            if (Brightness(bot) < darkThresh && !visited[bot]) { visited[bot] = true; queue.Enqueue(bot); }
            if (Brightness(top) < darkThresh && !visited[top]) { visited[top] = true; queue.Enqueue(top); }
        }
        for (int y = 0; y < h; y++)
        {
            int left = y * w + 0;
            int right = y * w + (w - 1);
            if (Brightness(left) < darkThresh && !visited[left]) { visited[left] = true; queue.Enqueue(left); }
            if (Brightness(right) < darkThresh && !visited[right]) { visited[right] = true; queue.Enqueue(right); }
        }

        int[] dxArr = { -1, 1, 0, 0 };
        int[] dyArr = { 0, 0, -1, 1 };

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            bgMask[idx] = true;
            int cx = idx % w, cy = idx / w;
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dxArr[d], ny = cy + dyArr[d];
                if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                {
                    int ni = ny * w + nx;
                    if (!visited[ni] && Brightness(ni) < tolerance)
                    {
                        visited[ni] = true;
                        queue.Enqueue(ni);
                    }
                }
            }
        }
        return bgMask;
    }
}
#endif

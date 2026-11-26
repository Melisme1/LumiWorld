using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.Acs.Editor
{
    /// <summary>
    /// Pure Unity RGBA32 card compositor shared by CardArtBuilderWindow and Editor scripts.
    /// Matches docs/tools/Build-CreatureCards.ps1 and Build-LumiCards.ps1 pixel geometry:
    /// 1024x1536 card, 240x276 uniform Affinity crests (0-3), soft drop shadow, optional
    /// Wet aqua hairline, and minimal Silver (Epic) / Gold (Legend) frames.
    /// </summary>
    public static class CardArtCompositorUtility
    {
        public const int CardW = 1024;
        public const int CardH = 1536;
        public const int FrameBleedPad = 44;
        public const int FrameFullW = CardW + FrameBleedPad * 2;
        public const int FrameFullH = CardH + FrameBleedPad * 2;
        public const float CardCornerRadiusPx = 74f;
        public const float CardCornerInsetPx = 6f;
        public const int BadgeW = 240;
        public const int BadgeH = 276;
        public const int BadgeX = 46;
        public const int StartYFromTop = 52;
        public const int GapYDefault = 14;
        public const int GapYTriple = 8;
        public const int ShadowPad = 16;
        public const int ShadowRadius = 5;
        public const float ShadowMaxAlpha = 95f / 255f;
        public const int ShadowOffsetYDown = 4;

        public const string BadgesAssetFolder = "Assets/Acs/UI Cards/AffinityBadges";
        public const string FramesAssetFolder = "Assets/Project/Resources/CardFrames";

        public static readonly string[] AllAffinities =
        {
            "Leafwood",
            "Bloomfield",
            "Windheath",
            "Reedmarsh",
            "Lilywater",
            "Streamstone",
            "Rainleaf"
        };

        public static string AssetPathToFullPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return string.Empty;
            if (Path.IsPathRooted(assetPath)) return assetPath;
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot ?? string.Empty, assetPath);
        }

        public static Texture2D LoadRawImageFile(string assetOrDiskPath)
        {
            string fullPath = AssetPathToFullPath(assetOrDiskPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return null;

            byte[] bytes = File.ReadAllBytes(fullPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!tex.LoadImage(bytes, markNonReadable: false))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return null;
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        public static Texture2D LoadReadableSourceTexture(Texture2D sourceAsset, string diskPathOverride = null)
        {
            if (!string.IsNullOrEmpty(diskPathOverride) && File.Exists(diskPathOverride))
            {
                return LoadRawImageFile(diskPathOverride);
            }

            if (sourceAsset == null) return null;

            string assetPath = AssetDatabase.GetAssetPath(sourceAsset);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string ext = Path.GetExtension(assetPath).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    Texture2D raw = LoadRawImageFile(assetPath);
                    if (raw != null) return raw;
                }
            }

            return CopyResizedReadable(sourceAsset, sourceAsset.width, sourceAsset.height);
        }

        public static Texture2D CopyResizedReadable(Texture2D src, int dstW, int dstH)
        {
            if (src == null) return null;
            RenderTexture prev = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(dstW, dstH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var result = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false, false);
                result.ReadPixels(new Rect(0, 0, dstW, dstH), 0, 0, false);
                result.Apply(false, false);
                return result;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        public static Texture2D LoadRarityFrameTexture(CardRarity rarity)
        {
            if (rarity == CardRarity.Common) return null;
            string frameFile = rarity == CardRarity.Legend
                ? $"{FramesAssetFolder}/Frame_Legend_Gold.png"
                : $"{FramesAssetFolder}/Frame_Epic_Silver.png";
            return LoadRawImageFile(frameFile);
        }

        public static void ApplyRoundedCornerAlpha(Texture2D tex, float radius = CardCornerRadiusPx, float inset = CardCornerInsetPx)
        {
            if (tex == null) return;
            Color32[] px = tex.GetPixels32();
            ApplyRoundedCornerAlpha(px, tex.width, tex.height, radius, inset);
            tex.SetPixels32(px);
            tex.Apply(false, false);
        }

        public static void ApplyRoundedCornerAlpha(Color32[] canvas, int w, int h, float radius = CardCornerRadiusPx, float inset = CardCornerInsetPx)
        {
            if (canvas == null || canvas.Length < w * h) return;

            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float bx = Mathf.Max(0f, halfW - inset - radius);
            float by = Mathf.Max(0f, halfH - inset - radius);

            for (int y = 0; y < h; y++)
            {
                float dy = Mathf.Abs((y + 0.5f) - halfH) - by;
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (x > inset + radius && x < w - inset - radius && y > inset + radius && y < h - inset - radius) continue;

                    float dx = Mathf.Abs((x + 0.5f) - halfW) - bx;
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                    float sdf = outside + inside - radius;
                    float keep = Mathf.Clamp01(0.5f - sdf);
                    if (keep >= 0.999f) continue;

                    int idx = row + x;
                    Color32 c = canvas[idx];
                    c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * keep), 0, 255);
                    if (c.a == 0)
                    {
                        c = new Color32(0, 0, 0, 0);
                    }
                    canvas[idx] = c;
                }
            }
        }

        public static Texture2D CompositeCard(
            Texture2D readableSource,
            IReadOnlyList<string> affinities,
            CardRarity rarity,
            bool applyRarityFrame,
            bool wetAquaHairline,
            bool previewTerraformArrows)
        {
            if (readableSource == null)
                throw new ArgumentNullException(nameof(readableSource));

            Texture2D sizedBase = (readableSource.width == CardW && readableSource.height == CardH)
                ? readableSource
                : CopyResizedReadable(readableSource, CardW, CardH);

            try
            {
                Color32[] canvas = sizedBase.GetPixels32();

                if (wetAquaHairline)
                {
                    DrawWetAquaHairline(canvas);
                }

                if (previewTerraformArrows)
                {
                    BlitArrowPreview(canvas, $"{FramesAssetFolder}/Arrow_Raise.png", 0.045f, 0.245f, 0.42f, 0.58f);
                    BlitArrowPreview(canvas, $"{FramesAssetFolder}/Arrow_Lower.png", 0.755f, 0.955f, 0.42f, 0.58f);
                }

                if (affinities != null && affinities.Count > 0)
                {
                    var validAffinities = new List<string>(3);
                    for (int i = 0; i < affinities.Count && validAffinities.Count < 3; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(affinities[i]))
                            validAffinities.Add(affinities[i].Trim());
                    }

                    int gapY = validAffinities.Count >= 3 ? GapYTriple : GapYDefault;
                    for (int i = 0; i < validAffinities.Count; i++)
                    {
                        string badgePath = $"{BadgesAssetFolder}/{validAffinities[i]}.png";
                        Texture2D crestTex = LoadRawImageFile(badgePath);
                        if (crestTex == null) continue;
                        try
                        {
                            Texture2D sizedCrest = (crestTex.width == BadgeW && crestTex.height == BadgeH)
                                ? crestTex
                                : CopyResizedReadable(crestTex, BadgeW, BadgeH);
                            try
                            {
                                Color32[] crestPx = sizedCrest.GetPixels32();
                                int topY = StartYFromTop + i * (BadgeH + gapY);
                                int dstBottomY = CardH - topY - BadgeH;

                                Color32[] shadowPx = BuildSoftShadow(crestPx, BadgeW, BadgeH, out int sw, out int sh);
                                BlitPixels(canvas, CardW, CardH, shadowPx, sw, sh, BadgeX - ShadowPad, dstBottomY - ShadowPad - ShadowOffsetYDown);
                                BlitPixels(canvas, CardW, CardH, crestPx, BadgeW, BadgeH, BadgeX, dstBottomY);
                            }
                            finally
                            {
                                if (sizedCrest != crestTex) UnityEngine.Object.DestroyImmediate(sizedCrest);
                            }
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(crestTex);
                        }
                    }
                }

                if (applyRarityFrame && rarity != CardRarity.Common)
                {
                    Texture2D frameTex = LoadRarityFrameTexture(rarity);
                    if (frameTex != null)
                    {
                        try
                        {
                            canvas = BakeOuterFrameWithBleed(canvas, frameTex);
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(frameTex);
                        }
                    }
                }

                var output = new Texture2D(CardW, CardH, TextureFormat.RGBA32, false, false);
                output.SetPixels32(canvas);
                output.Apply(false, false);
                output.wrapMode = TextureWrapMode.Clamp;
                output.filterMode = FilterMode.Bilinear;
                return output;
            }
            finally
            {
                if (sizedBase != readableSource) UnityEngine.Object.DestroyImmediate(sizedBase);
            }
        }

        private static Color32[] BakeOuterFrameWithBleed(Color32[] cardCanvas, Texture2D frameTex)
        {
            ApplyRoundedCornerAlpha(cardCanvas, CardW, CardH, CardCornerRadiusPx, CardCornerInsetPx);

            Color32[] outCanvas = new Color32[CardW * CardH];
            int fw = frameTex.width;
            int fh = frameTex.height;
            Color32[] framePx = frameTex.GetPixels32();

            // Outer frame assets (1112x1624) include 44px diagonal leaf bleed around the 1024x1536 card box.
            // Scale card artwork proportionately into the inner rect on transparent corners so baked PNGs keep 100% of leaf tips unclipped.
            int innerW = fw > CardW ? Mathf.RoundToInt(CardW * (float)CardW / fw) : CardW;
            int innerH = fh > CardH ? Mathf.RoundToInt(CardH * (float)CardH / fh) : CardH;
            int ox = (CardW - innerW) / 2;
            int oy = (CardH - innerH) / 2;

            ResampleBilinearStraightAlpha(cardCanvas, CardW, CardH, outCanvas, CardW, CardH, ox, oy, innerW, innerH);

            Color32[] sizedFrame = (fw == CardW && fh == CardH) ? framePx : ResampleCanvasBilinear(framePx, fw, fh, CardW, CardH);
            for (int i = 0; i < outCanvas.Length; i++)
            {
                BlendOver(ref outCanvas[i], sizedFrame[i]);
            }

            return outCanvas;
        }

        private static Color32[] ResampleCanvasBilinear(Color32[] src, int sw, int sh, int dw, int dh)
        {
            Color32[] dst = new Color32[dw * dh];
            ResampleBilinearStraightAlpha(src, sw, sh, dst, dw, dh, 0, 0, dw, dh);
            return dst;
        }

        private static void ResampleBilinearStraightAlpha(
            Color32[] src, int sw, int sh,
            Color32[] dst, int dw, int dh,
            int dstX0, int dstY0, int dstW, int dstH)
        {
            float scaleX = (float)sw / dstW;
            float scaleY = (float)sh / dstH;

            for (int y = 0; y < dstH; y++)
            {
                int dy = dstY0 + y;
                if (dy < 0 || dy >= dh) continue;

                float sy = Mathf.Clamp((y + 0.5f) * scaleY - 0.5f, 0f, sh - 1f);
                int y0 = Mathf.FloorToInt(sy);
                int y1 = Mathf.Min(y0 + 1, sh - 1);
                float fy = sy - y0;

                int dRow = dy * dw;
                for (int x = 0; x < dstW; x++)
                {
                    int dx = dstX0 + x;
                    if (dx < 0 || dx >= dw) continue;

                    float sx = Mathf.Clamp((x + 0.5f) * scaleX - 0.5f, 0f, sw - 1f);
                    int x0 = Mathf.FloorToInt(sx);
                    int x1 = Mathf.Min(x0 + 1, sw - 1);
                    float fx = sx - x0;

                    Color32 c00 = src[y0 * sw + x0];
                    Color32 c10 = src[y0 * sw + x1];
                    Color32 c01 = src[y1 * sw + x0];
                    Color32 c11 = src[y1 * sw + x1];

                    float w00 = (1f - fx) * (1f - fy);
                    float w10 = fx * (1f - fy);
                    float w01 = (1f - fx) * fy;
                    float w11 = fx * fy;

                    float a00 = c00.a / 255f, a10 = c10.a / 255f, a01 = c01.a / 255f, a11 = c11.a / 255f;
                    float a = a00 * w00 + a10 * w10 + a01 * w01 + a11 * w11;
                    if (a <= 0.001f)
                    {
                        dst[dRow + dx] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float pr = c00.r * a00 * w00 + c10.r * a10 * w10 + c01.r * a01 * w01 + c11.r * a11 * w11;
                    float pg = c00.g * a00 * w00 + c10.g * a10 * w10 + c01.g * a01 * w01 + c11.g * a11 * w11;
                    float pb = c00.b * a00 * w00 + c10.b * a10 * w10 + c01.b * a01 * w01 + c11.b * a11 * w11;

                    dst[dRow + dx] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(pr / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(pg / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(pb / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            }
        }

        public static Sprite ExportPngAndConfigureSprite(
            Texture2D composedTexture,
            string assetPath,
            CardData targetCard = null,
            CardRarity? assignRarity = null)
        {
            if (composedTexture == null) throw new ArgumentNullException(nameof(composedTexture));
            if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("Asset path is empty.", nameof(assetPath));

            string fullPath = AssetPathToFullPath(assetPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            byte[] pngBytes = composedTexture.EncodeToPNG();
            File.WriteAllBytes(fullPath, pngBytes);

            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (!importer.sRGBTexture) { importer.sRGBTexture = true; changed = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                if (importer.maxTextureSize != 2048) { importer.maxTextureSize = 2048; changed = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                if (changed) importer.SaveAndReimport();
            }

            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (targetCard != null && sp != null)
            {
                Undo.RecordObject(targetCard, "Assign Generated Card Art");
                targetCard.cardImage = sp;
                if (assignRarity.HasValue)
                {
                    targetCard.rarity = assignRarity.Value;
                }
                EditorUtility.SetDirty(targetCard);
                AssetDatabase.SaveAssets();
            }

            return sp;
        }

        private static void BlitArrowPreview(Color32[] canvas, string arrowPath, float minX, float maxX, float minY, float maxY)
        {
            Texture2D raw = LoadRawImageFile(arrowPath);
            if (raw == null) return;
            try
            {
                int boxW = Mathf.RoundToInt((maxX - minX) * CardW);
                int boxH = Mathf.RoundToInt((maxY - minY) * CardH);
                int size = Mathf.Min(boxW, boxH);
                int dstX = Mathf.RoundToInt(minX * CardW) + (boxW - size) / 2;
                int dstY = Mathf.RoundToInt(minY * CardH) + (boxH - size) / 2;

                Texture2D resized = CopyResizedReadable(raw, size, size);
                try
                {
                    BlitPixels(canvas, CardW, CardH, resized.GetPixels32(), size, size, dstX, dstY);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(resized);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(raw);
            }
        }

        private static void DrawWetAquaHairline(Color32[] canvas)
        {
            const float inset = 24f;
            const float radius = 60f;
            const float halfStroke = 2.5f;
            const float halfW = CardW * 0.5f;
            const float halfH = CardH * 0.5f;
            float bx = halfW - inset - radius;
            float by = halfH - inset - radius;

            for (int y = 0; y < CardH; y++)
            {
                float dy = Mathf.Abs((y + 0.5f) - halfH) - by;
                int row = y * CardW;
                for (int x = 0; x < CardW; x++)
                {
                    if (x > 100 && x < CardW - 100 && y > 100 && y < CardH - 100) continue;
                    float dx = Mathf.Abs((x + 0.5f) - halfW) - bx;
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                    float sdf = outside + inside - radius;
                    float dist = Mathf.Abs(sdf);
                    float aa = Mathf.Clamp01(halfStroke + 0.75f - dist);
                    if (aa <= 0.005f) continue;

                    byte a = (byte)Mathf.RoundToInt(aa * 140f);
                    BlendOver(ref canvas[row + x], new Color32(108, 206, 204, a));
                }
            }
        }

        private static Color32[] BuildSoftShadow(Color32[] src, int w, int h, out int pw, out int ph)
        {
            pw = w + ShadowPad * 2;
            ph = h + ShadowPad * 2;
            float[] mask = new float[pw * ph];

            for (int y = 0; y < h; y++)
            {
                int srcRow = y * w;
                int dstRow = (y + ShadowPad) * pw + ShadowPad;
                for (int x = 0; x < w; x++)
                {
                    mask[dstRow + x] = src[srcRow + x].a / 255f;
                }
            }

            float[] tmp = new float[pw * ph];
            BoxBlurPass(mask, tmp, pw, ph, ShadowRadius);
            BoxBlurPass(tmp, mask, pw, ph, ShadowRadius);

            Color32[] result = new Color32[pw * ph];
            for (int i = 0; i < result.Length; i++)
            {
                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(mask[i] * ShadowMaxAlpha * 255f), 0, 255);
                result[i] = new Color32(26, 18, 10, a);
            }
            return result;
        }

        private static void BoxBlurPass(float[] src, float[] dst, int w, int h, int radius)
        {
            float[] horiz = new float[w * h];
            float inv = 1f / (radius * 2 + 1);

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float sum = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    sum += src[row + Mathf.Clamp(k, 0, w - 1)];
                }
                horiz[row] = sum * inv;
                for (int x = 1; x < w; x++)
                {
                    sum += src[row + Mathf.Min(w - 1, x + radius)] - src[row + Mathf.Max(0, x - radius - 1)];
                    horiz[row + x] = sum * inv;
                }
            }

            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    sum += horiz[Mathf.Clamp(k, 0, h - 1) * w + x];
                }
                dst[x] = sum * inv;
                for (int y = 1; y < h; y++)
                {
                    sum += horiz[Mathf.Min(h - 1, y + radius) * w + x] - horiz[Mathf.Max(0, y - radius - 1) * w + x];
                    dst[y * w + x] = sum * inv;
                }
            }
        }

        private static void BlitPixels(Color32[] dst, int dstW, int dstH, Color32[] src, int srcW, int srcH, int offsetX, int offsetY)
        {
            for (int sy = 0; sy < srcH; sy++)
            {
                int dy = offsetY + sy;
                if (dy < 0 || dy >= dstH) continue;
                int sRow = sy * srcW;
                int dRow = dy * dstW;
                for (int sx = 0; sx < srcW; sx++)
                {
                    int dx = offsetX + sx;
                    if (dx < 0 || dx >= dstW) continue;
                    BlendOver(ref dst[dRow + dx], src[sRow + sx]);
                }
            }
        }

        private static void BlendOver(ref Color32 dst, Color32 src)
        {
            if (src.a == 0) return;
            if (src.a == 255)
            {
                dst = src;
                return;
            }
            float sa = src.a / 255f;
            float da = dst.a / 255f;
            float invSa = 1f - sa;
            float outA = sa + da * invSa;
            if (outA <= 0.0001f)
            {
                dst = new Color32(0, 0, 0, 0);
                return;
            }
            float invOut = 1f / outA;
            dst = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((src.r * sa + dst.r * da * invSa) * invOut), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((src.g * sa + dst.g * da * invSa) * invOut), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((src.b * sa + dst.b * da * invSa) * invOut), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255));
        }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace LumiWorld.Acs
{
    /// <summary>
    /// Shared presentation tokens, 3D soft-elevation helpers, and asset loaders for the redesigned LumiWorld UI.
    /// </summary>
    public static class LumiWorldUITheme
    {
        // Warm stone window + brighter elevated ivory cards/slots (matching P-00 / P-01 mockups)
        public static readonly Color Window = Hex("EBE6DE");
        public static readonly Color Well = Hex("E3DDD4");
        public static readonly Color Surface = Hex("FAF8F4");
        public static readonly Color Slot = Hex("FCFBF8");
        public static readonly Color Inset = Hex("DFDAD1");
        public static readonly Color Hover = Hex("E6E1D8");
        public static readonly Color Text = Hex("35291F");
        public static readonly Color Muted = Hex("756B60");
        public static readonly Color Primary = Hex("403F3C");
        public static readonly Color Disabled = Hex("B5B0A8");
        public static readonly Color Success = Hex("558B3E");
        public static readonly Color Warning = Hex("D87A22");
        public static readonly Color Error = Hex("B8443A");
        public static readonly Color Edge = Hex("D6CFC5");
        public static readonly Color Backdrop = new Color(0.14f, 0.12f, 0.10f, 0.48f);

        public const string VietnameseChars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ·—‹›/+:,.()!?-=[]%×−•✓←→↑↓\n" +
            "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝĂĐĨŨƠƯàáâãèéêìíòóôõùúýăđĩũơư" +
            "ẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼẾỀỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴỶỸ" +
            "ạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ";

        private static TMP_FontAsset body;
        private static TMP_FontAsset heading;
        private static Sprite surfaceSprite;
        private static Sprite borderSprite;
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public static TMP_FontAsset Body
        {
            get
            {
                if (body != null) return body;
                body = Resources.Load<TMP_FontAsset>("UI/Production/Nunito-SemiBold SDF");
                if (body == null) body = CreateRuntimeFontAsset("UI/Production/Fonts/Nunito-SemiBold", "Nunito-SemiBold Runtime SDF");
                if (body == null) body = Resources.Load<TMP_FontAsset>("Fonts/LumiWorldVietnameseSDF");
                if (body == null) body = TMP_Settings.defaultFontAsset;
                return body;
            }
        }

        public static TMP_FontAsset Heading
        {
            get
            {
                if (heading != null) return heading;
                heading = Resources.Load<TMP_FontAsset>("UI/Production/Nunito-ExtraBold SDF");
                if (heading == null) heading = CreateRuntimeFontAsset("UI/Production/Fonts/Nunito-ExtraBold", "Nunito-ExtraBold Runtime SDF");
                if (heading == null) heading = Body;
                return heading;
            }
        }

        public static Sprite Art(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return LoadOrWrapSprite("UI/Production/Art/" + id, Vector4.zero, false, false);
        }

        public static Sprite Glyph(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            bool pureWhiteMask = id != "StatusReady" && id != "StatusLow";
            return LoadOrWrapSprite("UI/Production/Glyphs/" + id, Vector4.zero, true, pureWhiteMask);
        }

        public static Sprite StatusReadySprite => Glyph("StatusReady");
        public static Sprite StatusLowSprite => Glyph("StatusLow");

        public static Sprite CreaturePortrait(string canonicalName)
        {
            if (string.IsNullOrEmpty(canonicalName)) return null;
            return LoadOrWrapSprite("UI/Production/Creatures/Portraits/" + canonicalName, Vector4.zero, false, false)
                ?? LoadOrWrapSprite("UI/Production/Creatures/" + canonicalName, Vector4.zero, false, false);
        }

        public static Sprite CreatureDetailArt(string canonicalName)
        {
            if (string.IsNullOrEmpty(canonicalName)) return null;
            return LoadOrWrapSprite("UI/Production/Creatures/" + canonicalName, Vector4.zero, false, false);
        }

        public static Sprite RankGlyph(int rankOneToThree)
        {
            int r = Mathf.Clamp(rankOneToThree, 1, 3);
            return Glyph("Rank" + r);
        }

        public static Sprite FootprintGlyph(int hexCount)
        {
            if (hexCount >= 10) return Glyph("Footprint10");
            if (hexCount >= 6) return Glyph("Footprint6");
            return Glyph("Footprint3");
        }

        public static Sprite BiomeEmblem(string biomeNameOrAffinity)
        {
            if (string.IsNullOrEmpty(biomeNameOrAffinity)) return Art("B-01");
            string lower = biomeNameOrAffinity.ToLowerInvariant();
            if (lower.Contains("leafwood")) return Art("B-01");
            if (lower.Contains("bloomfield")) return Art("B-02");
            if (lower.Contains("windheath")) return Art("B-03");
            if (lower.Contains("reedmarsh")) return Art("B-04");
            if (lower.Contains("lilywater")) return Art("B-05");
            if (lower.Contains("streamstone")) return Art("B-06");
            if (lower.Contains("rainleaf")) return Art("B-07");
            return Art("B-01");
        }

        public static Sprite SurfaceSprite
        {
            get
            {
                if (surfaceSprite == null)
                    surfaceSprite = LoadOrWrapSprite("UI/Production/Surfaces/Surface", new Vector4(24, 24, 24, 24), false, false) ?? CreateProceduralSurface(false);
                return surfaceSprite;
            }
        }

        // Aliases of the shared rounded surface (tint via Image.color). Pills use the same 9-slice.
        public static Sprite WindowSprite => SurfaceSprite;
        public static Sprite PillSprite => SurfaceSprite;
        public static Sprite ButtonPrimarySprite => SurfaceSprite;
        public static Sprite ButtonSecondarySprite => SurfaceSprite;

        public static Sprite BorderSprite
        {
            get
            {
                if (borderSprite == null)
                    borderSprite = LoadOrWrapSprite("UI/Production/Surfaces/Border", new Vector4(24, 24, 24, 24), false, false) ?? CreateProceduralSurface(true);
                return borderSprite;
            }
        }

        /// <summary>
        /// Adds a cozy 3D bottom bevel/lip and soft drop shadow to cards, item slots, and pills (matching P-00 / P-01 mockups).
        /// </summary>
        public static void AddSoftElevation(GameObject target, float depth = 3f, float shadowAlpha = 0.14f)
        {
            if (target == null) return;

            var lip = target.AddComponent<Shadow>();
            lip.effectColor = new Color(0.24f, 0.19f, 0.14f, shadowAlpha);
            lip.effectDistance = new Vector2(0f, -depth);
            lip.useGraphicAlpha = true;

            var ambient = target.AddComponent<Shadow>();
            ambient.effectColor = new Color(0.20f, 0.16f, 0.12f, shadowAlpha * 0.55f);
            ambient.effectDistance = new Vector2(0f, -(depth + 2.5f));
            ambient.useGraphicAlpha = true;
        }

        public static void ApplyToSkin(ResourceInventoryUISkin skin)
        {
            if (skin == null) return;
            if (Body != null) skin.font = Body;
            var surf = SurfaceSprite;
            if (surf != null)
            {
                skin.panelHabitat = skin.windowMain = skin.surfaceInset = surf;
                skin.buttonPrimary = skin.buttonSecondary = skin.buttonIcon = surf;
                skin.slotResource = skin.slotLocked = surf;
            }
            if (BorderSprite != null) skin.slotSelected = BorderSprite;
            var g01 = Glyph("G-01"); if (g01 != null) skin.iconWarehouse = g01;
            var close = Glyph("Close"); if (close != null) skin.iconClose = close;
            var lockSp = Glyph("Lock"); if (lockSp != null) skin.iconLock = lockSp;
            skin.ivory = Window;
            skin.forest = Text;
            skin.sage = Success;
            skin.muted = Muted;
            skin.panelPPU = skin.windowPPU = skin.insetPPU = skin.buttonPPU = 1f;
        }

        private static Sprite LoadOrWrapSprite(string resourcePath, Vector4 border, bool isGlyph, bool pureWhiteMask)
        {
            if (spriteCache.TryGetValue(resourcePath, out var cached) && cached != null)
                return cached;

            var sp = Resources.Load<Sprite>(resourcePath);
            Texture2D rawTex = sp != null ? sp.texture : Resources.Load<Texture2D>(resourcePath);
            if (rawTex == null)
                return null;

            if (isGlyph)
            {
                var smooth = CreateSmoothGlyphSprite(rawTex, border, pureWhiteMask);
                if (smooth != null)
                {
                    spriteCache[resourcePath] = smooth;
                    return smooth;
                }
            }

            if (sp != null)
            {
                spriteCache[resourcePath] = sp;
                return sp;
            }

            rawTex.filterMode = FilterMode.Bilinear;
            sp = Sprite.Create(
                rawTex,
                new Rect(0, 0, rawTex.width, rawTex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
            spriteCache[resourcePath] = sp;
            return sp;
        }

        private static Sprite CreateSmoothGlyphSprite(Texture2D sourceTex, Vector4 border, bool pureWhiteMask)
        {
            if (sourceTex == null) return null;
            try
            {
                const int targetSize = 64;
                sourceTex.filterMode = FilterMode.Bilinear;

                // Progressive 2x GPU supersampling down to 64x64 + full CPU mipChain generation
                RenderTexture prevRt = null;
                int curSize = Mathf.Max(targetSize, Mathf.ClosestPowerOfTwo(sourceTex.width) / 2);
                Texture currentSrc = sourceTex;

                while (curSize > targetSize)
                {
                    var nextRt = RenderTexture.GetTemporary(curSize, curSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                    nextRt.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(currentSrc, nextRt);
                    if (prevRt != null) RenderTexture.ReleaseTemporary(prevRt);
                    prevRt = nextRt;
                    currentSrc = prevRt;
                    curSize /= 2;
                }

                var finalRt = RenderTexture.GetTemporary(targetSize, targetSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                finalRt.filterMode = FilterMode.Bilinear;
                Graphics.Blit(currentSrc, finalRt);
                if (prevRt != null) RenderTexture.ReleaseTemporary(prevRt);

                var prevActive = RenderTexture.active;
                RenderTexture.active = finalRt;

                var smoothTex = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, true)
                {
                    name = sourceTex.name + "_SmoothMip",
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 4
                };
                smoothTex.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0, false);
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(finalRt);

                if (pureWhiteMask)
                {
                    // Ensure 100% pure-white RGB at all alpha values so bilinear/trilinear mip levels never bleed dark halos
                    var cols = smoothTex.GetPixels32();
                    for (int i = 0; i < cols.Length; i++)
                    {
                        byte a = cols[i].a;
                        if (a <= 8) a = 0;
                        cols[i] = new Color32(255, 255, 255, a);
                    }
                    smoothTex.SetPixels32(cols);
                }

                smoothTex.Apply(true, false);

                return Sprite.Create(
                    smoothTex,
                    new Rect(0, 0, targetSize, targetSize),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    border);
            }
            catch
            {
                return null;
            }
        }

        private static TMP_FontAsset CreateRuntimeFontAsset(string resourceFontPath, string assetName)
        {
            var sourceFont = Resources.Load<Font>(resourceFontPath);
            if (sourceFont == null) return null;
            try
            {
                var fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);
                if (fontAsset != null)
                {
                    fontAsset.name = assetName;
                    fontAsset.TryAddCharacters(VietnameseChars, out _);
                }
                return fontAsset;
            }
            catch
            {
                return null;
            }
        }

        private static Sprite CreateProceduralSurface(bool borderOnly)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - 48f) - 26f, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - 48f) - 26f, 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(22f - distance);
                    if (borderOnly) alpha *= Mathf.Clamp01(distance - 18.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(24, 24, 24, 24));
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var value);
            return value;
        }
    }
}

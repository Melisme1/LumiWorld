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
        private static TMP_FontAsset display;
        private static TMP_FontAsset displayRegular;
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

        /// <summary>
        /// Sculpted display font (Philosopher Bold) for hero HUD numerals and Harmony titles.
        /// </summary>
        public static TMP_FontAsset Display
        {
            get
            {
                if (display != null) return display;
                display = Resources.Load<TMP_FontAsset>("UI/Production/Philosopher-Bold SDF");
                if (display == null) display = CreateRuntimeFontAsset("UI/Production/Fonts/Philosopher-Bold", "Philosopher-Bold Runtime SDF");
                if (display == null) display = Heading;
                return display;
            }
        }

        /// <summary>
        /// Sculpted display font regular weight (Philosopher Regular) for understated HUD captions.
        /// </summary>
        public static TMP_FontAsset DisplayRegular
        {
            get
            {
                if (displayRegular != null) return displayRegular;
                displayRegular = Resources.Load<TMP_FontAsset>("UI/Production/Philosopher-Regular SDF");
                if (displayRegular == null) displayRegular = CreateRuntimeFontAsset("UI/Production/Fonts/Philosopher-Regular", "Philosopher-Regular Runtime SDF");
                if (displayRegular == null) displayRegular = Body;
                return displayRegular;
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

        public static Sprite IslandGlyph(string islandIdOrName)
        {
            bool rainy = IsRainyMarshName(islandIdOrName);
            string key = rainy ? "I-02" : "I-01";
            return Glyph(key) ?? Art(key);
        }

        public static string GetCardOriginIslandName(CardData card)
        {
            if (card == null) return "Starter plain";
            string probe = $"{card.cardID} {card.cardName} {card.name}";
            return IsRainyMarshName(probe) ? "Rainy marsh" : "Starter plain";
        }

        public static bool IsRainyMarshName(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string lower = value.ToLowerInvariant();
            return lower.Contains("rainy") || lower.Contains("marsh") || lower.Contains("wet")
                || lower.Contains("reedmarsh") || lower.Contains("lilywater")
                || lower.Contains("streamstone") || lower.Contains("rainleaf")
                || lower.Contains("i-02");
        }

        public static string ResolveOrderNpcPortraitId(string templateId, string customerName)
        {
            string probe = $"{templateId} {customerName}".ToLowerInvariant();
            if (probe.Contains("wood") || probe.Contains("carpenter")) return "N-01";
            if (probe.Contains("baker") || probe.Contains("bread") || probe.Contains("berry")) return "N-03";
            if (probe.Contains("merchant") || probe.Contains("paper")) return "N-05";
            if (probe.Contains("herb") || probe.Contains("healer")) return "N-07";
            if (probe.Contains("flower")) return "N-09";
            if (probe.Contains("tea") || probe.Contains("artisan")) return "N-11";
            if (probe.Contains("fair") || probe.Contains("island")) return "N-06";

            string hashKey = !string.IsNullOrEmpty(templateId) ? templateId : (customerName ?? "order");
            uint h = 2166136261u;
            foreach (char ch in hashKey)
            {
                h ^= ch;
                h *= 16777619u;
            }
            int idx = (int)(h % 12u) + 1;
            return "N-" + idx.ToString("00");
        }

        public static Sprite NpcPortrait(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return null;
            return Art(npcId);
        }

        public static Sprite AchievementBadge(string achievementOrMilestoneId)
        {
            if (string.IsNullOrEmpty(achievementOrMilestoneId)) return Art("A-01");
            string key = achievementOrMilestoneId.Trim().ToUpperInvariant();
            if (key.StartsWith("A-", StringComparison.Ordinal))
                return Art(key) ?? Art("A-01");

            switch (key)
            {
                case "M1_FIRST_HABITAT": return Art("A-01");
                case "M2_NEW_SPECIES": return Art("A-02");
                case "M3_RANK_UP": return Art("A-03");
                case "M4_ISLAND_STORY": return Art("A-04");
                case "ACH_FIRST_NATURE": return Art("A-05");
                case "ACH_HABITAT_3": return Art("A-06");
                case "ACH_HABITAT_6": return Art("A-07");
                case "ACH_FIRST_CREATURE": return Art("A-08");
                case "ACH_CONNECTION_DANCE": return Art("A-09");
                case "ACH_FIRST_CARE": return Art("A-10");
                case "ACH_HARVEST_10": return Art("A-11");
                default: return Art("A-01");
            }
        }

        public static Sprite GetBorderedSurfaceSprite(string cacheKey, Color fill, Color border, int radius = 16, float borderThickness = 1.5f)
        {
            string fullKey = "ProcSurf_" + cacheKey;
            if (spriteCache.TryGetValue(fullKey, out var cached) && cached != null)
                return cached;

            int r = Mathf.Max(radius, 4);
            int size = Mathf.Max(64, r * 2 + 16);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = fullKey,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] px = new Color[size * size];

            // 4x4 subpixel supersampling (16 geometric samples per texel) for studio-grade anti-aliased curves
            const int ss = 4;
            float invSsSq = 1f / (ss * ss);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float totalAlpha = 0f;
                    float totalBorder = 0f;

                    for (int sy = 0; sy < ss; sy++)
                    {
                        float py = y + (sy + 0.5f) / ss;
                        float dy = py < r ? r - py : (py > size - 1 - r ? py - (size - 1 - r) : 0f);

                        for (int sx = 0; sx < ss; sx++)
                        {
                            float pxX = x + (sx + 0.5f) / ss;
                            float dx = pxX < r ? r - pxX : (pxX > size - 1 - r ? pxX - (size - 1 - r) : 0f);

                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            if (dist <= r)
                            {
                                totalAlpha += 1f;
                                if (borderThickness > 0f && dist >= r - borderThickness)
                                {
                                    totalBorder += 1f;
                                }
                            }
                        }
                    }

                    float a = totalAlpha * invSsSq;
                    if (a <= 0.002f)
                    {
                        px[y * size + x] = Color.clear;
                    }
                    else
                    {
                        float bT = totalBorder * invSsSq;
                        Color c = Color.Lerp(fill, border, bT);
                        c.a *= a;
                        px[y * size + x] = c;
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            Sprite sp = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(r, r, r, r));
            spriteCache[fullKey] = sp;
            return sp;
        }

        public static float ComputeFitScale(Component anchorComponent, out Vector2 stageSize)
        {
            Canvas rootCanvas = anchorComponent != null ? anchorComponent.GetComponentInParent<Canvas>()?.rootCanvas : null;
            Vector2 pixels = rootCanvas != null && rootCanvas.scaleFactor > 0.01f
                ? ((RectTransform)rootCanvas.transform).rect.size * rootCanvas.scaleFactor
                : new Vector2(Screen.width, Screen.height);
            float fit = ResourceInventoryUILayout.FitScale(pixels.x, pixels.y);
            if (fit <= 0.01f) fit = 1f;
            stageSize = pixels / fit;

            float parentScale = anchorComponent != null ? anchorComponent.transform.lossyScale.x : 1f;
            if (parentScale <= 0.0001f)
            {
                parentScale = rootCanvas != null && rootCanvas.scaleFactor > 0.01f ? rootCanvas.scaleFactor : 1f;
            }
            return fit / parentScale;
        }

        // ---------------------------------------------------------
        // Shared UI Builder Helpers (used across Phase 4 & 5 UIs)
        // ---------------------------------------------------------

        public static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void PlaceTopRight(RectTransform rt, float xFromRight, float yFromTop, float w, float h)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-xFromRight, -yFromTop);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        public static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            float size,
            Color color,
            bool headingFont = false,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft,
            bool wrap = false)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.font = headingFont ? (Heading ?? Body) : Body;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = wrap ? TextOverflowModes.Ellipsis : (headingFont ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis);
            tmp.raycastTarget = false;
            return tmp;
        }

        public static void ApplyButtonDisabledNeutral(Button btn)
        {
            if (btn == null) return;
            ColorBlock cb = btn.colors;
            cb.disabledColor = Color.white;
            btn.colors = cb;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        public static Image CreateRoundedSurface(
            Transform parent,
            string name,
            Color fill,
            Color border,
            int radius = 16,
            float borderThickness = 1.5f,
            float shadowDepth = 3f,
            float shadowAlpha = 0.14f,
            bool raycastTarget = false)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image img = obj.GetComponent<Image>();
            string key = $"{fill.r:F2}_{fill.g:F2}_{fill.b:F2}_{fill.a:F2}_{border.r:F2}_{border.g:F2}_{border.b:F2}_{border.a:F2}_{radius}_{borderThickness:F1}";
            img.sprite = GetBorderedSurfaceSprite(key, fill, border, radius, borderThickness);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = raycastTarget;
            if (shadowDepth > 0f && shadowAlpha > 0f)
                AddSoftElevation(obj, shadowDepth, shadowAlpha);
            return img;
        }

        public static Button CreatePillButton(
            Transform parent,
            string name,
            string label,
            string glyphId,
            bool primary,
            float fontSize,
            Action onClick,
            out Image bgImg,
            out Image iconImg,
            out TextMeshProUGUI labelTmp,
            int radius = 14)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);

            bgImg = obj.GetComponent<Image>();
            bgImg.sprite = radius > 0 ? GetCapsuleSprite(radius * 2) : PillSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = primary ? Primary : Inset;
            bgImg.raycastTarget = true;
            AddSoftElevation(obj, primary ? 3f : 2f, primary ? 0.18f : 0.13f);

            Button btn = obj.GetComponent<Button>();
            btn.targetGraphic = bgImg;
            ApplyButtonDisabledNeutral(btn);
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            Color fg = primary ? Color.white : Text;
            Sprite sp = !string.IsNullOrEmpty(glyphId) ? Glyph(glyphId) : null;
            iconImg = null;

            GameObject row = new GameObject("Content", typeof(RectTransform));
            row.transform.SetParent(obj.transform, false);
            RectTransform rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = Vector2.zero;
            rowRt.anchorMax = Vector2.one;
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.offsetMin = new Vector2(6f, 0f);
            rowRt.offsetMax = new Vector2(-6f, 0f);

            if (sp != null && !string.IsNullOrEmpty(label))
            {
                HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 7f;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;

                float iconSize = Mathf.Round(fontSize * 1.25f);
                GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icObj.transform.SetParent(row.transform, false);
                icObj.GetComponent<RectTransform>().sizeDelta = new Vector2(iconSize, iconSize);
                iconImg = icObj.GetComponent<Image>();
                iconImg.sprite = sp;
                iconImg.preserveAspect = true;
                iconImg.color = fg;
                iconImg.raycastTarget = false;

                labelTmp = CreateLabel(row.transform, "Label", label, fontSize, fg, false, TextAlignmentOptions.Center);
                labelTmp.overflowMode = TextOverflowModes.Overflow;
                labelTmp.enableWordWrapping = false;
                float estW = Mathf.Max(24f, label.Length * (fontSize * 0.56f) + 6f);
                labelTmp.rectTransform.sizeDelta = new Vector2(estW, fontSize * 1.6f);
                ContentSizeFitter csf = labelTmp.gameObject.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else if (sp != null)
            {
                float iconSize = Mathf.Round(fontSize * 1.25f);
                GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icObj.transform.SetParent(row.transform, false);
                RectTransform icRt = icObj.GetComponent<RectTransform>();
                icRt.anchorMin = icRt.anchorMax = icRt.pivot = new Vector2(0.5f, 0.5f);
                icRt.anchoredPosition = Vector2.zero;
                icRt.sizeDelta = new Vector2(iconSize, iconSize);
                iconImg = icObj.GetComponent<Image>();
                iconImg.sprite = sp;
                iconImg.preserveAspect = true;
                iconImg.color = fg;
                iconImg.raycastTarget = false;
                labelTmp = null;
            }
            else
            {
                labelTmp = CreateLabel(row.transform, "Label", label, fontSize, fg, false, TextAlignmentOptions.Center);
                Stretch(labelTmp.rectTransform);
                labelTmp.overflowMode = TextOverflowModes.Overflow;
                labelTmp.enableWordWrapping = false;
            }

            return btn;
        }

        public static Button CreateSubTabButton(
            Transform parent,
            string name,
            string label,
            string glyphId,
            float fontSize,
            Action onClick,
            out Image bgImg,
            out Image iconImg,
            out TextMeshProUGUI labelTmp,
            out Shadow shadow)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);

            bgImg = obj.GetComponent<Image>();
            bgImg.sprite = GetCapsuleSprite(34);
            bgImg.type = Image.Type.Sliced;
            bgImg.raycastTarget = true;
            AddSoftElevation(obj, 2f, 0.14f);
            shadow = obj.GetComponent<Shadow>();

            Button btn = obj.GetComponent<Button>();
            btn.targetGraphic = bgImg;
            ApplyButtonDisabledNeutral(btn);
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            Sprite sp = !string.IsNullOrEmpty(glyphId) ? Glyph(glyphId) : null;
            iconImg = null;
            float iconSize = Mathf.Round(fontSize * 1.25f);
            if (sp != null)
            {
                GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icObj.transform.SetParent(obj.transform, false);
                RectTransform icRt = icObj.GetComponent<RectTransform>();
                icRt.anchorMin = icRt.anchorMax = new Vector2(0f, 0.5f);
                icRt.pivot = new Vector2(0f, 0.5f);
                icRt.anchoredPosition = new Vector2(12f, 0f);
                icRt.sizeDelta = new Vector2(iconSize, iconSize);
                iconImg = icObj.GetComponent<Image>();
                iconImg.sprite = sp;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            labelTmp = CreateLabel(obj.transform, "Label", label, fontSize, Text, false, sp != null ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center);
            labelTmp.enableAutoSizing = true;
            labelTmp.fontSizeMin = 9.5f;
            labelTmp.fontSizeMax = fontSize;
            RectTransform lrt = labelTmp.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(sp != null ? (iconSize + 17f) : 8f, 0f);
            lrt.offsetMax = new Vector2(-8f, 0f);
            return btn;
        }

        public static void SetSubTabActive(Image bg, Image icon, TextMeshProUGUI label, Shadow shadow, bool active, bool darkActivePill = false)
        {
            if (bg != null)
            {
                bg.sprite = darkActivePill
                    ? GetCapsuleSprite(34)
                    : GetBorderedSurfaceSprite(
                        active ? "SubTabActive" : "SubTabInactive",
                        active ? Slot : new Color(0.90f, 0.88f, 0.84f, 1f),
                        active ? new Color(0.84f, 0.80f, 0.74f, 1f) : new Color(0.86f, 0.83f, 0.78f, 1f),
                        13,
                        1.2f);
                bg.color = darkActivePill
                    ? (active ? Primary : new Color(0.89f, 0.87f, 0.83f, 1f))
                    : Color.white;
            }
            if (shadow != null) shadow.enabled = active;
            Color fg = darkActivePill
                ? (active ? Color.white : new Color(0.36f, 0.32f, 0.28f, 1f))
                : (active ? Text : new Color(0.41f, 0.36f, 0.31f, 1f));
            if (icon != null) icon.color = fg;
            if (label != null) label.color = fg;
        }

        public static Button CreateCloseButton(Transform parent, float xFromRight, float yFromTop, Action onClick, float size = 34f)
        {
            GameObject obj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            PlaceTopRight(obj.GetComponent<RectTransform>(), xFromRight, yFromTop, size, size);

            Image bg = obj.GetComponent<Image>();
            bg.sprite = GetCapsuleSprite(Mathf.RoundToInt(size));
            bg.type = Image.Type.Sliced;
            bg.color = Inset;
            bg.raycastTarget = true;

            GameObject icObj = new GameObject("X", typeof(RectTransform), typeof(Image));
            icObj.transform.SetParent(obj.transform, false);
            RectTransform icRt = icObj.GetComponent<RectTransform>();
            icRt.anchorMin = icRt.anchorMax = icRt.pivot = new Vector2(0.5f, 0.5f);
            icRt.sizeDelta = new Vector2(size * 0.44f, size * 0.44f);
            Image ic = icObj.GetComponent<Image>();
            ic.sprite = Glyph("Close");
            ic.preserveAspect = true;
            ic.color = Text;
            ic.raycastTarget = false;

            Button btn = obj.GetComponent<Button>();
            btn.targetGraphic = bg;
            ApplyButtonDisabledNeutral(btn);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static RectTransform CreateCoinChip(Transform parent, float xFromRight, float yFromTop, out TextMeshProUGUI valueText, Sprite coinSprite, float width = 122f, float height = 34f)
        {
            GameObject chipObj = new GameObject("CoinBalanceChip", typeof(RectTransform), typeof(Image));
            chipObj.transform.SetParent(parent, false);
            RectTransform rt = chipObj.GetComponent<RectTransform>();
            PlaceTopRight(rt, xFromRight, yFromTop, width, height);

            Image bg = chipObj.GetComponent<Image>();
            bg.sprite = GetCapsuleSprite(Mathf.RoundToInt(height));
            bg.type = Image.Type.Sliced;
            bg.color = Primary;
            bg.raycastTarget = false;
            AddSoftElevation(chipObj, 2f, 0.16f);

            GameObject icObj = new GameObject("Coin", typeof(RectTransform), typeof(Image));
            icObj.transform.SetParent(rt, false);
            RectTransform icRt = icObj.GetComponent<RectTransform>();
            icRt.anchorMin = icRt.anchorMax = new Vector2(0f, 0.5f);
            icRt.pivot = new Vector2(0f, 0.5f);
            icRt.anchoredPosition = new Vector2(10f, 0f);
            icRt.sizeDelta = new Vector2(height - 12f, height - 12f);
            Image icImg = icObj.GetComponent<Image>();
            icImg.sprite = coinSprite;
            icImg.preserveAspect = true;
            icImg.raycastTarget = false;

            valueText = CreateLabel(rt, "BalanceText", "0", 14.5f, Color.white, true, TextAlignmentOptions.MidlineRight);
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 11f;
            valueText.fontSizeMax = 15.5f;
            RectTransform vtRt = valueText.rectTransform;
            vtRt.anchorMin = Vector2.zero;
            vtRt.anchorMax = Vector2.one;
            vtRt.offsetMin = new Vector2(height + 2f, 0f);
            vtRt.offsetMax = new Vector2(-12f, 0f);
            return rt;
        }

        public static Image CreateSelectionBorder(Transform parent, int radius = 22, float thickness = 2.6f)
        {
            GameObject obj = new GameObject("SelectionBorder", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Stretch(obj.GetComponent<RectTransform>());
            Image img = obj.GetComponent<Image>();
            img.sprite = GetBorderedSurfaceSprite($"SelOutline_{radius}_{thickness:F1}", Color.clear, Primary, radius, thickness);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

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

        // Aliases of the shared rounded surface (tint via Image.color). Pills use a dedicated smooth 28px capsule 9-slice.
        public static Sprite WindowSprite => SurfaceSprite;
        public static Sprite PillSprite => GetBorderedSurfaceSprite("Pill_Capsule_R28", Color.white, Color.clear, 28, 0f);

        public static Sprite GetCapsuleSprite(int height)
        {
            int r = Mathf.Max(6, height / 2);
            return GetBorderedSurfaceSprite("Capsule_H" + height, Color.white, Color.clear, r, 0f);
        }

        public static Sprite ButtonPrimarySprite => GetBorderedSurfaceSprite("BtnPrimary_R28", Color.white, Color.clear, 28, 0f);
        public static Sprite ButtonSecondarySprite => GetBorderedSurfaceSprite("BtnSecondary_R28", Color.white, Color.clear, 28, 0f);

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

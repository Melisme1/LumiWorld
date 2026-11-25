using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared helper for loading and attaching cozy outer Satin Foil Silver (Epic) / Gold (Legend)
/// card frames with UI shimmer shader, rounded artwork masks, and directional Raise/Lower arrows
/// across CardUI and CardShopUI.
/// </summary>
public static class CardRarityFrameUtility
{
    public const float FrameBleedFracX = 44f / 1024f;
    public const float FrameBleedFracY = 44f / 1536f;

    private const string FrameSilverResource = "CardFrames/Frame_Epic_Silver";
    private const string FrameGoldResource   = "CardFrames/Frame_Legend_Gold";
    private const string ArrowRaiseResource  = "CardFrames/Arrow_Raise";
    private const string ArrowLowerResource  = "CardFrames/Arrow_Lower";
    private const string MatEpicResource     = "CardFrames/Mat_RarityShimmer_Epic";
    private const string MatLegendResource   = "CardFrames/Mat_RarityShimmer_Legend";
    private const string ShimmerShaderName   = "LumiWorld/UI/CardRarityShimmer";

    private static Sprite s_SilverFrame;
    private static Sprite s_GoldFrame;
    private static Sprite s_ArrowRaise;
    private static Sprite s_ArrowLower;
    private static Material s_EpicShimmerMat;
    private static Material s_LegendShimmerMat;
    private static readonly Dictionary<int, Sprite> s_RoundedMasks = new Dictionary<int, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        s_SilverFrame = null;
        s_GoldFrame = null;
        s_ArrowRaise = null;
        s_ArrowLower = null;
        s_EpicShimmerMat = null;
        s_LegendShimmerMat = null;
        s_RoundedMasks.Clear();
    }

    public static Sprite GetRarityFrameSprite(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Epic:
                if (s_SilverFrame == null) s_SilverFrame = Resources.Load<Sprite>(FrameSilverResource);
                return s_SilverFrame;
            case CardRarity.Legend:
                if (s_GoldFrame == null) s_GoldFrame = Resources.Load<Sprite>(FrameGoldResource);
                return s_GoldFrame;
            default:
                return null;
        }
    }

    public static Material GetRarityShimmerMaterial(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Epic:
                if (s_EpicShimmerMat == null) s_EpicShimmerMat = Resources.Load<Material>(MatEpicResource);
                if (s_EpicShimmerMat == null) s_EpicShimmerMat = CreateRuntimeShimmerMaterial(isLegend: false);
                return s_EpicShimmerMat;
            case CardRarity.Legend:
                if (s_LegendShimmerMat == null) s_LegendShimmerMat = Resources.Load<Material>(MatLegendResource);
                if (s_LegendShimmerMat == null) s_LegendShimmerMat = CreateRuntimeShimmerMaterial(isLegend: true);
                return s_LegendShimmerMat;
            default:
                return null;
        }
    }

    private static Material CreateRuntimeShimmerMaterial(bool isLegend)
    {
        Shader shader = Shader.Find(ShimmerShaderName);
        if (shader == null || !shader.isSupported) return null;

        Material mat = new Material(shader)
        {
            name = isLegend ? "Mat_RarityShimmer_Legend_Runtime" : "Mat_RarityShimmer_Epic_Runtime",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (isLegend)
        {
            mat.SetColor("_ShimmerColor", new Color(1f, 0.96f, 0.78f, 1f));
            mat.SetFloat("_ShimmerSpeed", 0.25f);
            mat.SetFloat("_ShimmerWidth", 0.14f);
            mat.SetFloat("_ShimmerIntensity", 0.52f);
            mat.SetColor("_GlintColor", new Color(1f, 0.94f, 0.66f, 1f));
            mat.SetFloat("_GlintSpeed", 1.95f);
            mat.SetFloat("_GlintIntensity", 0.62f);
        }
        else
        {
            mat.SetColor("_ShimmerColor", new Color(0.90f, 0.96f, 1f, 1f));
            mat.SetFloat("_ShimmerSpeed", 0.22f);
            mat.SetFloat("_ShimmerWidth", 0.14f);
            mat.SetFloat("_ShimmerIntensity", 0.38f);
            mat.SetColor("_GlintColor", new Color(0.86f, 0.95f, 1f, 1f));
            mat.SetFloat("_GlintSpeed", 1.65f);
            mat.SetFloat("_GlintIntensity", 0.45f);
        }

        return mat;
    }

    public static Sprite GetRoundedMaskSprite(int radiusPx)
    {
        int key = Mathf.Clamp(radiusPx, 2, 64);
        if (s_RoundedMasks.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        int size = Mathf.Max(32, key * 3 + 8);
        int border = key + 2;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = $"RoundedMask_{key}px",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] px = new Color[size * size];
        float half = size * 0.5f;
        float b = Mathf.Max(0f, half - key);

        for (int y = 0; y < size; y++)
        {
            float dy = Mathf.Abs((y + 0.5f) - half) - b;
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) - half) - b;
                float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
                float sdf = outside + inside - key;
                px[y * size + x] = sdf <= 0.35f ? Color.white : Color.clear;
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));

        s_RoundedMasks[key] = sprite;
        return sprite;
    }

    public static Sprite GetArrowSprite(bool isRaise)
    {
        if (isRaise)
        {
            if (s_ArrowRaise == null) s_ArrowRaise = Resources.Load<Sprite>(ArrowRaiseResource);
            return s_ArrowRaise;
        }
        if (s_ArrowLower == null) s_ArrowLower = Resources.Load<Sprite>(ArrowLowerResource);
        return s_ArrowLower;
    }

    public static void EnsureOverlayChildren(
        RectTransform parent,
        ref Image rarityOverlay,
        ref Image raiseArrow,
        ref Image lowerArrow)
    {
        EnsureOverlayChildren(parent, parent, ref rarityOverlay, ref raiseArrow, ref lowerArrow);
    }

    public static void EnsureOverlayChildren(
        RectTransform frameParent,
        RectTransform arrowParent,
        ref Image rarityOverlay,
        ref Image raiseArrow,
        ref Image lowerArrow)
    {
        if (frameParent == null) frameParent = arrowParent;
        if (arrowParent == null) arrowParent = frameParent;

        if (frameParent != null)
        {
            if (rarityOverlay == null)
            {
                Transform existing = frameParent.Find("RarityFrameOverlay");
                if (existing == null && arrowParent != null && arrowParent != frameParent)
                {
                    existing = arrowParent.Find("RarityFrameOverlay");
                }

                GameObject go = existing != null
                    ? existing.gameObject
                    : new GameObject("RarityFrameOverlay", typeof(RectTransform), typeof(Image));

                if (go.transform.parent != frameParent)
                {
                    go.transform.SetParent(frameParent, false);
                }

                rarityOverlay = go.GetComponent<Image>();
                rarityOverlay.enabled = false;
            }
            else if (rarityOverlay.transform.parent != frameParent)
            {
                rarityOverlay.transform.SetParent(frameParent, false);
            }

            RectTransform rt = rarityOverlay.rectTransform;
            rt.anchorMin = new Vector2(-FrameBleedFracX, -FrameBleedFracY);
            rt.anchorMax = new Vector2(1f + FrameBleedFracX, 1f + FrameBleedFracY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rarityOverlay.preserveAspect = false;
            rarityOverlay.raycastTarget = false;
        }

        if (arrowParent != null)
        {
            if (raiseArrow == null)
            {
                raiseArrow = EnsureArrowChild(arrowParent, "TerraformRaiseArrow", true);
            }
            if (lowerArrow == null)
            {
                lowerArrow = EnsureArrowChild(arrowParent, "TerraformLowerArrow", false);
            }
        }
    }

    private static Image EnsureArrowChild(RectTransform parent, string name, bool isRaise)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(Image));
        if (existing == null) go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = isRaise ? new Vector2(0.045f, 0.42f) : new Vector2(0.755f, 0.42f);
        rt.anchorMax = isRaise ? new Vector2(0.245f, 0.58f) : new Vector2(0.955f, 0.58f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.sprite = GetArrowSprite(isRaise);
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    public static void ApplyCardOverlays(
        RectTransform parent,
        CardData card,
        ref Image rarityOverlay,
        ref Image raiseArrow,
        ref Image lowerArrow)
    {
        ApplyCardOverlays(parent, parent, card, ref rarityOverlay, ref raiseArrow, ref lowerArrow);
    }

    public static void ApplyCardOverlays(
        RectTransform frameParent,
        RectTransform arrowParent,
        CardData card,
        ref Image rarityOverlay,
        ref Image raiseArrow,
        ref Image lowerArrow)
    {
        EnsureOverlayChildren(frameParent, arrowParent, ref rarityOverlay, ref raiseArrow, ref lowerArrow);

        if (rarityOverlay != null)
        {
            Sprite frame = card != null ? GetRarityFrameSprite(card.rarity) : null;
            rarityOverlay.sprite = frame;
            rarityOverlay.material = frame != null ? GetRarityShimmerMaterial(card.rarity) : null;
            rarityOverlay.enabled = frame != null;
            rarityOverlay.color = Color.white;
        }

        bool showArrows = card != null && card.IsTerraformTool();
        if (raiseArrow != null)
        {
            if (raiseArrow.sprite == null) raiseArrow.sprite = GetArrowSprite(true);
            raiseArrow.enabled = showArrows && raiseArrow.sprite != null;
            raiseArrow.color = new Color(1f, 1f, 1f, 0.96f);
            raiseArrow.rectTransform.localScale = Vector3.one;
        }
        if (lowerArrow != null)
        {
            if (lowerArrow.sprite == null) lowerArrow.sprite = GetArrowSprite(false);
            lowerArrow.enabled = showArrows && lowerArrow.sprite != null;
            lowerArrow.color = new Color(1f, 1f, 1f, 0.96f);
            lowerArrow.rectTransform.localScale = Vector3.one;
        }
    }
}

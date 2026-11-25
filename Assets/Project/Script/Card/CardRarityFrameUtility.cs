using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared helper for loading and attaching minimal metallic Silver (Epic) / Gold (Legend)
/// card frames and directional Raise/Lower arrows across CardUI and CardShopUI.
/// </summary>
public static class CardRarityFrameUtility
{
    private const string FrameSilverResource = "CardFrames/Frame_Epic_Silver";
    private const string FrameGoldResource   = "CardFrames/Frame_Legend_Gold";
    private const string ArrowRaiseResource  = "CardFrames/Arrow_Raise";
    private const string ArrowLowerResource  = "CardFrames/Arrow_Lower";

    private static Sprite s_SilverFrame;
    private static Sprite s_GoldFrame;
    private static Sprite s_ArrowRaise;
    private static Sprite s_ArrowLower;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        s_SilverFrame = null;
        s_GoldFrame = null;
        s_ArrowRaise = null;
        s_ArrowLower = null;
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
        if (parent == null) return;

        if (rarityOverlay == null)
        {
            Transform existing = parent.Find("RarityFrameOverlay");
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject("RarityFrameOverlay", typeof(RectTransform), typeof(Image));
            if (existing == null) go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rarityOverlay = go.GetComponent<Image>();
            rarityOverlay.preserveAspect = false;
            rarityOverlay.raycastTarget = false;
            rarityOverlay.enabled = false;
        }

        if (raiseArrow == null)
        {
            raiseArrow = EnsureArrowChild(parent, "TerraformRaiseArrow", true);
        }

        if (lowerArrow == null)
        {
            lowerArrow = EnsureArrowChild(parent, "TerraformLowerArrow", false);
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
        EnsureOverlayChildren(parent, ref rarityOverlay, ref raiseArrow, ref lowerArrow);

        if (rarityOverlay != null)
        {
            Sprite frame = card != null ? GetRarityFrameSprite(card.rarity) : null;
            rarityOverlay.sprite = frame;
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

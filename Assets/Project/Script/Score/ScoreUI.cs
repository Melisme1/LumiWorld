using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// Bottom-Left Harmony Badge (H-01) — LumiWorld UI Production Pack §5 HUD & 05_Confirmed_Assets_and_Order.md:
/// - Warm P-00 circular badge displaying H-01 Harmony emblem, current authoritative Harmony score,
///   and a radial progress ring to the next Harmony milestone (no separate player level).
/// - Clicking the main Harmony badge opens Achievements / Progress (AchievementUI).
/// - Hovering the badge shows a tooltip: "Progress & achievements · {current}/{next} Harmony".
/// - Terrain expansion charges are rendered as up to 5 hex-base icons (G-34) stacked along the upper-right arc
///   (with a "+N" badge when > 5). Clicking any terrain charge button starts/cancels land placement
///   with a separate hit target so badge clicks never trigger land placement and charge clicks never open Achievements.
/// </summary>
public class ScoreUI : MonoBehaviour
{
    public static ScoreUI Instance { get; private set; }

    [Header("Scene Reference")]
    [SerializeField] private TMP_Text scoreText;

    private static readonly Vector2 BadgeSize = new Vector2(124f, 124f);
    private const int MaxVisibleChargeIcons = 5;

    private RectTransform rootRect;
    private RectTransform badgeButtonRect;
    private Image radialFillImage;
    private TextMeshProUGUI milestoneSubLabel;
    private RectTransform tooltipRoot;
    private TextMeshProUGUI tooltipLabel;

    // Terrain expansion charge icons along upper-right arc
    private readonly List<RectTransform> chargeIconButtons = new List<RectTransform>();
    private readonly List<Image> chargeIconBgs = new List<Image>();
    private RectTransform chargeOverflowPill;
    private TextMeshProUGUI chargeOverflowText;

    // Runtime state
    private int currentScore;
    private int completedThreshold;
    private int activeThreshold = 10;
    private int upcomingThreshold = 30;
    private float currentFillRatio;
    private float targetFillRatio;
    private int availableCharges;
    private bool isInPlacement;
    private bool isBadgeHovered;

    private float punchScaleMultiplier = 1f;
    private Coroutine punchRoutine;

    private static Sprite s_CircleDiscSprite;
    private static Sprite s_RadialRingSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        rootRect = (RectTransform)transform;
        BuildHarmonyBadgeUI();
    }

    private void Start()
    {
        HideLegacyCreateButton();
        SubscribeEvents();
        RefreshMilestoneData(animatePunch: false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
            ScoreManager.Instance.OnMilestoneReached += HandleMilestoneReached;
        }

        if (HexPlacementController.Instance != null)
        {
            HexPlacementController.Instance.OnExpansionChargesChanged += HandleExpansionChargesChanged;
            HexPlacementController.Instance.OnPlacementModeChanged += HandlePlacementModeChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
            ScoreManager.Instance.OnMilestoneReached -= HandleMilestoneReached;
        }

        if (HexPlacementController.Instance != null)
        {
            HexPlacementController.Instance.OnExpansionChargesChanged -= HandleExpansionChargesChanged;
            HexPlacementController.Instance.OnPlacementModeChanged -= HandlePlacementModeChanged;
        }
    }

    private void HideLegacyCreateButton()
    {
        if (HexPlacementController.Instance != null && HexPlacementController.Instance.CreateLandButton != null)
        {
            HexPlacementController.Instance.CreateLandButton.gameObject.SetActive(false);
        }
        else
        {
            var oldBtn = GameObject.Find("Canvas/Button");
            if (oldBtn != null) oldBtn.SetActive(false);
        }
    }

    // =========================================================
    // SCORE & MILESTONE UPDATES
    // =========================================================

    public void UpdateScore(int score)
    {
        currentScore = score;
        RefreshMilestoneData(animatePunch: true);
    }

    private void HandleScoreChanged(int newScore)
    {
        currentScore = newScore;
        RefreshMilestoneData(animatePunch: true);
    }

    private void HandleMilestoneReached(ScoreRewardMilestone milestone)
    {
        if (milestone != null && milestone.grantLandExpansion)
        {
            TriggerPunch(1.18f, 0.30f);
        }
    }

    private void HandleExpansionChargesChanged(int newCharges)
    {
        int prev = availableCharges;
        availableCharges = newCharges;
        if (availableCharges > prev)
        {
            TriggerPunch(1.16f, 0.28f);
        }
        UpdateVisuals();
    }

    private void HandlePlacementModeChanged(bool inPlacement)
    {
        isInPlacement = inPlacement;
        UpdateVisuals();
    }

    private void RefreshMilestoneData(bool animatePunch)
    {
        if (ScoreManager.Instance != null)
        {
            currentScore = ScoreManager.Instance.CurrentScore;
            ScoreManager.Instance.GetMilestoneLeafTrio(
                out completedThreshold,
                out activeThreshold,
                out upcomingThreshold,
                out targetFillRatio
            );
        }

        if (HexPlacementController.Instance != null)
        {
            availableCharges = HexPlacementController.Instance.AvailableExpansionCharges;
            isInPlacement = HexPlacementController.Instance.IsInPlacementMode;
        }

        if (animatePunch)
        {
            TriggerPunch(1.08f, 0.18f);
        }

        UpdateVisuals();
    }

    private void Update()
    {
        currentFillRatio = Mathf.MoveTowards(currentFillRatio, targetFillRatio, Time.unscaledDeltaTime * 1.8f);
        if (radialFillImage != null)
        {
            radialFillImage.fillAmount = Mathf.Clamp01(currentFillRatio);
        }

        if (badgeButtonRect != null)
        {
            float hoverScale = isBadgeHovered ? 1.04f : 1.0f;
            badgeButtonRect.localScale = Vector3.one * (punchScaleMultiplier * hoverScale);
        }
    }

    private void UpdateVisuals()
    {
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }

        if (milestoneSubLabel != null)
        {
            milestoneSubLabel.text = $"/ {activeThreshold}";
        }

        if (tooltipLabel != null)
        {
            tooltipLabel.text = $"Progress & achievements  ·  {currentScore}/{activeThreshold}";
        }

        // Update stacked hex-base icons around the upper-right arc
        int visibleIcons = Mathf.Min(availableCharges, MaxVisibleChargeIcons);
        for (int i = 0; i < chargeIconButtons.Count; i++)
        {
            bool show = i < visibleIcons;
            if (chargeIconButtons[i] != null)
            {
                chargeIconButtons[i].gameObject.SetActive(show);
            }
            if (show && i < chargeIconBgs.Count && chargeIconBgs[i] != null)
            {
                chargeIconBgs[i].color = isInPlacement ? LumiWorldUITheme.Success : LumiWorldUITheme.Primary;
            }
        }

        if (chargeOverflowPill != null)
        {
            bool showOverflow = availableCharges > MaxVisibleChargeIcons;
            chargeOverflowPill.gameObject.SetActive(showOverflow);
            if (showOverflow && chargeOverflowText != null)
            {
                chargeOverflowText.text = $"+{availableCharges - MaxVisibleChargeIcons}";
            }
        }
    }

    // =========================================================
    // SEPARATE HIT TARGETS: BADGE vs TERRAIN CHARGES
    // =========================================================

    private void OnHarmonyBadgeClicked()
    {
        TriggerPunch(0.95f, 0.14f);
        GameAudioManager.Instance?.PlayClick();

        if (AchievementUI.Instance != null)
        {
            AchievementUI.Instance.ToggleWindow();
        }
        else
        {
            var ach = FindAnyObjectByType<AchievementUI>(FindObjectsInactive.Include);
            if (ach != null) ach.ToggleWindow();
        }
    }

    private void OnTerrainChargeClicked()
    {
        if (availableCharges <= 0) return;

        TriggerPunch(0.95f, 0.14f);
        GameAudioManager.Instance?.PlayClick();

        if (HexPlacementController.Instance != null)
        {
            if (HexPlacementController.Instance.IsInPlacementMode)
            {
                HexPlacementController.Instance.CancelPlacement();
            }
            else
            {
                HexPlacementController.Instance.StartPlacement();
            }
        }
    }

    private void TriggerPunch(float peakScale, float duration)
    {
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(CoPunch(peakScale, duration));
    }

    private IEnumerator CoPunch(float peakScale, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            punchScaleMultiplier = Mathf.Lerp(1f, peakScale, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        punchScaleMultiplier = 1f;
        punchRoutine = null;
    }

    // =========================================================
    // UI CONSTRUCTION (H-01 Harmony Badge + G-34 Charge Arc)
    // =========================================================

    private void BuildHarmonyBadgeUI()
    {
        Transform oldBg = transform.Find("Score_BG");
        if (oldBg != null) oldBg.gameObject.SetActive(false);

        Image oldSelfImg = GetComponent<Image>();
        if (oldSelfImg != null) oldSelfImg.enabled = false;

        rootRect.sizeDelta = BadgeSize;
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        // 1. Main Circular Harmony Badge Button (Hit Target #1 -> Opens AchievementUI)
        GameObject badgeObj = new GameObject("HarmonyBadgeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        badgeButtonRect = (RectTransform)badgeObj.transform;
        badgeButtonRect.SetParent(rootRect, false);
        badgeButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
        badgeButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
        badgeButtonRect.pivot = new Vector2(0.5f, 0.5f);
        badgeButtonRect.sizeDelta = BadgeSize;
        badgeButtonRect.anchoredPosition = Vector2.zero;

        Image outerImg = badgeObj.GetComponent<Image>();
        outerImg.sprite = CircleDiscSprite;
        outerImg.color = LumiWorldUITheme.Window; // Warm #EBE6DE outer ring
        outerImg.raycastTarget = true;
        LumiWorldUITheme.AddSoftElevation(badgeObj, 4f, 0.22f);

        Button badgeBtn = badgeObj.GetComponent<Button>();
        badgeBtn.targetGraphic = outerImg;
        badgeBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        badgeBtn.onClick.AddListener(OnHarmonyBadgeClicked);

        // Hover events for tooltip
        EventTrigger trigger = badgeObj.AddComponent<EventTrigger>();
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ =>
        {
            isBadgeHovered = true;
            if (tooltipRoot != null) tooltipRoot.gameObject.SetActive(true);
        });
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ =>
        {
            isBadgeHovered = false;
            if (tooltipRoot != null) tooltipRoot.gameObject.SetActive(false);
        });
        trigger.triggers.Add(exitEntry);

        // 2. Radial Progress Track (Inset ring)
        GameObject trackObj = new GameObject("RadialTrack", typeof(RectTransform), typeof(Image));
        RectTransform trackRect = (RectTransform)trackObj.transform;
        trackRect.SetParent(badgeButtonRect, false);
        trackRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(110f, 110f);
        Image trackImg = trackObj.GetComponent<Image>();
        trackImg.sprite = RadialRingSprite;
        trackImg.color = LumiWorldUITheme.Edge;
        trackImg.raycastTarget = false;

        // 3. Radial Progress Fill (Harmony milestone progress)
        GameObject fillObj = new GameObject("RadialFill", typeof(RectTransform), typeof(Image));
        RectTransform fillRect = (RectTransform)fillObj.transform;
        fillRect.SetParent(badgeButtonRect, false);
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.sizeDelta = new Vector2(110f, 110f);
        radialFillImage = fillObj.GetComponent<Image>();
        radialFillImage.sprite = RadialRingSprite;
        radialFillImage.type = Image.Type.Filled;
        radialFillImage.fillMethod = Image.FillMethod.Radial360;
        radialFillImage.fillOrigin = (int)Image.Origin360.Top;
        radialFillImage.fillClockwise = true;
        radialFillImage.fillAmount = 0f;
        radialFillImage.color = LumiWorldUITheme.Success;
        radialFillImage.raycastTarget = false;

        // 4. Inner Warm Ivory Disc (#FAF8F4)
        GameObject innerObj = new GameObject("InnerDisc", typeof(RectTransform), typeof(Image));
        RectTransform innerRect = (RectTransform)innerObj.transform;
        innerRect.SetParent(badgeButtonRect, false);
        innerRect.anchorMin = new Vector2(0.5f, 0.5f);
        innerRect.anchorMax = new Vector2(0.5f, 0.5f);
        innerRect.sizeDelta = new Vector2(92f, 92f);
        Image innerImg = innerObj.GetComponent<Image>();
        innerImg.sprite = CircleDiscSprite;
        innerImg.color = LumiWorldUITheme.Surface;
        innerImg.raycastTarget = false;

        // 5. H-01 Harmony Leaf Emblem at top of inner disc
        GameObject emblemObj = new GameObject("HarmonyEmblem", typeof(RectTransform), typeof(Image));
        RectTransform emblemRect = (RectTransform)emblemObj.transform;
        emblemRect.SetParent(innerRect, false);
        emblemRect.anchorMin = new Vector2(0.5f, 0.5f);
        emblemRect.anchorMax = new Vector2(0.5f, 0.5f);
        emblemRect.anchoredPosition = new Vector2(0f, 25f);
        emblemRect.sizeDelta = new Vector2(24f, 24f);
        Image emblemImg = emblemObj.GetComponent<Image>();
        Sprite h01 = LumiWorldUITheme.Art("H-01") ?? LumiWorldUITheme.Glyph("G-11");
        emblemImg.sprite = h01;
        emblemImg.preserveAspect = true;
        emblemImg.color = LumiWorldUITheme.Art("H-01") != null ? Color.white : LumiWorldUITheme.Success;
        emblemImg.raycastTarget = false;

        // 6. Authoritative Harmony Score Text in center
        if (scoreText == null)
        {
            Transform textTr = transform.Find("Score_Text");
            if (textTr != null) scoreText = textTr.GetComponent<TMP_Text>();
        }

        if (scoreText == null)
        {
            scoreText = CreateTMP("Score_Text", innerRect, 30f, LumiWorldUITheme.Text, TextAlignmentOptions.Center);
        }
        else
        {
            scoreText.transform.SetParent(innerRect, false);
        }

        RectTransform scoreRect = scoreText.rectTransform;
        scoreRect.anchorMin = new Vector2(0.5f, 0.5f);
        scoreRect.anchorMax = new Vector2(0.5f, 0.5f);
        scoreRect.pivot = new Vector2(0.5f, 0.5f);
        scoreRect.sizeDelta = new Vector2(84f, 36f);
        scoreRect.anchoredPosition = new Vector2(0f, -2f);
        scoreText.font = LumiWorldUITheme.Heading ?? scoreText.font;
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.fontSize = 28f;
        scoreText.fontStyle = FontStyles.Bold;
        scoreText.color = LumiWorldUITheme.Text;
        scoreText.raycastTarget = false;

        // 7. Next Milestone Label at bottom of inner disc
        milestoneSubLabel = CreateTMP("MilestoneSubLabel", innerRect, 11.5f, LumiWorldUITheme.Muted, TextAlignmentOptions.Center);
        milestoneSubLabel.rectTransform.sizeDelta = new Vector2(76f, 18f);
        milestoneSubLabel.rectTransform.anchoredPosition = new Vector2(0f, -25f);
        milestoneSubLabel.text = "/ 10";

        // 8. Stacked Terrain Expansion Charge Icons along Upper-Right Arc (Hit Target #2 -> Land Placement)
        BuildTerrainChargeArc();

        // 9. Hover Tooltip ("Progress & achievements")
        BuildHoverTooltip();
    }

    private void BuildTerrainChargeArc()
    {
        chargeIconButtons.Clear();
        chargeIconBgs.Clear();

        // 5 positions along the upper-right arc (in degrees from +X axis: 78°, 50°, 22°, -6°, -34°)
        float[] anglesDeg = { 78f, 50f, 22f, -6f, -34f };
        const float arcRadius = 74f;
        Sprite hexGlyph = LumiWorldUITheme.Glyph("G-34");

        for (int i = 0; i < MaxVisibleChargeIcons; i++)
        {
            float rad = anglesDeg[i] * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(rad) * arcRadius, Mathf.Sin(rad) * arcRadius);

            GameObject btnObj = new GameObject($"TerrainCharge_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rt = (RectTransform)btnObj.transform;
            rt.SetParent(rootRect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(32f, 32f);
            rt.anchoredPosition = pos;

            Image bg = btnObj.GetComponent<Image>();
            bg.sprite = CircleDiscSprite;
            bg.color = LumiWorldUITheme.Primary;
            bg.raycastTarget = true;
            LumiWorldUITheme.AddSoftElevation(btnObj, 2.5f, 0.22f);

            GameObject icObj = new GameObject("HexGlyph", typeof(RectTransform), typeof(Image));
            RectTransform icRect = (RectTransform)icObj.transform;
            icRect.SetParent(rt, false);
            icRect.anchorMin = new Vector2(0.5f, 0.5f);
            icRect.anchorMax = new Vector2(0.5f, 0.5f);
            icRect.sizeDelta = new Vector2(18f, 18f);
            icRect.anchoredPosition = Vector2.zero;

            Image icImg = icObj.GetComponent<Image>();
            icImg.sprite = hexGlyph;
            icImg.preserveAspect = true;
            icImg.color = Color.white;
            icImg.raycastTarget = false;

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(OnTerrainChargeClicked);

            btnObj.SetActive(false);
            chargeIconButtons.Add(rt);
            chargeIconBgs.Add(bg);
        }

        // "+N" overflow pill at bottom-right of the arc when charges > 5
        float overRad = -58f * Mathf.Deg2Rad;
        GameObject overObj = new GameObject("ChargeOverflowPill", typeof(RectTransform), typeof(Image), typeof(Button));
        chargeOverflowPill = (RectTransform)overObj.transform;
        chargeOverflowPill.SetParent(rootRect, false);
        chargeOverflowPill.anchorMin = new Vector2(0.5f, 0.5f);
        chargeOverflowPill.anchorMax = new Vector2(0.5f, 0.5f);
        chargeOverflowPill.pivot = new Vector2(0.5f, 0.5f);
        chargeOverflowPill.sizeDelta = new Vector2(38f, 24f);
        chargeOverflowPill.anchoredPosition = new Vector2(Mathf.Cos(overRad) * arcRadius, Mathf.Sin(overRad) * arcRadius);

        Image overBg = overObj.GetComponent<Image>();
        overBg.sprite = LumiWorldUITheme.PillSprite;
        overBg.type = Image.Type.Sliced;
        overBg.color = LumiWorldUITheme.Primary;
        LumiWorldUITheme.AddSoftElevation(overObj, 2f, 0.20f);

        Button overBtn = overObj.GetComponent<Button>();
        overBtn.targetGraphic = overBg;
        overBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        overBtn.onClick.AddListener(OnTerrainChargeClicked);

        chargeOverflowText = CreateTMP("OverflowText", chargeOverflowPill, 12f, Color.white, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(chargeOverflowText.rectTransform);
        chargeOverflowText.text = "+1";
        overObj.SetActive(false);
    }

    private void BuildHoverTooltip()
    {
        GameObject tipObj = new GameObject("HarmonyTooltip", typeof(RectTransform), typeof(Image));
        tooltipRoot = (RectTransform)tipObj.transform;
        tooltipRoot.SetParent(rootRect, false);
        tooltipRoot.anchorMin = new Vector2(0.5f, 1f);
        tooltipRoot.anchorMax = new Vector2(0.5f, 1f);
        tooltipRoot.pivot = new Vector2(0f, 0f);
        tooltipRoot.anchoredPosition = new Vector2(-48f, 14f);
        tooltipRoot.sizeDelta = new Vector2(246f, 34f);

        Image tipBg = tipObj.GetComponent<Image>();
        tipBg.sprite = LumiWorldUITheme.PillSprite;
        tipBg.type = Image.Type.Sliced;
        tipBg.color = LumiWorldUITheme.Surface;
        tipBg.raycastTarget = false;
        LumiWorldUITheme.AddSoftElevation(tipObj, 3f, 0.18f);

        tooltipLabel = CreateTMP("Label", tooltipRoot, 13f, LumiWorldUITheme.Text, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(tooltipLabel.rectTransform);
        tooltipLabel.text = "Progress & achievements";

        tipObj.SetActive(false);
    }

    private static TextMeshProUGUI CreateTMP(string name, RectTransform parent, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)obj.transform;
        rt.SetParent(parent, false);

        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.font = LumiWorldUITheme.Body;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    // =========================================================
    // PROCEDURAL SMOOTH CIRCLE & RADIAL RING SPRITES
    // =========================================================

    private static Sprite CircleDiscSprite
    {
        get
        {
            if (s_CircleDiscSprite == null) s_CircleDiscSprite = CreateSmoothCircleSprite(128, 0f);
            return s_CircleDiscSprite;
        }
    }

    private static Sprite RadialRingSprite
    {
        get
        {
            if (s_RadialRingSprite == null) s_RadialRingSprite = CreateSmoothCircleSprite(128, 0.80f);
            return s_RadialRingSprite;
        }
    }

    private static Sprite CreateSmoothCircleSprite(int size, float innerRadiusRatio)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];

        float center = (size - 1) * 0.5f;
        float maxR = center - 1.5f;
        float minR = maxR * Mathf.Clamp01(innerRadiusRatio);
        float aa = 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float outerA = Mathf.Clamp01((maxR - dist) / aa + 0.5f);
                float innerA = innerRadiusRatio > 0f ? Mathf.Clamp01((dist - minR) / aa + 0.5f) : 1f;
                float a = outerA * innerA;

                px[y * size + x] = a > 0.004f ? new Color(1f, 1f, 1f, a) : Color.clear;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

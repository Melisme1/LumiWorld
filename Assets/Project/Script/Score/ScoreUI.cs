using System.Collections.Generic;
using LumiWorld.Acs;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Bottom-Left Harmony HUD Widget:
/// - Live 3D HarmonyCore crystal (RenderTexture studio with mouse parallax & orbiting facet glint)
/// - True miter-joined flat-top hexagonal progress rim starting at 12 o'clock clockwise
/// - Refined additive crystal sparkle & gain-burst motes
/// - 6 symmetrical crown sockets displaying Live 3D TerrainCharge crystals (or recessed empty sockets when 0)
/// - Always-on restrained Warm Stone Harmony Plaque below the hex (unobstructed progress rim) using Philosopher Display typography
/// </summary>
public class ScoreUI : MonoBehaviour
{
    public static ScoreUI Instance { get; private set; }

    [Header("Scene Reference")]
    [SerializeField] private TMP_Text scoreText;

    private const int MaxVisibleChargeIcons = 6;

    // 6 crown angles clockwise from 12 o'clock (upper & side arc, keeping bottom plaque 100% clear)
    private static readonly float[] ChargeCrownAngles = { -104f, -62.4f, -20.8f, 20.8f, 62.4f, 104f };
    private static readonly Color EmptySocketTint = new Color(0.11f, 0.18f, 0.24f, 0.30f);

    private sealed class ChargeSlotView
    {
        public Button Button;
        public RawImage CrystalImage;
        public HarmonyChargeGlowFx GlowFx;
    }

    private RectTransform rootRect;
    private RectTransform coreClusterRect;
    private RectTransform coreHitRect;
    private HarmonyHud3DStage stage3D;
    private HarmonyCrystalFx progressRimFx;
    private HarmonySparkleOverlay backHaloFx;
    private HarmonySparkleOverlay frontSparkleFx;
    private RawImage coreRawImage;

    private readonly List<ChargeSlotView> chargeSlots = new List<ChargeSlotView>();
    private Texture2D fallbackChargeTex;

    // Always-on Warm Stone Plaque
    private RectTransform plaqueRect;
    private TextMeshProUGUI plaqueScoreLabel;
    private RectTransform plaqueScoreRect;

    // Hover detail ribbon beside plaque (never covers progress ring)
    private CanvasGroup hoverRibbonGroup;
    private TextMeshProUGUI hoverRibbonText;

    // Overflow "+N" pill when availableCharges > 6
    private RectTransform chargeOverflowPill;
    private TextMeshProUGUI chargeOverflowText;

    // Runtime state
    private int currentScore;
    private float displayedScore;
    private int activeThreshold = 10;
    private float currentFillRatio;
    private float targetFillRatio;
    private int availableCharges;
    private bool isInPlacement;
    private bool isCoreHovered;
    private float scorePunchTimer;
    private float lastAppliedScale = -1f;

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
            Instance = null;

        UnsubscribeEvents();

        if (stage3D != null)
        {
            Destroy(stage3D.gameObject);
            stage3D = null;
        }
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
        if (progressRimFx == null || score == currentScore) return;
        currentScore = score;
        RefreshMilestoneData(animatePunch: true);
    }

    private void HandleScoreChanged(int newScore)
    {
        UpdateScore(newScore);
    }

    private void HandleMilestoneReached(ScoreRewardMilestone milestone)
    {
        if (milestone != null && milestone.grantLandExpansion)
        {
            TriggerCelebration(milestone: true);
        }
    }

    private void HandleExpansionChargesChanged(int newCharges)
    {
        int prev = availableCharges;
        availableCharges = newCharges;
        if (availableCharges > prev)
            TriggerCelebration(milestone: true);
        UpdateVisuals();
    }

    private void HandlePlacementModeChanged(bool inPlacement)
    {
        isInPlacement = inPlacement;
        UpdateVisuals();
    }

    private void TriggerCelebration(bool milestone)
    {
        progressRimFx?.Flash(milestone);
        frontSparkleFx?.TriggerBurst(milestone ? 10 : 6);
        stage3D?.TriggerPulse(milestone);
        scorePunchTimer = milestone ? 0.36f : 0.24f;
    }

    private void RefreshMilestoneData(bool animatePunch)
    {
        int previousTarget = activeThreshold;
        if (ScoreManager.Instance != null)
        {
            currentScore = ScoreManager.Instance.CurrentScore;
            ScoreManager.Instance.GetLandExpansionProgress(
                out _,
                out _,
                out targetFillRatio,
                out activeThreshold
            );
        }

        if (!animatePunch)
        {
            currentFillRatio = targetFillRatio;
            displayedScore = currentScore;
        }
        else if (activeThreshold != previousTarget)
        {
            currentFillRatio = 0f;
        }

        if (HexPlacementController.Instance != null)
        {
            availableCharges = HexPlacementController.Instance.AvailableExpansionCharges;
            isInPlacement = HexPlacementController.Instance.IsInPlacementMode;
        }

        if (animatePunch)
            TriggerCelebration(milestone: activeThreshold != previousTarget);

        UpdateVisuals();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        ApplyResponsiveScale();

        // Smooth hex progress fill
        currentFillRatio = Mathf.MoveTowards(currentFillRatio, targetFillRatio, dt * 1.65f);
        if (progressRimFx != null)
            progressRimFx.progress = currentFillRatio;

        // Smooth score number roll-up + micro-punch on gain
        if (!Mathf.Approximately(displayedScore, currentScore))
        {
            float speed = Mathf.Max(18f, Mathf.Abs(currentScore - displayedScore) * 6f);
            displayedScore = Mathf.MoveTowards(displayedScore, currentScore, dt * speed);
            RefreshPlaqueNumberText();
        }

        if (scorePunchTimer > 0f && plaqueScoreRect != null)
        {
            scorePunchTimer = Mathf.Max(0f, scorePunchTimer - dt);
            float bump = 1f + Mathf.Sin((scorePunchTimer / 0.36f) * Mathf.PI) * 0.09f;
            plaqueScoreRect.localScale = Vector3.one * bump;
        }
        else if (plaqueScoreRect != null && plaqueScoreRect.localScale != Vector3.one)
        {
            plaqueScoreRect.localScale = Vector3.one;
        }

        // Smooth hover detail ribbon fade
        if (hoverRibbonGroup != null)
        {
            float targetA = isCoreHovered ? 1f : 0f;
            hoverRibbonGroup.alpha = Mathf.MoveTowards(hoverRibbonGroup.alpha, targetA, dt * 7f);
        }

        // Live 3D mouse-tracking parallax while hovered
        if (stage3D != null && isCoreHovered && coreHitRect != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(coreHitRect, Input.mousePosition, null, out Vector2 localPt))
            {
                Vector2 half = coreHitRect.rect.size * 0.5f;
                Vector2 norm = new Vector2(
                    half.x > 1f ? localPt.x / half.x : 0f,
                    half.y > 1f ? localPt.y / half.y : 0f);
                stage3D.SetCoreHover(true, norm);
            }
        }
    }

    private void ApplyResponsiveScale()
    {
        if (rootRect == null || rootRect.parent == null) return;
        float fit = LumiWorldUITheme.ComputeFitScale(rootRect.parent, out _);
        if (Mathf.Approximately(fit, lastAppliedScale)) return;

        lastAppliedScale = fit;
        rootRect.localScale = Vector3.one * fit;
        rootRect.anchoredPosition = new Vector2(146f * fit, 140f * fit);
    }

    private void UpdateVisuals()
    {
        RefreshPlaqueNumberText();

        if (hoverRibbonText != null)
        {
            if (availableCharges > 0)
            {
                hoverRibbonText.text = isInPlacement
                    ? "Placing land  ·  Click crystal to cancel"
                    : $"Expand ready ({availableCharges})  ·  Achievements";
            }
            else
            {
                int remaining = Mathf.Max(0, activeThreshold - currentScore);
                hoverRibbonText.text = $"+{remaining} to expand  ·  Achievements";
            }
        }

        if (stage3D != null)
            stage3D.SetChargesState(availableCharges > 0, isInPlacement);

        Texture activeChargeTex = (stage3D != null && stage3D.HasChargeModel && stage3D.ChargeTexture != null)
            ? (Texture)stage3D.ChargeTexture
            : fallbackChargeTex;

        for (int i = 0; i < chargeSlots.Count; i++)
        {
            ChargeSlotView slot = chargeSlots[i];
            bool active = i < availableCharges;

            slot.Button.interactable = active;
            slot.GlowFx.activeCharge = active;
            slot.GlowFx.placementMode = isInPlacement;
            if (!active) slot.GlowFx.hovered = false;

            if (active)
            {
                slot.CrystalImage.texture = activeChargeTex;
                slot.CrystalImage.color = Color.white;
                slot.CrystalImage.rectTransform.sizeDelta = new Vector2(28f, 45f);
            }
            else
            {
                slot.CrystalImage.texture = fallbackChargeTex;
                slot.CrystalImage.color = EmptySocketTint;
                slot.CrystalImage.rectTransform.sizeDelta = new Vector2(21f, 34f);
            }
        }

        if (chargeOverflowPill != null)
        {
            bool showOverflow = availableCharges > MaxVisibleChargeIcons;
            chargeOverflowPill.gameObject.SetActive(showOverflow);
            if (showOverflow && chargeOverflowText != null)
                chargeOverflowText.text = $"+{availableCharges - MaxVisibleChargeIcons}";
        }
    }

    private void RefreshPlaqueNumberText()
    {
        if (plaqueScoreLabel == null) return;
        int shown = Mathf.RoundToInt(displayedScore);
        plaqueScoreLabel.text = $"{shown}<size=13.5><color=#857B70> / {activeThreshold}</color></size>";
    }

    // =========================================================
    // SEPARATE HIT TARGETS: HARMONY CORE/PLAQUE vs TERRAIN CHARGES
    // =========================================================

    private void OnHarmonyBadgeClicked()
    {
        TriggerCelebration(milestone: false);
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

        GameAudioManager.Instance?.PlayClick();
        frontSparkleFx?.TriggerBurst(5);

        if (HexPlacementController.Instance != null)
        {
            if (HexPlacementController.Instance.IsInPlacementMode)
                HexPlacementController.Instance.CancelPlacement();
            else
                HexPlacementController.Instance.StartPlacement();
        }
    }

    // =========================================================
    // HUD CONSTRUCTION
    // =========================================================

    private void BuildHarmonyBadgeUI()
    {
        Transform oldBg = transform.Find("Score_BG");
        if (oldBg != null) oldBg.gameObject.SetActive(false);
        if (scoreText != null) scoreText.enabled = false;
        Image legacy = GetComponent<Image>();
        if (legacy != null) legacy.enabled = false;

        // Clean any leftover runtime children on domain reload
        for (int i = rootRect.childCount - 1; i >= 0; i--)
        {
            Transform child = rootRect.GetChild(i);
            if (child.name == "Score_BG" || (scoreText != null && child == scoreText.transform))
                continue;
            Destroy(child.gameObject);
        }

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(256f, 250f);

        // Create isolated Live 3D studio stage
        stage3D = HarmonyHud3DStage.Create(null);
        fallbackChargeTex = Resources.Load<Texture2D>("UI/Harmony/TerrainCharge");
        Texture2D fallbackCoreTex = Resources.Load<Texture2D>("UI/Harmony/HarmonyCore");

        // 1. Core Crystal Cluster centered at (0, +20)
        GameObject clusterGo = new GameObject("CoreCluster", typeof(RectTransform));
        coreClusterRect = (RectTransform)clusterGo.transform;
        coreClusterRect.SetParent(rootRect, false);
        coreClusterRect.anchorMin = coreClusterRect.anchorMax = new Vector2(0.5f, 0.5f);
        coreClusterRect.pivot = new Vector2(0.5f, 0.5f);
        coreClusterRect.anchoredPosition = new Vector2(0f, 20f);
        coreClusterRect.sizeDelta = new Vector2(220f, 220f);

        // Ambient soft halo behind 3D crystal
        backHaloFx = new GameObject("AmbientHalo", typeof(RectTransform), typeof(HarmonySparkleOverlay)).GetComponent<HarmonySparkleOverlay>();
        backHaloFx.transform.SetParent(coreClusterRect, false);
        backHaloFx.rectTransform.sizeDelta = new Vector2(182f, 182f);
        backHaloFx.drawAmbientHalo = true;
        backHaloFx.raycastTarget = false;

        // Clickable Core Hitbox
        GameObject hitGo = new GameObject("HarmonyCoreButton", typeof(RectTransform), typeof(Image), typeof(Button));
        coreHitRect = (RectTransform)hitGo.transform;
        coreHitRect.SetParent(coreClusterRect, false);
        coreHitRect.sizeDelta = new Vector2(166f, 152f);
        Image hitImg = hitGo.GetComponent<Image>();
        hitImg.color = Color.clear;

        Button coreBtn = hitGo.GetComponent<Button>();
        coreBtn.targetGraphic = hitImg;
        coreBtn.transition = Selectable.Transition.None;
        coreBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        coreBtn.onClick.AddListener(OnHarmonyBadgeClicked);

        // Live 3D HarmonyCore RawImage (fallback to cleaned 2D PNG if FBX absent)
        coreRawImage = new GameObject("HarmonyCore3D", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        coreRawImage.transform.SetParent(coreHitRect, false);
        bool has3DCore = stage3D != null && stage3D.HasCoreModel && stage3D.CoreTexture != null;
        coreRawImage.texture = has3DCore ? (Texture)stage3D.CoreTexture : fallbackCoreTex;
        coreRawImage.rectTransform.sizeDelta = has3DCore ? new Vector2(182f, 182f) : new Vector2(150f, 129f);
        coreRawImage.raycastTarget = false;

        // Miter-Joined Clockwise 12h Hexagonal Progress Rim wrapping outside the crystal edge
        progressRimFx = new GameObject("HexProgressRim", typeof(RectTransform), typeof(HarmonyCrystalFx)).GetComponent<HarmonyCrystalFx>();
        progressRimFx.transform.SetParent(coreHitRect, false);
        progressRimFx.rectTransform.sizeDelta = new Vector2(160f, 138f);
        progressRimFx.raycastTarget = false;

        // Delicate additive facet sparkles + gain burst motes over the crystal
        frontSparkleFx = new GameObject("CrystalSparkles", typeof(RectTransform), typeof(HarmonySparkleOverlay)).GetComponent<HarmonySparkleOverlay>();
        frontSparkleFx.transform.SetParent(coreHitRect, false);
        frontSparkleFx.rectTransform.sizeDelta = new Vector2(156f, 136f);
        frontSparkleFx.drawAmbientHalo = false;
        frontSparkleFx.raycastTarget = false;

        WireCoreHoverEvents(hitGo);

        // 2. Six Symmetrical Crown TerrainCharge Sockets (upper & side arc, keeping bottom plaque clear)
        BuildTerrainChargeCrown();

        // 3. Always-On Restrained Warm Stone Harmony Plaque nestled cleanly below the hex
        BuildWarmStonePlaque();

        // 4. Compact Overflow Pill & Subtle Hover Ribbon
        BuildOverflowPill();
        BuildHoverRibbon();

        ApplyResponsiveScale();
        UpdateVisuals();
    }

    private void BuildTerrainChargeCrown()
    {
        chargeSlots.Clear();
        const float crownRadius = 104f;

        for (int i = 0; i < MaxVisibleChargeIcons; i++)
        {
            float angleDeg = ChargeCrownAngles[i];
            Vector2 outward = Quaternion.Euler(0f, 0f, -angleDeg) * Vector2.up;

            GameObject slotGo = new GameObject($"TerrainCharge_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform slotRt = (RectTransform)slotGo.transform;
            slotRt.SetParent(coreClusterRect, false);
            slotRt.anchoredPosition = outward * crownRadius;
            slotRt.sizeDelta = new Vector2(52f, 52f);

            Image hitBg = slotGo.GetComponent<Image>();
            hitBg.color = Color.clear;

            Button btn = slotGo.GetComponent<Button>();
            btn.targetGraphic = hitBg;
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(OnTerrainChargeClicked);

            GameObject visualHolder = new GameObject("Visual", typeof(RectTransform));
            RectTransform visRt = (RectTransform)visualHolder.transform;
            visRt.SetParent(slotRt, false);
            visRt.sizeDelta = new Vector2(44f, 54f);
            visRt.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);

            HarmonyChargeGlowFx glow = new GameObject("Glow", typeof(RectTransform), typeof(HarmonyChargeGlowFx)).GetComponent<HarmonyChargeGlowFx>();
            glow.transform.SetParent(visRt, false);
            glow.rectTransform.sizeDelta = new Vector2(40f, 54f);
            glow.phaseOffset = 0.72f * i;
            glow.raycastTarget = false;

            RawImage crystalImg = new GameObject("Crystal", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            crystalImg.transform.SetParent(visRt, false);
            crystalImg.rectTransform.sizeDelta = new Vector2(26f, 42f);
            crystalImg.raycastTarget = false;

            EventTrigger trig = slotGo.AddComponent<EventTrigger>();
            AddTrigger(trig, EventTriggerType.PointerEnter, _ => { if (btn.interactable) glow.hovered = true; });
            AddTrigger(trig, EventTriggerType.PointerExit, _ => { glow.hovered = false; });

            chargeSlots.Add(new ChargeSlotView
            {
                Button = btn,
                CrystalImage = crystalImg,
                GlowFx = glow
            });
        }
    }

    private void BuildWarmStonePlaque()
    {
        // Nestled cleanly below the hex progress rim (top at y = -63, progress bottom at y = -54 => ~9px crisp gap)
        Image plaqueImg = LumiWorldUITheme.CreateRoundedSurface(
            rootRect, "HarmonyWarmStonePlaque", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 18, borderThickness: 1.4f, shadowDepth: 3f, shadowAlpha: 0.16f, raycastTarget: true);
        plaqueRect = plaqueImg.rectTransform;
        plaqueRect.anchorMin = plaqueRect.anchorMax = new Vector2(0.5f, 0.5f);
        plaqueRect.pivot = new Vector2(0.5f, 0.5f);
        plaqueRect.anchoredPosition = new Vector2(0f, -86f);
        plaqueRect.sizeDelta = new Vector2(196f, 44f);

        Button plaqueBtn = plaqueImg.gameObject.AddComponent<Button>();
        plaqueBtn.targetGraphic = plaqueImg;
        plaqueBtn.transition = Selectable.Transition.None;
        plaqueBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        plaqueBtn.onClick.AddListener(OnHarmonyBadgeClicked);
        WireCoreHoverEvents(plaqueImg.gameObject);

        // Restrained muted glacial-teal diamond pip on the left
        GameObject pipGo = new GameObject("HarmonyPip", typeof(RectTransform), typeof(Image));
        RectTransform pipRt = (RectTransform)pipGo.transform;
        pipRt.SetParent(plaqueRect, false);
        pipRt.anchorMin = pipRt.anchorMax = new Vector2(0f, 0.5f);
        pipRt.pivot = new Vector2(0.5f, 0.5f);
        pipRt.anchoredPosition = new Vector2(17f, 0f);
        pipRt.sizeDelta = new Vector2(8f, 8f);
        pipRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image pipImg = pipGo.GetComponent<Image>();
        pipImg.color = new Color(0.28f, 0.67f, 0.64f, 0.92f);
        pipImg.raycastTarget = false;

        // Tracked uppercase HARMONY caption in Philosopher Regular
        TextMeshProUGUI caption = CreateDisplayTMP(
            "Caption", plaqueRect, 10.5f, LumiWorldUITheme.Muted,
            LumiWorldUITheme.DisplayRegular, TextAlignmentOptions.MidlineLeft);
        caption.characterSpacing = 9f;
        caption.text = "HARMONY";
        RectTransform capRt = caption.rectTransform;
        capRt.anchorMin = capRt.anchorMax = new Vector2(0f, 0.5f);
        capRt.pivot = new Vector2(0f, 0.5f);
        capRt.anchoredPosition = new Vector2(28f, 0f);
        capRt.sizeDelta = new Vector2(68f, 24f);

        // Subtle warm-stone vertical divider
        GameObject divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        RectTransform divRt = (RectTransform)divGo.transform;
        divRt.SetParent(plaqueRect, false);
        divRt.anchorMin = divRt.anchorMax = new Vector2(0f, 0.5f);
        divRt.pivot = new Vector2(0.5f, 0.5f);
        divRt.anchoredPosition = new Vector2(96f, 0f);
        divRt.sizeDelta = new Vector2(1.2f, 18f);
        Image divImg = divGo.GetComponent<Image>();
        divImg.color = LumiWorldUITheme.Edge;
        divImg.raycastTarget = false;

        // Sculpted Harmony Score in Philosopher Bold (always readable, restrained dark charcoal)
        plaqueScoreLabel = CreateDisplayTMP(
            "ScoreValue", plaqueRect, 22f, new Color(0.18f, 0.14f, 0.11f, 1f),
            LumiWorldUITheme.Display, TextAlignmentOptions.MidlineRight);
        plaqueScoreLabel.enableAutoSizing = true;
        plaqueScoreLabel.fontSizeMin = 15f;
        plaqueScoreLabel.fontSizeMax = 22f;
        plaqueScoreRect = plaqueScoreLabel.rectTransform;
        plaqueScoreRect.anchorMin = plaqueScoreRect.anchorMax = new Vector2(1f, 0.5f);
        plaqueScoreRect.pivot = new Vector2(0.5f, 0.5f);
        plaqueScoreRect.anchoredPosition = new Vector2(-50f, 1f);
        plaqueScoreRect.sizeDelta = new Vector2(86f, 34f);
        plaqueScoreLabel.text = "0 / 10";
    }

    private void BuildOverflowPill()
    {
        Image overImg = LumiWorldUITheme.CreateRoundedSurface(
            rootRect, "ChargeOverflowPill", LumiWorldUITheme.Primary, Color.clear,
            radius: 11, borderThickness: 0f, shadowDepth: 2f, shadowAlpha: 0.18f, raycastTarget: true);
        chargeOverflowPill = overImg.rectTransform;
        chargeOverflowPill.anchorMin = chargeOverflowPill.anchorMax = new Vector2(0.5f, 0.5f);
        chargeOverflowPill.pivot = new Vector2(0.5f, 0.5f);
        chargeOverflowPill.sizeDelta = new Vector2(38f, 22f);
        chargeOverflowPill.anchoredPosition = new Vector2(104f, -54f);

        Button overBtn = overImg.gameObject.AddComponent<Button>();
        overBtn.targetGraphic = overImg;
        overBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        overBtn.onClick.AddListener(OnTerrainChargeClicked);

        chargeOverflowText = CreateDisplayTMP(
            "OverflowText", chargeOverflowPill, 11.5f, Color.white,
            LumiWorldUITheme.Display, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(chargeOverflowText.rectTransform);
        chargeOverflowText.text = "+1";
        chargeOverflowPill.gameObject.SetActive(false);
    }

    private void BuildHoverRibbon()
    {
        // Appears cleanly to the right of the plaque on hover (zero overlap with progress rim)
        Image ribImg = LumiWorldUITheme.CreateRoundedSurface(
            rootRect, "HoverDetailRibbon", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 14, borderThickness: 1.2f, shadowDepth: 2.5f, shadowAlpha: 0.14f, raycastTarget: false);
        RectTransform ribRt = ribImg.rectTransform;
        ribRt.anchorMin = ribRt.anchorMax = new Vector2(0.5f, 0.5f);
        ribRt.pivot = new Vector2(0f, 0.5f);
        ribRt.anchoredPosition = new Vector2(106f, -86f);
        ribRt.sizeDelta = new Vector2(214f, 34f);

        hoverRibbonGroup = ribImg.gameObject.AddComponent<CanvasGroup>();
        hoverRibbonGroup.alpha = 0f;
        hoverRibbonGroup.interactable = false;
        hoverRibbonGroup.blocksRaycasts = false;

        hoverRibbonText = CreateDisplayTMP(
            "RibbonText", ribRt, 12f, LumiWorldUITheme.Muted,
            LumiWorldUITheme.DisplayRegular, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(hoverRibbonText.rectTransform, 10f);
        hoverRibbonText.text = "Achievements";
    }

    private void WireCoreHoverEvents(GameObject target)
    {
        EventTrigger trig = target.GetComponent<EventTrigger>();
        if (trig == null) trig = target.AddComponent<EventTrigger>();

        AddTrigger(trig, EventTriggerType.PointerEnter, _ => SetCoreHovered(true));
        AddTrigger(trig, EventTriggerType.PointerExit, _ => SetCoreHovered(false));
    }

    private void SetCoreHovered(bool hovered)
    {
        isCoreHovered = hovered;
        if (progressRimFx != null) progressRimFx.hovered = hovered;
        if (backHaloFx != null) backHaloFx.hovered = hovered;
        if (frontSparkleFx != null) frontSparkleFx.hovered = hovered;
        if (!hovered && stage3D != null) stage3D.SetCoreHover(false, Vector2.zero);
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private static TextMeshProUGUI CreateDisplayTMP(
        string name,
        RectTransform parent,
        float fontSize,
        Color color,
        TMP_FontAsset font,
        TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)obj.transform;
        rt.SetParent(parent, false);

        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : LumiWorldUITheme.Body;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }
}

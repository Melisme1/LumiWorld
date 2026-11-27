using System.Collections.Generic;
using LumiWorld.Acs;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Bottom-Left Harmony HUD Widget:
/// - Aligned level with the bottom Card Hand tray
/// - Live 3D HarmonyCore crystal (RenderTexture studio with mouse parallax & orbiting facet glint)
/// - True miter-joined flat-top hexagonal progress rim starting at 12 o'clock clockwise
/// - Refined additive crystal sparkle & gain-burst motes
/// - Up to 6 Live 3D TerrainCharge crystals appearing symmetrically along the upper crown ONLY when available
/// - Compact hover-only Warm Stone Harmony Plaque above the hex (balanced HorizontalLayout spacing, zero overlap with progress rim)
/// </summary>
public class ScoreUI : MonoBehaviour
{
    public static ScoreUI Instance { get; private set; }

    [Header("Scene Reference")]
    [SerializeField] private TMP_Text scoreText;

    private const int MaxVisibleChargeIcons = 6;
    private const float ChargeCrownRadius = 96f;

    // Hover plaque Y offsets above crystal center (clears hex top edge +73.5 and active top crown charges)
    private const float PlaqueHoverYNoCharges = 100f;
    private const float PlaqueHoverYWithCharges = 140f;
    private const float PlaqueSlideDistance = 6f;

    private sealed class ChargeSlotView
    {
        public RectTransform SlotRect;
        public RectTransform VisualRect;
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

    // Hover-only Warm Stone Plaque (auto-sized with balanced internal spacing)
    private RectTransform plaqueRect;
    private CanvasGroup plaqueGroup;
    private TextMeshProUGUI plaqueScoreLabel;
    private RectTransform plaqueScoreRect;

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
    private float hoverReveal;
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

        if (CurrencyWallet.Instance != null)
        {
            CurrencyWallet.Instance.OnBalanceChanged += HandleWalletBalanceChanged;
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

        if (CurrencyWallet.HasInstance)
        {
            CurrencyWallet.Instance.OnBalanceChanged -= HandleWalletBalanceChanged;
        }
    }

    private void HandleWalletBalanceChanged(int balance, int delta, CoinReason reason)
    {
        if (availableCharges > 0)
        {
            UpdateVisuals();
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
            float bump = 1f + Mathf.Sin((scorePunchTimer / 0.36f) * Mathf.PI) * 0.08f;
            plaqueScoreRect.localScale = Vector3.one * bump;
        }
        else if (plaqueScoreRect != null && plaqueScoreRect.localScale != Vector3.one)
        {
            plaqueScoreRect.localScale = Vector3.one;
        }

        // Smooth hover-only reveal & subtle upward glide for the Warm Stone plaque
        if (plaqueGroup != null && plaqueRect != null)
        {
            hoverReveal = Mathf.MoveTowards(hoverReveal, isCoreHovered ? 1f : 0f, dt * 7.5f);
            float eased = hoverReveal * hoverReveal * (3f - 2f * hoverReveal);
            plaqueGroup.alpha = eased;

            float baseY = availableCharges > 0 ? PlaqueHoverYWithCharges : PlaqueHoverYNoCharges;
            plaqueRect.anchoredPosition = new Vector2(0f, baseY - (1f - eased) * PlaqueSlideDistance);
        }

        // Live 3D mouse-tracking parallax while hovered
        if (stage3D != null && isCoreHovered && coreHitRect != null)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(coreHitRect, mouse.position.ReadValue(), null, out Vector2 localPt))
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
        // Aligned cleanly level with the bottom Card Hand tray (y = 8..170 in 1280x720 design units)
        rootRect.anchoredPosition = new Vector2(114f * fit, 90f * fit);
    }

    private void UpdateVisuals()
    {
        RefreshPlaqueNumberText();

        if (stage3D != null)
            stage3D.SetChargesState(availableCharges > 0, isInPlacement);

        Texture activeChargeTex = (stage3D != null && stage3D.HasChargeModel && stage3D.ChargeTexture != null)
            ? (Texture)stage3D.ChargeTexture
            : fallbackChargeTex;

        int visibleCount = Mathf.Clamp(availableCharges, 0, MaxVisibleChargeIcons);
        float stepAngle = visibleCount <= 1 ? 0f : Mathf.Min(42f, 204f / (visibleCount - 1));
        float startAngle = -0.5f * stepAngle * (visibleCount - 1);

        bool canAffordExpansion = HexPlacementController.Instance == null || HexPlacementController.Instance.CanAffordNextExpansion();
        Color crystalTint = canAffordExpansion ? Color.white : new Color(1f, 0.72f, 0.72f, 0.82f);

        for (int i = 0; i < chargeSlots.Count; i++)
        {
            ChargeSlotView slot = chargeSlots[i];
            bool active = i < visibleCount;

            slot.Button.gameObject.SetActive(active);
            if (!active)
            {
                slot.GlowFx.activeCharge = false;
                slot.GlowFx.hovered = false;
                continue;
            }

            float angleDeg = startAngle + stepAngle * i;
            Vector2 outward = Quaternion.Euler(0f, 0f, -angleDeg) * Vector2.up;
            slot.SlotRect.anchoredPosition = outward * ChargeCrownRadius;
            slot.VisualRect.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);

            slot.Button.interactable = true;
            slot.GlowFx.activeCharge = true;
            slot.GlowFx.placementMode = isInPlacement;
            slot.CrystalImage.texture = activeChargeTex;
            slot.CrystalImage.color = crystalTint;
            slot.CrystalImage.rectTransform.sizeDelta = new Vector2(28f, 45f);
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
        string text = $"{shown}<size=13><color=#857B70> / {activeThreshold}</color></size>";
        if (availableCharges > 0 && HexPlacementController.Instance != null)
        {
            int cost = HexPlacementController.Instance.GetNextExpansionCoinCost();
            if (cost > 0)
            {
                string costColor = HexPlacementController.Instance.CanAffordNextExpansion() ? "#B45309" : "#B91C1C";
                text += $"<size=12><color={costColor}>  ·  {cost}c</color></size>";
            }
        }
        plaqueScoreLabel.text = text;
        if (plaqueRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(plaqueRect);
    }

    // =========================================================
    // SEPARATE HIT TARGETS: HARMONY CORE vs TERRAIN CHARGES
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
        rootRect.sizeDelta = new Vector2(228f, 228f);

        // Create isolated Live 3D studio stage
        stage3D = HarmonyHud3DStage.Create(null);
        fallbackChargeTex = Resources.Load<Texture2D>("UI/Harmony/TerrainCharge");
        Texture2D fallbackCoreTex = Resources.Load<Texture2D>("UI/Harmony/HarmonyCore");

        // 1. Core Crystal Cluster centered cleanly at (0, 0)
        GameObject clusterGo = new GameObject("CoreCluster", typeof(RectTransform));
        coreClusterRect = (RectTransform)clusterGo.transform;
        coreClusterRect.SetParent(rootRect, false);
        coreClusterRect.anchorMin = coreClusterRect.anchorMax = new Vector2(0.5f, 0.5f);
        coreClusterRect.pivot = new Vector2(0.5f, 0.5f);
        coreClusterRect.anchoredPosition = Vector2.zero;
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

        // 2. Six Symmetrical Crown TerrainCharge Slots (shown ONLY when available)
        BuildTerrainChargeCrown();

        // 3. Hover-Only Compact Warm Stone Plaque above the hex (balanced internal whitespace)
        BuildHoverWarmStonePlaque();

        // 4. Compact Overflow Pill when availableCharges > 6
        BuildOverflowPill();

        ApplyResponsiveScale();
        UpdateVisuals();
    }

    private void BuildTerrainChargeCrown()
    {
        chargeSlots.Clear();

        for (int i = 0; i < MaxVisibleChargeIcons; i++)
        {
            GameObject slotGo = new GameObject($"TerrainCharge_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform slotRt = (RectTransform)slotGo.transform;
            slotRt.SetParent(coreClusterRect, false);
            slotRt.anchoredPosition = Vector2.up * ChargeCrownRadius;
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

            HarmonyChargeGlowFx glow = new GameObject("Glow", typeof(RectTransform), typeof(HarmonyChargeGlowFx)).GetComponent<HarmonyChargeGlowFx>();
            glow.transform.SetParent(visRt, false);
            glow.rectTransform.sizeDelta = new Vector2(40f, 54f);
            glow.phaseOffset = 0.72f * i;
            glow.raycastTarget = false;

            RawImage crystalImg = new GameObject("Crystal", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            crystalImg.transform.SetParent(visRt, false);
            crystalImg.rectTransform.sizeDelta = new Vector2(28f, 45f);
            crystalImg.raycastTarget = false;

            EventTrigger trig = slotGo.AddComponent<EventTrigger>();
            AddTrigger(trig, EventTriggerType.PointerEnter, _ => { if (btn.interactable) glow.hovered = true; });
            AddTrigger(trig, EventTriggerType.PointerExit, _ => { glow.hovered = false; });

            slotGo.SetActive(false);

            chargeSlots.Add(new ChargeSlotView
            {
                SlotRect = slotRt,
                VisualRect = visRt,
                Button = btn,
                CrystalImage = crystalImg,
                GlowFx = glow
            });
        }
    }

    private void BuildHoverWarmStonePlaque()
    {
        // Compact auto-hugging Warm Stone pill floating cleanly above the hex ONLY on hover
        Image plaqueImg = LumiWorldUITheme.CreateRoundedSurface(
            rootRect, "HarmonyWarmStonePlaque", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge,
            radius: 16, borderThickness: 1.3f, shadowDepth: 2.5f, shadowAlpha: 0.16f, raycastTarget: false);
        plaqueRect = plaqueImg.rectTransform;
        plaqueRect.anchorMin = plaqueRect.anchorMax = new Vector2(0.5f, 0.5f);
        plaqueRect.pivot = new Vector2(0.5f, 0.5f);
        plaqueRect.anchoredPosition = new Vector2(0f, PlaqueHoverYNoCharges - PlaqueSlideDistance);
        plaqueRect.sizeDelta = new Vector2(160f, 38f);

        plaqueGroup = plaqueImg.gameObject.AddComponent<CanvasGroup>();
        plaqueGroup.alpha = 0f;
        plaqueGroup.interactable = false;
        plaqueGroup.blocksRaycasts = false;

        HorizontalLayoutGroup hlg = plaqueImg.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 16, 0, 0);
        hlg.spacing = 9f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter csf = plaqueImg.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 1. Subtle glacial-teal diamond pip
        GameObject pipWrap = new GameObject("PipWrap", typeof(RectTransform), typeof(LayoutElement));
        pipWrap.transform.SetParent(plaqueRect, false);
        LayoutElement pipLe = pipWrap.GetComponent<LayoutElement>();
        pipLe.minWidth = 8f;
        pipLe.preferredWidth = 8f;
        pipLe.preferredHeight = 20f;

        GameObject pipGo = new GameObject("HarmonyPip", typeof(RectTransform), typeof(Image));
        RectTransform pipRt = (RectTransform)pipGo.transform;
        pipRt.SetParent(pipWrap.transform, false);
        pipRt.anchorMin = pipRt.anchorMax = new Vector2(0.5f, 0.5f);
        pipRt.pivot = new Vector2(0.5f, 0.5f);
        pipRt.sizeDelta = new Vector2(7.5f, 7.5f);
        pipRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image pipImg = pipGo.GetComponent<Image>();
        pipImg.color = new Color(0.28f, 0.67f, 0.64f, 0.92f);
        pipImg.raycastTarget = false;

        // 2. Tracked uppercase HARMONY caption
        TextMeshProUGUI caption = CreateDisplayTMP(
            "Caption", plaqueRect, 10f, LumiWorldUITheme.Muted,
            LumiWorldUITheme.DisplayRegular, TextAlignmentOptions.MidlineLeft);
        caption.characterSpacing = 7f;
        caption.text = "HARMONY";
        caption.rectTransform.sizeDelta = new Vector2(62f, 24f);
        LayoutElement capLe = caption.gameObject.AddComponent<LayoutElement>();
        capLe.preferredHeight = 24f;

        // 3. Hairline warm-stone vertical divider
        GameObject divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        divGo.transform.SetParent(plaqueRect, false);
        RectTransform divRt = (RectTransform)divGo.transform;
        divRt.sizeDelta = new Vector2(1.2f, 16f);
        Image divImg = divGo.GetComponent<Image>();
        divImg.color = LumiWorldUITheme.Edge;
        divImg.raycastTarget = false;
        LayoutElement divLe = divGo.GetComponent<LayoutElement>();
        divLe.minWidth = 1.2f;
        divLe.preferredWidth = 1.2f;
        divLe.preferredHeight = 16f;

        // 4. Sculpted Harmony Score in Philosopher Bold (hugs content tightly with zero hollow gap)
        plaqueScoreLabel = CreateDisplayTMP(
            "ScoreValue", plaqueRect, 20f, new Color(0.18f, 0.14f, 0.11f, 1f),
            LumiWorldUITheme.Display, TextAlignmentOptions.MidlineLeft);
        plaqueScoreRect = plaqueScoreLabel.rectTransform;
        plaqueScoreRect.sizeDelta = new Vector2(60f, 30f);
        LayoutElement scoreLe = plaqueScoreLabel.gameObject.AddComponent<LayoutElement>();
        scoreLe.preferredHeight = 30f;
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
        chargeOverflowPill.sizeDelta = new Vector2(36f, 22f);
        chargeOverflowPill.anchoredPosition = new Vector2(92f, -54f);

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

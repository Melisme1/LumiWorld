using System;
using System.Collections;
using System.Collections.Generic;
using LumiWorld.Acs;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Top-right inspect panel for Buildings and Streams, strictly synchronized with P-05 (HabitatCreaturePanelUI) & P-06:
/// - Opens ONLY on Double-Click (0.35s) to prevent accidental clicks on the map.
/// - Same 416 du main width & 314 du left drawer width as P-05 / P-06, docked cleanly at y = -82 du below the top HUD.
/// - Concise scannable hierarchy: Card Artwork + Lv pill + Footprint + Live Effect card + Local Auto-Feed Slot Editor.
/// - Bidirectional live sync with AutoFeedUI: editing slots locally updates AutoFeedUI immediately and vice versa.
/// </summary>
[DefaultExecutionOrder(-50)]
public class MapStructureInfoUI : MonoBehaviour
{
    private static MapStructureInfoUI instance;
    public static MapStructureInfoUI Instance => instance;

    private enum InspectedKind
    {
        None = 0,
        Building = 1,
        River = 2
    }

    private const float PanelWidth = 416f;
    private const float AssignPopupWidth = 920f;
    private const float AssignPopupHeight = 580f;

    private const float GranaryPanelHeight = 412f;
    private const float BuildingCompactHeight = 254f;
    private const float BuildingDirectUpgradeHeight = 314f;
    private const float RiverPanelHeight = 368f;

    private const float DoubleClickMaxInterval = 0.35f;
    private const float DoubleClickMaxDistance = 14f;

    private sealed class SlotRowView
    {
        public RectTransform root;
        public Image bg;
        public Image selectedBorder;
        public Image iconWell;
        public Image icon;
        public TextMeshProUGUI title;
        public TextMeshProUGUI sub;
        public Image chipBg;
        public TextMeshProUGUI chipText;
        public Button button;
    }

    private GameObject rootModal;
    private RectTransform stageRect;
    private RectTransform panelRect;

    // Header (mirrors P-05)
    private Image emblemImage;
    private Image emblemFallbackBg;
    private Image emblemFallbackIcon;
    private TextMeshProUGUI titleText;
    private Image levelPillBg;
    private TextMeshProUGUI levelPillText;
    private RectTransform footprintGroup;
    private Image footprintGlyphImg;
    private TextMeshProUGUI subtitleText;

    // Section 1: Concise Effect Card
    private Image effectCardBg;
    private Image effectWellBg;
    private Image effectIcon;
    private TextMeshProUGUI effectTitleText;
    private TextMeshProUGUI effectText;

    // Section 2A: Granary Post 3-Slot Cards
    private GameObject slotsSectionObj;
    private TextMeshProUGUI slotsHeaderText;
    private TextMeshProUGUI slotsHintText;
    private readonly SlotRowView[] slotRows = new SlotRowView[3];
    private TextMeshProUGUI mainFeedbackText;

    // Section 2B: Stat Pair + Optional Stream Bonus List
    private GameObject metricsSectionObj;
    private TextMeshProUGUI metric1Label;
    private TextMeshProUGUI metric1Value;
    private TextMeshProUGUI metric1Sub;
    private TextMeshProUGUI metric2Label;
    private TextMeshProUGUI metric2Value;
    private TextMeshProUGUI metric2Sub;

    private GameObject rulesContainer;
    private TextMeshProUGUI rulesHeaderText;
    private Image rulesCardBg;
    private readonly TextMeshProUGUI[] ruleLeftTexts = new TextMeshProUGUI[3];
    private readonly TextMeshProUGUI[] ruleRightTexts = new TextMeshProUGUI[3];
    private readonly GameObject[] ruleRows = new GameObject[3];

    // Bottom CTA
    private Button actionButton;
    private Image actionButtonBg;
    private TextMeshProUGUI actionButtonLabel;

    // Centered Assign Food Popup (mirrors Habitat P-07 Assign Resource Dialog)
    private GameObject slotPickerOverlay;
    private RectTransform slotPickerRect;
    private TextMeshProUGUI pickerHeaderTitle;
    private TextMeshProUGUI pickerHeaderSub;
    private Button pickerClearBtn;
    private RectTransform pickerFoodContent;
    private TextMeshProUGUI pickerEmptyFoodText;
    private Image pickerDetailIcon;
    private TextMeshProUGUI pickerDetailName;
    private TextMeshProUGUI pickerDetailStatus;
    private GameObject pickerCapCard;
    private TextMeshProUGUI pickerCapStockText;
    private TextMeshProUGUI pickerCapValueText;
    private Button pickerDecBtn;
    private Button pickerIncBtn;
    private Button pickerMaxBtn;
    private Slider pickerCapSlider;
    private Button pickerAssignBtn;
    private TextMeshProUGUI pickerFeedbackText;

    private readonly Dictionary<string, Image> pickerFoodBorders = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
    private string pendingFoodId = string.Empty;
    private int pendingBudget = 0;
    private bool suppressSliderCallback;

    // Click & double-click tracking
    private Vector2 pressPosition;
    private bool mapPress;
    private InspectedKind lastClickKind = InspectedKind.None;
    private HexCoordinates lastClickHex;
    private float lastClickTime = -10f;
    private Vector2 lastClickPos;

    private bool isQuitting;
    private InspectedKind currentKind = InspectedKind.None;
    private HexCoordinates currentHex;
    private string currentBuildingClusterKey;
    private int editingSlotIndex = -1;
    private Coroutine popRoutine;
    private Coroutine sidePopRoutine;
    private Coroutine feedbackFadeRoutine;
    private Coroutine pickerFeedbackRoutine;

    public bool IsOpen => rootModal != null && rootModal.activeSelf;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        AutoFeedUI.OnClusterSlotsUpdated += HandleAutoFeedClusterUpdated;
        SettlementClusterManager.OnBuildingClustersChanged += HandleBuildingClustersChanged;
        BuildUI();
    }

    private void Start()
    {
        if (rootModal != null)
        {
            EconomyHUD.RegisterWindow(rootModal, () =>
            {
                if (IsSlotPickerOpen()) CloseSlotPicker();
                else Close();
            });
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        AutoFeedUI.OnClusterSlotsUpdated -= HandleAutoFeedClusterUpdated;
        SettlementClusterManager.OnBuildingClustersChanged -= HandleBuildingClustersChanged;
        EconomyHUD.UnregisterWindow(rootModal);
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (IsOpen)
        {
            SyncStageScale();
        }

        if (WorldLaunch.IsVisit) return;
        HandleMapClick();
    }

    private void HandleAutoFeedClusterUpdated(string clusterKey)
    {
        if (!IsOpen || currentKind != InspectedKind.Building) return;
        if (!string.IsNullOrEmpty(clusterKey) &&
            !string.Equals(clusterKey, currentBuildingClusterKey, StringComparison.Ordinal))
        {
            return;
        }

        var worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen != null)
        {
            RefreshBuildingView(currentHex, worldGen);
        }
        if (IsSlotPickerOpen())
        {
            RefreshSlotPickerUI();
        }
    }

    private void HandleBuildingClustersChanged()
    {
        if (!IsOpen || currentKind != InspectedKind.Building) return;
        var mgr = SettlementClusterManager.Instance;
        if (mgr == null || !mgr.HasSettlement(currentHex))
        {
            Close();
            return;
        }

        var worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen != null)
        {
            RefreshBuildingView(currentHex, worldGen);
            if (IsSlotPickerOpen()) RefreshSlotPickerUI();
        }
    }

    private void HandleMapClick()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 pos = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressPosition = pos;
            mapPress = !EconomyHUD.IsPointerOverInteractiveUI(pos);
        }

        if (!mouse.leftButton.wasReleasedThisFrame || !mapPress) return;
        mapPress = false;

        if (Vector2.Distance(pos, pressPosition) > 6f || EconomyHUD.IsPointerOverInteractiveUI(pos)) return;
        if (!CanInspectWorld()) return;

        if (TryResolveStructureAtScreenPos(pos, out InspectedKind kind, out HexCoordinates hex))
        {
            float now = Time.unscaledTime;
            bool isDoubleClick =
                kind == lastClickKind &&
                hex == lastClickHex &&
                (now - lastClickTime) <= DoubleClickMaxInterval &&
                Vector2.Distance(pos, lastClickPos) <= DoubleClickMaxDistance;

            if (isDoubleClick)
            {
                lastClickKind = InspectedKind.None;
                lastClickTime = -10f;
                if (kind == InspectedKind.Building) OpenForBuilding(hex);
                else if (kind == InspectedKind.River) OpenForRiver(hex);
            }
            else
            {
                lastClickKind = kind;
                lastClickHex = hex;
                lastClickTime = now;
                lastClickPos = pos;
            }
        }
        else
        {
            lastClickKind = InspectedKind.None;
            lastClickTime = -10f;
        }
    }

    public bool ShouldInterceptMapHit(in RaycastHit hit)
    {
        if (hit.collider == null) return false;
        return TryResolveStructureFromHit(hit, out _, out _);
    }

    private bool CanInspectWorld()
    {
        if (WildAlphaInteractable.IsModalOpen) return false;
        var kb = Keyboard.current;
        if (kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed)) return false;
        foreach (var drag in FindObjectsByType<CardDrag>())
        {
            if (drag != null && drag.IsDragging) return false;
        }
        if (TerraformToolController.IsActiveToolMode) return false;
        return true;
    }

    public void OpenForBuilding(HexCoordinates hex)
    {
        var worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return;

        bool wasOpen = IsOpen;
        bool switchedHex = !wasOpen || currentKind != InspectedKind.Building || currentHex != hex;
        currentKind = InspectedKind.Building;
        currentHex = hex;

        if (switchedHex)
        {
            CloseSlotPicker(refreshParent: false);
            if (mainFeedbackText != null) mainFeedbackText.text = string.Empty;
        }

        ShowModal(!wasOpen);
        RefreshBuildingView(hex, worldGen);
        BiomeBuffVisualManager.Instance?.ShowSelectedBuildingBuff(hex, worldGen);
        GameAudioManager.Instance?.PlayClick();
    }

    public void OpenForRiver(HexCoordinates hex)
    {
        var worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return;

        bool wasOpen = IsOpen;
        currentKind = InspectedKind.River;
        currentHex = hex;
        currentBuildingClusterKey = string.Empty;
        CloseSlotPicker(refreshParent: false);
        if (mainFeedbackText != null) mainFeedbackText.text = string.Empty;

        ShowModal(!wasOpen);
        RefreshRiverView(hex);
        BiomeBuffVisualManager.Instance?.ShowSelectedRiverBuff(hex, worldGen);
        GameAudioManager.Instance?.PlayClick();
    }

    public void Close()
    {
        bool wasOpen = IsOpen;
        currentKind = InspectedKind.None;
        currentBuildingClusterKey = string.Empty;
        CloseSlotPicker(refreshParent: false);

        if (popRoutine != null)
        {
            StopCoroutine(popRoutine);
            popRoutine = null;
        }

        if (rootModal != null && rootModal.activeSelf)
        {
            rootModal.SetActive(false);
            EconomyHUD.MarkWindowClosed();
        }

        BiomeBuffVisualManager.ExistingInstance?.ClearSelectionBuff();
        if (wasOpen && !isQuitting) GameAudioManager.Instance?.PlayClick();
    }

    private void OnBackdropClicked()
    {
        var mouse = Mouse.current;
        if (mouse != null && CanInspectWorld())
        {
            Vector2 pos = mouse.position.ReadValue();
            if (TryResolveStructureAtScreenPos(pos, out InspectedKind kind, out HexCoordinates hex))
            {
                if (kind == currentKind && hex == currentHex)
                {
                    Close();
                    return;
                }

                // Record first click so quick double-clicking onto another Building/River opens it right away
                lastClickKind = kind;
                lastClickHex = hex;
                lastClickTime = Time.unscaledTime;
                lastClickPos = pos;
                Close();
                return;
            }
        }

        lastClickKind = InspectedKind.None;
        lastClickTime = -10f;
        Close();
    }

    private void ShowModal(bool animateEntrance)
    {
        if (rootModal == null) BuildUI();
        rootModal.SetActive(true);
        EconomyHUD.BringWindowToFront(rootModal);
        SyncStageScale();

        if (animateEntrance && panelRect != null && gameObject.activeInHierarchy)
        {
            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(AnimateRectPopIn(panelRect, r => popRoutine = null));
        }
        else if (panelRect != null)
        {
            panelRect.localScale = Vector3.one;
        }
    }

    private IEnumerator AnimateRectPopIn(RectTransform target, Action<RectTransform> onDone, float targetScale = 1f)
    {
        float duration = 0.14f;
        float elapsed = 0f;
        target.localScale = Vector3.one * (targetScale * 0.94f);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            float s = targetScale * Mathf.Lerp(0.94f, 1f, ease);
            target.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        target.localScale = Vector3.one * targetScale;
        onDone?.Invoke(target);
    }

    private bool IsSlotPickerOpen() => slotPickerOverlay != null && slotPickerOverlay.activeSelf && editingSlotIndex >= 0;

    private void OpenSlotPicker(int slotIndex)
    {
        if (currentKind != InspectedKind.Building || string.IsNullOrEmpty(currentBuildingClusterKey)) return;
        var cluster = SettlementClusterManager.Instance != null
            ? SettlementClusterManager.Instance.GetClusterInfo(currentHex)
            : null;
        int unlocked = cluster != null ? cluster.UnlockedFoodSlots : 1;
        if (slotIndex < 0 || slotIndex >= unlocked) return;

        bool wasClosed = !IsSlotPickerOpen();
        editingSlotIndex = slotIndex;

        // Pre-seed pending selection from existing slot value (or clear so first eligible food picks automatically)
        if (AutoFeedUI.Instance != null &&
            AutoFeedUI.Instance.TryGetClusterSlot(currentBuildingClusterKey, editingSlotIndex, out string curId, out int curBud))
        {
            pendingFoodId = curId;
            pendingBudget = curBud;
        }
        else
        {
            pendingFoodId = string.Empty;
            pendingBudget = 0;
        }

        if (slotPickerOverlay != null)
        {
            slotPickerOverlay.SetActive(true);
            slotPickerOverlay.transform.SetAsLastSibling();
            float modalScale = LumiWorldUITheme.ComputeResponsiveModalScale(this, AssignPopupWidth, AssignPopupHeight, marginX: 44f, marginY: 88f);
            if (wasClosed && gameObject.activeInHierarchy)
            {
                if (sidePopRoutine != null) StopCoroutine(sidePopRoutine);
                sidePopRoutine = StartCoroutine(AnimateRectPopIn(slotPickerRect, _ => sidePopRoutine = null, modalScale));
            }
            else if (slotPickerRect != null)
            {
                slotPickerRect.localScale = Vector3.one * modalScale;
            }
        }

        if (pickerFeedbackText != null) pickerFeedbackText.text = string.Empty;
        RefreshSlotPickerUI();
        UpdateSlotSelectionOutlines();
        GameAudioManager.Instance?.PlayClick();
    }

    private void CloseSlotPicker(bool refreshParent = true)
    {
        editingSlotIndex = -1;
        pendingFoodId = string.Empty;
        pendingBudget = 0;
        if (sidePopRoutine != null)
        {
            StopCoroutine(sidePopRoutine);
            sidePopRoutine = null;
        }
        if (slotPickerOverlay != null && slotPickerOverlay.activeSelf)
        {
            slotPickerOverlay.SetActive(false);
        }
        if (refreshParent)
        {
            UpdateSlotSelectionOutlines();
        }
    }

    private void UpdateSlotSelectionOutlines()
    {
        for (int i = 0; i < 3; i++)
        {
            if (slotRows[i] != null && slotRows[i].selectedBorder != null)
            {
                slotRows[i].selectedBorder.gameObject.SetActive(IsSlotPickerOpen() && editingSlotIndex == i);
            }
        }
    }

    private void RefreshBuildingView(HexCoordinates hex, HexWorldGenerator worldGen)
    {
        var cluster = SettlementClusterManager.Instance != null
            ? SettlementClusterManager.Instance.GetClusterInfo(hex)
            : null;

        SettlementTile tile = null;
        Sprite cardArtwork = null;
        if (worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) && tileObj != null)
        {
            tile = tileObj.GetComponent<SettlementTile>();
            cardArtwork = ResolveBuildingCardSprite(tileObj);
        }

        string displayName = cluster != null && !string.IsNullOrEmpty(cluster.DisplayName)
            ? cluster.DisplayName
            : (tile != null && !string.IsNullOrEmpty(tile.BuildingDisplayName) ? tile.BuildingDisplayName : BuildingRules.GranaryPostDisplayName);
        string typeId = cluster != null ? cluster.BuildingTypeId : (tile != null ? tile.BuildingTypeId : BuildingRules.GranaryPostTypeId);
        BuildingUpgradeMethod upgradeMethod = cluster != null ? cluster.UpgradeMethod : (tile != null ? tile.UpgradeMethod : BuildingUpgradeMethod.ClusterExpansion);
        BuildingFunctionKind funcKind = cluster != null ? cluster.FunctionKind : (tile != null ? tile.FunctionKind : BuildingFunctionKind.AutoFeeding);
        int level = cluster != null ? cluster.Level : (tile != null ? Mathf.Max(1, tile.BuildingLevel) : 1);
        int maxLevel = cluster != null ? cluster.MaxLevel : (tile != null ? Mathf.Max(1, tile.MaxLevel) : BuildingRules.GranaryPostMaxLevel);
        List<HexCoordinates> clusterHexes = cluster != null && cluster.Hexes.Count > 0 ? cluster.Hexes : new List<HexCoordinates> { hex };
        int tileCount = clusterHexes.Count;
        int radius = cluster != null ? cluster.AuraRadius : BuildingRules.GetAuraRadius(typeId, level);
        int unlockedSlots = cluster != null ? cluster.UnlockedFoodSlots : BuildingRules.GetUnlockedFoodSlots(typeId, level);
        currentBuildingClusterKey = cluster != null ? cluster.ClusterKey : BuildingRules.BuildClusterKey(typeId, clusterHexes);

        ApplyHeaderEmblem(cardArtwork, LumiWorldUITheme.Glyph("G-17") ?? LumiWorldUITheme.Glyph("G-06"), new Color(0.95f, 0.78f, 0.28f, 0.28f));

        titleText.text = displayName;
        levelPillBg.gameObject.SetActive(true);
        levelPillBg.color = level >= maxLevel ? new Color(0.84f, 0.90f, 0.80f, 1f) : LumiWorldUITheme.Inset;
        levelPillText.text = $"Lv. {level}/{maxLevel}";
        levelPillText.color = level >= maxLevel ? LumiWorldUITheme.Success : LumiWorldUITheme.Text;

        LumiWorldUITheme.PlaceTopLeft(footprintGroup, 158f, 48f, 160f, 22f);
        footprintGlyphImg.sprite = LumiWorldUITheme.FootprintGlyph(tileCount);
        subtitleText.text = $"{tileCount} {(tileCount == 1 ? "tile" : "tiles")}";

        List<string> buffedBiomeNames = CollectBuffedBiomeNamesInAura(BiomeBuffCalculator.CollectAuraHexesAroundCluster(clusterHexes, radius));
        int buffedCount = buffedBiomeNames.Count;

        if (funcKind == BuildingFunctionKind.AutoFeeding)
        {
            effectWellBg.color = buffedCount > 0 ? new Color(0.87f, 0.93f, 0.84f, 1f) : new Color(0.95f, 0.90f, 0.76f, 1f);
            effectIcon.sprite = LumiWorldUITheme.Glyph("G-06") ?? LumiWorldUITheme.Glyph("G-17");
            effectIcon.color = buffedCount > 0 ? LumiWorldUITheme.Success : new Color(0.75f, 0.55f, 0.14f, 1f);

            if (buffedCount > 0)
            {
                string joined = buffedCount <= 2
                    ? string.Join(", ", buffedBiomeNames)
                    : $"{buffedBiomeNames[0]}, {buffedBiomeNames[1]} +{buffedCount - 2}";
                effectTitleText.text = $"Feeding {joined}";
                effectTitleText.color = LumiWorldUITheme.Success;
                effectText.text = $"{radius}-hex aura · {buffedCount} {(buffedCount == 1 ? "Biome" : "Biomes")}";
            }
            else
            {
                effectTitleText.text = $"Auto-Feed · {radius}-hex aura";
                effectTitleText.color = LumiWorldUITheme.Text;
                effectText.text = "No Biomes in golden ring";
            }

            slotsSectionObj.SetActive(true);
            metricsSectionObj.SetActive(false);
            rulesContainer.SetActive(false);

            int configuredSlots = AutoFeedUI.Instance != null
                ? AutoFeedUI.Instance.GetClusterConfiguredSlotCount(currentBuildingClusterKey)
                : 0;
            slotsHeaderText.text = $"Auto-Feed ({configuredSlots}/{unlockedSlots})";
            slotsHintText.text = level >= maxLevel ? "Max Lv. 5" : (tileCount < 5 ? $"+{5 - tileCount} for Lv.5" : "Lv. 5");

            for (int i = 0; i < 3; i++)
            {
                bool unlocked = i < unlockedSlots;
                int unlockLv = BuildingRules.GetRequiredLevelForFoodSlot(i);
                string foodId = string.Empty;
                int budget = 0;
                bool hasFood = unlocked && AutoFeedUI.Instance != null &&
                               AutoFeedUI.Instance.TryGetClusterSlot(currentBuildingClusterKey, i, out foodId, out budget);
                BindSlotRow(i, unlocked, unlockLv, hasFood, foodId, budget);
            }
            UpdateSlotSelectionOutlines();

            bool canFeedNow = configuredSlots > 0;
            ConfigureActionButton(
                visible: true,
                interactable: canFeedNow,
                label: canFeedNow ? "Feed Nearby Now" : "Tap a slot to add food",
                primary: canFeedNow,
                topY: GranaryPanelHeight - 60f);
            panelRect.sizeDelta = new Vector2(PanelWidth, GranaryPanelHeight);
        }
        else
        {
            CloseSlotPicker(refreshParent: false);
            effectWellBg.color = new Color(0.95f, 0.90f, 0.76f, 1f);
            effectIcon.sprite = LumiWorldUITheme.Glyph("G-17");
            effectIcon.color = LumiWorldUITheme.Text;
            effectTitleText.text = buffedCount > 0 ? $"Buffing {buffedCount} {(buffedCount == 1 ? "Biome" : "Biomes")}" : $"{radius}-hex aura";
            effectTitleText.color = buffedCount > 0 ? LumiWorldUITheme.Success : LumiWorldUITheme.Text;
            effectText.text = upgradeMethod == BuildingUpgradeMethod.DirectUpgrade
                ? "Direct upgrade"
                : "Expand adjacent";

            slotsSectionObj.SetActive(false);
            metricsSectionObj.SetActive(true);
            rulesContainer.SetActive(false);

            metric1Label.text = "Aura";
            metric1Value.text = $"{radius} hex";
            metric1Value.color = LumiWorldUITheme.Success;
            metric1Sub.text = buffedCount > 0 ? $"{buffedCount} in range" : "0 in range";
            metric1Sub.color = buffedCount > 0 ? LumiWorldUITheme.Success : LumiWorldUITheme.Muted;

            metric2Label.text = "Upgrade";
            metric2Value.text = upgradeMethod == BuildingUpgradeMethod.DirectUpgrade ? "Direct" : "Expand";
            metric2Value.color = LumiWorldUITheme.Text;
            metric2Sub.text = level >= maxLevel ? "Max level" : $"Lv. {level}/{maxLevel}";
            metric2Sub.color = LumiWorldUITheme.Muted;

            bool showUpgradeBtn = upgradeMethod == BuildingUpgradeMethod.DirectUpgrade;
            bool canDirectUpgrade = showUpgradeBtn && level < maxLevel;
            ConfigureActionButton(
                visible: showUpgradeBtn,
                interactable: canDirectUpgrade,
                label: canDirectUpgrade ? $"Upgrade to Lv. {level + 1}" : "Max Level",
                primary: canDirectUpgrade,
                topY: BuildingDirectUpgradeHeight - 60f);
            panelRect.sizeDelta = new Vector2(PanelWidth, showUpgradeBtn ? BuildingDirectUpgradeHeight : BuildingCompactHeight);
        }
    }

    private void BindSlotRow(int index, bool unlocked, int unlockLevel, bool hasFood, string foodId, int budget)
    {
        SlotRowView view = slotRows[index];
        if (view == null) return;

        if (!unlocked)
        {
            view.bg.color = LumiWorldUITheme.Well;
            view.iconWell.color = LumiWorldUITheme.Inset;
            view.icon.sprite = LumiWorldUITheme.Glyph("Lock");
            view.icon.color = LumiWorldUITheme.Muted;
            view.icon.rectTransform.sizeDelta = new Vector2(15f, 15f);
            view.title.text = $"Locked · Lv. {unlockLevel}";
            view.title.color = LumiWorldUITheme.Muted;
            LumiWorldUITheme.PlaceTopLeft(view.title.rectTransform, 58f, 15f, 240f, 22f);
            view.sub.gameObject.SetActive(false);
            view.chipBg.gameObject.SetActive(false);
            view.button.interactable = false;
            return;
        }

        view.button.interactable = true;
        view.bg.color = LumiWorldUITheme.Surface;
        if (hasFood)
        {
            var res = ResourceCatalog.Find(foodId);
            string foodName = ResourceInventoryUIPresentation.LocalizeResourceName(foodId, res != null ? res.resourceName : foodId);
            int stock = GetInventoryStock(foodId);
            view.iconWell.color = LumiWorldUITheme.Slot;
            view.icon.sprite = res != null && res.icon != null ? res.icon : LumiWorldUITheme.Glyph("G-06");
            view.icon.color = Color.white;
            view.icon.rectTransform.sizeDelta = new Vector2(28f, 28f);
            view.title.text = foodName;
            view.title.color = LumiWorldUITheme.Text;
            LumiWorldUITheme.PlaceTopLeft(view.title.rectTransform, 58f, 7f, 210f, 20f);
            view.sub.gameObject.SetActive(true);
            view.sub.text = stock > 0 ? $"Stock: {stock}" : "Out of stock";
            view.sub.color = stock > 0 ? LumiWorldUITheme.Muted : LumiWorldUITheme.Warning;
            view.chipBg.gameObject.SetActive(true);
            view.chipBg.color = new Color(0.84f, 0.90f, 0.80f, 1f);
            view.chipText.text = $"{budget} cap";
            view.chipText.color = LumiWorldUITheme.Success;
        }
        else
        {
            view.iconWell.color = LumiWorldUITheme.Inset;
            view.icon.sprite = LumiWorldUITheme.Glyph("Plus") ?? LumiWorldUITheme.Glyph("G-06");
            view.icon.color = LumiWorldUITheme.Text;
            view.icon.rectTransform.sizeDelta = new Vector2(15f, 15f);
            view.title.text = "Empty slot";
            view.title.color = LumiWorldUITheme.Muted;
            LumiWorldUITheme.PlaceTopLeft(view.title.rectTransform, 58f, 15f, 210f, 22f);
            view.sub.gameObject.SetActive(false);
            view.chipBg.gameObject.SetActive(true);
            view.chipBg.color = LumiWorldUITheme.Inset;
            view.chipText.text = "Add";
            view.chipText.color = LumiWorldUITheme.Text;
        }
    }

    private void RefreshSlotPickerUI()
    {
        if (!IsSlotPickerOpen() || pickerFoodContent == null) return;

        var cluster = SettlementClusterManager.Instance != null
            ? SettlementClusterManager.Instance.GetClusterInfo(currentHex)
            : null;
        if (pickerHeaderTitle != null)
        {
            pickerHeaderTitle.text = $"Assign food · Slot {editingSlotIndex + 1}";
        }
        if (pickerHeaderSub != null)
        {
            pickerHeaderSub.text = cluster != null
                ? $"Granary Post · Lv.{cluster.Level}/{cluster.MaxLevel} ({cluster.AuraRadius}-hex aura)"
                : "Granary Post";
        }

        string assignedFoodId = string.Empty;
        int assignedBudget = 0;
        bool hasAssigned = AutoFeedUI.Instance != null &&
                           AutoFeedUI.Instance.TryGetClusterSlot(currentBuildingClusterKey, editingSlotIndex, out assignedFoodId, out assignedBudget);

        if (pickerClearBtn != null)
        {
            pickerClearBtn.gameObject.SetActive(hasAssigned);
        }

        // Collect available foods (plus currently assigned food if stock depleted)
        var production = ResourceProductionRuntime.Instance;
        var inventory = production != null ? production.Inventory : null;
        var foodItems = new List<ResourceUIItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (production != null && production.FoodTypes != null)
        {
            foreach (var f in production.FoodTypes)
            {
                if (f == null || string.IsNullOrEmpty(f.ResourceId)) continue;
                int stock = inventory != null ? inventory.GetAmount(f.ResourceId) : 0;
                bool isCurrent = hasAssigned && string.Equals(f.ResourceId, assignedFoodId, StringComparison.OrdinalIgnoreCase);
                if (stock <= 0 && !isCurrent) continue;
                if (!seen.Add(f.ResourceId)) continue;

                var res = ResourceCatalog.Find(f.ResourceId);
                foodItems.Add(new ResourceUIItem
                {
                    id = f.ResourceId,
                    name = ResourceInventoryUIPresentation.LocalizeResourceName(f.ResourceId, res != null ? res.resourceName : f.ResourceId),
                    icon = res != null ? res.icon : null,
                    stock = stock
                });
            }
        }

        if (hasAssigned && !seen.Contains(assignedFoodId))
        {
            var res = ResourceCatalog.Find(assignedFoodId);
            foodItems.Insert(0, new ResourceUIItem
            {
                id = assignedFoodId,
                name = ResourceInventoryUIPresentation.LocalizeResourceName(assignedFoodId, res != null ? res.resourceName : assignedFoodId),
                icon = res != null ? res.icon : null,
                stock = inventory != null ? inventory.GetAmount(assignedFoodId) : 0
            });
        }

        // Ensure valid pending selection if empty or no longer available
        if (string.IsNullOrEmpty(pendingFoodId) || !foodItems.Exists(f => string.Equals(f.id, pendingFoodId, StringComparison.OrdinalIgnoreCase)))
        {
            var firstEligible = foodItems.Find(f => FindOtherSlotUsingFood(f.id) < 0);
            if (firstEligible != null)
            {
                pendingFoodId = firstEligible.id;
                pendingBudget = Mathf.Min(Mathf.Max(1, firstEligible.stock), 20);
            }
            else if (foodItems.Count > 0)
            {
                pendingFoodId = foodItems[0].id;
                pendingBudget = Mathf.Min(Mathf.Max(1, foodItems[0].stock), 20);
            }
            else
            {
                pendingFoodId = string.Empty;
                pendingBudget = 0;
            }
        }

        ResourceUIGraphics.Clear(pickerFoodContent);
        pickerFoodBorders.Clear();

        if (pickerEmptyFoodText != null)
        {
            pickerEmptyFoodText.gameObject.SetActive(foodItems.Count == 0);
        }

        const float rowW = 460f;
        const float rowH = 72f;
        const float gapY = 10f;
        float curY = 4f;

        ResourceUIItem pendingItem = null;
        for (int i = 0; i < foodItems.Count; i++)
        {
            var item = foodItems[i];
            bool isSelected = string.Equals(item.id, pendingFoodId, StringComparison.OrdinalIgnoreCase);
            if (isSelected) pendingItem = item;
            int otherSlotIdx = FindOtherSlotUsingFood(item.id);
            bool usedElsewhere = otherSlotIdx >= 0;

            var rowObj = new GameObject("PickFood_" + item.id, typeof(RectTransform), typeof(Image), typeof(Button));
            rowObj.transform.SetParent(pickerFoodContent, false);
            var rowRt = rowObj.GetComponent<RectTransform>();
            LumiWorldUITheme.PlaceTopLeft(rowRt, 4f, curY, rowW, rowH);

            var rowBg = rowObj.GetComponent<Image>();
            rowBg.sprite = LumiWorldUITheme.SurfaceSprite;
            rowBg.type = Image.Type.Sliced;
            rowBg.color = usedElsewhere ? new Color(0.89f, 0.87f, 0.84f, 0.78f) : LumiWorldUITheme.Surface;
            rowObj.AddComponent<ResourceUIInputBlocker>();
            LumiWorldUITheme.AddSoftElevation(rowObj, 3f, 0.14f);

            var icWell = LumiWorldUITheme.CreateRoundedSurface(rowRt, "IconBox", LumiWorldUITheme.Slot, Color.clear, 12, 0f, 0f, 0f);
            LumiWorldUITheme.PlaceTopLeft(icWell.rectTransform, 12f, 10f, 52f, 52f);

            if (item.icon != null)
            {
                var icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icObj.transform.SetParent(icWell.transform, false);
                LumiWorldUITheme.Stretch(icObj.GetComponent<RectTransform>(), 4f);
                var icImg = icObj.GetComponent<Image>();
                icImg.sprite = item.icon;
                icImg.preserveAspect = true;
                icImg.color = usedElsewhere ? new Color(0.75f, 0.75f, 0.75f, 0.50f) : Color.white;
                icImg.raycastTarget = false;
            }

            var nameTmp = LumiWorldUITheme.CreateLabel(rowRt, "Name", item.name, 16.5f, usedElsewhere ? LumiWorldUITheme.Muted : LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
            LumiWorldUITheme.PlaceTopLeft(nameTmp.rectTransform, 76f, 12f, 224f, 24f);

            var stockTmp = LumiWorldUITheme.CreateLabel(rowRt, "Stock", item.stock > 0 ? $"Stock: {item.stock:N0} available" : "Out of stock", 13f, item.stock > 0 ? LumiWorldUITheme.Muted : LumiWorldUITheme.Warning, false, TextAlignmentOptions.MidlineLeft);
            LumiWorldUITheme.PlaceTopLeft(stockTmp.rectTransform, 76f, 36f, 224f, 20f);

            // Status pill on right ("Available" vs "In Slot N")
            if (usedElsewhere)
            {
                var badgeBg = LumiWorldUITheme.CreateRoundedSurface(rowRt, "UsedBadge", LumiWorldUITheme.Inset, LumiWorldUITheme.Edge, 14, 1f, 0f, 0f);
                LumiWorldUITheme.PlaceTopRight(badgeBg.rectTransform, 14f, 20f, 96f, 32f);
                var badgeTxt = LumiWorldUITheme.CreateLabel(badgeBg.transform, "Text", $"In Slot {otherSlotIdx + 1}", 12.5f, LumiWorldUITheme.Muted, true, TextAlignmentOptions.Center);
                LumiWorldUITheme.Stretch(badgeTxt.rectTransform);
            }
            else
            {
                var statusObj = new GameObject("StatusBadge", typeof(RectTransform));
                statusObj.transform.SetParent(rowRt, false);
                LumiWorldUITheme.PlaceTopRight(statusObj.GetComponent<RectTransform>(), 14f, 20f, 108f, 32f);
                var stIcObj = new GameObject("Check", typeof(RectTransform), typeof(Image));
                stIcObj.transform.SetParent(statusObj.transform, false);
                LumiWorldUITheme.PlaceTopLeft(stIcObj.GetComponent<RectTransform>(), 4f, 6f, 20f, 20f);
                var stIc = stIcObj.GetComponent<Image>();
                stIc.sprite = LumiWorldUITheme.StatusReadySprite;
                stIc.preserveAspect = true;
                stIc.raycastTarget = false;
                var stTxt = LumiWorldUITheme.CreateLabel(statusObj.transform, "Text", "Available", 14f, LumiWorldUITheme.Success, true, TextAlignmentOptions.MidlineLeft);
                LumiWorldUITheme.PlaceTopLeft(stTxt.rectTransform, 28f, 0f, 80f, 32f);
            }

            var borderObj = new GameObject("SelectedBorder", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(rowRt, false);
            LumiWorldUITheme.Stretch(borderObj.GetComponent<RectTransform>());
            var borderImg = borderObj.GetComponent<Image>();
            borderImg.sprite = LumiWorldUITheme.GetBorderedSurfaceSprite("SelOutline_R16_S3.0", Color.clear, LumiWorldUITheme.Primary, 16, 3f);
            borderImg.type = Image.Type.Sliced;
            borderImg.color = Color.white;
            borderImg.raycastTarget = false;
            borderImg.gameObject.SetActive(isSelected);
            pickerFoodBorders[item.id] = borderImg;

            var btn = rowObj.GetComponent<Button>();
            btn.targetGraphic = rowBg;
            LumiWorldUITheme.ApplyButtonDisabledNeutral(btn);
            btn.interactable = !usedElsewhere;
            string capturedId = item.id;
            int capturedStock = item.stock;
            btn.onClick.AddListener(() => OnPickFoodRow(capturedId, capturedStock));

            curY += rowH + gapY;
        }

        pickerFoodContent.sizeDelta = new Vector2(rowW + 8f, Mathf.Max(468f, curY + 8f));
        RefreshPickerDetailCard(pendingItem, hasAssigned && string.Equals(assignedFoodId, pendingFoodId, StringComparison.OrdinalIgnoreCase) ? assignedBudget : 0);
    }

    private void RefreshPickerDetailCard(ResourceUIItem item, int existingBudget)
    {
        bool valid = item != null && FindOtherSlotUsingFood(item.id) < 0;
        int stock = item != null ? item.stock : 0;
        int maxCap = Mathf.Max(1, Mathf.Max(stock, existingBudget));

        if (valid)
        {
            if (pendingBudget <= 0)
            {
                pendingBudget = existingBudget > 0 ? existingBudget : Mathf.Min(stock, 20);
            }
            pendingBudget = Mathf.Clamp(pendingBudget, 1, maxCap);
        }

        if (pickerDetailIcon != null)
        {
            pickerDetailIcon.sprite = item != null ? item.icon : null;
            pickerDetailIcon.gameObject.SetActive(item != null && item.icon != null);
        }
        if (pickerDetailName != null)
        {
            pickerDetailName.text = item != null ? item.name : "Select a food";
        }
        if (pickerDetailStatus != null)
        {
            pickerDetailStatus.text = item == null
                ? "Choose from Inventory on the left"
                : (stock > 0 ? $"Available · {stock:N0} in Inventory" : "Out of stock in Inventory");
            pickerDetailStatus.color = item != null && stock > 0 ? LumiWorldUITheme.Success : LumiWorldUITheme.Muted;
        }

        if (pickerCapCard != null)
        {
            pickerCapStockText.text = valid ? $"Max: {maxCap:N0}" : "Max: —";
            pickerCapValueText.text = valid ? pendingBudget.ToString() : "—";

            if (pickerDecBtn != null) pickerDecBtn.interactable = valid && pendingBudget > 1;
            if (pickerIncBtn != null) pickerIncBtn.interactable = valid && pendingBudget < maxCap;
            if (pickerMaxBtn != null) pickerMaxBtn.interactable = valid && maxCap > 0 && pendingBudget != maxCap;

            if (pickerCapSlider != null)
            {
                suppressSliderCallback = true;
                pickerCapSlider.interactable = valid && maxCap > 1;
                pickerCapSlider.minValue = 1;
                pickerCapSlider.maxValue = maxCap;
                pickerCapSlider.SetValueWithoutNotify(valid ? pendingBudget : 1);
                suppressSliderCallback = false;
            }
        }

        if (pickerAssignBtn != null)
        {
            pickerAssignBtn.interactable = valid && (stock > 0 || existingBudget > 0);
        }
    }

    private int FindOtherSlotUsingFood(string foodId)
    {
        if (AutoFeedUI.Instance == null || string.IsNullOrEmpty(currentBuildingClusterKey)) return -1;
        for (int i = 0; i < 3; i++)
        {
            if (i == editingSlotIndex) continue;
            if (AutoFeedUI.Instance.TryGetClusterSlot(currentBuildingClusterKey, i, out string otherId, out _) &&
                string.Equals(otherId, foodId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    private void OnPickFoodRow(string foodId, int stock)
    {
        if (editingSlotIndex < 0) return;
        GameAudioManager.Instance?.PlayClick();

        pendingFoodId = foodId;
        int existing = 0;
        if (AutoFeedUI.Instance != null &&
            AutoFeedUI.Instance.TryGetClusterSlot(currentBuildingClusterKey, editingSlotIndex, out string curId, out int curBud) &&
            string.Equals(curId, foodId, StringComparison.OrdinalIgnoreCase))
        {
            existing = curBud;
        }
        pendingBudget = existing > 0 ? existing : Mathf.Min(Mathf.Max(1, stock), 20);
        RefreshSlotPickerUI();
    }

    private void ChangePickerBudget(int delta)
    {
        if (string.IsNullOrEmpty(pendingFoodId)) return;
        int maxCap = Mathf.Max(pendingBudget, GetInventoryStock(pendingFoodId));
        int next = Mathf.Clamp(pendingBudget + delta, 1, Mathf.Max(1, maxCap));
        if (next == pendingBudget) return;

        pendingBudget = next;
        GameAudioManager.Instance?.PlayClick();
        RefreshSlotPickerUI();
    }

    private void SetPickerBudgetToMax()
    {
        if (string.IsNullOrEmpty(pendingFoodId)) return;
        int stock = GetInventoryStock(pendingFoodId);
        if (stock <= 0 || pendingBudget == stock) return;

        pendingBudget = stock;
        GameAudioManager.Instance?.PlayClick();
        RefreshSlotPickerUI();
    }

    private void OnPickerSliderChanged(float val)
    {
        if (suppressSliderCallback || string.IsNullOrEmpty(pendingFoodId)) return;
        int next = Mathf.RoundToInt(val);
        if (next == pendingBudget) return;

        pendingBudget = next;
        if (pickerCapValueText != null) pickerCapValueText.text = pendingBudget.ToString();
        int maxCap = Mathf.Max(pendingBudget, GetInventoryStock(pendingFoodId));
        if (pickerDecBtn != null) pickerDecBtn.interactable = pendingBudget > 1;
        if (pickerIncBtn != null) pickerIncBtn.interactable = pendingBudget < maxCap;
        if (pickerMaxBtn != null) pickerMaxBtn.interactable = maxCap > 0 && pendingBudget != maxCap;
    }

    private void OnConfirmAssignSlotClicked()
    {
        if (AutoFeedUI.Instance == null || editingSlotIndex < 0 || string.IsNullOrEmpty(pendingFoodId)) return;

        if (AutoFeedUI.Instance.SetClusterSlot(currentBuildingClusterKey, editingSlotIndex, pendingFoodId, pendingBudget, out string err))
        {
            GameAudioManager.Instance?.PlayClick();
            int assignedCap = pendingBudget;
            CloseSlotPicker(refreshParent: true);
            ShowMainFeedback($"Assigned ({assignedCap} cap)", LumiWorldUITheme.Success);
        }
        else if (!string.IsNullOrEmpty(err))
        {
            ShowPickerFeedback(err, LumiWorldUITheme.Warning);
        }
    }

    private void OnClearCurrentSlotClicked()
    {
        if (AutoFeedUI.Instance == null || editingSlotIndex < 0) return;
        if (AutoFeedUI.Instance.ClearClusterSlot(currentBuildingClusterKey, editingSlotIndex))
        {
            GameAudioManager.Instance?.PlayClick();
            CloseSlotPicker(refreshParent: true);
            ShowMainFeedback("Cleared slot", LumiWorldUITheme.Muted);
        }
    }

    private static int GetInventoryStock(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return 0;
        var prod = ResourceProductionRuntime.Instance;
        return prod != null && prod.Inventory != null ? prod.Inventory.GetAmount(resourceId) : 0;
    }

    private void RefreshRiverView(HexCoordinates hex)
    {
        var riverMgr = RiverNetworkManager.Instance;
        RiverTileSegment seg = riverMgr != null ? riverMgr.GetRiver(hex) : null;
        RiverConfig cfg = riverMgr != null ? riverMgr.Config : null;

        List<HexCoordinates> riverCluster = BiomeBuffCalculator.CollectConnectedRiverCluster(
            hex,
            h => riverMgr != null && riverMgr.HasRiver(h));
        int tileCount = Mathf.Max(1, riverCluster.Count);
        HashSet<HexCoordinates> riverSet = new HashSet<HexCoordinates>(riverCluster);

        int riversideClusters = 0;
        int peninsulaClusters = 0;
        if (HabitatRuntimeManager.TryGetActive(out var hm) && hm.Habitats != null)
        {
            foreach (var hab in hm.Habitats)
            {
                if (hab == null || !hab.IsFormedBiome) continue;
                var habHexes = new List<HexCoordinates>(hab.members.Count);
                foreach (var m in hab.members) habHexes.Add(m.hex);

                RiverBuffTier rTier = BiomeBuffCalculator.EvaluateRiverBuffForCluster(
                    habHexes,
                    h => riverMgr != null && riverMgr.HasRiver(h),
                    riverSet);

                if (rTier == RiverBuffTier.Peninsula) peninsulaClusters++;
                else if (rTier == RiverBuffTier.Riverside) riversideClusters++;
            }
        }

        int wateredCreatures = 0;
        int peninsulaCreatures = 0;
        PlacedCard[] allCards = FindObjectsByType<PlacedCard>(FindObjectsInactive.Exclude);
        foreach (var card in allCards)
        {
            if (card == null || card.cardData == null || card.cardData.cardType != CardType.Creature) continue;
            bool onRiver = riverSet.Contains(card.placedHex);
            int touchingCount = 0;
            for (int dir = 0; dir < 6; dir++)
            {
                if (riverSet.Contains(card.placedHex.GetNeighbor(dir))) touchingCount++;
            }

            if (onRiver || touchingCount > 0)
            {
                wateredCreatures++;
                if (touchingCount >= 2) peninsulaCreatures++;
            }
        }

        int ptsCoLoc = cfg != null ? cfg.pointsPerCoLocatedBiome : 2;
        int ptsAdjBiome = cfg != null ? cfg.pointsPerAdjacentBiome : 1;
        int ptsCreature = cfg != null ? cfg.pointsPerAdjacentCreature : 5;
        int ptsPeninsula = cfg != null ? cfg.pointsPeninsulaBonus : 5;
        int totalBuffedBiomes = riversideClusters + peninsulaClusters;

        ApplyHeaderEmblem(null, LumiWorldUITheme.Glyph("I-02") ?? LumiWorldUITheme.Glyph("G-08"), new Color(0.22f, 0.80f, 0.94f, 0.24f));

        titleText.text = FormatRiverPiece(seg != null ? seg.pieceType : RiverPieceType.Straight);
        levelPillBg.gameObject.SetActive(false);
        LumiWorldUITheme.PlaceTopLeft(footprintGroup, 86f, 48f, 200f, 22f);
        footprintGlyphImg.sprite = LumiWorldUITheme.FootprintGlyph(tileCount);
        subtitleText.text = $"{tileCount} {(tileCount == 1 ? "tile" : "tiles")}";

        effectWellBg.color = new Color(0.84f, 0.94f, 0.98f, 1f);
        effectIcon.sprite = LumiWorldUITheme.Glyph("I-02") ?? LumiWorldUITheme.Glyph("G-08");
        effectIcon.color = new Color(0.16f, 0.56f, 0.72f, 1f);
        effectTitleText.text = "Harmony & Watering";
        effectTitleText.color = LumiWorldUITheme.Text;
        effectText.text = $"{totalBuffedBiomes} Biomes · {wateredCreatures} Creatures buffed";

        slotsSectionObj.SetActive(false);
        metricsSectionObj.SetActive(true);
        rulesContainer.SetActive(true);

        metric1Label.text = "Biomes";
        metric1Value.text = $"{totalBuffedBiomes}";
        metric1Value.color = totalBuffedBiomes > 0 ? LumiWorldUITheme.Success : LumiWorldUITheme.Muted;
        metric1Sub.text = totalBuffedBiomes > 0 ? $"{riversideClusters} Side · {peninsulaClusters} Pen." : "0 in range";
        metric1Sub.color = LumiWorldUITheme.Muted;

        metric2Label.text = "Creatures";
        metric2Value.text = $"{wateredCreatures}";
        metric2Value.color = wateredCreatures > 0 ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted;
        metric2Sub.text = peninsulaCreatures > 0 ? $"{peninsulaCreatures} Peninsula" : "Beside stream";
        metric2Sub.color = LumiWorldUITheme.Muted;

        rulesHeaderText.text = "Bonuses";
        SetRuleRow(0, "Riverside", $"+{ptsAdjBiome} edge / +{ptsCoLoc} tile");
        SetRuleRow(1, "Peninsula (2+)", $"+{ptsPeninsula} bonus pts");
        SetRuleRow(2, "Watering hole", $"+{ptsCreature} pts / creature");

        ConfigureActionButton(visible: false, interactable: false, label: string.Empty, primary: false, topY: 222f);
        panelRect.sizeDelta = new Vector2(PanelWidth, RiverPanelHeight);
    }

    private void ApplyHeaderEmblem(Sprite artwork, Sprite fallbackGlyph, Color fallbackBgColor)
    {
        bool hasArt = artwork != null;
        if (emblemImage != null)
        {
            emblemImage.gameObject.SetActive(hasArt);
            emblemImage.sprite = artwork;
        }
        if (emblemFallbackBg != null)
        {
            emblemFallbackBg.gameObject.SetActive(!hasArt);
            emblemFallbackBg.color = fallbackBgColor;
        }
        if (emblemFallbackIcon != null)
        {
            emblemFallbackIcon.sprite = fallbackGlyph;
            emblemFallbackIcon.color = LumiWorldUITheme.Text;
        }
    }

    private static Sprite ResolveBuildingCardSprite(GameObject tileObj)
    {
        if (tileObj == null) return null;
        foreach (var pc in tileObj.GetComponentsInChildren<PlacedCard>(true))
        {
            if (pc != null && pc.cardData != null && pc.cardData.IsSettlementCard() && pc.cardData.cardImage != null)
            {
                return pc.cardData.cardImage;
            }
        }
        foreach (var card in Resources.LoadAll<CardData>("Cards"))
        {
            if (card != null && card.IsSettlementCard() && card.cardImage != null)
            {
                return card.cardImage;
            }
        }
        return null;
    }

    private void OnActionButtonClicked()
    {
        if (currentKind != InspectedKind.Building) return;

        var cluster = SettlementClusterManager.Instance != null
            ? SettlementClusterManager.Instance.GetClusterInfo(currentHex)
            : null;
        if (cluster != null && cluster.FunctionKind == BuildingFunctionKind.AutoFeeding)
        {
            if (AutoFeedUI.Instance == null) return;
            string key = !string.IsNullOrEmpty(currentBuildingClusterKey) ? currentBuildingClusterKey : cluster.ClusterKey;
            int deposited = AutoFeedUI.Instance.TriggerClusterFeedingNow(key, out int creatures, out string reason);
            GameAudioManager.Instance?.PlayClick();

            if (deposited > 0)
            {
                ShowMainFeedback($"Fed {deposited} unit(s) nearby", LumiWorldUITheme.Success);
            }
            else if (creatures == 0)
            {
                ShowMainFeedback($"No residents within {cluster.AuraRadius} hex", LumiWorldUITheme.Muted);
            }
            else if (!string.IsNullOrEmpty(reason))
            {
                ShowMainFeedback(reason, LumiWorldUITheme.Warning);
            }
            else
            {
                ShowMainFeedback("Nearby residents are full", LumiWorldUITheme.Success);
            }
            return;
        }

        if (cluster != null && cluster.UpgradeMethod == BuildingUpgradeMethod.DirectUpgrade &&
            SettlementClusterManager.Instance != null &&
            SettlementClusterManager.Instance.TryDirectUpgrade(currentHex, out _))
        {
            OpenForBuilding(currentHex);
        }
    }

    private void ConfigureActionButton(bool visible, bool interactable, string label, bool primary, float topY)
    {
        if (actionButton == null) return;
        actionButton.gameObject.SetActive(visible);
        if (!visible) return;
        LumiWorldUITheme.PlaceTopLeft(actionButton.GetComponent<RectTransform>(), 16f, topY, PanelWidth - 32f, 44f);
        actionButton.interactable = interactable;
        if (actionButtonLabel != null)
        {
            actionButtonLabel.text = label;
            actionButtonLabel.color = primary ? Color.white : LumiWorldUITheme.Muted;
        }
        if (actionButtonBg != null)
        {
            actionButtonBg.color = primary ? LumiWorldUITheme.Primary : LumiWorldUITheme.Inset;
        }
    }

    private void ShowMainFeedback(string msg, Color col)
    {
        if (mainFeedbackText == null) return;
        mainFeedbackText.text = msg;
        mainFeedbackText.color = col;
        if (feedbackFadeRoutine != null) StopCoroutine(feedbackFadeRoutine);
        feedbackFadeRoutine = StartCoroutine(ClearTextAfterDelay(mainFeedbackText, 3f));
    }

    private void ShowPickerFeedback(string msg, Color col)
    {
        if (pickerFeedbackText == null) return;
        pickerFeedbackText.text = msg;
        pickerFeedbackText.color = col;
        if (pickerFeedbackRoutine != null) StopCoroutine(pickerFeedbackRoutine);
        pickerFeedbackRoutine = StartCoroutine(ClearTextAfterDelay(pickerFeedbackText, 2.5f));
    }

    private static IEnumerator ClearTextAfterDelay(TextMeshProUGUI target, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (target != null) target.text = string.Empty;
    }

    private void SetRuleRow(int index, string left, string right)
    {
        if (index < 0 || index >= ruleRows.Length) return;
        if (ruleRows[index] != null) ruleRows[index].SetActive(true);
        if (ruleLeftTexts[index] != null) ruleLeftTexts[index].text = left;
        if (ruleRightTexts[index] != null) ruleRightTexts[index].text = right;
    }

    private static List<string> CollectBuffedBiomeNamesInAura(HashSet<HexCoordinates> auraHexes)
    {
        var names = new List<string>();
        if (auraHexes == null || auraHexes.Count == 0) return names;
        if (!HabitatRuntimeManager.TryGetActive(out var hm) || hm.Habitats == null) return names;

        foreach (var hab in hm.Habitats)
        {
            if (hab == null || !hab.IsFormedBiome) continue;
            bool touches = false;
            foreach (var member in hab.members)
            {
                if (auraHexes.Contains(member.hex))
                {
                    touches = true;
                    break;
                }
            }
            if (touches)
            {
                names.Add(HabitatCreaturePanelStyle.GetAffinityDisplayName(hab.affinity));
            }
        }
        return names;
    }

    private bool TryResolveStructureAtScreenPos(Vector2 screenPos, out InspectedKind kind, out HexCoordinates hex)
    {
        kind = InspectedKind.None;
        hex = default;

        Camera cam = Camera.main;
        if (cam == null) return false;

        var hits = Physics.RaycastAll(cam.ScreenPointToRay(screenPos), 1000f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (TryResolveStructureFromHit(hits[i], out kind, out hex))
            {
                return true;
            }

            if (hits[i].collider != null &&
                (hits[i].collider.GetComponentInParent<HexTileInfo>() != null ||
                 hits[i].collider.GetComponentInParent<BiomeSurface>() != null))
            {
                break;
            }
        }

        return false;
    }

    private static bool TryResolveStructureFromHit(in RaycastHit hit, out InspectedKind kind, out HexCoordinates hex)
    {
        kind = InspectedKind.None;
        hex = default;
        if (hit.collider == null) return false;

        SettlementTile settlement = hit.collider.GetComponentInParent<SettlementTile>();
        if (settlement != null)
        {
            kind = InspectedKind.Building;
            hex = settlement.Coordinates;
            return true;
        }

        RiverTileSegment riverSeg = hit.collider.GetComponentInParent<RiverTileSegment>();
        if (riverSeg != null)
        {
            kind = InspectedKind.River;
            hex = riverSeg.placedHex;
            return true;
        }

        if (hit.collider.GetComponentInParent<SettlementBorderBuilder>() != null ||
            hit.collider.GetComponentInParent<SettlementClusterManager>() != null)
        {
            HexCoordinates approx = HexMetrics.WorldToHex(hit.point);
            if (SettlementClusterManager.Instance != null)
            {
                if (SettlementClusterManager.Instance.HasSettlement(approx))
                {
                    kind = InspectedKind.Building;
                    hex = approx;
                    return true;
                }
                for (int d = 0; d < 6; d++)
                {
                    var nb = approx.GetNeighbor(d);
                    if (SettlementClusterManager.Instance.HasSettlement(nb))
                    {
                        kind = InspectedKind.Building;
                        hex = nb;
                        return true;
                    }
                }
            }
        }

        HexTileInfo tileInfo = hit.collider.GetComponentInParent<HexTileInfo>();
        if (tileInfo != null)
        {
            if (tileInfo.GetComponent<SettlementTile>() != null ||
                (SettlementClusterManager.Instance != null && SettlementClusterManager.Instance.HasSettlement(tileInfo.coordinates)))
            {
                kind = InspectedKind.Building;
                hex = tileInfo.coordinates;
                return true;
            }

            if (HexMetrics.IsTopSurfaceHit(hit) &&
                RiverNetworkManager.Instance != null &&
                RiverNetworkManager.Instance.HasRiver(tileInfo.coordinates))
            {
                bool hasFormedBiome = HabitatRuntimeManager.TryGetActive(out var hm) &&
                                      hm.FindHabitatByHex(tileInfo.coordinates) is HabitatState hab &&
                                      hab.IsFormedBiome;

                if (!hasFormedBiome || IsPointNearTileRiver(tileInfo.coordinates, hit.point))
                {
                    kind = InspectedKind.River;
                    hex = tileInfo.coordinates;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPointNearTileRiver(HexCoordinates hex, Vector3 worldPoint)
    {
        var riverMgr = RiverNetworkManager.Instance;
        if (riverMgr == null) return false;
        RiverTileSegment seg = riverMgr.GetRiver(hex);
        if (seg == null) return false;

        const float maxDistSq = 0.34f * 0.34f;
        Vector3 segPos = seg.transform.position;
        if (seg.CachedPaths != null && seg.CachedPaths.Count > 0)
        {
            for (int p = 0; p < seg.CachedPaths.Count; p++)
            {
                var path = seg.CachedPaths[p];
                for (int w = 0; w < path.Count; w++)
                {
                    Vector3 wp = segPos + path[w].position;
                    float dx = worldPoint.x - wp.x;
                    float dz = worldPoint.z - wp.z;
                    if (dx * dx + dz * dz <= maxDistSq) return true;
                }
            }
        }

        float cdx = worldPoint.x - segPos.x;
        float cdz = worldPoint.z - segPos.z;
        return cdx * cdx + cdz * cdz <= 0.30f * 0.30f;
    }

    private static string FormatRiverPiece(RiverPieceType piece)
    {
        switch (piece)
        {
            case RiverPieceType.SpringHead: return "Spring Head";
            case RiverPieceType.PondBasin: return "Pond Basin";
            case RiverPieceType.Waterfall: return "Waterfall";
            default: return "Mountain Stream";
        }
    }

    private void BuildUI()
    {
        if (rootModal != null) return;

        rootModal = new GameObject("MapStructureInfoModal", typeof(RectTransform));
        rootModal.transform.SetParent(transform, false);
        EconomyHUD.Stretch(rootModal.GetComponent<RectTransform>());

        GameObject backdropObj = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdropObj.transform.SetParent(rootModal.transform, false);
        EconomyHUD.Stretch(backdropObj.GetComponent<RectTransform>());
        Image bdImg = backdropObj.GetComponent<Image>();
        bdImg.color = new Color(0.10f, 0.09f, 0.08f, 0.03f);
        Button bdBtn = backdropObj.GetComponent<Button>();
        bdBtn.transition = Selectable.Transition.None;
        bdBtn.onClick.AddListener(OnBackdropClicked);

        GameObject stageObj = new GameObject("Stage", typeof(RectTransform));
        stageObj.transform.SetParent(rootModal.transform, false);
        stageRect = stageObj.GetComponent<RectTransform>();
        SyncStageScale();

        // --- MAIN PANEL (416 du wide, docked at y = -82 du matching P-05) ---
        GameObject panelObj = new GameObject("StructureInfoPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(stageRect, false);
        panelRect = panelObj.GetComponent<RectTransform>();
        LumiWorldUITheme.PlaceTopRight(panelRect, 14f, 82f, PanelWidth, GranaryPanelHeight);

        Image pBg = panelObj.GetComponent<Image>();
        pBg.sprite = LumiWorldUITheme.WindowSprite;
        pBg.type = Image.Type.Sliced;
        pBg.color = LumiWorldUITheme.Window;
        pBg.raycastTarget = true;
        panelObj.AddComponent<ResourceUIInputBlocker>();
        LumiWorldUITheme.AddSoftElevation(panelObj, 6f, 0.24f);

        // 1. Header: 60x60 Card Art Emblem + Title + Level Pill + Footprint + [x]
        GameObject artObj = new GameObject("StructureArtwork", typeof(RectTransform), typeof(Image));
        artObj.transform.SetParent(panelRect, false);
        LumiWorldUITheme.PlaceTopLeft(artObj.GetComponent<RectTransform>(), 16f, 12f, 60f, 60f);
        emblemImage = artObj.GetComponent<Image>();
        emblemImage.preserveAspect = true;
        emblemImage.raycastTarget = false;

        GameObject fbObj = new GameObject("FallbackBadge", typeof(RectTransform), typeof(Image));
        fbObj.transform.SetParent(panelRect, false);
        LumiWorldUITheme.PlaceTopLeft(fbObj.GetComponent<RectTransform>(), 18f, 14f, 54f, 54f);
        emblemFallbackBg = fbObj.GetComponent<Image>();
        emblemFallbackBg.sprite = LumiWorldUITheme.GetCapsuleSprite(54);
        emblemFallbackBg.type = Image.Type.Sliced;
        emblemFallbackBg.color = LumiWorldUITheme.Inset;
        emblemFallbackBg.raycastTarget = false;

        GameObject fbIcObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        fbIcObj.transform.SetParent(fbObj.transform, false);
        RectTransform fbIcRt = fbIcObj.GetComponent<RectTransform>();
        fbIcRt.anchorMin = fbIcRt.anchorMax = fbIcRt.pivot = new Vector2(0.5f, 0.5f);
        fbIcRt.sizeDelta = new Vector2(26f, 26f);
        emblemFallbackIcon = fbIcObj.GetComponent<Image>();
        emblemFallbackIcon.preserveAspect = true;
        emblemFallbackIcon.raycastTarget = false;

        titleText = LumiWorldUITheme.CreateLabel(panelRect, "Title", "Granary Post", 22.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 16f;
        titleText.fontSizeMax = 23f;
        LumiWorldUITheme.PlaceTopLeft(titleText.rectTransform, 86f, 12f, 274f, 34f);

        GameObject lvObj = new GameObject("LevelPill", typeof(RectTransform), typeof(Image));
        lvObj.transform.SetParent(panelRect, false);
        LumiWorldUITheme.PlaceTopLeft(lvObj.GetComponent<RectTransform>(), 86f, 48f, 64f, 22f);
        levelPillBg = lvObj.GetComponent<Image>();
        levelPillBg.sprite = LumiWorldUITheme.GetCapsuleSprite(22);
        levelPillBg.type = Image.Type.Sliced;
        levelPillBg.color = LumiWorldUITheme.Inset;
        levelPillBg.raycastTarget = false;
        levelPillText = LumiWorldUITheme.CreateLabel(lvObj.transform, "Text", "Lv. 1/5", 12f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(levelPillText.rectTransform);

        GameObject fpObj = new GameObject("FootprintRow", typeof(RectTransform));
        fpObj.transform.SetParent(panelRect, false);
        footprintGroup = fpObj.GetComponent<RectTransform>();
        LumiWorldUITheme.PlaceTopLeft(footprintGroup, 158f, 48f, 160f, 22f);

        GameObject fpIcObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        fpIcObj.transform.SetParent(footprintGroup, false);
        LumiWorldUITheme.PlaceTopLeft(fpIcObj.GetComponent<RectTransform>(), 0f, 2f, 18f, 18f);
        footprintGlyphImg = fpIcObj.GetComponent<Image>();
        footprintGlyphImg.preserveAspect = true;
        footprintGlyphImg.color = LumiWorldUITheme.Muted;
        footprintGlyphImg.raycastTarget = false;

        subtitleText = LumiWorldUITheme.CreateLabel(footprintGroup, "Count", "5 tiles", 13f, LumiWorldUITheme.Muted, true, TextAlignmentOptions.MidlineLeft);
        subtitleText.overflowMode = TextOverflowModes.Overflow;
        LumiWorldUITheme.PlaceTopLeft(subtitleText.rectTransform, 23f, 0f, 130f, 22f);

        LumiWorldUITheme.CreateCloseButton(panelRect, 14f, 14f, Close, 30f);

        // 2. Concise Effect Card
        effectCardBg = LumiWorldUITheme.CreateRoundedSurface(
            panelRect, "EffectCard", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge, 14, 1.2f, 2.5f, 0.14f);
        LumiWorldUITheme.PlaceTopLeft(effectCardBg.rectTransform, 16f, 82f, PanelWidth - 32f, 58f);

        effectWellBg = LumiWorldUITheme.CreateRoundedSurface(
            effectCardBg.transform, "IconWell", LumiWorldUITheme.Inset, Color.clear, 11, 0f, 0f, 0f);
        LumiWorldUITheme.PlaceTopLeft(effectWellBg.rectTransform, 12f, 10f, 38f, 38f);

        GameObject effIcObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        effIcObj.transform.SetParent(effectWellBg.transform, false);
        RectTransform effIcRt = effIcObj.GetComponent<RectTransform>();
        effIcRt.anchorMin = effIcRt.anchorMax = effIcRt.pivot = new Vector2(0.5f, 0.5f);
        effIcRt.sizeDelta = new Vector2(20f, 20f);
        effectIcon = effIcObj.GetComponent<Image>();
        effectIcon.preserveAspect = true;
        effectIcon.raycastTarget = false;

        effectTitleText = LumiWorldUITheme.CreateLabel(effectCardBg.transform, "EffectTitle", "", 14f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        effectTitleText.enableAutoSizing = true;
        effectTitleText.fontSizeMin = 12f;
        effectTitleText.fontSizeMax = 14f;
        LumiWorldUITheme.PlaceTopLeft(effectTitleText.rectTransform, 60f, 9f, PanelWidth - 104f, 20f);

        effectText = LumiWorldUITheme.CreateLabel(effectCardBg.transform, "EffectDesc", "", 12f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(effectText.rectTransform, 60f, 30f, PanelWidth - 104f, 18f);

        // 3A. Granary Post Auto-Feed Slots Section
        slotsSectionObj = new GameObject("AutoFeedSlotsSection", typeof(RectTransform));
        slotsSectionObj.transform.SetParent(panelRect, false);
        LumiWorldUITheme.PlaceTopLeft(slotsSectionObj.GetComponent<RectTransform>(), 16f, 150f, PanelWidth - 32f, 198f);

        slotsHeaderText = LumiWorldUITheme.CreateLabel(slotsSectionObj.transform, "SlotsHeader", "Auto-Feed (0/3)", 15f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(slotsHeaderText.rectTransform, 0f, 0f, 240f, 22f);

        slotsHintText = LumiWorldUITheme.CreateLabel(slotsSectionObj.transform, "SlotsHint", "Max Lv. 5", 12f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopLeft(slotsHintText.rectTransform, 240f, 0f, PanelWidth - 272f, 22f);

        for (int i = 0; i < 3; i++)
        {
            slotRows[i] = BuildSlotRow(slotsSectionObj.transform, i, 28f + i * 56f);
        }

        mainFeedbackText = LumiWorldUITheme.CreateLabel(panelRect, "FeedbackText", "", 11.5f, LumiWorldUITheme.Success, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(mainFeedbackText.rectTransform, 16f, GranaryPanelHeight - 78f, PanelWidth - 32f, 18f);

        // 3B. Metrics Section (Streams & Non-AutoFeed Buildings)
        metricsSectionObj = new GameObject("MetricsSection", typeof(RectTransform));
        metricsSectionObj.transform.SetParent(panelRect, false);
        LumiWorldUITheme.PlaceTopLeft(metricsSectionObj.GetComponent<RectTransform>(), 16f, 152f, PanelWidth - 32f, 76f);

        float cardW = (PanelWidth - 32f - 10f) * 0.5f;
        BuildMetricBox(metricsSectionObj.transform, 0f, 0f, cardW, 76f, out metric1Label, out metric1Value, out metric1Sub);
        BuildMetricBox(metricsSectionObj.transform, cardW + 10f, 0f, cardW, 76f, out metric2Label, out metric2Value, out metric2Sub);
        metricsSectionObj.SetActive(false);

        // 3C. Stream Bonuses List
        rulesContainer = new GameObject("RulesSection", typeof(RectTransform));
        rulesContainer.transform.SetParent(panelRect, false);
        LumiWorldUITheme.PlaceTopLeft(rulesContainer.GetComponent<RectTransform>(), 16f, 240f, PanelWidth - 32f, 112f);

        rulesHeaderText = LumiWorldUITheme.CreateLabel(rulesContainer.transform, "RulesHeader", "Bonuses", 15f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(rulesHeaderText.rectTransform, 0f, 0f, PanelWidth - 32f, 22f);

        rulesCardBg = LumiWorldUITheme.CreateRoundedSurface(
            rulesContainer.transform, "RulesCard", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge, 14, 1.2f, 2f, 0.12f);
        LumiWorldUITheme.PlaceTopLeft(rulesCardBg.rectTransform, 0f, 26f, PanelWidth - 32f, 82f);

        for (int i = 0; i < 3; i++)
        {
            GameObject rowObj = new GameObject("RuleRow_" + i, typeof(RectTransform));
            rowObj.transform.SetParent(rulesCardBg.transform, false);
            LumiWorldUITheme.PlaceTopLeft(rowObj.GetComponent<RectTransform>(), 14f, 6f + i * 24f, PanelWidth - 60f, 22f);
            ruleRows[i] = rowObj;

            var leftTmp = LumiWorldUITheme.CreateLabel(rowObj.transform, "Left", "", 12.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
            LumiWorldUITheme.PlaceTopLeft(leftTmp.rectTransform, 0f, 0f, 146f, 22f);
            ruleLeftTexts[i] = leftTmp;

            var rightTmp = LumiWorldUITheme.CreateLabel(rowObj.transform, "Right", "", 12.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineRight);
            rightTmp.overflowMode = TextOverflowModes.Overflow;
            LumiWorldUITheme.PlaceTopLeft(rightTmp.rectTransform, 148f, 0f, PanelWidth - 208f, 22f);
            ruleRightTexts[i] = rightTmp;
        }
        rulesContainer.SetActive(false);

        // 4. Bottom Primary CTA
        actionButton = LumiWorldUITheme.CreatePillButton(
            panelRect,
            "ActionBtn",
            "Feed Nearby Now",
            "G-06",
            primary: true,
            fontSize: 15f,
            onClick: OnActionButtonClicked,
            out actionButtonBg,
            out _,
            out actionButtonLabel,
            radius: 14);
        actionButton.gameObject.AddComponent<DialogButtonJuice>();
        LumiWorldUITheme.PlaceTopLeft(actionButton.GetComponent<RectTransform>(), 16f, GranaryPanelHeight - 60f, PanelWidth - 32f, 44f);
        actionButton.gameObject.SetActive(false);

        // --- CENTERED ASSIGN FOOD POPUP (mirrors Habitat P-07 Assign Resource) ---
        BuildSlotPickerDrawer();

        rootModal.SetActive(false);
    }

    private void BuildSlotPickerDrawer()
    {
        slotPickerOverlay = new GameObject("GranaryAssignFoodOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        slotPickerOverlay.transform.SetParent(rootModal.transform, false);
        LumiWorldUITheme.Stretch(slotPickerOverlay.GetComponent<RectTransform>());
        Image dimImg = slotPickerOverlay.GetComponent<Image>();
        dimImg.color = LumiWorldUITheme.Backdrop;
        dimImg.raycastTarget = true;
        slotPickerOverlay.AddComponent<ResourceUIInputBlocker>();
        Button dimBtn = slotPickerOverlay.GetComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(() => CloseSlotPicker(true));

        GameObject spObj = new GameObject("GranarySlotAssignDialog", typeof(RectTransform), typeof(Image));
        spObj.transform.SetParent(slotPickerOverlay.transform, false);
        slotPickerRect = spObj.GetComponent<RectTransform>();
        slotPickerRect.anchorMin = slotPickerRect.anchorMax = slotPickerRect.pivot = new Vector2(0.5f, 0.5f);
        slotPickerRect.anchoredPosition = Vector2.zero;
        slotPickerRect.sizeDelta = new Vector2(AssignPopupWidth, AssignPopupHeight);

        Image spBg = spObj.GetComponent<Image>();
        spBg.sprite = LumiWorldUITheme.GetBorderedSurfaceSprite("AssignPopupFrame", LumiWorldUITheme.Window, LumiWorldUITheme.Edge, 22, 1.5f);
        spBg.type = Image.Type.Sliced;
        spBg.color = Color.white;
        spBg.raycastTarget = true;
        spObj.AddComponent<ResourceUIInputBlocker>();
        LumiWorldUITheme.AddSoftElevation(spObj, 6f, 0.24f);

        // Header: Title + Sub + [Clear slot] + [x]
        pickerHeaderTitle = LumiWorldUITheme.CreateLabel(slotPickerRect, "Title", "Assign food · Slot 1", 24f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(pickerHeaderTitle.rectTransform, 30f, 16f, 440f, 34f);

        pickerHeaderSub = LumiWorldUITheme.CreateLabel(slotPickerRect, "Subtitle", "Granary Post · Lv. 5/5", 13f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(pickerHeaderSub.rectTransform, 30f, 46f, 440f, 20f);

        pickerClearBtn = LumiWorldUITheme.CreatePillButton(
            slotPickerRect, "ClearSlotBtn", "Clear slot", null, primary: false, fontSize: 13f,
            onClick: OnClearCurrentSlotClicked, out Image clrBg, out _, out TextMeshProUGUI clrLbl, radius: 14);
        clrBg.color = new Color(0.95f, 0.87f, 0.86f, 1f);
        if (clrLbl != null) clrLbl.color = LumiWorldUITheme.Error;
        pickerClearBtn.gameObject.AddComponent<DialogButtonJuice>();
        LumiWorldUITheme.PlaceTopRight(pickerClearBtn.GetComponent<RectTransform>(), 78f, 18f, 110f, 36f);

        LumiWorldUITheme.CreateCloseButton(slotPickerRect, 24f, 16f, () => CloseSlotPicker(true), 40f);

        GameObject divObj = new GameObject("HeaderDivider", typeof(RectTransform), typeof(Image));
        divObj.transform.SetParent(slotPickerRect, false);
        LumiWorldUITheme.PlaceTopLeft(divObj.GetComponent<RectTransform>(), 28f, 70f, AssignPopupWidth - 56f, 2f);
        divObj.GetComponent<Image>().color = LumiWorldUITheme.Inset;

        // Left Column: Scrollable Food List (476 x 468, matches P-07)
        GameObject scrollObj = new GameObject("Choices", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(slotPickerRect, false);
        LumiWorldUITheme.PlaceTopLeft(scrollObj.GetComponent<RectTransform>(), 28f, 84f, 476f, 468f);

        var scroll = scrollObj.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        GameObject vpObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        vpObj.transform.SetParent(scrollObj.transform, false);
        LumiWorldUITheme.Stretch(vpObj.GetComponent<RectTransform>());
        Image vpImg = vpObj.GetComponent<Image>();
        vpImg.color = Color.clear;
        vpImg.raycastTarget = true;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpObj.transform, false);
        pickerFoodContent = contentObj.GetComponent<RectTransform>();
        pickerFoodContent.anchorMin = new Vector2(0f, 1f);
        pickerFoodContent.anchorMax = new Vector2(1f, 1f);
        pickerFoodContent.pivot = new Vector2(0f, 1f);
        pickerFoodContent.anchoredPosition = Vector2.zero;
        pickerFoodContent.sizeDelta = new Vector2(468f, 468f);

        scroll.viewport = vpObj.GetComponent<RectTransform>();
        scroll.content = pickerFoodContent;

        pickerEmptyFoodText = LumiWorldUITheme.CreateLabel(scrollObj.transform, "Empty", "No creature food in Inventory.", 15f, LumiWorldUITheme.Muted, true, TextAlignmentOptions.Center, wrap: true);
        LumiWorldUITheme.Stretch(pickerEmptyFoodText.rectTransform, 24f);
        pickerEmptyFoodText.gameObject.SetActive(false);

        // Right Column: Detail Card (372 x 468, matches P-07)
        const float detailW = 372f;
        const float detailH = 468f;
        Image detailCard = LumiWorldUITheme.CreateRoundedSurface(
            slotPickerRect, "DetailCard", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge, 18, 1.2f, 4f, 0.16f, raycastTarget: true);
        LumiWorldUITheme.PlaceTopLeft(detailCard.rectTransform, 520f, 84f, detailW, detailH);
        detailCard.gameObject.AddComponent<ResourceUIInputBlocker>();

        Image artBox = LumiWorldUITheme.CreateRoundedSurface(
            detailCard.transform, "ArtworkBox", LumiWorldUITheme.Window, LumiWorldUITheme.Edge, 14, 1f, 0f, 0f);
        LumiWorldUITheme.PlaceTopLeft(artBox.rectTransform, 24f, 20f, detailW - 48f, 148f);

        GameObject dIcObj = new GameObject("FoodIcon", typeof(RectTransform), typeof(Image));
        dIcObj.transform.SetParent(artBox.transform, false);
        RectTransform dIcRt = dIcObj.GetComponent<RectTransform>();
        dIcRt.anchorMin = dIcRt.anchorMax = dIcRt.pivot = new Vector2(0.5f, 0.5f);
        dIcRt.sizeDelta = new Vector2(104f, 104f);
        pickerDetailIcon = dIcObj.GetComponent<Image>();
        pickerDetailIcon.preserveAspect = true;
        pickerDetailIcon.raycastTarget = false;

        pickerDetailName = LumiWorldUITheme.CreateLabel(detailCard.transform, "Name", "Raspberries", 22f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(pickerDetailName.rectTransform, 24f, 178f, detailW - 48f, 30f);

        pickerDetailStatus = LumiWorldUITheme.CreateLabel(detailCard.transform, "Status", "", 14f, LumiWorldUITheme.Success, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(pickerDetailStatus.rectTransform, 24f, 210f, detailW - 48f, 22f);

        // Allocation Budget Card (clean icon steppers + unbreakable circular slider)
        Image capCardImg = LumiWorldUITheme.CreateRoundedSurface(
            detailCard.transform, "CapCard", LumiWorldUITheme.Window, LumiWorldUITheme.Edge, 14, 1f, 0f, 0f);
        pickerCapCard = capCardImg.gameObject;
        float capW = detailW - 48f;
        LumiWorldUITheme.PlaceTopLeft(capCardImg.rectTransform, 24f, 244f, capW, 134f);

        TextMeshProUGUI capTitle = LumiWorldUITheme.CreateLabel(capCardImg.transform, "Title", "Allocation budget", 14f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(capTitle.rectTransform, 14f, 12f, 160f, 22f);

        pickerCapStockText = LumiWorldUITheme.CreateLabel(capCardImg.transform, "Stock", "Max: 0", 13f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineRight);
        LumiWorldUITheme.PlaceTopRight(pickerCapStockText.rectTransform, 14f, 12f, 130f, 22f);

        pickerDecBtn = LumiWorldUITheme.CreateIconSquareButton(capCardImg.transform, "Dec", "Minus", 42f, 38f, () => ChangePickerBudget(-1), surfaceBg: true);
        LumiWorldUITheme.PlaceTopLeft(pickerDecBtn.GetComponent<RectTransform>(), 14f, 44f, 42f, 38f);

        Image valBg = LumiWorldUITheme.CreateRoundedSurface(capCardImg.transform, "Val", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge, 10, 1.2f, 0f, 0f);
        LumiWorldUITheme.PlaceTopLeft(valBg.rectTransform, 64f, 44f, 102f, 38f);
        pickerCapValueText = LumiWorldUITheme.CreateLabel(valBg.transform, "Text", "20", 17f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(pickerCapValueText.rectTransform);

        pickerIncBtn = LumiWorldUITheme.CreateIconSquareButton(capCardImg.transform, "Inc", "Plus", 42f, 38f, () => ChangePickerBudget(1), surfaceBg: true);
        LumiWorldUITheme.PlaceTopLeft(pickerIncBtn.GetComponent<RectTransform>(), 174f, 44f, 42f, 38f);

        pickerMaxBtn = LumiWorldUITheme.CreatePillButton(
            capCardImg.transform, "MaxBtn", "Max", null, primary: false, fontSize: 13.5f, onClick: SetPickerBudgetToMax, out _, out _, out _, radius: 10);
        LumiWorldUITheme.PlaceTopRight(pickerMaxBtn.GetComponent<RectTransform>(), 14f, 44f, 80f, 38f);

        pickerCapSlider = LumiWorldUITheme.CreateCleanSlider(capCardImg.transform, "Slider", trackHeight: 8f, knobDiameter: 22f);
        LumiWorldUITheme.PlaceTopLeft(pickerCapSlider.GetComponent<RectTransform>(), 14f, 94f, capW - 28f, 28f);
        pickerCapSlider.onValueChanged.AddListener(OnPickerSliderChanged);

        pickerFeedbackText = LumiWorldUITheme.CreateLabel(detailCard.transform, "Feedback", "", 12f, LumiWorldUITheme.Warning, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(pickerFeedbackText.rectTransform, 24f, 384f, capW, 18f);

        pickerAssignBtn = LumiWorldUITheme.CreatePillButton(
            detailCard.transform,
            "AssignSlotBtn",
            "Assign",
            null,
            primary: true,
            fontSize: 15f,
            onClick: OnConfirmAssignSlotClicked,
            out _,
            out _,
            out _,
            radius: 14);
        pickerAssignBtn.gameObject.AddComponent<DialogButtonJuice>();
        LumiWorldUITheme.PlaceTopLeft(pickerAssignBtn.GetComponent<RectTransform>(), 24f, detailH - 60f, capW, 44f);

        slotPickerOverlay.SetActive(false);
    }

    private SlotRowView BuildSlotRow(Transform parent, int index, float yTop)
    {
        float w = PanelWidth - 32f;
        const float h = 50f;
        int capturedSlot = index;

        GameObject rowObj = new GameObject($"AutoFeedSlot_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        rowObj.transform.SetParent(parent, false);
        RectTransform rt = rowObj.GetComponent<RectTransform>();
        LumiWorldUITheme.PlaceTopLeft(rt, 0f, yTop, w, h);

        Image bg = rowObj.GetComponent<Image>();
        bg.sprite = LumiWorldUITheme.SurfaceSprite;
        bg.type = Image.Type.Sliced;
        bg.color = LumiWorldUITheme.Surface;
        LumiWorldUITheme.AddSoftElevation(rowObj, 2f, 0.10f);

        Button btn = rowObj.GetComponent<Button>();
        btn.targetGraphic = bg;
        LumiWorldUITheme.ApplyButtonDisabledNeutral(btn);
        btn.onClick.AddListener(() => OpenSlotPicker(capturedSlot));

        Image iconWell = LumiWorldUITheme.CreateRoundedSurface(
            rt, "IconWell", LumiWorldUITheme.Inset, Color.clear, 10, 0f, 0f, 0f);
        LumiWorldUITheme.PlaceTopLeft(iconWell.rectTransform, 10f, 6f, 38f, 38f);

        GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(iconWell.transform, false);
        RectTransform icRt = icObj.GetComponent<RectTransform>();
        icRt.anchorMin = icRt.anchorMax = icRt.pivot = new Vector2(0.5f, 0.5f);
        icRt.sizeDelta = new Vector2(20f, 20f);
        Image ic = icObj.GetComponent<Image>();
        ic.preserveAspect = true;
        ic.raycastTarget = false;

        TextMeshProUGUI title = LumiWorldUITheme.CreateLabel(rt, "Title", "", 14f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(title.rectTransform, 58f, 7f, 220f, 20f);

        TextMeshProUGUI sub = LumiWorldUITheme.CreateLabel(rt, "Sub", "", 11.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        sub.overflowMode = TextOverflowModes.Overflow;
        LumiWorldUITheme.PlaceTopLeft(sub.rectTransform, 58f, 27f, 220f, 16f);

        GameObject chipObj = new GameObject("StatusChip", typeof(RectTransform), typeof(Image));
        chipObj.transform.SetParent(rt, false);
        LumiWorldUITheme.PlaceTopRight(chipObj.GetComponent<RectTransform>(), 10f, 12f, 70f, 26f);
        Image chipBg = chipObj.GetComponent<Image>();
        chipBg.sprite = LumiWorldUITheme.GetCapsuleSprite(26);
        chipBg.type = Image.Type.Sliced;
        chipBg.color = LumiWorldUITheme.Inset;
        chipBg.raycastTarget = false;

        TextMeshProUGUI chipText = LumiWorldUITheme.CreateLabel(chipObj.transform, "ChipText", "Add", 12f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        LumiWorldUITheme.Stretch(chipText.rectTransform);

        GameObject selObj = new GameObject("SelectedBorder", typeof(RectTransform), typeof(Image));
        selObj.transform.SetParent(rt, false);
        LumiWorldUITheme.Stretch(selObj.GetComponent<RectTransform>());
        Image selImg = selObj.GetComponent<Image>();
        selImg.sprite = LumiWorldUITheme.GetBorderedSurfaceSprite("SelOutline_R16_S2.5", Color.clear, LumiWorldUITheme.Primary, 16, 2.5f);
        selImg.type = Image.Type.Sliced;
        selImg.color = Color.white;
        selImg.raycastTarget = false;
        selObj.SetActive(false);

        return new SlotRowView
        {
            root = rt,
            bg = bg,
            selectedBorder = selImg,
            iconWell = iconWell,
            icon = ic,
            title = title,
            sub = sub,
            chipBg = chipBg,
            chipText = chipText,
            button = btn
        };
    }

    private static void BuildMetricBox(
        Transform parent,
        float x,
        float y,
        float w,
        float h,
        out TextMeshProUGUI labelTmp,
        out TextMeshProUGUI valueTmp,
        out TextMeshProUGUI subTmp)
    {
        Image box = LumiWorldUITheme.CreateRoundedSurface(
            parent, "MetricBox", LumiWorldUITheme.Surface, LumiWorldUITheme.Edge, 14, 1.2f, 2f, 0.12f);
        LumiWorldUITheme.PlaceTopLeft(box.rectTransform, x, y, w, h);

        labelTmp = LumiWorldUITheme.CreateLabel(box.transform, "Label", "", 12.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        LumiWorldUITheme.PlaceTopLeft(labelTmp.rectTransform, 14f, 8f, w - 28f, 18f);

        valueTmp = LumiWorldUITheme.CreateLabel(box.transform, "Value", "", 19f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        valueTmp.overflowMode = TextOverflowModes.Overflow;
        LumiWorldUITheme.PlaceTopLeft(valueTmp.rectTransform, 14f, 26f, w - 28f, 24f);

        subTmp = LumiWorldUITheme.CreateLabel(box.transform, "Sub", "", 11.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        subTmp.overflowMode = TextOverflowModes.Overflow;
        LumiWorldUITheme.PlaceTopLeft(subTmp.rectTransform, 14f, 52f, w - 28f, 16f);
    }

    private void SyncStageScale()
    {
        if (stageRect != null)
        {
            float relScale = LumiWorldUITheme.ComputeFitScale(this, out Vector2 stageSize);
            stageRect.anchorMin = Vector2.zero;
            stageRect.anchorMax = Vector2.zero;
            stageRect.pivot = Vector2.zero;
            stageRect.anchoredPosition = Vector2.zero;
            stageRect.sizeDelta = stageSize;
            stageRect.localScale = Vector3.one * relScale;
        }

        SyncSlotPickerScale();
    }

    private void SyncSlotPickerScale()
    {
        if (slotPickerRect == null) return;
        float modalScale = LumiWorldUITheme.ComputeResponsiveModalScale(this, AssignPopupWidth, AssignPopupHeight, marginX: 44f, marginY: 88f);
        if (sidePopRoutine == null)
        {
            slotPickerRect.localScale = Vector3.one * modalScale;
        }
    }
}

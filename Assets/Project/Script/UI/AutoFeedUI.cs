using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// P-08 Automatic Feeding UI — LumiWorld UI Production Pack:
/// - Single biome type selected at a time (NO "All biomes").
/// - Biome selector pills use the same colored Biome Emblem (B-01..B-07) and canonical name as P-05.
/// - Each biome type has its own independent 3 food slots and shared remaining budget for habitats/creatures
///   of that biome type in the active world (saved per user/world/biome type; never copies global config).
/// - Budget is never multiplied per creature and is only decremented when DepositCreatureFood actually succeeds.
/// </summary>
public class AutoFeedUI : MonoBehaviour
{
    private static AutoFeedUI instance;
    public static AutoFeedUI Instance => instance;

    public const float WindowWidth = 1040f;
    public const float WindowHeight = 620f;

    public bool IsOpen => window != null && window.activeSelf;

    private GameObject window;
    private RectTransform panel;
    private RectTransform biomeTabsRow;
    private RectTransform foodListContent;
    private TextMeshProUGUI emptyFoodNoticeText;
    private TextMeshProUGUI remainingBudgetText;
    private TextMeshProUGUI feedbackNotifyText;
    private Coroutine feedbackFadeRoutine;
    private Button applySetupButton;

    // 3 Slot views for the currently selected biome
    private readonly DispenserSlotView[] slotViews = new DispenserSlotView[3];

    private class DispenserSlotView
    {
        public int index;
        public RectTransform root;
        public GameObject emptyContainer;
        public Button addFoodButton;
        public TextMeshProUGUI addFoodLabel;
        public GameObject occupiedContainer;
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI subText;
        public Button decButton;
        public Button amountButton;
        public TextMeshProUGUI amountText;
        public Button incButton;
        public Button removeButton;
    }

    private class BiomeTabView
    {
        public BiomeAffinity affinity;
        public Button button;
        public Image bg;
        public Image border;
        public Image emblem;
        public TextMeshProUGUI label;
    }

    private readonly List<BiomeTabView> biomeTabs = new List<BiomeTabView>();

    // Per-biome 3-slot config in memory (synced with PlayerPrefs per world + biome)
    private class BiomeFeedSlots
    {
        public readonly string[] foodIds = new string[3];
        public readonly int[] budgets = new int[3];
    }

    private readonly Dictionary<BiomeAffinity, BiomeFeedSlots> slotsByBiome = new Dictionary<BiomeAffinity, BiomeFeedSlots>();
    private readonly Dictionary<BiomeAffinity, BiomeFeedSlots> draftsByBiome = new Dictionary<BiomeAffinity, BiomeFeedSlots>();
    private BiomeAffinity activeBiome = BiomeAffinity.Leafwood;
    private string loadedWorldScopeKey = string.Empty;

    // Available foods in Inventory (left column)
    private readonly List<ResourceUIItem> currentFoodList = new List<ResourceUIItem>();
    private readonly Dictionary<string, Image> foodRowBorders = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextMeshProUGUI> foodStockLabels = new Dictionary<string, TextMeshProUGUI>(StringComparer.OrdinalIgnoreCase);
    private string selectedFoodId = string.Empty;

    // Quantity Modal
    private GameObject quantityModalObj;
    private Image modalFoodIcon;
    private TextMeshProUGUI modalFoodNameText;
    private TextMeshProUGUI modalStorageText;
    private TextMeshProUGUI modalAmountValueText;
    private Slider modalSlider;
    private int modalEditingSlot = -1;
    private int modalCurrentAmount = 1;

    private float nextAutoTickTime;
    private static Sprite s_SelectionBorderSprite;

    private static readonly BiomeAffinity[] SupportedBiomes =
    {
        BiomeAffinity.Leafwood,
        BiomeAffinity.Bloomfield,
        BiomeAffinity.Windheath,
        BiomeAffinity.Reedmarsh,
        BiomeAffinity.Lilywater,
        BiomeAffinity.Streamstone,
        BiomeAffinity.Rainleaf
    };

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        EnsureWorldConfigLoaded();
        BuildWindow();
    }

    private void Start()
    {
        if (window != null)
        {
            EconomyHUD.RegisterWindow(window, () =>
            {
                if (quantityModalObj != null && quantityModalObj.activeSelf) CloseQuantityModal();
                else Close();
            });
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        EconomyHUD.UnregisterWindow(window);
    }

    private void Update()
    {
        if (IsOpen && panel != null)
        {
            float scale = ComputePanelScale();
            panel.localScale = new Vector3(scale, scale, 1f);
        }

        // Background auto-feeding tick every 3 seconds for biomes with remaining budget
        if (Application.isPlaying && !WorldLaunch.IsVisit && Time.unscaledTime >= nextAutoTickTime)
        {
            nextAutoTickTime = Time.unscaledTime + 3.0f;
            EnsureWorldConfigLoaded();
            RunBackgroundAutoFeedTick();
        }
    }

    private float ComputePanelScale()
    {
        var invCtrl = FindAnyObjectByType<ResourceInventoryUIController>(FindObjectsInactive.Include);
        if (invCtrl != null && invCtrl.transform.lossyScale.x > 0.0001f && transform.lossyScale.x > 0.0001f)
        {
            return invCtrl.transform.lossyScale.x / transform.lossyScale.x;
        }

        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        float canvasScale = rootCanvas != null && rootCanvas.scaleFactor > 0.01f ? rootCanvas.scaleFactor : 1f;
        float fit = ResourceInventoryUILayout.FitScale(Screen.width, Screen.height);
        float parentScale = transform.lossyScale.x > 0.0001f ? transform.lossyScale.x : (1f / canvasScale);
        return fit / parentScale;
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        EnsureWorldConfigLoaded();
        if (window == null) BuildWindow();
        if (panel != null)
        {
            float scale = ComputePanelScale();
            panel.localScale = new Vector3(scale, scale, 1f);
        }

        window.SetActive(true);
        window.transform.SetAsLastSibling();
        EconomyHUD.BringWindowToFront(window);

        RefreshBiomeSelectorTabs();
        RefreshFoodInventory();
        RefreshSlotsAndBudgetUI();
        GameAudioManager.Instance?.PlayClick();
    }

    public void Close()
    {
        draftsByBiome.Clear();
        modalEditingSlot = -1;
        if (quantityModalObj != null && quantityModalObj.activeSelf)
        {
            quantityModalObj.SetActive(false);
        }
        if (window != null && window.activeSelf)
        {
            window.SetActive(false);
            EconomyHUD.MarkWindowClosed();
            GameAudioManager.Instance?.PlayClick();
        }
    }

    // =========================================================
    // PER-WORLD, PER-BIOME PERSISTENCE
    // =========================================================

    private static string GetWorldScopeKey()
    {
        string player = !string.IsNullOrEmpty(SaveSlot.PlayerId) ? SaveSlot.PlayerId : "local";
        string world = !string.IsNullOrEmpty(WorldLaunch.WorldId) ? WorldLaunch.WorldId : SceneManager.GetActiveScene().name;
        return $"{player}_{world}";
    }

    private void EnsureWorldConfigLoaded()
    {
        string currentKey = GetWorldScopeKey();
        if (string.Equals(loadedWorldScopeKey, currentKey, StringComparison.Ordinal) && slotsByBiome.Count > 0)
        {
            return;
        }

        loadedWorldScopeKey = currentKey;
        slotsByBiome.Clear();
        draftsByBiome.Clear();
        modalEditingSlot = -1;
        if (quantityModalObj != null) quantityModalObj.SetActive(false);

        foreach (BiomeAffinity biome in SupportedBiomes)
        {
            var data = new BiomeFeedSlots();
            for (int i = 0; i < 3; i++)
            {
                string keyPrefix = $"LumiWorld_AutoFeed_v2_{loadedWorldScopeKey}_{biome}_{i}";
                data.foodIds[i] = PlayerPrefs.GetString(keyPrefix + "_Id", string.Empty);
                data.budgets[i] = Mathf.Max(0, PlayerPrefs.GetInt(keyPrefix + "_Budget", 0));
                if (string.IsNullOrEmpty(data.foodIds[i]) || data.budgets[i] <= 0)
                {
                    data.foodIds[i] = string.Empty;
                    data.budgets[i] = 0;
                }
            }
            slotsByBiome[biome] = data;
        }

        int savedBiomeInt = PlayerPrefs.GetInt($"LumiWorld_AutoFeed_v2_{loadedWorldScopeKey}_ActiveBiome", (int)BiomeAffinity.Leafwood);
        if (Enum.IsDefined(typeof(BiomeAffinity), savedBiomeInt) && savedBiomeInt != (int)BiomeAffinity.None)
        {
            activeBiome = (BiomeAffinity)savedBiomeInt;
        }
        else
        {
            activeBiome = BiomeAffinity.Leafwood;
        }
    }

    private BiomeFeedSlots GetSlotsFor(BiomeAffinity biome)
    {
        EnsureWorldConfigLoaded();
        if (!slotsByBiome.TryGetValue(biome, out var data) || data == null)
        {
            data = new BiomeFeedSlots();
            slotsByBiome[biome] = data;
        }
        return data;
    }

    private BiomeFeedSlots GetDisplayedSlots()
    {
        var saved = GetSlotsFor(activeBiome);
        return draftsByBiome.TryGetValue(activeBiome, out var draft) ? draft : saved;
    }

    private BiomeFeedSlots EditActiveSlots()
    {
        var saved = GetSlotsFor(activeBiome);
        if (!draftsByBiome.TryGetValue(activeBiome, out var draft))
        {
            draft = new BiomeFeedSlots();
            Array.Copy(saved.foodIds, draft.foodIds, saved.foodIds.Length);
            Array.Copy(saved.budgets, draft.budgets, saved.budgets.Length);
            draftsByBiome.Add(activeBiome, draft);
        }
        return draft;
    }

    private void SaveBiomeSlots(BiomeAffinity biome)
    {
        if (WorldLaunch.IsVisit) return;
        EnsureWorldConfigLoaded();
        var data = GetSlotsFor(biome);
        for (int i = 0; i < 3; i++)
        {
            string keyPrefix = $"LumiWorld_AutoFeed_v2_{loadedWorldScopeKey}_{biome}_{i}";
            PlayerPrefs.SetString(keyPrefix + "_Id", data.foodIds[i] ?? string.Empty);
            PlayerPrefs.SetInt(keyPrefix + "_Budget", Mathf.Max(0, data.budgets[i]));
        }
        PlayerPrefs.SetInt($"LumiWorld_AutoFeed_v2_{loadedWorldScopeKey}_ActiveBiome", (int)activeBiome);
        PlayerPrefs.Save();
    }

    // =========================================================
    // UI CONSTRUCTION (P-08 Warm Theme)
    // =========================================================

    private void BuildWindow()
    {
        if (window != null) return;

        window = new GameObject("AutoFeedWindow", typeof(RectTransform));
        window.transform.SetParent(transform, false);
        var winRect = window.GetComponent<RectTransform>();
        EconomyHUD.Stretch(winRect);

        var dimObj = new GameObject("Dimmer", typeof(RectTransform), typeof(Image), typeof(Button));
        dimObj.transform.SetParent(window.transform, false);
        EconomyHUD.Stretch(dimObj.GetComponent<RectTransform>());
        var dimImg = dimObj.GetComponent<Image>();
        dimImg.color = LumiWorldUITheme.Backdrop;
        dimImg.raycastTarget = true;
        dimObj.AddComponent<ResourceUIInputBlocker>();
        var dimBtn = dimObj.GetComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        GameObject panelObj = new GameObject("MainPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(window.transform, false);
        panel = panelObj.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);

        var pImg = panelObj.GetComponent<Image>();
        pImg.sprite = LumiWorldUITheme.WindowSprite;
        pImg.type = Image.Type.Sliced;
        pImg.color = LumiWorldUITheme.Window;
        pImg.raycastTarget = true;
        panelObj.AddComponent<ResourceUIInputBlocker>();
        LumiWorldUITheme.AddSoftElevation(panelObj, 6f, 0.22f);

        BuildHeader();
        BuildBiomeSelectorRow();
        BuildLeftFoodListSection();
        BuildRightAssignedSection();
        BuildFooterBar();

        window.SetActive(false);
    }

    private void BuildHeader()
    {
        var titleTxt = CreateTMP(panel, "Title", "Automatic Feeding", 25f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        titleTxt.font = LumiWorldUITheme.Heading ?? titleTxt.font;
        titleTxt.overflowMode = TextOverflowModes.Overflow;
        SetTopLeft(titleTxt.rectTransform, new Vector2(30f, -15f), new Vector2(540f, 42f));

        // Close [×] button
        GameObject closeObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(panel, false);
        var cRect = closeObj.GetComponent<RectTransform>();
        cRect.anchorMin = cRect.anchorMax = cRect.pivot = new Vector2(1f, 1f);
        cRect.anchoredPosition = new Vector2(-24f, -16f);
        cRect.sizeDelta = new Vector2(42f, 42f);

        var cImg = closeObj.GetComponent<Image>();
        cImg.sprite = LumiWorldUITheme.PillSprite;
        cImg.type = Image.Type.Sliced;
        cImg.color = LumiWorldUITheme.Inset;
        LumiWorldUITheme.AddSoftElevation(closeObj, 2.5f, 0.14f);

        GameObject xIcObj = new GameObject("X", typeof(RectTransform), typeof(Image));
        xIcObj.transform.SetParent(cRect, false);
        RectTransform xIcRect = xIcObj.GetComponent<RectTransform>();
        xIcRect.anchorMin = xIcRect.anchorMax = xIcRect.pivot = new Vector2(0.5f, 0.5f);
        xIcRect.sizeDelta = new Vector2(18f, 18f);
        Image xIcImg = xIcObj.GetComponent<Image>();
        xIcImg.sprite = LumiWorldUITheme.Glyph("Close");
        xIcImg.preserveAspect = true;
        xIcImg.color = LumiWorldUITheme.Text;
        xIcImg.raycastTarget = false;

        var cBtn = closeObj.GetComponent<Button>();
        cBtn.targetGraphic = cImg;
        cBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        cBtn.onClick.AddListener(Close);
    }

    private void BuildBiomeSelectorRow()
    {
        GameObject rowObj = new GameObject("BiomeSelectorRow", typeof(RectTransform));
        rowObj.transform.SetParent(panel, false);
        biomeTabsRow = rowObj.GetComponent<RectTransform>();
        SetTopLeft(biomeTabsRow, new Vector2(28f, -68f), new Vector2(WindowWidth - 56f, 44f));

        RefreshBiomeSelectorTabs();
    }

    private void RefreshBiomeSelectorTabs()
    {
        if (biomeTabsRow == null) return;

        for (int i = biomeTabsRow.childCount - 1; i >= 0; i--)
        {
            Destroy(biomeTabsRow.GetChild(i).gameObject);
        }
        biomeTabs.Clear();

        // Collect biomes present on the active island first, followed by standard island biomes (no "All biomes")
        List<BiomeAffinity> biomesToShow = new List<BiomeAffinity>();
        HabitatRuntimeManager habManager = null;
        if (ResourceProductionRuntime.Instance != null && ResourceProductionRuntime.Instance.HabitatManager != null)
            habManager = ResourceProductionRuntime.Instance.HabitatManager;
        else
            HabitatRuntimeManager.TryGetActive(out habManager);

        if (habManager != null && habManager.Habitats != null)
        {
            foreach (var h in habManager.Habitats)
            {
                if (h != null && Array.IndexOf(SupportedBiomes, h.affinity) >= 0 && !biomesToShow.Contains(h.affinity))
                {
                    biomesToShow.Add(h.affinity);
                }
            }
        }

        // Ensure core biomes are always selectable even before habitats are built
        BiomeAffinity[] defaultIslandBiomes =
        {
            BiomeAffinity.Leafwood,
            BiomeAffinity.Bloomfield,
            BiomeAffinity.Windheath,
            BiomeAffinity.Reedmarsh
        };
        foreach (var b in defaultIslandBiomes)
        {
            if (!biomesToShow.Contains(b)) biomesToShow.Add(b);
        }

        // Also include any biome that already has saved food slots in this world
        foreach (var b in SupportedBiomes)
        {
            if (!biomesToShow.Contains(b))
            {
                var slots = GetSlotsFor(b);
                if (!string.IsNullOrEmpty(slots.foodIds[0]) || !string.IsNullOrEmpty(slots.foodIds[1]) || !string.IsNullOrEmpty(slots.foodIds[2]))
                {
                    biomesToShow.Add(b);
                }
            }
        }

        if (!biomesToShow.Contains(activeBiome) && biomesToShow.Count > 0)
        {
            activeBiome = biomesToShow[0];
        }

        float curX = 0f;
        const float tabH = 40f;
        const float tabW = 150f;
        const float gapX = 12f;

        foreach (BiomeAffinity affinity in biomesToShow)
        {
            string bName = HabitatCreaturePanelStyle.GetAffinityDisplayName(affinity);

            GameObject tabObj = new GameObject("BiomeTab_" + bName, typeof(RectTransform), typeof(Image), typeof(Button));
            tabObj.transform.SetParent(biomeTabsRow, false);
            RectTransform tRect = tabObj.GetComponent<RectTransform>();
            SetTopLeft(tRect, new Vector2(curX, 0f), new Vector2(tabW, tabH));

            Image bg = tabObj.GetComponent<Image>();
            bg.sprite = LumiWorldUITheme.GetCapsuleSprite(Mathf.RoundToInt(tabH));
            bg.type = Image.Type.Sliced;
            LumiWorldUITheme.AddSoftElevation(tabObj, 2.5f, 0.14f);

            // Colored Biome Emblem (B-01..B-07, shared with P-05)
            GameObject emObj = new GameObject("Emblem", typeof(RectTransform), typeof(Image));
            emObj.transform.SetParent(tRect, false);
            RectTransform emRect = emObj.GetComponent<RectTransform>();
            emRect.anchorMin = emRect.anchorMax = new Vector2(0f, 0.5f);
            emRect.pivot = new Vector2(0f, 0.5f);
            emRect.anchoredPosition = new Vector2(12f, 0f);
            emRect.sizeDelta = new Vector2(28f, 28f);
            Image emImg = emObj.GetComponent<Image>();
            emImg.sprite = LumiWorldUITheme.BiomeEmblem(bName);
            emImg.preserveAspect = true;
            emImg.raycastTarget = false;

            TextMeshProUGUI lbl = CreateTMP(tRect, "Label", bName, 15f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = new Vector2(46f, 0f);
            lbl.rectTransform.offsetMax = new Vector2(-12f, 0f);
            lbl.enableAutoSizing = true;
            lbl.fontSizeMin = 11f;
            lbl.fontSizeMax = 15f;

            GameObject borderObj = new GameObject("SelectedBorder", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(tRect, false);
            EconomyHUD.Stretch(borderObj.GetComponent<RectTransform>());
            Image borderImg = borderObj.GetComponent<Image>();
            borderImg.sprite = GetTabSelectionBorderSprite(Mathf.RoundToInt(tabH));
            borderImg.type = Image.Type.Sliced;
            borderImg.color = Color.white;
            borderImg.raycastTarget = false;

            Button btn = tabObj.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            BiomeAffinity captured = affinity;
            btn.onClick.AddListener(() => SelectBiomeTab(captured));

            biomeTabs.Add(new BiomeTabView
            {
                affinity = affinity,
                button = btn,
                bg = bg,
                border = borderImg,
                emblem = emImg,
                label = lbl
            });

            curX += tabW + gapX;
        }

        UpdateBiomeTabVisuals();
    }

    private void SelectBiomeTab(BiomeAffinity biome)
    {
        if (activeBiome == biome) return;
        activeBiome = biome;
        UpdateBiomeTabVisuals();
        UpdateFoodRowBorders();
        RefreshSlotsAndBudgetUI();
        GameAudioManager.Instance?.PlayClick();
    }

    private void UpdateBiomeTabVisuals()
    {
        foreach (var tab in biomeTabs)
        {
            bool active = tab.affinity == activeBiome;
            tab.bg.color = active ? LumiWorldUITheme.Slot : LumiWorldUITheme.Inset;
            if (tab.border != null) tab.border.gameObject.SetActive(active);
            if (tab.label != null) tab.label.color = active ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted;
        }
    }

    private void BuildLeftFoodListSection()
    {
        GameObject leftBox = new GameObject("LeftFoodListWell", typeof(RectTransform), typeof(Image));
        leftBox.transform.SetParent(panel, false);
        RectTransform lRect = leftBox.GetComponent<RectTransform>();
        SetTopLeft(lRect, new Vector2(28f, -124f), new Vector2(464f, 416f));

        Image wellBg = leftBox.GetComponent<Image>();
        wellBg.sprite = LumiWorldUITheme.SurfaceSprite;
        wellBg.type = Image.Type.Sliced;
        wellBg.color = LumiWorldUITheme.Well;
        wellBg.raycastTarget = false;

        GameObject scrollObj = new GameObject("FoodScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(lRect, false);
        RectTransform sRect = scrollObj.GetComponent<RectTransform>();
        sRect.anchorMin = Vector2.zero;
        sRect.anchorMax = Vector2.one;
        sRect.offsetMin = new Vector2(6f, 6f);
        sRect.offsetMax = new Vector2(-6f, -6f);

        var scroll = scrollObj.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        var viewObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewObj.transform.SetParent(scrollObj.transform, false);
        var vRect = viewObj.GetComponent<RectTransform>();
        EconomyHUD.Stretch(vRect);
        var vImg = viewObj.GetComponent<Image>();
        vImg.color = Color.clear;
        vImg.raycastTarget = true;
        viewObj.AddComponent<ResourceUIInputBlocker>();

        var contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(viewObj.transform, false);
        foodListContent = contentObj.GetComponent<RectTransform>();
        foodListContent.anchorMin = new Vector2(0f, 1f);
        foodListContent.anchorMax = new Vector2(1f, 1f);
        foodListContent.pivot = new Vector2(0f, 1f);
        foodListContent.anchoredPosition = Vector2.zero;
        foodListContent.sizeDelta = new Vector2(0f, 404f);

        scroll.viewport = vRect;
        scroll.content = foodListContent;

        emptyFoodNoticeText = CreateTMP(
            scrollObj.transform,
            "EmptyNotice",
            "No configured creature food in Inventory.",
            15f,
            LumiWorldUITheme.Muted,
            true,
            TextAlignmentOptions.Center);
        EconomyHUD.Stretch(emptyFoodNoticeText.rectTransform);
        emptyFoodNoticeText.textWrappingMode = TextWrappingModes.Normal;
        emptyFoodNoticeText.gameObject.SetActive(false);
    }

    private void BuildRightAssignedSection()
    {
        GameObject rightCard = new GameObject("RightAssignedCard", typeof(RectTransform), typeof(Image));
        rightCard.transform.SetParent(panel, false);
        RectTransform rRect = rightCard.GetComponent<RectTransform>();
        SetTopLeft(rRect, new Vector2(508f, -124f), new Vector2(504f, 416f));

        Image rBg = rightCard.GetComponent<Image>();
        rBg.sprite = LumiWorldUITheme.SurfaceSprite;
        rBg.type = Image.Type.Sliced;
        rBg.color = LumiWorldUITheme.Surface;
        rBg.raycastTarget = true;
        rightCard.AddComponent<ResourceUIInputBlocker>();
        LumiWorldUITheme.AddSoftElevation(rightCard, 4f, 0.16f);

        // Header: "Assigned foods" + "Max 3 foods" pill
        var hdrTxt = CreateTMP(rRect, "AssignedHeader", "Assigned foods", 18.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(hdrTxt.rectTransform, new Vector2(22f, -16f), new Vector2(240f, 28f));

        GameObject maxPill = new GameObject("Max3Pill", typeof(RectTransform), typeof(Image));
        maxPill.transform.SetParent(rRect, false);
        RectTransform mpRect = maxPill.GetComponent<RectTransform>();
        mpRect.anchorMin = mpRect.anchorMax = mpRect.pivot = new Vector2(1f, 1f);
        mpRect.anchoredPosition = new Vector2(-22f, -16f);
        mpRect.sizeDelta = new Vector2(118f, 28f);
        Image mpBg = maxPill.GetComponent<Image>();
        mpBg.sprite = LumiWorldUITheme.GetCapsuleSprite(28);
        mpBg.type = Image.Type.Sliced;
        mpBg.color = LumiWorldUITheme.Inset;
        var mpTxt = CreateTMP(mpRect, "Label", "Max 3 foods", 12.5f, LumiWorldUITheme.Muted, true, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(mpTxt.rectTransform);

        // 3 Vertical Food Slot Rows
        const float slotW = 460f;
        const float slotH = 104f;
        const float startY = -58f;
        const float gapY = 12f;

        for (int i = 0; i < 3; i++)
        {
            int slotIndex = i;
            float y = startY - i * (slotH + gapY);

            GameObject slotObj = new GameObject("AssignedSlot_" + i, typeof(RectTransform), typeof(Image));
            slotObj.transform.SetParent(rRect, false);
            RectTransform sRect = slotObj.GetComponent<RectTransform>();
            SetTopLeft(sRect, new Vector2(22f, y), new Vector2(slotW, slotH));

            Image sBg = slotObj.GetComponent<Image>();
            sBg.sprite = LumiWorldUITheme.SurfaceSprite;
            sBg.type = Image.Type.Sliced;
            sBg.color = LumiWorldUITheme.Window;
            LumiWorldUITheme.AddSoftElevation(slotObj, 2.5f, 0.14f);

            var view = new DispenserSlotView { index = slotIndex, root = sRect };

            // --- A. EMPTY CONTAINER ("[+ Add food]") ---
            GameObject emptyCont = new GameObject("EmptyContainer", typeof(RectTransform));
            emptyCont.transform.SetParent(sRect, false);
            EconomyHUD.Stretch(emptyCont.GetComponent<RectTransform>());
            view.emptyContainer = emptyCont;

            GameObject addBtnObj = new GameObject("AddFoodBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            addBtnObj.transform.SetParent(emptyCont.transform, false);
            RectTransform abRect = addBtnObj.GetComponent<RectTransform>();
            abRect.anchorMin = abRect.anchorMax = abRect.pivot = new Vector2(0.5f, 0.5f);
            abRect.sizeDelta = new Vector2(168f, 40f);

            Image abBg = addBtnObj.GetComponent<Image>();
            abBg.sprite = LumiWorldUITheme.PillSprite;
            abBg.type = Image.Type.Sliced;
            abBg.color = LumiWorldUITheme.Surface;
            LumiWorldUITheme.AddSoftElevation(addBtnObj, 2f, 0.14f);

            GameObject plusIcObj = new GameObject("PlusIcon", typeof(RectTransform), typeof(Image));
            plusIcObj.transform.SetParent(abRect, false);
            RectTransform plusRect = plusIcObj.GetComponent<RectTransform>();
            plusRect.anchorMin = plusRect.anchorMax = new Vector2(0f, 0.5f);
            plusRect.pivot = new Vector2(0f, 0.5f);
            plusRect.anchoredPosition = new Vector2(22f, 0f);
            plusRect.sizeDelta = new Vector2(16f, 16f);
            Image plusImg = plusIcObj.GetComponent<Image>();
            plusImg.sprite = LumiWorldUITheme.Glyph("Plus");
            plusImg.preserveAspect = true;
            plusImg.color = LumiWorldUITheme.Text;
            plusImg.raycastTarget = false;

            view.addFoodLabel = CreateTMP(abRect, "Label", "Add food", 14.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
            view.addFoodLabel.rectTransform.anchorMin = Vector2.zero;
            view.addFoodLabel.rectTransform.anchorMax = Vector2.one;
            view.addFoodLabel.rectTransform.offsetMin = new Vector2(46f, 0f);
            view.addFoodLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);

            view.addFoodButton = addBtnObj.GetComponent<Button>();
            view.addFoodButton.targetGraphic = abBg;
            view.addFoodButton.navigation = new Navigation { mode = Navigation.Mode.None };
            view.addFoodButton.onClick.AddListener(() => OnAddFoodToSlotClicked(slotIndex));

            // --- B. OCCUPIED CONTAINER ---
            GameObject occCont = new GameObject("OccupiedContainer", typeof(RectTransform));
            occCont.transform.SetParent(sRect, false);
            EconomyHUD.Stretch(occCont.GetComponent<RectTransform>());
            view.occupiedContainer = occCont;

            // Icon Box (64x64)
            GameObject icBox = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
            icBox.transform.SetParent(occCont.transform, false);
            RectTransform ibRect = icBox.GetComponent<RectTransform>();
            SetTopLeft(ibRect, new Vector2(16f, -20f), new Vector2(64f, 64f));
            Image ibBg = icBox.GetComponent<Image>();
            ibBg.sprite = LumiWorldUITheme.SurfaceSprite;
            ibBg.type = Image.Type.Sliced;
            ibBg.color = LumiWorldUITheme.Slot;

            GameObject icObj = new GameObject("FoodIcon", typeof(RectTransform), typeof(Image));
            icObj.transform.SetParent(ibRect, false);
            RectTransform icRect = icObj.GetComponent<RectTransform>();
            icRect.anchorMin = Vector2.zero;
            icRect.anchorMax = Vector2.one;
            icRect.offsetMin = new Vector2(6f, 6f);
            icRect.offsetMax = new Vector2(-6f, -6f);
            view.iconImage = icObj.GetComponent<Image>();
            view.iconImage.preserveAspect = true;
            view.iconImage.raycastTarget = false;

            // Food Name & Subtitle
            view.nameText = CreateTMP(occCont.transform, "FoodName", "Raspberries", 16.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
            SetTopLeft(view.nameText.rectTransform, new Vector2(94f, -24f), new Vector2(180f, 26f));

            view.subText = CreateTMP(occCont.transform, "SubText", "Allocated budget", 13f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
            SetTopLeft(view.subText.rectTransform, new Vector2(94f, -52f), new Vector2(180f, 22f));

            // Stepper Controls on Right: [-] [Amount] [+] and [×]
            view.decButton = CreateStepperSquareButton(occCont.transform, "DecBtn", new Vector2(280f, -34f), new Vector2(36f, 36f), "Minus", () => ChangeSlotAmount(slotIndex, -1));

            GameObject amtBtnObj = new GameObject("AmountBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            amtBtnObj.transform.SetParent(occCont.transform, false);
            RectTransform amRect = amtBtnObj.GetComponent<RectTransform>();
            SetTopLeft(amRect, new Vector2(322f, -34f), new Vector2(56f, 36f));
            Image amBg = amtBtnObj.GetComponent<Image>();
            amBg.sprite = LumiWorldUITheme.SurfaceSprite;
            amBg.type = Image.Type.Sliced;
            amBg.color = LumiWorldUITheme.Slot;
            view.amountText = CreateTMP(amRect, "AmountText", "10", 15f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(view.amountText.rectTransform);
            view.amountButton = amtBtnObj.GetComponent<Button>();
            view.amountButton.targetGraphic = amBg;
            view.amountButton.onClick.AddListener(() => OpenQuantityModal(slotIndex));

            view.incButton = CreateStepperSquareButton(occCont.transform, "IncBtn", new Vector2(384f, -34f), new Vector2(36f, 36f), "Plus", () => ChangeSlotAmount(slotIndex, 1));

            // Remove [×] button at top-right of slot
            view.removeButton = CreateStepperSquareButton(occCont.transform, "RemoveBtn", new Vector2(slotW - 34f, -8f), new Vector2(26f, 26f), "Close", () => ClearSlot(slotIndex));

            slotViews[i] = view;
        }
    }

    private Button CreateStepperSquareButton(Transform parent, string name, Vector2 topLeftPos, Vector2 size, string glyphName, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        SetTopLeft(rt, topLeftPos, size);

        Image bg = btnObj.GetComponent<Image>();
        bg.sprite = LumiWorldUITheme.PillSprite;
        bg.type = Image.Type.Sliced;
        bg.color = LumiWorldUITheme.Surface;
        LumiWorldUITheme.AddSoftElevation(btnObj, 2f, 0.14f);

        GameObject icObj = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(rt, false);
        RectTransform icRect = icObj.GetComponent<RectTransform>();
        icRect.anchorMin = icRect.anchorMax = icRect.pivot = new Vector2(0.5f, 0.5f);
        icRect.sizeDelta = new Vector2(size.x * 0.48f, size.y * 0.48f);
        Image icImg = icObj.GetComponent<Image>();
        icImg.sprite = LumiWorldUITheme.Glyph(glyphName);
        icImg.preserveAspect = true;
        icImg.color = LumiWorldUITheme.Text;
        icImg.raycastTarget = false;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(onClick);
        return btn;
    }

    private void BuildFooterBar()
    {
        // Left: [G-18] Remaining budget: N
        GameObject budgetRow = new GameObject("RemainingBudgetRow", typeof(RectTransform));
        budgetRow.transform.SetParent(panel, false);
        RectTransform brRect = budgetRow.GetComponent<RectTransform>();
        brRect.anchorMin = brRect.anchorMax = brRect.pivot = new Vector2(0f, 0f);
        brRect.anchoredPosition = new Vector2(30f, 22f);
        brRect.sizeDelta = new Vector2(320f, 36f);

        GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(brRect, false);
        RectTransform icRect = icObj.GetComponent<RectTransform>();
        icRect.anchorMin = icRect.anchorMax = new Vector2(0f, 0.5f);
        icRect.pivot = new Vector2(0f, 0.5f);
        icRect.anchoredPosition = new Vector2(0f, 0f);
        icRect.sizeDelta = new Vector2(22f, 22f);
        Image icImg = icObj.GetComponent<Image>();
        icImg.sprite = LumiWorldUITheme.Glyph("G-18");
        icImg.preserveAspect = true;
        icImg.color = LumiWorldUITheme.Muted;
        icImg.raycastTarget = false;

        remainingBudgetText = CreateTMP(brRect, "BudgetText", "Remaining budget: 0", 16f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        remainingBudgetText.rectTransform.anchorMin = Vector2.zero;
        remainingBudgetText.rectTransform.anchorMax = Vector2.one;
        remainingBudgetText.rectTransform.offsetMin = new Vector2(30f, 0f);
        remainingBudgetText.rectTransform.offsetMax = Vector2.zero;

        // Center Feedback Notify Text
        feedbackNotifyText = CreateTMP(panel, "FeedbackNotify", "", 13.5f, LumiWorldUITheme.Success, true, TextAlignmentOptions.Center);
        feedbackNotifyText.rectTransform.anchorMin = feedbackNotifyText.rectTransform.anchorMax = feedbackNotifyText.rectTransform.pivot = new Vector2(0.5f, 0f);
        feedbackNotifyText.rectTransform.anchoredPosition = new Vector2(-10f, 26f);
        feedbackNotifyText.rectTransform.sizeDelta = new Vector2(380f, 28f);

        // Right Primary CTA: "Apply feeding setup"
        GameObject applyObj = new GameObject("ApplySetupBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        applyObj.transform.SetParent(panel, false);
        RectTransform apRect = applyObj.GetComponent<RectTransform>();
        apRect.anchorMin = apRect.anchorMax = apRect.pivot = new Vector2(1f, 0f);
        apRect.anchoredPosition = new Vector2(-28f, 16f);
        apRect.sizeDelta = new Vector2(236f, 48f);

        Image apImg = applyObj.GetComponent<Image>();
        apImg.sprite = LumiWorldUITheme.ButtonPrimarySprite;
        apImg.type = Image.Type.Sliced;
        apImg.color = LumiWorldUITheme.Primary;
        LumiWorldUITheme.AddSoftElevation(applyObj, 3f, 0.20f);

        var apTxt = CreateTMP(apRect, "Label", "Apply feeding setup", 16f, Color.white, true, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(apTxt.rectTransform);

        applySetupButton = applyObj.GetComponent<Button>();
        applySetupButton.targetGraphic = apImg;
        applySetupButton.navigation = new Navigation { mode = Navigation.Mode.None };
        applySetupButton.onClick.AddListener(OnApplyFeedingSetupClicked);
    }

    // =========================================================
    // FOOD INVENTORY & SLOT MANAGEMENT
    // =========================================================

    public void RefreshFoodInventory()
    {
        if (foodListContent == null) return;
        var production = ResourceProductionRuntime.Instance;
        var inventory = production != null ? production.Inventory : null;
        var available = new List<ResourceUIItem>();

        if (inventory != null)
        {
            foreach (var food in production.FoodTypes)
            {
                string id = food.ResourceId;
                int stock = inventory.GetAmount(id);
                if (stock <= 0) continue;
                var resource = ResourceCatalog.Find(id);
                available.Add(new ResourceUIItem
                {
                    id = id,
                    name = ResourceInventoryUIPresentation.LocalizeResourceName(id, resource != null ? resource.resourceName : id),
                    icon = resource != null ? resource.icon : null,
                    category = "Food",
                    stock = stock
                });
            }
        }

        bool sameRows = available.Count == currentFoodList.Count;
        for (int i = 0; sameRows && i < available.Count; i++)
        {
            sameRows = available[i].id == currentFoodList[i].id && available[i].name == currentFoodList[i].name &&
                available[i].icon == currentFoodList[i].icon;
        }
        currentFoodList.Clear();
        currentFoodList.AddRange(available);

        if (emptyFoodNoticeText != null)
        {
            emptyFoodNoticeText.gameObject.SetActive(currentFoodList.Count == 0);
        }

        if (sameRows)
        {
            foreach (var food in currentFoodList) foodStockLabels[food.id].text = $"{food.stock} available";
            return;
        }

        ResourceUIGraphics.Clear(foodListContent);
        foodRowBorders.Clear();
        foodStockLabels.Clear();

        if (currentFoodList.Count == 0)
        {
            selectedFoodId = string.Empty;
            foodListContent.sizeDelta = new Vector2(0f, 404f);
            return;
        }

        if (string.IsNullOrEmpty(selectedFoodId) || !currentFoodList.Exists(f => string.Equals(f.id, selectedFoodId, StringComparison.OrdinalIgnoreCase)))
        {
            selectedFoodId = currentFoodList[0].id;
        }

        const float rowW = 440f;
        const float rowH = 68f;
        const float gapY = 10f;
        float curY = 4f;

        for (int i = 0; i < currentFoodList.Count; i++)
        {
            var item = currentFoodList[i];
            float y = -curY;

            var rowObj = new GameObject("FoodRow_" + item.id, typeof(RectTransform), typeof(Image), typeof(Button));
            rowObj.transform.SetParent(foodListContent, false);
            var rowRect = rowObj.GetComponent<RectTransform>();
            SetTopLeft(rowRect, new Vector2(6f, y), new Vector2(rowW, rowH));

            var rowBg = rowObj.GetComponent<Image>();
            rowBg.sprite = LumiWorldUITheme.SurfaceSprite;
            rowBg.type = Image.Type.Sliced;
            rowBg.color = LumiWorldUITheme.Slot;
            rowObj.AddComponent<ResourceUIInputBlocker>();
            LumiWorldUITheme.AddSoftElevation(rowObj, 3f, 0.14f);

            if (item.icon != null)
            {
                var icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icObj.transform.SetParent(rowRect, false);
                var icRect = icObj.GetComponent<RectTransform>();
                SetTopLeft(icRect, new Vector2(12f, -10f), new Vector2(48f, 48f));
                var icImg = icObj.GetComponent<Image>();
                icImg.sprite = item.icon;
                icImg.preserveAspect = true;
                icImg.raycastTarget = false;
            }

            var nameTxt = CreateTMP(rowRect, "Name", item.name, 16.5f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
            SetTopLeft(nameTxt.rectTransform, new Vector2(72f, -12f), new Vector2(224f, 44f));

            var stockTxt = CreateTMP(rowRect, "Stock", $"{item.stock} available", 14f, LumiWorldUITheme.Muted, true, TextAlignmentOptions.MidlineRight);
            SetTopLeft(stockTxt.rectTransform, new Vector2(rowW - 144f, -12f), new Vector2(128f, 44f));
            foodStockLabels[item.id] = stockTxt;

            var borderObj = new GameObject("SelectedBorder", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(rowRect, false);
            EconomyHUD.Stretch(borderObj.GetComponent<RectTransform>());
            var borderImg = borderObj.GetComponent<Image>();
            borderImg.sprite = GetSelectionBorderSprite();
            borderImg.type = Image.Type.Sliced;
            borderImg.color = Color.white;
            borderImg.raycastTarget = false;
            foodRowBorders[item.id] = borderImg;

            var btn = rowObj.GetComponent<Button>();
            btn.targetGraphic = rowBg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            string capturedId = item.id;
            btn.onClick.AddListener(() => OnFoodRowClicked(capturedId));

            curY += rowH + gapY;
        }

        foodListContent.sizeDelta = new Vector2(0f, Mathf.Max(404f, curY + 6f));
        UpdateFoodRowBorders();
    }

    private void OnFoodRowClicked(string foodId)
    {
        GameAudioManager.Instance?.PlayClick();
        selectedFoodId = foodId;
        UpdateFoodRowBorders();
    }

    private void OnAddFoodToSlotClicked(int slotIndex)
    {
        GameAudioManager.Instance?.PlayClick();

        string candidateId = selectedFoodId;
        if (string.IsNullOrEmpty(candidateId))
        {
            ShowFeedback("Select an unassigned food from Inventory on the left.", LumiWorldUITheme.Warning);
            return;
        }
        if (IsFoodAssignedInActiveBiome(candidateId))
        {
            ShowFeedback("This food already has a slot. Select another food.", LumiWorldUITheme.Warning);
            return;
        }

        AssignFoodToSlot(slotIndex, candidateId);
    }

    private bool IsFoodAssignedInActiveBiome(string foodId)
    {
        if (string.IsNullOrEmpty(foodId)) return false;
        var slots = GetDisplayedSlots();
        for (int i = 0; i < 3; i++)
        {
            if (string.Equals(slots.foodIds[i], foodId, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void AssignFoodToSlot(int slotIndex, string foodId)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        var slots = GetDisplayedSlots();

        for (int i = 0; i < 3; i++)
        {
            if (i != slotIndex && string.Equals(slots.foodIds[i], foodId, StringComparison.OrdinalIgnoreCase))
            {
                ShowFeedback("Food is already assigned to another slot in this biome.", LumiWorldUITheme.Warning);
                return;
            }
        }

        var food = currentFoodList.Find(f => string.Equals(f.id, foodId, StringComparison.OrdinalIgnoreCase));
        int stock = GetInventoryStock(foodId);
        if (stock <= 0)
        {
            ShowFeedback("Out of stock in Inventory.", LumiWorldUITheme.Error);
            return;
        }

        int defaultBudget = Mathf.Min(stock, 20);
        slots = EditActiveSlots();
        slots.foodIds[slotIndex] = foodId;
        slots.budgets[slotIndex] = defaultBudget;

        UpdateFoodRowBorders();
        RefreshSlotsAndBudgetUI();

        string bName = HabitatCreaturePanelStyle.GetAffinityDisplayName(activeBiome);
        string fName = food != null ? food.name : foodId;
        ShowFeedback($"Added {fName} (budget {defaultBudget}) to {bName}.", LumiWorldUITheme.Success);
    }

    private void ClearSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        GameAudioManager.Instance?.PlayClick();

        var slots = EditActiveSlots();
        slots.foodIds[slotIndex] = string.Empty;
        slots.budgets[slotIndex] = 0;

        UpdateFoodRowBorders();
        RefreshSlotsAndBudgetUI();
    }

    private void ChangeSlotAmount(int slotIndex, int delta)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        var slots = GetDisplayedSlots();
        string foodId = slots.foodIds[slotIndex];
        if (string.IsNullOrEmpty(foodId)) return;

        int maxBudget = Mathf.Max(slots.budgets[slotIndex], GetInventoryStock(foodId));
        int next = Mathf.Clamp(slots.budgets[slotIndex] + delta, 1, maxBudget);
        if (next != slots.budgets[slotIndex])
        {
            slots = EditActiveSlots();
            slots.budgets[slotIndex] = next;
            RefreshSlotsAndBudgetUI();
            GameAudioManager.Instance?.PlayClick();
        }
    }

    private static int GetInventoryStock(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return 0;
        var prod = ResourceProductionRuntime.Instance;
        return prod != null && prod.Inventory != null ? prod.Inventory.GetAmount(resourceId) : 0;
    }

    private void UpdateFoodRowBorders()
    {
        foreach (var kvp in foodRowBorders)
        {
            if (kvp.Value == null) continue;
            bool isSelected = string.Equals(kvp.Key, selectedFoodId, StringComparison.OrdinalIgnoreCase);
            kvp.Value.gameObject.SetActive(isSelected);
        }
    }

    private void RefreshSlotsAndBudgetUI()
    {
        var slots = GetDisplayedSlots();
        long totalRemainingBudget = 0;

        for (int i = 0; i < 3; i++)
        {
            var view = slotViews[i];
            if (view == null) continue;

            string foodId = slots.foodIds[i];
            int budget = slots.budgets[i];
            int currentStock = GetInventoryStock(foodId);

            if (string.IsNullOrEmpty(foodId) || budget <= 0)
            {
                view.emptyContainer.SetActive(true);
                view.occupiedContainer.SetActive(false);
            }
            else
            {
                view.emptyContainer.SetActive(false);
                view.occupiedContainer.SetActive(true);

                var item = currentFoodList.Find(f => string.Equals(f.id, foodId, StringComparison.OrdinalIgnoreCase));
                ResourceData resData = ResourceCatalog.Find(foodId);
                Sprite icon = item != null ? item.icon : (resData != null ? resData.icon : null);
                string dispName = item != null
                    ? item.name
                    : ResourceInventoryUIPresentation.LocalizeResourceName(foodId, resData != null ? resData.resourceName : foodId);

                view.iconImage.sprite = icon;
                view.iconImage.color = icon != null ? Color.white : Color.clear;
                view.nameText.text = dispName;
                view.subText.text = $"Shared budget · {currentStock} in stock";
                view.amountText.text = budget.ToString();
                view.decButton.interactable = budget > 1;
                view.incButton.interactable = budget < currentStock;
                view.amountButton.interactable = Mathf.Max(budget, currentStock) > 1;

                totalRemainingBudget += budget;
            }
        }

        if (remainingBudgetText != null)
        {
            string label = draftsByBiome.ContainsKey(activeBiome) ? "Pending budget" : "Remaining budget";
            remainingBudgetText.text = $"{label}: {totalRemainingBudget}";
        }
    }

    // =========================================================
    // APPLY FEEDING SETUP & ACTUAL DEPOSIT EXECUTION
    // =========================================================

    private void OnApplyFeedingSetupClicked()
    {
        GameAudioManager.Instance?.PlayClick();
        var slots = GetSlotsFor(activeBiome);
        if (draftsByBiome.TryGetValue(activeBiome, out var draft))
        {
            Array.Copy(draft.foodIds, slots.foodIds, slots.foodIds.Length);
            Array.Copy(draft.budgets, slots.budgets, slots.budgets.Length);
            draftsByBiome.Remove(activeBiome);
        }
        SaveBiomeSlots(activeBiome);

        int configuredCount = 0;
        for (int i = 0; i < 3; i++)
        {
            if (!string.IsNullOrEmpty(slots.foodIds[i]) && slots.budgets[i] > 0)
                configuredCount++;
        }

        string biomeName = HabitatCreaturePanelStyle.GetAffinityDisplayName(activeBiome);
        if (configuredCount == 0)
        {
            ShowFeedback($"Cleared automatic feeding setup for {biomeName}.", LumiWorldUITheme.Muted);
            RefreshFoodInventory();
            RefreshSlotsAndBudgetUI();
            return;
        }

        int depositedUnits = ExecuteBiomeFeeding(activeBiome, out int matchingCreatures, out string lastFailureReason);
        RefreshFoodInventory();
        RefreshSlotsAndBudgetUI();

        if (depositedUnits > 0)
        {
            ShowFeedback($"Fed {depositedUnits} food unit(s) to {biomeName} residents.", LumiWorldUITheme.Success);
        }
        else if (matchingCreatures == 0)
        {
            ShowFeedback($"Saved {biomeName} setup (no residents in {biomeName} yet).", LumiWorldUITheme.Muted);
        }
        else if (!string.IsNullOrEmpty(lastFailureReason))
        {
            ShowFeedback($"Saved {biomeName} setup · {lastFailureReason}", LumiWorldUITheme.Warning);
        }
        else
        {
            ShowFeedback($"Saved {biomeName} setup. Food is ready for residents when hungry.", LumiWorldUITheme.Success);
        }
    }

    private void RunBackgroundAutoFeedTick()
    {
        var production = ResourceProductionRuntime.Instance;
        if (production == null || production.Inventory == null) return;

        foreach (BiomeAffinity biome in SupportedBiomes)
        {
            // Preserve the edited budget until the player applies or closes the window.
            if (draftsByBiome.ContainsKey(biome) || (biome == activeBiome && modalEditingSlot >= 0)) continue;
            var slots = GetSlotsFor(biome);
            bool hasBudget = false;
            for (int i = 0; i < 3; i++)
            {
                if (!string.IsNullOrEmpty(slots.foodIds[i]) && slots.budgets[i] > 0)
                {
                    hasBudget = true;
                    break;
                }
            }
            if (!hasBudget) continue;

            ExecuteBiomeFeeding(biome, out _, out _);
        }

        if (IsOpen)
        {
            RefreshFoodInventory();
            RefreshSlotsAndBudgetUI();
        }
    }

    /// <summary>
    /// Feeds only creatures belonging to habitats of the specified biome type, using that biome's shared budget.
    /// Decrements the slot budget ONLY when DepositCreatureFood returns true.
    /// </summary>
    private int ExecuteBiomeFeeding(BiomeAffinity targetBiome, out int matchingCreaturesCount, out string lastFailureReason)
    {
        matchingCreaturesCount = 0;
        lastFailureReason = string.Empty;

        if (WorldLaunch.IsVisit) return 0;

        var production = ResourceProductionRuntime.Instance;
        if (production == null || production.Inventory == null) return 0;

        HabitatRuntimeManager habManager = production.HabitatManager;
        if (habManager == null) HabitatRuntimeManager.TryGetActive(out habManager);
        if (habManager == null || habManager.Habitats == null) return 0;

        var residentsList = new List<(string habitatId, string individualId)>();
        foreach (var h in habManager.Habitats)
        {
            if (h == null || h.affinity != targetBiome || h.residents == null) continue;
            foreach (var resident in h.residents)
            {
                if (resident == null || string.IsNullOrEmpty(resident.individualId)) continue;
                residentsList.Add((h.habitatId, resident.individualId));
            }
        }

        matchingCreaturesCount = residentsList.Count;
        if (matchingCreaturesCount == 0) return 0;

        var slots = GetSlotsFor(targetBiome);
        int totalDeposited = 0;

        int seedUnits = production.CareSettings != null
            ? Mathf.Max(1, production.CareSettings.seedFoodUnitsToFull)
            : 50;

        for (int s = 0; s < 3; s++)
        {
            string foodId = slots.foodIds[s];
            int remainingBudget = slots.budgets[s];
            if (string.IsNullOrEmpty(foodId) || remainingBudget <= 0) continue;

            int stock = production.Inventory.GetAmount(foodId);
            if (stock <= 0)
            {
                lastFailureReason = "Out of food in Inventory.";
                continue;
            }

            int distributedThisSlot = 0;

            for (int r = 0; r < residentsList.Count; r++)
            {
                if (remainingBudget - distributedThisSlot <= 0 || stock - distributedThisSlot <= 0) break;

                var (habId, indivId) = residentsList[r];
                var care = production.GetCareState(indivId);

                // Calculate how much food this creature currently needs based on hunger
                int neededUnits = 0;
                if (care != null)
                {
                    float satietyDeficit = Mathf.Clamp(100f - (float)care.Satiety, 0f, 100f);
                    neededUnits = Mathf.CeilToInt(satietyDeficit / (100f / seedUnits));
                }

                long currentPantry = production.GetCreatureFoodAmount(indivId, foodId);
                int hungerDeficit = Mathf.Max(0, neededUnits - (int)currentPantry);

                // If creature is not hungry and has enough food, do not take food yet!
                if (hungerDeficit <= 0) continue;

                int availableForThis = Mathf.Min(hungerDeficit, Mathf.Min(remainingBudget - distributedThisSlot, stock - distributedThisSlot));
                if (availableForThis <= 0) continue;

                if (production.DepositCreatureFood(habId, indivId, foodId, availableForThis, out string reason))
                {
                    distributedThisSlot += availableForThis;
                    totalDeposited += availableForThis;
                }
                else if (!string.IsNullOrEmpty(reason))
                {
                    lastFailureReason = reason;
                }
            }

            slots.budgets[s] = Mathf.Max(0, remainingBudget - distributedThisSlot);
            if (slots.budgets[s] == 0) slots.foodIds[s] = string.Empty;
        }

        if (totalDeposited > 0)
        {
            SaveBiomeSlots(targetBiome);
        }

        return totalDeposited;
    }

    // =========================================================
    // QUANTITY ADJUSTMENT MODAL
    // =========================================================

    private void OpenQuantityModal(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        var slots = GetDisplayedSlots();
        string foodId = slots.foodIds[slotIndex];
        if (string.IsNullOrEmpty(foodId)) return;

        var food = currentFoodList.Find(f => string.Equals(f.id, foodId, StringComparison.OrdinalIgnoreCase));
        int stock = GetInventoryStock(foodId);
        int maxBudget = Mathf.Max(slots.budgets[slotIndex], stock);

        modalEditingSlot = slotIndex;
        modalCurrentAmount = slots.budgets[slotIndex];

        if (quantityModalObj == null)
        {
            BuildQuantityModal();
        }

        if (modalFoodIcon != null)
        {
            modalFoodIcon.sprite = food != null ? food.icon : null;
            modalFoodIcon.gameObject.SetActive(modalFoodIcon.sprite != null);
        }
        if (modalFoodNameText != null)
        {
            modalFoodNameText.text = food != null ? food.name : foodId;
        }
        if (modalStorageText != null)
        {
            modalStorageText.text = $"Available in Inventory: {stock:N0}";
        }
        if (modalSlider != null)
        {
            modalSlider.minValue = 1;
            modalSlider.maxValue = maxBudget;
            modalSlider.SetValueWithoutNotify(modalCurrentAmount);
        }

        UpdateModalAmountDisplay();
        quantityModalObj.SetActive(true);
        quantityModalObj.transform.SetAsLastSibling();
        GameAudioManager.Instance?.PlayClick();
    }

    private void BuildQuantityModal()
    {
        if (panel == null) return;

        quantityModalObj = new GameObject("QuantityModalOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
        quantityModalObj.transform.SetParent(panel, false);
        EconomyHUD.Stretch(quantityModalObj.GetComponent<RectTransform>());

        var qImg = quantityModalObj.GetComponent<Image>();
        qImg.color = LumiWorldUITheme.Backdrop;
        quantityModalObj.AddComponent<ResourceUIInputBlocker>();
        quantityModalObj.GetComponent<Button>().onClick.AddListener(CloseQuantityModal);

        var cardObj = new GameObject("Card", typeof(RectTransform), typeof(Image));
        cardObj.transform.SetParent(quantityModalObj.transform, false);
        var cRect = cardObj.GetComponent<RectTransform>();
        cRect.anchorMin = cRect.anchorMax = cRect.pivot = new Vector2(0.5f, 0.5f);
        cRect.sizeDelta = new Vector2(360f, 256f);

        var cImg = cardObj.GetComponent<Image>();
        cImg.sprite = LumiWorldUITheme.SurfaceSprite;
        cImg.type = Image.Type.Sliced;
        cImg.color = LumiWorldUITheme.Surface;
        cardObj.AddComponent<ResourceUIInputBlocker>();
        LumiWorldUITheme.AddSoftElevation(cardObj, 5f, 0.24f);

        var tTxt = CreateTMP(cRect, "Title", "Adjust food budget", 17f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        SetTopLeft(tTxt.rectTransform, new Vector2(20f, -14f), new Vector2(320f, 26f));

        CreateStepperSquareButton(cRect, "CloseBtn", new Vector2(360f - 38f, -12f), new Vector2(28f, 28f), "Close", CloseQuantityModal);

        var iconObj = new GameObject("FoodIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(cRect, false);
        SetTopLeft(iconObj.GetComponent<RectTransform>(), new Vector2(24f, -48f), new Vector2(38f, 38f));
        modalFoodIcon = iconObj.GetComponent<Image>();
        modalFoodIcon.preserveAspect = true;

        modalFoodNameText = CreateTMP(cRect, "FoodName", "", 15f, LumiWorldUITheme.Text, true, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(modalFoodNameText.rectTransform, new Vector2(72f, -48f), new Vector2(260f, 20f));

        modalStorageText = CreateTMP(cRect, "StorageInfo", "", 12.5f, LumiWorldUITheme.Muted, false, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(modalStorageText.rectTransform, new Vector2(72f, -68f), new Vector2(260f, 18f));

        modalAmountValueText = CreateTMP(cRect, "AmountValue", "1", 24f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        SetTopLeft(modalAmountValueText.rectTransform, new Vector2(80f, -94f), new Vector2(200f, 30f));

        // Slider
        var sliderObj = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObj.transform.SetParent(cRect, false);
        SetTopLeft(sliderObj.GetComponent<RectTransform>(), new Vector2(30f, -132f), new Vector2(300f, 20f));

        modalSlider = sliderObj.GetComponent<Slider>();
        modalSlider.wholeNumbers = true;

        var trackObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        trackObj.transform.SetParent(sliderObj.transform, false);
        var trRect = trackObj.GetComponent<RectTransform>();
        trRect.anchorMin = new Vector2(0f, 0.30f);
        trRect.anchorMax = new Vector2(1f, 0.70f);
        trRect.offsetMin = Vector2.zero;
        trRect.offsetMax = Vector2.zero;
        var trImg = trackObj.GetComponent<Image>();
        trImg.sprite = LumiWorldUITheme.PillSprite;
        trImg.type = Image.Type.Sliced;
        trImg.color = LumiWorldUITheme.Inset;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        var faRect = fillArea.GetComponent<RectTransform>();
        faRect.anchorMin = new Vector2(0f, 0.30f);
        faRect.anchorMax = new Vector2(1f, 0.70f);
        faRect.sizeDelta = new Vector2(-12f, 0f);

        var fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(fillArea.transform, false);
        var fRect = fillObj.GetComponent<RectTransform>();
        EconomyHUD.Stretch(fRect);
        var fImg = fillObj.GetComponent<Image>();
        fImg.sprite = LumiWorldUITheme.PillSprite;
        fImg.type = Image.Type.Sliced;
        fImg.color = LumiWorldUITheme.Primary;
        modalSlider.fillRect = fRect;

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObj.transform, false);
        var haRect = handleArea.GetComponent<RectTransform>();
        EconomyHUD.Stretch(haRect);
        haRect.sizeDelta = new Vector2(-16f, 0f);

        var handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObj.transform.SetParent(handleArea.transform, false);
        var hRect = handleObj.GetComponent<RectTransform>();
        hRect.sizeDelta = new Vector2(20f, 20f);
        var hImg = handleObj.GetComponent<Image>();
        hImg.sprite = LumiWorldUITheme.PillSprite;
        hImg.type = Image.Type.Sliced;
        hImg.color = LumiWorldUITheme.Primary;
        modalSlider.handleRect = hRect;
        modalSlider.targetGraphic = hImg;

        modalSlider.onValueChanged.AddListener((v) =>
        {
            modalCurrentAmount = Mathf.RoundToInt(v);
            UpdateModalAmountDisplay();
        });

        // Cancel & Apply buttons
        GameObject cancelObj = new GameObject("CancelBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelObj.transform.SetParent(cRect, false);
        RectTransform cbRect = cancelObj.GetComponent<RectTransform>();
        cbRect.anchorMin = cbRect.anchorMax = cbRect.pivot = new Vector2(0f, 0f);
        cbRect.anchoredPosition = new Vector2(30f, 18f);
        cbRect.sizeDelta = new Vector2(142f, 38f);
        Image cbImg = cancelObj.GetComponent<Image>();
        cbImg.sprite = LumiWorldUITheme.ButtonSecondarySprite;
        cbImg.type = Image.Type.Sliced;
        cbImg.color = LumiWorldUITheme.Inset;
        var cbTxt = CreateTMP(cbRect, "Label", "Cancel", 14f, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(cbTxt.rectTransform);
        cancelObj.GetComponent<Button>().onClick.AddListener(CloseQuantityModal);

        GameObject applyObj = new GameObject("ApplyBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        applyObj.transform.SetParent(cRect, false);
        RectTransform abRect = applyObj.GetComponent<RectTransform>();
        abRect.anchorMin = abRect.anchorMax = abRect.pivot = new Vector2(1f, 0f);
        abRect.anchoredPosition = new Vector2(-30f, 18f);
        abRect.sizeDelta = new Vector2(142f, 38f);
        Image abImg = applyObj.GetComponent<Image>();
        abImg.sprite = LumiWorldUITheme.ButtonPrimarySprite;
        abImg.type = Image.Type.Sliced;
        abImg.color = LumiWorldUITheme.Primary;
        var abTxt = CreateTMP(abRect, "Label", "Apply", 14f, Color.white, true, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(abTxt.rectTransform);
        applyObj.GetComponent<Button>().onClick.AddListener(OnApplyQuantityModal);
    }

    private void UpdateModalAmountDisplay()
    {
        if (modalAmountValueText != null)
        {
            modalAmountValueText.text = $"{modalCurrentAmount:N0}";
        }
    }

    private void CloseQuantityModal()
    {
        modalEditingSlot = -1;
        if (quantityModalObj != null)
        {
            quantityModalObj.SetActive(false);
        }
        GameAudioManager.Instance?.PlayClick();
    }

    private void OnApplyQuantityModal()
    {
        if (modalEditingSlot >= 0 && modalEditingSlot < 3)
        {
            var slots = GetDisplayedSlots();
            if (!string.IsNullOrEmpty(slots.foodIds[modalEditingSlot]) &&
                slots.budgets[modalEditingSlot] != modalCurrentAmount)
            {
                slots = EditActiveSlots();
                slots.budgets[modalEditingSlot] = modalCurrentAmount;
                RefreshSlotsAndBudgetUI();
            }
        }
        CloseQuantityModal();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void ShowFeedback(string msg, Color col)
    {
        if (feedbackNotifyText == null) return;
        feedbackNotifyText.text = msg;
        feedbackNotifyText.color = col;

        if (feedbackFadeRoutine != null) StopCoroutine(feedbackFadeRoutine);
        feedbackFadeRoutine = StartCoroutine(FadeFeedbackRoutine());
    }

    private IEnumerator FadeFeedbackRoutine()
    {
        yield return new WaitForSeconds(3.5f);
        if (feedbackNotifyText != null)
        {
            feedbackNotifyText.text = "";
        }
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, Color color, bool bold, TextAlignmentOptions align)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.font = LumiWorldUITheme.Body;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.color = color;
        tmp.alignment = align;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    private static Sprite GetSelectionBorderSprite()
    {
        return LumiWorldUITheme.GetBorderedSurfaceSprite("SelOutline_R20_S2.5", Color.clear, LumiWorldUITheme.Primary, 20, 2.5f);
    }

    private static Sprite GetTabSelectionBorderSprite(int height)
    {
        int r = Mathf.Max(6, height / 2);
        return LumiWorldUITheme.GetBorderedSurfaceSprite("TabSelBorder_H" + height, Color.clear, Color.black, r, 2.5f);
    }
}

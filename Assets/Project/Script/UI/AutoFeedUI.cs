using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// P-08 Automatic Feeding UI — Per-Cluster Granary Post model:
/// - Each Granary Post cluster on the active island has its own independent 3 food slots and budget,
///   saved per user/world/clusterKey (and migrated automatically when adjacent Granary Post tiles merge).
/// - Number of unlocked slots (1 -> 2 -> 2 -> 3 -> 3) and aura radius (1 -> 1 -> 2 -> 2 -> 3)
///   scale with that Granary Post's Level (1..5).
/// - Feeds only formed Biomes that have at least one hex inside that Granary Post's aura.
/// </summary>
public class AutoFeedUI : MonoBehaviour
{
    private static AutoFeedUI instance;
    public static AutoFeedUI Instance => instance;

    /// <summary>
    /// Fired whenever any Granary Post cluster's food slots or budgets are saved/consumed,
    /// keeping the global Auto-Feed modal and per-Building inspector synchronized.
    /// </summary>
    public static event Action<string> OnClusterSlotsUpdated;

    public const float WindowWidth = 1040f;
    public const float WindowHeight = 620f;

    public bool IsOpen => window != null && window.activeSelf;

    private GameObject window;
    private RectTransform panel;
    private RectTransform biomeTabsRow;
    private RectTransform biomeTabsContent;
    private TextMeshProUGUI clusterStatusPillText;
    private TextMeshProUGUI clusterCoverageText;
    private Button locateClusterBtn;
    private RectTransform foodListContent;
    private TextMeshProUGUI emptyFoodNoticeText;
    private TextMeshProUGUI remainingBudgetText;
    private TextMeshProUGUI feedbackNotifyText;
    private Coroutine feedbackFadeRoutine;
    private Button applySetupButton;

    // 3 Slot views for the currently selected Granary Post cluster
    private readonly DispenserSlotView[] slotViews = new DispenserSlotView[3];

    private class DispenserSlotView
    {
        public int index;
        public RectTransform root;
        public Image bg;
        public Shadow shadow;
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
        public GameObject lockedContainer;
        public TextMeshProUGUI lockedText;
    }

    private class ClusterTabView
    {
        public string clusterKey;
        public Button button;
        public Image bg;
        public Shadow shadow;
        public Image border;
        public Image emblem;
        public TextMeshProUGUI label;
        public TextMeshProUGUI subLabel;
    }

    private readonly List<ClusterTabView> clusterTabs = new List<ClusterTabView>();

    // Per-cluster 3-slot config in memory (synced with PlayerPrefs per world + clusterKey)
    private class ClusterFeedSlots
    {
        public readonly string[] foodIds = new string[3];
        public readonly int[] budgets = new int[3];
    }

    private readonly Dictionary<string, ClusterFeedSlots> slotsByCluster = new Dictionary<string, ClusterFeedSlots>(StringComparer.Ordinal);
    private readonly Dictionary<string, ClusterFeedSlots> draftsByCluster = new Dictionary<string, ClusterFeedSlots>(StringComparer.Ordinal);
    private string activeClusterKey = string.Empty;
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

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        SettlementClusterManager.OnBuildingClusterMerged += HandleClusterMerged;
        SettlementClusterManager.OnBuildingClustersChanged += HandleClustersChanged;
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
        SettlementClusterManager.OnBuildingClusterMerged -= HandleClusterMerged;
        SettlementClusterManager.OnBuildingClustersChanged -= HandleClustersChanged;
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

        // Background auto-feeding tick every 3 seconds for Granary Post clusters with remaining budget
        if (Application.isPlaying && !WorldLaunch.IsVisit && Time.unscaledTime >= nextAutoTickTime)
        {
            nextAutoTickTime = Time.unscaledTime + 3.0f;
            if (LumiWorld.Acs.TimeSystem.GameTimeRuntime.Instance != null &&
                LumiWorld.Acs.TimeSystem.GameTimeRuntime.Instance.IsPaused)
            {
                return;
            }
            EnsureWorldConfigLoaded();
            RunBackgroundAutoFeedTick();
        }
    }

    private float ComputePanelScale()
    {
        return LumiWorldUITheme.ComputeResponsiveModalScale(this, WindowWidth, WindowHeight, marginX: 40f, marginY: 88f);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        OpenForCluster(activeClusterKey);
    }

    public void OpenForCluster(string clusterKey)
    {
        EnsureWorldConfigLoaded();
        if (!string.IsNullOrEmpty(clusterKey))
        {
            activeClusterKey = clusterKey;
        }
        if (window == null) BuildWindow();
        if (panel != null)
        {
            float scale = ComputePanelScale();
            panel.localScale = new Vector3(scale, scale, 1f);
        }

        window.SetActive(true);
        window.transform.SetAsLastSibling();
        EconomyHUD.BringWindowToFront(window);

        RefreshClusterTabs();
        RefreshFoodInventory();
        RefreshSlotsAndBudgetUI();
        GameAudioManager.Instance?.PlayClick();
    }

    public void Close()
    {
        draftsByCluster.Clear();
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

    public int GetClusterConfiguredSlotCount(string clusterKey)
    {
        if (string.IsNullOrEmpty(clusterKey)) return 0;
        var cluster = FindClusterByKey(clusterKey);
        int unlocked = cluster != null ? Mathf.Clamp(cluster.UnlockedFoodSlots, 0, 3) : 3;
        var slots = GetSlotsFor(clusterKey);
        int count = 0;
        for (int i = 0; i < unlocked; i++)
        {
            if (!string.IsNullOrEmpty(slots.foodIds[i]) && slots.budgets[i] > 0) count++;
        }
        return count;
    }

    public bool TryGetClusterSlot(string clusterKey, int slotIndex, out string foodId, out int budget)
    {
        foodId = string.Empty;
        budget = 0;
        if (string.IsNullOrEmpty(clusterKey) || slotIndex < 0 || slotIndex >= 3) return false;
        EnsureWorldConfigLoaded();
        var slots = GetSlotsFor(clusterKey);
        foodId = slots.foodIds[slotIndex] ?? string.Empty;
        budget = slots.budgets[slotIndex];
        return !string.IsNullOrEmpty(foodId) && budget > 0;
    }

    public bool SetClusterSlot(string clusterKey, int slotIndex, string foodId, int budget, out string feedbackMessage)
    {
        feedbackMessage = string.Empty;
        if (WorldLaunch.IsVisit)
        {
            feedbackMessage = "Cannot edit on visiting island.";
            return false;
        }
        if (string.IsNullOrEmpty(clusterKey) || slotIndex < 0 || slotIndex >= 3)
        {
            feedbackMessage = "Invalid Granary Post slot.";
            return false;
        }

        EnsureWorldConfigLoaded();
        var cluster = FindClusterByKey(clusterKey);
        if (cluster == null)
        {
            feedbackMessage = "Granary Post not found.";
            return false;
        }
        if (slotIndex >= cluster.UnlockedFoodSlots)
        {
            feedbackMessage = $"Unlocks at Lv. {BuildingRules.GetRequiredLevelForFoodSlot(slotIndex)}.";
            return false;
        }
        if (string.IsNullOrEmpty(foodId))
        {
            return ClearClusterSlot(clusterKey, slotIndex);
        }

        var slots = GetSlotsFor(clusterKey);
        for (int i = 0; i < 3; i++)
        {
            if (i != slotIndex && string.Equals(slots.foodIds[i], foodId, StringComparison.OrdinalIgnoreCase))
            {
                feedbackMessage = "Already assigned in another slot.";
                return false;
            }
        }

        int stock = GetInventoryStock(foodId);
        if (stock <= 0 && !string.Equals(slots.foodIds[slotIndex], foodId, StringComparison.OrdinalIgnoreCase))
        {
            feedbackMessage = "Out of stock in Inventory.";
            return false;
        }

        int maxCap = Mathf.Max(1, Mathf.Max(stock, string.Equals(slots.foodIds[slotIndex], foodId, StringComparison.OrdinalIgnoreCase) ? slots.budgets[slotIndex] : 0));
        int clampedBudget = Mathf.Clamp(budget > 0 ? budget : Mathf.Min(stock, 20), 1, maxCap);

        slots.foodIds[slotIndex] = foodId;
        slots.budgets[slotIndex] = clampedBudget;

        if (draftsByCluster.TryGetValue(clusterKey, out var draft))
        {
            draft.foodIds[slotIndex] = foodId;
            draft.budgets[slotIndex] = clampedBudget;
        }

        SaveClusterSlots(clusterKey);
        SyncGlobalUIIfVisible(clusterKey);
        OnClusterSlotsUpdated?.Invoke(clusterKey);
        return true;
    }

    public bool ClearClusterSlot(string clusterKey, int slotIndex)
    {
        if (WorldLaunch.IsVisit || string.IsNullOrEmpty(clusterKey) || slotIndex < 0 || slotIndex >= 3) return false;
        EnsureWorldConfigLoaded();

        var slots = GetSlotsFor(clusterKey);
        bool changed = !string.IsNullOrEmpty(slots.foodIds[slotIndex]) || slots.budgets[slotIndex] != 0;
        slots.foodIds[slotIndex] = string.Empty;
        slots.budgets[slotIndex] = 0;

        if (draftsByCluster.TryGetValue(clusterKey, out var draft))
        {
            draft.foodIds[slotIndex] = string.Empty;
            draft.budgets[slotIndex] = 0;
        }

        if (changed)
        {
            SaveClusterSlots(clusterKey);
            SyncGlobalUIIfVisible(clusterKey);
            OnClusterSlotsUpdated?.Invoke(clusterKey);
        }
        return changed;
    }

    public int TriggerClusterFeedingNow(string clusterKey, out int matchingCreatures, out string lastFailureReason)
    {
        matchingCreatures = 0;
        lastFailureReason = string.Empty;
        if (WorldLaunch.IsVisit || string.IsNullOrEmpty(clusterKey)) return 0;

        EnsureWorldConfigLoaded();
        var cluster = FindClusterByKey(clusterKey);
        if (cluster == null)
        {
            lastFailureReason = "Granary Post not found.";
            return 0;
        }

        int deposited = ExecuteClusterFeeding(cluster, out matchingCreatures, out lastFailureReason);
        SyncGlobalUIIfVisible(clusterKey);
        OnClusterSlotsUpdated?.Invoke(clusterKey);
        return deposited;
    }

    private void SyncGlobalUIIfVisible(string clusterKey)
    {
        if (IsOpen)
        {
            RefreshFoodInventory();
            if (string.Equals(activeClusterKey, clusterKey, StringComparison.Ordinal))
            {
                UpdateFoodRowBorders();
            }
            RefreshClusterTabs();
            RefreshSlotsAndBudgetUI();
        }
    }

    // =========================================================
    // PER-WORLD, PER-GRANARY-CLUSTER PERSISTENCE
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
        if (string.Equals(loadedWorldScopeKey, currentKey, StringComparison.Ordinal))
        {
            return;
        }

        loadedWorldScopeKey = currentKey;
        slotsByCluster.Clear();
        draftsByCluster.Clear();
        modalEditingSlot = -1;
        if (quantityModalObj != null) quantityModalObj.SetActive(false);

        activeClusterKey = PlayerPrefs.GetString($"LumiWorld_AutoFeed_v3_{loadedWorldScopeKey}_ActiveCluster", string.Empty);
    }

    private void HandleClustersChanged()
    {
        if (!IsOpen) return;
        RefreshClusterTabs();
        RefreshSlotsAndBudgetUI();
    }

    private void HandleClusterMerged(IReadOnlyList<string> oldKeys, string newKey)
    {
        if (oldKeys == null || oldKeys.Count == 0 || string.IsNullOrEmpty(newKey)) return;
        EnsureWorldConfigLoaded();

        var merged = new ClusterFeedSlots();
        var mergedFoods = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void CollectFrom(ClusterFeedSlots source)
        {
            if (source == null) return;
            for (int i = 0; i < 3; i++)
            {
                string fid = source.foodIds[i];
                int bud = source.budgets[i];
                if (string.IsNullOrEmpty(fid) || bud <= 0) continue;
                if (mergedFoods.TryGetValue(fid, out int existing)) mergedFoods[fid] = existing + bud;
                else mergedFoods[fid] = bud;
            }
        }

        // Preserve any already-saved slots on newKey first, then fold oldKeys into it
        if (slotsByCluster.TryGetValue(newKey, out var existingNew) || TryLoadClusterSlots(newKey, out existingNew))
        {
            CollectFrom(existingNew);
        }

        bool anyOldDiffers = false;
        for (int k = 0; k < oldKeys.Count; k++)
        {
            string oldKey = oldKeys[k];
            if (string.IsNullOrEmpty(oldKey) || string.Equals(oldKey, newKey, StringComparison.Ordinal)) continue;
            anyOldDiffers = true;

            var oldData = GetSlotsFor(oldKey);
            CollectFrom(oldData);

            slotsByCluster.Remove(oldKey);
            draftsByCluster.Remove(oldKey);
            ClearPersistedClusterSlots(oldKey);

            if (string.Equals(activeClusterKey, oldKey, StringComparison.Ordinal))
            {
                activeClusterKey = newKey;
            }
        }

        if (!anyOldDiffers) return;

        int slotIdx = 0;
        foreach (var kv in mergedFoods)
        {
            if (slotIdx >= 3) break;
            merged.foodIds[slotIdx] = kv.Key;
            merged.budgets[slotIdx] = kv.Value;
            slotIdx++;
        }

        slotsByCluster[newKey] = merged;
        SaveClusterSlots(newKey);
        OnClusterSlotsUpdated?.Invoke(newKey);
    }

    private bool TryLoadClusterSlots(string clusterKey, out ClusterFeedSlots data)
    {
        data = new ClusterFeedSlots();
        if (string.IsNullOrEmpty(clusterKey)) return false;

        bool hasAny = false;
        for (int i = 0; i < 3; i++)
        {
            string keyPrefix = $"LumiWorld_AutoFeed_v3_{loadedWorldScopeKey}_{clusterKey}_{i}";
            data.foodIds[i] = PlayerPrefs.GetString(keyPrefix + "_Id", string.Empty);
            data.budgets[i] = Mathf.Max(0, PlayerPrefs.GetInt(keyPrefix + "_Budget", 0));
            if (string.IsNullOrEmpty(data.foodIds[i]) || data.budgets[i] <= 0)
            {
                data.foodIds[i] = string.Empty;
                data.budgets[i] = 0;
            }
            else
            {
                hasAny = true;
            }
        }
        return hasAny;
    }

    private ClusterFeedSlots GetSlotsFor(string clusterKey)
    {
        EnsureWorldConfigLoaded();
        if (string.IsNullOrEmpty(clusterKey)) return new ClusterFeedSlots();
        if (!slotsByCluster.TryGetValue(clusterKey, out var data) || data == null)
        {
            TryLoadClusterSlots(clusterKey, out data);
            slotsByCluster[clusterKey] = data;
        }
        return data;
    }

    private ClusterFeedSlots GetDisplayedSlots()
    {
        if (string.IsNullOrEmpty(activeClusterKey)) return new ClusterFeedSlots();
        var saved = GetSlotsFor(activeClusterKey);
        return draftsByCluster.TryGetValue(activeClusterKey, out var draft) ? draft : saved;
    }

    private ClusterFeedSlots EditActiveSlots()
    {
        if (string.IsNullOrEmpty(activeClusterKey)) return new ClusterFeedSlots();
        var saved = GetSlotsFor(activeClusterKey);
        if (!draftsByCluster.TryGetValue(activeClusterKey, out var draft))
        {
            draft = new ClusterFeedSlots();
            Array.Copy(saved.foodIds, draft.foodIds, saved.foodIds.Length);
            Array.Copy(saved.budgets, draft.budgets, saved.budgets.Length);
            draftsByCluster.Add(activeClusterKey, draft);
        }
        return draft;
    }

    private void SaveClusterSlots(string clusterKey)
    {
        if (WorldLaunch.IsVisit || string.IsNullOrEmpty(clusterKey)) return;
        EnsureWorldConfigLoaded();
        var data = GetSlotsFor(clusterKey);
        for (int i = 0; i < 3; i++)
        {
            string keyPrefix = $"LumiWorld_AutoFeed_v3_{loadedWorldScopeKey}_{clusterKey}_{i}";
            PlayerPrefs.SetString(keyPrefix + "_Id", data.foodIds[i] ?? string.Empty);
            PlayerPrefs.SetInt(keyPrefix + "_Budget", Mathf.Max(0, data.budgets[i]));
        }
        if (!string.IsNullOrEmpty(activeClusterKey))
        {
            PlayerPrefs.SetString($"LumiWorld_AutoFeed_v3_{loadedWorldScopeKey}_ActiveCluster", activeClusterKey);
        }
        PlayerPrefs.Save();
    }

    private void ClearPersistedClusterSlots(string clusterKey)
    {
        if (string.IsNullOrEmpty(clusterKey)) return;
        for (int i = 0; i < 3; i++)
        {
            string keyPrefix = $"LumiWorld_AutoFeed_v3_{loadedWorldScopeKey}_{clusterKey}_{i}";
            PlayerPrefs.DeleteKey(keyPrefix + "_Id");
            PlayerPrefs.DeleteKey(keyPrefix + "_Budget");
        }
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
        var titleTxt = LumiWorldUITheme.CreateLabel(panel, "Title", "Automatic Feeding", 25f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
        titleTxt.overflowMode = TextOverflowModes.Overflow;
        SetTopLeft(titleTxt.rectTransform, new Vector2(28f, -15f), new Vector2(540f, 42f));

        // Close [×] button (matches P-00 / P-07 circle close button)
        Button cBtn = LumiWorldUITheme.CreateCloseButton(panel, 28f, 16f, Close, 40f);
        LumiWorldUITheme.AddSoftElevation(cBtn.gameObject, 2.5f, 0.14f);

        // Header Divider (matches P-00 / P-07)
        var divObj = new GameObject("HeaderDivider", typeof(RectTransform), typeof(Image));
        divObj.transform.SetParent(panel, false);
        SetTopLeft(divObj.GetComponent<RectTransform>(), new Vector2(28f, -70f), new Vector2(WindowWidth - 56f, 2f));
        var divImg = divObj.GetComponent<Image>();
        divImg.color = LumiWorldUITheme.Inset;
        divImg.raycastTarget = false;
    }

    private void BuildBiomeSelectorRow()
    {
        GameObject rowObj = new GameObject("ClusterSelectorRow", typeof(RectTransform));
        rowObj.transform.SetParent(panel, false);
        biomeTabsRow = rowObj.GetComponent<RectTransform>();
        SetTopLeft(biomeTabsRow, new Vector2(28f, -82f), new Vector2(WindowWidth - 56f, 52f));

        GameObject scrollObj = new GameObject("TabsScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(biomeTabsRow, false);
        EconomyHUD.Stretch(scrollObj.GetComponent<RectTransform>());

        ScrollRect scroll = scrollObj.GetComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        GameObject vpObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        vpObj.transform.SetParent(scrollObj.transform, false);
        RectTransform vpRect = vpObj.GetComponent<RectTransform>();
        EconomyHUD.Stretch(vpRect);
        vpObj.GetComponent<Image>().color = Color.clear;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpRect, false);
        biomeTabsContent = contentObj.GetComponent<RectTransform>();
        biomeTabsContent.anchorMin = new Vector2(0f, 0f);
        biomeTabsContent.anchorMax = new Vector2(0f, 1f);
        biomeTabsContent.pivot = new Vector2(0f, 0.5f);
        biomeTabsContent.anchoredPosition = Vector2.zero;
        biomeTabsContent.sizeDelta = new Vector2(WindowWidth - 56f, 0f);

        scroll.viewport = vpRect;
        scroll.content = biomeTabsContent;

        RefreshClusterTabs();
    }

    private static List<SettlementClusterManager.BuildingClusterInfo> GetGranaryClusters()
    {
        return SettlementClusterManager.Instance != null
            ? SettlementClusterManager.Instance.GetGranaryPostClusters()
            : new List<SettlementClusterManager.BuildingClusterInfo>();
    }

    private static SettlementClusterManager.BuildingClusterInfo FindClusterByKey(string clusterKey)
    {
        if (string.IsNullOrEmpty(clusterKey)) return null;
        var clusters = GetGranaryClusters();
        for (int i = 0; i < clusters.Count; i++)
        {
            if (string.Equals(clusters[i].ClusterKey, clusterKey, StringComparison.Ordinal))
                return clusters[i];
        }
        return null;
    }

    private static List<string> CollectCoveredBiomeNames(SettlementClusterManager.BuildingClusterInfo cluster)
    {
        var names = new List<string>();
        if (cluster == null || cluster.Hexes == null || cluster.Hexes.Count == 0) return names;
        if (!HabitatRuntimeManager.TryGetActive(out var hm) || hm.Habitats == null) return names;

        HashSet<HexCoordinates> aura = BiomeBuffCalculator.CollectAuraHexesAroundCluster(cluster.Hexes, cluster.AuraRadius);
        if (aura == null || aura.Count == 0) return names;

        foreach (var hab in hm.Habitats)
        {
            if (hab == null || !hab.IsFormedBiome || hab.members == null) continue;
            bool touches = false;
            for (int m = 0; m < hab.members.Count; m++)
            {
                if (hab.members[m] != null && aura.Contains(hab.members[m].hex))
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

    private void RefreshClusterTabs()
    {
        if (biomeTabsRow == null || biomeTabsContent == null) return;

        for (int i = biomeTabsContent.childCount - 1; i >= 0; i--)
        {
            Destroy(biomeTabsContent.GetChild(i).gameObject);
        }
        clusterTabs.Clear();

        List<SettlementClusterManager.BuildingClusterInfo> clusters = GetGranaryClusters();
        if (clusters.Count == 0)
        {
            activeClusterKey = string.Empty;
            biomeTabsContent.sizeDelta = new Vector2(WindowWidth - 56f, 0f);
            TextMeshProUGUI emptyNotice = LumiWorldUITheme.CreateLabel(
                biomeTabsContent,
                "NoGranaryNotice",
                "Place a Granary Post on a watered tile to configure Auto-Feed for nearby biomes.",
                14f,
                LumiWorldUITheme.Muted,
                headingFont: true,
                align: TextAlignmentOptions.MidlineLeft);
            EconomyHUD.Stretch(emptyNotice.rectTransform);
            return;
        }

        if (string.IsNullOrEmpty(activeClusterKey) || !clusters.Exists(c => string.Equals(c.ClusterKey, activeClusterKey, StringComparison.Ordinal)))
        {
            activeClusterKey = clusters[0].ClusterKey;
        }

        float curX = 2f;
        const float tabH = 48f;
        const float tabW = 248f;
        const float gapX = 12f;

        for (int i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            int configured = GetClusterConfiguredSlotCount(cluster.ClusterKey);
            List<string> coveredBiomes = CollectCoveredBiomeNames(cluster);
            string biomeSummary = coveredBiomes.Count == 0
                ? "No biomes"
                : (coveredBiomes.Count == 1 ? coveredBiomes[0] : $"{coveredBiomes[0]} +{coveredBiomes.Count - 1}");
            string tabLabel = $"Granary #{i + 1} · Lv.{cluster.Level}";
            string subLabel = $"{biomeSummary} · {configured}/{cluster.UnlockedFoodSlots} slots";

            GameObject tabObj = new GameObject("GranaryTab_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            tabObj.transform.SetParent(biomeTabsContent, false);
            RectTransform tRect = tabObj.GetComponent<RectTransform>();
            SetTopLeft(tRect, new Vector2(curX, -2f), new Vector2(tabW, tabH));

            Image bg = tabObj.GetComponent<Image>();
            bg.sprite = LumiWorldUITheme.SurfaceSprite;
            bg.type = Image.Type.Sliced;
            bg.color = LumiWorldUITheme.Slot;
            LumiWorldUITheme.AddSoftElevation(tabObj, 2f, 0.12f);
            Shadow tabShadow = tabObj.GetComponent<Shadow>();

            GameObject emWell = new GameObject("EmblemWell", typeof(RectTransform), typeof(Image));
            emWell.transform.SetParent(tRect, false);
            SetTopLeft(emWell.GetComponent<RectTransform>(), new Vector2(10f, -8f), new Vector2(32f, 32f));
            var ewBg = emWell.GetComponent<Image>();
            ewBg.sprite = LumiWorldUITheme.SurfaceSprite;
            ewBg.type = Image.Type.Sliced;
            ewBg.color = LumiWorldUITheme.Window;
            ewBg.raycastTarget = false;

            GameObject emObj = new GameObject("Emblem", typeof(RectTransform), typeof(Image));
            emObj.transform.SetParent(emWell.transform, false);
            RectTransform emRect = emObj.GetComponent<RectTransform>();
            emRect.anchorMin = emRect.anchorMax = emRect.pivot = new Vector2(0.5f, 0.5f);
            emRect.sizeDelta = new Vector2(18f, 18f);
            Image emImg = emObj.GetComponent<Image>();
            emImg.sprite = LumiWorldUITheme.Glyph("G-17") ?? LumiWorldUITheme.Glyph("G-06");
            emImg.preserveAspect = true;
            emImg.color = LumiWorldUITheme.Text;
            emImg.raycastTarget = false;

            TextMeshProUGUI lbl = LumiWorldUITheme.CreateLabel(tRect, "Label", tabLabel, 14f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
            SetTopLeft(lbl.rectTransform, new Vector2(50f, -5f), new Vector2(tabW - 60f, 20f));

            TextMeshProUGUI sub = LumiWorldUITheme.CreateLabel(tRect, "SubLabel", subLabel, 11.5f, LumiWorldUITheme.Muted, headingFont: false, align: TextAlignmentOptions.MidlineLeft);
            sub.fontStyle = FontStyles.Normal;
            SetTopLeft(sub.rectTransform, new Vector2(50f, -25f), new Vector2(tabW - 60f, 18f));

            // Radius 20 matches LumiWorldUITheme.SurfaceSprite corners pixel-for-pixel
            Image borderImg = LumiWorldUITheme.CreateSelectionBorder(tRect, radius: 20, thickness: 2.5f);

            Button btn = tabObj.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            string capturedKey = cluster.ClusterKey;
            btn.onClick.AddListener(() => SelectClusterTab(capturedKey));

            clusterTabs.Add(new ClusterTabView
            {
                clusterKey = cluster.ClusterKey,
                button = btn,
                bg = bg,
                shadow = tabShadow,
                border = borderImg,
                emblem = emImg,
                label = lbl,
                subLabel = sub
            });

            curX += tabW + gapX;
        }

        biomeTabsContent.sizeDelta = new Vector2(Mathf.Max(WindowWidth - 56f, curX - gapX + 4f), 0f);
        UpdateClusterTabVisuals();
    }

    private void SelectClusterTab(string clusterKey)
    {
        if (string.Equals(activeClusterKey, clusterKey, StringComparison.Ordinal)) return;
        activeClusterKey = clusterKey;
        UpdateClusterTabVisuals();
        UpdateFoodRowBorders();
        RefreshSlotsAndBudgetUI();
        GameAudioManager.Instance?.PlayClick();
    }

    private void UpdateClusterTabVisuals()
    {
        foreach (var tab in clusterTabs)
        {
            bool active = string.Equals(tab.clusterKey, activeClusterKey, StringComparison.Ordinal);
            tab.bg.color = active ? LumiWorldUITheme.Slot : LumiWorldUITheme.Inset;
            if (tab.shadow != null) tab.shadow.enabled = active;
            if (tab.border != null) tab.border.gameObject.SetActive(active);
            if (tab.label != null) tab.label.color = active ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted;
            if (tab.subLabel != null) tab.subLabel.color = active ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted;
            if (tab.emblem != null) tab.emblem.color = active ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted;
        }
    }

    private void BuildLeftFoodListSection()
    {
        GameObject leftBox = new GameObject("LeftFoodListWell", typeof(RectTransform), typeof(Image));
        leftBox.transform.SetParent(panel, false);
        RectTransform lRect = leftBox.GetComponent<RectTransform>();
        SetTopLeft(lRect, new Vector2(28f, -144f), new Vector2(468f, 404f));

        Image wellBg = leftBox.GetComponent<Image>();
        wellBg.sprite = LumiWorldUITheme.SurfaceSprite;
        wellBg.type = Image.Type.Sliced;
        wellBg.color = LumiWorldUITheme.Well;
        wellBg.raycastTarget = false;

        var leftHdr = LumiWorldUITheme.CreateLabel(lRect, "LeftHeader", "Available foods", 17.5f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
        SetTopLeft(leftHdr.rectTransform, new Vector2(18f, -14f), new Vector2(240f, 26f));

        var leftHint = LumiWorldUITheme.CreateLabel(lRect, "LeftHint", "Select, then tap Assign", 12f, LumiWorldUITheme.Muted, headingFont: false, align: TextAlignmentOptions.MidlineRight);
        leftHint.fontStyle = FontStyles.Normal;
        SetTopLeft(leftHint.rectTransform, new Vector2(258f, -17f), new Vector2(192f, 20f));

        GameObject scrollObj = new GameObject("FoodScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(lRect, false);
        RectTransform sRect = scrollObj.GetComponent<RectTransform>();
        sRect.anchorMin = Vector2.zero;
        sRect.anchorMax = Vector2.one;
        sRect.offsetMin = new Vector2(10f, 10f);
        sRect.offsetMax = new Vector2(-10f, -48f);

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
        foodListContent.sizeDelta = new Vector2(0f, 346f);

        scroll.viewport = vRect;
        scroll.content = foodListContent;

        emptyFoodNoticeText = LumiWorldUITheme.CreateLabel(
            scrollObj.transform,
            "EmptyNotice",
            "No configured creature food in Inventory.",
            15f,
            LumiWorldUITheme.Muted,
            headingFont: true,
            align: TextAlignmentOptions.Center,
            wrap: true);
        LumiWorldUITheme.Stretch(emptyFoodNoticeText.rectTransform, 16f);
        emptyFoodNoticeText.gameObject.SetActive(false);
    }

    private void BuildRightAssignedSection()
    {
        GameObject rightCard = new GameObject("RightAssignedCard", typeof(RectTransform), typeof(Image));
        rightCard.transform.SetParent(panel, false);
        RectTransform rRect = rightCard.GetComponent<RectTransform>();
        SetTopLeft(rRect, new Vector2(512f, -144f), new Vector2(500f, 404f));

        Image rBg = rightCard.GetComponent<Image>();
        rBg.sprite = LumiWorldUITheme.SurfaceSprite;
        rBg.type = Image.Type.Sliced;
        rBg.color = LumiWorldUITheme.Surface;
        rBg.raycastTarget = true;
        rightCard.AddComponent<ResourceUIInputBlocker>();
        LumiWorldUITheme.AddSoftElevation(rightCard, 4f, 0.16f);

        // Header: "Assigned foods" + coverage summary + [Locate] button
        var hdrTxt = LumiWorldUITheme.CreateLabel(rRect, "AssignedHeader", "Assigned foods", 18f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
        SetTopLeft(hdrTxt.rectTransform, new Vector2(20f, -12f), new Vector2(240f, 26f));

        clusterCoverageText = LumiWorldUITheme.CreateLabel(rRect, "CoverageSub", "Select a Granary Post", 12.5f, LumiWorldUITheme.Muted, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
        SetTopLeft(clusterCoverageText.rectTransform, new Vector2(20f, -39f), new Vector2(344f, 20f));

        locateClusterBtn = LumiWorldUITheme.CreatePillButton(
            rRect,
            "LocateBtn",
            "Locate",
            "G-10",
            primary: false,
            fontSize: 13f,
            onClick: OnLocateActiveClusterClicked,
            out _,
            out _,
            out _,
            radius: 17);
        locateClusterBtn.gameObject.AddComponent<DialogButtonJuice>();
        LumiWorldUITheme.PlaceTopRight(locateClusterBtn.GetComponent<RectTransform>(), 20f, 16f, 106f, 34f);

        // 3 Vertical Food Slot Rows (balanced 20px side margins, 22px bottom margin)
        const float slotW = 460f;
        const float slotH = 94f;
        const float startY = -74f;
        const float gapY = 12f;

        for (int i = 0; i < 3; i++)
        {
            int slotIndex = i;
            float y = startY - i * (slotH + gapY);

            GameObject slotObj = new GameObject("AssignedSlot_" + i, typeof(RectTransform), typeof(Image));
            slotObj.transform.SetParent(rRect, false);
            RectTransform sRect = slotObj.GetComponent<RectTransform>();
            SetTopLeft(sRect, new Vector2(20f, y), new Vector2(slotW, slotH));

            Image sBg = slotObj.GetComponent<Image>();
            sBg.sprite = LumiWorldUITheme.SurfaceSprite;
            sBg.type = Image.Type.Sliced;
            sBg.color = LumiWorldUITheme.Slot;
            LumiWorldUITheme.AddSoftElevation(slotObj, 2.5f, 0.14f);
            Shadow sShadow = slotObj.GetComponent<Shadow>();

            var view = new DispenserSlotView { index = slotIndex, root = sRect, bg = sBg, shadow = sShadow };

            // --- A. EMPTY CONTAINER (mirrors Habitat/Building Info slot layout + right primary "Assign" capsule) ---
            GameObject emptyCont = new GameObject("EmptyContainer", typeof(RectTransform), typeof(Button));
            emptyCont.transform.SetParent(sRect, false);
            EconomyHUD.Stretch(emptyCont.GetComponent<RectTransform>());
            view.emptyContainer = emptyCont;
            Button rowAssignBtn = emptyCont.GetComponent<Button>();
            rowAssignBtn.transition = Selectable.Transition.None;
            rowAssignBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            rowAssignBtn.onClick.AddListener(() => OnAddFoodToSlotClicked(slotIndex));

            GameObject emIcBox = new GameObject("EmptyIconBox", typeof(RectTransform), typeof(Image));
            emIcBox.transform.SetParent(emptyCont.transform, false);
            SetTopLeft(emIcBox.GetComponent<RectTransform>(), new Vector2(16f, -23f), new Vector2(48f, 48f));
            var emIcBoxBg = emIcBox.GetComponent<Image>();
            emIcBoxBg.sprite = LumiWorldUITheme.SurfaceSprite;
            emIcBoxBg.type = Image.Type.Sliced;
            emIcBoxBg.color = LumiWorldUITheme.Slot;
            emIcBoxBg.raycastTarget = false;

            GameObject plusIcObj = new GameObject("PlusIcon", typeof(RectTransform), typeof(Image));
            plusIcObj.transform.SetParent(emIcBox.transform, false);
            RectTransform plusRect = plusIcObj.GetComponent<RectTransform>();
            plusRect.anchorMin = plusRect.anchorMax = plusRect.pivot = new Vector2(0.5f, 0.5f);
            plusRect.sizeDelta = new Vector2(20f, 20f);
            Image plusImg = plusIcObj.GetComponent<Image>();
            plusImg.sprite = LumiWorldUITheme.Glyph("Plus");
            plusImg.preserveAspect = true;
            plusImg.color = LumiWorldUITheme.Muted;
            plusImg.raycastTarget = false;

            var emTitle = LumiWorldUITheme.CreateLabel(emptyCont.transform, "Title", "Unassigned slot", 15f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
            SetTopLeft(emTitle.rectTransform, new Vector2(76f, -23f), new Vector2(246f, 24f));

            var emSub = LumiWorldUITheme.CreateLabel(emptyCont.transform, "Sub", "Select food on left, then assign", 12f, LumiWorldUITheme.Muted, headingFont: false, align: TextAlignmentOptions.MidlineLeft);
            emSub.fontStyle = FontStyles.Normal;
            SetTopLeft(emSub.rectTransform, new Vector2(76f, -49f), new Vector2(246f, 20f));

            view.addFoodButton = LumiWorldUITheme.CreatePillButton(
                emptyCont.transform,
                "AddFoodBtn",
                "Assign",
                null,
                primary: true,
                fontSize: 13f,
                onClick: () => OnAddFoodToSlotClicked(slotIndex),
                out _,
                out _,
                out view.addFoodLabel,
                radius: 17);
            RectTransform abRect = view.addFoodButton.GetComponent<RectTransform>();
            abRect.anchorMin = abRect.anchorMax = abRect.pivot = new Vector2(1f, 0.5f);
            abRect.anchoredPosition = new Vector2(-16f, 0f);
            abRect.sizeDelta = new Vector2(96f, 34f);

            // --- B. OCCUPIED CONTAINER (single clean horizontal axis: Icon | Name/Stock | [-] [Amount] [+] | [×]) ---
            GameObject occCont = new GameObject("OccupiedContainer", typeof(RectTransform));
            occCont.transform.SetParent(sRect, false);
            EconomyHUD.Stretch(occCont.GetComponent<RectTransform>());
            view.occupiedContainer = occCont;

            // Icon Box (56x56)
            GameObject icBox = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
            icBox.transform.SetParent(occCont.transform, false);
            RectTransform ibRect = icBox.GetComponent<RectTransform>();
            SetTopLeft(ibRect, new Vector2(14f, -19f), new Vector2(56f, 56f));
            Image ibBg = icBox.GetComponent<Image>();
            ibBg.sprite = LumiWorldUITheme.SurfaceSprite;
            ibBg.type = Image.Type.Sliced;
            ibBg.color = LumiWorldUITheme.Window;
            ibBg.raycastTarget = false;

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
            view.nameText = LumiWorldUITheme.CreateLabel(occCont.transform, "FoodName", "Raspberries", 16f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
            SetTopLeft(view.nameText.rectTransform, new Vector2(80f, -22f), new Vector2(162f, 24f));

            view.subText = LumiWorldUITheme.CreateLabel(occCont.transform, "SubText", "Stock: 27", 12.5f, LumiWorldUITheme.Success, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
            SetTopLeft(view.subText.rectTransform, new Vector2(80f, -48f), new Vector2(162f, 20f));

            // Stepper Controls on Right (all vertically centered on y = -29 / -31, zero overlap!)
            view.decButton = LumiWorldUITheme.CreateIconSquareButton(occCont.transform, "DecBtn", "Minus", 36f, 36f, () => ChangeSlotAmount(slotIndex, -1), surfaceBg: true);
            SetTopLeft(view.decButton.GetComponent<RectTransform>(), new Vector2(252f, -29f), new Vector2(36f, 36f));

            GameObject amtBtnObj = new GameObject("AmountBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            amtBtnObj.transform.SetParent(occCont.transform, false);
            RectTransform amRect = amtBtnObj.GetComponent<RectTransform>();
            SetTopLeft(amRect, new Vector2(294f, -29f), new Vector2(58f, 36f));
            Image amBg = amtBtnObj.GetComponent<Image>();
            amBg.sprite = LumiWorldUITheme.GetBorderedSurfaceSprite("AutoFeedAmt_R10", Color.white, LumiWorldUITheme.Edge, 10, 1.2f);
            amBg.type = Image.Type.Sliced;
            amBg.color = LumiWorldUITheme.Surface;
            view.amountText = LumiWorldUITheme.CreateLabel(amRect, "AmountText", "10", 15.5f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.Center);
            EconomyHUD.Stretch(view.amountText.rectTransform);
            view.amountButton = amtBtnObj.GetComponent<Button>();
            view.amountButton.targetGraphic = amBg;
            view.amountButton.navigation = new Navigation { mode = Navigation.Mode.None };
            view.amountButton.onClick.AddListener(() => OpenQuantityModal(slotIndex));

            view.incButton = LumiWorldUITheme.CreateIconSquareButton(occCont.transform, "IncBtn", "Plus", 36f, 36f, () => ChangeSlotAmount(slotIndex, 1), surfaceBg: true);
            SetTopLeft(view.incButton.GetComponent<RectTransform>(), new Vector2(358f, -29f), new Vector2(36f, 36f));

            // Clean circle Remove [×] button aligned inline at right
            view.removeButton = LumiWorldUITheme.CreateCloseButton(occCont.transform, 16f, 31f, () => ClearSlot(slotIndex), 32f);

            // --- C. LOCKED CONTAINER (mirrors Habitat/Building Info locked slot) ---
            GameObject lockedCont = new GameObject("LockedContainer", typeof(RectTransform));
            lockedCont.transform.SetParent(sRect, false);
            EconomyHUD.Stretch(lockedCont.GetComponent<RectTransform>());
            view.lockedContainer = lockedCont;

            GameObject lockBox = new GameObject("LockIconBox", typeof(RectTransform), typeof(Image));
            lockBox.transform.SetParent(lockedCont.transform, false);
            SetTopLeft(lockBox.GetComponent<RectTransform>(), new Vector2(16f, -23f), new Vector2(48f, 48f));
            var lbBg = lockBox.GetComponent<Image>();
            lbBg.sprite = LumiWorldUITheme.SurfaceSprite;
            lbBg.type = Image.Type.Sliced;
            lbBg.color = LumiWorldUITheme.Slot;
            lbBg.raycastTarget = false;

            GameObject lockIcObj = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            lockIcObj.transform.SetParent(lockBox.transform, false);
            RectTransform lIcRect = lockIcObj.GetComponent<RectTransform>();
            lIcRect.anchorMin = lIcRect.anchorMax = lIcRect.pivot = new Vector2(0.5f, 0.5f);
            lIcRect.sizeDelta = new Vector2(20f, 20f);
            Image lIcImg = lockIcObj.GetComponent<Image>();
            lIcImg.sprite = LumiWorldUITheme.Glyph("Lock");
            lIcImg.preserveAspect = true;
            lIcImg.color = LumiWorldUITheme.Muted;
            lIcImg.raycastTarget = false;

            var lockTitle = LumiWorldUITheme.CreateLabel(lockedCont.transform, "LockedTitle", "Locked slot", 15f, LumiWorldUITheme.Muted, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
            SetTopLeft(lockTitle.rectTransform, new Vector2(76f, -23f), new Vector2(340f, 24f));

            view.lockedText = LumiWorldUITheme.CreateLabel(lockedCont.transform, "LockedLabel", "Unlocks at Lv. 2", 12.5f, LumiWorldUITheme.Muted, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
            SetTopLeft(view.lockedText.rectTransform, new Vector2(76f, -49f), new Vector2(340f, 20f));

            slotViews[i] = view;
        }
    }

    private void BuildFooterBar()
    {
        // Left: [G-18] Remaining budget: N
        GameObject budgetRow = new GameObject("RemainingBudgetRow", typeof(RectTransform));
        budgetRow.transform.SetParent(panel, false);
        RectTransform brRect = budgetRow.GetComponent<RectTransform>();
        brRect.anchorMin = brRect.anchorMax = brRect.pivot = new Vector2(0f, 0f);
        brRect.anchoredPosition = new Vector2(28f, 18f);
        brRect.sizeDelta = new Vector2(320f, 38f);

        GameObject icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icObj.transform.SetParent(brRect, false);
        RectTransform icRect = icObj.GetComponent<RectTransform>();
        icRect.anchorMin = icRect.anchorMax = new Vector2(0f, 0.5f);
        icRect.pivot = new Vector2(0f, 0.5f);
        icRect.anchoredPosition = new Vector2(0f, 0f);
        icRect.sizeDelta = new Vector2(20f, 20f);
        Image icImg = icObj.GetComponent<Image>();
        icImg.sprite = LumiWorldUITheme.Glyph("G-18");
        icImg.preserveAspect = true;
        icImg.color = LumiWorldUITheme.Muted;
        icImg.raycastTarget = false;

        remainingBudgetText = LumiWorldUITheme.CreateLabel(brRect, "BudgetText", "Remaining budget: 0", 15.5f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
        remainingBudgetText.rectTransform.anchorMin = Vector2.zero;
        remainingBudgetText.rectTransform.anchorMax = Vector2.one;
        remainingBudgetText.rectTransform.offsetMin = new Vector2(28f, 0f);
        remainingBudgetText.rectTransform.offsetMax = Vector2.zero;

        // Center Feedback Notify Text
        feedbackNotifyText = LumiWorldUITheme.CreateLabel(panel, "FeedbackNotify", "", 13.5f, LumiWorldUITheme.Success, headingFont: true, align: TextAlignmentOptions.Center);
        feedbackNotifyText.rectTransform.anchorMin = feedbackNotifyText.rectTransform.anchorMax = feedbackNotifyText.rectTransform.pivot = new Vector2(0.5f, 0f);
        feedbackNotifyText.rectTransform.anchoredPosition = new Vector2(-20f, 23f);
        feedbackNotifyText.rectTransform.sizeDelta = new Vector2(360f, 28f);

        // Right Primary CTA: "Apply feeding setup" (clean capsule matching P-05 CollectAllBtn)
        applySetupButton = LumiWorldUITheme.CreatePillButton(
            panel,
            "ApplySetupBtn",
            "Apply feeding setup",
            null,
            primary: true,
            fontSize: 15f,
            onClick: OnApplyFeedingSetupClicked,
            out _,
            out _,
            out _,
            radius: 23);
        applySetupButton.gameObject.AddComponent<DialogButtonJuice>();
        RectTransform apRect = applySetupButton.GetComponent<RectTransform>();
        apRect.anchorMin = apRect.anchorMax = apRect.pivot = new Vector2(1f, 0f);
        apRect.anchoredPosition = new Vector2(-28f, 14f);
        apRect.sizeDelta = new Vector2(224f, 46f);
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
            foreach (var food in currentFoodList) foodStockLabels[food.id].text = $"Stock: {food.stock} available";
            return;
        }

        ResourceUIGraphics.Clear(foodListContent);
        foodRowBorders.Clear();
        foodStockLabels.Clear();

        if (currentFoodList.Count == 0)
        {
            selectedFoodId = string.Empty;
            foodListContent.sizeDelta = new Vector2(0f, 346f);
            return;
        }

        if (string.IsNullOrEmpty(selectedFoodId) || !currentFoodList.Exists(f => string.Equals(f.id, selectedFoodId, StringComparison.OrdinalIgnoreCase)))
        {
            selectedFoodId = currentFoodList[0].id;
        }

        const float rowW = 436f;
        const float rowH = 72f;
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
            rowBg.color = LumiWorldUITheme.Surface;
            rowObj.AddComponent<ResourceUIInputBlocker>();
            LumiWorldUITheme.AddSoftElevation(rowObj, 2.5f, 0.14f);

            var icWell = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
            icWell.transform.SetParent(rowRect, false);
            SetTopLeft(icWell.GetComponent<RectTransform>(), new Vector2(12f, -11f), new Vector2(50f, 50f));
            var iwBg = icWell.GetComponent<Image>();
            iwBg.sprite = LumiWorldUITheme.SurfaceSprite;
            iwBg.type = Image.Type.Sliced;
            iwBg.color = LumiWorldUITheme.Slot;
            iwBg.raycastTarget = false;

            if (item.icon != null)
            {
                var icObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icObj.transform.SetParent(icWell.transform, false);
                LumiWorldUITheme.Stretch(icObj.GetComponent<RectTransform>(), 5f);
                var icImg = icObj.GetComponent<Image>();
                icImg.sprite = item.icon;
                icImg.preserveAspect = true;
                icImg.raycastTarget = false;
            }

            var nameTxt = LumiWorldUITheme.CreateLabel(rowRect, "Name", item.name, 16f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
            SetTopLeft(nameTxt.rectTransform, new Vector2(74f, -13f), new Vector2(226f, 24f));

            var stockTxt = LumiWorldUITheme.CreateLabel(rowRect, "Stock", $"Stock: {item.stock} available", 13f, LumiWorldUITheme.Muted, headingFont: false, align: TextAlignmentOptions.MidlineLeft);
            stockTxt.fontStyle = FontStyles.Normal;
            SetTopLeft(stockTxt.rectTransform, new Vector2(74f, -38f), new Vector2(226f, 20f));
            foodStockLabels[item.id] = stockTxt;

            var statusObj = new GameObject("ReadyTag", typeof(RectTransform));
            statusObj.transform.SetParent(rowRect, false);
            RectTransform stRt = statusObj.GetComponent<RectTransform>();
            stRt.anchorMin = stRt.anchorMax = new Vector2(1f, 0.5f);
            stRt.pivot = new Vector2(1f, 0.5f);
            stRt.anchoredPosition = new Vector2(-14f, 0f);
            stRt.sizeDelta = new Vector2(106f, 28f);

            var stIcObj = new GameObject("Check", typeof(RectTransform), typeof(Image));
            stIcObj.transform.SetParent(stRt, false);
            SetTopLeft(stIcObj.GetComponent<RectTransform>(), new Vector2(4f, -5f), new Vector2(18f, 18f));
            var stIc = stIcObj.GetComponent<Image>();
            stIc.sprite = LumiWorldUITheme.StatusReadySprite;
            stIc.preserveAspect = true;
            stIc.raycastTarget = false;

            var stLbl = LumiWorldUITheme.CreateLabel(stRt, "Text", "Available", 13.5f, LumiWorldUITheme.Success, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
            SetTopLeft(stLbl.rectTransform, new Vector2(26f, 0f), new Vector2(78f, 28f));

            Image borderImg = LumiWorldUITheme.CreateSelectionBorder(rowRect, radius: 20, thickness: 2.5f);
            foodRowBorders[item.id] = borderImg;

            var btn = rowObj.GetComponent<Button>();
            btn.targetGraphic = rowBg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            string capturedId = item.id;
            btn.onClick.AddListener(() => OnFoodRowClicked(capturedId));

            curY += rowH + gapY;
        }

        foodListContent.sizeDelta = new Vector2(0f, Mathf.Max(346f, curY + 6f));
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

        var cluster = FindClusterByKey(activeClusterKey);
        if (cluster == null)
        {
            ShowFeedback("Place a Granary Post first.", LumiWorldUITheme.Warning);
            return;
        }
        if (slotIndex >= cluster.UnlockedFoodSlots)
        {
            ShowFeedback($"Slot unlocks at Granary Post Lv. {BuildingRules.GetRequiredLevelForFoodSlot(slotIndex)}.", LumiWorldUITheme.Warning);
            return;
        }

        string candidateId = selectedFoodId;
        if (string.IsNullOrEmpty(candidateId))
        {
            ShowFeedback("Select an unassigned food from Inventory on the left.", LumiWorldUITheme.Warning);
            return;
        }
        if (IsFoodAssignedInActiveCluster(candidateId))
        {
            ShowFeedback("This food already has a slot. Select another food.", LumiWorldUITheme.Warning);
            return;
        }

        AssignFoodToSlot(slotIndex, candidateId);
    }

    private bool IsFoodAssignedInActiveCluster(string foodId)
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
        var cluster = FindClusterByKey(activeClusterKey);
        if (cluster == null || slotIndex >= cluster.UnlockedFoodSlots) return;

        var slots = GetDisplayedSlots();

        for (int i = 0; i < 3; i++)
        {
            if (i != slotIndex && string.Equals(slots.foodIds[i], foodId, StringComparison.OrdinalIgnoreCase))
            {
                ShowFeedback("Food is already assigned to another slot in this Granary Post.", LumiWorldUITheme.Warning);
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

        string fName = food != null ? food.name : foodId;
        ShowFeedback($"Added {fName} (budget {defaultBudget}) to Granary Post Lv.{cluster.Level}.", LumiWorldUITheme.Success);
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

    private void OnLocateActiveClusterClicked()
    {
        var cluster = FindClusterByKey(activeClusterKey);
        if (cluster == null || cluster.Hexes == null || cluster.Hexes.Count == 0) return;

        HexCoordinates targetHex = cluster.Hexes[0];
        Vector3 worldCenter = Vector3.zero;
        for (int i = 0; i < cluster.Hexes.Count; i++)
        {
            worldCenter += HexMetrics.HexToWorldPosition(cluster.Hexes[i], 0f);
        }
        worldCenter /= cluster.Hexes.Count;

        Close();
        HexCameraController.Instance?.FocusOnTarget(worldCenter, targetDistance: 6.2f, duration: 0.75f);
        MapStructureInfoUI.Instance?.OpenForBuilding(targetHex);
    }

    private void RefreshSlotsAndBudgetUI()
    {
        var cluster = FindClusterByKey(activeClusterKey);
        int unlockedSlots = cluster != null ? Mathf.Clamp(cluster.UnlockedFoodSlots, 0, 3) : 0;

        if (clusterCoverageText != null)
        {
            if (cluster == null)
            {
                clusterCoverageText.text = "No Granary Post placed";
                clusterCoverageText.color = LumiWorldUITheme.Muted;
            }
            else
            {
                var covered = CollectCoveredBiomeNames(cluster);
                string biomeText = covered.Count > 0 ? $"Feeds: {string.Join(", ", covered)}" : "No biomes in range";
                clusterCoverageText.text = $"Lv.{cluster.Level} · {cluster.Hexes.Count} {(cluster.Hexes.Count == 1 ? "tile" : "tiles")} · {cluster.AuraRadius}-hex · {biomeText}";
                clusterCoverageText.color = covered.Count > 0 ? LumiWorldUITheme.Success : LumiWorldUITheme.Muted;
            }
        }
        if (locateClusterBtn != null)
        {
            locateClusterBtn.interactable = cluster != null && cluster.Hexes != null && cluster.Hexes.Count > 0;
        }

        if (clusterStatusPillText != null)
        {
            clusterStatusPillText.text = cluster != null
                ? $"Lv.{cluster.Level} · {unlockedSlots}/3 slots · {cluster.AuraRadius} hex"
                : "No Granary Post";
        }

        if (applySetupButton != null)
        {
            applySetupButton.interactable = cluster != null;
        }

        var slots = GetDisplayedSlots();
        long totalRemainingBudget = 0;

        for (int i = 0; i < 3; i++)
        {
            var view = slotViews[i];
            if (view == null) continue;

            if (cluster == null || i >= unlockedSlots)
            {
                if (view.bg != null) view.bg.color = LumiWorldUITheme.Inset;
                if (view.shadow != null) view.shadow.enabled = false;
                view.emptyContainer.SetActive(false);
                view.occupiedContainer.SetActive(false);
                if (view.lockedContainer != null) view.lockedContainer.SetActive(true);
                if (view.lockedText != null)
                {
                    view.lockedText.text = cluster == null
                        ? "Requires Granary Post"
                        : $"Unlocks at Lv. {BuildingRules.GetRequiredLevelForFoodSlot(i)}";
                }
                continue;
            }

            if (view.lockedContainer != null) view.lockedContainer.SetActive(false);

            string foodId = slots.foodIds[i];
            int budget = slots.budgets[i];
            int currentStock = GetInventoryStock(foodId);

            if (string.IsNullOrEmpty(foodId) || budget <= 0)
            {
                if (view.bg != null) view.bg.color = LumiWorldUITheme.Well;
                if (view.shadow != null) view.shadow.enabled = false;
                view.emptyContainer.SetActive(true);
                view.occupiedContainer.SetActive(false);
            }
            else
            {
                if (view.bg != null) view.bg.color = LumiWorldUITheme.Slot;
                if (view.shadow != null) view.shadow.enabled = true;
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
                view.subText.text = currentStock > 0 ? $"Stock: {currentStock} in stock" : "Out of stock";
                view.subText.color = currentStock > 0 ? LumiWorldUITheme.Success : LumiWorldUITheme.Warning;
                view.amountText.text = budget.ToString();
                view.decButton.interactable = budget > 1;
                view.incButton.interactable = budget < currentStock;
                view.amountButton.interactable = Mathf.Max(budget, currentStock) > 1;

                totalRemainingBudget += budget;
            }
        }

        if (remainingBudgetText != null)
        {
            string label = !string.IsNullOrEmpty(activeClusterKey) && draftsByCluster.ContainsKey(activeClusterKey)
                ? "Pending budget"
                : "Remaining budget";
            remainingBudgetText.text = $"{label}: {totalRemainingBudget}";
        }
    }

    // =========================================================
    // APPLY FEEDING SETUP & CLUSTER-SCOPED DEPOSIT EXECUTION
    // =========================================================

    private void OnApplyFeedingSetupClicked()
    {
        GameAudioManager.Instance?.PlayClick();
        var cluster = FindClusterByKey(activeClusterKey);
        if (cluster == null)
        {
            ShowFeedback("Place a Granary Post first.", LumiWorldUITheme.Warning);
            return;
        }

        var slots = GetSlotsFor(activeClusterKey);
        if (draftsByCluster.TryGetValue(activeClusterKey, out var draft))
        {
            Array.Copy(draft.foodIds, slots.foodIds, slots.foodIds.Length);
            Array.Copy(draft.budgets, slots.budgets, slots.budgets.Length);
            draftsByCluster.Remove(activeClusterKey);
        }
        SaveClusterSlots(activeClusterKey);

        int unlockedSlots = Mathf.Clamp(cluster.UnlockedFoodSlots, 0, 3);
        int configuredCount = 0;
        for (int i = 0; i < unlockedSlots; i++)
        {
            if (!string.IsNullOrEmpty(slots.foodIds[i]) && slots.budgets[i] > 0)
                configuredCount++;
        }

        if (configuredCount == 0)
        {
            ShowFeedback("Cleared automatic feeding setup for this Granary Post.", LumiWorldUITheme.Muted);
            RefreshFoodInventory();
            RefreshSlotsAndBudgetUI();
            OnClusterSlotsUpdated?.Invoke(activeClusterKey);
            return;
        }

        int depositedUnits = ExecuteClusterFeeding(cluster, out int matchingCreatures, out string lastFailureReason);
        RefreshFoodInventory();
        RefreshSlotsAndBudgetUI();
        OnClusterSlotsUpdated?.Invoke(activeClusterKey);

        if (depositedUnits > 0)
        {
            ShowFeedback($"Fed {depositedUnits} food unit(s) to nearby biome residents.", LumiWorldUITheme.Success);
        }
        else if (matchingCreatures == 0)
        {
            ShowFeedback($"Saved setup (no biome residents within {cluster.AuraRadius}-hex range yet).", LumiWorldUITheme.Muted);
        }
        else if (!string.IsNullOrEmpty(lastFailureReason))
        {
            ShowFeedback($"Saved setup · {lastFailureReason}", LumiWorldUITheme.Warning);
        }
        else
        {
            ShowFeedback("Saved setup. Food is ready for nearby residents when hungry.", LumiWorldUITheme.Success);
        }
    }

    private void RunBackgroundAutoFeedTick()
    {
        var production = ResourceProductionRuntime.Instance;
        if (production == null || production.Inventory == null) return;

        var clusters = GetGranaryClusters();
        for (int c = 0; c < clusters.Count; c++)
        {
            var cluster = clusters[c];
            if (cluster == null || string.IsNullOrEmpty(cluster.ClusterKey)) continue;
            if (draftsByCluster.ContainsKey(cluster.ClusterKey) ||
                (string.Equals(cluster.ClusterKey, activeClusterKey, StringComparison.Ordinal) && modalEditingSlot >= 0))
            {
                continue;
            }

            var slots = GetSlotsFor(cluster.ClusterKey);
            int unlockedSlots = Mathf.Clamp(cluster.UnlockedFoodSlots, 0, 3);
            bool hasBudget = false;
            for (int i = 0; i < unlockedSlots; i++)
            {
                if (!string.IsNullOrEmpty(slots.foodIds[i]) && slots.budgets[i] > 0)
                {
                    hasBudget = true;
                    break;
                }
            }
            if (!hasBudget) continue;

            if (ExecuteClusterFeeding(cluster, out _, out _) > 0)
            {
                OnClusterSlotsUpdated?.Invoke(cluster.ClusterKey);
            }
        }

        if (IsOpen)
        {
            RefreshFoodInventory();
            RefreshSlotsAndBudgetUI();
        }
    }

    /// <summary>
    /// Feeds only creatures belonging to formed Biomes that have at least 1 hex inside this Granary Post's aura.
    /// Uses only unlocked slots (1..3 based on Level) and decrements budget ONLY when DepositCreatureFood returns true.
    /// </summary>
    private int ExecuteClusterFeeding(SettlementClusterManager.BuildingClusterInfo cluster, out int matchingCreaturesCount, out string lastFailureReason)
    {
        matchingCreaturesCount = 0;
        lastFailureReason = string.Empty;

        if (WorldLaunch.IsVisit || cluster == null || cluster.Hexes.Count == 0) return 0;

        var production = ResourceProductionRuntime.Instance;
        if (production == null || production.Inventory == null) return 0;

        HabitatRuntimeManager habManager = production.HabitatManager;
        if (habManager == null) HabitatRuntimeManager.TryGetActive(out habManager);
        if (habManager == null || habManager.Habitats == null) return 0;

        HashSet<HexCoordinates> auraHexes = BiomeBuffCalculator.CollectAuraHexesAroundCluster(cluster.Hexes, cluster.AuraRadius);
        if (auraHexes.Count == 0) return 0;

        var residentsList = new List<(string habitatId, string individualId)>();
        foreach (var h in habManager.Habitats)
        {
            if (h == null || !h.IsFormedBiome || h.residents == null || h.members == null) continue;

            bool touchesAura = false;
            for (int m = 0; m < h.members.Count; m++)
            {
                if (h.members[m] != null && auraHexes.Contains(h.members[m].hex))
                {
                    touchesAura = true;
                    break;
                }
            }
            if (!touchesAura) continue;

            foreach (var resident in h.residents)
            {
                if (resident == null || string.IsNullOrEmpty(resident.individualId)) continue;
                residentsList.Add((h.habitatId, resident.individualId));
            }
        }

        matchingCreaturesCount = residentsList.Count;
        if (matchingCreaturesCount == 0) return 0;

        var slots = GetSlotsFor(cluster.ClusterKey);
        int unlockedSlots = Mathf.Clamp(cluster.UnlockedFoodSlots, 0, 3);
        int totalDeposited = 0;

        int seedUnits = production.CareSettings != null
            ? Mathf.Max(1, production.CareSettings.seedFoodUnitsToFull)
            : 50;

        for (int s = 0; s < unlockedSlots; s++)
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

                int neededUnits = 0;
                if (care != null)
                {
                    float satietyDeficit = Mathf.Clamp(100f - (float)care.Satiety, 0f, 100f);
                    neededUnits = Mathf.CeilToInt(satietyDeficit / (100f / seedUnits));
                }

                long currentPantry = production.GetCreatureFoodAmount(indivId, foodId);
                int hungerDeficit = Mathf.Max(0, neededUnits - (int)currentPantry);

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
            SaveClusterSlots(cluster.ClusterKey);
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
        int initialAmount = Mathf.Max(1, slots.budgets[slotIndex]);

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
            modalSlider.maxValue = Mathf.Max(1, maxBudget);
            modalSlider.SetValueWithoutNotify(Mathf.Clamp(initialAmount, 1, Mathf.Max(1, maxBudget)));
        }

        modalCurrentAmount = Mathf.Clamp(initialAmount, 1, Mathf.Max(1, maxBudget));
        UpdateModalAmountDisplay();
        quantityModalObj.SetActive(true);
        quantityModalObj.transform.SetAsLastSibling();
        modalFoodNameText?.ForceMeshUpdate();
        modalAmountValueText?.ForceMeshUpdate();
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

        var tTxt = LumiWorldUITheme.CreateLabel(cRect, "Title", "Adjust food budget", 17f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.Center);
        SetTopLeft(tTxt.rectTransform, new Vector2(20f, -12f), new Vector2(320f, 30f));

        LumiWorldUITheme.CreateCloseButton(cRect, 14f, 12f, CloseQuantityModal, 30f);

        var iconObj = new GameObject("FoodIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(cRect, false);
        SetTopLeft(iconObj.GetComponent<RectTransform>(), new Vector2(24f, -48f), new Vector2(38f, 38f));
        modalFoodIcon = iconObj.GetComponent<Image>();
        modalFoodIcon.preserveAspect = true;

        modalFoodNameText = LumiWorldUITheme.CreateLabel(cRect, "FoodName", "", 15.5f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.MidlineLeft);
        SetTopLeft(modalFoodNameText.rectTransform, new Vector2(72f, -45f), new Vector2(260f, 26f));

        modalStorageText = LumiWorldUITheme.CreateLabel(cRect, "StorageInfo", "", 12.5f, LumiWorldUITheme.Muted, headingFont: false, align: TextAlignmentOptions.MidlineLeft);
        modalStorageText.fontStyle = FontStyles.Normal;
        SetTopLeft(modalStorageText.rectTransform, new Vector2(72f, -68f), new Vector2(260f, 22f));

        modalAmountValueText = LumiWorldUITheme.CreateLabel(cRect, "AmountValue", "1", 24f, LumiWorldUITheme.Text, headingFont: true, align: TextAlignmentOptions.Center);
        SetTopLeft(modalAmountValueText.rectTransform, new Vector2(80f, -92f), new Vector2(200f, 38f));

        // Slider (clean supersampled circle knob + 8px recessed track)
        modalSlider = LumiWorldUITheme.CreateCleanSlider(cRect, "Slider", trackHeight: 8f, knobDiameter: 22f);
        SetTopLeft(modalSlider.GetComponent<RectTransform>(), new Vector2(30f, -128f), new Vector2(300f, 28f));

        modalSlider.onValueChanged.AddListener((v) =>
        {
            modalCurrentAmount = Mathf.RoundToInt(v);
            UpdateModalAmountDisplay();
        });

        // Cancel & Apply capsule buttons
        Button cancelBtn = LumiWorldUITheme.CreatePillButton(
            cRect, "CancelBtn", "Cancel", null, primary: false, fontSize: 14f, onClick: CloseQuantityModal, out _, out _, out _, radius: 19);
        cancelBtn.gameObject.AddComponent<DialogButtonJuice>();
        RectTransform cbRect = cancelBtn.GetComponent<RectTransform>();
        cbRect.anchorMin = cbRect.anchorMax = cbRect.pivot = new Vector2(0f, 0f);
        cbRect.anchoredPosition = new Vector2(30f, 18f);
        cbRect.sizeDelta = new Vector2(142f, 38f);

        Button applyBtn = LumiWorldUITheme.CreatePillButton(
            cRect, "ApplyBtn", "Apply", null, primary: true, fontSize: 14f, onClick: OnApplyQuantityModal, out _, out _, out _, radius: 19);
        applyBtn.gameObject.AddComponent<DialogButtonJuice>();
        RectTransform abRect = applyBtn.GetComponent<RectTransform>();
        abRect.anchorMin = abRect.anchorMax = abRect.pivot = new Vector2(1f, 0f);
        abRect.anchoredPosition = new Vector2(-30f, 18f);
        abRect.sizeDelta = new Vector2(142f, 38f);
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

    private static void SetTopLeft(RectTransform rect, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }
}

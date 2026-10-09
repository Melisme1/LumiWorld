using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LumiWorld.Acs
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResourceInventoryUIController : MonoBehaviour
    {
        [SerializeField] private ResourceInventoryUISkin skin;
        [SerializeField] private ResourceProductionRuntime production;
        [SerializeField] private Camera mapCamera;
        [SerializeField] private bool replaceLegacyResourceBar = true;
        [Min(.1f)] [SerializeField] private float refreshSeconds = .2f;
        private ResourceInventoryUIView view;
        private Canvas canvas;
        private HabitatRuntimeManager manager;
        private ResourceInventory inventory;
        private InventoryUI legacyBar;
        private bool legacyWasActive, subscribed, dirty = true, summaryOffline;
        private string selectedHabitatId, selectedAnchor, selectorHabitatId, pendingResourceId;
        private ResourceTier selectorTier;
        private int selectorIndex;
        private float nextRefresh, receiptUntil;
        private Vector2 pressPosition;
        private bool mapPress;
        private List<ResourceUIItem> warehouseItems;

        public void Initialize(ResourceProductionRuntime runtime, ResourceInventoryUISkin theme)
        { production = runtime; skin = theme; }

        private void Start()
        {
            if (production == null) production = FindAnyObjectByType<ResourceProductionRuntime>();
            if (production == null || skin == null || skin.font == null)
            { Debug.LogError("[LumiWorld UI] Thiếu ResourceProductionRuntime hoặc UI Skin/font. Chạy Setup Inventory UI Skin trong Tools.", this); enabled = false; return; }
            manager = production.HabitatManager; inventory = production.Inventory;
            canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (manager == null || !manager.IsConfigured || canvas == null || inventory == null)
            { enabled = false; Debug.LogError("[LumiWorld UI] Cần Canvas, Inventory và Habitat Manager đã cấu hình catalog.", this); return; }
            view = new ResourceInventoryUIView((RectTransform)transform, skin, Actions());
            EconomyHUD.RegisterWindow(view.WarehouseWindow.gameObject, CloseWarehouse);
            EconomyHUD.RegisterWindow(view.SelectorWindow.gameObject, CloseSelector);
            EconomyHUD.RegisterWindow(view.SummaryWindow.gameObject, CloseSummary);
            warehouseItems = ResourceInventoryUIPresentation.Warehouse(manager.Catalog, inventory.GetAmount);
            view.BuildWarehouseItems(warehouseItems); Fit(); Subscribe();
            if (replaceLegacyResourceBar)
            {
                legacyBar = FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Include);
                if (legacyBar != null) { legacyWasActive = legacyBar.gameObject.activeSelf; legacyBar.gameObject.SetActive(false); }
            }
        }

        private ResourceInventoryUIActions Actions() => new ResourceInventoryUIActions
        {
            openWarehouse = OpenWarehouse, toggleHabitat = ToggleHabitat, closeHabitat = () => view.HabitatPanel.gameObject.SetActive(false),
            previousHabitat = () => Cycle(-1), nextHabitat = () => Cycle(1), closeWarehouse = CloseWarehouse,
            closeSelector = CloseSelector, openSelector = OpenSelector, chooseResource = id => { pendingResourceId = id; dirty = true; },
            applySelection = ApplySelection, clearSelection = () => { pendingResourceId = string.Empty; ApplySelection(); },
            collectHabitat = () => production.CollectHabitat(selectedHabitatId), collectResource = id => production.CollectResource(selectedHabitatId, id),
            openRecovery = OpenRecovery, closeSummary = CloseSummary,
            collectSummary = () => { if (summaryOffline) production.CollectAll(); else production.CollectRecovery(); },
            collectPending = (habitatId, resourceId) => production.CollectResource(habitatId, resourceId),
            closeReceipt = () => view.Receipt.gameObject.SetActive(false)
        };

        private void Update()
        {
            if (view == null) return;
            if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame && !EconomyHUD.IsWindowOpen)
                OpenWarehouse();
            HandleMapSelection();
        }
        private void LateUpdate()
        {
            if (view == null) return; Fit();
            if (view.Receipt.gameObject.activeSelf && Time.unscaledTime > receiptUntil) view.Receipt.gameObject.SetActive(false);
            if (!dirty && Time.unscaledTime < nextRefresh) return;
            dirty = false; nextRefresh = Time.unscaledTime + Mathf.Max(.1f, refreshSeconds); Refresh();
        }
        private void Fit()
        {
            if (canvas == null || view == null) return;
            var rect = (RectTransform)transform;
            Vector2 pixels = ((RectTransform)canvas.transform).rect.size * canvas.scaleFactor;
            float fit = ResourceInventoryUILayout.FitScale(pixels.x, pixels.y); if (fit <= 0 || canvas.scaleFactor <= 0) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = pixels / fit; rect.localScale = Vector3.one * (fit / canvas.scaleFactor);
            view.Layout(rect.sizeDelta.x, rect.sizeDelta.y);
        }
        private HabitatState FindHabitat(string id)
        {
            foreach (var habitat in manager.Habitats) if (habitat.habitatId == id) return habitat;
            return null;
        }
        private void Refresh()
        {
            var habitat = FindHabitat(selectedHabitatId);
            if (habitat == null && !string.IsNullOrEmpty(selectedAnchor))
                foreach (var candidate in manager.Habitats)
                    if (candidate.members.Exists(m => m.placementId == selectedAnchor))
                    { selectedHabitatId = candidate.habitatId; habitat = candidate; break; }
            view.BindHabitat(ResourceInventoryUIPresentation.Habitat(habitat, production.FindState(selectedHabitatId), manager.Catalog, production.Settings));
            foreach (var item in warehouseItems) item.stock = inventory.GetAmount(item.id);
            var recovered = ResourceInventoryUIPresentation.Buffers(production.States, manager.Catalog, production.Settings, true);
            long recoveryCount = 0; foreach (var buffer in recovered) recoveryCount += buffer.amount;
            view.UpdateWarehouse(warehouseItems, recoveryCount);
            if (view.SelectorWindow.gameObject.activeSelf)
            {
                var source = FindHabitat(selectorHabitatId);
                if (source == null) { CloseSelector(); Notice("Habitat đã thay đổi. Hãy chọn lại nhóm."); }
                else
                {
                    var options = ResourceInventoryUIPresentation.Options(source, manager.Catalog, selectorTier, selectorIndex);
                    var eligible = HabitatEligibilityService.EvaluateSelection(source, selectorTier, selectorIndex, pendingResourceId, manager.Catalog);
                    bool canApply = HabitatEligibilityService.IsSlotUnlocked(source, selectorTier, selectorIndex) &&
                        (string.IsNullOrEmpty(pendingResourceId) || eligible.IsEligible);
                    view.UpdateOptions(options, pendingResourceId, canApply, string.IsNullOrEmpty(pendingResourceId) ? "Bỏ lựa chọn: giữ nguyên hàng đã sản xuất." : eligible.Reason);
                }
            }
            if (view.SummaryWindow.gameObject.activeSelf)
                view.BindSummary(summaryOffline ? ResourceInventoryUIPresentation.Buffers(production.States, manager.Catalog, production.Settings) : recovered, summaryOffline);
        }
        private void Select(HabitatState habitat)
        {
            if (habitat == null) return; selectedHabitatId = habitat.habitatId;
            selectedAnchor = habitat.members.Count > 0 ? habitat.members[0].placementId : null;
            view.HabitatPanel.gameObject.SetActive(true); dirty = true;
        }
        private void Cycle(int direction)
        {
            var list = new List<HabitatState>(manager.Habitats); if (list.Count == 0) return;
            list.Sort((a,b) => string.CompareOrdinal(a.habitatId, b.habitatId));
            int index = list.FindIndex(h => h.habitatId == selectedHabitatId); Select(list[(Mathf.Max(0,index) + direction + list.Count) % list.Count]);
        }
        private void ToggleHabitat()
        {
            if (view.HabitatPanel.gameObject.activeSelf) { view.HabitatPanel.gameObject.SetActive(false); return; }
            if (selectedHabitatId == null && manager.Habitats.Count > 0) Select(manager.Habitats[0]);
            else view.HabitatPanel.gameObject.SetActive(true);
            dirty = true;
        }
        private void HandleMapSelection()
        {
            var mouse = Mouse.current; if (mouse == null) return;
            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            { pressPosition = position; mapPress = !EconomyHUD.IsPointerOverInteractiveUI(position); }
            if (!mouse.leftButton.wasReleasedThisFrame || !mapPress) return;
            mapPress = false;
            if (Vector2.Distance(position, pressPosition) > 6 || EconomyHUD.IsPointerOverInteractiveUI(position)) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed)) return;
            foreach (var drag in FindObjectsByType<CardDrag>()) if (drag.IsDragging) return;
            if (mapCamera == null) mapCamera = Camera.main; if (mapCamera == null) return;
            manager.RefreshNow();
            var hits = Physics.RaycastAll(mapCamera.ScreenPointToRay(position), 1000);
            Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var identity = hit.collider.GetComponentInParent<HabitatPlacementIdentity>();
                var tile = hit.collider.GetComponentInParent<HexTileInfo>();
                foreach (var habitat in manager.Habitats)
                    if (identity != null && (habitat.members.Exists(m => m.placementId == identity.PlacementId) || habitat.residents.Exists(r => r.individualId == identity.PlacementId)) ||
                        tile != null && habitat.ContainsHex(tile.coordinates))
                    { Select(habitat); return; }
            }
        }
        private void ShowModal(RectTransform modal)
        { modal.gameObject.SetActive(true); modal.SetAsLastSibling(); EconomyHUD.BringWindowToFront(modal.gameObject); dirty = true; }
        public void OpenWarehouse() { if (view != null) ShowModal(view.WarehouseWindow); }
        public void CloseWarehouse() { view?.WarehouseWindow.gameObject.SetActive(false); EconomyHUD.MarkWindowClosed(); }
        private void OpenSelector(ResourceTier tier, int index)
        {
            var habitat = FindHabitat(selectedHabitatId);
            if (!HabitatEligibilityService.IsSlotUnlocked(habitat, tier, index)) return;
            selectorHabitatId = selectedHabitatId; selectorTier = tier; selectorIndex = index;
            pendingResourceId = habitat.GetSlot(tier,index).selectedResourceId;
            view.BuildOptions(ResourceInventoryUIPresentation.Options(habitat, manager.Catalog, tier, index)); ShowModal(view.SelectorWindow);
        }
        public void CloseSelector() { view?.SelectorWindow.gameObject.SetActive(false); pendingResourceId = null; EconomyHUD.MarkWindowClosed(); }
        private void ApplySelection()
        {
            if (manager.TrySelectResource(selectorHabitatId, selectorTier, selectorIndex, pendingResourceId, out string reason)) CloseSelector();
            else Notice(reason);
            dirty = true;
        }
        private void OpenRecovery() { summaryOffline = false; ShowModal(view.SummaryWindow); }
        // Call only after a future save/offline service restores and settles the authoritative buffers.
        // This presentation hook never creates production or automatically deposits it into Inventory.
        public void ShowOfflineSummary() { if (view == null) return; summaryOffline = true; ShowModal(view.SummaryWindow); }
        public void CloseSummary() { view?.SummaryWindow.gameObject.SetActive(false); EconomyHUD.MarkWindowClosed(); }
        private void Notice(string message) { view.ShowReceipt(message, new List<ResourceUIBuffer>()); receiptUntil = Time.unscaledTime + 6; }
        private void Received(ResourceCollectionReceipt receipt)
        {
            var received = new List<ResourceUIBuffer>();
            foreach (var item in receipt.Items)
                if (item.Transferred > 0)
                {
                    var definition = HabitatEligibilityService.FindDefinition(manager.Catalog, item.ResourceId);
                    var row = received.Find(r => r.resourceId == item.ResourceId);
                    if (row != null) row.amount += item.Transferred;
                    else received.Add(new ResourceUIBuffer { resourceId = item.ResourceId, name = item.ResourceName, icon = definition?.resource?.icon, amount = item.Transferred });
                }
            view.ShowReceipt(receipt.Message + " Nhận " + receipt.TotalTransferred + "; còn chờ " + receipt.TotalRemaining + ".", received);
            receiptUntil = Time.unscaledTime + 6; dirty = true;
        }
        private void Changed() => dirty = true;
        private void StockChanged(string id, int stock, int delta) => dirty = true;
        private void Subscribe()
        {
            if (subscribed || production == null) return;
            production.ProductionChanged += Changed; production.ResourcesCollected += Received;
            manager.HabitatsChanged += Changed; inventory.OnInventoryChanged += StockChanged; subscribed = true;
        }
        private void OnEnable()
        {
            if (view != null)
            {
                Subscribe(); dirty = true;
                if (replaceLegacyResourceBar && legacyBar != null) legacyBar.gameObject.SetActive(false);
            }
        }
        private void OnDisable()
        {
            if (subscribed)
            {
                if (production != null) { production.ProductionChanged -= Changed; production.ResourcesCollected -= Received; }
                if (manager != null) manager.HabitatsChanged -= Changed;
                if (inventory != null) inventory.OnInventoryChanged -= StockChanged;
                subscribed = false;
            }
            if (legacyBar != null && legacyWasActive) legacyBar.gameObject.SetActive(true);
        }
        private void OnDestroy()
        {
            if (view == null) return;
            EconomyHUD.UnregisterWindow(view.WarehouseWindow.gameObject);
            EconomyHUD.UnregisterWindow(view.SelectorWindow.gameObject);
            EconomyHUD.UnregisterWindow(view.SummaryWindow.gameObject);
        }
    }
}

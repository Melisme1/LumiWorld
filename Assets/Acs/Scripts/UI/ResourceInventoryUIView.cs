using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Economy.Crafting;
using G = LumiWorld.Acs.ResourceUIGraphics;

namespace LumiWorld.Acs
{
    public sealed class ResourceInventoryUIActions
    {
        public Action openWarehouse, toggleHabitat, closeHabitat, previousHabitat, nextHabitat;
        public Action closeWarehouse, closeSelector, applySelection, clearSelection, openRecovery;
        public Action collectHabitat, closeSummary, collectSummary, closeReceipt;
        public Action<ResourceTier, int> openSelector;
        public Action<string> chooseResource, collectResource;
        public Action<string, string> collectPending;
        public Func<string, int> getStock;
    }

    // Shared by gameplay and the offscreen layout verification scene.
    public sealed class ResourceInventoryUIView
    {
        private readonly RectTransform root;
        private readonly ResourceInventoryUISkin skin;
        private readonly ResourceInventoryUIActions actions;
        public RectTransform HabitatPanel { get; private set; }
        public RectTransform WarehouseWindow { get; private set; }
        public RectTransform SelectorWindow { get; private set; }
        public RectTransform SummaryWindow { get; private set; }
        public RectTransform Receipt { get; private set; }
        private RectTransform habitatContent, warehouseContent, selectorContent, summaryContent, detailPane;
        private TextMeshProUGUI habitatTitle, habitatSubtitle, detailName, detailMeta, detailStock, detailDescription, selectorStatus;
        private Image detailIcon;
        private Button collectHabitat, apply, recovery, collectSummary;
        private readonly Dictionary<string, TextMeshProUGUI> warehouseCounts = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Image> warehouseBorders = new Dictionary<string, Image>();
        private readonly Dictionary<string, TextMeshProUGUI> bufferCounts = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Button> bufferButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, OptionCard> optionCards = new Dictionary<string, OptionCard>();
        private RectTransform selectorDetailPane;
        private Image selectorDetailIcon;
        private TextMeshProUGUI selectorDetailName;
        private TextMeshProUGUI selectorDetailTime;
        private TextMeshProUGUI selectorDetailSub;
        private GameObject selectorDetailTimePill;
        private List<ResourceUIOption> cachedOptions = new List<ResourceUIOption>();
        private RectTransform mainSelectorPanel;
        private RectTransform sideDetailPanel;
        private bool isSideDetailOpen;
        private Image sideDetailBg;
        private Image sideDetailIcon;
        private TextMeshProUGUI sideDetailName;
        private TextMeshProUGUI sideDetailBiome;
        private TextMeshProUGUI sideDetailStock;
        private TextMeshProUGUI sideDetailTier;
        private TextMeshProUGUI sideDetailTime;
        private TextMeshProUGUI sideDetailReq;
        private RectTransform sideDetailRecipesContent;
        private TextMeshProUGUI sideDetailRecipeTip;
        private TextMeshProUGUI sideDetailDesc;
        private ResourceUIOption currentSelectedOption;
        private readonly Dictionary<string, TextMeshProUGUI> summaryCounts = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Button> summaryButtons = new Dictionary<string, Button>();
        private string habitatKey, summaryKey, selectedWarehouseId;
        private string activeWarehouseTab = "All";
        private readonly List<(string name, Button btn, Image bg, TextMeshProUGUI lbl)> warehouseTabs = new List<(string, Button, Image, TextMeshProUGUI)>();
        private List<ResourceUIItem> allWarehouseItems = new List<ResourceUIItem>();
        private List<ResourceUIItem> warehouseItems;
        private sealed class OptionCard
        {
            public string id;
            public Image cardBackground;
            public Image border;
            public Image icon;
            public Button button;
            public bool eligible;
            public ResourceUIOption option;
        }

        public ResourceInventoryUIView(RectTransform root, ResourceInventoryUISkin skin, ResourceInventoryUIActions actions)
        {
            this.root = root; this.skin = skin; this.actions = actions;
            G.Round(root, "WarehouseButton", skin.iconWarehouse, skin, 38, 18, 52, () => actions.openWarehouse?.Invoke());
            // HabitatButton removed per user request: all habitat interaction is unified in Living Creatures Panel 1
            var whCap = G.Text(root, "WarehouseCaption", "Warehouse", skin, 14, 74, 100, 22, 12.5f, new Color(0.06f, 0.12f, 0.20f, 1f), bold: true);
            whCap.alignment = TextAlignmentOptions.Center;
            whCap.overflowMode = TextOverflowModes.Overflow;
            whCap.textWrappingMode = TextWrappingModes.NoWrap;
            BuildHabitat(); BuildWarehouse(); BuildSelector(); BuildSummary();
            Receipt = G.Rect("HarvestReceipt", root); G.Place(Receipt, 240, 20, 470, 100);
            G.Image(Receipt, skin.surfaceInset, true, skin.insetPPU, true); Receipt.gameObject.SetActive(false);
            HabitatPanel.gameObject.SetActive(false);
        }

        public void Layout(float width, float height)
        {
            G.Place(HabitatPanel, width - 296, 110, 280, ResourceInventoryUILayout.PanelHeight(height));
            var viewport = (RectTransform)habitatContent.parent.parent;
            G.Place(viewport, 22, 93, 236, HabitatPanel.sizeDelta.y - 162);
            G.Place((RectTransform)collectHabitat.transform, 28, HabitatPanel.sizeDelta.y - 58, 224, 42);
            G.Place(Receipt, (width - 470) * .5f, 18, 470, Receipt.sizeDelta.y);
        }

        private RectTransform Modal(string name, float width, float height, string title, string subtitle, Action close, out RectTransform panel)
        {
            var modal = G.Rect(name, root); G.Stretch(modal);
            var dimmer = G.Rect("Dimmer", modal); G.Stretch(dimmer);
            var dim = dimmer.gameObject.AddComponent<Image>(); dim.color = new Color(.1f, .17f, .13f, .48f); dim.raycastTarget = true;
            dimmer.gameObject.AddComponent<ResourceUIInputBlocker>();
            panel = G.Rect("Panel", modal); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(width, height); G.Image(panel, skin.windowMain, true, skin.windowPPU, true);
            G.Text(panel, "Title", title, skin, 30, 24, width - 116, 32, 25, bold: true);
            G.Text(panel, "Subtitle", subtitle, skin, 30, 60, width - 80, 24, 14);
            G.Round(panel, "Close", skin.iconClose, skin, width - 68, 20, 38, () => close?.Invoke());
            modal.gameObject.SetActive(false); return modal;
        }

        private void BuildHabitat()
        {
            HabitatPanel = G.Rect("HabitatPanel", root); G.Place(HabitatPanel, 984, 110, 280, 442);
            G.Image(HabitatPanel, skin.panelHabitat, true, skin.panelPPU, true);
            habitatTitle = G.Text(HabitatPanel, "Title", "Habitat", skin, 26, 25, 195, 30, 23, bold: true);
            habitatSubtitle = G.Text(HabitatPanel, "Population", "Select a habitat on the map", skin, 26, 59, 228, 25, 13);
            G.Round(HabitatPanel, "Close", skin.iconClose, skin, 230, 25, 30, () => actions.closeHabitat?.Invoke());
            habitatContent = G.Scroll(HabitatPanel, "HabitatScroll", 22, 93, 236, 280);
            collectHabitat = G.Button(HabitatPanel, "CollectHabitat", "Collect All", skin, 28, 384, 224, 42,
                () => actions.collectHabitat?.Invoke(), true);
        }

        public void BindHabitat(ResourceUIHabitat model)
        {
            if (model == null)
            {
                habitatTitle.text = "Habitat"; habitatSubtitle.text = "Select a habitat on the map"; collectHabitat.interactable = false;
                if (habitatKey != "empty") { G.Clear(habitatContent); G.Text(habitatContent, "Empty", "Click Nature or creature to view habitat.", skin, 2, 4, 222, 100, 16); habitatKey = "empty"; }
                return;
            }
            habitatTitle.text = model.title;
            int unlockedSlotsCount = 0;
            if (model.slots != null) { foreach (var s in model.slots) if (s != null && s.unlocked) unlockedSlotsCount++; }
            habitatSubtitle.text = $"{model.hexCount} Hexes · Residents: {model.residentCount}/{model.capacity} · Slots: {unlockedSlotsCount}/{(model.slots != null ? model.slots.Count : 3)}";
            if (habitatKey != model.structureKey)
            {
                habitatKey = model.structureKey; G.Clear(habitatContent); bufferCounts.Clear(); bufferButtons.Clear();
                G.Button(habitatContent, "Previous", "‹ Prev", skin, 0, 0, 104, 30, () => actions.previousHabitat?.Invoke());
                G.Button(habitatContent, "Next", "Next ›", skin, 112, 0, 104, 30, () => actions.nextHabitat?.Invoke());
                float residentHeight = Mathf.Max(66, model.residents.Split('\n').Length * 20 + 10);
                var residents = G.Text(habitatContent, "Residents", model.residents, skin, 2, 38, 214, residentHeight, 13);
                residents.overflowMode = TextOverflowModes.Truncate;
                G.Text(habitatContent, "Hint", model.hint, skin, 2, 42 + residentHeight, 214, 40, 13, skin.muted);
                float y = 86 + residentHeight;
                foreach (var slot in model.slots)
                {
                    var row = G.Rect("Slot_" + slot.tier + "_" + slot.index, habitatContent); G.Place(row, 0, y, 216, 154);
                    G.Image(row, skin.surfaceInset, true, skin.insetPPU);
                    var art = G.Rect("ResourceSlot", row); G.Place(art, 8, 12, 60, 60);
                    G.Image(art, slot.unlocked ? skin.slotResource : skin.slotLocked);
                    if (slot.icon != null && slot.unlocked) G.Icon(art, "ResourceIcon", slot.icon, 11, 11, 38);
                    if (!slot.unlocked) G.Icon(art, "Lock", skin.iconLock, 19, 17, 24);
                    G.Text(row, "Tier", (slot.tier == ResourceTier.Tier1 ? "T1" : "T2") + " · Slot " + (slot.index + 1), skin, 76, 8, 132, 23, 14, bold: true);
                    G.Text(row, "ResourceName", slot.name, skin, 76, 31, 132, 36, 15, bold: true);
                    G.Text(row, "Rate", slot.unlocked ? slot.rate.ToString("0.##") + "/min" : "Locked", skin, 8, 75, 120, 22, 14);
                    var button = G.Button(row, "Choose", "Select", skin, 136, 73, 72, 28, () => actions.openSelector?.Invoke(slot.tier, slot.index));
                    button.interactable = slot.unlocked;
                    G.Text(row, "Reason", slot.reason, skin, 8, 103, 200, 45, 12, skin.muted);
                    y += 164;
                }
                G.Text(habitatContent, "PendingHeader", "PENDING COLLECTION", skin, 2, y, 214, 28, 14, bold: true); y += 34;
                if (model.buffers.Count == 0) { G.Text(habitatContent, "NoGoods", "No goods in buffer.", skin, 2, y, 214, 32, 13); y += 36; }
                foreach (var buffer in model.buffers)
                {
                    var row = G.Rect("Buffer_" + buffer.resourceId, habitatContent); G.Place(row, 0, y, 216, 76);
                    G.Icon(row, "Icon", buffer.icon, 0, 8, 42);
                    G.Text(row, "Name", buffer.name, skin, 48, 2, 168, 29, 14, bold: true);
                    bufferCounts[buffer.resourceId] = G.Text(row, "PendingAmount", "", skin, 48, 34, 97, 35, 13);
                    bufferButtons[buffer.resourceId] = G.Button(row, "Collect", "Collect", skin, 148, 37, 68, 30, () => actions.collectResource?.Invoke(buffer.resourceId), true);
                    y += 82;
                }
                habitatContent.sizeDelta = new Vector2(226, y + 8);
            }
            foreach (var buffer in model.buffers)
            {
                bufferCounts[buffer.resourceId].text = buffer.amount + " / " + buffer.cap + (buffer.amount >= buffer.cap ? " · Full" : "");
                bufferButtons[buffer.resourceId].interactable = buffer.amount > 0;
            }
            collectHabitat.interactable = model.buffers.Exists(b => b.amount > 0);
        }

        private void BuildWarehouse()
        {
            var modal = G.Rect("WarehouseWindow", root); G.Stretch(modal);
            var dimmer = G.Rect("Dimmer", modal); G.Stretch(dimmer);
            var dim = dimmer.gameObject.AddComponent<Image>();
            dim.color = new Color(0.04f, 0.08f, 0.14f, 0.65f);
            dim.raycastTarget = true;
            dimmer.gameObject.AddComponent<ResourceUIInputBlocker>();

            var panel = G.Rect("Panel", modal);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(910, 560);
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.sprite = GetSelectorPanelSprite();
            panelImg.type = Image.Type.Sliced;
            panelImg.color = new Color(0.88f, 0.93f, 0.97f, 1f);
            panelImg.raycastTarget = true;
            panel.gameObject.AddComponent<ResourceUIInputBlocker>();

            G.Text(panel, "Title", "INVENTORY", skin, 32, 22, 750, 32, 22, new Color(0.04f, 0.12f, 0.22f, 1f), bold: true);
            BuildWarehouseTabs(panel);

            G.Round(panel, "Close", skin.iconClose, skin, 910 - 64, 20, 36, () => actions.closeWarehouse?.Invoke());

            warehouseContent = G.Scroll(panel, "ResourceGrid", 28, 96, 598, 382);

            // Right detail pane (Clean Modern Tech Card Box)
            detailPane = G.Rect("ResourceDetails", panel);
            G.Place(detailPane, 646, 96, 236, 382);
            var detailBg = detailPane.gameObject.AddComponent<Image>();
            detailBg.sprite = GetSelectorOptionCardSprite();
            detailBg.type = Image.Type.Sliced;
            detailBg.color = Color.white;

            var art = G.Rect("DetailIconBox", detailPane);
            G.Place(art, 78, 20, 80, 80);
            var artImg = art.gameObject.AddComponent<Image>();
            artImg.sprite = GetSelectorIconBoxSprite();
            artImg.type = Image.Type.Sliced;
            artImg.color = Color.white;

            detailIcon = G.Icon(art, "Icon", null, 14, 14, 52);

            detailName = G.Text(detailPane, "Name", "Select an Item", skin, 16, 114, 204, 38, 18, new Color(0.04f, 0.12f, 0.22f, 1f), bold: true);
            detailName.alignment = TextAlignmentOptions.Center;

            detailStock = G.Text(detailPane, "Stock", "", skin, 16, 154, 204, 28, 16, new Color(0.02f, 0.52f, 0.78f, 1f), bold: true);
            detailStock.alignment = TextAlignmentOptions.Center;

            detailMeta = G.Text(detailPane, "Source", "", skin, 16, 184, 204, 32, 13f, new Color(0.02f, 0.52f, 0.78f, 1f));
            detailMeta.alignment = TextAlignmentOptions.Center;

            detailDescription = G.Text(detailPane, "Description", "", skin, 20, 224, 196, 144, 13, new Color(0.20f, 0.28f, 0.38f, 1f));
            detailDescription.alignment = TextAlignmentOptions.TopLeft;

            detailPane.gameObject.SetActive(false);

            recovery = TechPillButton(panel, "Recovery", "Safe Stash", GetSelectorButtonSecondarySprite(), new Color(0.12f, 0.22f, 0.35f, 1f), 30, 494, 220, 40, () => actions.openRecovery?.Invoke());
            G.Text(panel, "CapacityHint", "Capacity tracked individually per resource type", skin, 270, 502, 590, 28, 13.5f, new Color(0.40f, 0.50f, 0.60f, 1f));

            modal.gameObject.SetActive(false);
            WarehouseWindow = modal;
        }

        private void BuildWarehouseTabs(RectTransform panel)
        {
            warehouseTabs.Clear();
            var tabs = new[] { ("All", 88f), ("Resources", 116f), ("Food", 88f), ("Medicine", 116f) };
            float curX = 28f;
            for (int i = 0; i < tabs.Length; i++)
            {
                string tabName = tabs[i].Item1;
                float tabW = tabs[i].Item2;
                var tabRect = G.Rect("Tab_" + tabName, panel);
                G.Place(tabRect, curX, 58, tabW, 30);

                var bg = tabRect.gameObject.AddComponent<Image>();
                bg.sprite = GetSelectorButtonSecondarySprite();
                bg.type = Image.Type.Sliced;
                bg.raycastTarget = true;
                tabRect.gameObject.AddComponent<ResourceUIInputBlocker>();

                var btn = tabRect.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };

                var lblRect = G.Rect("Label", tabRect);
                G.Stretch(lblRect);
                var lbl = lblRect.gameObject.AddComponent<TextMeshProUGUI>();
                lbl.font = skin.font;
                lbl.fontSize = 13.5f;
                lbl.fontStyle = FontStyles.Bold;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.raycastTarget = false;
                lbl.text = tabName;

                string captured = tabName;
                btn.onClick.AddListener(() => SelectWarehouseTab(captured));

                warehouseTabs.Add((tabName, btn, bg, lbl));
                curX += tabW + 10f;
            }
            UpdateWarehouseTabVisuals();
        }

        public void ResetSelection()
        {
            selectedWarehouseId = null;
            if (detailPane != null)
                detailPane.gameObject.SetActive(false);
            foreach (var pair in warehouseBorders)
                if (pair.Value != null) pair.Value.gameObject.SetActive(false);
        }

        private void SelectWarehouseTab(string tabName)
        {
            activeWarehouseTab = tabName;
            ResetSelection();
            UpdateWarehouseTabVisuals();
            RenderWarehouseGrid();
        }

        private void UpdateWarehouseTabVisuals()
        {
            foreach (var tab in warehouseTabs)
            {
                bool active = string.Equals(tab.name, activeWarehouseTab, StringComparison.OrdinalIgnoreCase);
                tab.bg.color = active ? new Color(0.01f, 0.60f, 0.90f, 1f) : new Color(0.88f, 0.94f, 0.99f, 1f);
                tab.lbl.color = active ? Color.white : new Color(0.28f, 0.38f, 0.50f, 1f);
            }
        }

        public void BuildWarehouseItems(List<ResourceUIItem> items)
        {
            allWarehouseItems = items ?? new List<ResourceUIItem>();
            RenderWarehouseGrid();
        }

        private void RenderWarehouseGrid()
        {
            List<ResourceUIItem> filtered;
            if (string.Equals(activeWarehouseTab, "Resources", StringComparison.OrdinalIgnoreCase))
                filtered = allWarehouseItems.FindAll(i => string.Equals(i.category, "Resource", StringComparison.OrdinalIgnoreCase));
            else if (string.Equals(activeWarehouseTab, "Food", StringComparison.OrdinalIgnoreCase))
                filtered = allWarehouseItems.FindAll(i => string.Equals(i.category, "Food", StringComparison.OrdinalIgnoreCase));
            else if (string.Equals(activeWarehouseTab, "Medicine", StringComparison.OrdinalIgnoreCase))
                filtered = allWarehouseItems.FindAll(i => string.Equals(i.category, "Medicine", StringComparison.OrdinalIgnoreCase));
            else
                filtered = allWarehouseItems;

            warehouseItems = filtered;
            G.Clear(warehouseContent);
            warehouseCounts.Clear();
            warehouseBorders.Clear();

            if (filtered == null || filtered.Count == 0)
            {
                ClearWarehouseDetails(activeWarehouseTab);
                var emptyRect = G.Rect("EmptyNotice", warehouseContent);
                G.Place(emptyRect, 0, 80, 580, 100);
                var emptyTxt = emptyRect.gameObject.AddComponent<TextMeshProUGUI>();
                emptyTxt.font = skin.font;
                emptyTxt.fontSize = 15f;
                emptyTxt.fontStyle = FontStyles.Bold;
                emptyTxt.alignment = TextAlignmentOptions.Center;
                emptyTxt.color = new Color(0.40f, 0.50f, 0.60f, 1f);

                if (string.Equals(activeWarehouseTab, "Food", StringComparison.OrdinalIgnoreCase))
                    emptyTxt.text = "No food in inventory.\nCraft nutritious meals for your creatures in the Workshop!";
                else if (string.Equals(activeWarehouseTab, "Medicine", StringComparison.OrdinalIgnoreCase))
                    emptyTxt.text = "No medicine in inventory.\nBrew herbal remedies in the Workshop!";
                else if (string.Equals(activeWarehouseTab, "Resources", StringComparison.OrdinalIgnoreCase))
                    emptyTxt.text = "No raw resources collected.\nAssign creatures to habitats to harvest resources!";
                else
                    emptyTxt.text = "Storage is empty.\nHarvest resources or craft items to store them here!";

                return;
            }

            const int cols = 6;
            const float slotW = 78f;
            const float slotH = 78f;
            const float gapX = 18f;
            const float gapY = 16f;
            const float startX = 20f;
            const float startY = 12f;

            for (int i = 0; i < filtered.Count; i++)
            {
                var item = filtered[i];
                float x = startX + (i % cols) * (slotW + gapX);
                float y = startY + (i / cols) * (slotH + gapY);

                // Ô vật phẩm: Chỉ khung viền xanh công nghệ (GetSelectorIconBoxSprite), không viền trắng ngoài, không tên chữ bên dưới
                var rect = G.Rect("Item_" + item.id, warehouseContent);
                G.Place(rect, x, y, slotW, slotH);

                var slotImg = rect.gameObject.AddComponent<Image>();
                slotImg.sprite = GetSelectorIconBoxSprite();
                slotImg.type = Image.Type.Sliced;
                slotImg.color = Color.white;
                slotImg.raycastTarget = true;
                rect.gameObject.AddComponent<ResourceUIInputBlocker>();

                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = slotImg;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => SelectWarehouse(item.id));

                // Selected Neon Cyan Glow Frame
                var selRect = G.Rect("Selected", rect);
                G.Stretch(selRect);
                var selImg = selRect.gameObject.AddComponent<Image>();
                selImg.sprite = GetSelectorOptionSelectedSprite();
                selImg.type = Image.Type.Sliced;
                selImg.color = Color.white;
                selImg.raycastTarget = false;
                selRect.gameObject.SetActive(false);
                warehouseBorders[item.id] = selImg;

                // Icon ở chính giữa ô (50x50px)
                if (item.icon != null)
                {
                    var ic = G.Icon(rect, "ResourceIcon", item.icon, 14, 14, 50);
                    ic.raycastTarget = false;
                }

                // Số lượng ở góc dưới phải của ô
                var countRect = G.Rect("CountBox", rect);
                G.Place(countRect, 4, 50, 68, 22);
                var countTxt = countRect.gameObject.AddComponent<TextMeshProUGUI>();
                countTxt.font = skin.font;
                countTxt.fontSize = 13.5f;
                countTxt.fontStyle = FontStyles.Bold;
                countTxt.alignment = TextAlignmentOptions.BottomRight;
                countTxt.color = new Color(0.02f, 0.48f, 0.72f, 1f);
                countTxt.text = item.stock.ToString();
                countTxt.raycastTarget = false;
                warehouseCounts[item.id] = countTxt;
            }

            warehouseContent.sizeDelta = new Vector2(588, Mathf.Max(380, startY * 2 + Mathf.CeilToInt(filtered.Count / (float)cols) * (slotH + gapY)));
            if (!string.IsNullOrEmpty(selectedWarehouseId) && filtered.Exists(i => i.id == selectedWarehouseId))
                SelectWarehouse(selectedWarehouseId);
            else
                ResetSelection();
        }

        public void UpdateWarehouse(List<ResourceUIItem> items, long recoveryAmount)
        {
            allWarehouseItems = items ?? new List<ResourceUIItem>();
            foreach (var item in allWarehouseItems)
            {
                if (warehouseCounts.TryGetValue(item.id, out var label))
                {
                    label.text = item.stock.ToString();
                    label.color = item.stock >= item.capacity ? new Color(0.85f, 0.45f, 0.1f, 1f) : new Color(0.02f, 0.48f, 0.72f, 1f);
                }
            }
            if (recovery != null)
            {
                recovery.interactable = recoveryAmount > 0;
                var recTxt = recovery.GetComponentInChildren<TextMeshProUGUI>();
                if (recTxt != null) recTxt.text = "Safe Stash · " + recoveryAmount;
            }
            if (!string.IsNullOrEmpty(selectedWarehouseId) && warehouseItems != null && warehouseItems.Exists(i => i.id == selectedWarehouseId))
            {
                SelectWarehouse(selectedWarehouseId);
            }
            else if (string.IsNullOrEmpty(selectedWarehouseId))
            {
                if (detailPane != null) detailPane.gameObject.SetActive(false);
            }
            else
            {
                ResetSelection();
            }
        }

        private void SelectWarehouse(string id)
        {
            selectedWarehouseId = id;
            foreach (var pair in warehouseBorders)
                if (pair.Value != null) pair.Value.gameObject.SetActive(pair.Key == id);

            var item = warehouseItems?.Find(i => i.id == id);
            if (item == null)
            {
                ResetSelection();
                return;
            }

            if (detailPane != null) detailPane.gameObject.SetActive(true);

            if (detailIcon != null)
            {
                detailIcon.sprite = item.icon;
                detailIcon.color = item.icon != null ? Color.white : Color.clear;
            }
            if (detailName != null) detailName.text = item.name;
            string source = (item.category == "Food" || item.category == "Medicine" || item.biome == "Workshop" || item.biome == "Crafting") ? "Crafting" : item.biome;
            if (detailMeta != null) detailMeta.text = $"<color=#0284C7>From: {source}</color>";
            if (detailStock != null) detailStock.text = "Collected: " + item.stock;
            if (detailDescription != null) detailDescription.text = item.description;
        }

        private void ClearWarehouseDetails(string tab)
        {
            ResetSelection();
        }

        private static Sprite selectorPanelSprite;
        private static Sprite selectorOptionCardSprite;
        private static Sprite selectorOptionSelectedSprite;
        private static Sprite selectorButtonPrimarySprite;
        private static Sprite selectorButtonSecondarySprite;
        private static Sprite selectorIconBoxSprite;
        private static Sprite selectorItemCardSprite;
        private static Sprite selectorCardSelectedSprite;
        private static Sprite selectorPillBannerSprite;
        private static Sprite selectorLockBannerSprite;
        private static Sprite selectorDetailCardSprite;
        private static Sprite selectorCircleFrameSprite;
        private static Sprite selectorTimePillSprite;
        private static Sprite selectorAccentBarSprite;
        private static Sprite selectorClockIconSprite;

        private static Sprite CreateTechBox(int w, int h, int r, Color fillColor, Color borderColor)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x < r) ? r - x : (x > w - 1 - r ? x - (w - 1 - r) : 0f);
                    float dy = (y < r) ? r - y : (y > h - 1 - r ? y - (h - 1 - r) : 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > r)
                    {
                        px[y * w + x] = Color.clear;
                    }
                    else if (dist >= r - 1.5f || x == 0 || x == w - 1 || y == 0 || y == h - 1)
                    {
                        px[y * w + x] = borderColor;
                    }
                    else
                    {
                        px[y * w + x] = fillColor;
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            Vector4 border = new Vector4(r + 2, r + 2, r + 2, r + 2);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateOutlineBox(int w, int h, int r, Color borderColor, float strokeWidth)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x < r) ? r - x : (x > w - 1 - r ? x - (w - 1 - r) : 0f);
                    float dy = (y < r) ? r - y : (y > h - 1 - r ? y - (h - 1 - r) : 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > r)
                    {
                        px[y * w + x] = Color.clear;
                    }
                    else if (dist >= r - strokeWidth || x < strokeWidth || x >= w - strokeWidth || y < strokeWidth || y >= h - strokeWidth)
                    {
                        px[y * w + x] = borderColor;
                    }
                    else
                    {
                        px[y * w + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            Vector4 border = new Vector4(r + 2, r + 2, r + 2, r + 2);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateCapsule(int w, int h, int r, Color fillColor, Color borderColor)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x < r) ? r - x : (x > w - 1 - r ? x - (w - 1 - r) : 0f);
                    float dy = (y < r) ? r - y : (y > h - 1 - r ? y - (h - 1 - r) : 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > r)
                    {
                        px[y * w + x] = Color.clear;
                    }
                    else if (dist >= r - 1.2f || x == 0 || x == w - 1 || y == 0 || y == h - 1)
                    {
                        px[y * w + x] = borderColor;
                    }
                    else
                    {
                        px[y * w + x] = fillColor;
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            Vector4 border = new Vector4(r, 0, r, 0);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite GetSelectorPanelSprite()
        {
            if (selectorPanelSprite == null)
                selectorPanelSprite = CreateTechBox(64, 64, 12, new Color(0.965f, 0.985f, 1f, 0.98f), new Color(0f, 0.70f, 0.88f, 1f));
            return selectorPanelSprite;
        }

        private static Sprite GetSelectorOptionCardSprite()
        {
            if (selectorOptionCardSprite == null)
                selectorOptionCardSprite = CreateTechBox(64, 64, 8, Color.white, new Color(0.82f, 0.88f, 0.94f, 1f));
            return selectorOptionCardSprite;
        }

        private static Sprite GetSelectorOptionSelectedSprite()
        {
            if (selectorOptionSelectedSprite == null)
                selectorOptionSelectedSprite = CreateTechBox(64, 64, 8, Color.clear, new Color(0f, 0.71f, 0.85f, 1f));
            return selectorOptionSelectedSprite;
        }

        private static Sprite GetSelectorIconBoxSprite()
        {
            if (selectorIconBoxSprite == null)
                selectorIconBoxSprite = CreateTechBox(48, 48, 6, new Color(0.92f, 0.96f, 1f, 1f), new Color(0.60f, 0.80f, 0.92f, 1f));
            return selectorIconBoxSprite;
        }

        private static Sprite GetSelectorButtonPrimarySprite()
        {
            if (selectorButtonPrimarySprite == null)
                selectorButtonPrimarySprite = CreateTechBox(64, 36, 6, new Color(0f, 0.68f, 0.86f, 1f), new Color(0.35f, 0.85f, 1f, 1f));
            return selectorButtonPrimarySprite;
        }

        private static Sprite GetSelectorButtonSecondarySprite()
        {
            if (selectorButtonSecondarySprite == null)
                selectorButtonSecondarySprite = CreateTechBox(64, 36, 6, Color.white, new Color(0.70f, 0.82f, 0.90f, 1f));
            return selectorButtonSecondarySprite;
        }

        private static Sprite GetSelectorItemCardSprite()
        {
            if (selectorItemCardSprite == null)
                selectorItemCardSprite = CreateTechBox(64, 64, 12, new Color(0.18f, 0.19f, 0.28f, 1f), new Color(0.28f, 0.30f, 0.42f, 1f));
            return selectorItemCardSprite;
        }

        private static Sprite GetSelectorCardSelectedSprite()
        {
            if (selectorCardSelectedSprite == null)
                selectorCardSelectedSprite = CreateOutlineBox(64, 64, 12, new Color(0.20f, 0.85f, 1f, 1f), 3.5f);
            return selectorCardSelectedSprite;
        }

        private static Sprite GetSelectorPillBannerSprite()
        {
            if (selectorPillBannerSprite == null)
                selectorPillBannerSprite = CreateCapsule(64, 32, 16, new Color(0.28f, 0.50f, 0.90f, 1f), new Color(0.40f, 0.65f, 1f, 1f));
            return selectorPillBannerSprite;
        }

        private static Sprite GetSelectorLockBannerSprite()
        {
            if (selectorLockBannerSprite == null)
                selectorLockBannerSprite = CreateCapsule(64, 34, 17, new Color(0.60f, 0.65f, 0.72f, 1f), new Color(0.50f, 0.55f, 0.62f, 1f));
            return selectorLockBannerSprite;
        }

        private static Sprite GetSelectorDetailCardSprite()
        {
            if (selectorDetailCardSprite == null)
                selectorDetailCardSprite = CreateTechBox(64, 64, 14, new Color(0.93f, 0.96f, 0.98f, 1f), new Color(0.80f, 0.86f, 0.92f, 1f));
            return selectorDetailCardSprite;
        }

        private static Sprite GetSelectorCircleFrameSprite()
        {
            if (selectorCircleFrameSprite == null)
            {
                int w = 64, h = 64;
                float cx = w * 0.5f, cy = h * 0.5f, r = 29f;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                Color[] px = new Color[w * h];
                Color cyan = new Color(0.20f, 0.88f, 0.96f, 1f);
                Color lightBorder = new Color(0.86f, 0.90f, 0.94f, 1f);
                Color fill = Color.white;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = x - cx;
                        float dy = y - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        if (dist > r)
                        {
                            px[y * w + x] = Color.clear;
                        }
                        else if (dist >= r - 3.5f)
                        {
                            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                            if (angle < 0) angle += 360f;
                            if (angle >= 45f && angle <= 135f)
                                px[y * w + x] = cyan;
                            else
                                px[y * w + x] = lightBorder;
                        }
                        else
                        {
                            px[y * w + x] = fill;
                        }
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                selectorCircleFrameSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
            return selectorCircleFrameSprite;
        }

        private static Sprite GetSelectorTimePillSprite()
        {
            if (selectorTimePillSprite == null)
                selectorTimePillSprite = CreateCapsule(48, 26, 13, new Color(0.42f, 0.48f, 0.56f, 1f), new Color(0.35f, 0.40f, 0.48f, 1f));
            return selectorTimePillSprite;
        }

        private static Sprite GetSelectorAccentBarSprite()
        {
            if (selectorAccentBarSprite == null)
                selectorAccentBarSprite = CreateCapsule(32, 8, 4, new Color(0.78f, 0.28f, 0.92f, 1f), new Color(0.78f, 0.28f, 0.92f, 1f));
            return selectorAccentBarSprite;
        }

        private static Sprite GetSelectorClockIconSprite()
        {
            if (selectorClockIconSprite == null)
            {
                int w = 24, h = 24;
                float cx = 12f, cy = 12f, r = 10f;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                Color[] px = new Color[w * h];
                Color white = Color.white;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = x - cx;
                        float dy = y - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        if (dist > r)
                        {
                            px[y * w + x] = Color.clear;
                        }
                        else if (dist >= r - 1.8f)
                        {
                            px[y * w + x] = white;
                        }
                        else
                        {
                            bool isHourHand = (Mathf.Abs(dx) <= 0.9f && dy >= 0 && dy <= 5.5f);
                            bool isMinHand = (Mathf.Abs(dy) <= 0.9f && dx >= 0 && dx <= 5.5f);
                            bool isCenterDot = dist <= 1.8f;

                            if (isHourHand || isMinHand || isCenterDot)
                                px[y * w + x] = white;
                            else
                                px[y * w + x] = Color.clear;
                        }
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                selectorClockIconSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
            return selectorClockIconSprite;
        }

        private Button TechPillButton(Transform parent, string name, string label, Sprite bgSprite, Color textColor, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var rect = G.Rect(name, parent); G.Place(rect, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = bgSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = true;
            rect.gameObject.AddComponent<ResourceUIInputBlocker>();

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.92f, 0.98f, 1f);
            colors.disabledColor = new Color(0.75f, 0.80f, 0.85f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(action);

            var text = G.Text(rect, "Label", label, skin, 0, 0, w, h, 14.5f, textColor, true);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private void BuildSelector()
        {
            var modal = G.Rect("ResourceSelectorDialog", root); G.Stretch(modal);
            var dimmer = G.Rect("Dimmer", modal); G.Stretch(dimmer);
            var dim = dimmer.gameObject.AddComponent<Image>(); dim.color = new Color(0.04f, 0.08f, 0.14f, 0.65f); dim.raycastTarget = true;
            dimmer.gameObject.AddComponent<ResourceUIInputBlocker>();

            var panel = G.Rect("Panel", modal); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(800, 560);
            mainSelectorPanel = panel;
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.sprite = GetSelectorPanelSprite();
            panelImg.type = Image.Type.Sliced;
            panelImg.color = Color.white;
            panelImg.raycastTarget = true;
            panel.gameObject.AddComponent<ResourceUIInputBlocker>();

            G.Text(panel, "Title", "SELECT HARVEST RESOURCE", skin, 32, 20, 680, 30, 22, new Color(0.04f, 0.12f, 0.22f, 1f), bold: true);
            G.Text(panel, "Subtitle", "Same biome & tier · Automated harvest based on matching habitat residents", skin, 32, 50, 680, 22, 13, new Color(0.28f, 0.38f, 0.48f, 1f));

            G.Round(panel, "Close", skin.iconClose, skin, 800 - 64, 18, 36, () => {
                SetSideDetailOpen(false);
                actions.closeSelector?.Invoke();
            });

            // Scrollable view for choices (Image 2 style)
            selectorContent = G.Scroll(panel, "Choices", 28, 76, 744, 268);

            // Selected Item Detail Card (Image 3 style)
            BuildSelectorDetailCard(panel, 28, 350, 744, 82);

            selectorStatus = G.Text(panel, "Eligibility", "", skin, 30, 444, 738, 32, 13f, new Color(0.15f, 0.25f, 0.38f, 1f));

            // Preserved Clear, Cancel, Apply buttons
            TechPillButton(panel, "Clear", "Clear", GetSelectorButtonSecondarySprite(), new Color(0.12f, 0.22f, 0.35f, 1f), 30, 492, 152, 42, () => actions.clearSelection?.Invoke());
            TechPillButton(panel, "Cancel", "Cancel", GetSelectorButtonSecondarySprite(), new Color(0.12f, 0.22f, 0.35f, 1f), 474, 492, 130, 42, () => {
                SetSideDetailOpen(false);
                actions.closeSelector?.Invoke();
            });
            apply = TechPillButton(panel, "Apply", "Apply", GetSelectorButtonPrimarySprite(), Color.white, 616, 490, 154, 44, () => actions.applySelection?.Invoke());

            BuildSideDetailPanel(modal);

            modal.gameObject.SetActive(false);
            SelectorWindow = modal;
        }

        private void BuildSelectorDetailCard(Transform parent, float x, float y, float w, float h)
        {
            selectorDetailPane = G.Rect("DetailCard", parent);
            G.Place(selectorDetailPane, x, y, w, h);
            var bgImg = selectorDetailPane.gameObject.AddComponent<Image>();
            bgImg.sprite = GetSelectorDetailCardSprite();
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Color.white;
            selectorDetailPane.gameObject.AddComponent<ResourceUIInputBlocker>();

            // Circular avatar frame on left (Image 3)
            var avatarRing = G.Rect("AvatarFrame", selectorDetailPane);
            G.Place(avatarRing, 12, 11, 60, 60);
            var ringImg = avatarRing.gameObject.AddComponent<Image>();
            ringImg.sprite = GetSelectorCircleFrameSprite();
            ringImg.type = Image.Type.Simple;
            ringImg.preserveAspect = true;

            // Resource Icon inside circle
            var iconRect = G.Rect("ResourceIcon", avatarRing);
            G.Place(iconRect, 7, 7, 46, 46);
            selectorDetailIcon = iconRect.gameObject.AddComponent<Image>();
            selectorDetailIcon.preserveAspect = true;
            selectorDetailIcon.raycastTarget = false;

            // Resource Name (Bold dark text)
            selectorDetailName = G.Text(selectorDetailPane, "Name", "No Resource Selected", skin, 86, 12, 440, 26, 18.5f, new Color(0.10f, 0.14f, 0.22f, 1f), bold: true);

            // Time Pill Badge with clock icon
            var timePill = G.Rect("TimePill", selectorDetailPane);
            G.Place(timePill, 86, 44, 150, 26);
            var pillImg = timePill.gameObject.AddComponent<Image>();
            pillImg.sprite = GetSelectorTimePillSprite();
            pillImg.type = Image.Type.Sliced;
            pillImg.color = Color.white;
            timePill.gameObject.AddComponent<ResourceUIInputBlocker>();

            // Clock icon
            G.Icon(timePill, "ClockIcon", GetSelectorClockIconSprite(), 7, 5, 16);

            // Time text
            selectorDetailTime = G.Text(timePill, "TimeText", "10s", skin, 28, 0, 118, 26, 12.5f, Color.white, bold: true);
            selectorDetailTime.alignment = TextAlignmentOptions.MidlineLeft;
            selectorDetailTimePill = timePill.gameObject;
            selectorDetailTimePill.SetActive(false);

            // Sub text / condition
            selectorDetailSub = G.Text(selectorDetailPane, "SubText", "Select an available item above to start harvesting.", skin, 246, 44, 430, 25, 13f, new Color(0.35f, 0.45f, 0.55f, 1f), bold: true);

            // Clickable info badge on right (Image 3) to toggle Resource Details Side Panel
            var infoBadge = G.Rect("InfoBadge", selectorDetailPane);
            G.Place(infoBadge, w - 46, 24, 34, 34);
            var infoImg = infoBadge.gameObject.AddComponent<Image>();
            infoImg.sprite = GetSelectorTimePillSprite();
            infoImg.type = Image.Type.Sliced;
            infoImg.color = new Color(0.24f, 0.28f, 0.36f, 0.9f);
            var infoIcon = G.Text(infoBadge, "InfoIcon", "≡", skin, 0, 0, 34, 34, 18, Color.white, bold: true);
            infoIcon.alignment = TextAlignmentOptions.Center;

            infoBadge.gameObject.AddComponent<ResourceUIInputBlocker>();
            var infoBtn = infoBadge.gameObject.AddComponent<Button>();
            infoBtn.targetGraphic = infoImg;
            infoBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            infoBtn.onClick.AddListener(ToggleSideDetailPanel);
        }

        public void BuildOptions(List<ResourceUIOption> options)
        {
            SetSideDetailOpen(false);
            G.Clear(selectorContent);
            optionCards.Clear();
            cachedOptions = options ?? new List<ResourceUIOption>();

            var eligibleList = cachedOptions.FindAll(o => o.eligible);
            var lockedList = cachedOptions.FindAll(o => !o.eligible);

            float curY = 0f;
            const float cardW = 80f, cardH = 80f;
            const float spaceX = 16f, spaceY = 14f;
            const int maxCols = 7;

            // ==========================================
            // 1. ELIGIBLE SECTION (Image 2 style)
            // ==========================================
            var bannerEligible = G.Rect("HeaderEligible", selectorContent);
            G.Place(bannerEligible, 0, curY, 210, 30);
            var banEligImg = bannerEligible.gameObject.AddComponent<Image>();
            banEligImg.sprite = GetSelectorPillBannerSprite();
            banEligImg.type = Image.Type.Sliced;
            banEligImg.color = Color.white;
            var banEligText = G.Text(bannerEligible, "Label", "AVAILABLE FOR HARVEST", skin, 0, 0, 210, 30, 13.5f, Color.white, bold: true);
            banEligText.alignment = TextAlignmentOptions.Center;
            curY += 38f;

            if (eligibleList.Count == 0)
            {
                var emptyText = G.Text(selectorContent, "EmptyEligible", "No eligible resources in this habitat.", skin, 12, curY, 700, 26, 13f, new Color(0.40f, 0.50f, 0.60f, 1f));
                curY += 34f;
            }
            else
            {
                for (int i = 0; i < eligibleList.Count; i++)
                {
                    var opt = eligibleList[i];
                    int col = i % maxCols;
                    int row = i / maxCols;
                    float x = 6f + col * (cardW + spaceX);
                    float y = curY + row * (cardH + spaceY);

                    var cardRect = G.Rect("Card_" + opt.id, selectorContent);
                    G.Place(cardRect, x, y, cardW, cardH);
                    var cardBg = cardRect.gameObject.AddComponent<Image>();
                    cardBg.sprite = GetSelectorItemCardSprite();
                    cardBg.type = Image.Type.Sliced;
                    cardBg.color = Color.white;
                    cardRect.gameObject.AddComponent<ResourceUIInputBlocker>();

                    // Bottom accent bar (vibrant purple)
                    var barRect = G.Rect("AccentBar", cardRect);
                    G.Place(barRect, 14, 71, 52, 4);
                    var barImg = barRect.gameObject.AddComponent<Image>();
                    barImg.sprite = GetSelectorAccentBarSprite();
                    barImg.type = Image.Type.Sliced;
                    barImg.color = Color.white;
                    barImg.raycastTarget = false;

                    // Centered resource icon
                    Image iconImg = null;
                    if (opt.icon != null)
                    {
                        iconImg = G.Icon(cardRect, "Icon", opt.icon, 14, 10, 52);
                        iconImg.preserveAspect = true;
                        iconImg.raycastTarget = false;
                    }

                    // Selected glowing border (cyan)
                    var borderRect = G.Rect("SelectedBorder", cardRect);
                    G.Stretch(borderRect);
                    var borderImg = borderRect.gameObject.AddComponent<Image>();
                    borderImg.sprite = GetSelectorCardSelectedSprite();
                    borderImg.type = Image.Type.Sliced;
                    borderImg.color = Color.white;
                    borderImg.raycastTarget = false;
                    borderRect.gameObject.SetActive(false);

                    // Button
                    var btn = cardRect.gameObject.AddComponent<Button>();
                    btn.targetGraphic = cardBg;
                    btn.navigation = new Navigation { mode = Navigation.Mode.None };
                    string targetId = opt.id;
                    btn.onClick.AddListener(() => actions.chooseResource?.Invoke(targetId));

                    optionCards[opt.id] = new OptionCard
                    {
                        id = opt.id,
                        cardBackground = cardBg,
                        border = borderImg,
                        icon = iconImg,
                        button = btn,
                        eligible = true,
                        option = opt
                    };
                }

                int totalRows = Mathf.CeilToInt(eligibleList.Count / (float)maxCols);
                curY += totalRows * (cardH + spaceY) + 4f;
            }

            // ==========================================
            // 2. LOCKED SECTION (Image 2 style)
            // ==========================================
            curY += 8f;
            var bannerLocked = G.Rect("HeaderLocked", selectorContent);
            G.Place(bannerLocked, 0, curY, 714, 34);
            var banLockImg = bannerLocked.gameObject.AddComponent<Image>();
            banLockImg.sprite = GetSelectorLockBannerSprite();
            banLockImg.type = Image.Type.Sliced;
            banLockImg.color = Color.white;

            // Lock icon on banner
            G.Icon(bannerLocked, "LockIcon", skin.iconLock, 10, 7, 20);

            // Lock condition text
            string lockCondition = "Locked · Missing required habitat residents:";
            if (lockedList.Count > 0)
            {
                var reqs = new HashSet<string>();
                foreach (var opt in lockedList)
                {
                    if (!string.IsNullOrEmpty(opt.reason) && opt.reason.Contains(":"))
                    {
                        var parts = opt.reason.Split(':');
                        if (parts.Length > 1) reqs.Add(parts[1].Trim());
                    }
                }
                if (reqs.Count > 0)
                {
                    lockCondition = "Locked · Missing: " + string.Join(", ", reqs);
                }
            }

            var lockLabel = G.Text(bannerLocked, "Label", lockCondition, skin, 36, 0, 665, 34, 13f, Color.white, bold: true);
            lockLabel.alignment = TextAlignmentOptions.MidlineLeft;
            curY += 44f;

            if (lockedList.Count == 0)
            {
                var allUnlockedText = G.Text(selectorContent, "EmptyLocked", "All resources for this biome are currently eligible!", skin, 12, curY, 700, 26, 13f, new Color(0.40f, 0.50f, 0.60f, 1f));
                curY += 34f;
            }
            else
            {
                for (int i = 0; i < lockedList.Count; i++)
                {
                    var opt = lockedList[i];
                    int col = i % maxCols;
                    int row = i / maxCols;
                    float x = 6f + col * (cardW + spaceX);
                    float y = curY + row * (cardH + spaceY);

                    var cardRect = G.Rect("LockedCard_" + opt.id, selectorContent);
                    G.Place(cardRect, x, y, cardW, cardH);
                    var cardBg = cardRect.gameObject.AddComponent<Image>();
                    cardBg.sprite = GetSelectorItemCardSprite();
                    cardBg.type = Image.Type.Sliced;
                    cardBg.color = new Color(0.6f, 0.62f, 0.70f, 0.45f); // Dimmed
                    cardRect.gameObject.AddComponent<ResourceUIInputBlocker>();

                    // Dimmed bottom accent bar
                    var barRect = G.Rect("AccentBar", cardRect);
                    G.Place(barRect, 14, 71, 52, 4);
                    var barImg = barRect.gameObject.AddComponent<Image>();
                    barImg.sprite = GetSelectorAccentBarSprite();
                    barImg.type = Image.Type.Sliced;
                    barImg.color = new Color(0.5f, 0.3f, 0.6f, 0.35f);
                    barImg.raycastTarget = false;

                    // Dimmed resource icon
                    Image iconImg = null;
                    if (opt.icon != null)
                    {
                        iconImg = G.Icon(cardRect, "Icon", opt.icon, 14, 10, 52);
                        iconImg.preserveAspect = true;
                        iconImg.color = new Color(0.75f, 0.75f, 0.8f, 0.4f);
                        iconImg.raycastTarget = false;
                    }

                    // Center Lock Icon overlay (Image 2 style)
                    var lockOverlay = G.Icon(cardRect, "LockOverlay", skin.iconLock, 26, 26, 28);
                    lockOverlay.color = new Color(1f, 1f, 1f, 0.85f);
                    lockOverlay.raycastTarget = false;

                    // Locked button: clicking shows reason in status bar but does not select
                    var btn = cardRect.gameObject.AddComponent<Button>();
                    btn.targetGraphic = cardBg;
                    btn.navigation = new Navigation { mode = Navigation.Mode.None };
                    string reasonMsg = opt.name + ": " + opt.reason;
                    btn.onClick.AddListener(() =>
                    {
                        if (selectorStatus != null) selectorStatus.text = "<color=#ef4444>🔒 " + reasonMsg + "</color>";
                    });

                    optionCards[opt.id] = new OptionCard
                    {
                        id = opt.id,
                        cardBackground = cardBg,
                        border = null,
                        icon = iconImg,
                        button = btn,
                        eligible = false,
                        option = opt
                    };
                }

                int totalRows = Mathf.CeilToInt(lockedList.Count / (float)maxCols);
                curY += totalRows * (cardH + spaceY) + 12f;
            }

            selectorContent.sizeDelta = new Vector2(734, Mathf.Max(268, curY));
        }

        public void UpdateOptions(List<ResourceUIOption> options, string pending, bool canApply, string reason)
        {
            if (options != null) cachedOptions = options;

            ResourceUIOption selectedOpt = null;

            foreach (var kvp in optionCards)
            {
                var card = kvp.Value;
                bool isSelected = (!string.IsNullOrEmpty(pending) && card.id == pending);
                if (card.border != null)
                {
                    card.border.gameObject.SetActive(isSelected);
                }
                if (isSelected)
                {
                    selectedOpt = card.option;
                }
            }

            if (selectedOpt == null && !string.IsNullOrEmpty(pending) && cachedOptions != null)
            {
                selectedOpt = cachedOptions.Find(o => o.id == pending);
            }

            // Update Image 3 Detail Card
            if (selectedOpt != null)
            {
                if (selectorDetailIcon != null)
                {
                    selectorDetailIcon.sprite = selectedOpt.icon;
                    selectorDetailIcon.gameObject.SetActive(selectedOpt.icon != null);
                }
                if (selectorDetailName != null)
                {
                    selectorDetailName.text = selectedOpt.name;
                }
                if (selectorDetailTime != null)
                {
                    selectorDetailTime.text = !string.IsNullOrEmpty(selectedOpt.timeText) ? selectedOpt.timeText : "10s";
                }
                if (selectorDetailTimePill != null)
                {
                    selectorDetailTimePill.SetActive(true);
                }
                if (selectorDetailSub != null)
                {
                    selectorDetailSub.text = selectedOpt.eligible ? "✓ Eligible for automated harvest" : selectedOpt.reason;
                    selectorDetailSub.color = selectedOpt.eligible ? new Color(0.02f, 0.58f, 0.38f, 1f) : new Color(0.85f, 0.15f, 0.25f, 1f);
                }
            }
            else
            {
                if (selectorDetailIcon != null)
                {
                    selectorDetailIcon.sprite = null;
                    selectorDetailIcon.gameObject.SetActive(false);
                }
                if (selectorDetailName != null)
                {
                    selectorDetailName.text = "No Resource Selected";
                }
                if (selectorDetailTimePill != null)
                {
                    selectorDetailTimePill.SetActive(false);
                }
                if (selectorDetailSub != null)
                {
                    selectorDetailSub.text = "Select an available item above to start harvesting.";
                    selectorDetailSub.color = new Color(0.35f, 0.45f, 0.55f, 1f);
                }
            }

            if (selectorStatus != null) selectorStatus.text = reason;
            if (apply != null) apply.interactable = canApply;

            currentSelectedOption = selectedOpt;
            if (isSideDetailOpen && selectedOpt != null)
            {
                UpdateSideDetailPanel(selectedOpt);
            }
        }

        private static RectTransform HorizontalScroll(Transform parent, string name, float x, float y, float w, float h)
        {
            var area = G.Rect(name, parent); G.Place(area, x, y, w, h);
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            var viewport = G.Rect("Viewport", area); G.Stretch(viewport);
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            viewport.gameObject.AddComponent<ResourceUIInputBlocker>();
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = G.Rect("Content", viewport);
            G.Place(content, 0, 0, w, h);
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        private void BuildSideDetailPanel(RectTransform modal)
        {
            sideDetailPanel = G.Rect("SideDetailPanel", modal);
            sideDetailPanel.anchorMin = sideDetailPanel.anchorMax = sideDetailPanel.pivot = new Vector2(0.5f, 0.5f);
            sideDetailPanel.sizeDelta = new Vector2(320, 560);
            sideDetailPanel.anchoredPosition = new Vector2(405f, 0f);

            var panelImg = sideDetailPanel.gameObject.AddComponent<Image>();
            panelImg.sprite = GetSelectorPanelSprite();
            panelImg.type = Image.Type.Sliced;
            panelImg.color = Color.white;
            panelImg.raycastTarget = true;
            sideDetailPanel.gameObject.AddComponent<ResourceUIInputBlocker>();

            // Title Bar
            G.Text(sideDetailPanel, "Title", "RESOURCE DETAILS", skin, 20, 16, 230, 26, 16f, new Color(0.04f, 0.12f, 0.22f, 1f), bold: true);
            G.Round(sideDetailPanel, "Close", skin.iconClose, skin, 320 - 46, 12, 30, () => SetSideDetailOpen(false));

            // Top Colored Frame (h = 146, w = 284)
            var frameRect = G.Rect("ColoredFrame", sideDetailPanel);
            G.Place(frameRect, 18, 48, 284, 146);
            sideDetailBg = frameRect.gameObject.AddComponent<Image>();
            sideDetailBg.type = Image.Type.Simple;
            sideDetailBg.preserveAspect = false;
            sideDetailBg.color = Color.white;
            sideDetailBg.raycastTarget = true;
            frameRect.gameObject.AddComponent<ResourceUIInputBlocker>();

            // Left info in frame
            sideDetailName = G.Text(frameRect, "Name", "Select Item", skin, 14, 12, 170, 40, 17f, Color.white, bold: true);
            sideDetailBiome = G.Text(frameRect, "Biome", "Habitat Resource", skin, 14, 54, 170, 22, 12.5f, new Color(0.92f, 0.98f, 1f, 0.95f), bold: true);
            sideDetailTier = G.Text(frameRect, "Tier", "Rank I", skin, 14, 78, 170, 22, 12f, new Color(0.92f, 0.98f, 1f, 0.85f));
            sideDetailStock = G.Text(frameRect, "Stock", "In Bag: 0", skin, 14, 104, 170, 28, 14.5f, new Color(1f, 0.95f, 0.50f, 1f), bold: true);

            // Right icon in frame
            var iconBox = G.Rect("IconBox", frameRect);
            G.Place(iconBox, 192, 32, 80, 80);
            sideDetailIcon = iconBox.gameObject.AddComponent<Image>();
            sideDetailIcon.preserveAspect = true;
            sideDetailIcon.raycastTarget = false;

            // Harvest Cycle / Time info
            var cycleBox = G.Rect("CycleBox", sideDetailPanel);
            G.Place(cycleBox, 18, 204, 284, 38);
            var cycleBg = cycleBox.gameObject.AddComponent<Image>();
            cycleBg.sprite = GetSelectorTimePillSprite();
            cycleBg.type = Image.Type.Sliced;
            cycleBg.color = new Color(0.93f, 0.96f, 0.99f, 1f);
            cycleBox.gameObject.AddComponent<ResourceUIInputBlocker>();

            G.Icon(cycleBox, "ClockIcon", GetSelectorClockIconSprite(), 10, 9, 20);
            sideDetailTime = G.Text(cycleBox, "Time", "Cycle Time: 10s", skin, 36, 0, 240, 38, 13f, new Color(0.12f, 0.22f, 0.35f, 1f), bold: true);
            sideDetailTime.alignment = TextAlignmentOptions.MidlineLeft;

            // "CAN CRAFT" Section
            G.Text(sideDetailPanel, "CanCraftHeader", "CAN CRAFT", skin, 18, 252, 284, 22, 13f, new Color(0.12f, 0.22f, 0.35f, 1f), bold: true);
            sideDetailRecipesContent = HorizontalScroll(sideDetailPanel, "RecipesScroll", 18, 276, 284, 76);

            // Selected Recipe Info Tip
            sideDetailRecipeTip = G.Text(sideDetailPanel, "RecipeTip", "Click a recipe to see details", skin, 18, 358, 284, 34, 12f, new Color(0.02f, 0.48f, 0.76f, 1f), bold: true);

            // Divider
            var div = G.Rect("Divider", sideDetailPanel);
            G.Place(div, 18, 396, 284, 1.5f);
            var divImg = div.gameObject.AddComponent<Image>();
            divImg.color = new Color(0.82f, 0.88f, 0.94f, 1f);

            // Description Section
            G.Text(sideDetailPanel, "DescHeader", "DESCRIPTION", skin, 18, 404, 284, 20, 12f, new Color(0.40f, 0.50f, 0.60f, 1f), bold: true);
            sideDetailDesc = G.Text(sideDetailPanel, "DescText", "", skin, 18, 428, 284, 116, 12.5f, new Color(0.20f, 0.28f, 0.38f, 1f));
            sideDetailDesc.alignment = TextAlignmentOptions.TopLeft;
            sideDetailDesc.textWrappingMode = TextWrappingModes.Normal;
            sideDetailDesc.overflowMode = TextOverflowModes.Truncate;

            sideDetailPanel.gameObject.SetActive(false);
            isSideDetailOpen = false;
        }

        private void ToggleSideDetailPanel()
        {
            if (!isSideDetailOpen && currentSelectedOption == null)
            {
                if (cachedOptions != null && cachedOptions.Count > 0)
                {
                    currentSelectedOption = cachedOptions.Find(o => o.eligible) ?? cachedOptions[0];
                }
            }
            SetSideDetailOpen(!isSideDetailOpen);
        }

        public void SetSideDetailOpen(bool open)
        {
            isSideDetailOpen = open;
            if (sideDetailPanel != null)
            {
                sideDetailPanel.gameObject.SetActive(open);
            }
            if (mainSelectorPanel != null)
            {
                mainSelectorPanel.anchoredPosition = open ? new Vector2(-165f, 0f) : Vector2.zero;
            }
            if (open && currentSelectedOption != null)
            {
                UpdateSideDetailPanel(currentSelectedOption);
            }
        }

        private void UpdateSideDetailPanel(ResourceUIOption opt)
        {
            if (opt == null) return;

            Sprite bgSprite = HabitatCreaturePanelStyle.GetResourceDetailCardBackground(opt.affinity);
            if (bgSprite != null)
            {
                sideDetailBg.sprite = bgSprite;
                sideDetailBg.type = Image.Type.Simple;
                sideDetailBg.preserveAspect = false;
                sideDetailBg.color = Color.white;
            }
            else
            {
                sideDetailBg.sprite = GetSelectorDetailCardSprite();
                sideDetailBg.type = Image.Type.Sliced;
                sideDetailBg.color = GetAffinityFallbackColor(opt.affinity);
            }

            if (sideDetailIcon != null)
            {
                sideDetailIcon.sprite = opt.icon;
                sideDetailIcon.gameObject.SetActive(opt.icon != null);
            }

            if (sideDetailName != null) sideDetailName.text = opt.name;
            if (sideDetailBiome != null) sideDetailBiome.text = GetAffinityDisplayName(opt.affinity);
            if (sideDetailTier != null)
            {
                sideDetailTier.text = opt.tier == ResourceTier.Tier1 ? "Rank I · Tier 1" : (opt.tier == ResourceTier.Tier2 ? "Rank II · Tier 2" : "Rank III · Tier 3");
            }

            int stock = actions.getStock != null ? actions.getStock(opt.id) : 0;
            if (sideDetailStock != null) sideDetailStock.text = "In Bag: " + stock;

            string timeStr = !string.IsNullOrEmpty(opt.timeText) ? opt.timeText : "10s";
            if (sideDetailTime != null)
            {
                sideDetailTime.text = "Cycle Time: " + timeStr + (opt.eligible ? " (Active)" : " (Locked)");
            }

            if (sideDetailDesc != null)
            {
                sideDetailDesc.text = !string.IsNullOrEmpty(opt.description)
                    ? opt.description
                    : "Raw material harvested from habitat nodes. Can be stored or used in crafting recipes.";
            }

            // Populate craftable recipes
            if (sideDetailRecipesContent != null)
            {
                G.Clear(sideDetailRecipesContent);
                var recipes = new List<CraftingRecipeData>();
                if (CraftingCatalog.All != null)
                {
                    foreach (var r in CraftingCatalog.All)
                    {
                        if (r != null && r.inputs != null)
                        {
                            if (r.inputs.Exists(inp => inp != null && string.Equals(inp.resourceId, opt.id, StringComparison.OrdinalIgnoreCase)))
                            {
                                recipes.Add(r);
                            }
                        }
                    }
                }

                if (recipes.Count == 0)
                {
                    var noRecipe = G.Text(sideDetailRecipesContent, "NoRecipes", "No crafting recipes use this resource yet.", skin, 4, 16, 276, 36, 12f, new Color(0.50f, 0.58f, 0.66f, 1f));
                    noRecipe.alignment = TextAlignmentOptions.Center;
                    sideDetailRecipesContent.sizeDelta = new Vector2(284, 76);
                    if (sideDetailRecipeTip != null) sideDetailRecipeTip.text = "No craftable products available.";
                }
                else
                {
                    float curX = 6f;
                    const float cardW = 56f, cardH = 56f, gapX = 10f;
                    for (int i = 0; i < recipes.Count; i++)
                    {
                        var recipe = recipes[i];
                        var rCard = G.Rect("Recipe_" + recipe.recipeID, sideDetailRecipesContent);
                        G.Place(rCard, curX, 6, cardW, cardH);
                        var rBg = rCard.gameObject.AddComponent<Image>();
                        rBg.sprite = GetSelectorIconBoxSprite();
                        rBg.type = Image.Type.Sliced;
                        rBg.color = Color.white;
                        rCard.gameObject.AddComponent<ResourceUIInputBlocker>();

                        var rIcon = G.Icon(rCard, "Icon", recipe.GetIcon(), 8, 8, 40);
                        rIcon.raycastTarget = false;

                        var rBtn = rCard.gameObject.AddComponent<Button>();
                        rBtn.targetGraphic = rBg;
                        rBtn.navigation = new Navigation { mode = Navigation.Mode.None };
                        var captured = recipe;
                        rBtn.onClick.AddListener(() =>
                        {
                            string engName = GetEnglishRecipeName(captured);
                            string category = captured.category.ToString();
                            if (sideDetailRecipeTip != null)
                            {
                                sideDetailRecipeTip.text = $"<b>{engName}</b> · {category} ({captured.craftDurationSeconds:0}s)";
                            }
                        });

                        curX += cardW + gapX;
                    }
                    sideDetailRecipesContent.sizeDelta = new Vector2(Mathf.Max(284f, curX + 6f), 76);
                    string firstEng = GetEnglishRecipeName(recipes[0]);
                    if (sideDetailRecipeTip != null)
                    {
                        sideDetailRecipeTip.text = $"<b>{firstEng}</b> · {recipes[0].category} ({recipes[0].craftDurationSeconds:0}s)";
                    }
                }
            }
        }

        private static string GetEnglishRecipeName(CraftingRecipeData recipe)
        {
            if (recipe == null) return string.Empty;
            switch (recipe.recipeID)
            {
                case "recipe.food.crispy_tender_leaves": return "Crispy Leaves";
                case "recipe.food.roasted_acorns": return "Roasted Acorns";
                case "recipe.food.dried_raspberries": return "Dried Raspberries";
                case "recipe.food.dried_blueberries": return "Dried Blueberries";
                case "recipe.refined.dried_dandelion": return "Dried Dandelions";
                case "recipe.refined.nectar_essence": return "Nectar Essence";
                case "recipe.refined.wheat_flour": return "Wheat Flour";
                case "recipe.herbal.flower_powder": return "Herbal Flower Powder";
                case "recipe.paper.bark_paper": return "Bark Paper";
                case "recipe.food.dried_wild_fruit_mix": return "Wild Berry Mix";
                case "recipe.food.honey_glazed_acorns": return "Honey Glazed Acorns";
                case "recipe.herbal.forest_syrup": return "Wild Herbal Syrup";
                case "recipe.paper.pressed_flower_paper": return "Pressed Flower Paper";
                case "recipe.paper.scented_paper": return "Scented Paper";
                case "recipe.food.wild_berry_nut_cake": return "Berry Nut Cake";
                default:
                    return !string.IsNullOrEmpty(recipe.recipeName) ? recipe.recipeName : "Crafted Product";
            }
        }

        private static string GetAffinityDisplayName(BiomeAffinity affinity)
        {
            switch (affinity)
            {
                case BiomeAffinity.Leafwood: return "Leafwood Biome";
                case BiomeAffinity.Bloomfield: return "Bloomfield Biome";
                case BiomeAffinity.Windheath: return "Windheath Biome";
                case BiomeAffinity.Reedmarsh: return "Reedmarsh Biome";
                case BiomeAffinity.Rainleaf: return "Rainleaf Biome";
                case BiomeAffinity.Streamstone: return "Streamstone Biome";
                case BiomeAffinity.Lilywater: return "Lilywater Biome";
                default: return "Natural Biome";
            }
        }

        private static Color GetAffinityFallbackColor(BiomeAffinity affinity)
        {
            switch (affinity)
            {
                case BiomeAffinity.Leafwood:
                case BiomeAffinity.Rainleaf:
                    return new Color(0.20f, 0.54f, 0.32f, 1f);
                case BiomeAffinity.Bloomfield:
                    return new Color(0.85f, 0.50f, 0.22f, 1f);
                case BiomeAffinity.Reedmarsh:
                case BiomeAffinity.Lilywater:
                    return new Color(0.18f, 0.50f, 0.78f, 1f);
                case BiomeAffinity.Windheath:
                case BiomeAffinity.Streamstone:
                    return new Color(0.38f, 0.44f, 0.52f, 1f);
                default:
                    return new Color(0.28f, 0.45f, 0.62f, 1f);
            }
        }

        private void BuildSummary()
        {
            SummaryWindow = Modal("PendingSummary", 700, 530, "Safe Stash", "Goods in transit · Not yet added to Warehouse", actions.closeSummary, out var panel);
            summaryContent = G.Scroll(panel, "PendingGoods", 30, 100, 640, 345);
            collectSummary = G.Button(panel, "CollectPending", "Transfer All to Warehouse", skin, 454, 466, 214, 42, () => actions.collectSummary?.Invoke(), true);
        }
        public void BindSummary(List<ResourceUIBuffer> buffers, bool offline)
        {
            var panel = SummaryWindow.Find("Panel"); panel.Find("Title").GetComponent<TextMeshProUGUI>().text = offline ? "Accumulated while offline" : "Safe Stash";
            string key = string.Join("|", buffers.ConvertAll(b => b.habitatId + ":" + b.resourceId));
            if (key != summaryKey)
            {
                summaryKey = key; G.Clear(summaryContent); summaryCounts.Clear(); summaryButtons.Clear(); float y = 0;
                foreach (var buffer in buffers)
                {
                    string rowKey = buffer.habitatId + ":" + buffer.resourceId;
                    var row = G.Rect("Pending_" + rowKey, summaryContent); G.Place(row, 0, y, 620, 70);
                    G.Image(row, skin.surfaceInset, true, skin.insetPPU);
                    G.Icon(row, "Icon", buffer.icon, 12, 11, 48);
                    G.Text(row, "Name", buffer.name, skin, 78, 8, 355, 24, 17, bold: true);
                    summaryCounts[rowKey] = G.Text(row, "PendingAmount", "", skin, 78, 34, 355, 26, 14);
                    summaryButtons[rowKey] = G.Button(row, "Collect", "Collect", skin, 500, 18, 104, 36,
                        () => actions.collectPending?.Invoke(buffer.habitatId, buffer.resourceId), true);
                    y += 80;
                }
                if (buffers.Count == 0) G.Text(summaryContent, "Empty", "No pending resources.", skin, 0, 20, 600, 60, 17);
                summaryContent.sizeDelta = new Vector2(630, Mathf.Max(345, y));
            }
            foreach (var buffer in buffers)
            {
                string keyId = buffer.habitatId + ":" + buffer.resourceId;
                summaryCounts[keyId].text = "Pending: " + buffer.amount; summaryButtons[keyId].interactable = buffer.amount > 0;
            }
            collectSummary.interactable = buffers.Exists(b => b.amount > 0);
        }

        public void ShowReceipt(string message, List<ResourceUIBuffer> transferred)
        {
            G.Clear(Receipt); float height = 80 + Mathf.CeilToInt(transferred.Count / 3f) * 32;
            Receipt.sizeDelta = new Vector2(470, height);
            G.Text(Receipt, "Message", message, skin, 20, 12, 400, 54, 15, bold: true);
            G.Round(Receipt, "Close", skin.iconClose, skin, 430, 12, 25, () => actions.closeReceipt?.Invoke());
            for (int i = 0; i < transferred.Count; i++)
            {
                var item = transferred[i]; float x = 22 + i % 3 * 143, y = 69 + i / 3 * 32;
                G.Icon(Receipt, "Icon_" + item.resourceId, item.icon, x, y, 27);
                G.Text(Receipt, "Received_" + item.resourceId, "+" + item.amount, skin, x + 33, y, 104, 27, 17, bold: true);
            }
            Receipt.gameObject.SetActive(true); Receipt.SetAsLastSibling();
        }
    }
}

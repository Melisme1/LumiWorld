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
        public Action openWarehouse, openCrafting, toggleHabitat, closeHabitat, previousHabitat, nextHabitat;
        public Action closeWarehouse, closeSelector, applySelection, openRecovery;
        public Action collectHabitat, closeSummary, collectSummary, closeReceipt;
        public Action<ResourceTier, int> openSelector;
        public Action<string> chooseResource, collectResource;
        public Action<string, string> collectPending;
        public Func<string, int> getStock;
    }

    public sealed class ResourceInventoryUIView
    {
        public const float WindowWidth = 1080f;
        public const float WindowHeight = 640f;

        private readonly RectTransform root;
        private readonly ResourceInventoryUISkin skin;
        private readonly ResourceInventoryUIActions actions;

        public RectTransform WarehouseWindow { get; private set; }
        public RectTransform SelectorWindow { get; private set; }
        public RectTransform SummaryWindow { get; private set; }
        public RectTransform Receipt { get; private set; }

        private RectTransform warehousePanel, warehouseContent, selectorContent, summaryContent, detailPane;
        private TextMeshProUGUI detailName, detailMeta, detailDescription, warehouseEmptyText, selectorStatus;
        private Image detailIcon, detailSourceIcon, detailArtBg;
        private Button detailArtButton, recovery, apply, collectSummary;
        private RectTransform recipePopover;

        private readonly Dictionary<string, TextMeshProUGUI> warehouseCounts = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Image> warehouseBorders = new Dictionary<string, Image>();
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
        private RectTransform sideDetailRecipesContent;
        private TextMeshProUGUI sideDetailRecipeTip;
        private TextMeshProUGUI sideDetailDesc;
        private string selectorRecipeResourceId;

        private readonly Dictionary<string, TextMeshProUGUI> summaryCounts = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Button> summaryButtons = new Dictionary<string, Button>();
        private string summaryKey, selectedWarehouseId;
        private string activeWarehouseTab = "Ingredients";
        private readonly List<(string name, Button btn, Image bg, Image border, Image icon, TextMeshProUGUI lbl)> warehouseTabs =
            new List<(string, Button, Image, Image, Image, TextMeshProUGUI)>();
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
            this.root = root;
            this.skin = skin;
            this.actions = actions;

            // Only show standalone Warehouse button in Visit mode where CollapsibleHUDToolbar is absent
            if (WorldLaunch.IsVisit || VisitSession.Active)
            {
                G.Round(root, "WarehouseButton", LumiWorldUITheme.Glyph("G-01") ?? skin.iconWarehouse, skin, 24, 18, 48, () => actions.openWarehouse?.Invoke());
            }

            BuildWarehouse();
            BuildSelector();
            BuildSummary();

            Receipt = G.Rect("HarvestReceipt", root);
            G.Place(Receipt, 240, 20, 470, 100);
            G.Image(Receipt, LumiWorldUITheme.SurfaceSprite, true, 1f, true);
            Receipt.gameObject.SetActive(false);
        }

        public void Layout(float width, float height)
        {
            G.Place(Receipt, (width - 470f) * 0.5f, 18f, 470f, Receipt.sizeDelta.y);
        }

        private RectTransform Modal(string name, float width, float height, string title, string subtitle, Action close, out RectTransform panel)
        {
            var modal = G.Rect(name, root);
            G.Stretch(modal);
            var dimmer = G.Rect("Dimmer", modal);
            G.Stretch(dimmer);
            var dim = dimmer.gameObject.AddComponent<Image>();
            dim.color = LumiWorldUITheme.Backdrop;
            dim.raycastTarget = true;
            dimmer.gameObject.AddComponent<ResourceUIInputBlocker>();
            var dimBtn = dimmer.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            if (close != null) dimBtn.onClick.AddListener(() => close());

            panel = G.Rect("Panel", modal);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(width, height);
            var panelImg = G.Image(panel, LumiWorldUITheme.SurfaceSprite, true, 1f, true);
            panelImg.color = LumiWorldUITheme.Window;
            AddOutline(panel, LumiWorldUITheme.Edge);

            G.Text(panel, "Title", title, skin, 30, 19, width - 116, 38, 24, LumiWorldUITheme.Text, bold: true);
            if (!string.IsNullOrEmpty(subtitle))
                G.Text(panel, "Subtitle", subtitle, skin, 30, 56, width - 80, 22, 14, LumiWorldUITheme.Muted);
            G.Round(panel, "Close", LumiWorldUITheme.Glyph("Close") ?? skin.iconClose, skin, width - 64, 18, 38, () => close?.Invoke());
            modal.gameObject.SetActive(false);
            return modal;
        }

        // =========================================================
        // P-00 INVENTORY WINDOW
        // =========================================================

        private void BuildWarehouse()
        {
            var modal = G.Rect("WarehouseWindow", root);
            G.Stretch(modal);

            var dimmer = G.Rect("Dimmer", modal);
            G.Stretch(dimmer);
            var dim = dimmer.gameObject.AddComponent<Image>();
            dim.color = LumiWorldUITheme.Backdrop;
            dim.raycastTarget = true;
            dimmer.gameObject.AddComponent<ResourceUIInputBlocker>();
            var dimBtn = dimmer.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            dimBtn.onClick.AddListener(() => actions.closeWarehouse?.Invoke());

            var panel = G.Rect("Panel", modal);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);
            warehousePanel = panel;

            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.sprite = LumiWorldUITheme.SurfaceSprite;
            panelImg.type = Image.Type.Sliced;
            panelImg.color = LumiWorldUITheme.Window;
            panelImg.raycastTarget = true;
            panel.gameObject.AddComponent<ResourceUIInputBlocker>();
            LumiWorldUITheme.AddSoftElevation(panel.gameObject, 6f, 0.22f);
            AddOutline(panel, LumiWorldUITheme.Edge);

            // Top Header Pill: [G-01 Inventory] only (linking to Crafting happens via Recipe Popover 'Open')
            BuildTopModePill(panel, "HeaderInventoryBtn", "Inventory", LumiWorldUITheme.Glyph("G-01"), 28f, 14f, 216f, 50f, true, null);

            // Optional Safe Stash recovery pill (only shown when recoveryAmount > 0)
            recovery = BuildHeaderActionPill(panel, "SafeStashBtn", "Safe Stash (0)", WindowWidth - 260f, 16f, 176f, 46f, () => actions.openRecovery?.Invoke());
            recovery.gameObject.SetActive(false);

            // Close [×] button
            var closeBtn = G.Round(panel, "Close", LumiWorldUITheme.Glyph("Close") ?? skin.iconClose, skin, WindowWidth - 72f, 16f, 46f, () => actions.closeWarehouse?.Invoke());
            LumiWorldUITheme.AddSoftElevation(closeBtn.gameObject, 2.5f, 0.14f);

            // Header Divider
            var divider = G.Rect("HeaderDivider", panel);
            G.Place(divider, 28f, 74f, WindowWidth - 56f, 2f);
            divider.gameObject.AddComponent<Image>().color = LumiWorldUITheme.Inset;

            // Category Tabs: Ingredients / Food / Medicine / Products
            BuildWarehouseTabs(panel);

            // Subtle recessed Grid Well behind the 6-column Item Grid (matching P-00 mockup depth)
            var gridWell = G.Rect("ResourceGridWell", panel);
            G.Place(gridWell, 24f, 146f, 676f, 468f);
            var gridWellImg = gridWell.gameObject.AddComponent<Image>();
            gridWellImg.sprite = LumiWorldUITheme.SurfaceSprite;
            gridWellImg.type = Image.Type.Sliced;
            gridWellImg.color = LumiWorldUITheme.Well;
            gridWellImg.raycastTarget = false;

            // Left 6-column Item Grid
            warehouseContent = G.Scroll(panel, "ResourceGrid", 28f, 150f, 668f, 460f);

            // Right Detail Card (P-00)
            detailPane = G.Rect("ResourceDetails", panel);
            G.Place(detailPane, 712f, 86f, 340f, 528f);
            var detailBg = detailPane.gameObject.AddComponent<Image>();
            detailBg.sprite = LumiWorldUITheme.SurfaceSprite;
            detailBg.type = Image.Type.Sliced;
            detailBg.color = LumiWorldUITheme.Surface;
            LumiWorldUITheme.AddSoftElevation(detailPane.gameObject, 4f, 0.15f);

            // Empty selection notice inside detail card
            warehouseEmptyText = G.Text(detailPane, "EmptyDetail", "Select an item to view details", skin, 24f, 230f, 292f, 60f, 16f, LumiWorldUITheme.Muted, bold: true);
            warehouseEmptyText.alignment = TextAlignmentOptions.Center;

            // Large Artwork Box (clickable when item has recipes -> opens Recipe Popover directly below artwork)
            var art = G.Rect("DetailIconBox", detailPane);
            G.Place(art, 22f, 20f, 296f, 212f);
            detailArtBg = art.gameObject.AddComponent<Image>();
            detailArtBg.sprite = LumiWorldUITheme.SurfaceSprite;
            detailArtBg.type = Image.Type.Sliced;
            detailArtBg.color = LumiWorldUITheme.Window;

            detailIcon = G.Icon(art, "Icon", null, 62f, 20f, 172f);
            detailIcon.preserveAspect = true;
            detailIcon.raycastTarget = false;

            detailArtButton = art.gameObject.AddComponent<Button>();
            detailArtButton.targetGraphic = detailArtBg;
            detailArtButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var artColors = detailArtButton.colors;
            artColors.normalColor = Color.white;
            artColors.highlightedColor = new Color(0.95f, 0.94f, 0.91f, 1f);
            artColors.pressedColor = new Color(0.88f, 0.86f, 0.82f, 1f);
            artColors.disabledColor = Color.white;
            detailArtButton.colors = artColors;
            detailArtButton.onClick.AddListener(OpenSelectedRecipe);

            // Item Name (bold, large, no "owned" count on right per P-00)
            detailName = G.Text(detailPane, "Name", "", skin, 24f, 248f, 292f, 40f, 26f, LumiWorldUITheme.Text, bold: true);
            detailName.alignment = TextAlignmentOptions.MidlineLeft;
            detailName.enableAutoSizing = true;
            detailName.fontSizeMin = 18f;
            detailName.fontSizeMax = 26f;

            // Source / Category Row
            var sourceRow = G.Rect("SourceRow", detailPane);
            G.Place(sourceRow, 24f, 294f, 292f, 30f);
            detailSourceIcon = G.Icon(sourceRow, "SourceIcon", null, 0f, 2f, 26f);
            detailSourceIcon.preserveAspect = true;
            detailSourceIcon.raycastTarget = false;

            detailMeta = G.Text(sourceRow, "Source", "", skin, 34f, 0f, 258f, 30f, 15.5f, LumiWorldUITheme.Muted, bold: true);
            detailMeta.alignment = TextAlignmentOptions.MidlineLeft;

            // Divider
            var detailDiv = G.Rect("DetailDivider", detailPane);
            G.Place(detailDiv, 24f, 338f, 292f, 2f);
            detailDiv.gameObject.AddComponent<Image>().color = LumiWorldUITheme.Inset;

            // Description
            detailDescription = G.Text(detailPane, "Description", "", skin, 24f, 354f, 292f, 150f, 15f, LumiWorldUITheme.Muted, bold: false);
            detailDescription.alignment = TextAlignmentOptions.TopLeft;
            detailDescription.textWrappingMode = TextWrappingModes.Normal;

            SetDetailContentVisible(false);

            modal.gameObject.SetActive(false);
            WarehouseWindow = modal;
        }

        private Button BuildTopModePill(Transform parent, string name, string label, Sprite glyph, float x, float y, float w, float h, bool active, UnityEngine.Events.UnityAction onClick)
        {
            var rect = G.Rect(name, parent);
            G.Place(rect, x, y, w, h);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = LumiWorldUITheme.PillSprite;
            bg.type = Image.Type.Sliced;
            bg.color = active ? LumiWorldUITheme.Primary : LumiWorldUITheme.Inset;
            bg.raycastTarget = true;
            rect.gameObject.AddComponent<ResourceUIInputBlocker>();
            LumiWorldUITheme.AddSoftElevation(rect.gameObject, 3f, 0.16f);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.94f, 0.92f, 1f);
            colors.pressedColor = new Color(0.86f, 0.85f, 0.82f, 1f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);

            if (glyph != null)
            {
                var ic = G.Icon(rect, "Glyph", glyph, 18f, (h - 28f) * 0.5f, 28f);
                ic.color = active ? Color.white : LumiWorldUITheme.Text;
                ic.raycastTarget = false;
            }

            var txt = G.Text(rect, "Label", label, skin, glyph != null ? 54f : 14f, 0f, w - (glyph != null ? 64f : 28f), h, 20f, active ? Color.white : LumiWorldUITheme.Text, bold: true);
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            return btn;
        }

        private Button BuildHeaderActionPill(Transform parent, string name, string label, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            var rect = G.Rect(name, parent);
            G.Place(rect, x, y, w, h);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = LumiWorldUITheme.PillSprite;
            bg.type = Image.Type.Sliced;
            bg.color = LumiWorldUITheme.Inset;
            bg.raycastTarget = true;
            rect.gameObject.AddComponent<ResourceUIInputBlocker>();
            LumiWorldUITheme.AddSoftElevation(rect.gameObject, 2.5f, 0.13f);

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) btn.onClick.AddListener(onClick);

            var txt = G.Text(rect, "Label", label, skin, 10f, 0f, w - 20f, h, 15.5f, LumiWorldUITheme.Text, bold: true);
            txt.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        private static Image AddOutline(RectTransform parent, Color color)
        {
            var outlineRect = G.Rect("Outline", parent);
            G.Stretch(outlineRect);
            var img = outlineRect.gameObject.AddComponent<Image>();
            img.sprite = LumiWorldUITheme.BorderSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void BuildWarehouseTabs(RectTransform panel)
        {
            warehouseTabs.Clear();
            var tabs = new[]
            {
                ("Ingredients", 176f, "G-15"),
                ("Food", 142f, "G-16"),
                ("Medicine", 166f, "G-17"),
                ("Products", 166f, "G-18")
            };
            float curX = 24f;
            for (int i = 0; i < tabs.Length; i++)
            {
                string tabName = tabs[i].Item1;
                float tabW = tabs[i].Item2;
                string glyphId = tabs[i].Item3;

                var tabRect = G.Rect("Tab_" + tabName, panel);
                G.Place(tabRect, curX, 86f, tabW, 48f);

                var bg = tabRect.gameObject.AddComponent<Image>();
                bg.sprite = LumiWorldUITheme.PillSprite;
                bg.type = Image.Type.Sliced;
                bg.raycastTarget = true;
                tabRect.gameObject.AddComponent<ResourceUIInputBlocker>();
                LumiWorldUITheme.AddSoftElevation(tabRect.gameObject, 2.5f, 0.13f);

                var btn = tabRect.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };

                var icon = G.Icon(tabRect, "Glyph", LumiWorldUITheme.Glyph(glyphId), 16f, 10f, 28f);
                icon.raycastTarget = false;

                var lbl = G.Text(tabRect, "Label", tabName, skin, 50f, 0f, tabW - 58f, 48f, 16.5f, LumiWorldUITheme.Text, bold: true);
                lbl.alignment = TextAlignmentOptions.MidlineLeft;
                lbl.raycastTarget = false;

                string captured = tabName;
                btn.onClick.AddListener(() => SelectWarehouseTab(captured));

                warehouseTabs.Add((tabName, btn, bg, null, icon, lbl));
                curX += tabW + 9f;
            }
            UpdateWarehouseTabVisuals();
        }

        private void SetDetailContentVisible(bool hasSelection)
        {
            if (warehouseEmptyText != null) warehouseEmptyText.gameObject.SetActive(!hasSelection);
            if (detailArtButton != null) detailArtButton.gameObject.SetActive(hasSelection);
            if (detailName != null) detailName.gameObject.SetActive(hasSelection);
            if (detailMeta != null && detailMeta.transform.parent != null)
                detailMeta.transform.parent.gameObject.SetActive(hasSelection);
            if (detailDescription != null) detailDescription.gameObject.SetActive(hasSelection);
        }

        public void ResetSelection()
        {
            selectedWarehouseId = null;
            SetDetailContentVisible(false);
            foreach (var pair in warehouseBorders)
                if (pair.Value != null) pair.Value.gameObject.SetActive(false);
        }

        private void SelectWarehouseTab(string tabName)
        {
            CloseRecipePopover();
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
                tab.bg.color = active ? LumiWorldUITheme.Slot : LumiWorldUITheme.Inset;
                if (tab.border != null) tab.border.gameObject.SetActive(active);
                tab.lbl.color = active ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted;
                if (tab.icon != null)
                    tab.icon.color = active ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted;
            }
        }

        public void BuildWarehouseItems(List<ResourceUIItem> items)
        {
            allWarehouseItems = items ?? new List<ResourceUIItem>();
            RenderWarehouseGrid();
        }

        public void FocusWarehouseResource(List<ResourceUIItem> items, string resourceId)
        {
            allWarehouseItems = items ?? new List<ResourceUIItem>();
            var found = allWarehouseItems.Find(i => string.Equals(i.id, resourceId, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                activeWarehouseTab = string.Equals(found.category, "Resource", StringComparison.OrdinalIgnoreCase)
                    ? "Ingredients"
                    : found.category;
                selectedWarehouseId = found.id;
            }
            UpdateWarehouseTabVisuals();
            RenderWarehouseGrid();
        }

        private void RenderWarehouseGrid()
        {
            List<ResourceUIItem> filtered;
            if (string.Equals(activeWarehouseTab, "Ingredients", StringComparison.OrdinalIgnoreCase))
                filtered = allWarehouseItems.FindAll(i => string.Equals(i.category, "Resource", StringComparison.OrdinalIgnoreCase));
            else if (string.Equals(activeWarehouseTab, "Food", StringComparison.OrdinalIgnoreCase))
                filtered = allWarehouseItems.FindAll(i => string.Equals(i.category, "Food", StringComparison.OrdinalIgnoreCase));
            else if (string.Equals(activeWarehouseTab, "Medicine", StringComparison.OrdinalIgnoreCase))
                filtered = allWarehouseItems.FindAll(i => string.Equals(i.category, "Medicine", StringComparison.OrdinalIgnoreCase));
            else if (string.Equals(activeWarehouseTab, "Products", StringComparison.OrdinalIgnoreCase))
                filtered = allWarehouseItems.FindAll(i => string.Equals(i.category, "Products", StringComparison.OrdinalIgnoreCase));
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
                G.Place(emptyRect, 20f, 140f, 620f, 100f);
                var emptyTxt = emptyRect.gameObject.AddComponent<TextMeshProUGUI>();
                emptyTxt.font = LumiWorldUITheme.Body ?? skin.font;
                emptyTxt.fontSize = 16f;
                emptyTxt.fontStyle = FontStyles.Bold;
                emptyTxt.alignment = TextAlignmentOptions.Center;
                emptyTxt.color = LumiWorldUITheme.Muted;

                if (string.Equals(activeWarehouseTab, "Food", StringComparison.OrdinalIgnoreCase))
                    emptyTxt.text = "No food in inventory.\nCraft meals for your creatures in Crafting.";
                else if (string.Equals(activeWarehouseTab, "Medicine", StringComparison.OrdinalIgnoreCase))
                    emptyTxt.text = "No medicine in inventory.\nCraft herbal remedies in Crafting.";
                else if (string.Equals(activeWarehouseTab, "Products", StringComparison.OrdinalIgnoreCase))
                    emptyTxt.text = "No crafted products in inventory.\nCraft refined goods and paper in Crafting.";
                else
                    emptyTxt.text = "No raw ingredients collected yet.\nAssign creatures to habitats to harvest resources.";

                return;
            }

            const int cols = 6;
            const float slotW = 98f;
            const float slotH = 100f;
            const float gapX = 11f;
            const float gapY = 12f;
            const float startX = 6f;
            const float startY = 6f;

            for (int i = 0; i < filtered.Count; i++)
            {
                var item = filtered[i];
                float x = startX + (i % cols) * (slotW + gapX);
                float y = startY + (i / cols) * (slotH + gapY);

                var rect = G.Rect("Item_" + item.id, warehouseContent);
                G.Place(rect, x, y, slotW, slotH);

                var slotImg = rect.gameObject.AddComponent<Image>();
                slotImg.sprite = LumiWorldUITheme.SurfaceSprite;
                slotImg.type = Image.Type.Sliced;
                slotImg.color = LumiWorldUITheme.Slot;
                slotImg.raycastTarget = true;
                rect.gameObject.AddComponent<ResourceUIInputBlocker>();
                LumiWorldUITheme.AddSoftElevation(rect.gameObject, 3.5f, 0.16f);

                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = slotImg;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                string itemId = item.id;
                button.onClick.AddListener(() => SelectWarehouse(itemId));

                if (item.icon != null)
                {
                    var ic = G.Icon(rect, "ResourceIcon", item.icon, 15f, 8f, 68f);
                    ic.preserveAspect = true;
                    ic.raycastTarget = false;
                }

                var countTxt = G.Text(rect, "CountBox", item.stock.ToString(), skin, 4f, 70f, slotW - 20f, 24f, 15f, LumiWorldUITheme.Text, bold: true);
                countTxt.alignment = TextAlignmentOptions.BottomRight;
                countTxt.raycastTarget = false;
                warehouseCounts[item.id] = countTxt;

                // Selected dark graphite border (no checkmark per P-00)
                var selRect = G.Rect("Selected", rect);
                G.Stretch(selRect);
                var selImg = selRect.gameObject.AddComponent<Image>();
                selImg.sprite = GetSelectorOptionSelectedSprite();
                selImg.type = Image.Type.Sliced;
                selImg.color = Color.white;
                selImg.raycastTarget = false;
                selRect.gameObject.SetActive(false);
                warehouseBorders[item.id] = selImg;
            }

            warehouseContent.sizeDelta = new Vector2(656f, Mathf.Max(452f, startY * 2f + Mathf.CeilToInt(filtered.Count / (float)cols) * (slotH + gapY)));

            if (!string.IsNullOrEmpty(selectedWarehouseId) && filtered.Exists(i => i.id == selectedWarehouseId))
                SelectWarehouse(selectedWarehouseId);
            else
                SelectWarehouse(filtered[0].id);
        }

        public void UpdateWarehouse(List<ResourceUIItem> items, long recoveryAmount)
        {
            allWarehouseItems = items ?? new List<ResourceUIItem>();
            foreach (var item in allWarehouseItems)
            {
                if (warehouseCounts.TryGetValue(item.id, out var label))
                {
                    label.text = item.stock.ToString();
                    label.color = item.stock >= item.capacity ? LumiWorldUITheme.Muted : LumiWorldUITheme.Text;
                }
            }
            if (recovery != null)
            {
                recovery.gameObject.SetActive(recoveryAmount > 0);
                recovery.interactable = recoveryAmount > 0;
                var recTxt = recovery.GetComponentInChildren<TextMeshProUGUI>();
                if (recTxt != null) recTxt.text = $"Safe Stash ({recoveryAmount})";
            }
            if (!string.IsNullOrEmpty(selectedWarehouseId) && warehouseItems != null && warehouseItems.Exists(i => i.id == selectedWarehouseId))
            {
                SelectWarehouse(selectedWarehouseId);
            }
            else if (warehouseItems != null && warehouseItems.Count > 0)
            {
                SelectWarehouse(warehouseItems[0].id);
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

            SetDetailContentVisible(true);

            if (detailIcon != null)
            {
                detailIcon.sprite = item.icon;
                detailIcon.color = item.icon != null ? Color.white : Color.clear;
            }
            if (detailName != null) detailName.text = item.name;

            bool isCrafted = item.category == "Food" || item.category == "Medicine" || item.category == "Products" ||
                             item.biome == "Workshop" || item.biome == "Crafting";
            string sourceLabel = isCrafted ? "Crafting · " + activeWarehouseTab : item.biome + " · " + activeWarehouseTab;
            if (detailMeta != null) detailMeta.text = sourceLabel;
            if (detailSourceIcon != null)
            {
                detailSourceIcon.sprite = isCrafted ? LumiWorldUITheme.Glyph("G-02") : LumiWorldUITheme.BiomeEmblem(item.biome);
                detailSourceIcon.color = detailSourceIcon.sprite != null
                    ? (isCrafted ? LumiWorldUITheme.Muted : Color.white)
                    : Color.clear;
            }
            if (detailDescription != null) detailDescription.text = item.description;

            bool hasRecipes = HasAnyRecipes(item.id);
            if (detailArtButton != null) detailArtButton.interactable = hasRecipes;
        }

        private static bool HasAnyRecipes(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId) || CraftingCatalog.All == null) return false;
            if (CraftingCatalog.FindByOutput(resourceId) != null) return true;
            foreach (var recipe in CraftingCatalog.All)
            {
                if (recipe != null && recipe.inputs != null &&
                    recipe.inputs.Exists(input => input != null && string.Equals(input.resourceId, resourceId, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            return false;
        }

        private static List<CraftingRecipeData> UsedInRecipesFor(string resourceId)
        {
            var result = new List<CraftingRecipeData>();
            if (string.IsNullOrEmpty(resourceId) || CraftingCatalog.All == null) return result;
            foreach (var recipe in CraftingCatalog.All)
            {
                if (recipe != null && recipe.inputs != null &&
                    recipe.inputs.Exists(input => input != null && string.Equals(input.resourceId, resourceId, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(recipe);
                }
            }
            return result;
        }

        private void OpenSelectedRecipe()
        {
            if (string.IsNullOrEmpty(selectedWarehouseId) || warehousePanel == null) return;

            var craftedBy = CraftingCatalog.FindByOutput(selectedWarehouseId);
            var usedIn = UsedInRecipesFor(selectedWarehouseId);
            bool hasCraftedBy = craftedBy != null && craftedBy.inputs != null && craftedBy.inputs.Count > 0;
            bool hasUsedIn = usedIn.Count > 0;
            if (!hasCraftedBy && !hasUsedIn) return;

            CloseRecipePopover();

            var item = warehouseItems?.Find(i => i.id == selectedWarehouseId);
            string itemTitle = item != null ? item.name : "Recipes";

            recipePopover = G.Rect("RecipePopoverOverlay", warehousePanel);
            G.Stretch(recipePopover);
            var dismissImg = recipePopover.gameObject.AddComponent<Image>();
            dismissImg.color = new Color(0.14f, 0.12f, 0.10f, 0.16f);
            dismissImg.raycastTarget = true;
            var dismissBtn = recipePopover.gameObject.AddComponent<Button>();
            dismissBtn.transition = Selectable.Transition.None;
            dismissBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            dismissBtn.onClick.AddListener(CloseRecipePopover);

            // Calculate content height for the 2 distinct sections:
            // Section 1: "Crafted by" (ingredients formula to craft this item; hidden if uncraftable)
            // Section 2: "Used in" (output items crafted using this item; hidden if not used in any recipe)
            float contentH = 0f;
            if (hasCraftedBy)
                contentH += 26f + craftedBy.inputs.Count * 54f;
            if (hasCraftedBy && hasUsedIn)
                contentH += 8f;
            if (hasUsedIn)
                contentH += 26f + usedIn.Count * 58f;

            // Position directly below the horizontal center of DetailIconBox (center x = 882, bottom y = 318)
            float popW = 340f;
            float listH = Mathf.Clamp(contentH, 74f, 224f);
            float popH = 66f + listH;
            float cardX = 882f - popW * 0.5f; // 712f (aligned with detailPane and centered under artwork)
            float cardY = 320f;               // immediately below DetailIconBox

            var card = G.Rect("PopoverCard", recipePopover);
            G.Place(card, cardX, cardY, popW, popH);
            var cardBg = card.gameObject.AddComponent<Image>();
            cardBg.sprite = LumiWorldUITheme.SurfaceSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = LumiWorldUITheme.Surface;
            cardBg.raycastTarget = true;
            card.gameObject.AddComponent<ResourceUIInputBlocker>();
            LumiWorldUITheme.AddSoftElevation(card.gameObject, 5f, 0.22f);
            AddOutline(card, LumiWorldUITheme.Edge);

            var titleLbl = G.Text(card, "Title", itemTitle, skin, 18f, 14f, popW - 68f, 28f, 18.5f, LumiWorldUITheme.Text, bold: true);
            titleLbl.enableAutoSizing = true;
            titleLbl.fontSizeMin = 14f;
            titleLbl.fontSizeMax = 18.5f;
            G.Round(card, "Close", LumiWorldUITheme.Glyph("Close") ?? skin.iconClose, skin, popW - 44f, 12f, 30f, CloseRecipePopover);

            var list = G.Scroll(card, "RecipesList", 14f, 52f, popW - 28f, listH);
            float rowW = popW - 36f;
            float curY = 0f;

            void OpenRecipeInCrafting(CraftingRecipeData targetRecipe)
            {
                if (targetRecipe == null) return;
                var crafting = UnityEngine.Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);
                if (crafting == null) return;
                CloseRecipePopover();
                actions.closeWarehouse?.Invoke();
                crafting.OpenRecipe(targetRecipe);
            }

            // Section 1: Crafted by (formula / required ingredients) — hidden if item cannot be crafted
            if (hasCraftedBy)
            {
                var headerRect = G.Rect("CraftedByHeader", list);
                G.Place(headerRect, 2f, curY, rowW - 4f, 22f);
                var hdrLbl = G.Text(headerRect, "Lbl", "Crafted by:", skin, 0f, 0f, 180f, 22f, 13.5f, LumiWorldUITheme.Muted, bold: true);
                hdrLbl.alignment = TextAlignmentOptions.MidlineLeft;

                var openRecipeBtnRect = G.Rect("OpenCraftedByBtn", headerRect);
                G.Place(openRecipeBtnRect, rowW - 110f, 0f, 106f, 22f);
                var openRecipeTxt = G.Text(openRecipeBtnRect, "OpenTxt", "Open ›", skin, 0f, 0f, 106f, 22f, 13.5f, LumiWorldUITheme.Primary, bold: true);
                openRecipeTxt.alignment = TextAlignmentOptions.MidlineRight;
                openRecipeTxt.raycastTarget = true;
                var hdrBtn = openRecipeBtnRect.gameObject.AddComponent<Button>();
                hdrBtn.targetGraphic = openRecipeTxt;
                hdrBtn.navigation = new Navigation { mode = Navigation.Mode.None };
                var capturedCraftedBy = craftedBy;
                hdrBtn.onClick.AddListener(() => OpenRecipeInCrafting(capturedCraftedBy));

                curY += 26f;

                for (int i = 0; i < craftedBy.inputs.Count; i++)
                {
                    var inp = craftedBy.inputs[i];
                    if (inp == null) continue;
                    ResourceData inRes = CraftingCatalog.GetResource(inp.resourceId);
                    string rawInName = inRes != null && !string.IsNullOrEmpty(inRes.resourceName) ? inRes.resourceName : inp.resourceId;
                    string inName = ResourceInventoryUIPresentation.LocalizeResourceName(inp.resourceId, rawInName);
                    Sprite inIcon = inRes != null ? inRes.icon : null;

                    var row = G.Rect("CraftedByIng_" + inp.resourceId, list);
                    G.Place(row, 0f, curY, rowW, 48f);
                    var rowBg = row.gameObject.AddComponent<Image>();
                    rowBg.sprite = LumiWorldUITheme.SurfaceSprite;
                    rowBg.type = Image.Type.Sliced;
                    rowBg.color = LumiWorldUITheme.Window;
                    rowBg.raycastTarget = true;

                    if (inIcon != null)
                    {
                        var ic = G.Icon(row, "Icon", inIcon, 10f, 6f, 36f);
                        ic.preserveAspect = true;
                        ic.raycastTarget = false;
                    }

                    var nameTxt = G.Text(row, "Name", inName, skin, 54f, 4f, rowW - 122f, 22f, 15f, LumiWorldUITheme.Text, bold: true);
                    nameTxt.enableAutoSizing = true;
                    nameTxt.fontSizeMin = 12f;
                    nameTxt.fontSizeMax = 15f;

                    G.Text(row, "Req", $"Ingredient · ×{inp.amount}", skin, 54f, 24f, rowW - 122f, 18f, 12.5f, LumiWorldUITheme.Muted);

                    var openLbl = G.Text(row, "OpenHint", "Open ›", skin, rowW - 68f, 0f, 56f, 48f, 13.5f, LumiWorldUITheme.Primary, bold: true);
                    openLbl.alignment = TextAlignmentOptions.MidlineRight;

                    var btn = row.gameObject.AddComponent<Button>();
                    btn.targetGraphic = rowBg;
                    btn.navigation = new Navigation { mode = Navigation.Mode.None };
                    btn.onClick.AddListener(() => OpenRecipeInCrafting(capturedCraftedBy));

                    curY += 54f;
                }
            }

            if (hasCraftedBy && hasUsedIn)
                curY += 8f;

            // Section 2: Used in (recipes that consume this item) — hidden if not used in any recipe
            if (hasUsedIn)
            {
                var usedHdr = G.Text(list, "UsedInHeader", "Used in:", skin, 2f, curY, rowW - 4f, 22f, 13.5f, LumiWorldUITheme.Muted, bold: true);
                usedHdr.alignment = TextAlignmentOptions.MidlineLeft;
                curY += 26f;

                for (int i = 0; i < usedIn.Count; i++)
                {
                    var recipe = usedIn[i];
                    var row = G.Rect("UsedIn_" + recipe.recipeID, list);
                    G.Place(row, 0f, curY, rowW, 52f);
                    var rowBg = row.gameObject.AddComponent<Image>();
                    rowBg.sprite = LumiWorldUITheme.SurfaceSprite;
                    rowBg.type = Image.Type.Sliced;
                    rowBg.color = LumiWorldUITheme.Window;
                    rowBg.raycastTarget = true;

                    var rIcon = G.Icon(row, "Icon", recipe.GetIcon(), 10f, 7f, 38f);
                    rIcon.preserveAspect = true;
                    rIcon.raycastTarget = false;

                    string outId = recipe.outputResource != null ? recipe.outputResource.resourceID : recipe.recipeID;
                    string rName = ResourceInventoryUIPresentation.LocalizeResourceName(outId, GetEnglishRecipeName(recipe));
                    var nameTxt = G.Text(row, "Name", rName, skin, 56f, 5f, rowW - 124f, 22f, 15.5f, LumiWorldUITheme.Text, bold: true);
                    nameTxt.enableAutoSizing = true;
                    nameTxt.fontSizeMin = 12f;
                    nameTxt.fontSizeMax = 15.5f;

                    string catLabel = recipe.category == CraftingCategory.Food ? "Food" :
                                      recipe.category == CraftingCategory.Herbal ? "Medicine" : "Product";
                    G.Text(row, "Cat", $"{catLabel} · {recipe.craftDurationSeconds:0.#}s", skin, 56f, 26f, rowW - 124f, 20f, 12.5f, LumiWorldUITheme.Muted);

                    var openLbl = G.Text(row, "OpenHint", "Open ›", skin, rowW - 68f, 0f, 56f, 52f, 13.5f, LumiWorldUITheme.Primary, bold: true);
                    openLbl.alignment = TextAlignmentOptions.MidlineRight;

                    var btn = row.gameObject.AddComponent<Button>();
                    btn.targetGraphic = rowBg;
                    btn.navigation = new Navigation { mode = Navigation.Mode.None };
                    var captured = recipe;
                    btn.onClick.AddListener(() => OpenRecipeInCrafting(captured));

                    curY += 58f;
                }
            }

            list.sizeDelta = new Vector2(rowW, Mathf.Max(listH, curY));
        }

        public void CloseRecipePopover()
        {
            if (recipePopover != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(recipePopover.gameObject);
                else UnityEngine.Object.DestroyImmediate(recipePopover.gameObject);
                recipePopover = null;
            }
        }

        private void ClearWarehouseDetails(string tab)
        {
            ResetSelection();
        }

        // =========================================================
        // PROCEDURAL SPRITE HELPERS & SELECTOR / SUMMARY WINDOWS
        // =========================================================

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
                        px[y * w + x] = Color.clear;
                    else if (dist >= r - 1.5f || x == 0 || x == w - 1 || y == 0 || y == h - 1)
                        px[y * w + x] = borderColor;
                    else
                        px[y * w + x] = fillColor;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            Vector4 border = new Vector4(r + 2, r + 2, r + 2, r + 2);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateOutlineBox(int w, int h, int r, Color borderColor, float strokeWidth)
        {
            return LumiWorldUITheme.GetBorderedSurfaceSprite($"OutlineBox_R{r}_S{strokeWidth:F1}", Color.clear, borderColor, Mathf.Max(r, 20), strokeWidth);
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
                        px[y * w + x] = Color.clear;
                    else if (dist >= r - 1.2f || x == 0 || x == w - 1 || y == 0 || y == h - 1)
                        px[y * w + x] = borderColor;
                    else
                        px[y * w + x] = fillColor;
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
                selectorPanelSprite = CreateTechBox(64, 64, 12, LumiWorldUITheme.Window, LumiWorldUITheme.Edge);
            return selectorPanelSprite;
        }

        private static Sprite GetSelectorOptionCardSprite()
        {
            if (selectorOptionCardSprite == null)
                selectorOptionCardSprite = CreateTechBox(64, 64, 8, LumiWorldUITheme.Surface, LumiWorldUITheme.Edge);
            return selectorOptionCardSprite;
        }

        private static Sprite GetSelectorOptionSelectedSprite()
        {
            if (selectorOptionSelectedSprite == null)
                selectorOptionSelectedSprite = CreateOutlineBox(64, 64, 10, LumiWorldUITheme.Primary, 3f);
            return selectorOptionSelectedSprite;
        }

        private static Sprite GetSelectorIconBoxSprite()
        {
            if (selectorIconBoxSprite == null)
                selectorIconBoxSprite = CreateTechBox(48, 48, 8, LumiWorldUITheme.Surface, LumiWorldUITheme.Edge);
            return selectorIconBoxSprite;
        }

        private static Sprite GetSelectorButtonPrimarySprite()
        {
            if (selectorButtonPrimarySprite == null)
                selectorButtonPrimarySprite = CreateTechBox(64, 36, 10, LumiWorldUITheme.Primary, LumiWorldUITheme.Primary);
            return selectorButtonPrimarySprite;
        }

        private static Sprite GetSelectorButtonSecondarySprite()
        {
            if (selectorButtonSecondarySprite == null)
                selectorButtonSecondarySprite = CreateTechBox(64, 36, 10, Color.white, LumiWorldUITheme.Edge);
            return selectorButtonSecondarySprite;
        }

        private static Sprite GetSelectorItemCardSprite()
        {
            if (selectorItemCardSprite == null)
                selectorItemCardSprite = CreateTechBox(64, 64, 12, LumiWorldUITheme.Surface, LumiWorldUITheme.Edge);
            return selectorItemCardSprite;
        }

        private static Sprite GetSelectorCardSelectedSprite()
        {
            if (selectorCardSelectedSprite == null)
                selectorCardSelectedSprite = CreateOutlineBox(64, 64, 12, LumiWorldUITheme.Primary, 3f);
            return selectorCardSelectedSprite;
        }

        private static Sprite GetSelectorPillBannerSprite()
        {
            if (selectorPillBannerSprite == null)
                selectorPillBannerSprite = CreateCapsule(64, 32, 16, LumiWorldUITheme.Primary, LumiWorldUITheme.Primary);
            return selectorPillBannerSprite;
        }

        private static Sprite GetSelectorLockBannerSprite()
        {
            if (selectorLockBannerSprite == null)
                selectorLockBannerSprite = CreateCapsule(64, 34, 17, LumiWorldUITheme.Inset, LumiWorldUITheme.Edge);
            return selectorLockBannerSprite;
        }

        private static Sprite GetSelectorDetailCardSprite()
        {
            if (selectorDetailCardSprite == null)
                selectorDetailCardSprite = CreateTechBox(64, 64, 14, LumiWorldUITheme.Surface, LumiWorldUITheme.Edge);
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
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist > r) px[y * w + x] = Color.clear;
                        else if (dist >= r - 2.5f) px[y * w + x] = LumiWorldUITheme.Edge;
                        else px[y * w + x] = LumiWorldUITheme.Slot;
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
                selectorTimePillSprite = CreateCapsule(48, 26, 13, LumiWorldUITheme.Inset, LumiWorldUITheme.Edge);
            return selectorTimePillSprite;
        }

        private static Sprite GetSelectorAccentBarSprite()
        {
            if (selectorAccentBarSprite == null)
                selectorAccentBarSprite = CreateCapsule(32, 8, 4, LumiWorldUITheme.Edge, LumiWorldUITheme.Edge);
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
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist > r) px[y * w + x] = Color.clear;
                        else if (dist >= r - 1.8f) px[y * w + x] = LumiWorldUITheme.Text;
                        else
                        {
                            bool isHourHand = (Mathf.Abs(dx) <= 0.9f && dy >= 0 && dy <= 5.5f);
                            bool isMinHand = (Mathf.Abs(dy) <= 0.9f && dx >= 0 && dx <= 5.5f);
                            bool isCenterDot = dist <= 1.8f;
                            px[y * w + x] = (isHourHand || isMinHand || isCenterDot) ? LumiWorldUITheme.Text : Color.clear;
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
            var rect = G.Rect(name, parent);
            G.Place(rect, x, y, w, h);
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
            colors.highlightedColor = new Color(0.95f, 0.94f, 0.92f, 1f);
            colors.pressedColor = new Color(0.86f, 0.85f, 0.82f, 1f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.73f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(action);

            var text = G.Text(rect, "Label", label, skin, 0, 0, w, h, 14.5f, textColor, true);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        // =========================================================
        // P-07 ASSIGN RESOURCE DIALOG
        // =========================================================

        private RectTransform selectorUsedInList;
        private RectTransform selectorRecipePopover;

        private void BuildSelector()
        {
            var modal = G.Rect("ResourceSelectorDialog", root);
            G.Stretch(modal);
            var dimmer = G.Rect("Dimmer", modal);
            G.Stretch(dimmer);
            var dim = dimmer.gameObject.AddComponent<Image>();
            dim.color = LumiWorldUITheme.Backdrop;
            dim.raycastTarget = true;
            dimmer.gameObject.AddComponent<ResourceUIInputBlocker>();
            var dimBtn = dimmer.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            dimBtn.onClick.AddListener(() =>
            {
                CloseSelectorRecipePopover();
                actions.closeSelector?.Invoke();
            });

            var panel = G.Rect("Panel", modal);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(920f, 580f);
            mainSelectorPanel = panel;

            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.sprite = LumiWorldUITheme.SurfaceSprite;
            panelImg.type = Image.Type.Sliced;
            panelImg.color = LumiWorldUITheme.Window;
            panelImg.raycastTarget = true;
            panel.gameObject.AddComponent<ResourceUIInputBlocker>();
            LumiWorldUITheme.AddSoftElevation(panel.gameObject, 6f, 0.22f);
            AddOutline(panel, LumiWorldUITheme.Edge);

            // Title "Assign resource" (no subtitle per P-07 spec) + Close [×]
            G.Text(panel, "Title", "Assign resource", skin, 30f, 20f, 680f, 36f, 24f, LumiWorldUITheme.Text, bold: true);
            G.Round(panel, "Close", LumiWorldUITheme.Glyph("Close") ?? skin.iconClose, skin, 920f - 68f, 16f, 40f, () =>
            {
                CloseSelectorRecipePopover();
                actions.closeSelector?.Invoke();
            });

            var divider = G.Rect("HeaderDivider", panel);
            G.Place(divider, 28f, 70f, 920f - 56f, 2f);
            divider.gameObject.AddComponent<Image>().color = LumiWorldUITheme.Inset;

            // Left Column: Scrollable vertical list of resource rows
            selectorContent = G.Scroll(panel, "Choices", 28f, 84f, 476f, 468f);

            // Right Column: Detail Card (P-07)
            BuildSelectorDetailCard(panel, 520f, 84f, 372f, 468f);

            modal.gameObject.SetActive(false);
            SelectorWindow = modal;
        }

        private void BuildSelectorDetailCard(Transform parent, float x, float y, float w, float h)
        {
            selectorDetailPane = G.Rect("DetailCard", parent);
            G.Place(selectorDetailPane, x, y, w, h);
            var bgImg = selectorDetailPane.gameObject.AddComponent<Image>();
            bgImg.sprite = LumiWorldUITheme.SurfaceSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = LumiWorldUITheme.Surface;
            selectorDetailPane.gameObject.AddComponent<ResourceUIInputBlocker>();
            LumiWorldUITheme.AddSoftElevation(selectorDetailPane.gameObject, 4f, 0.16f);

            // Large Artwork Box
            var artBox = G.Rect("ArtworkBox", selectorDetailPane);
            G.Place(artBox, 24f, 20f, w - 48f, 152f);
            var artBg = artBox.gameObject.AddComponent<Image>();
            artBg.sprite = LumiWorldUITheme.SurfaceSprite;
            artBg.type = Image.Type.Sliced;
            artBg.color = LumiWorldUITheme.Window;

            selectorDetailIcon = G.Icon(artBox, "ResourceIcon", null, (w - 48f - 116f) * 0.5f, 18f, 116f);
            selectorDetailIcon.preserveAspect = true;
            selectorDetailIcon.raycastTarget = false;

            // Resource Name + Availability Status
            selectorDetailName = G.Text(selectorDetailPane, "Name", "Select a resource", skin, 24f, 182f, w - 48f, 32f, 22f, LumiWorldUITheme.Text, bold: true);
            selectorDetailSub = G.Text(selectorDetailPane, "AvailabilityStatus", "", skin, 24f, 214f, w - 48f, 22f, 14f, LumiWorldUITheme.Success, bold: true);

            // Requirement Pill
            var reqPill = G.Rect("RequirementPill", selectorDetailPane);
            G.Place(reqPill, 24f, 244f, w - 48f, 36f);
            var reqBg = reqPill.gameObject.AddComponent<Image>();
            reqBg.sprite = LumiWorldUITheme.PillSprite;
            reqBg.type = Image.Type.Sliced;
            reqBg.color = LumiWorldUITheme.Inset;
            selectorDetailTimePill = reqPill.gameObject;

            selectorDetailTime = G.Text(reqPill, "RequirementText", "Required resident: Present", skin, 14f, 0f, w - 76f, 36f, 13f, LumiWorldUITheme.Text, bold: true);
            selectorDetailTime.alignment = TextAlignmentOptions.MidlineLeft;

            // "Used in" section (click recipe row like Bark Paper opens popover; no "View recipe" button per P-07)
            G.Text(selectorDetailPane, "UsedInHeader", "Used in", skin, 24f, 290f, w - 48f, 22f, 13.5f, LumiWorldUITheme.Muted, bold: true);
            selectorUsedInList = G.Scroll(selectorDetailPane, "UsedInScroll", 24f, 314f, w - 48f, 82f);

            // Bottom primary CTA button: "Assign" (no leaf icon per P-07 spec)
            apply = TechPillButton(
                selectorDetailPane,
                "AssignBtn",
                "Assign",
                GetSelectorButtonPrimarySprite(),
                Color.white,
                24f,
                h - 60f,
                w - 48f,
                44f,
                () =>
                {
                    CloseSelectorRecipePopover();
                    actions.applySelection?.Invoke();
                });
        }

        public void BuildOptions(List<ResourceUIOption> options)
        {
            CloseSelectorRecipePopover();
            selectorRecipeResourceId = null;
            G.Clear(selectorContent);
            optionCards.Clear();
            cachedOptions = options ?? new List<ResourceUIOption>();

            // Order: eligible items first, then locked items
            var ordered = new List<ResourceUIOption>();
            ordered.AddRange(cachedOptions.FindAll(o => o != null && o.eligible));
            ordered.AddRange(cachedOptions.FindAll(o => o != null && !o.eligible));

            float curY = 4f;
            const float rowW = 460f;
            const float rowH = 72f;
            const float gapY = 10f;

            for (int i = 0; i < ordered.Count; i++)
            {
                var opt = ordered[i];
                bool isEligible = opt.eligible;

                var cardRect = G.Rect("OptionRow_" + opt.id, selectorContent);
                G.Place(cardRect, 4f, curY, rowW, rowH);
                var cardBg = cardRect.gameObject.AddComponent<Image>();
                cardBg.sprite = LumiWorldUITheme.SurfaceSprite;
                cardBg.type = Image.Type.Sliced;
                cardBg.color = isEligible ? LumiWorldUITheme.Surface : new Color(0.89f, 0.87f, 0.84f, 0.78f);
                cardRect.gameObject.AddComponent<ResourceUIInputBlocker>();
                LumiWorldUITheme.AddSoftElevation(cardRect.gameObject, 3f, 0.14f);

                // Icon Box (52x52)
                var icBox = G.Rect("IconBox", cardRect);
                G.Place(icBox, 12f, 10f, 52f, 52f);
                var icBoxBg = icBox.gameObject.AddComponent<Image>();
                icBoxBg.sprite = LumiWorldUITheme.SurfaceSprite;
                icBoxBg.type = Image.Type.Sliced;
                icBoxBg.color = LumiWorldUITheme.Slot;

                Image iconImg = null;
                if (opt.icon != null)
                {
                    iconImg = G.Icon(icBox, "Icon", opt.icon, 4f, 4f, 44f);
                    iconImg.preserveAspect = true;
                    iconImg.color = isEligible ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.45f);
                    iconImg.raycastTarget = false;
                }

                if (!isEligible)
                {
                    var lockOverlay = G.Icon(icBox, "LockOverlay", LumiWorldUITheme.Glyph("Lock") ?? skin.iconLock, 16f, 16f, 20f);
                    lockOverlay.color = LumiWorldUITheme.Text;
                    lockOverlay.raycastTarget = false;
                }

                // Name + Subtitle
                var nameLbl = G.Text(cardRect, "Name", opt.name, skin, 76f, 12f, 220f, 24f, 16.5f, isEligible ? LumiWorldUITheme.Text : LumiWorldUITheme.Muted, bold: true);
                nameLbl.alignment = TextAlignmentOptions.MidlineLeft;

                string subLine = isEligible
                    ? (!string.IsNullOrEmpty(opt.timeText) ? $"Cycle: {opt.timeText}" : "Ready to assign")
                    : "View requirement ›";
                var subLbl = G.Text(cardRect, "Sub", subLine, skin, 76f, 36f, 220f, 20f, 13f, LumiWorldUITheme.Muted, bold: !isEligible);
                subLbl.alignment = TextAlignmentOptions.MidlineLeft;

                // Right status badge ("✓ Available" vs "Locked")
                var statusBadge = G.Rect("StatusBadge", cardRect);
                G.Place(statusBadge, rowW - 128f, 20f, 114f, 32f);
                if (isEligible)
                {
                    var stIc = G.Icon(statusBadge, "Check", LumiWorldUITheme.StatusReadySprite, 4f, 6f, 20f);
                    stIc.preserveAspect = true;
                    stIc.raycastTarget = false;
                    var stTxt = G.Text(statusBadge, "Text", "Available", skin, 28f, 0f, 84f, 32f, 14f, LumiWorldUITheme.Success, bold: true);
                    stTxt.alignment = TextAlignmentOptions.MidlineLeft;
                }
                else
                {
                    var stBg = statusBadge.gameObject.AddComponent<Image>();
                    stBg.sprite = LumiWorldUITheme.PillSprite;
                    stBg.type = Image.Type.Sliced;
                    stBg.color = LumiWorldUITheme.Inset;
                    var stIc = G.Icon(statusBadge, "Lock", LumiWorldUITheme.Glyph("Lock"), 12f, 8f, 16f);
                    stIc.color = LumiWorldUITheme.Muted;
                    var stTxt = G.Text(statusBadge, "Text", "Locked", skin, 32f, 0f, 76f, 32f, 13.5f, LumiWorldUITheme.Muted, bold: true);
                    stTxt.alignment = TextAlignmentOptions.MidlineLeft;
                }

                // Selected 3px dark graphite border (no checkmark per P-07)
                var borderRect = G.Rect("SelectedBorder", cardRect);
                G.Stretch(borderRect);
                var borderImg = borderRect.gameObject.AddComponent<Image>();
                borderImg.sprite = GetSelectorCardSelectedSprite();
                borderImg.type = Image.Type.Sliced;
                borderImg.color = Color.white;
                borderImg.raycastTarget = false;
                borderRect.gameObject.SetActive(false);

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
                    eligible = isEligible,
                    option = opt
                };

                curY += rowH + gapY;
            }

            selectorContent.sizeDelta = new Vector2(rowW + 8f, Mathf.Max(468f, curY + 8f));
        }

        private bool SelectorOptionsChanged(List<ResourceUIOption> options)
        {
            if (cachedOptions == null || cachedOptions.Count != options.Count) return true;
            for (int i = 0; i < options.Count; i++)
            {
                var previous = cachedOptions[i];
                var current = options[i];
                if (previous.id != current.id || previous.eligible != current.eligible || previous.name != current.name ||
                    previous.icon != current.icon || previous.timeText != current.timeText || previous.reason != current.reason)
                    return true;
            }
            return false;
        }

        public void UpdateOptions(List<ResourceUIOption> options, string pending)
        {
            if (SelectorOptionsChanged(options)) BuildOptions(options);
            ResourceUIOption selectedOpt = null;

            foreach (var kvp in optionCards)
            {
                var card = kvp.Value;
                bool isSelected = (!string.IsNullOrEmpty(pending) && card.id == pending);
                if (card.border != null) card.border.gameObject.SetActive(isSelected);
                if (isSelected) selectedOpt = card.option;
            }

            string selectedId = selectedOpt?.id;
            if (selectorRecipeResourceId != selectedId)
            {
                selectorRecipeResourceId = selectedId;
                CloseSelectorRecipePopover();
                if (selectedId != null) PopulateSelectorUsedInList(selectedId);
            }
            BindSelectorDetailOption(selectedOpt);
        }

        private void BindSelectorDetailOption(ResourceUIOption selectedOpt)
        {
            if (selectedOpt != null)
            {
                if (selectorDetailIcon != null)
                {
                    selectorDetailIcon.sprite = selectedOpt.icon;
                    selectorDetailIcon.gameObject.SetActive(selectedOpt.icon != null);
                    selectorDetailIcon.color = selectedOpt.eligible ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.55f);
                }
                if (selectorDetailName != null) selectorDetailName.text = selectedOpt.name;

                if (selectorDetailSub != null)
                {
                    selectorDetailSub.text = selectedOpt.eligible ? "✓ Available" : "Locked";
                    selectorDetailSub.color = selectedOpt.eligible ? LumiWorldUITheme.Success : LumiWorldUITheme.Warning;
                }

                if (selectorDetailTimePill != null) selectorDetailTimePill.SetActive(true);
                if (selectorDetailTime != null)
                {
                    selectorDetailTime.text = selectedOpt.eligible
                        ? "✓ Required resident: Present"
                        : (!string.IsNullOrEmpty(selectedOpt.reason) ? selectedOpt.reason : "Locked · Missing required resident");
                    selectorDetailTime.color = selectedOpt.eligible ? LumiWorldUITheme.Success : LumiWorldUITheme.Error;
                }

            }
            else
            {
                if (selectorDetailIcon != null)
                {
                    selectorDetailIcon.sprite = null;
                    selectorDetailIcon.gameObject.SetActive(false);
                }
                if (selectorDetailName != null) selectorDetailName.text = "Select a resource";
                if (selectorDetailSub != null) selectorDetailSub.text = "";
                if (selectorDetailTimePill != null) selectorDetailTimePill.SetActive(false);
                if (selectorUsedInList != null) G.Clear(selectorUsedInList);
            }

            if (apply != null) apply.interactable = selectedOpt != null && selectedOpt.eligible;
        }

        private void PopulateSelectorUsedInList(string resourceId)
        {
            if (selectorUsedInList == null) return;
            G.Clear(selectorUsedInList);

            var usedIn = UsedInRecipesFor(resourceId);
            if (usedIn.Count == 0)
            {
                var emptyLbl = G.Text(selectorUsedInList, "NoUsedIn", "Raw material · Stored in Inventory", skin, 4f, 8f, 300f, 28f, 13f, LumiWorldUITheme.Muted);
                emptyLbl.alignment = TextAlignmentOptions.MidlineLeft;
                selectorUsedInList.sizeDelta = new Vector2(316f, 44f);
                return;
            }

            float curY = 0f;
            const float rowW = 316f;
            const float rowH = 44f;
            for (int i = 0; i < usedIn.Count; i++)
            {
                var recipe = usedIn[i];
                var row = G.Rect("UsedInRow_" + recipe.recipeID, selectorUsedInList);
                G.Place(row, 0f, curY, rowW, rowH);
                var rowBg = row.gameObject.AddComponent<Image>();
                rowBg.sprite = LumiWorldUITheme.SurfaceSprite;
                rowBg.type = Image.Type.Sliced;
                rowBg.color = LumiWorldUITheme.Window;
                rowBg.raycastTarget = true;

                var ic = G.Icon(row, "Icon", recipe.GetIcon(), 8f, 5f, 34f);
                ic.preserveAspect = true;
                ic.raycastTarget = false;

                string outId = recipe.outputResource != null ? recipe.outputResource.resourceID : recipe.recipeID;
                string rName = ResourceInventoryUIPresentation.LocalizeResourceName(outId, GetEnglishRecipeName(recipe));
                var nameTxt = G.Text(row, "Name", rName, skin, 50f, 0f, rowW - 62f, rowH, 14.5f, LumiWorldUITheme.Text, bold: true);
                nameTxt.alignment = TextAlignmentOptions.MidlineLeft;

                // Clicking the recipe row (e.g. Bark Paper) opens the Recipe Popover (no "View recipe" text per P-07)
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = rowBg;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                var captured = recipe;
                btn.onClick.AddListener(() => OpenSelectorRecipePopover(captured));

                curY += rowH + 6f;
            }

            selectorUsedInList.sizeDelta = new Vector2(rowW, Mathf.Max(82f, curY));
        }

        private void OpenSelectorRecipePopover(CraftingRecipeData recipe)
        {
            if (recipe == null || mainSelectorPanel == null) return;
            CloseSelectorRecipePopover();

            selectorRecipePopover = G.Rect("SelectorRecipePopover", mainSelectorPanel);
            G.Stretch(selectorRecipePopover);
            var dismissImg = selectorRecipePopover.gameObject.AddComponent<Image>();
            dismissImg.color = new Color(0.14f, 0.12f, 0.10f, 0.16f);
            dismissImg.raycastTarget = true;
            var dismissBtn = selectorRecipePopover.gameObject.AddComponent<Button>();
            dismissBtn.transition = Selectable.Transition.None;
            dismissBtn.onClick.AddListener(CloseSelectorRecipePopover);

            int ingCount = recipe.inputs != null ? recipe.inputs.Count : 0;
            float popW = 320f;
            float popH = 92f + Mathf.Max(1, ingCount) * 48f;

            var card = G.Rect("Card", selectorRecipePopover);
            G.Place(card, 546f, 200f, popW, popH);
            var cardBg = card.gameObject.AddComponent<Image>();
            cardBg.sprite = LumiWorldUITheme.SurfaceSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = LumiWorldUITheme.Surface;
            cardBg.raycastTarget = true;
            card.gameObject.AddComponent<ResourceUIInputBlocker>();
            LumiWorldUITheme.AddSoftElevation(card.gameObject, 5f, 0.22f);
            AddOutline(card, LumiWorldUITheme.Edge);

            string outId = recipe.outputResource != null ? recipe.outputResource.resourceID : recipe.recipeID;
            string rName = ResourceInventoryUIPresentation.LocalizeResourceName(outId, GetEnglishRecipeName(recipe));
            G.Text(card, "Title", rName, skin, 16f, 12f, popW - 110f, 26f, 17f, LumiWorldUITheme.Text, bold: true);

            var openBtnRect = G.Rect("OpenInCraftingBtn", card);
            G.Place(openBtnRect, popW - 104f, 12f, 56f, 26f);
            var openTxt = G.Text(openBtnRect, "OpenTxt", "Open ›", skin, 0f, 0f, 56f, 26f, 13.5f, LumiWorldUITheme.Primary, bold: true);
            openTxt.alignment = TextAlignmentOptions.MidlineRight;
            openTxt.raycastTarget = true;
            var openBtn = openBtnRect.gameObject.AddComponent<Button>();
            openBtn.targetGraphic = openTxt;
            openBtn.onClick.AddListener(() =>
            {
                var crafting = UnityEngine.Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);
                if (crafting != null)
                {
                    CloseSelectorRecipePopover();
                    actions.closeSelector?.Invoke();
                    crafting.OpenRecipe(recipe);
                }
            });

            G.Round(card, "Close", LumiWorldUITheme.Glyph("Close") ?? skin.iconClose, skin, popW - 40f, 11f, 28f, CloseSelectorRecipePopover);
            G.Text(card, "Sub", "Crafted by:", skin, 16f, 42f, popW - 32f, 20f, 12.5f, LumiWorldUITheme.Muted, bold: true);

            float curY = 66f;
            if (recipe.inputs != null)
            {
                foreach (var inp in recipe.inputs)
                {
                    if (inp == null) continue;
                    ResourceData inRes = CraftingCatalog.GetResource(inp.resourceId);
                    string rawInName = inRes != null && !string.IsNullOrEmpty(inRes.resourceName) ? inRes.resourceName : inp.resourceId;
                    string inName = ResourceInventoryUIPresentation.LocalizeResourceName(inp.resourceId, rawInName);
                    Sprite inIcon = inRes != null ? inRes.icon : null;

                    var row = G.Rect("Ing_" + inp.resourceId, card);
                    G.Place(row, 14f, curY, popW - 28f, 42f);
                    var rowBg = row.gameObject.AddComponent<Image>();
                    rowBg.sprite = LumiWorldUITheme.SurfaceSprite;
                    rowBg.type = Image.Type.Sliced;
                    rowBg.color = LumiWorldUITheme.Window;

                    if (inIcon != null)
                    {
                        var ic = G.Icon(row, "Icon", inIcon, 8f, 5f, 32f);
                        ic.preserveAspect = true;
                    }
                    G.Text(row, "Name", inName, skin, 48f, 0f, popW - 130f, 42f, 14f, LumiWorldUITheme.Text, bold: true);
                    var amtTxt = G.Text(row, "Amt", $"×{inp.amount}", skin, popW - 84f, 0f, 44f, 42f, 14f, LumiWorldUITheme.Muted, bold: true);
                    amtTxt.alignment = TextAlignmentOptions.MidlineRight;
                    curY += 46f;
                }
            }
        }

        private void CloseSelectorRecipePopover()
        {
            if (selectorRecipePopover != null)
            {
                selectorRecipePopover.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(selectorRecipePopover.gameObject);
                else UnityEngine.Object.DestroyImmediate(selectorRecipePopover.gameObject);
                selectorRecipePopover = null;
            }
        }

        public bool TryCloseSelectorRecipePopover()
        {
            if (selectorRecipePopover == null) return false;
            CloseSelectorRecipePopover();
            return true;
        }

        public void SetSideDetailOpen(bool open)
        {
            isSideDetailOpen = false;
            if (!open) CloseSelectorRecipePopover();
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
                sideDetailTier.text = opt.tier == ResourceTier.Tier1 ? "Rank I · Tier 1" : (opt.tier == ResourceTier.Tier2 ? "Rank II · Tier 2" : "Rank III · Tier 3");

            int stock = actions.getStock != null ? actions.getStock(opt.id) : 0;
            if (sideDetailStock != null) sideDetailStock.text = "Owned: " + stock;

            string timeStr = !string.IsNullOrEmpty(opt.timeText) ? opt.timeText : "10s";
            if (sideDetailTime != null)
                sideDetailTime.text = "Cycle Time: " + timeStr + (opt.eligible ? " (Active)" : " (Locked)");

            if (sideDetailDesc != null)
                sideDetailDesc.text = !string.IsNullOrEmpty(opt.description)
                    ? opt.description
                    : "Raw material harvested from habitat nodes. Can be stored or used in crafting recipes.";

            if (sideDetailRecipesContent != null)
            {
                G.Clear(sideDetailRecipesContent);
                var recipes = new List<CraftingRecipeData>();
                if (CraftingCatalog.All != null)
                {
                    foreach (var r in CraftingCatalog.All)
                    {
                        if (r != null && r.inputs != null &&
                            r.inputs.Exists(inp => inp != null && string.Equals(inp.resourceId, opt.id, StringComparison.OrdinalIgnoreCase)))
                        {
                            recipes.Add(r);
                        }
                    }
                }

                if (recipes.Count == 0)
                {
                    var noRecipe = G.Text(sideDetailRecipesContent, "NoRecipes", "No crafting recipes use this resource yet.", skin, 4, 16, 276, 36, 12f, LumiWorldUITheme.Muted);
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
                                sideDetailRecipeTip.text = $"<b>{engName}</b> · {category} ({captured.craftDurationSeconds:0}s)";
                        });

                        curX += cardW + gapX;
                    }
                    sideDetailRecipesContent.sizeDelta = new Vector2(Mathf.Max(284f, curX + 6f), 76);
                    string firstEng = GetEnglishRecipeName(recipes[0]);
                    if (sideDetailRecipeTip != null)
                        sideDetailRecipeTip.text = $"<b>{firstEng}</b> · {recipes[0].category} ({recipes[0].craftDurationSeconds:0}s)";
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
                case BiomeAffinity.Leafwood: return "Leafwood";
                case BiomeAffinity.Bloomfield: return "Bloomfield";
                case BiomeAffinity.Windheath: return "Windheath";
                case BiomeAffinity.Reedmarsh: return "Reedmarsh";
                case BiomeAffinity.Rainleaf: return "Rainleaf";
                case BiomeAffinity.Streamstone: return "Streamstone";
                case BiomeAffinity.Lilywater: return "Lilywater";
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
            SummaryWindow = Modal("PendingSummary", 700, 530, "Safe Stash", "Goods in transit · Not yet added to Inventory", actions.closeSummary, out var panel);
            summaryContent = G.Scroll(panel, "PendingGoods", 30, 100, 640, 345);
            collectSummary = G.Button(panel, "CollectPending", "Transfer All to Inventory", skin, 454, 466, 214, 42, () => actions.collectSummary?.Invoke(), true);
        }

        public void BindSummary(List<ResourceUIBuffer> buffers, bool offline)
        {
            var panel = SummaryWindow.Find("Panel");
            panel.Find("Title").GetComponent<TextMeshProUGUI>().text = offline ? "Accumulated while offline" : "Safe Stash";
            string key = string.Join("|", buffers.ConvertAll(b => b.habitatId + ":" + b.resourceId));
            if (key != summaryKey)
            {
                summaryKey = key;
                G.Clear(summaryContent);
                summaryCounts.Clear();
                summaryButtons.Clear();
                float y = 0;
                foreach (var buffer in buffers)
                {
                    string rowKey = buffer.habitatId + ":" + buffer.resourceId;
                    var row = G.Rect("Pending_" + rowKey, summaryContent);
                    G.Place(row, 0, y, 620, 70);
                    G.Image(row, LumiWorldUITheme.SurfaceSprite, true, 1f);
                    G.Icon(row, "Icon", buffer.icon, 12, 11, 48);
                    G.Text(row, "Name", buffer.name, skin, 78, 8, 355, 24, 17, LumiWorldUITheme.Text, bold: true);
                    summaryCounts[rowKey] = G.Text(row, "PendingAmount", "", skin, 78, 34, 355, 26, 14, LumiWorldUITheme.Muted);
                    summaryButtons[rowKey] = G.Button(row, "Collect", "Collect", skin, 500, 18, 104, 36,
                        () => actions.collectPending?.Invoke(buffer.habitatId, buffer.resourceId), true);
                    y += 80;
                }
                if (buffers.Count == 0) G.Text(summaryContent, "Empty", "No pending resources.", skin, 0, 20, 600, 60, 17, LumiWorldUITheme.Muted);
                summaryContent.sizeDelta = new Vector2(630, Mathf.Max(345, y));
            }
            foreach (var buffer in buffers)
            {
                string keyId = buffer.habitatId + ":" + buffer.resourceId;
                summaryCounts[keyId].text = "Pending: " + buffer.amount;
                summaryButtons[keyId].interactable = buffer.amount > 0;
            }
            collectSummary.interactable = buffers.Exists(b => b.amount > 0);
        }

        public void ShowReceipt(string message, List<ResourceUIBuffer> transferred)
        {
            G.Clear(Receipt);
            float height = 80 + Mathf.CeilToInt(transferred.Count / 3f) * 32;
            Receipt.sizeDelta = new Vector2(470, height);
            G.Text(Receipt, "Message", message, skin, 20, 12, 400, 54, 15, LumiWorldUITheme.Text, bold: true);
            G.Round(Receipt, "Close", LumiWorldUITheme.Glyph("Close") ?? skin.iconClose, skin, 430, 12, 25, () => actions.closeReceipt?.Invoke());
            for (int i = 0; i < transferred.Count; i++)
            {
                var item = transferred[i];
                float x = 22 + i % 3 * 143, y = 69 + i / 3 * 32;
                G.Icon(Receipt, "Icon_" + item.resourceId, item.icon, x, y, 27);
                G.Text(Receipt, "Received_" + item.resourceId, "+" + item.amount, skin, x + 33, y, 104, 27, 17, LumiWorldUITheme.Text, bold: true);
            }
            Receipt.gameObject.SetActive(true);
            Receipt.SetAsLastSibling();
        }
    }
}

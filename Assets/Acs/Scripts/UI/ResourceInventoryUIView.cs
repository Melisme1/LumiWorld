using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
        private RectTransform habitatContent, warehouseContent, selectorContent, summaryContent;
        private TextMeshProUGUI habitatTitle, habitatSubtitle, detailName, detailMeta, detailStock, detailDescription, selectorStatus;
        private Image detailIcon;
        private Button collectHabitat, apply, recovery, collectSummary;
        private readonly Dictionary<string, TextMeshProUGUI> warehouseCounts = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Image> warehouseBorders = new Dictionary<string, Image>();
        private readonly Dictionary<string, TextMeshProUGUI> bufferCounts = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Button> bufferButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, OptionRow> optionRows = new Dictionary<string, OptionRow>();
        private readonly Dictionary<string, TextMeshProUGUI> summaryCounts = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, Button> summaryButtons = new Dictionary<string, Button>();
        private string habitatKey, summaryKey, selectedWarehouseId;
        private List<ResourceUIItem> warehouseItems;
        private sealed class OptionRow { public Image cardBackground; public Image border; public TextMeshProUGUI name, requirements, reason; }

        public ResourceInventoryUIView(RectTransform root, ResourceInventoryUISkin skin, ResourceInventoryUIActions actions)
        {
            this.root = root; this.skin = skin; this.actions = actions;
            G.Round(root, "WarehouseButton", skin.iconWarehouse, skin, 38, 18, 52, () => actions.openWarehouse?.Invoke());
            // HabitatButton removed per user request: all habitat interaction is unified in Living Creatures Panel 1
            var whCap = G.Text(root, "WarehouseCaption", "Kho tài nguyên", skin, 4, 74, 120, 22, 13f, new Color(0.06f, 0.12f, 0.20f, 1f), bold: true);
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
            habitatSubtitle = G.Text(HabitatPanel, "Population", "Chọn một nhóm trên map", skin, 26, 59, 228, 25, 13);
            G.Round(HabitatPanel, "Close", skin.iconClose, skin, 230, 25, 30, () => actions.closeHabitat?.Invoke());
            habitatContent = G.Scroll(HabitatPanel, "HabitatScroll", 22, 93, 236, 280);
            collectHabitat = G.Button(HabitatPanel, "CollectHabitat", "Thu tất cả của nhóm", skin, 28, 384, 224, 42,
                () => actions.collectHabitat?.Invoke(), true);
        }

        public void BindHabitat(ResourceUIHabitat model)
        {
            if (model == null)
            {
                habitatTitle.text = "Habitat"; habitatSubtitle.text = "Chọn một nhóm trên map"; collectHabitat.interactable = false;
                if (habitatKey != "empty") { G.Clear(habitatContent); G.Text(habitatContent, "Empty", "Bấm vào Nature hoặc creature để xem nhóm. Bạn vẫn có thể thao tác map ngoài panel.", skin, 2, 4, 222, 100, 16); habitatKey = "empty"; }
                return;
            }
            habitatTitle.text = model.title;
            int unlockedSlotsCount = 0;
            if (model.slots != null) { foreach (var s in model.slots) if (s != null && s.unlocked) unlockedSlotsCount++; }
            habitatSubtitle.text = $"{model.hexCount} hex · Cư dân: {model.residentCount}/{model.capacity} · Slot: {unlockedSlotsCount}/{(model.slots != null ? model.slots.Count : 3)}";
            if (habitatKey != model.structureKey)
            {
                habitatKey = model.structureKey; G.Clear(habitatContent); bufferCounts.Clear(); bufferButtons.Clear();
                G.Button(habitatContent, "Previous", "‹ Nhóm", skin, 0, 0, 104, 30, () => actions.previousHabitat?.Invoke());
                G.Button(habitatContent, "Next", "Nhóm ›", skin, 112, 0, 104, 30, () => actions.nextHabitat?.Invoke());
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
                    G.Text(row, "Tier", (slot.tier == ResourceTier.Tier1 ? "T1" : "T2") + " · Ô " + (slot.index + 1), skin, 76, 8, 132, 23, 14, bold: true);
                    G.Text(row, "ResourceName", slot.name, skin, 76, 31, 132, 36, 15, bold: true);
                    G.Text(row, "Rate", slot.unlocked ? slot.rate.ToString("0.##") + "/phút" : "Chưa mở", skin, 8, 75, 120, 22, 14);
                    var button = G.Button(row, "Choose", "Chọn", skin, 136, 73, 72, 28, () => actions.openSelector?.Invoke(slot.tier, slot.index));
                    button.interactable = slot.unlocked;
                    G.Text(row, "Reason", slot.reason, skin, 8, 103, 200, 45, 12, skin.muted);
                    y += 164;
                }
                G.Text(habitatContent, "PendingHeader", "ĐANG CHỜ THU", skin, 2, y, 214, 28, 14, bold: true); y += 34;
                if (model.buffers.Count == 0) { G.Text(habitatContent, "NoGoods", "Chưa có hàng trong buffer.", skin, 2, y, 214, 32, 13); y += 36; }
                foreach (var buffer in model.buffers)
                {
                    var row = G.Rect("Buffer_" + buffer.resourceId, habitatContent); G.Place(row, 0, y, 216, 76);
                    G.Icon(row, "Icon", buffer.icon, 0, 8, 42);
                    G.Text(row, "Name", buffer.name, skin, 48, 2, 168, 29, 14, bold: true);
                    bufferCounts[buffer.resourceId] = G.Text(row, "PendingAmount", "", skin, 48, 34, 97, 35, 13);
                    bufferButtons[buffer.resourceId] = G.Button(row, "Collect", "Thu", skin, 148, 37, 68, 30, () => actions.collectResource?.Invoke(buffer.resourceId), true);
                    y += 82;
                }
                habitatContent.sizeDelta = new Vector2(226, y + 8);
            }
            foreach (var buffer in model.buffers)
            {
                bufferCounts[buffer.resourceId].text = buffer.amount + " / " + buffer.cap + (buffer.amount >= buffer.cap ? " · Đầy" : "");
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
            panelImg.color = Color.white;
            panelImg.raycastTarget = true;
            panel.gameObject.AddComponent<ResourceUIInputBlocker>();

            G.Text(panel, "Title", "KHO TÀI NGUYÊN", skin, 32, 24, 750, 32, 22, new Color(0.04f, 0.12f, 0.22f, 1f), bold: true);
            G.Text(panel, "Subtitle", "Túi đồ cá nhân · Chỉ hiển thị tài nguyên đã khai thác vào kho", skin, 32, 58, 750, 24, 13, new Color(0.28f, 0.38f, 0.48f, 1f));

            G.Round(panel, "Close", skin.iconClose, skin, 910 - 64, 20, 36, () => actions.closeWarehouse?.Invoke());

            warehouseContent = G.Scroll(panel, "ResourceGrid", 28, 92, 598, 386);

            // Right detail pane (Clean Modern Tech Card Box)
            var detail = G.Rect("ResourceDetails", panel);
            G.Place(detail, 646, 92, 236, 386);
            var detailBg = detail.gameObject.AddComponent<Image>();
            detailBg.sprite = GetSelectorOptionCardSprite();
            detailBg.type = Image.Type.Sliced;
            detailBg.color = Color.white;

            var art = G.Rect("DetailIconBox", detail);
            G.Place(art, 78, 20, 80, 80);
            var artImg = art.gameObject.AddComponent<Image>();
            artImg.sprite = GetSelectorIconBoxSprite();
            artImg.type = Image.Type.Sliced;
            artImg.color = Color.white;

            detailIcon = G.Icon(art, "Icon", null, 14, 14, 52);

            detailName = G.Text(detail, "Name", "Chọn tài nguyên", skin, 16, 114, 204, 38, 18, new Color(0.04f, 0.12f, 0.22f, 1f), bold: true);
            detailName.alignment = TextAlignmentOptions.Center;

            detailMeta = G.Text(detail, "Source", "", skin, 16, 154, 204, 24, 13, new Color(0.28f, 0.38f, 0.48f, 1f));
            detailMeta.alignment = TextAlignmentOptions.Center;

            detailStock = G.Text(detail, "Stock", "", skin, 16, 188, 204, 34, 16, new Color(0.02f, 0.52f, 0.78f, 1f), bold: true);
            detailStock.alignment = TextAlignmentOptions.Center;

            detailDescription = G.Text(detail, "Description", "", skin, 20, 232, 196, 136, 13, new Color(0.20f, 0.28f, 0.38f, 1f));
            detailDescription.alignment = TextAlignmentOptions.TopLeft;

            recovery = TechPillButton(panel, "Recovery", "Hàng bảo toàn", GetSelectorButtonSecondarySprite(), new Color(0.12f, 0.22f, 0.35f, 1f), 30, 494, 220, 40, () => actions.openRecovery?.Invoke());
            G.Text(panel, "CapacityHint", "Sức chứa tính riêng theo từng loại tài nguyên trong kho", skin, 270, 502, 590, 28, 13.5f, new Color(0.40f, 0.50f, 0.60f, 1f));

            modal.gameObject.SetActive(false);
            WarehouseWindow = modal;
        }

        public void BuildWarehouseItems(List<ResourceUIItem> items)
        {
            warehouseItems = items;
            G.Clear(warehouseContent);
            warehouseCounts.Clear();
            warehouseBorders.Clear();

            if (items == null || items.Count == 0)
            {
                var emptyRect = G.Rect("EmptyNotice", warehouseContent);
                G.Place(emptyRect, 0, 80, 580, 100);
                var emptyTxt = emptyRect.gameObject.AddComponent<TextMeshProUGUI>();
                emptyTxt.font = skin.font;
                emptyTxt.fontSize = 15f;
                emptyTxt.alignment = TextAlignmentOptions.Center;
                emptyTxt.color = new Color(0.40f, 0.50f, 0.60f, 1f);
                emptyTxt.text = "Túi đồ hiện đang trống.\nHãy khai thác và thu hoạch tài nguyên từ các Môi trường (Habitat)!";

                detailIcon.sprite = null;
                detailName.text = "Túi đồ trống";
                detailMeta.text = "";
                detailStock.text = "0 tài nguyên";
                detailDescription.text = "Chưa có tài nguyên nào được thu thập vào túi đồ.";
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float x = (i % 5) * 118f;
                float y = (i / 5) * 132f;

                var rect = G.Rect("Item_" + item.id, warehouseContent);
                G.Place(rect, x, y, 110, 122);

                var bgImg = rect.gameObject.AddComponent<Image>();
                bgImg.sprite = GetSelectorOptionCardSprite();
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
                bgImg.raycastTarget = true;
                rect.gameObject.AddComponent<ResourceUIInputBlocker>();

                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = bgImg;
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

                // Center Icon Box
                var art = G.Rect("Slot", rect);
                G.Place(art, 24, 10, 62, 62);
                var artImg = art.gameObject.AddComponent<Image>();
                artImg.sprite = GetSelectorIconBoxSprite();
                artImg.type = Image.Type.Sliced;
                artImg.color = Color.white;
                artImg.raycastTarget = false;

                if (item.icon != null)
                {
                    var ic = G.Icon(art, "ResourceIcon", item.icon, 10, 10, 42);
                    ic.raycastTarget = false;
                }

                // Số lượng ở góc dưới phải của Icon Box
                var countRect = G.Rect("CountBox", art);
                G.Place(countRect, 4, 38, 54, 20);
                var countTxt = countRect.gameObject.AddComponent<TextMeshProUGUI>();
                countTxt.font = skin.font;
                countTxt.fontSize = 13.5f;
                countTxt.fontStyle = FontStyles.Bold;
                countTxt.alignment = TextAlignmentOptions.BottomRight;
                countTxt.color = new Color(0.02f, 0.48f, 0.72f, 1f);
                countTxt.text = item.stock.ToString();
                countTxt.raycastTarget = false;
                warehouseCounts[item.id] = countTxt;

                // Tên tài nguyên bên dưới
                var nameRect = G.Rect("Name", rect);
                G.Place(nameRect, 4, 76, 102, 38);
                var nameTxt = nameRect.gameObject.AddComponent<TextMeshProUGUI>();
                nameTxt.font = skin.font;
                nameTxt.fontSize = 12f;
                nameTxt.fontStyle = FontStyles.Bold;
                nameTxt.alignment = TextAlignmentOptions.Top;
                nameTxt.overflowMode = TextOverflowModes.Ellipsis;
                nameTxt.color = new Color(0.08f, 0.16f, 0.28f, 1f);
                nameTxt.text = item.name;
                nameTxt.raycastTarget = false;
            }

            warehouseContent.sizeDelta = new Vector2(588, Mathf.Max(380, Mathf.CeilToInt(items.Count / 5f) * 132));
            if (items.Count > 0) SelectWarehouse(items[0].id);
            UpdateWarehouse(items, 0);
        }

        public void UpdateWarehouse(List<ResourceUIItem> items, long recoveryAmount)
        {
            warehouseItems = items;
            foreach (var item in items)
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
                if (recTxt != null) recTxt.text = "Hàng bảo toàn · " + recoveryAmount;
            }
            if (!string.IsNullOrEmpty(selectedWarehouseId))
            {
                SelectWarehouse(selectedWarehouseId);
            }
        }

        private void SelectWarehouse(string id)
        {
            selectedWarehouseId = id;
            foreach (var pair in warehouseBorders) pair.Value.gameObject.SetActive(pair.Key == id);
            var item = warehouseItems?.Find(i => i.id == id);
            if (item == null) return;
            detailIcon.sprite = item.icon;
            detailName.text = item.name;
            detailMeta.text = item.biome + " · " + item.tier;
            detailStock.text = "Đã thu: " + item.stock + " / " + item.capacity;
            detailDescription.text = item.description;
        }

        private static Sprite selectorPanelSprite;
        private static Sprite selectorOptionCardSprite;
        private static Sprite selectorOptionSelectedSprite;
        private static Sprite selectorButtonPrimarySprite;
        private static Sprite selectorButtonSecondarySprite;
        private static Sprite selectorIconBoxSprite;

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
            panel.sizeDelta = new Vector2(800, 550);
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.sprite = GetSelectorPanelSprite();
            panelImg.type = Image.Type.Sliced;
            panelImg.color = Color.white;
            panelImg.raycastTarget = true;
            panel.gameObject.AddComponent<ResourceUIInputBlocker>();

            G.Text(panel, "Title", "CHỌN TÀI NGUYÊN KHAI THÁC", skin, 32, 24, 680, 32, 22, new Color(0.04f, 0.12f, 0.22f, 1f), bold: true);
            G.Text(panel, "Subtitle", "Cùng biome & tier · Combo cần đủ loài sinh vật trong cùng môi trường", skin, 32, 58, 680, 24, 13, new Color(0.28f, 0.38f, 0.48f, 1f));

            G.Round(panel, "Close", skin.iconClose, skin, 800 - 64, 20, 36, () => actions.closeSelector?.Invoke());

            selectorContent = G.Scroll(panel, "Choices", 28, 92, 744, 344);
            selectorStatus = G.Text(panel, "Eligibility", "", skin, 30, 448, 738, 36, 13.5f, new Color(0.15f, 0.25f, 0.38f, 1f));

            TechPillButton(panel, "Clear", "Bỏ lựa chọn", GetSelectorButtonSecondarySprite(), new Color(0.12f, 0.22f, 0.35f, 1f), 30, 492, 152, 40, () => actions.clearSelection?.Invoke());
            TechPillButton(panel, "Cancel", "Hủy", GetSelectorButtonSecondarySprite(), new Color(0.12f, 0.22f, 0.35f, 1f), 474, 492, 130, 40, () => actions.closeSelector?.Invoke());
            apply = TechPillButton(panel, "Apply", "Áp dụng", GetSelectorButtonPrimarySprite(), Color.white, 616, 490, 154, 42, () => actions.applySelection?.Invoke());

            modal.gameObject.SetActive(false);
            SelectorWindow = modal;
        }

        public void BuildOptions(List<ResourceUIOption> options)
        {
            G.Clear(selectorContent); optionRows.Clear(); float y = 0;
            foreach (var option in options)
            {
                var rect = G.Rect("Option_" + option.id, selectorContent); G.Place(rect, 0, y, 730, 102);
                var bgImg = rect.gameObject.AddComponent<Image>();
                bgImg.sprite = GetSelectorOptionCardSprite();
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
                bgImg.raycastTarget = true;
                rect.gameObject.AddComponent<ResourceUIInputBlocker>();

                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = bgImg;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => actions.chooseResource?.Invoke(option.id));

                // Left Icon Box
                var art = G.Rect("Slot", rect); G.Place(art, 12, 12, 76, 76);
                var artImg = art.gameObject.AddComponent<Image>();
                artImg.sprite = GetSelectorIconBoxSprite();
                artImg.type = Image.Type.Sliced;
                artImg.color = Color.white;
                artImg.raycastTarget = false;

                if (option.icon != null)
                {
                    var iconImg = G.Icon(art, "ResourceIcon", option.icon, 12, 12, 52);
                    iconImg.raycastTarget = false;
                }

                // Selected Neon Cyan Border
                var borderRect = G.Rect("Selected", rect); G.Stretch(borderRect);
                var borderImg = borderRect.gameObject.AddComponent<Image>();
                borderImg.sprite = GetSelectorOptionSelectedSprite();
                borderImg.type = Image.Type.Sliced;
                borderImg.color = Color.white;
                borderImg.raycastTarget = false;
                borderRect.gameObject.SetActive(false);

                // Option Row Texts: Crisp Navy, Slate, Mint
                var nameText = G.Text(rect, "Name", option.name, skin, 104, 10, 610, 26, 17.5f, new Color(0.05f, 0.12f, 0.22f, 1f), bold: true);
                var reqText = G.Text(rect, "Species", "", skin, 104, 38, 610, 32, 13.5f, new Color(0.22f, 0.32f, 0.44f, 1f));
                var reasonText = G.Text(rect, "Reason", "", skin, 104, 70, 610, 24, 13f, bold: true);

                optionRows[option.id] = new OptionRow
                {
                    cardBackground = bgImg,
                    border = borderImg,
                    name = nameText,
                    requirements = reqText,
                    reason = reasonText
                };
                y += 110;
            }
            selectorContent.sizeDelta = new Vector2(734, Mathf.Max(344, y));
        }

        public void UpdateOptions(List<ResourceUIOption> options, string pending, bool canApply, string reason)
        {
            foreach (var option in options)
            {
                if (optionRows.TryGetValue(option.id, out var row))
                {
                    bool isSelected = (option.id == pending);
                    if (row.border != null) row.border.gameObject.SetActive(isSelected);
                    if (row.cardBackground != null)
                    {
                        row.cardBackground.color = isSelected ? new Color(0.92f, 0.97f, 1f, 1f) : Color.white;
                    }
                    if (row.requirements != null) row.requirements.text = option.requirements;
                    if (row.reason != null)
                    {
                        row.reason.text = option.eligible ? "✓ " + option.reason : option.reason;
                        row.reason.color = option.eligible ? new Color(0.02f, 0.58f, 0.38f, 1f) : new Color(0.85f, 0.15f, 0.25f, 1f);
                    }
                }
            }
            if (selectorStatus != null) selectorStatus.text = reason;
            if (apply != null) apply.interactable = canApply;
        }

        private void BuildSummary()
        {
            SummaryWindow = Modal("PendingSummary", 700, 530, "Hàng bảo toàn", "Đang chờ thu · Chưa được cộng vào kho", actions.closeSummary, out var panel);
            summaryContent = G.Scroll(panel, "PendingGoods", 30, 100, 640, 345);
            collectSummary = G.Button(panel, "CollectPending", "Nhận vào kho", skin, 454, 466, 214, 42, () => actions.collectSummary?.Invoke(), true);
        }
        public void BindSummary(List<ResourceUIBuffer> buffers, bool offline)
        {
            var panel = SummaryWindow.Find("Panel"); panel.Find("Title").GetComponent<TextMeshProUGUI>().text = offline ? "Tích lũy khi bạn vắng mặt" : "Hàng bảo toàn";
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
                    summaryButtons[rowKey] = G.Button(row, "Collect", "Nhận", skin, 500, 18, 104, 36,
                        () => actions.collectPending?.Invoke(buffer.habitatId, buffer.resourceId), true);
                    y += 80;
                }
                if (buffers.Count == 0) G.Text(summaryContent, "Empty", "Chưa có tài nguyên chờ thu.", skin, 0, 20, 600, 60, 17);
                summaryContent.sizeDelta = new Vector2(630, Mathf.Max(345, y));
            }
            foreach (var buffer in buffers)
            {
                string keyId = buffer.habitatId + ":" + buffer.resourceId;
                summaryCounts[keyId].text = "Chờ thu: " + buffer.amount; summaryButtons[keyId].interactable = buffer.amount > 0;
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

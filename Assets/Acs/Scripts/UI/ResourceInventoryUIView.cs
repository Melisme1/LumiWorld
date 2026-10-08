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
        private sealed class OptionRow { public Image border; public TextMeshProUGUI requirements, reason; }

        public ResourceInventoryUIView(RectTransform root, ResourceInventoryUISkin skin, ResourceInventoryUIActions actions)
        {
            this.root = root; this.skin = skin; this.actions = actions;
            G.Round(root, "WarehouseButton", skin.iconWarehouse, skin, 20, 18, 52, () => actions.openWarehouse?.Invoke());
            G.Button(root, "HabitatButton", "Habitat", skin, 82, 22, 110, 44, () => actions.toggleHabitat?.Invoke());
            G.Text(root, "WarehouseCaption", "Kho tài nguyên", skin, 20, 70, 155, 22, 14);
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
            habitatSubtitle.text = model.hexCount + " hex · Cư dân " + model.residentCount + "/" + model.capacity;
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
            WarehouseWindow = Modal("WarehouseWindow", 910, 560, "Kho tài nguyên", "Đã thu trong kho · Buffer chờ thu nằm ở habitat", actions.closeWarehouse, out var panel);
            warehouseContent = G.Scroll(panel, "ResourceGrid", 28, 98, 598, 390);
            var detail = G.Rect("ResourceDetails", panel); G.Place(detail, 650, 98, 230, 390);
            G.Image(detail, skin.surfaceInset, true, skin.insetPPU);
            detailIcon = G.Icon(detail, "Icon", null, 71, 22, 88);
            detailName = G.Text(detail, "Name", "Chọn tài nguyên", skin, 20, 120, 190, 54, 21, bold: true);
            detailMeta = G.Text(detail, "Source", "", skin, 20, 184, 190, 28, 15);
            detailStock = G.Text(detail, "Stock", "", skin, 20, 220, 190, 42, 17, bold: true);
            detailDescription = G.Text(detail, "Description", "", skin, 20, 272, 190, 97, 14);
            recovery = G.Button(panel, "Recovery", "Hàng bảo toàn", skin, 30, 502, 270, 34, () => actions.openRecovery?.Invoke());
            G.Text(panel, "CapacityHint", "Sức chứa tính riêng theo từng loại tài nguyên", skin, 336, 500, 520, 36, 14, skin.muted);
        }

        public void BuildWarehouseItems(List<ResourceUIItem> items)
        {
            warehouseItems = items; G.Clear(warehouseContent); warehouseCounts.Clear(); warehouseBorders.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i]; float x = i % 5 * 116, y = i / 5 * 130;
                var rect = G.Rect("Item_" + item.id, warehouseContent); G.Place(rect, x, y, 108, 124);
                var hit = rect.gameObject.AddComponent<Image>(); hit.color = Color.clear;
                var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = hit; button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => SelectWarehouse(item.id));
                var art = G.Rect("Slot", rect); G.Place(art, 14, 0, 80, 80); G.Image(art, skin.slotResource);
                G.Icon(art, "ResourceIcon", item.icon, 15, 12, 50);
                var selected = G.Rect("Selected", art); G.Stretch(selected); warehouseBorders[item.id] = G.Image(selected, skin.slotSelected);
                warehouseCounts[item.id] = G.Text(art, "Stock", "", skin, 0, 55, 73, 23, 17, bold: true);
                warehouseCounts[item.id].alignment = TextAlignmentOptions.MidlineRight;
                G.Text(rect, "Name", item.name, skin, 0, 83, 108, 40, 15).alignment = TextAlignmentOptions.Top;
            }
            warehouseContent.sizeDelta = new Vector2(588, Mathf.CeilToInt(items.Count / 5f) * 130);
            if (items.Count > 0) SelectWarehouse(items[0].id);
            UpdateWarehouse(items, 0);
        }
        public void UpdateWarehouse(List<ResourceUIItem> items, long recoveryAmount)
        {
            warehouseItems = items;
            foreach (var item in items)
                if (warehouseCounts.TryGetValue(item.id, out var label))
                { label.text = item.stock.ToString(); label.color = item.stock >= item.capacity ? skin.gold : skin.forest; }
            recovery.interactable = recoveryAmount > 0;
            recovery.GetComponentInChildren<TextMeshProUGUI>().text = "Hàng bảo toàn · " + recoveryAmount;
            SelectWarehouse(selectedWarehouseId);
        }
        private void SelectWarehouse(string id)
        {
            selectedWarehouseId = id;
            foreach (var pair in warehouseBorders) pair.Value.gameObject.SetActive(pair.Key == id);
            var item = warehouseItems?.Find(i => i.id == id); if (item == null) return;
            detailIcon.sprite = item.icon; detailName.text = item.name; detailMeta.text = item.biome + " · " + item.tier;
            detailStock.text = "Đã thu: " + item.stock + " / " + item.capacity;
            detailDescription.text = item.description;
        }

        private void BuildSelector()
        {
            SelectorWindow = Modal("ResourceSelectorDialog", 800, 550, "Chọn tài nguyên", "Cùng biome và tier · Combo cần đủ loài trong cùng habitat", actions.closeSelector, out var panel);
            selectorContent = G.Scroll(panel, "Choices", 28, 98, 744, 340);
            selectorStatus = G.Text(panel, "Eligibility", "", skin, 30, 446, 738, 42, 14);
            G.Button(panel, "Clear", "Bỏ lựa chọn", skin, 30, 496, 152, 34, () => actions.clearSelection?.Invoke());
            G.Button(panel, "Cancel", "Hủy", skin, 474, 496, 130, 34, () => actions.closeSelector?.Invoke());
            apply = G.Button(panel, "Apply", "Áp dụng", skin, 616, 492, 154, 42, () => actions.applySelection?.Invoke(), true);
        }
        public void BuildOptions(List<ResourceUIOption> options)
        {
            G.Clear(selectorContent); optionRows.Clear(); float y = 0;
            foreach (var option in options)
            {
                var rect = G.Rect("Option_" + option.id, selectorContent); G.Place(rect, 0, y, 730, 104);
                var image = G.Image(rect, skin.surfaceInset, true, skin.insetPPU, true);
                var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => actions.chooseResource?.Invoke(option.id));
                var art = G.Rect("Slot", rect); G.Place(art, 10, 12, 76, 76); G.Image(art, skin.slotResource);
                G.Icon(art, "ResourceIcon", option.icon, 14, 14, 48);
                var border = G.Rect("Selected", art); G.Stretch(border);
                var row = new OptionRow { border = G.Image(border, skin.slotSelected) };
                G.Text(rect, "Name", option.name, skin, 100, 8, 614, 25, 19, bold: true);
                row.requirements = G.Text(rect, "Species", "", skin, 100, 34, 614, 37, 14);
                row.reason = G.Text(rect, "Reason", "", skin, 100, 72, 614, 26, 13);
                optionRows[option.id] = row; y += 112;
            }
            selectorContent.sizeDelta = new Vector2(734, Mathf.Max(340, y));
        }
        public void UpdateOptions(List<ResourceUIOption> options, string pending, bool canApply, string reason)
        {
            foreach (var option in options)
                if (optionRows.TryGetValue(option.id, out var row))
                {
                    row.border.gameObject.SetActive(option.id == pending); row.requirements.text = option.requirements;
                    row.reason.text = option.reason; row.reason.color = option.eligible ? skin.forest : skin.muted;
                }
            selectorStatus.text = reason; apply.interactable = canApply;
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

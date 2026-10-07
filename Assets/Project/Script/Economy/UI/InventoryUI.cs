using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Thanh nhỏ ở góc trên màn chơi, hiện số lượng từng loại tài nguyên trong kho (Gỗ, Hoa tươi, Thảo mộc...).
/// Khi kho đổi, ô của loại đó nảy nhẹ và hiện số bay (+3 khi thu hoạch, -10 khi giao đơn).
/// Số chuyển màu cam khi loại đó đã đầy kho, lúc này bong bóng thu hoạch sẽ giữ phần dư.
/// EconomyHUD tự tạo script này, không cần gắn tay.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class InventoryUI : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private float barHeight = 48f;
    [SerializeField] private float iconSize = 30f;
    [Tooltip("Bề rộng phần số của mỗi ô, đủ cho 3 chữ số (sức chứa hiện là 999)")]
    [SerializeField] private float amountWidth = 50f;
    [SerializeField] private float slotSpacing = 14f;
    [SerializeField] private float padding = 14f;
    [SerializeField] private Color backgroundColor = new Color(0.06f, 0.09f, 0.16f, 0.88f);
    [SerializeField] private Color amountColor = Color.white;
    [Tooltip("Màu số lượng khi loại tài nguyên đó đã đầy kho")]
    [SerializeField] private Color fullColor = new Color(1f, 0.70f, 0.25f, 1f);
    [SerializeField] private Color gainColor = new Color(0.45f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color lossColor = new Color(1f, 0.42f, 0.38f, 1f);

    [Header("Motion")]
    [Tooltip("Thời gian số bay +/- hiện trên màn hình (giây)")]
    [SerializeField] private float floatDuration = 0.9f;
    [Tooltip("Quãng đường số bay di chuyển (pixel)")]
    [SerializeField] private float floatDistance = 26f;

    private class Slot
    {
        public ResourceData resource;
        public RectTransform rect;
        public TextMeshProUGUI amountText;
        public Coroutine punch;
    }

    private readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();
    private ResourceInventory inventory;
    private RectTransform floatLayer;

    private void Awake()
    {
        Build();
    }

    private void Start()
    {
        // Lần đầu gọi Instance sẽ tạo kho và nạp số lượng đã lưu
        inventory = ResourceInventory.Instance;
        foreach (Slot slot in slots.Values)
        {
            RefreshSlot(slot);
        }
        inventory.OnInventoryChanged += HandleInventoryChanged;
    }

    private void OnDisable()
    {
        // Coroutine dừng khi HUD bị ẩn: trả các ô về kích thước chuẩn và bỏ số bay còn dở
        foreach (Slot slot in slots.Values)
        {
            slot.punch = null;
            if (slot.rect != null) slot.rect.localScale = Vector3.one;
        }
        EconomyHUD.ClearChildren(floatLayer);
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    private void Build()
    {
        IReadOnlyList<ResourceData> resources = ResourceCatalog.All;
        float slotWidth = iconSize + 6f + amountWidth;

        RectTransform rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(padding * 2f + resources.Count * slotWidth + Mathf.Max(0, resources.Count - 1) * slotSpacing, barHeight);

        RectTransform background = EconomyHUD.CreateRect("Background", rect);
        EconomyHUD.Stretch(background);
        EconomyHUD.CreatePanel(background, backgroundColor);

        for (int i = 0; i < resources.Count; i++)
        {
            ResourceData resource = resources[i];

            // Pivot ở giữa để ô nảy quanh tâm
            RectTransform slotRect = EconomyHUD.CreateRect(resource.resourceID, rect);
            slotRect.anchorMin = new Vector2(0f, 0.5f);
            slotRect.anchorMax = new Vector2(0f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(slotWidth, barHeight);
            slotRect.anchoredPosition = new Vector2(padding + i * (slotWidth + slotSpacing) + slotWidth * 0.5f, 0f);

            // Icon giống bong bóng thu hoạch: dùng ResourceData.icon nếu có, không thì icon vẽ bằng code
            EconomyHUD.CreateIcon("Icon", slotRect, BiomeHarvestIndicator.GetResourceSprite(resource), iconSize, 0f);

            TextMeshProUGUI amountText = EconomyHUD.CreateText("Amount", slotRect, 22f, amountColor, TextAlignmentOptions.Left);
            RectTransform amountRect = amountText.rectTransform;
            amountRect.anchorMin = Vector2.zero;
            amountRect.anchorMax = Vector2.one;
            amountRect.offsetMin = new Vector2(iconSize + 6f, 0f);
            amountRect.offsetMax = Vector2.zero;
            amountText.text = "0";

            slots[resource.resourceID] = new Slot { resource = resource, rect = slotRect, amountText = amountText };
        }

        floatLayer = EconomyHUD.CreateRect("FloatingNumbers", rect);
        EconomyHUD.Stretch(floatLayer);

        // Không có loại tài nguyên nào để hiện (ResourceCatalog đã báo lý do trong Console)
        if (resources.Count == 0) gameObject.SetActive(false);
    }

    private void HandleInventoryChanged(string resourceId, int amount, int delta)
    {
        if (!slots.TryGetValue(resourceId, out Slot slot)) return;

        RefreshSlot(slot);
        if (!isActiveAndEnabled || delta == 0) return;

        if (slot.punch != null) StopCoroutine(slot.punch);
        slot.punch = StartCoroutine(EconomyHUD.Punch(slot.rect, 1.18f, 0.25f));

        // Số bay ngay dưới ô của loại tài nguyên đó: vào kho thì bay lên, ra khỏi kho thì rơi xuống
        float x = slot.rect.anchoredPosition.x - floatLayer.rect.width * 0.5f;
        if (delta > 0)
        {
            StartCoroutine(EconomyHUD.FloatText(floatLayer, "+" + delta, gainColor, 20f,
                new Vector2(x, -floatDistance - 12f), new Vector2(x, -12f), floatDuration));
        }
        else
        {
            StartCoroutine(EconomyHUD.FloatText(floatLayer, "-" + (-delta), lossColor, 20f,
                new Vector2(x, -12f), new Vector2(x, -floatDistance - 12f), floatDuration));
        }
    }

    private void RefreshSlot(Slot slot)
    {
        int amount = inventory.GetAmount(slot.resource);
        slot.amountText.text = amount.ToString();
        slot.amountText.color = amount >= slot.resource.maxStackSize ? fullColor : amountColor;
    }
}

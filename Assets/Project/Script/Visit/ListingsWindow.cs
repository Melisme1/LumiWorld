using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cửa sổ "Listings from ...": các lô đang bán của một người chơi, mua tại chỗ qua MarketSync như trong chợ.
/// Dùng chung cho WorldMap và VisitWorld. MarketSync trừ Coins và lưu thao tác chờ trước khi gửi,
/// nên mất mạng không mất Coins; hàng về kho qua hộp thư. Esc hoặc Close để đóng. Chữ hiện ra là tiếng Anh.
/// </summary>
public class ListingsWindow : MonoBehaviour
{
    private const float Width = 560f;
    private const float Height = 600f;
    private const float RowHeight = 36f;
    private const int MaxRows = 12;

    private static readonly Color PanelColor = new Color(0.22f, 0.15f, 0.08f, 0.94f);
    private static readonly Color RowColor = new Color(0.98f, 0.93f, 0.8f, 1f);
    private static readonly Color TextColor = new Color(0.98f, 0.95f, 0.88f, 1f);
    private static readonly Color DarkText = new Color(0.25f, 0.17f, 0.08f, 1f);
    private static readonly Color MutedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
    private static readonly Color AccentColor = new Color(0.75f, 0.2f, 0.15f, 1f);
    private static readonly Color DangerColor = new Color(0.55f, 0.12f, 0.1f, 1f);
    private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.45f);

    private TextMeshProUGUI title;
    private TextMeshProUGUI status;
    private RectTransform list;
    private string sellerId;
    private bool busy;

    public bool IsOpen => gameObject.activeSelf;

    /// <summary>Mở cửa sổ (tạo lần đầu) dưới parent, phủ kín parent.</summary>
    public static ListingsWindow Open(ListingsWindow existing, Transform parent, string sellerId, string sellerName)
    {
        ListingsWindow window = existing != null ? existing : Build(parent);
        window.sellerId = sellerId;
        window.title.text = "Listings from " + sellerName;
        window.gameObject.SetActive(true);
        window.transform.SetAsLastSibling();
        window.Refresh();
        return window;
    }

    public void Close() => gameObject.SetActive(false);

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
    }

    private void Refresh()
    {
        EconomyHUD.ClearChildren(list);
        status.text = "Loading...";
        StartCoroutine(WorldShareService.LoadListings(sellerId, Fill, message => status.text = message));
    }

    private void Fill(List<MarketListingDto> lots)
    {
        EconomyHUD.ClearChildren(list);
        if (lots.Count == 0)
        {
            status.text = "This player has nothing for sale.";
            return;
        }

        status.text = "Buying takes Coins now; the goods arrive in your inventory through the inbox.";
        for (int i = 0; i < lots.Count && i < MaxRows; i++)
        {
            MarketListingDto lot = lots[i];
            RectTransform row = EconomyHUD.CreateRect("Lot" + i, list);
            SetTopLeft(row, new Vector2(0f, -i * (RowHeight + 6f)), new Vector2(list.sizeDelta.x, RowHeight));
            EconomyHUD.CreatePanel(row, RowColor);

            TextMeshProUGUI label = EconomyHUD.CreateText("Label", row, 15f, DarkText, TextAlignmentOptions.Left);
            EconomyHUD.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-104f, 0f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.text = MarketSync.NameOf(lot.resourceId) + " x" + lot.amount + "  ·  " + lot.priceCoins + " Coins";

            AddButton(row, "Buy", new Vector2(row.sizeDelta.x - 96f, -2f), new Vector2(90f, RowHeight - 4f), AccentColor, () => Buy(lot));
        }
    }

    private void Buy(MarketListingDto lot)
    {
        if (busy) return;
        if (MarketSync.Instance == null)
        {
            status.text = "The market is not ready yet.";
            return;
        }

        busy = true;
        status.text = "Buying...";
        MarketSync.Buy(lot, (ok, message) =>
        {
            busy = false;
            status.text = message;
            if (ok) Refresh();
        });
    }

    private static ListingsWindow Build(Transform parent)
    {
        RectTransform windowRect = EconomyHUD.CreateRect("ListingsWindow", parent);
        EconomyHUD.Stretch(windowRect);
        var window = windowRect.gameObject.AddComponent<ListingsWindow>();

        RectTransform backdrop = EconomyHUD.CreateRect("Backdrop", windowRect);
        EconomyHUD.Stretch(backdrop);
        Image shade = backdrop.gameObject.AddComponent<Image>();
        shade.color = BackdropColor;
        shade.raycastTarget = true;

        RectTransform panel = EconomyHUD.CreateRect("Panel", windowRect);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(Width, Height);
        EconomyHUD.CreatePanel(panel, PanelColor).raycastTarget = true;

        window.title = EconomyHUD.CreateText("Title", panel, 24f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(window.title.rectTransform, new Vector2(18f, -14f), new Vector2(Width - 140f, 34f));
        AddButton(panel, "Close", new Vector2(Width - 112f, -12f), new Vector2(96f, 38f), DangerColor, window.Close);

        window.status = EconomyHUD.CreateText("Status", panel, 15f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(window.status.rectTransform, new Vector2(18f, -56f), new Vector2(Width - 36f, 40f));
        window.status.textWrappingMode = TextWrappingModes.Normal;

        window.list = EconomyHUD.CreateRect("List", panel);
        SetTopLeft(window.list, new Vector2(16f, -104f), new Vector2(Width - 32f, Height - 120f));
        return window;
    }

    private static void AddButton(RectTransform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = EconomyHUD.CreateRect(text + "Button", parent);
        SetTopLeft(rect, position, size);
        EconomyHUD.AddButton(rect, color).onClick.AddListener(onClick);

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", rect, 17f, TextColor, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.text = text;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}

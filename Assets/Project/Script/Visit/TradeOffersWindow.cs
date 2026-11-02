using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cửa sổ "Trade offers" trên đảo của mình: đề nghị người khác gửi tới (Accept / Decline)
/// và đề nghị mình đã gửi (Cancel). Thẻ nhận được về tay qua hộp thư. Esc hoặc Close để đóng.
/// Chữ hiện ra là tiếng Anh.
/// </summary>
public class TradeOffersWindow : MonoBehaviour
{
    private const float Width = 640f;
    private const float Height = 650f;
    private const float RowHeight = 52f;
    private const int MaxRows = 4;

    private static readonly Color PanelColor = new Color(0.22f, 0.15f, 0.08f, 0.94f);
    private static readonly Color RowColor = new Color(0.98f, 0.93f, 0.8f, 1f);
    private static readonly Color TextColor = new Color(0.98f, 0.95f, 0.88f, 1f);
    private static readonly Color DarkText = new Color(0.25f, 0.17f, 0.08f, 1f);
    private static readonly Color MutedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
    private static readonly Color AccentColor = new Color(0.2f, 0.55f, 0.25f, 1f);
    private static readonly Color DangerColor = new Color(0.55f, 0.12f, 0.1f, 1f);
    private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.45f);

    private TextMeshProUGUI status;
    private RectTransform incomingList;
    private RectTransform outgoingList;
    private TextMeshProUGUI incomingTitle;
    private TextMeshProUGUI outgoingTitle;
    private bool busy;

    public bool IsOpen => gameObject.activeSelf;

    public static TradeOffersWindow Open(TradeOffersWindow existing, Transform parent)
    {
        TradeOffersWindow window = existing != null ? existing : Build(parent);
        window.status.text = string.Empty;
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

    public void Refresh()
    {
        EconomyHUD.ClearChildren(incomingList);
        EconomyHUD.ClearChildren(outgoingList);
        incomingTitle.text = "Offers to you: loading...";
        outgoingTitle.text = "Your offers: loading...";

        TradeSync.Incoming(FillIncoming, message => incomingTitle.text = "Offers to you: " + message);
        TradeSync.Outgoing(FillOutgoing, message => outgoingTitle.text = "Your offers: " + message);
    }

    private void FillIncoming(List<TradeDto> trades)
    {
        if (!IsOpen) return;
        EconomyHUD.ClearChildren(incomingList);
        incomingTitle.text = trades.Count == 0 ? "Offers to you: none." : "Offers to you (" + trades.Count + ")";

        for (int i = 0; i < trades.Count && i < MaxRows; i++)
        {
            TradeDto trade = trades[i];
            int held = HandCards.Count(CardTradeIds.ToCardId(trade.requestCardId));
            string text = trade.fromName + " gives " + MarketSync.NameOf(trade.offerCardId) + " x" + trade.offerCount +
                          "\nfor your " + MarketSync.NameOf(trade.requestCardId) + " x" + trade.requestCount +
                          "  (you have " + held + ")";
            RectTransform row = AddRow(incomingList, i, text, 196f);
            Button accept = AddButton(row, "Accept", new Vector2(row.sizeDelta.x - 192f, -4f), new Vector2(92f, RowHeight - 8f),
                AccentColor, () => Answer(trade, true));
            accept.interactable = held >= trade.requestCount && HandCards.Available;
            AddButton(row, "Decline", new Vector2(row.sizeDelta.x - 96f, -4f), new Vector2(92f, RowHeight - 8f),
                DangerColor, () => Answer(trade, false));
        }
    }

    private void FillOutgoing(List<TradeDto> trades)
    {
        if (!IsOpen) return;
        EconomyHUD.ClearChildren(outgoingList);
        outgoingTitle.text = trades.Count == 0 ? "Your offers: none." : "Your offers (" + trades.Count + ")";

        for (int i = 0; i < trades.Count && i < MaxRows; i++)
        {
            TradeDto trade = trades[i];
            string text = "To " + trade.toName + ": your " + MarketSync.NameOf(trade.offerCardId) + " x" + trade.offerCount +
                          "\nfor " + MarketSync.NameOf(trade.requestCardId) + " x" + trade.requestCount;
            RectTransform row = AddRow(outgoingList, i, text, 100f);
            AddButton(row, "Cancel", new Vector2(row.sizeDelta.x - 96f, -4f), new Vector2(92f, RowHeight - 8f),
                DangerColor, () => CancelOffer(trade));
        }
    }

    private void Answer(TradeDto trade, bool accept)
    {
        if (busy) return;
        busy = true;
        status.text = accept ? "Accepting..." : "Declining...";
        System.Action<bool, string> done = (ok, message) =>
        {
            busy = false;
            status.text = message;
            if (IsOpen) Refresh();
        };
        if (accept) TradeSync.Accept(trade, done);
        else TradeSync.Decline(trade, done);
    }

    private void CancelOffer(TradeDto trade)
    {
        if (busy) return;
        busy = true;
        status.text = "Cancelling...";
        TradeSync.Cancel(trade, (ok, message) =>
        {
            busy = false;
            status.text = message;
            if (IsOpen) Refresh();
        });
    }

    private static RectTransform AddRow(RectTransform list, int index, string text, float buttonsWidth)
    {
        RectTransform row = EconomyHUD.CreateRect("Trade" + index, list);
        SetTopLeft(row, new Vector2(0f, -index * (RowHeight + 6f)), new Vector2(list.sizeDelta.x, RowHeight));
        EconomyHUD.CreatePanel(row, RowColor);

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", row, 14f, DarkText, TextAlignmentOptions.Left);
        EconomyHUD.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(12f, 0f);
        label.rectTransform.offsetMax = new Vector2(-buttonsWidth - 8f, 0f);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.text = text;
        return row;
    }

    private static TradeOffersWindow Build(Transform parent)
    {
        RectTransform windowRect = EconomyHUD.CreateRect("TradeOffersWindow", parent);
        EconomyHUD.Stretch(windowRect);
        var window = windowRect.gameObject.AddComponent<TradeOffersWindow>();

        RectTransform backdrop = EconomyHUD.CreateRect("Backdrop", windowRect);
        EconomyHUD.Stretch(backdrop);
        Image shade = backdrop.gameObject.AddComponent<Image>();
        shade.color = BackdropColor;
        shade.raycastTarget = true;

        RectTransform panel = EconomyHUD.CreateRect("Panel", windowRect);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(Width, Height);
        EconomyHUD.CreatePanel(panel, PanelColor).raycastTarget = true;

        TextMeshProUGUI title = EconomyHUD.CreateText("Title", panel, 24f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(title.rectTransform, new Vector2(18f, -14f), new Vector2(Width - 140f, 34f));
        title.text = "Trade offers";
        AddButton(panel, "Close", new Vector2(Width - 112f, -12f), new Vector2(96f, 38f), DangerColor, window.Close);

        window.status = EconomyHUD.CreateText("Status", panel, 15f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(window.status.rectTransform, new Vector2(18f, -54f), new Vector2(Width - 36f, 40f));
        window.status.textWrappingMode = TextWrappingModes.Normal;

        float listHeight = MaxRows * (RowHeight + 6f);

        window.incomingTitle = EconomyHUD.CreateText("IncomingTitle", panel, 17f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(window.incomingTitle.rectTransform, new Vector2(18f, -96f), new Vector2(Width - 36f, 26f));
        window.incomingList = EconomyHUD.CreateRect("Incoming", panel);
        SetTopLeft(window.incomingList, new Vector2(16f, -124f), new Vector2(Width - 32f, listHeight));

        float outTop = -124f - listHeight - 8f;
        window.outgoingTitle = EconomyHUD.CreateText("OutgoingTitle", panel, 17f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(window.outgoingTitle.rectTransform, new Vector2(18f, outTop), new Vector2(Width - 36f, 26f));
        window.outgoingList = EconomyHUD.CreateRect("Outgoing", panel);
        SetTopLeft(window.outgoingList, new Vector2(16f, outTop - 28f), new Vector2(Width - 32f, listHeight));
        return window;
    }

    private static Button AddButton(RectTransform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = EconomyHUD.CreateRect(text + "Button", parent);
        SetTopLeft(rect, position, size);
        Button button = EconomyHUD.AddButton(rect, color);
        button.onClick.AddListener(onClick);

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", rect, 16f, TextColor, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.text = text;
        return button;
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

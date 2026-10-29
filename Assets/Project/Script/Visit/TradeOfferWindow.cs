using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cửa sổ "Trade cards" lúc thăm đảo: chọn một loại thẻ thú của mình và số lá đưa, một loại thẻ thú muốn nhận
/// và số lá, rồi gửi đề nghị cho chủ đảo qua TradeSync. Không thấy bài trên tay chủ đảo, nên được chọn mọi thẻ thú;
/// chủ đảo chỉ đồng ý được khi đủ lá. Esc hoặc Close để đóng. Chữ hiện ra là tiếng Anh.
/// </summary>
public class TradeOfferWindow : MonoBehaviour
{
    private const float Width = 560f;
    private const float Height = 420f;

    private static readonly Color PanelColor = new Color(0.22f, 0.15f, 0.08f, 0.94f);
    private static readonly Color RowColor = new Color(0.98f, 0.93f, 0.8f, 1f);
    private static readonly Color TextColor = new Color(0.98f, 0.95f, 0.88f, 1f);
    private static readonly Color DarkText = new Color(0.25f, 0.17f, 0.08f, 1f);
    private static readonly Color MutedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
    private static readonly Color AccentColor = new Color(0.75f, 0.2f, 0.15f, 1f);
    private static readonly Color NeutralColor = new Color(0.45f, 0.33f, 0.2f, 1f);
    private static readonly Color DangerColor = new Color(0.55f, 0.12f, 0.1f, 1f);
    private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.45f);

    private TextMeshProUGUI title;
    private TextMeshProUGUI giveLabel;
    private TextMeshProUGUI getLabel;
    private TextMeshProUGUI status;
    private Button sendButton;

    private string ownerId;
    private string ownerName;
    private int giveIndex;
    private int getIndex;
    private int giveCount = 1;
    private int getCount = 1;
    private bool busy;

    public bool IsOpen => gameObject.activeSelf;

    public static TradeOfferWindow Open(TradeOfferWindow existing, Transform parent, string ownerId, string ownerName)
    {
        TradeOfferWindow window = existing != null ? existing : Build(parent);
        window.ownerId = ownerId;
        window.ownerName = ownerName;
        window.title.text = "Trade cards with " + ownerName;
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

    // Thẻ thú mình đang có (đưa đi) và mọi thẻ thú trong game (muốn nhận)
    private static List<KeyValuePair<CardData, int>> GiveOptions() => HandCards.Creatures();

    private static List<CardData> GetOptions()
    {
        var result = new List<CardData>();
        CardCatalog catalog = WorldSaveSystem.Instance != null ? WorldSaveSystem.Instance.Cards : null;
        if (catalog == null) return result;
        foreach (CardData card in catalog.All)
            if (CardTradeIds.IsTradable(card)) result.Add(card);
        result.Sort((a, b) => string.CompareOrdinal(a.cardName, b.cardName));
        return result;
    }

    private void Refresh()
    {
        List<KeyValuePair<CardData, int>> give = GiveOptions();
        List<CardData> get = GetOptions();
        if (giveIndex >= give.Count) giveIndex = 0;
        if (getIndex >= get.Count) getIndex = 0;

        int held = give.Count > 0 ? give[giveIndex].Value : 0;
        giveCount = Mathf.Clamp(giveCount, 1, Mathf.Max(1, Mathf.Min(held, TradeSync.MaxCardsPerSide)));
        getCount = Mathf.Clamp(getCount, 1, TradeSync.MaxCardsPerSide);

        giveLabel.text = give.Count == 0
            ? "You have no creature cards in your hand."
            : "You give: " + give[giveIndex].Key.cardName + " x" + giveCount + "   (you have " + held + ")";
        getLabel.text = get.Count == 0
            ? "No creature cards found."
            : "You get: " + get[getIndex].cardName + " x" + getCount;

        sendButton.interactable = !busy && give.Count > 0 && get.Count > 0 && !string.IsNullOrEmpty(ownerId);
    }

    private void CycleGive()
    {
        int count = GiveOptions().Count;
        if (count == 0) return;
        giveIndex = (giveIndex + 1) % count;
        giveCount = 1;
        Refresh();
    }

    private void CycleGet()
    {
        int count = GetOptions().Count;
        if (count == 0) return;
        getIndex = (getIndex + 1) % count;
        Refresh();
    }

    private void Send()
    {
        if (busy) return;
        List<KeyValuePair<CardData, int>> give = GiveOptions();
        List<CardData> get = GetOptions();
        if (give.Count == 0 || get.Count == 0) return;

        busy = true;
        status.text = "Sending...";
        Refresh();
        TradeSync.Offer(ownerId, give[giveIndex].Key, giveCount, get[getIndex], getCount, (ok, message) =>
        {
            busy = false;
            status.text = message;
            if (IsOpen) Refresh();
        });
    }

    private static TradeOfferWindow Build(Transform parent)
    {
        RectTransform windowRect = EconomyHUD.CreateRect("TradeOfferWindow", parent);
        EconomyHUD.Stretch(windowRect);
        var window = windowRect.gameObject.AddComponent<TradeOfferWindow>();

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
        window.title.textWrappingMode = TextWrappingModes.NoWrap;
        window.title.overflowMode = TextOverflowModes.Ellipsis;
        AddButton(panel, "Close", new Vector2(Width - 112f, -12f), new Vector2(96f, 38f), DangerColor, window.Close);

        TextMeshProUGUI intro = EconomyHUD.CreateText("Intro", panel, 15f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(intro.rectTransform, new Vector2(18f, -56f), new Vector2(Width - 36f, 60f));
        intro.textWrappingMode = TextWrappingModes.Normal;
        intro.text = "One card type for one card type, no Coins and no fee. Your cards leave your hand now " +
                     "and come back if the owner declines or the offer expires (72 h).";

        window.giveLabel = AddChoiceRow(panel, -126f, window.CycleGive,
            () => { window.giveCount--; window.Refresh(); },
            () => { window.giveCount++; window.Refresh(); });
        window.getLabel = AddChoiceRow(panel, -196f, window.CycleGet,
            () => { window.getCount--; window.Refresh(); },
            () => { window.getCount++; window.Refresh(); });

        window.sendButton = AddButton(panel, "Send offer", new Vector2(18f, -270f), new Vector2(Width - 36f, 44f), AccentColor, window.Send);

        window.status = EconomyHUD.CreateText("Status", panel, 15f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(window.status.rectTransform, new Vector2(18f, -326f), new Vector2(Width - 36f, 80f));
        window.status.textWrappingMode = TextWrappingModes.Normal;
        return window;
    }

    /// <summary>Một dòng: nhãn bấm để đổi thẻ, cùng nút - và + đổi số lá.</summary>
    private static TextMeshProUGUI AddChoiceRow(RectTransform panel, float y,
        UnityEngine.Events.UnityAction onCycle, UnityEngine.Events.UnityAction onLess, UnityEngine.Events.UnityAction onMore)
    {
        RectTransform row = EconomyHUD.CreateRect("Choice", panel);
        SetTopLeft(row, new Vector2(18f, y), new Vector2(Width - 36f - 112f, 56f));
        EconomyHUD.AddButton(row, RowColor).onClick.AddListener(onCycle);

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", row, 16f, DarkText, TextAlignmentOptions.Left);
        EconomyHUD.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(12f, 0f);
        label.rectTransform.offsetMax = new Vector2(-12f, 0f);
        label.textWrappingMode = TextWrappingModes.Normal;

        AddButton(panel, "-", new Vector2(Width - 18f - 104f, y - 6f), new Vector2(48f, 44f), NeutralColor, onLess);
        AddButton(panel, "+", new Vector2(Width - 18f - 50f, y - 6f), new Vector2(48f, 44f), NeutralColor, onMore);
        return label;
    }

    private static Button AddButton(RectTransform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = EconomyHUD.CreateRect(text + "Button", parent);
        SetTopLeft(rect, position, size);
        Button button = EconomyHUD.AddButton(rect, color);
        button.onClick.AddListener(onClick);

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", rect, 17f, TextColor, TextAlignmentOptions.Center);
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

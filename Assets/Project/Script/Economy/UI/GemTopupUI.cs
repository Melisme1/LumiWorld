using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ô Gem ở hàng trên bên phải (bên trái nút World Map) và bảng "Top up Gems".
/// Gem chỉ có được qua nạp tiền (sandbox VNPay): chọn gói và kênh, game tạo đơn trên server rồi mở trình duyệt
/// tới /topup/pay (vé dùng một lần, không đưa token đăng nhập lên URL). Trong lúc chờ, bảng hỏi trạng thái đơn
/// mỗi 5 giây (tối đa 15 phút); đơn Paid thì GemWallet đọc lại số dư. Giá do server quyết định.
/// EconomyHUD tự tạo (chỉ trên đảo của mình). Chữ hiện ra là tiếng Anh.
/// </summary>
public class GemTopupUI : MonoBehaviour
{
    private const float PanelWidth = 680f;
    private const float PanelHeight = 600f;
    private const float PollSeconds = 5f;
    private const float WaitMinutes = 15f;

    private static readonly Color BackdropColor = new Color(0.04f, 0.05f, 0.12f, 0.65f);
    private static readonly Color PanelColor = new Color(0.97f, 0.96f, 1f, 0.99f);
    private static readonly Color CardColor = Color.white;
    private static readonly Color CardChosenColor = new Color(0.93f, 0.89f, 1f, 1f);
    private static readonly Color TextColor = new Color(0.11f, 0.09f, 0.19f, 1f);
    private static readonly Color MutedColor = new Color(0.39f, 0.36f, 0.49f, 1f);
    private static readonly Color AccentColor = new Color(0.49f, 0.23f, 0.93f, 1f);
    private static readonly Color DisabledColor = new Color(0.78f, 0.76f, 0.84f, 1f);
    private static readonly Color OkColor = new Color(0.14f, 0.5f, 0.29f, 1f);
    private static readonly Color WarnColor = new Color(0.7f, 0.22f, 0.17f, 1f);

    private static Sprite gemSprite;

    private TextMeshProUGUI chipValue;
    private RectTransform chip;
    private GameObject window;
    private RectTransform packageGrid;
    private RectTransform providerRow;
    private TextMeshProUGUI statusText;
    private Button checkButton;
    private Button stopWaitingButton;

    private readonly List<GemPackageDto> packages = new List<GemPackageDto>();
    private readonly List<PaymentProviderDto> providers = new List<PaymentProviderDto>();
    private GemPackageDto chosen;
    private bool busy;
    private GemOrderDto waiting;
    private Coroutine pollRoutine;

    public bool IsOpen => window != null && window.activeSelf;

    private void Start()
    {
        BuildChip();
        BuildWindow();
        GemWallet.Changed += HandleGemsChanged;
        ShowBalance(GemWallet.Balance);
        GemWallet.Refresh();
    }

    private void OnDestroy()
    {
        GemWallet.Changed -= HandleGemsChanged;
        if (window != null) EconomyHUD.UnregisterWindow(window);
    }

    // =========================================================
    // Ô GEM TRÊN HUD
    // =========================================================

    private void BuildChip()
    {
        // Hàng trên bên phải: Settings (x -70..-118), Coins (-126..-264), World Map (-274..-322); ô Gem nằm bên trái World Map (-334..-468)
        chip = EconomyHUD.CreateRect("GemChip", transform);
        chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(1f, 1f);
        chip.anchoredPosition = new Vector2(-334f, -14f);
        chip.sizeDelta = new Vector2(134f, 48f);
        // Nút bo tròn cùng màu ô Coins (AddButton tự tạo Image nền, không thêm CreatePanel: một object chỉ có một Image)
        Button open = EconomyHUD.AddButton(chip, LumiWorld.Acs.LumiWorldUITheme.Primary);
        open.onClick.AddListener(Toggle);
        LumiWorld.Acs.LumiWorldUITheme.AddSoftElevation(chip.gameObject, 3f, 0.16f);

        EconomyHUD.CreateIcon("GemIcon", chip, GemSprite, 26f, 10f);

        chipValue = EconomyHUD.CreateText("Value", chip, 22f, Color.white, TextAlignmentOptions.Center);
        if (LumiWorld.Acs.LumiWorldUITheme.Heading != null) chipValue.font = LumiWorld.Acs.LumiWorldUITheme.Heading;
        RectTransform valueRect = chipValue.rectTransform;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(40f, 2f);
        valueRect.offsetMax = new Vector2(-30f, -2f);
        chipValue.enableAutoSizing = true;
        chipValue.fontSizeMin = 14f;
        chipValue.fontSizeMax = 22f;

        // Dấu "+" báo bấm vào để nạp
        TextMeshProUGUI plus = EconomyHUD.CreateText("Plus", chip, 26f, new Color(0.85f, 0.78f, 1f, 1f), TextAlignmentOptions.Center);
        RectTransform plusRect = plus.rectTransform;
        plusRect.anchorMin = new Vector2(1f, 0f);
        plusRect.anchorMax = new Vector2(1f, 1f);
        plusRect.pivot = new Vector2(1f, 0.5f);
        plusRect.sizeDelta = new Vector2(28f, 0f);
        plusRect.anchoredPosition = new Vector2(-4f, 0f);
        plus.text = "+";
    }

    private void HandleGemsChanged(long balance, long gained)
    {
        ShowBalance(balance);
        if (gained > 0 && chip != null && isActiveAndEnabled)
        {
            StartCoroutine(EconomyHUD.Punch(chip, 1.12f, 0.25f));
            StartCoroutine(EconomyHUD.FloatText(chip, "+" + Format(gained), new Color(0.75f, 0.6f, 1f, 1f), 24f,
                new Vector2(0f, -70f), new Vector2(0f, -40f), 1.2f));
        }
    }

    private void ShowBalance(long balance) => chipValue.text = balance < 0 ? "–" : Format(balance);

    // =========================================================
    // BẢNG TOP UP
    // =========================================================

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (window == null || IsOpen) return;
        transform.SetAsLastSibling();
        window.SetActive(true);
        EconomyHUD.BringWindowToFront(window);
        if (waiting == null) SetStatus("Loading packages…", false);
        StartCoroutine(LoadPackages());
        GemWallet.Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        window.SetActive(false);
        EconomyHUD.MarkWindowClosed();
    }

    private IEnumerator LoadPackages()
    {
        GemPackagesResponse list = null;
        PaymentProvidersResponse kinds = null;
        ApiError failure = null;
        yield return ApiClient.Get<GemPackagesResponse>("/api/v1/gems/packages", false, r => list = r, e => failure = e);
        yield return ApiClient.Get<PaymentProvidersResponse>("/api/v1/payments/providers", false, r => kinds = r, e => failure = e);

        if (list == null || kinds == null)
        {
            SetStatus(MarketSync.Describe(failure), true);
            yield break;
        }

        packages.Clear();
        packages.AddRange(list.items);
        providers.Clear();
        providers.AddRange(kinds.items);
        if (chosen != null) chosen = packages.Find(p => p.id == chosen.id);
        RenderPackages();
        RenderProviders();
        if (waiting == null) SetStatus(AuthSession.IsLoggedIn ? "Choose a package." : "Log in to buy Gems.", false);
    }

    private void RenderPackages()
    {
        EconomyHUD.ClearChildren(packageGrid);
        const float cardW = 200f, cardH = 126f, gap = 14f;
        for (int i = 0; i < packages.Count; i++)
        {
            GemPackageDto pack = packages[i];
            RectTransform card = EconomyHUD.CreateRect("Package_" + pack.id, packageGrid);
            SetTopLeft(card, new Vector2((i % 3) * (cardW + gap), -(i / 3) * (cardH + gap)), new Vector2(cardW, cardH));
            Button button = EconomyHUD.AddButton(card, chosen == pack ? CardChosenColor : CardColor);
            button.onClick.AddListener(() => { chosen = pack; RenderPackages(); RenderProviders(); });

            Image icon = EconomyHUD.CreateIcon("Gem", card, GemSprite, 30f, (cardW - 30f) / 2f);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -10f);

            Line(card, pack.name, 14f, MutedColor, -42f, 20f);
            Line(card, Format(pack.totalGems) + " Gems", 22f, TextColor, -62f, 28f);
            Line(card, pack.bonusGems > 0 ? $"{Format(pack.gems)} + {Format(pack.bonusGems)} bonus" : " ", 13f, OkColor, -88f, 18f);
            Line(card, Vnd(pack.priceVnd), 16f, AccentColor, -104f, 20f);
        }
    }

    private void RenderProviders()
    {
        EconomyHUD.ClearChildren(providerRow);
        for (int i = 0; i < providers.Count; i++)
        {
            PaymentProviderDto provider = providers[i];
            // Đang chờ một đơn vẫn bấm được: người chơi có thể đã đóng trang VNPay, chọn gói khác để thanh toán lại
            bool usable = provider.available && chosen != null && !busy && AuthSession.IsLoggedIn;
            RectTransform rect = EconomyHUD.CreateRect("Pay_" + provider.id, providerRow);
            SetTopLeft(rect, new Vector2(i * 228f, 0f), new Vector2(214f, 46f));
            Button button = EconomyHUD.AddButton(rect, provider.available ? AccentColor : DisabledColor);
            button.interactable = usable;
            string id = provider.id;
            button.onClick.AddListener(() => StartCoroutine(Pay(id)));
            TextMeshProUGUI label = EconomyHUD.CreateText("Label", rect, 17f, Color.white, TextAlignmentOptions.Center);
            EconomyHUD.Stretch(label.rectTransform);
            label.text = provider.available ? "Pay with " + provider.name : provider.name + " (soon)";
        }
    }

    // =========================================================
    // TẠO ĐƠN, MỞ TRÌNH DUYỆT, CHỜ KẾT QUẢ
    // =========================================================

    private IEnumerator Pay(string providerId)
    {
        if (busy || chosen == null) yield break;
        // Đang chờ đơn cũ (đóng trang VNPay giữa chừng): bỏ chờ đơn đó, đơn mới thay thế
        if (waiting != null) StopWaiting(null);
        busy = true;
        RenderProviders();
        SetStatus("Creating your order…", false);

        GemOrderDto order = null;
        ApiError failure = null;
        yield return ApiClient.Post<CreateGemOrderRequest, GemOrderDto>("/api/v1/payments/orders",
            new CreateGemOrderRequest { packageId = chosen.id, provider = providerId }, true,
            r => order = r, e => failure = e, Guid.NewGuid().ToString("N"));
        busy = false;

        if (order == null || string.IsNullOrEmpty(order.payUrl))
        {
            SetStatus(MarketSync.Describe(failure), true);
            RenderProviders();
            yield break;
        }

        waiting = order;
        RenderProviders();
        Application.OpenURL(order.payUrl);
        SetStatus($"Order {order.orderCode}: finish the payment in your browser, then come back. Waiting…\n" +
                  "Closed the payment page? Choose another package or press \"Choose another package\".", false);
        checkButton.gameObject.SetActive(true);
        stopWaitingButton.gameObject.SetActive(true);
        if (pollRoutine != null) StopCoroutine(pollRoutine);
        pollRoutine = StartCoroutine(Poll(order));
    }

    private IEnumerator Poll(GemOrderDto order)
    {
        float until = Time.realtimeSinceStartup + WaitMinutes * 60f;
        while (waiting == order && Time.realtimeSinceStartup < until)
        {
            yield return CheckOnce(order);
            if (waiting != order) yield break;
            yield return new WaitForSecondsRealtime(PollSeconds);
        }
        if (waiting == order) Finish("The payment was not finished in time. You have not been charged; choose a package to try again.", true);
    }

    private IEnumerator CheckOnce(GemOrderDto order)
    {
        GemOrderDto status = null;
        yield return ApiClient.Get<GemOrderDto>("/api/v1/payments/orders/" + order.id, true, r => status = r, _ => { });
        if (status == null || waiting != order) yield break;

        switch (status.status)
        {
            case "Paid":
                GemWallet.Refresh();
                InboxSync.RequestSync();
                Finish($"Payment successful: +{Format(status.gems)} Gems!", false);
                break;
            case "Cancelled":
                Finish("Payment cancelled. No Gems were added and you were not charged.", true);
                break;
            case "Failed":
            case "Expired":
                Finish("The payment did not go through. You were not charged; choose a package to try again.", true);
                break;
        }
    }

    private void CheckNow()
    {
        if (waiting != null) StartCoroutine(CheckOnce(waiting));
    }

    private void Finish(string message, bool warning)
    {
        waiting = null;
        if (pollRoutine != null) { StopCoroutine(pollRoutine); pollRoutine = null; }
        checkButton.gameObject.SetActive(false);
        stopWaitingButton.gameObject.SetActive(false);
        SetStatus(message, warning);
        RenderProviders();
    }

    /// <summary>
    /// Thôi chờ đơn hiện tại (người chơi đã đóng trang thanh toán). Đơn không bị hủy trên server: tự hết hạn sau 15 phút,
    /// và nếu thật ra đã trả tiền thì Gem vẫn được cộng (hộp thư GemsPurchased cập nhật ô Gem).
    /// </summary>
    private void StopWaiting(string message)
    {
        if (waiting == null) return;
        string code = waiting.orderCode;
        Finish(message ?? $"Stopped waiting for order {code}. If you did pay for it, the Gems will still arrive.", false);
    }

    // =========================================================
    // DỰNG UI
    // =========================================================

    private void BuildWindow()
    {
        RectTransform windowRect = EconomyHUD.CreateRect("GemTopupWindow", transform);
        EconomyHUD.Stretch(windowRect);
        window = windowRect.gameObject;

        RectTransform backdrop = EconomyHUD.CreateRect("Backdrop", windowRect);
        EconomyHUD.Stretch(backdrop);
        EconomyHUD.AddButton(backdrop, BackdropColor).onClick.AddListener(Close);

        RectTransform panel = EconomyHUD.CreateRect("Panel", windowRect);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        EconomyHUD.CreatePanel(panel, PanelColor).raycastTarget = true;

        TextMeshProUGUI title = EconomyHUD.CreateText("Title", panel, 26f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(title.rectTransform, new Vector2(24f, -18f), new Vector2(PanelWidth - 160f, 36f));
        title.text = "Top up Gems";

        RectTransform close = EconomyHUD.CreateRect("Close", panel);
        SetTopLeft(close, new Vector2(PanelWidth - 112f, -16f), new Vector2(92f, 38f));
        EconomyHUD.AddButton(close, DisabledColor).onClick.AddListener(Close);
        TextMeshProUGUI closeLabel = EconomyHUD.CreateText("Label", close, 17f, TextColor, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(closeLabel.rectTransform);
        closeLabel.text = "Close";

        TextMeshProUGUI note = EconomyHUD.CreateText("Note", panel, 14f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(note.rectTransform, new Vector2(24f, -56f), new Vector2(PanelWidth - 48f, 24f));
        note.text = "Gems can only be bought here. Test mode: VNPay sandbox, no real money is charged.";

        packageGrid = EconomyHUD.CreateRect("Packages", panel);
        SetTopLeft(packageGrid, new Vector2(26f, -92f), new Vector2(PanelWidth - 52f, 280f));

        providerRow = EconomyHUD.CreateRect("Providers", panel);
        SetTopLeft(providerRow, new Vector2(26f, -392f), new Vector2(PanelWidth - 52f, 46f));

        statusText = EconomyHUD.CreateText("Status", panel, 15f, MutedColor, TextAlignmentOptions.TopLeft);
        SetTopLeft(statusText.rectTransform, new Vector2(26f, -452f), new Vector2(PanelWidth - 52f, 70f));
        statusText.textWrappingMode = TextWrappingModes.Normal;

        RectTransform check = EconomyHUD.CreateRect("CheckAgain", panel);
        SetTopLeft(check, new Vector2(26f, -532f), new Vector2(260f, 42f));
        checkButton = EconomyHUD.AddButton(check, AccentColor);
        checkButton.onClick.AddListener(CheckNow);
        TextMeshProUGUI checkLabel = EconomyHUD.CreateText("Label", check, 16f, Color.white, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(checkLabel.rectTransform);
        checkLabel.text = "I've paid, check again";
        check.gameObject.SetActive(false);

        RectTransform stop = EconomyHUD.CreateRect("StopWaiting", panel);
        SetTopLeft(stop, new Vector2(300f, -532f), new Vector2(260f, 42f));
        stopWaitingButton = EconomyHUD.AddButton(stop, DisabledColor);
        stopWaitingButton.onClick.AddListener(() => StopWaiting(null));
        TextMeshProUGUI stopLabel = EconomyHUD.CreateText("Label", stop, 16f, TextColor, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(stopLabel.rectTransform);
        stopLabel.text = "Choose another package";
        stop.gameObject.SetActive(false);

        EconomyHUD.RegisterWindow(window, Close);
        window.SetActive(false);
    }

    private static void Line(RectTransform parent, string text, float size, Color color, float y, float height)
    {
        TextMeshProUGUI line = EconomyHUD.CreateText("Line", parent, size, color, TextAlignmentOptions.Center);
        RectTransform rect = line.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-12f, height);
        line.textWrappingMode = TextWrappingModes.NoWrap;
        line.overflowMode = TextOverflowModes.Ellipsis;
        line.text = text;
    }

    private void SetStatus(string message, bool warning)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = warning ? WarnColor : MutedColor;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static string Format(long value) => value.ToString("#,0", CultureInfo.InvariantCulture);

    // Giá VNĐ kiểu Việt Nam: 50.000 ₫
    private static string Vnd(int value) => value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.') + " ₫";

    /// <summary>Viên ngọc tím (hình thoi chuyển màu), vẽ bằng code như đồng Coin.</summary>
    private static Sprite GemSprite
    {
        get
        {
            if (gemSprite != null) return gemSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Color top = new Color(0.85f, 0.78f, 1f, 1f), bottom = new Color(0.45f, 0.2f, 0.9f, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - size / 2f) / (size * 0.42f);
                    float dy = (y + 0.5f - size * 0.62f) / (y >= size * 0.62f ? size * 0.34f : size * 0.6f);
                    float d = dx + Mathf.Abs(dy);
                    float alpha = Mathf.Clamp01((1f - d) * size * 0.2f);
                    Color c = Color.Lerp(bottom, top, (float)y / size);
                    if (x < size / 2) c = Color.Lerp(c, Color.white, 0.12f); // mặt trái sáng hơn
                    c.a = alpha;
                    texture.SetPixel(x, y, c);
                }
            texture.Apply();
            gemSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return gemSprite;
        }
    }
}

using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Số Lumi Coin ở góc trên màn chơi. Khi số dư đổi, con số chạy dần tới số mới, ô Coins nảy nhẹ
/// và hiện số bay: +75 màu xanh bay lên vào ô, -10 màu đỏ rơi khỏi ô.
/// EconomyHUD tự tạo script này, không cần gắn tay.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CoinHUD : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private Vector2 panelSize = new Vector2(170f, 48f);
    [SerializeField] private Color backgroundColor = new Color(0.06f, 0.09f, 0.16f, 0.88f);
    [SerializeField] private Color valueColor = Color.white;
    [SerializeField] private Color gainColor = new Color(0.45f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color spendColor = new Color(1f, 0.42f, 0.38f, 1f);

    [Header("Motion")]
    [Tooltip("Thời gian con số chạy từ số dư cũ tới số dư mới (giây)")]
    [SerializeField] private float countDuration = 0.45f;
    [Tooltip("Thời gian số bay +/- hiện trên màn hình (giây)")]
    [SerializeField] private float floatDuration = 1.1f;
    [Tooltip("Quãng đường số bay di chuyển (pixel)")]
    [SerializeField] private float floatDistance = 34f;

    private CurrencyWallet wallet;
    private RectTransform body;
    private RectTransform floatLayer;
    private TextMeshProUGUI valueText;
    private int shownBalance;
    private Coroutine countRoutine;
    private Coroutine punchRoutine;

    private void Awake()
    {
        Build();
    }

    private void Start()
    {
        // Lần đầu gọi Instance sẽ tạo ví và nạp số dư đã lưu (người chơi mới nhận Starter Kit)
        wallet = CurrencyWallet.Instance;
        ShowBalance(wallet.Balance);
        wallet.OnBalanceChanged += HandleBalanceChanged;
    }

    private void OnDisable()
    {
        // Coroutine dừng khi HUD bị ẩn: chốt về trạng thái cuối để lúc hiện lại không kẹt giữa chừng
        countRoutine = null;
        punchRoutine = null;
        if (body != null) body.localScale = Vector3.one;
        if (wallet != null && valueText != null) ShowBalance(wallet.Balance);
        EconomyHUD.ClearChildren(floatLayer);
    }

    private void OnDestroy()
    {
        if (wallet != null) wallet.OnBalanceChanged -= HandleBalanceChanged;
    }

    private void Build()
    {
        RectTransform rect = (RectTransform)transform;
        rect.sizeDelta = panelSize;

        // Body nảy khi số dư đổi; số bay nằm ngoài Body nên không nảy theo
        body = EconomyHUD.CreateRect("Body", rect);
        EconomyHUD.Stretch(body);
        EconomyHUD.CreatePanel(body, backgroundColor);

        float iconSize = panelSize.y - 16f;
        EconomyHUD.CreateIcon("CoinIcon", body, EconomyHUD.CoinSprite, iconSize, 10f);

        valueText = EconomyHUD.CreateText("Value", body, 26f, valueColor, TextAlignmentOptions.Center);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(10f + iconSize + 4f, 2f);
        valueRect.offsetMax = new Vector2(-12f, -2f);
        valueText.enableAutoSizing = true;
        valueText.fontSizeMin = 14f;
        valueText.fontSizeMax = 26f;
        valueText.text = "0";

        floatLayer = EconomyHUD.CreateRect("FloatingNumbers", rect);
        EconomyHUD.Stretch(floatLayer);
    }

    private void HandleBalanceChanged(int balance, int delta, CoinReason reason)
    {
        if (!isActiveAndEnabled)
        {
            ShowBalance(balance);
            return;
        }

        if (countRoutine != null) StopCoroutine(countRoutine);
        countRoutine = StartCoroutine(CountTo(balance));

        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(EconomyHUD.Punch(body, 1.12f, 0.25f));

        if (delta > 0)
        {
            // Coins vào: số bay từ dưới lên, chui vào ô
            StartCoroutine(EconomyHUD.FloatText(floatLayer, "+" + FormatCoins(delta), gainColor, 24f,
                new Vector2(0f, -floatDistance - 14f), new Vector2(0f, -14f), floatDuration));
        }
        else if (delta < 0)
        {
            // Coins ra: số bay từ ô rơi xuống
            StartCoroutine(EconomyHUD.FloatText(floatLayer, "-" + FormatCoins(-delta), spendColor, 24f,
                new Vector2(0f, -14f), new Vector2(0f, -floatDistance - 14f), floatDuration));
        }
    }

    private IEnumerator CountTo(int target)
    {
        int start = shownBalance;
        float time = 0f;
        while (time < countDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / countDuration));
            ShowBalance(Mathf.RoundToInt(Mathf.Lerp(start, target, t)));
            yield return null;
        }

        ShowBalance(target);
        countRoutine = null;
    }

    private void ShowBalance(int value)
    {
        shownBalance = value;
        valueText.text = FormatCoins(value);
    }

    private static string FormatCoins(int value)
    {
        return value.ToString("#,0", CultureInfo.InvariantCulture);
    }
}

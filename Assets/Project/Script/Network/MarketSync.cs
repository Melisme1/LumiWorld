using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Chợ listing: đăng bán, mua cả lô, hủy, và gửi lại các thao tác còn chờ server.
/// Hàng hoặc Coins được trừ trên máy trước rồi mới gửi, và thao tác chờ được lưu cùng bản lưu.
/// Server từ chối hẳn: trả lại hàng hoặc Coins. Mất mạng hoặc server lỗi: giữ thao tác và gửi lại đúng opKey,
/// nên một lô không bao giờ được tạo hai lần và một lô mua không bị trừ Coins hai lần.
/// Tự tạo khi game mở, không cần đặt trong scene.
/// </summary>
public class MarketSync : MonoBehaviour
{
    public const string CreateKind = "CreateListing";
    public const string BuyKind = "BuyListing";
    public const string CancelKind = "CancelListing";

    private const int ResendSeconds = 60;

    // Giá trị dùng khi chưa tải được tham số từ server (khớp server)
    private const int DefaultFeePercent = 5;
    private const int DefaultMaxActiveListings = 5;
    private const int DefaultListingHours = 72;
    private const float DefaultMinPriceFactor = 0.5f;
    private const float DefaultMaxPriceFactor = 3f;

    private static MarketSync _instance;

    // Tham số chợ từ GET /api/v1/config
    private static int feePercent = DefaultFeePercent;
    private static int maxActiveListings = DefaultMaxActiveListings;
    private static int listingHours = DefaultListingHours;
    private static float minPriceFactor = DefaultMinPriceFactor;
    private static float maxPriceFactor = DefaultMaxPriceFactor;

    // Các opKey đang được gửi, để một thao tác không gửi hai lượt cùng lúc
    private static readonly HashSet<string> inFlight = new HashSet<string>();

    public static int FeePercent => feePercent;
    public static int MaxActiveListings => maxActiveListings;
    public static int ListingHours => listingHours;

    public static MarketSync Instance
    {
        get
        {
            if (_instance == null) Create();
            return _instance;
        }
    }

    // Khi tắt Domain Reload, biến static không tự xóa: xóa lúc vào Play Mode
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        inFlight.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (_instance != null) return;

        var go = new GameObject("MarketSync");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MarketSync>();
    }

    private void Start()
    {
        StartCoroutine(ResendLoop());
    }

    private IEnumerator ResendLoop()
    {
        while (true)
        {
            StartCoroutine(LoadConfig());
            ResendPending();
            yield return new WaitForSecondsRealtime(ResendSeconds);
        }
    }

    private static IEnumerator LoadConfig()
    {
        yield return ApiClient.Get<GameConfigDto>("/api/v1/config", false,
            config =>
            {
                if (config == null) return;
                if (config.marketFeePercent >= 0) feePercent = config.marketFeePercent;
                if (config.marketMaxActiveListings > 0) maxActiveListings = config.marketMaxActiveListings;
                if (config.marketListingHours > 0) listingHours = config.marketListingHours;
                if (config.marketMinPriceFactor > 0f) minPriceFactor = config.marketMinPriceFactor;
                if (config.marketMaxPriceFactor > 0f) maxPriceFactor = config.marketMaxPriceFactor;
            },
            error => Debug.LogWarning($"[LumiWorld Chợ] Không đọc được tham số chợ, dùng mặc định: {error}"));
    }

    /// <summary>
    /// Gửi lại mọi thao tác chợ còn chờ. Gọi khi vào game và mỗi phút; chưa đăng nhập thì bỏ qua.
    /// </summary>
    public static void ResendPending()
    {
        if (!AuthSession.IsLoggedIn) return;

        foreach (PendingMarketOp op in EconomySaveSystem.Instance.PendingMarketOps)
            Send(op, null);
    }

    // =========================================================
    // ĐỌC CHỢ
    // =========================================================

    /// <summary>
    /// Lô còn hạn, lọc theo tài nguyên (null là tất cả), sắp theo giá mỗi đơn vị hoặc mới nhất.
    /// </summary>
    /// <param name="kind">null = mọi lô, "resource" = chỉ tài nguyên, "card" = chỉ thẻ thú.</param>
    public static void Browse(string resourceId, bool newestFirst, int page, int pageSize,
        Action<MarketListingsResponse> onDone, Action<string> onError, string kind = null)
    {
        string url = "/api/v1/market/listings?page=" + page + "&pageSize=" + pageSize +
                     "&sort=" + (newestFirst ? "newest" : "pricePerUnit");
        if (!string.IsNullOrEmpty(resourceId)) url += "&resourceId=" + UnityWebRequest.EscapeURL(resourceId);
        if (!string.IsNullOrEmpty(kind)) url += "&kind=" + kind;

        Instance.StartCoroutine(ApiClient.Get<MarketListingsResponse>(url, true, onDone,
            error => onError?.Invoke(Describe(error))));
    }

    /// <summary>
    /// Các lô đang bán của chính người chơi.
    /// </summary>
    public static void Mine(Action<MarketListingsResponse> onDone, Action<string> onError)
    {
        Instance.StartCoroutine(ApiClient.Get<MarketListingsResponse>("/api/v1/market/listings/mine", true, onDone,
            error => onError?.Invoke(Describe(error))));
    }

    // =========================================================
    // THAO TÁC: trừ trên máy, ghi thao tác chờ, rồi gửi
    // =========================================================

    /// <summary>
    /// Đưa hàng từ kho lên chợ. Hàng rời kho cùng lúc với thao tác chờ được ghi.
    /// </summary>
    public static void Sell(string resourceId, int amount, int priceCoins, Action<bool, string> done)
    {
        if (CardTradeIds.IsCardId(resourceId))
        {
            SellCards(resourceId, amount, priceCoins, done);
            return;
        }

        ResourceData resource = ResourceCatalog.Find(resourceId);
        if (resource == null)
        {
            done?.Invoke(false, "Unknown resource.");
            return;
        }

        ResourceInventory inventory = ResourceInventory.Instance;
        var stock = new List<ResourceStack> { new ResourceStack { resourceId = resourceId, amount = amount } };
        if (!inventory.TryRemoveFromAccount(stock))
        {
            done?.Invoke(false, "Not enough " + resource.resourceName + " in your inventory.");
            return;
        }

        PendingMarketOp op = NewOp(CreateKind);
        op.resourceId = resourceId;
        op.amount = amount;
        op.priceCoins = priceCoins;

        // Không ghi được file thì trả hàng lại kho, không để hàng mất
        if (!EconomySaveSystem.Instance.AddPendingMarketOp(op))
        {
            inventory.AddFromAccount(resource, amount);
            done?.Invoke(false, "Could not save. Nothing was listed.");
            return;
        }

        Send(op, done);
    }

    /// <summary>
    /// Đưa thẻ thú từ tay lên chợ. Thẻ rời tay và thao tác chờ nằm trong cùng một lần ghi file.
    /// </summary>
    private static void SellCards(string marketId, int amount, int priceCoins, Action<bool, string> done)
    {
        CardData card = CardTradeIds.Find(marketId);
        if (card == null)
        {
            done?.Invoke(false, "Only creature cards can be sold.");
            return;
        }
        if (!HandCards.Available)
        {
            done?.Invoke(false, "Your hand is not ready yet. Try again in a moment.");
            return;
        }
        if (!HandCards.TakeWithoutSaving(card.cardID, amount))
        {
            done?.Invoke(false, "Not enough " + card.cardName + " cards in your hand.");
            return;
        }

        PendingMarketOp op = NewOp(CreateKind);
        op.resourceId = marketId;
        op.amount = amount;
        op.priceCoins = priceCoins;

        // Không ghi được file thì trả thẻ lại tay (lần ghi sau sẽ lưu đúng tay bài), không để thẻ mất
        if (!EconomySaveSystem.Instance.AddPendingMarketOp(op))
        {
            HandCards.AddWithoutSaving(card, amount);
            done?.Invoke(false, "Could not save. Nothing was listed.");
            return;
        }

        Send(op, done);
    }

    /// <summary>
    /// Mua cả lô. Coins được trừ trên máy; lô đã bán thì server từ chối và Coins được hoàn lại.
    /// Thẻ thú mua được về tay qua hộp thư (đang thăm đảo khác thì về khi quay lại đảo mình).
    /// </summary>
    public static void Buy(MarketListingDto lot, Action<bool, string> done)
    {
        bool isCard = CardTradeIds.IsCardId(lot.resourceId);
        ResourceData resource = isCard ? null : ResourceCatalog.Find(lot.resourceId);
        if (isCard ? CardTradeIds.Find(lot.resourceId) == null : resource == null)
        {
            done?.Invoke(false, isCard ? "Unknown card." : "Unknown resource.");
            return;
        }

        CurrencyWallet wallet = CurrencyWallet.Instance;
        if (!wallet.CanAfford(lot.priceCoins))
        {
            done?.Invoke(false, "Not enough Coins.");
            return;
        }

        if (!isCard && ResourceInventory.Instance.GetFreeSpace(resource) < lot.amount)
        {
            done?.Invoke(false, "Not enough room in your inventory for " + lot.amount + " " + resource.resourceName + ".");
            return;
        }

        PendingMarketOp op = NewOp(BuyKind);
        op.listingId = lot.id;
        op.resourceId = lot.resourceId;
        op.amount = lot.amount;
        op.priceCoins = lot.priceCoins;

        wallet.TrySpend(lot.priceCoins, CoinReason.MarketPurchase);
        if (!EconomySaveSystem.Instance.AddPendingMarketOp(op))
        {
            wallet.Add(lot.priceCoins, CoinReason.MarketRefund);
            done?.Invoke(false, "Could not save. Nothing was bought.");
            return;
        }

        Send(op, done);
    }

    /// <summary>
    /// Hủy lô của mình. Hàng trả về qua hộp thư (ItemsReturned).
    /// </summary>
    public static void Cancel(string listingId, Action<bool, string> done)
    {
        PendingMarketOp op = NewOp(CancelKind);
        op.listingId = listingId;

        if (!EconomySaveSystem.Instance.AddPendingMarketOp(op))
        {
            done?.Invoke(false, "Could not save. Try again.");
            return;
        }

        Send(op, done);
    }

    private static void Send(PendingMarketOp op, Action<bool, string> done)
    {
        // Lượt định kỳ gặp thao tác đang gửi thì bỏ qua, server vẫn chỉ làm một lần nhờ opKey
        if (!inFlight.Add(op.opKey)) return;

        // Kết quả về sau khi đã đổi tài khoản thì bỏ: bản lưu đang mở không còn là của người gửi.
        // Thao tác vẫn nằm trong bản lưu cũ và được gửi lại đúng opKey khi tài khoản đó vào lại.
        string owner = AuthSession.PlayerId;
        bool AccountChanged()
        {
            if (AuthSession.PlayerId == owner) return false;
            inFlight.Remove(op.opKey);
            return true;
        }
        void Ok(string message) { if (!AccountChanged()) Succeed(op, message, done); }
        void Err(ApiError error) { if (!AccountChanged()) Fail(op, error, done); }

        MarketSync sync = Instance;
        switch (op.kind)
        {
            case CreateKind:
                sync.StartCoroutine(ApiClient.Post<CreateListingRequest, MarketListingDto>(
                    "/api/v1/market/listings",
                    new CreateListingRequest { resourceId = op.resourceId, amount = op.amount, priceCoins = op.priceCoins },
                    true,
                    listing => Ok("Listed " + op.amount + " " + NameOf(op.resourceId) +
                        " for " + op.priceCoins + " Coins."),
                    Err,
                    op.opKey));
                break;

            case BuyKind:
                sync.StartCoroutine(ApiClient.Post<EmptyRequest, MarketPurchaseResponse>(
                    "/api/v1/market/listings/" + op.listingId + "/buy",
                    new EmptyRequest(),
                    true,
                    purchase => Ok("Bought " + purchase.amount + " " + NameOf(purchase.resourceId) +
                        (CardTradeIds.IsCardId(purchase.resourceId)
                            ? ". The cards will arrive in your hand."
                            : ". It will arrive in your inventory.")),
                    Err,
                    op.opKey));
                break;

            case CancelKind:
                sync.StartCoroutine(ApiClient.Post<EmptyRequest, MarketCancelResponse>(
                    "/api/v1/market/listings/" + op.listingId + "/cancel",
                    new EmptyRequest(),
                    true,
                    cancel => Ok("Lot cancelled. Goods return to your inventory when there is room; cards return to your hand."),
                    Err,
                    op.opKey));
                break;

            default:
                inFlight.Remove(op.opKey);
                Debug.LogWarning($"[LumiWorld Chợ] Loại thao tác chưa hỗ trợ: {op.kind}");
                break;
        }
    }

    private static void Succeed(PendingMarketOp op, string message, Action<bool, string> done)
    {
        inFlight.Remove(op.opKey);
        EconomySaveSystem.Instance.RemovePendingMarketOp(op.opKey);

        // Hàng mua được hoặc hàng trả lại: lấy về ngay thay vì đợi lần đồng bộ sau
        if (op.kind != CreateKind) InboxSync.RequestSync();

        done?.Invoke(true, message);
    }

    private static void Fail(PendingMarketOp op, ApiError error, Action<bool, string> done)
    {
        inFlight.Remove(op.opKey);
        string message = Describe(error);

        // Mất mạng, hết phiên, server bận: chưa biết server có làm không, nên giữ thao tác để gửi lại
        if (!IsDefinitiveRefusal(error))
        {
            done?.Invoke(false, message + " We'll send it again automatically.");
            return;
        }

        // Lô thẻ bị từ chối khi đang thăm đảo khác: chưa trả thẻ về tay được, giữ thao tác.
        // Lần gửi lại ở đảo mình server từ chối lần nữa và thẻ được trả lúc đó.
        if (op.kind == CreateKind && CardTradeIds.IsCardId(op.resourceId) && !HandCards.Available)
        {
            done?.Invoke(false, message);
            return;
        }

        // Server từ chối hẳn: trả lại hàng hoặc Coins đã trừ trên máy
        Refund(op);
        EconomySaveSystem.Instance.RemovePendingMarketOp(op.opKey);
        done?.Invoke(false, message);
    }

    private static void Refund(PendingMarketOp op)
    {
        switch (op.kind)
        {
            case CreateKind when CardTradeIds.IsCardId(op.resourceId):
            {
                // Thẻ về tay; RemovePendingMarketOp sau đó ghi file cùng tay bài
                CardData card = CardTradeIds.Find(op.resourceId);
                if (card != null) HandCards.AddWithoutSaving(card, op.amount);
                else Debug.LogWarning($"[LumiWorld Chợ] Không tra được thẻ {op.resourceId} khi trả lại {op.amount} lá.");
                break;
            }

            case CreateKind:
            {
                ResourceData resource = ResourceCatalog.Find(op.resourceId);
                int returned = resource != null ? ResourceInventory.Instance.AddFromAccount(resource, op.amount) : 0;
                if (returned < op.amount)
                    Debug.LogWarning($"[LumiWorld Chợ] Kho đầy khi trả hàng, chỉ trả lại {returned}/{op.amount} {op.resourceId}.");
                break;
            }

            case BuyKind:
                CurrencyWallet.Instance.Add(op.priceCoins, CoinReason.MarketRefund);
                break;

            // Hủy lô không trừ gì trên máy nên không có gì để trả
        }
    }

    private static bool IsDefinitiveRefusal(ApiError error)
    {
        if (error == null || error.IsNetworkError) return false;
        if (error.Code == "OPERATION_IN_PROGRESS") return false;

        // 401 hết phiên, 408 và 429 quá tải, 5xx server lỗi: đều là thử lại được
        return error.StatusCode >= 400 && error.StatusCode < 500
            && error.StatusCode != 401 && error.StatusCode != 408 && error.StatusCode != 429;
    }

    // =========================================================
    // HỖ TRỢ HIỂN THỊ
    // =========================================================

    /// <summary>
    /// Khoảng giá cả lô hợp lệ, theo đúng công thức của server (MarketRules).
    /// </summary>
    public static void PriceRange(int amount, int baseValue, out int min, out int max)
    {
        decimal unit = Math.Max(baseValue, 1);
        decimal low = Math.Round((decimal)minPriceFactor, 3);
        decimal high = Math.Round((decimal)maxPriceFactor, 3);
        min = (int)Math.Max(2m, Math.Floor(amount * low * unit));
        max = (int)Math.Floor(amount * high * unit);
    }

    /// <summary>
    /// Phí chợ: phần trăm giá, làm tròn lên, ít nhất 1 Coin.
    /// </summary>
    public static int Fee(int priceCoins) => (int)Math.Max(1L, ((long)priceCoins * feePercent + 99) / 100);

    public static string NameOf(string resourceId)
    {
        if (CardTradeIds.IsCardId(resourceId))
        {
            CardData card = CardTradeIds.Find(resourceId);
            return (card != null ? card.cardName : CardTradeIds.ToCardId(resourceId)) + " card";
        }

        ResourceData resource = ResourceCatalog.Find(resourceId);
        return resource != null ? resource.resourceName : resourceId;
    }

    /// <summary>
    /// Lời nhắn ngắn tiếng Anh cho người chơi. Server đã trả lời bằng tiếng Anh nên dùng luôn.
    /// </summary>
    public static string Describe(ApiError error)
    {
        if (error == null) return "Something went wrong.";
        if (error.IsNetworkError) return "No connection to the server.";
        return string.IsNullOrEmpty(error.Detail) ? "The server refused the request." : error.Detail;
    }

    private static PendingMarketOp NewOp(string kind) => new PendingMarketOp
    {
        opKey = Guid.NewGuid().ToString("N"),
        kind = kind,
        createdAtUtc = DateTime.UtcNow.ToString("o")
    };
}

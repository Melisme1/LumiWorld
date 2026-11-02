using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đổi thẻ thú với người chơi khác: gửi đề nghị lúc thăm đảo, đồng ý, từ chối, hủy, và gửi lại thao tác còn chờ.
/// Một loại thẻ đổi một loại thẻ, không Coins, không phí. Giống MarketSync: thẻ rời tay cùng lúc thao tác chờ
/// được ghi; server từ chối hẳn thì thẻ về tay; mất mạng thì gửi lại đúng opKey. Thẻ nhận được về qua hộp thư.
/// Tự tạo khi game mở, không cần đặt trong scene.
/// </summary>
public class TradeSync : MonoBehaviour
{
    public const string OfferKind = "Offer";
    public const string AcceptKind = "Accept";
    public const string DeclineKind = "Decline";
    public const string CancelKind = "Cancel";

    // Khớp trade.maxCardsPerSide trên server
    public const int MaxCardsPerSide = 10;

    private const int ResendSeconds = 60;

    private static TradeSync _instance;
    private static readonly HashSet<string> inFlight = new HashSet<string>();

    /// <summary>Có đề nghị mới gửi tới mình (từ hộp thư). Tham số: tên người gửi.</summary>
    public static event Action<string> OfferReceived;

    public static TradeSync Instance
    {
        get
        {
            if (_instance == null) Create();
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        inFlight.Clear();
        OfferReceived = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (_instance != null) return;

        var go = new GameObject("TradeSync");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TradeSync>();
    }

    private void Start()
    {
        StartCoroutine(ResendLoop());
    }

    private IEnumerator ResendLoop()
    {
        while (true)
        {
            ResendPending();
            yield return new WaitForSecondsRealtime(ResendSeconds);
        }
    }

    public static void ResendPending()
    {
        if (!AuthSession.IsLoggedIn || EconomySaveSystem.Instance == null) return;

        foreach (PendingTradeOp op in EconomySaveSystem.Instance.PendingTradeOps)
            Send(op, null);
    }

    /// <summary>Gọi từ EconomySaveSystem khi áp mục hộp thư TradeOffered.</summary>
    public static void NotifyOffered(TradeOfferedPayload offer)
    {
        string from = offer != null && !string.IsNullOrEmpty(offer.fromName) ? offer.fromName : "Someone";
        OfferReceived?.Invoke(from);
    }

    // =========================================================
    // ĐỌC
    // =========================================================

    public static void Incoming(Action<List<TradeDto>> onDone, Action<string> onError) => List("incoming", onDone, onError);

    public static void Outgoing(Action<List<TradeDto>> onDone, Action<string> onError) => List("outgoing", onDone, onError);

    private static void List(string which, Action<List<TradeDto>> onDone, Action<string> onError)
    {
        Instance.StartCoroutine(ApiClient.Get<TradeListResponse>("/api/v1/trades/" + which, true,
            response => onDone?.Invoke(response != null && response.items != null ? response.items : new List<TradeDto>()),
            error => onError?.Invoke(MarketSync.Describe(error))));
    }

    // =========================================================
    // THAO TÁC
    // =========================================================

    /// <summary>Gửi đề nghị: offerCount lá thẻ của mình đổi lấy requestCount lá thẻ của chủ đảo.</summary>
    public static void Offer(string toPlayerId, CardData offerCard, int offerCount, CardData requestCard, int requestCount,
        Action<bool, string> done)
    {
        if (!CardTradeIds.IsTradable(offerCard) || !CardTradeIds.IsTradable(requestCard))
        {
            done?.Invoke(false, "Only creature cards can be traded.");
            return;
        }
        if (offerCount < 1 || offerCount > MaxCardsPerSide || requestCount < 1 || requestCount > MaxCardsPerSide)
        {
            done?.Invoke(false, "Each side gives 1 to " + MaxCardsPerSide + " cards.");
            return;
        }
        if (!HandCards.TakeWithoutSaving(offerCard.cardID, offerCount))
        {
            done?.Invoke(false, "Not enough " + offerCard.cardName + " cards in your hand.");
            return;
        }

        PendingTradeOp op = NewOp(OfferKind);
        op.toPlayerId = toPlayerId;
        op.offerCardId = CardTradeIds.ToMarketId(offerCard);
        op.offerCount = offerCount;
        op.requestCardId = CardTradeIds.ToMarketId(requestCard);
        op.requestCount = requestCount;

        if (!EconomySaveSystem.Instance.AddPendingTradeOp(op))
        {
            HandCards.AddWithoutSaving(offerCard, offerCount);
            done?.Invoke(false, "Could not save. Nothing was sent.");
            return;
        }

        Send(op, done);
    }

    /// <summary>Đồng ý: thẻ được yêu cầu rời tay mình trước, server giao thẻ hai bên qua hộp thư.</summary>
    public static void Accept(TradeDto trade, Action<bool, string> done)
    {
        CardData give = CardTradeIds.Find(trade.requestCardId);
        if (give == null)
        {
            done?.Invoke(false, "Unknown card.");
            return;
        }
        if (!HandCards.TakeWithoutSaving(give.cardID, trade.requestCount))
        {
            done?.Invoke(false, "You need " + trade.requestCount + " " + give.cardName + " cards in your hand.");
            return;
        }

        PendingTradeOp op = NewOp(AcceptKind);
        op.tradeId = trade.id;
        op.offerCardId = trade.offerCardId;
        op.offerCount = trade.offerCount;
        op.requestCardId = trade.requestCardId;
        op.requestCount = trade.requestCount;

        if (!EconomySaveSystem.Instance.AddPendingTradeOp(op))
        {
            HandCards.AddWithoutSaving(give, trade.requestCount);
            done?.Invoke(false, "Could not save. Nothing was traded.");
            return;
        }

        Send(op, done);
    }

    public static void Decline(TradeDto trade, Action<bool, string> done) => Resolve(DeclineKind, trade, done);

    public static void Cancel(TradeDto trade, Action<bool, string> done) => Resolve(CancelKind, trade, done);

    private static void Resolve(string kind, TradeDto trade, Action<bool, string> done)
    {
        PendingTradeOp op = NewOp(kind);
        op.tradeId = trade.id;

        if (!EconomySaveSystem.Instance.AddPendingTradeOp(op))
        {
            done?.Invoke(false, "Could not save. Try again.");
            return;
        }

        Send(op, done);
    }

    private static void Send(PendingTradeOp op, Action<bool, string> done)
    {
        if (!inFlight.Add(op.opKey)) return;

        // Kết quả về sau khi đã đổi tài khoản thì bỏ; thao tác còn trong bản lưu cũ, gửi lại khi tài khoản đó vào lại
        string owner = AuthSession.PlayerId;
        bool AccountChanged()
        {
            if (AuthSession.PlayerId == owner) return false;
            inFlight.Remove(op.opKey);
            return true;
        }
        void Err(ApiError error) { if (!AccountChanged()) Fail(op, error, done); }

        TradeSync sync = Instance;
        switch (op.kind)
        {
            case OfferKind:
                sync.StartCoroutine(ApiClient.Post<CreateTradeRequest, TradeDto>(
                    "/api/v1/trades",
                    new CreateTradeRequest
                    {
                        toPlayerId = op.toPlayerId,
                        offerCardId = op.offerCardId,
                        offerCount = op.offerCount,
                        requestCardId = op.requestCardId,
                        requestCount = op.requestCount
                    },
                    true,
                    trade =>
                    {
                        if (AccountChanged()) return;
                        Succeed(op, "Offer sent to " + (trade != null ? trade.toName : "the owner") +
                            ". Your cards come back if it is declined or expires.", done);
                    },
                    Err,
                    op.opKey));
                break;

            case AcceptKind:
            case DeclineKind:
            case CancelKind:
            {
                string action = op.kind == AcceptKind ? "accept" : op.kind == DeclineKind ? "decline" : "cancel";
                string message = op.kind == AcceptKind
                    ? "Trade done. " + MarketSync.NameOf(op.offerCardId) + " x" + op.offerCount + " arrives in your hand."
                    : op.kind == DeclineKind ? "Offer declined." : "Offer cancelled. Your cards return to your hand.";
                sync.StartCoroutine(ApiClient.Post<EmptyRequest, TradeResolveResponse>(
                    "/api/v1/trades/" + op.tradeId + "/" + action,
                    new EmptyRequest(),
                    true,
                    response => { if (!AccountChanged()) Succeed(op, message, done); },
                    Err,
                    op.opKey));
                break;
            }

            default:
                inFlight.Remove(op.opKey);
                Debug.LogWarning($"[LumiWorld Đổi thẻ] Loại thao tác chưa hỗ trợ: {op.kind}");
                break;
        }
    }

    private static void Succeed(PendingTradeOp op, string message, Action<bool, string> done)
    {
        inFlight.Remove(op.opKey);
        EconomySaveSystem.Instance.RemovePendingTradeOp(op.opKey);

        // Thẻ nhận được hoặc thẻ trả lại: lấy về ngay
        InboxSync.RequestSync();
        done?.Invoke(true, message);
    }

    private static void Fail(PendingTradeOp op, ApiError error, Action<bool, string> done)
    {
        inFlight.Remove(op.opKey);
        string message = MarketSync.Describe(error);

        if (!IsDefinitiveRefusal(error))
        {
            done?.Invoke(false, message + " We'll send it again automatically.");
            return;
        }

        // Thẻ cần trả mà tay bài chưa sẵn sàng: giữ thao tác, lần gửi lại server từ chối lần nữa và trả lúc đó
        bool refundsCards = op.kind == OfferKind || op.kind == AcceptKind;
        if (refundsCards && !HandCards.Available)
        {
            done?.Invoke(false, message);
            return;
        }

        if (refundsCards)
        {
            string marketId = op.kind == OfferKind ? op.offerCardId : op.requestCardId;
            int count = op.kind == OfferKind ? op.offerCount : op.requestCount;
            CardData card = CardTradeIds.Find(marketId);
            if (card != null) HandCards.AddWithoutSaving(card, count);
            else Debug.LogWarning($"[LumiWorld Đổi thẻ] Không tra được thẻ {marketId} khi trả lại {count} lá.");
        }

        // Ghi file cùng thẻ vừa trả
        EconomySaveSystem.Instance.RemovePendingTradeOp(op.opKey);
        done?.Invoke(false, message);
    }

    private static bool IsDefinitiveRefusal(ApiError error)
    {
        if (error == null || error.IsNetworkError) return false;
        if (error.Code == "OPERATION_IN_PROGRESS") return false;

        return error.StatusCode >= 400 && error.StatusCode < 500
            && error.StatusCode != 401 && error.StatusCode != 408 && error.StatusCode != 429;
    }

    private static PendingTradeOp NewOp(string kind) => new PendingTradeOp
    {
        opKey = Guid.NewGuid().ToString("N"),
        kind = kind,
        createdAtUtc = DateTime.UtcNow.ToString("o")
    };
}

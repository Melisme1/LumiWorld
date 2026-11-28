using System.Security.Claims;
using System.Text.Json;
using Lumi.Api.Features.Config;
using Lumi.Api.Features.Inbox;
using Lumi.Api.Infrastructure;
using Npgsql;

namespace Lumi.Api.Features.Trades;

public sealed record CreateTradeRequest(Guid? ToPlayerId, string? OfferCardId, int? OfferCount, string? RequestCardId, int? RequestCount);

public sealed record TradeView(
    Guid Id,
    Guid FromPlayerId,
    string FromName,
    Guid ToPlayerId,
    string ToName,
    string OfferCardId,
    int OfferCount,
    string RequestCardId,
    int RequestCount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Đổi thẻ thú giữa hai người chơi (gửi từ màn thăm đảo). Một loại thẻ đổi một loại thẻ, không Coins, không phí.
/// Thẻ người gửi đã rời tay lúc gửi và server giữ tới khi đề nghị kết thúc:
/// đồng ý -> mỗi bên nhận thẻ của bên kia qua hộp thư (TradeReceived);
/// từ chối, hủy, hết hạn -> thẻ về người gửi (TradeReturned).
/// Người nhận trừ thẻ của mình trên máy trước khi gọi accept; server không xem tay bài của ai.
/// </summary>
public static class TradeEndpoints
{
    private const string CardPrefix = "card.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Columns =
        "t.id, t.from_player, pf.display_name, t.to_player, pt.display_name, t.offer_card_id, t.offer_count, " +
        "t.request_card_id, t.request_count, t.status, t.created_at, t.expires_at";

    private const string FromJoin =
        "from card_trades t join players pf on pf.id = t.from_player join players pt on pt.id = t.to_player";

    public static void MapTradeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/trades").RequireAuthorization();
        group.MapGet("/incoming", Incoming);
        group.MapGet("/outgoing", Outgoing);
        group.MapPost("/", Create);
        group.MapPost("/{id:guid}/accept", Accept);
        group.MapPost("/{id:guid}/decline", Decline);
        group.MapPost("/{id:guid}/cancel", Cancel);
    }

    private static Task<IResult> Incoming(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct) =>
        ListAsync(db, "t.to_player = @p", user.PlayerId(), ct);

    private static Task<IResult> Outgoing(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct) =>
        ListAsync(db, "t.from_player = @p", user.PlayerId(), ct);

    private static async Task<IResult> ListAsync(NpgsqlDataSource db, string filter, Guid playerId, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select " + Columns + " " + FromJoin + " where " + filter +
            " and t.status = 'Pending' and t.expires_at > now() order by t.created_at desc", conn);
        cmd.Parameters.AddWithValue("p", playerId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var items = new List<TradeView>();
        while (await reader.ReadAsync(ct))
            items.Add(Read(reader));
        return Results.Ok(new { items });
    }

    private static async Task<IResult> Create(
        ClaimsPrincipal user, HttpRequest http, CreateTradeRequest? body,
        NpgsqlDataSource db, GameConfigService config, CancellationToken ct)
    {
        string? opKey = OpKeys.ReadKey(http);
        if (opKey == null)
            return MissingKey();

        Guid playerId = user.PlayerId();
        Guid toPlayer = body?.ToPlayerId ?? Guid.Empty;
        string offerCard = (body?.OfferCardId ?? string.Empty).Trim();
        string requestCard = (body?.RequestCardId ?? string.Empty).Trim();
        int offerCount = body?.OfferCount ?? 0;
        int requestCount = body?.RequestCount ?? 0;

        if (toPlayer == Guid.Empty)
            return ApiProblem.Create(400, ErrorCodes.BadRequest, "toPlayerId is required.");
        if (toPlayer == playerId)
            return ApiProblem.Create(400, ErrorCodes.TradeOwnWorld, "You cannot trade with yourself.");

        GameConfig settings = await config.GetAsync(ct);
        int maxCards = settings.TradeMaxCardsPerSide;
        if (offerCount < 1 || offerCount > maxCards || requestCount < 1 || requestCount > maxCards)
            return ApiProblem.Create(400, ErrorCodes.InvalidAmount, $"Each side must give between 1 and {maxCards} cards.");

        try
        {
            await using var conn = await db.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            var stored = await OpKeys.FindAsync(conn, tx, playerId, opKey, ct);
            if (stored != null)
                return Replay(stored);

            if (!await IsCreatureCardAsync(conn, tx, offerCard, ct) || !await IsCreatureCardAsync(conn, tx, requestCard, ct))
                return ApiProblem.Create(400, ErrorCodes.UnknownResource, "Only creature cards can be traded.");

            await using (var target = new NpgsqlCommand("select 1 from players where id = @t", conn, tx))
            {
                target.Parameters.AddWithValue("t", toPlayer);
                if (await target.ExecuteScalarAsync(ct) == null)
                    return ApiProblem.Create(404, ErrorCodes.NotFound, "This player does not exist.");
            }

            // Khóa dòng người gửi: hai đề nghị gửi cùng lúc đếm lần lượt, không cùng vượt giới hạn
            await using (var lockSender = new NpgsqlCommand("select 1 from players where id = @p for update", conn, tx))
            {
                lockSender.Parameters.AddWithValue("p", playerId);
                await lockSender.ExecuteNonQueryAsync(ct);
            }

            await using (var count = new NpgsqlCommand(
                "select count(*), count(*) filter (where to_player = @t) from card_trades " +
                "where from_player = @p and status = 'Pending' and expires_at > now()", conn, tx))
            {
                count.Parameters.AddWithValue("p", playerId);
                count.Parameters.AddWithValue("t", toPlayer);
                await using var reader = await count.ExecuteReaderAsync(ct);
                await reader.ReadAsync(ct);
                if (reader.GetInt64(1) > 0)
                    return ApiProblem.Create(409, ErrorCodes.TradeLimitReached, "You already have a pending offer to this player.");
                if (reader.GetInt64(0) >= settings.TradeMaxPendingOutgoing)
                    return ApiProblem.Create(409, ErrorCodes.TradeLimitReached,
                        $"You can have at most {settings.TradeMaxPendingOutgoing} pending offers.");
            }

            Guid id = Guid.NewGuid();
            await using (var insert = new NpgsqlCommand(
                "insert into card_trades (id, from_player, to_player, offer_card_id, offer_count, request_card_id, request_count, expires_at) " +
                "values (@id, @f, @t, @oc, @on, @rc, @rn, now() + make_interval(hours => @h))", conn, tx))
            {
                insert.Parameters.AddWithValue("id", id);
                insert.Parameters.AddWithValue("f", playerId);
                insert.Parameters.AddWithValue("t", toPlayer);
                insert.Parameters.AddWithValue("oc", offerCard);
                insert.Parameters.AddWithValue("on", offerCount);
                insert.Parameters.AddWithValue("rc", requestCard);
                insert.Parameters.AddWithValue("rn", requestCount);
                insert.Parameters.AddWithValue("h", settings.TradeOfferHours);
                await insert.ExecuteNonQueryAsync(ct);
            }

            TradeView trade = (await ReadOneAsync(conn, tx, id, ct))!;
            string json = JsonSerializer.Serialize(trade, Json);

            // Báo người nhận; mục này không mang thẻ
            await InboxEndpoints.InsertAsync(conn, tx, toPlayer, Guid.NewGuid(), "TradeOffered",
                new { tradeId = id, fromName = trade.FromName, offerCardId = offerCard, offerCount, requestCardId = requestCard, requestCount }, ct);

            await OpKeys.SaveAsync(conn, tx, playerId, opKey, 201, json, ct);
            await ActionLog.WriteAsync(conn, playerId, "trade.offer",
                new { tradeId = id, toPlayer, offerCard, offerCount, requestCard, requestCount }, tx, ct);
            await tx.CommitAsync(ct);

            return Results.Content(json, "application/json", statusCode: 201);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return await ReplayRaceAsync(db, playerId, opKey, ct);
        }
    }

    /// <summary>Người nhận đồng ý: mỗi bên nhận thẻ của bên kia qua hộp thư.</summary>
    private static Task<IResult> Accept(Guid id, ClaimsPrincipal user, HttpRequest http, NpgsqlDataSource db, CancellationToken ct) =>
        ResolveAsync(id, user, http, db, "Accepted", ct);

    /// <summary>Người nhận từ chối: thẻ về người gửi.</summary>
    private static Task<IResult> Decline(Guid id, ClaimsPrincipal user, HttpRequest http, NpgsqlDataSource db, CancellationToken ct) =>
        ResolveAsync(id, user, http, db, "Declined", ct);

    /// <summary>Người gửi hủy: thẻ về người gửi.</summary>
    private static Task<IResult> Cancel(Guid id, ClaimsPrincipal user, HttpRequest http, NpgsqlDataSource db, CancellationToken ct) =>
        ResolveAsync(id, user, http, db, "Cancelled", ct);

    private static async Task<IResult> ResolveAsync(
        Guid id, ClaimsPrincipal user, HttpRequest http, NpgsqlDataSource db, string outcome, CancellationToken ct)
    {
        string? opKey = OpKeys.ReadKey(http);
        if (opKey == null)
            return MissingKey();

        Guid playerId = user.PlayerId();

        try
        {
            await using var conn = await db.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            // Khóa đề nghị trước rồi mới đọc mã op: accept và cancel cùng lúc thì chỉ một bên thắng
            var trade = await LockAsync(conn, tx, id, ct);
            if (trade == null)
                return ApiProblem.Create(404, ErrorCodes.TradeNotFound, "This offer no longer exists.");

            var stored = await OpKeys.FindAsync(conn, tx, playerId, opKey, ct);
            if (stored != null)
                return Replay(stored);

            Guid allowed = outcome == "Cancelled" ? trade.FromPlayer : trade.ToPlayer;
            if (playerId != allowed)
                return ApiProblem.Create(403, ErrorCodes.NotYourTrade,
                    outcome == "Cancelled" ? "Only the sender can cancel this offer." : "Only the receiver can answer this offer.");

            if (trade.Status != "Pending" || trade.Expired)
                return ApiProblem.Create(409, ErrorCodes.TradeUnavailable, "This offer was already answered, cancelled or expired.");

            await using (var close = new NpgsqlCommand(
                "update card_trades set status = @s, resolved_at = now() where id = @id", conn, tx))
            {
                close.Parameters.AddWithValue("s", outcome);
                close.Parameters.AddWithValue("id", id);
                await close.ExecuteNonQueryAsync(ct);
            }

            if (outcome == "Accepted")
            {
                await InboxEndpoints.InsertAsync(conn, tx, trade.FromPlayer, Guid.NewGuid(), "TradeReceived",
                    new { tradeId = id, resourceId = trade.RequestCard, amount = trade.RequestCount }, ct);
                await InboxEndpoints.InsertAsync(conn, tx, trade.ToPlayer, Guid.NewGuid(), "TradeReceived",
                    new { tradeId = id, resourceId = trade.OfferCard, amount = trade.OfferCount }, ct);
            }
            else
            {
                await InboxEndpoints.InsertAsync(conn, tx, trade.FromPlayer, Guid.NewGuid(), "TradeReturned",
                    new { tradeId = id, resourceId = trade.OfferCard, amount = trade.OfferCount, reason = outcome.ToLowerInvariant() }, ct);
            }

            string json = JsonSerializer.Serialize(new
            {
                tradeId = id,
                status = outcome,
                offerCardId = trade.OfferCard,
                offerCount = trade.OfferCount,
                requestCardId = trade.RequestCard,
                requestCount = trade.RequestCount
            });

            await OpKeys.SaveAsync(conn, tx, playerId, opKey, 200, json, ct);
            await ActionLog.WriteAsync(conn, playerId, "trade." + outcome.ToLowerInvariant(),
                new { tradeId = id, trade.FromPlayer, trade.ToPlayer, trade.OfferCard, trade.OfferCount, trade.RequestCard, trade.RequestCount }, tx, ct);
            await tx.CommitAsync(ct);

            return Results.Content(json, "application/json", statusCode: 200);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return await ReplayRaceAsync(db, playerId, opKey, ct);
        }
    }

    /// <summary>Thẻ thú hợp lệ là thẻ có giá gốc trên chợ (market.baseValue.card.&lt;cardID&gt;, migration 006).</summary>
    private static async Task<bool> IsCreatureCardAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string cardId, CancellationToken ct)
    {
        if (!cardId.StartsWith(CardPrefix, StringComparison.Ordinal) || cardId.Length > 100)
            return false;
        await using var cmd = new NpgsqlCommand("select 1 from game_config where key = @k", conn, tx);
        cmd.Parameters.AddWithValue("k", "market.baseValue." + cardId);
        return await cmd.ExecuteScalarAsync(ct) != null;
    }

    private sealed record LockedTrade(Guid FromPlayer, Guid ToPlayer, string OfferCard, int OfferCount,
        string RequestCard, int RequestCount, string Status, bool Expired);

    private static async Task<LockedTrade?> LockAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "select from_player, to_player, offer_card_id, offer_count, request_card_id, request_count, status, expires_at <= now() " +
            "from card_trades where id = @id for update", conn, tx);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        return new LockedTrade(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetInt32(3),
            reader.GetString(4), reader.GetInt32(5), reader.GetString(6), reader.GetBoolean(7));
    }

    private static async Task<TradeView?> ReadOneAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("select " + Columns + " " + FromJoin + " where t.id = @id", conn, tx);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    private static TradeView Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetGuid(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetInt32(6),
        reader.GetString(7),
        reader.GetInt32(8),
        reader.GetString(9),
        reader.GetFieldValue<DateTimeOffset>(10),
        reader.GetFieldValue<DateTimeOffset>(11));

    private static IResult Replay(OpKeys.StoredResponse stored) =>
        Results.Content(stored.ResponseJson ?? "{}", "application/json", statusCode: stored.StatusCode);

    private static async Task<IResult> ReplayRaceAsync(NpgsqlDataSource db, Guid playerId, string opKey, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var stored = await OpKeys.FindAsync(conn, tx, playerId, opKey, ct);
        return stored != null
            ? Replay(stored)
            : ApiProblem.Create(409, ErrorCodes.OperationInProgress, "The same request is still being processed. Try again.");
    }

    private static IResult MissingKey() =>
        ApiProblem.Create(400, ErrorCodes.IdempotencyKeyMissing, "Send an Idempotency-Key header with this request.");
}

/// <summary>
/// Trả thẻ cho người gửi các đề nghị đã quá hạn. Chạy định kỳ trong TradeExpiryService; tách riêng để test gọi trực tiếp.
/// </summary>
public static class TradeExpiry
{
    private const string Sql = @"
with expired as (
    update card_trades set status = 'Expired', resolved_at = now()
    where status = 'Pending' and expires_at <= now()
    returning id, from_player, offer_card_id, offer_count
)
insert into inbox_items (id, player_id, kind, payload)
select gen_random_uuid(), from_player, 'TradeReturned',
       jsonb_build_object('tradeId', id, 'resourceId', offer_card_id, 'amount', offer_count, 'reason', 'expired')
from expired";

    public static async Task<int> RunOnceAsync(NpgsqlDataSource db, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(Sql, conn);
        return await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class TradeExpiryService(
    NpgsqlDataSource db, IConfiguration configuration, ILogger<TradeExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int seconds = Math.Max(1, configuration.GetValue("Market:ExpiryJobSeconds", 60));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                int returned = await TradeExpiry.RunOnceAsync(db, stoppingToken);
                if (returned > 0)
                    logger.LogInformation("Trả thẻ cho {Count} đề nghị đổi thẻ đã hết hạn.", returned);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Job hết hạn đổi thẻ lỗi, sẽ thử lại sau {Seconds} giây.", seconds);
            }
        }
    }
}

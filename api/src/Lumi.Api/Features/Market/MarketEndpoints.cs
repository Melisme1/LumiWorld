using System.Security.Claims;
using System.Text.Json;
using Lumi.Api.Features.Config;
using Lumi.Api.Features.Inbox;
using Lumi.Api.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Lumi.Api.Features.Market;

public sealed record CreateListingRequest(string? ResourceId, int? Amount, int? PriceCoins);

/// <summary>Một lô trên chợ. PricePerUnit để client sắp xếp và hiện giá mỗi đơn vị.</summary>
public sealed record ListingView(
    Guid Id,
    Guid SellerId,
    string SellerName,
    string ResourceId,
    int Amount,
    int PriceCoins,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    public double PricePerUnit => Math.Round((double)PriceCoins / Amount, 2);
}

/// <summary>
/// Chợ listing (giai đoạn 1): đăng bán, mua cả lô, hủy. Hết hạn do ListingExpiry chạy định kỳ.
/// Mọi POST làm thay đổi dữ liệu cần header Idempotency-Key. Client gửi lại cùng khóa thì nhận lại đúng kết quả cũ.
/// Lô được khóa hàng (FOR UPDATE) trong transaction, nên hai người cùng mua một lô thì chỉ một người thành công.
/// </summary>
public static class MarketEndpoints
{
    private const string Columns =
        "l.id, l.seller_id, p.display_name, l.resource_id, l.amount, l.price_coins, l.status, l.created_at, l.expires_at";

    private const string FromJoin = "from listings l join players p on p.id = l.seller_id";

    // camelCase như mọi response khác của API (JsonUtility phía client cần đúng tên trường)
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapMarketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/market").RequireAuthorization();

        group.MapGet("/listings", BrowseListings);
        group.MapGet("/listings/mine", MyListings);
        group.MapPost("/listings", CreateListing);
        group.MapPost("/listings/{id:guid}/buy", BuyListing);
        group.MapPost("/listings/{id:guid}/cancel", CancelListing);
    }

    /// <summary>
    /// Danh sách lô còn hạn. Lọc theo resourceId, sellerId hoặc kind (resource = tài nguyên, card = thẻ thú);
    /// sort = pricePerUnit (mặc định) hoặc newest.
    /// </summary>
    private static async Task<IResult> BrowseListings(
        string? resourceId, Guid? sellerId, string? kind, string? sort, int? page, int? pageSize,
        NpgsqlDataSource db, CancellationToken ct)
    {
        int size = Math.Clamp(pageSize ?? 20, 1, 50);
        int pageNo = Math.Max(1, page ?? 1);

        var where = new List<string> { "l.status = 'Active'", "l.expires_at > now()" };
        var parameters = new List<NpgsqlParameter>();

        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            where.Add("l.resource_id = @resource");
            parameters.Add(new NpgsqlParameter("resource", resourceId.Trim()));
        }
        if (kind == "card")
            where.Add("l.resource_id like 'card.%'");
        else if (kind == "resource")
            where.Add("l.resource_id not like 'card.%'");
        if (sellerId is Guid seller)
        {
            where.Add("l.seller_id = @seller");
            parameters.Add(new NpgsqlParameter("seller", seller));
        }

        string order = sort == "newest"
            ? "l.created_at desc, l.id"
            : "(l.price_coins::float8 / l.amount) asc, l.created_at, l.id";

        string sql = "select " + Columns + " " + FromJoin +
                     " where " + string.Join(" and ", where) +
                     " order by " + order + " limit @take offset @skip";
        parameters.Add(new NpgsqlParameter("take", size + 1));
        parameters.Add(new NpgsqlParameter("skip", (pageNo - 1) * size));

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddRange(parameters.ToArray());
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var items = new List<ListingView>();
        while (await reader.ReadAsync(ct))
            items.Add(ReadListing(reader));

        bool hasMore = items.Count > size;
        if (hasMore) items.RemoveAt(items.Count - 1);

        return Results.Ok(new { items, page = pageNo, pageSize = size, hasMore });
    }

    private static async Task<IResult> MyListings(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select " + Columns + " " + FromJoin +
            " where l.seller_id = @p and l.status = 'Active' and l.expires_at > now() order by l.created_at desc",
            conn);
        cmd.Parameters.AddWithValue("p", user.PlayerId());
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var items = new List<ListingView>();
        while (await reader.ReadAsync(ct))
            items.Add(ReadListing(reader));

        return Results.Ok(new { items });
    }

    private static async Task<IResult> CreateListing(
        ClaimsPrincipal user, HttpRequest http, CreateListingRequest? body,
        NpgsqlDataSource db, GameConfigService config, CancellationToken ct)
    {
        string? opKey = OpKeys.ReadKey(http);
        if (opKey == null)
            return MissingKey();

        Guid playerId = user.PlayerId();
        string resourceId = (body?.ResourceId ?? string.Empty).Trim();
        if (resourceId.Length == 0 || resourceId.Length > 100)
            return ApiProblem.Create(400, ErrorCodes.BadRequest, "resourceId is required (at most 100 characters).");

        try
        {
            await using var conn = await db.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            var stored = await OpKeys.FindAsync(conn, tx, playerId, opKey, ct);
            if (stored != null)
                return Replay(stored);

            MarketSettings settings = await MarketSettings.LoadAsync(conn, tx, config, ct);

            if (!settings.BaseValues.TryGetValue(resourceId, out int baseValue))
                return ApiProblem.Create(400, ErrorCodes.UnknownResource, "This resource cannot be sold on the market.");

            bool isCard = MarketRules.IsCard(resourceId);
            int maxAmount = isCard ? settings.MaxCardsPerListing : MarketRules.MaxAmount;
            int amount = body?.Amount ?? 0;
            if (amount < 1 || amount > maxAmount)
                return ApiProblem.Create(400, ErrorCodes.InvalidAmount, $"Amount must be between 1 and {maxAmount}.");

            int price = body?.PriceCoins ?? 0;
            var (minPrice, maxPrice) = MarketRules.PriceRange(amount, baseValue, settings);
            if (price < minPrice || price > maxPrice)
            {
                return ApiProblem.Create(400, ErrorCodes.PriceOutOfRange,
                    $"Price must be between {minPrice} and {maxPrice} Coins for this lot.",
                    new { minPriceCoins = minPrice, maxPriceCoins = maxPrice });
            }

            // Khóa dòng người bán: hai lần đăng cùng lúc đếm lần lượt, không cùng vượt giới hạn
            await using (var lockSeller = new NpgsqlCommand("select 1 from players where id = @p for update", conn, tx))
            {
                lockSeller.Parameters.AddWithValue("p", playerId);
                await lockSeller.ExecuteNonQueryAsync(ct);
            }

            await using (var count = new NpgsqlCommand(
                "select count(*), count(*) filter (where resource_id like 'card.%') " +
                "from listings where seller_id = @p and status = 'Active' and expires_at > now()", conn, tx))
            {
                count.Parameters.AddWithValue("p", playerId);
                await using var reader = await count.ExecuteReaderAsync(ct);
                await reader.ReadAsync(ct);
                long active = reader.GetInt64(0);
                long activeCards = reader.GetInt64(1);
                if (active >= settings.MaxActiveListings)
                {
                    return ApiProblem.Create(409, ErrorCodes.ListingLimitReached,
                        $"You can sell at most {settings.MaxActiveListings} lots at a time.");
                }
                if (isCard && activeCards >= settings.MaxActiveCardListings)
                {
                    return ApiProblem.Create(409, ErrorCodes.ListingLimitReached,
                        $"You can sell at most {settings.MaxActiveCardListings} card lots at a time.");
                }
            }

            Guid id = Guid.NewGuid();
            await using (var insert = new NpgsqlCommand(
                "insert into listings (id, seller_id, resource_id, amount, price_coins, expires_at) " +
                "values (@id, @s, @r, @a, @c, now() + make_interval(hours => @h))", conn, tx))
            {
                insert.Parameters.AddWithValue("id", id);
                insert.Parameters.AddWithValue("s", playerId);
                insert.Parameters.AddWithValue("r", resourceId);
                insert.Parameters.AddWithValue("a", amount);
                insert.Parameters.AddWithValue("c", price);
                insert.Parameters.AddWithValue("h", settings.ListingHours);
                await insert.ExecuteNonQueryAsync(ct);
            }

            ListingView listing = (await ReadOneAsync(conn, tx, id, ct))!;
            string json = JsonSerializer.Serialize(listing, Json);

            await OpKeys.SaveAsync(conn, tx, playerId, opKey, 201, json, ct);
            await ActionLog.WriteAsync(conn, playerId, "market.list",
                new { listingId = id, resourceId, amount, priceCoins = price }, tx, ct);
            await tx.CommitAsync(ct);

            return Results.Content(json, "application/json", statusCode: 201);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return await ReplayRaceAsync(db, playerId, opKey, ct);
        }
    }

    private static async Task<IResult> BuyListing(
        Guid id, ClaimsPrincipal user, HttpRequest http, NpgsqlDataSource db, GameConfigService config, CancellationToken ct)
    {
        string? opKey = OpKeys.ReadKey(http);
        if (opKey == null)
            return MissingKey();

        Guid playerId = user.PlayerId();

        try
        {
            await using var conn = await db.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            // Khóa lô trước rồi mới đọc mã op: request trùng phải chờ ở đây và thấy kết quả của request đầu
            var lot = await LockListingAsync(conn, tx, id, ct);
            if (lot == null)
                return ApiProblem.Create(404, ErrorCodes.ListingNotFound, "This lot no longer exists.");

            var stored = await OpKeys.FindAsync(conn, tx, playerId, opKey, ct);
            if (stored != null)
                return Replay(stored);

            if (lot.SellerId == playerId)
                return ApiProblem.Create(400, ErrorCodes.OwnListing, "You cannot buy your own lot.");

            if (lot.Status != "Active" || lot.Expired)
                return ApiProblem.Create(409, ErrorCodes.ListingUnavailable, "This lot was already sold, cancelled or expired.");

            MarketSettings settings = await MarketSettings.LoadAsync(conn, tx, config, ct);
            int fee = MarketRules.Fee(lot.PriceCoins, settings.FeePercent);

            await using (var close = new NpgsqlCommand(
                "update listings set status = 'Sold', buyer_id = @b, sold_at = now(), closed_at = now() where id = @id", conn, tx))
            {
                close.Parameters.AddWithValue("b", playerId);
                close.Parameters.AddWithValue("id", id);
                await close.ExecuteNonQueryAsync(ct);
            }

            await InboxEndpoints.InsertAsync(conn, tx, playerId, Guid.NewGuid(), "ItemsBought",
                new { listingId = id, resourceId = lot.ResourceId, amount = lot.Amount }, ct);
            await InboxEndpoints.InsertAsync(conn, tx, lot.SellerId, Guid.NewGuid(), "CoinsFromSale",
                new { listingId = id, resourceId = lot.ResourceId, amount = lot.Amount, coins = lot.PriceCoins - fee, fee }, ct);

            string json = JsonSerializer.Serialize(new
            {
                listingId = id,
                resourceId = lot.ResourceId,
                amount = lot.Amount,
                paidCoins = lot.PriceCoins
            });

            await OpKeys.SaveAsync(conn, tx, playerId, opKey, 200, json, ct);
            await ActionLog.WriteAsync(conn, playerId, "market.buy",
                new { listingId = id, sellerId = lot.SellerId, resourceId = lot.ResourceId, amount = lot.Amount, priceCoins = lot.PriceCoins, fee }, tx, ct);
            await tx.CommitAsync(ct);

            return Results.Content(json, "application/json", statusCode: 200);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return await ReplayRaceAsync(db, playerId, opKey, ct);
        }
    }

    private static async Task<IResult> CancelListing(
        Guid id, ClaimsPrincipal user, HttpRequest http, NpgsqlDataSource db, CancellationToken ct)
    {
        string? opKey = OpKeys.ReadKey(http);
        if (opKey == null)
            return MissingKey();

        Guid playerId = user.PlayerId();

        try
        {
            await using var conn = await db.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            var lot = await LockListingAsync(conn, tx, id, ct);
            if (lot == null)
                return ApiProblem.Create(404, ErrorCodes.ListingNotFound, "This lot no longer exists.");

            var stored = await OpKeys.FindAsync(conn, tx, playerId, opKey, ct);
            if (stored != null)
                return Replay(stored);

            if (lot.SellerId != playerId)
                return ApiProblem.Create(403, ErrorCodes.NotYourListing, "Only the seller can cancel this lot.");

            if (lot.Status != "Active" || lot.Expired)
                return ApiProblem.Create(409, ErrorCodes.ListingUnavailable, "This lot was already sold, cancelled or expired.");

            await using (var close = new NpgsqlCommand(
                "update listings set status = 'Cancelled', closed_at = now() where id = @id", conn, tx))
            {
                close.Parameters.AddWithValue("id", id);
                await close.ExecuteNonQueryAsync(ct);
            }

            await InboxEndpoints.InsertAsync(conn, tx, playerId, Guid.NewGuid(), "ItemsReturned",
                new { listingId = id, resourceId = lot.ResourceId, amount = lot.Amount, reason = "cancelled" }, ct);

            string json = JsonSerializer.Serialize(new { listingId = id, status = "Cancelled" });

            await OpKeys.SaveAsync(conn, tx, playerId, opKey, 200, json, ct);
            await ActionLog.WriteAsync(conn, playerId, "market.cancel",
                new { listingId = id, resourceId = lot.ResourceId, amount = lot.Amount }, tx, ct);
            await tx.CommitAsync(ct);

            return Results.Content(json, "application/json", statusCode: 200);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return await ReplayRaceAsync(db, playerId, opKey, ct);
        }
    }

    private sealed record LockedLot(Guid SellerId, string ResourceId, int Amount, int PriceCoins, string Status, bool Expired);

    /// <summary>Khóa dòng lô tới hết transaction. Trả về null nếu không có lô.</summary>
    private static async Task<LockedLot?> LockListingAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "select seller_id, resource_id, amount, price_coins, status, expires_at <= now() " +
            "from listings where id = @id for update", conn, tx);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new LockedLot(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2),
            reader.GetInt32(3), reader.GetString(4), reader.GetBoolean(5));
    }

    private static async Task<ListingView?> ReadOneAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("select " + Columns + " " + FromJoin + " where l.id = @id", conn, tx);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadListing(reader) : null;
    }

    private static ListingView ReadListing(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetInt32(4),
        reader.GetInt32(5),
        reader.GetString(6),
        reader.GetFieldValue<DateTimeOffset>(7),
        reader.GetFieldValue<DateTimeOffset>(8));

    private static IResult Replay(OpKeys.StoredResponse stored) =>
        Results.Content(stored.ResponseJson ?? "{}", "application/json", statusCode: stored.StatusCode);

    /// <summary>
    /// Hai request cùng khóa chạy song song và cùng chèn op key: request tới sau bị lỗi unique,
    /// nên lấy đúng kết quả của request tới trước.
    /// </summary>
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
/// Trả hàng cho người bán các lô đã hết hạn. Chạy định kỳ trong ListingExpiryService;
/// tách riêng để test gọi trực tiếp.
/// </summary>
public static class ListingExpiry
{
    private const string Sql = @"
with expired as (
    update listings set status = 'Expired', closed_at = now()
    where status = 'Active' and expires_at <= now()
    returning id, seller_id, resource_id, amount
)
insert into inbox_items (id, player_id, kind, payload)
select gen_random_uuid(), seller_id, 'ItemsReturned',
       jsonb_build_object('listingId', id, 'resourceId', resource_id, 'amount', amount, 'reason', 'expired')
from expired";

    /// <summary>Đóng mọi lô hết hạn và xếp hàng trả vào hộp thư. Trả về số mục đã tạo.</summary>
    public static async Task<int> RunOnceAsync(NpgsqlDataSource db, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(Sql, conn);
        return await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class ListingExpiryService(
    NpgsqlDataSource db, IConfiguration configuration, ILogger<ListingExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int seconds = Math.Max(1, configuration.GetValue("Market:ExpiryJobSeconds", 60));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                int returned = await ListingExpiry.RunOnceAsync(db, stoppingToken);
                if (returned > 0)
                    logger.LogInformation("Trả hàng cho {Count} lô đã hết hạn.", returned);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Có thể bảng chưa sẵn sàng lúc khởi động; lượt sau thử lại
                logger.LogWarning(ex, "Job hết hạn chợ lỗi, sẽ thử lại sau {Seconds} giây.", seconds);
            }
        }
    }
}

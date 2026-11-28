using System.Security.Claims;
using System.Text.Json;
using Lumi.Api.Infrastructure;
using Npgsql;

namespace Lumi.Api.Features.Rivers;

/// <summary>
/// Sông trên đảo: mỗi ô sông là một placement cardID "River" trong world (save) hoặc snapshot.
/// Client ghi thêm riverFlowStep (-1 hoặc thiếu ở bản lưu cũ) và riverIsOrigin.
/// Server không dựng lại hình sông, chỉ kiểm tra dữ liệu hợp lệ và trả thống kê.
/// </summary>
public static class RiverEndpoints
{
    public const string RiverCardId = "River";

    public static void MapRiverEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/rivers").RequireAuthorization();
        group.MapGet("/me", GetMine);
    }

    /// <summary>
    /// Kiểm tra các ô sông trong mảng placements. Trả null khi hợp lệ, ngược lại là câu báo lỗi.
    /// Ô sông phải nằm trên một ô đất có trong tiles; riverFlowStep (nếu có) từ -1 tới số ô đất;
    /// riverIsOrigin (nếu có) là true/false. Thiếu placements hoặc không có sông thì hợp lệ.
    /// </summary>
    public static string? Validate(JsonElement world)
    {
        if (!world.TryGetProperty("placements", out var placements) || placements.ValueKind != JsonValueKind.Array)
            return null;

        HashSet<(int, int)>? tileHexes = null;
        if (world.TryGetProperty("tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array)
        {
            tileHexes = new HashSet<(int, int)>();
            foreach (var tile in tiles.EnumerateArray())
                if (TryGetHex(tile, out var hex)) tileHexes.Add(hex);
        }

        foreach (var placement in placements.EnumerateArray())
        {
            if (!IsRiver(placement)) continue;

            if (!TryGetHex(placement, out var hex))
                return "Every river needs integer q and r.";
            if (tileHexes != null && !tileHexes.Contains(hex))
                return $"River at ({hex.Item1}, {hex.Item2}) is not on a land tile.";

            if (placement.TryGetProperty("riverFlowStep", out var step)
                && (!step.TryGetInt32(out int flowStep) || flowStep < -1 || (tileHexes != null && flowStep > tileHexes.Count)))
                return $"River at ({hex.Item1}, {hex.Item2}) has an invalid riverFlowStep.";

            if (placement.TryGetProperty("riverIsOrigin", out var origin)
                && origin.ValueKind != JsonValueKind.True && origin.ValueKind != JsonValueKind.False)
                return $"River at ({hex.Item1}, {hex.Item2}) has an invalid riverIsOrigin.";
        }
        return null;
    }

    /// <summary>Đọc sông trong save của chính người chơi qua view river_tiles (migration 005_river_view).</summary>
    private static async Task<IResult> GetMine(ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select q, r, flow_step, is_origin from river_tiles where player_id = @p order by flow_step nulls last, q, r",
            conn);
        cmd.Parameters.AddWithValue("p", user.PlayerId());

        var tiles = new List<object>();
        int springs = 0, longestFlow = 0;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                int? flowStep = reader.IsDBNull(2) ? null : reader.GetInt32(2);
                bool? isOrigin = reader.IsDBNull(3) ? null : reader.GetBoolean(3);
                if (isOrigin == true) springs++;
                if (flowStep is int step) longestFlow = Math.Max(longestFlow, step + 1);
                tiles.Add(new { q = reader.GetInt32(0), r = reader.GetInt32(1), flowStep, isOrigin });
            }
        }

        return Results.Ok(new { riverTiles = tiles.Count, springs, longestFlow, tiles });
    }

    private static bool IsRiver(JsonElement placement) =>
        placement.ValueKind == JsonValueKind.Object
        && placement.TryGetProperty("cardID", out var id)
        && id.ValueKind == JsonValueKind.String
        && id.GetString() == RiverCardId;

    private static bool TryGetHex(JsonElement element, out (int, int) hex)
    {
        hex = default;
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("q", out var q) || !q.TryGetInt32(out int qi)
            || !element.TryGetProperty("r", out var r) || !r.TryGetInt32(out int ri))
            return false;
        hex = (qi, ri);
        return true;
    }
}

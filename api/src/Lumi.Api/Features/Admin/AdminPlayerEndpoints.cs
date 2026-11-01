using System.Security.Claims;
using Lumi.Api.Infrastructure;
using Npgsql;

namespace Lumi.Api.Features.Admin;

public sealed record SuspendRequest(string? Reason);

public sealed record RoleRequest(string? Role);

/// <summary>
/// Admin quản lý người chơi: tìm, khóa / mở khóa, cấp / gỡ quyền admin.
/// Khóa có hiệu lực ngay: token còn hạn bị từ chối ở request kế tiếp (OnTokenValidated trong Program.cs).
/// Admin không tự khóa hay tự đổi quyền của mình, và không khóa admin khác (gỡ quyền trước),
/// nên lúc nào cũng còn ít nhất một admin dùng được. Mọi thay đổi ghi action_log kèm admin đã làm.
/// </summary>
public static class AdminPlayerEndpoints
{
    private const int MaxPageSize = 50;
    private const int MaxSearchLength = 32;
    private const int MaxReasonLength = 200;

    public static void MapAdminPlayerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/players").RequireAuthorization();
        group.MapGet("/", List);
        group.MapPost("/{id:guid}/suspend", Suspend);
        group.MapPost("/{id:guid}/unsuspend", Unsuspend);
        group.MapPost("/{id:guid}/role", SetRole);
    }

    /// <summary>
    /// Danh sách người chơi, mới nhất trước. search lọc theo username hoặc tên hiển thị (không phân biệt hoa thường);
    /// suspended=true chỉ lấy tài khoản bị khóa.
    /// </summary>
    private static async Task<IResult> List(
        string? search, bool? suspended, int? page, int? pageSize,
        ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        if (await AdminEndpoints.RequireAdminAsync(user, db, ct) is { } denied) return denied;

        int size = Math.Clamp(pageSize ?? 20, 1, MaxPageSize);
        int pageNo = Math.Max(1, page ?? 1);

        var where = new List<string>();
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand { Connection = conn };

        string text = (search ?? string.Empty).Trim();
        if (text.Length > MaxSearchLength)
            return ApiProblem.Create(400, ErrorCodes.SearchInvalid, $"Search text is at most {MaxSearchLength} characters.");
        if (text.Length > 0)
        {
            where.Add("(p.username_normalized like @q or lower(p.display_name) like @q)");
            cmd.Parameters.AddWithValue("q", "%" + EscapeLike(text.ToLowerInvariant()) + "%");
        }
        if (suspended == true) where.Add("p.is_suspended");

        cmd.CommandText =
            "select p.id, p.username, p.display_name, p.role, p.is_suspended, p.created_at, p.last_login_at, " +
            "w.score, s.updated_at " +
            "from players p left join world_snapshots w on w.player_id = p.id left join saves s on s.player_id = p.id" +
            (where.Count > 0 ? " where " + string.Join(" and ", where) : string.Empty) +
            " order by p.created_at desc, p.id limit @take offset @skip";
        cmd.Parameters.AddWithValue("take", size + 1);
        cmd.Parameters.AddWithValue("skip", (pageNo - 1) * size);

        var items = new List<object>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                items.Add(new
                {
                    playerId = reader.GetGuid(0).ToString(),
                    username = reader.GetString(1),
                    displayName = reader.GetString(2),
                    role = reader.GetString(3),
                    isSuspended = reader.GetBoolean(4),
                    createdAt = reader.GetFieldValue<DateTimeOffset>(5),
                    lastLoginAt = reader.IsDBNull(6) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(6),
                    score = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                    savedAt = reader.IsDBNull(8) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(8)
                });
            }
        }

        bool hasMore = items.Count > size;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return Results.Ok(new { items, page = pageNo, pageSize = size, hasMore });
    }

    private static async Task<IResult> Suspend(
        Guid id, SuspendRequest? body, ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        if (await AdminEndpoints.RequireAdminAsync(user, db, ct) is { } denied) return denied;

        string reason = (body?.Reason ?? string.Empty).Trim();
        if (reason.Length > MaxReasonLength)
            return ApiProblem.Create(400, ErrorCodes.BadRequest, $"Reason is at most {MaxReasonLength} characters.");

        Guid adminId = user.PlayerId();
        if (id == adminId)
            return ApiProblem.Create(400, ErrorCodes.AdminSelf, "You cannot suspend your own account.");

        return await ChangeAsync(db, id, adminId, ct, async (conn, tx, target) =>
        {
            if (target.Role == "admin")
                return ApiProblem.Create(409, ErrorCodes.AdminTargetIsAdmin, "Remove admin rights before suspending this account.");

            await UpdateAsync(conn, tx, "update players set is_suspended = true where id = @p", id, ct);
            await ActionLog.WriteAsync(conn, adminId, "admin.suspend",
                new { playerId = id, target.Username, reason = reason.Length > 0 ? reason : null }, tx, ct);
            return null;
        });
    }

    private static async Task<IResult> Unsuspend(Guid id, ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        if (await AdminEndpoints.RequireAdminAsync(user, db, ct) is { } denied) return denied;

        Guid adminId = user.PlayerId();
        return await ChangeAsync(db, id, adminId, ct, async (conn, tx, target) =>
        {
            await UpdateAsync(conn, tx, "update players set is_suspended = false where id = @p", id, ct);
            await ActionLog.WriteAsync(conn, adminId, "admin.unsuspend", new { playerId = id, target.Username }, tx, ct);
            return null;
        });
    }

    private static async Task<IResult> SetRole(
        Guid id, RoleRequest? body, ClaimsPrincipal user, NpgsqlDataSource db, CancellationToken ct)
    {
        if (await AdminEndpoints.RequireAdminAsync(user, db, ct) is { } denied) return denied;

        string role = (body?.Role ?? string.Empty).Trim();
        if (role != "admin" && role != "player")
            return ApiProblem.Create(400, ErrorCodes.BadRequest, "role must be \"admin\" or \"player\".");

        Guid adminId = user.PlayerId();
        if (id == adminId)
            return ApiProblem.Create(400, ErrorCodes.AdminSelf, "You cannot change your own role.");

        return await ChangeAsync(db, id, adminId, ct, async (conn, tx, target) =>
        {
            if (role == "admin" && target.Suspended)
                return ApiProblem.Create(409, ErrorCodes.AdminTargetSuspended, "Unsuspend this account before making it an admin.");

            await using (var cmd = new NpgsqlCommand("update players set role = @r where id = @p", conn, tx))
            {
                cmd.Parameters.AddWithValue("r", role);
                cmd.Parameters.AddWithValue("p", id);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await ActionLog.WriteAsync(conn, adminId, "admin.role",
                new { playerId = id, target.Username, from = target.Role, to = role }, tx, ct);
            return null;
        });
    }

    private sealed record Target(string Username, string Role, bool Suspended);

    /// <summary>
    /// Khóa dòng người chơi, chạy thay đổi trong một transaction rồi trả về trạng thái mới.
    /// change trả về IResult khi từ chối (không ghi gì), null khi đã ghi.
    /// </summary>
    private static async Task<IResult> ChangeAsync(
        NpgsqlDataSource db, Guid id, Guid adminId, CancellationToken ct,
        Func<NpgsqlConnection, NpgsqlTransaction, Target, Task<IResult?>> change)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        Target? target;
        await using (var read = new NpgsqlCommand(
            "select username, role, is_suspended from players where id = @p for update", conn, tx))
        {
            read.Parameters.AddWithValue("p", id);
            await using var reader = await read.ExecuteReaderAsync(ct);
            target = await reader.ReadAsync(ct) ? new Target(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)) : null;
        }
        if (target == null)
            return ApiProblem.Create(404, ErrorCodes.NotFound, "This player does not exist.");

        if (await change(conn, tx, target) is { } refused) return refused;

        string role;
        bool suspended;
        await using (var after = new NpgsqlCommand("select role, is_suspended from players where id = @p", conn, tx))
        {
            after.Parameters.AddWithValue("p", id);
            await using var reader = await after.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            role = reader.GetString(0);
            suspended = reader.GetBoolean(1);
        }
        await tx.CommitAsync(ct);

        return Results.Ok(new { playerId = id.ToString(), username = target.Username, role, isSuspended = suspended });
    }

    private static async Task UpdateAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, Guid id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("p", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Tìm theo chuỗi người dùng gõ: % và _ là ký tự thường, không phải ký tự đại diện
    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

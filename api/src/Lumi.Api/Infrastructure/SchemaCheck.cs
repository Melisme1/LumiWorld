using Npgsql;

namespace Lumi.Api.Infrastructure;

/// <summary>
/// Schema nằm trong D:\LumiDB\migrations và chạy bằng scripts\migrate.ps1 (role lumi_owner).
/// Server không tạo bảng; lúc khởi động chỉ kiểm tra schema_migrations đã có đủ các bản server cần.
/// Thêm migration mới mà server dùng tới thì thêm tên file (không đuôi) vào RequiredVersions.
/// </summary>
public static class SchemaCheck
{
    public static readonly string[] RequiredVersions = ["001_init", "002_market", "003_world_map", "004_crafted_resource_prices", "005_river_view", "006_creature_card_market", "007_card_trades", "009_report_views"];

    public static async Task EnsureAsync(NpgsqlDataSource dataSource, ILogger logger, CancellationToken ct = default)
    {
        var applied = new HashSet<string>();
        await using (var conn = await dataSource.OpenConnectionAsync(ct))
        await using (var exists = new NpgsqlCommand("select to_regclass('schema_migrations') is not null", conn))
        {
            if ((bool)(await exists.ExecuteScalarAsync(ct))!)
            {
                await using var read = new NpgsqlCommand("select version from schema_migrations", conn);
                await using var reader = await read.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    applied.Add(reader.GetString(0));
            }
        }

        string[] missing = RequiredVersions.Where(v => !applied.Contains(v)).ToArray();
        if (missing.Length == 0)
            return;

        string message = $"Database thiếu migration {string.Join(", ", missing)}. "
            + @"Chạy D:\LumiDB\scripts\migrate.ps1 rồi khởi động lại server.";
        logger.LogCritical("{Message}", message);
        throw new InvalidOperationException(message);
    }
}

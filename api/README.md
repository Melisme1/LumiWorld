# LumiWorld Server

Server cho các tính năng online của LumiWorld: tài khoản, save duy nhất của mỗi người chơi,
đảo chia sẻ cho người khác thăm, hộp thư, tham số game. Chợ, obstacle và admin thêm theo
tab Server trong "Kế hoạch Multiplayer LumiWorld".

Stack: ASP.NET Core 8 (Minimal API), PostgreSQL 18 qua Npgsql (SQL viết tay, không EF Core),
JWT, Serilog, xUnit.

## Cần cài

- .NET SDK 9 trở lên (`dotnet --list-sdks`). Server build net8.0, project test build net9.0.
- PostgreSQL 18 cài bằng installer cho Windows, và thư mục database `D:\LumiDB`
  (schema, role, migration; xem `D:\LumiDB\README.md`). Lần đầu chạy trong `D:\LumiDB`:
  `scripts\setup.ps1` rồi `scripts\migrate.ps1`.

## Chạy

```
cd src/Lumi.Api
dotnet run
```

Server nghe ở `http://localhost:8080`, đúng địa chỉ mặc định của `ApiClient.cs` phía Unity.
Server không tự tạo bảng: lúc khởi động nó kiểm tra bảng `schema_migrations` đã có đủ migration,
thiếu thì dừng và bảo chạy `D:\LumiDB\scripts\migrate.ps1`. Kiểm tra: mở `http://localhost:8080/health`.

Cấu hình dev nằm trong `src/Lumi.Api/appsettings.Development.json` (không commit): chép từ `appsettings.Development.example.json` rồi điền mật khẩu `lumi_app`. Khi chạy thật, đặt qua biến môi trường:

- `ConnectionStrings__Lumi`: chuỗi kết nối PostgreSQL, bằng role `lumi_app`
- `Jwt__Key`: khóa ký token, ít nhất 32 ký tự, không commit

## Test

Cần database `lumi_test` (`D:\LumiDB\scripts\setup.ps1` tạo sẵn), rồi chạy `dotnet test` ở thư mục này.
Mỗi lần chạy, test dùng `lumi_owner` xóa sạch `lumi_test`, chạy các file trong `$LUMI_DB_DIR\migrations`
(mặc định `D:\LumiDB`), rồi server trong test nối bằng `lumi_app`. Không đụng database `lumi`.
Mật khẩu lấy từ biến môi trường `LUMI_OWNER_PASSWORD` và `LUMI_APP_PASSWORD` (cùng tên trong `database/.env`), hoặc đổi cả chuỗi kết nối bằng `LUMI_TEST_OWNER_DB` và `LUMI_TEST_DB`.

## Unity gọi được server

chitan2 đặt `insecureHttpOption` về 0 nên Unity chặn `http://`. Khi dev, vào
Project Settings > Player > Other Settings > Allow downloads over HTTP, chọn
"Allowed in development builds"; hoặc dùng địa chỉ https của Cloudflare tunnel trên màn đăng nhập.

## Endpoint

| Endpoint | Việc |
|---|---|
| POST /api/v1/auth/register, POST /api/v1/auth/login | Đăng ký, đăng nhập (khớp AuthScreen.cs) |
| GET /api/v1/auth/me | Thông tin tài khoản |
| GET /api/v1/config | Tham số game (không cần đăng nhập) |
| GET /api/v1/saves/me, PUT /api/v1/saves/me | Save duy nhất của người chơi, có kiểm tra revision |
| PUT /api/v1/worlds/me, DELETE /api/v1/worlds/me | Chia sẻ hoặc gỡ snapshot đảo |
| GET /api/v1/worlds, GET /api/v1/worlds/{playerId} | Danh sách đảo, xem một đảo |
| GET /api/v1/inbox, POST /api/v1/inbox/ack | Hộp thư |
| POST /api/v1/inbox/dev-item | Chỉ có khi Development: tự gửi một mục thử |
| GET /api/v1/market/listings, GET /api/v1/market/listings/mine | Chợ: xem lô đang bán, lô của mình |
| POST /api/v1/market/listings, POST /api/v1/market/listings/{id}/buy, POST /api/v1/market/listings/{id}/cancel | Đăng bán, mua, hủy (bắt buộc Idempotency-Key) |
| POST /api/v1/worlds/{playerId}/obstacles | Gửi obstacle tới đảo đã chia sẻ (bắt buộc Idempotency-Key) |
| GET /api/v1/leaderboard | Bảng xếp hạng theo điểm world |
| GET /api/v1/admin/config, PUT /api/v1/admin/config | Admin xem và đổi tham số game_config |
| GET /api/v1/admin/stats | Admin xem số liệu người chơi, chợ, lượt thăm |
| GET /api/v1/admin/stats/daily?from=&to= | Admin xem số liệu theo từng ngày (giờ Việt Nam, đọc view report_* của migration 009) |
| GET /api/v1/admin/players, POST /api/v1/admin/players/{id}/suspend, /unsuspend, /role | Admin tìm, khóa, mở khóa, đổi quyền người chơi |

Lỗi luôn là `application/problem+json` có trường `code` (ví dụ `USERNAME_TAKEN`, `SAVE_CONFLICT`).
Client rẽ nhánh theo `code`, nên không đổi tên mã đã dùng.

## Thêm bảng

Thêm file mới `D:\LumiDB\migrations\004_ten.sql` rồi chạy `D:\LumiDB\scripts\migrate.ps1`. Không sửa file đã chạy.
Nếu server cần bảng mới, thêm tên file (không đuôi) vào `RequiredVersions` trong
`src/Lumi.Api/Infrastructure/SchemaCheck.cs`.

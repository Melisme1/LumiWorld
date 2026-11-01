# LumiDB

Mọi thứ về database PostgreSQL của LumiWorld: schema, role, script cài đặt và tài liệu.
LumiServer (D:\Lumi) không tự tạo bảng; nó chỉ kiểm tra lúc khởi động là migration đã chạy đủ.

Cần PostgreSQL 18 cài bằng installer cho Windows (service `postgresql-x64-18`, cổng 5432,
chỉ nghe localhost). Các script chạy được bằng Windows PowerShell 5.1.

## Mật khẩu

Chép `.env.example` thành `.env` rồi điền mật khẩu của ba role. `.env` nằm trong `.gitignore`,
không commit. Mật khẩu `postgres` không ghi vào file: đặt `$env:PGPASSWORD` trước khi chạy
`setup.ps1`, hoặc để psql hỏi.

| Role | Dùng cho | Quyền |
|---|---|---|
| postgres | `scripts\setup.ps1` | Toàn quyền, không dùng hằng ngày |
| lumi_owner | `scripts\migrate.ps1`, dotnet test xóa và dựng lại lumi_test | Sở hữu database và bảng |
| lumi_app | LumiServer | select, insert, update, delete trên bảng; dùng sequence |
| lumi_report | Dashboard (giai đoạn 4) | Chỉ select trên các view trong `views\` |

## Cài lần đầu

```
cd D:\LumiDB
$env:PGPASSWORD = '<mật khẩu postgres>'
powershell -ExecutionPolicy Bypass -File scripts\setup.ps1
Remove-Item env:PGPASSWORD
powershell -ExecutionPolicy Bypass -File scripts\migrate.ps1
powershell -ExecutionPolicy Bypass -File scripts\migrate.ps1 -Database lumi_test
```

`setup.ps1` chạy `setup\01_roles.sql`, `02_databases.sql` (tạo `lumi` và `lumi_test`, owner lumi_owner,
UTF8) và `03_grants.sql` cho cả hai database. Chạy lại được: role có rồi thì chỉ đặt lại mật khẩu.

## Thêm bảng

1. Thêm file mới `migrations\004_ten.sql`. Không sửa file đã chạy.
2. Chạy `scripts\migrate.ps1`. Mỗi file chạy trong một transaction cùng dòng ghi vào
   `schema_migrations`, nên lỗi giữa chừng thì database không đổi.
3. Nếu LumiServer cần bảng mới, thêm tên file (không đuôi) vào `RequiredVersions` trong
   `D:\Lumi\src\Lumi.Api\Infrastructure\SchemaCheck.cs`.

Bảng và sequence lumi_owner tạo ra tự cấp quyền cho lumi_app (default privileges trong `03_grants.sql`).
`dotnet test` trong D:\Lumi đọc migration từ `$LUMI_DB_DIR\migrations` (mặc định `D:\LumiDB`).

## Sao lưu và khôi phục

```
powershell -ExecutionPolicy Bypass -File D:\LumiDB\scripts\backup.ps1
powershell -ExecutionPolicy Bypass -File D:\LumiDB\scripts\restore.ps1 -File D:\LumiDB\backups\lumi_<ngày_giờ>.dump
```

`backup.ps1` ghi `backups\lumi_<yyyyMMdd_HHmmss>.dump` (pg_dump định dạng custom), đọc lại mục lục để chắc
file không hỏng, và chỉ giữ 14 bản mới nhất (`-Keep`). `-Database lumi_test` để sao lưu database khác.
File chứa bảng, view, dữ liệu và quyền trên bảng; role và mật khẩu thì dựng lại bằng `setup.ps1`.

`restore.ps1` hỏi gõ lại tên database, tự sao lưu database đích trước, rồi khôi phục trong một transaction
(lỗi thì database không đổi). Tắt LumiServer trước khi khôi phục vào `lumi`. Muốn thử thì khôi phục vào
`lumi_test` trước (`-Database lumi_test`); `dotnet test` sẽ dựng lại `lumi_test` sau đó. Bản sao lưu cũ hơn
code hiện tại thì chạy `migrate.ps1` sau khi khôi phục. Database đích đã chạy migration mới hơn bản sao lưu
thì script dừng, không đổi gì (bảng mới sẽ còn sót và migrate báo trùng): khi đó khôi phục vào database trống.

## Cho bạn bè chơi

1. Chạy `powershell -ExecutionPolicy Bypass -File D:\LumiDB\scripts\share-server.ps1`.
2. Script in ra địa chỉ `https://....trycloudflare.com` (đã chép vào clipboard). Gửi địa chỉ đó cho bạn bè.
3. Bạn bè dán địa chỉ vào ô server ở màn đăng nhập của game.

Địa chỉ đổi mỗi lần chạy script, nên mỗi lần phải gửi lại. Máy phải bật và cửa sổ script phải mở
trong lúc mọi người chơi; Ctrl+C tắt cả tunnel và server.

## Thư mục

```
setup\       chạy một lần bằng postgres: role, database, quyền
migrations\  schema, chạy theo thứ tự tên, mỗi file đúng một lần
views\       view cho dashboard (giai đoạn 4)
jobs\        dọn dữ liệu cũ, tổng hợp số liệu mỗi đêm (giai đoạn 2, 3)
seed\        dữ liệu thử (giai đoạn 4)
scripts\     setup.ps1, migrate.ps1, backup.ps1, restore.ps1, share-server.ps1
docs\        ERD và từ điển dữ liệu (giai đoạn 4)
backups\     bản pg_dump, không commit
```

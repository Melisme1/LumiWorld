-- LumiServer (lumi_app) chỉ cần đọc schema_migrations để kiểm tra lúc khởi động (SchemaCheck).
-- Bảng này do migrate.ps1 tạo bằng lumi_owner nên ăn default privileges ghi đủ quyền cho lumi_app;
-- thu lại để server (hoặc ai lấy được mật khẩu lumi_app) không giả được migration đã chạy.
revoke insert, update, delete, truncate on schema_migrations from lumi_app;
grant select on schema_migrations to lumi_app;

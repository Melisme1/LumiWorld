# LumiWorld Staging Package (SEP490 Defense 1.1)

Bộ cấu hình Docker + PostgreSQL 18 + script nạp dữ liệu mẫu cho môi trường **Staging** của LumiWorld (khớp nhánh `origin/backend`: ASP.NET Core 8 `Lumi.Api` + PostgreSQL 18 `database/migrations/001..010`).

> **Lưu ý về nhánh Git:** Hiện tại bạn đang ở nhánh `MapBuilding` (Unity Client), còn `api/` và `database/` nằm trên nhánh `origin/backend`. Hãy copy thư mục `Staging/` này vào root của nhánh `backend` (cạnh `api/` và `database/`), hoặc checkout `backend` rồi merge/copy thư mục này vào.

---

## 1. Cấu trúc file

- `Staging/Dockerfile` — Multi-stage build .NET 8 SDK → ASP.NET Core 8 Runtime (`ASPNETCORE_ENVIRONMENT=Staging`, cổng `8080`, `HEALTHCHECK /health`).
- `Staging/docker-compose.yml` — Dựng đủ 3 service chính (`db` PostgreSQL 18 → `db-migrate` tự chạy `setup` + `migrations/001..010` → `api`) + profile tùy chọn `caddy` để cấp HTTPS tự động khi chạy trên VPS có domain.
- `Staging/db-bootstrap.sh` — Script Linux tương đương `setup.ps1` + `migrate.ps1`, tạo đủ role (`lumi_owner`, `lumi_app`, `lumi_report`), 2 DB (`lumi`, `lumi_test`) và chạy migration có kiểm tra `schema_migrations`.
- `Staging/.env.staging.example` — Mẫu biến môi trường (copy thành `.env.staging` và đổi mật khẩu + `Jwt__Key` ≥ 32 ký tự).
- `Staging/Caddyfile` — Reverse proxy HTTPS tự động cho VPS có domain (Unity bắt buộc `https://` do `insecureHttpOption = 0`).
- `Staging/seed-staging.ps1` — Script nạp + reset dữ liệu mẫu cho **toàn bộ 7 workflows (WF-01..WF-07)** và tài khoản Admin `/admin`.

---

## 2. Cách triển khai lên server Staging (VPS / Server có Docker)

```bash
# 1. Ở thư mục root của nhánh backend (nơi có api/, database/, Staging/)
cp Staging/.env.staging.example Staging/.env.staging
# Mở Staging/.env.staging và điền mật khẩu thật + LUMI_JWT_KEY (>= 32 ký tự)

# 2a. Chạy HTTP (sau load balancer / Render / Railway / Cloudflare Zero Trust Tunnel):
docker compose -f Staging/docker-compose.yml --env-file Staging/.env.staging up -d --build

# 2b. Hoặc chạy kèm Caddy tự cấp chứng chỉ HTTPS trên VPS có domain:
docker compose -f Staging/docker-compose.yml --env-file Staging/.env.staging --profile https up -d --build

# 3. Kiểm tra server sống & DB migration xong:
curl -s http://localhost:8080/health
```

---

## 3. Cách nạp & reset dữ liệu mẫu cho Defense 1.1

Chạy lặp lại được bất kỳ lúc nào (thêm `-Reset` để xóa sạch giao dịch cũ và nạp lại từ đầu trước khi demo cho Hội đồng):

```powershell
# Nạp mới / Reset hoàn toàn trên staging Docker:
powershell -ExecutionPolicy Bypass -File Staging\seed-staging.ps1 `
  -BaseUrl "https://<your-staging-url>" -Reset

# Hoặc nạp qua API mà không cần đụng trực tiếp DB (khi API đã nối sẵn DB cloud):
powershell -ExecutionPolicy Bypass -File Staging\seed-staging.ps1 `
  -BaseUrl "https://<your-staging-url>" -SkipDbSql
```

### Danh sách tài khoản seed sẵn (mật khẩu mặc định: `LumiDemo2026!`)

| Username | Vai trò / Mục đích demo | Dữ liệu seed sẵn |
|---|---|---|
| `staging_admin` | Admin (`F-14` / `/admin/`) | Quyền `admin` truy cập Admin Portal |
| `demo_vinh` | Owner `WF-01`, `WF-06` | Đảo đã có Leafwood cluster + `Medieval_Village` + Snapshot World Map |
| `demo_toan` | Owner `WF-02` | Đảo + tài nguyên kho (`raspberries`, `acorns`, `wheat`) + `500` Coins |
| `demo_tan` | Owner `WF-03`, `WF-05` | Tài khoản **đã qua 96 giờ (>72h bảo vệ)** + Snapshot World Map + `600` Coins |
| `demo_tuananh` | Seller `WF-04` | Lô chợ active (`resource.leafwood.acorns` 25 Coins, `card.creature_deer` 50 Coins) |
| `demo_buyer` | Buyer `WF-04` | Có `800` Coins sẵn sàng mua / barter & test tranh chấp đồng thời |
| `demo_khang` | Owner `WF-07` | Có sẵn cặp `creature_deer` & `creature_caterpillar` + `600` Coins |

> **Lưu ý Unity Client:** Trong màn hình đăng nhập (`AuthScreen.cs`), nhập đúng URL **`https://...`** của Staging vào ô **Server URL**. (Không dùng `http://` thường trên bản Release vì Unity bật `insecureHttpOption = 0`.)

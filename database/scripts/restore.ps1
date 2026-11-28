# Khôi phục một bản sao lưu (backup.ps1) vào database, bằng role lumi_owner, trong một transaction:
# lỗi giữa chừng thì database giữ nguyên như trước. Mọi bảng, view và dữ liệu hiện có bị thay bằng bản sao lưu.
# Trước khi khôi phục, script tự sao lưu database đích một lần (trừ khi -NoSafetyBackup), để còn đường lùi.
#   powershell -ExecutionPolicy Bypass -File D:\LumiDB\scripts\restore.ps1 -File D:\LumiDB\backups\lumi_20261101_090000.dump
#   powershell -ExecutionPolicy Bypass -File D:\LumiDB\scripts\restore.ps1 -File ... -Database lumi_test   (thử trước trên lumi_test)
# Tắt LumiServer trước khi khôi phục vào lumi (server đang ghi thì restore phải đợi hoặc lỗi khóa bảng).
param(
    [Parameter(Mandatory = $true)][string]$File,
    [string]$Database = 'lumi',
    [switch]$Yes,
    [switch]$NoSafetyBackup
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_env.ps1')

$PgRestore = Join-Path $PgBin 'pg_restore.exe'
if (-not (Test-Path $PgRestore)) { $PgRestore = 'pg_restore' }

if (-not (Test-Path $File)) { throw "Không thấy file $File" }
$File = (Resolve-Path $File).Path

$env:PGPASSWORD = $env:LUMI_OWNER_PASSWORD
& $PgRestore --list $File | Out-Null
if ($LASTEXITCODE -ne 0) { throw "$File không phải bản sao lưu pg_dump hợp lệ" }

# --clean chỉ xóa đối tượng có trong bản sao lưu: database đích đã chạy migration mới hơn thì bảng mới còn lại
# trong khi schema_migrations quay về bản cũ, migrate.ps1 sau đó sẽ báo trùng. Dừng trước khi đổi gì.
$backupVersions = @(& $PgRestore -a -t schema_migrations -f - $File |
    Where-Object { $_ -match '^\d{3}_\S+\t' } | ForEach-Object { ($_ -split "`t")[0] })
$targetVersions = @(& $Psql -U lumi_owner -d $Database -X -tA -c 'select version from schema_migrations' 2>$null)
$newer = @($targetVersions | Where-Object { $_ -and ($backupVersions -notcontains $_) })
if ($backupVersions.Count -gt 0 -and $newer.Count -gt 0) {
    throw "Database $Database đã có migration mới hơn bản sao lưu ($($newer -join ', ')). " +
          "Khôi phục vào database trống (xóa và tạo lại $Database bằng postgres, chạy 03_grants.sql) rồi chạy migrate.ps1."
}

if (-not $Yes) {
    Write-Host "Sắp THAY toàn bộ dữ liệu của database '$Database' bằng $File."
    $answer = Read-Host "Gõ tên database ($Database) để tiếp tục"
    if ($answer -ne $Database) { Write-Host 'Đã hủy, không đổi gì.'; exit 1 }
}

if (-not $NoSafetyBackup) {
    & (Join-Path $PSScriptRoot 'backup.ps1') -Database $Database -Keep 1000
}

$env:PGPASSWORD = $env:LUMI_OWNER_PASSWORD
& $PgRestore -U lumi_owner -d $Database --clean --if-exists --no-owner --single-transaction --exit-on-error $File
if ($LASTEXITCODE -ne 0) { throw "Khôi phục lỗi, database $Database không đổi" }

$migrations = & $Psql -U lumi_owner -d $Database -X -tA -c 'select count(*) from schema_migrations'
$players = & $Psql -U lumi_owner -d $Database -X -tA -c 'select count(*) from players'
Write-Host "Đã khôi phục $Database từ $(Split-Path -Leaf $File): $migrations migration, $players người chơi."
Write-Host 'Nếu bản sao lưu cũ hơn code hiện tại, chạy migrate.ps1 để bù các migration còn thiếu.'

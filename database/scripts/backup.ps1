# Sao lưu một database bằng pg_dump (định dạng custom, nén sẵn) vào backups\, bằng role lumi_owner.
# Tên file: <database>_<yyyyMMdd_HHmmss>.dump. Giữ lại -Keep bản mới nhất của database đó, bản cũ hơn bị xóa.
# Sau khi ghi, đọc lại mục lục bằng pg_restore --list để chắc file không hỏng.
#   powershell -ExecutionPolicy Bypass -File D:\LumiDB\scripts\backup.ps1
#   powershell -ExecutionPolicy Bypass -File D:\LumiDB\scripts\backup.ps1 -Database lumi_test -Keep 3
# Role, mật khẩu và quyền mặc định không nằm trong file (dựng lại bằng setup.ps1); quyền trên bảng thì có.
param(
    [string]$Database = 'lumi',
    [int]$Keep = 14
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_env.ps1')

$PgDump = Join-Path $PgBin 'pg_dump.exe'
$PgRestore = Join-Path $PgBin 'pg_restore.exe'
if (-not (Test-Path $PgDump)) { $PgDump = 'pg_dump' }
if (-not (Test-Path $PgRestore)) { $PgRestore = 'pg_restore' }

$backupDir = Join-Path $LumiDbDir 'backups'
New-Item -ItemType Directory -Force $backupDir | Out-Null

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$file = Join-Path $backupDir "${Database}_$stamp.dump"

$env:PGPASSWORD = $env:LUMI_OWNER_PASSWORD
& $PgDump -U lumi_owner -d $Database -Fc -Z 6 -f $file
if ($LASTEXITCODE -ne 0) {
    Remove-Item -Force -ErrorAction SilentlyContinue $file
    throw "pg_dump lỗi, không sao lưu được $Database"
}

# File đọc được mục lục thì pg_restore dùng được
$entries = @(& $PgRestore --list $file | Where-Object { $_ -and -not $_.StartsWith(';') })
if ($LASTEXITCODE -ne 0 -or $entries.Count -eq 0) { throw "File sao lưu $file hỏng hoặc rỗng" }

$sizeKb = [math]::Round((Get-Item $file).Length / 1KB, 1)
Write-Host "Đã sao lưu $Database vào $file ($sizeKb KB, $($entries.Count) mục)"

# Chỉ giữ -Keep bản mới nhất của database này
$old = Get-ChildItem $backupDir -Filter "${Database}_*.dump" | Sort-Object Name -Descending | Select-Object -Skip ([math]::Max(1, $Keep))
foreach ($f in $old) {
    Remove-Item -Force $f.FullName
    Write-Host "Xóa bản cũ $($f.Name)"
}

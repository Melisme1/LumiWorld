param(
    [string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor'
)
$ErrorActionPreference = 'Stop'

$suites = @(
    @{ Name = 'World';        Script = 'Tests\World\Run-WorldChecks.ps1' },
    @{ Name = 'Islands';      Script = 'Tests\Islands\Run-IslandChecks.ps1' },
    @{ Name = 'Disasters';    Script = 'Tests\Disasters\Run-DisasterChecks.ps1' },
    @{ Name = 'OnlineClient'; Script = 'Tests\OnlineClient\Run-OnlineClientChecks.ps1' }
)

$failures = 0
foreach ($s in $suites) {
    Write-Host ""
    Write-Host "================ RUNNING SUITE: $($s.Name) ================" -ForegroundColor Cyan
    try {
        powershell -NoProfile -ExecutionPolicy Bypass -File $s.Script -UnityEditorPath $UnityEditorPath
        if ($LASTEXITCODE -ne 0) { $failures++ }
    } catch {
        $failures++
        Write-Host "[ERROR] Suite $($s.Name) failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "================ RUNNING TIME CORE CHECKS ================" -ForegroundColor Cyan
try {
    # Compile & run pure TimeChecks (excluding legacy IntegrationChecks until OrderBoard stubs updated)
    $projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $timeOut = Join-Path $projectRoot 'Temp/TimeChecks'
    New-Item -ItemType Directory -Path $timeOut -Force | Out-Null
    $dotnetPath = Join-Path $UnityEditorPath 'Data/DotNetSdk/dotnet.exe'
    $sdkBase = Join-Path $UnityEditorPath 'Data/DotNetSdk/sdk'
    $packBase = Join-Path $UnityEditorPath 'Data/DotNetSdk/packs/Microsoft.NETCore.App.Ref'
    if (-not (Test-Path $dotnetPath) -or -not (Test-Path $packBase)) {
        $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
        $dotnetRoot = Split-Path $dotnetPath -Parent
        $sdkBase = Join-Path $dotnetRoot 'sdk'
        $packBase = Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref'
    }
    $sdkPath = Get-ChildItem $sdkBase -Directory | Where-Object { $_.Name -like '8.*' } | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $sdkPath) { $sdkPath = Get-ChildItem $sdkBase -Directory | Sort-Object Name -Descending | Select-Object -First 1 }
    $refVer = Get-ChildItem $packBase -Directory | Where-Object { $_.Name -like '8.*' } | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $refVer) { $refVer = Get-ChildItem $packBase -Directory | Sort-Object Name -Descending | Select-Object -First 1 }
    $refs = Get-ChildItem (Join-Path $refVer.FullName 'ref/net8.0') -Filter '*.dll'
    $timeSources = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Acs/Scripts/Time/Core') -Filter '*.cs' | ForEach-Object FullName)
    $timeSources += @(
        (Join-Path $projectRoot 'Assets/Acs/Scripts/Production/ProductionState.cs'),
        (Join-Path $projectRoot 'Assets/Acs/Scripts/Production/ProductionLedger.cs'),
        (Join-Path $projectRoot 'Tests/Time/LedgerTestTypes.cs'),
        (Join-Path $projectRoot 'Tests/Time/TimeChecks.cs')
    )
    $timeDll = Join-Path $timeOut 'TimeChecks.dll'
    $timeRsp = Join-Path $timeOut 'time_ci.rsp'
    $resp = @('/nologo','/target:exe','/langversion:latest',('/out:"' + $timeDll + '"'))
    $resp += $refs | ForEach-Object { '/reference:"' + $_.FullName + '"' }
    $resp += $timeSources | ForEach-Object { '"' + $_ + '"' }
    [IO.File]::WriteAllLines($timeRsp, $resp)
    [IO.File]::WriteAllText((Join-Path $timeOut 'TimeChecks.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
    & $dotnetPath (Join-Path $sdkPath.FullName 'Roslyn/bincore/csc.dll') ('@' + $timeRsp)
    if ($LASTEXITCODE -ne 0) { throw 'TimeChecks compilation failed.' }
    & $dotnetPath $timeDll
    if ($LASTEXITCODE -ne 0) { $failures++ }
} catch {
    $failures++
    Write-Host "[ERROR] TimeChecks failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
if ($failures -gt 0) {
    Write-Host "ALL CHECKS RESULT: $failures suite(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "ALL CLIENT CHECK SUITES PASSED CLEANLY." -ForegroundColor Green

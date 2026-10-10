param([string]$UnityEditorPath = 'D:/Unity/6000.5.1f1/Editor')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputPath = Join-Path $projectRoot 'Temp/TimeChecks'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$dotnetPath = Join-Path $UnityEditorPath 'Data/DotNetSdk/dotnet.exe'
$sdkPath = Get-ChildItem (Join-Path $UnityEditorPath 'Data/DotNetSdk/sdk') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$referenceVersion = Get-ChildItem (Join-Path $UnityEditorPath 'Data/DotNetSdk/packs/Microsoft.NETCore.App.Ref') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$references = Get-ChildItem (Join-Path $referenceVersion.FullName 'ref/net8.0') -Filter '*.dll'
$sourceFiles = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Acs/Scripts/Time/Core') -Filter '*.cs' | ForEach-Object FullName)
$sourceFiles += @(
    (Join-Path $projectRoot 'Assets/Acs/Scripts/Production/ProductionState.cs'),
    (Join-Path $projectRoot 'Assets/Acs/Scripts/Production/ProductionLedger.cs'),
    (Join-Path $PSScriptRoot 'LedgerTestTypes.cs'),
    (Join-Path $PSScriptRoot 'TimeChecks.cs')
)
$response = @('/nologo','/target:exe','/langversion:latest',('/out:"' + (Join-Path $outputPath 'TimeChecks.dll') + '"'))
$response += $references | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$response += $sourceFiles | ForEach-Object { '"' + $_ + '"' }
$responsePath = Join-Path $outputPath 'time.rsp'
[IO.File]::WriteAllLines($responsePath, $response)
[IO.File]::WriteAllText((Join-Path $outputPath 'TimeChecks.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
& $dotnetPath (Join-Path $sdkPath.FullName 'Roslyn/bincore/csc.dll') ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Time checks compilation failed.' }
& $dotnetPath (Join-Path $outputPath 'TimeChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Time checks failed.' }

$integrationSources = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Acs/Scripts/Time/Core') -Filter '*.cs' | ForEach-Object FullName)
$integrationSources += @(
    (Join-Path $projectRoot 'Assets/Acs/Scripts/Time/Runtime/GameTimeRuntime.cs'),
    (Join-Path $projectRoot 'Assets/Project/Script/Economy/OrderBoardSystem.cs'),
    (Join-Path $projectRoot 'Assets/Project/Script/Economy/EconomySaveSystem.cs'),
    (Join-Path $projectRoot 'Assets/Project/Script/Economy/EconomySaveData.cs'),
    (Join-Path $PSScriptRoot 'IntegrationTestTypes.cs'),
    (Join-Path $PSScriptRoot 'IntegrationChecks.cs')
)
$response = @('/nologo','/target:exe','/langversion:latest',('/out:"' + (Join-Path $outputPath 'IntegrationChecks.dll') + '"'))
$response += $references | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$response += $integrationSources | ForEach-Object { '"' + $_ + '"' }
[IO.File]::WriteAllLines($responsePath, $response)
[IO.File]::WriteAllText((Join-Path $outputPath 'IntegrationChecks.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
& $dotnetPath (Join-Path $sdkPath.FullName 'Roslyn/bincore/csc.dll') ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Integration checks compilation failed.' }
& $dotnetPath (Join-Path $outputPath 'IntegrationChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Integration checks failed.' }

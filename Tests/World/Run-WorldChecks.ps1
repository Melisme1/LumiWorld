param(
    [string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor',
    [string]$JsonOut = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputPath = Join-Path $projectRoot 'Temp/WorldChecks'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

# Locate Roslyn csc.dll + net8.0 reference assemblies (prefer Unity embedded SDK, fall back to system dotnet)
$dotnetPath = Join-Path $UnityEditorPath 'Data/DotNetSdk/dotnet.exe'
$sdkBase = Join-Path $UnityEditorPath 'Data/DotNetSdk/sdk'
$packBase = Join-Path $UnityEditorPath 'Data/DotNetSdk/packs/Microsoft.NETCore.App.Ref'

if (-not (Test-Path $dotnetPath) -or -not (Test-Path $packBase)) {
    $dotnetCmd = Get-Command dotnet -ErrorAction Stop
    $dotnetPath = $dotnetCmd.Source
    $dotnetRoot = Split-Path $dotnetPath -Parent
    $sdkBase = Join-Path $dotnetRoot 'sdk'
    $packBase = Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref'
}

$sdkPath = Get-ChildItem $sdkBase -Directory | Where-Object { $_.Name -like '8.*' } | Sort-Object Name -Descending | Select-Object -First 1
if (-not $sdkPath) { $sdkPath = Get-ChildItem $sdkBase -Directory | Sort-Object Name -Descending | Select-Object -First 1 }

$referenceVersion = Get-ChildItem $packBase -Directory | Where-Object { $_.Name -like '8.*' } | Sort-Object Name -Descending | Select-Object -First 1
if (-not $referenceVersion) { $referenceVersion = Get-ChildItem $packBase -Directory | Sort-Object Name -Descending | Select-Object -First 1 }
$references = Get-ChildItem (Join-Path $referenceVersion.FullName 'ref/net8.0') -Filter '*.dll'

$sourceFiles = @(
    (Join-Path $PSScriptRoot 'WorldTestTypes.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Animal/AnimalSpeciesData.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Core/HexCoordinates.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Core/HexMetrics.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Core/HexTileInfo.cs'),
    (Join-Path $projectRoot 'Assets/Project/Script/Card/CardData.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Settlement/SettlementEnums.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Settlement/SettlementClusterCalculator.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Buff/BiomeBuffCalculator.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/River/RiverPieceType.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/River/RiverConfig.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/River/RiverAutoTileSolver.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/WildIncident/WildIncidentConfig.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/WildIncident/WildIncidentEvaluator.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/WildIncident/WildPackIncident.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/WildIncident/Minigame/RhythmMinigameEngine.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/WildIncident/Minigame/RunnerMinigameEngine.cs'),
    (Join-Path $PSScriptRoot 'WorldChecks.cs')
)

$outDll = Join-Path $outputPath 'WorldChecks.dll'
$response = @('/nologo', '/target:exe', '/langversion:latest', ('/out:"' + $outDll + '"'))
$response += $references | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$response += $sourceFiles | ForEach-Object { '"' + $_ + '"' }
$responsePath = Join-Path $outputPath 'world.rsp'
[IO.File]::WriteAllLines($responsePath, $response)

$tfmMajor = ($referenceVersion.Name -split '\.')[0]
$runtimeJson = '{"runtimeOptions":{"tfm":"net' + $tfmMajor + '.0","framework":{"name":"Microsoft.NETCore.App","version":"' + $referenceVersion.Name + '"}}}'
[IO.File]::WriteAllText((Join-Path $outputPath 'WorldChecks.runtimeconfig.json'), $runtimeJson)

& $dotnetPath (Join-Path $sdkPath.FullName 'Roslyn/bincore/csc.dll') ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'WorldChecks compilation failed.' }

if (-not $JsonOut) { $JsonOut = Join-Path $outputPath 'results.json' }
& $dotnetPath $outDll $JsonOut
if ($LASTEXITCODE -ne 0) { throw 'WorldChecks execution failed.' }

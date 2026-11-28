param(
    [string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor',
    [string]$JsonOut = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputPath = Join-Path $projectRoot 'Temp/IslandChecks'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

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
$referenceVersion = Get-ChildItem $packBase -Directory | Where-Object { $_.Name -like '8.*' } | Sort-Object Name -Descending | Select-Object -First 1
if (-not $referenceVersion) { $referenceVersion = Get-ChildItem $packBase -Directory | Sort-Object Name -Descending | Select-Object -First 1 }
$references = Get-ChildItem (Join-Path $referenceVersion.FullName 'ref/net8.0') -Filter '*.dll'

$sourceFiles = @(
    (Join-Path $PSScriptRoot 'IslandTestTypes.cs'),
    (Join-Path $projectRoot 'Assets/Acs/Scripts/Islands/IslandAccessService.cs'),
    (Join-Path $projectRoot 'Assets/Acs/Scripts/Islands/EconomyIslandAccessStore.cs'),
    (Join-Path $PSScriptRoot 'IslandChecks.cs')
)

$outDll = Join-Path $outputPath 'IslandChecks.dll'
$response = @('/nologo', '/target:exe', '/langversion:latest', ('/out:"' + $outDll + '"'))
$response += $references | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$response += $sourceFiles | ForEach-Object { '"' + $_ + '"' }
$responsePath = Join-Path $outputPath 'islands.rsp'
[IO.File]::WriteAllLines($responsePath, $response)

$tfmMajor = ($referenceVersion.Name -split '\.')[0]
[IO.File]::WriteAllText((Join-Path $outputPath 'IslandChecks.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net' + $tfmMajor + '.0","framework":{"name":"Microsoft.NETCore.App","version":"' + $referenceVersion.Name + '"}}}')

& $dotnetPath (Join-Path $sdkPath.FullName 'Roslyn/bincore/csc.dll') ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'IslandChecks compilation failed.' }

if (-not $JsonOut) { $JsonOut = Join-Path $outputPath 'results.json' }
& $dotnetPath $outDll $JsonOut
if ($LASTEXITCODE -ne 0) { throw 'IslandChecks execution failed.' }

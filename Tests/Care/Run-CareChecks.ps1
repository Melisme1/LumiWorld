param([string]$UnityEditorPath = 'D:/Unity/6000.5.1f1/Editor')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputPath = Join-Path $projectRoot 'Temp/CareChecks'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$dotnetPath = Join-Path $UnityEditorPath 'Data/DotNetSdk/dotnet.exe'
$sdkPath = Get-ChildItem (Join-Path $UnityEditorPath 'Data/DotNetSdk/sdk') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$referenceVersion = Get-ChildItem (Join-Path $UnityEditorPath 'Data/DotNetSdk/packs/Microsoft.NETCore.App.Ref') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$references = Get-ChildItem (Join-Path $referenceVersion.FullName 'ref/net8.0') -Filter '*.dll'
$sources = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Acs/Scripts/Time/Core') -Filter '*.cs' | ForEach-Object FullName)
@(
    'Care/CreatureCareService.cs', 'Care/CreatureFeedingService.cs', 'Care/CreatureCareSettings.cs',
    'Production/CareProductionSimulation.cs', 'Production/ProductionState.cs', 'Production/ProductionLedger.cs',
    'Production/ProductionRateCalculator.cs', 'Production/ProductionRuntimeSettings.cs', 'Production/ProductionBalanceConfig.cs',
    'Production/ResourceProductionDefinition.cs', 'Habitat/HabitatState.cs', 'Habitat/HabitatDataTypes.cs',
    'Habitat/HabitatEligibilityService.cs', 'Habitat/SpeciesMappingRegistry.cs'
) | ForEach-Object { $sources += Join-Path $projectRoot ('Assets/Acs/Scripts/' + $_) }
$sources += Join-Path $projectRoot 'Assets/Project/Script/Economy/ResourceInventory.cs'
$sources += Join-Path $PSScriptRoot 'CareTestTypes.cs'
$sources += Join-Path $PSScriptRoot 'CareChecks.cs'
$response = @('/nologo','/target:exe','/langversion:latest',('/out:"' + (Join-Path $outputPath 'CareChecks.dll') + '"'))
$response += $references | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$response += $sources | ForEach-Object { '"' + $_ + '"' }
$responsePath = Join-Path $outputPath 'care.rsp'
[IO.File]::WriteAllLines($responsePath, $response)
[IO.File]::WriteAllText((Join-Path $outputPath 'CareChecks.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
& $dotnetPath (Join-Path $sdkPath.FullName 'Roslyn/bincore/csc.dll') ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Care checks compilation failed.' }
& $dotnetPath (Join-Path $outputPath 'CareChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Care checks failed.' }

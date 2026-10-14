param([string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputPath = Join-Path $projectRoot 'Temp/CraftingChecks'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$dotnetPath = Join-Path $UnityEditorPath 'Data/DotNetSdk/dotnet.exe'
if (-not (Test-Path $dotnetPath)) {
    $dotnetPath = 'dotnet.exe'
}
$sdkPath = Get-ChildItem (Join-Path $UnityEditorPath 'Data/DotNetSdk/sdk') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$referenceVersion = Get-ChildItem (Join-Path $UnityEditorPath 'Data/DotNetSdk/packs/Microsoft.NETCore.App.Ref') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$references = Get-ChildItem (Join-Path $referenceVersion.FullName 'ref/net8.0') -Filter '*.dll'

$sourceFiles = @(
    (Join-Path $PSScriptRoot 'CraftingTestTypes.cs'),
    (Join-Path $projectRoot 'Assets/Project/Script/Economy/EconomySaveData.cs'),
    (Join-Path $projectRoot 'Assets/Project/Script/Economy/ResourceCatalog.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Crafting/Data/CraftingCategory.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Crafting/Data/CraftingRecipeData.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Crafting/Data/CraftingCatalog.cs'),
    (Join-Path $projectRoot 'Assets/VinhWorkSpace/Scripts/Crafting/Core/CraftingCalculator.cs'),
    (Join-Path $PSScriptRoot 'CraftingChecks.cs')
)

$response = @('/nologo','/target:exe','/langversion:latest',('/out:"' + (Join-Path $outputPath 'CraftingChecks.dll') + '"'))
$response += $references | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$response += $sourceFiles | ForEach-Object { '"' + $_ + '"' }
$responsePath = Join-Path $outputPath 'crafting.rsp'
[IO.File]::WriteAllLines($responsePath, $response)
[IO.File]::WriteAllText((Join-Path $outputPath 'CraftingChecks.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')

& $dotnetPath (Join-Path $sdkPath.FullName 'Roslyn/bincore/csc.dll') ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Crafting checks compilation failed.' }
& $dotnetPath (Join-Path $outputPath 'CraftingChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Crafting checks failed.' }

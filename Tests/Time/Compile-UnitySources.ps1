param([string]$UnityEditorPath = 'D:/Unity/6000.5.1f1/Editor')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputPath = Join-Path $projectRoot 'Temp/TimeChecks'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$sdkPath = Get-ChildItem (Join-Path $UnityEditorPath 'Data/DotNetSdk/sdk') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$mainResponse = Get-ChildItem (Join-Path $projectRoot 'Library/Bee/artifacts') -Filter 'Assembly-CSharp.rsp' -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (!$mainResponse) { throw 'Open the project in Unity once to generate compiler references.' }
Push-Location $projectRoot
try {
    foreach ($assembly in @('Assembly-CSharp','Assembly-CSharp-Editor')) {
        $sourceResponse = Join-Path $mainResponse.DirectoryName ($assembly + '.rsp')
        $lines = [Collections.Generic.List[string]]::new()
        foreach ($line in [IO.File]::ReadAllLines($sourceResponse)) {
            if ($line.StartsWith('-out:')) { $lines.Add('-out:"Temp/TimeChecks/' + $assembly + '.dll"') }
            elseif ($line.StartsWith('-refout:')) { $lines.Add('-refout:"Temp/TimeChecks/' + $assembly + '.ref.dll"') }
            elseif ($line.StartsWith('-r:') -and $line.Contains('/Assembly-CSharp.ref.dll')) { $lines.Add('-r:"Temp/TimeChecks/Assembly-CSharp.ref.dll"') }
            else { $lines.Add($line) }
        }
        $extraSources = if ($assembly -eq 'Assembly-CSharp') {
            @(Get-ChildItem 'Assets/Acs/Scripts/Time' -Filter '*.cs' -Recurse) + @(Get-Item 'Assets/Acs/Scripts/Persistence/TimerSnapshotJson.cs', 'Assets/VinhWorkSpace/Scripts/Placement/HexCreatureTileGuide.cs')
        } else { @(Get-Item 'Assets/Acs/Scripts/Editor/GameTimeRuntimeEditor.cs') }
        foreach ($file in $extraSources) {
            $relative = [IO.Path]::GetRelativePath($projectRoot,$file.FullName).Replace('\','/')
            if (!$lines.Contains('"' + $relative + '"')) { $lines.Add('"' + $relative + '"') }
        }
        $responsePath = Join-Path $outputPath ($assembly + '.rsp')
        [IO.File]::WriteAllLines($responsePath,$lines)
        & (Join-Path $UnityEditorPath 'Data/DotNetSdk/dotnet.exe') (Join-Path $sdkPath.FullName 'Roslyn/bincore/csc.dll') ('@' + $responsePath)
        if ($LASTEXITCODE -ne 0) { throw ($assembly + ' compilation failed.') }
    }
} finally { Pop-Location }

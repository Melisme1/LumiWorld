$src = "C:\Users\Khang\.gemini\antigravity\brain\b7d462ec-1110-4f3b-b479-e17c88fab98c\scratch\test_clean_setup_panel4.png"
$dst = "D:\LumiWorld\Assets\Project\Resources\UI_Breeding\breeding_setup_panel.png"

# Backup first
$bak = "D:\LumiWorld\Assets\Project\Resources\UI_Breeding\breeding_setup_panel.png.bak"
if (-not (Test-Path $bak)) {
    Copy-Item $dst $bak -Force
}

Copy-Item $src $dst -Force
Write-Host "Successfully replaced breeding_setup_panel.png with clean rounded panel!"

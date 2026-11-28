# World Mechanics Pure-C# Checks (`WF-01` & `WF-06`)

Bộ kiểm thử tự động Pure-C# cho các hệ thống xây dựng đảo, định cư, buff hệ sinh thái, sông suối và thu phục thú hoang (`F-02`, `F-03`, `F-15`, `F-16`, `STATE-03`, `STATE-09`), biên dịch trực tiếp mã nguồn sản phẩm trong `Assets/VinhWorkSpace/Scripts/` và `Assets/Project/Script/Card/`.

## Cách chạy

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tests\World\Run-WorldChecks.ps1
```

File kết quả JSON được xuất tại `Temp/WorldChecks/results.json`.

## Phạm vi kiểm tra (`35 checks` → `26 CI Test Cases`)

- **Unit Tests (`21` checks → `TC-F02/F03/F15/F16-01..03`)**:
  - `FN-01` (`HexCoordinates` / `HexMetrics`)
  - `FN-02` (`SettlementClusterCalculator`)
  - `FN-03` (`BiomeBuffCalculator`)
  - `FN-14` (`RiverAutoTileSolver`)
  - `FN-13A` (`RhythmMinigameEngine`)
  - `FN-13B` (`RunnerMinigameEngine`)
- **Integration Tests (`8` checks → `TC-F02/F03/15/16-04..05`)**:
  - `INT-01..02` (`SettlementClusterCalculator + BiomeBuffCalculator + CardData/HexTileInfo`)
  - `INT-03..04` (`BiomeBuffCalculator + GrandCity + River + Split Cluster Isolation`)
  - `INT-05..06` (`WildIncidentEvaluator + WildIncidentConfig + WildPackIncident + Rhythm/Runner`)
  - `INT-07..08` (`RiverAutoTileSolver + RiverConfig + CardData.IsRiverCard + Order-Independence`)
- **State Transition Tests (`6` checks → `TC-TR-07..09`, `TC-TR-25..27`)**:
  - `STR-03` (`STATE-03` Biome/Building/River: `Preview→Established`, forbidden invalid Village/arid, `Damaged→Established` via Rain)
  - `STR-09` (`STATE-09` WildEncounter: `ActivePack→Inspecting→AttemptRunning`, idempotent `Resolve()`, Visitor guard)

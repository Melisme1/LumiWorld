# Creature care checks

Run from the project root in PowerShell:

```powershell
./Tests/Care/Run-CareChecks.ps1
```

Uses Unity's bundled .NET SDK without NuGet. Outputs only `Temp/CareChecks`; never touches the player's economy file or launches Unity.

Compiles the actual Care service, food rules, pantry store/transfers, feeding/settings, Time Core, production coordinator/ledger/rate calculator, eligibility, habitat state, species mapping and ResourceInventory. Stand-ins replace Unity object/attribute APIs, geometry equality, catalog container and economy save attachment. They do not replace the tested satiety/rate/inventory transactions.

140 assertions cover rank decay and exact capacity cutoff; collection/resumption; no duplicate work across slots; healthy replacement and T2 minimum counts; auto-eating at 50; gradual nutrition and full-only resumption; 50 seed units over five seconds; partial meal starvation; early feeding; long/partitioned time equivalence; favorites and allowed-food fallback; isolated individual stock without cross-feeding; atomic Warehouse/pantry transfers and capacity-limited withdrawal; inventory rollback and observer consistency; failed observer isolation; merge/split preserving individual ownership, stale habitat rejection, orphan withdrawal and duplicate identity rejection; same-ID rebuild/rebind; rank and configuration changes; stop/enable and gameplay pause. A floating-point regression checks that repeated 0.1-second bites resume production at exactly full.

These are **managed behavioral checks**, not Unity Play Mode, native asset serialization, UI rendering, web-player verification or persistence of creature state. Full runtime/Editor source compilation uses `Tests/Time/Compile-UnitySources.ps1`; scene steps and remaining manual checks are in `D:/Dev/LumiWorld/docs/LumiWorld_Creature_Hunger_Setup.md`.

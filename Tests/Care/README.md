# Creature care checks

Run from the project root in PowerShell:

```powershell
./Tests/Care/Run-CareChecks.ps1
```

Uses Unity's bundled .NET SDK without NuGet. Outputs only `Temp/CareChecks`; never touches the player's economy file or launches Unity.

Compiles the actual Care service, feeding/settings, Time Core, production coordinator/ledger/rate calculator, eligibility, habitat state, species mapping and ResourceInventory. Stand-ins replace Unity object/attribute APIs, geometry equality, catalog container and economy save attachment. They do not replace the tested hunger/rate/inventory transactions.

62 assertions cover rank thresholds; single/partitioned focus gaps; exact capacity cutoff and collection/resumption; no duplicate work across slots; healthy fallback and T2 minimum counts; no selected/unlocked production; healthy/hungry/recovering feeding; inventory rollback and observer consistency; failed UI observer isolation; recovery boundaries; same-ID rebuild/rebind; rank changes; stop/enable and gameplay pause.

These are **managed behavioral checks**, not Unity Play Mode, native asset serialization, UI rendering, web-player verification or persistence of creature state. Full runtime/Editor source compilation uses `Tests/Time/Compile-UnitySources.ps1`; scene steps and remaining manual checks are in `D:/Dev/LumiWorld/docs/LumiWorld_Creature_Hunger_Setup.md`.

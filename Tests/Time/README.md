# Time checks

From the project root in PowerShell:

```powershell
./Tests/Time/Run-TimeChecks.ps1
./Tests/Time/Compile-UnitySources.ps1
```

Both accept `-UnityEditorPath` (default `D:/Unity/6000.5.1f1/Editor`). The harness uses Unity's bundled .NET 8 SDK, without NuGet. Outputs go to `Temp/TimeChecks`; tests never read or modify the player's economy profile.

- `TimeChecks.cs` runs the real time core and ProductionLedger against fake time and minimal habitat types. Snapshot JSON round trips use System.Text.Json.
- `IntegrationChecks.cs` runs actual GameTimeRuntime, OrderBoardSystem and EconomySaveSystem methods against managed Unity stand-ins and memory storage. It checks pause boundaries, background gaps, legacy deadline persistence, offline reload, order actions, save failure retries and event isolation.
- `Compile-UnitySources.ps1` compiles runtime and Editor sources with the project's existing Bee response files and Unity references; it redirects assembly outputs to Temp. Unity must have imported the project previously. If other scripts/assemblies change, regenerate Bee references in Unity before using this check.

These are managed checks, not Unity Play Mode tests. They do not validate native Unity object destruction, Unity JsonUtility, scene wiring, rendering or a Web player. See `D:/Dev/LumiWorld/docs/LumiWorld_Time_System_Setup.md` for the MapBuilding smoke checklist and API examples.

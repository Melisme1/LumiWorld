# Online & Data Client Checks (`F-01`, `F-10`, `F-11`, `F-12`, `F-17`, `STATE-02/11`)

Kiểm thử Pure-C# cho các module lưu trữ, chia sẻ bản đồ, khóa/giao dịch thẻ, inbox và công thức lai tạo (`LocalWorldSaveStore`, `WorldSnapshot`, `WorldMapLayout`, `CardTradeIds`, `CardLockManager`, `EconomySaveData`, `BreedingRecipe`).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tests\OnlineClient\Run-OnlineClientChecks.ps1
```

using System;

namespace LumiWorld.Acs
{
    // Main-thread transfer commands; UI never mutates two inventories independently.
    public static class HabitatFoodTransferService
    {
        public static bool Deposit(ResourceInventory warehouse, HabitatFoodStore pantries,
            CreatureFoodRules foods, string habitatId, string foodId, int amount, out string reason)
        {
            if (warehouse == null || pantries == null || foods == null || !pantries.IsActive(habitatId))
            { reason = "Biome hoặc kho không khả dụng."; return false; }
            if (amount <= 0 || !foods.ContainsFood(foodId))
            { reason = "Chọn thức ăn trong catalog và số lượng > 0."; return false; }
            bool done = warehouse.TryConsume(foodId, amount, () => pantries.TryAdd(habitatId, foodId, amount));
            reason = done ? "Đã nạp thức ăn vào máng biome." : "Kho không đủ thức ăn; chưa chuyển.";
            return done;
        }
        public static int Withdraw(ResourceInventory warehouse, HabitatFoodStore pantries,
            ResourceData resource, string habitatId, int requested, out string reason)
        {
            if (warehouse == null || pantries == null || resource == null || requested <= 0)
            { reason = "Thiếu kho/resource hoặc số lượng không hợp lệ."; return 0; }
            int amount = (int)Math.Min(requested, Math.Min(pantries.GetAmount(habitatId, resource.resourceID), warehouse.GetFreeSpace(resource)));
            if (amount <= 0) { reason = "Máng trống hoặc Warehouse hết chỗ."; return 0; }
            bool done = warehouse.TryAddAll(new[] { new ResourceInventory.Deposit(resource, amount) }, () =>
            {
                if (!pantries.TryTake(habitatId, resource.resourceID, amount))
                    throw new InvalidOperationException("Pantry changed during withdrawal.");
            });
            reason = done ? "Đã rút " + amount + " về Warehouse." : "Chưa chuyển được thức ăn.";
            return done ? amount : 0;
        }
    }
}

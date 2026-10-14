using System;

namespace LumiWorld.Acs
{
    // Main-thread transfer commands; UI never mutates two inventories independently.
    public static class CreatureFoodTransferService
    {
        public static bool Deposit(ResourceInventory warehouse, CreatureFoodStore pantries,
            CreatureFoodRules foods, string habitatId, string individualId, string foodId, int amount, out string reason)
        {
            if (warehouse == null || pantries == null || foods == null ||
                !pantries.IsResident(habitatId, individualId))
            { reason = "Cá thể không còn ở biome đã chọn hoặc kho không khả dụng."; return false; }
            if (amount <= 0 || !foods.ContainsFood(foodId))
            { reason = "Chọn thức ăn trong catalog và số lượng > 0."; return false; }
            bool done = warehouse.TryConsume(foodId, amount, () => pantries.TryAdd(individualId, foodId, amount));
            reason = done ? "Đã nạp thức ăn riêng cho cá thể." : "Kho không đủ thức ăn; chưa chuyển.";
            return done;
        }
        public static int Withdraw(ResourceInventory warehouse, CreatureFoodStore pantries,
            ResourceData resource, string individualId, int requested, out string reason)
        {
            if (warehouse == null || pantries == null || resource == null || requested <= 0)
            { reason = "Thiếu kho/resource hoặc số lượng không hợp lệ."; return 0; }
            int amount = (int)Math.Min(requested, Math.Min(pantries.GetAmount(individualId, resource.resourceID), warehouse.GetFreeSpace(resource)));
            if (amount <= 0) { reason = "Ngăn thức ăn trống hoặc Warehouse hết chỗ."; return 0; }
            bool done = warehouse.TryAddAll(new[] { new ResourceInventory.Deposit(resource, amount) }, () =>
            {
                if (!pantries.TryTake(individualId, resource.resourceID, amount))
                    throw new InvalidOperationException("Creature food stock changed during withdrawal.");
            });
            reason = done ? "Đã rút " + amount + " về Warehouse." : "Chưa chuyển được thức ăn.";
            return done ? amount : 0;
        }
    }
}

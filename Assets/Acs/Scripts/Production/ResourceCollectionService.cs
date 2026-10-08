using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    // Runtime owns settlement. All collection entry points use this same transfer operation.
    public sealed class ResourceCollectionService
    {
        private bool collecting;

        private sealed class Transfer
        {
            public ResourceProductionBuffer buffer;
            public int amount;
        }

        public ResourceCollectionReceipt Collect(IReadOnlyList<HabitatProductionState> states,
            ResourceInventory inventory, Func<string, ResourceData> resolveResource,
            string habitatId = null, string resourceId = null)
        {
            if (collecting) return Failure(ResourceCollectionStatus.Busy, "Đang xử lý lần thu trước.");
            if (states == null || inventory == null || resolveResource == null)
                return Failure(ResourceCollectionStatus.Unavailable, "Thiếu production hoặc Inventory.");
            if (habitatId != null && string.IsNullOrWhiteSpace(habitatId) ||
                resourceId != null && string.IsNullOrWhiteSpace(resourceId))
                return Failure(ResourceCollectionStatus.Unavailable, "ID habitat hoặc tài nguyên không hợp lệ.");
            collecting = true;
            try
            {
                var sources = new List<HabitatProductionState>();
                foreach (var state in states)
                    if (state != null && (habitatId == null || state.habitatId == habitatId)) sources.Add(state);
                if (habitatId != null && sources.Count == 0)
                    return Failure(ResourceCollectionStatus.Unavailable, "Habitat đã đổi hoặc không còn tồn tại; kiểm tra recovery buffer.");
                sources.Sort((a, b) => string.CompareOrdinal(a.habitatId, b.habitatId));

                var rooms = new Dictionary<string, int>(StringComparer.Ordinal);
                var resources = new Dictionary<string, ResourceData>(StringComparer.Ordinal);
                var totals = new Dictionary<string, int>(StringComparer.Ordinal);
                var seenBuffers = new HashSet<ResourceProductionBuffer>();
                var transfers = new List<Transfer>();
                var items = new List<ResourceCollectionItem>();
                bool missingResource = false;
                foreach (var state in sources)
                    foreach (var buffer in state.buffers)
                    {
                        if (buffer == null || buffer.amount <= 0 || resourceId != null && buffer.resourceId != resourceId) continue;
                        if (string.IsNullOrWhiteSpace(buffer.resourceId) || !seenBuffers.Add(buffer))
                            return Failure(ResourceCollectionStatus.Unavailable, "Buffer có ID thiếu hoặc nguồn bị lặp; chưa chuyển hàng.");
                        if (!resources.TryGetValue(buffer.resourceId, out var resource))
                        {
                            resource = resolveResource(buffer.resourceId);
                            if (resource != null && resource.resourceID != buffer.resourceId) resource = null;
                            resources[buffer.resourceId] = resource;
                        }
                        if (resource == null)
                        {
                            missingResource = true;
                            items.Add(new ResourceCollectionItem(state.habitatId, buffer.resourceId, buffer.resourceId,
                                buffer.amount, 0, "Thiếu ResourceData; giữ nguyên buffer."));
                            continue;
                        }
                        if (!rooms.TryGetValue(buffer.resourceId, out int room)) room = inventory.GetFreeSpace(resource);
                        int accepted = (int)Math.Min(buffer.amount, (long)room);
                        rooms[buffer.resourceId] = room - accepted;
                        if (accepted > 0)
                        {
                            totals.TryGetValue(buffer.resourceId, out int total);
                            totals[buffer.resourceId] = checked(total + accepted);
                            transfers.Add(new Transfer { buffer = buffer, amount = accepted });
                        }
                        items.Add(new ResourceCollectionItem(state.habitatId, buffer.resourceId, resource.resourceName,
                            buffer.amount, accepted, accepted == buffer.amount ? "Đã thu hết phần nguyên." : "Kho không đủ chỗ; giữ phần còn lại."));
                    }
                if (items.Count == 0) return Failure(ResourceCollectionStatus.Empty, "Chưa có tài nguyên nguyên đơn vị để thu.");

                bool remaining = items.Exists(i => i.Remaining > 0);
                var status = transfers.Count > 0 ? (remaining ? ResourceCollectionStatus.Partial : ResourceCollectionStatus.Complete) :
                    missingResource ? ResourceCollectionStatus.Unavailable : ResourceCollectionStatus.InventoryFull;
                var receipt = new ResourceCollectionReceipt(status,
                    status == ResourceCollectionStatus.Complete ? "Đã chuyển tài nguyên vào kho." :
                    status == ResourceCollectionStatus.Partial ? "Đã thu một phần; hàng còn lại giữ trong buffer." :
                    status == ResourceCollectionStatus.InventoryFull ? "Kho đã đầy; buffer được giữ nguyên." :
                    "Thiếu ResourceData; hàng chưa chuyển được vẫn giữ trong buffer.", items);
                if (transfers.Count == 0) return receipt;

                var deposits = new List<ResourceInventory.Deposit>();
                foreach (var pair in totals) deposits.Add(new ResourceInventory.Deposit(resources[pair.Key], pair.Value));
                // No callbacks between stock/buffer mutations. Inventory publishes only after the complete batch.
                if (!inventory.TryAddAll(deposits, () =>
                {
                    foreach (var transfer in transfers) transfer.buffer.amount -= transfer.amount;
                    // fractionalCarry belongs to future production and is never collected as a whole unit.
                })) return Failure(ResourceCollectionStatus.Unavailable, "Sức chứa kho đã thay đổi; chưa chuyển hàng.");
                return receipt;
            }
            finally { collecting = false; }
        }

        internal static ResourceCollectionReceipt Failure(ResourceCollectionStatus status, string message) =>
            new ResourceCollectionReceipt(status, message);
    }
}

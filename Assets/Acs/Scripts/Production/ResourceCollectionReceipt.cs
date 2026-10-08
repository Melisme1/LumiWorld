using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    public enum ResourceCollectionStatus { Complete, Partial, InventoryFull, Empty, Unavailable, Busy }

    // Immutable values captured at commit time; UI must not infer receipts from live buffers.
    public sealed class ResourceCollectionItem
    {
        public string HabitatId { get; }
        public string ResourceId { get; }
        public string ResourceName { get; }
        public long Requested { get; }
        public int Transferred { get; }
        public long Remaining { get; }
        public string Reason { get; }

        internal ResourceCollectionItem(string habitatId, string resourceId, string resourceName,
            long requested, int transferred, string reason)
        {
            HabitatId = habitatId; ResourceId = resourceId; ResourceName = resourceName;
            Requested = requested; Transferred = transferred; Remaining = requested - transferred; Reason = reason;
        }
    }

    public sealed class ResourceCollectionReceipt
    {
        public string ActionId { get; }
        public ResourceCollectionStatus Status { get; }
        public string Message { get; }
        public IReadOnlyList<ResourceCollectionItem> Items { get; }
        public long TotalTransferred { get; }
        public long TotalRemaining { get; }

        internal ResourceCollectionReceipt(ResourceCollectionStatus status, string message,
            List<ResourceCollectionItem> items = null)
        {
            ActionId = Guid.NewGuid().ToString("N"); Status = status; Message = message;
            var copy = items != null ? new List<ResourceCollectionItem>(items) : new List<ResourceCollectionItem>();
            Items = copy.AsReadOnly();
            foreach (var item in copy)
            {
                TotalTransferred = SaturatingAdd(TotalTransferred, item.Transferred);
                TotalRemaining = SaturatingAdd(TotalRemaining, item.Remaining);
            }
        }

        private static long SaturatingAdd(long a, long b) => b > long.MaxValue - a ? long.MaxValue : a + b;
    }
}

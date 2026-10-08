using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    public enum CreatureQualityRank { RankI = 1, RankII = 2, RankIII = 3 }

    [Serializable]
    public sealed class ResourceProductionBuffer
    {
        public string resourceId;
        public ResourceTier tier;
        public long amount;
        public double fractionalCarry;
    }

    [Serializable]
    public sealed class ProductionSlotRate
    {
        public ResourceTier tier;
        public int index;
        public string resourceId;
        public double unitsPerMinute;
        public int bufferCap;
        public string reason;
    }

    [Serializable]
    public sealed class HabitatProductionState
    {
        public string habitatId;
        public string islandId;
        public string natureCardId;
        public List<string> memberIds = new List<string>();
        public bool isRecovery;
        public bool hasSettlementTime;
        public double lastSettledUtc;
        public List<ResourceProductionBuffer> buffers = new List<ResourceProductionBuffer>();
        public List<ProductionSlotRate> rates = new List<ProductionSlotRate>();

        public ResourceProductionBuffer FindBuffer(string resourceId) =>
            buffers.Find(b => b.resourceId == resourceId);

        public ProductionSlotRate FindRate(ResourceTier tier, int index) =>
            rates.Find(r => r.tier == tier && r.index == index);
    }
}

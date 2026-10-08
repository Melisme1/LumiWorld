using System;
using UnityEngine;

namespace LumiWorld.Acs
{
    [Serializable]
    public sealed class ProductionRuntimeSettings
    {
        [Tooltip("Tắt để dừng tích lũy mới; hàng đã sinh vẫn được giữ.")]
        public bool productionEnabled = true;
        [Min(0)] public float tier1UnitsPerInterval = 20f;
        [Min(0)] public float tier1IntervalSeconds = 10f;
        [Min(0)] public float tier2UnitsPerInterval = 5f;
        [Min(0)] public float tier2IntervalSeconds = 30f;
        [Min(0)] public float rankIContribution = 1f;
        [Min(0)] public float rankIIContribution = 1.5f;
        [Min(0)] public float rankIIIContribution = 2f;
        public ContributionSharingPolicy sharingPolicy = ContributionSharingPolicy.EqualAcrossEligibleSlots;
        public ComboContributionPolicy comboPolicy = ComboContributionPolicy.MinimumRequiredSpeciesContribution;
        [Min(1)] public int tier1BufferCap = 100;
        [Min(1)] public int tier2BufferCap = 50;

        public double Tier1UnitsPerMinute => 60d * tier1UnitsPerInterval / tier1IntervalSeconds;
        public double Tier2UnitsPerMinute => 60d * tier2UnitsPerInterval / tier2IntervalSeconds;

        public double GetContribution(CreatureQualityRank rank)
        {
            switch (rank)
            {
                case CreatureQualityRank.RankI: return rankIContribution;
                case CreatureQualityRank.RankII: return rankIIContribution;
                case CreatureQualityRank.RankIII: return rankIIIContribution;
                default: return 0d;
            }
        }

        public bool TryValidate(out string reason)
        {
            if (!productionEnabled) { reason = "Production đang tắt trong Hierarchy."; return false; }
            if (!PositiveFinite(tier1UnitsPerInterval) || !PositiveFinite(tier1IntervalSeconds) ||
                !PositiveFinite(tier2UnitsPerInterval) || !PositiveFinite(tier2IntervalSeconds))
            { reason = "Số lượng và chu kỳ T1/T2 phải lớn hơn 0 và hữu hạn."; return false; }
            if (!PositiveFinite(rankIContribution) || !PositiveFinite(rankIIContribution) || !PositiveFinite(rankIIIContribution))
            { reason = "Contribution Rank I–III phải lớn hơn 0 và hữu hạn."; return false; }
            if (sharingPolicy != ContributionSharingPolicy.EqualAcrossEligibleSlots ||
                comboPolicy != ComboContributionPolicy.MinimumRequiredSpeciesContribution)
            { reason = "Chọn policy chia đều slot hợp lệ và combo lấy contribution thấp nhất."; return false; }
            if (tier1BufferCap <= 0 || tier2BufferCap <= 0)
            { reason = "Buffer cap T1/T2 phải lớn hơn 0."; return false; }
            reason = "Production đang bật; số liệu lấy từ Settings trong Hierarchy.";
            return true;
        }

        private static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

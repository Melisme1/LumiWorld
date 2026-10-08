using UnityEngine;

namespace LumiWorld.Acs
{
    public enum ContributionSharingPolicy { Unspecified = 0, EqualAcrossEligibleSlots = 1 }
    public enum ComboContributionPolicy { Unspecified = 0, MinimumRequiredSpeciesContribution = 1 }

    [CreateAssetMenu(fileName = "ProductionBalance", menuName = "LumiWorld/Resources/Production Balance Config")]
    public sealed class ProductionBalanceConfig : ScriptableObject
    {
        [Tooltip("Để tắt ở bước 4. Chỉ bật sau khi rate/cap/sharing được chủ dự án chốt.")]
        public bool approvedForProduction;
        [Min(0)] public float tier1UnitsPerMinute;
        [Min(0)] public float tier2UnitsPerMinute;
        [Min(0)] public float rankIContribution;
        [Min(0)] public float rankIIContribution;
        [Min(0)] public float rankIIIContribution;
        public ContributionSharingPolicy sharingPolicy;
        public ComboContributionPolicy comboPolicy;
        [Min(0)] public int tier1BufferCap;
        [Min(0)] public int tier2BufferCap;
        [Tooltip("0 = không tính offline. Chưa duyệt ở bước 4.")]
        [Min(0)] public float maxOfflineHours;

        public bool TryValidateForProduction(out string reason)
        {
            if (!approvedForProduction) { reason = "Balance chưa được duyệt."; return false; }
            if (!PositiveFinite(tier1UnitsPerMinute) || !PositiveFinite(tier2UnitsPerMinute))
            { reason = "Rate T1/T2 phải lớn hơn 0 và hữu hạn."; return false; }
            if (!PositiveFinite(rankIContribution) || !PositiveFinite(rankIIContribution) || !PositiveFinite(rankIIIContribution))
            { reason = "Contribution Rank I–III phải lớn hơn 0 và hữu hạn."; return false; }
            if (sharingPolicy != ContributionSharingPolicy.EqualAcrossEligibleSlots ||
                comboPolicy != ComboContributionPolicy.MinimumRequiredSpeciesContribution)
            { reason = "Chưa chọn policy sharing/combo được hỗ trợ."; return false; }
            if (tier1BufferCap <= 0 || tier2BufferCap <= 0)
            { reason = "Buffer cap T1/T2 phải lớn hơn 0."; return false; }
            if (float.IsNaN(maxOfflineHours) || float.IsInfinity(maxOfflineHours) || maxOfflineHours < 0)
            { reason = "Offline cap không được âm hoặc vô hạn."; return false; }
            reason = string.Empty;
            return true;
        }

        private static bool PositiveFinite(float value)
        {
            return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

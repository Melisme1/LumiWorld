using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Cấu hình trọng số cấp sao của Thú Đầu Đàn (Alpha Leader) theo từng Mốc Tiến Trình (Milestone)
/// </summary>
[Serializable]
public class MilestoneRankWeight
{
    [Tooltip("Mốc Milestone tối thiểu để áp dụng cấu hình này (0 = tân thủ, 1 = sau mốc đầu tiên, v.v.)")]
    public int minMilestoneIndex = 0;

    [Range(0, 100)] public float weight1Star = 70f;
    [Range(0, 100)] public float weight2Star = 25f;
    [Range(0, 100)] public float weight3Star = 5f;
    [Range(0, 100)] public float weight4Star = 0f;
    [Range(0, 100)] public float weight5Star = 0f;
}

/// <summary>
/// Ánh xạ một loại Vùng Đất / Biome sang danh sách các loài sinh vật tương thích có thể xuất hiện
/// </summary>
[Serializable]
public class BiomeSpeciesMapping
{
    [Tooltip("Lá bài Địa hình (ví dụ: Leafwood, Bloomfield, Windheath)")]
    public CardData terrainCard;

    [Tooltip("Danh sách các Thẻ Linh Thú (CreatureCard) tương thích có thể xuất hiện ở địa hình này")]
    public List<CardData> compatibleCreatures = new List<CardData>();
}

/// <summary>
/// Cấu hình dữ liệu tập trung cho Hệ thống Thú Hoang Quậy Phá (Wild Pack Incident System)
/// </summary>
[CreateAssetMenu(fileName = "WildIncidentConfig", menuName = "LumiWorld/Ecosystem/Wild Incident Config")]
public class WildIncidentConfig : ScriptableObject
{
    [Header("1. Tiến trình & Kích hoạt (Progression Gatekeeper)")]
    [Tooltip("Mốc Milestone tối thiểu để tính năng thú hoang bắt đầu hoạt động (0 = tự động kích hoạt khi có Cụm 3 ô)")]
    public int requiredUnlockMilestone = 0;

    [Tooltip("Thời gian chờ tối thiểu (giây) giữa các đợt thú hoang (Mặc định: 180s = 3 phút)")]
    public float minIncidentIntervalSeconds = 180f;

    [Tooltip("Thời gian chờ tối đa (giây) giữa các đợt thú hoang (Mặc định: 300s = 5 phút)")]
    public float maxIncidentIntervalSeconds = 300f;

    [Tooltip("Chu kỳ danh nghĩa (dùng cho hiển thị hoặc fallback)")]
    public float incidentIntervalSeconds = 240f;

    [Tooltip("Số lượng sự cố đàn thú tối đa có thể đồng thời hoạt động trên bản đồ")]
    public int maxConcurrentIncidents = 1;

    /// <summary>
    /// Lấy khoảng thời gian chu kỳ ngẫu nhiên cân bằng: không quá nhanh (>= 3 phút) cũng không quá lâu (<= 5 phút).
    /// </summary>
    public float GetRandomInterval()
    {
        float min = minIncidentIntervalSeconds > 0 ? minIncidentIntervalSeconds : 180f;
        float max = maxIncidentIntervalSeconds >= min ? maxIncidentIntervalSeconds : min + 60f;
        return UnityEngine.Random.Range(min, max);
    }

    [Header("2. Quy tắc Cụm Biome 3 - 6 - 9 (Cluster Eligibility)")]
    [Tooltip("Kích thước cụm tối thiểu (số ô hex) để một cụm có thể trở thành Cụm Đầu Não (mặc định mốc 3 ô)")]
    public int minClusterSizeToHost = 3;

    [Header("3. Ảnh hưởng xấu (Obstacle Debuffs)")]
    [Tooltip("Hệ số tốc độ chu kỳ sản xuất cho Cụm Vệ Tinh bị Minion quấy phá (0.7f = giảm 30% tốc độ)")]
    [Range(0.1f, 1.0f)]
    public float satelliteSpeedDebuff = 0.7f;

    [Header("4. Bảng trọng số Rank theo Tiến trình (Progression Gating)")]
    public List<MilestoneRankWeight> milestoneRankWeights = new List<MilestoneRankWeight>
    {
        // Milestone 0 (Mới chơi): 100% ra 1★
        new MilestoneRankWeight { minMilestoneIndex = 0, weight1Star = 100f, weight2Star = 0f, weight3Star = 0f, weight4Star = 0f, weight5Star = 0f },
        // Milestone 1: 75% ra 1★, 25% ra 2★
        new MilestoneRankWeight { minMilestoneIndex = 1, weight1Star = 75f, weight2Star = 25f, weight3Star = 0f, weight4Star = 0f, weight5Star = 0f },
        // Milestone 2: 50% ra 1★, 35% ra 2★, 15% ra 3★
        new MilestoneRankWeight { minMilestoneIndex = 2, weight1Star = 50f, weight2Star = 35f, weight3Star = 15f, weight4Star = 0f, weight5Star = 0f },
        // Milestone 3+: 20% ra 1★, 40% ra 2★, 30% ra 3★, 10% ra 4★
        new MilestoneRankWeight { minMilestoneIndex = 3, weight1Star = 20f, weight2Star = 40f, weight3Star = 30f, weight4Star = 10f, weight5Star = 0f }
    };

    [Header("5. Bể loài tương thích theo từng Biome (Cluster-First Mapping)")]
    public List<BiomeSpeciesMapping> biomeSpeciesMappings = new List<BiomeSpeciesMapping>();

    /// <summary>
    /// Tính toán Rank của Thú Đầu Đàn (Alpha Leader) dựa trên Tiến trình người chơi
    /// </summary>
    public AnimalRank RollAlphaRank(int playerMilestoneIndex)
    {
        MilestoneRankWeight selectedWeight = null;
        // Tìm cấu hình phù hợp với milestone cao nhất mà người chơi đã đạt được
        foreach (var w in milestoneRankWeights)
        {
            if (w != null && playerMilestoneIndex >= w.minMilestoneIndex)
            {
                if (selectedWeight == null || w.minMilestoneIndex > selectedWeight.minMilestoneIndex)
                {
                    selectedWeight = w;
                }
            }
        }

        if (selectedWeight == null) return AnimalRank.Common;

        float totalWeight = selectedWeight.weight1Star + selectedWeight.weight2Star +
                            selectedWeight.weight3Star + selectedWeight.weight4Star + selectedWeight.weight5Star;
        if (totalWeight <= 0f) return AnimalRank.Common;

        float roll = Random.Range(0f, totalWeight);
        if (roll < selectedWeight.weight1Star) return AnimalRank.Common;
        roll -= selectedWeight.weight1Star;

        if (roll < selectedWeight.weight2Star) return AnimalRank.Uncommon;
        roll -= selectedWeight.weight2Star;

        if (roll < selectedWeight.weight3Star) return AnimalRank.Rare;
        roll -= selectedWeight.weight3Star;

        if (roll < selectedWeight.weight4Star) return AnimalRank.Epic;
        return AnimalRank.Legendary;
    }

    /// <summary>
    /// Lấy ngẫu nhiên Thẻ Linh Thú tương thích với Vùng Đất / Biome đã chọn (có fallback an toàn)
    /// </summary>
    public CardData GetCompatibleCreature(CardData terrainCard = null, string biomeKeyword = null)
    {
        if (biomeSpeciesMappings == null || biomeSpeciesMappings.Count == 0) return null;

        // 1. Khớp chính xác theo CardData
        if (terrainCard != null)
        {
            foreach (var mapping in biomeSpeciesMappings)
            {
                if (mapping != null && mapping.terrainCard == terrainCard && mapping.compatibleCreatures != null && mapping.compatibleCreatures.Count > 0)
                {
                    return mapping.compatibleCreatures[Random.Range(0, mapping.compatibleCreatures.Count)];
                }
            }
        }

        // 2. Khớp theo tên hoặc từ khóa Biome (ví dụ "Leafwood", "Bloomfield", "Windheath")
        if (!string.IsNullOrEmpty(biomeKeyword))
        {
            foreach (var mapping in biomeSpeciesMappings)
            {
                if (mapping != null && mapping.terrainCard != null && mapping.compatibleCreatures != null && mapping.compatibleCreatures.Count > 0)
                {
                    if (mapping.terrainCard.cardName.IndexOf(biomeKeyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        mapping.terrainCard.cardID.IndexOf(biomeKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return mapping.compatibleCreatures[Random.Range(0, mapping.compatibleCreatures.Count)];
                    }
                }
            }
        }

        // 3. Fallback an toàn: lấy ngẫu nhiên bất kỳ loài nào có trong config
        foreach (var mapping in biomeSpeciesMappings)
        {
            if (mapping != null && mapping.compatibleCreatures != null && mapping.compatibleCreatures.Count > 0)
            {
                return mapping.compatibleCreatures[Random.Range(0, mapping.compatibleCreatures.Count)];
            }
        }

        return null;
    }

    /// <summary>
    /// Tính toán số lượng thú con (Minions) tản sang các cụm lân cận dựa trên mốc 3 - 6 - 9
    /// </summary>
    public int CalculateMinionCount(int hostClusterHexCount)
    {
        if (hostClusterHexCount < 3) return 0;

        // Cụm 3 - 5 ô: 1 thú con
        if (hostClusterHexCount < 6) return 1;

        // Cụm 6 - 8 ô: Random 1 đến 2 thú con
        if (hostClusterHexCount < 9) return Random.Range(1, 3);

        // Cụm 9+ ô: Random 2 đến 3 thú con
        return Random.Range(2, 4);
    }
}

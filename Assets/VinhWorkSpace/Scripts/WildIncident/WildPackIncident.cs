using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đại diện thống nhất cho một Cụm Biome / Habitat trên bản đồ:
/// Hỗ trợ liền mạch cả HabitatRuntimeManager (GDD Acs), BiomeHarvestCluster (VinhWorkSpace)
/// hoặc cụm phân tích từ PlacedCard.
/// </summary>
public class IncidentClusterTarget
{
    public string ClusterId { get; set; } = "";
    public string BiomeName { get; set; } = "Biome";
    public int HexCount { get; set; } = 3;
    public Vector3 CenterPosition { get; set; } = Vector3.zero;
    public CardData TerrainCard { get; set; }
    public string NatureKey { get; set; } = "";
    public bool IsDisrupted { get; set; } = false;

    public Action<bool> SetDisruptedAction { get; set; }
    public Action<float> SetSpeedMultiplierAction { get; set; }

    public void SetDisrupted(bool disrupted)
    {
        IsDisrupted = disrupted;
        SetDisruptedAction?.Invoke(disrupted);
    }

    public void SetSpeedMultiplier(float mult)
    {
        SetSpeedMultiplierAction?.Invoke(mult);
    }
}

/// <summary>
/// Đại diện cho một sự cố Đàn Thú Hoang đang diễn ra trong thế giới:
/// - Thú Đầu Đàn (Alpha Leader) chiếm giữ Cụm Đầu Não (Host Cluster).
/// - Các Thú Con (Minions) phân tán sang quấy phá các Cụm Vệ Tinh (Satellite Clusters) lân cận.
/// </summary>
public class WildPackIncident
{
    public string IncidentID { get; private set; }
    public IncidentClusterTarget HostCluster { get; private set; }
    public CardData AlphaCreatureCard { get; private set; }
    public AnimalRank AlphaRank { get; private set; }
    public GameObject AlphaObject { get; private set; }

    public List<IncidentClusterTarget> SatelliteClusters { get; private set; } = new List<IncidentClusterTarget>();
    public List<GameObject> MinionObjects { get; private set; } = new List<GameObject>();

    public bool IsActive { get; private set; } = true;

    public WildPackIncident(
        string id,
        IncidentClusterTarget host,
        CardData alphaCard,
        AnimalRank rank,
        GameObject alphaObj,
        List<IncidentClusterTarget> satellites,
        List<GameObject> minions,
        float speedDebuff)
    {
        this.IncidentID = id;
        this.HostCluster = host;
        this.AlphaCreatureCard = alphaCard;
        this.AlphaRank = rank;
        this.AlphaObject = alphaObj;
        this.SatelliteClusters = satellites ?? new List<IncidentClusterTarget>();
        this.MinionObjects = minions ?? new List<GameObject>();

        // 1. Áp dụng phong tỏa Cụm Đầu Não
        if (HostCluster != null)
        {
            HostCluster.SetDisrupted(true);
        }

        // 2. Áp dụng hiệu ứng làm chậm cho các Cụm Vệ Tinh
        foreach (var sat in SatelliteClusters)
        {
            if (sat != null)
            {
                sat.SetSpeedMultiplier(speedDebuff);
            }
        }
    }

    /// <summary>
    /// Giải quyết sự cố: Giải phóng toàn bộ các cụm bị ảnh hưởng và dọn dẹp các mô hình thú hoang
    /// </summary>
    public void Resolve()
    {
        if (!IsActive) return;
        IsActive = false;

        // 1. Giải phóng Cụm Đầu Não
        if (HostCluster != null)
        {
            HostCluster.SetDisrupted(false);
        }

        // 2. Giải phóng các Cụm Vệ Tinh
        foreach (var sat in SatelliteClusters)
        {
            if (sat != null)
            {
                sat.SetSpeedMultiplier(1.0f);
            }
        }

        // 3. Dọn dẹp GameObject của Alpha
        if (AlphaObject != null)
        {
            UnityEngine.Object.Destroy(AlphaObject);
        }

        // 4. Dọn dẹp GameObjects của Minions
        foreach (var minion in MinionObjects)
        {
            if (minion != null)
            {
                UnityEngine.Object.Destroy(minion);
            }
        }

        MinionObjects.Clear();
        SatelliteClusters.Clear();
    }
}

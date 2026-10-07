using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đại diện cho một Cụm Vùng Đất (Biome Territory) gồm các ô lục giác liền kề cùng loại (ví dụ Cụm Rừng Leafwood).
/// Quản lý chu kỳ sản xuất chung của cả vùng, tính toán tổng sản lượng do các thú đóng góp,
/// và hiển thị duy nhất 1 Vòng tròn / Bong bóng thu hoạch đại diện cho cả vùng.
/// </summary>
public class BiomeHarvestCluster
{
    public string ClusterID { get; private set; }
    public CardData TerrainCard { get; private set; }
    public ResourceData ResourceData { get; private set; }

    public List<PlacedCard> Tiles { get; private set; } = new List<PlacedCard>();
    public HashSet<HexCoordinates> HexCoords { get; private set; } = new HashSet<HexCoordinates>();
    public List<AnimalIndividual> Animals { get; private set; } = new List<AnimalIndividual>();

    public float Timer { get; private set; } = 0f;
    public float CycleDuration { get; private set; } = 30f;
    public bool IsReadyToHarvest { get; private set; } = false;
    public int PendingHarvestAmount { get; private set; } = 0;

    public BiomeHarvestIndicator Indicator { get; private set; }
    public Vector3 CenterPosition { get; private set; }

    public event Action<BiomeHarvestCluster, ResourceData, int> OnHarvestCollected;

    public BiomeHarvestCluster(string id, CardData terrain, ResourceData resource, float cycleTime)
    {
        this.ClusterID = id;
        this.TerrainCard = terrain;
        this.ResourceData = resource;
        this.CycleDuration = Mathf.Max(5f, cycleTime);
    }

    /// <summary>
    /// Cập nhật danh sách các ô thuộc cụm này và tính lại tâm cụm
    /// </summary>
    public void SetTiles(List<PlacedCard> newTiles)
    {
        Tiles.Clear();
        HexCoords.Clear();

        Vector3 sumPos = Vector3.zero;
        int validCount = 0;
        float maxSurfaceY = 0f;

        foreach (var tile in newTiles)
        {
            if (tile == null) continue;
            Tiles.Add(tile);
            HexCoords.Add(tile.placedHex);

            sumPos += tile.transform.position;
            validCount++;

            // Lấy độ cao mặt phẳng chuẩn của khối lục giác này
            GameObject rootTile = tile.transform.parent != null ? tile.transform.parent.gameObject : tile.gameObject;
            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(rootTile);
            if (topY > maxSurfaceY)
            {
                maxSurfaceY = topY;
            }
        }

        if (validCount > 0)
        {
            CenterPosition = new Vector3(sumPos.x / validCount, maxSurfaceY, sumPos.z / validCount);
        }

        UpdateIndicatorPosition();
    }

    /// <summary>
    /// Gán hoặc tạo UI Indicator cho cụm này
    /// </summary>
    public void AttachIndicator(BiomeHarvestIndicator ind)
    {
        this.Indicator = ind;
        if (Indicator != null)
        {
            Indicator.Initialize(this, ResourceData);
            UpdateIndicatorPosition();
        }
    }

    public void UpdateIndicatorPosition()
    {
        if (Indicator != null)
        {
            Indicator.UpdatePosition(CenterPosition);
        }
    }

    /// <summary>
    /// Quét lại các con thú đang sinh sống trong phạm vi các ô thuộc cụm này
    /// </summary>
    public void RefreshAnimals(List<AnimalIndividual> allAnimals)
    {
        Animals.Clear();
        if (allAnimals == null) return;

        foreach (var animal in allAnimals)
        {
            if (animal == null) continue;

            // Kiểm tra xem con thú có đang thuộc ô nào trong cụm này không
            HexCoordinates animalHex = HexMetrics.WorldToHex(animal.transform.position);
            PlacedCard animalPlaced = animal.GetComponent<PlacedCard>();
            if (animalPlaced != null)
            {
                animalHex = animalPlaced.placedHex;
            }

            if (HexCoords.Contains(animalHex))
            {
                Animals.Add(animal);
            }
        }
    }

    /// <summary>
    /// Vòng lặp đếm giờ chu kỳ sản xuất của Cụm Biome
    /// </summary>
    public void Update(float deltaTime)
    {
        // 1. Nếu Cụm chưa có con thú nào: Không sản xuất (đất tĩnh, ẩn hoặc chờ có thú)
        if (Animals.Count == 0)
        {
            if (Indicator != null && Indicator.gameObject.activeSelf)
            {
                Indicator.gameObject.SetActive(false);
            }
            return;
        }

        // 2. Có thú làm việc: Bật Indicator
        if (Indicator != null && !Indicator.gameObject.activeSelf)
        {
            Indicator.gameObject.SetActive(true);
        }

        // 3. Nếu đã đầy 100% (chờ thu hoạch): Dừng timer, không cho tràn tài nguyên!
        if (IsReadyToHarvest)
        {
            return;
        }

        // 4. Đang trong chu kỳ sản xuất
        Timer += deltaTime;
        float progress = Mathf.Clamp01(Timer / CycleDuration);

        // Tính sản lượng dự kiến
        int expectedYield = CalculateCurrentYieldPreview();
        float remainingSeconds = Mathf.Max(0f, CycleDuration - Timer);

        if (Indicator != null)
        {
            Indicator.SetProgress(progress, Animals.Count, expectedYield, remainingSeconds);
        }

        // 5. Chu kỳ hoàn tất (100%): Kích hoạt trạng thái Bong bóng sẵn sàng thu hoạch
        if (Timer >= CycleDuration)
        {
            Timer = CycleDuration;
            IsReadyToHarvest = true;

            // Tính toán sản lượng mẻ thực tế (áp dụng trait may mắn, cấp sao, bonus đất)
            PendingHarvestAmount = FinalizeHarvestYield();

            if (Indicator != null)
            {
                Indicator.SetHarvestReady(PendingHarvestAmount);
            }

            Debug.Log($"<color=#38BDF8>🔔 [LumiWorld Cụm Biome] Cụm <b>{TerrainCard.cardName}</b> ({Tiles.Count} ô, {Animals.Count} thú) đã sản xuất xong: +{PendingHarvestAmount} {ResourceData?.resourceName}! Chờ người chơi thu hoạch.</color>");
        }
    }

    /// <summary>
    /// Tính sản lượng dự kiến hiển thị trên thanh tiến độ
    /// </summary>
    private int CalculateCurrentYieldPreview()
    {
        int total = 0;
        foreach (var a in Animals)
        {
            if (a != null) total += a.ActualResourceAmount;
        }
        if (TerrainCard != null && TerrainCard.landResourceBonus > 0)
        {
            total += TerrainCard.landResourceBonus;
        }
        return Mathf.Max(1, total);
    }

    /// <summary>
    /// Tính sản lượng thực tế khi kết thúc mẻ (kích hoạt các trait may mắn của từng thú)
    /// </summary>
    private int FinalizeHarvestYield()
    {
        int total = 0;
        foreach (var a in Animals)
        {
            if (a != null)
            {
                total += a.CalculateHarvestYield();
            }
        }
        if (TerrainCard != null && TerrainCard.landResourceBonus > 0)
        {
            total += TerrainCard.landResourceBonus;
        }
        return Mathf.Max(1, total);
    }

    /// <summary>
    /// Thu hoạch mẻ tài nguyên của Cụm Biome khi người chơi click
    /// </summary>
    public int CollectHarvest()
    {
        if (!IsReadyToHarvest) return 0;

        int amount = PendingHarvestAmount;

        // Sinh popup chữ nổi bay lên tại tâm cụm
        ResourceHarvestPopup.Spawn(CenterPosition + Vector3.up * 0.8f, ResourceData, amount);

        Debug.Log($"<color=green>✨ [THU HOẠCH THÀNH CÔNG] Bạn vừa gặt hái +{amount} {ResourceData?.GetColoredName()} từ cụm {TerrainCard.cardName}!</color>");

        // Reset chu kỳ mới
        Timer = 0f;
        IsReadyToHarvest = false;
        PendingHarvestAmount = 0;

        OnHarvestCollected?.Invoke(this, ResourceData, amount);

        return amount;
    }

    public void Destroy()
    {
        if (Indicator != null && Indicator.gameObject != null)
        {
            UnityEngine.Object.Destroy(Indicator.gameObject);
        }
    }
}

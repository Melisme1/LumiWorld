using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chuyên trách việc kiểm tra logic hợp lệ khi đặt bài lên ô lục giác
/// (Ô đã bị chiếm dụng chưa, loại địa hình có thỏa mãn điều kiện của lá bài không, quy mô cụm Biome).
/// </summary>
public class HexPlacementValidator : MonoBehaviour
{
    [Header("Biome Cluster Limit (Giới hạn quy mô Biome)")]
    [Tooltip("Bật/Tắt giới hạn số ô lục giác tối đa trong một Cụm Biome")]
    [SerializeField] private bool enableBiomeSizeLimit = true;

    [Tooltip("Số ô lục giác tối đa cho phép trong một Cụm Biome (mặc định = 9). Nếu người chơi ghép thêm ô làm cụm vượt quá số này sẽ bị chặn.")]
    [SerializeField] private int maxTilesPerBiome = 9;

    private string lastValidationError = "";

    public bool EnableBiomeSizeLimit
    {
        get => enableBiomeSizeLimit;
        set => enableBiomeSizeLimit = value;
    }

    public int MaxTilesPerBiome
    {
        get => maxTilesPerBiome;
        set => maxTilesPerBiome = value;
    }

    public string LastValidationError => lastValidationError;
    /// <summary>
    /// Kiểm tra ô có thỏa mãn toàn bộ điều kiện để đặt lá bài hiện tại hay không
    /// </summary>
    public bool ValidatePlacement(
        HexCoordinates coords, 
        HexWorldGenerator worldGen, 
        CardData cardData, 
        out GameObject tileObj)
    {
        tileObj = null;
        lastValidationError = string.Empty;
        if (worldGen == null || worldGen.MapTiles == null) return false;

        if (!worldGen.MapTiles.TryGetValue(coords, out tileObj) || tileObj == null)
        {
            return false;
        }

        bool useHabitatRules = LumiWorld.Acs.HabitatRuntimeManager.TryGetActive(out var habitatManager);

        // 0. Kiểm tra thẻ Mưa (Rain Card) và trạng thái thiên tai / cháy rụi (isBurned)
        var disasterState = tileObj != null ? tileObj.GetComponent<LumiWorld.Acs.TileDisasterState>() : null;
        bool hasDisasterOrBurned = disasterState != null && (disasterState.HasAnyDisaster || disasterState.isBurned);
        bool hasPendingFire = LumiWorld.Acs.DisasterRuntimeManager.TryGetActive(out var disasterManager) &&
                              disasterManager.IsTilePendingFire(tileObj);
        bool isRainCard = cardData != null && (cardData.cardID == "Rain" || string.Equals(cardData.cardName, "Rain", System.StringComparison.OrdinalIgnoreCase));

        if (isRainCard)
        {
            if (disasterState != null && disasterState.Has(LumiWorld.Acs.DisasterKind.Cyclone))
            {
                lastValidationError = "Cannot use Rain on a tile affected by a Cyclone";
                return false;
            }

            // Nếu ô đang có thiên tai (cháy rừng, lốc xoáy, hạn hán...) hoặc đang bị cháy rụi (isBurned)
            // -> LUÔN CHO PHÉP thả thẻ Mưa để dập lửa / dọn tàn tích phục hồi đất!
            if (hasDisasterOrBurned || hasPendingFire)
            {
                lastValidationError = "";
                return true;
            }

            // Nếu không có thiên tai, kiểm tra xem ô có phải là ô đất khô Arid cần cải tạo hay không
            if (!cardData.IsTileTransformCard() || !cardData.IsTileAllowed(tileObj, worldGen))
            {
                lastValidationError = "This tile has no disaster and is already lush, no need for rain";
                return false;
            }

            // Ô đất khô đã được biến đổi trước đó thì không cho đặt thêm
            PlacedCard[] transformCards = tileObj.GetComponentsInChildren<PlacedCard>();
            foreach (var pc in transformCards)
            {
                if (pc != null && pc.cardData != null && pc.cardData.IsTileTransformCard())
                {
                    lastValidationError = "This tile has already been transformed";
                    return false;
                }
            }

            lastValidationError = "";
            return true;
        }

        // Nếu ô đang bị cháy tro tàn (isBurned), CHẶN các thẻ khác (Terrain, Creature, Building)
        // và yêu cầu người chơi phải dùng thẻ Mưa để dọn tàn tích trước!
        if (disasterState != null && disasterState.isBurned)
        {
            lastValidationError = "This tile is burned to ashes. Use a Rain card to clear ruins first!";
            return false;
        }

        // Nếu ô đang có thiên tai hoạt động (cháy rừng, lốc xoáy...), chặn đặt thẻ thường
        if (disasterState != null && disasterState.HasAnyDisaster)
        {
            lastValidationError = "A disaster is currently active on this tile, cannot place card!";
            return false;
        }

        // 0.8 Kiểm tra thẻ Sông Suối (River Card)
        if (cardData != null && cardData.IsRiverCard())
        {
            // Ô đã có dòng suối từ trước -> Không cho đặt đè
            if (RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.HasRiver(coords))
            {
                lastValidationError = "Ô này đã có dòng suối";
                return false;
            }

            // Ô đang có thú sinh sống trực tiếp -> Không cho đặt trực tiếp lên thú
            if (HasPlacedCreature(tileObj) || (useHabitatRules && habitatManager.HasCreatureAtHome(coords)))
            {
                lastValidationError = "Ô này đang có thú sinh sống. Hãy đặt suối ở ô lân cận để tạo bến nước!";
                return false;
            }

            // BẮT BUỘC: Ô này phải ĐÃ ĐẶT thẻ Môi trường (Biome / Terrain card)
            if (!HasPlacedTerrain(tileObj))
            {
                lastValidationError = "Cần đặt thẻ Môi trường (Biome) trước khi khai thông dòng suối!";
                return false;
            }

            // Quy tắc đặt suối: Khởi nguồn từ đỉnh cao nhất & Nối tiếp dòng chảy dốc xuống (Elevation Origin & Downhill Rule)
            HexTileInfo tileInfo = tileObj.GetComponent<HexTileInfo>();
            var (targetTopY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);

            List<HexCoordinates> riverNeighbors = new List<HexCoordinates>();
            if (RiverNetworkManager.Instance != null)
            {
                for (int dir = 0; dir < 6; dir++)
                {
                    HexCoordinates n = coords.GetNeighbor(dir);
                    if (RiverNetworkManager.Instance.HasRiver(n))
                    {
                        riverNeighbors.Add(n);
                    }
                }
            }

            // TRƯỜNG HỢP 1: ĐẶT Ô THƯỢNG NGUỒN (Độc lập, chưa kề ô suối nào)
            if (riverNeighbors.Count == 0)
            {
                // Thượng nguồn BẮT BUỘC phải bắt đầu từ Tầng trung (Tier 1) hoặc Tầng cao (Tier 2): topY >= 1.35m
                // Không cho phép đặt nguồn ở vùng thung lũng trũng thấp nhất (Tier 0)
                if (targetTopY < 1.35f)
                {
                    lastValidationError = "Thượng nguồn suối phải bắt đầu từ Tầng trung (đồi) hoặc Tầng cao (núi) để có độ dốc dòng chảy!";
                    return false;
                }
            }
            // TRƯỜNG HỢP 2: NỐI TIẾP DÒNG SUỐI ĐÃ CÓ (Kề ít nhất 1 ô suối)
            else
            {
                // DỐC ĐỊA HÌNH: Nước chỉ chảy dốc xuống hoặc bằng phẳng, TUYỆT ĐỐI không thể chảy ngược lên dốc cao hơn!
                bool canFlowDownhill = false;
                foreach (var nCoords in riverNeighbors)
                {
                    if (worldGen.MapTiles.TryGetValue(nCoords, out GameObject nTileObj) && nTileObj != null)
                    {
                        var (nTopY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(nTileObj);
                        if (targetTopY <= nTopY + 0.15f)
                        {
                            canFlowDownhill = true;
                            break;
                        }
                    }
                }

                if (!canFlowDownhill)
                {
                    lastValidationError = "Nước không thể chảy ngược lên dốc cao hơn!";
                    return false;
                }
            }

            lastValidationError = "";
            return true;
        }

        // 1. Phân biệt theo loại thẻ:
        if (cardData != null && cardData.cardType == CardType.Creature)
        {
            // Thẻ thú không được đặt nếu ô này ĐÃ CÓ một con thú khác
            if (HasPlacedCreature(tileObj) || (useHabitatRules && habitatManager.HasCreatureAtHome(coords)))
            {
                lastValidationError = "Each home hex can only contain one creature.";
                return false;
            }

            // Thú bắt buộc phải có môi trường sống: Ô phải ĐÃ CÓ thẻ bài loại Terrain được đặt lên trước đó!
            // (Kể cả khi ô đã được tưới Rain thành Lush, người chơi vẫn BẮT BUỘC phải đặt thẻ Terrain lên trước rồi mới được đặt thú)
            if (!HasPlacedTerrain(tileObj))
            {
                lastValidationError = "Must place a Terrain card before placing a creature.";
                return false;
            }

            if (useHabitatRules)
            {
                if (!habitatManager.CanPlaceCreature(cardData, GetPlacedTerrainCard(tileObj), coords, out lastValidationError))
                    return false;
            }
            else
            {
                CardData terrainCard = GetPlacedTerrainCard(tileObj);
                var (clusterSize, creatureCount) = GetClusterCreatureStats(coords, terrainCard, worldGen);
                if (clusterSize < 3)
                {
                    lastValidationError = $"Biome cluster requires at least 3 Nature tiles to raise creatures (Currently: {clusterSize} tiles)";
                    return false;
                }

                int capacity = CalculateClusterCreatureCapacity(clusterSize);
                if (creatureCount >= capacity)
                {
                    lastValidationError = $"Biome cluster has reached max creature capacity ({creatureCount}/{capacity}). Expand the cluster (milestones: 3/6/9 tiles)";
                    return false;
                }
            }
        }
        else if (cardData != null && cardData.cardType == CardType.Terrain)
        {
            // Ô này đã có thẻ Terrain từ trước thì không được đè thêm thẻ Terrain khác
            if (HasPlacedTerrain(tileObj))
            {
                lastValidationError = "This tile already has terrain.";
                return false;
            }

            // Các thẻ Terrain tuân theo quy tắc kiểm tra chiếm ô
            if (IsTileOccupied(tileObj))
            {
                lastValidationError = "This tile is already occupied.";
                return false;
            }
        }
        else
        {
            // Các thẻ khác (Building...): Tuân theo quy tắc kiểm tra chiếm ô thông thường
            if (IsTileOccupied(tileObj))
            {
                lastValidationError = "This tile is already occupied.";
                return false;
            }
        }

        // Không cho phép đặt thêm thẻ biến đổi địa hình (như Rain) nếu ô này đã có thẻ biến đổi địa hình từ trước
        if (cardData != null && cardData.IsTileTransformCard())
        {
            PlacedCard[] placedCards = tileObj.GetComponentsInChildren<PlacedCard>();
            foreach (var pc in placedCards)
            {
                if (pc != null && pc.cardData != null && pc.cardData.IsTileTransformCard())
                {
                    lastValidationError = "This tile has already been transformed.";
                    return false;
                }
            }
        }

        // Mapped creature residency follows GDD affinities; physical prefab lists remain for other cards.
        if (cardData != null && !(useHabitatRules && cardData.cardType == CardType.Creature) && !cardData.IsTileAllowed(tileObj, worldGen))
        {
            lastValidationError = "Terrain is not suitable for this creature/card.";
            return false;
        }

        lastValidationError = "";
        return true;
    }

    /// <summary>
    /// Kiểm tra xem tọa độ ô lục giác đã có vật thể / công trình nào chiếm chỗ chưa
    /// </summary>
    public bool IsTileOccupied(HexCoordinates coords, HexWorldGenerator worldGen)
    {
        if (worldGen == null || worldGen.MapTiles == null) return false;
        if (worldGen.MapTiles.TryGetValue(coords, out GameObject tileObj))
        {
            return IsTileOccupied(tileObj);
        }
        return false;
    }

    /// <summary>
    /// Kiểm tra xem GameObject của ô lục giác đã có công trình, thẻ bài hoặc habitat chưa
    /// </summary>
    public bool IsTileOccupied(GameObject tileObj)
    {
        if (tileObj == null) return false;

        // 1. Kiểm tra cờ isOccupied trong HexTileInfo nếu có
        HexTileInfo tileInfo = tileObj.GetComponent<HexTileInfo>();
        if (tileInfo != null && tileInfo.isOccupied)
        {
            return true;
        }

        // 2. Kiểm tra xem tile hoặc các object con của tile đã gắn PlacedCard loại chiếm ô không
        PlacedCard[] placedCards = tileObj.GetComponentsInChildren<PlacedCard>();
        foreach (var pc in placedCards)
        {
            if (pc != null && (pc.cardData == null || pc.cardData.occupiesTile))
            {
                return true;
            }
        }

        // 3. Kiểm tra xem tile có chứa container Habitat_ không
        for (int i = 0; i < tileObj.transform.childCount; i++)
        {
            Transform child = tileObj.transform.GetChild(i);
            if (child != null && child.name.StartsWith("Habitat_"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra xem ô lục giác đã có sinh vật (Creature) nào sinh sống chưa
    /// </summary>
    public bool HasPlacedCreature(GameObject tileObj)
    {
        if (tileObj == null) return false;

        PlacedCard[] placedCards = tileObj.GetComponentsInChildren<PlacedCard>();
        foreach (var pc in placedCards)
        {
            if (pc != null && pc.CardType == CardType.Creature)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Kiểm tra xem ô lục giác đã có thẻ bài loại địa hình (Terrain) được đặt lên chưa.
    /// Bắt buộc ô phải có PlacedCard thuộc CardType.Terrain (hoặc container Habitat_ sinh ra từ thẻ Terrain).
    /// Việc chỉ mới dùng thẻ Rain làm đất tươi tốt (Lush) chưa đủ điều kiện, người chơi bắt buộc phải đặt thẻ Terrain lên trước!
    /// </summary>
    public bool HasPlacedTerrain(GameObject tileObj)
    {
        if (tileObj == null) return false;

        // 1. Kiểm tra PlacedCard loại Terrain trên ô
        PlacedCard[] placedCards = tileObj.GetComponentsInChildren<PlacedCard>();
        foreach (var pc in placedCards)
        {
            if (pc != null && pc.CardType == CardType.Terrain)
            {
                return true;
            }
        }

        // 2. Kiểm tra container Habitat_ đã sinh cây cối/cỏ hoa từ thẻ Terrain
        for (int i = 0; i < tileObj.transform.childCount; i++)
        {
            Transform child = tileObj.transform.GetChild(i);
            if (child != null && child.name.StartsWith("Habitat_"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Giữ tương thích ngược cho các lời gọi cũ
    /// </summary>
    public bool HasPlacedTerrainOrHabitat(GameObject tileObj)
    {
        return HasPlacedTerrain(tileObj);
    }

    /// <summary>
    /// Tính toán kích thước cụm Biome nếu người chơi đặt lá bài địa hình này vào tọa độ targetHex.
    /// Trả về tổng số ô của cụm Biome sau khi sáp nhập.
    /// </summary>
    public int CalculateProspectiveClusterSize(HexCoordinates targetHex, CardData terrainCard, HexWorldGenerator worldGen)
    {
        if (worldGen == null || worldGen.MapTiles == null || terrainCard == null) return 1;

        HashSet<HexCoordinates> prospectiveCluster = new HashSet<HexCoordinates>();
        Queue<HexCoordinates> queue = new Queue<HexCoordinates>();

        prospectiveCluster.Add(targetHex);

        // Quét 6 hướng láng giềng của ô đang muốn đặt
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates neighborHex = targetHex.GetNeighbor(dir);
            if (prospectiveCluster.Contains(neighborHex)) continue;

            if (worldGen.MapTiles.TryGetValue(neighborHex, out GameObject neighborTile) && neighborTile != null)
            {
                CardData neighborTerrain = GetPlacedTerrainCard(neighborTile);
                if (neighborTerrain != null && IsMatchingTerrainOrBiome(terrainCard, neighborTerrain))
                {
                    prospectiveCluster.Add(neighborHex);
                    queue.Enqueue(neighborHex);
                }
            }
        }

        // Lan truyền BFS qua tất cả các ô đã kết nối thuộc cùng cụm
        while (queue.Count > 0)
        {
            HexCoordinates current = queue.Dequeue();

            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates nextHex = current.GetNeighbor(dir);
                if (prospectiveCluster.Contains(nextHex)) continue;

                if (worldGen.MapTiles.TryGetValue(nextHex, out GameObject nextTile) && nextTile != null)
                {
                    CardData nextTerrain = GetPlacedTerrainCard(nextTile);
                    if (nextTerrain != null && IsMatchingTerrainOrBiome(terrainCard, nextTerrain))
                    {
                        prospectiveCluster.Add(nextHex);
                        queue.Enqueue(nextHex);
                    }
                }
            }
        }

        return prospectiveCluster.Count;
    }

    /// <summary>
    /// Lấy thẻ Terrain đã đặt trên ô lục giác (nếu có)
    /// </summary>
    public CardData GetPlacedTerrainCard(GameObject tileObj)
    {
        if (tileObj == null) return null;
        PlacedCard[] placedCards = tileObj.GetComponentsInChildren<PlacedCard>();
        foreach (var pc in placedCards)
        {
            if (pc != null && pc.cardData != null)
            {
                if (pc.CardType == CardType.Terrain || pc.cardData.HasHabitatProps())
                {
                    return pc.cardData;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Kiểm tra xem hai thẻ địa hình có thuộc cùng một Biome hay không
    /// </summary>
    public static bool IsMatchingTerrainOrBiome(CardData a, CardData b)
    {
        return HexBiomeClusterConnector.AreNatureCardsMatching(a, b);
    }

    /// <summary>
    /// Tính toán sức chứa thú tối đa của một cụm Biome theo các mốc 3, 6, 9 ô:
    /// - Dưới 3 ô: 0 thú (chưa đủ điều kiện hình thành sinh thái)
    /// - 3 đến 5 ô: 1 thú
    /// - 6 đến 8 ô: 2 thú
    /// - 9 ô trở lên: 3 thú
    /// </summary>
    public static int CalculateClusterCreatureCapacity(int hexCount)
    {
        if (hexCount < 3) return 0;
        return Mathf.Clamp(hexCount / 3, 1, 3);
    }

    /// <summary>
    /// Đếm số ô và số lượng thú hiện có trong cụm Biome chứa ô hex này
    /// </summary>
    public (int clusterSize, int creatureCount) GetClusterCreatureStats(HexCoordinates targetHex, CardData terrainCard, HexWorldGenerator worldGen)
    {
        if (worldGen == null || worldGen.MapTiles == null || terrainCard == null) return (0, 0);

        // 1. Thử lấy theo PlacedCard.clusterId nếu đã được phân cụm
        if (worldGen.MapTiles.TryGetValue(targetHex, out GameObject targetTile) && targetTile != null)
        {
            PlacedCard targetPlaced = HexBiomeClusterConnector.GetHabitatCardOnTile(targetTile);
            if (targetPlaced != null && targetPlaced.clusterId > 0)
            {
                int targetClusterId = targetPlaced.clusterId;
                int size = 0;
                int creatures = 0;
                foreach (var pair in worldGen.MapTiles)
                {
                    if (pair.Value == null) continue;
                    PlacedCard pc = HexBiomeClusterConnector.GetHabitatCardOnTile(pair.Value);
                    if (pc != null && pc.clusterId == targetClusterId)
                    {
                        size++;
                        if (HasPlacedCreature(pair.Value)) creatures++;
                    }
                }
                return (size, creatures);
            }
        }

        // 2. Fallback quét BFS theo địa hình liền kề
        HashSet<HexCoordinates> visited = new HashSet<HexCoordinates>();
        Queue<HexCoordinates> queue = new Queue<HexCoordinates>();
        visited.Add(targetHex);
        queue.Enqueue(targetHex);

        int creatureCount = 0;
        if (worldGen.MapTiles.TryGetValue(targetHex, out GameObject startTile) && startTile != null)
        {
            if (HasPlacedCreature(startTile)) creatureCount++;
        }

        while (queue.Count > 0)
        {
            HexCoordinates current = queue.Dequeue();
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates neighborHex = current.GetNeighbor(dir);
                if (visited.Contains(neighborHex)) continue;

                if (worldGen.MapTiles.TryGetValue(neighborHex, out GameObject neighborTile) && neighborTile != null)
                {
                    CardData neighborTerrain = GetPlacedTerrainCard(neighborTile);
                    if (neighborTerrain != null && IsMatchingTerrainOrBiome(terrainCard, neighborTerrain))
                    {
                        visited.Add(neighborHex);
                        queue.Enqueue(neighborHex);
                        if (HasPlacedCreature(neighborTile)) creatureCount++;
                    }
                }
            }
        }

        return (visited.Count, creatureCount);
    }
}

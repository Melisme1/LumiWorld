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

    [Tooltip("Số ô lục giác tối đa cho phép trong một Cụm Biome (mặc định = 6). Nếu người chơi ghép thêm ô làm cụm vượt quá số này sẽ bị chặn.")]
    [SerializeField] private int maxTilesPerBiome = 6;

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

        // 1. Phân biệt theo loại thẻ:
        if (cardData != null && cardData.cardType == CardType.Creature)
        {
            // Thẻ thú không được đặt nếu ô này ĐÃ CÓ một con thú khác
            if (HasPlacedCreature(tileObj) || (useHabitatRules && habitatManager.HasCreatureAtHome(coords)))
            {
                lastValidationError = "Mỗi home hex chỉ chứa một creature.";
                return false;
            }

            // Thú bắt buộc phải có môi trường sống: Ô phải ĐÃ CÓ thẻ bài loại Terrain được đặt lên trước đó!
            // (Kể cả khi ô đã được tưới Rain thành Lush, người chơi vẫn BẮT BUỘC phải đặt thẻ Terrain lên trước rồi mới được đặt thú)
            if (!HasPlacedTerrain(tileObj))
            {
                lastValidationError = "Cần đặt thẻ Địa hình (Terrain) trước khi thả thú";
                return false;
            }

            if (useHabitatRules && !habitatManager.CanPlaceCreature(cardData, GetPlacedTerrainCard(tileObj), coords, out lastValidationError))
                return false;
        }
        else if (cardData != null && cardData.cardType == CardType.Terrain)
        {
            // Ô này đã có thẻ Terrain từ trước thì không được đè thêm thẻ Terrain khác
            if (HasPlacedTerrain(tileObj))
            {
                lastValidationError = "Ô này đã được phủ địa hình";
                return false;
            }

            // Các thẻ Terrain tuân theo quy tắc kiểm tra chiếm ô
            if (IsTileOccupied(tileObj))
            {
                lastValidationError = "Ô này đã bị chiếm dụng";
                return false;
            }
        }
        else
        {
            // Các thẻ khác (Building...): Tuân theo quy tắc kiểm tra chiếm ô thông thường
            if (IsTileOccupied(tileObj))
            {
                lastValidationError = "Ô này đã bị chiếm dụng";
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
                    lastValidationError = "Ô này đã được biến đổi địa hình";
                    return false;
                }
            }
        }

        // Mapped creature residency follows GDD affinities; physical prefab lists remain for other cards.
        if (cardData != null && !(useHabitatRules && cardData.cardType == CardType.Creature) && !cardData.IsTileAllowed(tileObj, worldGen))
        {
            lastValidationError = "Địa hình không thích hợp với loài thú/thẻ bài này";
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
}

using UnityEngine;

/// <summary>
/// Chuyên trách việc kiểm tra logic hợp lệ khi đặt bài lên ô lục giác
/// (Ô đã bị chiếm dụng chưa, loại địa hình có thỏa mãn điều kiện của lá bài không).
/// </summary>
public class HexPlacementValidator : MonoBehaviour
{
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
        if (worldGen == null || worldGen.MapTiles == null) return false;

        if (!worldGen.MapTiles.TryGetValue(coords, out tileObj) || tileObj == null)
        {
            return false;
        }

<<<<<<< HEAD
        if (IsTileOccupied(tileObj))
        {
            return false;
=======
        // 1. Phân biệt theo loại thẻ:
        if (cardData != null && cardData.cardType == CardType.Creature)
        {
            // Thẻ thú không được đặt nếu ô này ĐÃ CÓ một con thú khác
            if (HasPlacedCreature(tileObj))
            {
                return false;
            }

            // Thú bắt buộc phải có môi trường sống: Ô phải ĐÃ CÓ thẻ bài loại Terrain được đặt lên trước đó!
            // (Kể cả khi ô đã được tưới Rain thành Lush, người chơi vẫn BẮT BUỘC phải đặt thẻ Terrain lên trước rồi mới được đặt thú)
            if (!HasPlacedTerrain(tileObj))
            {
                return false;
            }
        }
        else if (cardData != null && cardData.cardType == CardType.Terrain)
        {
            // Ô này đã có thẻ Terrain từ trước thì không được đè thêm thẻ Terrain khác
            if (HasPlacedTerrain(tileObj))
            {
                return false;
            }

            // Các thẻ Terrain tuân theo quy tắc kiểm tra chiếm ô
            if (IsTileOccupied(tileObj))
            {
                return false;
            }
        }
        else
        {
            // Các thẻ khác (Building...): Tuân theo quy tắc kiểm tra chiếm ô thông thường
            if (IsTileOccupied(tileObj))
            {
                return false;
            }
>>>>>>> origin/AnKhang_zoo_connection
        }

        // Không cho phép đặt thêm thẻ biến đổi địa hình (như Rain) nếu ô này đã có thẻ biến đổi địa hình từ trước
        if (cardData != null && cardData.IsTileTransformCard())
        {
            PlacedCard[] placedCards = tileObj.GetComponentsInChildren<PlacedCard>();
            foreach (var pc in placedCards)
            {
                if (pc != null && pc.cardData != null && pc.cardData.IsTileTransformCard())
                {
                    return false;
                }
            }
        }

        if (cardData != null && !cardData.IsTileAllowed(tileObj, worldGen))
        {
            return false;
        }

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
<<<<<<< HEAD
=======

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
>>>>>>> origin/AnKhang_zoo_connection
}

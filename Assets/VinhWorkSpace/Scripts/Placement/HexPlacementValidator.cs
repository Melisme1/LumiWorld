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

        if (IsTileOccupied(tileObj))
        {
            return false;
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
}

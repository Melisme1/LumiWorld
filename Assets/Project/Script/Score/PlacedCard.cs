using UnityEngine;

public class PlacedCard : MonoBehaviour
{
    [Header("Card Data")]
    public CardData cardData;

    [Header("Placement")]
    public HexCoordinates placedHex;

    [Header("Biome Cluster")]
    [Tooltip("ID của Cụm Biome độc lập mà ô này trực thuộc (mỗi cụm tối đa 6 ô theo mặc định). Các ô khác cụm sẽ không tạo viền nối.")]
    public int clusterId = -1;

    [Header("Score Display")]
    [Tooltip("Tổng điểm đã hiển thị cho khối này (dùng để tính phần nâng cấp khi nhóm đủ lớn). Không sửa thủ công.")]
    public int displayedScore = 0;

    public CardType CardType
    {
        get
        {
            if (cardData == null)
                return (CardType)(-1);

            return cardData.cardType;
        }
    }

    public string CardID
    {
        get
        {
            if (cardData == null)
                return "";

            return cardData.cardID;
        }
    }
}
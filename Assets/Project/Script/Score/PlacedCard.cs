using UnityEngine;

public class PlacedCard : MonoBehaviour
{
    [Header("Card Data")]
    public CardData cardData;

    [Header("Placement")]
    public HexCoordinates placedHex;

    public CardType CardType
    {
        get
        {
            if (cardData == null)
                return CardType.Terrain;

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
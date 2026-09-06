using UnityEngine;

public enum CardType
{
    Creature,
    Terrain,
    Building,
    Special
}

[CreateAssetMenu(
    fileName = "NewCard",
    menuName = "Card/Card Data"
)]
public class CardData : ScriptableObject
{
    [Header("Basic Information")]
    public string cardID;
    public string cardName;
    public Sprite cardImage;

    [Header("Type")]
    public CardType cardType;
}
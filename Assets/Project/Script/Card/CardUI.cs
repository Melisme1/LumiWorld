using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour
{
    [Header("Card Data")]
    [SerializeField] private CardData cardData;

    [Header("UI")]
    [SerializeField] private Image cardImage;

    public CardData CardData => cardData;

    private void Start()
    {
        Refresh();
    }

    public void Setup(CardData data)
    {
        cardData = data;
        Refresh();
    }

    private void Refresh()
    {
        if (cardData == null)
        {
            Debug.LogWarning(
                "CardUI: CardData is not assigned.",
                this
            );
            return;
        }

        if (cardImage != null)
        {
            cardImage.sprite = cardData.cardImage;
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardDeskController : MonoBehaviour
{

    public static CardDeskController Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    [Header("References")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform cardContainer;

    [Header("Cards")]
    [SerializeField] private List<CardData> cardsToSpawn = new();

    [Header("Layout")]
    [SerializeField] private float maxSpacing = 170f;
    [SerializeField] private float minSpacing = 80f;
    [SerializeField] private float handWidth = 900f;

    [Header("Appear Animation")]
    [SerializeField] private float appearDelay = 0.15f;

    [Header("Rearrange Animation")]
    [SerializeField] private float rearrangeDuration = 0.25f;

    private readonly List<GameObject> cards = new();

    private void Start()
    {
        StartCoroutine(SpawnCards());
    }

    private IEnumerator SpawnCards()
    {
        List<CardData> sortedCards = new List<CardData>(cardsToSpawn);
        sortedCards.Sort(CompareCardData);

        int cardCount =
            sortedCards.Count;

        if (cardCount == 0)
        {
            Debug.LogWarning(
                "CardDeskController: No CardData assigned."
            );

            yield break;
        }

        float spacing =
            CalculateSpacing(cardCount);

        float totalWidth =
            (cardCount - 1) * spacing;

        for (int i = 0; i < cardCount; i++)
        {
            GameObject card =
                Instantiate(
                    cardPrefab,
                    cardContainer
                );

            // -------------------------
            // CARD DATA
            // -------------------------

            CardUI cardUI =
                card.GetComponent<CardUI>();

            if (cardUI != null)
            {
                cardUI.Setup(
                    sortedCards[i]
                );
            }

            // -------------------------
            // POSITION
            // -------------------------

            float x =
                i * spacing -
                totalWidth / 2f;

            Vector2 targetPosition =
                new Vector2(x, 0f);

            // Tell Card its Home Position
            CardHandSlot handSlot =
                card.GetComponent<CardHandSlot>();

            if (handSlot != null)
            {
                handSlot.SetTargetPosition(
                    targetPosition
                );
            }

            // -------------------------
            // APPEAR
            // -------------------------

            CardAppear appear =
                card.GetComponent<CardAppear>();

            if (appear != null)
            {
                appear.Play(
                    targetPosition,
                    i * appearDelay
                );
            }
            else
            {
                RectTransform rect =
                    card.GetComponent<RectTransform>();

                rect.anchoredPosition =
                    targetPosition;
            }

            cards.Add(card);

            yield return new WaitForSeconds(
                appearDelay
            );
        }
    }

    // =========================================
    // REMOVE CARD
    // =========================================

    public void RemoveCard(
        GameObject card
    )
    {
        if (!cards.Contains(card))
        {
            return;
        }

        cards.Remove(card);

        Destroy(card);

        RearrangeCards();
    }

    // =========================================
    // REARRANGE CARDS
    // =========================================

    public void RearrangeCards()
    {
        RearrangeCards(null);
    }

    private void RearrangeCards(GameObject cardAlreadyAnimating)
    {
        SortCardsByTypeAndName();

        int cardCount =
            cards.Count;

        if (cardCount == 0)
        {
            return;
        }

        float spacing =
            CalculateSpacing(cardCount);

        float totalWidth =
            (cardCount - 1) * spacing;

        for (int i = 0; i < cardCount; i++)
        {
            float x =
                i * spacing -
                totalWidth / 2f;

            Vector2 targetPosition =
                new Vector2(x, 0f);

            // Update Home Position
            CardHandSlot handSlot =
                cards[i].GetComponent<CardHandSlot>();

            if (handSlot != null)
            {
                handSlot.SetTargetPosition(
                    targetPosition
                );
            }

            // Move Card
            CardMoveToPosition mover =
                cards[i].GetComponent<CardMoveToPosition>();

            if (cards[i] == cardAlreadyAnimating)
            {
                continue;
            }
            else if (mover != null)
            {
                mover.MoveTo(
                    targetPosition,
                    rearrangeDuration
                );
            }
            else
            {
                RectTransform rect =
                    cards[i].GetComponent<RectTransform>();

                rect.anchoredPosition =
                    targetPosition;
            }
        }
    }

    // =========================================
    // ADD REWARD CARD
    // =========================================

public void AddRewardCard(CardData cardData)
{
    if (cardData == null)
    {
        Debug.LogWarning(
            "CardDeskController: Reward CardData is null."
        );
        return;
    }

    if (cardPrefab == null)
    {
        Debug.LogError(
            "CardDeskController: Card Prefab is not assigned."
        );
        return;
    }

    if (cardContainer == null)
    {
        Debug.LogError(
            "CardDeskController: Card Container is not assigned."
        );
        return;
    }

    GameObject card =
        Instantiate(
            cardPrefab,
            cardContainer
        );

    CardUI cardUI =
        card.GetComponent<CardUI>();

    if (cardUI != null)
    {
        cardUI.Setup(cardData);
    }

    cards.Add(card);

    SortCardsByTypeAndName();

    // Tính lại vị trí cho toàn bộ hand
    int cardCount = cards.Count;

    float spacing =
        CalculateSpacing(cardCount);

    float totalWidth =
        (cardCount - 1) * spacing;

    int newCardIndex =
        cards.IndexOf(card);

    float x =
        newCardIndex * spacing -
        totalWidth / 2f;

    Vector2 targetPosition =
        new Vector2(x, 0f);

    // Card mới
    CardHandSlot handSlot =
        card.GetComponent<CardHandSlot>();

    if (handSlot != null)
    {
        handSlot.SetTargetPosition(
            targetPosition
        );
    }

    CardAppear appear =
        card.GetComponent<CardAppear>();

    if (appear != null)
    {
        appear.Play(
            targetPosition,
            0f
        );
    }
    else
    {
        RectTransform rect =
            card.GetComponent<RectTransform>();

        if (rect != null)
        {
            rect.anchoredPosition =
                targetPosition;
        }
    }

    // Các card cũ tự sắp xếp lại
    RearrangeCards(card);

    Debug.Log(
        $"Reward Card added: {cardData.cardName}"
    );
}
    // =========================================
    // CALCULATE SPACING
    // =========================================

    private float CalculateSpacing(
        int cardCount
    )
    {
        if (cardCount <= 1)
        {
            return 0f;
        }

        float spacing =
            handWidth /
            (cardCount - 1);

        return Mathf.Clamp(
            spacing,
            minSpacing,
            maxSpacing
        );
    }

    private void SortCardsByTypeAndName()
    {
        cards.Sort((left, right) => CompareCardData(
            left != null ? left.GetComponent<CardUI>()?.CardData : null,
            right != null ? right.GetComponent<CardUI>()?.CardData : null
        ));

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
            {
                cards[i].transform.SetSiblingIndex(i);
            }
        }
    }

    // Hand order: type first, then card name, then ID for a deterministic tie-break.
    private static int CompareCardData(CardData left, CardData right)
    {
        if (left == right) return 0;
        if (left == null) return 1;
        if (right == null) return -1;

        int typeComparison = left.cardType.CompareTo(right.cardType);
        if (typeComparison != 0) return typeComparison;

        int nameComparison = string.Compare(
            left.cardName,
            right.cardName,
            StringComparison.OrdinalIgnoreCase
        );
        if (nameComparison != 0) return nameComparison;

        return string.Compare(
            left.cardID,
            right.cardID,
            StringComparison.OrdinalIgnoreCase
        );
    }
}

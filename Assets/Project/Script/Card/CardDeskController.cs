using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardDeskController : MonoBehaviour
{
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
        int cardCount =
            cardsToSpawn.Count;

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
                    cardsToSpawn[i]
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

            if (mover != null)
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
}
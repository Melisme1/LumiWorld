using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardDeskController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform cardContainer;

    [Header("Cards")]
    [SerializeField] private int cardCount = 5;

    [Header("Layout")]
    [SerializeField] private float spacing = 170f;

    [Header("Appear Animation")]
    [SerializeField] private float appearDelay = 0.15f;

    private readonly List<GameObject> cards = new();

    private void Start()
    {
        StartCoroutine(SpawnCards());
    }

    private IEnumerator SpawnCards()
    {
        for (int i = 0; i < cardCount; i++)
        {
            // Tạo Card
            GameObject card =
                Instantiate(cardPrefab, cardContainer);

            RectTransform cardRect =
                card.GetComponent<RectTransform>();

            // Tính vị trí
            float totalWidth =
                (cardCount - 1) * spacing;

            float x =
                i * spacing - totalWidth / 2f;

            Vector2 targetPosition =
                new Vector2(x, 0f);

            // Đưa Card về vị trí đích
            cardRect.anchoredPosition =
                targetPosition;

            // Chạy animation
            CardAppear appear =
                card.GetComponent<CardAppear>();

            if (appear != null)
            {
                appear.Play(
                    targetPosition,
                    0f
                );
            }

            cards.Add(card);

            // Chờ trước khi tạo Card tiếp theo
            yield return new WaitForSeconds(appearDelay);
        }
    }
}
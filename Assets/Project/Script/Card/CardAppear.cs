using System.Collections;
using UnityEngine;

public class CardAppear : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private float startScale = 0.7f;
    [SerializeField] private float startOffsetY = -100f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private Vector2 targetPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        rectTransform.localScale = Vector3.one * startScale;
    }

    public void Play(Vector2 finalPosition, float delay)
    {
        targetPosition = finalPosition;

        StartCoroutine(Appear(delay));
    }

    private IEnumerator Appear(float delay)
    {
        yield return new WaitForSeconds(delay);

        Vector2 startPosition =
            targetPosition + Vector2.up * startOffsetY;

        rectTransform.anchoredPosition = startPosition;
        rectTransform.localScale = Vector3.one * startScale;

        canvasGroup.alpha = 0f;

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;

            t = Mathf.SmoothStep(0f, 1f, t);

            rectTransform.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    t
                );

            rectTransform.localScale =
                Vector3.Lerp(
                    Vector3.one * startScale,
                    Vector3.one,
                    t
                );

            canvasGroup.alpha = t;

            yield return null;
        }

        rectTransform.anchoredPosition = targetPosition;
        rectTransform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
    }
}
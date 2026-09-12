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
    private Coroutine appearCoroutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup =
                gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;

        rectTransform.localScale =
            Vector3.one * startScale;
    }

    public void Play(Vector2 finalPosition, float delay)
    {
        targetPosition = finalPosition;

        if (appearCoroutine != null)
        {
            StopCoroutine(appearCoroutine);
        }

        appearCoroutine =
            StartCoroutine(Appear(delay));
    }

    private IEnumerator Appear(float delay)
    {
        yield return new WaitForSeconds(delay);

        Vector2 startPosition =
            targetPosition +
            Vector2.up * startOffsetY;

        rectTransform.anchoredPosition =
            startPosition;

        rectTransform.localScale =
            Vector3.one * startScale;

        canvasGroup.alpha = 0f;

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(time / duration);

            t =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

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

        // Đảm bảo trạng thái cuối cùng luôn đúng
        rectTransform.anchoredPosition =
            targetPosition;

        rectTransform.localScale =
            Vector3.one;

        canvasGroup.alpha = 1f;

        appearCoroutine = null;
    }

    public void ShowImmediately(Vector2 position)
    {
        if (appearCoroutine != null)
        {
            StopCoroutine(appearCoroutine);
            appearCoroutine = null;
        }

        targetPosition = position;

        rectTransform.anchoredPosition =
            position;

        rectTransform.localScale =
            Vector3.one;

        canvasGroup.alpha = 1f;
    }
}
using System.Collections;
using UnityEngine;

public class CardMoveToPosition : MonoBehaviour
{
    private RectTransform rectTransform;

    private Coroutine moveCoroutine;

    private void Awake()
    {
        rectTransform =
            GetComponent<RectTransform>();
    }

    public void MoveTo(
        Vector2 targetPosition,
        float duration
    )
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        if (duration <= 0f)
        {
            rectTransform.anchoredPosition = targetPosition;
            return;
        }

        moveCoroutine =
            StartCoroutine(
                MoveAnimation(
                    targetPosition,
                    duration
                )
            );
    }

    /// <summary>
    /// Hủy animation dời chỗ đang chạy để nhường quyền điều khiển vị trí
    /// cho hệ thống khác (appear / return / rearrange).
    /// </summary>
    public void Stop()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
    }

    private IEnumerator MoveAnimation(
        Vector2 targetPosition,
        float duration
    )
    {
        Vector2 startPosition =
            rectTransform.anchoredPosition;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t =
                elapsed / duration;

            t = Mathf.SmoothStep(
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

            yield return null;
        }

        rectTransform.anchoredPosition =
            targetPosition;

        moveCoroutine = null;
    }
}
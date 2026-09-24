using System.Collections;
using UnityEngine;

public class CardAppear : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private float startScale = 0.7f;
    [SerializeField] private float startOffsetY = -100f;

    [Header("Reward Juice (game feel khi nhận card)")]
    [Tooltip("Tỉ lệ phóng to đỉnh khi card mới 'bật' ra khỏi tay (overshoot pop).")]
    [SerializeField] private float popScale = 1.22f;

    [Tooltip("Biên độ lắc qua lại (độ) sau khi card đáp xuống vị trí.")]
    [SerializeField] private float wobbleAngle = 9f;

    [Tooltip("Số lần lắc qua lại sau khi đáp xuống.")]
    [SerializeField] private float wobbleCount = 2.5f;

    [Tooltip("Thời gian lắc (giây).")]
    [SerializeField] private float wobbleDuration = 0.32f;

    [Tooltip("Độ nhún dọc trục Y khi đáp xuống (squash).")]
    [SerializeField] private float landSquashY = 0.14f;

    [Tooltip("Tỉ lệ phóng to khi 'punch' lúc cộng dồn số lượng vào slot đã có.")]
    [SerializeField] private float punchScale = 1.35f;

    [Tooltip("Biên độ lắc (độ) khi 'punch' lúc cộng dồn số lượng.")]
    [SerializeField] private float punchWobbleAngle = 7f;

    [Header("Reward Flash (vệt sáng chạy qua card mới)")]
    [Tooltip("Tự sinh một vệt sáng trắng chạy ngang card khi card mới xuất hiện (không cần setup prefab).")]
    [SerializeField] private bool playFlashOnAppear = true;

    [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField] private float flashDuration = 0.5f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    // Vệt sáng sinh tự động dùng cho hiệu ứng reward (cache lại để tái sử dụng).
    private RectTransform flashRect;
    private UnityEngine.UI.Image flashImage;

    private CardDrag cardDrag;

    /// <summary>
    /// True khi card đang được kéo hoặc đang bay về tay.
    /// Lúc đó CardDrag là nguồn duy nhất được ghi canvasGroup.alpha; nếu CardAppear
    /// chen vào ghi alpha (punch/flash khi vừa nhận reward) thì animation trả bài
    /// sẽ bị đè và card kẹt ở trạng thái mờ.
    /// </summary>
    private bool IsDragOwned =>
        cardDrag != null && (cardDrag.IsDragging || cardDrag.IsReturningToHand);

    private void SetAlpha(float value)
    {
        if (canvasGroup == null || IsDragOwned)
        {
            return;
        }

        canvasGroup.alpha = value;
    }

    private Vector2 targetPosition;
    private Coroutine appearCoroutine;

    /// <summary>
    /// True khi appear animation đang chạy. CardHover đọc cờ này để nhường
    /// quyền điều khiển anchoredPosition, tránh hai hệ thống giằng co nhau.
    /// </summary>
    public bool IsPlaying => appearCoroutine != null;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        cardDrag = GetComponent<CardDrag>();

        if (canvasGroup == null)
        {
            canvasGroup =
                gameObject.AddComponent<CanvasGroup>();
        }

        // Không đặt alpha/scale ở đây: prefab dùng cho cả card stack sẵn có.
        // Trạng thái ẩn được thiết lập ngay khi Appear() bắt đầu.
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

    /// <summary>
    /// Hiệu ứng "pop" tại chỗ (không dịch chuyển): dùng khi cộng dồn số lượng
    /// vào một slot đã nằm đúng vị trí. Tránh việc card bị nhảy xuống dưới
    /// rồi trồi lên như Play() gây giật.
    /// </summary>
    public void PlayPunch(float scaleAmount = 1.15f)
    {
        if (appearCoroutine != null)
        {
            StopCoroutine(appearCoroutine);
        }

        appearCoroutine =
            StartCoroutine(Punch(scaleAmount, punchWobbleAngle));
    }

    /// <summary>
    /// Punch mạnh hơn + lắc nhẹ: dùng khi người chơi VỪA NHẬN card mới (reward)
    /// nhưng card cùng loại đã có sẵn trên tay nên chỉ cộng dồn số lượng.
    /// </summary>
    public void PlayRewardPunch()
    {
        if (appearCoroutine != null)
        {
            StopCoroutine(appearCoroutine);
        }

        appearCoroutine =
            StartCoroutine(Punch(punchScale, punchWobbleAngle));
    }

    /// <summary>
    /// Chạy riêng hiệu ứng vệt sáng (không tranh chấp scale/position với các animation khác).
    /// </summary>
    public void PlayFlash()
    {
        if (flashRect == null)
        {
            CreateFlash();
        }

        if (flashRect == null)
        {
            return;
        }

        StopCoroutine(nameof(FlashRoutine));
        StartCoroutine(FlashRoutine());
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

        SetAlpha(0f);

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

            // Overshoot: phóng vọt qua 1.0 rồi mới thu về đúng tỉ lệ -> cảm giác card "bật" ra.
            float scaleCurve = t < 0.78f
                ? Mathf.Lerp(startScale, popScale, Mathf.SmoothStep(0f, 1f, t / 0.78f))
                : Mathf.Lerp(popScale, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.78f) / 0.22f));

            rectTransform.localScale =
                Vector3.one * scaleCurve;

            SetAlpha(Mathf.Clamp01(t * 2f));

            yield return null;
        }

        // Đảm bảo trạng thái cuối cùng luôn đúng
        rectTransform.anchoredPosition =
            targetPosition;

        rectTransform.localScale =
            Vector3.one;

        SetAlpha(1f);

        // Đáp xuống: lắc nhẹ + nhún -> game feel rõ ràng hơn hẳn.
        yield return Wobble(wobbleDuration, wobbleAngle, wobbleCount, landSquashY);

        // Rung sprite rất ngắn (giảm dần) để card trông có trọng lượng.
        yield return LocalShake(0.1f, 4.5f);

        appearCoroutine = null;
    }

    private IEnumerator Punch(float scaleAmount, float wobbleAngleDegrees)
    {
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        SetAlpha(1f);

        float up = 0.12f;
        float down = 0.18f;

        float time = 0f;
        while (time < up)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / up));
            rectTransform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * scaleAmount, t);
            yield return null;
        }

        time = 0f;
        while (time < down)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / down));
            rectTransform.localScale = Vector3.Lerp(Vector3.one * scaleAmount, Vector3.one, t);
            yield return null;
        }

        rectTransform.localScale = Vector3.one;
        SetAlpha(1f);

        // Punch là báo hiệu "vừa được cộng thêm 1 lá" -> kèm vệt sáng + lắc nhẹ.
        PlayFlash();
        yield return Wobble(0.22f, wobbleAngleDegrees, 1.5f, 0.06f);

        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;

        appearCoroutine = null;
    }

    /// <summary>
    /// Lắc qua lại quanh trục Z giảm dần (damped) + nhún trục Y khi đáp xuống.
    /// Đây là phần tạo cảm giác "đồ chơi" (game feel) rõ nhất khi card chạm tay.
    /// </summary>
    private IEnumerator Wobble(float totalDuration, float angleDegrees, float cycles, float squashY)
    {
        if (totalDuration <= 0f)
        {
            yield break;
        }

        float time = 0f;

        while (time < totalDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / totalDuration);

            // Biên độ giảm dần theo thời gian, dao động hình sin theo số vòng.
            float damp = 1f - t;
            float angle = Mathf.Sin(t * Mathf.PI * 2f * cycles) * angleDegrees * damp;

            // Nhún: bẹp xuống trục Y ở nửa đầu, nảy lại ở nửa sau.
            float squash = Mathf.Sin(t * Mathf.PI) * squashY;

            rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            rectTransform.localScale = new Vector3(1f + squash * 0.6f, 1f - squash, 1f);

            yield return null;
        }

        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Rung vị trí rất ngắn quanh vị trí hiện tại (hit-stop nhỏ) để tăng trọng lượng.
    /// </summary>
    private IEnumerator LocalShake(float totalDuration, float amplitude)
    {
        if (totalDuration <= 0f)
        {
            yield break;
        }

        Vector2 basePosition = rectTransform.anchoredPosition;
        float time = 0f;

        while (time < totalDuration)
        {
            time += Time.deltaTime;
            float damp = 1f - Mathf.Clamp01(time / totalDuration);

            Vector2 offset = new Vector2(
                Random.Range(-amplitude, amplitude),
                Random.Range(-amplitude, amplitude)
            ) * damp;

            rectTransform.anchoredPosition = basePosition + offset;

            yield return null;
        }

        rectTransform.anchoredPosition = basePosition;
    }

    /// <summary>
    /// Sinh một vệt sáng trắng ngang card (child Image + Material additive) rồi chạy qua.
    /// Không phụ thuộc sprite/setup prefab nên chạy được với mọi card.
    /// </summary>
    private void CreateFlash()
    {
        GameObject flashObj = new GameObject("RewardFlash", typeof(RectTransform));
        flashObj.transform.SetParent(rectTransform, false);

        flashRect = flashObj.GetComponent<RectTransform>();
        flashRect.anchorMin = new Vector2(0f, 0f);
        flashRect.anchorMax = new Vector2(0f, 1f);
        flashRect.pivot = new Vector2(0.5f, 0.5f);
        flashRect.sizeDelta = new Vector2(46f, 0f);

        flashImage = flashObj.AddComponent<UnityEngine.UI.Image>();
        flashImage.raycastTarget = false;
        flashImage.maskable = false;

        // Dải mờ dần ở hai mép để vệt sáng trông mềm, không bị cắt vuông.
        int width = 24;
        Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int x = 0; x < width; x++)
        {
            float normalized = x / (float)(width - 1);
            float alpha = Mathf.Sin(normalized * Mathf.PI);
            texture.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply();

        flashImage.sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, 1f),
            new Vector2(0.5f, 0.5f),
            100f
        );

        flashObj.SetActive(false);
    }

    private IEnumerator FlashRoutine()
    {
        float cardWidth = Mathf.Max(rectTransform.rect.width, 1f);

        // Bắt đầu ngoài mép trái, kết thúc ngoài mép phải -> sweep toàn bộ card.
        flashRect.anchoredPosition = new Vector2(-cardWidth * 0.8f, 0f);
        flashImage.color = flashColor;
        flashImage.gameObject.SetActive(true);

        float time = 0f;

        while (time < flashDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / flashDuration);

            float eased = Mathf.SmoothStep(0f, 1f, t);

            flashRect.anchoredPosition = new Vector2(
                Mathf.Lerp(-cardWidth * 0.8f, cardWidth * 0.8f, eased),
                0f
            );

            // Mờ dần ở nửa sau để vệt sáng tan biến thay vì tắt đột ngột.
            Color color = flashColor;
            color.a = flashColor.a * (1f - Mathf.Clamp01((t - 0.45f) / 0.55f));
            flashImage.color = color;

            yield return null;
        }

        flashImage.gameObject.SetActive(false);
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

        rectTransform.localRotation =
            Quaternion.identity;

        SetAlpha(1f);
    }
}
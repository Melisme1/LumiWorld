using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Gói game feel" cho việc NHẬN CARD mới: âm thanh + tia sáng bắn ra + vòng highlight
/// bao quanh slot thẻ vừa được thêm.
///
/// Toàn bộ object đều sinh tại runtime (không cần prefab, không cần sửa Scene):
///  - Âm thanh: tự tổng hợp 1 tiếng "ding" bằng code -> không cần file .wav.
///    Nếu bạn muốn dùng âm thanh riêng, bật useCustomClip và gán cardRewardClip.
///  - Sparkle: 8 tia sáng nhỏ bắn ra từ tâm thẻ.
///  - Highlight: vòng sáng bao quanh thẻ, nhấp nháy 2 nhịp rồi tắt.
/// </summary>
public class CardRewardJuice : MonoBehaviour
{
    // Cho phép bật/tắt nhanh toàn bộ gói hiệu ứng khi nhận card.
    public static bool Enabled = true;

    [Header("Sound")]
    [Tooltip("Bật để dùng AudioClip riêng thay vì tiếng 'ding' tổng hợp bằng code.")]
    [SerializeField] private bool useCustomClip = false;
    [SerializeField] private AudioClip cardRewardClip;
    [Range(0f, 1f)][SerializeField] private float soundVolume = 0.5f;

    // =========================================================
    // ENTRY POINT
    // =========================================================

    /// <summary>
    /// Chạy toàn bộ hiệu ứng nhận card tại RectTransform của slot vừa được thêm/cộng dồn.
    /// </summary>
    public static void Play(RectTransform slotRect, bool isNewSlot)
    {
        if (!Enabled || slotRect == null)
        {
            return;
        }

        GameObject juiceObj = new GameObject("CardRewardJuice");
        CardRewardJuice juice = juiceObj.AddComponent<CardRewardJuice>();
        juice.Run(slotRect, isNewSlot);
    }

    private void Run(RectTransform slotRect, bool isNewSlot)
    {
        PlaySound();

        // Slot mới: thẻ toả sáng mạnh. Slot cộng dồn: chỉ nhấp nhẹ để không rối mắt.
        StartCoroutine(HighlightRoutine(slotRect, isNewSlot));

        if (isNewSlot)
        {
            StartCoroutine(SparkleRoutine(slotRect));
        }
    }

    // =========================================================
    // SOUND
    // =========================================================

    private void PlaySound()
    {
        AudioClip clip = useCustomClip ? cardRewardClip : GetOrCreateDingClip();

        if (clip == null)
        {
            return;
        }

        // Dùng GameObject riêng để nhiều tiếng có thể phát chồng lên nhau.
        GameObject audioObj = new GameObject("CardRewardSfx");
        audioObj.transform.SetParent(transform, false);

        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = soundVolume;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D: UI sound, không bị giảm âm theo vị trí camera
        source.Play();

        Destroy(audioObj, clip.length + 0.1f);
    }

    private static AudioClip cachedDingClip;

    /// <summary>
    /// Tổng hợp tiếng "ting" vui tai bằng code (2 nốt nối tiếp, tần số 5th)
    /// để project không cần kèm file âm thanh nào.
    /// </summary>
    private static AudioClip GetOrCreateDingClip()
    {
        if (cachedDingClip != null)
        {
            return cachedDingClip;
        }

        const int sampleRate = 44100;
        const float duration = 0.34f;

        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Nốt 1 (E6) -> nốt 2 (B6): nghe như tiếng "nhận thưởng".
        float freqA = 1318.51f;
        float freqB = 1975.53f;
        float switchPoint = 0.45f; // đổi nốt tại 45% thời lượng

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float normalized = t / duration;

            float frequency = normalized < switchPoint
                ? freqA
                : Mathf.Lerp(freqA, freqB, (normalized - switchPoint) / (1f - switchPoint));

            // Envelope: vào rất nhanh, tắt dần theo hàm mũ -> nghe "trong trẻo".
            float attack = Mathf.Clamp01(t / 0.008f);
            float decay = Mathf.Exp(-t * 9f);

            float sine = Mathf.Sin(2f * Mathf.PI * frequency * t);

            // Thêm hài bậc 2 nhẹ cho tiếng dày hơn.
            float harmonic = Mathf.Sin(4f * Mathf.PI * frequency * t) * 0.22f;

            samples[i] = (sine + harmonic) * attack * decay * 0.55f;
        }

        cachedDingClip = AudioClip.Create("CardRewardDing", sampleCount, 1, sampleRate, false);
        cachedDingClip.SetData(samples, 0);

        return cachedDingClip;
    }

    // =========================================================
    // HIGHLIGHT
    // =========================================================

    private IEnumerator HighlightRoutine(RectTransform slotRect, bool isNewSlot)
    {
        const string highlightName = "RewardHighlight";

        Transform canvasRoot = GetCanvasRoot(slotRect);
        if (canvasRoot == null)
        {
            yield break;
        }

        // Đã có vòng highlight của lần nhận trước -> tái sử dụng, tránh chồng nhiều vòng.
        Transform existing = canvasRoot.Find(highlightName);
        GameObject highlightObj;

        if (existing != null)
        {
            highlightObj = existing.gameObject;
        }
        else
        {
            highlightObj = new GameObject(highlightName, typeof(RectTransform));
            highlightObj.transform.SetParent(canvasRoot, false);

            RectTransform highlightRect = highlightObj.GetComponent<RectTransform>();
            highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
            highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
            highlightRect.pivot = new Vector2(0.5f, 0.5f);

            // Viền dày + sprite vuông mặc định của Unity -> khung sáng bo góc nhẹ.
            Image image = highlightObj.AddComponent<Image>();
            image.sprite = CreateOutlineSprite();
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            image.maskable = false;
            image.color = new Color(1f, 0.88f, 0.4f, 0f);
        }

        RectTransform rect = highlightObj.GetComponent<RectTransform>();
        Image highlightImage = highlightObj.GetComponent<Image>();

        // Khớp vị trí/kích thước với thẻ, phình ra một chút để viền nằm ngoài thẻ.
        rect.anchoredPosition = slotRect.anchoredPosition;
        rect.sizeDelta = slotRect.rect.size + new Vector2(26f, 26f);

        float pulseCount = isNewSlot ? 2f : 1f;
        float duration = isNewSlot ? 0.62f : 0.34f;

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * pulseCount));
            float fade = 1f - t;

            Color color = highlightImage.color;
            color.a = pulse * fade * (isNewSlot ? 0.95f : 0.7f);
            highlightImage.color = color;

            yield return null;
        }

        if (highlightObj != null)
        {
            Destroy(highlightObj);
        }
    }

    private static Sprite cachedOutlineSprite;

    /// <summary>
    /// Sprite 9-slice dạng khung rỗng (viền 3px) để làm vòng highlight.
    /// </summary>
    private static Sprite CreateOutlineSprite()
    {
        if (cachedOutlineSprite != null)
        {
            return cachedOutlineSprite;
        }

        const int size = 16;
        const int border = 3;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder =
                    x < border || y < border ||
                    x >= size - border || y >= size - border;

                texture.SetPixel(x, y, isBorder
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0f));
            }
        }

        texture.Apply();

        cachedOutlineSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border)
        );

        return cachedOutlineSprite;
    }

    // =========================================================
    // SPARKLE
    // =========================================================

    private IEnumerator SparkleRoutine(RectTransform slotRect)
    {
        Transform canvasRoot = GetCanvasRoot(slotRect);
        if (canvasRoot == null)
        {
            yield break;
        }

        const int sparkleCount = 8;
        const float duration = 0.55f;
        const float distance = 130f;

        RectTransform[] sparkleRects = new RectTransform[sparkleCount];
        Image[] sparkleImages = new Image[sparkleCount];
        Vector2[] directions = new Vector2[sparkleCount];

        Sprite sparkleSprite = CreateSparkleSprite();

        for (int i = 0; i < sparkleCount; i++)
        {
            GameObject sparkleObj = new GameObject($"Sparkle_{i}", typeof(RectTransform));
            sparkleObj.transform.SetParent(canvasRoot, false);

            RectTransform sparkleRect = sparkleObj.GetComponent<RectTransform>();
            sparkleRect.anchorMin = new Vector2(0.5f, 0.5f);
            sparkleRect.anchorMax = new Vector2(0.5f, 0.5f);
            sparkleRect.pivot = new Vector2(0.5f, 0.5f);
            sparkleRect.sizeDelta = new Vector2(18f, 18f);
            sparkleRect.anchoredPosition = slotRect.anchoredPosition;

            Image sparkleImage = sparkleObj.AddComponent<Image>();
            sparkleImage.sprite = sparkleSprite;
            sparkleImage.raycastTarget = false;
            sparkleImage.maskable = false;
            sparkleImage.color = Color.white;

            // Chia đều 8 hướng, lệch ngẫu nhiên nhẹ để trông tự nhiên.
            float angle = (i / (float)sparkleCount) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            sparkleRects[i] = sparkleRect;
            sparkleImages[i] = sparkleImage;
        }

        Vector2 origin = slotRect.anchoredPosition;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            float eased = 1f - Mathf.Pow(1f - t, 3f);   // ease-out: bắn nhanh rồi chậm
            float fade = 1f - t;

            for (int i = 0; i < sparkleCount; i++)
            {
                if (sparkleRects[i] == null) continue;

                Vector2 direction = directions[i];

                // Đẩy ra xa dần kèm gia tốc nhẹ theo trục Y (như hạt bụi sáng bay lên).
                Vector2 offset = direction * (distance * eased)
                                 + Vector2.up * (18f * eased * eased);

                sparkleRects[i].anchoredPosition = origin + offset;

                float scale = Mathf.Lerp(0.4f, 1.15f, Mathf.Sin(t * Mathf.PI));
                sparkleRects[i].localScale = Vector3.one * scale;

                Color color = sparkleImages[i].color;
                color.a = fade;
                sparkleImages[i].color = color;
            }

            yield return null;
        }

        for (int i = 0; i < sparkleCount; i++)
        {
            if (sparkleRects[i] != null)
            {
                Destroy(sparkleRects[i].gameObject);
            }
        }
    }

    private static Sprite cachedSparkleSprite;

    /// <summary>
    /// Hạt sáng 4 cánh (tia sáng) sinh bằng code: dọc + ngang + 2 chéo mờ hơn.
    /// </summary>
    private static Sprite CreateSparkleSprite()
    {
        if (cachedSparkleSprite != null)
        {
            return cachedSparkleSprite;
        }

        const int size = 32;
        float center = (size - 1) * 0.5f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;

                // Cánh chính (dọc/ngang) mảnh và dài.
                float main = Mathf.Max(
                    Falloff(Mathf.Abs(dx), 1.6f) * Falloff(Mathf.Abs(dy), 5.5f),
                    Falloff(Mathf.Abs(dy), 1.6f) * Falloff(Mathf.Abs(dx), 5.5f)
                );

                // Cánh chéo ngắn hơn cho hạt sáng trông "lấp lánh".
                float diagonal = Mathf.Max(
                    Falloff(Mathf.Abs(dx + dy), 3.2f) * Falloff(Mathf.Abs(dx - dy), 2.2f),
                    Falloff(Mathf.Abs(dx - dy), 3.2f) * Falloff(Mathf.Abs(dx + dy), 2.2f)
                ) * 0.6f;

                // Lõi sáng ở tâm.
                float core = Falloff(Mathf.Sqrt(dx * dx + dy * dy), 2.4f);

                float alpha = Mathf.Clamp01(Mathf.Max(main, Mathf.Max(diagonal, core)));

                texture.SetPixel(x, y, new Color(1f, 0.96f, 0.78f, alpha));
            }
        }

        texture.Apply();

        cachedSparkleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );

        return cachedSparkleSprite;
    }

    /// <summary>
    /// Hàm suy giảm dạng mũ: 1 tại tâm, về ~0 ở xa.
    /// </summary>
    private static float Falloff(float distanceFromCenter, float sharpness)
    {
        return Mathf.Exp(-(distanceFromCenter * distanceFromCenter) / (sharpness * sharpness));
    }

    // =========================================================
    // HELPERS
    // =========================================================

    /// <summary>
    /// Tìm Canvas chứa slot để các hiệu ứng con nằm đúng hệ toạ độ UI.
    /// </summary>
    private static Transform GetCanvasRoot(RectTransform slotRect)
    {
        Canvas canvas = slotRect.GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            return canvas.transform;
        }

        if (slotRect.parent != null)
        {
            return slotRect.parent;
        }

        return null;
    }
}
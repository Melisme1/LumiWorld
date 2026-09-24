using UnityEngine;

/// <summary>
/// "Gói game feel" cho việc NHẬN CARD mới.
///
/// Toàn bộ object đều sinh tại runtime (không cần prefab, không cần sửa Scene):
///  - Âm thanh: tự tổng hợp 1 tiếng "ding" bằng code -> không cần file .wav.
///    Nếu bạn muốn dùng âm thanh riêng, bật useCustomClip và gán cardRewardClip.
///
/// ĐÃ BỎ toàn bộ hiệu ứng ÁNH SÁNG (tia sparkle bắn ra + vệt sáng chạy qua card)
/// và vòng highlight: chúng gây rối mắt và dễ lệch khỏi khung thẻ. Hiệu ứng nhận
/// card giờ chỉ còn tiếng 'ding' + badge số lượng trên thẻ.
/// </summary>
public class CardRewardJuice : MonoBehaviour
{
    // Cho phép bật/tắt nhanh âm thanh khi nhận card.
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
    /// Phát phản hồi khi nhận card tại slot vừa được thêm/cộng dồn.
    /// slotRect/isNewSlot/delay được giữ lại trong chữ ký để tương thích với caller,
    /// nhưng hiện không còn hiệu ứng hình ảnh nào dùng chúng.
    /// </summary>
    public static void Play(RectTransform slotRect, bool isNewSlot, float delay = 0f)
    {
        if (!Enabled || slotRect == null)
        {
            return;
        }

        GameObject juiceObj = new GameObject("CardRewardJuice");
        CardRewardJuice juice = juiceObj.AddComponent<CardRewardJuice>();
        juice.Run();
    }

    private void Run()
    {
        PlaySound();
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
}
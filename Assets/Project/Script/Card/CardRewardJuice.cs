using UnityEngine;

/// <summary>
/// Phản hồi khi người chơi nhận Card.
///
/// Chức năng hiện tại:
/// - Phát âm thanh "ding" khi nhận Card.
/// - Không tạo GameObject runtime.
/// - Không tạo AudioSource runtime.
/// - Dùng AudioSource có sẵn trong Scene.
/// - Hỗ trợ AudioClip riêng hoặc tiếng ding được tạo bằng code.
/// - Có thể phát nhiều âm thanh cùng lúc bằng PlayOneShot().
/// </summary>
public class CardRewardJuice : MonoBehaviour
{
    // =========================================================
    // SINGLETON
    // =========================================================

    public static CardRewardJuice Instance { get; private set; }

    // Cho phép bật/tắt toàn bộ hiệu ứng nhận Card.
    public static bool Enabled = true;


    // =========================================================
    // SOUND SETTINGS
    // =========================================================

    [Header("Sound")]

    [Tooltip("Bật để sử dụng AudioClip riêng.")]
    [SerializeField] private bool useCustomClip = false;

    [Tooltip("Âm thanh khi nhận Card.")]
    [SerializeField] private AudioClip cardRewardClip;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.5f;

    [Tooltip("AudioSource dùng để phát âm thanh.")]
    [SerializeField] private AudioSource audioSource;


    // =========================================================
    // INTERNAL
    // =========================================================

    private static AudioClip cachedDingClip;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Giữ object này khi đổi Scene nếu sau này cần.
        // Nếu không cần thì có thể bỏ dòng này.
        DontDestroyOnLoad(gameObject);

        // Tự tìm AudioSource nếu chưa kéo vào Inspector.
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Nếu vẫn chưa có AudioSource thì thêm một lần duy nhất.
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Cấu hình AudioSource cho UI sound.
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }


    // =========================================================
    // ENTRY POINT
    // =========================================================

    /// <summary>
    /// Phát hiệu ứng khi nhận Card.
    ///
    /// slotRect / isNewSlot / delay được giữ lại
    /// để tương thích với code CardDesk hiện tại.
    /// </summary>
    public static void Play(
        RectTransform slotRect,
        bool isNewSlot,
        float delay = 0f)
    {
        if (!Enabled)
            return;

        if (Instance == null)
        {
            Debug.LogWarning(
                "CardRewardJuice chưa được đặt vào Scene."
            );

            return;
        }

        Instance.PlayRewardSound();
    }


    // =========================================================
    // SOUND
    // =========================================================

    private void PlayRewardSound()
    {
        if (audioSource == null)
            return;

        AudioClip clip = GetRewardClip();

        if (clip == null)
            return;

        // PlayOneShot cho phép nhiều tiếng phát chồng lên nhau.
        audioSource.PlayOneShot(
            clip,
            soundVolume
        );
    }


    /// <summary>
    /// Lấy AudioClip đang được sử dụng.
    /// </summary>
    private AudioClip GetRewardClip()
    {
        // Nếu người dùng muốn dùng file âm thanh riêng.
        if (useCustomClip)
        {
            return cardRewardClip;
        }

        // Nếu không thì tạo tiếng ding bằng code.
        return GetOrCreateDingClip();
    }


    // =========================================================
    // GENERATED DING
    // =========================================================

    /// <summary>
    /// Tạo tiếng "ding" bằng code.
    ///
    /// Không tạo lại mỗi lần nhận Card.
    /// AudioClip được cache bằng cachedDingClip.
    /// </summary>
    private static AudioClip GetOrCreateDingClip()
    {
        if (cachedDingClip != null)
        {
            return cachedDingClip;
        }

        const int sampleRate = 44100;
        const float duration = 0.34f;

        int sampleCount =
            Mathf.CeilToInt(sampleRate * duration);

        float[] samples =
            new float[sampleCount];


        // E6
        float freqA = 1318.51f;

        // B6
        float freqB = 1975.53f;

        // Thời điểm chuyển nốt.
        float switchPoint = 0.45f;


        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;

            float normalized =
                t / duration;


            // Chuyển từ E6 -> B6.
            float frequency =
                normalized < switchPoint
                ? freqA
                : Mathf.Lerp(
                    freqA,
                    freqB,
                    (normalized - switchPoint) /
                    (1f - switchPoint)
                );


            // Attack rất nhanh.
            float attack =
                Mathf.Clamp01(t / 0.008f);


            // Decay.
            float decay =
                Mathf.Exp(-t * 9f);


            // Âm chính.
            float sine =
                Mathf.Sin(
                    2f *
                    Mathf.PI *
                    frequency *
                    t
                );


            // Harmonic nhẹ để âm thanh dày hơn.
            float harmonic =
                Mathf.Sin(
                    4f *
                    Mathf.PI *
                    frequency *
                    t
                ) * 0.22f;


            samples[i] =
                (sine + harmonic) *
                attack *
                decay *
                0.55f;
        }


        cachedDingClip =
            AudioClip.Create(
                "CardRewardDing",
                sampleCount,
                1,
                sampleRate,
                false
            );


        cachedDingClip.SetData(
            samples,
            0
        );


        return cachedDingClip;
    }
}
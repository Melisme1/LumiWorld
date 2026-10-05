using System.Collections;
using UnityEngine;

/// <summary>
/// Hiệu ứng hiển thị "+X" bay lên khi người chơi đặt bài xuống khối (giống game Preserve).
/// Sử dụng sprite từ Assets/Project/Art/poitn.png (+1, +2, +10).
/// </summary>
public class ScorePopup : MonoBehaviour
{
    // =========================================================
    // SPRITE LOOKUP (từ sprite sheet poitn.png)
    // =========================================================

    // Cho phép gán sprite trực tiếp từ Inspector (khuyến nghị).
    // Nếu chưa gán, hệ thống sẽ tự tìm trong Resources / AssetDatabase.
    public static Sprite SpritePlus1;
    public static Sprite SpritePlus2;
    public static Sprite SpritePlus10;

    private static Sprite[] cachedSprites;
    private static bool spritesLoaded;

    /// <summary>
    /// Lấy sprite "+1", "+2" hoặc "+10" tương ứng với số điểm.
    /// Trả về sprite gần nhất nếu giá trị vượt quá các sprite có sẵn.
    /// </summary>
    public static Sprite GetSpriteForScore(int score)
    {
        // Ưu tiên sprite được gán trực tiếp trong Inspector
        if (score == 1 && SpritePlus1 != null) return SpritePlus1;
        if (score == 2 && SpritePlus2 != null) return SpritePlus2;
        if (score == 10 && SpritePlus10 != null) return SpritePlus10;

        EnsureSpritesLoaded();

        if (cachedSprites == null || cachedSprites.Length == 0)
            return null;

        // Tên sprite trong sheet: "+1", "+2", "+10"
        string targetName = "+" + score;

        foreach (Sprite sprite in cachedSprites)
        {
            if (sprite != null && sprite.name == targetName)
                return sprite;
        }

        // Nếu không có sprite khớp chính xác, chọn sprite có điểm cao nhất không vượt quá score
        Sprite fallback = cachedSprites[0];
        int bestValue = -1;

        foreach (Sprite sprite in cachedSprites)
        {
            if (sprite == null) continue;

            int value = ParseSpriteValue(sprite.name);
            if (value <= score && value > bestValue)
            {
                bestValue = value;
                fallback = sprite;
            }
        }

        return fallback;
    }

    private static int ParseSpriteValue(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return 0;

        string numeric = spriteName.TrimStart('+');
        return int.TryParse(numeric, out int value) ? value : 0;
    }

    private static void EnsureSpritesLoaded()
    {
        if (spritesLoaded) return;
        spritesLoaded = true;

        // Load toàn bộ sprite từ sprite sheet "poitn"
        cachedSprites = Resources.LoadAll<Sprite>("poitn");

        if (cachedSprites != null && cachedSprites.Length > 0)
            return;

#if UNITY_EDITOR
        // Fallback dành riêng cho Editor: dùng AssetDatabase để load sprite sheet
        cachedSprites = LoadSpritesFromAssetDatabase();
#endif
    }

#if UNITY_EDITOR
    private static Sprite[] LoadSpritesFromAssetDatabase()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("poitn t:Sprite");

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);

            var sprites = new System.Collections.Generic.List<Sprite>();

            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }

            if (sprites.Count > 0)
                return sprites.ToArray();
        }

        return null;
    }
#endif

    // =========================================================
    // SPAWNER
    // =========================================================

    /// <summary>
    /// Tạo hiệu ứng "+score" bay lên tại vị trí world position.
    /// </summary>
    public static ScorePopup Spawn(int score, Vector3 worldPosition, float scale = 1f)
    {
        if (score <= 0) return null;

        Sprite sprite = GetSpriteForScore(score);
        if (sprite == null)
        {
            Debug.LogWarning("ScorePopup: Không tìm thấy sprite. " +
                             "Hãy gán sprite (+1/+2/+10) vào ScorePopupManager trong Inspector.");
            return null;
        }

        GameObject popupObj = new GameObject($"ScorePopup_+{score}");
        popupObj.transform.position = worldPosition;

        // Sprite gốc chỉ ~11x11 pixel (PPU=100) nên nhỏ xíu; nhân thêm hệ số phóng to
        // để luôn nhìn thấy rõ trong không gian 3D.
        float spriteWorldSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float sizeCompensation = spriteWorldSize > 0.0001f ? (1.2f / spriteWorldSize) : 1f;
        popupObj.transform.localScale = Vector3.one * scale * sizeCompensation;

        // Billboard ngay từ đầu để lần render đầu tiên đã đúng hướng camera
        Camera cam = Camera.main;
        if (cam != null)
        {
            popupObj.transform.rotation = cam.transform.rotation;
        }

        SpriteRenderer renderer = popupObj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 1000;

        // Đảm bảo hiệu ứng luôn render đè lên vật thể 3D (tránh bị tile che khuất)
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.renderQueue = 4000;

        ScorePopup popup = popupObj.AddComponent<ScorePopup>();
        popup.Play();

        return popup;
    }

    // =========================================================
    // ANIMATION PARAMETERS
    // =========================================================

    [Header("Motion")]
    [SerializeField] private float riseDistance = 1.1f;
    [SerializeField] private float lifetime = 1.0f;
    [SerializeField] private float popInDuration = 0.15f;

    [Header("Scale Punch")]
    [SerializeField] private float startScaleMultiplier = 0.6f;
    [SerializeField] private float overshootMultiplier = 1.25f;

    [Header("Fade")]
    [SerializeField] private float fadeStartNormalized = 0.55f;

    private SpriteRenderer spriteRenderer;
    private Camera targetCamera;
    private Vector3 baseScale;
    private Vector3 startPosition;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    private void Play()
    {
        // Ghi nhớ camera để billboard + bay lên theo hướng camera
        targetCamera = Camera.main;
        startPosition = transform.position;
        StartCoroutine(AnimateRoutine());
    }

    /// <summary>
    /// Xoay sprite luôn hướng mặt về phía camera (billboard).
    /// Sprite nằm trong mặt phẳng XY, hướng nhìn là +Z (transform.forward),
    /// nên chỉ cần gán cùng rotation với camera là mặt sprite luôn đối diện camera.
    /// </summary>
    private void FaceCamera()
    {
        if (targetCamera == null) return;
        transform.rotation = targetCamera.transform.rotation;
    }

    private IEnumerator AnimateRoutine()
    {
        float elapsed = 0f;

        // Billboard ngay từ khung hình đầu để trục local Y của sprite
        // trùng đúng trục "lên trên màn hình" của camera.
        FaceCamera();

        // Sau khi billboard, transform.up chính là hướng "lên" trên màn hình.
        // Lấy 1 lần duy nhất và giữ cố định (camera tĩnh trong lúc hiệu ứng chạy),
        // tránh tính lại mỗi khung hình.
        Vector3 screenUp = transform.up;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            // 0. Giữ sprite luôn đối diện camera (billboard)
            FaceCamera();

            // 1. Di chuyển bay lên theo trục Up CỦA CAMERA (thẳng đứng trên màn hình),
            //    không dùng Vector3.up của thế giới để tránh bị lệch khi camera nghiêng.
            float easeOut = 1f - Mathf.Pow(1f - t, 2.2f);
            transform.position = startPosition + screenUp * (riseDistance * easeOut);

            // 2. Scale: pop-in nhanh rồi overshoot nhẹ
            float scaleT = Mathf.Clamp01(elapsed / popInDuration);
            float popScale = Mathf.Lerp(startScaleMultiplier, overshootMultiplier, Mathf.Sin(scaleT * Mathf.PI * 0.5f));
            if (scaleT >= 1f)
            {
                // Sau khi overshoot, thu về đúng tỉ lệ
                float settleT = Mathf.Clamp01((elapsed - popInDuration) / 0.12f);
                popScale = Mathf.Lerp(overshootMultiplier, 1f, settleT);
            }
            transform.localScale = baseScale * popScale;

            // 3. Fade dần ở cuối đời
            if (spriteRenderer != null && t >= fadeStartNormalized)
            {
                float fadeT = Mathf.InverseLerp(fadeStartNormalized, 1f, t);
                Color color = spriteRenderer.color;
                color.a = 1f - fadeT;
                spriteRenderer.color = color;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}

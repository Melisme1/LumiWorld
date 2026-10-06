using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Giao diện World Space hiển thị Vòng tròn tiến độ sản xuất 360° (Circular Progress Ring)
/// và Bong bóng thu hoạch tài nguyên (Harvest Bubble) cho toàn bộ Cụm Biome (Leafwood, Bloomfield, Windheath).
/// Thiết kế kích thước nhỏ gọn (~0.32m), tinh tế, nổi phía trên ngọn cây, không che khuất cảnh quan.
/// </summary>
public class BiomeHarvestIndicator : MonoBehaviour, IPointerClickHandler
{
    [Header("Visual Config")]
    [Tooltip("Độ cao nhô lên phía trên mặt bằng ngọn cây (mét)")]
    [SerializeField] private float heightOffset = 1.60f;

    [Tooltip("Tỉ lệ kích thước World Space (nhỏ gọn ~0.34m, thẩm mỹ và không choán màn hình)")]
    [SerializeField] private Vector3 baseScale = new Vector3(0.0040f, 0.0040f, 0.0040f);

    private BiomeHarvestCluster cluster;
    private ResourceData resourceData;
    private Camera cachedCam;

    // UI Components
    private Canvas canvas;
    private RectTransform rootRect;
    private Image bgImage;
    private Image progressTrackImage; // Rãnh ranh giới vòng tròn tiến độ
    private Image progressRingImage;  // Vòng tiến độ chạy 360 độ
    private Image iconImage;
    private TextMeshProUGUI valueText;
    private TextMeshProUGUI subText;

    private bool isReady = false;
    private int currentReadyAmount = 0;
    private Coroutine popCoroutine;
    private Vector3 baseWorldCenter;

    // Static procedural sprite cache (siêu nhẹ, chỉ tạo 1 lần trong RAM)
    private static Sprite cachedCircleDiscSprite;
    private static Sprite cachedProgressRingSprite;
    private static Sprite cachedWoodSprite;
    private static Sprite cachedFlowerSprite;
    private static Sprite cachedHerbSprite;
    private static Sprite cachedDefaultResourceSprite;

    public void Initialize(BiomeHarvestCluster ownerCluster, ResourceData resource)
    {
        this.cluster = ownerCluster;
        this.resourceData = resource;
        this.cachedCam = Camera.main;

        BuildProceduralUI();
    }

    private void BuildProceduralUI()
    {
        EnsureSpritesCreated();

        // 1. Tạo Canvas World Space
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 300;

        gameObject.AddComponent<GraphicRaycaster>();

        rootRect = GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(90f, 90f);
        transform.localScale = baseScale;

        // 2. Nền vòng tròn (Dark Translucent Disc)
        GameObject bgObj = new GameObject("BackgroundDisc", typeof(RectTransform));
        bgObj.transform.SetParent(transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(84f, 84f);
        bgImage = bgObj.AddComponent<Image>();
        bgImage.sprite = cachedCircleDiscSprite;
        bgImage.color = new Color(0.07f, 0.10f, 0.17f, 0.88f); // Xanh đêm bóng bẩy, tương phản vừa vặn
        bgImage.raycastTarget = true;

        // 3. Rãnh vòng tròn tiến độ (Progress Track Groove) giúp nhìn rõ toàn bộ khuôn khổ tiến trình
        GameObject trackObj = new GameObject("ProgressTrack", typeof(RectTransform));
        trackObj.transform.SetParent(transform, false);
        RectTransform trackRect = trackObj.GetComponent<RectTransform>();
        trackRect.sizeDelta = new Vector2(84f, 84f);
        progressTrackImage = trackObj.AddComponent<Image>();
        progressTrackImage.sprite = cachedProgressRingSprite;
        progressTrackImage.type = Image.Type.Simple;
        progressTrackImage.color = new Color(1f, 1f, 1f, 0.15f); // Rãnh mờ định hình vòng tròn
        progressTrackImage.raycastTarget = false;

        // 4. Vòng tròn tiến độ chạy 360° (Active Radial Fill Ring)
        GameObject ringObj = new GameObject("ProgressRing", typeof(RectTransform));
        ringObj.transform.SetParent(transform, false);
        RectTransform ringRect = ringObj.GetComponent<RectTransform>();
        ringRect.sizeDelta = new Vector2(84f, 84f);
        progressRingImage = ringObj.AddComponent<Image>();
        progressRingImage.sprite = cachedProgressRingSprite;
        progressRingImage.type = Image.Type.Filled;
        progressRingImage.fillMethod = Image.FillMethod.Radial360;
        progressRingImage.fillOrigin = (int)Image.Origin360.Top;
        progressRingImage.fillClockwise = true;
        progressRingImage.fillAmount = 0f;
        Color ringColor = GetVibrantProgressColor(resourceData);
        progressRingImage.color = ringColor;
        progressRingImage.raycastTarget = false;

        // 5. Icon tài nguyên ở tâm (nổi bật, thay thế cho số giây hiển thị)
        GameObject iconObj = new GameObject("ResourceIcon", typeof(RectTransform));
        iconObj.transform.SetParent(transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(38f, 38f);
        iconRect.anchoredPosition = new Vector2(0f, 2f);
        iconImage = iconObj.AddComponent<Image>();
        iconImage.sprite = GetResourceSprite(resourceData);
        iconImage.color = Color.white;
        iconImage.enabled = true;
        iconImage.raycastTarget = false;

        // 6. Dòng chữ số lượng thu hoạch (chỉ hiện khi đã gặt xong hoặc số lượng lớn)
        GameObject valObj = new GameObject("ValueText", typeof(RectTransform));
        valObj.transform.SetParent(transform, false);
        RectTransform valRect = valObj.GetComponent<RectTransform>();
        valRect.sizeDelta = new Vector2(84f, 24f);
        valRect.anchoredPosition = new Vector2(0f, -8f);
        valueText = valObj.AddComponent<TextMeshProUGUI>();
        valueText.fontSize = 18f;
        valueText.fontStyle = FontStyles.Bold;
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.color = Color.white;
        valueText.raycastTarget = false;
        valueText.text = ""; // Mặc định không hiển thị thời gian giây

        // 7. Dòng nhãn phụ (sản lượng dự kiến hoặc chữ "GẶT")
        GameObject subObj = new GameObject("SubText", typeof(RectTransform));
        subObj.transform.SetParent(transform, false);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.sizeDelta = new Vector2(84f, 18f);
        subRect.anchoredPosition = new Vector2(0f, -22f);
        subText = subObj.AddComponent<TextMeshProUGUI>();
        subText.fontSize = 12f;
        subText.fontStyle = FontStyles.Bold;
        subText.alignment = TextAlignmentOptions.Center;
        subText.color = new Color(0.95f, 0.98f, 1f, 0.95f);
        subText.raycastTarget = false;
    }

    private void LateUpdate()
    {
        // Billboard: Luôn xoay mặt đối diện góc nhìn Camera
        if (cachedCam == null) cachedCam = Camera.main;
        if (cachedCam != null)
        {
            transform.rotation = cachedCam.transform.rotation;
        }

        // Hiệu ứng nhún nhảy bồng bềnh nhẹ nhàng khi Bong bóng sẵn sàng thu hoạch
        if (isReady && popCoroutine == null)
        {
            float bobY = Mathf.Sin(Time.time * 3.5f) * 0.05f;
            transform.position = baseWorldCenter + Vector3.up * bobY;

            float pulseScale = 1.0f + Mathf.Sin(Time.time * 4f) * 0.05f;
            transform.localScale = baseScale * 1.15f * pulseScale;
        }
    }

    /// <summary>
    /// Cập nhật tiến độ vòng tròn khi đang trong chu kỳ sản xuất:
    /// - Tiến trình nổi bật vừa phải nhờ rãnh định hình (Track) và màu sắc rực rỡ tươi sáng.
    /// - ĐÃ BỎ HIỂN THỊ THỜI GIAN GIÂY (24s, 18s...), thay vào đó tâm vòng tròn hiển thị Icon loại tài nguyên.
    /// - Vòng tròn 360° tự cuộn để biểu thị thời gian trực quan.
    /// </summary>
    public void SetProgress(float progress, int animalCount, int expectedYield, float remainingSeconds)
    {
        isReady = false;
        transform.localScale = baseScale;
        transform.position = baseWorldCenter;

        Color vibrantColor = GetVibrantProgressColor(resourceData);

        if (progressTrackImage != null)
        {
            // Rãnh vòng mờ giúp người chơi nhìn rõ tiến độ đã đi được bao nhiêu %
            progressTrackImage.enabled = true;
            progressTrackImage.color = new Color(1f, 1f, 1f, 0.16f);
        }

        if (progressRingImage != null)
        {
            progressRingImage.fillAmount = Mathf.Clamp01(progress);
            progressRingImage.color = vibrantColor;
        }

        if (bgImage != null)
        {
            bgImage.color = new Color(0.07f, 0.10f, 0.17f, 0.88f);
        }

        if (iconImage != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = GetResourceSprite(resourceData);
            iconImage.rectTransform.sizeDelta = new Vector2(38f, 38f);
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        }

        // Bỏ hiển thị thời gian giây: để trống valueText để tôn trọn Icon tài nguyên ở tâm
        if (valueText != null)
        {
            valueText.text = "";
        }

        // Nhãn phụ hiển thị tinh tế lượng sản lượng dự kiến
        if (subText != null)
        {
            subText.text = expectedYield > 0 ? $"<color=#FEF08A>+{expectedYield}</color>" : "";
            subText.fontSize = 12f;
            subText.rectTransform.anchoredPosition = new Vector2(0f, -22f);
        }
    }

    /// <summary>
    /// Chuyển sang trạng thái Bong bóng thu hoạch (Harvest Bubble) khi tiến độ đầy 100%
    /// </summary>
    public void SetHarvestReady(int totalAmount)
    {
        isReady = true;
        currentReadyAmount = totalAmount;

        if (progressTrackImage != null)
        {
            progressTrackImage.enabled = false;
        }

        if (progressRingImage != null)
        {
            progressRingImage.fillAmount = 1.0f;
            progressRingImage.color = Color.white;
        }

        if (bgImage != null)
        {
            Color bubbleColor = resourceData != null ? resourceData.resourceColor : new Color(0.2f, 0.8f, 0.4f);
            bgImage.color = new Color(bubbleColor.r * 0.9f, bubbleColor.g * 0.9f, bubbleColor.b * 0.9f, 0.96f);
        }

        if (iconImage != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = GetResourceSprite(resourceData);
            iconImage.rectTransform.sizeDelta = new Vector2(34f, 34f);
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, 12f);
        }

        if (valueText != null)
        {
            valueText.text = $"+{totalAmount}";
            valueText.color = Color.white;
            valueText.fontSize = 18f;
            valueText.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        }

        if (subText != null)
        {
            subText.text = "<color=#FEF08A>GẶT</color>";
            subText.fontSize = 10f;
            subText.rectTransform.anchoredPosition = new Vector2(0f, -24f);
        }

        // Kích hoạt animation Pop-In nảy nhẹ báo hiệu sẵn sàng
        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(BounceReadyRoutine());
    }

    /// <summary>
    /// Khi người chơi click chuột hoặc chạm vào Bong bóng thu hoạch
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        TryCollect();
    }

    public bool TryCollect()
    {
        if (!isReady || cluster == null) return false;

        if (popCoroutine != null) StopCoroutine(popCoroutine);
        StartCoroutine(PopAndCollectRoutine());
        return true;
    }

    private IEnumerator BounceReadyRoutine()
    {
        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.deltaTime;
            float factor = 1.0f + Mathf.Sin(t / 0.22f * Mathf.PI) * 0.25f;
            transform.localScale = baseScale * 1.15f * factor;
            yield return null;
        }
        transform.localScale = baseScale * 1.15f;
        popCoroutine = null;
    }

    private IEnumerator PopAndCollectRoutine()
    {
        // 1. Nảy to rồi thu nhỏ (Juice Burst)
        float t = 0f;
        while (t < 0.16f)
        {
            t += Time.deltaTime;
            float factor = Mathf.Lerp(1.3f, 0.2f, t / 0.16f);
            transform.localScale = baseScale * factor;
            yield return null;
        }

        // 2. Kích hoạt logic thu hoạch trên Cụm Biome
        cluster.CollectHarvest();

        isReady = false;
        transform.localScale = baseScale;
        transform.position = baseWorldCenter;
        popCoroutine = null;
    }

    public void UpdatePosition(Vector3 clusterCenter)
    {
        baseWorldCenter = clusterCenter + Vector3.up * heightOffset;
        transform.position = baseWorldCenter;
    }

    private static void EnsureSpritesCreated()
    {
        if (cachedCircleDiscSprite == null)
        {
            cachedCircleDiscSprite = CreateProceduralCircleSprite(128, false, 0f);
        }
        if (cachedProgressRingSprite == null)
        {
            // Tăng độ dày viền từ 0.82f -> 0.73f (~27% bề rộng) giúp tiến trình nổi bật, rõ nét vừa vặn
            cachedProgressRingSprite = CreateProceduralCircleSprite(128, true, 0.73f);
        }
    }

    private static Sprite CreateProceduralCircleSprite(int size, bool isRing, float innerRatio)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float outerRadius = center - 1.5f;
        float innerRadius = isRing ? (outerRadius * innerRatio) : 0f;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = 0f;
                if (isRing)
                {
                    float outerAlpha = Mathf.Clamp01(outerRadius - dist);
                    float innerAlpha = Mathf.Clamp01(dist - innerRadius);
                    alpha = Mathf.Min(outerAlpha, innerAlpha);
                }
                else
                {
                    alpha = Mathf.Clamp01(outerRadius - dist);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Lấy Sprite biểu tượng đại diện cho tài nguyên:
    /// 1. Ưu tiên số 1: Sử dụng resourceData.icon nếu đã được gán file hình trong Inspector.
    /// 2. Dự phòng thông minh: Tự động tạo Icon đồ họa chuẩn Procedural (Gỗ, Hoa tươi, Thảo mộc, v.v.)
    /// </summary>
    public static Sprite GetResourceSprite(ResourceData data)
    {
        if (data != null && data.icon != null)
        {
            return data.icon;
        }

        string resId = data != null ? data.resourceID.ToLowerInvariant() : "";
        if (resId.Contains("wood") || resId.Contains("go"))
        {
            if (cachedWoodSprite == null) cachedWoodSprite = CreateWoodIconSprite(64);
            return cachedWoodSprite;
        }
        if (resId.Contains("flower") || resId.Contains("hoa") || resId.Contains("bloom"))
        {
            if (cachedFlowerSprite == null) cachedFlowerSprite = CreateFlowerIconSprite(64);
            return cachedFlowerSprite;
        }
        if (resId.Contains("herb") || resId.Contains("thao") || resId.Contains("wind"))
        {
            if (cachedHerbSprite == null) cachedHerbSprite = CreateHerbIconSprite(64);
            return cachedHerbSprite;
        }

        if (cachedDefaultResourceSprite == null) cachedDefaultResourceSprite = CreateDefaultResourceSprite(64);
        return cachedDefaultResourceSprite;
    }

    /// <summary>
    /// Tính toán màu sắc rực rỡ, độ sáng cao cho thanh tiến độ:
    /// Đảm bảo nổi bật trên nền đĩa tối và cảnh quan thiên nhiên phía sau mà không bị chói quá mức.
    /// </summary>
    public static Color GetVibrantProgressColor(ResourceData res)
    {
        if (res == null) return new Color(0.98f, 0.76f, 0.22f, 1f);

        string resId = res.resourceID != null ? res.resourceID.ToLowerInvariant() : "";
        if (resId.Contains("wood") || resId.Contains("go"))
        {
            // Màu hổ phách vàng cam ấm rực rỡ (thay vì màu nâu đất tối)
            return new Color(0.98f, 0.64f, 0.14f, 1f);
        }
        if (resId.Contains("flower") || resId.Contains("hoa") || resId.Contains("bloom"))
        {
            // Màu hồng hoa anh đào tươi tắn
            return new Color(0.96f, 0.46f, 0.74f, 1f);
        }
        if (resId.Contains("herb") || resId.Contains("thao") || resId.Contains("wind"))
        {
            // Màu xanh ngọc lục bảo tươi mát
            return new Color(0.20f, 0.88f, 0.58f, 1f);
        }

        // Tự động tối ưu độ sáng HSV cho các loại tài nguyên khác trong tương lai
        Color baseCol = res.resourceColor;
        Color.RGBToHSV(baseCol, out float h, out float s, out float v);
        v = Mathf.Max(v, 0.95f);
        s = Mathf.Clamp(s, 0.70f, 0.90f);
        return Color.HSVToRGB(h, s, v);
    }

    private static Sprite CreateWoodIconSprite(int size = 64)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        Color barkColor = new Color(0.42f, 0.22f, 0.10f, 1f);      // Vỏ cây nâu sẫm
        Color woodColor = new Color(0.85f, 0.58f, 0.28f, 1f);      // Thịt gỗ vàng cam ấm
        Color ring1Color = new Color(0.72f, 0.44f, 0.18f, 1f);     // Vòng năm tuổi
        Color ring2Color = new Color(0.65f, 0.38f, 0.15f, 1f);
        Color coreColor = new Color(0.48f, 0.26f, 0.11f, 1f);      // Lõi gỗ trung tâm
        Color sproutColor = new Color(0.32f, 0.82f, 0.38f, 1f);    // Mầm lá xanh non

        float barkRadius = size * 0.38f;
        float woodRadius = size * 0.31f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                Color col = Color.clear;

                // 1. Thân khúc gỗ tròn
                if (dist <= barkRadius + 1f)
                {
                    float alpha = Mathf.Clamp01(barkRadius + 1f - dist);
                    if (dist > woodRadius)
                    {
                        col = barkColor;
                    }
                    else
                    {
                        col = woodColor;

                        // Vân gỗ 1
                        float r1Dist = Mathf.Abs(dist - size * 0.22f);
                        if (r1Dist < 1.4f)
                        {
                            col = Color.Lerp(ring1Color, col, r1Dist / 1.4f);
                        }

                        // Vân gỗ 2
                        float r2Dist = Mathf.Abs(dist - size * 0.13f);
                        if (r2Dist < 1.2f)
                        {
                            col = Color.Lerp(ring2Color, col, r2Dist / 1.2f);
                        }

                        // Lõi tâm
                        if (dist <= size * 0.05f)
                        {
                            col = Color.Lerp(coreColor, col, dist / (size * 0.05f));
                        }
                    }
                    col.a *= alpha;
                }

                // 2. Chồi mầm lá non nhô ra góc trên phải
                float sproutDx = dx - size * 0.26f;
                float sproutDy = dy - size * 0.28f;
                float sproutDist = Mathf.Sqrt(sproutDx * sproutDx * 1.5f + sproutDy * sproutDy);
                if (sproutDist <= size * 0.09f)
                {
                    float sproutAlpha = Mathf.Clamp01(size * 0.09f - sproutDist);
                    Color sp = sproutColor;
                    sp.a = sproutAlpha;
                    col = Color.Lerp(col, sp, sproutAlpha);
                }

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateFlowerIconSprite(int size = 64)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        Color petalColor = new Color(0.96f, 0.45f, 0.70f, 1f);     // Hồng phấn tươi tắn
        Color petalRimColor = new Color(0.99f, 0.72f, 0.86f, 1f);  // Viền cánh hoa sáng
        Color coreColor = new Color(0.98f, 0.82f, 0.16f, 1f);       // Nhụy vàng rực rỡ
        Color coreBorderColor = new Color(0.92f, 0.65f, 0.08f, 1f);

        float petalDistFromCenter = size * 0.18f;
        float petalRadius = size * 0.16f;
        float coreRadius = size * 0.12f;

        Vector2[] petalCenters = new Vector2[5];
        for (int i = 0; i < 5; i++)
        {
            float angle = (i * 72f - 90f) * Mathf.Deg2Rad;
            petalCenters[i] = new Vector2(
                center + Mathf.Cos(angle) * petalDistFromCenter,
                center + Mathf.Sin(angle) * petalDistFromCenter
            );
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                float distToCenter = Vector2.Distance(pt, new Vector2(center, center));

                Color col = Color.clear;

                float minPetalDist = float.MaxValue;
                for (int i = 0; i < 5; i++)
                {
                    float d = Vector2.Distance(pt, petalCenters[i]);
                    if (d < minPetalDist) minPetalDist = d;
                }

                if (minPetalDist <= petalRadius + 1f)
                {
                    float petalAlpha = Mathf.Clamp01(petalRadius + 1f - minPetalDist);
                    Color pCol = Color.Lerp(petalRimColor, petalColor, Mathf.Clamp01(minPetalDist / petalRadius));
                    pCol.a = petalAlpha;
                    col = pCol;
                }

                if (distToCenter <= coreRadius + 1f)
                {
                    float coreAlpha = Mathf.Clamp01(coreRadius + 1f - distToCenter);
                    Color cCol = Color.Lerp(coreColor, coreBorderColor, Mathf.Clamp01(distToCenter / coreRadius));
                    cCol.a = coreAlpha;
                    col = Color.Lerp(col, cCol, coreAlpha);
                }

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateHerbIconSprite(int size = 64)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        Color leafMainColor = new Color(0.20f, 0.82f, 0.55f, 1f);   // Xanh ngọc thảo mộc
        Color leafTipColor = new Color(0.48f, 0.95f, 0.72f, 1f);    // Đỉnh ngọn sáng
        Color leafSubColor = new Color(0.12f, 0.70f, 0.44f, 1f);    // Lá phụ xanh thẫm
        Color stemColor = new Color(0.08f, 0.52f, 0.30f, 1f);       // Cuống nhánh

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;

                Color col = Color.clear;

                // Lá chính chéo 45 độ
                float r45_x = (dx + dy) * 0.7071f;
                float r45_y = (-dx + dy) * 0.7071f;
                float leaf1_d = (r45_x * r45_x) / ((size * 0.12f) * (size * 0.12f)) +
                                (r45_y * r45_y) / ((size * 0.28f) * (size * 0.28f));
                if (leaf1_d <= 1f)
                {
                    float a = Mathf.Clamp01((1f - leaf1_d) * 6f);
                    Color lc = Color.Lerp(leafMainColor, leafTipColor, Mathf.Clamp01((r45_y + size * 0.1f) / (size * 0.3f)));
                    lc.a = a;
                    col = lc;
                }

                // Lá phụ xoay -40 độ
                float shiftX = dx + size * 0.10f;
                float shiftY = dy + size * 0.04f;
                float r_neg40_x = (shiftX * 0.766f - shiftY * 0.643f);
                float r_neg40_y = (shiftX * 0.643f + shiftY * 0.766f);
                float leaf2_d = (r_neg40_x * r_neg40_x) / ((size * 0.09f) * (size * 0.09f)) +
                                (r_neg40_y * r_neg40_y) / ((size * 0.20f) * (size * 0.20f));
                if (leaf2_d <= 1f)
                {
                    float a = Mathf.Clamp01((1f - leaf2_d) * 6f);
                    Color lc = leafSubColor;
                    lc.a = a;
                    col = Color.Lerp(col, lc, a);
                }

                // Cuống nhánh
                if (dx >= -size * 0.06f && dx <= size * 0.04f && dy >= -size * 0.30f && dy <= -size * 0.05f)
                {
                    float stemDist = Mathf.Abs(dx + dy * 0.15f);
                    if (stemDist <= size * 0.035f)
                    {
                        float a = Mathf.Clamp01((size * 0.035f - stemDist) * 3f);
                        Color sc = stemColor;
                        sc.a = a;
                        col = Color.Lerp(col, sc, a);
                    }
                }

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateDefaultResourceSprite(int size = 64)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        Color gemBase = new Color(0.9f, 0.95f, 1f, 1f);
        Color gemShade = new Color(0.7f, 0.8f, 0.95f, 1f);
        Color gemHighlight = Color.white;

        float r = size * 0.32f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center + 0.5f);
                float dy = Mathf.Abs(y - center + 0.5f);

                float d = (dx + dy) / r;
                Color col = Color.clear;
                if (d <= 1f)
                {
                    float a = Mathf.Clamp01((1f - d) * 5f);
                    Color c = (y > center) ? Color.Lerp(gemBase, gemHighlight, (y - center) / r) : Color.Lerp(gemBase, gemShade, (center - y) / r);
                    c.a = a;
                    col = c;
                }
                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}

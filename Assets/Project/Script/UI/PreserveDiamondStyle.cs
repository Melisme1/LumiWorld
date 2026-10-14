using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// Vị trí góc hình thoi tiếp giáp với ô tròn số ở tâm huy hiệu Habitat
/// </summary>
public enum HabitatSlotCorner
{
    None,
    Bottom, // Slot 12h (Top resident): Cắt vòng cung ở góc dưới tiếp giáp ô tròn số
    Left,   // Slot 3h (Right resident): Cắt vòng cung ở góc trái tiếp giáp ô tròn số
    Top     // Slot 6h (Bottom resident): Cắt vòng cung ở góc trên tiếp giáp ô tròn số
}

/// <summary>
/// Bộ sinh Texture và Sprite hình thoi (Diamond / Rhombus) chuẩn phong cách mỹ thuật game Preserve.
/// Tự sinh bằng toán học vector, đảm bảo độ sắc nét cao (Crisp 256x256), chống răng cưa và bo góc nhẹ thanh lịch.
/// </summary>
public static class PreserveDiamondStyle
{
    private static Sprite diamondBaseSprite;
    private static Sprite diamondOutlineSprite;
    private static Sprite diamondGlowSprite;
    private static Sprite lockIconSprite;
    private static Sprite leafIconSprite;
    private static Sprite coinMiniSprite;
    private static Sprite starMiniSprite;

    private static Sprite creamDiamondSprite;
    private static Sprite greenSubDiamondSprite;
    private static Sprite pineTreeSprite;
    private static Sprite flowerBiomeSprite;
    private static Sprite mountainBiomeSprite;
    private static Sprite waterBiomeSprite;
    private static Sprite lockWhiteSprite;
    private static Sprite creatureHeadSprite;
    private static Sprite image2TemplateSprite;
    private static readonly Dictionary<string, Sprite> habitatBadgeCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite> diamondAvatarCache = new Dictionary<string, Sprite>();

    /// <summary>
    /// Cắt và chuyển đổi ảnh thẻ thú (tarot card) thành Avatar hình thoi sắc nét vừa khít khung badge.
    /// Có tùy chọn bo tròn / cắt vòng cung ôm khít ô tròn số ở giữa theo góc tiếp xúc tương ứng.
    /// </summary>
    public static Sprite GetDiamondAvatar(Sprite cardSprite, HabitatSlotCorner corner = HabitatSlotCorner.Bottom)
    {
        if (cardSprite == null) return null;

        string spriteId = !string.IsNullOrEmpty(cardSprite.name) ? cardSprite.name : cardSprite.GetHashCode().ToString();
        string cacheKey = $"{spriteId}_{(int)corner}";
        if (diamondAvatarCache.TryGetValue(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        // 1. Kiểm tra trong Resources/CreatureAvatars theo tên sprite hoặc tên texture (nếu không cần cắt góc)
        if (corner == HabitatSlotCorner.None)
        {
            string spriteName = cardSprite.name;
            Sprite prebaked = Resources.Load<Sprite>("CreatureAvatars/" + spriteName);
            if (prebaked == null && cardSprite.texture != null)
            {
                prebaked = Resources.Load<Sprite>("CreatureAvatars/" + cardSprite.texture.name);
            }

            if (prebaked != null)
            {
                diamondAvatarCache[cacheKey] = prebaked;
                return prebaked;
            }
        }

        // 2. Tự sinh dynamically qua RenderTexture (hoạt động với cả texture không readable)
        Sprite generated = GenerateDiamondAvatarRuntime(cardSprite, corner);
        if (generated != null)
        {
            diamondAvatarCache[cacheKey] = generated;
            return generated;
        }

        return cardSprite;
    }

    private static Sprite GenerateDiamondAvatarRuntime(Sprite cardSprite, HabitatSlotCorner corner = HabitatSlotCorner.Bottom)
    {
        if (cardSprite == null || cardSprite.texture == null) return null;

        Texture2D srcTex = cardSprite.texture;
        const int targetSize = 256;

        Rect spriteRect = cardSprite.rect;
        float cropSize = spriteRect.width * 0.94f;
        float cropX = spriteRect.x + (spriteRect.width - cropSize) * 0.5f;
        float cropY = spriteRect.y + spriteRect.height * (1f - 0.12f) - cropSize;

        RenderTexture rt = RenderTexture.GetTemporary(targetSize, targetSize, 0, RenderTextureFormat.ARGB32);
        RenderTexture prevRT = RenderTexture.active;

        Vector2 scale = new Vector2(cropSize / srcTex.width, cropSize / srcTex.height);
        Vector2 offset = new Vector2(cropX / srcTex.width, cropY / srcTex.height);
        Graphics.Blit(srcTex, rt, scale, offset);

        RenderTexture.active = rt;
        Texture2D readTex = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false);
        readTex.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0);
        readTex.Apply();

        RenderTexture.active = prevRT;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = readTex.GetPixels();
        Vector2 center = new Vector2(targetSize * 0.5f, targetSize * 0.5f);
        float half = targetSize * 0.5f;

        // Căn chỉnh hình học vòng cung bo tròn ôm khít viền ô tròn số ở giữa huy hiệu
        // Phân tách chuẩn xác bán kính và vị trí theo trục X và trục Y để ôm khít 100% không bị hở:
        // - Trục Y (Top 12h & Bottom 6h): Bán kính vòng tròn ở tâm badge là 10.6 đơn vị, slot ở vị trí 24.0 đơn vị, kích thước 43.5 đơn vị
        // - Trục X (Right 3h): Bán kính vòng tròn ở tâm badge là 12.2 đơn vị, slot ở vị trí 24.5 đơn vị, kích thước 44.5 đơn vị
        float slotSize = (corner == HabitatSlotCorner.Left) ? 44.5f : 43.5f;
        float slotPos = (corner == HabitatSlotCorner.Left) ? 24.5f : 24.0f;
        float circleRadiusUnits = (corner == HabitatSlotCorner.Left) ? 12.2f : 10.6f;

        float scaleFactor = targetSize / slotSize;
        float distCenterToCircle = slotPos * scaleFactor;
        float circleRadius = circleRadiusUnits * scaleFactor;

        Vector2 circleCenter = Vector2.zero;
        bool hasCutout = corner != HabitatSlotCorner.None;
        switch (corner)
        {
            case HabitatSlotCorner.Bottom:
                circleCenter = new Vector2(center.x, center.y - distCenterToCircle);
                break;
            case HabitatSlotCorner.Left:
                circleCenter = new Vector2(center.x - distCenterToCircle, center.y);
                break;
            case HabitatSlotCorner.Top:
                circleCenter = new Vector2(center.x, center.y + distCenterToCircle);
                break;
            default:
                hasCutout = false;
                break;
        }

        for (int y = 0; y < targetSize; y++)
        {
            for (int x = 0; x < targetSize; x++)
            {
                int idx = y * targetSize + x;
                float dx = Mathf.Abs(x + 0.5f - center.x);
                float dy = Mathf.Abs(y + 0.5f - center.y);
                float normDist = (dx / half) + (dy / half);
                // Hệ số 0.982f giúp hình thoi nằm gọn ghẽ 100% bên trong khung viền, không bị lem tràn ra ngoài
                float distPixels = (0.982f - normDist) * half;

                if (distPixels < 0f)
                {
                    pixels[idx] = Color.clear;
                    continue;
                }

                float alpha = 1f;
                if (distPixels < 1.5f)
                {
                    alpha = Mathf.Clamp01(distPixels / 1.5f);
                }

                // Cắt vòng cung mềm mại cho riêng góc tiếp giáp với ô tròn số
                if (hasCutout)
                {
                    float distToCircle = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), circleCenter);
                    float arcDist = distToCircle - circleRadius;
                    if (arcDist < 0f)
                    {
                        pixels[idx] = Color.clear;
                        continue;
                    }
                    else if (arcDist < 1.5f)
                    {
                        alpha *= Mathf.Clamp01(arcDist / 1.5f);
                    }
                }

                pixels[idx].a *= alpha;
            }
        }

        readTex.SetPixels(pixels);
        readTex.Apply();
        readTex.wrapMode = TextureWrapMode.Clamp;
        readTex.filterMode = FilterMode.Bilinear;

        return Sprite.Create(readTex, new Rect(0, 0, targetSize, targetSize), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Lấy ảnh nền huy hiệu hình thoi tương ứng theo từng hệ môi trường (7 file gốc đã cắt sạch nền caro)
    /// </summary>
    public static Sprite GetHabitatBadgeSprite(BiomeAffinity affinity)
    {
        string name = affinity.ToString();
        if (habitatBadgeCache.TryGetValue(name, out var cached) && cached != null)
        {
            return cached;
        }

        Sprite sp = Resources.Load<Sprite>("HabitatBadges/" + name);
        if (sp == null)
        {
            sp = Image2Template;
        }

        if (sp != null)
        {
            habitatBadgeCache[name] = sp;
        }
        return sp;
    }

    /// <summary>
    /// Sprite mẫu huy hiệu hình thoi được cắt trực tiếp từ Ảnh 2 của Preserve
    /// </summary>
    public static Sprite Image2Template
    {
        get
        {
            if (image2TemplateSprite == null)
            {
                image2TemplateSprite = Resources.Load<Sprite>("PreserveHabitatDiamondTemplate");
                if (image2TemplateSprite == null)
                {
                    image2TemplateSprite = CreamDiamondBase;
                }
            }
            return image2TemplateSprite;
        }
    }
    public static Sprite CreamDiamondBase
    {
        get
        {
            if (creamDiamondSprite == null)
            {
                creamDiamondSprite = CreateProceduralDiamondSprite(fill: true, outlineWidth: 5, new Color(0.955f, 0.965f, 0.92f, 0.98f), new Color(0.24f, 0.44f, 0.17f, 0.90f));
            }
            return creamDiamondSprite;
        }
    }

    /// <summary>
    /// Ô hình thoi phụ màu xanh rêu lá (#3E722C) cho các góc trong Ảnh 3
    /// </summary>
    public static Sprite GreenSubDiamond
    {
        get
        {
            if (greenSubDiamondSprite == null)
            {
                greenSubDiamondSprite = CreateProceduralDiamondSprite(fill: true, outlineWidth: 3, new Color(0.24f, 0.44f, 0.17f, 1f), new Color(0.38f, 0.62f, 0.28f, 1f));
            }
            return greenSubDiamondSprite;
        }
    }

    /// <summary>
    /// Biểu tượng Cây Thông (Pine Tree) xanh chuẩn Preserve cho môi trường Rừng (Forest/Leafwood)
    /// </summary>
    public static Sprite PineTreeSprite
    {
        get
        {
            if (pineTreeSprite == null) pineTreeSprite = CreateProceduralPineTreeSprite();
            return pineTreeSprite;
        }
    }

    /// <summary>
    /// Biểu tượng Bông Hoa cho môi trường Thung lũng (Bloomfield)
    /// </summary>
    public static Sprite FlowerBiomeSprite
    {
        get
        {
            if (flowerBiomeSprite == null) flowerBiomeSprite = CreateProceduralFlowerSprite();
            return flowerBiomeSprite;
        }
    }

    /// <summary>
    /// Biểu tượng Đỉnh Núi cho môi trường Đỉnh cao (Windheath)
    /// </summary>
    public static Sprite MountainBiomeSprite
    {
        get
        {
            if (mountainBiomeSprite == null) mountainBiomeSprite = CreateProceduralMountainSprite();
            return mountainBiomeSprite;
        }
    }

    /// <summary>
    /// Biểu tượng Sóng Nước cho môi trường Nước (Reedmarsh/Lilywater)
    /// </summary>
    public static Sprite WaterBiomeSprite
    {
        get
        {
            if (waterBiomeSprite == null) waterBiomeSprite = CreateProceduralWaterSprite();
            return waterBiomeSprite;
        }
    }

    /// <summary>
    /// Biểu tượng Ổ Khóa Màu Trắng (Lock Icon) trên nền xanh trong ô dưới cùng của Ảnh 3
    /// </summary>
    public static Sprite LockWhiteSprite
    {
        get
        {
            if (lockWhiteSprite == null)
            {
                lockWhiteSprite = Resources.Load<Sprite>("PreserveLockIcon");
                if (lockWhiteSprite == null) lockWhiteSprite = CreateProceduralLockWhiteSprite();
            }
            return lockWhiteSprite;
        }
    }

    /// <summary>
    /// Biểu tượng Hình đầu sinh vật màu kem (Creature Portrait Silhouette) cho các slot cư dân
    /// </summary>
    public static Sprite CreatureHeadSprite
    {
        get
        {
            if (creatureHeadSprite == null) creatureHeadSprite = CreateProceduralCreatureHeadSprite();
            return creatureHeadSprite;
        }
    }

    /// <summary>
    /// Sprite hình thoi nền tối sang trọng với viền mờ (Filled Diamond)
    /// </summary>
    public static Sprite DiamondBase
    {
        get
        {
            if (diamondBaseSprite == null)
            {
                diamondBaseSprite = CreateProceduralDiamondSprite(fill: true, outlineWidth: 6, new Color(0.08f, 0.12f, 0.15f, 0.90f), new Color(0.40f, 0.75f, 0.55f, 0.95f));
            }
            return diamondBaseSprite;
        }
    }

    /// <summary>
    /// Sprite viền hình thoi sáng (Diamond Outline)
    /// </summary>
    public static Sprite DiamondOutline
    {
        get
        {
            if (diamondOutlineSprite == null)
            {
                diamondOutlineSprite = CreateProceduralDiamondSprite(fill: false, outlineWidth: 8, Color.clear, new Color(0.45f, 0.85f, 0.60f, 1f));
            }
            return diamondOutlineSprite;
        }
    }

    /// <summary>
    /// Sprite hào quang phát sáng hình thoi (Glow)
    /// </summary>
    public static Sprite DiamondGlow
    {
        get
        {
            if (diamondGlowSprite == null)
            {
                diamondGlowSprite = CreateProceduralGlowSprite(new Color(0.35f, 0.95f, 0.60f, 0.45f));
            }
            return diamondGlowSprite;
        }
    }

    /// <summary>
    /// Sprite ổ khóa (Lock Icon) cho các slot hoặc thẻ bị khóa
    /// </summary>
    public static Sprite LockIcon
    {
        get
        {
            if (lockIconSprite == null)
            {
                lockIconSprite = CreateProceduralLockSprite();
            }
            return lockIconSprite;
        }
    }

    /// <summary>
    /// Sprite biểu tượng lá cây / Harmony
    /// </summary>
    public static Sprite LeafIcon
    {
        get
        {
            if (leafIconSprite == null)
            {
                leafIconSprite = CreateProceduralLeafSprite();
            }
            return leafIconSprite;
        }
    }

    private static Sprite CreateProceduralDiamondSprite(bool fill, int outlineWidth, Color fillColor, Color outlineColor)
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.44f;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center.x);
                float dy = Mathf.Abs(y - center.y);

                // Khoảng cách Manhattan = hình thoi xoay 45 độ
                float manhattanDist = (dx + dy) / 1.4142f;
                float diff = radius - manhattanDist;

                Color c = Color.clear;

                if (diff >= 0)
                {
                    if (diff < outlineWidth)
                    {
                        float t = Mathf.Clamp01(diff / 2.0f);
                        c = outlineColor * t;
                    }
                    else if (fill)
                    {
                        c = fillColor;
                    }
                }
                else if (diff > -2.0f)
                {
                    // Antialiasing viền ngoài
                    float alpha = Mathf.Clamp01(1.0f + diff * 0.5f);
                    c = outlineColor * alpha;
                }

                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateProceduralGlowSprite(Color glowColor)
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.48f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center.x);
                float dy = Mathf.Abs(y - center.y);
                float manhattanDist = (dx + dy) / 1.4142f;

                float factor = Mathf.Clamp01(1.0f - (manhattanDist / maxDist));
                factor = Mathf.Pow(factor, 2.2f);

                pixels[y * size + x] = glowColor * factor;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateProceduralLockSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(32, 32);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = Color.clear;

                // Thân khóa: hình chữ nhật góc bo ở nửa dưới
                if (x >= 18 && x <= 46 && y >= 10 && y <= 32)
                {
                    c = new Color(0.95f, 0.80f, 0.30f, 1f); // Màu vàng đồng
                    // Lỗ khóa nhỏ
                    if (x >= 30 && x <= 34 && y >= 18 && y <= 25)
                    {
                        c = new Color(0.20f, 0.15f, 0.05f, 1f);
                    }
                }

                // Càng khóa: vòng cung ở nửa trên
                float distShackle = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                if (y >= 28 && distShackle >= 12f && distShackle <= 17f && x >= 20 && x <= 44)
                {
                    c = new Color(0.85f, 0.85f, 0.90f, 1f); // Màu bạc kim loại
                }

                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateProceduralLeafSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - 32f) / 30f;
                float ny = (y - 10f) / 48f;

                // Phương trình dáng lá mầm sinh thái
                if (ny >= 0f && ny <= 1f)
                {
                    float widthAtY = Mathf.Sin(ny * Mathf.PI) * 0.65f;
                    if (Mathf.Abs(nx) <= widthAtY)
                    {
                        // Gradient từ xanh ngọc đậm lên xanh non phát sáng
                        Color leafColor = Color.Lerp(new Color(0.25f, 0.75f, 0.45f, 1f), new Color(0.55f, 0.95f, 0.65f, 1f), ny);
                        // Gân lá chính giữa
                        if (Mathf.Abs(nx) < 0.06f) leafColor = new Color(0.85f, 1f, 0.80f, 1f);
                        pixels[y * size + x] = leafColor;
                        continue;
                    }
                }
                pixels[y * size + x] = Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateProceduralPineTreeSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Color foliageColor = new Color(0.24f, 0.44f, 0.17f, 1f); // Deep pine green
        Color trunkColor = new Color(0.38f, 0.28f, 0.16f, 1f);   // Wood trunk

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = Color.clear;
                float nx = Mathf.Abs(x - 64f);

                // Thân cây: y từ 10 đến 30
                if (y >= 10 && y <= 30 && nx <= 6f)
                {
                    c = trunkColor;
                }
                // Tầng lá dưới: y 28 đến 62
                else if (y >= 28 && y <= 62)
                {
                    float width = (62f - y) * 1.05f + 6f;
                    if (nx <= width) c = foliageColor;
                }
                // Tầng lá giữa: y 56 đến 90
                else if (y >= 56 && y <= 90)
                {
                    float width = (90f - y) * 0.95f + 4f;
                    if (nx <= width) c = foliageColor;
                }
                // Tầng lá chóp: y 82 đến 118
                else if (y >= 82 && y <= 118)
                {
                    float width = (118f - y) * 0.75f + 1f;
                    if (nx <= width) c = foliageColor;
                }

                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateProceduralFlowerSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(64f, 64f);
        Color petalColor = new Color(0.88f, 0.42f, 0.65f, 1f);
        Color coreColor = new Color(0.98f, 0.82f, 0.25f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);
                Color c = Color.clear;

                if (dist <= 14f)
                {
                    c = coreColor;
                }
                else
                {
                    float angle = Mathf.Atan2(y - center.y, x - center.x);
                    float petalDist = 32f + Mathf.Cos(angle * 5f) * 12f;
                    if (dist <= petalDist)
                    {
                        c = petalColor;
                    }
                }
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateProceduralMountainSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Color rockColor = new Color(0.50f, 0.60f, 0.68f, 1f);
        Color snowColor = new Color(0.92f, 0.95f, 0.98f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = Color.clear;
                float h1 = Mathf.Max(0f, 96f - Mathf.Abs(x - 52f) * 1.5f);
                float h2 = Mathf.Max(0f, 78f - Mathf.Abs(x - 88f) * 1.4f);
                float mountainTop = Mathf.Max(h1, h2);

                if (y >= 20 && y <= mountainTop)
                {
                    c = (y > 75) ? snowColor : rockColor;
                }
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateProceduralWaterSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Color waveColor = new Color(0.25f, 0.65f, 0.88f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = Color.clear;
                float wave1 = 68f + Mathf.Sin(x * 0.15f) * 8f;
                float wave2 = 42f + Mathf.Sin((x + 20f) * 0.15f) * 8f;

                if (Mathf.Abs(y - wave1) <= 6f && x >= 18 && x <= 110) c = waveColor;
                else if (Mathf.Abs(y - wave2) <= 6f && x >= 18 && x <= 110) c = waveColor;

                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateProceduralLockWhiteSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Color white = Color.white;
        Color keyhole = new Color(0.24f, 0.44f, 0.17f, 1f); // Cắt vào nền xanh

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = Color.clear;

                // Thân khóa: hình chữ nhật x=18..46, y=10..32
                if (x >= 18 && x <= 46 && y >= 10 && y <= 32)
                {
                    c = white;
                }

                // Càng khóa hình vòm ở nửa trên
                float distShackle = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                if (y >= 28 && distShackle >= 10f && distShackle <= 15f && x >= 20 && x <= 44)
                {
                    c = white;
                }

                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateProceduralCreatureHeadSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Color headColor = new Color(0.96f, 0.97f, 0.93f, 1f); // Bóng đầu thú màu kem

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = Color.clear;
                Vector2 pos = new Vector2(x, y);

                // Đầu hình oval
                float headDist = Vector2.Distance(new Vector2(x, (y - 28f) * 1.15f + 28f), new Vector2(32, 28));
                if (headDist <= 14f)
                {
                    c = headColor;
                }

                // Tai trái
                if (Vector2.Distance(pos, new Vector2(23, 44)) <= 5.5f) c = headColor;
                // Tai phải
                if (Vector2.Distance(pos, new Vector2(41, 44)) <= 5.5f) c = headColor;

                // Mõm dưới
                if (Vector2.Distance(pos, new Vector2(32, 19)) <= 6.5f) c = headColor;

                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

using System;
using UnityEngine;

/// <summary>
/// Quản lý và sinh thủ tục (Procedural Generation) toàn bộ Texture/Sprite 2D/3D Cozy
/// cho Minigame Vũ Điệu Hòa Nhịp (Rhythm Minigame), tối ưu hóa bộ nhớ và hiển thị sắc nét.
/// </summary>
public static class RhythmProceduralSprites
{
    private static Sprite woodBoardSpriteCache;
    private static Sprite jellyLeftSpriteCache;
    private static Sprite jellyUpSpriteCache;
    private static Sprite jellyDownSpriteCache;
    private static Sprite jellyRightSpriteCache;
    private static Sprite jellyCorrectSpriteCache;
    private static Sprite stoneSpaceSpriteCache;
    private static Sprite capsuleBarSpriteCache;
    private static Sprite glossyFillSpriteCache;
    private static Sprite pearlOrbSpriteCache;
    private static Sprite targetStarburstSpriteCache;
    private static Sprite bigPillBtnSpriteCache;
    private static Sprite greenBadgeSpriteCache;
    private static Sprite redCloseBtnSpriteCache;
    private static Sprite cornerFloraSpriteCache;
    private static Sprite ringSpriteCache;
    private static Sprite whiteSpriteCache;
    private static Sprite sparkleStarSpriteCache;
    private static Sprite heartSpriteCache;
    private static Sprite sunburstSpriteCache;
    private static Sprite ivyGarlandSpriteCache;
    private static Sprite sideIvyLeftSpriteCache;
    private static Sprite sideIvyRightSpriteCache;
    private static Sprite floraLeftSpriteCache;
    private static Sprite floraRightSpriteCache;
    private static Sprite barWingSpriteCache;
    private static Sprite goldBeadSpriteCache;
    private static Sprite clockIconSpriteCache;

    public static Sprite WoodBoard => woodBoardSpriteCache ??= CreateWoodBoardSprite();
    public static Sprite JellyLeft => jellyLeftSpriteCache ??= CreateJellyButtonSprite(new Color(0.95f, 0.35f, 0.40f, 1f), new Color(0.68f, 0.18f, 0.22f, 1f), RhythmDirection.Left, false);
    public static Sprite JellyUp => jellyUpSpriteCache ??= CreateJellyButtonSprite(new Color(0.98f, 0.80f, 0.20f, 1f), new Color(0.72f, 0.52f, 0.10f, 1f), RhythmDirection.Up, false);
    public static Sprite JellyDown => jellyDownSpriteCache ??= CreateJellyButtonSprite(new Color(0.96f, 0.56f, 0.22f, 1f), new Color(0.72f, 0.34f, 0.12f, 1f), RhythmDirection.Down, false);
    public static Sprite JellyRight => jellyRightSpriteCache ??= CreateJellyButtonSprite(new Color(0.24f, 0.72f, 0.90f, 1f), new Color(0.12f, 0.46f, 0.65f, 1f), RhythmDirection.Right, false);
    public static Sprite JellyCorrect => jellyCorrectSpriteCache ??= CreateJellyButtonSprite(new Color(0.28f, 0.78f, 0.38f, 1f), new Color(0.14f, 0.48f, 0.20f, 1f), null, true);
    public static Sprite StoneSpace => stoneSpaceSpriteCache ??= CreateStoneSpaceSprite();
    public static Sprite CapsuleBar => capsuleBarSpriteCache ??= CreateCapsuleBarSprite();
    public static Sprite GlossyFill => glossyFillSpriteCache ??= CreateGlossyFillSprite();
    public static Sprite PearlOrb => pearlOrbSpriteCache ??= CreatePearlOrbSprite();
    public static Sprite TargetStarburst => targetStarburstSpriteCache ??= CreateLuminousHitTargetSprite();
    public static Sprite BigPillBtn => bigPillBtnSpriteCache ??= CreateBigPillButtonSprite();
    public static Sprite GreenBadge => greenBadgeSpriteCache ??= CreateGreenBadgeSprite();
    public static Sprite RedCloseBtn => redCloseBtnSpriteCache ??= CreateRedCloseButtonSprite();
    public static Sprite CornerFlora => cornerFloraSpriteCache ??= CreateCornerFloraSprite();
    public static Sprite Ring => ringSpriteCache ??= CreateRingSprite();
    public static Sprite White => whiteSpriteCache ??= GetWhiteSprite();
    public static Sprite SparkleStar => sparkleStarSpriteCache ??= CreateSparkleStarSprite();
    public static Sprite Heart => heartSpriteCache ??= CreateHeartSprite();
    public static Sprite Sunburst => sunburstSpriteCache ??= CreateSunburstSprite();
    public static Sprite IvyGarland => ivyGarlandSpriteCache ??= CreateIvyGarlandSprite();
    public static Sprite SideIvyLeft => sideIvyLeftSpriteCache ??= CreateSideIvySprite(false);
    public static Sprite SideIvyRight => sideIvyRightSpriteCache ??= CreateSideIvySprite(true);
    public static Sprite FloraLeft => floraLeftSpriteCache ??= CreateFloraBouquetSprite(false);
    public static Sprite FloraRight => floraRightSpriteCache ??= CreateFloraBouquetSprite(true);
    public static Sprite BarWing => barWingSpriteCache ??= CreateBarVineWingSprite();
    public static Sprite GoldBead => goldBeadSpriteCache ??= CreateGoldBeadSprite();
    public static Sprite ClockIcon => clockIconSpriteCache ??= CreateClockIconSprite();

    public static void EnsureLoaded()
    {
        _ = WoodBoard;
        _ = JellyLeft;
        _ = JellyUp;
        _ = JellyDown;
        _ = JellyRight;
        _ = JellyCorrect;
        _ = StoneSpace;
        _ = CapsuleBar;
        _ = GlossyFill;
        _ = PearlOrb;
        _ = TargetStarburst;
        _ = BigPillBtn;
        _ = GreenBadge;
        _ = RedCloseBtn;
        _ = CornerFlora;
        _ = Ring;
        _ = White;
        _ = SparkleStar;
        _ = Heart;
        _ = Sunburst;
        _ = IvyGarland;
        _ = SideIvyLeft;
        _ = SideIvyRight;
        _ = FloraLeft;
        _ = FloraRight;
        _ = BarWing;
        _ = GoldBead;
        _ = ClockIcon;
    }

    public static Sprite GetJellySprite(RhythmDirection dir)
    {
        return dir switch
        {
            RhythmDirection.Left => JellyLeft,
            RhythmDirection.Up => JellyUp,
            RhythmDirection.Down => JellyDown,
            RhythmDirection.Right => JellyRight,
            _ => JellyLeft
        };
    }

    private static Sprite CreateSparkleStarSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 64f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x - center);
                float py = Mathf.Abs(y - center);
                float dist = Mathf.Sqrt(px * px + py * py);

                // Đường cong sao 4 cánh
                float starCurve = Mathf.Pow(px / 52f, 0.55f) + Mathf.Pow(py / 52f, 0.55f);
                if (starCurve <= 1.0f)
                {
                    Color c;
                    if (dist <= 12f)
                    {
                        c = Color.white; // Lõi tâm sáng chói
                    }
                    else
                    {
                        float t = (starCurve - 0.2f) / 0.8f;
                        c = Color.Lerp(new Color(1f, 0.96f, 0.65f, 1f), new Color(0.98f, 0.78f, 0.15f, 1f), t);
                    }
                    cols[y * size + x] = c;
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateHeartSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x - 64f) / 38f;
                float v = (y - 52f) / 38f;

                float eq = (u * u + v * v - 1f);
                float val = eq * eq * eq - (u * u * v * v * v);

                if (val <= 0f)
                {
                    Color c;
                    if (v < -0.1f)
                    {
                        c = new Color(0.75f, 0.16f, 0.25f, 1f);
                    }
                    else
                    {
                        c = new Color(0.98f, 0.32f, 0.44f, 1f);
                        float elX = (u + 0.32f) / 0.24f;
                        float elY = (v - 0.38f) / 0.20f;
                        if (elX * elX + elY * elY <= 1.0f)
                        {
                            c = Color.Lerp(c, Color.white, 0.45f);
                        }
                    }
                    cols[y * size + x] = c;
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateSunburstSprite()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 128f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x - center;
                float py = y - center;
                float dist = Mathf.Sqrt(px * px + py * py);

                if (dist <= 124f)
                {
                    float angle = Mathf.Atan2(py, px);
                    float rays = Mathf.Cos(12f * angle);
                    float rayFactor = Mathf.Clamp01(rays * 0.5f + 0.5f);

                    float fade = 1.0f - (dist / 124f);
                    float alpha = fade * (0.25f + 0.75f * rayFactor);
                    if (dist <= 32f)
                    {
                        alpha = Mathf.Max(alpha, 0.85f * (1.0f - dist / 32f));
                    }

                    cols[y * size + x] = new Color(1f, 0.90f, 0.40f, alpha * 0.85f);
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSpriteCache == null)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(cols);
            tex.Apply();
            whiteSpriteCache = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }
        return whiteSpriteCache;
    }

    private static Sprite CreateWoodBoardSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];

        float r = 28f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = x < 32 ? 32f : (x > size - 32 ? size - 32 : x);
                float cy = y < 32 ? 32f : (y > size - 32 ? size - 32 : y);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > r)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                // Đáy đổ bóng 3D của bảng gỗ
                if (y < 12)
                {
                    cols[y * size + x] = new Color(0.38f, 0.22f, 0.12f, 1f);
                }
                // Khung viền gỗ caramel ấm
                else if (x <= 14 || x >= size - 15 || y <= 16 || y >= size - 15 || dist >= r - 12f)
                {
                    if (y >= size - 14)
                        cols[y * size + x] = new Color(0.82f, 0.58f, 0.34f, 1f);
                    else
                        cols[y * size + x] = new Color(0.68f, 0.44f, 0.24f, 1f);
                }
                // Chỉ rãnh trong viền gỗ
                else if (x <= 17 || x >= size - 18 || y <= 19 || y >= size - 18 || dist >= r - 15f)
                {
                    cols[y * size + x] = new Color(0.50f, 0.30f, 0.15f, 1f);
                }
                // Lòng bảng ngà sáng tự nhiên (Parchment Ivory)
                else
                {
                    cols[y * size + x] = new Color(0.97f, 0.94f, 0.88f, 1f);
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(38f, 38f, 38f, 38f));
    }

    private static Sprite CreateJellyButtonSprite(Color baseCol, Color bevelCol, RhythmDirection? dir, bool isCheck)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];

        float cornerR = 26f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x < 10 || x > 118 || y < 10 || y > 118)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                float cx = x < 36 ? 36f : (x > 92 ? 92f : x);
                float cy = y < 36 ? 36f : (y > 92 ? 92f : y);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > cornerR)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                Color c;
                if (y < 24)
                {
                    c = bevelCol;
                }
                else
                {
                    c = baseCol;
                    // Vệt cong elip highlight bóng bẩy ở nửa trên
                    float elX = (x - 64f) / 38f;
                    float elY = (y - 88f) / 18f;
                    float elDist = elX * elX + elY * elY;
                    if (elDist <= 1.0f)
                    {
                        float gloss = (1.0f - elDist) * 0.42f;
                        c = Color.Lerp(c, Color.white, gloss);
                    }
                    if (y >= 106 && dist >= cornerR - 3f)
                    {
                        c = Color.Lerp(c, Color.white, 0.45f);
                    }
                }

                // Vẽ icon dày dặn với 2x2 supersampling
                float iconHit = 0f;
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float px = x + (sx + 0.5f) * 0.5f;
                        float py = y + (sy + 0.5f) * 0.5f;

                        bool inIcon = false;
                        if (isCheck)
                        {
                            float d1 = DistToSegment(px, py, 44f, 66f, 58f, 50f);
                            float d2 = DistToSegment(px, py, 58f, 50f, 84f, 84f);
                            if (d1 <= 6f || d2 <= 6f) inIcon = true;
                        }
                        else if (dir == RhythmDirection.Left)
                        {
                            if (px >= 38f && px <= 64f && Mathf.Abs(py - 66f) <= (px - 38f) * 0.85f) inIcon = true;
                            if (px >= 64f && px <= 88f && Mathf.Abs(py - 66f) <= 10f) inIcon = true;
                        }
                        else if (dir == RhythmDirection.Right)
                        {
                            if (px >= 64f && px <= 90f && Mathf.Abs(py - 66f) <= (90f - px) * 0.85f) inIcon = true;
                            if (px >= 40f && px <= 64f && Mathf.Abs(py - 66f) <= 10f) inIcon = true;
                        }
                        else if (dir == RhythmDirection.Up)
                        {
                            if (py >= 64f && py <= 90f && Mathf.Abs(px - 64f) <= (90f - py) * 0.85f) inIcon = true;
                            if (py >= 40f && py <= 64f && Mathf.Abs(px - 64f) <= 10f) inIcon = true;
                        }
                        else if (dir == RhythmDirection.Down)
                        {
                            if (py >= 38f && py <= 64f && Mathf.Abs(px - 64f) <= (py - 38f) * 0.85f) inIcon = true;
                            if (py >= 64f && py <= 88f && Mathf.Abs(px - 64f) <= 10f) inIcon = true;
                        }

                        if (inIcon) iconHit += 1f;
                    }
                }

                if (iconHit > 0f)
                {
                    float alpha = iconHit / 4f;
                    c = Color.Lerp(c, Color.white, alpha);
                }

                cols[y * size + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static float DistToSegment(float px, float py, float x1, float y1, float x2, float y2)
    {
        float l2 = (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
        if (l2 == 0f) return Vector2.Distance(new Vector2(px, py), new Vector2(x1, y1));
        float t = Mathf.Clamp01(((px - x1) * (x2 - x1) + (py - y1) * (y2 - y1)) / l2);
        Vector2 proj = new Vector2(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
        return Vector2.Distance(new Vector2(px, py), proj);
    }

    private static Sprite CreateStoneSpaceSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];

        float cornerR = 24f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x < 10 || x > 118 || y < 10 || y > 118)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                float cx = x < 34 ? 34f : (x > 94 ? 94f : x);
                float cy = y < 34 ? 34f : (y > 94 ? 94f : y);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > cornerR)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                if (y < 22)
                {
                    cols[y * size + x] = new Color(0.60f, 0.54f, 0.44f, 1f);
                }
                else
                {
                    Color c = new Color(0.92f, 0.88f, 0.81f, 1f);
                    float elX = (x - 64f) / 42f;
                    float elY = (y - 86f) / 18f;
                    float elDist = elX * elX + elY * elY;
                    if (elDist <= 1.0f)
                    {
                        c = Color.Lerp(c, Color.white, (1.0f - elDist) * 0.35f);
                    }
                    cols[y * size + x] = c;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(34f, 34f, 34f, 34f));
    }

    private static Sprite CreateCapsuleBarSprite()
    {
        int width = 128;
        int height = 64;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[width * height];

        float r = 26f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float cx = x < 32 ? 32f : (x > width - 32 ? width - 32 : x);
                float cy = 32f;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > r)
                {
                    cols[y * width + x] = Color.clear;
                    continue;
                }

                if (dist >= r - 4f)
                {
                    cols[y * width + x] = new Color(0.68f, 0.46f, 0.28f, 1f);
                }
                else
                {
                    float depth = (r - dist) / r;
                    cols[y * width + x] = Color.Lerp(new Color(0.82f, 0.78f, 0.70f, 1f), new Color(0.92f, 0.89f, 0.83f, 1f), depth);
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32f, 26f, 32f, 26f));
    }

    private static Sprite CreateGlossyFillSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = Color.white;
                if (y >= 32 && y <= 58)
                {
                    float norm = 1.0f - Mathf.Abs(y - 45f) / 13f;
                    c = Color.Lerp(new Color(0.92f, 0.92f, 0.92f, 1f), Color.white, norm);
                }
                else
                {
                    c = new Color(0.85f, 0.85f, 0.85f, 1f);
                }
                cols[y * size + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16f, 16f, 16f, 16f));
    }

    private static Sprite CreatePearlOrbSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 64f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= 44f)
                {
                    float norm = dist / 44f;
                    float lightDist = Vector2.Distance(new Vector2(x, y), new Vector2(46f, 82f));
                    Color pearl = Color.Lerp(new Color(1f, 1f, 0.96f, 1f), new Color(0.92f, 0.84f, 0.65f, 1f), norm);

                    if (lightDist <= 12f)
                    {
                        float spec = 1.0f - (lightDist / 12f);
                        pearl = Color.Lerp(pearl, Color.white, spec * spec);
                    }
                    cols[y * size + x] = pearl;
                }
                else if (dist <= 58f)
                {
                    float glow = 1.0f - ((dist - 44f) / 14f);
                    cols[y * size + x] = new Color(1f, 0.95f, 0.65f, glow * glow * 0.70f);
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateLuminousHitTargetSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 64f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x - center);
                float py = Mathf.Abs(y - center);
                float dist = Mathf.Sqrt(px * px + py * py);

                Color c = Color.clear;

                // 1. Quầng hào quang vàng tỏa sáng mềm mại bên ngoài
                if (dist <= 58f)
                {
                    float glow = 1.0f - (dist / 58f);
                    c = new Color(1f, 0.88f, 0.35f, glow * glow * 0.45f);
                }

                // 2. Vành tròn vàng hổ phách định vị
                if (dist >= 30f && dist <= 38f)
                {
                    float ringNorm = 1.0f - Mathf.Abs(dist - 34f) / 4f;
                    Color ringCol = new Color(1f, 0.90f, 0.40f, ringNorm * 0.85f);
                    c = Color.Lerp(c, ringCol, ringCol.a);
                }

                // 3. Nhân ngọc phát sáng ở tâm
                if (dist <= 20f)
                {
                    float coreNorm = 1.0f - (dist / 20f);
                    Color coreCol = Color.Lerp(new Color(1f, 0.82f, 0.20f, 1f), Color.white, coreNorm * coreNorm);
                    c = Color.Lerp(c, coreCol, 0.90f);
                }

                // 4. Tia sáng 4 cánh (Lens Flare Cross & Star Curve)
                float starCurve = Mathf.Pow(px / 46f, 0.52f) + Mathf.Pow(py / 46f, 0.52f);
                if (starCurve <= 1.0f)
                {
                    float flareT = 1.0f - starCurve;
                    Color flareCol = Color.Lerp(new Color(1f, 0.92f, 0.50f, 1f), Color.white, flareT);
                    c = Color.Lerp(c, flareCol, Mathf.Clamp01(flareT * 1.5f));
                }

                cols[y * size + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateBigPillButtonSprite()
    {
        int width = 256;
        int height = 96;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[width * height];

        float r = 38f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float cx = x < 48 ? 48f : (x > width - 48 ? width - 48 : x);
                float cy = 48f;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > r)
                {
                    cols[y * width + x] = Color.clear;
                    continue;
                }

                Color c;
                if (y < 22)
                {
                    c = new Color(0.14f, 0.44f, 0.18f, 1f); // Bóng đổ đáy 3D
                }
                else
                {
                    c = new Color(0.28f, 0.76f, 0.26f, 1f); // Xanh bơ / chanh tươi tắn
                    // Vệt bóng gương cong ở nửa trên
                    if (y >= 50 && y <= 86)
                    {
                        float norm = 1.0f - Mathf.Abs(y - 68f) / 18f;
                        c = Color.Lerp(c, Color.white, norm * 0.42f);
                    }
                    if (y >= height - 14 && dist <= r - 2f)
                    {
                        c = Color.Lerp(c, Color.white, 0.35f);
                    }
                }
                cols[y * width + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(46f, 28f, 46f, 28f));
    }

    private static Sprite CreateGreenBadgeSprite()
    {
        int width = 128;
        int height = 64;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[width * height];

        float r = 26f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float cx = x < 32 ? 32f : (x > width - 32 ? width - 32 : x);
                float cy = 32f;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                if (dist > r)
                {
                    cols[y * width + x] = Color.clear;
                    continue;
                }

                Color c;
                if (y < 16)
                {
                    c = new Color(0.22f, 0.54f, 0.16f, 1f); // Bóng đổ 3D đáy
                }
                else
                {
                    c = new Color(0.33f, 0.68f, 0.20f, 1f); // Xanh lá táo tươi tắn
                    if (y >= height - 12 && dist <= r - 1f)
                    {
                        c = Color.Lerp(c, Color.white, 0.35f); // Viền sáng trên đỉnh
                    }
                }

                cols[y * width + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32f, 26f, 32f, 26f));
    }

    private static Sprite CreateRedCloseButtonSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 64f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist > 52f)
                {
                    cols[y * size + x] = Color.clear;
                    continue;
                }

                Color c;
                if (dist >= 46f)
                {
                    c = new Color(1f, 0.92f, 0.88f, 1f); // Viền kem bọc ngoài
                }
                else if (y < 24)
                {
                    c = new Color(0.60f, 0.12f, 0.12f, 1f); // Bóng đổ đỏ trầm đáy 3D
                }
                else
                {
                    c = new Color(0.92f, 0.26f, 0.24f, 1f); // Đỏ kẹo ngọt
                    float elDist = Vector2.Distance(new Vector2(x, y), new Vector2(center, 78f));
                    if (elDist <= 24f)
                    {
                        c = Color.Lerp(c, Color.white, (1.0f - elDist / 24f) * 0.40f);
                    }
                }

                // Dấu X trắng dày dặn sắc nét
                float px = x - center;
                float py = y - (center + 2f);
                float d1 = Mathf.Abs(px - py);
                float d2 = Mathf.Abs(px + py);
                if ((d1 <= 6f || d2 <= 6f) && Mathf.Abs(px) <= 18f && Mathf.Abs(py) <= 18f)
                {
                    c = Color.white;
                }

                cols[y * size + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateCornerFloraSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        Array.Clear(cols, 0, cols.Length);

        // Nhánh dây leo ôm cong góc bảng
        DrawPointedLeaf(cols, size, size, new Vector2(36f, 94f), -35f, 28f, 16f, new Color(0.20f, 0.50f, 0.16f, 1f), new Color(0.38f, 0.76f, 0.26f, 1f));
        DrawPointedLeaf(cols, size, size, new Vector2(72f, 68f), -65f, 26f, 15f, new Color(0.22f, 0.54f, 0.18f, 1f), new Color(0.42f, 0.80f, 0.30f, 1f));
        DrawSmallBlossom(cols, size, size, new Vector2(28f, 102f), 12f);

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateRingSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 64f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist >= 42f && dist <= 56f)
                {
                    float t = 1.0f - Mathf.Abs(dist - 49f) / 7f;
                    cols[y * size + x] = new Color(1f, 1f, 1f, t * t);
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static void DrawPointedLeaf(Color[] cols, int texW, int texH, Vector2 basePos, float angleDeg, float length, float width, Color baseCol, Color tipCol)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        float r = length + width;
        int minX = Mathf.Clamp(Mathf.FloorToInt(basePos.x - r), 0, texW - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(basePos.x + r), 0, texW - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(basePos.y - r), 0, texH - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(basePos.y + r), 0, texH - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - basePos.x;
                float dy = y - basePos.y;

                float forward = dx * cos + dy * sin;
                float lateral = -dx * sin + dy * cos;

                if (forward >= 0f && forward <= length)
                {
                    float t = forward / length;
                    float halfWidth = (width * 0.5f) * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.65f));
                    float latAbs = Mathf.Abs(lateral);

                    if (latAbs <= halfWidth)
                    {
                        float edgeDist = halfWidth - latAbs;
                        float aa = Mathf.Clamp01(edgeDist / 1.0f);

                        Color col = Color.Lerp(baseCol, tipCol, t * 0.5f);

                        if (latAbs <= 1.0f)
                        {
                            col = Color.Lerp(col, new Color(0.72f, 0.94f, 0.48f, 1f), (1.0f - latAbs) * 0.65f);
                        }
                        else if (lateral > 0f)
                        {
                            col = Color.Lerp(col, Color.white, 0.15f * (1.0f - latAbs / halfWidth));
                        }
                        else
                        {
                            col = Color.Lerp(col, new Color(0.12f, 0.32f, 0.10f, 1f), 0.25f);
                        }

                        int idx = y * texW + x;
                        cols[idx] = Color.Lerp(cols[idx], col, aa * col.a);
                    }
                }
            }
        }
    }

    private static void DrawSmallBlossom(Color[] cols, int texW, int texH, Vector2 pos, float radius)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(pos.x - radius), 0, texW - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(pos.x + radius), 0, texW - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(pos.y - radius), 0, texH - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(pos.y + radius), 0, texH - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - pos.x;
                float dy = y - pos.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float petalR = radius * (0.65f + 0.35f * Mathf.Cos(5f * angle));
                if (dist <= petalR)
                {
                    Color col = dist <= radius * 0.35f
                        ? new Color(0.98f, 0.54f, 0.12f, 1f)
                        : new Color(1f, 0.90f, 0.35f, 1f);
                    int idx = y * texW + x;
                    cols[idx] = Color.Lerp(cols[idx], col, col.a);
                }
            }
        }
    }

    private static void DrawBerry(Color[] cols, int texW, int texH, Vector2 pos, float radius, Color color)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(pos.x - radius), 0, texW - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(pos.x + radius), 0, texW - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(pos.y - radius), 0, texH - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(pos.y + radius), 0, texH - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), pos);
                if (dist <= radius)
                {
                    float t = dist / radius;
                    float specDist = Vector2.Distance(new Vector2(x, y), pos + new Vector2(-radius * 0.3f, radius * 0.3f));
                    Color c = Color.Lerp(color, color * 0.70f, t);
                    if (specDist <= radius * 0.4f)
                    {
                        c = Color.Lerp(c, Color.white, (1.0f - specDist / (radius * 0.4f)) * 0.75f);
                    }
                    int idx = y * texW + x;
                    cols[idx] = Color.Lerp(cols[idx], c, c.a);
                }
            }
        }
    }

    private static Sprite CreateIvyGarlandSprite()
    {
        int width = 512;
        int height = 64;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[width * height];
        Array.Clear(cols, 0, cols.Length);

        // 1. Thân dây leo uốn lượn tự nhiên
        for (int x = 0; x < width; x++)
        {
            float normX = (float)x / width;
            float stemY = 40f + 6f * Mathf.Sin(normX * 18f) + 4f * Mathf.Cos(normX * 32f);
            int minY = Mathf.Clamp(Mathf.FloorToInt(stemY - 3f), 0, height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(stemY + 3f), 0, height - 1);
            for (int y = minY; y <= maxY; y++)
            {
                float dy = Mathf.Abs(y - stemY);
                if (dy <= 2.8f)
                {
                    float aa = Mathf.Clamp01((2.8f - dy) / 1.0f);
                    Color stemCol = new Color(0.18f, 0.36f, 0.12f, 1f);
                    int idx = y * width + x;
                    cols[idx] = Color.Lerp(cols[idx], stemCol, aa);
                }
            }
        }

        // 2. Các nhánh lá hình quả hạnh có chóp nhọn (Botanical Almond Leaves)
        int leafCount = 20;
        for (int i = 0; i < leafCount; i++)
        {
            float norm = (i + 0.5f) / leafCount;
            float lx = norm * (width - 40f) + 20f + 4f * Mathf.Sin(i * 1.7f);
            float ly = 40f + 6f * Mathf.Sin((lx / width) * 18f) + 4f * Mathf.Cos((lx / width) * 32f);

            bool pointDown = (i % 2 == 0);
            float angle = pointDown ? (-40f + 10f * Mathf.Sin(i * 2.3f)) : (35f + 10f * Mathf.Cos(i * 2.3f));
            float len = pointDown ? 22f : 18f;
            float w = pointDown ? 13f : 11f;

            DrawPointedLeaf(cols, width, height, new Vector2(lx, ly), angle, len, w,
                new Color(0.24f, 0.54f, 0.18f, 1f),
                new Color(0.42f, 0.82f, 0.28f, 1f));
        }

        // 3. Điểm các bông hoa vàng nhỏ nở dọc theo dây leo
        DrawSmallBlossom(cols, width, height, new Vector2(90f, 44f), 9f);
        DrawSmallBlossom(cols, width, height, new Vector2(256f, 46f), 10f);
        DrawSmallBlossom(cols, width, height, new Vector2(420f, 42f), 9f);

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateSideIvySprite(bool isRight)
    {
        int width = 64;
        int height = 480;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[width * height];
        Array.Clear(cols, 0, cols.Length);

        // 1. Thân dây leo uốn lượn dọc
        for (int y = 0; y < height; y++)
        {
            float normY = (float)y / height;
            float stemX = 32f + 5f * Mathf.Sin(normY * 16f) + 3f * Mathf.Cos(normY * 26f);
            int minX = Mathf.Clamp(Mathf.FloorToInt(stemX - 3f), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(stemX + 3f), 0, width - 1);
            for (int x = minX; x <= maxX; x++)
            {
                float dx = Mathf.Abs(x - stemX);
                if (dx <= 2.6f)
                {
                    float aa = Mathf.Clamp01((2.6f - dx) / 1.0f);
                    Color stemCol = new Color(0.18f, 0.36f, 0.12f, 1f);
                    int idx = y * width + x;
                    cols[idx] = Color.Lerp(cols[idx], stemCol, aa);
                }
            }
        }

        // 2. Lá cây mọc so le hai bên
        int leafCount = 18;
        for (int i = 0; i < leafCount; i++)
        {
            float norm = (i + 0.5f) / leafCount;
            float ly = norm * (height - 40f) + 20f;
            float lx = 32f + 5f * Mathf.Sin((ly / height) * 16f) + 3f * Mathf.Cos((ly / height) * 26f);

            bool pointIn = (i % 2 == 0);
            float angle = pointIn ? (130f + 15f * Mathf.Sin(i * 1.5f)) : (50f + 15f * Mathf.Cos(i * 1.5f));
            float len = 19f;
            float w = 11f;

            DrawPointedLeaf(cols, width, height, new Vector2(lx, ly), angle, len, w,
                new Color(0.24f, 0.54f, 0.18f, 1f),
                new Color(0.42f, 0.82f, 0.28f, 1f));
        }

        // 3. Nụ hoa / quả nhỏ
        DrawSmallBlossom(cols, width, height, new Vector2(30f, 140f), 8f);
        DrawSmallBlossom(cols, width, height, new Vector2(34f, 320f), 8f);

        // Lật ảnh nếu là bên phải
        if (isRight)
        {
            Color[] flipped = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    flipped[y * width + x] = cols[y * width + (width - 1 - x)];
                }
            }
            cols = flipped;
        }

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateBarVineWingSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        Array.Clear(cols, 0, cols.Length);

        // Cụm lá ôm đuôi thanh tiến trình
        DrawPointedLeaf(cols, size, size, new Vector2(26f, 32f), 30f, 22f, 12f, new Color(0.22f, 0.56f, 0.24f, 1f), new Color(0.42f, 0.80f, 0.32f, 1f));
        DrawPointedLeaf(cols, size, size, new Vector2(26f, 32f), 110f, 18f, 10f, new Color(0.20f, 0.50f, 0.20f, 1f), new Color(0.38f, 0.74f, 0.28f, 1f));
        DrawSmallBlossom(cols, size, size, new Vector2(22f, 30f), 6f);

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateFloraBouquetSprite(bool rightSide)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        Array.Clear(cols, 0, cols.Length);

        // 1. Chùm lá xanh tỏa đều
        DrawPointedLeaf(cols, size, size, new Vector2(50f, 48f), 45f, 32f, 18f, new Color(0.20f, 0.50f, 0.16f, 1f), new Color(0.38f, 0.76f, 0.26f, 1f));
        DrawPointedLeaf(cols, size, size, new Vector2(50f, 48f), 115f, 34f, 19f, new Color(0.22f, 0.54f, 0.18f, 1f), new Color(0.42f, 0.80f, 0.30f, 1f));
        DrawPointedLeaf(cols, size, size, new Vector2(50f, 48f), 165f, 30f, 17f, new Color(0.18f, 0.46f, 0.14f, 1f), new Color(0.35f, 0.72f, 0.24f, 1f));
        DrawPointedLeaf(cols, size, size, new Vector2(50f, 48f), -20f, 28f, 16f, new Color(0.24f, 0.56f, 0.19f, 1f), new Color(0.44f, 0.84f, 0.32f, 1f));

        if (!rightSide)
        {
            // Bên trái: Hoa vàng 5 cánh nở xòe rực rỡ với nhụy cam tươi
            DrawSmallBlossom(cols, size, size, new Vector2(54f, 50f), 20f);
            DrawSmallBlossom(cols, size, size, new Vector2(86f, 36f), 11f);
        }
        else
        {
            // Bên phải: Chùm quả mọng ngọt ngào (Đỏ, Tím, Lam, Cam)
            DrawBerry(cols, size, size, new Vector2(54f, 50f), 11f, new Color(0.92f, 0.26f, 0.26f, 1f)); // Đỏ
            DrawBerry(cols, size, size, new Vector2(40f, 66f), 9.5f, new Color(0.68f, 0.32f, 0.88f, 1f)); // Tím
            DrawBerry(cols, size, size, new Vector2(72f, 42f), 9f, new Color(0.16f, 0.68f, 0.88f, 1f)); // Lam ngọc
            DrawBerry(cols, size, size, new Vector2(34f, 42f), 8f, new Color(0.98f, 0.62f, 0.16f, 1f)); // Cam hổ phách
        }

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateGoldBeadSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 16f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= 12f)
                {
                    float norm = dist / 12f;
                    Color gold = Color.Lerp(new Color(1f, 0.90f, 0.45f, 1f), new Color(0.85f, 0.55f, 0.12f, 1f), norm);
                    float specDist = Vector2.Distance(new Vector2(x, y), new Vector2(13f, 20f));
                    if (specDist <= 4f)
                    {
                        gold = Color.Lerp(gold, Color.white, 1.0f - specDist / 4f);
                    }
                    cols[y * size + x] = gold;
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateClockIconSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[size * size];
        float center = 16f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= 13f)
                {
                    if (dist >= 10.5f)
                    {
                        cols[y * size + x] = Color.white;
                    }
                    else
                    {
                        Color c = new Color(1f, 1f, 1f, 0.15f);

                        float px = x - center;
                        float py = y - center;
                        bool minHand = px >= 0f && px <= 6.5f && Mathf.Abs(py - px) <= 1.2f;
                        bool hourHand = px <= 0f && px >= -5f && Mathf.Abs(py - (-px * 1.4f)) <= 1.2f;
                        bool centerPin = dist <= 2.2f;

                        if (minHand || hourHand || centerPin)
                        {
                            c = Color.white;
                        }
                        cols[y * size + x] = c;
                    }
                }
                else
                {
                    cols[y * size + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}


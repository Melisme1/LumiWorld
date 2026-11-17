using UnityEngine;

/// <summary>
/// Helper UI chuyên cung cấp Sprite đại diện cho ResourceData
/// (ưu tiên icon được gán sẵn, fallback sang icon procedural).
/// </summary>
public static class ResourceIconUtility
{
    private static Sprite cachedWoodSprite;
    private static Sprite cachedFlowerSprite;
    private static Sprite cachedHerbSprite;
    private static Sprite cachedDefaultResourceSprite;

    public static Sprite GetResourceSprite(ResourceData data)
    {
        if (data != null && data.icon != null)
        {
            return data.icon;
        }

        string resId = data != null && data.resourceID != null ? data.resourceID.ToLowerInvariant() : "";
        if (resId.Contains("wood") || resId.Contains("go"))
        {
            return cachedWoodSprite ??= CreateWoodIconSprite(64);
        }
        if (resId.Contains("flower") || resId.Contains("hoa") || resId.Contains("bloom"))
        {
            return cachedFlowerSprite ??= CreateFlowerIconSprite(64);
        }
        if (resId.Contains("herb") || resId.Contains("thao") || resId.Contains("wind"))
        {
            return cachedHerbSprite ??= CreateHerbIconSprite(64);
        }

        return cachedDefaultResourceSprite ??= CreateDefaultResourceSprite(64);
    }

    private static Sprite CreateWoodIconSprite(int size = 64)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        Color barkColor = new Color(0.42f, 0.22f, 0.10f, 1f);
        Color woodColor = new Color(0.85f, 0.58f, 0.28f, 1f);
        Color ring1Color = new Color(0.72f, 0.44f, 0.18f, 1f);
        Color ring2Color = new Color(0.65f, 0.38f, 0.15f, 1f);
        Color coreColor = new Color(0.48f, 0.26f, 0.11f, 1f);
        Color sproutColor = new Color(0.32f, 0.82f, 0.38f, 1f);

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

                        float r1Dist = Mathf.Abs(dist - size * 0.22f);
                        if (r1Dist < 1.4f) col = Color.Lerp(ring1Color, col, r1Dist / 1.4f);

                        float r2Dist = Mathf.Abs(dist - size * 0.13f);
                        if (r2Dist < 1.2f) col = Color.Lerp(ring2Color, col, r2Dist / 1.2f);

                        if (dist <= size * 0.05f) col = Color.Lerp(coreColor, col, dist / (size * 0.05f));
                    }
                    col.a *= alpha;
                }

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
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        Color petalColor = new Color(0.96f, 0.45f, 0.70f, 1f);
        Color petalRimColor = new Color(0.99f, 0.72f, 0.86f, 1f);
        Color coreColor = new Color(0.98f, 0.82f, 0.16f, 1f);
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
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        Color leafMainColor = new Color(0.20f, 0.82f, 0.55f, 1f);
        Color leafTipColor = new Color(0.48f, 0.95f, 0.72f, 1f);
        Color leafSubColor = new Color(0.12f, 0.70f, 0.44f, 1f);
        Color stemColor = new Color(0.08f, 0.52f, 0.30f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;

                Color col = Color.clear;

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
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

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

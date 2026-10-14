using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hình tượng trưng cho đảo trên WorldMap. Hình không phải đảo thật của người chơi: chọn theo bậc điểm,
/// kiểu cố định theo playerId. Ô nào để trống thì dùng hình tạm vẽ bằng code, nên nhóm art thay dần được
/// mà không sửa script. Tạo bằng Assets > Create > LumiWorld > Island Art Set rồi gán vào WorldMapController.
/// </summary>
[CreateAssetMenu(menuName = "LumiWorld/Island Art Set", fileName = "IslandArtSet")]
public class IslandArtSet : ScriptableObject
{
    [Tooltip("Ngưỡng điểm của từng bậc: điểm < ngưỡng[0] là bậc 0, ... ; vượt hết là bậc cuối")]
    public int[] scoreThresholds = { 50, 200, 600 };

    [Tooltip("Mỗi bậc một danh sách sprite; kiểu trong bậc chọn theo playerId. Để trống thì dùng hình tạm")]
    public List<TierSprites> tiers = new List<TierSprites>();

    [Tooltip("Đảo hoang trang trí ở ô trống. Để trống thì dùng hình tạm")]
    public List<Sprite> wildSprites = new List<Sprite>();

    [Tooltip("Vòng sáng quanh đảo của mình và đảo đang chọn. Để trống thì dùng vòng tạm")]
    public Sprite ringSprite;

    [System.Serializable]
    public class TierSprites
    {
        public List<Sprite> sprites = new List<Sprite>();
    }

    public int TierOf(int score)
    {
        int[] limits = scoreThresholds ?? new int[0];
        for (int i = 0; i < limits.Length; i++)
            if (score < limits[i]) return i;
        return limits.Length;
    }
}

/// <summary>
/// Chọn sprite cho đảo: lấy từ IslandArtSet nếu có, không thì vẽ hình tạm (viền cát, thân xanh, chấm cây)
/// và cache theo kiểu, nên vài trăm đảo chỉ tạo vài chục texture.
/// </summary>
public static class IslandSprites
{
    public const int Variants = 6;
    private const int TextureSize = 128;
    private const float PixelsPerUnit = 32f;

    private static readonly Dictionary<int, Sprite> placeholders = new Dictionary<int, Sprite>();
    private static Sprite ring;

    /// <summary>Kích thước hiển thị theo bậc: bậc cao thì đảo to hơn.</summary>
    public static float ScaleOfTier(int tier) => 0.75f + 0.18f * Mathf.Clamp(tier, 0, 4);

    public static Sprite ForPlayer(IslandArtSet art, string playerId, int tier)
    {
        uint hash = StableHash.Of(playerId);
        if (art != null && tier < art.tiers.Count && art.tiers[tier] != null && art.tiers[tier].sprites.Count > 0)
        {
            List<Sprite> list = art.tiers[tier].sprites;
            Sprite chosen = list[(int)(hash % (uint)list.Count)];
            if (chosen != null) return chosen;
        }
        return Placeholder((int)(hash % Variants), wild: false);
    }

    public static Sprite ForWild(IslandArtSet art, int x, int y)
    {
        uint hash = StableHash.Of(x, y);
        if (art != null && art.wildSprites.Count > 0)
        {
            Sprite chosen = art.wildSprites[(int)(hash % (uint)art.wildSprites.Count)];
            if (chosen != null) return chosen;
        }
        return Placeholder((int)(hash % Variants), wild: true);
    }

    public static Sprite Ring(IslandArtSet art)
    {
        if (art != null && art.ringSprite != null) return art.ringSprite;
        if (ring != null) return ring;

        var texture = NewTexture("IslandRing");
        var pixels = new Color32[TextureSize * TextureSize];
        float half = TextureSize * 0.5f;
        for (int py = 0; py < TextureSize; py++)
        {
            for (int px = 0; px < TextureSize; px++)
            {
                float d = Vector2.Distance(new Vector2(px + 0.5f, py + 0.5f), new Vector2(half, half)) / half;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.92f) / 0.06f);
                pixels[py * TextureSize + px] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        ring = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        return ring;
    }

    // Đảo tạm: một khối tròn méo (méo theo kiểu), viền cát, thân xanh, vài chấm cây tối màu.
    // Đảo hoang nhạt màu hơn để không lẫn với đảo người chơi.
    private static Sprite Placeholder(int variant, bool wild)
    {
        int key = variant * 2 + (wild ? 1 : 0);
        if (placeholders.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        var random = new System.Random(1000 + key);
        float phaseA = (float)random.NextDouble() * Mathf.PI * 2f;
        float phaseB = (float)random.NextDouble() * Mathf.PI * 2f;
        int lobes = 2 + random.Next(3);

        Color32 sand = wild ? new Color32(214, 204, 160, 255) : new Color32(236, 214, 150, 255);
        Color32 grass = wild ? new Color32(120, 160, 110, 255) : new Color32(92, 170, 70, 255);
        Color32 tree = wild ? new Color32(95, 130, 90, 255) : new Color32(46, 112, 46, 255);
        Color32 shallow = new Color32(120, 210, 230, 110);

        var trees = new List<Vector2>();
        int treeCount = wild ? 3 : 6 + random.Next(5);
        for (int i = 0; i < treeCount; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = (float)random.NextDouble() * 0.45f;
            trees.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }

        var texture = NewTexture(wild ? "WildIsland" + variant : "Island" + variant);
        var pixels = new Color32[TextureSize * TextureSize];
        float half = TextureSize * 0.5f;
        for (int py = 0; py < TextureSize; py++)
        {
            for (int px = 0; px < TextureSize; px++)
            {
                // Toạ độ chuẩn hoá trong [-1, 1], ép dẹt theo chiều dọc như đảo nhìn nghiêng nhẹ
                var p = new Vector2((px + 0.5f - half) / half, (py + 0.5f - half) / half * 1.25f);
                float angle = Mathf.Atan2(p.y, p.x);
                float edge = 0.78f + 0.09f * Mathf.Sin(angle * lobes + phaseA) + 0.05f * Mathf.Sin(angle * (lobes + 2) + phaseB);
                float d = p.magnitude;

                Color32 c = new Color32(0, 0, 0, 0);
                if (d < edge + 0.1f) c = shallow;
                if (d < edge) c = sand;
                if (d < edge - 0.12f)
                {
                    c = grass;
                    foreach (Vector2 t in trees)
                        if ((p - t).sqrMagnitude < 0.008f) { c = tree; break; }
                }
                pixels[py * TextureSize + px] = c;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        placeholders[key] = sprite;
        return sprite;
    }

    private static Texture2D NewTexture(string name)
    {
        return new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
    }
}

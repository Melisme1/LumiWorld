using UnityEngine;

/// <summary>
/// Bộ số ngẫu nhiên có seed cho cây, đá, cỏ trang trí: cùng seed thì ra đúng cùng một dãy số,
/// nên mỗi lần vào game (và khi người khác thăm đảo) trang trí mọc y hệt mà không phải lưu gì.
/// Dùng thay cho UnityEngine.Random trong các routine spawn: không đụng tới trạng thái Random chung
/// của các hệ thống khác, và an toàn với coroutine có yield giữa chừng.
/// Seed tính bằng FNV-1a (ổn định giữa các lần chạy và các máy, khác string.GetHashCode).
/// </summary>
public sealed class DecorRandom
{
    private System.Random random;

    public DecorRandom(int seed) { random = new System.Random(seed); }

    /// <summary>Seed theo ô và thẻ: dữ liệu có cả trong bản lưu của chủ đảo lẫn snapshot người thăm.</summary>
    public static DecorRandom ForTile(HexCoordinates hex, string cardId, string salt = null)
    {
        return new DecorRandom(Hash($"{salt}|{hex.Q},{hex.R}|{cardId}"));
    }

    /// <summary>Seed theo một cạnh nối giữa hai ô, không phụ thuộc thứ tự hai ô.</summary>
    public static DecorRandom ForEdge(HexCoordinates a, HexCoordinates b, string salt = null)
    {
        bool aFirst = a.Q < b.Q || (a.Q == b.Q && a.R <= b.R);
        HexCoordinates first = aFirst ? a : b, second = aFirst ? b : a;
        return new DecorRandom(Hash($"{salt}|{first.Q},{first.R}|{second.Q},{second.R}"));
    }

    public static DecorRandom ForKey(string key) => new DecorRandom(Hash(key));

    /// <summary>Gieo lại seed (dùng khi một routine cần bắt đầu lại dãy cho từng phần tử).</summary>
    public void Reseed(string key) { random = new System.Random(Hash(key)); }

    // Cùng quy ước với UnityEngine.Random.Range: số nguyên thì max không lấy, số thực thì lấy cả hai đầu
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return random.Next(minInclusive, maxExclusive);
    }

    public float Range(float minInclusive, float maxInclusive)
    {
        return minInclusive + (float)random.NextDouble() * (maxInclusive - minInclusive);
    }

    public float value => (float)random.NextDouble();

    public Vector2 insideUnitCircle
    {
        get
        {
            float angle = Range(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(value);
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
    }

    public Vector3 insideUnitSphere
    {
        get
        {
            Vector3 point;
            do
            {
                point = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
            } while (point.sqrMagnitude > 1f);
            return point;
        }
    }

    public Quaternion rotation => Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));

    public static int Hash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            if (text != null)
                foreach (char c in text)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
            return (int)hash;
        }
    }
}

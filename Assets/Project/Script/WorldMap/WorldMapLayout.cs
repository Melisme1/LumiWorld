using UnityEngine;

/// <summary>
/// Đổi ô [x:y] của server (lưới 10 cột, toạ độ bắt đầu từ 1) thành vị trí trên màn biển của scene WorldMap.
/// Hàng lẻ lệch nửa ô và mỗi ô lệch nhẹ một khoảng cố định, nên các đảo nằm so le như Ikariam
/// mà lần nào mở cũng đúng chỗ cũ.
/// </summary>
public static class WorldMapLayout
{
    public const int Columns = WorldShareService.MapColumns;
    public const float CellWidth = 5f;
    public const float CellHeight = 3.6f;
    private const float Jitter = 0.7f;

    /// <summary>Tâm đảo ở ô [x:y]. Trục y của thế giới đi xuống theo số hàng.</summary>
    public static Vector3 SlotPosition(int x, int y)
    {
        float px = (x - 1) * CellWidth + (y % 2 == 0 ? CellWidth * 0.5f : 0f);
        float py = -(y - 1) * CellHeight;

        uint h = StableHash.Of(x, y);
        px += ((h & 0xFF) / 255f - 0.5f) * 2f * Jitter;
        py += (((h >> 8) & 0xFF) / 255f - 0.5f) * 2f * Jitter * 0.6f;
        return new Vector3(px, py, 0f);
    }

    /// <summary>Hàng gần nhất với toạ độ y của thế giới (có thể nhỏ hơn 1 hoặc vượt số hàng).</summary>
    public static int RowAt(float worldY) => Mathf.RoundToInt(-worldY / CellHeight) + 1;

    /// <summary>Khung chứa toàn bộ các hàng [1, rows], kèm lề một ô.</summary>
    public static Rect Bounds(int rows)
    {
        rows = Mathf.Max(rows, 1);
        float left = -CellWidth;
        float right = Columns * CellWidth + CellWidth * 0.5f;
        float top = CellHeight;
        float bottom = -rows * CellHeight;
        return Rect.MinMaxRect(left, bottom, right, top);
    }
}

/// <summary>
/// Hash FNV-1a ổn định giữa các lần chạy và các máy (string.GetHashCode thì không),
/// dùng để chọn kiểu đảo theo playerId và độ lệch theo ô.
/// </summary>
public static class StableHash
{
    public static uint Of(string text)
    {
        uint hash = 2166136261;
        if (string.IsNullOrEmpty(text)) return hash;
        foreach (char c in text)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }

    public static uint Of(int a, int b)
    {
        uint hash = 2166136261;
        hash = (hash ^ (uint)a) * 16777619;
        hash = (hash ^ (uint)b) * 16777619;
        hash ^= hash >> 13;
        hash *= 0x5bd1e995;
        hash ^= hash >> 15;
        return hash;
    }
}

using UnityEngine;

public static class HexMetrics
{
    // Bán kính từ tâm ra đỉnh nhọn (Z size = 2.309 -> R = 1.1547f = 2 / sqrt(3))
    public const float OuterRadius = 1.1547f;
    public const float InnerRadius = 1.0f; // Bán kính trong (X size / 2 = 1.0f)
    public const float TileHeight = 1.0f;  // Chiều cao khối (Y size = 1.0f)
    public const float DefaultTileY = -0.5f; // Mực nền của các khối lục giác chìm dưới nước (-0.5f)

    // Đổi từ Hex Coordinates sang World Position (Pointed-Top)
    public static Vector3 HexToWorldPosition(HexCoordinates hex, float yElevation = DefaultTileY)
    {
        // Khoảng cách ngang giữa các cột = 2 * InnerRadius = 2.0f
        float x = (2f * hex.Q + hex.R) * InnerRadius;

        // Khoảng cách dọc giữa các hàng = 1.5 * OuterRadius
        float z = (1.5f * hex.R) * OuterRadius;

        return new Vector3(x, yElevation, z);
    }

    // Đổi từ World Position về Hex Coordinates (Pointed-Top)
    public static HexCoordinates WorldToHex(Vector3 worldPos)
    {
        float q = (worldPos.x / (2f * InnerRadius)) - (worldPos.z / (3f * OuterRadius));
        float r = (2f / 3f * worldPos.z) / OuterRadius;

        float s = -q - r;
        int roundQ = Mathf.RoundToInt(q);
        int roundR = Mathf.RoundToInt(r);
        int roundS = Mathf.RoundToInt(s);

        float qDiff = Mathf.Abs(roundQ - q);
        float rDiff = Mathf.Abs(roundR - r);
        float sDiff = Mathf.Abs(roundS - s);

        if (qDiff > rDiff && qDiff > sDiff)
            roundQ = -roundR - roundS;
        else if (rDiff > sDiff)
            roundR = -roundQ - roundS;

        return new HexCoordinates(roundQ, roundR);
    }
}
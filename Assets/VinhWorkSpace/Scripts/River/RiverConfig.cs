using System;
using UnityEngine;

/// <summary>
/// Cấu hình các Prefab mô hình 3D, tỉ lệ scale, độ lệch góc và bảng điểm của Hệ thống Sông Suối
/// </summary>
[CreateAssetMenu(fileName = "RiverConfig", menuName = "LumiWorld/River Config", order = 15)]
public class RiverConfig : ScriptableObject
{
    [Header("3D Model Prefabs (Preserve Style)")]
    [Tooltip("Dải suối thẳng 180° (Variant A / C)")]
    public GameObject straightPrefab;

    [Tooltip("Đầu nguồn suối / Hang đá rỗng cạn (Variant D)")]
    public GameObject springHeadPrefab;

    [Tooltip("Lòng hồ ao nước tròn tĩnh lặng (Variant F)")]
    public GameObject pondBasinPrefab;

    [Tooltip("Thác nước bậc thang 70° (Variant G)")]
    public GameObject waterfallPrefab;


    [Header("Transform & Scale Settings")]
    [Tooltip("Tỉ lệ scale chung cho các mô hình suối (mặc định 1, 1, 1)")]
    public Vector3 modelScale = Vector3.one;

    [Header("Per-Piece Scale Settings (Tỉ lệ co giãn vừa vặn thẩm mỹ với ô lục giác)")]
    [Tooltip("Scale suối thẳng Straight (chiều rộng 0.85, chiều cao 0.85, chiều dài 1.25 vừa vặn kết nối mép ô)")]
    public Vector3 straightScale = new Vector3(0.85f, 0.85f, 1.25f);

    [Tooltip("Scale Thượng Nguồn Spring Head Grotto (mặc định 1.0, 1.0, 1.0 vừa vặn ô)")]
    public Vector3 springHeadScale = new Vector3(1.0f, 1.0f, 1.0f);

    [Tooltip("Scale Hồ Đọng Pond Basin (mặc định ~1.2, 1.0, 1.2)")]
    public Vector3 pondBasinScale = new Vector3(1.2f, 1.0f, 1.2f);

    [Tooltip("Scale Thác Nước Waterfall Ramp (mặc định ~1.0)")]
    public Vector3 waterfallScale = Vector3.one;

    [Tooltip("Độ nâng trục Y chung (mặc định 0.02m)")]
    public float yElevationOffset = 0.02f;

    /// <summary>
    /// Lấy Scale phù hợp cho từng loại prefab để các khúc sông vừa vặn thẩm mỹ với ô lục giác
    /// </summary>
    public Vector3 GetScaleForPiece(RiverPieceType pieceType)
    {
        switch (pieceType)
        {
            case RiverPieceType.Straight:
            case RiverPieceType.Curve120:
            case RiverPieceType.Curve60:
                return (straightScale.sqrMagnitude > 0.01f) ? straightScale : new Vector3(0.32f, 0.3f, 0.28f);
            case RiverPieceType.SpringHead:
                return (springHeadScale.sqrMagnitude > 0.01f) ? springHeadScale : new Vector3(1.0f, 1.0f, 1.0f);
            case RiverPieceType.PondBasin:
                return (pondBasinScale.sqrMagnitude > 0.01f) ? pondBasinScale : new Vector3(1.2f, 1.0f, 1.2f);
            case RiverPieceType.Waterfall:
                return waterfallScale;
            default:
                return modelScale.sqrMagnitude > 0.01f ? modelScale : Vector3.one;
        }
    }

    [Header("Per-Piece Rotation Offsets (Bù góc xoay mô hình chuẩn hình học)")]
    [Tooltip("Độ bù góc xoay cho Suối thẳng Straight (0°: trục Local Z trùng khớp với hướng dòng chảy giữa 2 ô)")]
    public float straightRotationOffset = 0f;

    [Tooltip("Độ bù góc xoay cho Thượng Nguồn Grotto (180° để xoay đầu miệng tràn quay chuẩn xác hướng thẳng vào lòng suối nối)")]
    public float springHeadRotationOffset = 180f;

    [Tooltip("Độ bù góc xoay cho Hồ đọng Pond Basin")]
    public float pondBasinRotationOffset = 0f;

    [Tooltip("Độ bù góc xoay cho Thác nước Waterfall Ramp")]
    public float waterfallRotationOffset = 0f;

    public float GetRotationOffsetForPiece(RiverPieceType pieceType)
    {
        switch (pieceType)
        {
            case RiverPieceType.Straight:
            case RiverPieceType.Curve120:
            case RiverPieceType.Curve60:
                return straightRotationOffset;
            case RiverPieceType.SpringHead:
                return springHeadRotationOffset;
            case RiverPieceType.PondBasin:
                return pondBasinRotationOffset;
            case RiverPieceType.Waterfall:
                return waterfallRotationOffset;
            default:
                return 0f;
        }
    }

    [Header("Piece Position Offsets (Vị trí bù X, Y, Z cho từng mô hình)")]
    [Tooltip("Vị trí bù (X, Y, Z) cho Thượng Nguồn Grotto. (Y là độ nâng tránh chìm đất, mặc định ~0.194m).")]
    public Vector3 springHeadOffset = new Vector3(0f, 0.194f, 0f);

    [Tooltip("Vị trí bù (X, Y, Z) cho Hồ đọng cuối nguồn Pond Basin (Mặc định X=0, Y=0.07, Z=0.17).")]
    public Vector3 pondOffset = new Vector3(0f, 0.07f, 0.17f);

    [Tooltip("Độ bù UV Texture (X, Y) cho mặt nước hồ Pond Basin (hoàn toàn độc lập, không ảnh hưởng vị trí hồ hay dòng chảy suối).")]
    public Vector2 pondTextureOffset = Vector2.zero;

    [Tooltip("Độ nâng trục Y cho mương suối thẳng / cua (mặc định ~0.056m)")]
    public float straightYOffset = 0.056f;

    [Header("1. Single-Tier Waterfall (Thác thường 1 tầng)")]
    [Tooltip("Độ bù vị trí (X, Y, Z) cho Thác nước thường:\n" +
             "• X: Dịch ngang trái/phải vuông góc dòng chảy\n" +
             "• Y: Cao độ đặt máng thác so với nền tầng dưới (mặc định ~0.427m)\n" +
             "• Z: Đẩy tiến/lùi theo hướng dòng chảy (Z > 0: đẩy ra xa vách núi). Mặc định 0.15m.")]
    public Vector3 waterfallOffset = new Vector3(0f, 0.427f, 0.15f);

    [Tooltip("Độ thụt lùi vào trong của mương tại đỉnh thác thường (~0.04m) để mương vươn sát mép vách dốc mà không thò lơ lửng.")]
    public float waterfallInwardOffset = 0.04f;

    [Header("2. Double-Tier Waterfall (Thác vượt 2 tầng Cao -> Thấp)")]
    [Tooltip("Ngưỡng chênh lệch cao độ (m) để kích hoạt thác 2 tầng (mặc định 1.2m: chênh 1 tầng ~0.8m, chênh 2 tầng Cao -> Thấp ~1.6m).")]
    public float doubleWaterfallHeightThreshold = 1.2f;

    [Tooltip("Vị trí cục bộ Local Position (X, Y, Z) của máng thác thứ 2 (tầng trên) so với máng thác thứ 1.\n" +
             "Mặc định theo thông số kiểm nghiệm: X = 0, Y = 0.186, Z = -0.668.")]
    public Vector3 upperWaterfallPosition = new Vector3(0f, 0.186f, -0.668f);

    [Tooltip("Góc xoay cục bộ Local Rotation Euler (X, Y, Z) của máng thác thứ 2 (tầng trên).\n" +
             "Mặc định theo thông số kiểm nghiệm: X = -29.556, Y = 0, Z = 0.")]
    public Vector3 upperWaterfallRotation = new Vector3(-29.556f, 0f, 0f);

    [Tooltip("Tỉ lệ co giãn cục bộ Local Scale của máng thác thứ 2 (tầng trên). Mặc định (1, 1, 1).")]
    public Vector3 upperWaterfallScale = Vector3.one;

    [Tooltip("Độ thụt lùi vào trong của mương tại đỉnh thác 2 tầng (~0.04m - 0.08m). Mặc định 0.04m.")]
    public float doubleWaterfallInwardOffset = 0.04f;

    /// <summary>
    /// Kiểm tra xem độ chênh cao giữa 2 ô có phải là vượt tầng (Cao -> Thấp) hay không
    /// </summary>
    public bool IsDoubleTierWaterfall(float heightDifference)
    {
        float threshold = doubleWaterfallHeightThreshold > 0.1f ? doubleWaterfallHeightThreshold : 1.2f;
        return heightDifference >= threshold;
    }

    [Header("Connecting Trench Offsets (Tinh chỉnh mương nối để khít hang nguồn và đỉnh thác)")]
    [Tooltip("Khoảng cách từ tâm hang tới điểm bắt đầu của mương nối. Giá trị nhỏ hơn (~0.18m - 0.22m) sẽ đẩy mương cắm sâu vào trong lòng hang, bù kín khe đất tím giữa nguồn và mương. Mặc định 0.20m.")]
    public float springHeadMouthDistance = 0.20f;

    [Tooltip("Khoảng cách từ tâm hồ tới điểm kết thúc của mương nối (~0.45m - 0.52m) để mương nối dừng đúng tại đầu ngoài của cổ máng hồ, không đè lên mặt nước hồ. Mặc định 0.48m.")]
    public float pondInletDistance = 0.48f;

    [Tooltip("Độ bù góc xoay Y mặc định nếu mô hình FBX xuất khẩu lệch hướng trong Blender")]
    public float globalRotationOffset = 0f;

    /// <summary>
    /// Lấy độ nâng Y phù hợp cho từng loại prefab để không bị chìm dưới mặt đất
    /// </summary>
    public float GetYOffsetForPiece(RiverPieceType pieceType)
    {
        switch (pieceType)
        {
            case RiverPieceType.SpringHead:
                return springHeadOffset.y;
            case RiverPieceType.PondBasin:
                return pondOffset.y;
            case RiverPieceType.Straight:
            case RiverPieceType.Curve120:
            case RiverPieceType.Curve60:
                return straightYOffset;
            case RiverPieceType.Waterfall:
                return waterfallOffset.y;
            default:
                return yElevationOffset;
        }
    }

    [Header("Scoring Rules (Cơ chế tính điểm phản ứng hai chiều)")]
    [Tooltip("Điểm cơ bản cho mỗi ô lục giác có suối chảy qua")]
    public int pointsPerRiverTile = 3;

    [Tooltip("Điểm cộng hưởng khi suối chảy xuyên qua một ô ĐÃ CÓ Biome (Địa hình)")]
    public int pointsPerCoLocatedBiome = 2;

    [Tooltip("Điểm thưởng cho mỗi ô Biome kề sát bờ suối (Sinh thái ven sông)")]
    public int pointsPerAdjacentBiome = 1;

    [Tooltip("Điểm bến nước muôn loài (Watering Hole) cho mỗi con thú sinh sống ở ô kề sát suối")]
    public int pointsPerAdjacentCreature = 5;

    [Tooltip("Điểm thưởng Bán đảo êm đềm: Khi 1 ô thú được dòng suối uốn lượn ôm từ 2 phía trở lên")]
    public int pointsPeninsulaBonus = 5;

    /// <summary>
    /// Lấy Prefab phù hợp tương ứng với loại miếng suối
    /// </summary>
    public GameObject GetPrefabForPiece(RiverPieceType pieceType)
    {
        switch (pieceType)
        {
            case RiverPieceType.Straight:
            case RiverPieceType.Curve120:
            case RiverPieceType.Curve60:
                return straightPrefab;
            case RiverPieceType.SpringHead:
                return springHeadPrefab;
            case RiverPieceType.PondBasin:
                return pondBasinPrefab != null ? pondBasinPrefab : springHeadPrefab;
            case RiverPieceType.Waterfall:
                return waterfallPrefab;
            default:
                return straightPrefab != null ? straightPrefab : springHeadPrefab;
        }
    }
}

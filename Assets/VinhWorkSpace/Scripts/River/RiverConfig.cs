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

    [Tooltip("Dải suối cua gắt 60° (Prefab mô hình uốn cong riêng)")]
    public GameObject curve60Prefab;

    [Tooltip("Đầu nguồn suối / Hang đá rỗng cạn (Variant D)")]
    public GameObject springHeadPrefab;

    [Tooltip("Ngã ba hợp lưu suối hình chữ Y / Giao lộ (Variant E)")]
    public GameObject fork3WayPrefab;

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

    [Tooltip("Scale suối cua 120° để vươn tới 2 mép ô lục giác (mặc định ~1.6, 0.9, 1.6)")]
    public Vector3 curve120Scale = new Vector3(1.6f, 0.9f, 1.6f);

    [Tooltip("Scale suối cua 60° (mặc định ~1.6, 0.9, 1.6)")]
    public Vector3 curve60Scale = new Vector3(1.6f, 0.9f, 1.6f);

    [Tooltip("Scale ngã ba Fork 3-Way để 3 nhánh vươn tới 3 mép ô lục giác (mặc định ~1.6, 0.9, 1.6)")]
    public Vector3 fork3WayScale = new Vector3(1.6f, 0.9f, 1.6f);

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
                return (straightScale.sqrMagnitude > 0.01f) ? straightScale : new Vector3(0.32f, 0.3f, 0.28f);
            case RiverPieceType.Curve120:
                return (curve120Scale.sqrMagnitude > 0.01f) ? curve120Scale : straightScale;
            case RiverPieceType.Curve60:
                return (curve60Scale.sqrMagnitude > 0.01f) ? curve60Scale : Vector3.one;
            case RiverPieceType.Fork3Way:
                return (fork3WayScale.sqrMagnitude > 0.01f) ? fork3WayScale : straightScale;
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

    [Tooltip("Độ bù góc xoay cho Suối cua 120°")]
    public float curve120RotationOffset = 0f;

    [Tooltip("Độ bù góc xoay cho Suối cua 60°")]
    public float curve60RotationOffset = 0f;

    [Tooltip("Độ bù góc xoay cho Ngã ba Fork 3-Way")]
    public float fork3WayRotationOffset = 0f;

    [Tooltip("Độ bù góc xoay cho Hồ đọng Pond Basin")]
    public float pondBasinRotationOffset = 0f;

    [Tooltip("Độ bù góc xoay cho Thác nước Waterfall Ramp")]
    public float waterfallRotationOffset = 0f;

    public float GetRotationOffsetForPiece(RiverPieceType pieceType)
    {
        switch (pieceType)
        {
            case RiverPieceType.Straight: return straightRotationOffset;
            case RiverPieceType.SpringHead: return springHeadRotationOffset;
            case RiverPieceType.Curve120: return curve120RotationOffset;
            case RiverPieceType.Curve60: return curve60RotationOffset;
            case RiverPieceType.Fork3Way: return fork3WayRotationOffset;
            case RiverPieceType.PondBasin: return pondBasinRotationOffset;
            case RiverPieceType.Waterfall: return waterfallRotationOffset;
            default: return 0f;
        }
    }

    [Header("Per-Piece Vertical Elevation Offsets (Bù cao độ tránh chìm mô hình)")]
    [Tooltip("Độ nâng trục Y cho Thượng Nguồn Grotto (mặc định ~0.29m vì pivot nằm ở tâm khối)")]
    public float springHeadYOffset = 0.285f;

    [Tooltip("Độ bù vị trí (X, Y, Z) cho Thượng Nguồn Grotto (Transform Position cục bộ của prefab trong ô).")]
    public Vector3 springHeadOffset = Vector3.zero;

    [Tooltip("Độ bù vị trí (X, Y, Z) cho Suối cua 60° (Transform Position cục bộ của prefab trong ô).")]
    public Vector3 curve60Offset = Vector3.zero;

    [Tooltip("Độ nâng trục Y cho Hồ đọng cuối nguồn Pond Basin (mặc định ~0.12m)")]
    public float pondBasinYOffset = 0.125f;

    [Header("Pond Offsets (Nơi duy nhất tinh chỉnh vị trí Hồ và Texture Hồ)")]
    [Tooltip("Độ bù vị trí (X, Y, Z) cho mô hình hồ Pond Basin (Transform Position cục bộ của prefab trong ô).")]
    public Vector3 pondOffset = Vector3.zero;

    [Tooltip("Độ bù UV Texture (X, Y) cho mặt nước hồ Pond Basin (hoàn toàn độc lập, không ảnh hưởng vị trí hồ hay dòng chảy suối).")]
    public Vector2 pondTextureOffset = Vector2.zero;

    [Header("Waterfall Offsets (Nơi tinh chỉnh vị trí Thác Nước / Cầu nối vách tầng)")]
    [Tooltip("Độ bù vị trí (X, Y, Z) cho mô hình Thác nước Waterfall Ramp:\n" +
             "• X: Dịch ngang trái/phải vuông góc dòng chảy\n" +
             "• Y: Nâng/hạ cao độ (cộng dồn với waterfallYOffset)\n" +
             "• Z: Đẩy tiến/lùi theo hướng dòng chảy (Z > 0: đẩy ra xa vách núi về phía hạ lưu, tránh bị đâm sâu vào vách núi/lòng đất). Mặc định ~0.18m.")]
    public Vector3 waterfallOffset = new Vector3(0f, 0f, 0.18f);

    [Header("Connecting Trench Offsets (Tinh chỉnh mương nối để khít hang nguồn và đỉnh thác)")]
    [Tooltip("Khoảng cách từ tâm hang tới điểm bắt đầu của mương nối. Giá trị nhỏ hơn (~0.18m - 0.22m) sẽ đẩy mương cắm sâu vào trong lòng hang, bù kín khe đất tím giữa nguồn và mương. Mặc định 0.20m.")]
    public float springHeadMouthDistance = 0.20f;

    [Tooltip("Khoảng cách từ tâm hồ tới điểm kết thúc của mương nối (~0.45m - 0.52m) để mương nối dừng đúng tại đầu ngoài của cổ máng hồ, không đè lên mặt nước hồ. Mặc định 0.48m.")]
    public float pondInletDistance = 0.48f;

    [Tooltip("Độ thụt lùi vào trong của mương tại đỉnh thác nước / vách dốc (~0.04m - 0.08m) để mương vươn dài sát mép đỉnh vách dốc mà không bị thò lơ lửng. Mặc định 0.04m.")]
    public float waterfallInwardOffset = 0.04f;

    [Tooltip("Khoang cach tu tam o toi diem noi muong cua Curved60 (~0.28m - 0.35m) de muong noi khop vao mieng muong cua model Curved60. Mac dinh 0.35m.")]
    public float curve60MouthDistance = 0.35f;

    [Tooltip("Độ nâng trục Y cho Suối thẳng Straight (mặc định ~0.10m)")]
    public float straightYOffset = 0.095f;

    [Tooltip("Độ nâng trục Y cho Suối cua 120° (mặc định ~0.13m)")]
    public float curve120YOffset = 0.135f;

    [Tooltip("Độ nâng trục Y cho Suối cua 60° (mặc định ~0.13m)")]
    public float curve60YOffset = 0.135f;

    [Tooltip("Độ nâng trục Y cho Ngã ba Fork 3-Way (mặc định ~0.08m)")]
    public float fork3WayYOffset = 0.080f;

    [Tooltip("Độ nâng trục Y cho Thác nước Waterfall Ramp (mặc định ~0.48m)")]
    public float waterfallYOffset = 0.477f;

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
                return springHeadYOffset;
            case RiverPieceType.PondBasin:
                return pondBasinYOffset;
            case RiverPieceType.Straight:
                return straightYOffset;
            case RiverPieceType.Curve120:
                return curve120YOffset;
            case RiverPieceType.Curve60:
                return curve60YOffset;
            case RiverPieceType.Fork3Way:
                return fork3WayYOffset;
            case RiverPieceType.Waterfall:
                return waterfallYOffset;
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
                return straightPrefab;
            case RiverPieceType.Curve120:
                return straightPrefab;
            case RiverPieceType.Curve60:
                return curve60Prefab;
            case RiverPieceType.SpringHead:
                return springHeadPrefab;
            case RiverPieceType.Fork3Way:
                return fork3WayPrefab;
            case RiverPieceType.PondBasin:
                return pondBasinPrefab != null ? pondBasinPrefab : springHeadPrefab;
            case RiverPieceType.Waterfall:
                return waterfallPrefab;
            default:
                return straightPrefab != null ? straightPrefab : springHeadPrefab;
        }
    }

}

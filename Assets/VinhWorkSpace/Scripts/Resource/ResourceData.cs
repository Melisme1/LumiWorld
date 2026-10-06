using System;
using UnityEngine;

/// <summary>
/// Blueprint định nghĩa một loại tài nguyên trong LumiWorld (Gỗ, Sữa tươi, Lông cừu, Thảo mộc...).
/// Áp dụng mô hình Data-Driven ScriptableObject (Flyweight Pattern).
/// </summary>
[CreateAssetMenu(fileName = "NewResource", menuName = "LumiWorld/Resource Data", order = 10)]
public class ResourceData : ScriptableObject
{
    [Header("1. Định danh tài nguyên")]
    [Tooltip("Mã định danh duy nhất (ví dụ: wood, milk, wool, herbs...)")]
    public string resourceID = "wood";

    [Tooltip("Tên hiển thị của tài nguyên trong game (ví dụ: Gỗ rừng, Sữa tươi, Lông cừu...)")]
    public string resourceName = "Gỗ rừng";

    [TextArea(2, 4)]
    [Tooltip("Mô tả chi tiết về tài nguyên")]
    public string description = "Gỗ tự nhiên thu hoạch từ những cánh rừng Leafwood bạt ngàn.";

    [Header("2. Hình ảnh & Màu sắc đại diện (UI Visuals)")]
    [Tooltip("Icon đại diện hiển thị trên UI kho đồ / HUD / Thông báo")]
    public Sprite icon;

    [Tooltip("Màu sắc nhận diện đặc trưng của tài nguyên")]
    public Color resourceColor = new Color(0.72f, 0.45f, 0.20f, 1.0f); // Nâu gỗ ấm áp

    [Tooltip("Mã màu Hex dùng cho Rich Text (ví dụ #B87333 cho Gỗ, #38BDF8 cho Nước/Sữa)")]
    public string hexColor = "#B87333";

    [Header("3. Thuộc tính kinh tế & Kho chứa")]
    [Tooltip("Giá trị cơ bản khi quy đổi hoặc tính điểm")]
    public int baseValue = 5;

    [Tooltip("Số lượng tối đa có thể xếp chồng trong một ô kho")]
    public int maxStackSize = 999;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(hexColor))
        {
            hexColor = "#" + ColorUtility.ToHtmlStringRGB(resourceColor);
        }
    }

    /// <summary>
    /// Trả về tên tài nguyên kèm mã màu Rich Text để in ra Debug Log / UI đẹp mắt
    /// </summary>
    public string GetColoredName()
    {
        string colorTag = !string.IsNullOrEmpty(hexColor) ? hexColor : "#FFFFFF";
        return $"<color={colorTag}><b>{resourceName}</b></color>";
    }
}

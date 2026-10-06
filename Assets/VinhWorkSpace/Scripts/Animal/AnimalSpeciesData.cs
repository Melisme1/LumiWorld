using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Các phẩm cấp (Rank) của động vật tương ứng với cấp sao từ 1★ đến 5★
/// </summary>
public enum AnimalRank
{
    [InspectorName("1★ Phổ thông (Common)")]
    Common = 1,

    [InspectorName("2★ Hảo hạng (Uncommon)")]
    Uncommon = 2,

    [InspectorName("3★ Quý hiếm (Rare)")]
    Rare = 3,

    [InspectorName("4★ Sử thi (Epic)")]
    Epic = 4,

    [InspectorName("5★ Huyền thoại (Legendary)")]
    Legendary = 5
}

/// <summary>
/// Các thiên phú / tính cách độc nhất của con thú
/// </summary>
public enum AnimalTrait
{
    [InspectorName("Năng động (Energetic) - Tốc độ +25%, chu kỳ nhanh hơn 15%")]
    Energetic,

    [InspectorName("Lười biếng (Lazy) - Nghỉ nhiều hơn, nhưng sản xuất tích trữ +1 sản lượng")]
    Lazy,

    [InspectorName("Thần tài (Resource Hoarder) - Luôn nhận thêm +1 sản lượng tài nguyên")]
    ResourceHoarder,

    [InspectorName("Yêu thiên nhiên (Nature Lover) - Chu kỳ sản xuất nhanh hơn 20%")]
    NatureLover,

    [InspectorName("Hòa đồng (Friendly) - Tăng 10% hiệu suất sản xuất")]
    Friendly,

    [InspectorName("Ngôi sao may mắn (Lucky Star) - 15% cơ hội nhân đôi sản lượng")]
    LuckyStar
}

/// <summary>
/// Blueprint định nghĩa thông số chung của một loài động vật (Flyweight Pattern - ScriptableObject).
/// Tất cả các con thú thuộc cùng loài này đều chia sẻ dữ liệu mẫu ở đây.
/// </summary>
[CreateAssetMenu(fileName = "NewAnimalSpecies", menuName = "LumiWorld/Animal Species Data")]
public class AnimalSpeciesData : ScriptableObject
{
    [Header("1. Định danh loài")]
    [Tooltip("Mã định danh duy nhất của loài (ví dụ: cow, deer, fox...)")]
    public string speciesID = "cow";

    [Tooltip("Tên hiển thị của loài (ví dụ: Bò Sữa, Hươu Rừng, Cáo Đỏ...)")]
    public string speciesName = "Bò Sữa";

    [TextArea(2, 3)]
    public string description = "Loài động vật hiền lành, yêu thích những thảm cỏ xanh mướt và bóng mát rừng cây.";

    [Header("2. Chỉ số di chuyển cơ bản (Base Movement)")]
    [Tooltip("Tốc độ đi bộ cơ bản")]
    public float baseWalkSpeed = 0.5f;

    [Tooltip("Tốc độ chạy cơ bản")]
    public float baseRunSpeed = 1.0f;

    [Tooltip("Bán kính vùng đệm cá nhân")]
    public float personalRadius = 0.38f;

    [Tooltip("Độ cao tiếp xúc mặt đất Y")]
    public float yOffset = 0.35f;

    [Header("3. Năng suất lao động cơ bản (Base Productivity)")]
    [Tooltip("Số lượng tài nguyên khai thác cơ bản mỗi chu kỳ (loại tài nguyên hoàn toàn do Vùng đất quyết định, ví dụ Leafwood -> Gỗ)")]
    public int baseResourceAmount = 1;

    [Tooltip("Thời gian mỗi chu kỳ khai thác / sản xuất (giây)")]
    public float baseProductionInterval = 25f;

    [Header("4. Tỉ lệ xuất hiện các cấp sao (Rank / Star Weights)")]
    [Range(0, 100)] public float weight1Star = 50f; // 1★: 50%
    [Range(0, 100)] public float weight2Star = 28f; // 2★: 28%
    [Range(0, 100)] public float weight3Star = 14f; // 3★: 14%
    [Range(0, 100)] public float weight4Star = 6f;  // 4★: 6%
    [Range(0, 100)] public float weight5Star = 2f;  // 5★: 2% (Huyền thoại)

    [Header("5. Bể thiên phú / tính cách của loài này (Trait Pool)")]
    public List<AnimalTrait> traitPool = new List<AnimalTrait>
    {
        AnimalTrait.Energetic,
        AnimalTrait.Lazy,
        AnimalTrait.ResourceHoarder,
        AnimalTrait.NatureLover,
        AnimalTrait.Friendly,
        AnimalTrait.LuckyStar
    };
}

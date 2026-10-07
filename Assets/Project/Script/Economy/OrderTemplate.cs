using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mẫu đơn hàng của một khách NPC (ví dụ Thợ mộc làng cần 8 đến 12 Gỗ).
/// Bảng Đơn Hàng chọn ngẫu nhiên một mẫu rồi chốt số lượng trong khoảng min-max của từng dòng.
/// Thưởng = tổng (số lượng × baseValue của tài nguyên) × rewardMultiplier.
///
/// Asset mẫu đơn phải nằm trong một thư mục Resources (hiện là Assets/Data/Resources/Orders)
/// để game tự tìm thấy. Thêm mẫu mới chỉ cần tạo asset: Create > LumiWorld > Order Template.
/// </summary>
[CreateAssetMenu(fileName = "order_new", menuName = "LumiWorld/Order Template", order = 11)]
public class OrderTemplate : ScriptableObject
{
    [Header("1. Định danh")]
    [Tooltip("Mã mẫu đơn (ví dụ order_wood_basic). Đơn đã lưu tham chiếu mã này, nên đừng đổi sau khi đã chơi thử.")]
    public string templateId = "order_new";

    [Header("2. Khách NPC")]
    [Tooltip("Tên khách hiện trên thẻ đơn")]
    public string customerName = "Khách hàng";

    [Tooltip("Ảnh khách. Để trống thì thẻ đơn hiện chữ cái đầu của tên khách trên nền tròn màu portraitColor.")]
    public Sprite portrait;

    [Tooltip("Màu nền tròn sau chữ cái đầu khi chưa có ảnh khách")]
    public Color portraitColor = new Color(0.22f, 0.47f, 0.76f, 1f);

    [Header("3. Yêu cầu và thưởng")]
    [Tooltip("1 đến 3 loại tài nguyên, mỗi loại có khoảng số lượng")]
    public List<OrderRequirement> requirements = new List<OrderRequirement>();

    [Tooltip("Hệ số thưởng: 1,5 cho đơn một loại, 1,6 trở lên cho đơn nhiều loại. Luôn trên 1 để giao đơn lợi hơn bán cho thương nhân.")]
    [Min(1f)]
    public float rewardMultiplier = 1.5f;

    [Tooltip("Đơn cơ bản: dùng cho ô đơn luôn có sẵn (ô đầu tiên, không bỏ được). Nên là đơn một loại, số lượng vừa phải.")]
    public bool isBaseline;

    private void OnValidate()
    {
        if (requirements == null) return;

        foreach (OrderRequirement requirement in requirements)
        {
            if (requirement == null) continue;
            requirement.minAmount = Mathf.Max(1, requirement.minAmount);
            requirement.maxAmount = Mathf.Max(requirement.minAmount, requirement.maxAmount);
        }
    }
}

/// <summary>
/// Một dòng yêu cầu của mẫu đơn: loại tài nguyên và khoảng số lượng.
/// </summary>
[Serializable]
public class OrderRequirement
{
    public ResourceData resource;

    [Min(1)]
    public int minAmount = 10;

    [Min(1)]
    public int maxAmount = 10;
}

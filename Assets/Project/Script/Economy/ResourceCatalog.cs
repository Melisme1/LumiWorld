using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Danh sách mọi loại tài nguyên (ResourceData) trong game, nạp một lần từ các thư mục Resources
/// (hiện là Assets/Data/Resources). Kho và file lưu chỉ giữ resourceID, nên phần nào cần icon,
/// màu hay tên (InventoryUI, sau này là Bảng Đơn Hàng) thì tra ngược ResourceData ở đây.
/// Thêm loại tài nguyên mới: tạo asset ResourceData trong Assets/Data/Resources là đủ.
/// </summary>
public static class ResourceCatalog
{
    private static List<ResourceData> all;

    /// <summary>
    /// Các loại tài nguyên, xếp theo tên hiển thị.
    /// </summary>
    public static IReadOnlyList<ResourceData> All
    {
        get
        {
            if (all == null) Load();
            return all;
        }
    }

    public static ResourceData Find(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return null;

        foreach (ResourceData resource in All)
        {
            if (resource.resourceID == resourceId) return resource;
        }
        return null;
    }

    // Khi tắt Domain Reload lúc vào Play Mode, biến static không tự xóa: nạp lại danh sách mỗi lần chạy
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        all = null;
    }

    private static void Load()
    {
        all = new List<ResourceData>();
        HashSet<string> seenIds = new HashSet<string>();

        foreach (ResourceData resource in Resources.LoadAll<ResourceData>(string.Empty))
        {
            if (string.IsNullOrEmpty(resource.resourceID))
            {
                Debug.LogWarning($"[LumiWorld Kho] ResourceData '{resource.name}' chưa có resourceID nên bị bỏ qua.", resource);
                continue;
            }

            if (!seenIds.Add(resource.resourceID))
            {
                Debug.LogWarning($"[LumiWorld Kho] Trùng resourceID '{resource.resourceID}' ở '{resource.name}', chỉ dùng asset gặp trước.", resource);
                continue;
            }

            all.Add(resource);
        }

        all.Sort((a, b) => string.CompareOrdinal(a.resourceName, b.resourceName));

        if (all.Count == 0)
        {
            Debug.LogWarning("[LumiWorld Kho] Không tìm thấy ResourceData nào trong thư mục Resources (Assets/Data/Resources).");
        }
    }
}

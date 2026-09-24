using UnityEngine;

/// <summary>
/// Chuyên trách việc bắn tia (Raycast) từ vị trí con trỏ màn hình xuống bản đồ lục giác.
/// Xác định tọa độ ô HexCoordinates và độ cao mặt bằng Y (surfaceY).
/// </summary>
public class HexPointerRaycaster : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] private LayerMask mapLayerMask = ~0;
    [SerializeField] private float minScreenYRatio = 0.18f; // Tránh bấm trúng khu vực bài trên tay
    [SerializeField] private float rayDistance = 1000f;

    private Camera cachedCamera;
    private readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

    public LayerMask MapLayerMask
    {
        get => mapLayerMask;
        set => mapLayerMask = value;
    }

    public void Initialize(Camera cam, LayerMask layerMask)
    {
        cachedCamera = cam;
        mapLayerMask = layerMask;
    }

    /// <summary>
    /// Kiểm tra xem con trỏ chuột có đang nằm ở khu vực khay bài (Hand) phía dưới không
    /// </summary>
    public bool IsOverHandArea(Vector2 screenPosition)
    {
        return screenPosition.y < Screen.height * minScreenYRatio;
    }

    /// <summary>
    /// Bắn tia Ray từ vị trí màn hình xuống để tìm điểm chạm và quy đổi ra ô HexCoordinates
    /// </summary>
    public bool TryRaycastHex(Vector2 screenPosition, Camera cam, out HexCoordinates targetHex, out Vector3 hitPoint)
    {
        targetHex = default;
        hitPoint = Vector3.zero;

        if (cam == null) cam = cachedCamera != null ? cachedCamera : Camera.main;
        if (cam == null) return false;

        if (IsOverHandArea(screenPosition)) return false;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        bool hasHit = false;

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, mapLayerMask))
        {
            hitPoint = hit.point;
            hasHit = true;
        }
        else if (groundPlane.Raycast(ray, out float enter))
        {
            hitPoint = ray.GetPoint(enter);
            hasHit = true;
        }

        if (hasHit)
        {
            targetHex = HexMetrics.WorldToHex(hitPoint);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Xác định độ cao bề mặt trên cùng của ô lục giác để đặt vật phẩm lên đúng đỉnh
    /// </summary>
    public float GetTileSurfaceY(GameObject tileObj)
    {
        if (tileObj != null)
        {
            Renderer[] renderers = tileObj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r != null && !IsPartOfPlacedProp(r.transform, tileObj.transform))
                {
                    return r.bounds.max.y;
                }
            }
        }
        return HexMetrics.TileHeight;
    }

    private bool IsPartOfPlacedProp(Transform t, Transform tileTransform)
    {
        Transform curr = t;
        while (curr != null && curr != tileTransform)
        {
            if (curr.GetComponent<PlacedCard>() != null || curr.name.StartsWith("Habitat_"))
            {
                return true;
            }
            curr = curr.parent;
        }
        return false;
    }
}

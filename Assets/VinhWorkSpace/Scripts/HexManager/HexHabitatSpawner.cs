using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý việc sinh hệ sinh thái Props ngẫu nhiên (Preserve-style) trên ô lục giác
/// Đảm bảo không trùng lặp, không lòi ra ngoài mép ô và không đè lấn lên nhau
/// </summary>
public class HexHabitatSpawner : MonoBehaviour
{
    private static HexHabitatSpawner _instance;
    public static HexHabitatSpawner Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<HexHabitatSpawner>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("HexHabitatSpawner");
                    _instance = go.AddComponent<HexHabitatSpawner>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Spawn Settings")]
    [Tooltip("Thời gian trễ giữa mỗi prop khi mọc lên (tạo hiệu ứng tuần tự tự nhiên)")]
    [SerializeField] private float staggerDelay = 0.035f;

    [Tooltip("Thời gian diễn hoạt trồi nảy của mỗi prop")]
    [SerializeField] private float popDuration = 0.28f;

    // Cache bán kính thực tế của từng Prefab để không phải tính lại nhiều lần
    private static readonly Dictionary<GameObject, float> _radiusCache = new Dictionary<GameObject, float>();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
    }

    /// <summary>
    /// Kích hoạt sinh toàn bộ hệ sinh thái prop lên đỉnh ô lục giác
    /// </summary>
    public void SpawnHabitat(CardData cardData, Transform tileTransform, Vector3 centerWorldPos)
    {
        if (cardData == null || !cardData.HasHabitatProps() || tileTransform == null) return;

        StartCoroutine(SpawnHabitatRoutine(cardData, tileTransform, centerWorldPos));
    }

    private IEnumerator SpawnHabitatRoutine(CardData cardData, Transform tileTransform, Vector3 centerWorldPos)
    {
        // Tạo GameObject cha để gom nhóm toàn bộ props trên ô
        GameObject habitatContainer = new GameObject($"Habitat_{cardData.cardName}");
        habitatContainer.transform.SetParent(tileTransform, true);
        habitatContainer.transform.position = centerWorldPos;

        // Danh sách lưu tọa độ cục bộ (X, Z) và bán kính chiếm dụng của các prop đã đặt
        List<(Vector2 pos2D, float clearanceRadius)> placedProps = new List<(Vector2, float)>();

        // Bán kính vùng an toàn của ô hex (Pointed-top: InnerRadius = 1.0f)
        float maxHexRadius = HexMetrics.InnerRadius * Mathf.Clamp(cardData.habitatSpawnMargin, 0.6f, 0.90f);

        WaitForSeconds waitStagger = (staggerDelay > 0f) ? new WaitForSeconds(staggerDelay) : null;

        // Duyệt qua từng quy tắc prop đã cấu hình trong lá bài
        foreach (var rule in cardData.habitatProps)
        {
            if (rule == null || rule.prefabs == null || rule.prefabs.Count == 0) continue;

            int targetCount = Random.Range(rule.countRange.x, rule.countRange.y + 1);
            if (targetCount <= 0) continue;

            int spawnedThisGroup = 0;
            int maxAttempts = 50; // Cho nhiều lượt thử để luôn tìm được chỗ trống

            for (int attempt = 0; attempt < maxAttempts && spawnedThisGroup < targetCount; attempt++)
            {
                // Chọn ngẫu nhiên 1 prefab trong danh sách biến thể
                GameObject selectedPrefab = rule.prefabs[Random.Range(0, rule.prefabs.Count)];
                if (selectedPrefab == null) continue;

                Vector2 candidatePos2D = Vector2.zero;

                if (rule.placeAtCenter)
                {
                    // Người dùng chủ động chọn đặt ở tâm
                    bool centerOccupied = false;
                    foreach (var placed in placedProps)
                    {
                        if (Vector2.Distance(Vector2.zero, placed.pos2D) < Mathf.Max(rule.minDistance, placed.clearanceRadius))
                        {
                            centerOccupied = true;
                            break;
                        }
                    }

                    if (centerOccupied && placedProps.Count > 0)
                    {
                        continue;
                    }

                    candidatePos2D = Vector2.zero;
                }
                else
                {
                    // Tự do tìm vị trí ngẫu nhiên trên toàn bộ ô lục giác
                    // Mở rộng bán kính để cây và props có thể mọc tỏa ra sát mép và 6 góc của ô
                    float allowedRadius = (rule.minDistance >= 0.45f) ? (maxHexRadius * 0.60f) : (maxHexRadius * 0.95f);

                    bool foundValidSpot = false;
                    for (int trySample = 0; trySample < 40; trySample++)
                    {
                        Vector2 sample = GetRandomPointInHexagon(allowedRadius);

                        bool isFarEnough = true;
                        foreach (var placed in placedProps)
                        {
                            // Kiểm tra khoảng cách tránh đè lên prop đã có
                            float requiredDist = Mathf.Max(rule.minDistance * 0.8f, placed.clearanceRadius);
                            if (Vector2.Distance(sample, placed.pos2D) < requiredDist)
                            {
                                isFarEnough = false;
                                break;
                            }
                        }

                        if (isFarEnough)
                        {
                            candidatePos2D = sample;
                            foundValidSpot = true;
                            break;
                        }
                    }

                    if (!foundValidSpot) continue;
                }

                // Vị trí và góc xoay ngẫu nhiên
                Vector3 worldSpawnPos = centerWorldPos + new Vector3(candidatePos2D.x, 0f, candidatePos2D.y);

                float tiltAngle = Mathf.Max(0f, rule.maxTiltAngle);
                Quaternion randomRotation = Quaternion.Euler(
                    Random.Range(-tiltAngle, tiltAngle),
                    Random.Range(0f, 360f),
                    Random.Range(-tiltAngle, tiltAngle)
                );

                float randomScale = Random.Range(rule.scaleRange.x, rule.scaleRange.y);
                Vector3 targetScale = Vector3.one * randomScale;

                // Instantiate prop
                GameObject propInstance = Instantiate(selectedPrefab, worldSpawnPos, randomRotation, habitatContainer.transform);
                propInstance.name = $"{selectedPrefab.name}_{spawnedThisGroup}";
                propInstance.transform.localScale = Vector3.zero; // Bắt đầu từ 0 để diễn hoạt nảy lên

                // Tự động tắt Collider trên các props trang trí để giải phóng CPU Physics
                Collider[] propColliders = propInstance.GetComponentsInChildren<Collider>();
                for (int c = 0; c < propColliders.Length; c++)
                {
                    propColliders[c].enabled = false;
                }

                // Tắt đổ bóng cho các prop nhỏ (cỏ, hoa, sỏi đá nhỏ) để tiết kiệm draw calls
                if (randomScale < 0.75f || rule.minDistance < 0.35f)
                {
                    Renderer[] propRenderers = propInstance.GetComponentsInChildren<Renderer>();
                    for (int r = 0; r < propRenderers.Length; r++)
                    {
                        propRenderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }

                // Bán kính vùng cấm của prop này để các prop tiếp theo tránh ra
                float footprint = Mathf.Clamp(rule.minDistance * 0.75f, 0.15f, 0.35f);
                placedProps.Add((candidatePos2D, footprint));
                spawnedThisGroup++;

                // Hoạt ảnh trồi nảy nẩy (Pop-in EaseOutBack)
                StartCoroutine(AnimatePropPopIn(propInstance.transform, targetScale));

                if (waitStagger != null)
                {
                    yield return waitStagger;
                }
            }
        }
    }

    /// <summary>
    /// Tự động đo bán kính ngang (X/Z) lớn nhất của prefab để tính khoảng cách chống tràn mép
    /// </summary>
    private float GetPrefabHorizontalRadius(GameObject prefab)
    {
        if (prefab == null) return 0.2f;
        if (_radiusCache.TryGetValue(prefab, out float cached)) return cached;

        float maxExtent = 0f;

        // 1. Thử đọc từ MeshFilter (chính xác nhất cho imported 3D models)
        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>();
        if (filters != null && filters.Length > 0)
        {
            foreach (var mf in filters)
            {
                if (mf != null && mf.sharedMesh != null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    maxExtent = Mathf.Max(maxExtent, b.extents.x, b.extents.z);
                }
            }
        }

        // 2. Dự phòng đọc từ Renderer
        if (maxExtent <= 0.01f)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                foreach (var r in renderers)
                {
                    if (r != null)
                    {
                        maxExtent = Mathf.Max(maxExtent, r.bounds.extents.x, r.bounds.extents.z);
                    }
                }
            }
        }

        if (maxExtent <= 0.01f) maxExtent = 0.25f;

        _radiusCache[prefab] = maxExtent;
        return maxExtent;
    }

    /// <summary>
    /// Sinh điểm 2D nằm gọn bên trong lục giác Pointed-Top bán kính maxInnerRadius
    /// </summary>
    private Vector2 GetRandomPointInHexagon(float maxInnerRadius)
    {
        if (maxInnerRadius <= 0.01f) return Vector2.zero;

        float maxOuterRadius = maxInnerRadius * 1.1547f;

        for (int i = 0; i < 40; i++)
        {
            Vector2 p = Random.insideUnitCircle * maxInnerRadius;

            // Kiểm tra phương trình biên lục giác đều Pointed-top:
            // |x| <= R_inner VÀ (|x| * tan(30 deg) + |y|) <= R_outer
            float absX = Mathf.Abs(p.x);
            float absY = Mathf.Abs(p.y);

            if (absX <= maxInnerRadius && (absX * 0.57735f + absY) <= maxOuterRadius)
            {
                return p;
            }
        }

        return Random.insideUnitCircle * (maxInnerRadius * 0.5f);
    }

    /// <summary>
    /// Hiệu ứng trồi lên nảy nhẹ (EaseOutBack overshoot) giống game Preserve
    /// </summary>
    private IEnumerator AnimatePropPopIn(Transform target, Vector3 finalScale)
    {
        if (target == null) yield break;

        float duration = Mathf.Max(0.05f, popDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Công thức EaseOutBack tạo độ nảy đàn hồi
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float overshoot = 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);

            target.localScale = finalScale * overshoot;
            yield return null;
        }

        if (target != null)
        {
            target.localScale = finalScale;
        }
    }
}

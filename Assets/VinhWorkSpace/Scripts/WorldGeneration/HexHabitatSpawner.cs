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
    private static readonly Dictionary<GameObject, float> _bottomOffsetCache = new Dictionary<GameObject, float>();

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

        // Danh sách lưu tọa độ cục bộ (X, Z), bán kính chiếm dụng, và clusterId (-1 nếu là cây đơn lẻ)
        List<(Vector2 pos2D, float clearanceRadius, int clusterId)> placedProps = new List<(Vector2, float, int)>();

        // Bán kính vùng an toàn của ô hex (Pointed-top: InnerRadius = 1.0f)
        float maxHexRadius = HexMetrics.InnerRadius * Mathf.Clamp(cardData.habitatSpawnMargin, 0.5f, 1.05f);

        WaitForSeconds waitStagger = (staggerDelay > 0f) ? new WaitForSeconds(staggerDelay) : null;
        int nextClusterId = 0;

        // Duyệt qua từng quy tắc prop đã cấu hình trong lá bài
        foreach (var rule in cardData.habitatProps)
        {
            if (rule == null || rule.prefabs == null || rule.prefabs.Count == 0) continue;

            if (rule.clusterAsPatch)
            {
                // =========================================================================
                // CHẾ ĐỘ 1: SINH THEO CỤM / BẦY (CLUSTERS / PATCHES)
                // Khi clusterAsPatch = true, countRange đóng vai trò là SỐ LƯỢNG CỤM trên ô hex
                // =========================================================================
                int numClusters = Random.Range(rule.countRange.x, rule.countRange.y + 1);
                if (numClusters <= 0) continue;

                for (int c = 0; c < numClusters; c++)
                {
                    int propsInCluster = Random.Range(rule.propsPerClusterRange.x, rule.propsPerClusterRange.y + 1);
                    if (propsInCluster <= 0) continue;

                    int thisClusterId = nextClusterId++;

                    // 1. Tìm vị trí đặt tâm cụm (clusterCenter) - Cho phép rải rộng ra khắp ô hex
                    float clusterRadius = Mathf.Clamp(rule.clusterRadius, 0.12f, 0.45f);
                    float allowedCenterRadius = Mathf.Max(0.15f, maxHexRadius - Mathf.Clamp(clusterRadius * 0.5f, 0.05f, 0.22f));

                    Vector2 clusterCenter = Vector2.zero;
                    bool foundClusterCenter = false;
                    float bestCenterScore = -1f;

                    for (int tryCenter = 0; tryCenter < 40; tryCenter++)
                    {
                        Vector2 sampleCenter = GetRandomPointInHexagon(allowedCenterRadius);
                        float minDist = float.MaxValue;
                        bool farEnough = true;

                        foreach (var placed in placedProps)
                        {
                            float d = Vector2.Distance(sampleCenter, placed.pos2D);
                            if (d < minDist) minDist = d;

                            // Khoảng cách tối thiểu giữa tâm cụm với các prop khác (dựa trên minDistance)
                            float requiredDist = Mathf.Max(0.18f, rule.minDistance * 0.80f);
                            if (d < requiredDist)
                            {
                                farEnough = false;
                                break;
                            }
                        }

                        if (farEnough && minDist > bestCenterScore)
                        {
                            bestCenterScore = minDist;
                            clusterCenter = sampleCenter;
                            foundClusterCenter = true;
                        }
                    }

                    // Nếu ô còn chỗ trống cho cụm: sinh cụm này; nếu đã chật cứng (dư ra) thì bỏ qua
                    if (!foundClusterCenter)
                    {
                        if (placedProps.Count == 0)
                        {
                            clusterCenter = Vector2.zero;
                            foundClusterCenter = true;
                        }
                        else
                        {
                            continue; // Ô đã chật, bỏ qua cụm dư thừa này
                        }
                    }

                    // 2. Sinh các cây/bụi tụ lại quây quần xung quanh clusterCenter
                    for (int pIdx = 0; pIdx < propsInCluster; pIdx++)
                    {
                        GameObject selectedPrefab = rule.prefabs[Random.Range(0, rule.prefabs.Count)];
                        if (selectedPrefab == null) continue;

                        float randomScale = Random.Range(rule.scaleRange.x, rule.scaleRange.y);
                        Vector2 propPos2D = clusterCenter;

                        if (pIdx == 0)
                        {
                            // Bụi / cây đầu tiên đặt ngay tâm cụm (lệch ngẫu nhiên cực nhẹ 2-3cm)
                            propPos2D = clusterCenter + Random.insideUnitCircle * 0.03f;
                        }
                        else
                        {
                            // Các bụi tiếp theo tìm vị trí quây tụ quanh clusterCenter trong bán kính clusterRadius
                            bool foundSpotInCluster = false;
                            Vector2 bestSpotInCluster = clusterCenter;
                            float bestDistToSibling = -1f;

                            for (int trySpot = 0; trySpot < 30; trySpot++)
                            {
                                float angle = Random.Range(0f, Mathf.PI * 2f);
                                float dist = Random.Range(0.08f, clusterRadius);
                                Vector2 candidateSpot = clusterCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                                float siblingFootprint = GetPrefabHorizontalFootprint(selectedPrefab, rule, randomScale);
                                if (!IsPointInHexagon(candidateSpot, Mathf.Max(0.12f, maxHexRadius - siblingFootprint))) continue;

                                bool validWithAll = true;
                                float minSiblingDist = float.MaxValue;

                                foreach (var placed in placedProps)
                                {
                                    float d = Vector2.Distance(candidateSpot, placed.pos2D);

                                    if (placed.clusterId == thisClusterId)
                                    {
                                        // Các bụi anh em trong cùng 1 cụm: cho phép đứng gần nhau (0.09m - 0.12m)
                                        if (d < 0.09f)
                                        {
                                            validWithAll = false;
                                            break;
                                        }
                                        if (d < minSiblingDist) minSiblingDist = d;
                                    }
                                    else
                                    {
                                        // Với các prop ngoài cụm: giữ khoảng cách mềm
                                        if (d < Mathf.Max(0.14f, rule.minDistance * 0.60f))
                                        {
                                            validWithAll = false;
                                            break;
                                        }
                                    }
                                }

                                if (validWithAll && minSiblingDist > bestDistToSibling)
                                {
                                    bestDistToSibling = minSiblingDist;
                                    bestSpotInCluster = candidateSpot;
                                    foundSpotInCluster = true;
                                }
                            }

                            if (foundSpotInCluster)
                            {
                                propPos2D = bestSpotInCluster;
                            }
                            else
                            {
                                continue; // Chỗ trong cụm đã hết, bỏ qua cây con này
                            }
                        }

                        // Tạo prop thực tế
                        SpawnPropInstance(selectedPrefab, propPos2D, rule, randomScale, habitatContainer.transform, centerWorldPos, $"{rule.groupName}_C{c}_{pIdx}");
                        placedProps.Add((propPos2D, Mathf.Max(0.12f, rule.minDistance * 0.65f), thisClusterId));

                        if (waitStagger != null)
                        {
                            yield return waitStagger;
                        }
                    }
                }
            }
            else
            {
                // =========================================================================
                // CHẾ ĐỘ 2: RẢI CÂY ĐƠN LẺ (SINGLE PROPS / PACKING)
                // Đưa thông số dư dả: cố gắng lấp đầy ô, chỗ nào hết chỗ thì bỏ qua
                // =========================================================================
                int targetCount = Random.Range(rule.countRange.x, rule.countRange.y + 1);
                if (targetCount <= 0) continue;

                int spawnedThisGroup = 0;
                int maxAttempts = 40;

                for (int i = 0; i < targetCount; i++)
                {
                    GameObject selectedPrefab = rule.prefabs[Random.Range(0, rule.prefabs.Count)];
                    if (selectedPrefab == null) continue;

                    float randomScale = Random.Range(rule.scaleRange.x, rule.scaleRange.y);
                    Vector2 candidatePos2D = Vector2.zero;

                    if (rule.placeAtCenter)
                    {
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
                        // Tự động trừ hao bán kính chân đế/tán cây (footprint) để cây to không bị thò rễ ra ngoài mép
                        float footprint = GetPrefabHorizontalFootprint(selectedPrefab, rule, randomScale);
                        float allowedRadius = Mathf.Max(0.12f, maxHexRadius - footprint);
                        bool foundValidSpot = false;
                        Vector2 bestSpot = Vector2.zero;
                        float bestScore = -1f;

                        for (int trySample = 0; trySample < maxAttempts; trySample++)
                        {
                            Vector2 sample = GetRandomPointInHexagon(allowedRadius);
                            float minDist = float.MaxValue;
                            bool isFarEnough = true;

                            foreach (var placed in placedProps)
                            {
                                float d = Vector2.Distance(sample, placed.pos2D);
                                if (d < minDist) minDist = d;

                                // Kiểm tra khoảng cách: minDistance của rule chính là tiêu chuẩn ngăn cách
                                float requiredDist = Mathf.Max(rule.minDistance, placed.clearanceRadius * 0.85f);
                                if (d < requiredDist)
                                {
                                    isFarEnough = false;
                                    break;
                                }
                            }

                            if (isFarEnough && minDist > bestScore)
                            {
                                bestScore = minDist;
                                bestSpot = sample;
                                foundValidSpot = true;
                            }
                        }

                        if (foundValidSpot)
                        {
                            candidatePos2D = bestSpot;
                        }
                        else
                        {
                            // Ô đã kín chỗ theo khoảng cách minDistance -> Bỏ qua phần dư này
                            continue;
                        }
                    }

                    SpawnPropInstance(selectedPrefab, candidatePos2D, rule, randomScale, habitatContainer.transform, centerWorldPos, $"{selectedPrefab.name}_{spawnedThisGroup}");

                    placedProps.Add((candidatePos2D, rule.minDistance, -1));
                    spawnedThisGroup++;

                    if (waitStagger != null)
                    {
                        yield return waitStagger;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tạo instance của một prop, xử lý chân đế tiếp đất Y, xoay ngẫu nhiên, tắt collider và chạy hiệu ứng nảy Pop-in
    /// </summary>
    private GameObject SpawnPropInstance(
        GameObject selectedPrefab,
        Vector2 pos2D,
        CardData.HabitatPropRule rule,
        float randomScale,
        Transform container,
        Vector3 centerWorldPos,
        string instanceName)
    {
        float yOffset = GetPrefabBottomYOffset(selectedPrefab, rule.customGroundEmbed) * randomScale;
        Vector3 worldSpawnPos = centerWorldPos + new Vector3(pos2D.x, yOffset, pos2D.y);

        float tiltAngle = Mathf.Max(0f, rule.maxTiltAngle);
        Quaternion randomRotation = Quaternion.Euler(
            Random.Range(-tiltAngle, tiltAngle),
            Random.Range(0f, 360f),
            Random.Range(-tiltAngle, tiltAngle)
        ) * selectedPrefab.transform.rotation;

        GameObject propInstance = Instantiate(selectedPrefab, worldSpawnPos, randomRotation, container);
        propInstance.name = instanceName;
        propInstance.transform.localScale = Vector3.zero;

        // Tự động tắt Collider trên các props trang trí để giải phóng CPU Physics
        Collider[] propColliders = propInstance.GetComponentsInChildren<Collider>();
        for (int c = 0; c < propColliders.Length; c++)
        {
            propColliders[c].enabled = false;
        }

        // Tắt đổ bóng cho các prop nhỏ (cỏ, hoa, sỏi đá, bụi cụm) để tối ưu draw calls
        if (randomScale < 0.75f || rule.minDistance < 0.35f || rule.clusterAsPatch)
        {
            Renderer[] propRenderers = propInstance.GetComponentsInChildren<Renderer>();
            for (int r = 0; r < propRenderers.Length; r++)
            {
                propRenderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        Vector3 targetScale = selectedPrefab.transform.localScale * randomScale;
        StartCoroutine(AnimatePropPopIn(propInstance.transform, targetScale));
        return propInstance;
    }

    /// <summary>
    /// Tính bán kính chiếm dụng mặt đất (XZ footprint) của prefab sau khi xoay và scale.
    /// Dùng để tự động trừ hao vùng sinh (boundary setback), đảm bảo rễ cây xòe hoặc tán cây to không bao giờ bị thò ra mép vực.
    /// </summary>
    private float GetPrefabHorizontalFootprint(GameObject prefab, CardData.HabitatPropRule rule, float scale)
    {
        if (prefab == null) return 0.05f;

        float baseFootprint = (rule != null ? Mathf.Max(0.04f, rule.minDistance * 0.55f) : 0.05f);

        if (!_radiusCache.TryGetValue(prefab, out float meshFootprintUnscaled))
        {
            meshFootprintUnscaled = 0f;
            MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                Vector3 scaledMin = Vector3.Scale(b.min, prefab.transform.localScale);
                Vector3 scaledMax = Vector3.Scale(b.max, prefab.transform.localScale);
                Quaternion rot = prefab.transform.localRotation;

                float maxRadiusXZ = 0f;
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            Vector3 corner = new Vector3(
                                x == 0 ? scaledMin.x : scaledMax.x,
                                y == 0 ? scaledMin.y : scaledMax.y,
                                z == 0 ? scaledMin.z : scaledMax.z
                            );
                            Vector3 rotCorner = rot * corner;
                            float distXZ = Mathf.Sqrt(rotCorner.x * rotCorner.x + rotCorner.z * rotCorner.z);
                            if (distXZ > maxRadiusXZ) maxRadiusXZ = distXZ;
                        }
                    }
                }

                // Với cây đại thụ (HeroOak), phần rễ bám đất xòe ra chiếm khoảng 60-70% bán kính tán lá
                meshFootprintUnscaled = maxRadiusXZ * 0.65f;
            }

            if (meshFootprintUnscaled <= 0.01f)
            {
                meshFootprintUnscaled = baseFootprint;
            }

            _radiusCache[prefab] = meshFootprintUnscaled;
        }

        float totalFootprint = Mathf.Max(baseFootprint, meshFootprintUnscaled * scale);
        return Mathf.Clamp(totalFootprint, 0.04f, 0.45f);
    }

    /// <summary>
    /// Tự động tính độ cao cần nâng lên để đáy mesh tiếp xúc đúng mặt cỏ (Y=0),
    /// cắm sâu thêm đối với hoa dạng bó (bouquet/stem) để giấu chân cành và hạ tỉ lệ hoa ngang bằng thảm cỏ.
    /// </summary>
    private float GetPrefabBottomYOffset(GameObject prefab, float customEmbed = 0f)
    {
        if (prefab == null) return 0f;

        float offset = 0f;
        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            Vector3 localMin = Vector3.Scale(b.min, prefab.transform.localScale);
            Vector3 localMax = Vector3.Scale(b.max, prefab.transform.localScale);
            Quaternion rot = prefab.transform.localRotation;
            float minY = float.MaxValue;

            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? localMin.x : localMax.x,
                            y == 0 ? localMin.y : localMax.y,
                            z == 0 ? localMin.z : localMax.z
                        );
                        float rotY = (rot * corner).y;
                        if (rotY < minY) minY = rotY;
                    }
                }
            }

            if (minY < -0.01f)
            {
                float groundEmbed = 0.025f;

                if (customEmbed > 0f)
                {
                    groundEmbed = customEmbed;
                }
                else
                {
                    string pName = prefab.name;
                    if (pName.IndexOf("Prairie", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        pName.IndexOf("Bouquet", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        pName.IndexOf("Ranunculus", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Hoa dạng bó cành (PrairieFlowerPatch): cắm sâu 35cm xuống lòng cỏ để giấu sạch chân cành/gốc bó,
                        // đồng thời hạ độ cao của hoa xuống ngang thảm cỏ, không bị cao quá khổ như cây
                        groundEmbed = 0.35f;
                    }
                    else if (pName.IndexOf("Bellflower", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Hoa chuông cành dài: cắm sâu 28cm để giấu phần cuống dài, chỉ nhú phần hoa mềm mại
                        groundEmbed = 0.28f;
                    }
                    else if (pName.IndexOf("Log", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        groundEmbed = 0.055f; // Khúc gỗ nhúng sâu 5.5cm vào mặt cỏ
                    }
                    else if (pName.IndexOf("Bush", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                             pName.IndexOf("Lavender", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        groundEmbed = 0.08f; // Bụi oải hương cắm sâu 8cm để tán hoa ôm sát cỏ
                    }
                    else if (pName.IndexOf("Scatter", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                             pName.IndexOf("Pebble", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        groundEmbed = 0.035f;
                    }
                }

                offset = -minY - groundEmbed;
            }
        }

        return offset;
    }

    /// <summary>
    /// Kiểm tra xem điểm 2D có nằm bên trong lục giác đều Pointed-Top với bán kính trong innerRadius hay không.
    /// Đối với Pointed-Top hexagon:
    /// - Cạnh nhọn đỉnh trên/dưới ở Z = ±outerRadius = ±innerRadius * (2 / sqrt(3)) ~ ±innerRadius * 1.1547f
    /// - Hai cạnh thẳng đứng ở X = ±innerRadius
    /// - Phương trình 4 cạnh nghiêng: |x| * (1 / sqrt(3)) + |y| <= outerRadius
    /// </summary>
    public static bool IsPointInHexagon(Vector2 point, float innerRadius)
    {
        if (innerRadius <= 0.001f) return false;

        float absX = Mathf.Abs(point.x);
        float absY = Mathf.Abs(point.y);
        float outerRadius = innerRadius * 1.1547005f;

        return absX <= innerRadius && (absX * 0.5773503f + absY) <= outerRadius;
    }

    /// <summary>
    /// Sinh điểm 2D ngẫu nhiên phân bố ĐỀU (Uniform Distribution) phủ kín toàn bộ 100% diện tích lục giác Pointed-Top,
    /// lan tỏa ra tận 6 góc nhọn và các cạnh biên mà không bị cắt tỉa hình tròn.
    /// </summary>
    private Vector2 GetRandomPointInHexagon(float maxInnerRadius)
    {
        if (maxInnerRadius <= 0.01f) return Vector2.zero;

        float maxOuterRadius = maxInnerRadius * 1.1547005f;

        // Bounding-box rejection sampling:
        // Diện tích lục giác đều chiếm chính xác 75% diện tích hình chữ nhật bao quanh [-R_in, R_in] x [-R_out, R_out].
        // Do đó tỉ lệ thành công là 75% ngay lần thử đầu tiên, đảm bảo phân bố hoàn toàn đồng đều ra tận 6 góc.
        for (int i = 0; i < 60; i++)
        {
            float rx = Random.Range(-maxInnerRadius, maxInnerRadius);
            float ry = Random.Range(-maxOuterRadius, maxOuterRadius);

            float absX = Mathf.Abs(rx);
            float absY = Mathf.Abs(ry);

            if ((absX * 0.5773503f + absY) <= maxOuterRadius)
            {
                return new Vector2(rx, ry);
            }
        }

        return Vector2.zero;
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

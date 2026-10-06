using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quản lý việc phủ thảm cỏ sinh thái (05_FilledGround) lên toàn bộ bề mặt ô lục giác.
/// Áp dụng thuật toán phân tầng đa lớp (Hierarchical Dual-Layer Infill):
/// - Lớp 1 (GrassClump): Bụi cỏ nền rải đều khắp mặt ô hex.
/// - Lớp 2 (MicroGrassTuft): Mầm cỏ nhỏ phủ viền mép lục giác và quây tụ bám chân gốc cây/đá.
/// - Hiệu ứng diễn hoạt: Sóng gợn sinh mệnh (Ripple Wave Pop-in) tỏa từ tâm ra biên.
/// </summary>
public class HexFilledGroundSpawner : MonoBehaviour
{
    private static HexFilledGroundSpawner _instance;
    public static HexFilledGroundSpawner Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<HexFilledGroundSpawner>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("HexFilledGroundSpawner");
                    _instance = go.AddComponent<HexFilledGroundSpawner>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Prefabs (05_FilledGround)")]
    [Tooltip("Prefab bụi cỏ chính GrassClump (kích thước ~10cm ngang, 9cm cao)")]
    [SerializeField] private GameObject grassClumpPrefab;
    public GameObject GrassClumpPrefab => grassClumpPrefab;

    [Tooltip("Prefab mầm cỏ nhỏ MicroGrassTuft (kích thước ~5cm ngang, 2.5cm cao)")]
    [SerializeField] private GameObject microGrassTuftPrefab;
    public GameObject MicroGrassTuftPrefab => microGrassTuftPrefab;

    [Header("Infill Density (Mật độ phủ cỏ)")]
    [Tooltip("Số lượng bụi cỏ chính GrassClump trên ô hex")]
    [SerializeField] private Vector2Int grassClumpCountRange = new Vector2Int(30, 40);

    [Tooltip("Số lượng mầm cỏ nhỏ MicroGrassTuft (phủ viền mép và quây quanh chân cây/đá)")]
    [SerializeField] private Vector2Int microTuftCountRange = new Vector2Int(50, 80);

    [Tooltip("Tỷ lệ bán kính phủ cỏ theo InnerRadius (0.9 - 0.95 để cỏ phủ sát gờ vát ngoài cùng)")]
    [Range(0.85f, 1.0f)]
    [SerializeField] private float hexCoverageMargin = 0.92f;

    [Header("Clearance & Spacing")]
    [Tooltip("Khoảng cách tối thiểu giữa 2 bụi GrassClump để tạo sự đan xen mềm mại")]
    [SerializeField] private float minClumpDistance = 0.16f;

    [Tooltip("Khoảng cách tối thiểu từ cỏ tới tâm các thân cây lớn / tim đá lớn để tránh mọc xuyên lõi")]
    [SerializeField] private float heavyPropCoreClearance = 0.12f;

    [Header("Ground Embed (Cắm sâu tiếp đất)")]
    [Tooltip("Độ cắm sâu cho GrassClump (khuyến nghị 0.012f = 1.2cm vì chiều cao tổng 9cm)")]
    [SerializeField] private float grassClumpGroundEmbed = 0.012f;

    [Tooltip("Độ cắm sâu cho MicroGrassTuft (khuyến nghị 0.005f = 0.5cm vì chiều cao tổng 2.5cm)")]
    [SerializeField] private float microTuftGroundEmbed = 0.005f;

    [Header("Transform Variations")]
    [Tooltip("Kích thước bụi cỏ chính GrassClump (1.6 - 2.4 tương đương đường kính 16 - 24cm tạo thảm cỏ xòe bồng bềnh)")]
    [SerializeField] private Vector2 clumpScaleRange = new Vector2(1.60f, 2.40f);

    [Tooltip("Kích thước mầm cỏ nhỏ MicroGrassTuft (1.1 - 1.8 giúp mầm cỏ nhú rõ nét xen kẽ chân các bụi to)")]
    [SerializeField] private Vector2 tuftScaleRange = new Vector2(1.10f, 1.80f);

    [Tooltip("Góc nghiêng ngẫu nhiên nhẹ để bụi cỏ nhấp nhô tự nhiên")]
    [SerializeField] private float maxTiltAngle = 4.0f;

    [Header("Animation & Rendering")]
    [Tooltip("Hiệu ứng trồi nảy theo gợn sóng từ tâm ra mép (Ripple Wave Pop-in)")]
    [SerializeField] private bool useRippleWavePopIn = true;

    [Tooltip("Tốc độ lan tỏa của làn sóng cỏ (m/s)")]
    [SerializeField] private float waveSpeed = 3.2f;

    [Tooltip("Thời gian nảy trồi của từng bụi cỏ")]
    [SerializeField] private float popDuration = 0.22f;

    [Tooltip("Tắt đổ bóng (Cast Shadows) cho lớp cỏ nền để tối ưu Draw Calls")]
    [SerializeField] private bool disableGrassShadows = true;

    [Tooltip("Tự động kích hoạt GPU Instancing trên Material")]
    [SerializeField] private bool ensureGPUInstancing = true;

    // Cache để không phải lặp lại việc bật GPU Instancing
    private readonly HashSet<Material> _instancedMaterials = new HashSet<Material>();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        AutoLoadDefaultPrefabsIfNeeded();
    }

    private void Reset()
    {
        AutoLoadDefaultPrefabsIfNeeded();
    }

    /// <summary>
    /// Tự động gán prefab mặc định từ thư mục 05_FilledGround nếu chưa được kéo vào Inspector
    /// </summary>
    public void AutoLoadDefaultPrefabsIfNeeded()
    {
#if UNITY_EDITOR
        if (grassClumpPrefab == null)
        {
            grassClumpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VinhWorkSpace/Prefabs/Leafwood/05_FilledGround/GrassClump.prefab");
        }
        if (microGrassTuftPrefab == null)
        {
            microGrassTuftPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VinhWorkSpace/Prefabs/Leafwood/05_FilledGround/MicroGrassTuft.prefab");
        }
#endif
    }

    /// <summary>
    /// Kiểm tra xem lá bài này có cần sinh tầng phủ cỏ FilledGround hay không
    /// </summary>
    public bool HasFilledGround(CardData cardData)
    {
        if (cardData == null) return false;

        // 1. Kiểm tra nếu trong cardData có quy tắc tên FilledGround / GroundCover
        if (cardData.habitatProps != null)
        {
            foreach (var rule in cardData.habitatProps)
            {
                if (IsFilledGroundRule(rule)) return true;
            }
        }

        // 2. Mặc định áp dụng cho Leafwood (rừng xanh tươi tốt)
        if (!string.IsNullOrEmpty(cardData.cardName) &&
            cardData.cardName.IndexOf("Leafwood", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Nhận diện xem một quy tắc prop có phải là tầng FilledGround hay không
    /// </summary>
    public bool IsFilledGroundRule(CardData.HabitatPropRule rule)
    {
        if (rule == null) return false;

        string gName = rule.groupName;
        if (!string.IsNullOrEmpty(gName))
        {
            if (gName.IndexOf("FilledGround", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Filled Ground", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("GroundCover", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Ground Cover", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                gName.IndexOf("Cỏ phủ", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        if (rule.prefabs != null)
        {
            foreach (var p in rule.prefabs)
            {
                if (p != null)
                {
                    if (p == grassClumpPrefab || p == microGrassTuftPrefab ||
                        p.name == "GrassClump" || p.name == "MicroGrassTuft")
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Kích hoạt sinh tầng phủ cỏ lên bề mặt ô lục giác
    /// </summary>
    public void SpawnFilledGround(
        CardData cardData,
        Transform habitatContainer,
        Vector3 centerWorldPos,
        List<Vector2> reservedObstacles = null,
        CardData.HabitatPropRule customRule = null)
    {
        AutoLoadDefaultPrefabsIfNeeded();
        StartCoroutine(SpawnFilledGroundRoutine(cardData, habitatContainer, centerWorldPos, reservedObstacles, customRule));
    }

    public IEnumerator SpawnFilledGroundRoutine(
        CardData cardData,
        Transform habitatContainer,
        Vector3 centerWorldPos,
        List<Vector2> reservedObstacles = null,
        CardData.HabitatPropRule customRule = null)
    {
        if (habitatContainer == null) yield break;

        // Tạo container con riêng cho thảm cỏ để quản lý gọn gàng
        GameObject carpetContainer = new GameObject("FilledGround_GrassCarpet");
        carpetContainer.transform.SetParent(habitatContainer, false);
        carpetContainer.transform.localPosition = Vector3.zero;

        // Xác định Prefab sử dụng (ưu tiên lấy từ customRule nếu có, hoặc dùng prefab mặc định)
        GameObject clumpObj = grassClumpPrefab;
        GameObject tuftObj = microGrassTuftPrefab;

        if (customRule != null && customRule.prefabs != null && customRule.prefabs.Count > 0)
        {
            foreach (var p in customRule.prefabs)
            {
                if (p == null) continue;
                if (p.name.IndexOf("Micro", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    tuftObj = p;
                }
                else
                {
                    clumpObj = p;
                }
            }
            if (clumpObj == null) clumpObj = customRule.prefabs[0];
            if (tuftObj == null) tuftObj = customRule.prefabs[customRule.prefabs.Count - 1];
        }

        if (clumpObj == null && tuftObj == null) yield break;

        // Chuẩn bị danh sách vật cản lớn cần né tâm (VD: thân cây đại thụ tại tâm (0,0) hoặc đá tảng)
        List<Vector2> obstacles = new List<Vector2>();
        if (reservedObstacles != null)
        {
            obstacles.AddRange(reservedObstacles);
        }
        else
        {
            // Mặc định né tâm (0,0) một khoảng nhỏ 12cm để dành chỗ cho thân cây Anchor
            obstacles.Add(Vector2.zero);
        }

        // Bán kính phủ cỏ (Pointed-Top hex, R_in = 1.0f)
        float maxHexRadius = HexMetrics.InnerRadius * Mathf.Clamp(hexCoverageMargin, 0.85f, 0.99f);

        // Danh sách lưu toàn bộ vị trí cỏ đã đặt để kiểm tra khoảng cách
        List<Vector2> placedClumps = new List<Vector2>();
        List<Vector2> placedTufts = new List<Vector2>();

        // Danh sách chứa các instance cỏ để diễn hoạt Pop-in
        List<(Transform target, Vector3 finalScale, float delay)> animatedInstances = new List<(Transform, Vector3, float)>();

        // =========================================================================
        // GIAI ĐOẠN 1: PHỦ LỚP CỎ NỀN CHÍNH (GrassClump)
        // Rải đều 20-26 bụi cỏ khắp mặt ô lục giác theo phân bố Poisson-Relaxed
        // =========================================================================
        if (clumpObj != null)
        {
            int targetClumpCount = Random.Range(grassClumpCountRange.x, grassClumpCountRange.y + 1);
            int attempts = 0;
            int maxAttempts = targetClumpCount * 25;

            while (placedClumps.Count < targetClumpCount && attempts < maxAttempts)
            {
                attempts++;
                Vector2 candidate = GetRandomPointInHexagon(maxHexRadius);

                // 1. Kiểm tra vật cản lớn (né tâm lõi thân cây / tim tảng đá)
                bool hitObstacle = false;
                foreach (var obs in obstacles)
                {
                    if (Vector2.Distance(candidate, obs) < heavyPropCoreClearance)
                    {
                        hitObstacle = true;
                        break;
                    }
                }
                if (hitObstacle) continue;

                // 2. Kiểm tra khoảng cách với các bụi GrassClump khác
                bool tooClose = false;
                foreach (var placed in placedClumps)
                {
                    if (Vector2.Distance(candidate, placed) < minClumpDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                placedClumps.Add(candidate);

                // Khởi tạo instance GrassClump
                float scale = Random.Range(clumpScaleRange.x, clumpScaleRange.y);
                GameObject inst = CreateGrassInstance(
                    clumpObj,
                    candidate,
                    grassClumpGroundEmbed,
                    scale,
                    carpetContainer.transform,
                    centerWorldPos,
                    $"Clump_{placedClumps.Count}");

                float distFromCenter = candidate.magnitude;
                float delay = useRippleWavePopIn ? (distFromCenter / Mathf.Max(0.5f, waveSpeed)) : 0f;
                animatedInstances.Add((inst.transform, inst.transform.localScale, delay));
                inst.transform.localScale = Vector3.zero;
            }
        }

        // =========================================================================
        // GIAI ĐOẠN 2: PHỦ LỚP MẦM CỎ VIỀN & CHÂN ĐẾ (MicroGrassTuft)
        // Phủ 35-48 mầm cỏ nhỏ tập trung vào:
        // - Viền mép ngoài cùng (Rim Accent)
        // - Vành đai chân gốc cây / tảng đá (Base Skirt)
        // - Lấp các khe hở còn trống (Infill)
        // =========================================================================
        if (tuftObj != null)
        {
            int targetTuftCount = Random.Range(microTuftCountRange.x, microTuftCountRange.y + 1);

            // Phân bổ: 45% viền ngoài, 25% quây quanh chân cây/đá, 30% rải tự do
            int rimTufts = Mathf.RoundToInt(targetTuftCount * 0.45f);
            int baseSkirtTufts = Mathf.RoundToInt(targetTuftCount * 0.25f);
            int freeTufts = targetTuftCount - rimTufts - baseSkirtTufts;

            // 2A. Viền mép ngoài cùng (0.82 * R -> 0.98 * R)
            for (int r = 0; r < rimTufts; r++)
            {
                float rimRadius = Random.Range(maxHexRadius * 0.82f, maxHexRadius * 0.98f);
                float angle = (r / (float)rimTufts) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
                Vector2 rimPt = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rimRadius;

                if (!HexHabitatSpawner.IsPointInHexagon(rimPt, maxHexRadius))
                {
                    rimPt = GetRandomPointInHexagon(maxHexRadius);
                }

                SpawnTuftInstance(tuftObj, rimPt, placedTufts, carpetContainer.transform, centerWorldPos, animatedInstances);
            }

            // 2B. Quây quanh chân đế vật cản lớn (r = 0.12m .. 0.28m)
            foreach (var obs in obstacles)
            {
                for (int b = 0; b < baseSkirtTufts / Mathf.Max(1, obstacles.Count); b++)
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float d = Random.Range(heavyPropCoreClearance + 0.02f, heavyPropCoreClearance + 0.16f);
                    Vector2 basePt = obs + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * d;

                    if (HexHabitatSpawner.IsPointInHexagon(basePt, maxHexRadius))
                    {
                        SpawnTuftInstance(tuftObj, basePt, placedTufts, carpetContainer.transform, centerWorldPos, animatedInstances);
                    }
                }
            }

            // 2C. Lấp ngách trống tự do
            int freeAttempts = 0;
            while (placedTufts.Count < targetTuftCount && freeAttempts < freeTufts * 20)
            {
                freeAttempts++;
                Vector2 freePt = GetRandomPointInHexagon(maxHexRadius);

                // Né tâm lõi vật cản
                bool hitObstacle = false;
                foreach (var obs in obstacles)
                {
                    if (Vector2.Distance(freePt, obs) < heavyPropCoreClearance)
                    {
                        hitObstacle = true;
                        break;
                    }
                }
                if (hitObstacle) continue;

                SpawnTuftInstance(tuftObj, freePt, placedTufts, carpetContainer.transform, centerWorldPos, animatedInstances);
            }
        }

        // =========================================================================
        // GIAI ĐOẠN 3: DIỄN HOẠT POP-IN THEO SÓNG GỢN (Ripple Wave Animation)
        // Các bụi cỏ nở bung từ tâm ra 6 góc theo sóng tỏa (0.2s - 0.3s)
        // =========================================================================
        foreach (var item in animatedInstances)
        {
            if (item.target != null)
            {
                StartCoroutine(AnimateSingleGrassPopIn(item.target, item.finalScale, item.delay));
            }
        }

        yield break;
    }

    private void SpawnTuftInstance(
        GameObject tuftObj,
        Vector2 pos2D,
        List<Vector2> placedTufts,
        Transform parent,
        Vector3 centerWorldPos,
        List<(Transform, Vector3, float)> animatedList)
    {
        // Kiểm tra dính sát mầm khác (khoảng cách tối thiểu 7cm)
        foreach (var p in placedTufts)
        {
            if (Vector2.Distance(pos2D, p) < 0.07f) return;
        }

        placedTufts.Add(pos2D);

        float scale = Random.Range(tuftScaleRange.x, tuftScaleRange.y);
        GameObject inst = CreateGrassInstance(
            tuftObj,
            pos2D,
            microTuftGroundEmbed,
            scale,
            parent,
            centerWorldPos,
            $"Tuft_{placedTufts.Count}");

        float distFromCenter = pos2D.magnitude;
        float delay = useRippleWavePopIn ? (distFromCenter / Mathf.Max(0.5f, waveSpeed)) : 0f;
        animatedList.Add((inst.transform, inst.transform.localScale, delay));
        inst.transform.localScale = Vector3.zero;
    }

    /// <summary>
    /// Tạo instance của một bụi cỏ, tính chuẩn tiếp đất Y-offset, xoay ngẫu nhiên, tắt collider và tối ưu render
    /// </summary>
    private GameObject CreateGrassInstance(
        GameObject prefab,
        Vector2 pos2D,
        float groundEmbed,
        float scale,
        Transform parent,
        Vector3 centerWorldPos,
        string instanceName)
    {
        float yOffset = CalculateExactBottomY(prefab, groundEmbed) * scale;
        Vector3 worldPos = centerWorldPos + new Vector3(pos2D.x, yOffset, pos2D.y);

        float tilt = Mathf.Max(0f, maxTiltAngle);
        Quaternion rot = Quaternion.Euler(
            Random.Range(-tilt, tilt),
            Random.Range(0f, 360f),
            Random.Range(-tilt, tilt)
        ) * prefab.transform.rotation;

        GameObject inst = Instantiate(prefab, worldPos, rot, parent);
        inst.name = instanceName;

        // Tắt toàn bộ Collider trên cỏ
        Collider[] colliders = inst.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        // Tối ưu Rendering: Tắt bóng đổ & bật GPU Instancing
        Renderer[] renderers = inst.GetComponentsInChildren<Renderer>();
        for (int r = 0; r < renderers.Length; r++)
        {
            if (disableGrassShadows)
            {
                renderers[r].shadowCastingMode = ShadowCastingMode.Off;
                renderers[r].receiveShadows = true;
            }

            if (ensureGPUInstancing && renderers[r].sharedMaterial != null)
            {
                Material mat = renderers[r].sharedMaterial;
                if (!_instancedMaterials.Contains(mat))
                {
                    mat.enableInstancing = true;
                    _instancedMaterials.Add(mat);
                }
            }
        }

        inst.transform.localScale = prefab.transform.localScale * scale;
        return inst;
    }

    /// <summary>
    /// Tính độ cao đáy chính xác của Mesh taking localRotation & localScale into account,
    /// đảm bảo bụi cỏ tiếp xúc chuẩn xác mặt phẳng Y=0 với độ cắm sâu tùy chỉnh.
    /// </summary>
    public static float CalculateExactBottomY(GameObject prefab, float customGroundEmbed)
    {
        if (prefab == null) return 0f;

        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            Vector3 scaledMin = Vector3.Scale(b.min, prefab.transform.localScale);
            Vector3 scaledMax = Vector3.Scale(b.max, prefab.transform.localScale);
            Quaternion rot = prefab.transform.localRotation;
            float minY = float.MaxValue;

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
                        float rotY = (rot * corner).y;
                        if (rotY < minY) minY = rotY;
                    }
                }
            }

            return -minY - customGroundEmbed;
        }

        return -customGroundEmbed;
    }

    /// <summary>
    /// Diễn hoạt nảy trồi từng bụi cỏ có hỗ trợ thời gian chờ (delay) theo sóng gợn
    /// </summary>
    private IEnumerator AnimateSingleGrassPopIn(Transform target, Vector3 finalScale, float startDelay)
    {
        if (target == null) yield break;

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        if (target == null) yield break;

        float duration = Mathf.Max(0.06f, popDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Công thức EaseOutBack nảy nhẹ tạo cảm giác sống động
            const float c1 = 1.4f;
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

    /// <summary>
    /// Lấy điểm ngẫu nhiên phân bố đều bên trong lục giác Pointed-Top bán kính maxInnerRadius
    /// </summary>
    private Vector2 GetRandomPointInHexagon(float maxInnerRadius)
    {
        if (maxInnerRadius <= 0.01f) return Vector2.zero;

        float maxOuterRadius = maxInnerRadius * 1.1547005f;

        for (int i = 0; i < 50; i++)
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
}

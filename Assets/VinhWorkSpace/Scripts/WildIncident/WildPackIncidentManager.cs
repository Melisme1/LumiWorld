using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý Runtime của Hệ thống Thú Hoang Quậy Phá (Wild Pack Incident System):
/// - Tự động nhận diện cụm Biome từ HabitatRuntimeManager (GDD Acs) hoặc PlacedCard.
/// - Áp dụng quy tắc Cluster-First: Chọn Cụm Đầu Não (mốc 3-6-9) ➔ Chọn Loài tương thích ➔ Roll Rank theo Milestone.
/// - Sinh mô hình Alpha Leader tại Cụm Đầu Não (khóa sản xuất) và Minions tại các Cụm Vệ Tinh (làm chậm).
/// - Xử lý khi người chơi click tương tác để thu phục con Alpha và dẹp yên toàn bộ đàn thú trên bản đồ.
/// </summary>
public class WildPackIncidentManager : MonoBehaviour
{
    private static WildPackIncidentManager _instance;
    public static WildPackIncidentManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<WildPackIncidentManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("WildPackIncidentManager");
                    _instance = go.AddComponent<WildPackIncidentManager>();
                }
            }
            return _instance;
        }
    }

    [Header("1. Cấu hình trung tâm (Config)")]
    [SerializeField] private WildIncidentConfig config;

    [Header("2. Giám sát trạng thái sự cố")]
    [SerializeField] private bool enableIncidents = true;
    [SerializeField] private float currentTimer = 0f;
    [SerializeField] private int activeIncidentCount = 0;

    private readonly List<WildPackIncident> activeIncidents = new List<WildPackIncident>();
    public IReadOnlyList<WildPackIncident> ActiveIncidents => activeIncidents;

    private readonly HashSet<string> disruptedHabitatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public event Action<WildPackIncident> OnIncidentSpawned;
    public event Action<WildPackIncident, bool> OnIncidentResolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (config == null)
        {
            config = Resources.Load<WildIncidentConfig>("WildIncidentConfig");
        }
    }

    private void Update()
    {
        if (!enableIncidents || config == null) return;

        // 1. Kiểm tra điều kiện mở khóa theo Milestone tiến trình người chơi
        int currentMilestone = ScoreManager.Instance != null ? ScoreManager.Instance.GrantedMilestoneCount : 0;
        if (!WildIncidentEvaluator.IsFeatureUnlocked(currentMilestone, config.requiredUnlockMilestone))
        {
            return;
        }

        // 2. Không sinh thêm nếu đã chạm trần số đàn tối đa trên map
        if (activeIncidents.Count >= config.maxConcurrentIncidents)
        {
            return;
        }

        // 3. Đếm giờ chu kỳ sự cố
        currentTimer += Time.deltaTime;
        if (currentTimer >= config.incidentIntervalSeconds)
        {
            currentTimer = 0f;
            TriggerNewIncident();
        }

        activeIncidentCount = activeIncidents.Count;
    }

    /// <summary>
    /// Thu thập danh sách toàn bộ các Cụm Biome / Habitat hiện có trên bản đồ:
    /// Hỗ trợ cả HabitatRuntimeManager (GDD Acs) hoặc PlacedCard.
    /// </summary>
    public List<IncidentClusterTarget> GetAllActiveClusters()
    {
        var result = new List<IncidentClusterTarget>();

        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();

        // 1. Kiểm tra HabitatRuntimeManager (Hệ thống GDD Acs chính quy trong scene)
        if (LumiWorld.Acs.HabitatRuntimeManager.TryGetActive(out var habitatManager))
        {
            if (habitatManager.Habitats != null && habitatManager.Habitats.Count > 0)
            {
                foreach (var habitat in habitatManager.Habitats)
                {
                    if (habitat == null) continue;

                    Vector3 sumPos = Vector3.zero;
                    int count = 0;
                    if (habitat.members != null)
                    {
                        foreach (var m in habitat.members)
                        {
                            if (m != null)
                            {
                                float tileSurfaceY = HexMetrics.TileHeight;
                                if (worldGen != null && worldGen.MapTiles.TryGetValue(m.hex, out GameObject tileObj) && tileObj != null)
                                {
                                    var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
                                    tileSurfaceY = topY;
                                }
                                sumPos += HexMetrics.HexToWorldPosition(m.hex, tileSurfaceY);
                                count++;
                            }
                        }
                    }

                    Vector3 center = count > 0 ? sumPos / count : Vector3.zero;
                    string natureKey = habitat.affinity.ToString();
                    CardData terrain = FindMatchingTerrainCard(natureKey);

                    var target = new IncidentClusterTarget
                    {
                        ClusterId = habitat.habitatId,
                        BiomeName = habitat.affinity.ToString(),
                        HexCount = habitat.HexCount,
                        CenterPosition = center,
                        TerrainCard = terrain,
                        NatureKey = natureKey,
                        IsDisrupted = disruptedHabitatIds.Contains(habitat.habitatId),
                        SetDisruptedAction = (disrupted) => {
                            if (disrupted) disruptedHabitatIds.Add(habitat.habitatId);
                            else disruptedHabitatIds.Remove(habitat.habitatId);
                        },
                        SetSpeedMultiplierAction = (mult) => { }
                    };

                    result.Add(target);
                }

                if (result.Count > 0) return result;
            }
        }

        // 2. Fallback an toàn: Quét trực tiếp các PlacedCard trên scene
        var allPlaced = FindObjectsByType<PlacedCard>();
        if (allPlaced != null && allPlaced.Length > 0)
        {
            var groups = new Dictionary<string, List<PlacedCard>>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in allPlaced)
            {
                if (card == null || card.cardData == null || card.CardType != CardType.Terrain) continue;
                string key = string.IsNullOrEmpty(card.cardData.cardID) ? card.cardData.cardName : card.cardData.cardID;
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<PlacedCard>();
                    groups[key] = list;
                }
                list.Add(card);
            }

            foreach (var kvp in groups)
            {
                var list = kvp.Value;
                Vector3 sumPos = Vector3.zero;
                foreach (var p in list)
                {
                    if (p != null)
                    {
                        float tileSurfaceY = p.transform.position.y;
                        if (worldGen != null && worldGen.MapTiles.TryGetValue(p.placedHex, out GameObject tileObj) && tileObj != null)
                        {
                            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
                            tileSurfaceY = topY;
                        }
                        sumPos += new Vector3(p.transform.position.x, tileSurfaceY, p.transform.position.z);
                    }
                }
                Vector3 center = list.Count > 0 ? sumPos / list.Count : Vector3.zero;

                var firstCard = list[0].cardData;
                var target = new IncidentClusterTarget
                {
                    ClusterId = kvp.Key,
                    BiomeName = firstCard.cardName,
                    HexCount = list.Count,
                    CenterPosition = center,
                    TerrainCard = firstCard,
                    NatureKey = firstCard.cardName,
                    SetDisruptedAction = (disrupted) => { },
                    SetSpeedMultiplierAction = (mult) => { }
                };
                result.Add(target);
            }
        }

        return result;
    }

    /// <summary>
    /// Kích hoạt một sự cố đàn thú hoang mới trên bản đồ
    /// </summary>
    [ContextMenu("Trigger Incident Now")]
    public bool TriggerNewIncident()
    {
        if (config == null)
        {
            config = Resources.Load<WildIncidentConfig>("WildIncidentConfig");
            if (config == null)
            {
                Debug.LogWarning("[WildPackIncidentManager] Không tìm thấy file cấu hình WildIncidentConfig!");
                return false;
            }
        }

        var clusters = GetAllActiveClusters();
        if (clusters == null || clusters.Count == 0)
        {
            Debug.LogWarning("[Wild Incident] Chưa tìm thấy Cụm Biome nào trên map! Hãy đặt ít nhất 3 ô Nature cùng loại (ví dụ 3 ô Leafwood hoặc Bloomfield).");
            return false;
        }

        // BƯỚC 1: LỰA CHỌN CỤM ĐẦU NÃO (Host Cluster) THEO QUY TẮC 3 - 6 - 9
        var hostCluster = WildIncidentEvaluator.PickHostCluster(clusters, config.minClusterSizeToHost);
        if (hostCluster == null)
        {
            Debug.LogWarning($"[Wild Incident] Chưa có Cụm Biome nào đạt mốc tối thiểu {config.minClusterSizeToHost} ô! Hiện tại có {clusters.Count} cụm nhưng kích thước đều < {config.minClusterSizeToHost} ô.");
            return false;
        }

        // BƯỚC 2: CHỌN LOÀI TƯƠNG THÍCH THEO BIOME CỦA CỤM
        CardData creatureCard = config.GetCompatibleCreature(hostCluster.TerrainCard, hostCluster.NatureKey);

        // BƯỚC 3: ROLL RANK CỦA CON ALPHA HOÀN TOÀN DỰA THEO TIẾN TRÌNH
        int currentMilestone = ScoreManager.Instance != null ? ScoreManager.Instance.GrantedMilestoneCount : 0;
        AnimalRank alphaRank = config.RollAlphaRank(currentMilestone);

        // BƯỚC 4: TÍNH SỐ LƯỢNG THÚ CON (MINIONS) VÀ CÁC CỤM VỆ TINH LÂN CẬN
        int minionCount = config.CalculateMinionCount(hostCluster.HexCount);
        var satelliteClusters = WildIncidentEvaluator.FindNearbySatelliteClusters(hostCluster, clusters, minionCount);

        // BƯỚC 5: SINH MÔ HÌNH THÚ ĐẦU ĐÀN (ALPHA) TẠI TÂM CỤM ĐẦU NÃO
        GameObject alphaInstance = SpawnCreatureModel(creatureCard, hostCluster.CenterPosition, isAlpha: true, alphaRank);

        // Zoom camera lướt êm vào con Thú Đầu Đàn (Alpha) khi xuất hiện để cảnh báo cho người chơi
        if (alphaInstance != null)
        {
            FocusCameraOnAlpha(alphaInstance.transform.position);
        }

        // BƯỚC 6: SINH CÁC MÔ HÌNH THÚ CON (MINIONS) TẠI CÁC CỤM VỆ TINH
        var minionInstances = new List<GameObject>();
        foreach (var sat in satelliteClusters)
        {
            if (sat != null)
            {
                GameObject minionObj = SpawnCreatureModel(creatureCard, sat.CenterPosition, isAlpha: false, AnimalRank.Common);
                if (minionObj != null)
                {
                    minionInstances.Add(minionObj);
                }
            }
        }

        // BƯỚC 7: TẠO INCIDENT VÀ ÁP DỤNG PHONG TỎA
        string incidentId = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        var incident = new WildPackIncident(
            incidentId,
            hostCluster,
            creatureCard,
            alphaRank,
            alphaInstance,
            satelliteClusters,
            minionInstances,
            config.satelliteSpeedDebuff
        );

        // Gắn component tương tác vào con Alpha
        if (alphaInstance != null)
        {
            var interactable = alphaInstance.AddComponent<WildAlphaInteractable>();
            interactable.Initialize(incident);
        }

        activeIncidents.Add(incident);
        OnIncidentSpawned?.Invoke(incident);

        Debug.Log($"<color=#FFA500><b>[Wild Incident]</b></color> Đàn thú hoang [Alpha {alphaRank} - {creatureCard?.cardName ?? "Linh Thú"}] đã chiếm cứ cụm <b>{hostCluster.BiomeName}</b> ({hostCluster.HexCount} ô)! {satelliteClusters.Count} cụm vệ tinh bị quấy phá.");
        return true;
    }

    /// <summary>
    /// Xử lý khi người chơi nhấp vào con Alpha: Thu phục thành công và giải phóng toàn bộ các cụm bị ảnh hưởng!
    /// </summary>
    public void OnAlphaInteracted(WildPackIncident incident)
    {
        if (incident == null || !activeIncidents.Contains(incident)) return;

        // 1. Thưởng Thẻ Linh Thú chất lượng cao cho người chơi
        if (incident.AlphaCreatureCard != null && CardDeskController.Instance != null)
        {
            CardDeskController.Instance.AddRewardCard(incident.AlphaCreatureCard);
            Debug.Log($"<color=#50C878><b>[Thu phục thành công!]</b></color> Nhận Thẻ Linh Thú: <b>{incident.AlphaCreatureCard.cardName}</b> [{incident.AlphaRank}] bay vào tay!");
        }

        // 2. Dọn dẹp sự cố và giải phóng hòn đảo
        ResolveIncident(incident, rewarded: true);
    }

    /// <summary>
    /// Xóa sự cố và khôi phục hoạt động cho các cụm (kèm hiệu ứng rút lui êm ái)
    /// </summary>
    public void ResolveIncident(WildPackIncident incident, bool rewarded)
    {
        if (incident == null) return;
        StartCoroutine(ResolveIncidentRoutine(incident, rewarded));
    }

    private IEnumerator ResolveIncidentRoutine(WildPackIncident incident, bool rewarded)
    {
        // 1. Giải phóng cụm đất ngay lập tức
        if (incident.HostCluster != null) incident.HostCluster.SetDisrupted(false);
        foreach (var sat in incident.SatelliteClusters)
        {
            if (sat != null) sat.SetSpeedMultiplier(1.0f);
        }

        // 2. Thu thập danh sách mô hình thú đang có mặt
        List<GameObject> allCreatures = new List<GameObject>();
        if (incident.AlphaObject != null) allCreatures.Add(incident.AlphaObject);
        if (incident.MinionObjects != null)
        {
            foreach (var m in incident.MinionObjects)
            {
                if (m != null) allCreatures.Add(m);
            }
        }

        Dictionary<GameObject, Vector3> startScales = new Dictionary<GameObject, Vector3>();
        Dictionary<GameObject, Vector3> startPositions = new Dictionary<GameObject, Vector3>();
        foreach (var obj in allCreatures)
        {
            if (obj != null)
            {
                startScales[obj] = obj.transform.localScale;
                startPositions[obj] = obj.transform.position;
            }
        }

        // 3. Animation nhảy cẫng vui sướng và co nhỏ dần bay vào bộ bài (~0.65s)
        float elapsed = 0f;
        float duration = 0.65f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scaleFactor = 1.0f - Mathf.SmoothStep(0f, 1f, t);
            float hopY = Mathf.Sin(t * Mathf.PI) * 0.45f;

            foreach (var obj in allCreatures)
            {
                if (obj != null && startScales.TryGetValue(obj, out Vector3 baseScale) && startPositions.TryGetValue(obj, out Vector3 basePos))
                {
                    obj.transform.localScale = baseScale * scaleFactor;
                    obj.transform.position = basePos + Vector3.up * hopY;
                }
            }
            yield return null;
        }

        incident.Resolve();
        activeIncidents.Remove(incident);
        currentTimer = 0f; // Reset timer để chờ đợt sau

        OnIncidentResolved?.Invoke(incident, rewarded);
        Debug.Log($"<color=#50C878><b>[Hệ sinh thái an toàn!]</b></color> Đàn thú hoang đã được thuần hóa / rút lui trong hòa bình.");
    }

    [ContextMenu("Resolve All Incidents")]
    public void ResolveAllIncidents()
    {
        var list = new List<WildPackIncident>(activeIncidents);
        foreach (var inc in list)
        {
            ResolveIncident(inc, rewarded: false);
        }
    }

    private CardData FindMatchingTerrainCard(string key)
    {
        if (config == null || config.biomeSpeciesMappings == null || string.IsNullOrEmpty(key)) return null;

        foreach (var m in config.biomeSpeciesMappings)
        {
            if (m != null && m.terrainCard != null)
            {
                if (m.terrainCard.cardName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.terrainCard.cardID.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return m.terrainCard;
                }
            }
        }
        return null;
    }

    private GameObject SpawnCreatureModel(CardData cardData, Vector3 position, bool isAlpha, AnimalRank rank)
    {
        // 1. Xác định bề mặt cỏ chuẩn (Surface Y) tại vị trí spawn
        float surfaceY = position.y;
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        HexCoordinates hex = HexMetrics.WorldToHex(position);
        if (worldGen != null && worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) && tileObj != null)
        {
            var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
            surfaceY = topY;
        }

        // Raycast từ trên cao để bắt chính xác bề mặt lưới địa hình thực tế
        if (Physics.Raycast(new Vector3(position.x, 30f, position.z), Vector3.down, out RaycastHit hit, 50f))
        {
            if (hit.point.y > 0.1f)
            {
                surfaceY = Mathf.Max(surfaceY, hit.point.y);
            }
        }

        GameObject prefab = cardData != null ? cardData.prefabToPlace : null;
        GameObject instance = null;

        Vector3 spawnBasePos = new Vector3(position.x, surfaceY, position.z);

        if (prefab != null)
        {
            instance = Instantiate(prefab, spawnBasePos, Quaternion.identity);
        }
        else
        {
            // Placeholder nếu chưa có prefab
            instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            instance.transform.position = spawnBasePos;
        }

        if (instance != null)
        {
            // Alpha to lớn uy nghi (scale 1.3), Minions nhỏ bé (scale 0.7)
            float scaleMultiplier = isAlpha ? 1.3f : 0.7f;
            instance.transform.localScale = instance.transform.localScale * scaleMultiplier;

            // 2. Tính toán chính xác độ chìm của chân (Hooves/Feet) để đặt con thú đứng phẳng phiu trên mặt cỏ:
            // Đa số model 3D động vật có pivot ở giữa bụng (~0.35m - 0.50m so với lòng bàn chân).
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            float bottomY = float.MaxValue;
            foreach (var r in renderers)
            {
                if (r is SpriteRenderer) continue;
                if (r.bounds.min.y < bottomY)
                {
                    bottomY = r.bounds.min.y;
                }
            }

            float pivotToFeet = 0.45f * scaleMultiplier;
            if (bottomY < float.MaxValue)
            {
                float calculatedOffset = instance.transform.position.y - bottomY;
                if (calculatedOffset > 0.05f)
                {
                    pivotToFeet = calculatedOffset;
                }
            }

            // Nâng pivot lên đúng bằng khoảng cách từ pivot đến bàn chân -> Chân chạm thẳng mặt cỏ!
            float finalPivotY = surfaceY + pivotToFeet;
            instance.transform.position = new Vector3(position.x, finalPivotY, position.z);

            // Gắn collider để bắt click chuột raycast
            Collider col = instance.GetComponentInChildren<Collider>();
            if (col == null)
            {
                SphereCollider sc = instance.AddComponent<SphereCollider>();
                sc.center = new Vector3(0f, 0.45f * scaleMultiplier, 0f);
                sc.radius = 0.65f * scaleMultiplier;
            }

            // Gắn hiệu ứng thị giác phân biệt thú hoang: Vòng tròn đỏ dưới chân + texture đỏ nổi bật + viền đỏ rực rỡ
            var visuals = instance.AddComponent<WildCreatureVisuals>();
            visuals.Initialize(isAlpha, surfaceY);

            // Hiệu ứng xuất hiện nhún nhảy (Spawn pop-in bounce animation)
            StartCoroutine(SpawnEntranceRoutine(instance.transform, instance.transform.localScale));
        }

        return instance;
    }

    /// <summary>
    /// Hiệu ứng xuất hiện nhún nhảy vui tươi khi đàn thú xuất hiện trên đảo
    /// </summary>
    private IEnumerator SpawnEntranceRoutine(Transform targetTransform, Vector3 targetScale)
    {
        if (targetTransform == null) yield break;

        targetTransform.localScale = Vector3.zero;
        float elapsed = 0f;
        float duration = 0.45f;

        while (elapsed < duration)
        {
            if (targetTransform == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Elastic bounce effect: overshoot up to 1.15 then settle smoothly
            float scaleMult = t < 0.7f ? (t / 0.7f) * 1.15f : Mathf.Lerp(1.15f, 1.0f, (t - 0.7f) / 0.3f);
            targetTransform.localScale = targetScale * scaleMult;
            yield return null;
        }

        if (targetTransform != null)
        {
            targetTransform.localScale = targetScale;
        }
    }

    /// <summary>
    /// Di chuyển và zoom camera mượt mà vào Thú Đầu Đàn (Alpha) khi nó xuất hiện
    /// </summary>
    public void FocusCameraOnAlpha(Vector3 alphaPosition)
    {
        var camController = HexCameraController.Instance;
        if (camController == null)
        {
            camController = FindAnyObjectByType<HexCameraController>();
        }

        if (camController != null)
        {
            camController.FocusOnTarget(alphaPosition, targetDistance: 10.5f, duration: 1.25f);
        }
    }
}

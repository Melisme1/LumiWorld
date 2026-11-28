using System.Collections;
using System.Collections.Generic;
using LumiWorld.Acs;
using UnityEngine;

/// <summary>
/// Quản lý hiển thị hiệu ứng buff 3 tầng (Hòa cảnh - Viền địa hình - Rõ khi tương tác) cho Sông và Building:
/// - Tầng 1: Viền địa hình sáng nhẹ một nhịp (~0.85 giây) khi cụm Biome vừa nhận buff mới.
/// - Tầng 2: Đường viền mảnh, dịu mắt chạy dọc mép ngoài cùng của cụm Biome đang được buff.
/// - Tầng 3: Lớp phủ vùng ảnh hưởng dịu mắt khi rê/đặt thẻ Building (từ 3 ô) hoặc Sông.
/// </summary>
public class BiomeBuffVisualManager : MonoBehaviour
{
    private static BiomeBuffVisualManager _instance;
    public static BiomeBuffVisualManager ExistingInstance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<BiomeBuffVisualManager>();
            return _instance;
        }
    }

    public static BiomeBuffVisualManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<BiomeBuffVisualManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("BiomeBuffVisualManager");
                    _instance = go.AddComponent<BiomeBuffVisualManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Harmony Palette (Màu hòa cảnh, dịu mắt trên nền cỏ)")]
    [SerializeField] private Color riverBuffColor = new Color(0.38f, 0.76f, 0.84f, 1f);
    [SerializeField] private Color buildingBuffColor = new Color(0.92f, 0.74f, 0.34f, 1f);
    [SerializeField] private Color combinedBuffColor = new Color(0.66f, 0.62f, 0.94f, 1f);
    [SerializeField] private Color wildIncidentColor = new Color(0.96f, 0.26f, 0.26f, 1f);
    [SerializeField] private float hoverAuraAlpha = 0.20f;
    [SerializeField] private float hoverAffectedBiomeAlpha = 0.34f;

    [Header("Perimeter Border (Tầng 1 & 2 - Viền địa hình + Rim đứng nhẹ)")]
    [SerializeField] private float borderAmbientAlpha = 0.48f;
    [SerializeField] private float borderIncidentAlpha = 0.82f;
    [SerializeField] private float borderPulseAlpha = 0.82f;
    [SerializeField] private float borderPulseDuration = 0.85f;
    [SerializeField] private float borderInnerRadiusRatio = 0.84f;
    [SerializeField] private float borderOuterRadiusRatio = 0.97f;
    [SerializeField] private float borderYOffset = 0.055f;
    [SerializeField] private float borderRimHeight = 0.13f;

    private sealed class ClusterBorderVisual
    {
        public GameObject Root;
        public MeshFilter MeshFilter;
        public MeshRenderer Renderer;
        public Mesh Mesh;
        public Material Material;
        public Coroutine PulseCoroutine;
        public Color BaseColor;
    }

    private enum SelectionBuffKind
    {
        None = 0,
        Building = 1,
        River = 2
    }

    private readonly Dictionary<string, BiomeBuffState> _previousBuffStates = new Dictionary<string, BiomeBuffState>();
    private readonly HashSet<string> _previousIncidentClusters = new HashSet<string>();
    private readonly Dictionary<string, ClusterBorderVisual> _borderVisuals = new Dictionary<string, ClusterBorderVisual>();
    private readonly List<GameObject> _hoverOverlayPool = new List<GameObject>();
    private readonly List<GameObject> _selectionOverlayPool = new List<GameObject>();
    private readonly HashSet<string> _selectedBuffedClusterKeys = new HashSet<string>();

    private Transform _borderRoot;
    private Transform _hoverRoot;
    private Transform _selectionRoot;
    private ClusterBorderVisual _selectionAuraRingVisual;

    private Material _sharedHoverBaseMaterial;
    private Mesh _hexOverlayMesh;

    private HexCoordinates _lastPreviewHex;
    private CardData _lastPreviewCard;
    private bool _hasActivePreview;
    private SelectionBuffKind _selectionBuffKind = SelectionBuffKind.None;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        EnsureContainers();
    }

    private HabitatRuntimeManager _habitatManager;

    private void Start()
    {
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen != null)
        {
            RefreshAllBuffs(worldGen);
        }
        if (HabitatRuntimeManager.TryGetActive(out _habitatManager))
        {
            _habitatManager.HabitatsChanged += OnHabitatsChanged;
        }
    }

    private void OnHabitatsChanged()
    {
        RefreshAllBuffs();
    }

    private void Update()
    {
        if (_selectionAuraRingVisual != null && _selectionAuraRingVisual.Root != null &&
            _selectionAuraRingVisual.Root.activeSelf && _selectionAuraRingVisual.Material != null)
        {
            Color c = _selectionAuraRingVisual.BaseColor;
            float pulse = 0.80f + 0.20f * Mathf.Sin(Time.unscaledTime * 3.2f);
            _selectionAuraRingVisual.Material.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(c.a * pulse));
        }
    }

    private void OnDestroy()
    {
        if (_habitatManager != null)
        {
            _habitatManager.HabitatsChanged -= OnHabitatsChanged;
        }
        foreach (var kvp in _borderVisuals)
        {
            DestroyBorderVisual(kvp.Value);
        }
        _borderVisuals.Clear();
        DestroyBorderVisual(_selectionAuraRingVisual);
        _selectionAuraRingVisual = null;

        if (_sharedHoverBaseMaterial != null) Destroy(_sharedHoverBaseMaterial);
        if (_hexOverlayMesh != null) Destroy(_hexOverlayMesh);

        if (_instance == this)
            _instance = null;
    }

    /// <summary>
    /// Tính lại trạng thái buff cho toàn bộ các cụm Biome và cập nhật viền địa hình Tầng 1 + Tầng 2.
    /// </summary>
    public void RefreshAllBuffs(HexWorldGenerator worldGen = null)
    {
        if (worldGen == null) worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return;

        EnsureContainers();
        EnsureSharedAssets();

        List<BiomeClusterBuffInfo> clusters = EvaluateCurrentBiomeBuffs(worldGen);
        HashSet<HexCoordinates> incidentHexes = CollectActiveIncidentHexes();
        HashSet<string> activeKeys = new HashSet<string>();

        for (int i = 0; i < clusters.Count; i++)
        {
            BiomeClusterBuffInfo info = clusters[i];
            activeKeys.Add(info.ClusterKey);

            _previousBuffStates.TryGetValue(info.ClusterKey, out BiomeBuffState prevState);
            _previousBuffStates[info.ClusterKey] = info.BuffState;

            bool hasWildIncident = ClusterOverlapsHexes(info.Hexes, incidentHexes);
            bool wasInIncident = _previousIncidentClusters.Contains(info.ClusterKey);
            if (hasWildIncident) _previousIncidentClusters.Add(info.ClusterKey);
            else _previousIncidentClusters.Remove(info.ClusterKey);

            bool isSelectedBuffed = _selectionBuffKind != SelectionBuffKind.None &&
                                    _selectedBuffedClusterKeys.Contains(info.ClusterKey) &&
                                    info.BuffState.HasAnyBuff;

            // Chỉ luôn hiện viền đỏ của thú hoang; viền buff Building / Suối ẩn bình thường và chỉ hiện khi click chọn.
            if (!hasWildIncident && !isSelectedBuffed)
            {
                RemoveBorderForCluster(info.ClusterKey);
                continue;
            }

            bool newlyActivated =
                (!wasInIncident && hasWildIncident) ||
                isSelectedBuffed;

            UpdateBorderForCluster(info, worldGen, newlyActivated, hasWildIncident);
        }

        List<string> staleKeys = new List<string>();
        foreach (var kvp in _borderVisuals)
        {
            if (!activeKeys.Contains(kvp.Key)) staleKeys.Add(kvp.Key);
        }
        for (int i = 0; i < staleKeys.Count; i++)
        {
            RemoveBorderForCluster(staleKeys[i]);
            _previousBuffStates.Remove(staleKeys[i]);
            _previousIncidentClusters.Remove(staleKeys[i]);
        }
    }

    /// <summary>
    /// Tầng 3: Hiển thị vùng ảnh hưởng dịu mắt khi đang rê thẻ Building hoặc Sông trên bản đồ.
    /// </summary>
    public void UpdateHoverPreview(CardData cardData, HexCoordinates hoverHex, bool isValid, HexWorldGenerator worldGen)
    {
        if (cardData == null || worldGen == null || worldGen.MapTiles == null)
        {
            HideHoverPreview();
            return;
        }

        bool isBuilding = cardData.IsSettlementCard();
        bool isRiver = cardData.IsRiverCard();
        if (!isBuilding && !isRiver)
        {
            HideHoverPreview();
            return;
        }

        if (_hasActivePreview && _lastPreviewCard == cardData && _lastPreviewHex == hoverHex)
        {
            return;
        }

        if (_selectionBuffKind != SelectionBuffKind.None)
        {
            ClearSelectionBuff(worldGen);
        }

        _hasActivePreview = true;
        _lastPreviewCard = cardData;
        _lastPreviewHex = hoverHex;

        EnsureContainers();
        EnsureSharedAssets();

        HashSet<HexCoordinates> auraHexes;
        Color baseColor;

        if (isBuilding)
        {
            string bType = cardData.GetBuildingTypeId();
            BuildingUpgradeMethod upgMethod = cardData.GetBuildingUpgradeMethod();
            int maxLvl = cardData.GetBuildingMaxLevel();

            auraHexes = BiomeBuffCalculator.CollectBuildingAuraHexes(
                hoverHex,
                bType,
                upgMethod,
                maxLvl,
                1,
                h => IsMatchingPreviewBuildingAt(h, bType, worldGen),
                out int _,
                out int radius);
            baseColor = buildingBuffColor;

            if (radius <= 0)
            {
                HideHoverOverlaysOnly();
                return;
            }
        }
        else
        {
            auraHexes = BiomeBuffCalculator.CollectRiverAuraHexes(hoverHex);
            baseColor = riverBuffColor;
        }

        // Tìm các cụm Biome chạm vào vùng ảnh hưởng (theo luật Chạm 1 ô = Cả Biome nhận buff)
        List<BiomeClusterBuffInfo> clusters = EvaluateCurrentBiomeBuffs(worldGen);
        HashSet<HexCoordinates> affectedBiomeHexes = new HashSet<HexCoordinates>();
        for (int i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            bool touchesAura = false;
            for (int h = 0; h < cluster.Hexes.Count; h++)
            {
                if (auraHexes.Contains(cluster.Hexes[h]))
                {
                    touchesAura = true;
                    break;
                }
            }

            if (touchesAura)
            {
                for (int h = 0; h < cluster.Hexes.Count; h++)
                {
                    affectedBiomeHexes.Add(cluster.Hexes[h]);
                }
            }
        }

        HashSet<HexCoordinates> allDisplayHexes = new HashSet<HexCoordinates>(auraHexes);
        foreach (var h in affectedBiomeHexes) allDisplayHexes.Add(h);

        int poolIndex = 0;
        foreach (var hex in allDisplayHexes)
        {
            if (!worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null) continue;

            bool isAffectedBiome = affectedBiomeHexes.Contains(hex);
            float alpha = isAffectedBiome ? hoverAffectedBiomeAlpha : hoverAuraAlpha;
            if (!isValid) alpha *= 0.45f;

            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            float surfaceY = GetTileSurfaceY(tileObj);
            Vector3 pos = tileObj.transform.position;
            pos.y = surfaceY + 0.015f;

            GameObject overlay = GetOrCreateHoverOverlay(poolIndex++);
            overlay.SetActive(true);
            overlay.transform.position = pos;

            Renderer rend = overlay.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = c;
            }
        }

        for (int i = poolIndex; i < _hoverOverlayPool.Count; i++)
        {
            if (_hoverOverlayPool[i] != null) _hoverOverlayPool[i].SetActive(false);
        }
    }

    public void HideHoverPreview()
    {
        if (!_hasActivePreview) return;
        _hasActivePreview = false;
        _lastPreviewCard = null;
        HideHoverOverlaysOnly();
    }

    private void HideHoverOverlaysOnly()
    {
        for (int i = 0; i < _hoverOverlayPool.Count; i++)
        {
            if (_hoverOverlayPool[i] != null) _hoverOverlayPool[i].SetActive(false);
        }
    }

    public void ShowSelectedBuildingBuff(HexCoordinates clickedHex, HexWorldGenerator worldGen = null)
    {
        if (worldGen == null) worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return;

        EnsureContainers();
        EnsureSharedAssets();

        var clusterInfo = SettlementClusterManager.Instance != null
            ? SettlementClusterManager.Instance.GetClusterInfo(clickedHex)
            : null;

        List<HexCoordinates> cluster;
        int radius;
        if (clusterInfo != null && clusterInfo.Hexes.Count > 0)
        {
            cluster = clusterInfo.Hexes;
            radius = clusterInfo.AuraRadius;
        }
        else
        {
            cluster = BiomeBuffCalculator.CollectConnectedBuildingCluster(
                clickedHex,
                h => IsSameBuildingClusterAt(clickedHex, h, worldGen));
            radius = BiomeBuffCalculator.GetBuildingAuraRadius(cluster.Count);
        }

        if (cluster.Count == 0)
        {
            ClearSelectionBuff(worldGen);
            return;
        }

        HashSet<HexCoordinates> clusterSet = new HashSet<HexCoordinates>(cluster);
        HashSet<HexCoordinates> auraHexes = BiomeBuffCalculator.CollectAuraHexesAroundCluster(cluster, radius);

        _selectionBuffKind = SelectionBuffKind.Building;
        ApplySelectionOverlaysAndBorders(clusterSet, auraHexes, buildingBuffColor, worldGen);
    }

    public void ShowSelectedRiverBuff(HexCoordinates clickedHex, HexWorldGenerator worldGen = null)
    {
        if (worldGen == null) worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen == null || worldGen.MapTiles == null) return;

        EnsureContainers();
        EnsureSharedAssets();

        List<HexCoordinates> riverCluster = BiomeBuffCalculator.CollectConnectedRiverCluster(clickedHex, IsRiverTileAt);
        if (riverCluster.Count == 0)
        {
            ClearSelectionBuff(worldGen);
            return;
        }

        HashSet<HexCoordinates> riverSet = new HashSet<HexCoordinates>(riverCluster);
        HashSet<HexCoordinates> auraHexes = BiomeBuffCalculator.CollectRiverNetworkAuraHexes(riverCluster);

        _selectionBuffKind = SelectionBuffKind.River;
        ApplySelectionOverlaysAndBorders(riverSet, auraHexes, riverBuffColor, worldGen);
    }

    public void ClearSelectionBuff(HexWorldGenerator worldGen = null)
    {
        bool hadSelection = _selectionBuffKind != SelectionBuffKind.None || _selectedBuffedClusterKeys.Count > 0;
        _selectionBuffKind = SelectionBuffKind.None;
        _selectedBuffedClusterKeys.Clear();

        if (_selectionAuraRingVisual != null && _selectionAuraRingVisual.Root != null)
        {
            _selectionAuraRingVisual.Root.SetActive(false);
        }

        for (int i = 0; i < _selectionOverlayPool.Count; i++)
        {
            if (_selectionOverlayPool[i] != null) _selectionOverlayPool[i].SetActive(false);
        }

        if (hadSelection)
        {
            RefreshAllBuffs(worldGen);
        }
    }

    private void ApplySelectionOverlaysAndBorders(
        HashSet<HexCoordinates> sourceHexes,
        HashSet<HexCoordinates> auraHexes,
        Color baseColor,
        HexWorldGenerator worldGen)
    {
        _selectedBuffedClusterKeys.Clear();

        // 1. Vẽ duy nhất 1 vòng bao ngoài cùng quanh phạm vi ảnh hưởng của công trình / suối
        HashSet<HexCoordinates> auraZoneHexes = new HashSet<HexCoordinates>(sourceHexes);
        if (auraHexes != null)
        {
            foreach (var h in auraHexes) auraZoneHexes.Add(h);
        }
        UpdateSelectionAuraRing(auraZoneHexes, sourceHexes, baseColor, worldGen);

        // 2. Chỉ khi có Biome đã hình thành chạm vào vùng ảnh hưởng mới bật buff visual cho Biome đó
        List<BiomeClusterBuffInfo> clusters = EvaluateCurrentBiomeBuffs(worldGen);
        HashSet<HexCoordinates> affectedBiomeHexes = new HashSet<HexCoordinates>();
        for (int i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            bool touchesAura = false;
            for (int h = 0; h < cluster.Hexes.Count; h++)
            {
                if (auraHexes != null && auraHexes.Contains(cluster.Hexes[h]))
                {
                    touchesAura = true;
                    break;
                }
            }

            if (touchesAura)
            {
                _selectedBuffedClusterKeys.Add(cluster.ClusterKey);
                for (int h = 0; h < cluster.Hexes.Count; h++)
                {
                    affectedBiomeHexes.Add(cluster.Hexes[h]);
                }
            }
        }

        int poolIndex = 0;
        foreach (var hex in affectedBiomeHexes)
        {
            if (!worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null) continue;

            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, hoverAffectedBiomeAlpha);
            float surfaceY = GetTileSurfaceY(tileObj);
            Vector3 pos = tileObj.transform.position;
            pos.y = surfaceY + 0.015f;

            GameObject overlay = GetOrCreateOverlayFromPool(_selectionOverlayPool, _selectionRoot, "BuffSelectHex", poolIndex++);
            overlay.SetActive(true);
            overlay.transform.position = pos;

            Renderer rend = overlay.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = c;
            }
        }

        for (int i = poolIndex; i < _selectionOverlayPool.Count; i++)
        {
            if (_selectionOverlayPool[i] != null) _selectionOverlayPool[i].SetActive(false);
        }

        RefreshAllBuffs(worldGen);
    }

    private void UpdateSelectionAuraRing(
        HashSet<HexCoordinates> zoneHexes,
        HashSet<HexCoordinates> sourceHexes,
        Color baseColor,
        HexWorldGenerator worldGen)
    {
        if (zoneHexes == null || zoneHexes.Count == 0)
        {
            if (_selectionAuraRingVisual != null && _selectionAuraRingVisual.Root != null)
                _selectionAuraRingVisual.Root.SetActive(false);
            return;
        }

        if (_selectionAuraRingVisual == null || _selectionAuraRingVisual.Root == null)
        {
            _selectionAuraRingVisual = CreateBorderVisual("SelectionAuraRing");
            _selectionAuraRingVisual.Root.transform.SetParent(_selectionRoot, false);
        }

        if (_selectionAuraRingVisual.Material != null)
        {
            _selectionAuraRingVisual.Material.renderQueue = 3250;
        }

        _selectionAuraRingVisual.Root.SetActive(true);
        RebuildZoneOutlineMesh(_selectionAuraRingVisual.Mesh, zoneHexes, sourceHexes, worldGen);
        Color vividColor = _selectionBuffKind == SelectionBuffKind.River
            ? new Color(0.22f, 0.88f, 1.00f, 0.96f)
            : new Color(1.00f, 0.84f, 0.20f, 0.96f);
        _selectionAuraRingVisual.BaseColor = vividColor;
        _selectionAuraRingVisual.Material.color = vividColor;
    }

    private void RebuildZoneOutlineMesh(
        Mesh mesh,
        HashSet<HexCoordinates> zoneHexes,
        HashSet<HexCoordinates> sourceHexes,
        HexWorldGenerator worldGen)
    {
        mesh.Clear();
        if (zoneHexes == null || zoneHexes.Count == 0) return;

        // Lấy độ cao cao nhất trong cụm/vùng ảnh hưởng để vòng bao luôn nổi rõ, không bị chìm dưới vách đá khi xoay camera
        float maxSurfaceY = HexMetrics.DefaultTileY + HexMetrics.TileHeight;
        if (worldGen != null && worldGen.MapTiles != null)
        {
            foreach (var zh in zoneHexes)
            {
                if (worldGen.MapTiles.TryGetValue(zh, out GameObject tObj) && tObj != null)
                {
                    maxSurfaceY = Mathf.Max(maxSurfaceY, GetTileSurfaceY(tObj));
                }
            }
            if (sourceHexes != null)
            {
                foreach (var sh in sourceHexes)
                {
                    if (worldGen.MapTiles.TryGetValue(sh, out GameObject st) && st != null)
                    {
                        maxSurfaceY = Mathf.Max(maxSurfaceY, GetTileSurfaceY(st));
                    }
                }
            }
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> triangles = new List<int>();

        float rInner = HexMetrics.OuterRadius * 0.78f;
        float rOuter = HexMetrics.OuterRadius * 1.03f;
        Color innerColor = new Color(1f, 1f, 1f, 0.25f);
        Color outerColor = new Color(1f, 1f, 1f, 1f);
        Color topFadeColor = new Color(1f, 1f, 1f, 0f);
        float ringBaseY = maxSurfaceY + 0.14f;
        float rimH = 0.52f;

        foreach (var hex in zoneHexes)
        {
            GameObject tileObj = null;
            bool hasTile = worldGen != null && worldGen.MapTiles != null &&
                           worldGen.MapTiles.TryGetValue(hex, out tileObj) && tileObj != null;
            Vector3 center = hasTile ? tileObj.transform.position : HexMetrics.HexToWorldPosition(hex, HexMetrics.DefaultTileY);

            for (int dir = 0; dir < 6; dir++)
            {
                if (zoneHexes.Contains(hex.GetNeighbor(dir))) continue;

                float normalDeg = -60f * dir;
                float a0 = Mathf.Deg2Rad * (normalDeg - 30f);
                float a1 = Mathf.Deg2Rad * (normalDeg + 30f);

                Vector3 dir0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 dir1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                int baseIdx = vertices.Count;
                Vector3 pInner0 = new Vector3(center.x + dir0.x * rInner, ringBaseY, center.z + dir0.z * rInner);
                Vector3 pOuter0 = new Vector3(center.x + dir0.x * rOuter, ringBaseY, center.z + dir0.z * rOuter);
                Vector3 pOuter1 = new Vector3(center.x + dir1.x * rOuter, ringBaseY, center.z + dir1.z * rOuter);
                Vector3 pInner1 = new Vector3(center.x + dir1.x * rInner, ringBaseY, center.z + dir1.z * rInner);

                vertices.Add(pInner0); colors.Add(innerColor);
                vertices.Add(pOuter0); colors.Add(outerColor);
                vertices.Add(pOuter1); colors.Add(outerColor);
                vertices.Add(pInner1); colors.Add(innerColor);

                // Vẽ 2 mặt cho vòng nằm ngang để nhìn rõ từ mọi góc camera
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 3);
                triangles.Add(baseIdx + 2);

                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 3);

                // Dải hào quang đứng cao 0.52m dễ thấy từ góc nghiêng & trên cao
                int rimIdx = vertices.Count;
                Vector3 pTop0 = new Vector3(pOuter0.x, pOuter0.y + rimH, pOuter0.z);
                Vector3 pTop1 = new Vector3(pOuter1.x, pOuter1.y + rimH, pOuter1.z);

                vertices.Add(pOuter0); colors.Add(outerColor);
                vertices.Add(pTop0); colors.Add(topFadeColor);
                vertices.Add(pTop1); colors.Add(topFadeColor);
                vertices.Add(pOuter1); colors.Add(outerColor);

                triangles.Add(rimIdx);
                triangles.Add(rimIdx + 1);
                triangles.Add(rimIdx + 2);
                triangles.Add(rimIdx);
                triangles.Add(rimIdx + 2);
                triangles.Add(rimIdx + 3);

                triangles.Add(rimIdx);
                triangles.Add(rimIdx + 2);
                triangles.Add(rimIdx + 1);
                triangles.Add(rimIdx);
                triangles.Add(rimIdx + 3);
                triangles.Add(rimIdx + 2);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private List<BiomeClusterBuffInfo> EvaluateCurrentBiomeBuffs(HexWorldGenerator worldGen)
    {
        return BiomeBuffCalculator.EvaluateAllBiomeBuffs(
            worldGen.MapTiles.Keys,
            h => GetBiomeTerrainIdAt(h, worldGen),
            h => GetBiomeClusterIdAt(h, worldGen),
            h => IsBuildingTileAt(h, worldGen),
            IsRiverTileAt,
            (a, b) => IsSameBuildingClusterAt(a, b, worldGen),
            (seed, count) => GetBuildingAuraRadiusAt(seed, count, worldGen));
    }

    private static bool IsMatchingPreviewBuildingAt(HexCoordinates hex, string buildingTypeId, HexWorldGenerator worldGen)
    {
        if (worldGen == null || worldGen.MapTiles == null ||
            !worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null)
            return false;
        SettlementTile st = tileObj.GetComponent<SettlementTile>();
        if (st == null || st.UpgradeMethod != BuildingUpgradeMethod.ClusterExpansion) return false;
        return string.Equals(st.BuildingTypeId, buildingTypeId, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameBuildingClusterAt(HexCoordinates a, HexCoordinates b, HexWorldGenerator worldGen)
    {
        if (SettlementClusterManager.Instance != null)
            return SettlementClusterManager.Instance.IsSameBuildingCluster(a, b);
        if (a == b) return IsBuildingTileAt(a, worldGen);
        if (!worldGen.MapTiles.TryGetValue(a, out var tileA) || tileA == null) return false;
        if (!worldGen.MapTiles.TryGetValue(b, out var tileB) || tileB == null) return false;
        var stA = tileA.GetComponent<SettlementTile>();
        var stB = tileB.GetComponent<SettlementTile>();
        if (stA == null || stB == null) return false;
        if (stA.UpgradeMethod != BuildingUpgradeMethod.ClusterExpansion ||
            stB.UpgradeMethod != BuildingUpgradeMethod.ClusterExpansion)
            return false;
        return string.Equals(stA.BuildingTypeId, stB.BuildingTypeId, System.StringComparison.OrdinalIgnoreCase);
    }

    private static int GetBuildingAuraRadiusAt(HexCoordinates seed, int clusterCount, HexWorldGenerator worldGen)
    {
        if (worldGen != null && worldGen.MapTiles != null &&
            worldGen.MapTiles.TryGetValue(seed, out var tileObj) && tileObj != null)
        {
            var st = tileObj.GetComponent<SettlementTile>();
            if (st != null)
            {
                int lvl = st.UpgradeMethod == BuildingUpgradeMethod.ClusterExpansion
                    ? BuildingRules.GetLevelForClusterSize(clusterCount, st.MaxLevel)
                    : Mathf.Max(1, st.BuildingLevel);
                return BuildingRules.GetAuraRadius(st.BuildingTypeId, lvl);
            }
        }
        return BiomeBuffCalculator.GetBuildingAuraRadius(clusterCount);
    }

    private static string GetBiomeTerrainIdAt(HexCoordinates hex, HexWorldGenerator worldGen)
    {
        if (!worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null) return null;
        if (HabitatRuntimeManager.TryGetActive(out var habitatMgr))
        {
            var habitat = habitatMgr.FindHabitatByHex(hex);
            if (habitat != null)
            {
                if (!habitat.IsFormedBiome) return null;
                return habitat.natureCardId;
            }
        }
        PlacedCard[] cards = tileObj.GetComponentsInChildren<PlacedCard>(true);
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c != null && c.CardType == CardType.Terrain && c.cardData != null)
            {
                return c.cardData.cardID;
            }
        }
        return null;
    }

    private static int GetBiomeClusterIdAt(HexCoordinates hex, HexWorldGenerator worldGen)
    {
        if (!worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null) return 0;
        if (HabitatRuntimeManager.TryGetActive(out var habitatMgr))
        {
            var habitat = habitatMgr.FindHabitatByHex(hex);
            if (habitat != null && habitat.IsFormedBiome && !string.IsNullOrEmpty(habitat.habitatId))
            {
                int hash = habitat.habitatId.GetHashCode() & 0x7FFFFFFF;
                return hash == 0 ? 1 : hash;
            }
        }
        PlacedCard[] cards = tileObj.GetComponentsInChildren<PlacedCard>(true);
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c != null && c.CardType == CardType.Terrain)
            {
                return c.clusterId;
            }
        }
        return 0;
    }

    private static bool IsBuildingTileAt(HexCoordinates hex, HexWorldGenerator worldGen)
    {
        return worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) &&
               tileObj != null &&
               tileObj.GetComponent<SettlementTile>() != null;
    }

    private static bool IsRiverTileAt(HexCoordinates hex)
    {
        return RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.HasRiver(hex);
    }

    private static HashSet<HexCoordinates> CollectActiveIncidentHexes()
    {
        HashSet<HexCoordinates> hexes = new HashSet<HexCoordinates>();
        if (WildPackIncidentManager.Instance == null || WildPackIncidentManager.Instance.ActiveIncidents == null)
            return hexes;

        var incidents = WildPackIncidentManager.Instance.ActiveIncidents;
        for (int i = 0; i < incidents.Count; i++)
        {
            WildPackIncident inc = incidents[i];
            if (inc == null || !inc.IsActive) continue;

            if (inc.HostCluster != null && inc.HostCluster.Hexes != null)
            {
                for (int h = 0; h < inc.HostCluster.Hexes.Count; h++)
                {
                    hexes.Add(inc.HostCluster.Hexes[h]);
                }
            }

            if (inc.SatelliteClusters != null)
            {
                for (int s = 0; s < inc.SatelliteClusters.Count; s++)
                {
                    var sat = inc.SatelliteClusters[s];
                    if (sat != null && sat.Hexes != null)
                    {
                        for (int h = 0; h < sat.Hexes.Count; h++)
                        {
                            hexes.Add(sat.Hexes[h]);
                        }
                    }
                }
            }
        }
        return hexes;
    }

    private static bool ClusterOverlapsHexes(List<HexCoordinates> clusterHexes, HashSet<HexCoordinates> targetHexes)
    {
        if (clusterHexes == null || targetHexes == null || targetHexes.Count == 0) return false;
        for (int i = 0; i < clusterHexes.Count; i++)
        {
            if (targetHexes.Contains(clusterHexes[i])) return true;
        }
        return false;
    }

    private void UpdateBorderForCluster(BiomeClusterBuffInfo info, HexWorldGenerator worldGen, bool pulseActivation, bool hasWildIncident)
    {
        if (!_borderVisuals.TryGetValue(info.ClusterKey, out ClusterBorderVisual visual) || visual == null || visual.Root == null)
        {
            visual = CreateBorderVisual(info.ClusterKey);
            _borderVisuals[info.ClusterKey] = visual;
        }

        RebuildPerimeterRibbonMesh(visual.Mesh, info.Hexes, worldGen);

        Color tint = hasWildIncident
            ? wildIncidentColor
            : (_selectionBuffKind == SelectionBuffKind.Building
                ? buildingBuffColor
                : (_selectionBuffKind == SelectionBuffKind.River ? riverBuffColor : ResolveBorderColor(info.BuffState)));
        float targetAlpha = hasWildIncident ? borderIncidentAlpha : borderAmbientAlpha;
        visual.BaseColor = new Color(tint.r, tint.g, tint.b, targetAlpha);

        if (pulseActivation)
        {
            if (visual.PulseCoroutine != null) StopCoroutine(visual.PulseCoroutine);
            visual.PulseCoroutine = StartCoroutine(AnimateBorderPulseCoroutine(visual));
        }
        else if (visual.PulseCoroutine == null)
        {
            visual.Material.color = visual.BaseColor;
        }
    }

    private ClusterBorderVisual CreateBorderVisual(string clusterKey)
    {
        GameObject go = new GameObject($"BuffBorder_{clusterKey}");
        go.transform.SetParent(_borderRoot, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh { name = $"BuffBorderMesh_{clusterKey}" };
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        Material mat = new Material(_sharedHoverBaseMaterial);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return new ClusterBorderVisual
        {
            Root = go,
            MeshFilter = mf,
            Renderer = mr,
            Mesh = mesh,
            Material = mat
        };
    }

    private void RebuildPerimeterRibbonMesh(Mesh mesh, List<HexCoordinates> hexes, HexWorldGenerator worldGen)
    {
        mesh.Clear();
        if (hexes == null || hexes.Count == 0) return;

        HashSet<HexCoordinates> clusterSet = new HashSet<HexCoordinates>(hexes);
        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> triangles = new List<int>();

        float rInner = HexMetrics.OuterRadius * borderInnerRadiusRatio;
        float rOuter = HexMetrics.OuterRadius * borderOuterRadiusRatio;
        Color innerColor = new Color(1f, 1f, 1f, 0.15f);
        Color outerColor = new Color(1f, 1f, 1f, 1f);
        Color topFadeColor = new Color(1f, 1f, 1f, 0f);

        for (int i = 0; i < hexes.Count; i++)
        {
            HexCoordinates hex = hexes[i];
            if (!worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null) continue;

            Vector3 center = tileObj.transform.position;
            var surface = tileObj.GetComponent<HexTileInfo>()?.biomeSurface;
            float baseY = GetTileSurfaceY(tileObj) + borderYOffset;

            for (int dir = 0; dir < 6; dir++)
            {
                if (clusterSet.Contains(hex.GetNeighbor(dir))) continue;

                // Pointed-Top hex: dir 0..5 ứng với góc pháp tuyến -60 * dir (độ)
                float normalDeg = -60f * dir;
                float a0 = Mathf.Deg2Rad * (normalDeg - 30f);
                float a1 = Mathf.Deg2Rad * (normalDeg + 30f);

                Vector3 dir0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 dir1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                // 1. Dải viền mặt đất (mờ dần vào trong) — bám sát bờ viền thực tế của Wet BiomeSurface nếu có
                int baseIdx = vertices.Count;
                Vector3 pInner0 = new Vector3(center.x + dir0.x * rInner, baseY, center.z + dir0.z * rInner);
                Vector3 pOuter0 = new Vector3(center.x + dir0.x * rOuter, baseY, center.z + dir0.z * rOuter);
                Vector3 pOuter1 = new Vector3(center.x + dir1.x * rOuter, baseY, center.z + dir1.z * rOuter);
                Vector3 pInner1 = new Vector3(center.x + dir1.x * rInner, baseY, center.z + dir1.z * rInner);

                if (surface != null)
                {
                    float y0 = baseY;
                    float y1 = baseY;
                    if (surface.TryGetHeight(pOuter0, out float hOut0)) y0 = Mathf.Max(y0, hOut0 + borderYOffset);
                    if (surface.TryGetHeight(pInner0, out float hIn0)) y0 = Mathf.Max(y0, hIn0 + borderYOffset);
                    if (surface.TryGetHeight(pOuter1, out float hOut1)) y1 = Mathf.Max(y1, hOut1 + borderYOffset);
                    if (surface.TryGetHeight(pInner1, out float hIn1)) y1 = Mathf.Max(y1, hIn1 + borderYOffset);
                    pInner0.y = pOuter0.y = y0;
                    pInner1.y = pOuter1.y = y1;
                }

                vertices.Add(pInner0); colors.Add(innerColor);
                vertices.Add(pOuter0); colors.Add(outerColor);
                vertices.Add(pOuter1); colors.Add(outerColor);
                vertices.Add(pInner1); colors.Add(innerColor);

                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 3);
                triangles.Add(baseIdx + 2);

                // 2. Dải hào quang đứng mềm ở mép ngoài (lướt qua tán cỏ thấp, fade 1 -> 0 lên trên)
                if (borderRimHeight > 0.001f)
                {
                    int rimIdx = vertices.Count;
                    Vector3 pTop0 = new Vector3(pOuter0.x, pOuter0.y + borderRimHeight, pOuter0.z);
                    Vector3 pTop1 = new Vector3(pOuter1.x, pOuter1.y + borderRimHeight, pOuter1.z);

                    vertices.Add(pOuter0); colors.Add(outerColor);
                    vertices.Add(pTop0);   colors.Add(topFadeColor);
                    vertices.Add(pTop1);   colors.Add(topFadeColor);
                    vertices.Add(pOuter1); colors.Add(outerColor);

                    // 2 mặt để nhìn rõ từ mọi hướng camera
                    triangles.Add(rimIdx);
                    triangles.Add(rimIdx + 1);
                    triangles.Add(rimIdx + 2);
                    triangles.Add(rimIdx);
                    triangles.Add(rimIdx + 2);
                    triangles.Add(rimIdx + 3);

                    triangles.Add(rimIdx);
                    triangles.Add(rimIdx + 2);
                    triangles.Add(rimIdx + 1);
                    triangles.Add(rimIdx);
                    triangles.Add(rimIdx + 3);
                    triangles.Add(rimIdx + 2);
                }
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private IEnumerator AnimateBorderPulseCoroutine(ClusterBorderVisual visual)
    {
        float elapsed = 0f;
        float targetAlpha = visual.BaseColor.a > 0.01f ? visual.BaseColor.a : borderAmbientAlpha;
        while (elapsed < borderPulseDuration && visual != null && visual.Material != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / borderPulseDuration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            float alpha = Mathf.Lerp(borderPulseAlpha, targetAlpha, ease);
            Color c = visual.BaseColor;
            visual.Material.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }

        if (visual != null && visual.Material != null)
        {
            visual.Material.color = visual.BaseColor;
            visual.PulseCoroutine = null;
        }
    }

    private Color ResolveBorderColor(BiomeBuffState state)
    {
        if (state.HasRiverBuff && state.HasBuildingBuff)
        {
            return combinedBuffColor;
        }
        return state.HasRiverBuff ? riverBuffColor : buildingBuffColor;
    }

    private void RemoveBorderForCluster(string clusterKey)
    {
        if (_borderVisuals.TryGetValue(clusterKey, out ClusterBorderVisual visual))
        {
            if (visual != null && visual.PulseCoroutine != null)
            {
                StopCoroutine(visual.PulseCoroutine);
            }
            DestroyBorderVisual(visual);
            _borderVisuals.Remove(clusterKey);
        }
    }

    private static void DestroyBorderVisual(ClusterBorderVisual visual)
    {
        if (visual == null) return;
        if (visual.Root != null) Destroy(visual.Root);
        if (visual.Mesh != null) Destroy(visual.Mesh);
        if (visual.Material != null) Destroy(visual.Material);
    }

    private GameObject GetOrCreateHoverOverlay(int index)
    {
        return GetOrCreateOverlayFromPool(_hoverOverlayPool, _hoverRoot, "BuffHoverHex", index);
    }

    private GameObject GetOrCreateOverlayFromPool(List<GameObject> pool, Transform root, string prefix, int index)
    {
        while (pool.Count <= index)
        {
            GameObject go = new GameObject($"{prefix}_{pool.Count}");
            go.transform.SetParent(root, false);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _hexOverlayMesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = new Material(_sharedHoverBaseMaterial);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            go.SetActive(false);
            pool.Add(go);
        }

        return pool[index];
    }

    private static float GetTileSurfaceY(GameObject tileObj)
    {
        if (tileObj == null) return 0f;
        var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
        return topY;
    }

    private void EnsureContainers()
    {
        if (_borderRoot == null) _borderRoot = CreateChildRoot("BiomeBuffBorders");
        if (_hoverRoot == null) _hoverRoot = CreateChildRoot("HoverAuraPreview");
        if (_selectionRoot == null) _selectionRoot = CreateChildRoot("SelectedBuffOverlays");
    }

    private Transform CreateChildRoot(string childName)
    {
        GameObject go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private void EnsureSharedAssets()
    {
        if (_sharedHoverBaseMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            _sharedHoverBaseMaterial = new Material(shader);
        }

        if (_hexOverlayMesh == null)
        {
            _hexOverlayMesh = CreateHexDiscMesh(HexMetrics.OuterRadius * 0.94f);
        }
    }

    private static Mesh CreateHexDiscMesh(float radius)
    {
        Mesh mesh = new Mesh { name = "BuffHexOverlayDisc" };
        Vector3[] vertices = new Vector3[7];
        vertices[0] = Vector3.zero;

        // Pointed-Top hex corners khớp với HexMetrics
        for (int i = 0; i < 6; i++)
        {
            float angleRad = Mathf.Deg2Rad * (60f * i + 30f);
            vertices[i + 1] = new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);
        }

        int[] triangles = new int[18];
        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6 + 1;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = next;
            triangles[i * 3 + 2] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }
}

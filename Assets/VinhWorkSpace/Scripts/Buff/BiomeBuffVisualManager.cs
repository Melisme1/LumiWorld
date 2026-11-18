using System.Collections;
using System.Collections.Generic;
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

    private readonly Dictionary<string, BiomeBuffState> _previousBuffStates = new Dictionary<string, BiomeBuffState>();
    private readonly HashSet<string> _previousIncidentClusters = new HashSet<string>();
    private readonly Dictionary<string, ClusterBorderVisual> _borderVisuals = new Dictionary<string, ClusterBorderVisual>();
    private readonly List<GameObject> _hoverOverlayPool = new List<GameObject>();

    private Transform _borderRoot;
    private Transform _hoverRoot;

    private Material _sharedHoverBaseMaterial;
    private Mesh _hexOverlayMesh;

    private HexCoordinates _lastPreviewHex;
    private CardData _lastPreviewCard;
    private bool _hasActivePreview;

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

    private void Start()
    {
        HexWorldGenerator worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen != null)
        {
            RefreshAllBuffs(worldGen);
        }
    }

    private void OnDestroy()
    {
        foreach (var kvp in _borderVisuals)
        {
            DestroyBorderVisual(kvp.Value);
        }
        _borderVisuals.Clear();

        if (_sharedHoverBaseMaterial != null) Destroy(_sharedHoverBaseMaterial);
        if (_hexOverlayMesh != null) Destroy(_hexOverlayMesh);
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

            if (!info.BuffState.HasAnyBuff && !hasWildIncident)
            {
                RemoveBorderForCluster(info.ClusterKey);
                continue;
            }

            bool newlyActivated =
                (!wasInIncident && hasWildIncident) ||
                (!prevState.HasRiverBuff && info.BuffState.HasRiverBuff) ||
                (!prevState.HasBuildingBuff && info.BuffState.HasBuildingBuff);

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

        _hasActivePreview = true;
        _lastPreviewCard = cardData;
        _lastPreviewHex = hoverHex;

        EnsureContainers();
        EnsureSharedAssets();

        HashSet<HexCoordinates> auraHexes;
        Color baseColor;

        if (isBuilding)
        {
            auraHexes = BiomeBuffCalculator.CollectBuildingAuraHexes(
                hoverHex,
                h => IsBuildingTileAt(h, worldGen),
                out int _,
                out int radius);
            baseColor = buildingBuffColor;

            // Khi cụm Building mới 1-2 ô (chưa đạt mốc 3 để tỏa buff), không vẽ aura rộng
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

    private List<BiomeClusterBuffInfo> EvaluateCurrentBiomeBuffs(HexWorldGenerator worldGen)
    {
        return BiomeBuffCalculator.EvaluateAllBiomeBuffs(
            worldGen.MapTiles.Keys,
            h => GetBiomeTerrainIdAt(h, worldGen),
            h => GetBiomeClusterIdAt(h, worldGen),
            h => IsBuildingTileAt(h, worldGen),
            IsRiverTileAt);
    }

    private static string GetBiomeTerrainIdAt(HexCoordinates hex, HexWorldGenerator worldGen)
    {
        if (!worldGen.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null) return null;
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

        Color tint = hasWildIncident ? wildIncidentColor : ResolveBorderColor(info.BuffState);
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
            float y = GetTileSurfaceY(tileObj) + borderYOffset;
            float yTop = y + borderRimHeight;

            for (int dir = 0; dir < 6; dir++)
            {
                if (clusterSet.Contains(hex.GetNeighbor(dir))) continue;

                // Pointed-Top hex: dir 0..5 ứng với góc pháp tuyến -60 * dir (độ)
                float normalDeg = -60f * dir;
                float a0 = Mathf.Deg2Rad * (normalDeg - 30f);
                float a1 = Mathf.Deg2Rad * (normalDeg + 30f);

                Vector3 dir0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 dir1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                // 1. Dải viền mặt đất (mờ dần vào trong)
                int baseIdx = vertices.Count;
                Vector3 pInner0 = new Vector3(center.x + dir0.x * rInner, y, center.z + dir0.z * rInner);
                Vector3 pOuter0 = new Vector3(center.x + dir0.x * rOuter, y, center.z + dir0.z * rOuter);
                Vector3 pOuter1 = new Vector3(center.x + dir1.x * rOuter, y, center.z + dir1.z * rOuter);
                Vector3 pInner1 = new Vector3(center.x + dir1.x * rInner, y, center.z + dir1.z * rInner);

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
                    Vector3 pTop0 = new Vector3(pOuter0.x, yTop, pOuter0.z);
                    Vector3 pTop1 = new Vector3(pOuter1.x, yTop, pOuter1.z);

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
        while (_hoverOverlayPool.Count <= index)
        {
            GameObject go = new GameObject($"BuffHoverHex_{_hoverOverlayPool.Count}");
            go.transform.SetParent(_hoverRoot, false);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _hexOverlayMesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = new Material(_sharedHoverBaseMaterial);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            go.SetActive(false);
            _hoverOverlayPool.Add(go);
        }

        return _hoverOverlayPool[index];
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

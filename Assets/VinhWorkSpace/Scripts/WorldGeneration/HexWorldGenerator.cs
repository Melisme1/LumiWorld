using System.Collections.Generic;
using UnityEngine;

public class HexWorldGenerator : MonoBehaviour
{
    [Header("Prefabs theo thứ tự từ Thấp -> Cao")]
    // [0] Thấp nhất: hex_water
    // [1] Ở giữa: hex_grass
    // [2] Trên cao: hex_road_A hoặc khối đá
    [SerializeField] private GameObject[] terrainPrefabs;

    [Header("Kích thước bản đồ ngẫu nhiên")]
    [SerializeField] private int minRadius = 2;
    [SerializeField] private int maxRadius = 2;

    [Header("Địa hình & Độ cao")]
    [Tooltip("Độ cao bậc giữa các tầng (0 = tất cả cùng nằm trên một mặt phẳng)")]
    [SerializeField] private float stepHeight = 0f;

    [Tooltip("Độ mượt của đồi núi")]
    [SerializeField] private float noiseScale = 0.15f;

    [Tooltip("Độ cao cơ sở khi spawn (để chân khối lục giác chìm xuống dưới mặt nước, tránh lơ lửng). Mặc định -0.5f")]
    [SerializeField] private float spawnOffsetY = -0.5f;

<<<<<<< HEAD
=======
    [Header("Tỷ lệ phân bố tầng địa hình (Noise Thresholds)")]
    [Tooltip("Ngưỡng phân cách Tầng thấp (Dưới ngưỡng này = Tầng thấp / Bloomfield). Giữ vừa phải (~25-28%), không quá nhiều. Mặc định 0.38")]
    [Range(0.2f, 0.6f)]
    [SerializeField] private float lowTierThreshold = 0.38f;

    [Tooltip("Ngưỡng phân cách Tầng trung (Từ lowTierThreshold đến ngưỡng này = Tầng trung / Leafwood ~52-55% cao nhất; trên ngưỡng này = Tầng cao / Windheath ~20-22%). Mặc định 0.63")]
    [Range(0.5f, 0.85f)]
    [SerializeField] private float midTierThreshold = 0.63f;

>>>>>>> origin/AnKhang_zoo_connection
    public float StepHeight => stepHeight;
    public GameObject[] TerrainPrefabs => terrainPrefabs;
    public float NoiseScale => noiseScale;
    public float SpawnOffsetY => spawnOffsetY;
<<<<<<< HEAD
=======
    public float LowTierThreshold => lowTierThreshold;
    public float MidTierThreshold => midTierThreshold;
>>>>>>> origin/AnKhang_zoo_connection

    private float seedX;
    private float seedZ;

    // Lưu danh sách các ô đã sinh ra để quản lý logic đặt bài sau này
    public Dictionary<HexCoordinates, GameObject> MapTiles { get; private set; } = new Dictionary<HexCoordinates, GameObject>();

    private void Start()
    {
        GenerateIsland();
    }

    [ContextMenu("Generate New Island")]
    public void GenerateIsland()
    {
        ClearCurrentMap();

        if (terrainPrefabs == null || terrainPrefabs.Length == 0)
        {
            Debug.LogError("Chưa gán Prefabs vào Terrain Prefabs!");
            return;
        }

        int islandRadius = Random.Range(minRadius, maxRadius + 1);
        seedX = Random.Range(0f, 1000f);
        seedZ = Random.Range(0f, 1000f);

        List<(HexCoordinates coords, float noise)> tileNoiseList = new List<(HexCoordinates, float)>();

        for (int q = -islandRadius; q <= islandRadius; q++)
        {
            int rMin = Mathf.Max(-islandRadius, -q - islandRadius);
            int rMax = Mathf.Min(islandRadius, -q + islandRadius);

            for (int r = rMin; r <= rMax; r++)
            {
                HexCoordinates coords = new HexCoordinates(q, r);
                float sampleX = (q + seedX) * noiseScale;
                float sampleZ = (r + seedZ) * noiseScale;
                float noise = Mathf.PerlinNoise(sampleX, sampleZ);
                tileNoiseList.Add((coords, noise));
            }
        }

        // 1. Phân loại theo tỷ lệ Perlin Noise: Tầng trung cao nhất (~52-55%), Tầng thấp vừa phải (~25-28%), Tầng cao tăng nhẹ (~20-22%)
        int[] tileLevels = new int[tileNoiseList.Count];
        int mountainCount = 0;
        int lowTierCount = 0;

        for (int i = 0; i < tileNoiseList.Count; i++)
        {
            float noise = tileNoiseList[i].noise;
            if (noise < lowTierThreshold)
            {
                tileLevels[i] = 0; // Tầng thấp (Bloomfield - thung lũng hoa)
                lowTierCount++;
            }
            else if (noise < midTierThreshold)
            {
                tileLevels[i] = 1; // Tầng trung (Leafwood - rừng xanh, chiếm cao nhất)
            }
            else
            {
                tileLevels[i] = 2; // Tầng cao (Windheath - cao nguyên gió, tăng nhẹ)
                mountainCount++;
            }
        }

        // 2. Bảo hiểm: Bắt buộc phải có tầng cao (tối thiểu ~18-20% diện tích, ví dụ đảo 19 ô có ít nhất 3-4 ô cao)
        int minMountains = Mathf.Max(3, Mathf.RoundToInt(tileNoiseList.Count * 0.18f));
        if (mountainCount < minMountains)
        {
            List<int> sortedIndices = new List<int>();
            for (int i = 0; i < tileNoiseList.Count; i++) sortedIndices.Add(i);
            sortedIndices.Sort((a, b) => tileNoiseList[b].noise.CompareTo(tileNoiseList[a].noise)); // Từ cao xuống thấp

            for (int k = 0; k < minMountains; k++)
            {
                tileLevels[sortedIndices[k]] = 2; // Đảm bảo các ô đỉnh cao nhất luôn là Tầng cao
            }
        }

        // 3. Khởi tạo vật thể trên cùng mặt phẳng độ cao (Y = 0)
        for (int i = 0; i < tileNoiseList.Count; i++)
        {
            HexCoordinates coords = tileNoiseList[i].coords;
            int levelIndex = tileLevels[i];

            levelIndex = Mathf.Clamp(levelIndex, 0, terrainPrefabs.Length - 1);
            GameObject prefabToSpawn = terrainPrefabs[levelIndex];

            Vector3 worldPosition = HexMetrics.HexToWorldPosition(coords, spawnOffsetY);

                        GameObject tileInstance = Instantiate(prefabToSpawn, worldPosition, Quaternion.identity, transform);
            tileInstance.name = $"Hex_{coords.Q}_{coords.R}_[{prefabToSpawn.name}]_Type{levelIndex}";

            // Tự động gắn MeshCollider nếu prefab chưa có để chuột Raycast chính xác bề mặt
            if (tileInstance.GetComponent<Collider>() == null)
            {
                MeshFilter mf = tileInstance.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = tileInstance.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }

            HexTileInfo tileInfo = tileInstance.AddComponent<HexTileInfo>();
            tileInfo.sourcePrefab = prefabToSpawn;
            tileInfo.terrainTypeIndex = levelIndex;
            tileInfo.coordinates = coords;

            MapTiles.Add(coords, tileInstance);
        }
    }

    private void ClearCurrentMap()
    {
        if (HexBiomeClusterConnector.Instance != null)
        {
            HexBiomeClusterConnector.Instance.ClearAllConnections();
        }

        foreach (var pair in MapTiles)
        {
            if (pair.Value != null)
            {
                if (Application.isPlaying)
                    Destroy(pair.Value);
                else
                    DestroyImmediate(pair.Value);
            }
        }
        MapTiles.Clear();
    }
}
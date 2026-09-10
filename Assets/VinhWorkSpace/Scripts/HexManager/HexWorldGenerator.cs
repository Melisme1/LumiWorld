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

    public float StepHeight => stepHeight;
    public GameObject[] TerrainPrefabs => terrainPrefabs;
    public float NoiseScale => noiseScale;

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

        // 1. Phân loại theo tỷ lệ Perlin Noise tự nhiên: Cỏ chiếm đa số (~60%), Nước (~25%), Núi là điểm nhấn (~15%)
        int[] tileLevels = new int[tileNoiseList.Count];
        int mountainCount = 0;
        int waterCount = 0;

        for (int i = 0; i < tileNoiseList.Count; i++)
        {
            float noise = tileNoiseList[i].noise;
            if (noise < 0.33f)
            {
                tileLevels[i] = 0; // Nước
                waterCount++;
            }
            else if (noise < 0.68f)
            {
                tileLevels[i] = 1; // Cỏ (chiếm đa số diện tích)
            }
            else
            {
                tileLevels[i] = 2; // Đồi / Núi (điểm nhấn cao tầng)
                mountainCount++;
            }
        }

        // 2. Bảo hiểm: Bắt buộc phải có núi (tối thiểu 2 ô tại các đỉnh noise cao nhất)
        int minMountains = Mathf.Max(2, tileNoiseList.Count / 10);
        if (mountainCount < minMountains)
        {
            List<int> sortedIndices = new List<int>();
            for (int i = 0; i < tileNoiseList.Count; i++) sortedIndices.Add(i);
            sortedIndices.Sort((a, b) => tileNoiseList[b].noise.CompareTo(tileNoiseList[a].noise)); // Từ cao xuống thấp

            for (int k = 0; k < minMountains; k++)
            {
                tileLevels[sortedIndices[k]] = 2; // Đảm bảo các ô đỉnh cao nhất luôn là Núi
            }
        }

        // 3. Khởi tạo vật thể trên cùng mặt phẳng độ cao (Y = 0)
        for (int i = 0; i < tileNoiseList.Count; i++)
        {
            HexCoordinates coords = tileNoiseList[i].coords;
            int levelIndex = tileLevels[i];

            levelIndex = Mathf.Clamp(levelIndex, 0, terrainPrefabs.Length - 1);
            GameObject prefabToSpawn = terrainPrefabs[levelIndex];

            Vector3 worldPosition = HexMetrics.HexToWorldPosition(coords, 0f);

            GameObject tileInstance = Instantiate(prefabToSpawn, worldPosition, Quaternion.identity, transform);
            tileInstance.name = $"Hex_{coords.Q}_{coords.R}_[{prefabToSpawn.name}]_Type{levelIndex}";

            HexTileInfo tileInfo = tileInstance.AddComponent<HexTileInfo>();
            tileInfo.sourcePrefab = prefabToSpawn;
            tileInfo.terrainTypeIndex = levelIndex;
            tileInfo.coordinates = coords;

            MapTiles.Add(coords, tileInstance);
        }
    }

    private void ClearCurrentMap()
    {
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
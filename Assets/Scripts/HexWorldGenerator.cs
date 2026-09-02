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
    [SerializeField] private int minRadius = 4;
    [SerializeField] private int maxRadius = 6;

    [Header("Địa hình & Độ cao")]
    [Tooltip("Khoảng cách thực tế giữa mặt phẳng trên cùng của các tầng")]
    [SerializeField] private float stepHeight = 0.5f;

    [Tooltip("Độ mượt của đồi núi: Càng nhỏ thì các mảng tụ lại càng rộng")]
    [SerializeField] private float noiseScale = 0.2f;

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
        float seedX = Random.Range(0f, 1000f);
        float seedZ = Random.Range(0f, 1000f);

        for (int q = -islandRadius; q <= islandRadius; q++)
        {
            int rMin = Mathf.Max(-islandRadius, -q - islandRadius);
            int rMax = Mathf.Min(islandRadius, -q + islandRadius);

            for (int r = rMin; r <= rMax; r++)
            {
                HexCoordinates coords = new HexCoordinates(q, r);

                // 1. Tính toán giá trị Perlin Noise (0.0 -> 1.0)
                float sampleX = (q + seedX) * noiseScale;
                float sampleZ = (r + seedZ) * noiseScale;
                float noise = Mathf.PerlinNoise(sampleX, sampleZ);

                // 2. Phân chia ngưỡng rõ ràng cho 3 loại địa hình
                int levelIndex = 0;
                if (noise < 0.35f)
                {
                    levelIndex = 0; // Nước
                }
                else if (noise < 0.70f)
                {
                    levelIndex = 1; // Cỏ
                }
                else
                {
                    levelIndex = 2; // Đồi / Đường
                }

                levelIndex = Mathf.Clamp(levelIndex, 0, terrainPrefabs.Length - 1);
                GameObject prefabToSpawn = terrainPrefabs[levelIndex];

                // 3. Tự động triệt tiêu sai lệch Pivot của Mesh (Căn chỉnh theo mặt phẳng trên cùng)
                float topSurfaceOffset = 0f;
                MeshFilter mf = prefabToSpawn.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    // Mặt trên cùng của Mesh so với gốc Pivot
                    topSurfaceOffset = mf.sharedMesh.bounds.center.y + (mf.sharedMesh.bounds.size.y / 2f);
                }

                // 4. Tính toán tọa độ Spawn: Mặt trên của mỗi tầng luôn cách nhau đúng stepHeight
                float targetTopY = levelIndex * stepHeight;
                float finalSpawnY = targetTopY - topSurfaceOffset;

                Vector3 worldPosition = HexMetrics.HexToWorldPosition(coords, finalSpawnY);

                // 5. Khởi tạo Prefab và lưu vào Map
                GameObject tileInstance = Instantiate(prefabToSpawn, worldPosition, Quaternion.identity, transform);
                tileInstance.name = $"Hex_{coords.Q}_{coords.R}_Lvl{levelIndex}";

                MapTiles.Add(coords, tileInstance);
            }
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
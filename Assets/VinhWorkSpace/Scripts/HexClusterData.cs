using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct HexTileData
{
    public HexCoordinates relativeCoord; // Tọa độ tương đối so với tâm cụm (0,0)
    public int prefabIndex;              // Loại địa hình (0: Nước, 1: Cỏ, 2: Đồi/Đá)
    public int heightLevel;              // Tầng độ cao (Đồng bộ tuyệt đối với prefabIndex)
}

[CreateAssetMenu(fileName = "NewHexCluster", menuName = "HexGrid/Cluster Data")]
public class HexClusterData : ScriptableObject
{
    public string clusterName = "Hex Region";
    public List<HexTileData> tiles = new List<HexTileData>();

    /// <summary>
    /// Sinh trọn vẹn một vùng lục giác bán kính radius = 3 (37 khối)
    /// Sử dụng phân phối Perlin tự nhiên (Cỏ chiếm đa số ~60%, Nước ~25%, Núi ~15%)
    /// Đồng thời đảm bảo LUÔN CÓ NÚI (ít nhất 2 - 4 ô tại đỉnh cao nhất)
    /// </summary>
    public static HexClusterData CreateSampleCluster(int radius = 3)
    {
        var cluster = ScriptableObject.CreateInstance<HexClusterData>();
        cluster.clusterName = $"Hex Region R{radius}";

        float seedX = Random.Range(0f, 1000f);
        float seedZ = Random.Range(0f, 1000f);
        float noiseScale = 0.15f;

        List<(HexCoordinates coords, float noise)> tileNoiseList = new List<(HexCoordinates, float)>();

        // 1. Quét toàn bộ tọa độ trong phạm vi bán kính radius
        for (int q = -radius; q <= radius; q++)
        {
            int rMin = Mathf.Max(-radius, -q - radius);
            int rMax = Mathf.Min(radius, -q + radius);

            for (int r = rMin; r <= rMax; r++)
            {
                HexCoordinates coords = new HexCoordinates(q, r);
                float sampleX = (q + seedX) * noiseScale;
                float sampleZ = (r + seedZ) * noiseScale;
                float noise = Mathf.PerlinNoise(sampleX, sampleZ);
                tileNoiseList.Add((coords, noise));
            }
        }

        // 2. Phân bố theo tỷ lệ tự nhiên: Cỏ chiếm đa số, Núi là điểm nhấn đỉnh cao
        int[] tileLevels = new int[tileNoiseList.Count];
        int mountainCount = 0;

        for (int i = 0; i < tileNoiseList.Count; i++)
        {
            float noise = tileNoiseList[i].noise;
            if (noise < 0.33f)
            {
                tileLevels[i] = 0; // Nước (~25%)
            }
            else if (noise < 0.68f)
            {
                tileLevels[i] = 1; // Cỏ (chiếm đa số ~60%)
            }
            else
            {
                tileLevels[i] = 2; // Đồi / Núi (điểm nhấn ~15%)
                mountainCount++;
            }
        }

        // 3. Đảm bảo BẮT BUỘC PHẢI CÓ NÚI: Nếu ngẫu nhiên ít hơn 3 ô núi, lấy 3 ô có đỉnh noise cao nhất làm núi
        const int minMountains = 3;
        if (mountainCount < minMountains)
        {
            List<int> sortedIndices = new List<int>();
            for (int i = 0; i < tileNoiseList.Count; i++) sortedIndices.Add(i);
            sortedIndices.Sort((a, b) => tileNoiseList[b].noise.CompareTo(tileNoiseList[a].noise)); // Sắp xếp từ đỉnh cao xuống

            for (int k = 0; k < minMountains; k++)
            {
                tileLevels[sortedIndices[k]] = 2; // Gán đỉnh cao nhất làm Núi
            }
        }

        // 4. Lưu danh sách dữ liệu (Độ cao phẳng Y = 0)
        for (int i = 0; i < tileNoiseList.Count; i++)
        {
            cluster.tiles.Add(new HexTileData
            {
                relativeCoord = tileNoiseList[i].coords,
                prefabIndex = tileLevels[i],
                heightLevel = 0
            });
        }

        return cluster;
    }
}

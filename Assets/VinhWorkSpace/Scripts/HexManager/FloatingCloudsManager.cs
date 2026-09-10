using System.Collections.Generic;
using UnityEngine;

public class FloatingCloudsManager : MonoBehaviour
{
    [Header("Cloud Prefabs")]
    [Tooltip("Danh sách các Prefab mây low-poly / stylized")]
    [SerializeField] private GameObject[] cloudPrefabs;

    [Header("Spawn Settings")]
    [Tooltip("Số lượng mây trôi cùng lúc")]
    [SerializeField] private int cloudCount = 10;

    [Tooltip("Khu vực mây xuất hiện (Rộng x Dài trên mặt phẳng XZ) - Tăng số này để mây tản ra xa")]
    [SerializeField] private Vector2 areaSize = new Vector2(80f, 80f);

    [Tooltip("Độ cao tối thiểu và tối đa của tầng mây")]
    [SerializeField] private float minHeight = 8f;
    [SerializeField] private float maxHeight = 16f;

    [Tooltip("Tỉ lệ phóng to/thu nhỏ ngẫu nhiên của mây")]
    [SerializeField] private Vector2 scaleRange = new Vector2(1.0f, 2.5f);

    [Header("Wind & Movement")]
    [Tooltip("Hướng gió thổi mây (trên mặt phẳng XZ)")]
    [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.2f);

    [Tooltip("Tốc độ trôi của mây")]
    [SerializeField] private float minSpeed = 0.5f;
    [SerializeField] private float maxSpeed = 1.2f;

    private readonly List<CloudInstance> activeClouds = new List<CloudInstance>();

    private class CloudInstance
    {
        public GameObject GameObject;
        public Transform Transform;
        public float Speed;
    }

    private void Start()
    {
        if (cloudPrefabs == null || cloudPrefabs.Length == 0)
        {
            // Tự tạo mây hình khối cơ bản nếu chưa có prefab
            CreateDefaultClouds();
        }
        else
        {
            SpawnInitialClouds();
        }
    }

    private void Update()
    {
        Vector3 moveStep = new Vector3(windDirection.x, 0, windDirection.y).normalized;
        float halfWidth = areaSize.x * 0.5f;
        float halfLength = areaSize.y * 0.5f;
        Vector3 center = transform.position;

        for (int i = 0; i < activeClouds.Count; i++)
        {
            CloudInstance cloud = activeClouds[i];
            if (cloud.GameObject == null) continue;

            // Di chuyển mây theo hướng gió
            cloud.Transform.position += moveStep * (cloud.Speed * Time.deltaTime);

            Vector3 pos = cloud.Transform.position;

            // Kiểm tra nếu mây bay vượt quá vùng giới hạn XZ
            if (pos.x > center.x + halfWidth || pos.x < center.x - halfWidth ||
                pos.z > center.z + halfLength || pos.z < center.z - halfLength)
            {
                // Đặt lại mây về phía đầu ngọn gió
                ResetCloudPosition(cloud, fromWindSource: true);
            }
        }
    }

    private void SpawnInitialClouds()
    {
        for (int i = 0; i < cloudCount; i++)
        {
            GameObject prefab = cloudPrefabs[Random.Range(0, cloudPrefabs.Length)];
            GameObject obj = Instantiate(prefab, transform);
            obj.name = $"Cloud_{i}";

            CloudInstance cloud = new CloudInstance
            {
                GameObject = obj,
                Transform = obj.transform,
                Speed = Random.Range(minSpeed, maxSpeed)
            };

            ResetCloudPosition(cloud, fromWindSource: false);
            activeClouds.Add(cloud);
        }
    }

    private void CreateDefaultClouds()
    {
        // Sử dụng StylizedCloud Shader mềm mại
        Shader cloudShader = Shader.Find("Custom/StylizedCloud");
        Material cloudMat = cloudShader != null 
            ? new Material(cloudShader) 
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        for (int i = 0; i < cloudCount; i++)
        {
            GameObject cloudRoot = new GameObject($"StylizedCloud_{i}");
            cloudRoot.transform.SetParent(transform);

            // Tạo dáng mây dẹt phẳng đáy tự nhiên (Puffy flat-bottom cloud)
            int puffCount = Random.Range(4, 7);
            for (int p = 0; p < puffCount; p++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.transform.SetParent(cloudRoot.transform);

                // Dáng mây bè ngang, dẹp trục Y và phẳng đáy
                float x = (p - puffCount * 0.5f) * Random.Range(0.8f, 1.3f);
                float y = Random.Range(0.0f, 0.4f);
                float z = Random.Range(-0.5f, 0.5f);
                puff.transform.localPosition = new Vector3(x, y, z);

                // Scale dẹp trục Y để tạo cảm giác mây trôi tự nhiên
                float baseSize = Random.Range(1.2f, 2.0f);
                puff.transform.localScale = new Vector3(baseSize * 1.2f, baseSize * 0.55f, baseSize * 1.0f);

                puff.GetComponent<Renderer>().sharedMaterial = cloudMat;
                
                // Tắt collider
                Destroy(puff.GetComponent<Collider>());
            }

            CloudInstance cloud = new CloudInstance
            {
                GameObject = cloudRoot,
                Transform = cloudRoot.transform,
                Speed = Random.Range(minSpeed, maxSpeed)
            };

            ResetCloudPosition(cloud, fromWindSource: false);
            activeClouds.Add(cloud);
        }
    }

    private void ResetCloudPosition(CloudInstance cloud, bool fromWindSource)
    {
        Vector3 center = transform.position;
        float halfWidth = areaSize.x * 0.5f;
        float halfLength = areaSize.y * 0.5f;

        float randomHeight = Random.Range(minHeight, maxHeight);
        float randomScale = Random.Range(scaleRange.x, scaleRange.y);
        cloud.Transform.localScale = Vector3.one * randomScale;
        cloud.Speed = Random.Range(minSpeed, maxSpeed);

        if (!fromWindSource)
        {
            // Phân bố đều khắp bầu trời
            float posX = Random.Range(center.x - halfWidth, center.x + halfWidth);
            float posZ = Random.Range(center.z - halfLength, center.z + halfLength);
            cloud.Transform.position = new Vector3(posX, center.y + randomHeight, posZ);
        }
        else
        {
            // Xuất hiện lại ở phía đầu hướng gió với độ lệch tản rộng
            Vector2 windNorm = windDirection.normalized;
            Vector2 perp = new Vector2(-windNorm.y, windNorm.x); // Vector vuông góc với hướng gió

            float spawnDistance = halfWidth;
            float spread = Random.Range(-halfLength, halfLength);

            Vector2 spawn2D = -windNorm * spawnDistance + perp * spread;

            cloud.Transform.position = new Vector3(center.x + spawn2D.x, center.y + randomHeight, center.z + spawn2D.y);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
        Vector3 center = transform.position + Vector3.up * ((minHeight + maxHeight) * 0.5f);
        Vector3 size = new Vector3(areaSize.x, maxHeight - minHeight, areaSize.y);
        Gizmos.DrawWireCube(center, size);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class HexPlacementController : MonoBehaviour
{
    [Header("Tham chiếu các thành phần")]
    [SerializeField] private HexWorldGenerator worldGenerator;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject[] terrainPrefabs;
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private LayerMask mapLayerMask = ~0; // Layer dùng cho Raycast

    [Header("Cài đặt")]
    [SerializeField] private float stepHeight = 0.6f;
    [SerializeField] private float smoothSpeed = 25f; // Tốc độ trượt bám theo chuột của Ghost

    // 6 Vector ghép cạnh hoàn hảo cho khối Hexagon bán kính R = 3 (Edge-to-Edge Flush Offset)
    private static readonly HexCoordinates[] SuperHexOffsetsR3 = new HexCoordinates[]
    {
        new HexCoordinates(4, 3),   // Hướng 0: Phải - Trên
        new HexCoordinates(7, -3),  // Hướng 1: Phải - Dưới
        new HexCoordinates(3, -7),  // Hướng 2: Dưới
        new HexCoordinates(-4, -3), // Hướng 3: Trái - Dưới
        new HexCoordinates(-7, 3),  // Hướng 4: Trái - Trên
        new HexCoordinates(-3, 7)   // Hướng 5: Trên
    };

    // Quản lý các tâm vùng lục giác đã đặt trên bản đồ
    private HashSet<HexCoordinates> placedClusterCenters = new HashSet<HexCoordinates>();

    // Trạng thái vận hành
    private HexClusterData currentCluster;
    private int currentRotationStep = 0; // Bước xoay: 0, 1, 2, 3, 4, 5 (tương ứng 0°, 60°, 120°, 180°, 240°, 300°)
    private HexPreviewGhost previewGhostInstance;
    private HexCoordinates currentHoverHex;
    private bool isHoveringValidPosition = false;
    private bool isInPlacementMode = false;

    public bool IsInPlacementMode => isInPlacementMode;
    private float lastScrollRotateTime = 0f;
    private const float ScrollCooldown = 0.10f;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    private void Start()
    {
        // Tâm của hòn đảo khởi đầu mặc định là (0, 0)
        placedClusterCenters.Add(new HexCoordinates(0, 0));
    }

    private void Update()
    {
        if (!isInPlacementMode || currentCluster == null) return;

        HandleInput();
        UpdateGhostPositionAndValidity();
    }

    /// <summary>
    /// Bắt đầu chế độ mô phỏng ngẫu nhiên (dành cho nút UI OnClick gọi trực tiếp)
    /// </summary>
    public void StartPlacement()
    {
        StartPlacement(null);
    }

    /// <summary>
    /// Bắt đầu chế độ mô phỏng với dữ liệu cụm truyền vào
    /// </summary>
    public void StartPlacement(HexClusterData clusterData)
    {
        // Tự động lấy prefabs và stepHeight từ WorldGenerator nếu chưa gán
        if ((terrainPrefabs == null || terrainPrefabs.Length == 0) && worldGenerator != null)
        {
            terrainPrefabs = worldGenerator.TerrainPrefabs;
            stepHeight = worldGenerator.StepHeight;
        }

        // Đảm bảo đảo khởi đầu đã có tâm (0, 0)
        if (placedClusterCenters.Count == 0)
        {
            placedClusterCenters.Add(new HexCoordinates(0, 0));
        }

        if (clusterData == null)
        {
            // Tạo trọn vẹn một vùng hexagon có bán kính Radius = 3 (37 khối lục giác)
            currentCluster = HexClusterData.CreateSampleCluster(3);
        }
        else
        {
            currentCluster = clusterData;
        }

        currentRotationStep = 0;
        isInPlacementMode = true;

        CreateOrRebuildGhost();
    }

    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        // 1. Xoay cụm 60 độ (Phím R, Q, E hoặc Lăn con lăn chuột)
        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
            {
                RotateCluster(1);
            }
            else if (keyboard.qKey.wasPressedThisFrame)
            {
                RotateCluster(-1);
            }
        }

        if (mouse != null && Time.time > lastScrollRotateTime + ScrollCooldown)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.001f)
            {
                int dir = scrollY > 0f ? 1 : -1;
                RotateCluster(dir);
                lastScrollRotateTime = Time.time;
            }
        }

        // 2. Hủy bỏ (Phím ESC hoặc Click chuột phải)
        if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
            (mouse != null && mouse.rightButton.wasPressedThisFrame))
        {
            CancelPlacement();
            return;
        }

        // 3. Đặt vào bản đồ (Click chuột trái)
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            // Tránh click xuyên qua UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (isHoveringValidPosition)
            {
                ConfirmPlacement();
            }
        }
    }

    /// <summary>
    /// Xoay hướng cụm theo chiều kim đồng hồ (+1) hoặc ngược lại (-1)
    /// </summary>
    public void RotateCluster(int direction)
    {
        currentRotationStep = ((currentRotationStep + direction) % 6 + 6) % 6;
        CreateOrRebuildGhost();
    }

    private void CreateOrRebuildGhost()
    {
        if (previewGhostInstance == null)
        {
            GameObject ghostObj = new GameObject("PlacementPreviewGhost");
            previewGhostInstance = ghostObj.AddComponent<HexPreviewGhost>();
        }

        previewGhostInstance.Initialize(terrainPrefabs, currentCluster, currentRotationStep, ghostMaterial, stepHeight);
    }

    private void UpdateGhostPositionAndValidity()
    {
        if (previewGhostInstance == null || Mouse.current == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        Vector3 hitPoint = Vector3.zero;
        bool hasHit = false;

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, mapLayerMask))
        {
            hitPoint = hit.point;
            hasHit = true;
        }
        else if (groundPlane.Raycast(ray, out float enter))
        {
            hitPoint = ray.GetPoint(enter);
            hasHit = true;
        }

        if (hasHit)
        {
            HexCoordinates rawMouseHex = HexMetrics.WorldToHex(hitPoint);

            // TÌM VỊ TRÍ SNAP NAM CHÂM GHÉP CẠNH HOÀN HẢO (SUPER-HEX MAGNET SNAP)
            if (TryFindBestSuperHexSnap(rawMouseHex, out HexCoordinates bestSnapCenter))
            {
                currentHoverHex = bestSnapCenter;
                isHoveringValidPosition = true;
            }
            else
            {
                currentHoverHex = rawMouseHex;
                isHoveringValidPosition = false;
            }

            Vector3 targetSnapPos = HexMetrics.HexToWorldPosition(currentHoverHex, 0f);

            // Cho Ghost lướt mượt mà và hút dính vào vị trí ghép cạnh
            previewGhostInstance.transform.position = Vector3.Lerp(
                previewGhostInstance.transform.position,
                targetSnapPos,
                Time.deltaTime * smoothSpeed
            );

            previewGhostInstance.SetPlacementValidity(isHoveringValidPosition);
        }
    }

    /// <summary>
    /// Tìm vị trí ghép cạnh khít 100% gần nhất với con trỏ chuột
    /// </summary>
    private bool TryFindBestSuperHexSnap(HexCoordinates mouseHex, out HexCoordinates bestSnapCenter)
    {
        bestSnapCenter = mouseHex;
        if (worldGenerator == null || worldGenerator.MapTiles == null) return false;

        var existingTiles = worldGenerator.MapTiles;
        HashSet<HexCoordinates> candidateCenters = new HashSet<HexCoordinates>();

        // 1. Quét tất cả các vị trí ghép cạnh quanh các tâm vùng đã có
        foreach (var center in placedClusterCenters)
        {
            for (int i = 0; i < 6; i++)
            {
                HexCoordinates candidate = center + SuperHexOffsetsR3[i];

                // Nếu tâm này chưa từng được đặt
                if (!placedClusterCenters.Contains(candidate))
                {
                    // Kiểm tra xem vị trí này có bị đè lên bất kỳ ô nào đã có trên Map không
                    if (IsRegionPlacementValid(candidate))
                    {
                        candidateCenters.Add(candidate);
                    }
                }
            }
        }

        if (candidateCenters.Count == 0) return false;

        // 2. Tìm điểm Snap có khoảng cách gần nhất với vị trí chuột
        int minDistance = int.MaxValue;
        bool found = false;

        foreach (var candidate in candidateCenters)
        {
            int dist = HexCoordinates.Distance(mouseHex, candidate);
            if (dist < minDistance)
            {
                minDistance = dist;
                bestSnapCenter = candidate;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Kiểm tra 37 ô của vùng R=3 tại vị trí candidateCenter có hoàn toàn trống không
    /// </summary>
    private bool IsRegionPlacementValid(HexCoordinates candidateCenter)
    {
        if (worldGenerator == null || worldGenerator.MapTiles == null) return false;
        var existingTiles = worldGenerator.MapTiles;

        foreach (var tile in currentCluster.tiles)
        {
            HexCoordinates worldHex = candidateCenter + tile.relativeCoord;
            if (existingTiles.ContainsKey(worldHex))
            {
                return false; // Bị trùng đè lên ô có sẵn
            }
        }

        return true;
    }

    /// <summary>
    /// Đặt thực tế 37 khối của vùng vào bản đồ
    /// </summary>
    private void ConfirmPlacement()
    {
        if (worldGenerator == null) return;

        foreach (var tile in currentCluster.tiles)
        {
            // Tọa độ đã qua phép xoay
            HexCoordinates rotatedRel = tile.relativeCoord.Rotate60Clockwise(currentRotationStep);
            HexCoordinates targetCoord = currentHoverHex + rotatedRel;

            int levelIndex = Mathf.Clamp(tile.prefabIndex, 0, terrainPrefabs.Length - 1);
            GameObject prefabToSpawn = terrainPrefabs[levelIndex];

            float topSurfaceOffset = 0f;
            MeshFilter mf = prefabToSpawn.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                topSurfaceOffset = mf.sharedMesh.bounds.center.y + (mf.sharedMesh.bounds.size.y / 2f);
            }

            float targetTopY = tile.heightLevel * stepHeight;
            float finalSpawnY = targetTopY - topSurfaceOffset;

            Vector3 worldPos = HexMetrics.HexToWorldPosition(targetCoord, finalSpawnY);
            Quaternion spawnRot = Quaternion.Euler(0f, currentRotationStep * 60f, 0f);

            GameObject tileInstance = Instantiate(prefabToSpawn, worldPos, spawnRot, worldGenerator.transform);
            tileInstance.name = $"Hex_{targetCoord.Q}_{targetCoord.R}_Lvl{levelIndex}";

            // Lưu vào dữ liệu Map
            worldGenerator.MapTiles.Add(targetCoord, tileInstance);
        }

        // Ghi nhận tâm vùng mới vào danh sách Super-Hex
        placedClusterCenters.Add(currentHoverHex);

        // Kết thúc lượt đặt hiện tại
        CancelPlacement();
    }

    /// <summary>
    /// Hủy bỏ mô phỏng
    /// </summary>
    public void CancelPlacement()
    {
        isInPlacementMode = false;
        currentCluster = null;

        if (previewGhostInstance != null)
        {
            Destroy(previewGhostInstance.gameObject);
            previewGhostInstance = null;
        }
    }
}

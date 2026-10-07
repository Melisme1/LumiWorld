using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Quản lý trung tâm toàn bộ hệ thống Thu Hoạch Tài Nguyên theo Cụm Biome (Biome Harvest System).
/// - Tự động phát hiện và gộp các ô đất liền kề (Leafwood, Bloomfield, Windheath) thành từng Cụm Biome.
/// - Mỗi cụm chỉ hiển thị DUY NHẤT 1 Vòng tròn tiến độ 360° / Bong bóng thu hoạch tại tâm cụm.
/// - Nhận diện tất cả các con thú (AnimalIndividual) đang sinh sống trong cụm để cộng dồn năng suất.
/// - Cho phép người chơi click vào Bong bóng hoặc click vào bất kỳ ô nào trong cụm để thu hoạch.
/// </summary>
public class BiomeHarvestManager : MonoBehaviour
{
    private static BiomeHarvestManager _instance;
    public static BiomeHarvestManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<BiomeHarvestManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("BiomeHarvestManager");
                    _instance = go.AddComponent<BiomeHarvestManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Cluster Settings")]
    [Tooltip("Độ trễ sau khi đặt bài trước khi quét lại cụm (tránh giật lag)")]
    [SerializeField] private float refreshDebounceTime = 0.15f;

    private readonly List<BiomeHarvestCluster> clusters = new List<BiomeHarvestCluster>();
    public IReadOnlyList<BiomeHarvestCluster> Clusters => clusters;

    public event Action<ResourceData, int> OnResourceHarvested;

    private Camera mainCamera;
    private float refreshTimer = -1f;

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

        mainCamera = Camera.main;
    }

    private void Start()
    {
        mainCamera = Camera.main;
        ScheduleRefresh();
    }

    /// <summary>
    /// Báo cho hệ thống biết thế giới vừa có thay đổi (vừa đặt thêm đất hoặc thêm thú)
    /// </summary>
    public void OnWorldChanged()
    {
        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        refreshTimer = refreshDebounceTime;
    }

    private void Update()
    {
        // 1. Quét lại cụm khi có yêu cầu (Debounced)
        if (refreshTimer >= 0f)
        {
            refreshTimer -= Time.deltaTime;
            if (refreshTimer < 0f)
            {
                RebuildClusters();
            }
        }

        // 2. Chạy vòng lặp sản xuất cho các cụm đang hoạt động
        float dt = Time.deltaTime;
        for (int i = 0; i < clusters.Count; i++)
        {
            clusters[i].Update(dt);
        }

        // 3. Xử lý click chuột gặt tài nguyên nhanh trên toàn cụm
        HandleInputHarvest();
    }

    private void HandleInputHarvest()
    {
        // Kiểm tra click hoàn toàn bằng New Input System (Mouse / Touchscreen / Pointer)
        bool clickDown = false;
        Vector2 screenPos = Vector2.zero;

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                clickDown = true;
                screenPos = Mouse.current.position.ReadValue();
            }
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            clickDown = true;
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            clickDown = true;
            screenPos = Pointer.current.position.ReadValue();
        }

        if (!clickDown) return;

        // Bấm trúng UI (nút, Bảng Đơn Hàng, bong bóng thu hoạch...) thì không thu hoạch ô đất nằm phía sau
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Tránh bấm trúng khu vực khay bài (Hand) ở dưới màn hình
        if (screenPos.y < Screen.height * 0.18f) return;

        // Nếu đang trong chế độ kéo bài preview thì không xử lý click thu hoạch
        if (HexSinglePlacementController.Instance != null && HexSinglePlacementController.Instance.IsPreviewing)
        {
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            // Kiểm tra xem ô lục giác chạm trúng có thuộc cụm nào đang sẵn sàng thu hoạch không
            HexCoordinates hitHex = HexMetrics.WorldToHex(hit.point);

            foreach (var cluster in clusters)
            {
                if (cluster.IsReadyToHarvest && cluster.HexCoords.Contains(hitHex))
                {
                    cluster.CollectHarvest();
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Tự động quét toàn bộ bản đồ và gom nhóm các ô đất liền kề thành từng Cụm Biome
    /// </summary>
    public void RebuildClusters()
    {
        // 1. Thu thập tất cả các PlacedCard loại Terrain có tài nguyên sản xuất
        PlacedCard[] allCards = FindObjectsByType<PlacedCard>(FindObjectsInactive.Exclude);
        List<PlacedCard> resourceTerrains = new List<PlacedCard>();
        Dictionary<HexCoordinates, PlacedCard> terrainByHex = new Dictionary<HexCoordinates, PlacedCard>();

        foreach (var c in allCards)
        {
            if (c != null && c.cardData != null && c.CardType == CardType.Terrain && c.cardData.producedResource != null)
            {
                resourceTerrains.Add(c);
                terrainByHex[c.placedHex] = c;
            }
        }

        // Thu thập danh sách thú đang có trong Scene
        AnimalIndividual[] allAnimalsArray = FindObjectsByType<AnimalIndividual>(FindObjectsInactive.Exclude);
        List<AnimalIndividual> allAnimals = new List<AnimalIndividual>(allAnimalsArray);

        // 2. Thuật toán Flood Fill (BFS) gom nhóm các ô cùng loại liền kề
        HashSet<PlacedCard> visited = new HashSet<PlacedCard>();
        List<List<PlacedCard>> groupedLists = new List<List<PlacedCard>>();

        foreach (var startCard in resourceTerrains)
        {
            if (visited.Contains(startCard)) continue;

            List<PlacedCard> group = new List<PlacedCard>();
            Queue<PlacedCard> queue = new Queue<PlacedCard>();

            queue.Enqueue(startCard);
            visited.Add(startCard);

            while (queue.Count > 0)
            {
                PlacedCard curr = queue.Dequeue();
                group.Add(curr);

                // Quét 6 hướng láng giềng lục giác
                for (int dir = 0; dir < 6; dir++)
                {
                    HexCoordinates neighborHex = curr.placedHex.GetNeighbor(dir);
                    if (terrainByHex.TryGetValue(neighborHex, out PlacedCard neighborCard))
                    {
                        // Cùng CardID hoặc cùng producedResource
                        if (neighborCard.CardID == startCard.CardID && !visited.Contains(neighborCard))
                        {
                            visited.Add(neighborCard);
                            queue.Enqueue(neighborCard);
                        }
                    }
                }
            }

            groupedLists.Add(group);
        }

        // 3. Đồng bộ danh sách Cụm hiện tại với các cụm vừa quét được
        // Giữ lại tiến trình timer của các cụm đang chạy nếu cụm không bị thay đổi
        List<BiomeHarvestCluster> newClusters = new List<BiomeHarvestCluster>();

        for (int i = 0; i < groupedLists.Count; i++)
        {
            var tileGroup = groupedLists[i];
            if (tileGroup.Count == 0) continue;

            CardData terrainCard = tileGroup[0].cardData;
            ResourceData res = terrainCard.producedResource;
            float cycleTime = terrainCard.biomeCycleDuration > 0 ? terrainCard.biomeCycleDuration : 30f;

            // Tìm cụm cũ tương ứng để bảo lưu tiến trình đếm giờ
            BiomeHarvestCluster existing = FindMatchingCluster(tileGroup);

            if (existing != null)
            {
                existing.SetTiles(tileGroup);
                existing.RefreshAnimals(allAnimals);
                newClusters.Add(existing);
                clusters.Remove(existing);
            }
            else
            {
                string newId = $"{terrainCard.cardName}_Cluster_{i}";
                BiomeHarvestCluster newCluster = new BiomeHarvestCluster(newId, terrainCard, res, cycleTime);
                newCluster.SetTiles(tileGroup);

                // Tạo UI Indicator cho cụm mới
                GameObject indObj = new GameObject($"Indicator_{newId}");
                indObj.transform.SetParent(transform);
                BiomeHarvestIndicator ind = indObj.AddComponent<BiomeHarvestIndicator>();
                newCluster.AttachIndicator(ind);

                newCluster.RefreshAnimals(allAnimals);
                newCluster.OnHarvestCollected += HandleClusterHarvest;

                newClusters.Add(newCluster);
            }
        }

        // Dọn dẹp các cụm cũ không còn tồn tại
        foreach (var oldCluster in clusters)
        {
            oldCluster.Destroy();
        }

        clusters.Clear();
        clusters.AddRange(newClusters);
    }

    private BiomeHarvestCluster FindMatchingCluster(List<PlacedCard> tiles)
    {
        if (tiles.Count == 0) return null;
        HexCoordinates firstHex = tiles[0].placedHex;

        foreach (var c in clusters)
        {
            if (c.HexCoords.Contains(firstHex))
            {
                return c;
            }
        }
        return null;
    }

    private void HandleClusterHarvest(BiomeHarvestCluster cluster, ResourceData resource, int amount)
    {
        OnResourceHarvested?.Invoke(resource, amount);
    }

    private void OnDestroy()
    {
        foreach (var c in clusters)
        {
            c.Destroy();
        }
        clusters.Clear();
    }
}

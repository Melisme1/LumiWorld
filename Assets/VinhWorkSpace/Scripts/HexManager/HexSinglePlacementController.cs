using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexSinglePlacementController : MonoBehaviour
{
    private static HexSinglePlacementController _instance;
    public static HexSinglePlacementController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<HexSinglePlacementController>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("HexSinglePlacementController");
                    _instance = go.AddComponent<HexSinglePlacementController>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("References")]
    [SerializeField] private HexWorldGenerator worldGenerator;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private LayerMask mapLayerMask = ~0;

    [Header("Preview Adjustment")]
    [Tooltip("Độ dịch lên theo trục Y để vỏ bọc trùm hẳn lên trên bề mặt đỉnh")]
    [SerializeField] private float previewHeightOffset = 0.04f;
    [Tooltip("Tỉ lệ phóng to của vỏ bọc Hologram")]
    [SerializeField] private Vector3 previewScale = new Vector3(1.03f, 1.05f, 1.03f);

    [Header("Indicator Colors")]
    [Tooltip("Màu xanh lá dạ quang bao trọn ô")]
    [SerializeField] private Color validColor = new Color(0.20f, 1.0f, 0.45f, 0.85f);
    [Tooltip("Màu đỏ cảnh báo khi trỏ ra ngoài ô")]
    [SerializeField] private Color invalidColor = new Color(0.95f, 0.20f, 0.20f, 0.70f);

    private HexCoordinates currentHoverHex;
    private HexCoordinates previousHoverHex;
    private bool hasValidPreviousHex = false;

    private bool isHoveringValidTile = false;
    private bool isPreviewing = false;

    // Quản lý Ghost và Encasement Shell
    private GameObject ghostRoot;
    private GameObject hologramShellObj;
    private GameObject itemGhostInstance;

    private CardData currentCardData;
    private MaterialPropertyBlock propBlock;
    private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");

    private GameObject currentActiveTileObj;
    private readonly List<MeshRenderer> cachedGhostRenderers = new List<MeshRenderer>();
    private Coroutine snapPunchCoroutine;

    public bool IsPreviewing => isPreviewing;
    public bool IsHoveringValidTile => isHoveringValidTile;
    public HexCoordinates CurrentHoverHex => currentHoverHex;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (worldGenerator == null)
        {
            worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
        }

        if (ghostMaterial == null)
        {
            var multiPlacement = FindAnyObjectByType<HexPlacementController>();
            if (multiPlacement != null)
            {
                var field = typeof(HexPlacementController).GetField("ghostMaterial",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    ghostMaterial = field.GetValue(multiPlacement) as Material;
                }
            }
        }

        propBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (isPreviewing && ghostRoot != null && ghostRoot.activeSelf)
        {
            AnimatePulseVisuals();
        }
    }

    /// <summary>
    /// Hiệu ứng thở nhẹ (Breathing Pulse) tăng độ sống động và phát sáng xanh lá cho toàn bộ ô
    /// </summary>
    private void AnimatePulseVisuals()
    {
        if (isHoveringValidTile)
        {
            float pulse = 0.78f + 0.12f * Mathf.Sin(Time.time * 4.0f);
            Color targetColor = new Color(validColor.r, validColor.g, validColor.b, pulse);
            ApplyColorToAllRenderers(targetColor);
        }
        else
        {
            ApplyColorToAllRenderers(invalidColor);
        }
    }

    /// <summary>
    /// Bắt đầu hiển thị Ghost Preview cho 1 lá bài khi người chơi bắt đầu kéo
    /// </summary>
    public void StartPreview(CardData cardData)
    {
        CancelPreview();

        if (worldGenerator == null) worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
        if (mainCamera == null) mainCamera = Camera.main;

        currentCardData = cardData;
        isPreviewing = true;
        isHoveringValidTile = false;
        hasValidPreviousHex = false;

        BuildGhostAndIndicator();
    }

    /// <summary>
    /// Xây dựng đối tượng cha quản lý Preview
    /// </summary>
    private void BuildGhostAndIndicator()
    {
        ghostRoot = new GameObject("SingleHexPlacementRoot");

        // Tạo mô hình vật phẩm xem trước (Cây / Đá /...) nếu có
        GameObject previewPrefab = null;
        if (currentCardData != null)
        {
            if (currentCardData.prefabToPlace != null)
            {
                previewPrefab = currentCardData.prefabToPlace;
            }
            else if (currentCardData.HasHabitatProps())
            {
                // Lấy 1 mẫu prop tiêu biểu làm preview
                foreach (var rule in currentCardData.habitatProps)
                {
                    if (rule != null && rule.prefabs != null && rule.prefabs.Count > 0 && rule.prefabs[0] != null)
                    {
                        previewPrefab = rule.prefabs[0];
                        break;
                    }
                }
            }
        }

        if (previewPrefab != null)
        {
            itemGhostInstance = Instantiate(previewPrefab, ghostRoot.transform);
            itemGhostInstance.name = "GhostItemPreview";
            itemGhostInstance.transform.localPosition = Vector3.zero;

            DisableColliders(itemGhostInstance);
            ApplyGhostMaterial(itemGhostInstance);
        }

        ghostRoot.SetActive(false);
        RefreshCachedRenderers();
        ApplyColorToAllRenderers(invalidColor);
    }

    /// <summary>
    /// Đồng bộ vỏ bọc 3D khớp 100% với Mesh của khối địa hình cụ thể đang được trỏ tới (Bất kể là Cỏ, Nước hay Núi cao)
    /// </summary>
    private void SyncHologramShellToTile(GameObject targetTileObj)
    {
        if (ghostRoot == null || targetTileObj == null) return;

        if (hologramShellObj != null)
        {
            Destroy(hologramShellObj);
            hologramShellObj = null;
        }

        hologramShellObj = new GameObject("HologramEncasementShell");
        hologramShellObj.transform.SetParent(ghostRoot.transform, false);
        hologramShellObj.transform.localPosition = new Vector3(0f, previewHeightOffset, 0f);
        hologramShellObj.transform.localRotation = targetTileObj.transform.localRotation;
        hologramShellObj.transform.localScale = previewScale;

        // Sao chép toàn bộ các MeshFilter và MeshRenderer từ khối đang trỏ tới sang vỏ bọc (bỏ qua các prop/vật phẩm đã đặt trên ô)
        MeshFilter[] sourceFilters = targetTileObj.GetComponentsInChildren<MeshFilter>();
        foreach (var srcMf in sourceFilters)
        {
            if (srcMf == null || srcMf.sharedMesh == null) continue;
            if (IsPartOfPlacedProp(srcMf.transform, targetTileObj.transform)) continue;

            GameObject part = new GameObject(srcMf.name);
            part.transform.SetParent(hologramShellObj.transform, false);
            part.transform.localPosition = targetTileObj.transform.InverseTransformPoint(srcMf.transform.position);
            part.transform.localRotation = Quaternion.Inverse(targetTileObj.transform.rotation) * srcMf.transform.rotation;
            part.transform.localScale = (srcMf.gameObject == targetTileObj) ? Vector3.one : srcMf.transform.localScale;

            MeshFilter mf = part.AddComponent<MeshFilter>();
            mf.sharedMesh = srcMf.sharedMesh;

            MeshRenderer mr = part.AddComponent<MeshRenderer>();
            if (ghostMaterial != null)
            {
                mr.sharedMaterial = ghostMaterial;
            }
        }

        RefreshCachedRenderers();
    }

    /// <summary>
    /// Cập nhật vị trí và tính hợp lệ của Ghost theo vị trí con trỏ chuột trên màn hình
    /// </summary>
    public void UpdatePreview(Vector2 screenPosition)
    {
        if (!isPreviewing || ghostRoot == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Nếu chuột còn ở sát cạnh dưới màn hình (khu vực Hand/Deck)
        if (screenPosition.y < Screen.height * 0.18f)
        {
            if (ghostRoot.activeSelf) ghostRoot.SetActive(false);
            ResetActiveTile();
            isHoveringValidTile = false;
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
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
            HexCoordinates targetHex = HexMetrics.WorldToHex(hitPoint);

            bool isTileInMap = (worldGenerator != null &&
                                worldGenerator.MapTiles != null &&
                                worldGenerator.MapTiles.ContainsKey(targetHex));

            GameObject tileObj = null;
            float surfaceY = HexMetrics.TileHeight;
            Vector3 baseTilePos = HexMetrics.HexToWorldPosition(targetHex, 0f);

            bool isAllowedByCard = true;
            bool isOccupied = false;
            if (isTileInMap)
            {
                tileObj = worldGenerator.MapTiles[targetHex];
                surfaceY = GetTileSurfaceY(tileObj);
                baseTilePos = tileObj.transform.position;

                isOccupied = IsTileOccupied(tileObj);

                if (currentCardData != null)
                {
                    isAllowedByCard = currentCardData.IsTileAllowed(tileObj, worldGenerator);
                }
            }

            // Card Special dạng biến đổi khối (Rain): được phép đặt lên ô đã có prop,
            // miễn là card có cấu hình khối đích (Prefab To Place).
            bool isTransformCard = currentCardData != null && currentCardData.replacesTile;

            bool isValid = isTileInMap && isAllowedByCard &&
                           (isTransformCard ? currentCardData.IsTileTransformCard() : !isOccupied);
            bool hexChanged = (!hasValidPreviousHex || targetHex != currentHoverHex);

            currentHoverHex = targetHex;
            isHoveringValidTile = isValid;
            hasValidPreviousHex = true;

            if (!ghostRoot.activeSelf)
            {
                ghostRoot.SetActive(true);
            }

            if (itemGhostInstance != null)
            {
                float localY = surfaceY - baseTilePos.y + previewHeightOffset;
                Vector3 offset = currentCardData != null ? currentCardData.placementOffset : Vector3.zero;
                itemGhostInstance.transform.localPosition = new Vector3(0f, localY, 0f) + offset;
            }

            if (hexChanged)
            {
                OnHexSelectionChanged(targetHex, isValid, tileObj);
            }
            else
            {
                AnimatePulseVisuals();
            }

            ghostRoot.transform.position = baseTilePos;
        }
        else
        {
            isHoveringValidTile = false;
            ResetActiveTile();
        }
    }

    /// <summary>
    /// Xử lý cảm giác lực và cập nhật vỏ bọc khi chuyển đổi ô
    /// </summary>
    private void OnHexSelectionChanged(HexCoordinates newHex, bool isValid, GameObject newTileObj)
    {
        ResetActiveTile();

        if (newTileObj != null)
        {
            currentActiveTileObj = newTileObj;

            // Đồng bộ vỏ bọc theo đúng hình dáng và chiều cao của chính ô này
            SyncHologramShellToTile(newTileObj);

            AnimatePulseVisuals();

            // Hiệu ứng nảy nhẹ khi snap trúng ô
            if (snapPunchCoroutine != null)
            {
                StopCoroutine(snapPunchCoroutine);
            }
            snapPunchCoroutine = StartCoroutine(AnimateSnapPunch());
        }
        else
        {
            if (hologramShellObj != null)
            {
                Destroy(hologramShellObj);
                hologramShellObj = null;
            }
        }
    }

    private void ResetActiveTile()
    {
        currentActiveTileObj = null;
    }

    /// <summary>
    /// Xác nhận đặt vật thể của lá bài lên ô lục giác đang trỏ tới
    /// </summary>
    public bool TryConfirmPlacement(CardData cardData, out HexCoordinates placedHex)
    {
        placedHex = currentHoverHex;

        if (!isPreviewing || !isHoveringValidTile || worldGenerator == null)
        {
            CancelPreview();
            return false;
        }

        if (!worldGenerator.MapTiles.TryGetValue(currentHoverHex, out GameObject targetTileObj))
        {
            CancelPreview();
            return false;
        }

        // Card Special dạng biến đổi khối (Rain): thay thế khối gốc tại chỗ thay vì đặt lên trên.
        if (cardData != null && cardData.replacesTile)
        {
            bool transformed = TryTransformTile(cardData, targetTileObj);

            currentActiveTileObj = null;

            if (transformed)
            {
                // Hiện hiệu ứng "+1" tại ô vừa biến đổi (Rain luôn cộng baseScore)
                Vector3 popupPos = HexMetrics.HexToWorldPosition(currentHoverHex, HexMetrics.TileHeight);
                ScorePopupManager.ShowScore(cardData.baseScore, popupPos);

                if (ScoreCalculator.Instance != null)
                {
                    ScoreCalculator.Instance.RecalculateScore();
                }
            }

            CancelPreview();
            return transformed;
        }

        if (IsTileOccupied(targetTileObj))
        {
            CancelPreview();
            return false;
        }

        if (cardData != null && !cardData.IsTileAllowed(targetTileObj, worldGenerator))
        {
            CancelPreview();
            return false;
        }

        currentActiveTileObj = null;

        float surfaceY = GetTileSurfaceY(targetTileObj);
        Vector3 spawnWorldPos = HexMetrics.HexToWorldPosition(currentHoverHex, surfaceY);

        if (cardData != null)
        {
            spawnWorldPos += cardData.placementOffset;
        }

        // Sinh vật phẩm / hệ sinh thái Props lên đỉnh ô
        if (cardData != null)
        {
            if (cardData.HasHabitatProps())
            {
                // Sinh tổ hợp props ngẫu nhiên phong cách Preserve
                HexHabitatSpawner.Instance.SpawnHabitat(cardData, targetTileObj.transform, spawnWorldPos);

                // Lưu thông tin card vào tile đã đặt cho hệ thống tính điểm
                PlacedCard placedCard = targetTileObj.GetComponent<PlacedCard>();
                if (placedCard == null)
                {
                    placedCard = targetTileObj.AddComponent<PlacedCard>();
                }
                placedCard.cardData = cardData;
                placedCard.placedHex = currentHoverHex;
            }
            else if (cardData.prefabToPlace != null)
            {
                // Sinh vật phẩm đơn lẻ thông thường
                GameObject placedObject = Instantiate(cardData.prefabToPlace, spawnWorldPos, Quaternion.identity, targetTileObj.transform);
                placedObject.name = $"{cardData.cardName}_{currentHoverHex.Q}_{currentHoverHex.R}";

                // LƯU THÔNG TIN CARD VÀO OBJECT ĐÃ ĐẶT
                PlacedCard placedCard = placedObject.GetComponent<PlacedCard>();
                if (placedCard == null)
                {
                    placedCard = placedObject.AddComponent<PlacedCard>();
                }
                placedCard.cardData = cardData;
                placedCard.placedHex = currentHoverHex;

                StartCoroutine(AnimatePopIn(placedObject.transform));
            }
            else
            {
                // Trường hợp lá bài không có prefab lẫn habitat props nhưng vẫn tính là đã đặt
                PlacedCard placedCard = targetTileObj.GetComponent<PlacedCard>();
                if (placedCard == null)
                {
                    placedCard = targetTileObj.AddComponent<PlacedCard>();
                }
                placedCard.cardData = cardData;
                placedCard.placedHex = currentHoverHex;
            }

            // Đánh dấu ô đã bị chiếm dụng trong HexTileInfo
            HexTileInfo tileInfo = targetTileObj.GetComponent<HexTileInfo>();
            if (tileInfo != null)
            {
                tileInfo.isOccupied = true;
            }

            // Tính toán lại điểm số
            if (ScoreCalculator.Instance != null)
            {
                ScoreCalculator.Instance.RecalculateScore();
            }

            // Hiệu ứng cộng điểm "+X" (Preserve style)
            // Hiển thị phần điểm tăng thêm của từng khối trong nhóm:
            //  - Nhóm < 3 khối  -> +1
            //  - Nhóm >= 3 khối -> khối mới +2, các khối cũ hiện thêm +1 (nâng cấp)
            ShowPlacementScorePopup();
        }

        CancelPreview();
        return true;
    }

    /// <summary>
    /// Biến đổi 1 ô lục giác tại chỗ: thay khối gốc bằng Prefab To Place của card,
    /// đồng bộ lại MapTiles và HexTileInfo để hệ thống đặt bài / tính điểm không bị lệch.
    /// Trả về true nếu biến đổi thành công.
    /// </summary>
    private bool TryTransformTile(CardData cardData, GameObject targetTileObj)
    {
        if (cardData == null || targetTileObj == null || worldGenerator == null) return false;

        GameObject newPrefab = cardData.prefabToPlace;
        if (newPrefab == null) return false;

        int newTypeIndex = cardData.ResolveTerrainTypeIndex(worldGenerator);
        HexCoordinates hex = currentHoverHex;

        // 1. Thu thập các object con đang đứng trên khối gốc (prop / card đã đặt)
        List<Transform> oldChildren = new List<Transform>();
        for (int i = 0; i < targetTileObj.transform.childCount; i++)
        {
            oldChildren.Add(targetTileObj.transform.GetChild(i));
        }

        // 2. Sinh khối mới tại đúng vị trí và hướng xoay của khối gốc
        Vector3 spawnPos = targetTileObj.transform.position;
        Quaternion spawnRot = targetTileObj.transform.rotation;

        GameObject newTile = Instantiate(newPrefab, spawnPos, spawnRot, worldGenerator.transform);
        newTile.name = $"Hex_{hex.Q}_{hex.R}_[{newPrefab.name}]_Type{Mathf.Max(0, newTypeIndex)}";

        HexTileInfo newTileInfo = newTile.GetComponent<HexTileInfo>();
        if (newTileInfo == null) newTileInfo = newTile.AddComponent<HexTileInfo>();
        newTileInfo.sourcePrefab = newPrefab;
        newTileInfo.terrainTypeIndex = newTypeIndex;
        newTileInfo.coordinates = hex;

        // 3. Ghi lại vào bản đồ để mọi hệ thống (đặt bài, tính điểm) trỏ đúng khối mới
        worldGenerator.MapTiles[hex] = newTile;

        // 3b. Ghi nhận card Rain để được tính điểm (luôn +1 qua alwaysBaseScore).
        //     Rain chỉ BIẾN ĐỔI ĐỊA HÌNH nên dùng một marker con riêng và KHÔNG chiếm ô.
        if (cardData.occupiesTile)
        {
            PlacedCard placedCard = newTile.GetComponent<PlacedCard>();
            if (placedCard == null) placedCard = newTile.AddComponent<PlacedCard>();
            placedCard.cardData = cardData;
            placedCard.placedHex = hex;
        }
        else
        {
            GameObject scoreMarker = new GameObject("RainScoreMarker");
            scoreMarker.transform.SetParent(newTile.transform, false);
            PlacedCard placedCard = scoreMarker.AddComponent<PlacedCard>();
            placedCard.cardData = cardData;
            placedCard.placedHex = hex;
        }

        // 4. Xử lý các object con cũ
        if (cardData.clearPropsOnTransform)
        {
            // Rain thường "gột rửa" ô: xóa prop cũ
            for (int i = 0; i < oldChildren.Count; i++)
            {
                if (oldChildren[i] != null) Destroy(oldChildren[i].gameObject);
            }
        }
        else
        {
            // Giữ lại prop cũ bằng cách chuyển sang khối mới
            for (int i = 0; i < oldChildren.Count; i++)
            {
                if (oldChildren[i] != null)
                {
                    oldChildren[i].SetParent(newTile.transform, true);
                }
            }
        }

        // Ô mới chỉ bị coi là "chiếm" nếu card này thực sự chiếm ô (Rain thì không)
        newTileInfo.isOccupied = false;

        // 5. Xóa khối gốc
        Destroy(targetTileObj);

        return true;
    }

    /// <summary>
    /// Hiển thị hiệu ứng "+X" cho phần điểm TĂNG THÊM của từng khối trong nhóm.
    ///
    /// Quy tắc (Preserve style):
    ///  - Nhóm < 3 khối  -> mỗi khối = baseScore           (ví dụ +1)
    ///  - Nhóm >= 3 khối -> mỗi khối = baseScore x hệ số   (ví dụ +2)
    ///
    /// Mỗi khối chỉ hiển thị phần chênh lệch so với giá trị đã hiện trước đó:
    ///  - Khối 1, 2 đã hiện +1 -> khi nhóm đủ 3, hiện thêm +1 (nâng cấp lên +2)
    ///  - Khối 3 vừa đặt -> hiện +2
    /// </summary>
    private void ShowPlacementScorePopup()
    {
        if (HexGroupDetector.Instance == null) return;

        // Tìm chip PlacedCard của khối vừa đặt
        GameObject tileObj = null;
        if (worldGenerator != null)
        {
            worldGenerator.MapTiles.TryGetValue(currentHoverHex, out tileObj);
        }

        // PlacedCard có thể nằm ngay trên tile (habitat props) hoặc trên object con (prefabToPlace)
        PlacedCard placedCard = tileObj != null
            ? tileObj.GetComponentInChildren<PlacedCard>()
            : null;

        if (placedCard == null || placedCard.cardData == null) return;

        // Tìm toàn bộ nhóm cùng loại chứa khối vừa đặt
        System.Collections.Generic.List<PlacedCard> group =
            HexGroupDetector.Instance.FindGroup(placedCard);

        if (group == null || group.Count == 0)
        {
            group = new System.Collections.Generic.List<PlacedCard> { placedCard };
        }

        // Hệ số nhân theo quy tắc của ScoreCalculator
        int groupRequired = 3;
        int groupMultiplier = 2;

        if (ScoreCalculator.Instance != null)
        {
            groupRequired = ScoreCalculator.Instance.GroupRequired;
            groupMultiplier = ScoreCalculator.Instance.GroupMultiplier;
        }

        // Nhóm đủ lớn -> nhân hệ số, ngược lại dùng điểm gốc
        bool isCompleteGroup = group.Count >= groupRequired;

        foreach (PlacedCard card in group)
        {
            if (card == null || card.cardData == null) continue;

            // Giá trị điểm đúng của khối theo quy tắc nhóm hiện tại
            int targetScore = isCompleteGroup
                ? card.cardData.baseScore * groupMultiplier
                : card.cardData.baseScore;

            // Phần tăng thêm so với lần hiển thị trước (đã lưu trên PlacedCard)
            int gained = targetScore - card.displayedScore;

            if (gained <= 0) continue;

            Vector3 cardWorldPos = HexMetrics.HexToWorldPosition(card.placedHex, HexMetrics.TileHeight);
            ScorePopupManager.ShowScore(gained, cardWorldPos);

            // Ghi nhớ mức điểm đã hiển thị để lần sau chỉ hiện phần chênh lệch
            card.displayedScore = targetScore;
        }
    }

    /// <summary>
    /// Hủy bỏ preview và xóa Ghost
    /// </summary>
    public void CancelPreview()
    {
        isPreviewing = false;
        isHoveringValidTile = false;
        hasValidPreviousHex = false;

        ResetActiveTile();
        cachedGhostRenderers.Clear();

        if (snapPunchCoroutine != null)
        {
            StopCoroutine(snapPunchCoroutine);
            snapPunchCoroutine = null;
        }

        if (ghostRoot != null)
        {
            Destroy(ghostRoot);
            ghostRoot = null;
            hologramShellObj = null;
            itemGhostInstance = null;
        }
    }

    /// <summary>
    /// Kiểm tra xem ô lục giác đã được đặt bài / vật phẩm từ trước hay chưa
    /// </summary>
    public bool IsTileOccupied(HexCoordinates coords)
    {
        if (worldGenerator == null || worldGenerator.MapTiles == null) return false;
        if (worldGenerator.MapTiles.TryGetValue(coords, out GameObject tileObj))
        {
            return IsTileOccupied(tileObj);
        }
        return false;
    }

    /// <summary>
    /// Kiểm tra xem Game Object của ô lục giác đã được đặt bài / vật phẩm từ trước hay chưa
    /// </summary>
    public bool IsTileOccupied(GameObject tileObj)
    {
        if (tileObj == null) return false;

        // 1. Kiểm tra cờ isOccupied trong HexTileInfo nếu có
        HexTileInfo tileInfo = tileObj.GetComponent<HexTileInfo>();
        if (tileInfo != null && tileInfo.isOccupied)
        {
            return true;
        }

        // 2. Kiểm tra xem tile hoặc các object con của tile đã gắn PlacedCard "chiếm ô" chưa.
        //    Card đánh dấu occupiesTile = false (Rain biến đổi địa hình) KHÔNG được coi là chiếm ô.
        PlacedCard[] placedCards = tileObj.GetComponentsInChildren<PlacedCard>(true);
        foreach (PlacedCard pc in placedCards)
        {
            if (pc == null || pc.cardData == null) continue;
            if (pc.cardData.occupiesTile)
            {
                return true;
            }
        }

        // 3. Kiểm tra xem tile có chứa container Habitat_ không
        for (int i = 0; i < tileObj.transform.childCount; i++)
        {
            Transform child = tileObj.transform.GetChild(i);
            if (child != null && child.name.StartsWith("Habitat_"))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPartOfPlacedProp(Transform t, Transform tileTransform)
    {
        Transform curr = t;
        while (curr != null && curr != tileTransform)
        {
            if (curr.GetComponent<PlacedCard>() != null || curr.name.StartsWith("Habitat_"))
            {
                return true;
            }
            curr = curr.parent;
        }
        return false;
    }

    private float GetTileSurfaceY(GameObject tileObj)
    {
        if (tileObj != null)
        {
            Renderer[] renderers = tileObj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r != null && !IsPartOfPlacedProp(r.transform, tileObj.transform))
                {
                    return r.bounds.max.y;
                }
            }
        }
        return HexMetrics.TileHeight;
    }

    private void RefreshCachedRenderers()
    {
        cachedGhostRenderers.Clear();
        if (ghostRoot != null)
        {
            ghostRoot.GetComponentsInChildren(true, cachedGhostRenderers);
        }
    }

    private void ApplyColorToAllRenderers(Color targetColor)
    {
        if (ghostRoot == null) return;

        for (int i = 0; i < cachedGhostRenderers.Count; i++)
        {
            var mr = cachedGhostRenderers[i];
            if (mr == null) continue;
            mr.GetPropertyBlock(propBlock);
            propBlock.SetColor(ColorProperty, targetColor);
            mr.SetPropertyBlock(propBlock);
        }
    }

    private void DisableColliders(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
    }

    private void ApplyGhostMaterial(GameObject obj)
    {
        if (ghostMaterial == null) return;

        MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>();
        foreach (var mr in renderers)
        {
            mr.sharedMaterial = ghostMaterial;
        }
    }

    private IEnumerator AnimateSnapPunch()
    {
        if (ghostRoot == null) yield break;

        Vector3 originalScale = Vector3.one;
        Vector3 punchScale = Vector3.one * 1.15f;

        float duration = 0.10f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (ghostRoot == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            ghostRoot.transform.localScale = Vector3.Lerp(punchScale, originalScale, t);
            yield return null;
        }

        if (ghostRoot != null)
        {
            ghostRoot.transform.localScale = originalScale;
        }
    }

    private IEnumerator AnimatePopIn(Transform target)
    {
        if (target == null) yield break;

        Vector3 finalScale = target.localScale;
        target.localScale = Vector3.zero;

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float scaleT = Mathf.Sin(t * Mathf.PI * 0.5f);
            target.localScale = Vector3.LerpUnclamped(Vector3.zero, finalScale, scaleT);
            yield return null;
        }

        if (target != null)
        {
            target.localScale = finalScale;
        }
    }
}

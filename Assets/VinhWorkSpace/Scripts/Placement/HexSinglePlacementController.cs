using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tổng tài điều phối quá trình đặt một ô lục giác (Single Placement Coordinator).
/// Kết nối Raycaster, Validator và GhostVisual; xử lý sinh vật thể và tính điểm khi đặt thành công.
/// </summary>
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

    [Header("Preview Settings")]
    [Tooltip("Độ dịch lên theo trục Y để vỏ bọc trùm hẳn lên trên bề mặt đỉnh")]
    [SerializeField] private float previewHeightOffset = 0.04f;
    [Tooltip("Tỉ lệ phóng to của vỏ bọc Hologram")]
    [SerializeField] private Vector3 previewScale = new Vector3(1.03f, 1.05f, 1.03f);

    [Header("Indicator Colors")]
    [Tooltip("Màu xanh lá dạ quang bao trọn ô")]
    [SerializeField] private Color validColor = new Color(0.20f, 1.0f, 0.45f, 0.85f);
    [Tooltip("Màu đỏ cảnh báo khi trỏ ra ngoài ô")]
    [SerializeField] private Color invalidColor = new Color(0.95f, 0.20f, 0.20f, 0.70f);

    [Header("Modular Components")]
    [SerializeField] private HexPointerRaycaster raycaster;
    [SerializeField] private HexPlacementValidator validator;
    [SerializeField] private HexGhostVisual ghostVisual;

    private HexCoordinates currentHoverHex;
    private HexCoordinates previousHoverHex;
    private bool hasValidPreviousHex = false;
    private bool isHoveringValidTile = false;
    private bool isPreviewing = false;
    private CardData currentCardData;
    private GameObject currentActiveTileObj;

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

        // Tự động tìm hoặc gắn các linh kiện thành phần nếu chưa có
        SetupModularComponents();

        // Cố gắng lấy GhostMaterial từ HexPlacementController cũ nếu chưa gán
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

        SyncSettingsToSubComponents();
    }

    private void SetupModularComponents()
    {
        if (raycaster == null)
        {
            raycaster = GetComponent<HexPointerRaycaster>();
            if (raycaster == null) raycaster = gameObject.AddComponent<HexPointerRaycaster>();
        }

        if (validator == null)
        {
            validator = GetComponent<HexPlacementValidator>();
            if (validator == null) validator = gameObject.AddComponent<HexPlacementValidator>();
        }

        if (ghostVisual == null)
        {
            ghostVisual = GetComponent<HexGhostVisual>();
            if (ghostVisual == null) ghostVisual = gameObject.AddComponent<HexGhostVisual>();
        }
    }

    private void SyncSettingsToSubComponents()
    {
        if (raycaster != null)
        {
            raycaster.Initialize(mainCamera, mapLayerMask);
        }

        if (ghostVisual != null)
        {
            if (ghostMaterial != null) ghostVisual.GhostMaterial = ghostMaterial;
            ghostVisual.PreviewHeightOffset = previewHeightOffset;
            ghostVisual.PreviewScale = previewScale;
            ghostVisual.ValidColor = validColor;
            ghostVisual.InvalidColor = invalidColor;
        }
    }

    private void Update()
    {
        if (isPreviewing && ghostVisual != null && ghostVisual.IsGhostActive)
        {
            ghostVisual.AnimatePulseVisuals(isHoveringValidTile);
        }
    }

    /// <summary>
    /// Bắt đầu hiển thị Ghost Preview khi người chơi kéo thẻ bài
    /// </summary>
    public void StartPreview(CardData cardData)
    {
        CancelPreview();

        if (worldGenerator == null) worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
        if (mainCamera == null) mainCamera = Camera.main;

        SetupModularComponents();
        SyncSettingsToSubComponents();

        currentCardData = cardData;
        isPreviewing = true;
        isHoveringValidTile = false;
        hasValidPreviousHex = false;

        if (ghostVisual != null)
        {
            ghostVisual.BuildPreview(cardData, ghostMaterial);
        }
    }

    /// <summary>
    /// Cập nhật vị trí và tính hợp lệ của Ghost theo vị trí con trỏ chuột trên màn hình
    /// </summary>
    public void UpdatePreview(Vector2 screenPosition)
    {
        if (!isPreviewing) return;
        if (mainCamera == null) mainCamera = Camera.main;

        if (raycaster == null || ghostVisual == null || validator == null)
        {
            SetupModularComponents();
            SyncSettingsToSubComponents();
        }

        if (mainCamera == null || raycaster == null) return;

        // Nếu chuột còn ở sát cạnh dưới màn hình (khu vực Hand/Deck)
        if (raycaster.IsOverHandArea(screenPosition))
        {
            if (ghostVisual != null) ghostVisual.SetActive(false);
            currentActiveTileObj = null;
            isHoveringValidTile = false;
            return;
        }

        if (raycaster.TryRaycastHex(screenPosition, mainCamera, out HexCoordinates targetHex, out _))
        {
            GameObject tileObj = null;
            bool isTileInMap = (worldGenerator != null &&
                                worldGenerator.MapTiles != null &&
                                worldGenerator.MapTiles.TryGetValue(targetHex, out tileObj));

            float surfaceY = raycaster.GetTileSurfaceY(tileObj);
            Vector3 baseTilePos = tileObj != null
                ? tileObj.transform.position
                : HexMetrics.HexToWorldPosition(targetHex, worldGenerator != null ? worldGenerator.SpawnOffsetY : HexMetrics.DefaultTileY);

            bool isValid = isTileInMap && validator != null && validator.ValidatePlacement(targetHex, worldGenerator, currentCardData, out _);
            bool hexChanged = (!hasValidPreviousHex || targetHex != currentHoverHex);

            currentHoverHex = targetHex;
            isHoveringValidTile = isValid;
            hasValidPreviousHex = true;

            if (ghostVisual != null)
            {
                ghostVisual.SetActive(true);
                ghostVisual.SetPositionAndOffset(baseTilePos, surfaceY, currentCardData);

                if (hexChanged)
                {
                    currentActiveTileObj = tileObj;
                    ghostVisual.OnHexSelectionChanged(tileObj);
                }
                else
                {
                    ghostVisual.AnimatePulseVisuals(isValid);
                }
            }
        }
        else
        {
            isHoveringValidTile = false;
            currentActiveTileObj = null;
            if (ghostVisual != null) ghostVisual.SetActive(false);
        }
    }

    /// <summary>
    /// Xác nhận đặt vật thể của lá bài lên ô lục giác đang trỏ tới
    /// </summary>
    public bool TryConfirmPlacement(CardData cardData, out HexCoordinates placedHex)
    {
        placedHex = currentHoverHex;

        if (!isPreviewing || !isHoveringValidTile || worldGenerator == null || validator == null)
        {
            CancelPreview();
            return false;
        }

        if (!validator.ValidatePlacement(currentHoverHex, worldGenerator, cardData, out GameObject targetTileObj))
        {
            CancelPreview();
            return false;
        }

        currentActiveTileObj = null;

        float surfaceY = raycaster != null ? raycaster.GetTileSurfaceY(targetTileObj) : HexMetrics.TileHeight;
        Vector3 spawnWorldPos = HexMetrics.HexToWorldPosition(currentHoverHex, surfaceY);

        if (cardData != null)
        {
            spawnWorldPos += cardData.placementOffset;
        }

        PlacedCard placedCardInstance = null;

        // Sinh vật phẩm / hệ sinh thái Props hoặc biến đổi địa hình
        if (cardData != null)
        {
            // TRƯỜNG HỢP 1: THẺ BIẾN ĐỔI ĐỊA HÌNH (SPECIAL CARD NHƯ RAIN)
            if (cardData.IsTileTransformCard())
            {
                placedCardInstance = TransformTile(targetTileObj, currentHoverHex, cardData);
            }
            // TRƯỜNG HỢP 2: THẺ SINH THÁI TỔ HỢP PROPS (HABITAT PROPS NHƯ FOREST, MOUNTAIN...)
            else if (cardData.HasHabitatProps())
            {
                HexHabitatSpawner.Instance.SpawnHabitat(cardData, targetTileObj.transform, spawnWorldPos);

                GameObject cardRecord = new GameObject($"PlacedCard_{cardData.cardName}");
                cardRecord.transform.SetParent(targetTileObj.transform, false);
                placedCardInstance = cardRecord.AddComponent<PlacedCard>();
                placedCardInstance.cardData = cardData;
                placedCardInstance.placedHex = currentHoverHex;

                HexTileInfo tileInfo = targetTileObj.GetComponent<HexTileInfo>();
                if (tileInfo != null && cardData.occupiesTile)
                {
                    tileInfo.isOccupied = true;
                }
            }
            // TRƯỜNG HỢP 3: THẺ ĐẶT MÔ HÌNH ĐƠN LẺ
            else if (cardData.prefabToPlace != null)
            {
                GameObject placedObject = Instantiate(cardData.prefabToPlace, spawnWorldPos, Quaternion.identity, targetTileObj.transform);
                placedObject.name = $"{cardData.cardName}_{currentHoverHex.Q}_{currentHoverHex.R}";

                placedCardInstance = placedObject.GetComponent<PlacedCard>();
                if (placedCardInstance == null)
                {
                    placedCardInstance = placedObject.AddComponent<PlacedCard>();
                }
                placedCardInstance.cardData = cardData;
                placedCardInstance.placedHex = currentHoverHex;

                HexTileInfo tileInfo = targetTileObj.GetComponent<HexTileInfo>();
                if (tileInfo != null && cardData.occupiesTile)
                {
                    tileInfo.isOccupied = true;
                }

                StartCoroutine(AnimatePopIn(placedObject.transform));
            }
            else
            {
                GameObject cardRecord = new GameObject($"PlacedCard_{cardData.cardName}");
                cardRecord.transform.SetParent(targetTileObj.transform, false);
                placedCardInstance = cardRecord.AddComponent<PlacedCard>();
                placedCardInstance.cardData = cardData;
                placedCardInstance.placedHex = currentHoverHex;

                HexTileInfo tileInfo = targetTileObj.GetComponent<HexTileInfo>();
                if (tileInfo != null && cardData.occupiesTile)
                {
                    tileInfo.isOccupied = true;
                }
            }

            // Tính toán lại điểm số
            if (ScoreCalculator.Instance != null)
            {
                ScoreCalculator.Instance.RecalculateScore();
            }

            // Hiệu ứng cộng điểm "+X" (Preserve style)
            ShowPlacementScorePopup(placedCardInstance);
        }

        CancelPreview();
        return true;
    }

    /// <summary>
    /// Biến đổi ô lục giác hiện tại (đất khô) thành ô đất mới (tươi tốt) khi sử dụng thẻ Special như Rain
    /// </summary>
    private PlacedCard TransformTile(GameObject oldTileObj, HexCoordinates coords, CardData cardData)
    {
        if (oldTileObj == null || worldGenerator == null) return null;

        // 1. Xác định Prefab mới (Lush) tương ứng với ô Arid hiện tại
        GameObject transformedPrefab = cardData.GetTransformedPrefab(oldTileObj);
        if (transformedPrefab == null)
        {
            transformedPrefab = cardData.prefabToPlace;
        }

        if (transformedPrefab == null)
        {
            Debug.LogWarning($"[TransformTile] Không tìm thấy prefab đích để biến đổi cho ô {oldTileObj.name}");
            return null;
        }

        Vector3 tilePos = oldTileObj.transform.position;
        Quaternion tileRot = oldTileObj.transform.rotation;
        Transform parentTransform = oldTileObj.transform.parent;

        // 2. Xóa ô cũ khỏi Scene
        Destroy(oldTileObj);

        // 3. Sinh khối lục giác mới (Lush tile)
        GameObject newTileObj = Instantiate(transformedPrefab, tilePos, tileRot, parentTransform);
        newTileObj.name = $"Hex_{coords.Q}_{coords.R}_[{transformedPrefab.name}]_Transformed";

        // Đảm bảo có MeshCollider để chuột Raycast chính xác bề mặt
        if (newTileObj.GetComponent<Collider>() == null)
        {
            MeshFilter mf = newTileObj.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc = newTileObj.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        }

        // 4. Cập nhật HexTileInfo cho ô mới
        HexTileInfo newTileInfo = newTileObj.GetComponent<HexTileInfo>();
        if (newTileInfo == null)
        {
            newTileInfo = newTileObj.AddComponent<HexTileInfo>();
        }
        newTileInfo.sourcePrefab = transformedPrefab;
        newTileInfo.terrainTypeIndex = -1;
        newTileInfo.coordinates = coords;
        newTileInfo.isOccupied = cardData.occupiesTile; // Rain: occupiesTile = false

        // 6. Cập nhật MapTiles trong HexWorldGenerator
        if (worldGenerator.MapTiles.ContainsKey(coords))
        {
            worldGenerator.MapTiles[coords] = newTileObj;
        }
        else
        {
            worldGenerator.MapTiles.Add(coords, newTileObj);
        }

        // 7. Tạo record PlacedCard cho thẻ Rain
        GameObject cardRecord = new GameObject($"PlacedCard_{cardData.cardName}");
        cardRecord.transform.SetParent(newTileObj.transform, false);
        PlacedCard placedCard = cardRecord.AddComponent<PlacedCard>();
        placedCard.cardData = cardData;
        placedCard.placedHex = coords;

        // 8. Hiệu ứng pop-in nảy nhẹ khi đất được tưới nước tươi tốt
        StartCoroutine(AnimatePopIn(newTileObj.transform));

        return placedCard;
    }

    /// <summary>
    /// Hiển thị hiệu ứng "+X" cho phần điểm TĂNG THÊM của từng khối trong nhóm
    /// </summary>
    private void ShowPlacementScorePopup(PlacedCard specificCard = null)
    {
        if (HexGroupDetector.Instance == null) return;

        PlacedCard placedCard = specificCard;

        if (placedCard == null)
        {
            GameObject tileObj = null;
            if (worldGenerator != null)
            {
                worldGenerator.MapTiles.TryGetValue(currentHoverHex, out tileObj);
            }

            placedCard = tileObj != null
                ? tileObj.GetComponentInChildren<PlacedCard>()
                : null;
        }

        if (placedCard == null || placedCard.cardData == null) return;

        List<PlacedCard> group = HexGroupDetector.Instance.FindGroup(placedCard);
        if (group == null || group.Count == 0)
        {
            group = new List<PlacedCard> { placedCard };
        }

        int groupRequired = 3;
        int groupMultiplier = 2;

        if (ScoreCalculator.Instance != null)
        {
            groupRequired = ScoreCalculator.Instance.GroupRequired;
            groupMultiplier = ScoreCalculator.Instance.GroupMultiplier;
        }

        bool isCompleteGroup = group.Count >= groupRequired;

        foreach (PlacedCard card in group)
        {
            if (card == null || card.cardData == null) continue;

            int targetScore = card.cardData.alwaysBaseScore
                ? card.cardData.baseScore
                : (isCompleteGroup ? card.cardData.baseScore * groupMultiplier : card.cardData.baseScore);

            int gained = targetScore - card.displayedScore;

            if (gained <= 0) continue;

            Vector3 cardWorldPos = HexMetrics.HexToWorldPosition(card.placedHex, HexMetrics.TileHeight);
            ScorePopupManager.ShowScore(gained, cardWorldPos);

            card.displayedScore = targetScore;
        }
    }

    /// <summary>
    /// Hủy bỏ preview và dọn dẹp
    /// </summary>
    public void CancelPreview()
    {
        isPreviewing = false;
        isHoveringValidTile = false;
        hasValidPreviousHex = false;
        currentActiveTileObj = null;

        if (ghostVisual != null)
        {
            ghostVisual.ClearVisuals();
        }
    }

    /// <summary>
    /// Kiểm tra xem ô lục giác đã được đặt bài / vật phẩm từ trước hay chưa
    /// </summary>
    public bool IsTileOccupied(HexCoordinates coords)
    {
        if (validator != null)
        {
            return validator.IsTileOccupied(coords, worldGenerator);
        }
        return false;
    }

    /// <summary>
    /// Kiểm tra xem GameObject của ô lục giác đã được đặt bài / vật phẩm từ trước hay chưa
    /// </summary>
    public bool IsTileOccupied(GameObject tileObj)
    {
        if (validator != null)
        {
            return validator.IsTileOccupied(tileObj);
        }
        return false;
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

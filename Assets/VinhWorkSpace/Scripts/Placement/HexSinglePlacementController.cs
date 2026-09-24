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

        // Sinh vật phẩm / hệ sinh thái Props lên đỉnh ô
        if (cardData != null)
        {
            if (cardData.HasHabitatProps())
            {
                // Sinh tổ hợp props ngẫu nhiên phong cách Preserve
                HexHabitatSpawner.Instance.SpawnHabitat(cardData, targetTileObj.transform, spawnWorldPos);

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
                // Lá bài không có prefab lẫn habitat props
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
            ShowPlacementScorePopup();
        }

        CancelPreview();
        return true;
    }

    /// <summary>
    /// Hiển thị hiệu ứng "+X" cho phần điểm TĂNG THÊM của từng khối trong nhóm
    /// </summary>
    private void ShowPlacementScorePopup()
    {
        if (HexGroupDetector.Instance == null) return;

        GameObject tileObj = null;
        if (worldGenerator != null)
        {
            worldGenerator.MapTiles.TryGetValue(currentHoverHex, out tileObj);
        }

        PlacedCard placedCard = tileObj != null
            ? tileObj.GetComponentInChildren<PlacedCard>()
            : null;

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

            int targetScore = isCompleteGroup
                ? card.cardData.baseScore * groupMultiplier
                : card.cardData.baseScore;

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

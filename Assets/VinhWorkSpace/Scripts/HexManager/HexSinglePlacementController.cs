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

        // Sao chép toàn bộ các MeshFilter và MeshRenderer từ khối đang trỏ tới sang vỏ bọc
        MeshFilter[] sourceFilters = targetTileObj.GetComponentsInChildren<MeshFilter>();
        foreach (var srcMf in sourceFilters)
        {
            if (srcMf == null || srcMf.sharedMesh == null) continue;

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
            if (isTileInMap)
            {
                tileObj = worldGenerator.MapTiles[targetHex];
                surfaceY = GetTileSurfaceY(tileObj);
                baseTilePos = tileObj.transform.position;

                if (currentCardData != null)
                {
                    isAllowedByCard = currentCardData.IsTileAllowed(tileObj, worldGenerator);
                }
            }

            bool isValid = isTileInMap && isAllowedByCard;
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

        // Ghi lại điểm trước khi đặt để tính lượng điểm tăng thêm (hiệu ứng +X)
        int scoreBefore = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;

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

            // Tính toán lại điểm số
            if (ScoreCalculator.Instance != null)
            {
                ScoreCalculator.Instance.RecalculateScore();
            }

            // Hiệu ứng cộng điểm "+X" (Preserve style)
            // Hiển thị tại TỪNG khối trong nhóm với điểm riêng của nó:
            //  - Nhóm < 3 khối  -> mỗi khối +1 (baseScore)
            //  - Nhóm >= 3 khối -> mỗi khối +2 (baseScore x hệ số nhóm)
            ShowGroupScorePopups();
        }

        CancelPreview();
        return true;
    }

    /// <summary>
    /// Hiển thị hiệu ứng "+X" tại từng khối trong nhóm của card vừa đặt.
    /// Điểm mỗi khối được tính lại đúng theo quy tắc nhóm hiện tại.
    /// </summary>
    private void ShowGroupScorePopups()
    {
        if (HexGroupDetector.Instance == null) return;

        // Tìm chip PlacedCard của khối vừa đặt
        GameObject tileObj = null;
        if (worldGenerator != null)
        {
            worldGenerator.MapTiles.TryGetValue(currentHoverHex, out tileObj);
        }

        PlacedCard placedCard = tileObj != null ? tileObj.GetComponent<PlacedCard>() : null;
        if (placedCard == null || placedCard.cardData == null) return;

        // Tìm toàn bộ nhóm cùng loại chứa khối vừa đặt
        System.Collections.Generic.List<PlacedCard> group =
            HexGroupDetector.Instance.FindGroup(placedCard);

        if (group == null || group.Count == 0) return;

        // Hệ số nhân theo quy tắc của ScoreCalculator
        int groupRequired = 3;
        int groupMultiplier = 2;

        if (ScoreCalculator.Instance != null)
        {
            groupRequired = ScoreCalculator.Instance.GroupRequired;
            groupMultiplier = ScoreCalculator.Instance.GroupMultiplier;
        }

        // Nhóm đủ lớn -> mỗi khối được nhân hệ số, ngược lại dùng điểm gốc
        bool isCompleteGroup = group.Count >= groupRequired;

        foreach (PlacedCard card in group)
        {
            if (card == null || card.cardData == null) continue;

            int cardScore = isCompleteGroup
                ? card.cardData.baseScore * groupMultiplier
                : card.cardData.baseScore;

            Vector3 worldPos = HexMetrics.HexToWorldPosition(card.placedHex, HexMetrics.TileHeight);
            ScorePopupManager.ShowScore(cardScore, worldPos);
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

    private float GetTileSurfaceY(GameObject tileObj)
    {
        if (tileObj != null)
        {
            Renderer r = tileObj.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                return r.bounds.max.y;
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

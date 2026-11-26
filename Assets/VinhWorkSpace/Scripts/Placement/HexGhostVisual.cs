using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Chuyên trách hiển thị hiệu ứng đồ họa xem trước (Ghost Preview):
/// Vỏ bọc Hologram, đổi màu dạ quang (xanh hợp lệ / đỏ không hợp lệ),
/// nhịp thở (breathing pulse) và hiệu ứng nảy (snap punch).
/// </summary>
public class HexGhostVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Material ghostMaterial;
    [Tooltip("Độ dịch lên theo trục Y để vỏ bọc trùm hẳn lên trên bề mặt đỉnh")]
    [SerializeField] private float previewHeightOffset = 0.04f;
    [Tooltip("Tỉ lệ phóng to của vỏ bọc Hologram")]
    [SerializeField] private Vector3 previewScale = new Vector3(1.03f, 1.05f, 1.03f);

    [Header("Indicator Colors")]
    [Tooltip("Màu xanh lá dạ quang bao trọn ô")]
    [SerializeField] private Color validColor = new Color(0.20f, 1.0f, 0.45f, 0.85f);
    [Tooltip("Màu đỏ cảnh báo khi trỏ ra ngoài ô")]
    [SerializeField] private Color invalidColor = new Color(0.95f, 0.20f, 0.20f, 0.70f);

    [Header("Placement Fee Label")]
    [Tooltip("Độ cao của nhãn phí đặt thẻ so với mặt ô")]
    [SerializeField] private float feeLabelHeight = 0.9f;
    [Tooltip("Màu nhãn phí khi đủ Coins (thiếu Coins thì nhãn dùng màu đỏ cảnh báo)")]
    [SerializeField] private Color feeAffordableColor = new Color(1.0f, 0.84f, 0.25f, 1.0f);

    public Material GhostMaterial { get => ghostMaterial; set => ghostMaterial = value; }
    public float PreviewHeightOffset { get => previewHeightOffset; set => previewHeightOffset = value; }
    public Vector3 PreviewScale { get => previewScale; set => previewScale = value; }
    public Color ValidColor { get => validColor; set => validColor = value; }
    public Color InvalidColor { get => invalidColor; set => invalidColor = value; }

    public bool IsGhostActive => ghostRoot != null && ghostRoot.activeSelf;

    private GameObject ghostRoot;
    private GameObject hologramShellObj;
    private GameObject itemGhostInstance;
    private CardData activeCardData;
    private readonly List<Renderer> cachedGhostRenderers = new List<Renderer>();
    private readonly List<(MeshRenderer renderer, bool valid)> triangleSlotRenderers = new List<(MeshRenderer, bool)>();
    private MaterialPropertyBlock propBlock;
    private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
    private Coroutine snapPunchCoroutine;
    private TextMeshPro feeLabel;
    private Camera cachedCam;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        // Nhãn phí luôn quay mặt về phía Camera (Billboard)
        if (feeLabel == null || !feeLabel.gameObject.activeInHierarchy) return;

        if (cachedCam == null) cachedCam = Camera.main;
        if (cachedCam != null)
        {
            feeLabel.transform.rotation = cachedCam.transform.rotation;
        }
    }

    /// <summary>
    /// Xây dựng đối tượng Ghost và vật mẫu xem trước của lá bài
    /// </summary>
    public void BuildPreview(CardData cardData, Material fallbackMaterial = null)
    {
        ClearVisuals();
        activeCardData = cardData;

        if (ghostMaterial == null && fallbackMaterial != null)
        {
            ghostMaterial = fallbackMaterial;
        }

        ghostRoot = new GameObject("SingleHexPlacementRoot");

        // Tạo mô hình xem trước (Cây / Đá /...) nếu có
        GameObject previewPrefab = GetPreviewPrefab(cardData);
        if (previewPrefab != null)
        {
            itemGhostInstance = Instantiate(previewPrefab, ghostRoot.transform);
            itemGhostInstance.name = "GhostItemPreview";
            itemGhostInstance.transform.localPosition = Vector3.zero;

            if (cardData != null && cardData.IsSettlementCard())
            {
                itemGhostInstance.transform.localScale = Vector3.one * SettlementAssetConfig.GetGlobalScale(SettlementScaleType.Building);
            }

            NeutralizeGhostInstance(itemGhostInstance);
            ApplyGhostMaterial(itemGhostInstance);
        }

        ghostRoot.SetActive(false);
        RefreshCachedRenderers();
        ApplyColorToAllRenderers(invalidColor);
    }

    /// <summary>
    /// Cập nhật vị trí và độ cao cho Ghost Root và Item xem trước
    /// </summary>
    public void SetPositionAndOffset(Vector3 baseTilePos, float surfaceY, CardData cardData)
    {
        if (ghostRoot == null) return;
        ghostRoot.transform.position = baseTilePos;

        if (itemGhostInstance != null)
        {
            float localY = surfaceY - baseTilePos.y + previewHeightOffset;
            Vector3 offset = cardData != null ? cardData.placementOffset : Vector3.zero;
            itemGhostInstance.transform.localPosition = new Vector3(0f, localY, 0f) + offset;
        }

        if (feeLabel != null)
        {
            feeLabel.transform.localPosition = new Vector3(0f, surfaceY - baseTilePos.y + feeLabelHeight, 0f);
        }
    }

    /// <summary>
    /// Phí đặt thẻ nay hiển thị bằng P-04 Floating White Tooltip Pill trong HexPlacementPreviewHUD.
    /// Ẩn nhãn 3D cũ để không bị trùng lặp.
    /// </summary>
    public void SetPlacementFee(int fee, bool canAfford)
    {
        if (feeLabel != null)
        {
            feeLabel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Bật/tắt hiển thị Ghost Root
    /// </summary>
    public void SetActive(bool active)
    {
        if (ghostRoot != null && ghostRoot.activeSelf != active)
        {
            ghostRoot.SetActive(active);
        }
    }

    /// <summary>
    /// Xử lý khi chuột chuyển sang một ô mới
    /// </summary>
    public void OnHexSelectionChanged(GameObject newTileObj, bool isValid = true)
    {
        triangleSlotRenderers.Clear();
        if (newTileObj != null)
        {
            // Đồng bộ vỏ bọc theo đúng hình dáng của ô lục giác mới
            SyncHologramShellToTile(newTileObj);
            AnimatePulseVisuals(isValid);

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

    /// <summary>
    /// Đồng bộ vỏ bọc 3D Hologram khớp 100% với Mesh của ô lục giác đang trỏ tới (hoặc ô Lush nếu là thẻ Special như Rain)
    /// </summary>
    public void SyncHologramShellToTile(GameObject targetTileObj)
    {
        if (ghostRoot == null || targetTileObj == null) return;

        if (hologramShellObj != null)
        {
            Destroy(hologramShellObj);
            hologramShellObj = null;
        }
        triangleSlotRenderers.Clear();

        hologramShellObj = CreateShellForTile(targetTileObj, ghostRoot.transform, useWorldPos: false, null);
        RefreshCachedRenderers();
    }

    public void SyncTriangleHologramShells(List<(HexCoordinates hex, GameObject tileObj, bool valid)> slots, bool canAfford)
    {
        if (ghostRoot == null) return;

        if (hologramShellObj != null)
        {
            Destroy(hologramShellObj);
            hologramShellObj = null;
        }
        triangleSlotRenderers.Clear();

        hologramShellObj = new GameObject("HologramTriangleShells");
        hologramShellObj.transform.SetParent(ghostRoot.transform, false);
        hologramShellObj.transform.localPosition = Vector3.zero;
        hologramShellObj.transform.localRotation = Quaternion.identity;
        hologramShellObj.transform.localScale = Vector3.one;

        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.tileObj == null) continue;
                bool slotValid = slot.valid && canAfford;
                CreateShellForTile(slot.tileObj, hologramShellObj.transform, useWorldPos: true, mr => triangleSlotRenderers.Add((mr, slotValid)));
            }
        }

        RefreshCachedRenderers();
        AnimatePulseVisuals(canAfford);
    }

    private GameObject CreateShellForTile(GameObject targetTileObj, Transform parent, bool useWorldPos, System.Action<MeshRenderer> onRendererCreated)
    {
        GameObject shell = new GameObject("HologramEncasementShell");
        shell.transform.SetParent(parent, false);
        if (useWorldPos)
        {
            shell.transform.position = targetTileObj.transform.position + new Vector3(0f, previewHeightOffset, 0f);
            shell.transform.rotation = targetTileObj.transform.rotation;
        }
        else
        {
            shell.transform.localPosition = new Vector3(0f, previewHeightOffset, 0f);
            shell.transform.rotation = targetTileObj.transform.rotation;
        }
        shell.transform.localScale = Vector3.Scale(previewScale, targetTileObj.transform.lossyScale);

        GameObject meshSource = targetTileObj;
        bool isTransformedPrefab = false;

        if (activeCardData != null && activeCardData.IsTileTransformCard())
        {
            var disasterSt = targetTileObj.GetComponent<LumiWorld.Acs.TileDisasterState>();
            bool hasDisaster = disasterSt != null && (disasterSt.HasAnyDisaster || disasterSt.isBurned);

            if (!hasDisaster)
            {
                GameObject transformed = activeCardData.GetTransformedPrefab(targetTileObj);
                if (transformed != null)
                {
                    meshSource = transformed;
                    isTransformedPrefab = true;
                }
            }
        }

        HexHighlightShellUtility.PopulateMeshHierarchy(
            meshSource.transform,
            shell.transform,
            ghostMaterial,
            skipPlacedProps: !isTransformedPrefab,
            onRendererCreated);

        return shell;
    }

    /// <summary>
    /// Hiệu ứng thở nhẹ (Breathing Pulse) đổi màu xanh lá dạ quang hoặc đỏ cảnh báo
    /// </summary>
    public void AnimatePulseVisuals(bool isValid)
    {
        if (ghostRoot == null) return;

        float pulse = 0.78f + 0.12f * Mathf.Sin(Time.time * 4.0f);
        Color pulseValidColor = new Color(validColor.r, validColor.g, validColor.b, pulse);

        if (triangleSlotRenderers.Count > 0)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            for (int i = 0; i < triangleSlotRenderers.Count; i++)
            {
                var entry = triangleSlotRenderers[i];
                if (entry.renderer == null) continue;
                entry.renderer.GetPropertyBlock(propBlock);
                propBlock.SetColor(ColorProperty, entry.valid ? pulseValidColor : invalidColor);
                entry.renderer.SetPropertyBlock(propBlock);
            }
            return;
        }

        if (isValid)
        {
            ApplyColorToAllRenderers(pulseValidColor);
        }
        else
        {
            ApplyColorToAllRenderers(invalidColor);
        }
    }

    /// <summary>
    /// Dọn dẹp đối tượng Ghost
    /// </summary>
    public void ClearVisuals()
    {
        cachedGhostRenderers.Clear();
        triangleSlotRenderers.Clear();

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
            feeLabel = null;
        }
    }

    private GameObject GetPreviewPrefab(CardData cardData)
    {
        if (cardData == null) return null;
        // Thẻ biến đổi địa hình (như Rain) không sinh vật phẩm nổi lên trên mặt ô
        if (cardData.IsTileTransformCard()) return null;
        if (cardData.IsRiverCard())
        {
            if (RiverNetworkManager.Instance != null && RiverNetworkManager.Instance.Config != null)
            {
                bool isFirstRiverTile = (RiverNetworkManager.Instance.RiverMap == null || RiverNetworkManager.Instance.RiverMap.Count == 0);
                return isFirstRiverTile
                    ? RiverNetworkManager.Instance.Config.springHeadPrefab
                    : RiverNetworkManager.Instance.Config.straightPrefab;
            }
        }

        if (cardData.IsSettlementCard())
        {
            if (SettlementClusterManager.Instance != null && SettlementClusterManager.Instance.Config != null)
            {
                var cfg = SettlementClusterManager.Instance.Config;
                if (cfg.smallHouses != null && cfg.smallHouses.Count > 0 && cfg.smallHouses[0] != null)
                    return cfg.smallHouses[0];
                if (cfg.villageCenterpieces != null && cfg.villageCenterpieces.Count > 0 && cfg.villageCenterpieces[0] != null)
                    return cfg.villageCenterpieces[0];
            }
        }

        if (cardData.prefabToPlace != null) return cardData.prefabToPlace;

        if (cardData.HasHabitatProps())
        {
            foreach (var rule in cardData.habitatProps)
            {
                if (rule != null && rule.prefabs != null && rule.prefabs.Count > 0 && rule.prefabs[0] != null)
                {
                    return rule.prefabs[0];
                }
            }
        }

        return null;
    }

    private void RefreshCachedRenderers()
    {
        cachedGhostRenderers.Clear();
        if (ghostRoot != null)
        {
            ghostRoot.GetComponentsInChildren(true, cachedGhostRenderers);

            // Nhãn phí giữ màu riêng, không nhuộm theo màu Ghost
            if (feeLabel != null) cachedGhostRenderers.Remove(feeLabel.GetComponent<Renderer>());
        }
    }

    private void ApplyColorToAllRenderers(Color targetColor)
    {
        if (ghostRoot == null) return;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        for (int i = 0; i < cachedGhostRenderers.Count; i++)
        {
            var r = cachedGhostRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor(ColorProperty, targetColor);
            r.SetPropertyBlock(propBlock);
        }
    }

    /// <summary>
    /// Vô hiệu hóa toàn bộ chuyển động, hoạt ảnh, collider và AI trên mô hình preview.
    /// Giúp mô hình đứng yên hoàn toàn (static dummy), không mô phỏng con thú thật khi đang rê chuột.
    /// </summary>
    private void NeutralizeGhostInstance(GameObject obj)
    {
        if (obj == null) return;

        // 1. Tắt toàn bộ Collider để không bắt va chạm hoặc cản trở Raycast chuột
        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // 2. Tắt toàn bộ Animator để mô hình đứng yên hoàn toàn (bind/idle pose tĩnh)
        Animator[] animators = obj.GetComponentsInChildren<Animator>(true);
        foreach (var anim in animators)
        {
            anim.enabled = false;
        }

        // 3. Tắt và dừng toàn bộ Animation truyền thống (Legacy)
        Animation[] legacyAnims = obj.GetComponentsInChildren<Animation>(true);
        foreach (var legAnim in legacyAnims)
        {
            legAnim.Stop();
            legAnim.enabled = false;
        }

        // 4. Vô hiệu hóa toàn bộ MonoBehaviour (AnimalMovementAI, FarmAnimalAI, AnimalVisualEnhancer, AnimalIndividual...)
        // Tránh việc con thú chạy script di chuyển, tính toán tài nguyên hay hiệu ứng trong preview
        MonoBehaviour[] scripts = obj.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var script in scripts)
        {
            script.enabled = false;
        }

        // 5. Khóa Rigidbody nếu có
        Rigidbody[] rbs = obj.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rbs)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // 6. Tắt AudioSource nếu có
        AudioSource[] audios = obj.GetComponentsInChildren<AudioSource>(true);
        foreach (var a in audios)
        {
            a.enabled = false;
        }
    }

    private void ApplyGhostMaterial(GameObject obj)
    {
        if (ghostMaterial == null) return;

        // Áp dụng cho cả MeshRenderer và SkinnedMeshRenderer (đối với mô hình thú)
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.sharedMaterial = ghostMaterial;
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
}

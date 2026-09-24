using System.Collections;
using System.Collections.Generic;
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

    public Material GhostMaterial { get => ghostMaterial; set => ghostMaterial = value; }
    public float PreviewHeightOffset { get => previewHeightOffset; set => previewHeightOffset = value; }
    public Vector3 PreviewScale { get => previewScale; set => previewScale = value; }
    public Color ValidColor { get => validColor; set => validColor = value; }
    public Color InvalidColor { get => invalidColor; set => invalidColor = value; }

    public bool IsGhostActive => ghostRoot != null && ghostRoot.activeSelf;

    private GameObject ghostRoot;
    private GameObject hologramShellObj;
    private GameObject itemGhostInstance;
    private readonly List<MeshRenderer> cachedGhostRenderers = new List<MeshRenderer>();
    private MaterialPropertyBlock propBlock;
    private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
    private Coroutine snapPunchCoroutine;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Xây dựng đối tượng Ghost và vật mẫu xem trước của lá bài
    /// </summary>
    public void BuildPreview(CardData cardData, Material fallbackMaterial = null)
    {
        ClearVisuals();

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

            DisableColliders(itemGhostInstance);
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
    public void OnHexSelectionChanged(GameObject newTileObj)
    {
        if (newTileObj != null)
        {
            // Đồng bộ vỏ bọc theo đúng hình dáng của ô lục giác mới
            SyncHologramShellToTile(newTileObj);
            AnimatePulseVisuals(true);

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
    /// Đồng bộ vỏ bọc 3D Hologram khớp 100% với Mesh của ô lục giác đang trỏ tới
    /// </summary>
    public void SyncHologramShellToTile(GameObject targetTileObj)
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
    /// Hiệu ứng thở nhẹ (Breathing Pulse) đổi màu xanh lá dạ quang hoặc đỏ cảnh báo
    /// </summary>
    public void AnimatePulseVisuals(bool isValid)
    {
        if (ghostRoot == null) return;

        if (isValid)
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
    /// Dọn dẹp đối tượng Ghost
    /// </summary>
    public void ClearVisuals()
    {
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

    private GameObject GetPreviewPrefab(CardData cardData)
    {
        if (cardData == null) return null;
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
        }
    }

    private void ApplyColorToAllRenderers(Color targetColor)
    {
        if (ghostRoot == null) return;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

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

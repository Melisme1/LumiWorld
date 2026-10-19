using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using LumiWorld.Acs;

/// <summary>
/// Quản lý tập trung các Huy hiệu hình thoi (Diamond Badges) nổi trên các cụm Habitat trong game (Preserve Style).
/// Điều khiển cơ chế hiển thị chuẩn GDD và thẩm mỹ:
/// - Bình thường: Ẩn đi để bản đồ sạch đẹp và thông thoáng.
/// - Khi người chơi kéo thẻ Thú (Creature Card): Hiện toàn bộ các cụm Habitat để dễ dàng quan sát và thả thú.
/// - Khi người chơi bấm vào các ô đất thuộc về Habitat: Hiện huy hiệu của Habitat đó.
/// - Độ cao nổi: 4.2m trên đỉnh cây và props, hoàn toàn không bị che khuất.
/// </summary>
public class HabitatDiamondBadgeManager : MonoBehaviour
{
    private static HabitatDiamondBadgeManager instance;
    public static HabitatDiamondBadgeManager Instance => instance;

    [Header("Settings")]
    [Tooltip("Bật/Tắt hiển thị huy hiệu hình thoi trên 3D Habitat (Đã tắt theo yêu cầu chuyển sang bảng 2 bên)")]
    [SerializeField] private bool showDiamondBadges = false;

    [Tooltip("Độ cao nổi phía trên mặt đất vừa vặn")]
    [SerializeField] private float badgeFloatHeight = 2.6f;

    private readonly Dictionary<string, HabitatDiamondBadge> activeBadges = new Dictionary<string, HabitatDiamondBadge>();
    private HabitatRuntimeManager habitatManager;
    private Camera mainCam;

    private bool isDraggingCreature = false;
    private string selectedHabitatId = null;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        mainCam = Camera.main;
        if (HabitatRuntimeManager.TryGetActive(out habitatManager))
        {
            habitatManager.HabitatsChanged += HandleHabitatsChanged;
            RefreshBadges();
        }
    }

    private void OnDestroy()
    {
        if (habitatManager != null)
        {
            habitatManager.HabitatsChanged -= HandleHabitatsChanged;
        }
    }

    private void Update()
    {
        HandleMapTileClick();
    }

    /// <summary>
    /// Phát hiện khi người chơi nhấp chuột vào ô đất trên bản đồ để bật/tắt huy hiệu Habitat
    /// </summary>
    private void HandleMapTileClick()
    {
        if (isDraggingCreature) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        Vector2 mousePos = mouse.position.ReadValue();
        if (EconomyHUD.IsPointerOverInteractiveUI(mousePos)) return;

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        Ray ray = mainCam.ScreenPointToRay(mousePos);
        var hits = Physics.RaycastAll(ray, 1000f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool clickedHabitatTile = false;

        foreach (var hit in hits)
        {
            var identity = hit.collider.GetComponentInParent<HabitatPlacementIdentity>();
            var tile = hit.collider.GetComponentInParent<HexTileInfo>();

            // Chỉ chấp nhận khi chuột trỏ trên bề mặt trên cùng của ô lục giác (không nhận khi bấm vào sườn vách/cạnh bên)
            if (identity == null && tile != null && !HexMetrics.IsTopSurfaceHit(hit))
            {
                // Chạm vào cạnh bên / vách đứng của ô -> không tính là chọn bề mặt và dừng kiểm tra để không xuyên thấu
                break;
            }

            if (habitatManager == null) HabitatRuntimeManager.TryGetActive(out habitatManager);
            if (habitatManager != null && habitatManager.Habitats != null)
            {
                foreach (var habitat in habitatManager.Habitats)
                {
                    if (habitat == null) continue;

                    bool match = (identity != null && (habitat.members.Exists(m => m.placementId == identity.PlacementId) ||
                                                       habitat.residents.Exists(r => r.individualId == identity.PlacementId))) ||
                                 (tile != null && habitat.ContainsHex(tile.coordinates));

                    if (match)
                    {
                        SelectHabitat(habitat.habitatId);
                        clickedHabitatTile = true;
                        return;
                    }
                }
            }

            // Đã chạm bề mặt của một ô lục giác (nhưng không thuộc Habitat nào) -> dừng kiểm tra không xuyên qua ô phía sau
            if (tile != null)
            {
                break;
            }
        }

        // Bấm ra ngoài khoảng trống / mặt nước -> Ẩn huy hiệu đang chọn
        if (!clickedHabitatTile && !string.IsNullOrEmpty(selectedHabitatId))
        {
            DeselectHabitat();
        }
    }

    private void HandleHabitatsChanged()
    {
        RefreshBadges();
    }

    /// <summary>
    /// Gọi khi bắt đầu kéo thẻ bài Thú -> Bật toàn bộ huy hiệu các Habitat lên (nếu showDiamondBadges được bật)
    /// </summary>
    public void OnStartDraggingCreature(CardData creatureCard)
    {
        if (!showDiamondBadges) return;
        isDraggingCreature = true;
        foreach (var badge in activeBadges.Values)
        {
            if (badge != null) badge.RefreshData();
        }
        UpdateBadgesVisibility();
    }

    /// <summary>
    /// Gọi khi thả xong hoặc hủy kéo thẻ bài Thú -> Ẩn toàn bộ huy hiệu (chỉ giữ lại habitat đang được chọn nếu có)
    /// </summary>
    public void OnStopDraggingCreature()
    {
        isDraggingCreature = false;
        UpdateBadgesVisibility();
    }

    /// <summary>
    /// Chọn một Habitat cụ thể khi bấm vào ô đất của nó -> Mở bảng sinh vật HabitatCreaturePanelUI
    /// </summary>
    public void SelectHabitat(string habitatId)
    {
        selectedHabitatId = habitatId;
        if (habitatManager == null) HabitatRuntimeManager.TryGetActive(out habitatManager);
        var habitat = habitatManager != null ? habitatManager.FindHabitat(habitatId) : null;
        if (habitat != null && HabitatCreaturePanelUI.Instance != null)
        {
            HabitatCreaturePanelUI.Instance.OpenForHabitat(habitat);
        }
        UpdateBadgesVisibility();
    }

    /// <summary>
    /// Hủy chọn Habitat -> Ẩn huy hiệu và đóng bảng sinh vật
    /// </summary>
    public void DeselectHabitat()
    {
        selectedHabitatId = null;
        UpdateBadgesVisibility();
        if (HabitatCreaturePanelUI.Instance != null)
        {
            HabitatCreaturePanelUI.Instance.ClosePanels();
        }
    }

    private void UpdateBadgesVisibility()
    {
        if (!showDiamondBadges)
        {
            foreach (var badge in activeBadges.Values)
            {
                if (badge != null) badge.SetVisible(false);
            }
            return;
        }
        foreach (var pair in activeBadges)
        {
            var badge = pair.Value;
            if (badge == null) continue;

            if (isDraggingCreature)
            {
                // Khi đang kéo thẻ thú: Hiện tất cả các cụm môi trường
                badge.SetVisible(true);
            }
            else if (!string.IsNullOrEmpty(selectedHabitatId) && pair.Key == selectedHabitatId)
            {
                // Khi đang chọn cụm môi trường này: Hiện huy hiệu
                badge.SetVisible(true);
            }
            else
            {
                // Bình thường: Ẩn đi
                badge.SetVisible(false);
            }
        }
    }

    /// <summary>
    /// Đồng bộ toàn bộ các Badge với danh sách Habitat hiện có trên bản đồ
    /// </summary>
    public void RefreshBadges()
    {
        if (!showDiamondBadges)
        {
            ClearAllBadges();
            return;
        }

        if (habitatManager == null)
        {
            HabitatRuntimeManager.TryGetActive(out habitatManager);
            if (habitatManager == null) return;
        }

        if (mainCam == null) mainCam = Camera.main;

        var currentHabitats = habitatManager.Habitats;
        HashSet<string> currentIds = new HashSet<string>();

        if (currentHabitats != null)
        {
            foreach (var habitat in currentHabitats)
            {
                if (habitat == null || string.IsNullOrEmpty(habitat.habitatId)) continue;
                currentIds.Add(habitat.habitatId);

                if (activeBadges.TryGetValue(habitat.habitatId, out var existingBadge))
                {
                    existingBadge.UpdateHabitat(habitat);
                }
                else
                {
                    // Tạo mới badge hình thoi cho habitat này
                    GameObject badgeObj = new GameObject($"DiamondBadge_{habitat.habitatId}");
                    badgeObj.transform.SetParent(transform, false);

                    HabitatDiamondBadge newBadge = badgeObj.AddComponent<HabitatDiamondBadge>();
                    newBadge.Initialize(habitat, mainCam, badgeFloatHeight);
                    activeBadges.Add(habitat.habitatId, newBadge);
                }
            }
        }

        // Xóa những badge của habitat không còn tồn tại
        List<string> toRemove = new List<string>();
        foreach (var pair in activeBadges)
        {
            if (!currentIds.Contains(pair.Key))
            {
                toRemove.Add(pair.Key);
                if (pair.Value != null) Destroy(pair.Value.gameObject);
            }
        }

        foreach (var id in toRemove)
        {
            activeBadges.Remove(id);
        }

        UpdateBadgesVisibility();
    }

    private void ClearAllBadges()
    {
        foreach (var badge in activeBadges.Values)
        {
            if (badge != null) Destroy(badge.gameObject);
        }
        activeBadges.Clear();
    }
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Component gắn trực tiếp lên Thú Đầu Đàn (Alpha Leader):
/// 1. Chống click xuyên (Spawn Grace Period 1.2s & Pointer Over UI guard).
/// 2. Hỗ trợ đầy đủ Unity Input System Package (New Input System):
///    - Phím ESC đóng bảng modal mượt mà.
///    - Click chuột trái vào thân thú để kích hoạt bảng tương tác.
/// 3. Khi người chơi click vào con Alpha: MỞ BẢNG QUAN SÁT & TƯƠNG TÁC (Inspection Modal):
///    - Người chơi có thể thoải mái xem thông tin chi tiết, cấp sao, hiệu ứng phong tỏa.
///    - Chọn "Thuần Hóa (Lấy Thẻ)" -> mới bắt đầu giải quyết sự cố và nhận Thẻ Thú vào tay.
///    - Chọn "Giữ Lại (Chỉ Ngắm)" -> đóng bảng, đàn thú tiếp tục ở lại chạy nhảy trên đảo.
/// </summary>
public class WildAlphaInteractable : MonoBehaviour
{
    private WildPackIncident associatedIncident;
    private float spawnTimestamp;
    private const float SpawnProtectionDuration = 1.2f;

    // Singleton modal controller để chỉ mở tối đa 1 bảng tại 1 thời điểm
    private static GameObject activeModalCanvasObj;

    public static bool IsModalOpen => activeModalCanvasObj != null;

    public event Action<WildPackIncident> OnAlphaClicked;

    private void Awake()
    {
        spawnTimestamp = Time.time;
    }

    public void Initialize(WildPackIncident incident)
    {
        this.associatedIncident = incident;
    }

    private void Update()
    {
        // 1. Phím ESC để đóng Modal nếu đang mở
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (activeModalCanvasObj != null)
            {
                CloseModal();
                return;
            }
        }

        // 2. Bắt click chuột bằng New Input System
        HandleMouseClickDetection();
    }

    private void HandleMouseClickDetection()
    {
        // Chống click xuyên trong 1.2s đầu sau khi spawn
        if (Time.time < spawnTimestamp + SpawnProtectionDuration) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        // Chặn nếu chuột đang nằm trên giao diện UI (Button, HUD, Card, etc.)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            if (hit.transform == this.transform || hit.transform.IsChildOf(this.transform))
            {
                HandleClick();
            }
        }
    }

    /// <summary>
    /// Xử lý khi người chơi click vào con Alpha: Mở bảng thông tin chi tiết
    /// </summary>
    public void HandleClick()
    {
        if (associatedIncident == null || !associatedIncident.IsActive) return;

        OnAlphaClicked?.Invoke(associatedIncident);
        ShowInspectionModal();
    }

    /// <summary>
    /// Hiển thị Bảng Thông Tin Chi Tiết & Tùy Chọn Hành Động cho Thú Đầu Đàn
    /// (Thiết kế đồng bộ theo phong cách ấm áp, parchment & forest của LumiWorld)
    /// </summary>
    private void ShowInspectionModal()
    {
        CloseModal();

        // Đóng panel Habitat nếu đang mở để tránh chồng chéo giao diện
        var invController = FindAnyObjectByType<LumiWorld.Acs.ResourceInventoryUIController>();
        invController?.CloseHabitatPanel();

        var skin = Resources.Load<LumiWorld.Acs.ResourceInventoryUISkin>("UI/ResourceInventoryUISkin");
        TMP_FontAsset fontAsset = skin != null && skin.font != null ? skin.font : null;

        activeModalCanvasObj = new GameObject("WildAlpha_InspectionModal");
        Canvas canvas = activeModalCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = activeModalCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        activeModalCanvasObj.AddComponent<GraphicRaycaster>();
        EconomyHUD.RegisterWindow(activeModalCanvasObj, CloseModal);

        // 1. Lớp nền mờ (Backdrop) chặn click ra thế giới 3D
        GameObject backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(activeModalCanvasObj.transform, false);
        Image bgImg = backdrop.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.16f, 0.14f, 0.60f);
        RectTransform bgRect = backdrop.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Button bgBtn = backdrop.AddComponent<Button>();
        bgBtn.onClick.AddListener(CloseModal);

        // 2. Khung Bảng Thông Tin Chính (Parchment Card)
        GameObject panel = new GameObject("PanelCard");
        panel.transform.SetParent(activeModalCanvasObj.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(560f, 480f);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImg = panel.AddComponent<Image>();
        if (skin != null && skin.windowMain != null)
        {
            panelImg.sprite = skin.windowMain;
            panelImg.type = Image.Type.Sliced;
            panelImg.pixelsPerUnitMultiplier = skin.windowPPU;
            panelImg.color = Color.white;
        }
        else
        {
            panelImg.color = new Color32(247, 242, 232, 255);
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color32(120, 147, 109, 255);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        // 3. Header: Nhãn phụ & Tiêu đề chính
        GameObject badgeObj = new GameObject("HeaderBadge");
        badgeObj.transform.SetParent(panel.transform, false);
        RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.sizeDelta = new Vector2(400f, 20f);
        badgeRect.anchoredPosition = new Vector2(0f, 198f);
        TextMeshProUGUI badgeText = badgeObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) badgeText.font = fontAsset;
        badgeText.text = "SỰ CỐ ĐẢO HOANG";
        badgeText.fontSize = 11;
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.characterSpacing = 2.5f;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.color = skin != null ? skin.gold : new Color(0.78f, 0.68f, 0.47f, 1f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panel.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(460f, 32f);
        titleRect.anchoredPosition = new Vector2(0f, 172f);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) titleText.font = fontAsset;
        titleText.text = "Thú Đầu Đàn Xuất Hiện";
        titleText.fontSize = 23;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = skin != null ? skin.forest : new Color(0.26f, 0.36f, 0.27f, 1f);

        // Nút đóng tròn góc trên bên phải
        CreateCloseButton(panel.transform, new Vector2(242f, 192f), new Vector2(34f, 34f), skin, fontAsset, CloseModal);

        // 4. Khung Chứa Nội Dung (Surface Inset)
        GameObject insetObj = new GameObject("ContentInset");
        insetObj.transform.SetParent(panel.transform, false);
        RectTransform insetRect = insetObj.AddComponent<RectTransform>();
        insetRect.sizeDelta = new Vector2(504f, 268f);
        insetRect.anchoredPosition = new Vector2(0f, 10f);

        Image insetImg = insetObj.AddComponent<Image>();
        if (skin != null && skin.surfaceInset != null)
        {
            insetImg.sprite = skin.surfaceInset;
            insetImg.type = Image.Type.Sliced;
            insetImg.pixelsPerUnitMultiplier = skin.insetPPU;
            insetImg.color = Color.white;
        }
        else
        {
            insetImg.color = new Color32(240, 235, 225, 255);
        }

        // A. Thông tin Loài & Địa bàn (Phần trên inset)
        string speciesName = GetSpeciesDisplayName();
        AnimalRank alphaRank = associatedIncident != null ? associatedIncident.AlphaRank : AnimalRank.Common;
        string rankTitle = GetRankDisplayName(alphaRank);
        string hostBiome = associatedIncident?.HostCluster != null ? associatedIncident.HostCluster.BiomeName : "Đất Hoang";
        int hexCount = associatedIncident?.HostCluster != null ? associatedIncident.HostCluster.HexCount : 3;

        GameObject creatureInfoObj = new GameObject("CreatureInfo");
        creatureInfoObj.transform.SetParent(insetObj.transform, false);
        RectTransform creatureInfoRect = creatureInfoObj.AddComponent<RectTransform>();
        creatureInfoRect.sizeDelta = new Vector2(464f, 66f);
        creatureInfoRect.anchoredPosition = new Vector2(0f, 82f);

        TextMeshProUGUI creatureInfoText = creatureInfoObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) creatureInfoText.font = fontAsset;
        creatureInfoText.fontSize = 15;
        creatureInfoText.lineSpacing = 10f;
        creatureInfoText.alignment = TextAlignmentOptions.Center;
        creatureInfoText.color = skin != null ? skin.forest : new Color(0.26f, 0.36f, 0.27f, 1f);
        creatureInfoText.text =
            $"<size=20><b>{speciesName}</b></size>   <size=13><color=#8C651E><b>[{rankTitle}]</b></color></size>\n" +
            $"<size=13.5><color=#455947>Địa bàn chiếm giữ: Cụm <b>{hostBiome}</b> ({hexCount} ô lục giác)</color></size>";

        // B. Hộp Cảnh Báo Phong Tỏa (Phần giữa inset)
        GameObject alertCardObj = new GameObject("AlertCard");
        alertCardObj.transform.SetParent(insetObj.transform, false);
        RectTransform alertCardRect = alertCardObj.AddComponent<RectTransform>();
        alertCardRect.sizeDelta = new Vector2(464f, 86f);
        alertCardRect.anchoredPosition = new Vector2(0f, -8f);

        Image alertCardImg = alertCardObj.AddComponent<Image>();
        alertCardImg.color = new Color(0.99f, 0.94f, 0.92f, 0.95f);
        Outline alertOutline = alertCardObj.AddComponent<Outline>();
        alertOutline.effectColor = new Color(0.88f, 0.55f, 0.50f, 0.40f);
        alertOutline.effectDistance = new Vector2(1f, -1f);

        GameObject alertTextObj = new GameObject("AlertText");
        alertTextObj.transform.SetParent(alertCardObj.transform, false);
        RectTransform alertTextRect = alertTextObj.AddComponent<RectTransform>();
        alertTextRect.anchorMin = Vector2.zero;
        alertTextRect.anchorMax = Vector2.one;
        alertTextRect.offsetMin = new Vector2(14f, 8f);
        alertTextRect.offsetMax = new Vector2(-14f, -8f);

        int minionCount = associatedIncident?.SatelliteClusters != null ? associatedIncident.SatelliteClusters.Count : 0;
        TextMeshProUGUI alertText = alertTextObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) alertText.font = fontAsset;
        alertText.fontSize = 13.5f;
        alertText.lineSpacing = 13f;
        alertText.color = new Color(0.22f, 0.22f, 0.20f, 1f);
        alertText.alignment = TextAlignmentOptions.TopLeft;
        alertText.text =
            $"<color=#B91C1C><b>⚠️ Tác động phong tỏa:</b></color>\n" +
            $"• Đóng băng <color=#B91C1C><b>100%</b></color> sản xuất tại Cụm Đầu Đàn.\n" +
            $"• Có <b>{minionCount}</b> thú con quấy rối cụm vệ tinh (-30% tốc độ).";

        // C. Lời khuyên hướng dẫn (Phần dưới inset)
        GameObject hintObj = new GameObject("HintText");
        hintObj.transform.SetParent(insetObj.transform, false);
        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.sizeDelta = new Vector2(464f, 26f);
        hintRect.anchoredPosition = new Vector2(0f, -88f);

        TextMeshProUGUI hintText = hintObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) hintText.font = fontAsset;
        hintText.text = "Hãy thuần hóa để nhận Thẻ Thú vào tay và khôi phục sản xuất của đảo!";
        hintText.fontSize = 12.5f;
        hintText.fontStyle = FontStyles.Italic;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = skin != null ? skin.muted : new Color(0.45f, 0.45f, 0.39f, 1f);

        // 5. Nút Bấm Hành Động (Bottom Actions)
        // Nút Xanh: Thuần Hóa (Lấy Thẻ)
        CreateActionButton(
            panel.transform,
            "TameButton",
            new Vector2(-126f, -180f),
            new Vector2(226f, 48f),
            skin != null ? skin.buttonPrimary : null,
            skin != null ? skin.buttonPPU : 8f,
            new Color(0.26f, 0.58f, 0.33f, 1f),
            "Thuần Hóa (Lấy Thẻ)",
            fontAsset,
            skin != null ? skin.ivory : Color.white,
            () => {
                CloseModal();
                if (WildPackIncidentManager.Instance != null && associatedIncident != null)
                {
                    WildPackIncidentManager.Instance.OnAlphaInteracted(associatedIncident);
                }
            }
        );

        // Nút Cát/Gỗ: Giữ Lại (Chỉ Ngắm)
        CreateActionButton(
            panel.transform,
            "ObserveButton",
            new Vector2(126f, -180f),
            new Vector2(226f, 48f),
            skin != null ? skin.buttonSecondary : null,
            skin != null ? skin.buttonPPU : 8f,
            new Color(0.88f, 0.85f, 0.78f, 1f),
            "Giữ Lại (Chỉ Ngắm)",
            fontAsset,
            skin != null ? skin.forest : new Color(0.26f, 0.36f, 0.27f, 1f),
            CloseModal
        );
    }

    private GameObject CreateActionButton(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sprite, float ppu, Color fallbackColor, string label, TMP_FontAsset font, Color textColor, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;

        Image img = btnObj.AddComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppu;
            img.color = Color.white;
        }
        else
        {
            img.color = fallbackColor;
        }

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.05f, 1.05f, 1.02f, 1f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.84f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(action);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.sizeDelta = size;
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 15;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;

        return btnObj;
    }

    private GameObject CreateCloseButton(Transform parent, Vector2 pos, Vector2 size, LumiWorld.Acs.ResourceInventoryUISkin skin, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;

        Image img = btnObj.AddComponent<Image>();
        if (skin != null && skin.buttonIcon != null)
        {
            img.sprite = skin.buttonIcon;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color32(235, 230, 218, 255);
            var outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color32(120, 147, 109, 180);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.05f, 1.05f, 1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.8f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(action);

        if (skin != null && skin.iconClose != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(btnObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(size.x * 0.48f, size.y * 0.48f);
            iconRect.anchoredPosition = Vector2.zero;
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = skin.iconClose;
            iconImg.color = skin.forest;
            iconImg.raycastTarget = false;
        }
        else
        {
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = size;
            textRect.anchoredPosition = Vector2.zero;
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = "X";
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = skin != null ? skin.forest : new Color(0.26f, 0.36f, 0.27f, 1f);
            tmp.raycastTarget = false;
        }

        return btnObj;
    }

    private string GetSpeciesDisplayName()
    {
        if (associatedIncident?.AlphaCreatureCard != null)
        {
            var card = associatedIncident.AlphaCreatureCard;
            if (card.animalSpeciesData != null && !string.IsNullOrEmpty(card.animalSpeciesData.speciesName))
            {
                return card.animalSpeciesData.speciesName;
            }
            if (!string.IsNullOrEmpty(card.cardName))
            {
                return card.cardName switch
                {
                    "Cow" => "Bò Sữa",
                    "Deer" => "Hươu Sao",
                    "Fox" => "Cáo Đỏ",
                    "Capybara" => "Chuột Lang Capybara",
                    "Peacock" => "Chim Công",
                    "Parrot" => "Vẹt Nam Mỹ",
                    "Dragonfly" => "Chuồn Chuồn Ngọc",
                    "Otter" => "Rái Cá",
                    _ => card.cardName
                };
            }
        }
        return "Linh Thú";
    }

    private static string GetRankDisplayName(AnimalRank rank)
    {
        return rank switch
        {
            AnimalRank.Common => "Cấp 1 • Phổ thông",
            AnimalRank.Uncommon => "Cấp 2 • Hảo hạng",
            AnimalRank.Rare => "Cấp 3 • Quý hiếm",
            AnimalRank.Epic => "Cấp 4 • Sử thi",
            AnimalRank.Legendary => "Cấp 5 • Huyền thoại",
            _ => $"Cấp {(int)rank}"
        };
    }

    public static void CloseModal()
    {
        if (activeModalCanvasObj != null)
        {
            EconomyHUD.UnregisterWindow(activeModalCanvasObj);
            EconomyHUD.MarkWindowClosed();
            Destroy(activeModalCanvasObj);
            activeModalCanvasObj = null;
        }
    }

    private void OnDestroy()
    {
        CloseModal();
    }
}

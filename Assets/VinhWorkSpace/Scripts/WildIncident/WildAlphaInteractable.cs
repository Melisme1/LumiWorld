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

        // Tạm dừng bước chân để người chơi dễ quan sát thú khi bảng thông tin mở
        var moveAI = GetComponent<AnimalMovementAI>();
        if (moveAI != null)
        {
            moveAI.InterruptWandering();
        }
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

        // 2. Khung Bảng Thông Tin Chính (Parchment Card - Kích thước lớn 720x600 chuẩn tỷ lệ màn hình)
        GameObject panel = new GameObject("PanelCard");
        panel.transform.SetParent(activeModalCanvasObj.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(850f, 680f);
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

        // 3. Header: Tiêu đề chính thanh thoát
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panel.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(700f, 44f);
        titleRect.anchoredPosition = new Vector2(0f, 275f);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) titleText.font = fontAsset;
        titleText.text = "Thú Đầu Đàn Xuất Hiện";
        titleText.fontSize = 28f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = skin != null ? skin.forest : new Color(0.24f, 0.35f, 0.25f, 1f);

        // Nút đóng tròn góc trên bên phải
        CreateCloseButton(panel.transform, new Vector2(382f, 282f), new Vector2(42f, 42f), skin, fontAsset, CloseModal);

        // 4. Khung Chứa Nội Dung (Surface Inset)
        GameObject insetObj = new GameObject("ContentInset");
        insetObj.transform.SetParent(panel.transform, false);
        RectTransform insetRect = insetObj.AddComponent<RectTransform>();
        insetRect.sizeDelta = new Vector2(770f, 440f);
        insetRect.anchoredPosition = new Vector2(0f, 15f);

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

        GameObject creatureInfoObj = new GameObject("CreatureInfo");
        creatureInfoObj.transform.SetParent(insetObj.transform, false);
        RectTransform creatureInfoRect = creatureInfoObj.AddComponent<RectTransform>();
        creatureInfoRect.sizeDelta = new Vector2(720f, 65f);
        creatureInfoRect.anchoredPosition = new Vector2(0f, 155f);

        TextMeshProUGUI creatureInfoText = creatureInfoObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) creatureInfoText.font = fontAsset;
        creatureInfoText.fontSize = 22f;
        creatureInfoText.lineSpacing = 6f;
        creatureInfoText.alignment = TextAlignmentOptions.Center;
        creatureInfoText.color = skin != null ? skin.forest : new Color(0.24f, 0.35f, 0.25f, 1f);
        creatureInfoText.text =
            $"<b>{speciesName} Đầu Đàn</b>\n" +
            $"<size=16><color=#854D0E><b>{rankTitle}</b></color>  •  <color=#4B5563>Cụm {hostBiome}</color></size>";

        // B. Hộp Cảnh Báo Phong Tỏa & Hướng Dẫn Tự Nhiên (Phần giữa inset)
        GameObject alertCardObj = new GameObject("AlertCard");
        alertCardObj.transform.SetParent(insetObj.transform, false);
        RectTransform alertCardRect = alertCardObj.AddComponent<RectTransform>();
        alertCardRect.sizeDelta = new Vector2(710f, 210f);
        alertCardRect.anchoredPosition = new Vector2(0f, -10f);

        Image alertCardImg = alertCardObj.AddComponent<Image>();
        alertCardImg.color = new Color(0.96f, 0.94f, 0.90f, 0.95f);
        Outline alertOutline = alertCardObj.AddComponent<Outline>();
        alertOutline.effectColor = new Color(0.76f, 0.70f, 0.58f, 0.85f);
        alertOutline.effectDistance = new Vector2(1f, -1f);

        // Header thẻ tác động
        GameObject alertHeaderObj = new GameObject("AlertHeader");
        alertHeaderObj.transform.SetParent(alertCardObj.transform, false);
        RectTransform alertHeaderRect = alertHeaderObj.AddComponent<RectTransform>();
        alertHeaderRect.sizeDelta = new Vector2(650f, 28f);
        alertHeaderRect.anchoredPosition = new Vector2(0f, 68f);
        TextMeshProUGUI alertHeaderText = alertHeaderObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) alertHeaderText.font = fontAsset;
        alertHeaderText.text = "Khu Vực Bị Phong Tỏa";
        alertHeaderText.fontSize = 17.5f;
        alertHeaderText.fontStyle = FontStyles.Bold;
        alertHeaderText.alignment = TextAlignmentOptions.TopLeft;
        alertHeaderText.color = new Color(0.75f, 0.22f, 0.12f, 1f);

        // Nội dung chi tiết - viết tự nhiên, không ký hiệu thừa
        GameObject alertTextObj = new GameObject("AlertBody");
        alertTextObj.transform.SetParent(alertCardObj.transform, false);
        RectTransform alertTextRect = alertTextObj.AddComponent<RectTransform>();
        alertTextRect.sizeDelta = new Vector2(650f, 130f);
        alertTextRect.anchoredPosition = new Vector2(0f, -18f);

        TextMeshProUGUI alertText = alertTextObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) alertText.font = fontAsset;
        alertText.fontSize = 16f;
        alertText.lineSpacing = 6f;
        alertText.color = new Color(0.20f, 0.24f, 0.22f, 1f);
        alertText.alignment = TextAlignmentOptions.TopLeft;
        alertText.text =
            $"Đàn thú đang chiếm giữ cụm <b>{hostBiome}</b>, khiến các ô đất tạm ngừng sinh tài nguyên.\n\n" +
            $"Hãy thuần hóa để giải phóng hòn đảo và nhận thẻ thú vào bộ bài!";

        // 5. Nút Bấm Hành Động (Bottom Actions - 2 nút lớn 310x56px cân đối, sang trọng)
        // Nút Xanh: Thuần Hóa
        CreateActionButton(
            panel.transform,
            "TameButton",
            new Vector2(-175f, -255f),
            new Vector2(310f, 56f),
            skin != null ? skin.buttonPrimary : null,
            skin != null ? skin.buttonPPU : 8f,
            new Color(0.24f, 0.52f, 0.30f, 1f),
            "Thuần Hóa",
            fontAsset,
            skin != null ? skin.ivory : Color.white,
            () => {
                CloseModal();
                if (associatedIncident != null)
                {
                    var context = new MinigameContext
                    {
                        SpeciesName = GetSpeciesDisplayName(),
                        Rank = associatedIncident.AlphaRank,
                        BiomeName = associatedIncident.HostCluster != null ? associatedIncident.HostCluster.BiomeName : "Đất Hoang",
                        CreatureCard = associatedIncident.AlphaCreatureCard,
                        Incident = associatedIncident,
                        TimeLimitSeconds = 24f
                    };

                    MinigameManager.Instance.StartMinigame(MinigameType.RhythmResonance, context, (result) =>
                    {
                        if (result != null && result.IsSuccess)
                        {
                            if (WildPackIncidentManager.Instance != null && associatedIncident != null)
                            {
                                WildPackIncidentManager.Instance.OnAlphaInteracted(associatedIncident);
                            }
                        }
                    });
                }
            }
        );

        // Nút Cát/Gỗ: Để Lại
        CreateActionButton(
            panel.transform,
            "ObserveButton",
            new Vector2(175f, -255f),
            new Vector2(310f, 56f),
            skin != null ? skin.buttonSecondary : null,
            skin != null ? skin.buttonPPU : 8f,
            new Color(0.88f, 0.85f, 0.78f, 1f),
            "Để Lại",
            fontAsset,
            skin != null ? skin.forest : new Color(0.24f, 0.35f, 0.25f, 1f),
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
        tmp.fontSize = 18;
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
            AnimalRank.Common => "Cấp 1 - Phổ thông",
            AnimalRank.Uncommon => "Cấp 2 - Hảo hạng",
            AnimalRank.Rare => "Cấp 3 - Quý hiếm",
            AnimalRank.Epic => "Cấp 4 - Sử thi",
            AnimalRank.Legendary => "Cấp 5 - Huyền thoại",
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

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// Huy hiệu hình thoi 3D nổi trên cụm Habitat chuẩn phong cách Preserve.
/// Căn chỉnh hình học đối xứng chính diện hoàn hảo:
/// 1. Cánh 9h: Icon môi trường phóng lớn, căn chính giữa tâm hình thoi nhỏ (-28.5, 0), không viền đen.
/// 2. Tâm 0h (Chính giữa): Số lượng ô liền nhau về môi trường đó (HexCount), màu trùng với màu icon môi trường.
/// 3. Cánh 12h, 3h, 6h: Các ô cư dân và ổ khóa theo cột mốc 2 - 4 - 6 ô môi trường:
///    - Cột mốc 2 ô (12h): Mở khóa slot thú 1. Khi chưa đủ ô: hiện ổ khóa ghi số 2. Khi đã thả thú: hiện avatar thú hình thoi.
///    - Cột mốc 4 ô (3h): Mở khóa slot thú 2. Khi chưa đủ ô: hiện ổ khóa ghi số 4. Khi đã thả thú: hiện avatar thú hình thoi.
///    - Cột mốc 6 ô (6h): Mở khóa slot thú 3. Khi chưa đủ ô: hiện ổ khóa ghi số 6. Khi đã thả thú: hiện avatar thú hình thoi.
/// 4. Thanh phụ dưới đáy: Đồng bộ chính xác số ô tài nguyên theo GDD (0/3, 1/3, 2/3, 3/3).
/// 5. Độ cao nổi: 2.6m vừa vặn trên mặt đất.
/// </summary>
public class HabitatDiamondBadge : MonoBehaviour
{
    private HabitatState habitatState;
    private Camera targetCamera;

    private Canvas badgeCanvas;
    private RectTransform rootRect;
    private GameObject visualContainer;
    private CanvasGroup canvasGroup;

    private Image diamondBgImage;
    private Image biomeIconImage;
    private TextMeshProUGUI hexCountText;

    // Slot 1 (12h) - Mốc 2 ô
    private Image topResidentIcon;
    private GameObject topLockContainer;
    private TextMeshProUGUI topLockText;

    // Slot 2 (3h) - Mốc 4 ô
    private Image rightResidentIcon;
    private GameObject rightLockContainer;
    private TextMeshProUGUI rightLockText;

    // Slot 3 (6h) - Mốc 6 ô
    private Image bottomResidentIcon;
    private GameObject bottomLockContainer;
    private TextMeshProUGUI bottomLockText;

    private GameObject resourcePillObj;
    private TextMeshProUGUI resourceSlotText;

    private Vector3 worldCenterPosition;
    private float floatHeight = 2.6f;
    private bool isExplicitlyVisible = false;

    private static readonly Dictionary<string, Sprite> creatureSpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    public HabitatState CurrentHabitat => habitatState;
    public bool IsVisible => isExplicitlyVisible;

    public void Initialize(HabitatState state, Camera cam, float height = 2.6f)
    {
        this.habitatState = state;
        this.targetCamera = cam != null ? cam : Camera.main;
        this.floatHeight = height;

        BuildDiamondUI();
        RefreshData();
        SetVisible(false);
    }

    public void UpdateHabitat(HabitatState newState)
    {
        if (newState != null)
        {
            this.habitatState = newState;
        }
        RefreshData();
    }

    private void BuildDiamondUI()
    {
        badgeCanvas = gameObject.AddComponent<Canvas>();
        badgeCanvas.renderMode = RenderMode.WorldSpace;
        badgeCanvas.worldCamera = targetCamera;
        badgeCanvas.overrideSorting = true;
        badgeCanvas.sortingOrder = 60;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        gameObject.AddComponent<GraphicRaycaster>();

        rootRect = GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(110f, 110f);
        rootRect.localScale = Vector3.one * 0.018f;

        visualContainer = new GameObject("VisualContainer", typeof(RectTransform), typeof(CanvasGroup));
        visualContainer.transform.SetParent(transform, false);
        RectTransform vcRect = visualContainer.GetComponent<RectTransform>();
        vcRect.sizeDelta = new Vector2(110f, 110f);
        vcRect.anchoredPosition = Vector2.zero;
        canvasGroup = visualContainer.GetComponent<CanvasGroup>();

        // 1. Nền hình thoi theo từng hệ môi trường
        GameObject bgObj = new GameObject("DiamondTemplateBG", typeof(RectTransform), typeof(Image), typeof(Button));
        bgObj.transform.SetParent(visualContainer.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(110f, 110f);
        bgRect.anchoredPosition = Vector2.zero;

        diamondBgImage = bgObj.GetComponent<Image>();
        diamondBgImage.sprite = PreserveDiamondStyle.Image2Template;
        diamondBgImage.color = Color.white;

        Button clickButton = bgObj.GetComponent<Button>();
        clickButton.targetGraphic = diamondBgImage;
        clickButton.onClick.AddListener(OnBadgeClicked);

        // --- CÁNH 9H (HƯỚNG TRÁI): Icon Môi trường phóng lớn chính giữa (-28.5, 0), KHÔNG VIỀN ĐEN ---
        GameObject biomeObj = new GameObject("BiomeIcon", typeof(RectTransform), typeof(Image));
        biomeObj.transform.SetParent(visualContainer.transform, false);
        RectTransform biomeRect = biomeObj.GetComponent<RectTransform>();
        biomeRect.pivot = new Vector2(0.5f, 0.5f);
        biomeRect.anchoredPosition = new Vector2(-28.5f, 0f);
        biomeRect.sizeDelta = new Vector2(28f, 28f);

        biomeIconImage = biomeObj.GetComponent<Image>();
        biomeIconImage.raycastTarget = false;

        // --- TÂM CHÍNH GIỮA (0, 0): Số lượng ô môi trường liền nhau (Hex Count) ---
        GameObject countObj = new GameObject("HexCountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        countObj.transform.SetParent(visualContainer.transform, false);
        RectTransform countRect = countObj.GetComponent<RectTransform>();
        countRect.pivot = new Vector2(0.5f, 0.5f);
        countRect.anchoredPosition = Vector2.zero;
        countRect.sizeDelta = new Vector2(40f, 40f);

        hexCountText = countObj.GetComponent<TextMeshProUGUI>();
        hexCountText.alignment = TextAlignmentOptions.Center;
        hexCountText.verticalAlignment = VerticalAlignmentOptions.Middle;
        hexCountText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        hexCountText.margin = Vector4.zero;
        hexCountText.fontSize = 28f;
        hexCountText.fontStyle = FontStyles.Bold;
        hexCountText.raycastTarget = false;

        // --- CÁNH 12H (HƯỚNG TRÊN): Slot cư dân 1 (Mốc 2 ô) ---
        GameObject topSlotObj = new GameObject("Slot_12h", typeof(RectTransform));
        topSlotObj.transform.SetParent(visualContainer.transform, false);
        RectTransform topSlotRect = topSlotObj.GetComponent<RectTransform>();
        topSlotRect.pivot = new Vector2(0.5f, 0.5f);
        topSlotRect.anchoredPosition = new Vector2(0f, 24.0f);
        topSlotRect.sizeDelta = new Vector2(43.5f, 43.5f);

        // Ảnh thẻ thú 1 (Avatar hình thoi lấp đầy hoàn toàn khung ô)
        GameObject topIconObj = new GameObject("TopResidentIcon", typeof(RectTransform), typeof(Image));
        topIconObj.transform.SetParent(topSlotObj.transform, false);
        RectTransform topIconRect = topIconObj.GetComponent<RectTransform>();
        topIconRect.pivot = new Vector2(0.5f, 0.5f);
        topIconRect.anchoredPosition = Vector2.zero;
        topIconRect.sizeDelta = new Vector2(43.5f, 43.5f);

        topResidentIcon = topIconObj.GetComponent<Image>();
        topResidentIcon.raycastTarget = false;
        topResidentIcon.enabled = false;

        // Ổ khóa slot 1 (Mốc 2)
        topLockContainer = new GameObject("TopLockContainer", typeof(RectTransform));
        topLockContainer.transform.SetParent(topSlotObj.transform, false);
        RectTransform tlcRect = topLockContainer.GetComponent<RectTransform>();
        tlcRect.pivot = new Vector2(0.5f, 0.5f);
        tlcRect.anchoredPosition = Vector2.zero;
        tlcRect.sizeDelta = new Vector2(43.5f, 43.5f);

        GameObject topLockImgObj = new GameObject("TopLockImage", typeof(RectTransform), typeof(Image));
        topLockImgObj.transform.SetParent(topLockContainer.transform, false);
        RectTransform tliRect = topLockImgObj.GetComponent<RectTransform>();
        tliRect.anchoredPosition = new Vector2(0f, 2.0f);
        tliRect.sizeDelta = new Vector2(20.5f, 24f);
        Image topLockImg = topLockImgObj.GetComponent<Image>();
        topLockImg.sprite = PreserveDiamondStyle.LockWhiteSprite;
        topLockImg.raycastTarget = false;

        GameObject topLockTxtObj = new GameObject("TopLockText", typeof(RectTransform), typeof(TextMeshProUGUI));
        topLockTxtObj.transform.SetParent(topLockContainer.transform, false);
        RectTransform tltRect = topLockTxtObj.GetComponent<RectTransform>();
        tltRect.anchoredPosition = new Vector2(0f, -3.3f);
        tltRect.sizeDelta = new Vector2(16f, 12f);
        topLockText = topLockTxtObj.GetComponent<TextMeshProUGUI>();
        topLockText.alignment = TextAlignmentOptions.Center;
        topLockText.verticalAlignment = VerticalAlignmentOptions.Middle;
        topLockText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        topLockText.margin = Vector4.zero;
        topLockText.fontSize = 11.5f;
        topLockText.fontStyle = FontStyles.Bold;
        topLockText.color = new Color(0.16f, 0.35f, 0.12f, 1f);
        topLockText.text = "2";
        topLockText.raycastTarget = false;

        // --- CÁNH 3H (HƯỚNG PHẢI): Slot cư dân 2 (Mốc 4 ô) ---
        GameObject rightSlotObj = new GameObject("Slot_3h", typeof(RectTransform));
        rightSlotObj.transform.SetParent(visualContainer.transform, false);
        RectTransform rightSlotRect = rightSlotObj.GetComponent<RectTransform>();
        rightSlotRect.pivot = new Vector2(0.5f, 0.5f);
        rightSlotRect.anchoredPosition = new Vector2(24.5f, 0f);
        rightSlotRect.sizeDelta = new Vector2(44.5f, 44.5f);

        // Ảnh thẻ thú 2 (Avatar hình thoi lấp đầy hoàn toàn khung ô)
        GameObject rightIconObj = new GameObject("RightResidentIcon", typeof(RectTransform), typeof(Image));
        rightIconObj.transform.SetParent(rightSlotObj.transform, false);
        RectTransform rightIconRect = rightIconObj.GetComponent<RectTransform>();
        rightIconRect.pivot = new Vector2(0.5f, 0.5f);
        rightIconRect.anchoredPosition = Vector2.zero;
        rightIconRect.sizeDelta = new Vector2(44.5f, 44.5f);

        rightResidentIcon = rightIconObj.GetComponent<Image>();
        rightResidentIcon.raycastTarget = false;
        rightResidentIcon.enabled = false;

        // Ổ khóa slot 2 (Mốc 4)
        rightLockContainer = new GameObject("RightLockContainer", typeof(RectTransform));
        rightLockContainer.transform.SetParent(rightSlotObj.transform, false);
        RectTransform rlcRect = rightLockContainer.GetComponent<RectTransform>();
        rlcRect.pivot = new Vector2(0.5f, 0.5f);
        rlcRect.anchoredPosition = Vector2.zero;
        rlcRect.sizeDelta = new Vector2(44.5f, 44.5f);

        GameObject rightLockImgObj = new GameObject("RightLockImage", typeof(RectTransform), typeof(Image));
        rightLockImgObj.transform.SetParent(rightLockContainer.transform, false);
        RectTransform rliRect = rightLockImgObj.GetComponent<RectTransform>();
        rliRect.anchoredPosition = new Vector2(0f, 2.0f);
        rliRect.sizeDelta = new Vector2(20.5f, 24f);
        Image rightLockImg = rightLockImgObj.GetComponent<Image>();
        rightLockImg.sprite = PreserveDiamondStyle.LockWhiteSprite;
        rightLockImg.raycastTarget = false;

        GameObject rightLockTxtObj = new GameObject("RightLockText", typeof(RectTransform), typeof(TextMeshProUGUI));
        rightLockTxtObj.transform.SetParent(rightLockContainer.transform, false);
        RectTransform rltRect = rightLockTxtObj.GetComponent<RectTransform>();
        rltRect.anchoredPosition = new Vector2(0f, -3.3f); // Nằm chính giữa tâm thân ổ khóa
        rltRect.sizeDelta = new Vector2(16f, 12f);
        rightLockText = rightLockTxtObj.GetComponent<TextMeshProUGUI>();
        rightLockText.alignment = TextAlignmentOptions.Center;
        rightLockText.verticalAlignment = VerticalAlignmentOptions.Middle;
        rightLockText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        rightLockText.margin = Vector4.zero;
        rightLockText.fontSize = 11.5f;
        rightLockText.fontStyle = FontStyles.Bold;
        rightLockText.color = new Color(0.16f, 0.35f, 0.12f, 1f);
        rightLockText.text = "4";
        rightLockText.raycastTarget = false;

        // --- CÁNH 6H (HƯỚNG DƯỚI): Slot cư dân 3 (Mốc 6 ô) ---
        // Ảnh gốc đã có sẵn hình ổ khóa trắng, KHÔNG cần spawn thêm Image ổ khóa để tránh đè lệch
        GameObject bottomSlotObj = new GameObject("Slot_6h", typeof(RectTransform));
        bottomSlotObj.transform.SetParent(visualContainer.transform, false);
        RectTransform bottomSlotRect = bottomSlotObj.GetComponent<RectTransform>();
        bottomSlotRect.pivot = new Vector2(0.5f, 0.5f);
        bottomSlotRect.anchoredPosition = new Vector2(0f, -24.0f);
        bottomSlotRect.sizeDelta = new Vector2(43.5f, 43.5f);

        // Ảnh thẻ thú 3 (khi đủ 6 ô và đã thả thú 3 vào -> che hoàn toàn ổ khóa, lấp đầy ô)
        GameObject bottomIconObj = new GameObject("BottomResidentIcon", typeof(RectTransform), typeof(Image));
        bottomIconObj.transform.SetParent(bottomSlotObj.transform, false);
        RectTransform bottomIconRect = bottomIconObj.GetComponent<RectTransform>();
        bottomIconRect.pivot = new Vector2(0.5f, 0.5f);
        bottomIconRect.anchoredPosition = Vector2.zero;
        bottomIconRect.sizeDelta = new Vector2(43.5f, 43.5f);

        bottomResidentIcon = bottomIconObj.GetComponent<Image>();
        bottomResidentIcon.raycastTarget = false;
        bottomResidentIcon.enabled = false;

        // Chức năng khóa slot 3: Số 6 nằm chính giữa thân ổ khóa có sẵn trên nền
        bottomLockContainer = new GameObject("BottomLockContainer", typeof(RectTransform));
        bottomLockContainer.transform.SetParent(bottomSlotObj.transform, false);
        RectTransform blcRect = bottomLockContainer.GetComponent<RectTransform>();
        blcRect.pivot = new Vector2(0.5f, 0.5f);
        blcRect.anchoredPosition = Vector2.zero;
        blcRect.sizeDelta = new Vector2(43.5f, 43.5f);

        GameObject bottomLockTxtObj = new GameObject("BottomLockText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bottomLockTxtObj.transform.SetParent(bottomLockContainer.transform, false);
        RectTransform bltRect = bottomLockTxtObj.GetComponent<RectTransform>();
        bltRect.anchoredPosition = new Vector2(0f, 0.5f); // Tâm thân ổ khóa 6h trên nền gốc
        bltRect.sizeDelta = new Vector2(18f, 14f);
        bottomLockText = bottomLockTxtObj.GetComponent<TextMeshProUGUI>();
        bottomLockText.alignment = TextAlignmentOptions.Center;
        bottomLockText.verticalAlignment = VerticalAlignmentOptions.Middle;
        bottomLockText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        bottomLockText.margin = Vector4.zero;
        bottomLockText.fontSize = 10.5f;
        bottomLockText.fontStyle = FontStyles.Bold;
        bottomLockText.color = new Color(0.16f, 0.35f, 0.12f, 1f);
        bottomLockText.text = "6";
        bottomLockText.raycastTarget = false;

        // --- THANH PHỤ DƯỚI ĐÁY: Hiển thị số ô tài nguyên theo GDD ---
        resourcePillObj = new GameObject("ResourceSlotPill", typeof(RectTransform), typeof(Image));
        resourcePillObj.transform.SetParent(visualContainer.transform, false);
        RectTransform pillRect = resourcePillObj.GetComponent<RectTransform>();
        pillRect.pivot = new Vector2(0.5f, 0.5f);
        pillRect.anchoredPosition = new Vector2(0f, -54f);
        pillRect.sizeDelta = new Vector2(108f, 18f);

        Image pillBg = resourcePillObj.GetComponent<Image>();
        pillBg.color = new Color(0.10f, 0.16f, 0.12f, 0.92f);
        pillBg.raycastTarget = false;

        GameObject pillTextObj = new GameObject("PillText", typeof(RectTransform), typeof(TextMeshProUGUI));
        pillTextObj.transform.SetParent(resourcePillObj.transform, false);
        RectTransform ptRect = pillTextObj.GetComponent<RectTransform>();
        ptRect.anchoredPosition = Vector2.zero;
        ptRect.sizeDelta = new Vector2(106f, 16f);

        resourceSlotText = pillTextObj.GetComponent<TextMeshProUGUI>();
        resourceSlotText.alignment = TextAlignmentOptions.Center;
        resourceSlotText.verticalAlignment = VerticalAlignmentOptions.Middle;
        resourceSlotText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        resourceSlotText.margin = Vector4.zero;
        resourceSlotText.enableWordWrapping = false;
        resourceSlotText.overflowMode = TextOverflowModes.Ellipsis;
        resourceSlotText.fontSize = 9.0f;
        resourceSlotText.fontStyle = FontStyles.Bold;
        resourceSlotText.color = Color.white;
        resourceSlotText.raycastTarget = false;

        // Đảm bảo Text hiển thị số ô (HexCountText) luôn nằm trên cùng của visualContainer
        if (countObj != null)
        {
            countObj.transform.SetAsLastSibling();
        }
    }

    public void SetVisible(bool visible)
    {
        isExplicitlyVisible = visible;
        if (visualContainer != null)
        {
            visualContainer.SetActive(visible);
        }
    }

    public void RefreshData()
    {
        // Luôn đồng bộ với HabitatState mới nhất từ HabitatRuntimeManager
        if (habitatState != null && HabitatRuntimeManager.TryGetActive(out var hrm) && hrm.Habitats != null)
        {
            foreach (var h in hrm.Habitats)
            {
                if (h != null && h.habitatId == habitatState.habitatId)
                {
                    habitatState = h;
                    break;
                }
            }
        }

        if (habitatState == null) return;

        // 1. Tính toán tâm không gian 3D trên đỉnh các khối hex và độ cao vừa vặn
        CalculateWorldCenter();
        transform.position = worldCenterPosition + Vector3.up * floatHeight;

        int hexCount = habitatState.HexCount;
        Color affinityColor = GetAffinityColor(habitatState.affinity);

        // 2. Nền huy hiệu tương ứng hệ môi trường từ 7 file gốc đã cắt sạch nền caro
        if (diamondBgImage != null)
        {
            diamondBgImage.sprite = PreserveDiamondStyle.GetHabitatBadgeSprite(habitatState.affinity);
            diamondBgImage.color = Color.white;
        }

        // 3. Icon Môi trường ở hướng 9h (-28.5, 0), màu theo hệ, không viền đen
        if (biomeIconImage != null)
        {
            biomeIconImage.sprite = GetBiomeSprite(habitatState.affinity);
            biomeIconImage.color = affinityColor;
        }

        // 4. Số lượng ô liền nhau về môi trường đó (Tâm chính giữa, màu trùng với icon môi trường)
        if (hexCountText != null)
        {
            hexCountText.text = hexCount.ToString();
            hexCountText.color = affinityColor;
        }

        // 5. Quét danh sách cư dân (Thú đã thả vào cụm)
        List<Sprite> creatureSprites = GetResidentCreatureSprites();

        // --- SLOT 1 (12h): Cột mốc 2 ô ---
        bool slot1Unlocked = hexCount >= 2;
        Sprite resident1Sprite = creatureSprites.Count > 0 ? creatureSprites[0] : null;

        if (resident1Sprite != null)
        {
            topResidentIcon.enabled = true;
            topResidentIcon.sprite = PreserveDiamondStyle.GetDiamondAvatar(resident1Sprite, HabitatSlotCorner.Bottom);
            topResidentIcon.color = Color.white;
            topLockContainer.SetActive(false);
        }
        else if (!slot1Unlocked)
        {
            topResidentIcon.enabled = false;
            topLockContainer.SetActive(true);
            topLockText.text = "2";
        }
        else
        {
            topResidentIcon.enabled = false;
            topLockContainer.SetActive(false);
        }

        // --- SLOT 2 (3h): Cột mốc 4 ô ---
        bool slot2Unlocked = hexCount >= 4;
        Sprite resident2Sprite = creatureSprites.Count > 1 ? creatureSprites[1] : null;

        if (resident2Sprite != null)
        {
            rightResidentIcon.enabled = true;
            rightResidentIcon.sprite = PreserveDiamondStyle.GetDiamondAvatar(resident2Sprite, HabitatSlotCorner.Left);
            rightResidentIcon.color = Color.white;
            rightLockContainer.SetActive(false);
        }
        else if (!slot2Unlocked)
        {
            rightResidentIcon.enabled = false;
            rightLockContainer.SetActive(true);
            rightLockText.text = "4";
        }
        else
        {
            rightResidentIcon.enabled = false;
            rightLockContainer.SetActive(false);
        }

        // --- SLOT 3 (6h): Cột mốc 6 ô ---
        bool slot3Unlocked = hexCount >= 6;
        Sprite resident3Sprite = creatureSprites.Count > 2 ? creatureSprites[2] : null;

        if (resident3Sprite != null)
        {
            bottomResidentIcon.enabled = true;
            bottomResidentIcon.sprite = PreserveDiamondStyle.GetDiamondAvatar(resident3Sprite, HabitatSlotCorner.Top);
            bottomResidentIcon.color = Color.white;
            bottomLockContainer.SetActive(false);
        }
        else if (!slot3Unlocked)
        {
            bottomResidentIcon.enabled = false;
            bottomLockContainer.SetActive(true);
            bottomLockText.text = "6";
        }
        else
        {
            bottomResidentIcon.enabled = false;
            bottomLockContainer.SetActive(false);
        }

        // 6. Số ô tài nguyên theo quy tắc GDD (1-2 hex: 0/3, 3 hex: 1/3, 4-5 hex: 2/3, 6+ hex: 3/3)
        if (resourceSlotText != null)
        {
            if (hexCount < 3)
            {
                resourceSlotText.text = "<color=#94A3B8>0/3 Resource Slots</color>";
            }
            else if (hexCount == 3)
            {
                resourceSlotText.text = "<color=#86EFAC>1/3 Resource Slots (T1)</color>";
            }
            else if (hexCount >= 4 && hexCount < 6)
            {
                resourceSlotText.text = "<color=#86EFAC>2/3 Resource Slots (2 T1)</color>";
            }
            else
            {
                resourceSlotText.text = "<color=#FDE047>3/3 Resource Slots (Full)</color>";
            }
        }
    }

    /// <summary>
    /// Tìm kiếm toàn diện các thẻ bài thú đang sinh sống trên cụm Habitat này
    /// </summary>
    private List<Sprite> GetResidentCreatureSprites()
    {
        var sprites = new List<Sprite>();
        if (habitatState == null) return sprites;

        // 1. Kiểm tra từ danh sách cư dân habitatState.residents
        if (habitatState.residents != null)
        {
            foreach (var res in habitatState.residents)
            {
                if (res == null) continue;
                Sprite sp = GetCreatureSprite(res.speciesId);
                if (sp != null)
                {
                    sprites.Add(sp);
                }
            }
        }

        // 2. Quét trực tiếp các thẻ PlacedCard hoặc Animal trên các ô đất của cụm
        var worldGen = FindAnyObjectByType<HexWorldGenerator>();
        if (worldGen != null && worldGen.MapTiles != null && habitatState.members != null)
        {
            foreach (var member in habitatState.members)
            {
                if (member == null) continue;
                if (!worldGen.MapTiles.TryGetValue(member.hex, out var tileObj) || tileObj == null) continue;

                var placedCards = tileObj.GetComponentsInChildren<PlacedCard>();
                foreach (var pc in placedCards)
                {
                    if (pc != null && pc.CardType == CardType.Creature && pc.cardData != null && pc.cardData.cardImage != null)
                    {
                        if (!sprites.Contains(pc.cardData.cardImage))
                        {
                            sprites.Add(pc.cardData.cardImage);
                        }
                    }
                }

                var animals = tileObj.GetComponentsInChildren<AnimalIndividual>();
                foreach (var animal in animals)
                {
                    if (animal != null && animal.SpeciesData != null)
                    {
                        Sprite sp = GetCreatureSprite(animal.SpeciesData.speciesID);
                        if (sp != null && !sprites.Contains(sp))
                        {
                            sprites.Add(sp);
                        }
                    }
                }
            }
        }

        return sprites;
    }

    private void CalculateWorldCenter()
    {
        if (habitatState == null || habitatState.members == null || habitatState.members.Count == 0) return;

        float maxSurfaceY = 0f;
        Vector3 sumPos = Vector3.zero;
        int validCount = 0;

        var worldGen = FindAnyObjectByType<HexWorldGenerator>();

        foreach (var member in habitatState.members)
        {
            if (member == null) continue;
            Vector3 worldPos = HexMetrics.HexToWorldPosition(member.hex, 0f);
            float tileSurfaceY = 0f;

            if (worldGen != null && worldGen.MapTiles != null && worldGen.MapTiles.TryGetValue(member.hex, out var tileObj) && tileObj != null)
            {
                worldPos = tileObj.transform.position;
                var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tileObj);
                tileSurfaceY = Mathf.Max(topY, worldPos.y);
            }

            maxSurfaceY = Mathf.Max(maxSurfaceY, tileSurfaceY);
            sumPos += new Vector3(worldPos.x, 0f, worldPos.z);
            validCount++;
        }

        if (validCount > 0)
        {
            worldCenterPosition = new Vector3(sumPos.x / validCount, maxSurfaceY, sumPos.z / validCount);
        }
    }

    private void LateUpdate()
    {
        if (!isExplicitlyVisible) return;
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        // Xoay mặt về Camera mượt mà chính diện (Billboard)
        transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position);
    }

    private void OnBadgeClicked()
    {
        if (HabitatCreaturePanelUI.Instance != null && habitatState != null)
        {
            HabitatCreaturePanelUI.Instance.OpenForHabitat(habitatState);
        }

        var uiController = FindAnyObjectByType<ResourceInventoryUIController>();
        if (uiController != null && habitatState != null)
        {
            var method = typeof(ResourceInventoryUIController).GetMethod("SelectHabitat",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (method != null)
            {
                method.Invoke(uiController, new object[] { habitatState.habitatId });
            }
        }
    }

    private Sprite GetBiomeSprite(BiomeAffinity affinity)
    {
        return affinity switch
        {
            BiomeAffinity.Leafwood => PreserveDiamondStyle.PineTreeSprite,
            BiomeAffinity.Bloomfield => PreserveDiamondStyle.FlowerBiomeSprite,
            BiomeAffinity.Windheath => PreserveDiamondStyle.MountainBiomeSprite,
            BiomeAffinity.Reedmarsh => PreserveDiamondStyle.WaterBiomeSprite,
            BiomeAffinity.Lilywater => PreserveDiamondStyle.WaterBiomeSprite,
            BiomeAffinity.Streamstone => PreserveDiamondStyle.MountainBiomeSprite,
            BiomeAffinity.Rainleaf => PreserveDiamondStyle.PineTreeSprite,
            _ => PreserveDiamondStyle.PineTreeSprite
        };
    }

    private Color GetAffinityColor(BiomeAffinity affinity)
    {
        return affinity switch
        {
            BiomeAffinity.Leafwood => new Color(0.24f, 0.52f, 0.18f, 1f),      // Rừng xanh lá
            BiomeAffinity.Bloomfield => new Color(0.82f, 0.35f, 0.62f, 1f),    // Thung lũng hoa hồng tím
            BiomeAffinity.Windheath => new Color(0.84f, 0.58f, 0.22f, 1f),     // Núi cao màu hổ phách vàng
            BiomeAffinity.Reedmarsh => new Color(0.18f, 0.55f, 0.78f, 1f),     // Sóng nước xanh ngọc
            BiomeAffinity.Lilywater => new Color(0.15f, 0.65f, 0.75f, 1f),     // Nước cạn xanh ngọc biển
            BiomeAffinity.Streamstone => new Color(0.35f, 0.55f, 0.58f, 1f),   // Đá suối xám xanh
            BiomeAffinity.Rainleaf => new Color(0.20f, 0.68f, 0.45f, 1f),      // Lá mưa xanh mướt
            _ => new Color(0.24f, 0.52f, 0.18f, 1f)
        };
    }

    private Sprite GetCreatureSprite(string speciesId)
    {
        if (string.IsNullOrEmpty(speciesId)) return null;
        if (creatureSpriteCache.TryGetValue(speciesId, out var cached) && cached != null) return cached;

        // 1. Tìm thông qua SpeciesMappingRegistry của Acs
        if (HabitatRuntimeManager.TryGetActive(out var hrm) && hrm.Catalog != null && hrm.Catalog.speciesRegistry != null)
        {
            var registry = hrm.Catalog.speciesRegistry;
            if (registry.TryGetActiveBySpeciesId(speciesId, out var entry) && entry != null && entry.creatureCard != null && entry.creatureCard.cardImage != null)
            {
                creatureSpriteCache[speciesId] = entry.creatureCard.cardImage;
                return entry.creatureCard.cardImage;
            }
            if (registry.TryGetByGddKey(speciesId, out var gddEntry) && gddEntry != null && gddEntry.creatureCard != null && gddEntry.creatureCard.cardImage != null)
            {
                creatureSpriteCache[speciesId] = gddEntry.creatureCard.cardImage;
                return gddEntry.creatureCard.cardImage;
            }
            if (registry.entries != null)
            {
                foreach (var e in registry.entries)
                {
                    if (e == null) continue;
                    if (string.Equals(e.SpeciesId, speciesId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.gddKey, speciesId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.displayName, speciesId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (e.creatureCard != null && e.creatureCard.cardImage != null)
                        {
                            creatureSpriteCache[speciesId] = e.creatureCard.cardImage;
                            return e.creatureCard.cardImage;
                        }
                    }
                }
            }
        }

        // 2. Tìm qua các CardData trong Resources / Memory
        CardData[] allCards = Resources.FindObjectsOfTypeAll<CardData>();
        foreach (var card in allCards)
        {
            if (card == null || card.cardImage == null) continue;
            if (string.Equals(card.cardID, speciesId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(card.cardName, speciesId, StringComparison.OrdinalIgnoreCase) ||
                (card.animalSpeciesData != null && (
                    string.Equals(card.animalSpeciesData.speciesID, speciesId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(card.animalSpeciesData.speciesName, speciesId, StringComparison.OrdinalIgnoreCase))))
            {
                creatureSpriteCache[speciesId] = card.cardImage;
                return card.cardImage;
            }
        }

        return null;
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Đại diện cho một ô nốt mũi tên kẹo thạch 3D trên khay hiển thị chuỗi
/// </summary>
public class NoteSlotView
{
    public GameObject RootObj;
    public Image Background;
    public Outline Outline;
    public Image ArrowImg;
    public RhythmDirection Direction;
    public Color BaseColor;
}

/// <summary>
/// Đại diện cho ô phím SPACE đá ấm áp ở cuối chuỗi mũi tên
/// </summary>
public class SpaceSlotView
{
    public GameObject RootObj;
    public Image Background;
    public Outline Outline;
    public TextMeshProUGUI MainLabel;
}

/// <summary>
/// Bộ chứa toàn bộ tham chiếu UI components được sinh ra cho Minigame Vũ Điệu Hòa Nhịp
/// </summary>
public class RhythmUIBindings
{
    public RectTransform PanelRect;
    public Image DimOverlayImg;

    public Image HarmonyFillImg;
    public RectTransform HarmonyFillRect;
    public TextMeshProUGUI HarmonyText;

    public RectTransform NoteContainer;

    public RectTransform RailRect;
    public RectTransform OrbRect;
    public Image OrbImg;
    public RectTransform HitZoneRect;
    public Image HitZoneImg;
    public RectTransform ApproachRingRect;
    public Image ApproachRingImg;

    public TextMeshProUGUI ComboText;
    public TextMeshProUGUI FeedbackGradeText;
    public TextMeshProUGUI TimerText;

    public Image BeatButtonImg;
    public TextMeshProUGUI BeatButtonText;
    public Button BeatButton;
}

/// <summary>
/// Builder chịu trách nhiệm dựng toàn bộ cây giao diện người dùng (Canvas/RectTransform/Widgets)
/// cho Minigame Vũ Điệu Hòa Nhịp, tách hoàn toàn việc tạo visual hierarchy khỏi logic vận hành.
/// </summary>
public static class RhythmMinigameUIBuilder
{
    public static RhythmUIBindings BuildInterface(
        Transform root,
        MinigameContext context,
        TMP_FontAsset fontAsset,
        UnityEngine.Events.UnityAction onCancel,
        UnityEngine.Events.UnityAction onBeatClick)
    {
        var bindings = new RhythmUIBindings();

        // 0. Nền Dim đen toàn màn hình (Focus mờ nền)
        GameObject dimObj = new GameObject("DimOverlay");
        dimObj.transform.SetParent(root, false);
        RectTransform dimRect = dimObj.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.sizeDelta = Vector2.zero;
        bindings.DimOverlayImg = dimObj.AddComponent<Image>();
        bindings.DimOverlayImg.color = new Color(0f, 0f, 0f, 0.52f);
        bindings.DimOverlayImg.raycastTarget = true;

        // 1. Khung Card Bảng Gỗ Cozy 3D (760x560)
        GameObject cardObj = new GameObject("RhythmWoodBoard");
        cardObj.transform.SetParent(root, false);
        bindings.PanelRect = cardObj.AddComponent<RectTransform>();
        bindings.PanelRect.sizeDelta = new Vector2(760f, 560f);
        bindings.PanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        bindings.PanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        bindings.PanelRect.anchoredPosition = Vector2.zero;

        Image panelImg = cardObj.AddComponent<Image>();
        panelImg.sprite = RhythmProceduralSprites.WoodBoard;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = Color.white;

        // Dây leo Ivy chạy dọc viền trên, trái và phải bảng gỗ
        CreateTopIvyGarland(cardObj.transform, new Vector2(0f, 268f));
        CreateSideIvyGarland(cardObj.transform, false);
        CreateSideIvyGarland(cardObj.transform, true);

        // Cụm hoa & lá xanh ở 2 góc dưới
        CreateFloraBouquet(cardObj.transform, new Vector2(-344f, -244f), false);
        CreateFloraBouquet(cardObj.transform, new Vector2(344f, -244f), true);

        // Trang trí lá cây nhẹ ở 2 góc trên
        CreateCornerDecoration(cardObj.transform, new Vector2(-336f, 240f), 0f);
        CreateCornerDecoration(cardObj.transform, new Vector2(336f, 240f), 180f);

        // 2. Header: Nút Đóng & Đồng Hồ Thời Gian
        CreateCloseButton(cardObj.transform, new Vector2(335f, 226f), onCancel);
        bindings.TimerText = CreateTopTimer(cardObj.transform, new Vector2(-280f, 226f), fontAsset);

        // Tiêu đề chính to rõ: VŨ ĐIỆU HÒA NHỊP
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRect = titleObj.AddComponent<RectTransform>();
        tRect.sizeDelta = new Vector2(580f, 38f);
        tRect.anchoredPosition = new Vector2(0f, 228f);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) titleText.font = fontAsset;
        titleText.text = "VŨ ĐIỆU HÒA NHỊP";
        titleText.fontSize = 28f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.98f, 0.65f, 0.12f, 1f);

        var titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.38f, 0.14f, 0.04f, 0.95f);
        titleOutline.effectDistance = new Vector2(1.8f, -1.8f);

        // Phụ đề tên loài thú & Tier
        GameObject subObj = new GameObject("SubTitle");
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform sRect = subObj.AddComponent<RectTransform>();
        sRect.sizeDelta = new Vector2(580f, 22f);
        sRect.anchoredPosition = new Vector2(0f, 196f);
        var subTitleText = subObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) subTitleText.font = fontAsset;
        subTitleText.text = $"{context.SpeciesName} • {GetRankText(context.Rank)}";
        subTitleText.fontSize = 14f;
        subTitleText.fontStyle = FontStyles.Bold;
        subTitleText.alignment = TextAlignmentOptions.Center;
        subTitleText.color = new Color(0.32f, 0.22f, 0.18f, 1f);

        // 3. Thanh Tiến Độ Thu Phục
        CreateHarmonyBar(cardObj.transform, fontAsset, bindings);

        // 4. Khay Nền & Khung Hiển Thị Chuỗi Mũi Tên Kẹo Thạch 3D
        GameObject seqBackplateObj = new GameObject("SequenceBackplate");
        seqBackplateObj.transform.SetParent(cardObj.transform, false);
        RectTransform sbpRect = seqBackplateObj.AddComponent<RectTransform>();
        sbpRect.sizeDelta = new Vector2(640f, 96f);
        sbpRect.anchoredPosition = new Vector2(0f, 50f);
        Image sbpImg = seqBackplateObj.AddComponent<Image>();
        sbpImg.sprite = RhythmProceduralSprites.CapsuleBar;
        sbpImg.type = Image.Type.Sliced;
        sbpImg.color = new Color(0.95f, 0.92f, 0.86f, 0.92f);

        GameObject seqContainerObj = new GameObject("SequenceContainer");
        seqContainerObj.transform.SetParent(cardObj.transform, false);
        bindings.NoteContainer = seqContainerObj.AddComponent<RectTransform>();
        bindings.NoteContainer.sizeDelta = new Vector2(640f, 96f);
        bindings.NoteContainer.anchoredPosition = new Vector2(0f, 50f);

        // 5. Thanh Nhịp Điệu Gỗ Trầm & Hồng Tâm Luminous Starburst
        CreateRhythmRail(cardObj.transform, bindings);

        // 6. Phản Hồi Đánh Giá & Nút Bấm Chạm Nhịp
        CreateBottomControls(cardObj.transform, fontAsset, onBeatClick, bindings);

        return bindings;
    }

    public static NoteSlotView CreateNoteSlot(Transform parent, RhythmDirection dir, int index, float posX, float width)
    {
        GameObject slotObj = new GameObject($"NoteSlot_{index}");
        slotObj.transform.SetParent(parent, false);
        RectTransform slotRect = slotObj.AddComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(0.5f, 0.5f);
        slotRect.anchorMax = new Vector2(0.5f, 0.5f);
        slotRect.pivot = new Vector2(0.5f, 0.5f);
        slotRect.sizeDelta = new Vector2(width, 76f);
        slotRect.anchoredPosition = new Vector2(posX, 0f);

        GameObject arrowImgObj = new GameObject("ArrowIcon");
        arrowImgObj.transform.SetParent(slotObj.transform, false);
        RectTransform arrRect = arrowImgObj.AddComponent<RectTransform>();
        arrRect.sizeDelta = new Vector2(66f, 66f);
        arrRect.anchoredPosition = Vector2.zero;

        Image arrowImg = arrowImgObj.AddComponent<Image>();
        arrowImg.sprite = RhythmProceduralSprites.GetJellySprite(dir);
        arrowImg.color = Color.white;
        arrowImg.raycastTarget = false;

        var outline = arrowImgObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.65f, 0.52f, 0.50f);
        outline.effectDistance = new Vector2(1f, -1f);

        return new NoteSlotView
        {
            RootObj = slotObj,
            Background = arrowImg,
            Outline = outline,
            ArrowImg = arrowImg,
            Direction = dir,
            BaseColor = GetDirectionColor(dir)
        };
    }

    public static SpaceSlotView CreateSpaceSlot(Transform parent, float posX, float width, TMP_FontAsset fontAsset)
    {
        GameObject spaceObj = new GameObject("SpaceSlot");
        spaceObj.transform.SetParent(parent, false);
        RectTransform rect = spaceObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 76f);
        rect.anchoredPosition = new Vector2(posX, 0f);

        GameObject spaceBtnObj = new GameObject("SpaceBtn");
        spaceBtnObj.transform.SetParent(spaceObj.transform, false);
        RectTransform btnRect = spaceBtnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(width - 4f, 66f);
        btnRect.anchoredPosition = Vector2.zero;

        Image bg = spaceBtnObj.AddComponent<Image>();
        bg.sprite = RhythmProceduralSprites.StoneSpace;
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;
        bg.raycastTarget = false;

        var outline = spaceBtnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.68f, 0.60f, 0.48f, 0.70f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject mainObj = new GameObject("MainLabel");
        mainObj.transform.SetParent(spaceBtnObj.transform, false);
        RectTransform mr = mainObj.AddComponent<RectTransform>();
        mr.anchorMin = Vector2.zero;
        mr.anchorMax = Vector2.one;
        mr.sizeDelta = Vector2.zero;
        var mainTmp = mainObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) mainTmp.font = fontAsset;
        mainTmp.text = "SPACE";
        mainTmp.fontSize = 15.5f;
        mainTmp.fontStyle = FontStyles.Bold;
        mainTmp.alignment = TextAlignmentOptions.Center;
        mainTmp.color = new Color(0.36f, 0.32f, 0.26f, 0.95f);
        mainTmp.raycastTarget = false;

        return new SpaceSlotView
        {
            RootObj = spaceObj,
            Background = bg,
            Outline = outline,
            MainLabel = mainTmp
        };
    }

    private static void CreateHarmonyBar(Transform parent, TMP_FontAsset fontAsset, RhythmUIBindings bindings)
    {
        GameObject bgObj = new GameObject("HarmonyBg");
        bgObj.transform.SetParent(parent, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(640f, 38f);
        bgRect.anchoredPosition = new Vector2(0f, 150f);

        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = RhythmProceduralSprites.CapsuleBar;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.92f, 0.88f, 0.80f, 1f);

        var bgOutline = bgObj.AddComponent<Outline>();
        bgOutline.effectColor = new Color(0.78f, 0.62f, 0.35f, 0.85f);
        bgOutline.effectDistance = new Vector2(1.2f, -1.2f);

        CreateBarWing(bgObj.transform, new Vector2(-326f, 0f), 0f);
        CreateBarWing(bgObj.transform, new Vector2(326f, 0f), 180f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(bgObj.transform, false);
        bindings.HarmonyFillRect = fillObj.AddComponent<RectTransform>();
        bindings.HarmonyFillRect.anchorMin = new Vector2(0f, 0f);
        bindings.HarmonyFillRect.anchorMax = new Vector2(0f, 1f);
        bindings.HarmonyFillRect.sizeDelta = Vector2.zero;
        bindings.HarmonyFillRect.pivot = new Vector2(0f, 0.5f);

        bindings.HarmonyFillImg = fillObj.AddComponent<Image>();
        bindings.HarmonyFillImg.sprite = RhythmProceduralSprites.GlossyFill;
        bindings.HarmonyFillImg.type = Image.Type.Filled;
        bindings.HarmonyFillImg.fillMethod = Image.FillMethod.Horizontal;
        bindings.HarmonyFillImg.fillAmount = 0f;
        bindings.HarmonyFillImg.color = new Color(0.98f, 0.74f, 0.16f, 1f);

        GameObject tObj = new GameObject("Label");
        tObj.transform.SetParent(bgObj.transform, false);
        RectTransform tlRect = tObj.AddComponent<RectTransform>();
        tlRect.anchorMin = Vector2.zero;
        tlRect.anchorMax = Vector2.one;
        tlRect.sizeDelta = Vector2.zero;
        bindings.HarmonyText = tObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) bindings.HarmonyText.font = fontAsset;
        bindings.HarmonyText.text = "TIẾN TRÌNH THU PHỤC: <b>0%</b>";
        bindings.HarmonyText.fontSize = 15.5f;
        bindings.HarmonyText.fontStyle = FontStyles.Bold;
        bindings.HarmonyText.alignment = TextAlignmentOptions.Center;
        bindings.HarmonyText.color = new Color(0.48f, 0.22f, 0.05f, 1f);

        var tOutline = tObj.AddComponent<Outline>();
        tOutline.effectColor = new Color(1f, 0.95f, 0.75f, 0.65f);
        tOutline.effectDistance = new Vector2(0.8f, -0.8f);
    }

    private static void CreateRhythmRail(Transform parent, RhythmUIBindings bindings)
    {
        GameObject railObj = new GameObject("RhythmRail");
        railObj.transform.SetParent(parent, false);
        bindings.RailRect = railObj.AddComponent<RectTransform>();
        bindings.RailRect.sizeDelta = new Vector2(640f, 48f);
        bindings.RailRect.anchoredPosition = new Vector2(0f, -46f);

        Image railImg = railObj.AddComponent<Image>();
        railImg.sprite = RhythmProceduralSprites.CapsuleBar;
        railImg.type = Image.Type.Sliced;
        railImg.color = new Color(0.24f, 0.16f, 0.12f, 0.98f);

        var railOutline = railObj.AddComponent<Outline>();
        railOutline.effectColor = new Color(0.62f, 0.42f, 0.20f, 0.85f);
        railOutline.effectDistance = new Vector2(1f, -1f);

        GameObject guideLineObj = new GameObject("GuideLine");
        guideLineObj.transform.SetParent(railObj.transform, false);
        RectTransform glRect = guideLineObj.AddComponent<RectTransform>();
        glRect.anchorMin = new Vector2(0.04f, 0.5f);
        glRect.anchorMax = new Vector2(0.96f, 0.5f);
        glRect.sizeDelta = new Vector2(0f, 5f);
        Image glImg = guideLineObj.AddComponent<Image>();
        glImg.sprite = RhythmProceduralSprites.White;
        glImg.color = new Color(0.88f, 0.72f, 0.48f, 0.45f);

        float railWidth = 640f - 60f;

        float[] beadPercents = new float[] { 0.16f, 0.32f, 0.48f, 0.64f, 0.80f };
        foreach (var bp in beadPercents)
        {
            GameObject beadObj = new GameObject($"Bead_{bp}");
            beadObj.transform.SetParent(railObj.transform, false);
            RectTransform br = beadObj.AddComponent<RectTransform>();
            br.sizeDelta = new Vector2(13f, 13f);
            float bx = (bp * railWidth) - (railWidth * 0.5f);
            br.anchoredPosition = new Vector2(bx, 0f);
            Image bImg = beadObj.AddComponent<Image>();
            bImg.sprite = RhythmProceduralSprites.GoldBead;
            bImg.color = Color.white;
            bImg.raycastTarget = false;
        }

        float targetPosX = (RhythmMinigameEngine.TargetBeatNormalizedPos * railWidth) - (railWidth * 0.5f);

        GameObject hitZoneObj = new GameObject("HitZone");
        hitZoneObj.transform.SetParent(railObj.transform, false);
        bindings.HitZoneRect = hitZoneObj.AddComponent<RectTransform>();
        bindings.HitZoneRect.sizeDelta = new Vector2(62f, 62f);
        bindings.HitZoneRect.anchoredPosition = new Vector2(targetPosX, 0f);

        bindings.HitZoneImg = hitZoneObj.AddComponent<Image>();
        bindings.HitZoneImg.sprite = RhythmProceduralSprites.TargetStarburst;
        bindings.HitZoneImg.color = Color.white;

        GameObject ringObj = new GameObject("ApproachRing");
        ringObj.transform.SetParent(railObj.transform, false);
        bindings.ApproachRingRect = ringObj.AddComponent<RectTransform>();
        bindings.ApproachRingRect.sizeDelta = new Vector2(62f, 62f);
        bindings.ApproachRingRect.anchoredPosition = new Vector2(targetPosX, 0f);

        bindings.ApproachRingImg = ringObj.AddComponent<Image>();
        bindings.ApproachRingImg.sprite = RhythmProceduralSprites.Ring;
        bindings.ApproachRingImg.color = new Color(1f, 0.90f, 0.45f, 0.85f);
        ringObj.SetActive(false);

        GameObject orbObj = new GameObject("LuminousOrb");
        orbObj.transform.SetParent(railObj.transform, false);
        bindings.OrbRect = orbObj.AddComponent<RectTransform>();
        bindings.OrbRect.sizeDelta = new Vector2(40f, 40f);
        bindings.OrbRect.anchoredPosition = new Vector2(-railWidth * 0.5f, 0f);

        bindings.OrbImg = orbObj.AddComponent<Image>();
        bindings.OrbImg.sprite = RhythmProceduralSprites.PearlOrb;
        bindings.OrbImg.color = Color.white;
    }

    private static TextMeshProUGUI CreateTopTimer(Transform parent, Vector2 pos, TMP_FontAsset fontAsset)
    {
        GameObject timerObj = new GameObject("TopTimerBadge");
        timerObj.transform.SetParent(parent, false);
        RectTransform tRect = timerObj.AddComponent<RectTransform>();
        tRect.sizeDelta = new Vector2(114f, 34f);
        tRect.anchoredPosition = pos;

        Image bg = timerObj.AddComponent<Image>();
        bg.sprite = RhythmProceduralSprites.GreenBadge;
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        var outline = timerObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.46f, 0.18f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject clockIconObj = new GameObject("ClockIcon");
        clockIconObj.transform.SetParent(timerObj.transform, false);
        RectTransform cir = clockIconObj.AddComponent<RectTransform>();
        cir.sizeDelta = new Vector2(18f, 18f);
        cir.anchoredPosition = new Vector2(-32f, 0f);
        Image cImg = clockIconObj.AddComponent<Image>();
        cImg.sprite = RhythmProceduralSprites.ClockIcon;
        cImg.color = Color.white;
        cImg.raycastTarget = false;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(timerObj.transform, false);
        RectTransform tr = textObj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;
        tr.anchoredPosition = new Vector2(10f, 0f);

        var timerText = textObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) timerText.font = fontAsset;
        timerText.fontSize = 14.5f;
        timerText.fontStyle = FontStyles.Bold;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.color = Color.white;
        timerText.text = "13,9s";

        return timerText;
    }

    private static void CreateBottomControls(
        Transform parent,
        TMP_FontAsset fontAsset,
        UnityEngine.Events.UnityAction onBeatClick,
        RhythmUIBindings bindings)
    {
        GameObject comboObj = new GameObject("ComboFeedback");
        comboObj.transform.SetParent(parent, false);
        RectTransform comboRect = comboObj.AddComponent<RectTransform>();
        comboRect.sizeDelta = new Vector2(500f, 22f);
        comboRect.anchoredPosition = new Vector2(0f, -98f);
        bindings.ComboText = comboObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) bindings.ComboText.font = fontAsset;
        bindings.ComboText.fontSize = 14f;
        bindings.ComboText.fontStyle = FontStyles.Bold;
        bindings.ComboText.alignment = TextAlignmentOptions.Center;
        bindings.ComboText.color = new Color(0.48f, 0.38f, 0.32f, 1f);
        bindings.ComboText.text = "";

        GameObject gradeObj = new GameObject("GradeFeedback");
        gradeObj.transform.SetParent(parent, false);
        RectTransform gradeRect = gradeObj.AddComponent<RectTransform>();
        gradeRect.sizeDelta = new Vector2(500f, 34f);
        gradeRect.anchoredPosition = new Vector2(0f, -126f);
        bindings.FeedbackGradeText = gradeObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) bindings.FeedbackGradeText.font = fontAsset;
        bindings.FeedbackGradeText.fontSize = 24f;
        bindings.FeedbackGradeText.fontStyle = FontStyles.Bold;
        bindings.FeedbackGradeText.alignment = TextAlignmentOptions.Center;
        bindings.FeedbackGradeText.textWrappingMode = TextWrappingModes.NoWrap;
        bindings.FeedbackGradeText.text = "";

        var gOutline = gradeObj.AddComponent<Outline>();
        gOutline.effectColor = new Color(0.42f, 0.18f, 0.05f, 0.85f);
        gOutline.effectDistance = new Vector2(1.2f, -1.2f);

        GameObject btnObj = new GameObject("Btn_Beat");
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320f, 54f);
        rect.anchoredPosition = new Vector2(0f, -172f);

        bindings.BeatButtonImg = btnObj.AddComponent<Image>();
        bindings.BeatButtonImg.sprite = RhythmProceduralSprites.BigPillBtn;
        bindings.BeatButtonImg.type = Image.Type.Sliced;
        bindings.BeatButtonImg.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bindings.BeatButtonImg;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 0.95f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.78f, 1f);
        btn.colors = colors;
        if (onBeatClick != null) btn.onClick.AddListener(onBeatClick);
        bindings.BeatButton = btn;

        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(btnObj.transform, false);
        RectTransform tr = tObj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;
        bindings.BeatButtonText = tObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) bindings.BeatButtonText.font = fontAsset;
        bindings.BeatButtonText.text = "CHẠM NHỊP (SPACE)";
        bindings.BeatButtonText.fontSize = 17f;
        bindings.BeatButtonText.fontStyle = FontStyles.Bold;
        bindings.BeatButtonText.alignment = TextAlignmentOptions.Center;
        bindings.BeatButtonText.color = Color.white;
        bindings.BeatButtonText.raycastTarget = false;

        var btnOutline = tObj.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.12f, 0.44f, 0.18f, 0.90f);
        btnOutline.effectDistance = new Vector2(1f, -1f);

        GameObject legendObj = new GameObject("HotkeyLegend");
        legendObj.transform.SetParent(parent, false);
        RectTransform legRect = legendObj.AddComponent<RectTransform>();
        legRect.sizeDelta = new Vector2(580f, 20f);
        legRect.anchoredPosition = new Vector2(0f, -222f);
        var legTmp = legendObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) legTmp.font = fontAsset;
        legTmp.text = "<color=#6E523D>[A, W, S, D] or [Arrow Keys] • [SPACE] Tap rhythm</color>";
        legTmp.fontSize = 12.5f;
        legTmp.alignment = TextAlignmentOptions.Center;
        legTmp.raycastTarget = false;
    }

    private static void CreateCloseButton(Transform parent, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("CloseBtn");
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(38f, 38f);
        rect.anchoredPosition = pos;

        Image img = btnObj.AddComponent<Image>();
        img.sprite = RhythmProceduralSprites.RedCloseBtn;
        img.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.8f, 1f);
        btn.colors = colors;
        if (action != null) btn.onClick.AddListener(action);
    }

    private static void CreateTopIvyGarland(Transform parent, Vector2 pos)
    {
        GameObject garlandObj = new GameObject("TopIvyGarland");
        garlandObj.transform.SetParent(parent, false);
        RectTransform gr = garlandObj.AddComponent<RectTransform>();
        gr.sizeDelta = new Vector2(560f, 48f);
        gr.anchoredPosition = pos;

        Image img = garlandObj.AddComponent<Image>();
        img.sprite = RhythmProceduralSprites.IvyGarland;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private static void CreateSideIvyGarland(Transform parent, bool isRight)
    {
        GameObject ivyObj = new GameObject(isRight ? "SideIvyRight" : "SideIvyLeft");
        ivyObj.transform.SetParent(parent, false);
        RectTransform r = ivyObj.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(46f, 480f);
        r.anchoredPosition = new Vector2(isRight ? 368f : -368f, 0f);

        Image img = ivyObj.AddComponent<Image>();
        img.sprite = isRight ? RhythmProceduralSprites.SideIvyRight : RhythmProceduralSprites.SideIvyLeft;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private static void CreateFloraBouquet(Transform parent, Vector2 pos, bool isRight)
    {
        GameObject floraObj = new GameObject(isRight ? "FloraBouquetRight" : "FloraBouquetLeft");
        floraObj.transform.SetParent(parent, false);
        RectTransform fr = floraObj.AddComponent<RectTransform>();
        fr.sizeDelta = new Vector2(110f, 110f);
        fr.anchoredPosition = pos;
        fr.localEulerAngles = isRight ? new Vector3(0f, 180f, 0f) : Vector3.zero;

        Image img = floraObj.AddComponent<Image>();
        img.sprite = isRight ? RhythmProceduralSprites.FloraRight : RhythmProceduralSprites.FloraLeft;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private static void CreateBarWing(Transform parent, Vector2 pos, float rotY)
    {
        GameObject wingObj = new GameObject("BarWing");
        wingObj.transform.SetParent(parent, false);
        RectTransform wr = wingObj.AddComponent<RectTransform>();
        wr.sizeDelta = new Vector2(48f, 48f);
        wr.anchoredPosition = pos;
        wr.localEulerAngles = new Vector3(0f, rotY, 0f);

        Image img = wingObj.AddComponent<Image>();
        img.sprite = RhythmProceduralSprites.BarWing;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private static void CreateCornerDecoration(Transform parent, Vector2 pos, float rotZ)
    {
        GameObject decoObj = new GameObject("CornerFlora");
        decoObj.transform.SetParent(parent, false);
        RectTransform dr = decoObj.AddComponent<RectTransform>();
        dr.sizeDelta = new Vector2(90f, 90f);
        dr.anchoredPosition = pos;
        dr.localEulerAngles = new Vector3(0f, 0f, rotZ);

        Image img = decoObj.AddComponent<Image>();
        img.sprite = RhythmProceduralSprites.CornerFlora;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    public static Color GetDirectionColor(RhythmDirection dir) => dir switch
    {
        RhythmDirection.Left => new Color(0.20f, 0.50f, 0.80f, 1f),
        RhythmDirection.Up => new Color(0.22f, 0.62f, 0.38f, 1f),
        RhythmDirection.Down => new Color(0.85f, 0.52f, 0.15f, 1f),
        RhythmDirection.Right => new Color(0.82f, 0.28f, 0.30f, 1f),
        _ => new Color(0.4f, 0.4f, 0.4f, 1f)
    };

    public static float GetDirectionRotationAngle(RhythmDirection dir) => dir switch
    {
        RhythmDirection.Up => 0f,
        RhythmDirection.Right => -90f,
        RhythmDirection.Down => 180f,
        RhythmDirection.Left => 90f,
        _ => 0f
    };

    public static string GetDirectionKeyHint(RhythmDirection dir) => dir switch
    {
        RhythmDirection.Left => "[ A ]",
        RhythmDirection.Up => "[ W ]",
        RhythmDirection.Down => "[ S ]",
        RhythmDirection.Right => "[ D ]",
        _ => ""
    };

    public static string GetRankText(AnimalRank rank) => rank switch
    {
        AnimalRank.Common => "Tier 1 - Common",
        AnimalRank.Uncommon => "Tier 2 - Uncommon",
        AnimalRank.Rare => "Tier 3 - Rare",
        AnimalRank.Epic => "Tier 4 - Epic",
        AnimalRank.Legendary => "Tier 5 - Legendary",
        _ => $"Tier {(int)rank}"
    };
}


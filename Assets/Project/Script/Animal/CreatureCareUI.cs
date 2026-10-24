using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>
/// Giao diện Tiến trình Chăm sóc Sinh vật (Creature Care Progress Bar & Rank Up UI).
/// Hiển thị trực quan:
/// - Tên loài, Cấp sao, Trait tính cách, và Rank chất lượng hiện tại (Rank I -> II -> III).
/// - Thanh tiến trình Care (Care Progress Bar) trực quan với tỉ lệ % và màu sắc ấm áp.
/// - Nút Cho ăn (Feed) giúp tăng thanh Care.
/// - Nút Nâng Rank (Rank Up) khi thanh Care đạt 100% kèm chi phí Coins.
/// </summary>
public class CreatureCareUI : MonoBehaviour
{
    private static CreatureCareUI instance;
    public static CreatureCareUI Instance => instance;

    [Header("UI Structure")]
    private RectTransform windowModal;
    private RectTransform panelRect;
    private TextMeshProUGUI creatureNameText;
    private TextMeshProUGUI rankBadgeText;
    private TextMeshProUGUI statsText;
    private Slider careSlider;
    private TextMeshProUGUI careProgressLabel;
    private Button feedButton;
    private TextMeshProUGUI feedButtonText;
    private Button rankUpButton;
    private TextMeshProUGUI rankUpButtonText;

    private AnimalIndividual currentAnimal;
    private HabitatPlacementIdentity currentIdentity;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildUI();
    }

    private void Start()
    {
        EconomyHUD.RegisterWindow(windowModal.gameObject, CloseWindow);
    }

    private void BuildUI()
    {
        // 1. Root Modal (Dimmer)
        GameObject modalObj = new GameObject("CreatureCareModal", typeof(RectTransform), typeof(Image));
        modalObj.transform.SetParent(transform, false);
        windowModal = modalObj.GetComponent<RectTransform>();
        EconomyHUD.Stretch(windowModal);

        Image dimmerImg = modalObj.GetComponent<Image>();
        dimmerImg.color = new Color(0.06f, 0.09f, 0.12f, 0.65f);

        Button dismissBtn = modalObj.AddComponent<Button>();
        dismissBtn.onClick.AddListener(CloseWindow);

        // 2. Main Panel
        GameObject panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(windowModal, false);
        panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(460f, 380f);

        Image panelImg = panelObj.GetComponent<Image>();
        panelImg.sprite = PreserveDiamondStyle.DiamondBase;
        panelImg.color = new Color(0.10f, 0.14f, 0.18f, 0.98f);

        // Header Title
        GameObject nameObj = new GameObject("CreatureName", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(panelRect, false);
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchoredPosition = new Vector2(0f, 150f);
        nameRect.sizeDelta = new Vector2(400f, 32f);

        creatureNameText = nameObj.GetComponent<TextMeshProUGUI>();
        creatureNameText.alignment = TextAlignmentOptions.Center;
        creatureNameText.fontSize = 20f;
        creatureNameText.fontStyle = FontStyles.Bold;
        creatureNameText.color = Color.white;

        // Rank Badge Text
        GameObject rankObj = new GameObject("RankBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
        rankObj.transform.SetParent(panelRect, false);
        RectTransform rankRect = rankObj.GetComponent<RectTransform>();
        rankRect.anchoredPosition = new Vector2(0f, 120f);
        rankRect.sizeDelta = new Vector2(300f, 24f);

        rankBadgeText = rankObj.GetComponent<TextMeshProUGUI>();
        rankBadgeText.alignment = TextAlignmentOptions.Center;
        rankBadgeText.fontSize = 15f;
        rankBadgeText.fontStyle = FontStyles.Bold;
        rankBadgeText.color = new Color(1f, 0.85f, 0.35f, 1f);

        // Stats Text (Sao, Trait)
        GameObject statsObj = new GameObject("StatsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statsObj.transform.SetParent(panelRect, false);
        RectTransform statsRect = statsObj.GetComponent<RectTransform>();
        statsRect.anchoredPosition = new Vector2(0f, 85f);
        statsRect.sizeDelta = new Vector2(400f, 40f);

        statsText = statsObj.GetComponent<TextMeshProUGUI>();
        statsText.alignment = TextAlignmentOptions.Center;
        statsText.fontSize = 13f;
        statsText.color = new Color(0.85f, 0.90f, 0.95f, 0.9f);

        // Nút Đóng
        GameObject closeObj = new GameObject("CloseBtn", typeof(RectTransform), typeof(Button), typeof(Image));
        closeObj.transform.SetParent(panelRect, false);
        RectTransform closeRect = closeObj.GetComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(200f, 160f);
        closeRect.sizeDelta = new Vector2(30f, 30f);

        Button closeBtn = closeObj.GetComponent<Button>();
        closeBtn.onClick.AddListener(CloseWindow);

        Image closeImg = closeObj.GetComponent<Image>();
        closeImg.sprite = PreserveDiamondStyle.DiamondBase;
        closeImg.color = new Color(0.85f, 0.25f, 0.25f, 0.9f);

        GameObject closeTxtObj = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeTxtObj.transform.SetParent(closeRect, false);
        TextMeshProUGUI closeTxt = closeTxtObj.GetComponent<TextMeshProUGUI>();
        closeTxt.text = "✕";
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.fontSize = 16f;
        closeTxt.color = Color.white;

        // 3. CARE PROGRESS BAR
        GameObject barBgObj = new GameObject("CareBarBG", typeof(RectTransform), typeof(Image));
        barBgObj.transform.SetParent(panelRect, false);
        RectTransform barBgRect = barBgObj.GetComponent<RectTransform>();
        barBgRect.anchoredPosition = new Vector2(0f, 25f);
        barBgRect.sizeDelta = new Vector2(360f, 26f);

        Image barBgImg = barBgObj.GetComponent<Image>();
        barBgImg.sprite = PreserveDiamondStyle.DiamondBase;
        barBgImg.color = new Color(0.06f, 0.08f, 0.10f, 0.95f);

        // Fill Rect
        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(barBgObj.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.sizeDelta = new Vector2(0f, 0f);

        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.color = new Color(0.95f, 0.65f, 0.20f, 1f); // Màu cam vàng năng lượng chăm sóc

        careSlider = barBgObj.AddComponent<Slider>();
        careSlider.fillRect = fillRect;
        careSlider.minValue = 0f;
        careSlider.maxValue = 100f;
        careSlider.interactable = false;

        // Label tiến trình
        GameObject labelObj = new GameObject("CareLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(barBgObj.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(360f, 26f);

        careProgressLabel = labelObj.GetComponent<TextMeshProUGUI>();
        careProgressLabel.alignment = TextAlignmentOptions.Center;
        careProgressLabel.fontSize = 13f;
        careProgressLabel.fontStyle = FontStyles.Bold;
        careProgressLabel.color = Color.white;

        // 4. ACTION BUTTONS: Cho Ăn & Nâng Rank
        // Nút Cho ăn (Feed)
        GameObject feedObj = new GameObject("FeedButton", typeof(RectTransform), typeof(Button), typeof(Image));
        feedObj.transform.SetParent(panelRect, false);
        RectTransform feedRect = feedObj.GetComponent<RectTransform>();
        feedRect.anchoredPosition = new Vector2(-95f, -65f);
        feedRect.sizeDelta = new Vector2(170f, 44f);

        feedButton = feedObj.GetComponent<Button>();
        feedButton.onClick.AddListener(OnCareClicked);

        Image feedImg = feedObj.GetComponent<Image>();
        feedImg.sprite = PreserveDiamondStyle.DiamondBase;
        feedImg.color = new Color(0.20f, 0.65f, 0.35f, 1f);

        GameObject feedLabelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        feedLabelObj.transform.SetParent(feedObj.transform, false);
        feedButtonText = feedLabelObj.GetComponent<TextMeshProUGUI>();
        feedButtonText.text = "CHĂM SÓC (-2C)\n<size=11>+25 Care</size>";
        feedButtonText.alignment = TextAlignmentOptions.Center;
        feedButtonText.fontSize = 13f;
        feedButtonText.fontStyle = FontStyles.Bold;
        feedButtonText.color = Color.white;

        // Nút Nâng Rank (Rank Up)
        GameObject rankUpObj = new GameObject("RankUpButton", typeof(RectTransform), typeof(Button), typeof(Image));
        rankUpObj.transform.SetParent(panelRect, false);
        RectTransform rankUpRect = rankUpObj.GetComponent<RectTransform>();
        rankUpRect.anchoredPosition = new Vector2(95f, -65f);
        rankUpRect.sizeDelta = new Vector2(170f, 44f);

        rankUpButton = rankUpObj.GetComponent<Button>();
        rankUpButton.onClick.AddListener(OnRankUpClicked);

        Image rankUpImg = rankUpObj.GetComponent<Image>();
        rankUpImg.sprite = PreserveDiamondStyle.DiamondBase;
        rankUpImg.color = new Color(0.95f, 0.70f, 0.20f, 1f);

        GameObject rankUpLabelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        rankUpLabelObj.transform.SetParent(rankUpObj.transform, false);
        rankUpButtonText = rankUpLabelObj.GetComponent<TextMeshProUGUI>();
        rankUpButtonText.text = "NÂNG RANK\n<size=11>(Đầy 100% Care)</size>";
        rankUpButtonText.alignment = TextAlignmentOptions.Center;
        rankUpButtonText.fontSize = 13f;
        rankUpButtonText.fontStyle = FontStyles.Bold;
        rankUpButtonText.color = Color.white;

        windowModal.gameObject.SetActive(false);
    }

    /// <summary>
    /// Mở bảng Care cho một cá thể sinh vật - Chuyển tiếp sang giao diện bảng đôi mới HabitatCreaturePanelUI
    /// </summary>
    public void OpenForCreature(AnimalIndividual animal, HabitatPlacementIdentity identity = null)
    {
        currentAnimal = animal;
        currentIdentity = identity != null ? identity : (animal != null ? animal.GetComponent<HabitatPlacementIdentity>() : null);

        if (HabitatCreaturePanelUI.Instance != null && HabitatRuntimeManager.TryGetActive(out var hrm) && hrm.Habitats != null)
        {
            string searchId = animal != null ? animal.UniqueID : (identity != null ? identity.PlacementId : "");
            foreach (var h in hrm.Habitats)
            {
                if (h != null && h.residents != null && h.residents.Exists(r => r.individualId == searchId))
                {
                    HabitatCreaturePanelUI.Instance.OpenForHabitat(h);
                    return;
                }
            }
            if (hrm.Habitats.Count > 0)
            {
                HabitatCreaturePanelUI.Instance.OpenForHabitat(hrm.Habitats[0]);
                return;
            }
        }

        RefreshData();
        windowModal.gameObject.SetActive(true);
    }

    public void RefreshData()
    {
        string id = currentAnimal != null ? currentAnimal.UniqueID : (currentIdentity != null ? currentIdentity.PlacementId : "");
        string name = currentAnimal != null ? currentAnimal.IndividualName : "Sinh vật";
        int star = currentAnimal != null ? currentAnimal.StarLevel : 1;
        string traitStr = currentAnimal != null ? currentAnimal.Trait.ToString() : "Thân thiện";

        CreatureQualityRank currentRank = currentIdentity != null ? currentIdentity.QualityRank : CreatureQualityRank.RankI;

        creatureNameText.text = $"★ {name} ★";
        rankBadgeText.text = $"CẤP ĐỘ GDD: <color=#38BDF8>{currentRank}</color>";
        statsText.text = $"{star}★ · Tính cách: {traitStr}";

        int care = CreatureCareManager.Instance != null ? CreatureCareManager.Instance.GetCareProgress(id) : 0;
        careSlider.value = care;
        careProgressLabel.text = $"Tiến trình Care: {care}/100 ({care}%)";

        // Cập nhật trạng thái nút Nâng Rank
        if (currentRank >= CreatureQualityRank.RankIII)
        {
            rankUpButton.interactable = false;
            rankUpButtonText.text = "MAX RANK (III)";
            feedButton.interactable = false;
        }
        else
        {
            feedButton.interactable = care < 100;
            bool canRankUp = care >= 100;
            rankUpButton.interactable = canRankUp;

            int cost = currentRank == CreatureQualityRank.RankI ? 15 : 30;
            string nextRankStr = currentRank == CreatureQualityRank.RankI ? "Rank II" : "Rank III";
            rankUpButtonText.text = canRankUp ? $"NÂNG {nextRankStr} (-{cost}C)" : $"CẦN 100% CARE\n<size=10>(Chi phí: {cost}C)</size>";
        }
    }

    private void OnCareClicked()
    {
        string id = currentAnimal != null ? currentAnimal.UniqueID : (currentIdentity != null ? currentIdentity.PlacementId : "");
        if (CreatureCareManager.Instance.CareCreature(id, out string fb))
        {
            HexPlacementPreviewHUD.ShowFeedbackToast(fb);
            RefreshData();
        }
        else
        {
            HexPlacementPreviewHUD.ShowFeedbackToast($"[!] {fb}");
        }
    }

    private void OnRankUpClicked()
    {
        CreatureQualityRank currentRank = currentIdentity != null ? currentIdentity.QualityRank : CreatureQualityRank.RankI;
        int cost = currentRank == CreatureQualityRank.RankI ? 15 : 30;
        string nextRankStr = currentRank == CreatureQualityRank.RankI ? "Rank II" : "Rank III";

        GameDialogUI.ShowConfirm(
            $"Bạn có muốn nâng cấp sinh vật lên {nextRankStr} với chi phí {cost} xu không?",
            () =>
            {
                if (CreatureCareManager.Instance.TryRankUp(currentAnimal, currentIdentity, out string result))
                {
                    HexPlacementPreviewHUD.ShowFeedbackToast($"★ {result}");
                    RefreshData();
                }
                else
                {
                    HexPlacementPreviewHUD.ShowFeedbackToast($"[!] {result}");
                }
            }
        );
    }

    public void CloseWindow()
    {
        windowModal.gameObject.SetActive(false);
    }
}

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LumiWorld.Acs;

/// <summary>Browse creature card data available in the project.</summary>
[DisallowMultipleComponent]
public sealed class CreatureCollectionUI : MonoBehaviour
{
    private static CreatureCollectionUI instance;
    public static CreatureCollectionUI Instance => instance;

    private GameObject backdrop;
    private RectTransform window;
    private RectTransform content;
    private ScrollRect scroll;
    private TextMeshProUGUI countLabel;
    private RectTransform stageRect;
    private Canvas rootCanvas;
    private int lastScreenW, lastScreenH;
    private float lastParentScale;

    // Match CardShopUI's fixed design stage.
    private const float PanelWidth = 880f;
    private const float PanelHeight = 560f;

    public bool IsOpen => backdrop != null && backdrop.activeSelf;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
        if (backdrop != null) backdrop.SetActive(false);
    }

    private void OnDestroy()
    {
        EconomyHUD.UnregisterWindow(backdrop);
        if (instance == this) instance = null;
    }

    public void Toggle() { if (IsOpen) Close(); else Open(); }
    public void Open()
    {
        backdrop.SetActive(true);
        EconomyHUD.RegisterWindow(backdrop, Close);
        EconomyHUD.BringWindowToFront(backdrop);
        Refresh();
    }
    public void Close()
    {
        if (IsOpen) EconomyHUD.MarkWindowClosed();
        backdrop.SetActive(false);
    }

    private void Build()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        rootCanvas = canvas.rootCanvas;
        backdrop = new GameObject("CreatureCollectionBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(transform, false);
        backdrop.transform.SetAsLastSibling();
        LumiWorldUITheme.Stretch(backdrop.GetComponent<RectTransform>());
        backdrop.GetComponent<Image>().color = LumiWorldUITheme.Backdrop;
        backdrop.GetComponent<Button>().transition = Selectable.Transition.None;

        GameObject stage = new GameObject("FitStage", typeof(RectTransform));
        stage.transform.SetParent(backdrop.transform, false);
        stageRect = stage.GetComponent<RectTransform>();
        SyncStageScale(true);
        Image panel = LumiWorldUITheme.CreateRoundedSurface(stageRect, "CollectionWindow", LumiWorldUITheme.Window, LumiWorldUITheme.Edge, 22, 1.5f, 6f, .22f);
        window = panel.rectTransform;
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f);
        window.anchoredPosition = Vector2.zero;
        window.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        LumiWorldUITheme.CreateLabel(window, "Title", "Creature Collection", 20, LumiWorldUITheme.Text, true);
        RectTransform title = window.Find("Title") as RectTransform;
        LumiWorldUITheme.PlaceTopLeft(title, 56, 13, 400, 34);
        countLabel = LumiWorldUITheme.CreateLabel(window, "Count", "", 12, LumiWorldUITheme.Muted);
        LumiWorldUITheme.PlaceTopLeft(countLabel.rectTransform, 24, 49, 300, 20);
        LumiWorldUITheme.CreateCloseButton(window, 22, 14, Close, 34);

        Image well = LumiWorldUITheme.CreateRoundedSurface(window, "CollectionWell", LumiWorldUITheme.Well, LumiWorldUITheme.Edge, 18, 1, 0, 0);
        LumiWorldUITheme.PlaceTopLeft(well.rectTransform, 24, 82, 832, 454);
        GameObject vp = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        vp.transform.SetParent(well.transform, false);
        LumiWorldUITheme.Stretch(vp.GetComponent<RectTransform>(), 8);
        vp.GetComponent<Image>().color = new Color(0, 0, 0, .001f);
        scroll = vp.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        GameObject cg = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        cg.transform.SetParent(vp.transform, false);
        content = cg.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 1);
        content.anchoredPosition = Vector2.zero;
        GridLayoutGroup grid = cg.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(184, 200);
        grid.spacing = new Vector2(12, 12);
        grid.padding = new RectOffset(16, 16, 12, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
        ContentSizeFitter fitter = cg.GetComponent<ContentSizeFitter>(); fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = vp.GetComponent<RectTransform>(); scroll.content = content;
        scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    private void LateUpdate() => SyncStageScale();

    private void SyncStageScale(bool force = false)
    {
        if (stageRect == null || rootCanvas == null) return;
        float parentScale = transform.lossyScale.x;
        if (!force && lastScreenW == Screen.width && lastScreenH == Screen.height && Mathf.Approximately(lastParentScale, parentScale)) return;
        lastScreenW = Screen.width; lastScreenH = Screen.height; lastParentScale = parentScale;
        float fit = LumiWorldUITheme.ComputeFitScale(this, out Vector2 stageSize);
        stageRect.anchorMin = stageRect.anchorMax = stageRect.pivot = Vector2.zero;
        stageRect.anchoredPosition = Vector2.zero;
        stageRect.sizeDelta = stageSize;
        stageRect.localScale = Vector3.one * fit;
    }

    private void Refresh()
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        List<CardData> creatures = GatherCreatureCatalog();
        CardDeskController desk = CardDeskController.Instance;
        EconomySaveSystem economySave = EconomySaveSystem.Instance;

        // Sync any currently held or placed creatures into persistent discovery storage.
        HashSet<string> placedIds = null;
        if (!WorldLaunch.IsVisit)
        {
            if (WorldSaveSystem.Instance != null)
            {
                placedIds = new HashSet<string>();
                WorldSaveSystem.Instance.CollectPlacedCreatureIds(placedIds);
                if (economySave != null && placedIds.Count > 0)
                    economySave.MarkCreaturesDiscovered(placedIds);
            }
            if (economySave != null && desk != null)
            {
                foreach (CardData card in creatures)
                {
                    string id = card.EffectiveCardId;
                    if (desk.GetHeldCount(id) > 0) economySave.MarkCreatureDiscovered(id);
                }
            }
        }

        int discovered = 0;
        foreach (CardData card in creatures)
        {
            string id = card.EffectiveCardId;
            bool isDiscovered = (economySave != null && economySave.IsCreatureDiscovered(id))
                || (desk != null && desk.GetHeldCount(id) > 0)
                || (placedIds != null && placedIds.Contains(id));
            if (isDiscovered) discovered++;
            BuildEntry(card, isDiscovered);
        }
        countLabel.text = $"{discovered} / {creatures.Count} discovered";
    }

    private static List<CardData> GatherCreatureCatalog()
    {
        var byId = new Dictionary<string, CardData>();

        // Prefer the official CardCatalog (Assets/Data/Save/CardCatalog.asset -> Assets/Data/Cards/Creature)
        // so stale duplicates under Resources/Cards do not shadow updated CardData assets.
        CardCatalog catalog = WorldSaveSystem.Instance != null ? WorldSaveSystem.Instance.Cards : null;
        if (catalog != null && catalog.All != null)
        {
            foreach (CardData card in catalog.All)
            {
                if (card == null || card.cardType != CardType.Creature) continue;
                string id = card.EffectiveCardId;
                if (!string.IsNullOrEmpty(id) && !byId.ContainsKey(id)) byId.Add(id, card);
            }
        }

        // Supplement with runtime-created or uncatalogued creature cards (e.g., Braluff).
        foreach (CardData card in Resources.FindObjectsOfTypeAll<CardData>())
        {
            if (card == null || card.cardType != CardType.Creature) continue;
            string id = card.EffectiveCardId;
            if (!string.IsNullOrEmpty(id) && !byId.ContainsKey(id)) byId.Add(id, card);
        }

        return byId.Values.OrderBy(c => c.cardName).ToList();
    }

    private void BuildEntry(CardData card, bool isDiscovered)
    {
        Image bg = LumiWorldUITheme.CreateRoundedSurface(content, "Creature_" + card.name, LumiWorldUITheme.Slot, LumiWorldUITheme.Edge, 16, 1, 2, .1f);
        // GridLayoutGroup exclusively controls the cell dimensions and position.
        GameObject imageObj = new GameObject("Artwork", typeof(RectTransform), typeof(Image)); imageObj.transform.SetParent(bg.transform, false);
        RectTransform artRect = imageObj.GetComponent<RectTransform>();
        artRect.anchorMin = new Vector2(.5f, 1f); artRect.anchorMax = new Vector2(.5f, 1f); artRect.pivot = new Vector2(.5f, 1f);
        artRect.anchoredPosition = new Vector2(0f, -10f); artRect.sizeDelta = new Vector2(150f, 142f);
        Image art = imageObj.GetComponent<Image>(); art.sprite = card.cardImage; art.preserveAspect = true; art.raycastTarget = false;
        if (!isDiscovered)
        {
            GameObject overlay = new GameObject("UndiscoveredOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(imageObj.transform, false);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one; overlayRect.offsetMin = Vector2.zero; overlayRect.offsetMax = Vector2.zero;
            Image overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0.08f, 0.10f, 0.13f, 0.78f);
            overlayImage.raycastTarget = false;

            TextMeshProUGUI mystery = LumiWorldUITheme.CreateLabel(overlay.transform, "Mystery", "?", 48, Color.white, true, TextAlignmentOptions.Center);
            LumiWorldUITheme.Stretch(mystery.rectTransform);
        }
        TextMeshProUGUI name = LumiWorldUITheme.CreateLabel(bg.transform, "Name", isDiscovered ? card.cardName : "?", 18, LumiWorldUITheme.Text, true, TextAlignmentOptions.Center, wrap: true);
        RectTransform nameRect = name.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f); nameRect.anchorMax = new Vector2(1f, 0f); nameRect.pivot = new Vector2(.5f, 0f);
        nameRect.offsetMin = new Vector2(10f, 27f); nameRect.offsetMax = new Vector2(-10f, 53f);
        TextMeshProUGUI rarity = LumiWorldUITheme.CreateLabel(bg.transform, "Rarity", isDiscovered ? card.rarity.ToString() : "?", 14, LumiWorldUITheme.Muted, false, TextAlignmentOptions.Center);
        RectTransform rarityRect = rarity.rectTransform;
        rarityRect.anchorMin = new Vector2(0f, 0f); rarityRect.anchorMax = new Vector2(1f, 0f); rarityRect.pivot = new Vector2(.5f, 0f);
        rarityRect.offsetMin = new Vector2(10f, 8f); rarityRect.offsetMax = new Vector2(-10f, 28f);
    }
}

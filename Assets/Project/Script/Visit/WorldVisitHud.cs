using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phần thế giới trên màn chơi. Bản đồ, ô tìm kiếm, Top islands và Share đã chuyển sang scene WorldMap,
/// ở đây chỉ còn:
/// - Đảo của mình (MapBuilding): nút World map, lưu đảo rồi mở scene WorldMap.
/// - Đang thăm (VisitWorld): tên chủ đảo, điểm, toạ độ, Listings, Back (về WorldMap đúng đảo vừa thăm) và Home.
/// Tự tạo khi vào scene có WorldSaveSystem. Chữ hiện ra là tiếng Anh.
/// </summary>
public class WorldVisitHud : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.13f, 0.92f);
    private static readonly Color AccentColor = new Color(0.22f, 0.55f, 0.95f, 1f);
    private static readonly Color DangerColor = new Color(0.78f, 0.32f, 0.30f, 1f);
    private static readonly Color NeutralColor = new Color(0.26f, 0.30f, 0.38f, 1f);
    private static readonly Color MutedColor = new Color(0.72f, 0.76f, 0.84f, 1f);
    private static readonly Color TextColor = Color.white;

    // Cột trái nằm dưới thanh tài nguyên (InventoryUI ở (20, 20), cao 48)
    private const float ColumnX = 20f;
    private const float ColumnWidth = 360f;
    private const float PanelTop = 86f;
    private static readonly string[] DefaultObstacleKinds =
    {
        "Obstacle", "BarrenLand", "Drought", "Cyclone", "ForestFire"
    };

    // Dời cả lớp HUD (Pos X / Pos Y của object WorldVisitHud trong Inspector)
    private static readonly Vector2 RootOffset = new Vector2(-5f, -99f);

    // Phóng theo Scale Factor của Canvas như EconomyHUD: 1 đơn vị = 1 pixel màn hình
    private Canvas canvas;
    private float fittedCanvasScale;
    private Vector2 fittedCanvasSize;

    private TextMeshProUGUI statusText;
    private TextMeshProUGUI visitStatusText;
    private Button retryObstacleButton;
    private ListingsWindow listings;
    private HexWorldGenerator visitedWorld;
    private HexCameraController cameraController;
    private int obstacleFeeCoins = 20;
    private string[] allowedObstacleKinds = DefaultObstacleKinds;
    private bool sendingObstacle;
    private bool pendingObstacleFeePaid;
    private ObstacleSendRequest pendingObstacle;
    private string pendingObstacleKey;
    private readonly Dictionary<GameObject, string> obstacleCardKinds = new Dictionary<GameObject, string>();
    private readonly Dictionary<GameObject, ObstacleCardDragOrigin> obstacleCardDragOrigins =
        new Dictionary<GameObject, ObstacleCardDragOrigin>();

    private sealed class ObstacleCardDragOrigin
    {
        public Transform parent;
        public int siblingIndex;
        public Vector2 anchoredPosition;
        public Vector3 localScale;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAutoSpawn()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        SpawnIfWorldScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SpawnIfWorldScene();
    }

    private static void SpawnIfWorldScene()
    {
        if (FindAnyObjectByType<WorldVisitHud>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<WorldSaveSystem>() == null) return;

        RectTransform root = EconomyHUD.CreateRect("WorldVisitHud", FindScreenCanvas(SceneManager.GetActiveScene()));
        EconomyHUD.Stretch(root);
        root.gameObject.AddComponent<WorldVisitHud>();
    }

    private static bool MapAvailable => Application.CanStreamedLevelBeLoaded(WorldMapController.SceneName);

    private void Awake()
    {
        if (VisitSession.Active) BuildVisitPanel();
        else BuildOwnPanel();
    }

    private void LateUpdate()
    {
        FitToCanvas();
    }

    private void Start()
    {
        if (VisitSession.Active)
        {
            BuildObstacleCards();
            StartCoroutine(LoadObstacleFee());
        }
    }

    private void OnDisable()
    {
        SetObstacleCardDragging(false);
    }

    private void Update()
    {
        if (!VisitSession.Active || sendingObstacle) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && obstacleCardDragOrigins.Count > 0)
        {
            foreach (var pair in new List<KeyValuePair<GameObject, ObstacleCardDragOrigin>>(obstacleCardDragOrigins))
                RestoreObstacleCard(pair.Key, pair.Value);
            obstacleCardDragOrigins.Clear();
            SetObstacleCardDragging(false);
        }
    }

    // =========================================================
    // ĐẢO CỦA MÌNH: CHỈ CÒN NÚT WORLD MAP
    // =========================================================

    private void BuildOwnPanel()
    {
        RectTransform panel = CreatePanel(new Vector2(ColumnWidth, 112f));

        AddButton(panel, "World map", new Vector2(12f, -12f), new Vector2(ColumnWidth - 24f, 44f), AccentColor, OpenWorldMap);

        statusText = CreateLine(panel, new Vector2(14f, -64f), new Vector2(ColumnWidth - 28f, 40f));
        statusText.text = MapAvailable
            ? "See other islands, visit them and share yours."
            : "World map scene is missing: run Tools > LumiWorld > Tạo scene WorldMap.";
    }

    private void OpenWorldMap()
    {
        if (!MapAvailable)
        {
            statusText.text = "World map scene is missing: run Tools > LumiWorld > Tạo scene WorldMap.";
            return;
        }
        WorldMapController.Open();
    }

    // =========================================================
    // ĐANG THĂM ĐẢO NGƯỜI KHÁC
    // =========================================================

    private void BuildVisitPanel()
    {
        RectTransform panel = CreatePanel(new Vector2(ColumnWidth, 308f));

        TextMeshProUGUI title = EconomyHUD.CreateText("Title", panel, 22f, TextColor, TextAlignmentOptions.Left);
        SetTopLeft(title.rectTransform, new Vector2(14f, -10f), new Vector2(ColumnWidth - 28f, 30f));
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.text = VisitSession.OwnerName + "'s island";

        TextMeshProUGUI line = CreateLine(panel, new Vector2(14f, -42f), new Vector2(ColumnWidth - 28f, 48f));
        line.text = "Score " + VisitSession.OwnerScore + " pts  ·  [" + VisitSession.OwnerX + ":" + VisitSession.OwnerY + "]"
            + "\nDrag an obstacle card from your hand onto this island.";

        float half = (ColumnWidth - 36f) / 2f;
        bool backToMap = VisitSession.FromWorldMap && MapAvailable;
        AddButton(panel, backToMap ? "Back to map" : "Back", new Vector2(12f, -98f), new Vector2(half, 42f), AccentColor, VisitSession.Leave);
        AddButton(panel, "Home", new Vector2(24f + half, -98f), new Vector2(half, 42f), DangerColor, GoHome);
        AddButton(panel, "Listings", new Vector2(12f, -150f), new Vector2(ColumnWidth - 24f, 42f), NeutralColor,
            () => listings = ListingsWindow.Open(listings, transform, VisitSession.OwnerId, VisitSession.OwnerName));

        visitStatusText = CreateLine(panel, new Vector2(14f, -198f), new Vector2(ColumnWidth - 28f, 64f));
        visitStatusText.text = "Choose a card, then drop it on the target hex.";
        retryObstacleButton = AddButton(panel, "Retry", new Vector2(ColumnWidth - 78f, -266f),
            new Vector2(64f, 30f), NeutralColor, SendPendingObstacle);
        retryObstacleButton.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator LoadObstacleFee()
    {
        GameConfigDto config = null;
        ApiError error = null;
        yield return ApiClient.Get<GameConfigDto>("/api/v1/config", false, value => config = value, value => error = value);
        if (config != null)
        {
            obstacleFeeCoins = Mathf.Max(0, config.obstacleFeeCoins);
            if (config.obstacleAllowedKinds != null && config.obstacleAllowedKinds.Length > 0)
                allowedObstacleKinds = config.obstacleAllowedKinds;
            BuildObstacleCards();
        }
        else if (error != null && !error.IsNetworkError)
        {
            SetVisitStatus("Could not load obstacle price: " + MarketSync.Describe(error));
        }
    }

    private void BuildObstacleCards()
    {
        CardDeskController desk = CardDeskController.Instance;
        RectTransform hand = desk != null ? desk.HandCardContainer : null;
        GameObject prefab = desk != null ? desk.HandCardPrefab : null;
        if (hand == null || prefab == null) return;

        foreach (GameObject oldCard in obstacleCardKinds.Keys)
            if (oldCard != null) Destroy(oldCard);
        obstacleCardKinds.Clear();

        if (allowedObstacleKinds == null || allowedObstacleKinds.Length == 0) return;
        Vector2[] positions = desk.CalculateHandLayout(allowedObstacleKinds.Length, out float scale);
        for (int i = 0; i < allowedObstacleKinds.Length; i++)
        {
            string kind = allowedObstacleKinds[i];
            if (string.IsNullOrWhiteSpace(kind)) continue;

            GameObject cardObject = Instantiate(prefab, hand, false);
            cardObject.name = "VisitObstacleCard_" + kind;
            RectTransform card = cardObject.GetComponent<RectTransform>();
            if (card == null)
            {
                Destroy(cardObject);
                continue;
            }
            card.anchoredPosition = positions[i];
            card.localScale = Vector3.one * scale;

            // Keep the CardDesk prefab and its Sprite-0001 face, but make this a temporary visit card.
            DisableCardGameplayComponents(cardObject);
            Image background = cardObject.GetComponent<Image>();
            if (background != null) background.color = ObstacleCardColor(kind);

            TextMeshProUGUI title = EconomyHUD.CreateText("VisitObstacleName", card, 18f, TextColor, TextAlignmentOptions.Center);
            SetTopLeft(title.rectTransform, new Vector2(8f, -48f), new Vector2(card.sizeDelta.x - 16f, 62f));
            title.text = DisplayObstacleKind(kind);
            title.textWrappingMode = TextWrappingModes.Normal;
            title.raycastTarget = false;

            TextMeshProUGUI fee = EconomyHUD.CreateText("VisitObstacleFee", card, 14f, new Color(1f, 0.84f, 0.35f), TextAlignmentOptions.Center);
            SetTopLeft(fee.rectTransform, new Vector2(8f, -137f), new Vector2(card.sizeDelta.x - 16f, 26f));
            fee.text = $"{obstacleFeeCoins} Coins";
            fee.raycastTarget = false;

            CanvasGroup group = cardObject.GetComponent<CanvasGroup>();
            if (group == null) group = cardObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            obstacleCardKinds[cardObject] = kind;
            AddObstacleCardDragEvents(cardObject);
        }
    }

    private static void DisableCardGameplayComponents(GameObject card)
    {
        CardUI cardUI = card.GetComponent<CardUI>();
        if (cardUI != null) cardUI.enabled = false;
        CardDrag cardDrag = card.GetComponent<CardDrag>();
        if (cardDrag != null) cardDrag.enabled = false;
        CardHover hover = card.GetComponent<CardHover>();
        if (hover != null) hover.enabled = false;
        CardHandSlot slot = card.GetComponent<CardHandSlot>();
        if (slot != null) slot.enabled = false;
        CardAppear appear = card.GetComponent<CardAppear>();
        if (appear != null) appear.enabled = false;
        CardMoveToPosition mover = card.GetComponent<CardMoveToPosition>();
        if (mover != null) mover.enabled = false;
    }

    private static Color ObstacleCardColor(string kind)
    {
        if (kind == "ForestFire") return new Color(1f, 0.55f, 0.34f);
        if (kind == "Cyclone") return new Color(0.48f, 0.75f, 1f);
        if (kind == "Drought") return new Color(1f, 0.83f, 0.40f);
        if (kind == "BarrenLand") return new Color(0.77f, 0.61f, 0.43f);
        return new Color(0.75f, 0.79f, 0.86f);
    }

    private void AddObstacleCardDragEvents(GameObject card)
    {
        EventTrigger trigger = card.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();
        AddEventTrigger(trigger, EventTriggerType.BeginDrag, data => BeginObstacleCardDrag(card, (PointerEventData)data));
        AddEventTrigger(trigger, EventTriggerType.Drag, data => DragObstacleCard(card, (PointerEventData)data));
        AddEventTrigger(trigger, EventTriggerType.EndDrag, data => EndObstacleCardDrag(card, (PointerEventData)data));
    }

    private static void AddEventTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => callback(data));
        trigger.triggers.Add(entry);
    }

    private void BeginObstacleCardDrag(GameObject card, PointerEventData eventData)
    {
        if (!VisitSession.Active || sendingObstacle || pendingObstacle != null ||
            eventData.button != PointerEventData.InputButton.Left || !obstacleCardKinds.ContainsKey(card)) return;

        RectTransform rect = card.GetComponent<RectTransform>();
        CanvasGroup group = card.GetComponent<CanvasGroup>();
        if (rect == null || group == null) return;

        obstacleCardDragOrigins[card] = new ObstacleCardDragOrigin
        {
            parent = card.transform.parent,
            siblingIndex = card.transform.GetSiblingIndex(),
            anchoredPosition = rect.anchoredPosition,
            localScale = rect.localScale
        };
        SetObstacleCardDragging(true);
        card.transform.SetParent(canvas != null ? canvas.transform : transform, true);
        card.transform.SetAsLastSibling();
        group.blocksRaycasts = false;
        rect.localScale = Vector3.one * 1.08f;
        DragObstacleCard(card, eventData);
        SetVisitStatus($"Dragging {DisplayObstacleKind(obstacleCardKinds[card])}. Drop it on a hex.");
    }

    private void DragObstacleCard(GameObject card, PointerEventData eventData)
    {
        if (!obstacleCardDragOrigins.ContainsKey(card)) return;
        RectTransform root = canvas != null ? canvas.transform as RectTransform : transform as RectTransform;
        RectTransform rect = card.GetComponent<RectTransform>();
        if (root == null || rect == null) return;

        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(root, eventData.position, eventCamera, out Vector3 world))
            rect.position = world;
    }

    private void EndObstacleCardDrag(GameObject card, PointerEventData eventData)
    {
        if (!obstacleCardDragOrigins.TryGetValue(card, out ObstacleCardDragOrigin origin)) return;
        string kind = obstacleCardKinds.TryGetValue(card, out string value) ? value : "Obstacle";
        bool hasTarget = TryGetTargetTile(eventData.position, out HexCoordinates target);
        RestoreObstacleCard(card, origin);
        obstacleCardDragOrigins.Remove(card);
        if (obstacleCardDragOrigins.Count == 0) SetObstacleCardDragging(false);

        if (!hasTarget)
        {
            SetVisitStatus("Drop the obstacle card directly onto an island hex.");
            return;
        }

        SendObstacleToTarget(kind, target);
    }

    private void RestoreObstacleCard(GameObject card, ObstacleCardDragOrigin origin)
    {
        if (card == null || origin == null) return;
        card.transform.SetParent(origin.parent, false);
        card.transform.SetSiblingIndex(Mathf.Clamp(origin.siblingIndex, 0, card.transform.parent.childCount - 1));
        RectTransform rect = card.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = origin.anchoredPosition;
            rect.localScale = origin.localScale;
        }
        CanvasGroup group = card.GetComponent<CanvasGroup>();
        if (group != null) group.blocksRaycasts = true;
    }

    private void SetObstacleCardDragging(bool dragging)
    {
        if (cameraController == null) cameraController = FindAnyObjectByType<HexCameraController>();
        if (cameraController != null) cameraController.SetCardDragging(dragging);
    }

    private void SendObstacleToTarget(string kind, HexCoordinates target)
    {
        CurrencyWallet wallet = CurrencyWallet.Instance;
        if (wallet == null || !wallet.CanAfford(obstacleFeeCoins))
        {
            SetVisitStatus($"Not enough Coins. Sending {DisplayObstacleKind(kind)} costs {obstacleFeeCoins} Coins.");
            return;
        }

        pendingObstacle = new ObstacleSendRequest { kind = kind, q = target.Q, r = target.R };
        pendingObstacleKey = System.Guid.NewGuid().ToString("N");
        if (!wallet.TrySpend(obstacleFeeCoins, CoinReason.ObstacleSend))
        {
            pendingObstacle = null;
            pendingObstacleKey = null;
            SetVisitStatus($"Could not spend {obstacleFeeCoins} Coins for {DisplayObstacleKind(kind)}.");
            return;
        }

        pendingObstacleFeePaid = true;
        SendPendingObstacle();
    }

    private bool TryGetTargetTile(Vector2 screenPosition, out HexCoordinates target)
    {
        target = default;
        if (visitedWorld == null) visitedWorld = FindAnyObjectByType<HexWorldGenerator>();
        Camera camera = Camera.main;
        if (visitedWorld == null || camera == null) return false;

        Ray ray = camera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        float closest = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].distance >= closest) continue;
            Transform current = hits[i].collider != null ? hits[i].collider.transform : null;
            while (current != null)
            {
                HexTileInfo info = current.GetComponent<HexTileInfo>();
                if (info != null && visitedWorld.MapTiles.TryGetValue(info.coordinates, out GameObject tile) && tile != null)
                {
                    closest = hits[i].distance;
                    target = info.coordinates;
                    found = true;
                    break;
                }
                current = current.parent;
            }
        }
        return found;
    }

    private void SendPendingObstacle()
    {
        if (pendingObstacle == null || sendingObstacle) return;
        ObstacleSendRequest sentObstacle = pendingObstacle;
        string idempotencyKey = pendingObstacleKey;
        sendingObstacle = true;
        if (retryObstacleButton != null) retryObstacleButton.interactable = false;
        string sentName = DisplayObstacleKind(sentObstacle.kind);
        SetVisitStatus($"Sending {sentName} to [{sentObstacle.q}:{sentObstacle.r}]...");
        StartCoroutine(WorldShareService.SendObstacle(
            VisitSession.OwnerId,
            sentObstacle.kind,
            sentObstacle.q,
            sentObstacle.r,
            idempotencyKey,
            response =>
            {
                sendingObstacle = false;
                pendingObstacle = null;
                pendingObstacleKey = null;
                pendingObstacleFeePaid = false;
                if (retryObstacleButton != null)
                {
                    retryObstacleButton.interactable = true;
                    retryObstacleButton.gameObject.SetActive(false);
                }
                string id = response != null ? response.obstacleId : null;
                SetVisitStatus(string.IsNullOrEmpty(id)
                    ? $"{sentName} sent to {VisitSession.OwnerName}'s island at [{sentObstacle.q}:{sentObstacle.r}]. Charged {obstacleFeeCoins} Coins."
                    : $"{sentName} #{id} sent to {VisitSession.OwnerName}'s inbox. Charged {obstacleFeeCoins} Coins.");
            },
            error =>
            {
                sendingObstacle = false;
                if (retryObstacleButton != null) retryObstacleButton.interactable = true;
                if (error != null && error.IsNetworkError)
                {
                    if (retryObstacleButton != null) retryObstacleButton.gameObject.SetActive(true);
                    SetVisitStatus("Connection uncertain. Retry to safely resend the same request.");
                    return;
                }

                RefundPendingObstacleFee();
                pendingObstacle = null;
                pendingObstacleKey = null;
                if (retryObstacleButton != null) retryObstacleButton.gameObject.SetActive(false);
                SetVisitStatus(MarketSync.Describe(error));
            }));
    }

    private void RefundPendingObstacleFee()
    {
        if (!pendingObstacleFeePaid) return;
        CurrencyWallet wallet = CurrencyWallet.Instance;
        if (wallet != null) wallet.Add(obstacleFeeCoins, CoinReason.ObstacleRefund);
        pendingObstacleFeePaid = false;
    }

    private static string DisplayObstacleKind(string kind)
    {
        if (System.Enum.TryParse(kind, true, out LumiWorld.Acs.DisasterKind disasterKind) &&
            System.Enum.IsDefined(typeof(LumiWorld.Acs.DisasterKind), disasterKind))
            return LumiWorld.Acs.DisasterCatalog.DisplayName(disasterKind);
        return string.IsNullOrWhiteSpace(kind) ? "Obstacle" : kind;
    }

    private void SetVisitStatus(string value)
    {
        if (visitStatusText != null) visitStatusText.text = value;
    }

    private static void GoHome()
    {
        // Kết thúc chuyến thăm trước khi vào đảo của mình để WorldSaveSystem ghi lại bình thường
        VisitSession.End();
        SceneManager.LoadScene(VisitSession.OwnScene);
    }

    // =========================================================
    // DỰNG UI
    // =========================================================

    private RectTransform CreatePanel(Vector2 size)
    {
        RectTransform panel = EconomyHUD.CreateRect("WorldPanel", transform);
        SetTopLeft(panel, new Vector2(ColumnX, -PanelTop), size);
        EconomyHUD.CreatePanel(panel, PanelColor).raycastTarget = true;
        return panel;
    }

    private static TextMeshProUGUI CreateLine(RectTransform parent, Vector2 position, Vector2 size)
    {
        TextMeshProUGUI line = EconomyHUD.CreateText("Line", parent, 16f, MutedColor, TextAlignmentOptions.Left);
        SetTopLeft(line.rectTransform, position, size);
        line.textWrappingMode = TextWrappingModes.Normal;
        return line;
    }

    private static Button AddButton(RectTransform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = EconomyHUD.CreateRect(text + "Button", parent);
        SetTopLeft(rect, position, size);
        Button button = EconomyHUD.AddButton(rect, color);
        button.onClick.AddListener(onClick);

        TextMeshProUGUI label = EconomyHUD.CreateText("Label", rect, 18f, TextColor, TextAlignmentOptions.Center);
        EconomyHUD.Stretch(label.rectTransform);
        label.text = text;
        return button;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void FitToCanvas()
    {
        if (canvas == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;
            canvas = parentCanvas.rootCanvas;
        }

        float canvasScale = canvas.scaleFactor;
        Vector2 canvasSize = ((RectTransform)canvas.transform).rect.size;
        if (canvasScale <= 0f || canvasSize.x <= 0f || canvasSize.y <= 0f) return;
        if (canvasScale == fittedCanvasScale && canvasSize == fittedCanvasSize) return;

        fittedCanvasScale = canvasScale;
        fittedCanvasSize = canvasSize;

        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = RootOffset;
        root.sizeDelta = canvasSize * canvasScale;
        root.localScale = new Vector3(1f / canvasScale, 1f / canvasScale, 1f);
    }

    private static Transform FindScreenCanvas(Scene scene)
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>())
        {
            if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace && canvas.gameObject.scene == scene)
                return canvas.transform;
        }

        // Scene chưa có Canvas màn hình: tạo một Canvas riêng
        GameObject canvasObject = new GameObject("WorldVisitCanvas", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);

        Canvas ownCanvas = canvasObject.AddComponent<Canvas>();
        ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ownCanvas.sortingOrder = 45;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject.transform;
    }
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Temporary obstacle hand used only while visiting another player's island.
/// It is separate from the saved player hand and never invokes normal card placement.
/// </summary>
public sealed class VisitObstacleHandController : MonoBehaviour
{
    private static readonly string[] DefaultKinds =
        { "Obstacle", "BarrenLand", "Drought", "Cyclone", "ForestFire" };

    [SerializeField] private GameObject cardPrefab;

    private readonly Dictionary<GameObject, string> cardKinds = new();
    private readonly Dictionary<GameObject, DragOrigin> draggingCards = new();
    private RectTransform handRoot;
    private TextMeshProUGUI statusText;
    private Canvas rootCanvas;
    private HexWorldGenerator visitedWorld;
    private HexCameraController cameraController;
    private LineRenderer targetHighlight;
    private Material targetHighlightMaterial;
    private string[] allowedKinds = DefaultKinds;
    private int feeCoins = 20;
    private bool sending;
    private bool feePaid;
    private ObstacleSendRequest pendingRequest;
    private string pendingIdempotencyKey;
    private int pendingRetryCount;
    private const int MaxAutomaticRetries = 2;

    private sealed class DragOrigin
    {
        public Transform parent;
        public int siblingIndex;
        public Vector2 anchoredPosition;
        public Vector3 localScale;
    }

    private void Start()
    {
        if (!VisitSession.Active || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != VisitSession.VisitScene)
        {
            enabled = false;
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas == null || cardPrefab == null)
        {
            Debug.LogError("[LumiWorld Visit] Obstacle hand needs a Canvas and card prefab.", this);
            enabled = false;
            return;
        }

        BuildHandRoot();
        StartCoroutine(LoadConfigAndBuildCards());
    }

    private void OnDisable()
    {
        foreach (var pair in new List<KeyValuePair<GameObject, DragOrigin>>(draggingCards))
            RestoreCard(pair.Key, pair.Value);
        draggingCards.Clear();
        HideTargetHighlight();
        SetCameraDragging(false);
    }

    private void OnDestroy()
    {
        if (targetHighlightMaterial != null)
        {
            if (Application.isPlaying) Destroy(targetHighlightMaterial);
            else DestroyImmediate(targetHighlightMaterial);
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && draggingCards.Count > 0)
        {
            foreach (var pair in new List<KeyValuePair<GameObject, DragOrigin>>(draggingCards))
                RestoreCard(pair.Key, pair.Value);
            draggingCards.Clear();
            HideTargetHighlight();
            SetCameraDragging(false);
        }
    }

    private void BuildHandRoot()
    {
        handRoot = EconomyHUD.CreateRect("VisitObstacleHand", transform);
        RectTransform root = handRoot;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, 10f);
        root.sizeDelta = new Vector2(900f, 280f);

        statusText = EconomyHUD.CreateText("ObstacleStatus", root, 45f,
            new Color(0.9f, 0.93f, 1f), TextAlignmentOptions.Center);
        SetBottomCenter(statusText.rectTransform, new Vector2(0f, 500f), new Vector2(760f, 32f));
        statusText.text = "Loading obstacle cards...";
        statusText.raycastTarget = false;

    }

    private IEnumerator LoadConfigAndBuildCards()
    {
        GameConfigDto config = null;
        ApiError error = null;
        yield return ApiClient.Get<GameConfigDto>("/api/v1/config", false,
            value => config = value, value => error = value);

        if (config != null)
        {
            feeCoins = Mathf.Max(0, config.obstacleFeeCoins);
            if (config.obstacleAllowedKinds != null)
                allowedKinds = config.obstacleAllowedKinds;
            BuildCards();
            SetStatus(allowedKinds.Length == 0 ? "No obstacle cards are enabled." : "Drag an obstacle card onto an island hex.");
            yield break;
        }

        BuildCards();
        SetStatus(error != null && error.IsNetworkError
            ? "Could not load obstacle settings. Showing the default cards."
            : "Could not load obstacle settings: " + MarketSync.Describe(error));
    }

    private void BuildCards()
    {
        ClearCards();
        if (allowedKinds == null || allowedKinds.Length == 0) return;

        const float cardWidth = 100f;
        const float maxHandWidth = 1300f;
        float scale = Mathf.Min(1.8f, maxHandWidth / (allowedKinds.Length * cardWidth));
        float spacing = allowedKinds.Length <= 1
            ? 0f
            : Mathf.Min(300f * scale, (maxHandWidth - cardWidth * scale) / (allowedKinds.Length - 1));
        float firstX = -(allowedKinds.Length - 1) * spacing * 0.5f;

        for (int i = 0; i < allowedKinds.Length; i++)
        {
            string kind = allowedKinds[i];
            if (string.IsNullOrWhiteSpace(kind)) continue;

            GameObject card = Instantiate(cardPrefab, handRoot, false);
            card.name = "VisitObstacleCard_" + kind;

            RectTransform rect = card.GetComponent<RectTransform>();
            if (rect == null)
            {
                Destroy(card);
                continue;
            }
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(firstX + i * spacing, 22f);
            rect.localScale = Vector3.one * scale;

            Image face = card.GetComponent<Image>();
            if (face != null)
            {
                face.color = CardColor(kind);
            }

            TextMeshProUGUI title = EconomyHUD.CreateText("ObstacleName", rect, 30f,
                Color.white, TextAlignmentOptions.Center);
            SetTopCenter(title.rectTransform, new Vector2(0f, -46f), new Vector2(136f, 64f));
            title.text = DisplayName(kind);
            title.textWrappingMode = TextWrappingModes.Normal;
            title.raycastTarget = false;

            TextMeshProUGUI fee = EconomyHUD.CreateText("ObstacleFee", rect, 27f,
                new Color(1f, 0.84f, 0.35f), TextAlignmentOptions.Center);
            SetBottomCenter(fee.rectTransform, new Vector2(0f, 16f), new Vector2(140f, 24f));
            fee.text = $"{feeCoins} Coins";
            fee.raycastTarget = false;

            CanvasGroup group = card.GetComponent<CanvasGroup>();
            if (group == null) group = card.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            cardKinds[card] = kind;
            AddDragEvents(card);
        }
    }

    private void ClearCards()
    {
        foreach (GameObject card in cardKinds.Keys)
            if (card != null) Destroy(card);
        cardKinds.Clear();
    }

    private void AddDragEvents(GameObject card)
    {
        EventTrigger trigger = card.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();
        AddEventTrigger(trigger, EventTriggerType.BeginDrag, data => BeginDragCard(card, (PointerEventData)data));
        AddEventTrigger(trigger, EventTriggerType.Drag, data => MoveDraggedCard(card, ((PointerEventData)data).position));
        AddEventTrigger(trigger, EventTriggerType.EndDrag, data => EndDragCard(card, (PointerEventData)data));
    }

    private void AddEventTrigger(EventTrigger trigger, EventTriggerType type,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private void BeginDragCard(GameObject card, PointerEventData eventData)
    {
        if (!VisitSession.Active || sending || pendingRequest != null ||
            eventData.button != PointerEventData.InputButton.Left || !cardKinds.ContainsKey(card)) return;

        RectTransform rect = card.GetComponent<RectTransform>();
        CanvasGroup group = card.GetComponent<CanvasGroup>();
        if (rect == null || group == null) return;

        draggingCards[card] = new DragOrigin
        {
            parent = card.transform.parent,
            siblingIndex = card.transform.GetSiblingIndex(),
            anchoredPosition = rect.anchoredPosition,
            localScale = rect.localScale
        };
        SetCameraDragging(true);
        card.transform.SetParent(rootCanvas.transform, true);
        card.transform.SetAsLastSibling();
        group.blocksRaycasts = false;
        rect.localScale = Vector3.one * 1.05f;
        MoveDraggedCard(card, eventData.position);
        SetStatus($"Dragging {DisplayName(cardKinds[card])}. Drop it on a hex.");
    }

    private void MoveDraggedCard(GameObject card, Vector2 screenPosition)
    {
        if (!draggingCards.ContainsKey(card)) return;
        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        RectTransform cardRect = card.GetComponent<RectTransform>();
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (canvasRect != null && cardRect != null &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector3 world))
            cardRect.position = world;

        UpdateTargetHighlight(screenPosition);
    }

    private void EndDragCard(GameObject card, PointerEventData eventData)
    {
        if (!draggingCards.TryGetValue(card, out DragOrigin origin)) return;
        string kind = cardKinds.TryGetValue(card, out string value) ? value : "Obstacle";
        bool hasTarget = TryGetTargetTile(eventData.position, out HexCoordinates target);
        RestoreCard(card, origin);
        draggingCards.Remove(card);
        HideTargetHighlight();
        if (draggingCards.Count == 0) SetCameraDragging(false);

        if (!hasTarget)
        {
            SetStatus("Drop the obstacle card directly onto an island hex.");
            return;
        }
        SendObstacleToTarget(kind, target);
    }

    private void UpdateTargetHighlight(Vector2 screenPosition)
    {
        if (!TryGetTargetTile(screenPosition, out HexCoordinates coordinates) ||
            !visitedWorld.MapTiles.TryGetValue(coordinates, out GameObject tile) || tile == null)
        {
            HideTargetHighlight();
            return;
        }

        EnsureTargetHighlight();
        if (targetHighlight == null) return;

        var (topY, _, _) = HexBiomeClusterConnector.GetTileHeightLevels(tile);
        Vector3 center = tile.transform.position;
        center.y = topY + 0.08f;
        float radius = HexMetrics.OuterRadius * 0.88f;
        const int cornerCount = 6;
        const float angleOffset = 30f;
        for (int i = 0; i <= cornerCount; i++)
        {
            float angle = (angleOffset + i * 60f) * Mathf.Deg2Rad;
            targetHighlight.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
        targetHighlight.enabled = true;
    }

    private void EnsureTargetHighlight()
    {
        if (targetHighlight != null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return;

        targetHighlightMaterial = new Material(shader) { name = "VisitObstacleTargetHighlightMaterial" };
        targetHighlightMaterial.color = new Color(0.25f, 1f, 0.65f, 1f);
        GameObject ring = new GameObject("VisitObstacleTargetHighlight", typeof(LineRenderer));
        targetHighlight = ring.GetComponent<LineRenderer>();
        targetHighlight.useWorldSpace = true;
        targetHighlight.loop = false;
        targetHighlight.positionCount = 7;
        targetHighlight.startWidth = 0.09f;
        targetHighlight.endWidth = 0.09f;
        targetHighlight.startColor = targetHighlightMaterial.color;
        targetHighlight.endColor = targetHighlightMaterial.color;
        targetHighlight.sharedMaterial = targetHighlightMaterial;
        targetHighlight.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        targetHighlight.receiveShadows = false;
        targetHighlight.enabled = false;
    }

    private void HideTargetHighlight()
    {
        if (targetHighlight != null) targetHighlight.enabled = false;
    }

    private void RestoreCard(GameObject card, DragOrigin origin)
    {
        if (card == null || origin == null || origin.parent == null) return;
        card.transform.SetParent(origin.parent, false);
        card.transform.SetSiblingIndex(Mathf.Clamp(origin.siblingIndex, 0, origin.parent.childCount - 1));
        RectTransform rect = card.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = origin.anchoredPosition;
            rect.localScale = origin.localScale;
        }
        CanvasGroup group = card.GetComponent<CanvasGroup>();
        if (group != null) group.blocksRaycasts = true;
    }

    private bool TryGetTargetTile(Vector2 screenPosition, out HexCoordinates target)
    {
        target = default;
        if (visitedWorld == null) visitedWorld = FindAnyObjectByType<HexWorldGenerator>();
        Camera camera = Camera.main;
        if (visitedWorld == null || camera == null) return false;

        RaycastHit[] hits = Physics.RaycastAll(camera.ScreenPointToRay(screenPosition), 1000f);
        float closest = float.PositiveInfinity;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.distance >= closest) continue;
            Transform current = hit.collider != null ? hit.collider.transform : null;
            while (current != null)
            {
                HexTileInfo info = current.GetComponent<HexTileInfo>();
                if (info != null && visitedWorld.MapTiles.TryGetValue(info.coordinates, out GameObject tile) && tile != null)
                {
                    closest = hit.distance;
                    target = info.coordinates;
                    found = true;
                    break;
                }
                current = current.parent;
            }
        }
        return found;
    }

    private void SendObstacleToTarget(string kind, HexCoordinates target)
    {
        CurrencyWallet wallet = CurrencyWallet.Instance;
        if (wallet == null || !wallet.CanAfford(feeCoins))
        {
            SetStatus($"Not enough Coins. Sending {DisplayName(kind)} costs {feeCoins} Coins.");
            return;
        }

        pendingRequest = new ObstacleSendRequest { kind = kind, q = target.Q, r = target.R };
        pendingIdempotencyKey = System.Guid.NewGuid().ToString("N");
        pendingRetryCount = 0;
        if (!wallet.TrySpend(feeCoins, CoinReason.ObstacleSend))
        {
            pendingRequest = null;
            pendingIdempotencyKey = null;
            SetStatus($"Could not spend {feeCoins} Coins for {DisplayName(kind)}.");
            return;
        }
        feePaid = true;
        SendPendingObstacle();
    }

    private void SendPendingObstacle()
    {
        if (pendingRequest == null || sending) return;
        ObstacleSendRequest sent = pendingRequest;
        string idempotencyKey = pendingIdempotencyKey;
        sending = true;
        string name = DisplayName(sent.kind);
        SetStatus($"Sending {name} to [{sent.q}:{sent.r}]...");

        StartCoroutine(WorldShareService.SendObstacle(VisitSession.OwnerId, sent.kind, sent.q, sent.r,
            idempotencyKey,
            response =>
            {
                sending = false;
                pendingRequest = null;
                pendingIdempotencyKey = null;
                pendingRetryCount = 0;
                feePaid = false;
                string id = response != null ? response.obstacleId : null;
                SetStatus(string.IsNullOrEmpty(id)
                    ? $"{name} sent to {VisitSession.OwnerName}'s island at [{sent.q}:{sent.r}]. Charged {feeCoins} Coins."
                    : $"{name} #{id} sent to {VisitSession.OwnerName}'s inbox. Charged {feeCoins} Coins.");
            },
            error =>
            {
                sending = false;
                if (error != null && error.IsNetworkError)
                {
                    if (pendingRetryCount < MaxAutomaticRetries)
                    {
                        pendingRetryCount++;
                        SetStatus($"Connection interrupted. Retrying automatically ({pendingRetryCount}/{MaxAutomaticRetries})...");
                        StartCoroutine(RetryPendingObstacle());
                        return;
                    }

                    // Keep the request and idempotency key so this client cannot accidentally
                    // charge again or create a duplicate after an uncertain server response.
                    SetStatus("Could not confirm the result. The request is kept pending; avoid sending another obstacle.");
                    return;
                }
                RefundFee();
                pendingRequest = null;
                pendingIdempotencyKey = null;
                pendingRetryCount = 0;
                SetStatus(MarketSync.Describe(error));
            }));
    }

    private IEnumerator RetryPendingObstacle()
    {
        yield return new WaitForSecondsRealtime(1.25f);
        SendPendingObstacle();
    }

    private void RefundFee()
    {
        if (!feePaid) return;
        CurrencyWallet.Instance?.Add(feeCoins, CoinReason.ObstacleRefund);
        feePaid = false;
    }

    private void SetCameraDragging(bool dragging)
    {
        if (cameraController == null) cameraController = FindAnyObjectByType<HexCameraController>();
        if (cameraController != null) cameraController.SetCardDragging(dragging);
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private static string DisplayName(string kind)
    {
        if (System.Enum.TryParse(kind, true, out LumiWorld.Acs.DisasterKind disasterKind) &&
            System.Enum.IsDefined(typeof(LumiWorld.Acs.DisasterKind), disasterKind))
            return LumiWorld.Acs.DisasterCatalog.DisplayName(disasterKind);
        return string.IsNullOrWhiteSpace(kind) ? "Obstacle" : kind;
    }

    private static Color CardColor(string kind)
    {
        if (kind == "ForestFire") return new Color(1f, 0.55f, 0.34f);
        if (kind == "Cyclone") return new Color(0.48f, 0.75f, 1f);
        if (kind == "Drought") return new Color(1f, 0.83f, 0.40f);
        if (kind == "BarrenLand") return new Color(0.77f, 0.61f, 0.43f);
        return new Color(0.75f, 0.79f, 0.86f);
    }

    private static void SetTopCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetBottomCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}

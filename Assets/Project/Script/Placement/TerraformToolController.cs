using System.Collections.Generic;
using LumiWorld.Acs;
using LumiWorld.Acs.Islands;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Manages click-to-toggle multi-tile Raise / Lower Land placement on Wet Island.
/// Players click individual tiles to queue elevation changes (+1 or -1) and confirm total cost.
/// </summary>
public class TerraformToolController : MonoBehaviour
{
    private static TerraformToolController instance;
    private static bool quitting;

    public static bool IsActiveToolMode => instance != null && instance.isActive;

    public static TerraformToolController Instance
    {
        get
        {
            if (quitting) return null;
            if (instance == null)
            {
                instance = FindAnyObjectByType<TerraformToolController>();
                if (instance == null && !quitting)
                {
                    var go = new GameObject("TerraformToolController");
                    instance = go.AddComponent<TerraformToolController>();
                }
            }
            return instance;
        }
    }

    private static readonly Color RaiseSelectedColor = new Color(0.28f, 0.86f, 0.44f, 0.56f);
    private static readonly Color LowerSelectedColor = new Color(0.22f, 0.68f, 0.96f, 0.56f);
    private static readonly Color HoverInvalidColor  = new Color(0.92f, 0.24f, 0.24f, 0.50f);
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorProp = Shader.PropertyToID("_Color");

    private bool isActive;
    private CardUI activeCardUI;
    private CardData activeCardData;
    private int activeDirection = 1;
    private bool canRaise = true;
    private bool canLower = true;

    private readonly HashSet<HexCoordinates> selectedHexes = new HashSet<HexCoordinates>();
    private readonly Dictionary<HexCoordinates, GameObject> selectedShells = new Dictionary<HexCoordinates, GameObject>();

    private HexCoordinates? hoverHex;
    private GameObject hoverShell;
    private bool hoverValid;
    private int hoverEmptyElev = -1;

    private bool pointerDownOnMap;
    private Vector2 pointerDownPos;
    private string statusHint = string.Empty;
    private float statusHintUntil;

    private HexWorldGenerator worldGenerator;
    private HexPointerRaycaster raycaster;
    private Camera mainCamera;
    private Material highlightMaterial;
    private MaterialPropertyBlock propBlock;

    // Floating Warm-Stone HUD Bar
    private Canvas hudCanvas;
    private RectTransform hudBarRect;
    private Button raiseBtn;
    private Image raiseBtnBg;
    private TextMeshProUGUI raiseBtnLabel;
    private Button lowerBtn;
    private Image lowerBtnBg;
    private TextMeshProUGUI lowerBtnLabel;
    private TextMeshProUGUI infoText;
    private Button applyBtn;
    private Image applyBtnBg;
    private TextMeshProUGUI applyBtnLabel;
    private Button cancelBtn;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        propBlock = new MaterialPropertyBlock();
    }

    private void OnApplicationQuit() => quitting = true;

    private void OnDestroy()
    {
        ClearAllVisuals();
        if (highlightMaterial != null) Destroy(highlightMaterial);
        if (hudCanvas != null) Destroy(hudCanvas.gameObject);
        if (instance == this) instance = null;
    }

    private void OnDisable()
    {
        if (!quitting && isActive) Deactivate();
    }

    public void HandleCardClick(CardUI cardUI, PointerEventData eventData)
    {
        if (WorldLaunch.IsVisit || cardUI == null || cardUI.CardData == null || !cardUI.CardData.IsTerraformTool())
        {
            return;
        }

        if (!isActive || activeCardUI != cardUI)
        {
            Activate(cardUI);
            int zone = cardUI.GetTerraformClickZone(eventData);
            if (zone != 0) TrySetDirection(zone);
            return;
        }

        int clickZone = cardUI.GetTerraformClickZone(eventData);
        if (clickZone != 0 && clickZone != activeDirection)
        {
            if (TrySetDirection(clickZone)) return;
        }

        Deactivate();
    }

    public void Activate(CardUI cardUI)
    {
        if (isActive) Deactivate();
        if (cardUI == null || cardUI.CardData == null) return;

        if (HexSinglePlacementController.Instance != null && HexSinglePlacementController.Instance.IsPreviewing)
        {
            HexSinglePlacementController.Instance.CancelPreview();
        }
        if (HexPlacementController.Instance != null && HexPlacementController.Instance.IsInPlacementMode)
        {
            HexPlacementController.Instance.CancelPlacement();
        }

        EnsureSceneRefs();
        EnsureHudBuilt();

        isActive = true;
        activeCardUI = cardUI;
        activeCardData = cardUI.CardData;
        activeDirection = 1;
        selectedHexes.Clear();
        statusHint = string.Empty;
        pointerDownOnMap = false;

        RefreshDirectionConstraints();
        RefreshHud();
        if (hudBarRect != null) hudBarRect.gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        isActive = false;
        pointerDownOnMap = false;
        statusHint = string.Empty;
        if (activeCardUI != null)
        {
            activeCardUI.SetTerraformVisualState(false, 1, true, true);
        }
        activeCardUI = null;
        activeCardData = null;
        selectedHexes.Clear();
        ClearAllVisuals();
        if (hudBarRect != null) hudBarRect.gameObject.SetActive(false);
    }

    public bool TrySetDirection(int dir)
    {
        int nextDir = dir >= 0 ? 1 : -1;
        if (nextDir > 0 && !canRaise)
        {
            SetHint("At High elevation — Raise is unavailable.");
            return false;
        }
        if (nextDir < 0 && !canLower)
        {
            SetHint("At Low elevation — Lower is unavailable.");
            return false;
        }

        activeDirection = nextDir;
        RebuildSelectedShellColors();
        if (activeCardUI != null)
        {
            activeCardUI.SetTerraformVisualState(true, activeDirection, canRaise, canLower);
        }
        RefreshHud();
        return true;
    }

    private void Update()
    {
        if (!isActive) return;

        if (WorldLaunch.IsVisit || activeCardUI == null || activeCardData == null ||
            !activeCardUI.gameObject.activeInHierarchy || EconomyHUD.IsWindowOpen ||
            (HexPlacementController.Instance != null && HexPlacementController.Instance.IsInPlacementMode) ||
            (IslandRuntimeManager.Instance != null && IslandRuntimeManager.Instance.ActiveIslandId != "wet"))
        {
            Deactivate();
            return;
        }

        EnsureSceneRefs();

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Deactivate();
                return;
            }
            if (kb.tabKey.wasPressedThisFrame)
            {
                TrySetDirection(-activeDirection);
            }
            if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && selectedHexes.Count > 0)
            {
                ApplySelection();
                return;
            }
        }

        if (mouse == null) return;
        Vector2 mousePos = mouse.position.ReadValue();
        bool overHudOrUI = IsPointerOverHudOrUI(mousePos);

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pointerDownOnMap = !overHudOrUI;
            pointerDownPos = mousePos;
        }

        UpdateHoverPreview(mousePos, overHudOrUI);

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            bool altHeld = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
            if (pointerDownOnMap && !overHudOrUI && !altHeld && Vector2.Distance(mousePos, pointerDownPos) <= 6f)
            {
                HandleTileClick(mousePos);
            }
            pointerDownOnMap = false;
        }

        AnimateShells();
    }

    private void HandleTileClick(Vector2 mousePos)
    {
        if (raycaster == null || mainCamera == null || worldGenerator == null || worldGenerator.MapTiles == null)
        {
            return;
        }

        if (!raycaster.TryRaycastHex(mousePos, mainCamera, out HexCoordinates hex, out _) ||
            !worldGenerator.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null)
        {
            return;
        }

        if (selectedHexes.Contains(hex))
        {
            selectedHexes.Remove(hex);
            if (selectedShells.TryGetValue(hex, out GameObject shell))
            {
                if (shell != null) Destroy(shell);
                selectedShells.Remove(hex);
            }
            statusHint = string.Empty;
            RefreshDirectionConstraints();
            RefreshHud();
            return;
        }

        if (!IslandPlacementRules.IsEmptyWetTerraformableTile(activeCardData, hex, tileObj, out int elev, out string reason))
        {
            SetHint(reason);
            return;
        }

        if (selectedHexes.Count == 0)
        {
            if (elev >= 2)
            {
                activeDirection = -1;
                SetHint("High tile selected — switched to Lower.");
            }
            else if (elev <= 0)
            {
                activeDirection = 1;
                SetHint("Low tile selected — switched to Raise.");
            }
            else
            {
                statusHint = string.Empty;
            }
        }
        else if (!IslandPlacementRules.CanTerraformTile(activeCardData, hex, tileObj, activeDirection, out string stepReason))
        {
            SetHint(stepReason);
            return;
        }

        selectedHexes.Add(hex);
        Color color = activeDirection > 0 ? RaiseSelectedColor : LowerSelectedColor;
        selectedShells[hex] = CreateTileShell(tileObj, color, 0.048f, "TerraformSelected_" + hex.Q + "_" + hex.R);

        RefreshDirectionConstraints();
        RefreshHud();
    }

    private void UpdateHoverPreview(Vector2 mousePos, bool overHudOrUI)
    {
        if (overHudOrUI || raycaster == null || mainCamera == null || worldGenerator == null || worldGenerator.MapTiles == null)
        {
            ClearHoverVisual();
            if (hoverEmptyElev != -1)
            {
                hoverEmptyElev = -1;
                RefreshDirectionConstraints();
                RefreshHud();
            }
            return;
        }

        if (!raycaster.TryRaycastHex(mousePos, mainCamera, out HexCoordinates hex, out _) ||
            !worldGenerator.MapTiles.TryGetValue(hex, out GameObject tileObj) || tileObj == null)
        {
            ClearHoverVisual();
            if (hoverEmptyElev != -1)
            {
                hoverEmptyElev = -1;
                RefreshDirectionConstraints();
                RefreshHud();
            }
            return;
        }

        bool isEmptyWet = IslandPlacementRules.IsEmptyWetTerraformableTile(activeCardData, hex, tileObj, out int emptyElev, out _);
        int nextEmptyElev = isEmptyWet ? emptyElev : -1;
        bool validForDir = isEmptyWet && (selectedHexes.Count == 0 || (emptyElev + activeDirection >= 0 && emptyElev + activeDirection <= 2));

        bool changedHex = !hoverHex.HasValue || hoverHex.Value != hex || hoverShell == null || hoverValid != validForDir;
        if (changedHex)
        {
            ClearHoverVisual();
            if (!selectedHexes.Contains(hex))
            {
                Color c;
                if (!validForDir) c = HoverInvalidColor;
                else if (selectedHexes.Count == 0 && emptyElev >= 2) c = LowerSelectedColor;
                else if (selectedHexes.Count == 0 && emptyElev <= 0) c = RaiseSelectedColor;
                else c = activeDirection > 0 ? RaiseSelectedColor : LowerSelectedColor;
                hoverShell = CreateTileShell(tileObj, c, 0.042f, "TerraformHover_" + hex.Q + "_" + hex.R);
            }
            hoverHex = hex;
            hoverValid = validForDir;
        }

        if (hoverEmptyElev != nextEmptyElev)
        {
            hoverEmptyElev = nextEmptyElev;
            RefreshDirectionConstraints();
            RefreshHud();
        }
    }

    private void RefreshDirectionConstraints()
    {
        if (selectedHexes.Count > 0 && worldGenerator != null && worldGenerator.MapTiles != null)
        {
            bool allCanRaise = true;
            bool allCanLower = true;
            foreach (var hex in selectedHexes)
            {
                if (!worldGenerator.MapTiles.TryGetValue(hex, out GameObject tile) || tile == null) continue;
                var info = tile.GetComponent<HexTileInfo>();
                if (info == null) continue;
                if (info.ElevationLevel >= 2) allCanRaise = false;
                if (info.ElevationLevel <= 0) allCanLower = false;
            }
            canRaise = allCanRaise;
            canLower = allCanLower;
        }
        else if (hoverEmptyElev >= 0)
        {
            canRaise = hoverEmptyElev < 2;
            canLower = hoverEmptyElev > 0;
        }
        else
        {
            canRaise = true;
            canLower = true;
        }

        if (activeCardUI != null)
        {
            activeCardUI.SetTerraformVisualState(true, activeDirection, canRaise, canLower);
        }
    }

    private void ApplySelection()
    {
        if (selectedHexes.Count == 0 || activeCardData == null || worldGenerator == null) return;

        int feePerTile = Mathf.Max(0, activeCardData.placementFee);
        int totalFee = selectedHexes.Count * feePerTile;
        if (totalFee > 0 && (CurrencyWallet.Instance == null || !CurrencyWallet.Instance.CanAfford(totalFee)))
        {
            GameDialogUI.ShowNotice($"Not enough Coins! Need {totalFee} Coins to terraform {selectedHexes.Count} tile(s).", title: "NOTICE");
            return;
        }

        var snapshot = new List<HexCoordinates>(selectedHexes);
        int applied = 0;
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (IslandPlacementRules.TryTerraform(activeCardData, snapshot[i], worldGenerator, activeDirection))
            {
                applied++;
            }
        }

        if (applied > 0)
        {
            GameAudioManager.Instance?.PlayPlace();
            GameAudioManager.Instance?.PlayCard();
        }

        foreach (var kv in selectedShells)
        {
            if (kv.Value != null) Destroy(kv.Value);
        }
        selectedShells.Clear();
        selectedHexes.Clear();
        ClearHoverVisual();
        hoverEmptyElev = -1;

        SetHint(applied > 0 ? $"Terraformed {applied} tile(s) (-{applied * feePerTile} Coins)." : "No tiles changed.");
        RefreshDirectionConstraints();
        RefreshHud();
    }

    private void RebuildSelectedShellColors()
    {
        Color c = activeDirection > 0 ? RaiseSelectedColor : LowerSelectedColor;
        foreach (var kv in selectedShells)
        {
            if (kv.Value != null) TintShell(kv.Value, c);
        }
        if (hoverShell != null && hoverHex.HasValue && worldGenerator != null && worldGenerator.MapTiles.TryGetValue(hoverHex.Value, out var t))
        {
            bool valid = IslandPlacementRules.CanTerraformTile(activeCardData, hoverHex.Value, t, activeDirection, out _);
            TintShell(hoverShell, valid ? c : HoverInvalidColor);
            hoverValid = valid;
        }
    }

    private void AnimateShells()
    {
        float pulse = 0.50f + 0.14f * Mathf.Sin(Time.unscaledTime * 4.2f);
        Color selColor = activeDirection > 0 ? RaiseSelectedColor : LowerSelectedColor;
        selColor.a = pulse;
        foreach (var kv in selectedShells)
        {
            if (kv.Value != null) TintShell(kv.Value, selColor);
        }
        if (hoverShell != null)
        {
            Color hc = hoverValid
                ? new Color(selColor.r, selColor.g, selColor.b, 0.36f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f))
                : HoverInvalidColor;
            TintShell(hoverShell, hc);
        }
    }

    private GameObject CreateTileShell(GameObject tileObj, Color color, float yOffset, string shellName)
    {
        EnsureHighlightMaterial();
        var shell = new GameObject(shellName);
        shell.transform.position = tileObj.transform.position + new Vector3(0f, yOffset, 0f);
        shell.transform.rotation = tileObj.transform.rotation;
        shell.transform.localScale = Vector3.Scale(tileObj.transform.lossyScale, new Vector3(1.028f, 1.045f, 1.028f));

        HexHighlightShellUtility.PopulateMeshHierarchy(tileObj.transform, shell.transform, highlightMaterial, skipPlacedProps: true);

        TintShell(shell, color);
        return shell;
    }

    private void TintShell(GameObject shell, Color c)
    {
        if (shell == null) return;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        foreach (var r in shell.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorProp, c);
            propBlock.SetColor(LegacyColorProp, c);
            r.SetPropertyBlock(propBlock);
        }
    }

    private void EnsureHighlightMaterial()
    {
        if (highlightMaterial != null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        highlightMaterial = new Material(shader);
        highlightMaterial.SetFloat("_Surface", 1f);
        highlightMaterial.SetFloat("_Blend", 0f);
        highlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        highlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        highlightMaterial.SetInt("_ZWrite", 0);
        highlightMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        highlightMaterial.renderQueue = 3000;
    }

    private void ClearHoverVisual()
    {
        if (hoverShell != null) Destroy(hoverShell);
        hoverShell = null;
        hoverHex = null;
    }

    private void ClearAllVisuals()
    {
        ClearHoverVisual();
        foreach (var kv in selectedShells)
        {
            if (kv.Value != null) Destroy(kv.Value);
        }
        selectedShells.Clear();
    }

    private bool IsPointerOverHudOrUI(Vector2 mousePos)
    {
        if (hudBarRect != null && hudBarRect.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(hudBarRect, mousePos, null))
        {
            return true;
        }
        if (raycaster != null && raycaster.IsOverHandArea(mousePos))
        {
            return true;
        }
        return EconomyHUD.IsPointerOverInteractiveUI(mousePos);
    }

    private void EnsureSceneRefs()
    {
        if (worldGenerator == null) worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (raycaster == null) raycaster = FindAnyObjectByType<HexPointerRaycaster>() ?? gameObject.AddComponent<HexPointerRaycaster>();
    }

    private void SetHint(string message)
    {
        statusHint = message;
        statusHintUntil = Time.unscaledTime + 2.6f;
        RefreshHud();
    }

    private void EnsureHudBuilt()
    {
        if (hudBarRect != null) return;

        var cGo = new GameObject("P04_TerraformHUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        hudCanvas = cGo.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 995;

        var scaler = cGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Image barImg = LumiWorldUITheme.CreateRoundedSurface(
            cGo.transform,
            "P04_TerraformModeBar",
            LumiWorldUITheme.Surface,
            LumiWorldUITheme.Edge,
            radius: 18,
            borderThickness: 1.4f,
            shadowDepth: 4f,
            shadowAlpha: 0.18f,
            raycastTarget: true);

        hudBarRect = barImg.rectTransform;
        hudBarRect.anchorMin = new Vector2(0.5f, 0f);
        hudBarRect.anchorMax = new Vector2(0.5f, 0f);
        hudBarRect.pivot = new Vector2(0.5f, 0f);
        hudBarRect.anchoredPosition = new Vector2(0f, 224f);
        hudBarRect.sizeDelta = new Vector2(676f, 52f);

        raiseBtn = LumiWorldUITheme.CreatePillButton(hudBarRect, "RaiseBtn", "▲ Raise", null, false, 14f, () => TrySetDirection(1), out raiseBtnBg, out _, out raiseBtnLabel, 14);
        LumiWorldUITheme.PlaceTopLeft(raiseBtn.GetComponent<RectTransform>(), 10f, 8f, 102f, 36f);

        lowerBtn = LumiWorldUITheme.CreatePillButton(hudBarRect, "LowerBtn", "▼ Lower", null, false, 14f, () => TrySetDirection(-1), out lowerBtnBg, out _, out lowerBtnLabel, 14);
        LumiWorldUITheme.PlaceTopLeft(lowerBtn.GetComponent<RectTransform>(), 118f, 8f, 102f, 36f);

        infoText = LumiWorldUITheme.CreateLabel(hudBarRect, "InfoText", "Click tiles · 0 selected", 13.5f, LumiWorldUITheme.Text, false, TextAlignmentOptions.Center);
        LumiWorldUITheme.PlaceTopLeft(infoText.rectTransform, 228f, 6f, 240f, 40f);

        applyBtn = LumiWorldUITheme.CreatePillButton(hudBarRect, "ApplyBtn", "Apply", null, true, 14f, ApplySelection, out applyBtnBg, out _, out applyBtnLabel, 14);
        LumiWorldUITheme.PlaceTopLeft(applyBtn.GetComponent<RectTransform>(), 474f, 8f, 104f, 36f);

        cancelBtn = LumiWorldUITheme.CreatePillButton(hudBarRect, "CancelBtn", "Done", null, false, 14f, Deactivate, out _, out _, out _, 14);
        LumiWorldUITheme.PlaceTopLeft(cancelBtn.GetComponent<RectTransform>(), 584f, 8f, 82f, 36f);

        hudBarRect.gameObject.SetActive(false);
    }

    private void RefreshHud()
    {
        if (hudBarRect == null || activeCardData == null) return;

        Color raiseActiveColor = new Color(0.32f, 0.56f, 0.24f, 1f);
        Color lowerActiveColor = new Color(0.18f, 0.49f, 0.78f, 1f);
        Color idleColor = LumiWorldUITheme.Inset;
        Color dimBg = new Color(0.86f, 0.84f, 0.80f, 0.60f);

        raiseBtn.interactable = canRaise;
        raiseBtnBg.color = !canRaise ? dimBg : (activeDirection > 0 ? raiseActiveColor : idleColor);
        raiseBtnLabel.color = !canRaise ? LumiWorldUITheme.Muted : (activeDirection > 0 ? Color.white : LumiWorldUITheme.Text);

        lowerBtn.interactable = canLower;
        lowerBtnBg.color = !canLower ? dimBg : (activeDirection < 0 ? lowerActiveColor : idleColor);
        lowerBtnLabel.color = !canLower ? LumiWorldUITheme.Muted : (activeDirection < 0 ? Color.white : LumiWorldUITheme.Text);

        int feePerTile = Mathf.Max(0, activeCardData.placementFee);
        int count = selectedHexes.Count;
        int total = count * feePerTile;
        bool affordable = total <= 0 || (CurrencyWallet.Instance != null && CurrencyWallet.Instance.CanAfford(total));

        if (!string.IsNullOrEmpty(statusHint) && Time.unscaledTime <= statusHintUntil)
        {
            infoText.text = statusHint;
            infoText.color = LumiWorldUITheme.Warning;
        }
        else if (count == 0)
        {
            infoText.text = $"Click Wet tiles ({feePerTile} Coins/tile)";
            infoText.color = LumiWorldUITheme.Muted;
        }
        else
        {
            infoText.text = $"{count} tile{(count > 1 ? "s" : "")} · {total} Coins";
            infoText.color = affordable ? LumiWorldUITheme.Text : LumiWorldUITheme.Error;
        }

        bool canApply = count > 0 && affordable;
        applyBtn.interactable = canApply;
        applyBtnBg.color = canApply ? LumiWorldUITheme.Primary : new Color(0.62f, 0.59f, 0.55f, 1f);
        applyBtnLabel.color = Color.white;
        applyBtnLabel.text = count > 0 ? $"Apply ({total})" : "Apply";
    }
}

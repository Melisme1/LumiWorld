using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Dims the Canvas, cuts a visual hole over the target UI, shows its details,
/// and optionally animates a cursor hint.
/// </summary>
public class ScoreTutorialOverlay : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private Image dimImage;
    [SerializeField] private Material holeMaterial;
    [SerializeField] private Canvas rootCanvas;
    [Tooltip("Temporarily render this overlay above runtime-created UI canvases while the tutorial is visible.")]
    [SerializeField] private bool bringOverlayToFront = true;
    [SerializeField] private RectTransform detailsPanel;
    [SerializeField] private TMP_Text purposeText;
    [Header("Pointer Hint")]
    [SerializeField] private RectTransform pointer;
    [SerializeField] private RectTransform clickEffect;
    [SerializeField, Min(0.1f)] private float pointerCycleDuration = 1.1f;
    [SerializeField, Min(1f)] private float clickEffectScale = 1.6f;

    public event Action ContinueRequested;
    public bool IsShowing { get; private set; }

    private Coroutine pointerRoutine;
    private int shownFrame;
    private bool waitingForContinueInput;
    private Canvas overlayCanvas;
    private bool originalOverrideSorting;
    private int originalSortingOrder;
    private int originalSortingLayerId;

    private void Awake()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        if (overlayRoot != null)
        {
            overlayCanvas = overlayRoot.GetComponent<Canvas>();
            if (overlayCanvas == null)
                overlayCanvas = overlayRoot.AddComponent<Canvas>();

            originalOverrideSorting = overlayCanvas.overrideSorting;
            originalSortingOrder = overlayCanvas.sortingOrder;
            originalSortingLayerId = overlayCanvas.sortingLayerID;
        }

        if (dimImage != null && holeMaterial != null)
            dimImage.material = new Material(holeMaterial);

        SetRaycastTargets(pointer, false);
        SetRaycastTargets(clickEffect, false);
        Hide();
    }

    private void OnDestroy()
    {
        StopPointerHint();
    }

    public void Show(ScoreTutorialStep step)
    {
        if (step == null || overlayRoot == null)
            return;

        IsShowing = true;
        shownFrame = Time.frameCount;
        waitingForContinueInput = true;
        overlayRoot.SetActive(true);
        SetOverlaySorting(true);
        if (dimImage != null) dimImage.gameObject.SetActive(true);
        UpdateHole(step);
        if (detailsPanel != null) detailsPanel.gameObject.SetActive(true);

        if (purposeText != null) purposeText.text = step.purpose;
        StartPointerHint(step);
    }

    public void Hide()
    {
        IsShowing = false;
        waitingForContinueInput = false;
        SetOverlaySorting(false);
        StopPointerHint();
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    private void UpdateHole(ScoreTutorialStep step)
    {
        RectTransform holeTarget = step.customHighlightArea;
        if (dimImage == null || dimImage.material == null || holeTarget == null || rootCanvas == null)
        {
            Debug.LogWarning("ScoreTutorialOverlay: assign the dim Image, hole material, root Canvas, and Custom Highlight Area.", this);
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Vector3[] corners = new Vector3[4];
        holeTarget.GetWorldCorners(corners);
        Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenMin, camera, out Vector2 localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenMax, camera, out Vector2 localMax);

        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 center = (localMin + localMax) * 0.5f;
        Vector2 size = localMax - localMin + Vector2.one * (step.holePadding * 2f);
        Vector2 normalizedCenter = new Vector2(center.x / canvasSize.x + 0.5f, center.y / canvasSize.y + 0.5f);
        Vector2 normalizedSize = new Vector2(size.x / canvasSize.x, size.y / canvasSize.y);

        dimImage.material.SetVector("_HoleCenter", normalizedCenter);
        dimImage.material.SetVector("_HoleSize", normalizedSize);
    }

    private void StartPointerHint(ScoreTutorialStep step)
    {
        StopPointerHint();
        if (pointer == null || rootCanvas == null || step.pointerMode == ScoreTutorialPointerMode.None || step.customHighlightArea == null)
        {
            if (pointer != null) pointer.gameObject.SetActive(false);
            if (clickEffect != null) clickEffect.gameObject.SetActive(false);
            return;
        }

        pointer.gameObject.SetActive(true);
        if (clickEffect != null) clickEffect.gameObject.SetActive(false);
        pointerRoutine = StartCoroutine(AnimatePointer(step));
    }

    private IEnumerator AnimatePointer(ScoreTutorialStep step)
    {
        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector3[] corners = new Vector3[4];
        step.customHighlightArea.GetWorldCorners(corners);
        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(camera, (corners[0] + corners[2]) * 0.5f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenCenter, camera, out Vector2 targetPosition);

        float duration = Mathf.Max(0.1f, pointerCycleDuration);
        while (IsShowing && pointer != null)
        {
            if (step.pointerMode == ScoreTutorialPointerMode.Click)
            {
                pointer.anchoredPosition = targetPosition;
                yield return PlayClickEffect(duration);
            }
            else
            {
                Vector2 start = targetPosition + step.dragStartOffset;
                pointer.anchoredPosition = start;
                float elapsed = 0f;
                while (elapsed < duration && IsShowing)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    pointer.anchoredPosition = Vector2.Lerp(start, targetPosition, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                yield return PlayClickEffect(0.45f);
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }
    }

    private IEnumerator PlayClickEffect(float duration)
    {
        if (clickEffect == null) yield break;
        clickEffect.gameObject.SetActive(true);
        clickEffect.anchoredPosition = pointer.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration && IsShowing)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            clickEffect.localScale = Vector3.one * Mathf.Lerp(0.6f, clickEffectScale, t);
            CanvasGroup group = clickEffect.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f - t;
            yield return null;
        }
        clickEffect.gameObject.SetActive(false);
    }

    private void StopPointerHint()
    {
        if (pointerRoutine != null)
        {
            StopCoroutine(pointerRoutine);
            pointerRoutine = null;
        }
        if (pointer != null) pointer.gameObject.SetActive(false);
        if (clickEffect != null) clickEffect.gameObject.SetActive(false);
    }

    private void SetOverlaySorting(bool bringToFront)
    {
        if (!bringOverlayToFront || overlayCanvas == null)
            return;

        if (bringToFront)
        {
            if (rootCanvas != null)
                overlayCanvas.sortingLayerID = rootCanvas.sortingLayerID;
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = short.MaxValue;
        }
        else
        {
            overlayCanvas.overrideSorting = originalOverrideSorting;
            overlayCanvas.sortingOrder = originalSortingOrder;
            overlayCanvas.sortingLayerID = originalSortingLayerId;
        }
    }

    private static void SetRaycastTargets(RectTransform root, bool value)
    {
        if (root == null) return;
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
            graphic.raycastTarget = value;
    }

    private void Continue()
    {
        if (!IsShowing || !waitingForContinueInput || Time.frameCount <= shownFrame)
            return;

        waitingForContinueInput = false;
        IsShowing = false;
        ContinueRequested?.Invoke();
    }

    private void Update()
    {
        if (!IsShowing || !waitingForContinueInput || Time.frameCount <= shownFrame)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            Continue();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
        {
            Continue();
            return;
        }

        foreach (Gamepad gamepad in Gamepad.all)
        {
            foreach (var control in gamepad.allControls)
            {
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
                {
                    Continue();
                    return;
                }
            }
        }

        foreach (InputDevice device in InputSystem.devices)
        {
            Touchscreen touchscreen = device as Touchscreen;
            if (touchscreen == null)
                continue;

            foreach (var touch in touchscreen.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    Continue();
                    return;
                }
            }
        }
    }

}

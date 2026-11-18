using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggles scene-authored dim/highlight images and tutorial details.
/// All visuals and their positions are authored in the Scene.
/// </summary>
public class ScoreTutorialOverlay : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private GameObject dimImage;
    [SerializeField] private GameObject highlightImage;
    [SerializeField] private RectTransform detailsPanel;
    [SerializeField] private Button showDetailsButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text purposeText;
    [SerializeField] private TMP_Text instructionsText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueButtonText;

    public event Action ContinueRequested;
    public bool IsShowing { get; private set; }

    private void Awake()
    {
        if (showDetailsButton != null)
            showDetailsButton.onClick.AddListener(ShowDetails);
        if (continueButton != null)
            continueButton.onClick.AddListener(Continue);
        Hide();
    }

    private void OnDestroy()
    {
        if (showDetailsButton != null)
            showDetailsButton.onClick.RemoveListener(ShowDetails);
        if (continueButton != null)
            continueButton.onClick.RemoveListener(Continue);
    }

    public void Show(ScoreTutorialStep step)
    {
        if (step == null || overlayRoot == null)
            return;

        IsShowing = true;
        overlayRoot.SetActive(true);
        if (dimImage != null) dimImage.SetActive(true);
        if (highlightImage != null) highlightImage.SetActive(false);
        if (step.highlightImage != null) step.highlightImage.SetActive(true);
        if (detailsPanel != null) detailsPanel.gameObject.SetActive(false);

        if (detailsPanel != null)
            detailsPanel.gameObject.SetActive(false);

        if (titleText != null) titleText.text = step.title;
        if (purposeText != null) purposeText.text = step.purpose;
        if (instructionsText != null) instructionsText.text = step.instructions;
        if (continueButtonText != null) continueButtonText.text = "Tiếp tục";
    }

    public void Hide()
    {
        IsShowing = false;
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    private void ShowDetails()
    {
        if (IsShowing && detailsPanel != null)
            detailsPanel.gameObject.SetActive(true);
    }

    private void Continue()
    {
        if (!IsShowing)
            return;

        IsShowing = false;
        ContinueRequested?.Invoke();
    }

}

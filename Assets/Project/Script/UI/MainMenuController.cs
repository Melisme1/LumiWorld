using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("UI Panels")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;


    private void Start()
    {
        InitializeMenu();
    }


    private void InitializeMenu()
    {
        // Hide Settings Panel when starting
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        // Check if save data exists
        UpdateContinueButton();
    }


    // ========================================
    // NEW GAME
    // ========================================

    public void OnNewGame()
    {
        Debug.Log("Starting New Game...");

        SceneManager.LoadScene(gameSceneName);
    }


    // ========================================
    // CONTINUE
    // ========================================

    public void OnContinue()
    {
        if (!HasSaveData())
        {
            Debug.Log("No save data found.");
            return;
        }

        Debug.Log("Continuing Game...");

        SceneManager.LoadScene(gameSceneName);
    }


    // ========================================
    // SETTINGS
    // ========================================

    public void OnSettings()
    {
        if (settingsPanel == null)
            return;

        settingsPanel.SetActive(true);
    }


    public void OnCloseSettings()
    {
        if (settingsPanel == null)
            return;

        settingsPanel.SetActive(false);
    }


    // ========================================
    // QUIT
    // ========================================

    public void OnQuit()
    {
        Debug.Log("Quit Game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }


    // ========================================
    // SAVE CHECK
    // ========================================

    private bool HasSaveData()
    {
        return PlayerPrefs.HasKey("GameSave");
    }


    private void UpdateContinueButton()
    {
        if (continueButton == null)
            return;

        bool hasSave = HasSaveData();

        continueButton.interactable = hasSave;

        // Nếu nút đang bị hover lúc bị disable, đưa hiệu ứng về trạng thái nghỉ.
        if (!hasSave)
        {
            MenuButtonEffect effect =
                continueButton.GetComponent<MenuButtonEffect>();

            if (effect != null)
            {
                effect.ResetVisual();
            }
        }
    }
}
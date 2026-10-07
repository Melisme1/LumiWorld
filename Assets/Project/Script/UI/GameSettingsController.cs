using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSettingsController : MonoBehaviour
{
    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Main Menu Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";


    private void Start()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }


    // ========================================
    // OPEN SETTINGS
    // ========================================

    public void OpenSettings()
    {
        if (settingsPanel == null)
            return;

        settingsPanel.SetActive(true);
    }


    // ========================================
    // CLOSE SETTINGS
    // ========================================

    public void CloseSettings()
    {
        if (settingsPanel == null)
            return;

        settingsPanel.SetActive(false);
    }


    // ========================================
    // BACK TO MAIN MENU
    // ========================================

    public void BackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }


    // ========================================
    // QUIT GAME
    // ========================================

    public void QuitGame()
    {
        Debug.Log("Quit Game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
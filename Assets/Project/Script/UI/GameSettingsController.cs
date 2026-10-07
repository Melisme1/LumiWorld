using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameSettingsController : MonoBehaviour
{
    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Main Menu Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Input")]
    [Tooltip("Phím bật/tắt Settings Panel.")]
    [SerializeField] private Key toggleSettingsKey = Key.Escape;


    private void Start()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }


    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (!keyboard[toggleSettingsKey].wasPressedThisFrame)
            return;

        ToggleSettings();
    }


    // ========================================
    // OPEN SETTINGS
    // ========================================

    public void OpenSettings()
    {
        SetSettingsPanelActive(true);
    }


    // ========================================
    // CLOSE SETTINGS
    // ========================================

    public void CloseSettings()
    {
        SetSettingsPanelActive(false);
    }


    // ========================================
    // TOGGLE SETTINGS
    // ========================================
    // Gán hàm này vào nút Settings: nhấn lần đầu mở panel,
    // nhấn lần nữa (khi panel đang mở) thì tắt panel đi.
    // Gọi ResetVisual để tránh nút bị kẹt ở trạng thái hover khi panel bị ẩn.

    public void ToggleSettings()
    {
        if (settingsPanel == null)
            return;

        SetSettingsPanelActive(!settingsPanel.activeSelf);
    }


    private void SetSettingsPanelActive(bool active)
    {
        if (settingsPanel == null)
            return;

        if (settingsPanel.activeSelf == active)
            return;

        settingsPanel.SetActive(active);

        // Khi đóng panel, con trỏ có thể đang hover nút Settings -> nút không nhận
        // OnPointerExit vì panel che/bị ẩn => giữ nguyên trạng thái phóng to về sau.
        // Reset lại để nút trở về trạng thái nghỉ.
        if (!active)
        {
            ResetButtonEffects();
        }
    }


    private void ResetButtonEffects()
    {
        // Chỉ reset các hiệu ứng đang active — nút nằm trong panel vừa ẩn
        // sẽ không được duyệt tới, nhưng OnEnable của MenuButtonEffect đã
        // tự đưa chúng về trạng thái nghỉ khi panel được bật lại.
        MenuButtonEffect[] effects =
            FindObjectsByType<MenuButtonEffect>(FindObjectsInactive.Exclude);

        for (int i = 0; i < effects.Length; i++)
        {
            effects[i].ResetVisual();
        }
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
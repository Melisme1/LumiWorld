using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Server từ chối token (401) ở bất kỳ màn chơi nào (MapBuilding, WorldMap, VisitWorld...): xóa phiên đã lưu,
/// kết thúc chuyến thăm nếu đang thăm, rồi đưa người chơi về màn đăng nhập kèm lời nhắn,
/// thay vì đứng ở một màn không tải được gì. Màn Login và MainMenu tự xử lý 401 nên bỏ qua ở đó.
/// Save trên máy không mất: SaveSync đánh dấu bản chưa gửi và gửi lại sau khi đăng nhập.
/// </summary>
public static class SessionGuard
{
    public const string ExpiredMessage = "Your session expired. Please log in again.";

    private const string LoginScene = "Login";
    private const string MenuScene = "MainMenu";

    private static string pendingMessage;
    private static bool redirecting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        // Khi tắt Domain Reload, biến static không tự xóa: đặt lại lúc vào Play Mode
        pendingMessage = null;
        redirecting = false;
        ApiClient.SessionExpired -= HandleSessionExpired;
        ApiClient.SessionExpired += HandleSessionExpired;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>Lời nhắn để màn đăng nhập hiện một lần, rồi xóa.</summary>
    public static string TakeMessage()
    {
        string message = pendingMessage;
        pendingMessage = null;
        return message;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => redirecting = false;

    private static void HandleSessionExpired()
    {
        string current = SceneManager.GetActiveScene().name;
        if (redirecting || current == LoginScene || current == MenuScene) return;

        // Nhiều request cùng trả 401 thì chỉ chuyển scene một lần
        redirecting = true;
        Debug.LogWarning("[LumiWorld Auth] Server từ chối token (401). Xóa phiên và mở lại màn đăng nhập.");

        if (VisitSession.Active) VisitSession.End();
        AuthSession.LogOut();
        pendingMessage = ExpiredMessage;

        string target = Application.CanStreamedLevelBeLoaded(LoginScene) ? LoginScene : MenuScene;
        SceneManager.LoadScene(target);
    }
}

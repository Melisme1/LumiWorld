using UnityEngine;

/// <summary>
/// Id của bản lưu đang dùng, và tên file lưu lấy theo id này.
/// Tài khoản đăng nhập thì dùng id tài khoản. Chưa đăng nhập thì dùng "guest",
/// chỉ để nhóm Vinh, Acs và Tân vẫn bấm Play trực tiếp trong MapBuilding ở Editor.
/// Bản build bắt buộc đăng nhập trước khi vào game (xem MainMenuController).
/// </summary>
public static class SaveSlot
{
    public const string GuestId = "guest";

    private static bool warnedAboutGuest;

    /// <summary>Đang đăng nhập bằng một tài khoản thật.</summary>
    public static bool IsAccount => AuthSession.IsLoggedIn && !string.IsNullOrEmpty(AuthSession.PlayerId);

    public static string PlayerId
    {
        get
        {
            if (IsAccount) return AuthSession.PlayerId;

            if (!warnedAboutGuest)
            {
                warnedAboutGuest = true;
                Debug.LogWarning("[LumiWorld Save] Chưa đăng nhập nên dùng bản lưu guest. Bản build sẽ yêu cầu đăng nhập trước.");
            }

            return GuestId;
        }
    }
}

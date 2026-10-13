using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// The logged-in player, kept across launches. Any system that calls the API
/// gets the token from here — ApiClient attaches it automatically.
///
/// Stored in PlayerPrefs, which on Windows is the registry in plain text.
/// Normal for a game session token, but never store the password here.
/// </summary>
public static class AuthSession
{
    private const string TokenKey = "lumi.auth.token";
    private const string PlayerIdKey = "lumi.auth.playerId";
    private const string UsernameKey = "lumi.auth.username";
    private const string DisplayNameKey = "lumi.auth.displayName";
    private const string ExpiresAtKey = "lumi.auth.expiresAt";

    // Treat the token as expired a little early, so a request never leaves
    // with a token that dies in flight.
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(60);

    public static string AccessToken => PlayerPrefs.GetString(TokenKey, "");
    public static string PlayerId => PlayerPrefs.GetString(PlayerIdKey, "");
    public static string Username => PlayerPrefs.GetString(UsernameKey, "");
    public static string DisplayName => PlayerPrefs.GetString(DisplayNameKey, "");

    public static bool IsLoggedIn
    {
        get
        {
            if (string.IsNullOrEmpty(AccessToken))
                return false;

            // InvariantCulture matters: on a Vietnamese Windows locale the default
            // culture can misread an ISO timestamp.
            bool parsed = DateTimeOffset.TryParse(
                PlayerPrefs.GetString(ExpiresAtKey, ""),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset expiresAt);

            if (!parsed)
                return false;

            return DateTimeOffset.UtcNow < expiresAt - ExpirySkew;
        }
    }

    public static void Save(AuthResponse response)
    {
        if (response == null || string.IsNullOrEmpty(response.accessToken))
        {
            Debug.LogWarning("AuthSession: refusing to save an empty auth response.");
            return;
        }

        PlayerPrefs.SetString(TokenKey, response.accessToken);
        PlayerPrefs.SetString(PlayerIdKey, response.playerId ?? "");
        PlayerPrefs.SetString(UsernameKey, response.username ?? "");
        PlayerPrefs.SetString(DisplayNameKey, response.displayName ?? "");
        PlayerPrefs.SetString(ExpiresAtKey, response.expiresAt ?? "");
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Forgets the session but keeps the username, so the login form can
    /// prefill it next time.
    /// </summary>
    public static void LogOut()
    {
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(PlayerIdKey);
        PlayerPrefs.DeleteKey(DisplayNameKey);
        PlayerPrefs.DeleteKey(ExpiresAtKey);
        PlayerPrefs.Save();
    }
}

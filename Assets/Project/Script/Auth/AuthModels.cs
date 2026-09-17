using System;

// Request and response bodies for /api/v1/auth.
//
// Field names are camelCase on purpose: JsonUtility maps JSON keys to field
// names exactly, and the backend speaks camelCase. Renaming a field to C#
// PascalCase silently breaks serialization with no error.

[Serializable]
public class RegisterRequest
{
    public string username;
    public string password;
    public string confirmPassword;
}

[Serializable]
public class LoginRequest
{
    public string username;
    public string password;
}

[Serializable]
public class AuthResponse
{
    public string playerId;
    public string username;
    public string displayName;
    public string accessToken;

    // ISO 8601 with offset, e.g. "2026-09-10T16:18:05.5138023+00:00".
    // Kept as a string: JsonUtility cannot deserialize DateTimeOffset.
    public string expiresAt;
}

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Talks to the LumiWorld backend. Coroutine-based: start calls with
/// StartCoroutine(ApiClient.Post(...)) from any MonoBehaviour.
/// </summary>
public static class ApiClient
{
    private const string BaseUrlKey = "lumi.api.baseUrl";
    private const string DefaultBaseUrl = "http://localhost:8080";
    private const int TimeoutSeconds = 15;

    /// <summary>
    /// Server address, remembered between launches. Change it on the login screen
    /// when testing against a Cloudflare tunnel URL.
    /// </summary>
    public static string BaseUrl
    {
        get => PlayerPrefs.GetString(BaseUrlKey, DefaultBaseUrl);
        set
        {
            string cleaned = string.IsNullOrWhiteSpace(value)
                ? DefaultBaseUrl
                : value.Trim().TrimEnd('/');

            PlayerPrefs.SetString(BaseUrlKey, cleaned);
            PlayerPrefs.Save();
        }
    }

    public static IEnumerator Post<TRequest, TResponse>(
        string path,
        TRequest body,
        bool withToken,
        Action<TResponse> onSuccess,
        Action<ApiError> onError)
    {
        byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));

        using (var request = new UnityWebRequest(BaseUrl + path, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return Send(request, withToken, onSuccess, onError);
        }
    }

    public static IEnumerator Get<TResponse>(
        string path,
        bool withToken,
        Action<TResponse> onSuccess,
        Action<ApiError> onError)
    {
        using (var request = UnityWebRequest.Get(BaseUrl + path))
        {
            yield return Send(request, withToken, onSuccess, onError);
        }
    }

    private static IEnumerator Send<TResponse>(
        UnityWebRequest request,
        bool withToken,
        Action<TResponse> onSuccess,
        Action<ApiError> onError)
    {
        request.timeout = TimeoutSeconds;
        request.SetRequestHeader("Accept", "application/json");

        if (withToken && AuthSession.IsLoggedIn)
            request.SetRequestHeader("Authorization", "Bearer " + AuthSession.AccessToken);

        // SendWebRequest throws synchronously for a malformed URL, or for plain
        // http:// when Player Settings block insecure connections. C# forbids
        // yielding inside a try/catch, so capture the failure and report it after.
        UnityWebRequestAsyncOperation operation = null;
        string sendFailure = null;

        try
        {
            operation = request.SendWebRequest();
        }
        catch (Exception ex)
        {
            sendFailure = ex.Message;
        }

        if (sendFailure != null)
        {
            onError?.Invoke(ApiError.Transport(sendFailure));
            yield break;
        }

        yield return operation;

        string text = request.downloadHandler != null ? request.downloadHandler.text : null;

        if (request.result == UnityWebRequest.Result.ProtocolError)
        {
            onError?.Invoke(ApiError.FromResponse(request.responseCode, text));
            yield break;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(ApiError.Transport(request.error));
            yield break;
        }

        TResponse response = default;
        string parseFailure = null;

        try
        {
            response = JsonUtility.FromJson<TResponse>(text);
        }
        catch (Exception ex)
        {
            parseFailure = ex.Message;
        }

        // Callbacks run outside the try so an exception in caller code is not
        // misreported as a bad server response.
        if (parseFailure != null)
            onError?.Invoke(ApiError.Transport("Unreadable response: " + parseFailure));
        else
            onSuccess?.Invoke(response);
    }
}

/// <summary>
/// RFC 9457 problem+json body returned by the backend on failure.
/// Field names match the JSON keys exactly — JsonUtility maps by field name.
/// </summary>
[Serializable]
public class ProblemDetails
{
    public string title;
    public int status;
    public string detail;
    public string code;
}

public class ApiError
{
    /// <summary>HTTP status, or 0 when the request never got a response.</summary>
    public long StatusCode { get; private set; }

    /// <summary>
    /// Stable machine-readable code such as "USERNAME_TAKEN". Branch on this,
    /// never on Detail: codes are frozen, human-readable messages are not.
    /// </summary>
    public string Code { get; private set; }

    public string Detail { get; private set; }

    public bool IsNetworkError => StatusCode == 0;

    public static ApiError Transport(string message)
    {
        return new ApiError { StatusCode = 0, Code = "", Detail = message ?? "" };
    }

    public static ApiError FromResponse(long statusCode, string body)
    {
        var error = new ApiError { StatusCode = statusCode, Code = "", Detail = "" };

        if (string.IsNullOrEmpty(body))
            return error;

        try
        {
            ProblemDetails problem = JsonUtility.FromJson<ProblemDetails>(body);

            if (problem != null)
            {
                error.Code = problem.code ?? "";
                error.Detail = !string.IsNullOrEmpty(problem.detail) ? problem.detail : problem.title ?? "";
            }
        }
        catch (Exception)
        {
            // Not JSON — for example an HTML 502 page from the tunnel when the
            // API is down. The status code alone is still meaningful.
        }

        return error;
    }

    public override string ToString()
    {
        return "HTTP " + StatusCode + " " + Code + " " + Detail;
    }
}

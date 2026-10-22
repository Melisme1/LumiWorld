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
    /// Header chống làm lại thao tác khi gửi lại vì mất mạng. Server đọc đúng tên này.
    /// </summary>
    public const string IdempotencyHeader = "Idempotency-Key";

    /// <summary>
    /// Báo khi server từ chối token đang gửi (401). Màn menu nghe sự kiện này để mời đăng nhập lại.
    /// Chỉ bắn khi request đã gửi kèm token, nên khách chưa đăng nhập không kích hoạt.
    /// </summary>
    public static event Action SessionExpired;

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
        Action<ApiError> onError,
        string idempotencyKey = null,
        int timeoutSeconds = 0)
    {
        return SendJson(UnityWebRequest.kHttpVerbPOST, path, body, withToken, onSuccess, onError, idempotencyKey, timeoutSeconds);
    }

    public static IEnumerator Put<TRequest, TResponse>(
        string path,
        TRequest body,
        bool withToken,
        Action<TResponse> onSuccess,
        Action<ApiError> onError,
        string idempotencyKey = null)
    {
        return SendJson(UnityWebRequest.kHttpVerbPUT, path, body, withToken, onSuccess, onError, idempotencyKey);
    }

    /// <summary>PUT với body là chuỗi JSON có sẵn (lưu save gửi nguyên văn file, không qua JsonUtility).</summary>
    public static IEnumerator PutRaw(
        string path,
        string jsonBody,
        bool withToken,
        Action<string> onSuccess,
        Action<ApiError> onError)
    {
        byte[] payload = Encoding.UTF8.GetBytes(jsonBody);
        using (var request = new UnityWebRequest(BaseUrl + path, UnityWebRequest.kHttpVerbPUT))
        {
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return SendText(request, withToken, onSuccess, onError, null);
        }
    }

    /// <summary>GET trả về nguyên văn body (JSON), để tự tách các phần object lớn.</summary>
    public static IEnumerator GetText(
        string path,
        bool withToken,
        Action<string> onSuccess,
        Action<ApiError> onError)
    {
        using (var request = UnityWebRequest.Get(BaseUrl + path))
        {
            yield return SendText(request, withToken, onSuccess, onError, null);
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
            yield return Send(request, withToken, onSuccess, onError, null);
        }
    }

    /// <summary>
    /// DELETE không có body. Server trả 204 nên onSuccess nhận null.
    /// </summary>
    public static IEnumerator Delete<TResponse>(
        string path,
        bool withToken,
        Action<TResponse> onSuccess,
        Action<ApiError> onError,
        string idempotencyKey = null)
    {
        using (var request = new UnityWebRequest(BaseUrl + path, UnityWebRequest.kHttpVerbDELETE))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            yield return Send(request, withToken, onSuccess, onError, idempotencyKey);
        }
    }

    private static IEnumerator SendJson<TRequest, TResponse>(
        string method,
        string path,
        TRequest body,
        bool withToken,
        Action<TResponse> onSuccess,
        Action<ApiError> onError,
        string idempotencyKey,
        int timeoutSeconds = 0)
    {
        byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));

        using (var request = new UnityWebRequest(BaseUrl + path, method))
        {
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            // 0 = dùng thời gian chờ mặc định; màn đăng nhập truyền 20 giây
            if (timeoutSeconds > 0) request.timeout = timeoutSeconds;

            yield return Send(request, withToken, onSuccess, onError, idempotencyKey);
        }
    }

    private static IEnumerator Send<TResponse>(
        UnityWebRequest request,
        bool withToken,
        Action<TResponse> onSuccess,
        Action<ApiError> onError,
        string idempotencyKey)
    {
        // Thành công thì phân tích JSON rồi mới gọi onSuccess; lỗi phân tích báo như lỗi server
        return SendText(request, withToken, text =>
        {
            TResponse response = default;
            string parseFailure = null;

            // 204 No Content (DELETE, và ack trả về rỗng) không có body để đọc
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    response = JsonUtility.FromJson<TResponse>(text);
                }
                catch (Exception ex)
                {
                    parseFailure = ex.Message;
                }
            }

            // Callbacks run outside the try so an exception in caller code is not
            // misreported as a bad server response.
            if (parseFailure != null)
                onError?.Invoke(ApiError.Transport("Unreadable response: " + parseFailure));
            else
                onSuccess?.Invoke(response);
        }, onError, idempotencyKey);
    }

    private static IEnumerator SendText(
        UnityWebRequest request,
        bool withToken,
        Action<string> onText,
        Action<ApiError> onError,
        string idempotencyKey)
    {
        if (request.timeout <= 0) request.timeout = TimeoutSeconds;
        request.SetRequestHeader("Accept", "application/json");

        bool sendingToken = withToken && AuthSession.IsLoggedIn;
        if (sendingToken)
            request.SetRequestHeader("Authorization", "Bearer " + AuthSession.AccessToken);

        if (!string.IsNullOrEmpty(idempotencyKey))
            request.SetRequestHeader(IdempotencyHeader, idempotencyKey);

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
            if (sendingToken && request.responseCode == 401)
                SessionExpired?.Invoke();

            onError?.Invoke(ApiError.FromResponse(request.responseCode, text));
            yield break;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(ApiError.Transport(request.error));
            yield break;
        }

        // 204 No Content không có body; onText nhận chuỗi rỗng
        onText?.Invoke(text);
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

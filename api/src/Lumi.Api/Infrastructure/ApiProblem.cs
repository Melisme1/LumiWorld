namespace Lumi.Api.Infrastructure;

/// <summary>
/// Mã lỗi cố định gửi cho client trong trường "code" của ProblemDetails.
/// Client rẽ nhánh theo mã này (AuthScreen.cs), nên không được đổi tên mã đã phát hành.
/// </summary>
public static class ErrorCodes
{
    public const string UsernameTaken = "USERNAME_TAKEN";
    public const string UsernameInvalid = "USERNAME_INVALID";
    public const string PasswordTooShort = "PASSWORD_TOO_SHORT";
    public const string PasswordMismatch = "PASSWORD_MISMATCH";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountSuspended = "ACCOUNT_SUSPENDED";
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string BadRequest = "BAD_REQUEST";
    public const string NotFound = "NOT_FOUND";
    public const string ServerError = "SERVER_ERROR";

    public const string SaveNotFound = "SAVE_NOT_FOUND";
    public const string SaveConflict = "SAVE_CONFLICT";
    public const string SaveInvalid = "SAVE_INVALID";
    public const string SaveTooLarge = "SAVE_TOO_LARGE";

    public const string WorldNotFound = "WORLD_NOT_FOUND";
    public const string SnapshotInvalid = "SNAPSHOT_INVALID";
    public const string SnapshotTooLarge = "SNAPSHOT_TOO_LARGE";
    public const string SnapshotVersionUnsupported = "SNAPSHOT_VERSION_UNSUPPORTED";
    public const string SearchInvalid = "SEARCH_INVALID";

    public const string IdempotencyKeyMissing = "IDEMPOTENCY_KEY_MISSING";
    public const string OperationInProgress = "OPERATION_IN_PROGRESS";

    public const string ListingNotFound = "LISTING_NOT_FOUND";
    public const string ListingUnavailable = "LISTING_UNAVAILABLE";
    public const string ListingLimitReached = "LISTING_LIMIT_REACHED";
    public const string OwnListing = "OWN_LISTING";
    public const string NotYourListing = "NOT_YOUR_LISTING";
    public const string UnknownResource = "UNKNOWN_RESOURCE";
    public const string InvalidAmount = "INVALID_AMOUNT";
    public const string PriceOutOfRange = "PRICE_OUT_OF_RANGE";

    public const string TradeNotFound = "TRADE_NOT_FOUND";
    public const string TradeUnavailable = "TRADE_UNAVAILABLE";
    public const string TradeLimitReached = "TRADE_LIMIT_REACHED";
    public const string TradeOwnWorld = "TRADE_OWN_WORLD";
    public const string NotYourTrade = "NOT_YOUR_TRADE";

    public const string Forbidden = "FORBIDDEN";
    public const string AdminSelf = "ADMIN_SELF";
    public const string AdminTargetIsAdmin = "ADMIN_TARGET_IS_ADMIN";
    public const string AdminTargetSuspended = "ADMIN_TARGET_SUSPENDED";
    public const string ConfigKeyUnknown = "CONFIG_KEY_UNKNOWN";
    public const string ConfigValueInvalid = "CONFIG_VALUE_INVALID";

    public const string ObstacleKindNotAllowed = "OBSTACLE_KIND_NOT_ALLOWED";
    public const string ObstacleOwnWorld = "OBSTACLE_OWN_WORLD";
    public const string ObstacleTileNotFound = "OBSTACLE_TILE_NOT_FOUND";
    public const string ObstacleProtected = "OBSTACLE_PROTECTED";
    public const string ObstacleDailyLimit = "OBSTACLE_DAILY_LIMIT";
    public const string ObstaclePendingLimit = "OBSTACLE_PENDING_LIMIT";
}

/// <summary>
/// Trả lỗi dạng RFC 9457 problem+json, có thêm trường "code" ở cấp ngoài cùng
/// đúng như ProblemDetails phía client đọc.
/// </summary>
public static class ApiProblem
{
    public static IResult Create(int status, string code, string detail, object? extra = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };

        if (extra != null)
        {
            foreach (var property in extra.GetType().GetProperties())
                extensions[property.Name] = property.GetValue(extra);
        }

        return Results.Problem(statusCode: status, detail: detail, extensions: extensions);
    }

    /// <summary>Mã mặc định khi lỗi không do handler tự tạo (body hỏng, 404 route, 500).</summary>
    public static string DefaultCode(int status) => status switch
    {
        400 => ErrorCodes.BadRequest,
        401 => ErrorCodes.Unauthorized,
        404 => ErrorCodes.NotFound,
        429 => ErrorCodes.TooManyRequests,
        >= 500 => ErrorCodes.ServerError,
        _ => "HTTP_" + status
    };
}

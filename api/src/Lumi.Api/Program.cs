using System.Threading.RateLimiting;
using Lumi.Api.Features.Admin;
using Lumi.Api.Features.Auth;
using Lumi.Api.Features.Config;
using Lumi.Api.Features.Inbox;
using Lumi.Api.Features.Market;
using Lumi.Api.Features.Obstacles;
using Lumi.Api.Features.Rivers;
using Lumi.Api.Features.Saves;
using Lumi.Api.Features.Trades;
using Lumi.Api.Features.Worlds;
using Lumi.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));

// --- Cơ sở dữ liệu ---
string connectionString = builder.Configuration.GetConnectionString("Lumi")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Lumi (đặt qua biến môi trường ConnectionStrings__Lumi).");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

// --- Đăng nhập bằng JWT ---
// Đánh dấu trong HttpContext.Items khi token hợp lệ nhưng tài khoản đã bị khóa
const string SuspendedItem = "lumi.accountSuspended";
var jwt =builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key phải dài ít nhất 32 ký tự (đặt qua biến môi trường Jwt__Key).");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPasswordHasher<PlayerAccount>, PasswordHasher<PlayerAccount>>();
builder.Services.AddSingleton<GameConfigService>();
builder.Services.AddHostedService<ListingExpiryService>();
builder.Services.AddHostedService<TradeExpiryService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Giữ nguyên tên claim "sub", "name", "role" thay vì đổi sang URI dài của .NET
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.SigningKey(),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // 401 cũng trả problem+json có code, để client nhận ra hết phiên và mở lại màn đăng nhập
        options.Events = new JwtBearerEvents
        {
            // Tài khoản bị admin khóa (hoặc đã xóa): token còn hạn cũng hết tác dụng ngay ở request kế tiếp
            OnTokenValidated = async context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out Guid playerId))
                {
                    context.Fail("Token has no player id.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<NpgsqlDataSource>();
                await using var cmd = db.CreateCommand("select is_suspended from players where id = @p");
                cmd.Parameters.AddWithValue("p", playerId);
                if (await cmd.ExecuteScalarAsync(context.HttpContext.RequestAborted) is not false)
                {
                    context.HttpContext.Items[SuspendedItem] = true;
                    context.Fail("Account suspended.");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                IResult problem = context.HttpContext.Items.ContainsKey(SuspendedItem)
                    ? ApiProblem.Create(403, ErrorCodes.AccountSuspended, "This account can't be used right now.")
                    : ApiProblem.Create(401, ErrorCodes.Unauthorized, "Log in again to continue.");
                await problem.ExecuteAsync(context.HttpContext);
            }
        };
    });
builder.Services.AddAuthorization();

// --- Giới hạn số lần thử đăng nhập theo IP (client hiện "Too many attempts") ---
int authPerMinute = builder.Configuration.GetValue("RateLimit:AuthPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));

    options.OnRejected = async (context, _) =>
        await ApiProblem.Create(429, ErrorCodes.TooManyRequests, "Too many attempts. Wait a minute, then try again.")
            .ExecuteAsync(context.HttpContext);
});

// --- Mọi lỗi đều là problem+json có trường code ---
// Body JSON hỏng trả 400 cả khi chạy Development (mặc định dev ném exception thành 500)
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        int status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        context.ProblemDetails.Extensions.TryAdd("code", ApiProblem.DefaultCode(status));
    };
});

// --- Chạy sau cloudflared (cùng máy): lấy IP thật của người chơi từ X-Forwarded-For ---
// Chỉ tin proxy loopback (mặc định), để giới hạn đăng nhập theo IP không gộp mọi người vào 127.0.0.1
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapConfigEndpoints();
app.MapSaveEndpoints();
app.MapWorldEndpoints();
app.MapRiverEndpoints();
app.MapTradeEndpoints();
app.MapInboxEndpoints(app.Environment);
app.MapMarketEndpoints();
app.MapAdminEndpoints();
app.MapAdminPlayerEndpoints();
app.MapObstacleEndpoints();
app.MapLeaderboardEndpoints();

// Bảng do D:\LumiDB\scripts\migrate.ps1 tạo; thiếu migration thì dừng trước khi nhận request
await SchemaCheck.EnsureAsync(app.Services.GetRequiredService<NpgsqlDataSource>(), app.Logger);

app.Run();

// Cho phép project test dựng server trong bộ nhớ (WebApplicationFactory<Program>)
public partial class Program;

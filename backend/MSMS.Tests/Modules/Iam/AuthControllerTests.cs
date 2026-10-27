using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Iam.Controllers;
using MSMS.Api.Modules.Iam.Dtos;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Modules.Iam;

/// <summary>WBS 4.1.1: web uses an HttpOnly session cookie; mobile receives refresh tokens in JSON.</summary>
public class AuthControllerTests
{
    private sealed class FakeAuthService : IAuthService
    {
        public string? ReceivedRefreshToken { get; private set; }
        public string? ReceivedUserAgent { get; private set; }
        public bool RejectLogin { get; init; }
        public bool RejectRefresh { get; init; }
        public LoginResponse Response { get; } = new("access-token", DateTime.UtcNow.AddMinutes(15), "new-refresh-token",
            new UserInfo(1, 2, "GV001", "Nguyễn Văn An", AccountTypes.Teacher, AccountStatuses.Active, false, null));

        public Task<LoginResponse> LoginAsync(LoginRequest request, string? userAgent)
        {
            ReceivedUserAgent = userAgent;
            return RejectLogin
                ? Task.FromException<LoginResponse>(ApiException.Unauthorized(
                    "Tên đăng nhập hoặc mật khẩu không đúng. Kiểm tra lại rồi thử đăng nhập tiếp.", ErrorCodes.InvalidCredentials))
                : Task.FromResult(Response);
        }

        public Task<LoginResponse> RefreshAsync(RefreshRequest request)
        {
            ReceivedRefreshToken = request.RefreshToken;
            return RejectRefresh
                ? Task.FromException<LoginResponse>(ApiException.Unauthorized("Phiên đăng nhập không hợp lệ."))
                : Task.FromResult(Response);
        }

        public Task LogoutAsync(Guid sessionId) => throw new NotSupportedException();
        public Task<UserInfo> GetMeAsync(int accountId) => throw new NotSupportedException();
        public Task ChangePasswordAsync(int accountId, Guid? sessionId, ChangePasswordRequest request) => throw new NotSupportedException();
    }

    private static (AuthController Controller, DefaultHttpContext Context) CreateController(FakeAuthService auth, string scheme = "https")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Headers.UserAgent = "test-device";
        var currentUser = new CurrentUser(new HttpContextAccessor { HttpContext = context });
        return (new AuthController(auth, currentUser)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        }, context);
    }

    private static void AssertWebSessionCookie(DefaultHttpContext context, bool secure)
    {
        var cookie = SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.Single()!);
        Assert.Equal("msms_rt", cookie.Name.Value);
        Assert.Equal("new-refresh-token", cookie.Value.Value);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(secure, cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal("/api/auth", cookie.Path.Value);
        Assert.Null(cookie.Expires);
        Assert.Null(cookie.MaxAge);
    }

    private static void AssertNoStore(DefaultHttpContext context)
    {
        Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, "https", true)]
    [InlineData("web", "https", true)]
    [InlineData(null, "http", false)]
    public async Task Login_Web_ReturnsRefreshTokenOnlyThroughHttpOnlySessionCookie(string? clientType, string scheme, bool secure)
    {
        var auth = new FakeAuthService();
        var (controller, context) = CreateController(auth, scheme);

        var action = await controller.Login(new LoginRequest("username", "password", clientType));
        var result = Assert.IsType<LoginResponse>(action.Value);

        Assert.Equal("access-token", result.Token);
        Assert.Null(result.RefreshToken);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.False(json.RootElement.TryGetProperty("refreshToken", out _));
        AssertWebSessionCookie(context, secure);
        AssertNoStore(context);
        Assert.Equal("test-device", auth.ReceivedUserAgent);
    }

    [Fact]
    public async Task Login_Mobile_ReturnsRefreshTokenInBodyWithoutSettingCookie()
    {
        var auth = new FakeAuthService();
        var (controller, context) = CreateController(auth);

        var action = await controller.Login(new LoginRequest("username", "password", "mobile"));
        var result = Assert.IsType<LoginResponse>(action.Value);

        Assert.Equal("new-refresh-token", result.RefreshToken);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        AssertNoStore(context);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsNeutralUnauthorizedWithoutCookieOrCaching()
    {
        var auth = new FakeAuthService { RejectLogin = true };
        var (controller, context) = CreateController(auth);

        var result = await controller.Login(new LoginRequest("username", "wrong-password"));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        var json = JsonSerializer.SerializeToElement(unauthorized.Value);
        Assert.Equal(ErrorCodes.InvalidCredentials, json.GetProperty("code").GetString());
        Assert.Equal("Tên đăng nhập hoặc mật khẩu không đúng. Kiểm tra lại rồi thử đăng nhập tiếp.",
            json.GetProperty("message").GetString());
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        AssertNoStore(context);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Refresh_Web_UsesCookieAndReturnsRotatedTokenOnlyThroughCookie(string? bodyToken)
    {
        var auth = new FakeAuthService();
        var (controller, context) = CreateController(auth);
        context.Request.Headers.Cookie = "msms_rt=old-cookie-token";

        var action = await controller.Refresh(new RefreshRequest(bodyToken));
        var result = Assert.IsType<LoginResponse>(action.Value);

        Assert.Equal("old-cookie-token", auth.ReceivedRefreshToken);
        Assert.Null(result.RefreshToken);
        AssertWebSessionCookie(context, secure: true);
        AssertNoStore(context);
    }

    [Fact]
    public async Task Refresh_Mobile_PrefersBodyTokenAndReturnsNewTokenWithoutSettingCookie()
    {
        var auth = new FakeAuthService();
        var (controller, context) = CreateController(auth);
        context.Request.Headers.Cookie = "msms_rt=unrelated-cookie-token";

        var action = await controller.Refresh(new RefreshRequest("old-mobile-token"));
        var result = Assert.IsType<LoginResponse>(action.Value);

        Assert.Equal("old-mobile-token", auth.ReceivedRefreshToken);
        Assert.Equal("new-refresh-token", result.RefreshToken);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        AssertNoStore(context);
    }

    [Fact]
    public async Task Refresh_InvalidWebSession_DeletesCookieAndDoesNotCacheFailure()
    {
        var auth = new FakeAuthService { RejectRefresh = true };
        var (controller, context) = CreateController(auth);
        context.Request.Headers.Cookie = "msms_rt=invalid-token";

        var result = await controller.Refresh(new RefreshRequest(null));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        var json = JsonSerializer.SerializeToElement(unauthorized.Value);
        Assert.Equal(ErrorCodes.Unauthorized, json.GetProperty("code").GetString());
        Assert.Equal("Phiên đăng nhập không hợp lệ.", json.GetProperty("message").GetString());
        var cookie = SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.Single()!);
        Assert.Equal("msms_rt", cookie.Name.Value);
        Assert.Equal("/api/auth", cookie.Path.Value);
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
        AssertNoStore(context);
    }

    [Fact]
    public async Task Refresh_InvalidMobileSession_ReturnsUnauthorizedWithoutChangingBrowserCookie()
    {
        var auth = new FakeAuthService { RejectRefresh = true };
        var (controller, context) = CreateController(auth);
        context.Request.Headers.Cookie = "msms_rt=unrelated-browser-session";

        var result = await controller.Refresh(new RefreshRequest("invalid-mobile-token"));

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        AssertNoStore(context);
    }
}

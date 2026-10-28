using System.Security.Claims;
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

/// <summary>WBS 4.1.2: revoke the caller's device before clearing its browser cookie.</summary>
public class LogoutControllerTests
{
    private sealed class FakeAuthService : IAuthService
    {
        public List<Guid> LogoutSessions { get; } = [];
        public bool FailLogout { get; init; }

        public Task LogoutAsync(Guid sessionId)
        {
            LogoutSessions.Add(sessionId);
            return FailLogout
                ? Task.FromException(new InvalidOperationException("Test storage unavailable."))
                : Task.CompletedTask;
        }

        public Task<LoginResponse> LoginAsync(LoginRequest request, string? userAgent) => throw new NotSupportedException();
        public Task<LoginResponse> RefreshAsync(RefreshRequest request) => throw new NotSupportedException();
        public Task<UserInfo> GetMeAsync(int accountId) => throw new NotSupportedException();
        public Task ChangePasswordAsync(int accountId, Guid? sessionId, ChangePasswordRequest request) => throw new NotSupportedException();
    }

    private static (AuthController Controller, DefaultHttpContext Context) CreateController(
        FakeAuthService auth, string? sessionId, string accountType = AccountTypes.Teacher, string scheme = "https")
    {
        var claims = new List<Claim>
        {
            new(CurrentUser.AccountIdClaim, "12"),
            new(CurrentUser.PersonIdClaim, "34"),
            new(CurrentUser.TypeClaim, accountType),
        };
        if (sessionId is not null) claims.Add(new Claim(CurrentUser.SessionIdClaim, sessionId));
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")),
        };
        context.Request.Scheme = scheme;
        context.Request.Headers.Cookie = "msms_rt=current-device-token";
        var user = new CurrentUser(new HttpContextAccessor { HttpContext = context });
        var controller = new AuthController(auth, user)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };
        return (controller, context);
    }

    [Theory]
    [InlineData(AccountTypes.SchoolAdmin)]
    [InlineData(AccountTypes.Principal)]
    [InlineData(AccountTypes.VicePrincipal)]
    [InlineData(AccountTypes.Teacher)]
    [InlineData(AccountTypes.Student)]
    [InlineData(AccountTypes.Parent)]
    public async Task Logout_AllSixRoles_RevokesOnlyCurrentSessionAndDeletesHttpOnlyCookie(string accountType)
    {
        var sid = Guid.NewGuid();
        var auth = new FakeAuthService();
        var (controller, context) = CreateController(auth, sid.ToString(), accountType);

        var result = await controller.Logout();

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(new[] { sid }, auth.LogoutSessions);
        var cookie = SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.Single()!);
        Assert.Equal("msms_rt", cookie.Name.Value);
        Assert.Equal("/api/auth", cookie.Path.Value);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
        Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Logout_LocalHttp_DeletesCookieWithoutSecureFlag()
    {
        var auth = new FakeAuthService();
        var (controller, context) = CreateController(auth, Guid.NewGuid().ToString(), scheme: "http");

        await controller.Logout();

        var cookie = SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.Single()!);
        Assert.False(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal("/api/auth", cookie.Path.Value);
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-session-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Logout_MissingOrInvalidSession_ReturnsUnauthorizedWithoutClaimingSuccess(string? sid)
    {
        var auth = new FakeAuthService();
        var (controller, context) = CreateController(auth, sid);

        var result = await controller.Logout();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        var json = JsonSerializer.SerializeToElement(unauthorized.Value);
        Assert.Equal(ErrorCodes.Unauthorized, json.GetProperty("code").GetString());
        Assert.Empty(auth.LogoutSessions);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Logout_RevocationFails_DoesNotDeleteCookieOrReturnSuccess()
    {
        var auth = new FakeAuthService { FailLogout = true };
        var (controller, context) = CreateController(auth, Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Logout());

        Assert.Single(auth.LogoutSessions);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString());
    }
}

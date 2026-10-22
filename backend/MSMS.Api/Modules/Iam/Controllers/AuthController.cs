using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Iam.Dtos;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Modules.Iam.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth, CurrentUser user) : ControllerBase
{
    private const string RefreshCookie = "msms_rt";

    // Cookie phiên (không đặt Expires): dùng chung giữa các tab, mất khi đóng trình duyệt.
    private CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/api/auth",
    };

    private void SetRefreshCookie(string? token)
    {
        if (!string.IsNullOrEmpty(token)) Response.Cookies.Append(RefreshCookie, token, RefreshCookieOptions());
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<LoginResponse> Login(LoginRequest request)
    {
        var res = await auth.LoginAsync(request, Request.Headers.UserAgent.ToString());
        SetRefreshCookie(res.RefreshToken);
        return res;
    }

    /// <summary>Đổi refresh token lấy access token mới; refresh token được xoay vòng mỗi lần.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<LoginResponse> Refresh(RefreshRequest? request)
    {
        // Web: refresh token nằm trong cookie HttpOnly; mobile: nằm trong body.
        var token = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(token)) token = Request.Cookies[RefreshCookie];
        try
        {
            var res = await auth.RefreshAsync(new RefreshRequest(token));
            SetRefreshCookie(res.RefreshToken);
            return res;
        }
        catch (ApiException)
        {
            Response.Cookies.Delete(RefreshCookie, RefreshCookieOptions());
            throw;
        }
    }

    [Authorize]
    [HttpGet("me")]
    public Task<UserInfo> Me() => auth.GetMeAsync(user.AccountId);

    /// <summary>Thu hồi phiên của thiết bị hiện tại: access token và refresh token đều mất hiệu lực ngay (BR-05).</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (user.SessionId is { } sid) await auth.LogoutAsync(sid);
        Response.Cookies.Delete(RefreshCookie, RefreshCookieOptions());
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        await auth.ChangePasswordAsync(user.AccountId, user.SessionId, request);
        return NoContent();
    }
}

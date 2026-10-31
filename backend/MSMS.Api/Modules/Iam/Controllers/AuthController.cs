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

    private void PreventTokenCaching()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        PreventTokenCaching();
        try
        {
            var res = await auth.LoginAsync(request, Request.Headers.UserAgent.ToString());
            if (string.Equals(request.ClientType, "mobile", StringComparison.OrdinalIgnoreCase)) return res;

            SetRefreshCookie(res.RefreshToken);
            return res with { RefreshToken = null };
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
        {
            // Lỗi xác thực là kết quả 401 bình thường, không để debugger dừng ở MVC.
            return Unauthorized(new { code = ex.Code, message = ex.Message });
        }
    }

    /// <summary>Đổi refresh token lấy access token mới; refresh token được xoay vòng mỗi lần.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest? request)
    {
        PreventTokenCaching();
        // Web: refresh token nằm trong cookie HttpOnly; mobile: nằm trong body.
        var mobile = !string.IsNullOrWhiteSpace(request?.RefreshToken);
        var token = mobile ? request!.RefreshToken : Request.Cookies[RefreshCookie];
        try
        {
            var res = await auth.RefreshAsync(new RefreshRequest(token));
            if (mobile) return res;

            SetRefreshCookie(res.RefreshToken);
            return res with { RefreshToken = null };
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
        {
            if (!mobile) Response.Cookies.Delete(RefreshCookie, RefreshCookieOptions());
            return Unauthorized(new { code = ex.Code, message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public Task<UserInfo> Me()
    {
        PreventTokenCaching();
        return auth.GetMeAsync(user.AccountId);
    }

    /// <summary>Thu hồi phiên của thiết bị hiện tại: access token và refresh token đều mất hiệu lực ngay (BR-05).</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        PreventTokenCaching();
        if (user.SessionId is not { } sid || sid == Guid.Empty)
            return Unauthorized(new { code = ErrorCodes.Unauthorized, message = "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Đăng nhập lại để tiếp tục." });

        await auth.LogoutAsync(sid);
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

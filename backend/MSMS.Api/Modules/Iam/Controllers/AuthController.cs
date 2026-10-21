using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Iam.Dtos;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Modules.Iam.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth, CurrentUser user) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public Task<LoginResponse> Login(LoginRequest request) => auth.LoginAsync(request);

    [Authorize]
    [HttpGet("me")]
    public Task<UserInfo> Me() => auth.GetMeAsync(user.AccountId);

    /// <summary>JWT không trạng thái: đăng xuất kết thúc phiên trên thiết bị hiện tại bằng cách client bỏ token (BR-05).</summary>
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout() => NoContent();

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        await auth.ChangePasswordAsync(user.AccountId, request);
        return NoContent();
    }
}

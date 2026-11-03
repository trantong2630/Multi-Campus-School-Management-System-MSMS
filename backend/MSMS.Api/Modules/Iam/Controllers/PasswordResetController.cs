using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MSMS.Api.Modules.Iam.Dtos;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Modules.Iam.Controllers;

[ApiController, Route("api/auth"), AllowAnonymous]
[EnableRateLimiting(IamModule.PasswordResetRatePolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PasswordResetController(PasswordResetService service) : ControllerBase
{
    /// <summary>4.1.21, 4.1.23: cùng phản hồi cho tài khoản không tồn tại, không hợp lệ, thiếu email hoặc SMTP hỏng.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> RequestCode(ForgotPasswordRequest request, CancellationToken ct)
    {
        await service.RequestAsync(request, ct);
        Response.Headers.RetryAfter = PasswordResetState.ResendSeconds.ToString();
        return NoContent();
    }

    [HttpPost("verify-reset-code")]
    public Task<PasswordResetVerificationResponse> VerifyCode(VerifyPasswordResetRequest request, CancellationToken ct)
        => service.VerifyAsync(request, ct);

    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset(ResetPasswordRequest request, CancellationToken ct)
    {
        await service.ResetAsync(request, ct);
        return NoContent();
    }
}

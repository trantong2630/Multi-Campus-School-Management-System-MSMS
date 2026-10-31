using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Common.Auth;

/// <summary>BR-05: đọc trạng thái tài khoản mỗi request. Khóa giữa phiên bị từ chối ngay; tài khoản chờ đổi mật khẩu chỉ gọi được vài đường.</summary>
public sealed class AccountStatusMiddleware(RequestDelegate next)
{
    private static readonly string[] AllowedWhenPending = ["/api/auth/change-password", "/api/auth/me", "/api/auth/logout"];

    public async Task InvokeAsync(HttpContext context, MsmsDbContext db, CurrentUser user, SessionService sessions)
    {
        if (user.IsAuthenticated)
        {
            var failure = await new AccountAccessValidator(db, sessions).ValidateAsync(context.User);
            var allowedPending = failure?.Code == ErrorCodes.PasswordChangeRequired
                && AllowedWhenPending.Any(path => context.Request.Path.Equals(new PathString(path), StringComparison.OrdinalIgnoreCase));
            if (failure is not null && !allowedPending)
            {
                await ExceptionMiddleware.Write(context, failure.StatusCode, failure.Code, failure.Message);
                return;
            }
        }
        await next(context);
    }
}

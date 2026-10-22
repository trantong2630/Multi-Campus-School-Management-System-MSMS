using Microsoft.EntityFrameworkCore;
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
            // Phiên đã đăng xuất hoặc bị thu hồi thì access token còn hạn cũng bị từ chối (BR-05).
            if (user.SessionId is not { } sid || !await sessions.IsActiveAsync(sid, user.AccountId))
            {
                await ExceptionMiddleware.Write(context, 401, ErrorCodes.Unauthorized,
                    "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Đăng nhập lại để tiếp tục.");
                return;
            }

            var status = await db.Account.AsNoTracking()
                .Where(a => a.AccountId == user.AccountId)
                .Select(a => a.Status)
                .FirstOrDefaultAsync();

            if (status is null or AccountStatuses.Locked)
            {
                await ExceptionMiddleware.Write(context, 401, ErrorCodes.AccountLocked,
                    "Tài khoản đã bị khóa. Liên hệ quản trị để được hỗ trợ.");
                return;
            }

            if (status == AccountStatuses.PendingPasswordChange &&
                !AllowedWhenPending.Contains(context.Request.Path.Value?.ToLowerInvariant()))
            {
                await ExceptionMiddleware.Write(context, 403, ErrorCodes.PasswordChangeRequired,
                    "Cần đổi mật khẩu ở lần đăng nhập đầu tiên.");
                return;
            }
        }
        await next(context);
    }
}

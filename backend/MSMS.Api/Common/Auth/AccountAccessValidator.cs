using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Common.Auth;

public sealed record AccountAccessFailure(int StatusCode, string Code, string Message);

/// <summary>IAM-01 4.1.6: REST và từng lệnh Hub cùng kiểm phiên, trạng thái và danh tính hiện tại.</summary>
public sealed class AccountAccessValidator(MsmsDbContext db, SessionService sessions)
{
    public async Task<AccountAccessFailure?> ValidateAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true
            || !int.TryParse(principal.FindFirstValue(CurrentUser.AccountIdClaim), out var accountId)
            || !int.TryParse(principal.FindFirstValue(CurrentUser.PersonIdClaim), out var personId)
            || !Guid.TryParse(principal.FindFirstValue(CurrentUser.SessionIdClaim), out var sessionId)
            || !await sessions.IsActiveAsync(sessionId, accountId))
            return new(401, ErrorCodes.Unauthorized, "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Đăng nhập lại để tiếp tục.");

        var account = await db.Account.AsNoTracking().Where(a => a.AccountId == accountId)
            .Select(a => new { a.Status, a.AccountType, a.PersonId }).FirstOrDefaultAsync();
        if (account is null || account.Status == AccountStatuses.Locked)
            return new(401, ErrorCodes.AccountLocked, "Tài khoản đã bị khóa. Liên hệ quản trị để được hỗ trợ.");
        if (account.PersonId != personId || account.AccountType != principal.FindFirstValue(CurrentUser.TypeClaim)
            || account.Status is not (AccountStatuses.Active or AccountStatuses.PendingPasswordChange))
            return new(401, ErrorCodes.Unauthorized, "Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Đăng nhập lại để tiếp tục.");
        if (account.Status == AccountStatuses.PendingPasswordChange)
            return new(403, ErrorCodes.PasswordChangeRequired, "Cần đổi mật khẩu ở lần đăng nhập đầu tiên.");
        return null;
    }
}

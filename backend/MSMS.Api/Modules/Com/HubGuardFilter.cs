using Microsoft.AspNetCore.SignalR;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Modules.Iam.Services;
using Microsoft.EntityFrameworkCore;

namespace MSMS.Api.Modules.Com;

/// <summary>
/// COM-4.8.6: kết nối đi qua cùng kiểm tài khoản và phiên như REST (BR-05). Mỗi lần kết nối và mỗi lệnh,
/// tài khoản bị khóa hoặc phiên bị thu hồi thì ngắt kết nối ngay.
/// </summary>
public sealed class HubGuardFilter(MsmsDbContext db, SessionService sessions) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        await EnsureActiveAsync(context.Context);
        return await next(context);
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await EnsureActiveAsync(context.Context);
        await next(context);
    }

    private async Task EnsureActiveAsync(HubCallerContext ctx)
    {
        var user = ctx.User;
        var ok = int.TryParse(user?.FindFirst(CurrentUser.AccountIdClaim)?.Value, out var accountId)
                 && Guid.TryParse(user?.FindFirst(CurrentUser.SessionIdClaim)?.Value, out var sessionId)
                 && await sessions.IsActiveAsync(sessionId, accountId)
                 && await db.Account.AsNoTracking().AnyAsync(a => a.AccountId == accountId && a.Status != AccountStatuses.Locked);
        if (ok) return;
        ctx.Abort();
        throw new HubException("Phiên đăng nhập đã hết hạn hoặc tài khoản đã bị khóa.");
    }
}

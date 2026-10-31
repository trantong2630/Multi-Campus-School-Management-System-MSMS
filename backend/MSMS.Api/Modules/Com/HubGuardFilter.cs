using Microsoft.AspNetCore.SignalR;
using MSMS.Api.Common.Auth;

namespace MSMS.Api.Modules.Com;

/// <summary>
/// COM-4.8.6: kết nối đi qua cùng kiểm tài khoản và phiên như REST (BR-05). Mỗi lần kết nối và mỗi lệnh,
/// tài khoản bị khóa hoặc phiên bị thu hồi thì ngắt kết nối ngay.
/// </summary>
public sealed class HubGuardFilter(AccountAccessValidator access) : IHubFilter
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
        var failure = await access.ValidateAsync(ctx.User ?? new());
        if (failure is null) return;
        ctx.Abort();
        throw new HubException(failure.Message);
    }
}

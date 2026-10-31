using Microsoft.AspNetCore.SignalR;
using MSMS.Api.Common.Auth;

namespace MSMS.Api.Modules.Com;

/// <summary>WebSocket không chạy lại HTTP middleware cho từng lệnh nên phải kiểm phiên ở Hub filter.</summary>
public sealed class AccountStatusHubFilter(AccountAccessValidator access) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        await EnsureAccessAsync(context.Context);
        return await next(context);
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await EnsureAccessAsync(context.Context);
        await next(context);
    }

    private async Task EnsureAccessAsync(HubCallerContext context)
    {
        var failure = await access.ValidateAsync(context.User ?? new());
        if (failure is null) return;
        context.Abort();
        throw new HubException(failure.Message);
    }
}

using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Ai;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Adm.Ai;
using MSMS.Api.Modules.Adm.Services;

namespace MSMS.Api.Modules.Adm;

public static class AdmModule
{
    public const string AiChatRatePolicy = "adm-ai-chat";

    public static IServiceCollection AddAdmModule(this IServiceCollection s)
    {
        // MSMS AI: giới hạn số câu hỏi mỗi tài khoản mỗi phút (Ai:ChatPerUserPerMinute) để không đốt hạn mức của nhà cung cấp AI.
        s.AddRateLimiter(options => options.AddPolicy(AiChatRatePolicy, context =>
        {
            var limit = context.RequestServices.GetRequiredService<IOptions<AiOptions>>().Value.ChatPerUserPerMinute;
            return RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(CurrentUser.AccountIdClaim) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1, limit), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
        }));
        return s
            .AddScoped<DashboardService>()
            .AddScoped<AdminDashboardService>()
            .AddScoped<HomeroomOverviewService>()
            .AddScoped<DepartmentBoardService>()
            .AddScoped<AuditLogService>()
            .AddScoped<ExportService>()
            .AddScoped<AdmAiChatService>();
    }
}

using System.Threading.RateLimiting;
using MSMS.Api.Common.Email;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Modules.Iam;

public static class IamModule
{
    public const string PasswordResetRatePolicy = "iam-password-reset";

    public static IServiceCollection AddIamModule(this IServiceCollection s)
    {
        s.AddOptions<SmtpOptions>().BindConfiguration("Smtp");
        s.AddRateLimiter(options =>
        {
            options.AddPolicy(PasswordResetRatePolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                }));
            options.OnRejected = (context, _) => new ValueTask(ExceptionMiddleware.Write(context.HttpContext,
                StatusCodes.Status429TooManyRequests, "TOO_MANY_REQUESTS", "Bạn thao tác quá nhanh. Đợi một phút rồi thử lại."));
        });
        return s.AddScoped<SessionService>()
            .AddScoped<IAuthService, AuthService>()
            .AddScoped<IOtpEmailSender, GmailOtpSender>()
            .AddScoped<PasswordResetService>()
            .AddScoped<AccountService>()
            .AddScoped<ProfileService>();
    }
}

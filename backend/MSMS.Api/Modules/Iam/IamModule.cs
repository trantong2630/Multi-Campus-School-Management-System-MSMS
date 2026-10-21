using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Modules.Iam;

public static class IamModule
{
    public static IServiceCollection AddIamModule(this IServiceCollection s)
        => s.AddScoped<IAuthService, AuthService>()
            .AddScoped<AccountService>();
}

using MSMS.Api.Modules.Adm.Services;

namespace MSMS.Api.Modules.Adm;

public static class AdmModule
{
    public static IServiceCollection AddAdmModule(this IServiceCollection s)
        => s.AddScoped<DashboardService>();
}

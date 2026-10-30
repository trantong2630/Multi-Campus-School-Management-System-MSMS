using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Api.Modules.Ppl;

public static class PplModule
{
    public static IServiceCollection AddPplModule(this IServiceCollection s)
        => s.AddScoped<StaffService>()
            .AddScoped<StaffImportService>()
            .AddScoped<ResponsibilityService>()
            .AddScoped<HomeroomService>();
}

using MSMS.Api.Modules.Par.Services;

namespace MSMS.Api.Modules.Par;

public static class ParModule
{
    public static IServiceCollection AddParModule(this IServiceCollection s)
        => s.AddScoped<LeaveRequestService>()
            .AddScoped<HomeroomLeaveService>();

}

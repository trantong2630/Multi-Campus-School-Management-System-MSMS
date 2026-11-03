using MSMS.Api.Modules.Att.Services;

namespace MSMS.Api.Modules.Att;

public static class AttModule
{
    public static IServiceCollection AddAttModule(this IServiceCollection s) => s.AddScoped<AttendanceService>();
}

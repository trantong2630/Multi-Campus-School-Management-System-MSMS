using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd;

public static class ScdModule
{
    public static IServiceCollection AddScdModule(this IServiceCollection s)
        => s.AddScoped<TimetableTemplateService>()
            .AddScoped<TimetableImportService>()
            .AddScoped<TimetablePublishService>()
            .AddScoped<ScheduleQueryService>();
}

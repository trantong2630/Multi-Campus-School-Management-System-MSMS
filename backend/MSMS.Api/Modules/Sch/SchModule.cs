using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch;

public static class SchModule
{
    public static IServiceCollection AddSchModule(this IServiceCollection s)
        => s.AddScoped<CampusService>()
            .AddScoped<CatalogService>()
            .AddScoped<CalendarService>()
            .AddScoped<PeriodSlotService>()
            .AddScoped<ClassService>()
            .AddScoped<EnrollmentService>()
            .AddScoped<SchoolProfileService>();
}

using MSMS.Api.Common.Email;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd;

public static class ScdModule
{
    public static IServiceCollection AddScdModule(this IServiceCollection s)
    {
        // Email phân công dạy thay / tiết bù (lệch BR-26, xem AssignmentEmailNotifier). Sender không giữ trạng thái nên dùng singleton.
        s.AddOptions<SmtpOptions>().BindConfiguration("Smtp");
        s.AddSingleton<IEmailSender, SmtpEmailSender>();
        return s.AddScoped<TimetableTemplateService>()
            .AddScoped<TimetableImportService>()
            .AddScoped<TimetablePublishService>()
            .AddScoped<TimetableDraftService>()
            .AddScoped<ScheduleQueryService>()
            .AddScoped<SubjectResponsibilityService>()
            .AddScoped<ScheduleSupport>()
            .AddScoped<ScheduleAdjustmentService>()
            .AddScoped<TeacherAbsenceService>()
            .AddScoped<AssignmentEmailNotifier>();
    }
}

using MSMS.Api.Common.Ai;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Api.Modules.Lrn;

public static class LrnModule
{
    public static IServiceCollection AddLrnModule(this IServiceCollection s, IConfiguration config)
    {
        s.Configure<AiOptions>(config.GetSection("Ai"));
        // Thời gian chờ thật do GeminiClient tự đặt theo AiOptions; HttpClient chỉ cần rộng hơn.
        s.AddHttpClient<IAiClient, GeminiClient>(c => c.Timeout = TimeSpan.FromMinutes(3));
        return s.AddSingleton<AnalysisJob>()
            .AddScoped<LrnAccess>()
            .AddScoped<QuestionService>()
            .AddScoped<QuestionDraftService>()
            .AddScoped<AssignmentService>()
            .AddScoped<AttemptGrader>()
            .AddScoped<AttemptService>()
            .AddScoped<ResultService>()
            .AddScoped<AnalysisService>()
            .AddScoped<DepartmentService>();
    }
}

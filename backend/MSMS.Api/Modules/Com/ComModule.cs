using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Data;
using MSMS.Api.Modules.Com.Services;

namespace MSMS.Api.Modules.Com;

public static class ComModule
{
    /// <summary>
    /// Đăng ký dịch vụ của Com module: trung tâm thông báo (COM-01), đẩy realtime sau commit,
    /// theo dõi online và bộ lọc kiểm tài khoản/phiên cho hub. AddSignalR và MapHub nằm ở Program.cs.
    /// </summary>
    public static IServiceCollection AddComModule(this IServiceCollection services)
    {
        services.AddScoped<NotificationService>();
        services.AddScoped<NotificationPushInterceptor>();
        services.AddScoped<PostService>();   // COM-02 bảng tin
        services.AddScoped<PostImageStorage>();
        // COM-4.8.7: gắn interceptor vào DbContext từ module Com, không đụng tới Program.cs.
        services.ConfigureDbContext<MsmsDbContext>((sp, o) => o.AddInterceptors(sp.GetRequiredService<NotificationPushInterceptor>()));
        services.AddSingleton<PresenceTracker>();
        services.AddScoped<HubGuardFilter>();
        services.Configure<HubOptions>(o => o.AddFilter<HubGuardFilter>());
        return services;
    }
}

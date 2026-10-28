namespace MSMS.Api.Modules.Com;

public static class ComModule
{
    /// <summary>
    /// Đăng ký các dịch vụ của Com module.
    /// Hiện tại chỉ có CommunicationHub để xử lý realtime events.
    /// </summary>
    public static IServiceCollection AddComModule(this IServiceCollection services)
    {
        // SignalR Hub được đăng ký ở Program.cs bằng AddSignalR()
        // và MapHub ở app.MapHub
        return services;
    }
}

using Microsoft.AspNetCore.SignalR;

namespace MSMS.Api.Modules.Com;

/// <summary>
/// Cấu hình xác thực cho SignalR Hub.
/// COM-4.8.6: Token được truyền qua query string access_token, hub kiểm như REST.
/// </summary>
public class HubAuthenticationOptions : HubOptions
{
    public HubAuthenticationOptions()
    {
        // Token được lấy từ query string khi kết nối WebSocket
        // SignalR framework mặc định hỗ trợ lấy từ query string
        // Không cần cấu hình gì thêm vì middleware JWT đã xử lý
    }
}

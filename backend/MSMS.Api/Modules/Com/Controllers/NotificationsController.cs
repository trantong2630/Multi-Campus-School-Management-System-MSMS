using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Modules.Com.Dtos;
using MSMS.Api.Modules.Com.Services;

namespace MSMS.Api.Modules.Com.Controllers;

/// <summary>COM-01: trung tâm thông báo của chính người đăng nhập (BR-26).</summary>
[ApiController, Route("api/com/notifications"), Authorize]
public class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet]
    public Task<NotificationListResultDto> List([FromQuery] NotificationQueryDto query) => service.ListAsync(query);

    [HttpGet("unread-count")]
    public Task<UnreadCountDto> UnreadCount() => service.UnreadCountAsync();

    [HttpPost("{id:long}/open")]
    public Task<OpenNotificationResultDto> Open(long id) => service.OpenAsync(id);

    [HttpPut("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id) { await service.MarkReadAsync(id); return NoContent(); }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead() => Ok(new { updated = await service.MarkAllReadAsync() });
}

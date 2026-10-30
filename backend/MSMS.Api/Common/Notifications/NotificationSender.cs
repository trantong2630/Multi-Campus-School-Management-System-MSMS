using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Common.Notifications;

public static class NotificationTypes
{
    public const string TimetablePublished = "TimetablePublished";
    public const string SessionPending = "SessionPending";
    public const string ScheduleChanged = "ScheduleChanged";
    public const string AssignmentAssigned = "AssignmentAssigned";
}

/// <summary>
/// Cổng duy nhất tạo thông báo trong ứng dụng (BR-26, danh sách đóng). Chỉ thêm vào context,
/// người gọi SaveChanges để thông báo cùng transaction với sự kiện sinh ra nó.
/// </summary>
public sealed class NotificationSender(MsmsDbContext db, IClock clock)
{
    public void Add(IEnumerable<int> recipientPersonIds, string type, string title, string? body = null,
        string? refEntityType = null, long? refEntityId = null)
    {
        var now = clock.Now;
        foreach (var id in recipientPersonIds.Distinct())
        {
            db.Notification.Add(new Notification
            {
                RecipientPersonId = id, NotificationType = type, Title = title, Body = body,
                RefEntityType = refEntityType, RefEntityId = refEntityId, CreatedAt = now,
            });
        }
    }
}

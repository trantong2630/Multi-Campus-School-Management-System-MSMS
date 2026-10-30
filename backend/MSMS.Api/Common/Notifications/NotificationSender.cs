using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Common.Notifications;

/// <summary>
/// Danh sách đóng các loại thông báo (BR-26). Thêm sự kiện mới = sửa spec trước, rồi mới thêm ở đây.
/// </summary>
public static class NotificationTypes
{
    public const string TimetablePublished = "TimetablePublished";
    public const string SessionPending = "SessionPending";
    /// <summary>Đổi giáo viên, hủy, thêm bù, dạy thay, trả lại giáo viên khi xóa khai báo vắng.</summary>
    public const string ScheduleChanged = "ScheduleChanged";
    public const string LeaveRequestSubmitted = "LeaveRequestSubmitted";
    public const string LeaveRequestDecided = "LeaveRequestDecided";
    public const string AssignmentAssigned = "AssignmentAssigned";
    public const string MessageReceived = "MessageReceived";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        TimetablePublished, SessionPending, ScheduleChanged, LeaveRequestSubmitted,
        LeaveRequestDecided, AssignmentAssigned, MessageReceived,
    };
}

/// <summary>
/// Cổng duy nhất tạo thông báo trong ứng dụng (BR-26, danh sách đóng). Chỉ thêm vào context,
/// người gọi SaveChanges để thông báo cùng transaction với sự kiện sinh ra nó.
/// Các thông báo vừa thêm được giữ lại để <see cref="NotificationPushInterceptor"/> đẩy realtime
/// sau khi SaveChanges thành công (COM-4.8.7); chữ ký <see cref="Add"/> không đổi.
/// </summary>
public sealed class NotificationSender(MsmsDbContext db, IClock clock)
{
    private readonly List<Notification> _pending = [];

    public void Add(IEnumerable<int> recipientPersonIds, string type, string title, string? body = null,
        string? refEntityType = null, long? refEntityId = null)
    {
        if (!NotificationTypes.All.Contains(type))
            throw new ArgumentException($"Loại thông báo '{type}' không thuộc danh sách đóng (BR-26).", nameof(type));

        var now = clock.Now;
        foreach (var id in recipientPersonIds.Distinct())
        {
            var n = new Notification
            {
                RecipientPersonId = id, NotificationType = type, Title = title, Body = body,
                RefEntityType = refEntityType, RefEntityId = refEntityId, CreatedAt = now,
            };
            db.Notification.Add(n);
            _pending.Add(n);
        }
    }

    /// <summary>Lấy và xóa các thông báo chờ đẩy. Chỉ gọi sau khi SaveChanges đã thành công.</summary>
    internal IReadOnlyList<Notification> TakePending()
    {
        var items = _pending.ToArray();
        _pending.Clear();
        return items;
    }

    /// <summary>Bỏ các thông báo chờ đẩy khi SaveChanges thất bại, để không đẩy nhầm ở lần lưu sau.</summary>
    internal void DiscardPending() => _pending.Clear();
}

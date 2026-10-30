using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Modules.Com.Dtos;

namespace MSMS.Api.Modules.Com.Services;

/// <summary>
/// Trung tâm thông báo (COM-01, BR-26): xem danh sách của chính mình, đánh dấu đã đọc,
/// mở thông báo và kiểm lại quyền trên bản ghi nguồn trước khi trả đường dẫn đích.
/// </summary>
public sealed class NotificationService(MsmsDbContext db, CurrentUser user, IClock clock)
{
    private const int MaxPageSize = 100;

    /// <summary>4.8.2 — Danh sách thông báo của chính mình, mới nhất trước.</summary>
    public async Task<NotificationListResultDto> ListAsync(NotificationQueryDto q)
    {
        var me = user.PersonId;
        var mine = db.Notification.AsNoTracking().Where(n => n.RecipientPersonId == me);
        var unread = await mine.CountAsync(n => !n.IsRead);

        var filtered = q.IsRead is { } read ? mine.Where(n => n.IsRead == read) : mine;
        var page = await filtered
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId)
            .Select(n => new NotificationItemDto(n.NotificationId, n.RecipientPersonId, n.NotificationType, n.Title, n.Body,
                n.RefEntityType, n.RefEntityId, n.IsRead, n.ReadAt, n.CreatedAt))
            .ToPagedAsync(q.Page, Math.Min(q.PageSize, MaxPageSize));
        return new NotificationListResultDto(page.Items, page.Total, unread);
    }

    public async Task<UnreadCountDto> UnreadCountAsync()
    {
        var me = user.PersonId;
        return new UnreadCountDto(await db.Notification.CountAsync(n => n.RecipientPersonId == me && !n.IsRead));
    }

    /// <summary>4.8.4 — Đánh dấu một thông báo đã đọc. Thông báo của người khác coi như không tồn tại.</summary>
    public async Task MarkReadAsync(long id)
    {
        var n = await FindMineAsync(id);
        MarkRead(n);
        await db.SaveChangesAsync();
    }

    /// <summary>4.8.4 — Đánh dấu tất cả thông báo của mình đã đọc.</summary>
    public async Task<int> MarkAllReadAsync()
    {
        var me = user.PersonId;
        var now = clock.Now;
        var unread = await db.Notification.Where(n => n.RecipientPersonId == me && !n.IsRead).ToListAsync();
        foreach (var n in unread) { n.IsRead = true; n.ReadAt = now; }
        await db.SaveChangesAsync();
        return unread.Count;
    }

    /// <summary>
    /// 4.8.3 — Mở thông báo: đánh dấu đã đọc rồi kiểm lại quyền trên bản ghi nguồn tại thời điểm mở (BR-26).
    /// Có thông báo không đồng nghĩa có quyền: không đủ quyền thì trả CanAccess = false, không trả đường dẫn.
    /// </summary>
    public async Task<OpenNotificationResultDto> OpenAsync(long id)
    {
        var n = await FindMineAsync(id);
        MarkRead(n);
        await db.SaveChangesAsync();

        var (allowed, route, reason) = await ResolveTargetAsync(n);
        return new OpenNotificationResultDto(n.NotificationId, n.IsRead, allowed, allowed ? route : null,
            n.RefEntityType, n.RefEntityId, allowed ? null : reason);
    }

    private async Task<Data.Entities.Notification> FindMineAsync(long id)
    {
        var me = user.PersonId;
        return await db.Notification.FirstOrDefaultAsync(n => n.NotificationId == id && n.RecipientPersonId == me)
               ?? throw ApiException.NotFound("Không tìm thấy thông báo.");
    }

    private void MarkRead(Data.Entities.Notification n)
    {
        if (n.IsRead) return;
        n.IsRead = true;
        n.ReadAt = clock.Now;
    }

    private const string Denied = "Bạn không còn quyền xem nội dung này.";
    private const string Gone = "Nội dung này không còn tồn tại.";

    private async Task<(bool Allowed, string? Route, string? Reason)> ResolveTargetAsync(Data.Entities.Notification n)
    {
        var me = user.PersonId;
        var type = user.AccountType;
        var today = clock.Today;
        var isStudent = type == AccountTypes.Student;
        var isParent = type == AccountTypes.Parent;
        var isTeacher = type == AccountTypes.Teacher;
        var isVice = type == AccountTypes.VicePrincipal;
        var scheduleRoute = isStudent || isParent ? "/scd/my-class" : "/scd/schedule";

        // Học sinh có ghi danh hiệu lực vào lớp (hoặc con của phụ huynh) — dùng chung cho các nguồn theo lớp.
        IQueryable<int> myStudentIds = isStudent
            ? db.Enrollment.Where(e => e.StudentId == me).Select(e => e.StudentId)
            : isParent
                ? db.ParentStudentLink.Where(l => l.ParentId == me).Select(l => l.StudentId)
                : db.StudentProfile.Where(_ => false).Select(s => s.StudentId);
        bool InClass(IQueryable<Data.Entities.Enrollment> e, int classId) =>
            e.Any(x => x.ClassId == classId && x.StartDate <= today && (x.EndDate == null || x.EndDate >= today));
        IQueryable<Data.Entities.Enrollment> myEnrollments() =>
            db.Enrollment.AsNoTracking().Where(e => myStudentIds.Contains(e.StudentId));

        switch (n.NotificationType)
        {
            case NotificationTypes.TimetablePublished when n.RefEntityId is { } versionId:
            {
                var v = (int)versionId;
                var classIds = db.TimetableVersionClass.Where(c => c.VersionId == v).Select(c => c.ClassId);
                var ok = isTeacher
                    ? await db.TimetableEntry.AnyAsync(e => e.VersionId == v && e.TeacherId == me)
                    : isVice
                        ? await db.TimetableVersion.Where(x => x.VersionId == v)
                            .AnyAsync(x => db.CampusLeadership.Any(l => l.CampusId == x.CampusId && l.StaffId == me
                                                                         && l.StartDate <= today && (l.EndDate == null || l.EndDate >= today)))
                        : await myEnrollments().AnyAsync(e => classIds.Contains(e.ClassId) && e.StartDate <= today && (e.EndDate == null || e.EndDate >= today));
                return ok ? (true, scheduleRoute, null) : (false, null, Denied);
            }

            case NotificationTypes.ScheduleChanged when n.RefEntityType == "TeachingSession" && n.RefEntityId is { } sid:
            {
                var s = await db.TeachingSession.AsNoTracking().Where(x => x.SessionId == sid)
                    .Select(x => new { x.ClassId, x.TeacherId, x.Class.CampusId }).FirstOrDefaultAsync();
                if (s is null) return (false, null, Gone);
                var ok = s.TeacherId == me
                         || (isVice && await db.CampusLeadership.AnyAsync(l => l.CampusId == s.CampusId && l.StaffId == me
                                                                                && l.StartDate <= today && (l.EndDate == null || l.EndDate >= today)))
                         || ((isStudent || isParent) && InClass(myEnrollments(), s.ClassId));
                return ok ? (true, scheduleRoute, null) : (false, null, Denied);
            }

            case NotificationTypes.SessionPending:
                // Chỉ người phụ trách phân hiệu còn hiệu lực xử lý tiết Chờ xử lý.
                return isVice && await db.CampusLeadership.AnyAsync(l => l.StaffId == me && l.StartDate <= today && (l.EndDate == null || l.EndDate >= today))
                    ? (true, "/scd/schedule", null)
                    : (false, null, Denied);

            case NotificationTypes.AssignmentAssigned when n.RefEntityId is { } aid:
            {
                var a = (int)aid;
                var exists = await db.Assignment.AsNoTracking().Where(x => x.AssignmentId == a)
                    .Select(x => new { x.AssignedByPersonId }).FirstOrDefaultAsync();
                if (exists is null) return (false, null, Gone);
                if (isStudent)
                    return await db.AssignmentRecipient.AnyAsync(r => r.AssignmentId == a && r.StudentId == me)
                        ? (true, "/lrn/todo", null) : (false, null, Denied);
                if (isParent)
                    return await db.AssignmentRecipient.AnyAsync(r => r.AssignmentId == a && myStudentIds.Contains(r.StudentId))
                        ? (true, "/lrn/children", null) : (false, null, Denied);
                return isTeacher && exists.AssignedByPersonId == me ? (true, "/lrn/assigned", null) : (false, null, Denied);
            }

            case NotificationTypes.MessageReceived when n.RefEntityId is { } cid:
            {
                var c = (int)cid;
                var c0 = await db.Conversation.AsNoTracking().Where(x => x.ConversationId == c)
                    .Select(x => new { x.ParentId, x.StaffId }).FirstOrDefaultAsync();
                if (c0 is null) return (false, null, Gone);
                return !user.IsSchoolAdmin && (c0.ParentId == me || c0.StaffId == me)
                    ? (true, $"/par/inbox?conversationId={c}", null) : (false, null, Denied);
            }

            case NotificationTypes.LeaveRequestSubmitted or NotificationTypes.LeaveRequestDecided when n.RefEntityId is { } lid:
            {
                var l = (int)lid;
                var r = await db.LeaveRequest.AsNoTracking().Where(x => x.LeaveRequestId == l)
                    .Select(x => new { x.ParentId, x.ClassId, x.Class.CampusId }).FirstOrDefaultAsync();
                if (r is null) return (false, null, Gone);
                if (isParent)
                    return r.ParentId == me ? (true, "/par/leave-requests", null) : (false, null, Denied);
                var ok = (isTeacher && await db.HomeroomAssignment.AnyAsync(h => h.ClassId == r.ClassId && h.StaffId == me
                                                                                  && h.StartDate <= today && (h.EndDate == null || h.EndDate >= today)))
                         || (isVice && await db.CampusLeadership.AnyAsync(x => x.CampusId == r.CampusId && x.StaffId == me
                                                                               && x.StartDate <= today && (x.EndDate == null || x.EndDate >= today)));
                return ok ? (true, "/par/leave-requests", null) : (false, null, Denied);
            }

            default:
                return (false, null, Gone);
        }
    }
}

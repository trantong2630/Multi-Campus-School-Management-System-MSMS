using System.Text.Json;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Common.Audit;

public static class AuditCategories
{
    public const string Account = "Account";
    public const string TimetablePublish = "TimetablePublish";
    public const string ScheduleAdjust = "ScheduleAdjust";
}

/// <summary>
/// Chỉ ghi thêm (BR-28): không có hàm sửa hay xóa. Chỉ ghi ba nhóm sự kiện của spec.
/// Hàm Add không gọi SaveChanges để bản ghi audit nằm cùng transaction với thao tác nghiệp vụ.
/// </summary>
public sealed class AuditWriter(MsmsDbContext db, CurrentUser user, IClock clock)
{
    public void Add(string category, string action, IEnumerable<int>? targetIds = null, object? detail = null)
    {
        db.AuditLog.Add(new AuditLog
        {
            Category = category,
            Action = action,
            ActorPersonId = user.PersonId,
            TargetIds = targetIds is null ? null : JsonSerializer.Serialize(targetIds),
            Detail = detail is null ? null : JsonSerializer.Serialize(detail),
            CreatedAt = clock.Now,
        });
    }
}

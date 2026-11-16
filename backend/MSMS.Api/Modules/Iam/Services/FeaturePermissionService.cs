using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Dtos;
using MSMS.Api.Modules.Iam.Permissions;

namespace MSMS.Api.Modules.Iam.Services;

/// <summary>
/// Phân quyền động: School Admin bật/tắt chức năng theo loại tài khoản, chỉ trong trần của FeatureCatalog.
/// Đây là lớp kiểm tra thêm: policy (tầng 1) và kiểm phạm vi trong service (tầng 2) vẫn giữ nguyên.
/// </summary>
public sealed class FeaturePermissionService(MsmsDbContext db, IClock clock, CurrentUser user, AuditWriter audit, IMemoryCache cache)
{
    private const string CacheKey = "iam:feature-disabled";

    private static readonly string[] Types =
    [
        AccountTypes.SchoolAdmin, AccountTypes.Principal, AccountTypes.VicePrincipal,
        AccountTypes.Teacher, AccountTypes.Student, AccountTypes.Parent,
    ];

    private static string TypeLabel(string t) => t switch
    {
        AccountTypes.SchoolAdmin => "School Admin", AccountTypes.Principal => "Hiệu trưởng", AccountTypes.VicePrincipal => "Hiệu phó",
        AccountTypes.Teacher => "Giáo viên", AccountTypes.Student => "Học sinh", AccountTypes.Parent => "Phụ huynh", _ => t,
    };

    /// <summary>Chức năng đang tắt của một loại tài khoản. Đọc từ cache, cache bị xóa ngay khi Admin lưu.</summary>
    public async Task<IReadOnlySet<string>> DisabledForAsync(string accountType)
    {
        var all = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var rows = await db.FeaturePermission.AsNoTracking().Where(p => !p.IsEnabled)
                .Select(p => new { p.AccountType, p.FeatureKey }).ToListAsync();
            return rows.GroupBy(r => r.AccountType)
                .ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(r => r.FeatureKey).ToHashSet());
        });
        var disabled = all?.GetValueOrDefault(accountType) ?? new HashSet<string>();
        // Chức năng đã khóa hoặc ra khỏi trần (danh mục đổi về sau) thì bỏ qua dòng cũ trong DB.
        return disabled.Where(k => FeatureCatalog.Find(k) is { } f && FeatureCatalog.IsToggleable(f, accountType)).ToHashSet();
    }

    public async Task<FeatureMatrix> GetMatrixAsync()
    {
        var rows = await db.FeaturePermission.AsNoTracking().ToListAsync();
        var disabled = rows.Where(r => !r.IsEnabled).Select(r => (r.AccountType, r.FeatureKey)).ToHashSet();
        var last = rows.OrderByDescending(r => r.UpdatedAt).FirstOrDefault();
        string? lastBy = last is null ? null
            : await db.Account.AsNoTracking().Where(a => a.AccountId == last.UpdatedBy).Select(a => a.Person.FullName).FirstOrDefaultAsync();

        var features = FeatureCatalog.Features.Select(f => new FeatureRow(f.Key, f.Group, f.Label,
            Types.Select(t =>
            {
                var allowed = f.Ceiling.Contains(t);
                return new FeatureCell(t, allowed, allowed && f.Locked, allowed && (f.Locked || !disabled.Contains((t, f.Key))));
            }).ToList())).ToList();
        return new FeatureMatrix([.. Types], features, last?.UpdatedAt, lastBy);
    }

    private static void Validate(List<FeatureChange> changes)
    {
        if (changes.Count == 0) throw ApiException.BadRequest("Chưa có thay đổi nào để lưu.");
        if (changes.Count > FeatureCatalog.Features.Count * Types.Length)
            throw ApiException.BadRequest("Danh sách thay đổi không hợp lệ.");

        foreach (var c in changes)
        {
            var f = FeatureCatalog.Find(c.FeatureKey ?? "") ?? throw ApiException.BadRequest("Chức năng không tồn tại. Tải lại trang rồi thử lại.");
            if (!Types.Contains(c.AccountType))
                throw ApiException.BadRequest("Loại tài khoản không hợp lệ.");
            if (!f.Ceiling.Contains(c.AccountType))
                throw ApiException.BadRequest($"“{f.Label}” không thuộc nghiệp vụ của {TypeLabel(c.AccountType)}, không bật/tắt được.");
            if (f.Locked)
                throw ApiException.BadRequest($"“{f.Label}” luôn bật, không tắt được.");
        }
    }

    /// <summary>
    /// Việc đang dở của các chức năng sắp tắt, để Admin cân nhắc trước khi lưu. Chỉ đếm, không đổi dữ liệu.
    /// Chức năng không có việc dở đáng kể thì không trả dòng nào.
    /// </summary>
    public async Task<List<FeatureImpact>> ImpactAsync(UpdateFeaturePermissionsRequest request)
    {
        var changes = request.Changes ?? [];
        Validate(changes);
        var today = clock.Today;
        var now = clock.Now;
        var result = new List<FeatureImpact>();

        foreach (var c in changes.Where(c => !c.Enabled).DistinctBy(c => (c.AccountType, c.FeatureKey)))
        {
            var label = FeatureCatalog.Find(c.FeatureKey)!.Label;
            string? text = (c.AccountType, c.FeatureKey) switch
            {
                (AccountTypes.Teacher, "att") => await UnmarkedTodayAsync(today) is var n and > 0
                    ? $"Hôm nay còn {n} tiết chưa điểm danh. Giáo viên chỉ điểm danh được trong ngày; nếu tắt qua hết hôm nay thì chỉ School Admin nhập bù được."
                    : null,
                (AccountTypes.Teacher, "par-leave") => await PendingLeavesAsync() is var n and > 0
                    ? $"Còn {n} đơn xin nghỉ đang chờ chủ nhiệm duyệt. Phụ huynh vẫn gửi đơn được nhưng sẽ không ai duyệt cho tới khi bật lại."
                    : null,
                (AccountTypes.Parent, "par-leave") => await PendingLeavesAsync() is var n and > 0
                    ? $"Còn {n} đơn xin nghỉ đang chờ duyệt. Phụ huynh sẽ không xem, gửi thêm hay rút đơn được khi chức năng đang tắt."
                    : null,
                (AccountTypes.Student, "lrn-todo") => await OpenUnsubmittedAsync(now) is var (a, s) && a > 0
                    ? $"{a} bài tập đang mở với {s} lượt học sinh chưa nộp. Bài hết hạn trong lúc tắt sẽ bị tính là không nộp."
                    : null,
                (AccountTypes.VicePrincipal, "scd-timetable") => await db.TimetableVersion.CountAsync(v => v.Status == "Draft") is var n and > 0
                    ? $"Còn {n} bản nháp thời khóa biểu chưa phát hành. Bản nháp giữ nguyên nhưng không sửa hay phát hành được khi đang tắt."
                    : null,
                (AccountTypes.VicePrincipal, "scd-absence") => await db.TeachingSession.CountAsync(s => s.Status == "Pending" && s.SessionDate >= today) is var n and > 0
                    ? $"Còn {n} tiết đang Chờ xử lý (chưa có giáo viên) từ hôm nay trở đi cần chọn người dạy thay hoặc hủy tiết."
                    : null,
                _ => null,
            };
            if (text is not null) result.Add(new FeatureImpact(c.AccountType, c.FeatureKey, label, text));
        }
        return result;
    }

    /// <summary>Tiết hôm nay đã xếp lịch, có giáo viên, chưa có bản ghi điểm danh nào.</summary>
    private Task<int> UnmarkedTodayAsync(DateOnly today) =>
        db.TeachingSession.CountAsync(s => s.SessionDate == today && s.Status == "Scheduled" && s.TeacherId != null
            && !db.AttendanceRecord.Any(r => r.SessionId == s.SessionId));

    private Task<int> PendingLeavesAsync() => db.LeaveRequest.CountAsync(l => l.Status == "Pending");

    /// <summary>Bài đang trong thời gian làm và số lượt học sinh được giao mà chưa nộp.</summary>
    private async Task<(int Assignments, int Students)> OpenUnsubmittedAsync(DateTime now)
    {
        var open = db.Assignment.Where(a => a.OpenAt <= now && a.DueAt > now);
        var assignments = await open.CountAsync();
        if (assignments == 0) return (0, 0);
        var students = await db.AssignmentRecipient.CountAsync(r => open.Any(a => a.AssignmentId == r.AssignmentId)
            && !db.Submission.Any(s => s.AssignmentId == r.AssignmentId && s.StudentId == r.StudentId && s.SubmittedAt != null));
        return (assignments, students);
    }

    public async Task UpdateAsync(UpdateFeaturePermissionsRequest request)
    {
        var changes = request.Changes ?? [];
        Validate(changes);

        var now = clock.Now;
        var existing = await db.FeaturePermission.ToListAsync();
        var turnedOn = new Dictionary<string, List<string>>();
        var turnedOff = new Dictionary<string, List<string>>();
        foreach (var c in changes.DistinctBy(c => (c.AccountType, c.FeatureKey)))
        {
            var row = existing.FirstOrDefault(r => r.AccountType == c.AccountType && r.FeatureKey == c.FeatureKey);
            var current = row?.IsEnabled ?? true;
            if (current == c.Enabled) continue;
            if (row is null)
            {
                row = new FeaturePermission { AccountType = c.AccountType, FeatureKey = c.FeatureKey };
                db.FeaturePermission.Add(row);
            }
            row.IsEnabled = c.Enabled;
            row.UpdatedAt = now;
            row.UpdatedBy = user.AccountId;
            var target = c.Enabled ? turnedOn : turnedOff;
            if (!target.TryGetValue(c.AccountType, out var list)) target[c.AccountType] = list = [];
            list.Add(FeatureCatalog.Find(c.FeatureKey)!.Label);
        }
        if (turnedOn.Count == 0 && turnedOff.Count == 0) return;

        // Mỗi loại tài khoản một bản ghi nhật ký, nội dung là tên chức năng đã bật và đã tắt.
        foreach (var t in Types.Where(t => turnedOn.ContainsKey(t) || turnedOff.ContainsKey(t)))
            audit.Add(AuditCategories.Permission, "UpdateFeaturePermissions", null, new
            {
                accountType = TypeLabel(t),
                turnedOn = turnedOn.GetValueOrDefault(t) ?? [],
                turnedOff = turnedOff.GetValueOrDefault(t) ?? [],
            });

        await db.SaveChangesAsync();
        cache.Remove(CacheKey);
    }
}

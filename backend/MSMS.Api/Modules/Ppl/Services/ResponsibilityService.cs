using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;

namespace MSMS.Api.Modules.Ppl.Services;

public static class ResponsibilityKinds
{
    public const string Campus = "campus";          // Hiệu phó phụ trách phân hiệu
    public const string Department = "department";  // Tổ trưởng
    public const string Homeroom = "homeroom";      // Chủ nhiệm lớp
}

/// <summary>
/// PPL-04: trách nhiệm có khoảng hiệu lực (BR-09). Mỗi đích tối đa một bản đang hiệu lực;
/// thay người là một transaction: bản cũ kết thúc vào ngày liền trước ngày hiệu lực của bản mới.
/// </summary>
public sealed class ResponsibilityService(MsmsDbContext db, CurrentUser user, IClock clock, ScopeResolver scope, AuditWriter audit)
{
    public async Task<List<ResponsibilityRow>> ListCurrentAsync(string kind, int? yearId)
    {
        var today = clock.Today;
        switch (kind)
        {
            case ResponsibilityKinds.Campus:
            {
                var holders = await db.CampusLeadership.AsNoTracking().Where(EffectivePeriod.CurrentResponsibility<CampusLeadership>(today))
                    .Select(x => new { x.Id, x.CampusId, x.StaffId, Name = x.Staff.Staff.FullName, Code = x.Staff.Staff.PersonCode, x.StartDate, x.EndDate, x.CreatedAt }).ToListAsync();
                var upcoming = await db.CampusLeadership.AsNoTracking().Where(x => x.StartDate > today).OrderBy(x => x.StartDate)
                    .Select(x => new { x.Id, x.CampusId, x.StaffId, Name = x.Staff.Staff.FullName, x.StartDate }).ToListAsync();
                var ended = (await db.CampusLeadership.AsNoTracking().Where(x => x.EndDate != null && x.EndDate <= today)
                    .Select(x => new { x.CampusId, Name = x.Staff.Staff.FullName, x.EndDate }).ToListAsync())
                    .GroupBy(x => x.CampusId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EndDate).First());
                var campusesQuery = db.Campus.AsNoTracking().Where(c => c.IsActive);
                if (user.AccountType == AccountTypes.VicePrincipal)
                {
                    var led = await scope.LedCampusIdsAsync();
                    campusesQuery = campusesQuery.Where(c => led.Contains(c.CampusId));
                }
                var campuses = await campusesQuery.OrderBy(c => c.Name).ToListAsync();
                return campuses.Select(c =>
                {
                    var h = holders.FirstOrDefault(x => x.CampusId == c.CampusId);
                    var u = upcoming.FirstOrDefault(x => x.CampusId == c.CampusId);
                    var e = ended.GetValueOrDefault(c.CampusId);
                    return new ResponsibilityRow(kind, c.CampusId, c.Name, null, h?.Id, h?.StaffId, h?.Name, h?.StartDate, u?.Name, u?.StartDate,
                        h?.Code, h?.EndDate, e?.Name, e?.EndDate, c.Code, u?.StaffId, h?.CreatedAt, u?.Id);
                }).ToList();
            }
            case ResponsibilityKinds.Department:
            {
                var holders = await db.DepartmentHead.AsNoTracking().Where(EffectivePeriod.CurrentResponsibility<DepartmentHead>(today))
                    .Select(x => new { x.Id, x.DepartmentId, x.StaffId, Name = x.Staff.Staff.FullName, Code = x.Staff.Staff.PersonCode, x.StartDate, x.EndDate, x.CreatedAt }).ToListAsync();
                var upcoming = await db.DepartmentHead.AsNoTracking().Where(x => x.StartDate > today).OrderBy(x => x.StartDate)
                    .Select(x => new { x.Id, x.DepartmentId, x.StaffId, Name = x.Staff.Staff.FullName, x.StartDate }).ToListAsync();
                var ended = (await db.DepartmentHead.AsNoTracking().Where(x => x.EndDate != null && x.EndDate <= today)
                    .Select(x => new { x.DepartmentId, Name = x.Staff.Staff.FullName, x.EndDate }).ToListAsync())
                    .GroupBy(x => x.DepartmentId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EndDate).First());
                var departmentsQuery = db.SubjectDepartment.AsNoTracking().Where(d => d.IsActive);
                if (user.AccountType == AccountTypes.VicePrincipal)
                {
                    var staff = await scope.FilterStaffAsync(db.StaffProfile.AsNoTracking());
                    var staffIds = await staff.Select(s => s.StaffId).ToListAsync();
                    var departmentIds = holders.Where(h => staffIds.Contains(h.StaffId)).Select(h => h.DepartmentId).ToList();
                    departmentsQuery = departmentsQuery.Where(d => departmentIds.Contains(d.DepartmentId));
                }
                var departments = await departmentsQuery.OrderBy(d => d.Name).ToListAsync();
                return departments.Select(d =>
                {
                    var h = holders.FirstOrDefault(x => x.DepartmentId == d.DepartmentId);
                    var u = upcoming.FirstOrDefault(x => x.DepartmentId == d.DepartmentId);
                    var e = ended.GetValueOrDefault(d.DepartmentId);
                    return new ResponsibilityRow(kind, d.DepartmentId, d.Name, null, h?.Id, h?.StaffId, h?.Name, h?.StartDate, u?.Name, u?.StartDate,
                        h?.Code, h?.EndDate, e?.Name, e?.EndDate, NextStaffId: u?.StaffId, StartedAt: h?.CreatedAt, NextRecordId: u?.Id);
                }).ToList();
            }
            case ResponsibilityKinds.Homeroom:
            {
                var year = yearId ?? await db.AcademicYear.Where(y => y.IsCurrent).Select(y => (int?)y.AcademicYearId).FirstOrDefaultAsync();
                var holders = await db.HomeroomAssignment.AsNoTracking().Where(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today))
                    .Select(x => new { x.Id, x.ClassId, x.StaffId, Name = x.Staff.Staff.FullName, Code = x.Staff.Staff.PersonCode, x.StartDate, x.EndDate, x.CreatedAt }).ToListAsync();
                var upcoming = await db.HomeroomAssignment.AsNoTracking().Where(x => x.StartDate > today).OrderBy(x => x.StartDate)
                    .Select(x => new { x.Id, x.ClassId, x.StaffId, Name = x.Staff.Staff.FullName, x.StartDate }).ToListAsync();
                var ended = (await db.HomeroomAssignment.AsNoTracking().Where(x => x.EndDate != null && x.EndDate <= today)
                    .Select(x => new { x.ClassId, Name = x.Staff.Staff.FullName, x.EndDate }).ToListAsync())
                    .GroupBy(x => x.ClassId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EndDate).First());
                var visibleClasses = await scope.FilterClassesAsync(db.Class.AsNoTracking());
                var classes = await visibleClasses.Where(c => c.IsActive && c.AcademicYearId == year)
                    .OrderBy(c => c.Campus.Name).ThenBy(c => c.GradeLevel).ThenBy(c => c.Name)
                    .Select(c => new { c.ClassId, c.Name, Campus = c.Campus.Name }).ToListAsync();
                return classes.Select(c =>
                {
                    var h = holders.FirstOrDefault(x => x.ClassId == c.ClassId);
                    var u = upcoming.FirstOrDefault(x => x.ClassId == c.ClassId);
                    var e = ended.GetValueOrDefault(c.ClassId);
                    return new ResponsibilityRow(kind, c.ClassId, c.Name, c.Campus, h?.Id, h?.StaffId, h?.Name, h?.StartDate, u?.Name, u?.StartDate,
                        h?.Code, h?.EndDate, e?.Name, e?.EndDate, NextStaffId: u?.StaffId, StartedAt: h?.CreatedAt, NextRecordId: u?.Id);
                }).ToList();
            }
            default:
                throw ApiException.BadRequest("Loại trách nhiệm không hợp lệ.");
        }
    }

    public async Task<List<ResponsibilityHistoryRow>> HistoryAsync(string kind, int targetId)
    {
        if (user.AccountType == AccountTypes.VicePrincipal)
        {
            var campuses = await scope.LedCampusIdsAsync();
            var allowed = kind switch
            {
                ResponsibilityKinds.Campus => campuses.Contains(targetId),
                ResponsibilityKinds.Homeroom => await db.Class.AnyAsync(c => c.ClassId == targetId && campuses.Contains(c.CampusId)),
                ResponsibilityKinds.Department => (await ListCurrentAsync(kind, null)).Any(r => r.TargetId == targetId),
                _ => false,
            };
            if (!allowed) throw ApiException.Forbidden("Không có quyền xem lịch sử trách nhiệm ngoài phân hiệu phụ trách.");
        }
        var today = clock.Today;
        var rows = kind switch
        {
            ResponsibilityKinds.Campus => await db.CampusLeadership.AsNoTracking().Where(x => x.CampusId == targetId)
                .Select(x => new { x.Id, x.StaffId, Name = x.Staff.Staff.FullName, x.StartDate, x.EndDate, x.CreatedAt, x.EndedAt }).ToListAsync(),
            ResponsibilityKinds.Department => await db.DepartmentHead.AsNoTracking().Where(x => x.DepartmentId == targetId)
                .Select(x => new { x.Id, x.StaffId, Name = x.Staff.Staff.FullName, x.StartDate, x.EndDate, x.CreatedAt, x.EndedAt }).ToListAsync(),
            ResponsibilityKinds.Homeroom => await db.HomeroomAssignment.AsNoTracking().Where(x => x.ClassId == targetId)
                .Select(x => new { x.Id, x.StaffId, Name = x.Staff.Staff.FullName, x.StartDate, x.EndDate, x.CreatedAt, x.EndedAt }).ToListAsync(),
            _ => throw ApiException.BadRequest("Loại trách nhiệm không hợp lệ."),
        };
        return rows.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new ResponsibilityHistoryRow(x.Id, x.StaffId, x.Name, x.StartDate, x.EndDate,
                x.StartDate <= today && (x.EndDate == null || x.EndDate > today), x.CreatedAt, x.EndedAt))
            .ToList();
    }

    /// <summary>Gán người mới; nếu đích đang có người thì thay người trong cùng transaction.</summary>
    public async Task AssignAsync(ResponsibilityAssignRequest r)
    {
        // Không viết lại lịch sử: người đã duyệt đơn hoặc giao bài trong khoảng đó vẫn là người giữ trách nhiệm lúc ấy.
        if (r.StartDate < clock.Today)
            throw ApiException.BadRequest("Ngày hiệu lực không được trước hôm nay. Chọn hôm nay hoặc một ngày sau đó.");
        await EnsureEligibleAsync(r.Kind, r.StaffId);
        await using var tx = await db.Database.BeginTransactionAsync();
        int? replacedStaffId = null;
        DateOnly? endDate = null;
        switch (r.Kind)
        {
            case ResponsibilityKinds.Campus:
                if (!await db.Campus.AnyAsync(c => c.CampusId == r.TargetId && c.IsActive)) throw ApiException.BadRequest("Phân hiệu không tồn tại hoặc đã ngừng hoạt động.");
                await EnsureSingleCampusAsync(r);
                replacedStaffId = await ReplaceAsync(db.CampusLeadership, x => x.CampusId == r.TargetId, r, () => new CampusLeadership { CampusId = r.TargetId });
                break;
            case ResponsibilityKinds.Department:
                if (!await db.SubjectDepartment.AnyAsync(d => d.DepartmentId == r.TargetId && d.IsActive)) throw ApiException.BadRequest("Tổ chuyên môn không tồn tại hoặc đã ngừng hoạt động.");
                await EnsureSingleDepartmentAsync(r);
                replacedStaffId = await ReplaceAsync(db.DepartmentHead, x => x.DepartmentId == r.TargetId, r, () => new DepartmentHead { DepartmentId = r.TargetId });
                break;
            case ResponsibilityKinds.Homeroom:
            {
                var yearEnd = await db.Class.Where(c => c.ClassId == r.TargetId && c.IsActive)
                    .Select(c => (DateOnly?)c.AcademicYear.EndDate).FirstOrDefaultAsync()
                    ?? throw ApiException.BadRequest("Lớp không tồn tại hoặc đã ngừng hoạt động.");
                if (r.StartDate > yearEnd)
                    throw ApiException.BadRequest($"Năm học của lớp đã kết thúc ngày {yearEnd:dd/MM/yyyy}. Chọn ngày hiệu lực trong năm học của lớp.");
                await EnsureSingleHomeroomAsync(r);
                // Chủ nhiệm gắn với lớp của một năm học: tự hết hiệu lực vào ngày cuối năm học đó (BR-09).
                endDate = yearEnd;
                replacedStaffId = await ReplaceAsync(db.HomeroomAssignment, x => x.ClassId == r.TargetId, r, () => new HomeroomAssignment { ClassId = r.TargetId }, yearEnd);
                break;
            }
            default:
                throw ApiException.BadRequest("Loại trách nhiệm không hợp lệ.");
        }
        audit.Add(AuditCategories.Responsibility,
            replacedStaffId is null ? AuditActions.AssignResponsibility : AuditActions.ReplaceResponsibility, null,
            ("Loại trách nhiệm", KindLabel(r.Kind)), ("Đối tượng", await TargetNameAsync(r.Kind, r.TargetId)),
            ("Người đảm nhiệm", await StaffNameAsync(r.StaffId)),
            ("Người đảm nhiệm cũ", replacedStaffId is int old ? await StaffNameAsync(old) : null),
            ("Từ ngày", r.StartDate), ("Đến ngày", endDate));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Kết thúc trách nhiệm: quyền tương ứng mất ngay, loại tài khoản không đổi (BR-09).</summary>
    public async Task EndAsync(ResponsibilityEndRequest r)
    {
        IResponsibilityRecord record = r.Kind switch
        {
            ResponsibilityKinds.Campus => await db.CampusLeadership.FindAsync(r.RecordId) ?? throw NotFound(),
            ResponsibilityKinds.Department => await db.DepartmentHead.FindAsync(r.RecordId) ?? throw NotFound(),
            ResponsibilityKinds.Homeroom => await db.HomeroomAssignment.FindAsync(r.RecordId) ?? throw NotFound(),
            _ => throw ApiException.BadRequest("Loại trách nhiệm không hợp lệ."),
        };
        if (record.EndDate is not null && record.EndDate < clock.Today)
            throw ApiException.BadRequest("Trách nhiệm này đã kết thúc từ trước.");
        if (r.EndDate < clock.Today)
            throw ApiException.BadRequest("Ngày kết thúc không được trước hôm nay. Chọn hôm nay hoặc một ngày sau đó.");
        if (r.EndDate < record.StartDate)
            throw ApiException.BadRequest($"Ngày kết thúc không được trước ngày bắt đầu ({record.StartDate:dd/MM/yyyy}).");
        if (r.Kind == ResponsibilityKinds.Campus && r.EndDate <= clock.Today)
            await EnsureCampusKeepsLeaderAsync((CampusLeadership)record);
        record.EndDate = r.EndDate;
        if (r.EndDate <= clock.Today) record.EndedAt = clock.Now;   // mất quyền ngay: ghi lại giờ để đọc lịch sử
        audit.Add(AuditCategories.Responsibility, AuditActions.EndResponsibility, null,
            ("Loại trách nhiệm", KindLabel(r.Kind)), ("Đối tượng", await TargetNameAsync(r.Kind, TargetIdOf(record))),
            ("Người đảm nhiệm", await StaffNameAsync(record.StaffId)), ("Từ ngày", record.StartDate), ("Kết thúc ngày", r.EndDate));
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Chỉ Hiệu phó đang phụ trách mới thao tác được thời khóa biểu, lịch dạy và khai báo giáo viên vắng của phân hiệu, Hiệu trưởng chỉ đọc.
    /// Kết thúc có hiệu lực hôm nay mà phân hiệu còn lớp hoạt động trong năm học hiện hành và chưa có người kế nhiệm thì phân hiệu bị bỏ trống: chặn,
    /// bắt dùng Thay người. Kết thúc vào ngày sau (bàn giao có hẹn) vẫn được, vì khi đó người dùng còn gán người mới từ ngày hẹn.
    /// Khóa hoặc đổi loại tài khoản (BR-05) cũng đóng trách nhiệm nhưng là hệ quả có cảnh báo riêng, không đi qua đây.
    /// </summary>
    private async Task EnsureCampusKeepsLeaderAsync(CampusLeadership record)
    {
        var today = clock.Today;
        var hasSuccessor = await db.CampusLeadership.AnyAsync(x =>
            x.CampusId == record.CampusId && x.Id != record.Id && (x.EndDate == null || x.EndDate >= today));
        if (hasSuccessor) return;
        var activeClasses = await db.Class.CountAsync(c => c.CampusId == record.CampusId && c.IsActive && c.AcademicYear.IsCurrent);
        if (activeClasses == 0) return;
        var campus = await db.Campus.Where(c => c.CampusId == record.CampusId).Select(c => c.Name).FirstAsync();
        throw ApiException.BadRequest(
            $"{campus} còn {activeClasses} lớp hoạt động. Dùng Thay người để giao cho Hiệu phó khác, không kết thúc khi chưa có người nhận.");
    }

    /// <summary>
    /// Hủy lịch hẹn: xóa hẳn bản trách nhiệm chưa tới ngày bắt đầu. Bản này chưa từng có hiệu lực nên không mất lịch sử (BR-09).
    /// Bản đã bắt đầu thì dùng Kết thúc.
    /// </summary>
    public async Task CancelAsync(ResponsibilityCancelRequest r)
    {
        IResponsibilityRecord record = r.Kind switch
        {
            ResponsibilityKinds.Campus => await db.CampusLeadership.FindAsync(r.RecordId) ?? throw NotFound(),
            ResponsibilityKinds.Department => await db.DepartmentHead.FindAsync(r.RecordId) ?? throw NotFound(),
            ResponsibilityKinds.Homeroom => await db.HomeroomAssignment.FindAsync(r.RecordId) ?? throw NotFound(),
            _ => throw ApiException.BadRequest("Loại trách nhiệm không hợp lệ."),
        };
        if (record.StartDate <= clock.Today)
            throw ApiException.BadRequest("Trách nhiệm này đã bắt đầu nên không hủy được. Dùng Kết thúc trách nhiệm.");
        // Bản hẹn bị xóa hẳn nên nhật ký là dấu vết duy nhất: ghi lại trước khi xóa.
        audit.Add(AuditCategories.Responsibility, AuditActions.CancelResponsibility, null,
            ("Loại trách nhiệm", KindLabel(r.Kind)), ("Đối tượng", await TargetNameAsync(r.Kind, TargetIdOf(record))),
            ("Người được hẹn", await StaffNameAsync(record.StaffId)), ("Hẹn từ ngày", record.StartDate));
        db.Remove(record);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Các lịch hẹn chưa bắt đầu của một cán bộ, mô tả từng lịch. <paramref name="cancel"/> = true thì đánh dấu xóa luôn (khi khóa, đổi loại
    /// tài khoản hoặc cho nghỉ); chỉ thêm vào context, người gọi SaveChanges. false để xem trước.
    /// </summary>
    internal static async Task<List<string>> UpcomingDutiesAsync(MsmsDbContext db, int staffId, DateOnly today, bool cancel)
    {
        var cancelled = new List<string>();
        foreach (var x in await db.CampusLeadership.Include(x => x.Campus).Where(x => x.StaffId == staffId && x.StartDate > today).ToListAsync())
        {
            cancelled.Add($"Phụ trách {x.Campus.Name} (hẹn từ {x.StartDate:dd/MM/yyyy})");
            if (cancel) db.CampusLeadership.Remove(x);
        }
        foreach (var x in await db.DepartmentHead.Include(x => x.Department).Where(x => x.StaffId == staffId && x.StartDate > today).ToListAsync())
        {
            cancelled.Add($"Tổ trưởng {x.Department.Name} (hẹn từ {x.StartDate:dd/MM/yyyy})");
            if (cancel) db.DepartmentHead.Remove(x);
        }
        foreach (var x in await db.HomeroomAssignment.Include(x => x.Class).Where(x => x.StaffId == staffId && x.StartDate > today).ToListAsync())
        {
            cancelled.Add($"Chủ nhiệm {x.Class.Name} (hẹn từ {x.StartDate:dd/MM/yyyy})");
            if (cancel) db.HomeroomAssignment.Remove(x);
        }
        return cancelled;
    }

    private async Task<int?> ReplaceAsync<T>(DbSet<T> set, System.Linq.Expressions.Expression<Func<T, bool>> forTarget,
        ResponsibilityAssignRequest r, Func<T> create, DateOnly? endDate = null) where T : class, IResponsibilityRecord
    {
        var records = await set.Where(forTarget).ToListAsync();
        if (records.Any(x => x.StartDate > r.StartDate))
            throw ApiException.BadRequest("Đích này đã có người nhận trách nhiệm bắt đầu sau ngày đã chọn. Chọn ngày muộn hơn.");

        // Người đương nhiệm tại ngày này: bản kết thúc đúng ngày này (đã kết thúc hoặc vừa được thay) không còn đương nhiệm (BR-09).
        var current = records.Where(x => x.StartDate <= r.StartDate && (x.EndDate == null || x.EndDate > r.StartDate))
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefault();
        var replacedStaffId = current?.StaffId;
        if (current is not null)
        {
            if (current.StaffId == r.StaffId) throw ApiException.BadRequest("Người này đang đảm nhiệm sẵn. Chọn người khác để thay.");
            // Thay người có hiệu lực ngay: hẹn ngày sau sẽ để người cũ mất quyền hôm nay mà người mới chưa nhận (khoảng trống).
            if (r.StartDate > clock.Today)
                throw ApiException.BadRequest("Thay người có hiệu lực ngay hôm nay. Chọn hôm nay, hoặc kết thúc người hiện tại vào ngày muốn rồi gán người mới sau.");
            // Thay người = kết thúc người cũ ngay bây giờ: ngày kết thúc là hôm nay (như nút Kết thúc), kèm giờ. Quyền đã mất vì kết thúc hôm nay là hết quyền (BR-09).
            current.EndDate = r.StartDate;
            current.EndedAt = clock.Now;
        }

        var record = create();
        record.StaffId = r.StaffId;
        record.StartDate = r.StartDate;
        record.EndDate = endDate;
        record.AssignedByPersonId = user.PersonId;
        record.CreatedAt = clock.Now;
        set.Add(record);
        return replacedStaffId;
    }

    private static string KindLabel(string kind) => kind switch
    {
        ResponsibilityKinds.Campus => "Phụ trách phân hiệu",
        ResponsibilityKinds.Department => "Tổ trưởng",
        _ => "Chủ nhiệm",
    };

    private static int TargetIdOf(IResponsibilityRecord record) => record switch
    {
        CampusLeadership c => c.CampusId,
        DepartmentHead d => d.DepartmentId,
        HomeroomAssignment h => h.ClassId,
        _ => 0,
    };

    /// <summary>Tên đích (phân hiệu, tổ hoặc lớp) để ghi nhật ký bằng chữ, vì bản hẹn bị hủy sẽ mất khỏi bảng.</summary>
    private async Task<string> TargetNameAsync(string kind, int targetId) => (kind switch
    {
        ResponsibilityKinds.Campus => await db.Campus.Where(c => c.CampusId == targetId).Select(c => c.Name).FirstOrDefaultAsync(),
        ResponsibilityKinds.Department => await db.SubjectDepartment.Where(d => d.DepartmentId == targetId).Select(d => d.Name).FirstOrDefaultAsync(),
        _ => await db.Class.Where(c => c.ClassId == targetId).Select(c => c.Name).FirstOrDefaultAsync(),
    }) ?? $"#{targetId}";

    private async Task<string> StaffNameAsync(int staffId) =>
        await db.Person.Where(p => p.PersonId == staffId).Select(p => p.FullName).FirstOrDefaultAsync() ?? $"#{staffId}";

    /// <summary>
    /// Mỗi Hiệu phó chỉ phụ trách một phân hiệu tại một thời điểm: chặn nếu người này đang giữ, hoặc đã được hẹn giữ, phân hiệu khác
    /// trong khoảng bản mới bắt đầu. Bản đã kết thúc từ ngày bắt đầu của bản mới trở về trước không tính (BR-09).
    /// </summary>
    private async Task EnsureSingleCampusAsync(ResponsibilityAssignRequest r)
    {
        var other = await db.CampusLeadership.AsNoTracking()
            .Where(x => x.StaffId == r.StaffId && x.CampusId != r.TargetId && (x.EndDate == null || x.EndDate > r.StartDate))
            .OrderBy(x => x.StartDate)
            .Select(x => new { Campus = x.Campus.Name, Staff = x.Staff.Staff.FullName, x.StartDate })
            .FirstOrDefaultAsync();
        if (other is not null)
            throw ApiException.BadRequest(
                $"{other.Staff} đang phụ trách {other.Campus} (từ {other.StartDate:dd/MM/yyyy}). Mỗi Hiệu phó chỉ phụ trách một phân hiệu: " +
                "kết thúc trách nhiệm đó trước khi gán sang phân hiệu khác.");
    }

    /// <summary>
    /// Mỗi giáo viên chỉ chủ nhiệm một lớp trong một năm học: chặn nếu người này đang giữ, hoặc đã được hẹn giữ, lớp khác của cùng năm học
    /// với lớp được gán. Lớp của năm học khác không tính, nên vẫn xếp trước chủ nhiệm cho năm sau (BR-09).
    /// </summary>
    private async Task EnsureSingleHomeroomAsync(ResponsibilityAssignRequest r)
    {
        var yearId = await db.Class.Where(c => c.ClassId == r.TargetId).Select(c => c.AcademicYearId).FirstAsync();
        var other = await db.HomeroomAssignment.AsNoTracking()
            .Where(x => x.StaffId == r.StaffId && x.ClassId != r.TargetId && x.Class.AcademicYearId == yearId
                        && (x.EndDate == null || x.EndDate > r.StartDate))
            .OrderBy(x => x.StartDate)
            .Select(x => new { Class = x.Class.Name, Staff = x.Staff.Staff.FullName, x.StartDate })
            .FirstOrDefaultAsync();
        if (other is not null)
            throw ApiException.BadRequest(
                $"{other.Staff} đang chủ nhiệm lớp {other.Class} (từ {other.StartDate:dd/MM/yyyy}). Mỗi giáo viên chỉ chủ nhiệm một lớp trong một năm học: " +
                "kết thúc trách nhiệm đó trước khi gán sang lớp khác.");
    }

    /// <summary>
    /// Mỗi giáo viên chỉ làm tổ trưởng một tổ tại một thời điểm: chặn nếu người này đang giữ, hoặc đã được hẹn giữ, tổ khác
    /// từ ngày bản mới bắt đầu. Thay tổ trưởng của chính tổ đó không bị ảnh hưởng (BR-09).
    /// </summary>
    private async Task EnsureSingleDepartmentAsync(ResponsibilityAssignRequest r)
    {
        var other = await db.DepartmentHead.AsNoTracking()
            .Where(x => x.StaffId == r.StaffId && x.DepartmentId != r.TargetId && (x.EndDate == null || x.EndDate > r.StartDate))
            .OrderBy(x => x.StartDate)
            .Select(x => new { Department = x.Department.Name, Staff = x.Staff.Staff.FullName, x.StartDate })
            .FirstOrDefaultAsync();
        if (other is not null)
            throw ApiException.BadRequest(
                $"{other.Staff} đang làm tổ trưởng {other.Department} (từ {other.StartDate:dd/MM/yyyy}). Mỗi giáo viên chỉ làm tổ trưởng một tổ: " +
                "kết thúc trách nhiệm đó trước khi gán sang tổ khác.");
    }

    /// <summary>Phụ trách phân hiệu chỉ gán cho Vice Principal; tổ trưởng và chủ nhiệm chỉ gán cho Teacher (BR-09).</summary>
    private async Task EnsureEligibleAsync(string kind, int staffId)
    {
        var staff = await db.StaffProfile.Include(s => s.Staff).ThenInclude(p => p.Account)
            .FirstOrDefaultAsync(s => s.StaffId == staffId) ?? throw ApiException.BadRequest("Chọn cán bộ để gán.");
        if (!staff.IsActive) throw ApiException.BadRequest("Cán bộ đã ngừng hoạt động. Chọn người khác.");

        var required = kind == ResponsibilityKinds.Campus ? AccountTypes.VicePrincipal : AccountTypes.Teacher;
        var requiredLabel = kind == ResponsibilityKinds.Campus ? "Hiệu phó" : "Giáo viên";
        var account = staff.Staff.Account;
        if (account is null || account.AccountType != required)
            throw ApiException.BadRequest($"{staff.Staff.FullName} chưa có tài khoản loại {requiredLabel}. Tạo tài khoản đúng loại ở mục Tài khoản rồi gán lại.");
        if (account.Status == AccountStatuses.Locked)
            throw ApiException.BadRequest($"Tài khoản của {staff.Staff.FullName} đang bị khóa. Mở khóa trước khi gán.");
    }

    private static ApiException NotFound() => ApiException.NotFound("Không tìm thấy bản ghi trách nhiệm.");
}

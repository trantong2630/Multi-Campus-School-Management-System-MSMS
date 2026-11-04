using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Excel;
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
public sealed class ResponsibilityService(MsmsDbContext db, CurrentUser user, IClock clock, ScopeResolver scope)
{
    public async Task<List<ResponsibilityRow>> ListCurrentAsync(string kind, int? yearId)
    {
        var today = clock.Today;
        switch (kind)
        {
            case ResponsibilityKinds.Campus:
            {
                var holders = await db.CampusLeadership.AsNoTracking().Where(EffectivePeriod.CurrentResponsibility<CampusLeadership>(today))
                    .Select(x => new { x.Id, x.CampusId, x.StaffId, Name = x.Staff.Staff.FullName, Code = x.Staff.Staff.PersonCode, x.StartDate, x.EndDate }).ToListAsync();
                var upcoming = await db.CampusLeadership.AsNoTracking().Where(x => x.StartDate > today).OrderBy(x => x.StartDate)
                    .Select(x => new { x.CampusId, Name = x.Staff.Staff.FullName, x.StartDate }).ToListAsync();
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
                        h?.Code, h?.EndDate, e?.Name, e?.EndDate, c.Code);
                }).ToList();
            }
            case ResponsibilityKinds.Department:
            {
                var holders = await db.DepartmentHead.AsNoTracking().Where(EffectivePeriod.CurrentResponsibility<DepartmentHead>(today))
                    .Select(x => new { x.Id, x.DepartmentId, x.StaffId, Name = x.Staff.Staff.FullName, Code = x.Staff.Staff.PersonCode, x.StartDate, x.EndDate }).ToListAsync();
                var upcoming = await db.DepartmentHead.AsNoTracking().Where(x => x.StartDate > today).OrderBy(x => x.StartDate)
                    .Select(x => new { x.DepartmentId, Name = x.Staff.Staff.FullName, x.StartDate }).ToListAsync();
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
                        h?.Code, h?.EndDate, e?.Name, e?.EndDate);
                }).ToList();
            }
            case ResponsibilityKinds.Homeroom:
            {
                var year = yearId ?? await db.AcademicYear.Where(y => y.IsCurrent).Select(y => (int?)y.AcademicYearId).FirstOrDefaultAsync();
                var holders = await db.HomeroomAssignment.AsNoTracking().Where(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today))
                    .Select(x => new { x.Id, x.ClassId, x.StaffId, Name = x.Staff.Staff.FullName, Code = x.Staff.Staff.PersonCode, x.StartDate, x.EndDate }).ToListAsync();
                var upcoming = await db.HomeroomAssignment.AsNoTracking().Where(x => x.StartDate > today).OrderBy(x => x.StartDate)
                    .Select(x => new { x.ClassId, Name = x.Staff.Staff.FullName, x.StartDate }).ToListAsync();
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
                        h?.Code, h?.EndDate, e?.Name, e?.EndDate);
                }).ToList();
            }
            default:
                throw ApiException.BadRequest("Loại trách nhiệm không hợp lệ.");
        }
    }

    /// <summary>Xuất danh sách một loại trách nhiệm ra Excel, cùng phạm vi và trạng thái với màn hình.</summary>
    public async Task<FileContentResult> ExportAsync(string kind, int? yearId)
    {
        var rows = await ListCurrentAsync(kind, yearId);
        var (sheet, file, target) = kind switch
        {
            ResponsibilityKinds.Campus => ("Phụ trách phân hiệu", "MSMS_Trach_nhiem_phu_trach_phan_hieu.xlsx", "Phân hiệu"),
            ResponsibilityKinds.Department => ("Tổ trưởng", "MSMS_Trach_nhiem_to_truong.xlsx", "Tổ chuyên môn"),
            _ => ("Chủ nhiệm", "MSMS_Trach_nhiem_chu_nhiem.xlsx", "Lớp"),
        };
        var homeroom = kind == ResponsibilityKinds.Homeroom;
        string[] headers = [..(kind == ResponsibilityKinds.Campus ? new[] { "Mã phân hiệu" } : []), target, ..(homeroom ? new[] { "Phân hiệu" } : []),
            "Người phụ trách", "Mã hồ sơ", "Từ ngày", "Đến ngày", "Trạng thái"];

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheet);
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        ws.StyleHeaderRow(1, headers.Length, 22);
        var r = 2;
        foreach (var row in rows)
        {
            var status = row.StaffId is not null ? "Đã phân công" : row.NextStaffName is not null ? "Sắp hiệu lực"
                : row.EndedStaffName is not null ? "Đã kết thúc" : "Chưa phân công";
            var cells = new List<object?>();
            if (kind == ResponsibilityKinds.Campus) cells.Add(row.TargetCode);
            cells.Add(row.TargetName);
            if (homeroom) cells.Add(row.TargetGroup);
            cells.Add(row.StaffName ?? row.NextStaffName ?? row.EndedStaffName);
            cells.Add(row.StaffCode);
            cells.Add(row.StartDate is { } s ? s.ToString("dd/MM/yyyy") : row.NextStartDate is { } n ? n.ToString("dd/MM/yyyy") : null);
            cells.Add(row.EndDate is { } e ? e.ToString("dd/MM/yyyy") : row.EndedDate is { } ed ? ed.ToString("dd/MM/yyyy") : null);
            cells.Add(status);
            for (var c = 0; c < cells.Count; c++) ws.Cell(r, c + 1).Value = cells[c]?.ToString() ?? "";
            r++;
        }
        ws.Columns().AdjustToContents();
        return wb.ToFile(file);
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
                .Select(x => new { x.Id, x.StaffId, Name = x.Staff.Staff.FullName, x.StartDate, x.EndDate }).ToListAsync(),
            ResponsibilityKinds.Department => await db.DepartmentHead.AsNoTracking().Where(x => x.DepartmentId == targetId)
                .Select(x => new { x.Id, x.StaffId, Name = x.Staff.Staff.FullName, x.StartDate, x.EndDate }).ToListAsync(),
            ResponsibilityKinds.Homeroom => await db.HomeroomAssignment.AsNoTracking().Where(x => x.ClassId == targetId)
                .Select(x => new { x.Id, x.StaffId, Name = x.Staff.Staff.FullName, x.StartDate, x.EndDate }).ToListAsync(),
            _ => throw ApiException.BadRequest("Loại trách nhiệm không hợp lệ."),
        };
        return rows.OrderByDescending(x => x.StartDate)
            .Select(x => new ResponsibilityHistoryRow(x.Id, x.StaffId, x.Name, x.StartDate, x.EndDate,
                x.StartDate <= today && (x.EndDate == null || x.EndDate >= today)))
            .ToList();
    }

    /// <summary>Gán người mới; nếu đích đang có người thì thay người trong cùng transaction.</summary>
    public async Task AssignAsync(ResponsibilityAssignRequest r)
    {
        await EnsureEligibleAsync(r.Kind, r.StaffId);
        await using var tx = await db.Database.BeginTransactionAsync();
        switch (r.Kind)
        {
            case ResponsibilityKinds.Campus:
                if (!await db.Campus.AnyAsync(c => c.CampusId == r.TargetId && c.IsActive)) throw ApiException.BadRequest("Phân hiệu không tồn tại hoặc đã ngừng hoạt động.");
                await EnsureSingleCampusAsync(r);
                await ReplaceAsync(db.CampusLeadership, x => x.CampusId == r.TargetId, r, () => new CampusLeadership { CampusId = r.TargetId });
                break;
            case ResponsibilityKinds.Department:
                if (!await db.SubjectDepartment.AnyAsync(d => d.DepartmentId == r.TargetId && d.IsActive)) throw ApiException.BadRequest("Tổ chuyên môn không tồn tại hoặc đã ngừng hoạt động.");
                await ReplaceAsync(db.DepartmentHead, x => x.DepartmentId == r.TargetId, r, () => new DepartmentHead { DepartmentId = r.TargetId });
                break;
            case ResponsibilityKinds.Homeroom:
                if (!await db.Class.AnyAsync(c => c.ClassId == r.TargetId && c.IsActive)) throw ApiException.BadRequest("Lớp không tồn tại hoặc đã ngừng hoạt động.");
                await EnsureSingleHomeroomAsync(r);
                await ReplaceAsync(db.HomeroomAssignment, x => x.ClassId == r.TargetId, r, () => new HomeroomAssignment { ClassId = r.TargetId });
                break;
            default:
                throw ApiException.BadRequest("Loại trách nhiệm không hợp lệ.");
        }
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
        if (r.EndDate < record.StartDate)
            throw ApiException.BadRequest($"Ngày kết thúc không được trước ngày bắt đầu ({record.StartDate:dd/MM/yyyy}).");
        record.EndDate = r.EndDate;
        await db.SaveChangesAsync();
    }

    private async Task ReplaceAsync<T>(DbSet<T> set, System.Linq.Expressions.Expression<Func<T, bool>> forTarget,
        ResponsibilityAssignRequest r, Func<T> create) where T : class, IResponsibilityRecord
    {
        var records = await set.Where(forTarget).ToListAsync();
        if (records.Any(x => x.StartDate > r.StartDate))
            throw ApiException.BadRequest("Đích này đã có người nhận trách nhiệm bắt đầu sau ngày đã chọn. Chọn ngày muộn hơn.");

        var current = records.FirstOrDefault(x => x.IsEffectiveAt(r.StartDate));
        if (current is not null)
        {
            if (current.StaffId == r.StaffId) throw ApiException.BadRequest("Người này đang đảm nhiệm sẵn. Chọn người khác để thay.");
            EffectivePeriod.CloseBefore(current, r.StartDate);
        }

        var record = create();
        record.StaffId = r.StaffId;
        record.StartDate = r.StartDate;
        record.EndDate = null;
        record.AssignedByPersonId = user.PersonId;
        set.Add(record);
    }

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

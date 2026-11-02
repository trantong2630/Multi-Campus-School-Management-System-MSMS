using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Sch.Services;

/// <summary>
/// Thống kê cấu trúc hiển thị ngay trên màn Thiết lập (phân hiệu, tổ và môn, lớp). Dashboard chỉ giữ số liệu vận hành.
/// Phạm vi theo người xem (BR-01): School Admin và Principal toàn trường, Vice Principal chỉ phân hiệu đang phụ trách.
/// Tổ và môn là danh mục chung toàn trường nên ai xem được màn cũng thấy đủ.
/// </summary>
public sealed class StructureStatsService(MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock,
    SubjectResponsibilityService responsibility)
{
    public async Task<List<CampusStatDto>> CampusesAsync()
    {
        var today = clock.Today;
        var campuses = db.Campus.AsNoTracking();
        if (user.AccountType == AccountTypes.VicePrincipal)
        {
            var led = await scope.LedCampusIdsAsync();
            campuses = campuses.Where(c => led.Contains(c.CampusId));
        }

        var classes = await (await scope.FilterClassesAsync(db.Class.AsNoTracking()))
            .Where(c => c.IsActive && c.AcademicYear.IsCurrent)
            .Select(c => new { c.CampusId, Students = c.Enrollment.AsQueryable().Count(EnrollmentRules.ActiveAsOf(today)) })
            .ToListAsync();
        var staff = await (await scope.FilterStaffAsync(db.StaffProfile.AsNoTracking()))
            .Where(s => s.IsActive).GroupBy(s => s.PrimaryCampusId)
            .Select(g => new { CampusId = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.CampusId, g => g.Count);
        var leaders = (await db.CampusLeadership.AsNoTracking()
                .Where(EffectivePeriod.CurrentResponsibility<CampusLeadership>(today))
                .Select(l => new { l.CampusId, l.Staff.Staff.FullName }).ToListAsync())
            .GroupBy(l => l.CampusId).ToDictionary(g => g.Key, g => g.Select(x => x.FullName).Order().ToList());

        return (await campuses.Select(c => c.CampusId).ToListAsync()).Select(id =>
        {
            var own = classes.Where(c => c.CampusId == id).ToList();
            return new CampusStatDto(id, own.Count, own.Sum(c => c.Students), staff.GetValueOrDefault(id),
                leaders.GetValueOrDefault(id) ?? []);
        }).ToList();
    }

    public async Task<CatalogStatsDto> CatalogAsync()
    {
        var today = clock.Today;
        var departments = await db.SubjectDepartment.AsNoTracking()
            .Select(d => new
            {
                d.DepartmentId,
                Head = d.DepartmentHead.AsQueryable().Where(EffectivePeriod.CurrentResponsibility<DepartmentHead>(today))
                    .Select(h => h.Staff.Staff.FullName).FirstOrDefault(),
                Teachers = d.StaffProfile.Count(s => s.IsActive),
            }).ToListAsync();

        // Phụ trách chuyên môn suy từ thời khóa biểu đã phát hành (SCD-04), không đọc lại bảng thời khóa biểu ở đây.
        var teaching = await responsibility.ListAsync(null, null);
        var cells = teaching.Teachers.SelectMany(t => t.Items.Select(i => (t.TeacherId, i.SubjectId, i.ClassId))).ToList();
        var subjects = cells.GroupBy(c => c.SubjectId)
            .Select(g => new SubjectStatDto(g.Key, g.Select(c => c.TeacherId).Distinct().Count(), g.Select(c => c.ClassId).Distinct().Count()))
            .ToList();

        int? withoutDepartment = user.AccountType == AccountTypes.SchoolAdmin
            ? await db.StaffProfile.AsNoTracking().CountAsync(s => s.IsActive && s.DepartmentId == null
                && s.Staff.Account != null && s.Staff.Account.AccountType == AccountTypes.Teacher)
            : null;

        return new CatalogStatsDto(teaching.SemesterName,
            departments.Select(d => new DepartmentStatDto(d.DepartmentId, d.Head, d.Teachers)).ToList(), subjects, withoutDepartment);
    }

    public async Task<ClassStatsDto> ClassesAsync()
    {
        if (user.AccountType != AccountTypes.SchoolAdmin) return new ClassStatsDto(null);
        var today = clock.Today;
        var count = await db.StudentProfile.AsNoTracking()
            .CountAsync(s => s.Status == "Studying" && !s.Enrollment.AsQueryable().Any(EnrollmentRules.ActiveAsOf(today)));
        return new ClassStatsDto(count);
    }
}

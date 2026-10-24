using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>
/// ADM-01: Principal xem toàn trường hoặc lọc một phân hiệu; Vice Principal chỉ xem phân hiệu đang phụ trách.
/// Lớp, học sinh, tiết tính theo phân hiệu của lớp; cán bộ tính theo phân hiệu chính.
/// </summary>
public sealed class DashboardService(MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock)
{
    public async Task<DashboardDto> GetAsync(int? campusId)
    {
        var campusIds = await ResolveScopeAsync(campusId);
        var today = clock.Today;
        var year = await db.AcademicYear.AsNoTracking().Where(y => y.IsCurrent).Select(y => new { y.AcademicYearId, y.Name }).FirstOrDefaultAsync();
        var yearId = year?.AcademicYearId ?? 0;

        var campuses = await db.Campus.AsNoTracking().Where(c => campusIds.Contains(c.CampusId))
            .OrderBy(c => c.Name).Select(c => new { c.CampusId, c.Name }).ToListAsync();

        var classes = db.Class.AsNoTracking()
            .Where(c => c.IsActive && c.AcademicYearId == yearId && campusIds.Contains(c.CampusId));

        var classStats = await classes
            .Select(c => new
            {
                c.CampusId,
                c.GradeLevel,
                Students = c.Enrollment.AsQueryable().Count(EffectivePeriod.EffectiveAt<Enrollment>(today)),
            })
            .ToListAsync();

        var staff = await db.StaffProfile.AsNoTracking()
            .Where(s => s.IsActive && campusIds.Contains(s.PrimaryCampusId))
            .GroupBy(s => s.PrimaryCampusId).Select(g => new { CampusId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CampusId, x => x.Count);

        var sessions = await db.TeachingSession.AsNoTracking()
            .Where(s => campusIds.Contains(s.Class.CampusId) && s.Status == "Scheduled" && s.SessionDate == today)
            .GroupBy(s => s.Class.CampusId).Select(g => new { CampusId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CampusId, x => x.Count);

        var rows = campuses.Select(c =>
        {
            var cs = classStats.Where(x => x.CampusId == c.CampusId).ToList();
            return new DashboardCampusRow(c.CampusId, c.Name, cs.Count, cs.Sum(x => x.Students), staff.GetValueOrDefault(c.CampusId),
                sessions.GetValueOrDefault(c.CampusId));
        }).ToList();

        var byGrade = classStats.GroupBy(x => (int)x.GradeLevel).OrderBy(g => g.Key)
            .Select(g => new GradeCount(g.Key, g.Count(), g.Sum(x => x.Students))).ToList();

        return new DashboardDto(campusId, year?.Name,
            rows.Sum(r => r.ClassCount), rows.Sum(r => r.StudentCount), rows.Sum(r => r.StaffCount), rows.Sum(r => r.SessionsToday),
            byGrade, rows);
    }

    private async Task<List<int>> ResolveScopeAsync(int? campusId)
    {
        if (user.AccountType == AccountTypes.Principal)
        {
            var active = db.Campus.AsNoTracking().Where(c => c.IsActive);
            if (campusId is not null) active = active.Where(c => c.CampusId == campusId);
            var ids = await active.Select(c => c.CampusId).ToListAsync();
            if (campusId is not null && ids.Count == 0) throw ApiException.NotFound("Không tìm thấy phân hiệu đang hoạt động này.");
            return ids;
        }

        var led = (await scope.LedCampusIdsAsync()).ToList();
        if (campusId is null) return led;
        await scope.EnsureCanReadCampusAsync(campusId.Value);
        return [campusId.Value];
    }
}

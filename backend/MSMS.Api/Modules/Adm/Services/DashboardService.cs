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
/// Lớp, học sinh, tiết, bài tập tính theo phân hiệu của lớp; cán bộ tính theo phân hiệu chính.
/// Principal không đọc đơn nghỉ (mục 2.3) nên không nhận số đơn chờ duyệt.
/// </summary>
public sealed class DashboardService(MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock)
{
    public async Task<DashboardDto> GetAsync(int? campusId)
    {
        var campusIds = await ResolveScopeAsync(campusId);
        var today = clock.Today;
        var now = clock.Now;
        var term = await DashboardSupport.CurrentTermAsync(db, today);
        var weekStart = DashboardSupport.WeekStart(today);
        var trendFrom = weekStart.AddDays(-49);
        var weekEnd = weekStart.AddDays(6);

        var campuses = await db.Campus.AsNoTracking().Where(c => campusIds.Contains(c.CampusId))
            .OrderBy(c => c.Name).Select(c => new { c.CampusId, c.Name }).ToListAsync();

        var classes = db.Class.AsNoTracking()
            .Where(c => c.IsActive && c.AcademicYearId == term.YearId && campusIds.Contains(c.CampusId));

        var classStats = await classes
            .Select(c => new
            {
                c.CampusId,
                c.GradeLevel,
                Students = c.Enrollment.AsQueryable().Count(EffectivePeriod.EffectiveAt<Enrollment>(today)),
                HasHomeroom = c.HomeroomAssignment.AsQueryable().Any(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today)),
            })
            .ToListAsync();

        var staffByCampus = await db.StaffProfile.AsNoTracking()
            .Where(s => s.IsActive && campusIds.Contains(s.PrimaryCampusId))
            .GroupBy(s => s.PrimaryCampusId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);

        // Đếm theo (ngày, phân hiệu) ngay trong SQL: 8 tuần toàn trường có thể là hàng chục nghìn tiết.
        var sessions = await db.TeachingSession.AsNoTracking()
            .Where(s => campusIds.Contains(s.Class.CampusId) && s.Status == "Scheduled" && s.SessionDate >= trendFrom && s.SessionDate <= weekEnd)
            .GroupBy(s => new { s.SessionDate, s.Class.CampusId })
            .Select(g => new { g.Key.SessionDate, g.Key.CampusId, Count = g.Count() }).ToListAsync();
        var todayByCampus = sessions.Where(s => s.SessionDate == today).GroupBy(s => s.CampusId).ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        var facts = await DashboardSupport.FactsAsync(db.Assignment
            .Where(a => campusIds.Contains(a.Class.CampusId) && a.Class.AcademicYearId == term.YearId));

        var leaders = (await db.CampusLeadership.AsNoTracking()
                .Where(EffectivePeriod.CurrentResponsibility<CampusLeadership>(today))
                .Where(l => campusIds.Contains(l.CampusId))
                .Select(l => new { l.CampusId, l.Staff.Staff.FullName }).ToListAsync())
            .GroupBy(l => l.CampusId).ToDictionary(g => g.Key, g => g.Select(x => x.FullName).Order().ToList());

        var rows = campuses.Select(c =>
        {
            var cs = classStats.Where(x => x.CampusId == c.CampusId).ToList();
            return new DashboardCampusRow(c.CampusId, c.Name, cs.Count, cs.Sum(x => x.Students), staffByCampus.GetValueOrDefault(c.CampusId),
                todayByCampus.GetValueOrDefault(c.CampusId), leaders.GetValueOrDefault(c.CampusId) ?? [],
                DashboardSupport.Rate(facts.Where(f => f.CampusId == c.CampusId), now));
        }).ToList();

        var attention = await AttentionAsync(campusIds, today, classStats.Count(c => !c.HasHomeroom));
        var byGrade = DashboardSupport.ByGrade(classStats, x => x.GradeLevel, x => x.Students).ToList();

        return new DashboardDto(campusId, term.YearName, term.SemesterName,
            campuses.Count, byGrade.Count, rows.Sum(r => r.ClassCount), rows.Sum(r => r.StudentCount), rows.Sum(r => r.StaffCount),
            rows.Sum(r => r.SessionsToday),
            attention, byGrade, rows,
            DashboardSupport.BySubject(facts, now),
            DashboardSupport.Weeks(today, sessions.SelectMany(s => Enumerable.Repeat(s.SessionDate, s.Count)), facts, now));
    }

    private async Task<DashboardAttention> AttentionAsync(List<int> campusIds, DateOnly today, int classesWithoutHomeroom)
    {
        var pendingSessions = await db.TeachingSession.AsNoTracking()
            .CountAsync(s => s.Status == "Pending" && s.SessionDate >= today && campusIds.Contains(s.Class.CampusId));
        var absences = await db.TeacherAbsence.AsNoTracking()
            .Where(a => a.FromDate <= today && a.ToDate >= today && campusIds.Contains(a.Teacher.PrimaryCampusId))
            .Select(a => a.TeacherId).Distinct().CountAsync();
        int? pendingLeave = user.AccountType == AccountTypes.VicePrincipal
            ? await db.LeaveRequest.AsNoTracking().CountAsync(l => l.Status == "Pending" && campusIds.Contains(l.Class.CampusId))
            : null;
        return new DashboardAttention(pendingSessions, absences, pendingLeave, classesWithoutHomeroom);
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

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
/// ADM-01: Principal xem toàn trường hoặc lọc một phân hiệu; Vice Principal chỉ xem phân hiệu đang phụ trách. Lọc thêm được theo khối.
/// Lớp, học sinh, tiết, bài tập, điểm danh tính theo phân hiệu của lớp; cán bộ tính theo phân hiệu chính.
/// Principal không đọc đơn nghỉ (mục 2.3) nên không nhận số đơn chờ duyệt.
/// </summary>
public sealed class DashboardService(MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock)
{
    private const int AttendanceDays = 28;

    public async Task<DashboardDto> GetAsync(int? campusId, int? gradeLevel = null)
    {
        var campusIds = await ResolveScopeAsync(campusId);
        var today = clock.Today;
        var term = await DashboardSupport.CurrentTermAsync(db, today);
        var trendFrom = DashboardSupport.WeekStart(today).AddDays(-49);

        var campuses = await db.Campus.AsNoTracking().Where(c => campusIds.Contains(c.CampusId))
            .OrderBy(c => c.Name).Select(c => new { c.CampusId, c.Name }).ToListAsync();

        var inScope = db.Class.AsNoTracking()
            .Where(c => c.IsActive && c.AcademicYearId == term.YearId && campusIds.Contains(c.CampusId));
        var gradeOptions = await inScope.Select(c => (int)c.GradeLevel).Distinct().OrderBy(g => g).ToListAsync();
        var classes = gradeLevel is null ? inScope : inScope.Where(c => c.GradeLevel == gradeLevel);

        var classStats = await classes
            .Select(c => new
            {
                c.ClassId, ClassName = c.Name, c.CampusId, CampusName = c.Campus.Name, c.GradeLevel,
                Students = c.Enrollment.AsQueryable().Count(EffectivePeriod.EffectiveAt<Enrollment>(today)),
                HasHomeroom = c.HomeroomAssignment.AsQueryable().Any(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today)),
            })
            .ToListAsync();
        var classIds = classStats.Select(c => c.ClassId).ToList();

        var staffByCampus = await db.StaffProfile.AsNoTracking()
            .Where(s => s.IsActive && campusIds.Contains(s.PrimaryCampusId))
            .GroupBy(s => s.PrimaryCampusId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);

        var todayByCampus = await db.TeachingSession.AsNoTracking()
            .Where(s => classIds.Contains(s.ClassId) && s.Status == AttendanceRules.SessionScheduled && s.SessionDate == today)
            .GroupBy(s => s.Class.CampusId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);

        var leaders = (await db.CampusLeadership.AsNoTracking()
                .Where(EffectivePeriod.CurrentResponsibility<CampusLeadership>(today))
                .Where(l => campusIds.Contains(l.CampusId))
                .Select(l => new { l.CampusId, l.Staff.Staff.FullName }).ToListAsync())
            .GroupBy(l => l.CampusId).ToDictionary(g => g.Key, g => g.Select(x => x.FullName).Order().ToList());

        var days = await AttendanceSupport.DaysAsync(db, classIds, trendFrom, today);
        var recent = days.Where(d => d.Date > today.AddDays(-AttendanceDays)).ToList();
        var unmarkedByClass = await AttendanceSupport.UnmarkedByClassAsync(db, classIds, today.AddDays(-AttendanceDays + 1), today);
        var todayMarked = await db.TeachingSession.AsNoTracking()
            .Where(s => classIds.Contains(s.ClassId) && s.Status == AttendanceRules.SessionScheduled && s.SessionDate == today)
            .Select(s => s.AttendanceRecord.Any()).ToListAsync();
        var classCampus = classStats.ToDictionary(c => c.ClassId, c => c.CampusId);
        var classGrade = classStats.ToDictionary(c => c.ClassId, c => c.GradeLevel);

        var rows = campuses.Select(c =>
        {
            var cs = classStats.Where(x => x.CampusId == c.CampusId).ToList();
            return new DashboardCampusRow(c.CampusId, c.Name, cs.Count, cs.Sum(x => x.Students), staffByCampus.GetValueOrDefault(c.CampusId),
                todayByCampus.GetValueOrDefault(c.CampusId), leaders.GetValueOrDefault(c.CampusId) ?? [],
                AttendanceSupport.Rate(recent.Where(d => classCampus[d.ClassId] == c.CampusId)));
        }).ToList();

        var attention = await AttentionAsync(campusIds, classIds, today, classStats.Count(c => !c.HasHomeroom));
        var byGrade = DashboardSupport.ByGrade(classStats, x => x.GradeLevel, x => x.Students,
            g => AttendanceSupport.Rate(recent.Where(d => classGrade[d.ClassId] == g))).ToList();
        var attendance = AttendanceSupport.Summary(recent, unmarkedByClass.Values.Sum(), todayMarked.Count, todayMarked.Count(m => m));

        var homerooms = (await db.HomeroomAssignment.AsNoTracking()
                .Where(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today))
                .Where(h => classIds.Contains(h.ClassId))
                .Select(h => new { h.ClassId, h.Staff.Staff.FullName }).ToListAsync())
            .GroupBy(h => h.ClassId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.FullName).Order()));
        var classRows = classStats.Select(c => new DashboardClassRow(c.ClassId, c.ClassName, c.GradeLevel, c.CampusId, c.CampusName,
                homerooms.GetValueOrDefault(c.ClassId), c.Students, AttendanceSupport.Rate(recent.Where(d => d.ClassId == c.ClassId)),
                unmarkedByClass.GetValueOrDefault(c.ClassId)))
            .OrderBy(r => r.AttendanceRate ?? 101).ThenByDescending(r => r.UnmarkedSessions)
            .ThenBy(r => r.ClassName, StringComparer.CurrentCultureIgnoreCase).ToList();

        return new DashboardDto(campusId, term.YearName, term.SemesterName,
            campuses.Count, byGrade.Count, rows.Sum(r => r.ClassCount), rows.Sum(r => r.StudentCount), rows.Sum(r => r.StaffCount),
            rows.Sum(r => r.SessionsToday),
            attention, byGrade, rows, AttendanceSupport.Weeks(days, today),
            attendance, classRows, AttendanceSupport.Weekdays(recent), gradeOptions, gradeLevel);
    }

    private async Task<DashboardAttention> AttentionAsync(List<int> campusIds, List<int> classIds, DateOnly today, int classesWithoutHomeroom)
    {
        var pendingSessions = await db.TeachingSession.AsNoTracking()
            .Where(SessionLockRules.NotEnded(clock))
            .CountAsync(s => s.Status == "Pending" && classIds.Contains(s.ClassId));
        var absences = await db.TeacherAbsence.AsNoTracking()
            .Where(a => a.FromDate <= today && a.ToDate >= today && campusIds.Contains(a.Teacher.PrimaryCampusId))
            .Select(a => a.TeacherId).Distinct().CountAsync();
        int? pendingLeave = user.AccountType == AccountTypes.VicePrincipal
            ? await db.LeaveRequest.AsNoTracking().CountAsync(l => l.Status == "Pending" && classIds.Contains(l.ClassId))
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

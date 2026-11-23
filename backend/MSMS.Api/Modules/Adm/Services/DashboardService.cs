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
/// ADM-01 Tình hình toàn trường (Principal, lọc được phân hiệu) và Tình hình phân hiệu (Vice Principal, chỉ phân hiệu đang phụ trách). Lọc thêm được theo khối.
/// Lớp, học sinh, tiết, bài tập, điểm danh tính theo phân hiệu của lớp; thầy cô tính theo phân hiệu chính. Chỉ số liệu tổng hợp, không có tên học sinh.
/// Mọi chỉ số so kỳ 4 tuần tính tới hôm nay với 4 tuần liền trước.
/// </summary>
public sealed class DashboardService(MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock)
{
    public async Task<DashboardDto> GetAsync(int? campusId, int? gradeLevel = null)
    {
        var campusIds = await ResolveScopeAsync(campusId);
        var today = clock.Today;
        var now = clock.Now;
        var term = await DashboardSupport.CurrentTermAsync(db, today);
        var weekStarts = DashboardSupport.TrendWeekStarts(today);
        var (current, previous) = DashboardSupport.Periods(today);
        var currentFrom = current.ToDateTime(TimeOnly.MinValue);

        var campuses = await db.Campus.AsNoTracking().Where(c => campusIds.Contains(c.CampusId))
            .OrderBy(c => c.Name).Select(c => new { c.CampusId, c.Name }).ToListAsync();

        var inScope = db.Class.AsNoTracking()
            .Where(c => c.IsActive && c.AcademicYearId == term.YearId && campusIds.Contains(c.CampusId));
        var gradeOptions = await inScope.Select(c => (int)c.GradeLevel).Distinct().OrderBy(g => g).ToListAsync();
        var classQuery = gradeLevel is null ? inScope : inScope.Where(c => c.GradeLevel == gradeLevel);

        var classes = await classQuery
            .Select(c => new
            {
                c.ClassId, ClassName = c.Name, c.CampusId, CampusName = c.Campus.Name, GradeLevel = (int)c.GradeLevel,
                Students = c.Enrollment.AsQueryable().Count(EnrollmentRules.ActiveAt(today)),
                HasHomeroom = c.HomeroomAssignment.AsQueryable().Any(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today)),
            })
            .ToListAsync();
        var classIds = classes.Select(c => c.ClassId).ToList();
        var classCampus = classes.ToDictionary(c => c.ClassId, c => c.CampusId);
        var classGrade = classes.ToDictionary(c => c.ClassId, c => c.GradeLevel);

        var days = await AttendanceSupport.DaysAsync(db, classIds, DashboardSupport.ReadFrom(today), today);
        var recent = days.Where(d => d.Date >= current).ToList();
        var streaks = await AttendanceSupport.StreaksAsync(db, classIds, today);
        var unmarked = await AttendanceSupport.UnmarkedByClassAsync(db, classIds, current, today);
        var uncovered = classIds.Count == 0 ? 0 : await db.TeachingSession.AsNoTracking()
            .Where(SessionLockRules.NotEnded(clock))
            .CountAsync(s => s.Status == OperationsSupport.Outcomes.Pending && classIds.Contains(s.ClassId));
        var affected = await OperationsSupport.AffectedAsync(db, classIds, DashboardSupport.ReadFrom(today), today);
        var assignments = await OperationsSupport.AssignmentsAsync(db, classIds, DashboardSupport.ReadFrom(today).ToDateTime(TimeOnly.MinValue));

        var homerooms = classIds.Count == 0 ? [] : (await db.HomeroomAssignment.AsNoTracking()
                .Where(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today))
                .Where(h => classIds.Contains(h.ClassId))
                .Select(h => new { h.ClassId, h.Staff.Staff.FullName }).ToListAsync())
            .GroupBy(h => h.ClassId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.FullName).Order()));

        // Chuyên cần 4 tuần theo phân hiệu và theo khối: chỉ nhóm có học sinh.
        var campusRates = campuses.Where(c => classes.Any(x => x.CampusId == c.CampusId && x.Students > 0))
            .Select(c => new RateRow(c.CampusId, c.Name, AttendanceSupport.Rate(recent.Where(d => classCampus[d.ClassId] == c.CampusId))))
            .ToList();
        var gradeRates = classes.Where(c => c.Students > 0).Select(c => c.GradeLevel).Distinct().Order()
            .Select(g => new RateRow(g, $"Khối {g}", AttendanceSupport.Rate(recent.Where(d => classGrade[d.ClassId] == g)))).ToList();

        var streakByClass = streaks.Values.GroupBy(s => s.ClassId).ToDictionary(g => g.Key, g => g.Count());
        var classRows = classes.Select(c =>
            {
                var cd = days.Where(d => d.ClassId == c.ClassId).ToList();
                var cur = cd.Where(d => d.Date >= current).ToList();
                return new LeadershipClassRow(c.ClassId, c.ClassName, c.GradeLevel, c.CampusName, homerooms.GetValueOrDefault(c.ClassId), c.Students,
                    AttendanceSupport.Rate(cur), AttendanceSupport.Rate(cd.Where(d => d.Date >= previous && d.Date < current)),
                    AttendanceSupport.UnexcusedRate(cur), streakByClass.GetValueOrDefault(c.ClassId), unmarked.GetValueOrDefault(c.ClassId),
                    OperationsSupport.Submission(assignments.Where(a => a.ClassId == c.ClassId), currentFrom, now).Rate);
            })
            .OrderBy(r => r.Rate ?? 101).ThenByDescending(r => r.StreakStudents)
            .ThenBy(r => r.ClassName, StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), true))
            .ToList();

        // Thầy cô trong phạm vi: theo phân hiệu chính.
        var staff = await db.StaffProfile.AsNoTracking()
            .Where(s => s.IsActive && campusIds.Contains(s.PrimaryCampusId)
                && s.Staff.Account != null && s.Staff.Account.AccountType == AccountTypes.Teacher)
            .Select(s => new { s.StaffId, s.Staff.FullName, s.DepartmentId, DepartmentName = s.Department != null ? s.Department.Name : null })
            .ToListAsync();
        var staffIds = staff.Select(s => s.StaffId).ToList();
        var absentDays = await OperationsSupport.AbsentDaysAsync(db, staffIds, current, today);
        var unmarkedByTeacher = await OperationsSupport.UnmarkedByTeacherAsync(db, classIds, current, today);
        var unmarkedByDept = await OperationsSupport.UnmarkedByDepartmentAsync(db, classIds, current, today);

        var departments = await db.SubjectDepartment.AsNoTracking().Where(d => d.IsActive)
            .OrderBy(d => d.Name).Select(d => new { d.DepartmentId, d.Name }).ToListAsync();
        var departmentRows = departments.Select(d =>
            {
                var members = staff.Where(s => s.DepartmentId == d.DepartmentId).ToList();
                var mine = assignments.Where(a => a.DepartmentId == d.DepartmentId).ToList();
                return new LeadershipDepartmentRow(d.DepartmentId, d.Name, members.Count,
                    members.Count(m => absentDays.GetValueOrDefault(m.StaffId) > 0),
                    affected.Count(a => a.DepartmentId == d.DepartmentId && a.Row.Date >= current),
                    unmarkedByDept.GetValueOrDefault(d.DepartmentId),
                    mine.Count(a => a.OpenAt >= currentFrom && a.OpenAt <= now),
                    OperationsSupport.Submission(mine, currentFrom, now).Rate);
            })
            .Where(r => r.TeacherCount > 0 || r.AffectedSessions > 0 || r.AssignmentCount > 0)
            .ToList();

        var teacherRows = staff
            .Select(s => new LeadershipTeacherRow(s.StaffId, s.FullName, s.DepartmentName, absentDays.GetValueOrDefault(s.StaffId), unmarkedByTeacher.GetValueOrDefault(s.StaffId)))
            .Where(t => t.AbsentDays > 0 || t.UnmarkedSessions > 0)
            .OrderByDescending(t => t.AbsentDays + t.UnmarkedSessions).ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var alerts = new LeadershipAlerts(streaks.Count, uncovered, unmarked.Values.Sum(),
            user.AccountType == AccountTypes.Principal ? classes.Count(c => !c.HasHomeroom) : null);

        var submissionGrades = assignments.Select(a => a.GradeLevel).Distinct().Order()
            .Select(g => new RateRow(g, $"Khối {g}", OperationsSupport.Submission(assignments.Where(a => a.GradeLevel == g), currentFrom, now).Rate))
            .Where(r => r.Rate is not null).ToList();

        return new DashboardDto(campusId, DashboardSupport.WeekNo(term, today), weekStarts, campuses.Count, classes.Count, classes.Sum(c => c.Students),
            alerts,
            AttendanceSupport.Kpi(days, today, weekStarts, AttendanceSupport.Rate),
            AttendanceSupport.Kpi(days, today, weekStarts, AttendanceSupport.UnexcusedRate),
            OperationsSupport.AffectedKpi(affected, today, weekStarts),
            OperationsSupport.SubmissionKpi(assignments, today, now, weekStarts),
            AttendanceSupport.Mix(recent),
            campusRates, gradeRates, AttendanceSupport.Weekdays(recent), submissionGrades, OperationsSupport.Summary(assignments, today, now),
            affected.Where(a => a.Row.Date == today).Select(a => a.Row).ToList(),
            classRows, departmentRows, teacherRows, gradeOptions, gradeLevel);
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

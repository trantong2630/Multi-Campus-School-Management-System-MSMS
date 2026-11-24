using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>
/// Tình hình lớp chủ nhiệm: sĩ số hôm nay, chuyên cần của lớp so với khối, đơn nghỉ chờ duyệt và nộp bài rèn luyện theo môn.
/// Chỉ số liệu tổng hợp, không có bài nộp hay điểm của từng em. Quyền tính theo trách nhiệm chủ nhiệm còn hiệu lực hôm nay (BR-09).
/// </summary>
public sealed class HomeroomOverviewService(MsmsDbContext db, CurrentUser user, IClock clock)
{
    public async Task<HomeroomOverviewDto> GetAsync(int? classId)
    {
        var today = clock.Today;
        var now = clock.Now;
        var scope = await new AccountScopeResolver(db, clock).ResolveAsync(user.PersonId, user.AccountType);
        if (scope.HomeroomClassIds.Count == 0)
            throw ApiException.Forbidden("Chỉ giáo viên đang chủ nhiệm mới xem được màn hình này.");
        var id = classId ?? scope.HomeroomClassIds[0];
        if (!scope.HomeroomClassIds.Contains(id))
            throw ApiException.Forbidden("Không có quyền xem lớp này. Bạn không phải chủ nhiệm của lớp.");

        var options = await db.Class.AsNoTracking().Where(c => scope.HomeroomClassIds.Contains(c.ClassId))
            .OrderBy(c => c.Name).Select(c => new NamedId(c.ClassId, c.Name)).ToListAsync();
        var cls = await db.Class.AsNoTracking().Where(c => c.ClassId == id)
            .Select(c => new { c.ClassId, c.Name, c.GradeLevel, c.AcademicYearId })
            .FirstOrDefaultAsync() ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        var term = await DashboardSupport.CurrentTermAsync(db, today);
        var weekStarts = DashboardSupport.TrendWeekStarts(today);
        var (current, _) = DashboardSupport.Periods(today);
        var currentFrom = current.ToDateTime(TimeOnly.MinValue);

        var studentCount = await db.Enrollment.AsNoTracking().Where(EnrollmentRules.ActiveAsOf(today)).CountAsync(e => e.ClassId == id);
        var pendingLeave = await db.LeaveRequest.AsNoTracking().CountAsync(l => l.ClassId == id && l.Status == "Pending");
        var streaks = await AttendanceSupport.StreaksAsync(db, [id], today);

        var todayRows = await db.AttendanceRecord.AsNoTracking()
            .Where(r => r.Session.ClassId == id && r.Session.Status == AttendanceRules.SessionScheduled && r.Session.SessionDate == today)
            .Select(r => new { r.StudentId, r.Status }).ToListAsync();
        var todaySessions = await db.TeachingSession.AsNoTracking()
            .Where(s => s.ClassId == id && s.Status == AttendanceRules.SessionScheduled && s.SessionDate == today)
            .Select(s => s.AttendanceRecord.Any()).ToListAsync();
        int? presentToday = todayRows.Count == 0 ? null
            : todayRows.Where(r => r.Status != AttendanceStatuses.Absent).Select(r => r.StudentId).Distinct().Count();

        var gradeClassIds = await db.Class.AsNoTracking()
            .Where(c => c.IsActive && c.AcademicYearId == cls.AcademicYearId && c.GradeLevel == cls.GradeLevel)
            .Select(c => c.ClassId).ToListAsync();
        var gradeDays = await AttendanceSupport.DaysAsync(db, gradeClassIds, DashboardSupport.ReadFrom(today), today);
        var days = gradeDays.Where(d => d.ClassId == id).ToList();

        var assignments = await OperationsSupport.AssignmentsAsync(db, [id], DashboardSupport.ReadFrom(today).ToDateTime(TimeOnly.MinValue));
        var bySubject = assignments.GroupBy(a => (a.SubjectId, a.SubjectName))
            .Select(g =>
            {
                var (count, rate) = OperationsSupport.Submission(g, currentFrom, now);
                return new SubjectSubmissionRow(g.Key.SubjectId, g.Key.SubjectName, count, rate);
            })
            .Where(r => r.AssignmentCount > 0)
            .OrderBy(r => r.Rate ?? 101).ThenBy(r => r.SubjectName, StringComparer.CurrentCultureIgnoreCase).ToList();

        return new HomeroomOverviewDto(cls.ClassId, cls.Name, cls.GradeLevel, DashboardSupport.WeekNo(term, today), options,
            studentCount, presentToday, todaySessions.Count, todaySessions.Count(m => m), pendingLeave, streaks.Count,
            AttendanceSupport.Kpi(days, today, weekStarts, AttendanceSupport.Rate),
            AttendanceSupport.Rate(gradeDays.Where(d => d.Date >= current)),
            OperationsSupport.SubmissionKpi(assignments, today, now, weekStarts), AttendanceSupport.Mix(days.Where(d => d.Date >= current)),
            weekStarts, AttendanceSupport.Weekly(gradeDays, weekStarts, AttendanceSupport.Rate),
            OperationsSupport.Summary(assignments, today, now), bySubject);
    }
}

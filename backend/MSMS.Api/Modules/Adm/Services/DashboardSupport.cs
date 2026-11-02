using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>Một bài tập rút gọn để tổng hợp: số học sinh được giao và số đã nộp.</summary>
internal sealed record AssignmentFact(
    int AssignmentId, int SubjectId, string SubjectName, int DepartmentId, int ClassId, int CampusId,
    DateTime OpenAt, DateTime DueAt, int Recipients, int Submitted);

internal sealed record CurrentTerm(int YearId, string? YearName, int? SemesterId, string? SemesterName, DateOnly? SemesterStart, DateOnly? SemesterEnd);

/// <summary>Phép tính dùng chung của các dashboard. Chỉ đọc và tổng hợp, không lưu số liệu suy ra.</summary>
internal static class DashboardSupport
{
    public static async Task<CurrentTerm> CurrentTermAsync(MsmsDbContext db, DateOnly today)
    {
        var year = await db.AcademicYear.AsNoTracking().Where(y => y.IsCurrent)
            .Select(y => new { y.AcademicYearId, y.Name }).FirstOrDefaultAsync();
        if (year is null) return new CurrentTerm(0, null, null, null, null, null);
        var sem = await db.Semester.AsNoTracking()
            .Where(s => s.AcademicYearId == year.AcademicYearId && s.StartDate <= today && s.EndDate >= today)
            .Select(s => new { s.SemesterId, s.Name, s.StartDate, s.EndDate }).FirstOrDefaultAsync();
        return new CurrentTerm(year.AcademicYearId, year.Name, sem?.SemesterId, sem?.Name, sem?.StartDate, sem?.EndDate);
    }

    /// <summary>Thứ Hai của tuần chứa ngày D.</summary>
    public static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public static Task<List<AssignmentFact>> FactsAsync(IQueryable<Assignment> query) =>
        query.AsNoTracking().Select(a => new AssignmentFact(
            a.AssignmentId, a.SubjectId, a.Subject.Name, a.Subject.DepartmentId, a.ClassId, a.Class.CampusId,
            a.OpenAt, a.DueAt, a.AssignmentRecipient.Count, a.Submission.Count(s => s.SubmittedAt != null))).ToListAsync();

    /// <summary>Tỷ lệ nộp trên các bài đã đóng (AssignmentRules.IsClosed). Null khi chưa có bài đóng nào có người nhận.</summary>
    public static double? Rate(IEnumerable<AssignmentFact> facts, DateTime now)
    {
        var closed = facts.Where(f => AssignmentRules.IsClosed(f.DueAt, now)).ToList();
        var recipients = closed.Sum(f => f.Recipients);
        return recipients == 0 ? null : Math.Round(closed.Sum(f => f.Submitted) * 100.0 / recipients, 1);
    }

    public static List<SubjectRate> BySubject(IEnumerable<AssignmentFact> facts, DateTime now) =>
        facts.GroupBy(f => (f.SubjectId, f.SubjectName))
            .Select(g => new SubjectRate(g.Key.SubjectId, g.Key.SubjectName, g.Count(), Rate(g, now)))
            .OrderBy(s => s.SubmissionRate ?? 101).ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Số tiết Đã xếp lịch và bài giao theo tuần, 8 tuần gần nhất tính cả tuần này.</summary>
    public static List<WeekPoint> Weeks(DateOnly today, IEnumerable<DateOnly> sessionDates, IReadOnlyList<AssignmentFact> facts, DateTime now)
    {
        var current = WeekStart(today);
        var sessionsByWeek = sessionDates.GroupBy(WeekStart).ToDictionary(g => g.Key, g => g.Count());
        return Enumerable.Range(0, 8).Select(i => current.AddDays(-7 * (7 - i))).Select(w =>
        {
            var opened = facts.Count(f => WeekStart(DateOnly.FromDateTime(f.OpenAt)) == w);
            var due = facts.Where(f => WeekStart(DateOnly.FromDateTime(f.DueAt)) == w);
            return new WeekPoint(w, sessionsByWeek.GetValueOrDefault(w), opened, Rate(due, now));
        }).ToList();
    }

    public static IEnumerable<GradeCount> ByGrade<T>(IEnumerable<T> classes, Func<T, int> grade, Func<T, int> students) =>
        classes.GroupBy(grade).OrderBy(g => g.Key).Select(g => new GradeCount(g.Key, g.Count(), g.Sum(students)));
}

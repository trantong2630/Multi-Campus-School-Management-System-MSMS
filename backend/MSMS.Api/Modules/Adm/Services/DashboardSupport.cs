using Microsoft.EntityFrameworkCore;
using MSMS.Api.Data;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

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

    public static IEnumerable<GradeCount> ByGrade<T>(IEnumerable<T> classes, Func<T, int> grade, Func<T, int> students, Func<int, double?>? attendance = null) =>
        classes.GroupBy(grade).OrderBy(g => g.Key).Select(g => new GradeCount(g.Key, g.Count(), g.Sum(students), attendance?.Invoke(g.Key)));
}
